# FabricaSoftware-Backend

Backend de la fábrica de software agentizada: .NET 10, Clean Architecture, solución `SoftwareFactory.sln` en la raíz.

## Estructura

| Carpeta | Proyecto | Responsabilidad |
|---|---|---|
| `Domain/` | `SoftwareFactory.Domain` | Entidades, enums, eventos de dominio e interfaces de repositorio. No referencia a nadie. |
| `Application/` | `SoftwareFactory.Application` | Servicios (IService/Service), DTOs, validadores y contratos públicos de módulos. Referencia solo Domain. |
| `Infrastructure/` | `SoftwareFactory.Infrastructure` | EF Core, repositorios y adaptadores externos (LLM, blob, vault, cola). |
| `Presentation/` | `SoftwareFactory.Api` | Host web: controllers, request/response, auth, middleware. Raíz de composición (DI). |
| `Presentation/` | `SoftwareFactory.AgentRuntime` | Host worker (Microsoft Agent Framework): consumidor de jobs. Raíz de composición (DI). Expone solo `/health`. |
| `Presentation/` | `SoftwareFactory.AppHost` | Orquestación local con Aspire: Postgres 17 + pgvector, MinIO, Api y AgentRuntime. No se despliega. |
| `infra/` | `docker-compose.yml` | Postgres + MinIO para quien no usa Aspire. Credenciales en `infra/.env` (ignorado), plantilla en `infra/.env.example`. |
| `Tests/` | `SoftwareFactory.*.Tests` | Pruebas por capa; `Api.Tests` contiene además los tests de arquitectura. |

Los módulos funcionales (Traceability, Brain, Project, FunctionalDesign, Architecture, Testing, Construction, Finops) más Platform (E1: tenants, usuarios, roles y cola de jobs) son namespaces dentro de Domain y Application y se comunican únicamente a través de `SoftwareFactory.Application.<Módulo>.Contracts`. `Common` es el núcleo compartido (entidad base, `AuthorType`, `Role`).

## Reglas de compilación

- `Directory.Build.props`: nullable, warnings-as-errors, analizadores .NET en modo `All`, estilo de código aplicado en build.
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
