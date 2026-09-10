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

Los módulos funcionales (Traceability, Brain, Project, FunctionalDesign, Architecture, Testing, Construction, Finops) son namespaces dentro de Domain y Application y se comunican únicamente a través de `SoftwareFactory.Application.<Módulo>.Contracts`.

## Reglas de compilación

- `Directory.Build.props`: nullable, warnings-as-errors, analizadores .NET en modo `All`, estilo de código aplicado en build.
- `Directory.Packages.props`: gestión centralizada de versiones de paquetes.
- `.editorconfig`: convenciones estrictas; las excepciones por carpeta viven en `Presentation/.editorconfig` y `Tests/.editorconfig`.
- Los tests de arquitectura (`Tests/SoftwareFactory.Api.Tests/Architecture`) verifican la regla de dependencias entre capas, el aislamiento entre módulos y la composición del Api: solo `Program` y el namespace `SoftwareFactory.Api.Composition` (registro de DI) pueden ver Infrastructure; los controllers (`SoftwareFactory.Api.Controllers`) solo dependen de Application.

## Comandos

```bash
dotnet build SoftwareFactory.sln -c Release
dotnet test SoftwareFactory.sln -c Release
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
dotnet run --project Presentation/SoftwareFactory.Api --launch-profile https
```

**Opción C: `./start.sh` en la raíz del workspace**, que hace la opción B y además arranca el frontend (Vite en 5173).

Puertos fijos: Api https 7160 (http 5160), AgentRuntime http 5170, Postgres 5432, MinIO API 9000 y consola 9001. El `start.sh` del workspace y el proxy del frontend dependen del 7160.

Salud: `GET /health` en Api y AgentRuntime; `pg_isready` en Postgres; `mc ready local` en MinIO.
