# 🤖 CEOAgent

> Backend SaaS multi-tenant en .NET para conversaciones empresariales asistidas por IA.

CEOAgent está pensado para que restaurantes puedan atender conversaciones de negocio por WhatsApp, procesar texto o audio, ejecutar herramientas aprobadas y sincronizar acciones con sistemas externos como Google Calendar. El diseño del MVP empieza por WhatsApp, pero el núcleo está preparado para sumar otros canales como Telegram, Instagram DM o web chat sin reescribir el motor conversacional.

---

## ✨ Qué construye este proyecto

CEOAgent recibe mensajes entrantes, identifica la empresa por el canal, persiste la conversación, ejecuta un agente basado en Microsoft Agent Framework, valida cualquier acción solicitada por el modelo y responde por el canal correspondiente.

Capacidades principales del MVP:

- 📲 WhatsApp Cloud API para mensajes entrantes y salientes.
- 🎙️ Soporte para notas de voz, transcripción y respuestas de audio.
- 🏢 Resolución multi-tenant por `(provider, provider_channel_id)`.
- 🧠 Agente de IA con perfil, prompt y modelo configurable por empresa.
- 🧰 Catálogo dinámico de herramientas habilitadas por empresa.
- 🛡️ Ejecución segura: el modelo LLM nunca ejecuta lógica directamente.
- 🗄️ Persistencia en PostgreSQL con filtros globales por `company_id`.
- 📦 Azure Storage Queues para trabajos en background.
- 🧾 Azure Blob Storage para media y adjuntos.
- 📈 Observabilidad con OpenTelemetry, ZLogger y Langfuse.
- 🧪 Tests API, integración y worker con TUnit.

---

## 🧱 Arquitectura

CEOAgent es un **modular monolith**. No es un sistema de microservicios. La API y el Worker corren como procesos separados, pero comparten modelo, base de datos y contratos internos.

```text
WhatsApp Cloud
  |
  v
CEOAgent.ApiService
  |  valida webhook, resuelve empresa, persiste mensaje y encola trabajo
  v
Azure Storage Queue
  |
  v
CEOAgent.Worker
  |  ejecuta agente, valida herramientas, llama adaptadores y responde
  v
PostgreSQL + Blob Storage + Integraciones externas
```

Principios arquitectónicos:

- 🧩 **Vertical Slice Architecture** para organizar casos de uso.
- 🏛️ **Ports and Adapters** para integraciones externas.
- 🧠 **Microsoft Agent Framework** para runtime de agentes/LLM.
- 🔐 **Tool handlers controlados** para cualquier efecto secundario.
- 🧬 **AOT-aware**, priorizando mantenibilidad sin bloquear Native AOT futuro.
- 🧭 **Options Pattern** con configuración fuertemente tipada y validación al iniciar.

---

## 📁 Estructura de la solución

| Proyecto                   | Rol                                                                                       |
| -------------------------- | ----------------------------------------------------------------------------------------- |
| `CEOAgent.AppHost`         | Orquestación local con .NET Aspire: API, Worker, PostgreSQL, queues, blobs y Key Vault.   |
| `CEOAgent.ApiService`      | Superficie HTTP con FastEndpoints, endpoints admin, middleware, errores y OpenAPI/Scalar. |
| `CEOAgent.Worker`          | Procesamiento background: jobs, agente, herramientas e integraciones.                     |
| `CEOAgent.ServiceDefaults` | Health checks, OpenTelemetry, service discovery y resiliencia base.                       |
| `CEOAgent.Application`     | Lógica de aplicación compartida y contratos internos de negocio.                          |
| `CEOAgent.Infrastructure`  | EF Core, entidades, DbContext, persistencia y configuración de infraestructura.           |
| `CEOAgent.Integrations`    | Puertos/contratos de integración. No contiene implementaciones.                           |
| `CEOAgent.Adapters`        | Implementaciones de puertos: WhatsApp, calendarios, proveedores AI, HTTP externo.         |
| `CEOAgent.Tools`           | Tool handlers nativos del MVP.                                                            |
| `CEOAgent.Shared`          | DTOs públicos de request/response y enums compartidos.                                    |
| `CEOAgent.Web`             | Proyecto web template; no es parte central del MVP backend por ahora.                     |
| `tests/*`                  | Tests API, integración y Worker.                                                          |

---

## 🔄 Flujo principal

1. 📥 WhatsApp envía un webhook al API.
2. 🔏 El API valida firma y payload.
3. 🏢 Se resuelve la empresa por canal, nunca por teléfono del cliente.
4. 👤 Se identifica o crea el customer dentro de esa empresa.
5. 💬 Se persiste el mensaje de forma idempotente.
6. 📬 Se encola un trabajo en Azure Storage Queue.
7. ⚙️ El Worker carga la conversación y ejecuta el agente.
8. 🧠 El agente produce respuesta o solicita una herramienta.
9. 🧰 El backend valida la herramienta contra el catálogo habilitado.
10. 📤 El Worker persiste resultados y envía la respuesta por WhatsApp.

---

## 🧰 Stack principal

| Tecnología                | Para qué se usa                                                  | Leer más                                                                                     |
| ------------------------- | ---------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| .NET                      | Plataforma base del backend.                                     | [Documentación .NET](https://learn.microsoft.com/en-us/dotnet/)                              |
| ASP.NET Core              | Hosting HTTP, middleware, health checks y pipeline web.          | [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/)                               |
| FastEndpoints             | Endpoints HTTP por slice, con menos ceremonia que controllers.   | [FastEndpoints Docs](https://fast-endpoints.com/docs/get-started)                            |
| Microsoft Agent Framework | Runtime de agentes IA y abstractions para modelos/herramientas.  | [Agent Framework Docs](https://learn.microsoft.com/en-us/agent-framework/)                   |
| .NET Aspire               | Orquestación local de API, Worker y dependencias.                | [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire)                               |
| Entity Framework Core     | ORM, DbContext único, migraciones y filtros multi-tenant.        | [EF Core](https://learn.microsoft.com/en-us/ef/core/)                                        |
| Npgsql                    | Provider PostgreSQL para EF Core.                                | [Npgsql EF Core](https://www.npgsql.org/efcore/)                                             |
| Azure Storage Queues      | Cola de jobs entre API y Worker.                                 | [Azure Queue Storage](https://learn.microsoft.com/en-us/azure/storage/queues/)               |
| Azure Blob Storage        | Almacenamiento de audios, TTS y media.                           | [Azure Blob Storage](https://learn.microsoft.com/en-us/azure/storage/blobs/)                 |
| Mediator                  | Dispatch in-process de comandos/queries con source generator.    | [Mediator GitHub](https://github.com/martinothamar/Mediator)                                 |
| FluentValidation          | Validación de requests y comandos.                               | [FluentValidation](https://docs.fluentvalidation.net/)                                       |
| Mapperly                  | Mapeo compile-time entre entidades, requests y DTOs.             | [Mapperly Docs](https://mapperly.riok.app/docs/)                                             |
| Refit                     | Clientes HTTP tipados para integraciones externas.               | [Refit GitHub](https://github.com/reactiveui/refit)                                          |
| Polly                     | Resiliencia en adaptadores HTTP propios.                         | [Polly](https://www.pollydocs.org/)                                                          |
| OpenTelemetry             | Trazas, métricas e instrumentación estándar.                     | [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)                           |
| Langfuse                  | Observabilidad de prompts, completions, tool calls y costos LLM. | [Langfuse Docs](https://langfuse.com/docs)                                                   |
| ZLogger                   | Logging estructurado de alto rendimiento.                        | [ZLogger GitHub](https://github.com/Cysharp/ZLogger)                                         |
| Scalar                    | UI de documentación OpenAPI en desarrollo.                       | [Scalar ASP.NET Core](https://github.com/scalar/scalar/tree/main/packages/scalar.aspnetcore) |
| TUnit                     | Framework de testing moderno para .NET.                          | [TUnit](https://tunit.dev/)                                                                  |
| Testcontainers            | Infraestructura real para tests de integración.                  | [Testcontainers for .NET](https://dotnet.testcontainers.org/)                                |

---

## 🛡️ Reglas importantes del proyecto

Estas reglas están detalladas en [AGENTS.md](./AGENTS.md), que es la fuente normativa del proyecto.

- ✅ Usar nombres descriptivos. Nada de `req`, `ct`, `ctx` en código escrito a mano.
- ✅ Usar DTOs de API en `CEOAgent.Shared`.
- ✅ Usar Mapperly por módulo, no un mapper global.
- ✅ Mapear request → entity con mapper cuando el mapeo sea no trivial o cruce tipos de frontera.
- ✅ Usar `Guid.CreateVersion7()` para identificadores.
- ✅ Usar `TimeProvider`, no `DateTime.Now`.
- ✅ Enforce multi-tenancy con `company_id` y filtros globales EF Core.
- ✅ El modelo no ejecuta acciones directamente; todo pasa por tool handlers validados.
- ✅ Los secretos reales no viven en base de datos ni en appsettings.

---

## ⚙️ Configuración

El proyecto usa **Options Pattern** con clases fuertemente tipadas y validación al iniciar.

Secciones relevantes:

- `Api`
  - CORS.
  - Rate limiting.
- `Persistence`
  - Modo PostgreSQL o InMemory para tests.
- `ServiceDefaults`
  - OTLP.
  - Langfuse.

Ejemplo de configuración local:

```json
{
  "Api": {
    "Cors": {
      "AllowedOrigins": []
    },
    "RateLimiting": {
      "AutoReplenishment": true,
      "PermitLimit": 120,
      "QueueLimit": 0,
      "WindowSeconds": 60
    }
  },
  "Persistence": {
    "UseInMemoryDatabase": false,
    "InMemoryDatabaseName": "CEOAgent"
  }
}
```

---

## 🚀 Desarrollo local

### Requisitos

- .NET SDK compatible con `net11.0`.
- Docker Desktop o runtime de contenedores compatible para Aspire.
- PowerShell, Windows Terminal o shell equivalente.

### Restaurar y compilar

```powershell
dotnet restore CEOAgent.slnx
dotnet build CEOAgent.slnx
```

### Ejecutar tests

```powershell
dotnet test CEOAgent.slnx
```

### Ejecutar con Aspire

```powershell
dotnet run --project CEOAgent.AppHost/CEOAgent.AppHost.csproj
```

El API expone:

```text
/health
```

En desarrollo, la referencia OpenAPI está disponible en:

```text
/scalar
```

---

## 🧪 Testing

El repo incluye:

- 🧩 `tests/Api.Tests`: endpoints, errores, mappers y contratos HTTP.
- 🗄️ `tests/Integration.Tests`: EF Core, modelo relacional, JSONB, aislamiento multi-tenant.
- ⚙️ `tests/Worker.Tests`: base para pruebas del Worker.

Comando recomendado:

```powershell
dotnet test CEOAgent.slnx --no-build
```

---

## 📊 Observabilidad

CEOAgent separa observabilidad general y observabilidad LLM:

- OpenTelemetry para trazas, métricas e instrumentación estándar.
- ZLogger para logs estructurados.
- Langfuse para trazas GenAI, prompts, tool calls, tokens, latencia y costos.

En producción, el contenido textual de prompts/completions debe estar deshabilitado por defecto para reducir exposición de PII.

---

## 🔐 Seguridad y multi-tenancy

- Cada tabla propiedad de una empresa contiene `company_id`.
- El teléfono del cliente no identifica la empresa.
- La empresa se resuelve por canal, por ejemplo WhatsApp `phone_number_id`.
- Los endpoints admin usan API key estática en el MVP.
- Los webhooks se autorizan por firma HMAC del proveedor.
- Las credenciales de proveedores se guardan como referencias, por ejemplo `kv://...`, nunca como secretos crudos.

---

## 🧭 Estado actual

El proyecto ya tiene la base de solución, proyectos principales, configuración fuerte, endpoints admin iniciales de Companies, mappers por módulo, persistencia EF Core, tests y reglas de ingeniería en [AGENTS.md](./AGENTS.md).

El foco inmediato sigue siendo completar el flujo MVP:

- Webhook WhatsApp.
- Persistencia conversacional.
- Queue processing.
- Agent runner.
- Tool handlers.
- Adaptadores reales.
- Integración Google Calendar.
- Observabilidad LLM end-to-end.

---

## 📚 Links útiles

- [.NET](https://learn.microsoft.com/en-us/dotnet/)
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/)
- [FastEndpoints](https://fast-endpoints.com/docs/get-started)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [PostgreSQL](https://www.postgresql.org/docs/)
- [Azure Storage](https://learn.microsoft.com/en-us/azure/storage/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [Langfuse](https://langfuse.com/docs)
- [Mapperly](https://mapperly.riok.app/docs/)
- [FluentValidation](https://docs.fluentvalidation.net/)
- [Refit](https://github.com/reactiveui/refit)
- [Polly](https://www.pollydocs.org/)
- [TUnit](https://tunit.dev/)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)

---

## 🤝 Cómo contribuir

Antes de tocar código:

1. Lee [AGENTS.md](./AGENTS.md).
2. Mantén los cambios pequeños y alineados con el slice correspondiente.
3. Usa nombres descriptivos.
4. Añade tests proporcionales al riesgo.
5. Ejecuta build y tests antes de cerrar el cambio.

### 📝 Mensajes de commit

Este repo usa un formato descriptivo basado en el historial del proyecto:

```text
Area/Subarea/tests/docs: [#issue] Resumen imperativo del cambio
```

Reglas:

- Usa las áreas tocadas como prefijo, separadas por `/`: `ApiService/Shared/tests/docs`.
- Usa `[#n]` para el issue o tarea relacionada.
- Usa `[#0]` cuando no exista issue asociado.
- Escribe el resumen en inglés, en modo imperativo: `Add`, `Fix`, `Move`, `Rename`, `Align`.
- No uses punto final.
- Mantén el mensaje concreto: qué cambió y en qué área.

Ejemplos:

```text
ApiService/Shared/tests/docs: [#0] Add typed config validation and Company response mappings
Infrastructure/ApiService/tests/docs: [#0] Add typed JSONB entity models and document manual migration policy
ApiService/AppHost/Infrastructure/Worker/tests/docs: [#6] Add MVP persistence, admin auth, tenant isolation, Aspire setup, and agent rules
```

```powershell
dotnet build CEOAgent.slnx
dotnet test CEOAgent.slnx --no-build
```
