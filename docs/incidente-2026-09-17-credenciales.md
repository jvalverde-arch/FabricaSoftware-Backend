# Incidente 2026-09-17 — credenciales dentro de las imágenes publicadas

## Qué pasó

Las primeras imágenes de `softwarefactory-api` y `softwarefactory-agentruntime` (T-008, publicadas en GHCR el
2026-09-16) llevaban dentro `/app/appsettings.Local.json`. El repo lo ignora en git, pero el `Dockerfile` hacía
`COPY . .` sin `.dockerignore`, así que el archivo entró al contexto de build y `dotnet publish` lo copió a la salida.

Dentro iban: contraseña del rol `postgres`, contraseña del rol de aplicación, contraseña del admin sembrado
(`admin@local`) y la clave de firma de los JWT (`kid` `local-1`).

Agravante: el host cargaba ese archivo **después** de las variables de entorno, así que además de viajar, pisaba la
configuración que inyecta el orquestador. Se descubrió al levantar la imagen contra el Postgres de compose y ver que
el worker intentaba conectarse a `localhost` pese a las variables del contenedor.

Son credenciales de desarrollo. Se rotan igual: el criterio se fija cuando no cuesta nada, no el día que la misma
cadena de errores ocurra contra un cliente self-hosted (modelo M3, que es exactamente a quien se le entregan estas
imágenes).

## Qué se corrigió

| Corrección | Dónde |
|---|---|
| `.dockerignore` en los dos repos | `.dockerignore` (backend y frontend) |
| Los archivos locales se cargan **antes** que las variables de entorno | `Presentation/Shared/HostConfiguration.cs`, enlazado en los dos hosts |
| El CI falla si la imagen construida contiene secretos | `.github/scripts/assert-no-secrets-in-image.sh` + paso previo al push en `ci.yml` de ambos repos |
| Prueba de composición del worker (`ValidateOnBuild`) | `Tests/SoftwareFactory.AgentRuntime.Tests` |

## Rotación (hecha el 2026-09-17)

| Credencial | Cómo se rotó | Verificación |
|---|---|---|
| Contraseña de `admin@local` | Por el propio flujo del producto: `POST /api/auth/login` + `POST /api/auth/change-password` | La vieja devuelve 401, la nueva 200 |
| Contraseña del rol `postgres` | `ALTER ROLE postgres WITH PASSWORD`, más `infra/.env` y los `appsettings.Local.json` | Desde otro contenedor: la vieja se rechaza, la nueva conecta |
| Contraseña del rol de aplicación | Nuevo valor en `Database:AppRolePassword`; el `DatabaseInitializer` la aplica al arrancar el Api | Desde otro contenedor: la vieja se rechaza, la nueva conecta |
| Clave de firma JWT | Clave nueva con `kid` **`local-2`**; `local-1` se retira por completo de la ventana de validación | El token emitido trae `kid: local-2`; los firmados con `local-1` ya no validan |

La verificación de las contraseñas de Postgres tiene que hacerse **desde otro contenedor**: el `pg_hba.conf` de la
imagen oficial confía (`trust`) en el loopback del propio contenedor, así que `docker exec psql` acepta cualquier
contraseña y no prueba nada.

Los parámetros que genera Aspire (user-secrets del AppHost) no estaban en la imagen y no se tocaron.

## Pendiente: borrar del registro las versiones contaminadas

Toda versión de `softwarefactory-api` y `softwarefactory-agentruntime` publicada **antes** del commit `47fa401`
(el que agrega el `.dockerignore`) contiene el archivo. Hay que borrarla del registro: mientras exista, la credencial
vieja sigue publicada aunque ya no sirva, y la imagen queda como ejemplo de lo que no se hace.

Requiere un PAT con `read:packages` y `delete:packages`; el agente no tiene credencial de GitHub, así que estos
comandos los corre Javier.

```bash
export GH_TOKEN=<PAT con read:packages y delete:packages>

# 1) Listar versiones con sus etiquetas y su fecha (repetir para softwarefactory-agentruntime)
curl -sS -H "Authorization: Bearer $GH_TOKEN" -H "Accept: application/vnd.github+json" \
  "https://api.github.com/user/packages/container/softwarefactory-api/versions?per_page=100" \
  | jq -r '.[] | [.id, .created_at, (.metadata.container.tags | join(","))] | @tsv'

# 2) Borrar cada versión anterior a la primera imagen limpia (por id, una a una)
curl -sS -X DELETE -H "Authorization: Bearer $GH_TOKEN" -H "Accept: application/vnd.github+json" \
  "https://api.github.com/user/packages/container/softwarefactory-api/versions/<ID>"
```

Comprobación de que lo republicado está limpio, antes de dar el incidente por cerrado:

```bash
docker pull ghcr.io/jvalverde-arch/softwarefactory-api:main
./.github/scripts/assert-no-secrets-in-image.sh ghcr.io/jvalverde-arch/softwarefactory-api:main
```

Las imágenes limpias las republica el propio CI en cada push a `main`, ya con el guardián por delante.
