# Demo de cierre — Sprint 0 (5 minutos)

Guion para grabar la demo del cierre de sprint: **levantar local → login → shell en claro y oscuro → job de
prueba con traza de consumo LLM real**. Todo corre en la máquina, sin nube.

## Antes de grabar (5 minutos, fuera de cámara)

1. Docker levantado y el certificado de desarrollo confiable: `dotnet dev-certs https --trust`.
2. Configuración local del backend, ignorada por git — copia `Presentation/SoftwareFactory.Api/appsettings.Local.example.json`
   a `appsettings.Local.json` y completa:
   - `ConnectionStrings:softwarefactory-admin`, `Database:AppRolePassword`, `Seed:AdminPassword`,
   - `Jwt:SigningKeys:0:Secret` (32+ caracteres),
   - `Llm:Providers:anthropic:ApiKey` **o**, para no gastar saldo, un proveedor local:
     ```bash
     docker run -d --rm --name sf-ollama -p 11434:11434 ollama/ollama
     docker exec sf-ollama ollama pull qwen2.5:0.5b
     ```
     y en `appsettings.Local.json` del **Api y del AgentRuntime**:
     ```json
     "Llm": {
       "Tiers": { "Small": { "Provider": "local", "Model": "qwen2.5:0.5b", "MaxOutputTokens": 256 } },
       "Models": { "qwen2.5:0.5b": { "InputPerMillion": 1, "OutputPerMillion": 5 } },
       "Providers": { "local": { "Kind": "OpenAiCompatible", "BaseUrl": "http://localhost:11434/v1" } }
     }
     ```
3. `AgentRuntime` con su `appsettings.Local.json` (misma conexión y `Database:AppRolePassword` que el Api).
4. Terminal con fuente grande, navegador en ventana limpia, y esta lista a la vista.

## Minuto 0:00 — 0:45 · Levantar la plataforma

```bash
cd ~/source/FabricaSoftware
./start.sh
```

Se ve: Postgres y MinIO por compose, el Api en `https://localhost:7160` y el SPA en `http://localhost:5173`.
Decir en voz alta: **el Api migra la base, aprovisiona el rol de aplicación y siembra el tenant «local»** la
primera vez. En otra terminal, el worker:

```bash
cd ~/source/FabricaSoftware/Backend/FabricaSoftware-Backend
dotnet run --project Presentation/SoftwareFactory.AgentRuntime
```

Mostrar en el log: `Job worker started: polling every 00:00:02, lease 00:05:00`.

## Minuto 0:45 — 1:45 · Login real

1. Abrir `http://localhost:5173` → redirige a `/login`.
2. Entrar con `admin@local` y la contraseña de `Seed:AdminPassword`.
3. Señalar en las herramientas del navegador:
   - la respuesta de `/api/auth/login` **no** trae el refresh token,
   - la cookie `sf_refresh` es `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`,
   - `localStorage` está vacío: el access token vive en memoria.
4. Recargar la página: la sesión se restaura sola con la cookie (el front pide un refresh).

## Minuto 1:45 — 2:45 · El shell en claro y oscuro

1. Recorrer las cuatro secciones: Portafolio, Proyecto, Cerebro, Configuración; todas con su estado vacío y su
   acción sugerida.
2. Colapsar y expandir la barra lateral.
3. Cambiar el tema con el toggle de tres estados: **Claro → Oscuro → Sistema**. Señalar que no hay colores
   literales en el código: todo son tokens, y el lint rompe el build si alguien escribe `bg-white`.
4. Recargar en oscuro: el tema persiste y no hay parpadeo (se aplica antes del primer pintado).

## Minuto 2:45 — 4:15 · Job de prueba con consumo LLM real

En una terminal (el token sale del login anterior o de este comando):

```bash
TOKEN=$(curl -sk -H "Content-Type: application/json" \
  -d '{"email":"admin@local","password":"<Seed:AdminPassword>"}' \
  https://localhost:7160/api/auth/login | python3 -c "import json,sys;print(json.load(sys.stdin)['accessToken'])")

JOB=$(curl -sk -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"type":"probe","payload":"{\"steps\":4,\"delayMs\":700}"}' \
  https://localhost:7160/api/jobs | python3 -c "import json,sys;print(json.load(sys.stdin)['id'])")

curl -sk -N -H "Authorization: Bearer $TOKEN" "https://localhost:7160/api/jobs/$JOB/events"
```

Se ve el stream: `state` (pending) → varios `progress` con las fases *leyendo contexto → generando → validando*
→ `completed`. Decir: **el Api encoló, el worker lo tomó con `FOR UPDATE SKIP LOCKED` y el progreso viaja por
server-sent events**; si se cierra la terminal, el servidor suelta la conexión.

Para la traza de consumo, una llamada LLM real dentro de una corrida (el handler de agentes llega en S2, así que
aquí se usa la prueba de extremo a extremo que sí llama al modelo):

```bash
cd Backend/FabricaSoftware-Backend
SF_LLM_KIND=openai SF_LLM_BASE_URL=http://localhost:11434/v1 SF_LLM_MODEL=qwen2.5:0.5b \
  dotnet test Tests/SoftwareFactory.Infrastructure.Tests -p:RunSettingsFilePath= \
  --filter "Category=RealEndpoint" -l "console;verbosity=detailed" | grep -E "gateway →|SQL cost"
```

Se ve la latencia medida, el costo calculado y el costo de la corrida saliendo de SQL.

## Minuto 4:15 — 5:00 · La traza queda en la base

```bash
docker exec -it softwarefactory-postgres psql -U postgres -d softwarefactory -c \
  "SELECT provider, model, input_tokens, output_tokens, latency_ms, cost, job_id
     FROM llm_call ORDER BY created_at DESC LIMIT 5;"
```

Cerrar con las tres ideas del sprint:

1. **Aislamiento por tenant real**: RLS forzada en las 14 tablas, probada tabla por tabla; ni el rol de la
   aplicación puede saltársela.
2. **Costo medido desde el primer día**: toda llamada al modelo queda en `llm_call` con su job, y hay
   presupuesto duro por llamada y por corrida.
3. **Cambiar de modelo o de proveedor es configuración**, no código: el mismo gateway habla con Anthropic o con
   un modelo local.

## Después de grabar

```bash
docker rm -f sf-ollama            # si se usó el modelo local
# Ctrl+C en start.sh y en el AgentRuntime
```
