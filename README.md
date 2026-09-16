# FabricaSoftware-Backend

[![ci](https://github.com/jvalverde-arch/FabricaSoftware-Backend/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/jvalverde-arch/FabricaSoftware-Backend/actions/workflows/ci.yml)

Backend de la fábrica de software agentizada: .NET 10, Clean Architecture, solución `SoftwareFactory.sln` en la raíz.

## Estructura

| Carpeta | Proyecto | Responsabilidad |
|---|---|---|
| `Domain/` | `SoftwareFactory.Domain` | Entidades, enums, eventos de dominio e interfaces de repositorio. No referencia a nadie. |
| `Application/` | `SoftwareFactory.Application` | Servicios (IService/Service), DTOs, validadores y contratos públicos de módulos. Referencia solo Domain. |
| `Infrastructure/` | `SoftwareFactory.Infrastructure` | EF Core, repositorios y adaptadores externos (LLM, blob, vault, cola). |
| `Presentation/` | `SoftwareFactory.Api` | Host web: controllers, request/response, auth, middleware. Raíz de composición (DI). |
| `Presentation/` | `SoftwareFactory.AgentRuntime` | Host worker: consume la cola de jobs (T-007) con los mismos servicios de Application que usa el Api. Raíz de composición (DI). Expone solo `/health`. |
| `Presentation/` | `SoftwareFactory.AppHost` | Orquestación local con Aspire: Postgres 17 + pgvector, MinIO, Api y AgentRuntime. No se despliega. |
| `infra/` | `docker-compose.yml` | Postgres + MinIO para quien no usa Aspire. Credenciales en `infra/.env` (ignorado), plantilla en `infra/.env.example`. |
| `Tests/` | `SoftwareFactory.*.Tests` | Pruebas por capa; `Api.Tests` contiene además los tests de arquitectura. |

Los módulos funcionales (Traceability, Brain, Project, FunctionalDesign, Architecture, Testing, Construction, Finops) más Platform (E1: tenants, usuarios, roles y cola de jobs) son namespaces dentro de Domain y Application y se comunican únicamente a través de `SoftwareFactory.Application.<Módulo>.Contracts`. `Common` es el núcleo compartido (entidad base, `AuthorType`, `Role`).

## Reglas de compilación

- `Directory.Build.props`: nullable, warnings-as-errors, analizadores .NET en modo `All` con `AnalysisLevel` **fijado** (`10.0-all`): con `latest-all` el conjunto de reglas cambiaba solo al cambiar de SDK y el mismo código compilaba aquí y fallaba dentro de la imagen. Subir el nivel es un cambio deliberado.
- `global.json`: SDK fijado a la banda `10.0.1xx` con `rollForward: latestPatch`, y los Dockerfiles usan la etiqueta `sdk:10.0.100` de esa misma banda. Cambiar de banda es un cambio consciente en los tres lugares a la vez.
- `Directory.Packages.props`: gestión centralizada de versiones de paquetes.
- `.editorconfig`: convenciones estrictas; las excepciones por carpeta viven en `Presentation/.editorconfig` y `Tests/.editorconfig`.
- Los tests de arquitectura (`Tests/SoftwareFactory.Api.Tests/Architecture`) verifican la regla de dependencias entre capas, el aislamiento entre módulos y la composición del Api: solo `Program` y el namespace `SoftwareFactory.Api.Composition` (registro de DI) pueden ver Infrastructure; los controllers (`SoftwareFactory.Api.Controllers`) solo dependen de Application.

## Comandos

```bash
dotnet build SoftwareFactory.sln -c Release
dotnet test SoftwareFactory.sln -c Release      # las pruebas de integración levantan Postgres con Testcontainers (requiere Docker)
```

## Base de datos

- **Esquema**: 14 tablas en snake_case singular (`tenant`, `app_user`, `user_role`, `project`, `artifact`, `artifact_version`, `relation`, `decision`, `job`, `llm_call`, `source_document`, `chunk`, `refresh_token`, `audit_event`), PK `uuid` v7 generada en la app, `timestamptz`, enums como `text` con check constraint, JSONB para contenido flexible, `vector(1536)` con índice HNSW en `chunk`, `xmin` como token de concurrencia optimista. `audit_event` es append-only: el rol de aplicación solo tiene `INSERT` y `SELECT`.
- **Aislamiento por tenant**: todas las tablas tienen `ENABLE` + `FORCE ROW LEVEL SECURITY` con la política `tenant_isolation` sobre `tenant_id` (la tabla `tenant` sobre `id`). La sesión declara su tenant con `set_config('app.tenant_id', ...)`; lo hace un interceptor de EF al abrir cada conexión a partir de `ITenantContext`. Sin tenant no se ve ninguna fila, con una única excepción: la política `login_lookup` de `app_user` deja leer, a una sesión sin tenant, solo las filas cuyo email coincide con `app.login_email` (fijado con `set_config(..., true)` dentro de la transacción del lookup de inicio de sesión). Así el login resuelve el tenant del usuario sin abrir la tabla.
- **Roles**: la migración crea el rol de grupo `softwarefactory_app` (sin login, sin `BYPASSRLS`) con los grants. La aplicación se conecta con un rol de login miembro de ese grupo (`Database:AppRoleName`, por defecto `softwarefactory_app_user`); el superusuario solo migra y siembra.
- **Conexiones**: `ConnectionStrings:softwarefactory-admin` (propietario; solo Development) y `ConnectionStrings:softwarefactory` (aplicación). Si la segunda no se configura, se deriva de la primera con las credenciales del rol de aplicación.
- **Arranque en Development**: el Api migra, aprovisiona el rol de login (contraseña `Database:AppRolePassword`, o generada por corrida) y siembra el tenant «local» con el admin `admin@local` si `Seed:AdminPassword` está configurado. Aspire inyecta todo esto solo; sin Aspire, copia `Presentation/SoftwareFactory.Api/appsettings.Local.example.json` a `appsettings.Local.json` (ignorado por git) o usa `dotnet user-secrets`.
- **Contraseñas**: Argon2id en formato PHC (`$argon2id$v=19$m=...,t=...,p=...$salt$hash`), parámetros en la sección `Argon2` de `appsettings.json`; la verificación lee los parámetros del propio hash.
- **Migraciones** (herramienta local `dotnet-ef` en el manifiesto del repo):

```bash
dotnet tool restore
dotnet build Infrastructure/SoftwareFactory.Infrastructure -c Release
dotnet ef migrations add <Nombre> --project Infrastructure/SoftwareFactory.Infrastructure --startup-project Infrastructure/SoftwareFactory.Infrastructure --output-dir Persistence/Migrations --no-build --configuration Release
dotnet ef migrations script --project Infrastructure/SoftwareFactory.Infrastructure --startup-project Infrastructure/SoftwareFactory.Infrastructure --no-build --configuration Release --idempotent
```

## Autenticación y autorización (T-004, `estandar-auth.md`)

- **Esquema**: ASP.NET Core Identity Core sobre `app_user` (store propio, sin tablas de Identity) + JWT emitido por la plataforma. Access token HS256 de 15 minutos con claims `jti`, `sub`, `tenant_id`, `roles`, `name`, cabecera `kid` y ventana de varias claves (`Jwt:SigningKeys`; solo `Jwt:ActiveKeyId` firma). Refresh token opaco de 256 bits, guardado como hash SHA-256 con familia por sesión, rotación en cada uso, expiración deslizante de 14 días y revocación de la familia completa al detectar reuso, al cerrar sesión o al cambiar la contraseña.
- **Cookie**: el refresh token viaja solo en la cookie `sf_refresh` (`HttpOnly; Secure; SameSite=Strict; Path=/api/auth`) con el formato `<tenant id>.<secreto>`; el prefijo solo enruta la búsqueda bajo RLS, la autorización la da el hash.
- **Endpoints**: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/auth/change-password`, `GET /api/auth/me`. Todo lo demás exige token (policy de respaldo); solo login, refresh, logout y `/health` son anónimos. Los fallos de login, refresh y sesión responden un 401 genérico (`Credenciales inválidas.`) para no revelar si el email existe o si la cuenta está bloqueada.
- **Contraseñas**: Argon2id (sección `Argon2`), mínimo 12 caracteres (`Auth:MinimumPasswordLength`), lista embebida de contraseñas comunes (SecLists, MIT), sin reglas de complejidad. Lockout progresivo configurable en `Auth:Lockout` (`Threshold` 5, `BaseDuration` 1 minuto duplicando en cada bloque, `MaximumDuration` 1 día; el tope evita bloqueos indefinidos provocados por terceros). Si los parámetros de Argon2 cambian, el hash se regenera en el siguiente inicio de sesión sin invalidar sesiones.
- **Autorización**: una policy por rol con el nombre del rol (`admin`, `functional`, `architect`, `qa`, `compliance`, `reader`) declarada en el controller con `[Authorize(Policy = "...")]`. El tenant de la petición sale del claim `tenant_id` del token (middleware `TenantClaimMiddleware`), jamás del request.
- **Hardening**: rate limiting de ventana fija por dirección de cliente en todo `/api/auth` (`Auth:RateLimit`, 10 por minuto por defecto, 429 con ProblemDetails y `Retry-After`; en Development se eleva a 200 por minuto porque cada recarga del SPA y cada prueba e2e consumen una petición de refresh), CORS restringido a `Cors:AllowedOrigins` con credenciales, cabeceras `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy` y `Permissions-Policy`; HSTS fuera de Development. Errores en formato ProblemDetails (RFC 7807) con títulos en español; validación FluentValidation en español con detalle por campo (422).
- **Auditoría**: `audit_event` registra login exitoso y fallido, lockout, reuso de refresh, logout y cambio de contraseña con IP y user agent. Los fallos sin tenant resoluble (email desconocido, email presente en varios tenants) solo van al log estructurado.
- **Configuración**: `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenLifetime` en `appsettings.json`; `Jwt:ActiveKeyId` y `Jwt:SigningKeys[].KeyId` en `appsettings.Development.json`; el secreto (`Jwt:SigningKeys:0:Secret`, 32+ caracteres) lo inyecta Aspire (parámetro `jwt-signing-key`) o va en `appsettings.Local.json` / user-secrets. Sin secreto, el Api no arranca (`ValidateOnStart`).

Prueba rápida (con la infra levantada y el Api en 7160):

```bash
curl -sk -c cookies.txt -H "Content-Type: application/json" -d '{"email":"admin@local","password":"<Seed:AdminPassword>"}' https://localhost:7160/api/auth/login
curl -sk -H "Authorization: Bearer <accessToken>" https://localhost:7160/api/auth/me
curl -sk -b cookies.txt -c cookies.txt -X POST https://localhost:7160/api/auth/refresh
curl -sk -b cookies.txt -X POST https://localhost:7160/api/auth/logout -i
```

## Adaptador LLM e instrumentación (T-006, doc 03 D3 y §6)

- **Puerto propio**: `ILlmProvider` (Application) con dos adaptadores en Infrastructure — **Anthropic** sobre el SDK oficial (`Anthropic` 12.48.0, licencia MIT igual que sus transitivas; relevante porque viaja dentro de los contenedores self-hosted) y **OpenAI-compatible** sobre `HttpClient` plano, que cubre Azure OpenAI, vLLM y Ollama. Ningún agente ni servicio conoce al proveedor.
- **Selección por configuración**: `Llm:Tasks` mapea tarea → tier (`Large`, `Medium`, `Small`), `Llm:Tiers` mapea tier → proveedor + modelo + tope de salida, y `Llm:Models` lleva el precio por millón de tokens de entrada, salida, lectura de caché y escritura de caché. Una tarea no listada cae en `Llm:DefaultTier`. Cambiar de modelo o de proveedor es configuración, nunca código; el Api no arranca si la configuración es incoherente (`ValidateOnStart`).
- **Puerta única**: `ILlmGateway.CompleteAsync` resuelve el modelo, mide la latencia con un reloj monótono, calcula el costo y escribe una fila en `llm_call` (proveedor, modelo, tokens de entrada/salida/caché, latencia, costo, job asociado). El tenant sale del contexto de la petición, así que la traza queda aislada por RLS.
- **Presupuestos duros** (`Llm:Budget`): antes de llamar se estima el peor caso (entrada aproximada + todo el tope de salida). Si excede `MaxCostPerCall`, o si lo ya gastado por el job más ese peor caso excede `MaxCostPerJob`, se lanza `LlmBudgetExceededException` **sin gastar nada**. Si la respuesta real termina costando más de lo previsto (por ejemplo por escritura de caché), la llamada se registra primero y la excepción se lanza después: el dinero ya se gastó y la traza no se pierde.
- **Temperatura**: el adaptador Anthropic no la envía; los modelos posteriores a Opus 4.6 rechazan cualquier valor distinto de 1.0. El adaptador OpenAI-compatible sí la admite, porque los modelos locales la usan.
- **Claves**: `Llm:Providers:<nombre>:ApiKey` sale de vault o de configuración local ignorada por git (D9); nunca del repositorio. El adaptador no registra prompts ni respuestas.

Costo de una corrida (criterio de aceptación de T-006):

```sql
SELECT job_id,
       COUNT(*)                AS llamadas,
       SUM(input_tokens)       AS tokens_entrada,
       SUM(output_tokens)      AS tokens_salida,
       SUM(cache_read_tokens)  AS tokens_cache,
       SUM(cost)               AS costo
FROM llm_call
WHERE job_id = '<id del job>'
GROUP BY job_id;
```

Pruebas: las de contrato usan un `HttpMessageHandler` stub para ambos adaptadores. La prueba contra **endpoint real** está marcada `Category=RealEndpoint` y queda fuera de la corrida normal (`tests.runsettings`):

```bash
# Anthropic (consume saldo real)
SF_LLM_KIND=anthropic SF_LLM_MODEL=claude-haiku-4-5 SF_LLM_API_KEY=sk-ant-... \
  dotnet test Tests/SoftwareFactory.Infrastructure.Tests -p:RunSettingsFilePath= --filter "Category=RealEndpoint"

# OpenAI-compatible con un modelo local (sin costo):
docker run -d --rm --name sf-ollama -p 11434:11434 ollama/ollama
docker exec sf-ollama ollama pull qwen2.5:0.5b
SF_LLM_KIND=openai SF_LLM_BASE_URL=http://localhost:11434/v1 SF_LLM_MODEL=qwen2.5:0.5b \
  dotnet test Tests/SoftwareFactory.Infrastructure.Tests -p:RunSettingsFilePath= --filter "Category=RealEndpoint"
```

`-p:RunSettingsFilePath=` es necesario porque `tests.runsettings` excluye esa categoría en la corrida normal. Son dos pruebas: el adaptador contra el endpoint, y el recorrido completo (gateway → `llm_call` → consulta SQL de costo) contra Postgres de Testcontainers.

## Cola de jobs y progreso (T-007, doc 03 D6)

- **Tabla, no broker**: `job` (tipo, payload JSONB, estado, intentos, `available_at`, `locked_until`, fase y porcentaje). El worker vive en **AgentRuntime**; el Api solo encola y lee (`Jobs:Enabled` está en `false` en el Api y en `true` en el runtime).
- **Reclamo**: la función `app_claim_next_job` (`SECURITY DEFINER`, creada por la migración y ejecutable solo por el rol de aplicación) hace `SELECT ... FOR UPDATE SKIP LOCKED` **a través de tenants**, porque el worker no tiene tenant cuando sondea. Devuelve solo identificadores; en la misma transacción la sesión declara ese tenant, carga el job y lo pasa a `running` con un lease. Dos workers nunca se llevan el mismo job, y fuera de esa transacción la sesión sigue sin ver nada de ese tenant.
- **Ciclo de vida**: intentos contados, reintentos con backoff exponencial (`Jobs:RetryBaseDelay` duplicando hasta `Jobs:RetryMaxDelay`, `Jobs:MaxAttempts` en total), y recuperación de corridas huérfanas: si el worker muere, el lease (`Jobs:Lease`) vence y otro lo retoma. Un apagado ordenado libera el lease sin gastar intento.
- **Costo por corrida**: el worker fija el job actual en el scope, así que **toda llamada LLM dentro de la corrida queda en `llm_call` con su `job_id`** sin que el llamador tenga que acordarse; de ahí sale la consulta de costo de T-006.
- **Endpoints**: `POST /api/jobs` encola (202 con la ubicación del job), `GET /api/jobs/{id}` devuelve su estado, y `GET /api/jobs/{id}/events` transmite el progreso por **server-sent events**. La cola respeta RLS: una corrida de otro tenant devuelve 404.
- **El stream no deja conexiones colgadas**: se cierra al terminar la corrida, ante `RequestAborted` (el cliente se fue), y al llegar al tope `Jobs:Stream:MaxDuration`; manda un latido cada `Jobs:Stream:Heartbeat` para detectar clientes muertos, desactiva el buffering (`X-Accel-Buffering: no`, importante detrás del Nginx del paquete self-hosted) y solo emite eventos cuando la fila cambió. Un cliente que llega tarde recibe un único evento `completed`.

Prueba manual del criterio (con `./start.sh` o los dos hosts arriba):

```bash
TOKEN=$(curl -sk -H "Content-Type: application/json" \
  -d '{"email":"admin@local","password":"<Seed:AdminPassword>"}' \
  https://localhost:7160/api/auth/login | python3 -c "import json,sys;print(json.load(sys.stdin)['accessToken'])")

JOB=$(curl -sk -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"type":"probe","payload":"{\"steps\":4,\"delayMs\":700}"}' \
  https://localhost:7160/api/jobs | python3 -c "import json,sys;print(json.load(sys.stdin)['id'])")

curl -sk -N -H "Authorization: Bearer $TOKEN" "https://localhost:7160/api/jobs/$JOB/events"
```

Se ven los eventos `state`, varios `progress` con las fases del handler de prueba y un `completed` final.

## Observabilidad

Serilog en ambos hosts (`estandar-backend.md` §3), configurado desde la sección `Serilog` de `appsettings.json`. El log de petición del Api añade `TraceId` y, cuando hay token validado, `TenantId` y `UserId`. Las llamadas LLM registran tarea, tier, proveedor, modelo, tokens, latencia y costo — nunca el prompt ni la respuesta, ni claves ni credenciales.

## Integración continua (T-008)

`.github/workflows/ci.yml` corre en cada pull request y en cada push a `main`:

1. **Build y pruebas**: `dotnet build -c Release` (warnings como errores) y `dotnet test` completo. Las pruebas de integración levantan Postgres con Testcontainers usando el Docker del runner; las marcadas `Category=RealEndpoint` quedan fuera por `tests.runsettings` (necesitan credenciales y gastan dinero).
2. **Imágenes**: solo desde `main` y solo si las pruebas pasaron, publica a GHCR desde los Dockerfiles del repo:
   - `ghcr.io/jvalverde-arch/softwarefactory-api`
   - `ghcr.io/jvalverde-arch/softwarefactory-agentruntime`

   Cada imagen lleva tres etiquetas: `sha-<commit>` (inmutable, la que se despliega), `main` (móvil) y `0.1.<número de corrida>` (legible). Cuando haya versionado de producto, esa tercera pasa a ser la versión real.

Demo de cierre del sprint: [`docs/demo-sprint-0.md`](docs/demo-sprint-0.md).

## Arranque local

Requisitos: .NET SDK 10, Docker y el certificado de desarrollo confiable (`dotnet dev-certs https --trust`).

**Opción A: Aspire (todo junto, con dashboard).**

```bash
dotnet run --project Presentation/SoftwareFactory.AppHost --launch-profile https
```

Levanta Postgres (pgvector), MinIO, el Api en `https://localhost:7160` y el AgentRuntime en `http://localhost:5170`. Las contraseñas de Postgres y MinIO se generan y persisten en user-secrets del AppHost. El dashboard muestra la salud de los cuatro recursos.

**Opción B: compose + Api por separado.**

```bash
cp infra/.env.example infra/.env        # y ajusta las contraseñas
docker compose -f infra/docker-compose.yml up -d
# appsettings.Local.json con la conexión admin, Database:AppRolePassword, Seed:AdminPassword y Jwt:SigningKeys:0:Secret (ver appsettings.Local.example.json)
dotnet run --project Presentation/SoftwareFactory.Api --launch-profile https
```

**Opción C: `./start.sh` en la raíz del workspace**, que hace la opción B y además arranca el frontend (Vite en 5173).

Puertos fijos: Api https 7160 (http 5160), AgentRuntime http 5170, Postgres 5432, MinIO API 9000 y consola 9001. El `start.sh` del workspace y el proxy del frontend dependen del 7160.

Salud: `GET /health` en Api y AgentRuntime; `pg_isready` en Postgres; `mc ready local` en MinIO.
