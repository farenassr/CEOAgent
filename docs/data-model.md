# CeoAgent Data Model 📚

Guia explicativa del modelo de datos actual del backend. Este documento describe para que sirve cada tabla, que representa cada propiedad, ejemplos de valores reales y las decisiones importantes de diseño.

> Estado actual: el MVP gestiona reservas mediante herramientas de Google Calendar. No existe una tabla relacional de reservas; las reservas viven en Google Calendar y se auditan mediante `tool_execution`.

## Vista General 🧭

El modelo esta pensado para un backend SaaS multi-company. Cada compañia configura sus canales, su agente, sus credenciales externas y sus herramientas disponibles. A partir de ahi, el sistema registra clientes, conversaciones, mensajes y ejecuciones de herramientas.

La regla central es simple: casi todo lo que pertenece a una compañia lleva `CompanyId`. Ese campo permite aislar datos por compañia y aplicar filtros globales desde Entity Framework Core.

| Area                         | Tablas                                                                                            | Proposito                                                                                                                         |
| ---------------------------- | ------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| 🏢 Configuracion de compañia | `company`, `company_channel`, `agent_profile`, `company_tool`, `integration_credential_reference` | Define quien es la compañia, por donde habla, como se comporta su agente, que herramientas tiene y que credenciales externas usa. |
| 👤 Conversaciones            | `customer`, `conversation`, `conversation_state`, `message`                                       | Guarda identidades de clientes, conversaciones abiertas/cerradas, estado temporal y mensajes de texto/audio.                      |
| 🛠️ Ejecucion de herramientas | `tool_execution`                                                                                  | Audita cada accion solicitada por el agente y su resultado.                                                                       |

## Convenciones Del Modelo 🧱

### IDs

Todos los identificadores son `Guid` generados como GUID v7. Esto da IDs globalmente unicos y mejor orden temporal que un GUID aleatorio clasico.

Ejemplo:

```text
018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30
```

### Auditoria

Las tablas company-owned heredan los campos:

| Propiedad   | Para que sirve                                                                             | Ejemplo                                |
| ----------- | ------------------------------------------------------------------------------------------ | -------------------------------------- |
| `CompanyId` | Identifica la compañia propietaria del registro. Es la base del aislamiento multi-company. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `CreatedAt` | Fecha UTC en la que se creo el registro.                                                   | `2026-05-22T10:15:30Z`                 |
| `UpdatedAt` | Fecha UTC de la ultima modificacion.                                                       | `2026-05-22T10:45:00Z`                 |

`CreatedAt` y `UpdatedAt` los estampa `CeoAgentDbContext` automaticamente al guardar cambios.

### JSON

Algunas propiedades se guardan como `jsonb` en PostgreSQL. En C# se modelan como complex types tipados bajo `CeoAgent.Infrastructure.Entities.JsonDocuments`, no como strings crudos ni diccionarios dinamicos. EF Core/Npgsql los mapea con `ComplexProperty(...).ToJson(...)` para que el modelo conozca su estructura interna.

- `WorkingHours`
- `Metadata`
- `Configuration`
- `Snapshot`
- `Payload`
- `Request`
- `Result`

La idea es mantener flexibilidad sin crear tablas prematuras para datos que todavia no tienen reglas relacionales fuertes, pero evitando documentos opacos que EF no pueda validar o mapear.

Las columnas fisicas siguen usando nombres `snake_case` historicos como `working_hours_json`, `metadata_json`, `state_json`, `payload_json`, `request_json` y `result_json`.

### Enums con nombres externos

Algunos enums se exponen o persisten con nombres externos `snake_case`, no con el nombre C# del miembro. Esto mantiene estable el contrato de API, JSON y base de datos.

| Enum | Miembro C# | Nombre externo |
| ---- | ---------- | -------------- |
| `IntegrationProvider` | `WhatsAppCloud` | `whatsapp_cloud` |
| `IntegrationProvider` | `GoogleCalendar` | `google_calendar` |

`IntegrationCredentialReference.Provider` usa `IntegrationProvider` en C# y se guarda en PostgreSQL con el nombre externo.

### Tipos JSON

Estos objetos viven en `CeoAgent.Infrastructure.Entities.JsonDocuments` y se guardan embebidos dentro de columnas `jsonb`.

| Entidad | Propiedad C# | Columna | Tipo C# |
| ------- | ------------ | ------- | ------- |
| `Company` | `WorkingHours` | `working_hours_json` | `WorkingHours?` |
| `CompanyChannel` | `Metadata` | `metadata_json` | `ChannelMetadata?` |
| `CompanyTool` | `Configuration` | `configuration_json` | `ToolConfiguration?` |
| `ConversationState` | `Snapshot` | `state_json` | `ConversationStateSnapshot` |
| `IntegrationCredentialReference` | `Metadata` | `metadata_json` | `CredentialMetadata?` |
| `Message` | `Payload` | `payload_json` | `MessagePayload?` |
| `ToolExecution` | `Request` | `request_json` | `ToolExecutionRequest?` |
| `ToolExecution` | `Result` | `result_json` | `ToolExecutionResult?` |

En los diagramas, estos tipos aparecen como `CT_*` o cajas punteadas para distinguirlos de tablas reales. No tienen PK, FK ni filas propias; viven dentro del documento `jsonb` de la entidad dueña.

#### `WorkingHours`

| Campo | Tipo | Uso |
| ----- | ---- | --- |
| `Schedule` | `WeeklySchedule` | Horarios semanales con propiedades explicitas por dia. |
| `Holidays` | `List<SpecialDay>` | Fechas especificas que sobreescriben el horario normal. |

`WeeklySchedule` contiene `Monday`, `Tuesday`, `Wednesday`, `Thursday`, `Friday`, `Saturday` y `Sunday`, todos como `List<TimeSlot>`. `TimeSlot` contiene `Start : TimeOnly` y `End : TimeOnly`. `SpecialDay` contiene `Date : DateOnly`, `IsClosed : bool`, `TimeSlots : List<TimeSlot>` y `Reason : string?`.

#### `ChannelMetadata`

`ChannelMetadata` es un wrapper concreto. La variante activa se identifica por la propiedad no null:

| Tipo | Campos |
| ---- | ------ |
| `WhatsAppCloudMetadata` | `BusinessAccountId : string`, `PhoneNumberId : string`, `DisplayPhoneNumber : string?`, `VerifiedName : string?` |
| `InstagramMetadata` | `IgUserId : string`, `PageId : string?` |
| `TelegramMetadata` | `BotUsername : string`, `ChatId : long` |

#### `ToolConfiguration`

`ToolConfiguration` es un wrapper concreto. Siempre incluye `ToolKey : string` y la variante activa se guarda en una propiedad nullable:

| Tipo | Campos |
| ---- | ------ |
| `CheckAvailabilityConfig` | `MaxPartySize : int`, `MinPartySize : int`, `SlotMinutes : int`, `AdvanceBookingDays : int` |
| `RequestHumanHandoffConfig` | `EscalationChannel : string?`, `NotifyUsers : List<string>`, `TimeoutMinutes : int` |
| `GoogleCalendarConfig` | `CalendarId : string`, `TimeZoneId : string`, `BufferMinutes : int`, `ReservationMinutes : int`, `AdvanceBookingDays : int`, `SlotMinutes : int` |

#### `ConversationStateSnapshot`

| Campo | Tipo | Uso |
| ----- | ---- | --- |
| `CurrentIntent` | `string?` | Intencion activa detectada, por ejemplo `human_handoff_request`. |
| `PendingAction` | `string?` | Proximo paso o herramienta esperada. |
| `Slots` | `List<ConversationSlot>` | Valores parciales capturados de forma tipada. |
| `ConversationFlags` | `List<string>` | Flags de estado, como `awaiting_confirmation` o `human_requested`. |
| `TurnCount` | `int` | Conteo de turnos usados por el flujo actual. |

`ConversationSlot` contiene `Name : string`, `TextValue : string?`, `NumberValue : decimal?`, `BooleanValue : bool?`, `DateValue : DateOnly?` y `TimeValue : TimeOnly?`.

#### `CredentialMetadata`

`CredentialMetadata` es un wrapper concreto. Siempre incluye `Provider : string` con el nombre externo del `IntegrationProvider`; la variante activa se guarda en una propiedad nullable:

| Tipo | Campos |
| ---- | ------ |
| `GoogleCalendarCredentialMetadata` | `CalendarId : string`, `Scope : string`, `ExpiresAt : DateTimeOffset?` |
| `WhatsAppCloudCredentialMetadata` | `AppId : string`, `TokenVersion : string` |

#### `MessagePayload`

`MessagePayload` guarda metadatos variables del mensaje dentro de `payload_json`. El texto canonico siempre vive en `Message.MessageText`, ya sea texto normal, transcript STT o texto fuente TTS. Para el MVP solo existe la variante de audio.

| Tipo | Campos |
| ---- | ------ |
| `MessagePayload` | `ProviderType : string?`, `ProviderMessageId : string?` |

#### `ToolExecutionRequest`

`ToolExecutionRequest` es un wrapper concreto. Siempre incluye `ToolKey : string` y la variante activa se guarda en una propiedad nullable:

| Tipo | Campos |
| ---- | ------ |
| `CheckAvailabilityRequest` | `Date : DateOnly`, `PartySize : int`, `PreferredTime : TimeOnly?` |
| `RequestHumanHandoffRequest` | `Reason : string`, `Notes : string?` |
| `CreateCalendarEventRequest` | `Start : DateTimeOffset`, `End : DateTimeOffset`, `Summary : string` |

#### `ToolExecutionResult`

`ToolExecutionResult` es un wrapper concreto. Siempre incluye `ToolKey : string` y la variante activa se guarda en una propiedad nullable:

| Tipo | Campos |
| ---- | ------ |
| `CheckAvailabilityResult` | `Available : bool`, `AlternativeSlots : List<TimeOnly>`, `UnavailabilityReason : string?` |
| `RequestHumanHandoffResult` | `HandoffRequested : bool`, `HandoffTicketId : string?`, `EstimatedPickupAt : DateTimeOffset?` |
| `CreateCalendarEventResult` | `EventId : string`, `EventUrl : string` |

### Migraciones

Las migraciones se guardan en `CeoAgent.Infrastructure/Persistence/Migrations/`, pero no se crean ni se aplican automaticamente. Los agentes de IA no deben crear, eliminar, aplicar ni ejecutar migraciones de EF Core. La creacion y ejecucion de migraciones, incluido `dotnet ef database update`, es una decision manual del propietario del proyecto.

---

# Tablas

## 🏢 `company`

Representa una compañia que usa la plataforma. Es el tenant funcional del sistema: todo canal, cliente, conversacion, herramienta o integracion termina asociado a una `company`.

### Para que sirve

`company` define la identidad operativa y configuracion base de una empresa. Si manana el sistema atiende cinco restaurantes, cinco tiendas o cinco negocios, cada uno tendra una fila en esta tabla.

### Propiedades

| Propiedad          | Tipo                | Explicacion                                                                                                              | Ejemplo                                        |
| ------------------ | ------------------- | ------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------- |
| `Id`               | `Guid`              | Identificador unico de la compañia. Se usa como FK en casi todo el modelo.                                               | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30`         |
| `Name`             | `string`            | Nombre humano de la compañia. Sirve para administracion, logs y pantallas internas.                                      | `Contoso Bistro`                               |
| `WorkingHours`     | `WorkingHours?` / `jsonb` | Horarios de operacion o disponibilidad. No impone reglas por si solo; es configuracion para procesos de negocio futuros. | `{"schedule":{"monday":[{"start":"09:00","end":"18:00"}]}}` |
| `TimeZoneId`       | `string`            | Zona horaria IANA de la compañia. Es clave para interpretar fechas locales, horarios y mensajes.                         | `America/Bogota`                               |
| `Status`           | `CompanyStatus`     | Estado de vida de la compañia. Controla si esta activa o deshabilitada.                                                  | `Active`                                       |
| `CreatedAt`        | `DateTime`          | Momento UTC en que se creo la compañia.                                                                                  | `2026-05-22T10:15:30Z`                         |
| `UpdatedAt`        | `DateTime`          | Momento UTC de la ultima actualizacion.                                                                                  | `2026-05-22T10:45:00Z`                         |

### Relaciones

- Una `company` tiene muchos `company_channel`.
- Una `company` tiene un `agent_profile`.
- Una `company` tiene muchos `company_tool`.
- Una `company` tiene muchas credenciales externas.
- Una `company` posee clientes, conversaciones, mensajes, audios y ejecuciones de herramientas.

### Ejemplo mental

Si `Contoso Bistro` usa WhatsApp Cloud, su fila `company` guarda el nombre, zona horaria y estado. Luego `company_channel` dira cual es el `phone_number_id` de WhatsApp que pertenece a esa compañia.

---

## 📡 `company_channel`

Registra canales externos por los que una compañia recibe o envia mensajes. En el MVP puede ser WhatsApp Cloud, pero el diseno permite otros proveedores.

### Para que sirve

Cuando entra un webhook, el sistema no debe identificar la compañia por el numero del cliente. La compañia se resuelve por el canal receptor, por ejemplo el `phone_number_id` de WhatsApp Cloud. Esa asociacion vive aqui.

### Propiedades

| Propiedad             | Tipo                | Explicacion                                                                                      | Ejemplo                                |
| --------------------- | ------------------- | ------------------------------------------------------------------------------------------------ | -------------------------------------- |
| `Id`                  | `Guid`              | Identificador unico del canal registrado.                                                        | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31` |
| `CompanyId`           | `Guid`              | Compañia propietaria del canal.                                                                  | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `Provider`            | `string`            | Nombre tecnico del proveedor de canal.                                                           | `whatsapp_cloud`                       |
| `ProviderChannelId`   | `string`            | Identificador estable del canal dentro del proveedor. Para WhatsApp suele ser `phone_number_id`. | `123456789012345`                      |
| `Metadata`            | `ChannelMetadata?` / `jsonb` | Datos adicionales del proveedor.                                                                 | `{"whatsapp_cloud":{"business_account_id":"987654321","phone_number_id":"123456789012345"}}` |
| `CredentialReferenceId` | `Guid?`           | FK opcional a `integration_credential_reference`. Nullable porque algunos canales pueden no necesitar credencial local. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42` |
| `CreatedAt`           | `DateTime`          | Fecha UTC de creacion.                                                                           | `2026-05-22T10:15:30Z`                 |
| `UpdatedAt`           | `DateTime`          | Fecha UTC de ultima actualizacion.                                                               | `2026-05-22T10:45:00Z`                 |

### Reglas importantes

- `Provider + ProviderChannelId` es unico.
- El telefono del cliente no identifica la compañia.
- Las credenciales se enlazan por FK a `integration_credential_reference`; esa tabla guarda referencias logicas, no valores sensibles.

---

## 🤖 `agent_profile`

Define como debe comportarse el agente de IA para una compañia.

### Para que sirve

No todas las compañias hablan igual ni usan el mismo modelo. `agent_profile` guarda la configuracion conversacional base: modelo, nombre visible, idioma y ajustes de prompt.

### Propiedades

| Propiedad        | Tipo       | Explicacion                                                                                    | Ejemplo                                |
| ---------------- | ---------- | ---------------------------------------------------------------------------------------------- | -------------------------------------- |
| `Id`             | `Guid`     | Identificador unico del perfil.                                                                | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32` |
| `CompanyId`      | `Guid`     | Compañia a la que pertenece el perfil.                                                         | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ModelName`      | `string`   | Modelo elegido para esa compañia. No se debe hardcodear en el agente.                          | `gpt-4.1-mini`                         |
| `LlmProvider`    | `enum`     | Proveedor LLM elegido para la compañia. En el codigo actual existe con valor por defecto `openai`; la columna fisica requiere migracion manual del operador. | `openai`                               |
| `DisplayName`    | `string`   | Nombre que se puede usar para describir al asistente.                                          | `Contoso Assistant`                    |
| `Language`       | `string`   | Idioma principal de respuesta.                                                                 | `es`                                   |
| `PromptOverride` | `string?`  | Instrucciones adicionales de estilo o comportamiento. No deben reemplazar reglas de seguridad. | `Usa un tono amable y responde breve.` |
| `CreatedAt`      | `DateTime` | Fecha UTC de creacion.                                                                         | `2026-05-22T10:15:30Z`                 |
| `UpdatedAt`      | `DateTime` | Fecha UTC de ultima actualizacion.                                                             | `2026-05-22T10:45:00Z`                 |

### Relacion

Cada compañia tiene como maximo un `agent_profile`. Esto se refuerza con un indice unico sobre `CompanyId`.

### Ejemplo mental

Dos compañias pueden usar el mismo sistema pero con estilos distintos:

- `Contoso Bistro`: responde en espanol, tono calido.
- `Northwind Support`: responde en ingles, tono formal.

Ambas comparten codigo, pero tienen perfiles diferentes.

---

## 🧰 `company_tool`

Controla que herramientas puede usar el agente para una compañia especifica.

### Para que sirve

El modelo nunca ejecuta efectos secundarios directamente. Cuando quiere realizar una accion, debe pedir una herramienta. `company_tool` dice cuales herramientas estan habilitadas para cada compañia.

### Propiedades

| Propiedad           | Tipo                | Explicacion                                                 | Ejemplo                                |
| ------------------- | ------------------- | ----------------------------------------------------------- | -------------------------------------- |
| `Id`                | `Guid`              | Identificador unico de la configuracion de herramienta.     | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40` |
| `CompanyId`         | `Guid`              | Compañia propietaria.                                       | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ToolKey`           | `string`            | Clave tecnica de la herramienta.                            | `request_human_handoff`                |
| `IsEnabled`         | `bool`              | Indica si la herramienta esta disponible para esa compañia. | `true`                                 |
| `CredentialReferenceId` | `Guid?`          | FK opcional a la credencial externa que usa la herramienta. Nullable para herramientas internas. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42` |
| `Configuration`     | `ToolConfiguration?` / `jsonb` | Configuracion especifica de la herramienta.                 | `{"toolKey":"request_human_handoff","request_human_handoff":{"escalationChannel":"front-desk"}}` |
| `CreatedAt`         | `DateTime`          | Fecha UTC de creacion.                                      | `2026-05-22T10:15:30Z`                 |
| `UpdatedAt`         | `DateTime`          | Fecha UTC de ultima actualizacion.                          | `2026-05-22T10:45:00Z`                 |

### Reglas importantes

- `CompanyId + ToolKey` es unico.
- Deshabilitar una herramienta debe impedir su ejecucion aunque el modelo la pida.
- Herramientas con sistemas externos, como Google Calendar, enlazan su credencial mediante `CredentialReferenceId`.
- La configuracion se deja en JSON porque cada herramienta puede necesitar parametros diferentes.
- Las herramientas de reserva de Google Calendar incluyen consultar, crear, actualizar y cancelar reservas. Para buscar, actualizar o cancelar, el backend resuelve el cliente actual desde la conversacion y no acepta telefonos enviados por el modelo.

---

## 🔐 `integration_credential_reference`

Guarda referencias a credenciales externas por compañia y proveedor.

### Para que sirve

El sistema necesita integrarse con servicios externos, pero la base de datos no debe guardar secretos crudos. Esta tabla guarda referencias como `kv://...`, que luego se resuelven contra un store seguro.

### Propiedades

| Propiedad      | Tipo                | Explicacion                                   | Ejemplo                                |
| -------------- | ------------------- | --------------------------------------------- | -------------------------------------- |
| `Id`           | `Guid`              | Identificador unico de la referencia.         | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42` |
| `CompanyId`    | `Guid`              | Compañia propietaria.                         | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `Provider`     | `IntegrationProvider` | Proveedor externo tipado; se persiste con nombre snake_case. | `whatsapp_cloud`                       |
| `Purpose`      | `string`            | Uso de esa credencial dentro del sistema.     | `message_send`                         |
| `Reference`    | `string`            | Ubicacion logica del secreto.                 | `kv://whatsapp/contoso/access-token`   |
| `Metadata`     | `CredentialMetadata?` / `jsonb` | Datos no secretos para operar la integracion. | `{"provider":"whatsapp_cloud","whatsapp_cloud":{"appId":"12345","tokenVersion":"v20.0"}}` |
| `CreatedAt`    | `DateTime`          | Fecha UTC de creacion.                        | `2026-05-22T10:15:30Z`                 |
| `UpdatedAt`    | `DateTime`          | Fecha UTC de ultima actualizacion.            | `2026-05-22T10:45:00Z`                 |

### Regla de seguridad

`Reference` no debe contener tokens, passwords ni API keys. Debe contener una referencia resoluble por infraestructura segura.

### Proveedores soportados

| Valor C# | Valor persistido/API | Uso principal |
| -------- | -------------------- | ------------- |
| `IntegrationProvider.WhatsAppCloud` | `whatsapp_cloud` | Credenciales de WhatsApp Cloud para envio y recepcion de mensajes. |
| `IntegrationProvider.GoogleCalendar` | `google_calendar` | Credenciales de Google Calendar para herramientas de calendario. |

---

## 👤 `customer`

Representa a una persona externa hablando con una compañia por un canal.

### Para que sirve

Un cliente no es global para toda la plataforma. La identidad del cliente se define dentro de una compañia y un canal. Esto evita mezclar identidades entre compañias.

### Propiedades

| Propiedad            | Tipo       | Explicacion                                                            | Ejemplo                                |
| -------------------- | ---------- | ---------------------------------------------------------------------- | -------------------------------------- |
| `Id`                 | `Guid`     | Identificador interno del cliente.                                     | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33` |
| `CompanyId`          | `Guid`     | Compañia propietaria del cliente.                                      | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `CompanyChannelId`   | `Guid`     | Canal concreto donde se observo esta identidad. Distingue, por ejemplo, dos numeros de WhatsApp de la misma compañia. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31` |
| `ExternalCustomerId` | `string`   | ID del cliente segun el proveedor. En WhatsApp suele ser el `wa_id`.   | `573001112233`                         |
| `DisplayName`        | `string?`  | Nombre opcional del cliente. Puede venir del canal o de staff interno. | `Karina Perez`                         |
| `CreatedAt`          | `DateTime` | Fecha UTC de creacion.                                                 | `2026-05-22T10:15:30Z`                 |
| `UpdatedAt`          | `DateTime` | Fecha UTC de ultima actualizacion.                                     | `2026-05-22T10:45:00Z`                 |

### Reglas importantes

- `CompanyChannelId + ExternalCustomerId` es unico.
- El mismo numero/persona puede existir como clientes distintos en compañias distintas.
- Esta tabla no intenta unificar identidades entre canales; eso queda para una etapa futura.

---

## 💬 `conversation`

Agrupa mensajes entre un cliente y una compañia dentro de un canal.

### Para que sirve

Una conversacion es la unidad de continuidad. Permite saber que mensajes pertenecen al mismo flujo y que estado tiene la interaccion.

### Propiedades

| Propiedad       | Tipo                 | Explicacion                                                                                   | Ejemplo                                |
| --------------- | -------------------- | --------------------------------------------------------------------------------------------- | -------------------------------------- |
| `Id`            | `Guid`               | Identificador unico de la conversacion.                                                       | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34` |
| `CompanyId`     | `Guid`               | Compañia propietaria.                                                                         | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `CustomerId`    | `Guid`               | Cliente participante.                                                                         | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33` |
| `CompanyChannelId` | `Guid`            | Canal concreto de la conversacion.                                                            | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31` |
| `AgentProfileId` | `Guid`             | Perfil de agente asignado al crear la conversacion. Se mantiene como snapshot auditable.       | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32` |
| `Status`        | `ConversationStatus` | Estado actual de la conversacion.                                                             | `Open`                                 |
| `LastMessageAt` | `DateTime`           | Momento UTC del ultimo mensaje conocido. Sirve para ordenar, reabrir o cerrar conversaciones. | `2026-05-22T10:20:00Z`                 |
| `CreatedAt`     | `DateTime`           | Fecha UTC de creacion.                                                                        | `2026-05-22T10:15:30Z`                 |
| `UpdatedAt`     | `DateTime`           | Fecha UTC de ultima actualizacion.                                                            | `2026-05-22T10:45:00Z`                 |

### Estados posibles

| Estado      | Significado                                                                                             |
| ----------- | ------------------------------------------------------------------------------------------------------- |
| `Open`      | La conversacion esta activa y puede ser procesada por el agente.                                        |
| `HandedOff` | Se paso a una persona o flujo humano. El agente debe ser cuidadoso antes de intervenir.                 |
| `Closed`    | La conversacion termino. Nuevos mensajes podrian crear o reabrir una conversacion segun reglas futuras. |

---

## 🧠 `conversation_state`

Guarda estado temporal y estructurado de una conversacion.

### Para que sirve

No todo estado debe vivir como mensajes. A veces el sistema necesita recordar una decision transitoria: por ejemplo que se solicito handoff, que se espera confirmacion o que hay un flujo en progreso. Ese estado vive aqui como JSON.

### Propiedades

| Propiedad        | Tipo               | Explicacion                            | Ejemplo                                                                   |
| ---------------- | ------------------ | -------------------------------------- | ------------------------------------------------------------------------- |
| `Id`             | `Guid`             | Identificador unico del estado.        | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b35`                                    |
| `CompanyId`      | `Guid`             | Compañia propietaria.                  | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30`                                    |
| `ConversationId` | `Guid`             | Conversacion asociada.                 | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34`                                    |
| `Snapshot`       | `ConversationStateSnapshot` / `jsonb` | Estado serializado de la conversacion. | `{"currentIntent":"human_handoff_request","slots":[{"name":"handoff_reason","textValue":"support"}],"conversationFlags":["human_requested"]}` |
| `CreatedAt`      | `DateTime`         | Fecha UTC de creacion.                 | `2026-05-22T10:15:30Z`                                                    |
| `UpdatedAt`      | `DateTime`         | Fecha UTC de ultima actualizacion.     | `2026-05-22T10:45:00Z`                                                    |

### Regla importante

`ConversationId` es unico. Una conversacion tiene como maximo una fila de estado activo.

### Ejemplo de cambio de estado por turno

Este ejemplo muestra que el `Snapshot` solo cambia cuando el sistema necesita recordar una accion pendiente o cuando esa accion ya se ejecuto y el estado debe limpiarse.

| Turno | Mensaje | Cambia state? | `Snapshot` despues |
| ----- | ------- | ------------- | ------------------- |
| 1 | Cliente: "Quiero saber si tienen citas..." | No | `{}` |
| 2 | Bot: "Si, tengo a las 15:00, 16:30 y 18:00..." | No* | `{}` |
| 3 | Cliente: "Las 16:30 esta bien" | No | `{}` |
| 4 | Bot: "Te agendo el 22 a las 16:30?" | Si | `{"awaiting":"confirmation","pending_action":{"type":"schedule_appointment","date":"22","time":"16:30"}}` |
| 5 | Cliente: "Si" | Si | `{}` accion ejecutada, state limpiado |

`No*` significa que el bot ofrecio opciones, pero todavia no dejo una accion lista para ejecutar; por eso no necesita guardar estado estructurado.

---

## ✉️ `message`

Guarda los mensajes crudos o normalizados de una conversacion.

### Para que sirve

Esta tabla es el historial principal. Contiene lo que dijo el usuario, lo que respondio el asistente, eventos de tool calls, tool results o mensajes de sistema.

### Propiedades

| Propiedad           | Tipo                | Explicacion                                      | Ejemplo                                |
| ------------------- | ------------------- | ------------------------------------------------ | -------------------------------------- |
| `Id`                | `Guid`              | Identificador unico del mensaje.                 | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36` |
| `CompanyId`         | `Guid`              | Compañia propietaria.                            | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ConversationId`    | `Guid`              | Conversacion donde vive el mensaje.              | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34` |
| `Role`              | `MessageRole`       | Rol del mensaje dentro del flujo conversacional. | `User`                                 |
| `Type`              | `MessageType`       | Tipo principal del mensaje. Por ahora `Text` o `Audio`. | `Audio`                         |
| `MessageText`       | `string?`           | Texto canonico del mensaje: texto normal, transcript STT o texto fuente TTS. | `Necesito hablar con una persona.` |
| `ProviderMessageId` | `string?`           | ID del proveedor usado para idempotencia.        | `wamid.HBgMNTczMDAxMTEyMjMz`           |
| `Payload`           | `MessagePayload?` / `jsonb` | Metadatos variables del mensaje, especialmente audio. | `{"providerType":"whatsapp","audio":{"blobUri":"","contentType":"audio/ogg","sizeBytes":184320,"language":"es","durationMs":12300,"sttStatus":"Completed","ttsStatus":null}}` |
| `OccurredAt`        | `DateTime`          | Momento UTC en que ocurrio el mensaje.           | `2026-05-22T10:15:30Z`                 |
| `CreatedAt`         | `DateTime`          | Fecha UTC de insercion en la base.               | `2026-05-22T10:15:31Z`                 |
| `UpdatedAt`         | `DateTime`          | Fecha UTC de ultima modificacion.                | `2026-05-22T10:15:31Z`                 |

### Roles posibles

| Rol          | Significado                                           |
| ------------ | ----------------------------------------------------- |
| `User`       | Mensaje enviado por el cliente.                       |
| `Assistant`  | Respuesta generada por el asistente.                  |
| `ToolCall`   | Registro de una herramienta solicitada por el agente. |
| `ToolResult` | Resultado de una herramienta.                         |
| `System`     | Mensaje tecnico o interno del sistema.                |

### Idempotencia

Hay un indice unico sobre `CompanyId + ProviderMessageId`, filtrado para cuando `ProviderMessageId` no es null. El canal se deriva por `Message -> Conversation -> CompanyChannel`. Esto evita guardar dos veces el mismo webhook entrante sin duplicar el canal en `message`.

El historial usado por el Worker consulta los ultimos turnos elegibles por `ConversationId`, ordenados por `OccurredAt` y `Id` descendente para desempate estable. El modelo debe mantener un indice compuesto por `CompanyId + ConversationId + OccurredAt DESC + Id DESC` para evitar degradacion en conversaciones largas. La migracion fisica de ese indice sigue siendo una operacion manual del propietario del proyecto.

---

## 🛠️ `tool_execution`

Audita cada ejecucion de herramienta solicitada por el agente.

### Para que sirve

El agente no ejecuta acciones directamente. El sistema recibe una solicitud de herramienta, la valida, la ejecuta si esta permitida y registra el resultado. Esta tabla permite trazabilidad, idempotencia y debugging.

### Propiedades

| Propiedad        | Tipo                  | Explicacion                                                          | Ejemplo                                    |
| ---------------- | --------------------- | -------------------------------------------------------------------- | ------------------------------------------ |
| `Id`             | `Guid`                | Identificador unico de la ejecucion.                                 | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b39`     |
| `CompanyId`      | `Guid`                | Compañia propietaria.                                                | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30`     |
| `ConversationId` | `Guid`                | Conversacion donde se solicito la herramienta.                       | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34`     |
| `CompanyToolId`  | `Guid`                | FK a la herramienta habilitada para la compañia.                     | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40`     |
| `TriggerMessageId` | `Guid`              | Mensaje assistant que solicito la herramienta.                       | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36`     |
| `ResultMessageId` | `Guid?`             | Mensaje que devuelve el resultado al hilo. Nullable porque se persiste despues de crear la ejecucion. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b37` |
| `ToolKey`        | `string`              | Snapshot de la clave tecnica ejecutada. Se conserva aunque `company_tool` cambie luego.        | `request_human_handoff`                    |
| `IdempotencyKey` | `string`              | Clave estable para evitar ejecutar dos veces la misma accion logica. | `conversation-123:request_human_handoff:1` |
| `Status`         | `ToolExecutionStatus` | Resultado final de la ejecucion.                                     | `Succeeded`                                |
| `Request`        | `ToolExecutionRequest?` / `jsonb` | Parametros enviados a la herramienta.                                | `{"toolKey":"request_human_handoff","request_human_handoff":{"reason":"customer_asked_for_person"}}` |
| `Result`         | `ToolExecutionResult?` / `jsonb`  | Resultado estructurado de la herramienta.                            | `{"toolKey":"request_human_handoff","request_human_handoff":{"handoffRequested":true}}` |
| `FailureReason`  | `string?`             | Motivo corto de falla o denegacion.                                  | `tool_not_enabled`                         |
| `CreatedAt`      | `DateTime`            | Fecha UTC de creacion.                                               | `2026-05-22T10:15:30Z`                     |
| `UpdatedAt`      | `DateTime`            | Fecha UTC de ultima actualizacion.                                   | `2026-05-22T10:45:00Z`                     |

Las ejecuciones de Google Calendar pueden usar claves como `find_google_calendar_reservations`,
`create_google_calendar_reservation`, `update_google_calendar_reservation` y
`cancel_google_calendar_reservation`. Los resultados devueltos al modelo son sanitizados:
incluyen identificadores de reserva/evento, horarios locales, resumen, nombre si esta disponible,
URL del evento si es seguro devolverla, conteo y si hace falta desambiguar.

### Estados posibles

| Estado      | Significado                                                                                         |
| ----------- | --------------------------------------------------------------------------------------------------- |
| `Pending`   | La ejecucion fue registrada y aun no tiene resultado final.                                         |
| `Succeeded` | La herramienta se ejecuto correctamente.                                                            |
| `Failed`    | La herramienta intento ejecutarse, pero fallo.                                                      |
| `Denied`    | La ejecucion fue rechazada antes de ejecutar, por ejemplo porque la herramienta no esta habilitada. |

### Regla importante

`CompanyId + IdempotencyKey` es unico. Esto permite reintentar jobs sin duplicar acciones.

---

# Flujo De Datos Ejemplo 🌊

1. Llega un webhook de WhatsApp Cloud.
2. El sistema busca `company_channel` usando `Provider = whatsapp_cloud` y `ProviderChannelId = phone_number_id`.
3. Con eso encuentra la `company`.
4. Busca o crea un `customer` usando `CompanyChannelId + ExternalCustomerId`.
5. Busca o crea una `conversation` abierta para ese cliente y ese canal, guardando el `AgentProfileId` vigente al crearla.
6. Guarda el `message` entrante con `ProviderMessageId` para evitar duplicados.
7. Si el mensaje trae audio, guarda sus metadatos en `message.payload_json.audio`.
8. El agente procesa el turno usando `agent_profile`, ultimos mensajes elegibles y herramientas habilitadas en `company_tool`.
9. Si el agente pide una herramienta, Worker orquesta el turno y delega en `CeoAgent.Infrastructure/Implementation/AITools`, donde el gateway valida el catalogo habilitado, ejecuta el handler nativo, registra `tool_execution` cuando aplica y devuelve un resultado sanitizado al modelo antes de responder al cliente.

---

# Resumen De Indices Y Restricciones 🔎

| Tabla                              | Restriccion                                                                                   | Por que importa                                                       |
| ---------------------------------- | --------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| `company_channel`                  | Unico por `Provider + ProviderChannelId`                                                      | Permite resolver una compañia desde el canal receptor sin ambiguedad. |
| `agent_profile`                    | Unico por `CompanyId`                                                                         | Cada compañia tiene un solo perfil activo de agente.                  |
| `company_tool`                     | Unico por `CompanyId + ToolKey`                                                               | Evita duplicar la misma herramienta para una compañia.                |
| `integration_credential_reference` | Unico por `CompanyId + Provider + Purpose`                                                    | Evita multiples credenciales conflictivas para el mismo uso.          |
| `customer`                         | Unico por `CompanyChannelId + ExternalCustomerId`                                            | Evita duplicar clientes dentro del mismo canal concreto.              |
| `conversation`                     | Unico por `CompanyId + CustomerId + CompanyChannelId` cuando `Status = Open`                 | Evita dos conversaciones abiertas para el mismo cliente en el mismo canal. |
| `conversation_state`               | Unico por `ConversationId`                                                                    | Una conversacion tiene un unico estado temporal activo.               |
| `message`                          | Unico por `CompanyId + ProviderMessageId` cuando `ProviderMessageId` no es null              | Hace idempotente la ingesta de webhooks.                              |
| `message`                          | Indice por `CompanyId + ConversationId + OccurredAt DESC + Id DESC`                         | Mantiene eficiente la carga de los ultimos turnos del agente.         |
| `tool_execution`                   | Unico por `CompanyId + IdempotencyKey`                                                        | Hace idempotente la ejecucion de herramientas.                        |

---

# Decisiones De Diseno Importantes ✅

## Multi-company primero

Cada tabla operativa usa `CompanyId`. Esto evita fugas de datos entre compañias y permite aplicar filtros globales de EF Core.

## Secretos fuera de la base

La base guarda referencias como `kv://...`, no secretos. Esto reduce el impacto de una exposicion de datos y mantiene separadas configuracion y credenciales sensibles.

## Historial crudo, no resumen

Los mensajes se guardan como turnos crudos en `message`. Esto permite auditar que paso realmente sin depender de resumenes generados.

## JSON donde hay variabilidad real

El modelo evita sobre-normalizar configuraciones que cambian por proveedor o herramienta. Por eso usa `jsonb` en metadata, payloads, state y resultados, expuestos en C# como objetos tipados en lugar de strings.

## Herramientas auditables

Toda accion solicitada por el agente debe pasar por `tool_execution`. Esto ayuda a seguridad, trazabilidad, reintentos e investigacion de fallos.

## Concurrencia optimista

Los agregados mutables clave deben usar tokens reales de concurrencia. En PostgreSQL el modelo usa `xmin` como row version en conversaciones, estado de conversacion, perfiles, clientes y herramientas configurables. Esto permite detectar escrituras concurrentes en flujos sensibles del Worker, aunque no reemplaza una outbox durable para envios externos.
