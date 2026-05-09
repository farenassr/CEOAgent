# agents.md — MVP Edition (Revised)

> **Audience:** AI coding agents and human contributors generating code for
> this project. Every rule here is normative. If something below conflicts
> with intuition, this document wins. When in doubt, choose the simpler
> option that is consistent with the rules.

---

## Project Summary

This project is a modern **C# / .NET multi-tenant SaaS backend** for
AI-assisted business conversations. The MVP focus is **restaurants using
WhatsApp Cloud and Google Calendar reservations**, but the architecture is
deliberately built so that other channels (Telegram, Instagram DM, web chat)
and other integrations (REST APIs, Excel, customer databases, POS) can be
added later without rewriting the core.

The platform receives inbound channel messages (text or audio), resolves the
tenant by channel, loads tenant configuration, processes the message through
an AI agent, executes approved business tools through a **Tool Execution
Gateway**, and synchronizes results with external systems (Google Calendar
for MVP).

The system is **AOT-aware**, not **AOT-first**. Maintainability and developer
experience win over Native AOT purity, but every chosen library is
source-generation friendly so AOT remains achievable later.

## Solution Layout

The solution lives at the repository root with a flat project layout (no
`src/` nesting), matching the Aspire template convention. Tests live under
`tests/`.

### Target project list

| Project                    | Type        | Purpose                                                                 |
| -------------------------- | ----------- | ----------------------------------------------------------------------- |
| `CEOAgent.AppHost`         | Aspire host | Local orchestration. References `Api` and `Worker`.                     |
| `CEOAgent.ServiceDefaults` | classlib    | OTel, health checks, resilience defaults. Referenced by `Api`/`Worker`. |
| `CEOAgent.ApiService`      | webapi      | HTTP surface (FastEndpoints). Webhook receiver and admin endpoints.     |
| `CEOAgent.Worker`          | worker      | Background processing. Queue-driven jobs, agent loop, integrations.     |
| `CEOAgent.Application`     | classlib    | Cross-slice business logic (`AgentRunner`, `PromptBuilder`, gateway).   |
| `CEOAgent.Infrastructure`  | classlib    | Persistence (EF Core), queues, blob storage, observability glue.        |
| `CEOAgent.Integrations`    | classlib    | **Port contracts only**. No implementations.                            |
| `CEOAgent.Adapters`        | classlib    | Port implementations (WhatsApp, Google Calendar, OpenAI, etc.).         |
| `CEOAgent.Tools`           | classlib    | `IToolHandler` implementations (native MVP tools).                      |

---

## MVP Scope

### Required for MVP

- WhatsApp Cloud inbound and outbound messaging — **text and audio**
  (audio is received as voice notes and sent as TTS-generated voice replies)
- Tenant resolution by `(provider, provider_channel_id)` — for WhatsApp this
  is `("whatsapp_cloud", phone_number_id)`
- Audio transcription (single attempt; on failure → human handoff)
- Audio synthesis (TTS) for outbound voice replies — single provider
- Google Calendar reservations
- Per-tenant agent profile with tenant-selectable model
- Per-tenant **dynamic tool registry** — even though MVP ships with 4 native
  tools, the registry contract is in place from day one
- Conversation history persistence (raw turns; no rolling summaries)
- `ToolExecutionGateway`
- Worker-based background processing
- PostgreSQL — single DbContext, code-first migrations
- Azure Storage Queues for background jobs
- Azure Blob Storage for media files (inbound voice notes, outbound TTS,
  attachments)
- Manual tenant onboarding through admin endpoints protected by a static API
  key
- Basic observability (OpenTelemetry + Langfuse)
- A single health check endpoint
- Auto human handoff after **two consecutive failures** of the same operation
  type within the same conversation turn (see _Failure Counting Rules_)

### Not Required for MVP

- Inbound images, documents, video, location, contacts, stickers
- Outbound interactive messages (buttons, lists) — contract exists, MVP
  returns `NotSupported`
- Payments, CRM, POS integrations
- RAG / knowledge retrieval
- Multiple calendar providers
- WhatsApp Web (only WhatsApp Business Cloud API is supported)
- Dedicated database per tenant
- Self-service tenant onboarding
- Customer long-term memory profile
- Conversation rolling summaries
- Outbox pattern
- DDD aggregates, value objects, domain events
- Distributed cache (Garnet / Redis)
- Feature flags table
- Model escalation tiers
- Keycloak (admin endpoints use a static API key for now)
- Soft delete
- Cross-channel customer identity unification (one customer = one
  `(tenant_id, channel_type, external_customer_id)` for now)

These are intentionally deferred. Add them only when concrete pain justifies
them.

---

## Non-Negotiable Rules

This is the canonical, deduplicated rule list. **Every other section in this
document is consistent with these rules.** If you find a conflict elsewhere,
this list wins.

### Architecture

1. Use **Modular Monolith** for MVP.
2. Use **Vertical Slice Architecture** as the organizing principle, but stay
   pragmatic — split files only when a slice grows enough that splitting
   improves readability.
3. Use **Mediator** (`martinothamar/Mediator`, source-generated, AOT-friendly)
   for in-process command and query dispatch in **both API and Worker**.
   Use Mediator unconditionally for new use cases — there is no
   "only if necessary" exception.
4. Use **FastEndpoints** for the HTTP surface.
5. Use **FluentValidation** for request validation in the API. Validate
   command preconditions inside Worker handlers via the same validators
   (resolved through DI), since the Worker has no FastEndpoints layer.
6. Use **Mapperly** only when the source and destination shape diverge
   (renames, transformations, flattening). When the request and command are
   shape-equivalent, instantiate the command directly — do not introduce
   Mapperly purely for "consistency".
7. Use **Aspire** for local orchestration only. Production Azure resources
   come from Bicep / Terraform / Azure DevOps pipelines.
8. Use `ProblemDetails` for all error responses, populated by a single global
   `IExceptionHandler`. Throw exceptions for unexpected errors. Do not invest
   in `Result<T, Error>` plumbing for MVP.
9. Use **PostgreSQL** as the primary database with one `AppDbContext`.
10. Use **Azure Blob Storage** for audio and other media.
11. Use **Azure Storage Queues** for background jobs.
12. Use **ZLogger** for structured logging.
13. Use **Refit** for typed HTTP clients to external integrations. Use raw
    `HttpClient` only for streaming, multipart uploads, or specialized
    resilience needs.

### Integrations

14. Use **Ports and Adapters** for external integrations. The MVP defines
    **three ports**:
    - `IMessageChannelIntegration` — inbound + outbound messaging across
      channels (WhatsApp for MVP).
    - `ICalendarIntegration` — calendar reads and writes (Google Calendar for
      MVP).
    - `ITranscriptionIntegration` — speech-to-text. Per-tenant adapter
      selection, single configured implementation in MVP.
      Optionally a fourth, internal-only **chat completion factory**
      (`IChatCompletionFactory`) acts as the LLM port — see
      _AI Agent Runtime_.
15. No keyed DI for ports yet — one implementation per port. Introduce keyed
    DI when a second provider arrives for the same capability.
16. Never call external systems directly from business logic. Go through a
    port.

### AI safety

17. The model **never** executes side effects directly.
18. Every model-requested action passes through `ToolExecutionGateway`.
19. The backend **never** trusts model output directly. Validate with
    OpenAI Structured Outputs at the provider, plus a small set of
    post-deserialization checks.
20. Do not send the full conversation transcript to the model. Send the
    last 8 raw turns (see _Definition of "turn"_).
21. Do not hardcode model names. Resolve from the tenant's `agent_profile`.
22. The model receives the **full enabled tool catalog** for the tenant
    every turn. Tool selection logic is unnecessary at MVP scale (≤10
    tools). Add it when catalogs exceed ~10 tools per tenant.

### Multi-tenancy

23. Every tenant-owned table includes `tenant_id`.
24. Every tenant-owned query enforces tenant isolation through EF Core
    **global query filters**. Manual tenant resolution from request bodies
    is forbidden.
25. The customer phone number is **never** a tenant identifier. Resolve
    tenant from `(provider, provider_channel_id)`.

### Reliability

26. Every job handler is idempotent.
27. Webhooks must persist, enqueue, and return `200 OK` quickly (under
    ~500ms).

### API & data

28. All API routes versioned under `/v1/`. The single exception is
    `/health` which is **not** versioned (operational endpoint).
29. All identifiers are `Guid` generated as **GUID v7**
    (`Guid.CreateVersion7()`).
30. Database tables and columns use **singular `snake_case`**
    (`reservation`, `tool_execution`, `tenant_channel`, `created_at`).
31. Entity Framework property names use **PascalCase** in C#. Apply
    `EFCore.NamingConventions` with `UseSnakeCaseNamingConvention()` once at
    `AppDbContext` configuration to map automatically.
32. All timestamps stored as `timestamptz` and represented in C# as
    `DateTime` with `DateTimeKind.Utc`. Never use `DateTime.Now`. Always use
    `TimeProvider`.
33. Date/time fields where time-of-day matters separately use `DateOnly`
    and `TimeOnly` in entities and Mediator commands. JSON contracts (model
    output, HTTP wire) use ISO 8601 strings — convert at the boundary.
34. Enable nullable reference types and treat warnings as errors.

---

## Architecture

The default runtime shape:

- One **API** process (HTTP surface + webhook receiver).
- One **Worker** process (background jobs, agent execution, integration
  calls).
- PostgreSQL.
- Azure Storage Queues.
- Azure Blob Storage.
- OpenTelemetry Collector (optional locally; required in production).
- Aspire dashboard for local observability.

For very early local development, API and Worker may be collapsed into a
single process by registering the queue listener as a `BackgroundService` in
the API host. Split them when independent scaling is required. **No
microservices for the MVP.**

---

## Aspire Setup

### Hosting packages (in `AppHost`)

- `Aspire.Hosting.PostgreSQL`
- `Aspire.Hosting.Azure.Storage` — uses Azurite for local Blob and Queue
  emulation
- `Aspire.OpenAI` — registers the OpenAI client with logging, metrics, and
  resilience. Currently preview; the underlying `OpenAIClient` is stable.
  If the preview API changes, only the inside of
  `IChatCompletionFactory` needs to update — consumers stay untouched.

We use **OpenAI directly**, not Azure OpenAI. `Aspire.Azure.AI.OpenAI` is
intentionally **not** used. If a tenant later requires Azure OpenAI for data
residency, swap the provider behind `IChatCompletionFactory`.

### `AppHost/Program.cs` skeleton

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("appdb");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var queues  = storage.AddQueues("queues");
var blobs   = storage.AddBlobs("blobs");

var openai = builder.AddConnectionString("openai");

// Langfuse credentials are passed through to API and Worker as env vars.
var langfuseHost      = builder.AddParameter("langfuse-host");
var langfusePublicKey = builder.AddParameter("langfuse-public-key", secret: true);
var langfuseSecretKey = builder.AddParameter("langfuse-secret-key", secret: true);

builder.AddProject<Projects.Api>("api")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithReference(openai)
    .WithEnvironment("LANGFUSE_HOST",       langfuseHost)
    .WithEnvironment("LANGFUSE_PUBLIC_KEY", langfusePublicKey)
    .WithEnvironment("LANGFUSE_SECRET_KEY", langfuseSecretKey);

builder.AddProject<Projects.Worker>("worker")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithReference(openai)
    .WithEnvironment("LANGFUSE_HOST",       langfuseHost)
    .WithEnvironment("LANGFUSE_PUBLIC_KEY", langfusePublicKey)
    .WithEnvironment("LANGFUSE_SECRET_KEY", langfuseSecretKey);

builder.Build().Run();
```

### Client packages (in API and Worker)

- `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`
- `Aspire.Azure.Storage.Queues`
- `Aspire.Azure.Storage.Blobs`
- `Aspire.OpenAI`

### Resilience

Aspire client integrations configure resilience pipelines internally. **Do
not add Polly to the Aspire-wired clients.** Polly **is** used inside
adapters that build their own HTTP stack (custom Refit clients, raw
`HttpClient` for streaming/multipart) — for retries with exponential backoff
and jitter, timeouts, and circuit breakers.

### In-Memory Processing

Evaluate **ZLinq** for hot paths with intensive in-memory collection
processing. Use it when it provides clear performance or allocation benefits.
If it makes the code harder to read or adds unnecessary friction, prefer
standard LINQ.

---

## LLM Observability (Langfuse)

LLM-specific tracing — prompts, completions, tool calls, token usage,
latency, estimated cost — is sent to **Langfuse** in addition to general
OpenTelemetry exports.

Langfuse exposes a native OTLP HTTP endpoint that understands the GenAI
semantic conventions emitted by Semantic Kernel. No Langfuse-specific SDK is
required.

### Configuration

Three values, injected by Aspire as environment variables:

- `LANGFUSE_HOST` — `https://cloud.langfuse.com` for cloud, or your
  self-hosted URL.
- `LANGFUSE_PUBLIC_KEY`
- `LANGFUSE_SECRET_KEY`

The OTLP endpoint is `{LANGFUSE_HOST}/api/public/otel/v1/traces`.
Authentication uses HTTP Basic with `public_key:secret_key` base64-encoded.

### Registration (API and Worker)

```csharp
var langfuseHost   = builder.Configuration["LANGFUSE_HOST"];
var langfusePub    = builder.Configuration["LANGFUSE_PUBLIC_KEY"];
var langfuseSecret = builder.Configuration["LANGFUSE_SECRET_KEY"];

var langfuseAuth = "Basic " + Convert.ToBase64String(
    Encoding.UTF8.GetBytes($"{langfusePub}:{langfuseSecret}"));

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Microsoft.SemanticKernel*")
        .AddSource("OpenAI.*")
        .AddSource("CeoAgent.*")          // application sources
        .AddOtlpExporter()                // general OTel (Aspire dashboard, Azure Monitor)
        .AddOtlpExporter("langfuse", o =>
        {
            o.Endpoint = new Uri($"{langfuseHost}/api/public/otel/v1/traces");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
            o.Headers  = $"Authorization={langfuseAuth}";
        }));
```

### Required trace attributes

Every LLM-related activity must carry, where applicable:

- `tenant_id`
- `conversation_id`
- `customer_id`
- `correlation_id`
- `channel_provider` (e.g. `whatsapp_cloud`)
- `model_name` (resolved from `agent_profile`)
- `prompt_version` (set by `PromptBuilder` when prompts change)

`AgentRunner` adds these as activity tags before invoking Semantic Kernel.

### What goes to Langfuse vs general OTel

| Signal                              | General OTel | Langfuse |
| ----------------------------------- | ------------ | -------- |
| HTTP request traces                 | Yes          | No       |
| Database spans                      | Yes          | No       |
| Queue spans                         | Yes          | No       |
| `Microsoft.SemanticKernel.*` spans  | Yes          | Yes      |
| `OpenAI.*` spans (chat completions) | Yes          | Yes      |
| Application logs                    | Yes          | No       |

The same span can be exported to both backends; Langfuse only displays the
GenAI-shaped ones.

### Privacy: prompt and completion content

The Semantic Kernel switch
`Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive`
controls whether prompt and completion **text** is included in traces. This
switch is **process-global** — it cannot be toggled per tenant.

Rule:

- In **production**, default to `false`. Token counts, latency, model name,
  and tool-call structure still flow through, which is enough for cost and
  performance analysis without PII exposure.
- For controlled debugging, enable it temporarily in a non-production
  environment and never on shared production processes.
- A future enhancement (post-MVP) is a custom `ActivityProcessor` that strips
  prompt/completion content per tenant before export. Until then, the
  per-tenant claim is **not** something we offer.

---

## Core Modules

Initial modules:

- `Tenancy` — tenants, channels, integration credentials references, agent
  profiles, tool configuration per tenant.
- `Customers` — customer records keyed by `(tenant_id, channel_type,
external_customer_id)`.
- `Conversations` — conversations, messages, transcriptions, conversation
  state.
- `Agents` — prompt building, tool selection (trivial in MVP), model
  orchestration, tool gateway.
- `Reservations` — reservation entity and lifecycle.
- `Integrations` — port contracts and shared DTOs (no implementations).

Modules communicate **only** through Mediator commands and queries within
the same process. **No direct cross-module entity references.** When a
slice in module A needs data owned by module B, it dispatches a query
to module B.

Adapters live outside the modules under `Adapters/`.

---

### `Application/` vs `Modules/Features/` — the rule

- **`Modules/<X>/Features/<UseCase>/`** holds the **slice**: HTTP endpoint,
  Mediator command/query + handler, request/response models, validators.
  This is "controller-like" code that orchestrates business logic.
- **`Application/<X>/`** holds **stateful or non-trivial business logic**
  shared across slices and used by the Worker. `AgentRunner`,
  `PromptBuilder`, `ToolExecutionGateway`, `ConversationStateApplier` live
  here. Slice handlers and tool handlers depend on `Application/`, not the
  other way around.
- **`Tools/`** holds `IToolHandler` implementations. Each tool handler may
  internally dispatch a Mediator command into the appropriate module —
  that is how a tool reuses a slice's logic without duplicating it. See
  _Tools and ToolExecutionGateway_.

---

## Vertical Slice Architecture Rules (Pragmatic Edition)

Rules:

- Endpoint files use the suffix `Endpoint`.
- A slice typically contains: an `Endpoint`, a Mediator `Command`/`Query` +
  `Handler`, and a `Models` file. Split files only when readability requires
  it — there is no minimum or maximum file count.
- Shared DTOs live at the feature level when reused across slices in the
  same module.
- Do **not** register services directly in `Program.cs`. Use
  `<Module>ServiceRegistrations` extension classes per module.

The endpoint:

1. Receives the request.
2. Lets FastEndpoints + FluentValidation validate it (status `400` on
   failure).
3. Maps to a Mediator command (direct construction; Mapperly only when
   shapes diverge).
4. Sends the command via Mediator.
5. Maps the response back if needed.
6. Returns.

Business logic lives in the handler.

---

## API Layer

Use **FastEndpoints** for HTTP, **Mediator** for command/query dispatch.

- All routes versioned under `/v1/`. Health endpoint at `/health` (not
  versioned).
- Validation runs **before** the handler, via FluentValidation integrated
  with FastEndpoints.
- Mapping uses **Mapperly only when needed** (see rule 6 in
  _Non-Negotiable Rules_).
- Errors are translated to `ProblemDetails` by a single global
  `IExceptionHandler`.
- `CancellationToken` is propagated through every call.

### Standard status codes

| Status | Meaning                                               |
| ------ | ----------------------------------------------------- |
| 400    | Invalid request shape or validation error             |
| 401    | Unauthenticated                                       |
| 403    | Authenticated but lacks permission                    |
| 404    | Not found, including cross-tenant hidden resources    |
| 409    | Concurrency conflict (`DbUpdateConcurrencyException`) |
| 422    | Semantic / domain rule violation                      |
| 499    | Client closed request (cancelled)                     |
| 500    | Unexpected server error                               |
| 503    | Downstream dependency unavailable                     |

---

## Error Handling

Three layers, deliberately simple.

### 1. Validation errors (FluentValidation)

Run before the handler. FastEndpoints serializes them into a
`ProblemDetails`-shaped response with an `errors` dictionary. Status: **400**.

### 2. Business rule violations

Throw a small set of dedicated exceptions where the rule is violated:

```csharp
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} {key} not found");

public sealed class BusinessRuleException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class IntegrationException(string provider, string message,
    Exception? inner = null) : Exception(message, inner)
{
    public string Provider { get; } = provider;
}
```

Rule for choosing between FluentValidation and `BusinessRuleException`:

- **FluentValidation** is for **request shape**: required fields, ranges
  visible from the request alone, regex, length. Returns **400**.
- **`BusinessRuleException`** is for rules that require domain state to
  evaluate: working hours, capacity, "already cancelled", tenant-disabled
  tool, etc. Returns **422**.

### 3. Unexpected errors

A single global `IExceptionHandler` catches everything, logs with
`correlation_id` and `trace_id`, and writes a `ProblemDetails` response with
the appropriate status. Internals never leak to the client.

| Exception                                        | HTTP                           |
| ------------------------------------------------ | ------------------------------ |
| `FluentValidation.ValidationException`           | 400 (handled by FastEndpoints) |
| `NotFoundException`                              | 404                            |
| `BusinessRuleException`                          | 422                            |
| `DbUpdateConcurrencyException`                   | 409 (`concurrency_conflict`)   |
| `IntegrationException` (in request)              | 503                            |
| `OperationCanceledException` (client disconnect) | 499                            |
| anything else                                    | 500                            |

### Required `ProblemDetails` extensions

Every error response includes:

- `traceId` — from `Activity.Current?.TraceId`
- `correlationId` — from middleware that reads or generates `X-Correlation-Id`
- `tenantId` — when the request is tenant-scoped

When `Result<T, Error>` and a richer error model start paying for themselves,
introduce them gradually. For now, exceptions plus FluentValidation are
enough.

---

## Multi-Tenancy

Use **shared database, shared schema, with `tenant_id` discriminator**.

Rules:

- The customer phone number is **never** a tenant identifier.
- Resolve tenant from the receiving channel:
  - For WhatsApp Cloud: `metadata.phone_number_id` from the webhook payload.
  - For any future channel: the channel's stable provider-side identifier.
  - The lookup is `tenant_channel WHERE provider = ? AND provider_channel_id = ?`.
- Use `messages[0].from` or `contacts[0].wa_id` to identify the customer
  within that tenant (for WhatsApp).
- Every tenant-owned table includes `tenant_id`.
- Every tenant-owned query filters by `tenant_id` via EF Core global query
  filters. Never rely on developers remembering to add the filter manually.
- Return `404` when a resource exists but belongs to another tenant — never
  reveal cross-tenant existence.

### Tenant context

A request middleware sets the ambient `ITenantContext` from one of two
sources:

- For webhook routes: resolved from the channel's
  `(provider, provider_channel_id)` after signature verification.
- For admin routes: read from a header (`X-Tenant-Id`) authorized by the
  static admin API key.

EF Core global query filters read the ambient `ITenantContext` and append
`WHERE tenant_id = @currentTenant` to every tenant-owned query. Manual
tenant resolution from request bodies is forbidden.

---

## Authentication and Authorization (MVP)

Keycloak is **not** used for MVP.

### Admin endpoints

Protected by a static API key in the `X-Admin-Api-Key` header, validated by
a custom **`AuthenticationHandler<>`** registered in the ASP.NET Core auth
pipeline. The key is stored as an Aspire/Azure secret. Endpoints under
`/v1/admin/...` carry an `[Authorize(AuthenticationSchemes = "AdminApiKey")]`
attribute (or the FastEndpoints equivalent).

Admin endpoints handle tenant onboarding, channel configuration, integration
credentials registration, agent profile management, and tool enablement.

### Webhook endpoints

No auth header. Authorization is established by HMAC signature verification
of the channel payload (see _Webhook Security_).

### Future

Multi-user dashboards or self-service portals will introduce JWT-based
authentication. Keycloak, Auth0, or ASP.NET Identity are all reasonable
choices. Plan for it; do not implement it now.

---

## Tenant Onboarding

For MVP, onboarding is **manual** by a platform operator hitting admin
endpoints.

Steps:

1. Create the `tenant` row.
2. Register the channel: `tenant_channel` row with `provider`,
   `provider_channel_id`, `metadata` (jsonb for provider-specific extras like
   `phone_number_id`, `business_account_id`, …), and a credentials reference.
3. Configure the agent profile (model, prompt overrides, language, timezone,
   working hours, capacity).
4. Configure the calendar integration credentials reference.
5. Enable tools for the tenant: insert `tenant_tool` rows for each native
   tool the tenant should expose (`create_reservation`, `check_availability`,
   `cancel_reservation`, `request_human_handoff`).
6. Smoke-test inbound and outbound flows.

All steps are scripted as admin endpoints under `/v1/admin/...`, available
only to callers with the admin API key.

---

## Channel Inbound Resolution Flow

Generalized for any channel (WhatsApp Cloud is the only MVP implementation):

```text
1. Webhook receives inbound channel payload.
2. Verify provider signature (see Webhook Security).
3. Extract (provider, provider_channel_id) — for WhatsApp this is
   ("whatsapp_cloud", metadata.phone_number_id).
4. Resolve tenant via tenant_channel.
5. Extract customer identifier from the provider payload — for WhatsApp,
   messages[0].from or contacts[0].wa_id.
6. Find or create customer by (tenant_id, channel_type, external_customer_id).
7. Find or create OPEN conversation for (tenant_id, customer_id, channel_type).
8. Persist inbound message idempotently using provider_message_id.
9. Enqueue ProcessIncomingMessageJob to Azure Storage Queue.
10. Return 200 OK.
```

The customer phone number is **never** used to resolve the tenant. The same
customer may message multiple businesses on the platform.

WhatsApp Web is not supported. Production must use WhatsApp Business Cloud
API.

### Definition of "open conversation"

A conversation is **open** when its `status = 'open'`. A conversation is
**closed** when it transitions to `closed`. The MVP closes conversations
under exactly these conditions:

1. **Inactivity timeout** — no inbound or outbound message for **24 hours**.
   Enforced by a periodic background job (`CloseInactiveConversationsJob`)
   that runs every 15 minutes.
2. **Explicit closure** by staff via
   `POST /v1/conversations/{id}/close`.

Closure resets `conversation_state` (cleared) but preserves all messages and
tool execution history.

A handed-off conversation (`status = 'handed_off'`) is **not** closed
automatically — staff must close it explicitly. Inactivity timeout does not
apply to `handed_off` conversations.

---

## Webhook Security

Every inbound channel webhook must be verified before any processing.

### WhatsApp Cloud signature verification

WhatsApp Cloud sends `X-Hub-Signature-256: sha256=<hex>`. The signature is
**HMAC-SHA256** of the **raw, unmodified request body** using the App Secret.

Required behavior:

1. Read the request body **as raw bytes**, not as JSON. FastEndpoints must
   be configured to retain the raw payload (`HttpRequest.Body` buffered or
   read into a `byte[]` via a pre-processor).
2. Compute `HMAC-SHA256(rawBody, appSecret)` and hex-encode.
3. Compare against the value in `X-Hub-Signature-256` using **constant-time
   comparison** (`CryptographicOperations.FixedTimeEquals`).
4. On mismatch, log and respond **401**. Do not enqueue. Do not return
   details.
5. Verification middleware/pre-processor runs **before** body
   deserialization.

### Replay protection

- Persist `provider_message_id` with a unique constraint on
  `(tenant_id, channel_type, provider_message_id)`.
- A duplicate insert returns **200 OK** without re-enqueuing.

---

## Conversation Storage and History

The platform owns the durable conversation state.

PostgreSQL stores:

- conversation
- message (text, transcribed audio, system, tool_call, tool_result)
- conversation_state (current short-lived state for the active interaction)
- reservation
- tool_execution
- audio_asset (blob URL + metadata)

Azure Blob Storage stores:

- inbound voice notes
- outbound TTS-generated audio

The model receives only:

- platform system prompt
- tenant context (brand voice, language, timezone, current local date/time,
  branch info)
- last 8 raw turns of the conversation
- the enabled tool catalog for the tenant

Full conversation history is retained for auditability but is **not** sent
to the model in full. No rolling summary, no customer memory profile
(deferred to post-MVP).

### Definition of "turn"

For the purpose of "send the last 8 turns to the model":

A **turn** is a single role-tagged entry in the conversation, where the role
is one of:

- `user` — an inbound customer message (text, or transcribed voice note).
- `assistant` — an outbound agent message (the `assistantMessage` of a
  `AgentTurnResult`).
- `tool_call` — the agent's request to invoke a tool, serialized from the
  `toolCallRequest` of an `AgentTurnResult`.
- `tool_result` — the structured outcome returned by `ToolExecutionGateway`.

The **last 8 turns** = the last 8 entries in chronological order, regardless
of role mix. The platform system prompt and tenant context are **not**
counted as turns; they are prepended on every model call.

System notes (handoff triggered, conversation reopened, etc.) are stored as
`system` rows but are **not** counted as turns and are **not** sent to the
model.

---

## Conversation State

Short-lived state for the current interaction:

```json
{
  "intent": "reservation_request",
  "date": "2026-05-10",
  "time": "20:00",
  "partySize": 4,
  "customerName": "Karina",
  "missingFields": ["confirmation"],
  "waitingForConfirmation": true,
  "language": "es"
}
```

This is **not** permanent customer memory. It is reset when the conversation
closes (see _Definition of "open conversation"_).

---

## AI Agent Runtime

Use **Semantic Kernel** for prompt execution and chat-completion abstraction.
The provider is **OpenAI direct** (not Azure OpenAI), wired through
`Aspire.OpenAI` so the underlying `OpenAIClient` benefits from Aspire's
logging, metrics, and resilience.

**Tool calling is not delegated to SK function calling.** All tool requests
come back inside `AgentTurnResult.ToolCallRequest` and are executed through
`ToolExecutionGateway`. This keeps the gateway as the single chokepoint for
side effects.

### Per-tenant model

Each tenant's `agent_profile` declares the model. There is one model per
tenant — no escalation tier. If the chosen model fails to produce valid
structured output after **one retry**, the agent triggers a human handoff.

Model names are never hardcoded. They live in `agent_profile.model_name`.

### Client registration (API and Worker)

```csharp
// Register the OpenAI client through Aspire — picks up connection string
// "openai" from AppHost, including API key and (optionally) base URL.
builder.AddOpenAIClient("openai");

// Register Semantic Kernel and the per-tenant chat completion factory.
// The model id is NOT bound at DI time — it is resolved per request from
// the tenant's agent_profile by IChatCompletionFactory.
builder.Services.AddKernel();
builder.Services.AddSingleton<IChatCompletionFactory, OpenAIChatCompletionFactory>();
```

```csharp
public interface IChatCompletionFactory
{
    IChatCompletionService ForModel(string modelId);
}

internal sealed class OpenAIChatCompletionFactory(OpenAIClient client)
    : IChatCompletionFactory
{
    public IChatCompletionService ForModel(string modelId)
        => new OpenAIChatCompletionService(modelId, client);
}
```

`AgentRunner` resolves the tenant's `agent_profile`, calls
`factory.ForModel(profile.ModelName)`, and uses the returned chat service for
the turn. If a tenant later needs Azure OpenAI for residency reasons, only
this factory's implementation changes — consumer code stays put.

### Required experimental switches

Enable the GenAI OpenTelemetry semantic conventions emitted by Semantic
Kernel:

```csharp
AppContext.SetSwitch(
    "Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnostics", true);

// Process-global. Default to false in production. See the Privacy section
// in LLM Observability.
AppContext.SetSwitch(
    "Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive",
    builder.Configuration.GetValue<bool>("Telemetry:EnableSensitiveLLMContent"));
```

### Agent loop (multi-step turn handling)

A single inbound user message can require multiple model invocations
(model → tool call → model → tool call → … → final assistant message).
The Worker orchestrates this loop inside `ProcessIncomingMessageJob`:

```text
loop iteration:
  1. Build prompt (system + tenant context + last 8 turns + tool catalog).
  2. Call model.
  3. Parse AgentTurnResult.
  4. If toolCallRequest != null:
        - Execute via ToolExecutionGateway.
        - Append tool_call and tool_result turns.
        - Increment iteration counter.
        - If iteration counter >= MAX_AGENT_LOOP (default 5), trigger
          RequestHumanHandoff with severity="warning",
          reason="agent_loop_exhausted".
        - Continue loop.
  5. If assistantMessage != null:
        - Persist as assistant turn.
        - Hand off to SendOutboundMessageJob (or send inline).
        - Exit loop.
  6. If handoffRecommendation != null:
        - Trigger handoff.
        - Exit loop.
```

`MAX_AGENT_LOOP` is configurable per tenant in `agent_profile`
(`max_loop_iterations`), default `5`.

This loop is **inside** the `ProcessIncomingMessageJob` handler (single
queue message → single agent loop). Tool execution uses `ToolExecutionGateway`
synchronously within the loop. `ExecuteToolCallJob` and
`SynthesizeAudioJob` exist as separate pipelines for **out-of-band**
operations (e.g., long-running tools or post-message audio generation), not
for splitting one inbound user message into multiple queue messages.

---

## Prompt Composition

`PromptBuilder` composes the final context from three layers:

1. **Platform system prompt** — safety rules, output schema instructions,
   tool-catalog contract, format reminders. Not editable by tenants.
2. **Tenant context** — brand voice, language, tone, agent name, timezone,
   current local date/time, branch info if any, working hours.
3. **Last 8 raw turns** — chronological, role-tagged.

`PromptBuilder` is the only component allowed to compose the final model
context. Adding a fourth layer (long-term memory, summary, etc.) is a
deliberate post-MVP decision.

Tenant prompts must not override platform safety, tenant isolation, tool
execution, or privacy rules. The platform system prompt explicitly reasserts
these constraints regardless of tenant configuration.

---

## Structured Model Output

The backend never trusts model output directly.

### Layer 1 — Provider-side enforcement

Use **OpenAI Structured Outputs**
(`response_format: { type: "json_schema", strict: true }`) configured
through Semantic Kernel's `OpenAIPromptExecutionSettings.ResponseFormat`.
This guarantees the model cannot return malformed JSON.

### Layer 2 — Source-generated deserialization

All model output records are immutable C# `record` types registered in a
`JsonSerializerContext`:

```csharp
[JsonSerializable(typeof(AgentTurnResult))]
[JsonSerializable(typeof(ConversationStatePatch))]
[JsonSerializable(typeof(ToolCallRequest))]
[JsonSerializable(typeof(HandoffRecommendation))]
internal partial class AgentJsonContext : JsonSerializerContext;
```

No reflection at runtime. AOT-friendly.

### Layer 3 — Lightweight semantic checks

A handful of inline checks on the deserialized record:

- `partySize` between 1 and the tenant's configured maximum.
- `date >= today` in tenant timezone.
- `time` parseable as `HH:mm`.
- `toolKey` exists in the tenant's enabled tool set
  (`ITenantToolRegistry`).

If any check fails, retry the model call **once**. If it fails again,
trigger a human handoff (`severity = "warning"`,
`reason = "model_validation_failed"`).

### Canonical schemas

```csharp
public sealed record AgentTurnResult(
    string AssistantMessage,
    ConversationStatePatch? ConversationStatePatch,
    ToolCallRequest? ToolCallRequest,
    HandoffRecommendation? HandoffRecommendation);

public sealed record ConversationStatePatch(
    string? Intent,
    Datetime? Date,
    int? PartySize,
    string? CustomerName,
    string? Branch,
    string? Language,
    bool? WaitingForConfirmation,
    bool? WaitingForAvailability,
    IReadOnlyList<string>? MissingFields);

public sealed record ToolCallRequest(
    string ToolKey,
    JsonElement Parameters,
    string? Reason);

public sealed record HandoffRecommendation(
    string Reason,
    string Severity);              // "info" | "warning" | "critical"
```

Only `AssistantMessage` is sent to the customer. The other fields are
internal.

`Date` and `Time` are exchanged with the model as ISO strings (because
Structured Outputs handles strings cleanly). The application converts to
`DateOnly` / `TimeOnly` at the boundary in
`ConversationStateApplier`.

---

## Tools and ToolExecutionGateway

### Dynamic tool registry — the extensibility backbone

The MVP ships with **four native** tools (see _Canonical MVP Tools_), but
the contract is designed from day one to support **dynamic tools registered
per tenant**, including future kinds such as:

- REST API calls into a tenant's own backend.
- Excel sheet lookups (read-only initially).
- SQL queries against a tenant's database.
- POS / CRM integrations.

The contract is the same for all of them.

```csharp
public interface IToolHandler
{
    string ToolKey { get; }
    string Description { get; }                  // shown to the model
    JsonElement ParametersJsonSchema { get; }    // for Structured Outputs
    bool RequiresExplicitConfirmation { get; }   // safety hint

    Task<ToolResult> ExecuteAsync(
        ToolExecutionContext ctx,
        JsonElement parameters,
        CancellationToken ct);
}

public sealed record ToolExecutionContext(
    Guid TenantId,
    Guid ConversationId,
    Guid CustomerId,
    Guid ExecutionId,
    DateTimeOffset NowInTenantTz);

public abstract record ToolResult
{
    public sealed record Success(JsonElement Output) : ToolResult;
    public sealed record Denied(string Code, string Message) : ToolResult;
    public sealed record Failure(string Code, string Message) : ToolResult;
}
```

```csharp
public interface ITenantToolRegistry
{
    Task<IReadOnlyList<ToolDescriptor>> GetEnabledToolsAsync(
        Guid tenantId, CancellationToken ct);
}

public sealed record ToolDescriptor(
    string ToolKey,
    string Description,
    JsonElement ParametersJsonSchema);
```

```csharp
public interface IToolHandlerFactory
{
    Task<IToolHandler?> ResolveAsync(
        Guid tenantId, string toolKey, CancellationToken ct);
}
```

### How native tools register

Each native handler is registered as a singleton keyed by its `ToolKey`:

```csharp
services.AddKeyedSingleton<IToolHandler, CreateReservationToolHandler>("create_reservation");
services.AddKeyedSingleton<IToolHandler, CheckAvailabilityToolHandler>("check_availability");
services.AddKeyedSingleton<IToolHandler, CancelReservationToolHandler>("cancel_reservation");
services.AddKeyedSingleton<IToolHandler, RequestHumanHandoffToolHandler>("request_human_handoff");
```

`tenant_tool` rows control which tools are exposed to the model for each
tenant. `IToolHandlerFactory` resolves a handler by checking that
`(tenant_id, tool_key)` is enabled and then pulling the keyed singleton.

### How dynamic tools will register (post-MVP)

A `tenant_tool` row carries a `kind` column (`native | rest_api | excel | sql`)
and a `config` jsonb. For non-native kinds, `IToolHandlerFactory` instantiates
a generic handler (e.g., `RestApiToolHandler`) parameterized by the config:
endpoint URL, auth reference, request schema, response shape. The
gateway, the agent loop, the prompt builder, and the canonical schemas do
**not** change to accommodate new tool kinds.

This is the deliberate extensibility point.

### `ToolExecutionGateway`

`ToolExecutionGateway` is a thin component that:

- Validates that the tool is enabled for the tenant (via `ITenantToolRegistry`).
- Validates that the conversation belongs to the tenant.
- Resolves the right `IToolHandler` (via `IToolHandlerFactory`).
- Logs the request and result to `tool_execution`.
- Returns a typed result.

```csharp
public sealed class ToolExecutionGateway(
    AppDbContext db,
    IToolHandlerFactory factory,
    ITenantContext tenant,
    TimeProvider clock,
    ILogger<ToolExecutionGateway> logger)
{
    public async Task<ToolResult> ExecuteAsync(
        Guid conversationId,
        ToolCallRequest req,
        CancellationToken ct)
    {
        var handler = await factory.ResolveAsync(tenant.TenantId, req.ToolKey, ct);
        if (handler is null)
            return new ToolResult.Denied("tool_not_enabled", "Tool not enabled for tenant");

        var execId = Guid.CreateVersion7();
        await LogIntentAsync(execId, conversationId, req, ct);

        var ctx = new ToolExecutionContext(
            tenant.TenantId, conversationId, /*customerId*/ default, execId,
            clock.GetUtcNow().ToOffset(/* tenant tz */ TimeSpan.Zero));

        var result = await handler.ExecuteAsync(ctx, req.Parameters, ct);
        await LogOutcomeAsync(execId, result, ct);
        return result;
    }
}
```

A model requesting a tool call is **not** authorization to execute it. Each
handler enforces its own business rules (capacity, working hours,
confirmation required, etc.).

### Tool ↔ Mediator command relationship

Native tools that change platform state (e.g., `CreateReservationToolHandler`)
**must dispatch a Mediator command** internally rather than duplicate
business rules:

```csharp
internal sealed class CreateReservationToolHandler(
    ISender mediator, ILogger<CreateReservationToolHandler> logger) : IToolHandler
{
    public string ToolKey => "create_reservation";
    /* ... schema, description ... */

    public async Task<ToolResult> ExecuteAsync(
        ToolExecutionContext ctx, JsonElement parameters, CancellationToken ct)
    {
        var input = JsonSerializer.Deserialize<CreateReservationInput>(parameters,
            AgentJsonContext.Default.CreateReservationInput)!;

        try
        {
            var result = await mediator.Send(
                new CreateReservationCommand(/* mapped fields */), ct);
            return new ToolResult.Success(/* serialized result */);
        }
        catch (BusinessRuleException ex)
        {
            return new ToolResult.Denied(ex.Code, ex.Message);
        }
    }
}
```

This is how a tool reuses the slice handler's logic without duplication.

---

## Canonical MVP Tools

Four tools are mandatory for MVP. Their schemas are normative.

### `create_reservation`

```csharp
public sealed record CreateReservationInput(
    Datetime Date,
    int PartySize,
    string CustomerName,
    string? SeatingPreference,
    string? BranchId,
    bool ConfirmedByCustomer);

public sealed record CreateReservationOutput(
    Guid ReservationId,
    string Status,                    // "confirmed" | "pending"
    string ExternalCalendarEventId);
```

Validation:

- `ConfirmedByCustomer == true` is required to execute.
- `(Date, Time)` not in the past, within tenant working hours.
- `PartySize` between 1 and tenant capacity max.

### `check_availability`

```csharp
public sealed record CheckAvailabilityInput(
    Datetime Date,
    int PartySize,
    string? BranchId);

public sealed record CheckAvailabilityOutput(
    bool Available,
    IReadOnlyList<string> AlternativeTimes);   // "HH:mm"
```

Read-only. Idempotent by definition.

### `cancel_reservation`

```csharp
public sealed record CancelReservationInput(
    Guid ReservationId,
    bool ConfirmedByCustomer,
    string? Reason);

public sealed record CancelReservationOutput(
    string Status);                  // "cancelled" | "already_cancelled"
```

`ConfirmedByCustomer == true` required.

### `request_human_handoff`

```csharp
public sealed record RequestHumanHandoffInput(
    string Reason,
    string Severity);                // "info" | "warning" | "critical"

public sealed record RequestHumanHandoffOutput(
    Guid HandoffId,
    string Status);                  // "notified"
```

Always allowed. Pauses autonomous replies on success.

---

## Human Handoff

Handoff is triggered when:

- The customer requests a person in chat (the agent detects intent and calls
  `request_human_handoff`).
- The agent decides it cannot safely complete the task (returns a
  `HandoffRecommendation`).
- **The two-failure rule fires** (see _Failure Counting Rules_).

Handoff effects:

- Conversation status set to `handed_off`.
- A customer-facing message is sent (text + TTS audio if the conversation is
  voice-based): _"A team member will be with you shortly."_
- Autonomous tool execution is paused. The Worker stops the agent loop for
  this conversation.
- Inactivity timeout closure does **not** apply while `handed_off`.
- Automation resumes only when staff explicitly re-enables the agent through
  `POST /v1/conversations/{id}/resume-automation`. On resume, conversation
  state is reset (because the human likely changed the world the agent's
  state was tracking).

### Failure Counting Rules

The "two consecutive failures" trigger is **per operation type, per
conversation, scoped to the current agent loop iteration plus the immediate
previous one**. In other words, the counter is local to the active turn,
not a lifetime counter.

The three operation types tracked:

- `model_call` — chat completion that fails or returns invalid structured
  output after one retry.
- `tool_execution` — `IToolHandler.ExecuteAsync` returns
  `ToolResult.Failure`.
- `outbound_send` — `IMessageChannelIntegration.SendTextAsync` /
  `SendAudioAsync` throws `IntegrationException` after the adapter's
  internal retries.

Counter logic:

- Each operation type maintains a counter on the current
  `ProcessIncomingMessageJob` invocation.
- The counter increments on each failure of that type within this job.
- On the **second** failure of the **same operation type**, the Worker
  triggers `request_human_handoff` with `severity = "warning"` and
  `reason = "{op_type}_repeated_failure"`. The agent loop stops.
- Failures of **different** operation types do **not** combine. Two model
  failures + one send failure = handoff (because of the model count). One
  model failure + one send failure = continue.
- Counters reset when the job ends (success, handoff, or DLQ).

This counter is in-memory of the running job. Cross-job persistence is not
needed because each job processes one inbound message; if the job fails
entirely, the queue's `dequeueCount` mechanism takes over (see
_Background Processing_).

---

## Integrations

Use Ports and Adapters with **four MVP ports**:

```csharp
public interface IMessageChannelIntegration
{
    string Provider { get; }   // "whatsapp_cloud", future: "telegram", etc.

    Task<SendResult> SendTextAsync(SendTextInput input, CancellationToken ct);
    Task<SendResult> SendAudioAsync(SendAudioInput input, CancellationToken ct);

    // Optional capabilities. Adapters that don't support them throw
    // NotSupportedException; consumers feature-detect via try/catch or
    // capability flags.
    Task<SendResult> SendInteractiveAsync(SendInteractiveInput input, CancellationToken ct);
    Task MarkAsReadAsync(string providerMessageId, CancellationToken ct);

    Task<Stream> DownloadMediaAsync(string mediaId, CancellationToken ct);
}

public interface ICalendarIntegration
{
    Task<CalendarEvent> CreateEventAsync(CreateEventInput input, CancellationToken ct);
    Task<bool> IsSlotAvailableAsync(DateTime startUtc, TimeSpan duration, CancellationToken ct);
    Task<bool> CancelEventAsync(string externalEventId, CancellationToken ct);
}

public interface ITranscriptionIntegration
{
    Task<TranscriptionResult> TranscribeAsync(
        Stream audio, string contentType, string? languageHint, CancellationToken ct);
}

public interface ISpeechSynthesisIntegration
{
    Task<SynthesisResult> SynthesizeAsync(
        string text, string voiceProfile, string language, CancellationToken ct);
}
```

One implementation per port for MVP:

- `WhatsAppCloudAdapter` → `IMessageChannelIntegration`.
- `GoogleCalendarAdapter` → `ICalendarIntegration`.
- `OpenAITranscriptionAdapter` (Whisper) → `ITranscriptionIntegration`.
- `OpenAISpeechAdapter` (TTS) → `ISpeechSynthesisIntegration`.

Inject directly. **No keyed DI for ports yet.** When a second provider
arrives for the same capability (e.g., Outlook Calendar), introduce keyed
DI and a tenant-driven selector.

Rules:

- Adapters depend on integration contracts, not on domain internals.
- Adapters implement timeouts, retries with exponential backoff and jitter,
  and circuit breakers where appropriate. **Polly** is used here, not in the
  Aspire-wired clients.
- Adapters propagate `CancellationToken`.
- Adapters convert provider-specific exceptions into `IntegrationException`
  with a `provider` field. They never leak raw HTTP exceptions upward.
- Use **Refit** for typed HTTP clients. Use raw `HttpClient` only for
  streaming, multipart uploads, or specialized resilience needs (e.g.,
  audio downloads).

---

## Audio Pipelines

### Inbound (transcription)

WhatsApp voice notes are transcribed before reaching the model. The model
never receives raw audio.

Rules:

- Store the audio file in Azure Blob Storage with a key like
  `tenants/{tenantId}/inbound/{conversationId}/{messageId}.ogg`.
- Store the transcription text on the `message` row and the blob URL on
  `audio_asset`.
- Do not convert OGG/Opus to WAV unless the selected provider requires it
  (Whisper handles OGG/Opus directly).
- Pass the customer's known language as a hint when available.

Failure policy:

- **One attempt** with the configured transcription provider.
- On failure, **do not retry** and **do not fall back** to a secondary
  provider.
- Trigger `request_human_handoff` with `severity = "warning"` and
  `reason = "transcription_failed"`.

Transcription runs in the Worker, never inside the webhook handler.

### Outbound (TTS)

When the active conversation is voice-based (the inbound message was a voice
note) or the tenant's `agent_profile.voice_reply_enabled = true`, the
assistant message is sent both as **text** and as **audio**.

Pipeline:

```text
1. AgentRunner produces assistantMessage.
2. SendOutboundMessageJob enqueues:
   - SendText via IMessageChannelIntegration.SendTextAsync
   - SynthesizeAudioJob, which:
     a. calls ISpeechSynthesisIntegration.SynthesizeAsync
     b. uploads result to Azure Blob Storage
     c. calls IMessageChannelIntegration.SendAudioAsync
```

TTS failure does **not** block the text send. If TTS fails, the text reply
still goes out and a warning is logged. The customer is not handed off
purely for a TTS failure (the text message is sufficient).

The configured TTS voice profile per tenant lives in
`agent_profile.tts_voice_profile`. Tenants may have voice replies disabled
entirely.

---

## Background Processing

Webhook handlers must be fast. **Long work belongs to the Worker.**

Webhook path:

```text
1. Verify signature.
2. Resolve tenant.
3. Identify customer.
4. Persist inbound message idempotently.
5. Enqueue ProcessIncomingMessageJob to Azure Storage Queue.
6. Return 200 OK.
```

Worker responsibilities:

- transcription
- agent loop (model calls + tool executions)
- TTS synthesis
- outbound message sending
- integration retries
- handoff notifications
- inactivity-based conversation closure (`CloseInactiveConversationsJob`)

Job handlers must be idempotent. Reprocessing must not double-create
reservations, double-send messages, double-create calendar events, or
double-charge customers.

### Failure handling without an Outbox

For MVP, side effects to external systems happen **inside the Worker job**,
not via a dedicated outbox table.

Pattern for each external call:

1. Try the external call (with the adapter's internal Polly policy).
2. If it fails, the failure increments the appropriate
   _Failure Counting Rules_ counter for the active turn.
3. The job itself can retry up to a small number of times via the queue's
   `dequeueCount` mechanism for transient errors (e.g., DB hiccup).
4. After **two consecutive failures of the same operation type** (per
   _Failure Counting Rules_), the Worker triggers human handoff and stops
   retrying for this turn.
5. Poison messages (very high `dequeueCount`, e.g., > 5) are moved to a
   dead-letter queue and an alert is emitted.

When inconsistencies between local state and external systems start
appearing in production, that is the signal to introduce the Outbox pattern.
Not before.

---

## Queue Ordering Strategy

Azure Storage Queues do **not** guarantee FIFO. Two messages from the same
conversation can be processed in parallel.

For MVP, configure the Worker to process messages with **concurrency of 1
per queue**. With low message volume during the first months, this is
sufficient and avoids ordering bugs without extra machinery.

When throughput requires more concurrency, the next step is the
**PostgreSQL advisory transaction lock** keyed by `conversation_id`:

```csharp
await db.Database.ExecuteSqlInterpolatedAsync(
    $"SELECT pg_advisory_xact_lock(hashtext({conversationId.ToString()}))",
    ct);
```

Beyond that, migrate to Azure Service Bus Sessions with
`SessionId = conversation_id`.

---

## Idempotency

For MVP, idempotency is enforced at three specific places:

| Origin                       | Key                                                                                               | Storage                                                                       |
| ---------------------------- | ------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| WhatsApp inbound webhook     | `provider_message_id`                                                                             | Unique constraint on `(tenant_id, channel_type, provider_message_id)`         |
| Tool call to Google Calendar | `request_id` derived from `(tenant_id, conversation_id, tool_key, params_canonical_json)` SHA-256 | Passed to Google Calendar's idempotency parameter; logged in `tool_execution` |
| Outbound message send        | `client_message_id` derived from `(tenant_id, conversation_id, message_id)`                       | Passed to WhatsApp Cloud's `biz_opaque_callback_data` and stored in `message` |

Before any side effect, the handler checks the appropriate key. If the
operation already completed, it returns the persisted result.

Other forms of idempotency (admin POST headers, hash-based dedup of business
operations) are deferred until needed.

---

## Naming Conventions

### Mediator commands and queries

- Commands end in `Command`: `CreateReservationCommand`,
  `CancelReservationCommand`, `EnableTenantChannelCommand`.
- Queries end in `Query`: `GetReservationByIdQuery`, `ListReservationsQuery`.

### Job messages

- Suffix `Job`: `ProcessIncomingMessageJob`, `ExecuteToolCallJob`,
  `TranscribeAudioJob`, `SynthesizeAudioJob`,
  `SendOutboundMessageJob`, `CloseInactiveConversationsJob`.

### Folder placement

- Mediator commands and queries live inside their slice folder.
- Jobs live in `Worker/Pipelines/<Pipeline>/<JobName>.cs`.
- Tool handlers live in `Tools/<Area>/<ToolKey>ToolHandler.cs`.

---

## Data Access Rules

Use PostgreSQL with EF Core directly.

- No generic repository.
- No custom Unit of Work — `SaveChangesAsync` is the commit.
- Use EF Core directly inside Mediator handlers.
- Use `AsNoTracking()` for read-only queries.
- Project directly to DTOs with `.Select(...)`.
- Avoid `.Include()` as a default; use it only when the full graph is
  needed.
- Use `AsSplitQuery()` when joins create cartesian explosion.
- Prefer `ExecuteUpdateAsync()` and `ExecuteDeleteAsync()` for simple
  updates and deletes.
- Prefer keyset pagination over offset pagination.

State changes are made directly on entities — no aggregate methods, no
domain events, no `Result<T, Error>`. Encapsulate non-trivial multi-field
rules in methods on the entity when it improves readability:

```csharp
public sealed class Reservation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public int PartySize { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime? CancelledAt { get; set; }            // UTC
    public uint RowVersion { get; set; }                  // xmin

    public bool TryCancel(string? reason, TimeProvider clock)
    {
        if (Status == ReservationStatus.Cancelled) return false;
        Status = ReservationStatus.Cancelled;
        CancelledAt = clock.GetUtcNow().UtcDateTime;
        return true;
    }
}
```

---

## EF Core Migrations

- **One** `AppDbContext` for the whole solution.
- Code-first migrations stored in `Infrastructure/Persistence/Migrations/`.
- Migrations run automatically on startup **only in non-production
  environments**.
- Production migrations are applied via a dedicated CLI step in the
  deployment pipeline (`dotnet ef database update`), gated by approval.
- Each migration is named `{yyyyMMddHHmmss}_{Description}` (the default
  EF Core format). Example: `20260508153012_AddTenantChannel`.
- Migrations must be **forward-only**. Down migrations exist for local
  rollback only — never relied on in production.

---

## Database Modeling Rules

- All identifiers are `Guid` generated as **GUID v7** via
  `Guid.CreateVersion7()`.
- Use **singular `snake_case`** for tables (`reservation`, `tool_execution`,
  `tenant_channel`).
- Use **`snake_case`** for columns.
- Entity Framework property names use **PascalCase** in C#. Map to snake_case
  via `EFCore.NamingConventions`:

  ```csharp
  options.UseSnakeCaseNamingConvention();
  ```

- Every tenant-owned table includes `tenant_id`, `created_at`, `updated_at`.
- All timestamps are `timestamptz` and represented as `DateTime` (UTC) in
  C#.
- Never use `DateTime.Now`. Use `TimeProvider`.
- Every foreign key is explicit.
- Persist enums as strings (`HasConversion<string>()`).
- Use `jsonb` only for genuinely flexible payloads (tool parameters,
  conversation state, channel metadata). Stable queryable fields are real
  columns.
- Use `IEntityTypeConfiguration<T>` classes in
  `Infrastructure/Persistence/Configurations/`. The `AppDbContext` only
  declares `DbSet<T>` properties and applies all configurations via
  `modelBuilder.ApplyConfigurationsFromAssembly(...)`.

### Mandatory minimum indexes

Every tenant-owned table has at minimum:

- `(tenant_id, created_at DESC)` — for tenant-scoped recent-first listings.
- `(tenant_id, <natural_lookup_field>)` — when lookups by a business key
  exist (e.g., `(tenant_id, provider_message_id)` on `message`).

Add more indexes only based on observed query patterns.

---

## Entity Navigation Rules

- Never use `virtual` navigation properties.
- Lazy loading proxies are not allowed.
- Navigation loading is explicit (`Include` only when full graph is needed,
  otherwise project to DTOs).
- Collection navigation uses `ICollection<T>` and is initialized at
  declaration.

```csharp
public sealed class Conversation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
```

---

## Reservation Rules (Non-DDD)

The `Reservation` entity is a normal EF Core entity with public setters and
methods where rules apply. Rules to enforce in handlers and entity methods:

- Cannot create a reservation outside tenant working hours.
- Cannot create a reservation in the past.
- Cannot confirm without all required fields (date, time, partySize,
  customerName, `ConfirmedByCustomer == true`).
- Cannot double-book the same `external_calendar_event_id` (unique
  constraint in DB).
- Cannot cancel an already cancelled reservation (handled in `TryCancel`).
- Party size respects tenant capacity.
- Rescheduling appends a `reservation_audit` row.

These live in `CreateReservationCommandHandler`,
`CancelReservationCommandHandler`, and on the entity itself, not in an
aggregate.

### Optimistic concurrency

`Reservation` carries a `RowVersion` (`xmin` mapped via `IsRowVersion()`) to
prevent double-booking races between the AI agent and human staff:

```csharp
builder.Property(r => r.RowVersion)
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```

`DbUpdateConcurrencyException` is mapped to **HTTP 409** (or to a
`ToolResult.Failure("concurrency_conflict", ...)` when raised from a tool
path).

---

## Security and Privacy

Store only the minimum personal data needed.

Treat as personal data:

- phone numbers
- names
- audio files (inbound and outbound)
- transcripts
- reservations

Rules:

- Do not log raw secrets, access tokens, or admin API keys.
- Do not log full customer messages unless explicitly required for
  controlled debugging.
- Verify all public webhook signatures before processing.
- Apply idempotency and replay protection to webhooks.
- Tenant data must remain isolated via global query filters.
- Secrets live in Azure Key Vault. Tenant integration tables store only
  references to secrets, never raw secrets.

---

## Observability

Use OpenTelemetry for traces and metrics. Correlate API, Worker, database,
queues, and external integrations through a shared `traceparent`.

### Required log fields when relevant

- `tenant_id`
- `conversation_id`
- `customer_id`
- `correlation_id`
- `provider`
- `tool_key`
- `job_id`
- `trace_id`
- `span_id`

### Metrics worth tracking from day one

- inbound messages per tenant
- outbound messages per tenant (text, audio)
- model invocation tokens per tenant
- tool calls by `(tenant, tool_key, outcome)`
- handoff rate (with reason breakdown)
- queue lag (oldest pending message age)
- dead-letter counts
- TTS / transcription success rate

---

## Logging (ZLogger)

Use **ZLogger** as the structured logger across API, Worker, and shared
libraries. Source-generated, zero-allocation, AOT-friendly.

### Conventions

- Log messages use compile-time interpolation:
  `logger.ZLogInformation($"Reservation {reservationId} confirmed");`
- Log levels:
  - `Trace` — disabled outside diagnostic sessions
  - `Debug` — local and dev only
  - `Information` — meaningful events (request start/end, tool execution,
    job dispatch)
  - `Warning` — recoverable issues, retries, validation failures
  - `Error` — unhandled exceptions, dead-letter, infrastructure failure
  - `Critical` — startup failure, data corruption, tenant isolation breach
- Every log emitted within a request includes the ambient `correlation_id`,
  `tenant_id`, and `trace_id` via a logging scope.

---

## Health Checks

A single endpoint for MVP:

| Endpoint  | Purpose                 | Checks                                                           |
| --------- | ----------------------- | ---------------------------------------------------------------- |
| `/health` | Is the service healthy? | PostgreSQL ping, Storage Queues metadata, Blob Storage metadata. |

Returns 503 if any dependency fails. The Worker exposes the same endpoint,
scoped to the dependencies it actually uses.

`/health` is **not** versioned because it is operational, not part of the
API contract. When you move to Kubernetes / Container Apps with separate
liveness, readiness, and startup probes, split this into the three classic
endpoints.

---

## .NET Project Conventions

### `Directory.Build.props` (root)

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

### `.editorconfig` (root)

```ini
root = true

[*]
end_of_line = lf
insert_final_newline = true
charset = utf-8
indent_style = space

[*.cs]
indent_size = 4
csharp_style_namespace_declarations = file_scoped:warning
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_for_built_in_types = false:suggestion
csharp_prefer_braces = true:warning
dotnet_style_qualification_for_field = false:warning
dotnet_style_require_accessibility_modifiers = always:warning

[*.{json,yml,yaml,xml,csproj,props,targets}]
indent_size = 2
```

### Nullable reference types

Enabled solution-wide. `string` is non-nullable; `string?` is nullable. Code
that ignores nullability hints causes build failures via
`TreatWarningsAsErrors`.

---

## Performance Rules

Principles:

- Prefer clarity over cleverness.
- Optimize based on measurement, not myths.
- Favor compile-time safety and source generators.
- Avoid reflection-heavy tooling when a modern alternative exists.
- The LLM call and external integrations are usually the cost and latency
  bottlenecks.

Avoid:

- generic repository
- custom Unit of Work
- indiscriminate `Include`
- loading entities for simple updates
- inconsistent error responses
- premature optimization
- treating Native AOT as dogma
- sending full conversation history to the model
- tenant-owned queries without tenant isolation

---

## Testing

Use:

- **TUnit**
- **NSubstitute**
- **Shouldly**
- **Verify**
- **Aspire Testing**
- **Testcontainers**

Do not use live LLM calls in CI. Use deterministic recorded responses or a
stubbed `IChatCompletionFactory`.

### Required tests

- `ToolExecutionGateway` denial reasons (tool not enabled, unknown tool,
  cross-tenant conversation).
- `ITenantToolRegistry` enables only tenant's own tools.
- `PromptBuilder` snapshot tests (Verify).
- Tenant isolation tests (cross-tenant access returns 404).
- Webhook signature verification (valid + invalid).
- Webhook idempotency (duplicate `provider_message_id`).
- Adapter contract tests (Refit clients against Testcontainers / WireMock).
- Reservation concurrency conflict (`DbUpdateConcurrencyException` → 409).
- Conversation state patch application (date/time string → `DateOnly` /
  `TimeOnly` conversion).
- Schema-validation rejection of malformed model output.
- Two-failure handoff trigger (per-operation-type counter).
- Agent loop iteration cap triggers handoff.
- TTS failure does not block text reply.
- Inbound voice note → transcription → agent loop end-to-end.

### Test layout

Tests mirror the slice/module structure:

```text
tests/
  Api.Tests/
    Modules/
      Tenancy/Features/CreateTenant/CreateTenantTests.cs
      Reservations/Features/CancelReservation/CancelReservationTests.cs
  Worker.Tests/
    Pipelines/
      ProcessIncomingMessage/ProcessIncomingMessageTests.cs
  Application.Tests/
    Agents/PromptBuilderTests.cs
    Tools/ToolExecutionGatewayTests.cs
  Integration.Tests/         // Aspire + Testcontainers
```

---

## Practical Guidance for AI Coding Agents

When proposing or modifying code:

1. Respect the slice layout: place files under
   `Modules/<Module>/Features/<UseCase>/`.
2. Use **Mediator** (martinothamar) for command/query dispatch in both
   API and Worker. Never mix MediatR.
3. Do not introduce generic repositories or custom Unit of Work.
4. Use FastEndpoints + Mediator + FluentValidation.
5. Use **Mapperly** only when shapes diverge; otherwise instantiate
   commands directly.
6. Use EF Core directly. Use `AsNoTracking()` and DTO projection for reads.
7. Consider `ExecuteUpdateAsync()` / `ExecuteDeleteAsync()` for simple
   writes.
8. Prefer keyset pagination.
9. Use `ProblemDetails` from a single global `IExceptionHandler`. Throw
   exceptions for unexpected errors.
10. Health check at `/health` (not under `/v1/`).
11. Keep `Program.cs` minimal. Use `<Module>ServiceRegistrations`.
12. Enforce tenant isolation through global query filters.
13. Use Ports and Adapters for external systems with the four MVP ports
    (`IMessageChannelIntegration`, `ICalendarIntegration`,
    `ITranscriptionIntegration`, `ISpeechSynthesisIntegration`) plus the
    internal `IChatCompletionFactory`.
14. Route every model-requested side effect through `ToolExecutionGateway`.
15. Implement new tools as `IToolHandler` and register them per tenant via
    `tenant_tool` rows. Do **not** add new code paths to the agent loop or
    the gateway for new tool kinds.
16. Native tools that mutate state must dispatch a Mediator command
    internally to reuse slice handlers.
17. Keep webhook handlers fast (under ~500ms). Long work belongs in the
    Worker.
18. Send the model only the system prompt, tenant context, and last 8
    turns (per _Definition of "turn"_).
19. Validate model output with Structured Outputs + source-gen STJ + a
    small set of inline checks.
20. Never hardcode model names. Resolve from `agent_profile`.
21. Pause autonomous replies during human handoff.
22. Trigger handoff after **two consecutive failures of the same operation
    type** within the active turn (per _Failure Counting Rules_) or after
    `MAX_AGENT_LOOP` iterations.
23. Use ZLogger with structured fields and ambient logging scopes.
24. Use **GUID v7** (`Guid.CreateVersion7()`) for every identifier.
25. Use `timestamptz` UTC for every timestamp; `DateOnly` / `TimeOnly`
    where time-of-day matters separately.
26. Database: **singular** `snake_case`. C# entity properties: PascalCase
    mapped via `EFCore.NamingConventions`.
27. Enable nullable reference types and treat warnings as errors.
28. All API routes go under `/v1/`, except `/health`.
29. When the bot sends voice replies, do not block text replies on TTS
    failure.
30. When adding a new channel, add a new `IMessageChannelIntegration`
    implementation and a new `tenant_channel.provider` value. Do not
    hardcode WhatsApp specifics outside the WhatsApp adapter.

---

## Executive Summary

This MVP backend must be:

- modern
- pragmatic
- observable
- maintainable
- multi-tenant by design
- safe for AI-driven side effects
- extensible through ports and adapters **and** through dynamic tool
  registration per tenant
- consistent across PostgreSQL, queues, workers, and external systems

This MVP backend must not become:

- over-engineered
- microservice-heavy too early
- obsessed with Native AOT
- dependent on provider-managed AI memory
- careless with model tool calls
- careless with tenant isolation
- expensive due to unnecessary model usage

The real priorities for MVP:

- strict tenant isolation
- predictable AI behavior
- controlled tool execution through a single chokepoint
- reliable background processing
- auto handoff on repeated failures
- clean architecture
- good developer experience
- a fast path to a working product

The two extensibility seams that matter most:

- **`IMessageChannelIntegration`** — new channels (Telegram, web, SMS) are
  new adapter implementations behind the same port.
- **`IToolHandler` + `ITenantToolRegistry` + `IToolHandlerFactory`** — new
  business capabilities (REST APIs, Excel, SQL, POS) are new tool handler
  kinds, registered per tenant, **without** modifying the agent loop, the
  gateway, the prompt builder, or the canonical schemas.

Patterns deliberately deferred to post-MVP (DDD aggregates, Outbox, customer
memory, summaries, escalation tiers, distributed cache, feature flags,
Keycloak, cross-channel customer identity) will be revisited only when
production pain justifies them.
