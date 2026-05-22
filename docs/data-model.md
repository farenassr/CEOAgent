# CEOAgent Data Model 📚

Guia explicativa del modelo de datos actual del backend. Este documento describe para que sirve cada tabla, que representa cada propiedad, ejemplos de valores reales y las decisiones importantes de diseño.

> Estado actual: el MVP ya no incluye reservas. El modelo esta enfocado en companias, canales, clientes, conversaciones, mensajes, media de audio, herramientas e integraciones externas.

## Vista General 🧭

El modelo esta pensado para un backend SaaS multi-company. Cada compania configura sus canales, su agente, sus credenciales externas y sus herramientas disponibles. A partir de ahi, el sistema registra clientes, conversaciones, mensajes, archivos de audio y ejecuciones de herramientas.

La regla central es simple: casi todo lo que pertenece a una compania lleva `CompanyId`. Ese campo permite aislar datos por compania y aplicar filtros globales desde EF Core.

| Area | Tablas | Proposito |
| --- | --- | --- |
| 🏢 Configuracion de compania | `company`, `company_channel`, `agent_profile`, `company_tool`, `integration_credential_reference` | Define quien es la compania, por donde habla, como se comporta su agente, que herramientas tiene y que credenciales externas usa. |
| 👤 Conversaciones | `customer`, `conversation`, `conversation_state`, `message` | Guarda identidades de clientes, conversaciones abiertas/cerradas, estado temporal y mensajes crudos. |
| 🎧 Media | `audio_asset` | Registra audios de entrada o salida, su ubicacion en blob storage y transcripciones. |
| 🛠️ Ejecucion de herramientas | `tool_execution` | Audita cada accion solicitada por el agente y su resultado. |

## Convenciones Del Modelo 🧱

### IDs

Todos los identificadores son `Guid` generados como GUID v7. Esto da IDs globalmente unicos y mejor orden temporal que un GUID aleatorio clasico.

Ejemplo:

```text
018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30
```

### Auditoria

Las tablas company-owned heredan los campos:

| Propiedad | Para que sirve | Ejemplo |
| --- | --- | --- |
| `CompanyId` | Identifica la compania propietaria del registro. Es la base del aislamiento multi-company. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `CreatedAt` | Fecha UTC en la que se creo el registro. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | Fecha UTC de la ultima modificacion. | `2026-05-22T10:45:00Z` |

`CreatedAt` y `UpdatedAt` los estampa `AppDbContext` automaticamente al guardar cambios.

### JSON

Algunas propiedades se guardan como `jsonb` en PostgreSQL. Se usan cuando la estructura puede variar por proveedor o configuracion:

- `WorkingHoursJson`
- `MetadataJson`
- `ConfigurationJson`
- `StateJson`
- `PayloadJson`
- `RequestJson`
- `ResultJson`

La idea es mantener flexibilidad sin crear tablas prematuras para datos que todavia no tienen reglas relacionales fuertes.

---

# Tablas

## 🏢 `company`

Representa una compania que usa la plataforma. Es el tenant funcional del sistema: todo canal, cliente, conversacion, herramienta o integracion termina asociado a una `company`.

### Para que sirve

`company` define la identidad operativa y configuracion base de una empresa. Si manana el sistema atiende cinco restaurantes, cinco tiendas o cinco negocios, cada uno tendra una fila en esta tabla.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico de la compania. Se usa como FK en casi todo el modelo. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `Name` | `string` | Nombre humano de la compania. Sirve para administracion, logs y pantallas internas. | `Contoso Bistro` |
| `WorkingHoursJson` | `string?` / `jsonb` | Horarios de operacion o disponibilidad. No impone reglas por si solo; es configuracion para procesos de negocio futuros. | `{"monday":[{"start":"09:00","end":"18:00"}]}` |
| `TimeZoneId` | `string` | Zona horaria IANA de la compania. Es clave para interpretar fechas locales, horarios y mensajes. | `America/Bogota` |
| `Status` | `CompanyStatus` | Estado de vida de la compania. Controla si esta activa o deshabilitada. | `Active` |
| `CreatedAt` | `DateTime` | Momento UTC en que se creo la compania. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Momento UTC de la ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Relaciones

- Una `company` tiene muchos `company_channel`.
- Una `company` tiene un `agent_profile`.
- Una `company` tiene muchos `company_tool`.
- Una `company` tiene muchas credenciales externas.
- Una `company` posee clientes, conversaciones, mensajes, audios y ejecuciones de herramientas.

### Ejemplo mental

Si `Contoso Bistro` usa WhatsApp Cloud, su fila `company` guarda el nombre, zona horaria y estado. Luego `company_channel` dira cual es el `phone_number_id` de WhatsApp que pertenece a esa compania.

---

## 📡 `company_channel`

Registra canales externos por los que una compania recibe o envia mensajes. En el MVP puede ser WhatsApp Cloud, pero el diseno permite otros proveedores.

### Para que sirve

Cuando entra un webhook, el sistema no debe identificar la compania por el numero del cliente. La compania se resuelve por el canal receptor, por ejemplo el `phone_number_id` de WhatsApp Cloud. Esa asociacion vive aqui.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico del canal registrado. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31` |
| `CompanyId` | `Guid` | Compania propietaria del canal. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `Provider` | `string` | Nombre tecnico del proveedor de canal. | `whatsapp_cloud` |
| `ProviderChannelId` | `string` | Identificador estable del canal dentro del proveedor. Para WhatsApp suele ser `phone_number_id`. | `123456789012345` |
| `MetadataJson` | `string?` / `jsonb` | Datos adicionales del proveedor. | `{"business_account_id":"987654321"}` |
| `CredentialReference` | `string?` | Referencia a secretos externos, nunca el secreto crudo. | `kv://whatsapp/contoso` |
| `CreatedAt` | `DateTime` | Fecha UTC de creacion. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Reglas importantes

- `Provider + ProviderChannelId` es unico.
- El telefono del cliente no identifica la compania.
- Las credenciales se guardan por referencia, no como valores sensibles.

---

## 🤖 `agent_profile`

Define como debe comportarse el agente de IA para una compania.

### Para que sirve

No todas las companias hablan igual ni usan el mismo modelo. `agent_profile` guarda la configuracion conversacional base: modelo, nombre visible, idioma y ajustes de prompt.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico del perfil. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32` |
| `CompanyId` | `Guid` | Compania a la que pertenece el perfil. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ModelName` | `string` | Modelo elegido para esa compania. No se debe hardcodear en el agente. | `gpt-4.1-mini` |
| `DisplayName` | `string` | Nombre que se puede usar para describir al asistente. | `Contoso Assistant` |
| `Language` | `string` | Idioma principal de respuesta. | `es` |
| `PromptOverride` | `string?` | Instrucciones adicionales de estilo o comportamiento. No deben reemplazar reglas de seguridad. | `Usa un tono amable y responde breve.` |
| `CreatedAt` | `DateTime` | Fecha UTC de creacion. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Relacion

Cada compania tiene como maximo un `agent_profile`. Esto se refuerza con un indice unico sobre `CompanyId`.

### Ejemplo mental

Dos companias pueden usar el mismo sistema pero con estilos distintos:

- `Contoso Bistro`: responde en espanol, tono calido.
- `Northwind Support`: responde en ingles, tono formal.

Ambas comparten codigo, pero tienen perfiles diferentes.

---

## 🧰 `company_tool`

Controla que herramientas puede usar el agente para una compania especifica.

### Para que sirve

El modelo nunca ejecuta efectos secundarios directamente. Cuando quiere realizar una accion, debe pedir una herramienta. `company_tool` dice cuales herramientas estan habilitadas para cada compania.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico de la configuracion de herramienta. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40` |
| `CompanyId` | `Guid` | Compania propietaria. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ToolKey` | `string` | Clave tecnica de la herramienta. | `request_human_handoff` |
| `IsEnabled` | `bool` | Indica si la herramienta esta disponible para esa compania. | `true` |
| `ConfigurationJson` | `string?` / `jsonb` | Configuracion especifica de la herramienta. | `{"handoffQueue":"front-desk"}` |
| `CreatedAt` | `DateTime` | Fecha UTC de creacion. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Reglas importantes

- `CompanyId + ToolKey` es unico.
- Deshabilitar una herramienta debe impedir su ejecucion aunque el modelo la pida.
- La configuracion se deja en JSON porque cada herramienta puede necesitar parametros diferentes.

---

## 🔐 `integration_credential_reference`

Guarda referencias a credenciales externas por compania y proveedor.

### Para que sirve

El sistema necesita integrarse con servicios externos, pero la base de datos no debe guardar secretos crudos. Esta tabla guarda referencias como `kv://...`, que luego se resuelven contra un store seguro.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico de la referencia. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b42` |
| `CompanyId` | `Guid` | Compania propietaria. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `Provider` | `string` | Proveedor externo. | `whatsapp_cloud` |
| `Purpose` | `string` | Uso de esa credencial dentro del sistema. | `message_send` |
| `Reference` | `string` | Ubicacion logica del secreto. | `kv://whatsapp/contoso/access-token` |
| `MetadataJson` | `string?` / `jsonb` | Datos no secretos para operar la integracion. | `{"appId":"12345"}` |
| `CreatedAt` | `DateTime` | Fecha UTC de creacion. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Regla de seguridad

`Reference` no debe contener tokens, passwords ni API keys. Debe contener una referencia resoluble por infraestructura segura.

---

## 👤 `customer`

Representa a una persona externa hablando con una compania por un canal.

### Para que sirve

Un cliente no es global para toda la plataforma. La identidad del cliente se define dentro de una compania y un canal. Esto evita mezclar identidades entre companias.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador interno del cliente. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33` |
| `CompanyId` | `Guid` | Compania propietaria del cliente. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ChannelType` | `string` | Canal donde se observo esta identidad. | `whatsapp_cloud` |
| `ExternalCustomerId` | `string` | ID del cliente segun el proveedor. En WhatsApp suele ser el `wa_id`. | `573001112233` |
| `DisplayName` | `string?` | Nombre opcional del cliente. Puede venir del canal o de staff interno. | `Karina Perez` |
| `CreatedAt` | `DateTime` | Fecha UTC de creacion. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Reglas importantes

- `CompanyId + ChannelType + ExternalCustomerId` es unico.
- El mismo numero/persona puede existir como clientes distintos en companias distintas.
- Esta tabla no intenta unificar identidades entre canales; eso queda para una etapa futura.

---

## 💬 `conversation`

Agrupa mensajes entre un cliente y una compania dentro de un canal.

### Para que sirve

Una conversacion es la unidad de continuidad. Permite saber que mensajes pertenecen al mismo flujo y que estado tiene la interaccion.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico de la conversacion. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34` |
| `CompanyId` | `Guid` | Compania propietaria. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `CustomerId` | `Guid` | Cliente participante. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33` |
| `ChannelType` | `string` | Canal de la conversacion. | `whatsapp_cloud` |
| `Status` | `ConversationStatus` | Estado actual de la conversacion. | `Open` |
| `LastMessageAt` | `DateTime` | Momento UTC del ultimo mensaje conocido. Sirve para ordenar, reabrir o cerrar conversaciones. | `2026-05-22T10:20:00Z` |
| `CreatedAt` | `DateTime` | Fecha UTC de creacion. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Estados posibles

| Estado | Significado |
| --- | --- |
| `Open` | La conversacion esta activa y puede ser procesada por el agente. |
| `HandedOff` | Se paso a una persona o flujo humano. El agente debe ser cuidadoso antes de intervenir. |
| `Closed` | La conversacion termino. Nuevos mensajes podrian crear o reabrir una conversacion segun reglas futuras. |

---

## 🧠 `conversation_state`

Guarda estado temporal y estructurado de una conversacion.

### Para que sirve

No todo estado debe vivir como mensajes. A veces el sistema necesita recordar una decision transitoria: por ejemplo que se solicito handoff, que se espera confirmacion o que hay un flujo en progreso. Ese estado vive aqui como JSON.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico del estado. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b35` |
| `CompanyId` | `Guid` | Compania propietaria. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ConversationId` | `Guid` | Conversacion asociada. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34` |
| `StateJson` | `string` / `jsonb` | Estado serializado de la conversacion. | `{"intent":"human_handoff_request","reason":"customer_asked_for_person"}` |
| `CreatedAt` | `DateTime` | Fecha UTC de creacion. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Regla importante

`ConversationId` es unico. Una conversacion tiene como maximo una fila de estado activo.

---

## ✉️ `message`

Guarda los mensajes crudos o normalizados de una conversacion.

### Para que sirve

Esta tabla es el historial principal. Contiene lo que dijo el usuario, lo que respondio el asistente, eventos de tool calls, tool results o mensajes de sistema.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico del mensaje. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36` |
| `CompanyId` | `Guid` | Compania propietaria. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ConversationId` | `Guid` | Conversacion donde vive el mensaje. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34` |
| `Role` | `MessageRole` | Rol del mensaje dentro del flujo conversacional. | `User` |
| `ChannelType` | `string` | Canal por el que entro o salio el mensaje. | `whatsapp_cloud` |
| `Text` | `string?` | Texto del mensaje o transcripcion de audio. | `Necesito hablar con una persona.` |
| `ProviderMessageId` | `string?` | ID del proveedor usado para idempotencia. | `wamid.HBgMNTczMDAxMTEyMjMz` |
| `PayloadJson` | `string?` / `jsonb` | Payload original o normalizado del proveedor. | `{"type":"text","id":"wamid..."}` |
| `OccurredAt` | `DateTime` | Momento UTC en que ocurrio el mensaje. | `2026-05-22T10:15:30Z` |
| `CreatedAt` | `DateTime` | Fecha UTC de insercion en la base. | `2026-05-22T10:15:31Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima modificacion. | `2026-05-22T10:15:31Z` |

### Roles posibles

| Rol | Significado |
| --- | --- |
| `User` | Mensaje enviado por el cliente. |
| `Assistant` | Respuesta generada por el asistente. |
| `ToolCall` | Registro de una herramienta solicitada por el agente. |
| `ToolResult` | Resultado de una herramienta. |
| `System` | Mensaje tecnico o interno del sistema. |

### Idempotencia

Hay un indice unico sobre `CompanyId + ChannelType + ProviderMessageId`, filtrado para cuando `ProviderMessageId` no es null. Esto evita guardar dos veces el mismo webhook entrante.

---

## 🎧 `audio_asset`

Registra archivos de audio asociados a mensajes o conversaciones.

### Para que sirve

El audio en si no vive en PostgreSQL. Vive en blob storage. Esta tabla guarda la referencia al archivo, metadatos utiles y transcripcion cuando aplica.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico del asset de audio. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b41` |
| `CompanyId` | `Guid` | Compania propietaria. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ConversationId` | `Guid?` | Conversacion asociada, si ya fue resuelta. Puede ser null en etapas tempranas de procesamiento. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34` |
| `MessageId` | `Guid?` | Mensaje asociado, si existe. Puede ser null para assets generados antes de persistir mensaje. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36` |
| `Direction` | `AudioAssetDirection` | Indica si el audio entro desde el cliente o salio desde el sistema. | `Inbound` |
| `BlobUri` | `string` | Ubicacion del archivo en blob storage. | `https://storage.example/audio/inbound/voice.ogg` |
| `ContentType` | `string` | MIME type del archivo. | `audio/ogg` |
| `SizeBytes` | `long` | Peso del archivo en bytes. | `184320` |
| `Transcript` | `string?` | Texto transcrito para audio entrante, si hubo transcripcion. | `Necesito hablar con soporte.` |
| `CreatedAt` | `DateTime` | Fecha UTC de creacion. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Direcciones posibles

| Direccion | Significado |
| --- | --- |
| `Inbound` | Audio recibido desde el cliente. |
| `Outbound` | Audio generado/enviado por el sistema. |

---

## 🛠️ `tool_execution`

Audita cada ejecucion de herramienta solicitada por el agente.

### Para que sirve

El agente no ejecuta acciones directamente. El sistema recibe una solicitud de herramienta, la valida, la ejecuta si esta permitida y registra el resultado. Esta tabla permite trazabilidad, idempotencia y debugging.

### Propiedades

| Propiedad | Tipo | Explicacion | Ejemplo |
| --- | --- | --- | --- |
| `Id` | `Guid` | Identificador unico de la ejecucion. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b39` |
| `CompanyId` | `Guid` | Compania propietaria. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30` |
| `ConversationId` | `Guid` | Conversacion donde se solicito la herramienta. | `018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34` |
| `ToolKey` | `string` | Clave tecnica de la herramienta ejecutada. | `request_human_handoff` |
| `IdempotencyKey` | `string` | Clave estable para evitar ejecutar dos veces la misma accion logica. | `conversation-123:request_human_handoff:1` |
| `Status` | `ToolExecutionStatus` | Resultado final de la ejecucion. | `Succeeded` |
| `RequestJson` | `string?` / `jsonb` | Parametros enviados a la herramienta. | `{"reason":"customer_asked_for_person"}` |
| `ResultJson` | `string?` / `jsonb` | Resultado estructurado de la herramienta. | `{"handoffRequested":true}` |
| `FailureReason` | `string?` | Motivo corto de falla o denegacion. | `tool_not_enabled` |
| `CreatedAt` | `DateTime` | Fecha UTC de creacion. | `2026-05-22T10:15:30Z` |
| `UpdatedAt` | `DateTime` | Fecha UTC de ultima actualizacion. | `2026-05-22T10:45:00Z` |

### Estados posibles

| Estado | Significado |
| --- | --- |
| `Succeeded` | La herramienta se ejecuto correctamente. |
| `Failed` | La herramienta intento ejecutarse, pero fallo. |
| `Denied` | La ejecucion fue rechazada antes de ejecutar, por ejemplo porque la herramienta no esta habilitada. |

### Regla importante

`CompanyId + IdempotencyKey` es unico. Esto permite reintentar jobs sin duplicar acciones.

---

# Flujo De Datos Ejemplo 🌊

1. Llega un webhook de WhatsApp Cloud.
2. El sistema busca `company_channel` usando `Provider = whatsapp_cloud` y `ProviderChannelId = phone_number_id`.
3. Con eso encuentra la `company`.
4. Busca o crea un `customer` usando `CompanyId + ChannelType + ExternalCustomerId`.
5. Busca o crea una `conversation` abierta para ese cliente.
6. Guarda el `message` entrante con `ProviderMessageId` para evitar duplicados.
7. Si el mensaje trae audio, registra un `audio_asset`.
8. El agente procesa el turno usando `agent_profile`, ultimos mensajes y herramientas habilitadas en `company_tool`.
9. Si el agente pide una herramienta, se registra un `tool_execution`.

---

# Resumen De Indices Y Restricciones 🔎

| Tabla | Restriccion | Por que importa |
| --- | --- | --- |
| `company_channel` | Unico por `Provider + ProviderChannelId` | Permite resolver una compania desde el canal receptor sin ambiguedad. |
| `agent_profile` | Unico por `CompanyId` | Cada compania tiene un solo perfil activo de agente. |
| `company_tool` | Unico por `CompanyId + ToolKey` | Evita duplicar la misma herramienta para una compania. |
| `integration_credential_reference` | Unico por `CompanyId + Provider + Purpose` | Evita multiples credenciales conflictivas para el mismo uso. |
| `customer` | Unico por `CompanyId + ChannelType + ExternalCustomerId` | Evita duplicar clientes dentro del mismo canal y compania. |
| `conversation_state` | Unico por `ConversationId` | Una conversacion tiene un unico estado temporal activo. |
| `message` | Unico por `CompanyId + ChannelType + ProviderMessageId` cuando `ProviderMessageId` no es null | Hace idempotente la ingesta de webhooks. |
| `tool_execution` | Unico por `CompanyId + IdempotencyKey` | Hace idempotente la ejecucion de herramientas. |

---

# Decisiones De Diseno Importantes ✅

## Multi-company primero

Cada tabla operativa usa `CompanyId`. Esto evita fugas de datos entre companias y permite aplicar filtros globales de EF Core.

## Secretos fuera de la base

La base guarda referencias como `kv://...`, no secretos. Esto reduce el impacto de una exposicion de datos y mantiene separadas configuracion y credenciales sensibles.

## Historial crudo, no resumen

Los mensajes se guardan como turnos crudos en `message`. Esto permite auditar que paso realmente sin depender de resumenes generados.

## JSON donde hay variabilidad real

El modelo evita sobre-normalizar configuraciones que cambian por proveedor o herramienta. Por eso usa `jsonb` en metadata, payloads, state y resultados.

## Herramientas auditables

Toda accion solicitada por el agente debe pasar por `tool_execution`. Esto ayuda a seguridad, trazabilidad, reintentos e investigacion de fallos.

