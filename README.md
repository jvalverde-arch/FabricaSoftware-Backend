# FabricaSoftware-Backend

Backend de la fábrica de software agentizada: .NET 10, Clean Architecture, solución `SoftwareFactory.sln` en la raíz.

## Estructura

| Carpeta | Proyecto | Responsabilidad |
|---|---|---|
| `Domain/` | `SoftwareFactory.Domain` | Entidades, enums, eventos de dominio e interfaces de repositorio. No referencia a nadie. |
| `Application/` | `SoftwareFactory.Application` | Servicios (IService/Service), DTOs, validadores y contratos públicos de módulos. Referencia solo Domain. |
| `Infrastructure/` | `SoftwareFactory.Infrastructure` | EF Core, repositorios y adaptadores externos (LLM, blob, vault, cola). |
| `Presentation/` | `SoftwareFactory.Api` | Host web: controllers, request/response, auth, middleware. Raíz de composición (DI). |
| `Presentation/` | `SoftwareFactory.AgentRuntime` | Host worker (Microsoft Agent Framework): consumidor de jobs. Raíz de composición (DI). |
| `Tests/` | `SoftwareFactory.*.Tests` | Pruebas por capa; `Api.Tests` contiene además los tests de arquitectura. |

Los módulos funcionales (Traceability, Brain, Project, FunctionalDesign, Architecture, Testing, Construction, Finops) son namespaces dentro de Domain y Application y se comunican únicamente a través de `SoftwareFactory.Application.<Módulo>.Contracts`.

## Reglas de compilación

- `Directory.Build.props`: nullable, warnings-as-errors, analizadores .NET en modo `All`, estilo de código aplicado en build.
- `Directory.Packages.props`: gestión centralizada de versiones de paquetes.
- `.editorconfig`: convenciones estrictas; las excepciones por carpeta viven en `Presentation/.editorconfig` y `Tests/.editorconfig`.
- Los tests de arquitectura (`Tests/SoftwareFactory.Api.Tests/Architecture`) verifican la regla de dependencias entre capas y el aislamiento entre módulos.

## Comandos

```bash
dotnet build SoftwareFactory.sln -c Release
dotnet test SoftwareFactory.sln -c Release
```
