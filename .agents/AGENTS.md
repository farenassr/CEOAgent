# agents.md — MVP Edition (Revised)

> **Audience:** AI coding agents and human contributors generating code for
> this project. Every rule here is normative. If something below conflicts
> with intuition, this document wins. When in doubt, choose the simpler
> option that is consistent with the rules.

---

## Project Summary

This project is a modern **C# / .NET multi-tenant SaaS backend** for
AI-assisted business conversations. The MVP focus is \*\*restaurants using
deliberately built so that other channels (Telegram, Instagram DM, web chat)
and other integrations (REST APIs, Excel, customer databases, POS) can be
added later without rewriting the core.

The platform receives inbound channel messages (text or audio), resolves the
company by channel, loads company configuration, processes the message through
an AI agent built with **Microsoft Agent Framework**, executes approved
business tools through validated application workflows, and synchronizes
results with external systems (Google Calendar for MVP).

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
| `CEOAgent.Application`     | classlib    | Cross-slice business logic (`AgentRunner`, `PromptBuilder`, AI runtime). |
| `CEOAgent.Infrastructure`  | classlib    | Persistence (EF Core), queues, blob storage, observability glue.        |
| `CEOAgent.Integrations`    | classlib    | **Port contracts only**. No implementations.                            |
| `CEOAgent.Adapters`        | classlib    | Port implementations (WhatsApp, Google Calendar, Agent Framework providers, etc.). |
| `CEOAgent.Tools`           | classlib    | `IToolHandler` implementations (native MVP tools).                      |

---

## MVP Scope

### Required for MVP

- WhatsApp Cloud inbound and outbound messaging — **text and audio**
  (audio is received as voice notes and sent as TTS-generated voice replies)
- Company resolution by `(provider, provider_channel_id)` — for WhatsApp this
  is `("whatsapp_cloud", phone_number_id)`
- Audio transcription (single attempt; on failure → human handoff)
- Audio synthesis (TTS) for outbound voice replies — single provider
- Per-company agent profile with company-selectable model
- Per-company **dynamic tool registry** — even though MVP ships with 4 native
  tools, the registry contract is in place from day one
- Conversation history persistence (raw turns; no rolling summaries)
- Microsoft Agent Framework for all AI/LLM agent runtime work
- Worker-based background processing
- PostgreSQL — single DbContext, code-first migrations
- Azure Storage Queues for background jobs
- Azure Blob Storage for media files (inbound voice notes, outbound TTS,
  attachments)
- Manual company onboarding through admin endpoints protected by a static API
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
- Dedicated database per company
- Self-service company onboarding
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
  `(company_id, company_channel_id, external_customer_id)` for now)

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
   for in-process command and query dispatch in **Worker**, cross-module
   operations, reusable business workflows, and API use cases whose logic is
   non-trivial. Do **not** create a command/query just for a thin CRUD-style
   API endpoint. If the use case is simple and only used by one endpoint, keep
   the logic in the endpoint.
4. Use **FastEndpoints** for the HTTP surface.
5. Use **FluentValidation** for request validation in the API. Validate
   command preconditions inside Worker handlers via the same validators
   (resolved through DI), since the Worker has no FastEndpoints layer.
6. Use **Mapperly** for entity-to-DTO mapping. Create one partial mapper per module (e.g., CompanyMapper for all Company CRUD operations)..
7. Use **Aspire** for local orchestration only. Production Azure resources
   come from Bicep / Terraform / Azure DevOps pipelines.
8. Use `ProblemDetails` for all error responses, populated by a single global
   `IExceptionHandler`. Throw exceptions for unexpected errors. Do not invest
   in `Result<T, Error>` plumbing for MVP.
9. Use **PostgreSQL** as the primary database with one `CEOAgentDbContext`.
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
    - `ITranscriptionIntegration` — speech-to-text. Per-company adapter
      selection, single configured implementation in MVP.
      Optionally a fourth, internal-only Microsoft Agent Framework-backed AI
      runtime abstraction acts as the LLM port — see
      _AI Agent Runtime_.
15. No keyed DI for ports yet — one implementation per port. Introduce keyed
    DI when a second provider arrives for the same capability.
16. Never call external systems directly from business logic. Go through a
    port.

### AI safety

17. The model **never** executes side effects directly.
18. Every model-requested action is validated against the enabled company
    tool catalog and executed only through application-owned tool handlers or
    workflows. Do not reintroduce `ToolExecutionGateway` or
    `CreateToolExecutionRequest`.
19. The backend **never** trusts model output directly. Use Microsoft Agent
    Framework capabilities and provider-side structured output where
    available, plus a small set of post-deserialization checks.
20. Do not send the full conversation transcript to the model. Send the
    last 8 raw turns (see _Definition of "turn"_).
21. Do not hardcode model names. Resolve from the company's `agent_profile`.
22. The model receives the **full enabled tool catalog** for the company
    every turn. Tool selection logic is unnecessary at MVP scale (≤10
    tools). Add it when catalogs exceed ~10 tools per company.

### Multi-tenancy

23. Every company-owned table includes `company_id`.
24. Every company-owned query enforces company isolation through EF Core
    **global query filters**. Manual company resolution from request bodies
    is forbidden.
25. The customer phone number is **never** a company identifier. Resolve
    company from `(provider, provider_channel_id)`.

### Reliability

26. Every job handler is idempotent.
27. Webhooks must persist, enqueue, and return `200 OK` quickly (under
    ~500ms).

### API & data

28. All API routes versioned under `/v1/`. The single exception is
    `/health` which is **not** versioned (operational endpoint).
29. All identifiers are `Guid` generated as **GUID v7**
    (`Guid.CreateVersion7()`).
30. Database tables and columns use **singular `snake_case`**.
    Closed provider sets, such as channel providers, are modeled as C# enums
    instead of raw string properties. Persist them as strings with
    `HasConversion<string>()`; never persist enum ordinals.
31. Entity Framework property names use **PascalCase** in C#. Apply
    `EFCore.NamingConventions` with `UseSnakeCaseNamingConvention()` once at
    `CEOAgentDbContext` configuration to map automatically.
32. All timestamps stored as `timestamptz` and represented in C# as
    `DateTime` with `DateTimeKind.Utc`. Never use `DateTime.Now`. Always use
    `TimeProvider`.
33. Date/time fields where time-of-day matters separately use `DateOnly`
    and `TimeOnly` in entities and Mediator commands. JSON contracts (model
    output, HTTP wire) use ISO 8601 strings — convert at the boundary.
34. Enable nullable reference types and treat warnings as errors.
35. Use C# primary constructors wherever they fit cleanly, especially for
    dependency-injected services, endpoints, middleware, handlers, exceptions,
    and simple immutable DTO initialization. Do not force them when a
    parameterless constructor is required by a framework, when object
    initializer binding is clearer, or when the constructor body contains
    meaningful setup logic.
36. API request/response DTOs live in `CEOAgent.Shared`, not
    `CEOAgent.ApiService`. Request DTOs go under
    `Request/<Domain>/<Name>Request.cs` and response DTOs go under
    `Response/<Domain>/<Name>Response.cs`, with one `public sealed class` per
    file. Example: `Request/Company/CompanyToolRequest.cs`.
37. JSONB document types used by Infrastructure entities live under
    `CEOAgent.Infrastructure/Persistence/Entities/JsonDocuments/`, not in
    `CEOAgent.Shared`. Shared API DTOs that accept flexible JSON payloads use
    boundary-safe JSON types such as `JsonElement`, then map to entity JSONB
    document classes inside API/Application code.
38. Generated code and hand-written initializers must not produce MA0007
    diagnostics. Prefer trailing commas in multi-line initializer and
    collection expressions when the analyzer requests them.
38a. Do not use abbreviated variable names in hand-written code. Prefer
    descriptive names such as `request`, `cancellationToken`,
    `companyContext`, and `toolExecutionContext` instead of abbreviated names.

### Secrets and configuration

39. Never hardcode connection strings, passwords, API keys, provider secrets,
    client secrets, refresh tokens, webhook secrets, or signing keys.
40. Aspire `.WithReference(...)` is the default source for runtime resource
    connection strings between AppHost-managed resources and projects. Do not
    move PostgreSQL, queue, or blob runtime connection strings into Key Vault.
41. Azure Key Vault is the target store for deployed/shared secrets such as
    admin API keys, provider app secrets, OAuth client secrets, and Langfuse
    keys.
42. User-secrets and environment variables are local development inputs only.
    Do not treat them as the production secret store.
43. EF design-time factories must read from configuration, environment
    variables, and user-secrets. They must fail fast with a clear message when
    a required connection string is missing.
44. Company credential tables store references only, such as `kv://...`.
    Database rows must never contain raw secret values.

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
- `Aspire.Hosting.Azure.KeyVault` — represents the existing deployed/shared
  secret store used in publish mode

Do **not** wire direct OpenAI Aspire client resources in AppHost. All AI and
LLM work is implemented through **Microsoft Agent Framework** packages in the
application/adapter layer, even when an OpenAI-compatible provider is used
behind that framework.

### `AppHost/Program.cs` skeleton

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("appdb");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var queues  = storage.AddQueues("queues");
var blobs   = storage.AddBlobs("blobs");

var keyVault = builder.AddAzureKeyVault("keyvault")
    .PublishAsExisting("kv-ceo-agent-dev", "rg-ceo-agent-dev");

// Langfuse credentials are passed through to API and Worker as env vars.
var langfuseHost      = builder.AddParameter("langfuse-host");
var langfusePublicKey = builder.AddParameter("langfuse-public-key", secret: true);
var langfuseSecretKey = builder.AddParameter("langfuse-secret-key", secret: true);
var adminApiKey       = builder.AddParameter("admin-api-key", secret: true);

builder.AddProject<Projects.Api>("api")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithEnvironment("LANGFUSE_HOST",       langfuseHost)
    .WithEnvironment("LANGFUSE_PUBLIC_KEY", langfusePublicKey)
    .WithEnvironment("LANGFUSE_SECRET_KEY", langfuseSecretKey);

builder.AddProject<Projects.Worker>("worker")
    .WithReference(postgres)
    .WithReference(queues)
    .WithReference(blobs)
    .WithEnvironment("LANGFUSE_HOST",       langfuseHost)
    .WithEnvironment("LANGFUSE_PUBLIC_KEY", langfusePublicKey)
    .WithEnvironment("LANGFUSE_SECRET_KEY", langfuseSecretKey);

builder.Build().Run();
```

### Secrets and configuration

- Runtime PostgreSQL, queue, blob, and similar resource connection strings
  flow from Aspire `.WithReference(...)`.
- Azure Key Vault has no local emulator. For MVP, do not require Azure Key
  Vault access for local runs.
- AppHost registers Key Vault with
  `.PublishAsExisting("kv-ceo-agent-dev", "rg-ceo-agent-dev")`.
- In `builder.ExecutionContext.IsPublishMode`, read deployed/shared secrets
  from `keyVault.GetSecret(...)` and pass them to API/Worker.
- Outside publish mode, use Aspire parameters backed by user-secrets or
  environment variables. Do not commit local secret values.
- Do not use `RunAsExisting`/`AsExisting` for MVP unless the explicit goal is
  to require Azure access during local development.
- `CEOAgentDbContextFactory` is design-time only and reads
  `ConnectionStrings:CEOAgent` from `appsettings.json`,
  `appsettings.Development.json`, user-secrets, or environment variables.
  It must throw a clear `InvalidOperationException` if the value is missing.
- Company integration credential rows store references such as
  `kv://company/provider/credential`, never secret payloads.

### Client packages (in API and Worker)

- `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`
- `Aspire.Azure.Storage.Queues`
- `Aspire.Azure.Storage.Blobs`
- `Microsoft.Agents.AI.Abstractions` where AI abstractions are needed
- `Microsoft.Agents.AI.OpenAI` only inside adapters that implement an
  Agent Framework provider against OpenAI-compatible services

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

Langfuse exposes a native OTLP HTTP endpoint that understands GenAI semantic
conventions emitted by Microsoft Agent Framework / Microsoft.Extensions.AI.
No Langfuse-specific SDK is required.

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
        .AddSource("Microsoft.AgentFramework*")
        .AddSource("Microsoft.Extensions.AI*")
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

- `company_id`
- `conversation_id`
- `customer_id`
- `correlation_id`
- `channel_provider` (e.g. `whatsapp_cloud`)
- `model_name` (resolved from `agent_profile`)
- `prompt_version` (set by `PromptBuilder` when prompts change)

`AgentRunner` adds these as activity tags before invoking the Microsoft Agent
Framework-backed runtime.

### What goes to Langfuse vs general OTel

| Signal                              | General OTel | Langfuse |
| ----------------------------------- | ------------ | -------- |
| HTTP request traces                 | Yes          | No       |
| Database spans                      | Yes          | No       |
| Queue spans                         | Yes          | No       |
| `Microsoft.AgentFramework.*` spans  | Yes          | Yes      |
| `Microsoft.Extensions.AI.*` spans   | Yes          | Yes      |
| Application logs                    | Yes          | No       |

The same span can be exported to both backends; Langfuse only displays the
GenAI-shaped ones.

### Privacy: prompt and completion content

Agent Framework / Microsoft.Extensions.AI diagnostics may include prompt and
completion **text** depending on provider instrumentation and configuration.
Treat sensitive LLM content capture as process-level operational behavior —
do not claim per-company toggling unless a custom processor enforces it.

Rule:

- In **production**, default to `false`. Token counts, latency, model name,
  and tool-call structure still flow through, which is enough for cost and
  performance analysis without PII exposure.
- For controlled debugging, enable it temporarily in a non-production
  environment and never on shared production processes.
- A future enhancement (post-MVP) is a custom `ActivityProcessor` that strips
  prompt/completion content per company before export. Until then, the
  per-company claim is **not** something we offer.

---

## Core Modules

Initial modules:

- `Companies` — companies, channels, integration credentials references, agent
  profiles, tool configuration per company.
- `Customers` — customer records keyed by `(company_id, company_channel_id,
external_customer_id)`.
- `Conversations` — conversations, messages, transcriptions, conversation
  state.
- `Agents` — prompt building, tool selection (trivial in MVP), model
  orchestration, and tool handling.
- `Integrations` — port contracts and shared DTOs (no implementations).

Modules communicate **only** through Mediator commands and queries within
the same process. **No direct cross-module entity references.** When a
slice in module A needs data owned by module B, it dispatches a query
to module B.

Adapters live outside the modules under `Adapters/`.

---

### `Application/` vs `Modules/Features/` — the rule

- **`Modules/<X>/Features/<UseCase>/`** holds the **slice**. Each slice uses
  explicit subfolders:
  - `Endpoints/` for FastEndpoints endpoint classes and endpoint validators.
  - `Commands/` for Mediator commands/queries and handlers, only when the
    use case actually needs Mediator.
  - Shared DTOs are referenced from `CEOAgent.Shared/Request/<Domain>` and
    `CEOAgent.Shared/Response/<Domain>`.
    This is "controller-like" code that orchestrates business logic.
- **`Application/<X>/`** holds **stateful or non-trivial business logic**
  shared across slices and used by the Worker. `AgentRunner`,
  `PromptBuilder`, AI runtime services, and `ConversationStateApplier` live
  here. Slice handlers and tool handlers depend on `Application/`, not the
  other way around.
- **`Tools/`** holds `IToolHandler` implementations. Each tool handler may
  internally dispatch a Mediator command into the appropriate module —
  that is how a tool reuses a slice's logic without duplicating it. See
  _Tools and Tool Handling_.

---

## Vertical Slice Architecture Rules (Pragmatic Edition)

Rules:

- Endpoint files use the suffix `Endpoint` and live under the slice's
  `Endpoints/` folder.
- Request DTO files live under `CEOAgent.Shared/Request/<Domain>/`.
- Response DTO files live under `CEOAgent.Shared/Response/<Domain>/`.
- Entity-owned JSONB document classes live under
  `CEOAgent.Infrastructure/Persistence/Entities/JsonDocuments/`; do not place
  them in `CEOAgent.Shared`.
- Every API request/response DTO is declared in its own independent class
  file. Do not group multiple DTO classes in one file.
- DTOs are declared as `class`, not `record` and not `sealed record`. Use
  `public sealed class` for API request/response DTOs unless inheritance is
  explicitly required.
- Mediator command/query files live under the slice's `Commands/` folder,
  but only when the use case is non-trivial, reused, cross-module, or needed
  by the Worker. Avoid command sprawl: for simple single-endpoint CRUD-style
  operations, keep the business logic in the endpoint and inject the needed
  dependencies directly.
- Shared non-request/non-response models may live directly under `Models/`
  at the feature level when reused across slices in the same module.
- Do **not** register services directly in `Program.cs`. Use
  `<Module>ServiceRegistrations` extension classes per module.
- When request-to-entity mapping is non-trivial, reused, or crosses boundary
  types such as `JsonElement` to entity JSONB documents, put that mapping in
  the module Mapperly mapper instead of constructing EF Core entities directly
  inside endpoints.

The endpoint:

1. Receives the request.
2. Lets FastEndpoints + FluentValidation validate it (status `400` on
   failure).
3. For simple single-endpoint operations, runs the logic directly using
   injected services such as `CEOAgentDbContext`, ports, and `TimeProvider`.
4. For non-trivial or reusable operations, maps to a Mediator command/query
   (direct construction; Mapperly only when shapes diverge) and sends it.
5. Maps the response back if needed.
6. Returns.

Business logic lives in the endpoint for simple one-off API operations. Move
it to a Mediator handler when reuse, Worker execution, cross-module dispatch,
or meaningful complexity justifies the extra type.

---

## API Layer

Use **FastEndpoints** for HTTP. Use **Mediator** for command/query dispatch
when the use case is non-trivial, reusable, cross-module, or shared with the
Worker.

- All routes versioned under `/v1/`. Health endpoint at `/health` (not
  versioned).
- In development, expose the Scalar API reference at `/scalar` and serve the
  backing OpenAPI document through the built-in OpenAPI endpoint.
- Validation runs **before** the handler, via FluentValidation integrated
  with FastEndpoints.
- Mapping uses **Mapperly only when needed** (see rule 6 in
  _Non-Negotiable Rules_).
- API request/response DTOs are `class` types, not records.
- Errors are translated to `ProblemDetails` by a single global
  `IExceptionHandler`.
- `CancellationToken` is propagated through every call.

### Standard status codes

| Status | Meaning                                               |
| ------ | ----------------------------------------------------- |
| 400    | Invalid request shape or validation error             |
| 401    | Unauthenticated                                       |
| 403    | Authenticated but lacks permission                    |
| 404    | Not found, including cross-company hidden resources   |
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
  evaluate: working hours, capacity, "already cancelled", company-disabled
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
- `companyId` — when the request is company-scoped

When `Result<T, Error>` and a richer error model start paying for themselves,
introduce them gradually. For now, exceptions plus FluentValidation are
enough.

---

## Multi-Tenancy

Use **shared database, shared schema, with `company_id` discriminator**.

Rules:

- The customer phone number is **never** a company identifier.
- Resolve company from the receiving channel:
  - For WhatsApp Cloud: `metadata.phone_number_id` from the webhook payload.
  - For any future channel: the channel's stable provider-side identifier.
  - The lookup is `company_channel WHERE provider = ? AND provider_channel_id = ?`.
- Use `messages[0].from` or `contacts[0].wa_id` to identify the customer
  within that company (for WhatsApp).
- Every company-owned table includes `company_id`.
- Every company-owned query filters by `company_id` via EF Core global query
  filters. Never rely on developers remembering to add the filter manually.
- Return `404` when a resource exists but belongs to another company — never
  reveal cross-company existence.

### Company context

A request middleware sets the ambient `ICompanyContext` from one of two
sources:

- For webhook routes: resolved from the channel's
  `(provider, provider_channel_id)` after signature verification.
- For admin routes: read from a header (`X-Company-Id`) authorized by the
  static admin API key.

EF Core global query filters read the ambient `ICompanyContext` and append
`WHERE company_id = @currentCompany` to every company-owned query. Manual
company resolution from request bodies is forbidden.

---

## Authentication and Authorization (MVP)

Keycloak is **not** used for MVP.

### Admin endpoints

Protected by a static API key in the `X-Admin-Api-Key` header, validated by
a custom **`AuthenticationHandler<>`** registered in the ASP.NET Core auth
pipeline. The key is stored as an Aspire/Azure secret. Endpoints under
`/v1/admin/...` carry an `[Authorize(AuthenticationSchemes = "AdminApiKey")]`
attribute (or the FastEndpoints equivalent).

This static admin API key remains the MVP admin authentication mechanism.
Do not introduce Keycloak, JWT, users, roles, or a dashboard identity system
as part of the Key Vault/configuration foundation.

Admin endpoints handle company onboarding, channel configuration, integration
credentials registration, agent profile management, and tool enablement.

### Webhook endpoints

No auth header. Authorization is established by HMAC signature verification
of the channel payload (see _Webhook Security_).

### Future

Multi-user dashboards or self-service portals will introduce JWT-based
authentication. Keycloak, Auth0, or ASP.NET Identity are all reasonable
choices. Plan for it; do not implement it now.

---

## Company Onboarding

For MVP, onboarding is **manual** by a platform operator hitting admin
endpoints.

Steps:

1. Create the `company` row.
2. Register the channel: `company_channel` row with `provider`,
   `provider_channel_id`, `metadata` (jsonb for provider-specific extras like
   `phone_number_id`, `business_account_id`, …), and a credentials reference.
3. Configure the agent profile (model, prompt overrides, language, timezone,
   working hours, capacity).
4. Configure the calendar integration credentials reference.
5. Enable tools for the company: insert `company_tool` rows for each native
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
4. Resolve company via company_channel.
5. Extract customer identifier from the provider payload — for WhatsApp,
   messages[0].from or contacts[0].wa_id.
6. Find or create customer by (company_id, company_channel_id, external_customer_id).
7. Find or create OPEN conversation for (company_id, customer_id, company_channel_id),
   snapshotting the agent_profile_id selected at conversation creation.
8. Persist inbound message idempotently using provider_message_id.
9. Enqueue ProcessIncomingMessageJob to Azure Storage Queue.
10. Return 200 OK.
```

The customer phone number is **never** used to resolve the company. The same
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
  `(company_id, provider_message_id)` when `provider_message_id` is not null.
- A duplicate insert returns **200 OK** without re-enqueuing.

---

## Conversation Storage and History

The platform owns the durable conversation state.

PostgreSQL stores:

- conversation
- message (text, transcribed audio, system, tool_call, tool_result)
- conversation_state (current short-lived state for the active interaction)
- tool_execution
- audio_asset (blob URL + metadata)

Azure Blob Storage stores:

- inbound voice notes
- outbound TTS-generated audio

The model receives only:

- platform system prompt
- company context (brand voice, language, timezone, current local date/time,
  branch info)
- last 8 raw turns of the conversation
- the enabled tool catalog for the company

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
- `tool_result` — the structured outcome returned by the application tool
  handler/workflow.

The **last 8 turns** = the last 8 entries in chronological order, regardless
of role mix. The platform system prompt and company context are **not**
counted as turns; they are prepended on every model call.

System notes (handoff triggered, conversation reopened, etc.) are stored as
`system` rows but are **not** counted as turns and are **not** sent to the
model.

---

## Conversation State

Short-lived state for the current interaction:

```json
{
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

Use **Microsoft Agent Framework** for all AI, LLM, agent orchestration, tool
calling, model interaction, and provider integration work. Do not introduce
direct OpenAI client usage in API, Worker, Application, or Infrastructure.
If an OpenAI-compatible model is used, it is accessed through the Agent
Framework provider packages from `CEOAgent.Adapters`.

Agent tool calls are not authorization to mutate state. Tool requests are
validated against the enabled company tool catalog and then routed into
application-owned tool handlers or Mediator workflows that enforce business
rules.

### Per-company model

Each company's `agent_profile` declares the model. There is one model per
company — no escalation tier. If the chosen model fails to produce valid
structured output after **one retry**, the agent triggers a human handoff.

Model names are never hardcoded. They live in `agent_profile.model_name`.

### Client registration

```csharp
// Register application AI abstractions and Microsoft Agent Framework-backed
// adapters. The model id is resolved per turn from agent_profile.
builder.Services.AddSingleton<IAgentRuntime, MicrosoftAgentFrameworkRuntime>();
```

`AgentRunner` resolves the company's `agent_profile`, calls
the Agent Framework-backed runtime for the turn, and never binds the model at
DI-registration time. If a company later needs a different provider for data
residency or procurement reasons, only the adapter changes — consumer code
stays put.

### Agent loop (multi-step turn handling)

A single inbound user message can require multiple model invocations
(model → tool call → model → tool call → … → final assistant message).
The Worker orchestrates this loop inside `ProcessIncomingMessageJob`:

```text
loop iteration:
  1. Build prompt (system + company context + last 8 turns + tool catalog).
  2. Run the Agent Framework agent.
  3. Parse AgentTurnResult.
  4. If toolCallRequest != null:
        - Validate against the enabled company tool catalog.
        - Execute through the matching application tool handler/workflow.
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

`MAX_AGENT_LOOP` is configurable per company in `agent_profile`
(`max_loop_iterations`), default `5`.

This loop is **inside** the `ProcessIncomingMessageJob` handler (single
queue message → single agent loop). Tool execution stays inside validated
application-owned handlers/workflows. `ExecuteToolCallJob` and
`SynthesizeAudioJob` exist as separate pipelines for **out-of-band**
operations (e.g., long-running tools or post-message audio generation), not
for splitting one inbound user message into multiple queue messages.

---

## Prompt Composition

`PromptBuilder` composes the final context from three layers:

1. **Platform system prompt** — safety rules, output schema instructions,
   tool-catalog contract, format reminders. Not editable by companies.
2. **Company context** — brand voice, language, tone, agent name, timezone,
   current local date/time, branch info if any, working hours.
3. **Last 8 raw turns** — chronological, role-tagged.

`PromptBuilder` is the only component allowed to compose the final model
context. Adding a fourth layer (long-term memory, summary, etc.) is a
deliberate post-MVP decision.

Company prompts must not override platform safety, company isolation, tool
execution, or privacy rules. The platform system prompt explicitly reasserts
these constraints regardless of company configuration.

---

## Structured Model Output

The backend never trusts model output directly.

### Layer 1 — Provider-side enforcement

Use Microsoft Agent Framework structured-output support and provider-side
schema enforcement where available. For OpenAI-compatible providers, strict
JSON schema response formatting must be configured through the Agent
Framework provider, not direct OpenAI client calls.

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

- `partySize` between 1 and the company's configured maximum.
- `date >= today` in company timezone.
- `time` parseable as `HH:mm`.
- `toolKey` exists in the company's enabled tool set
  (`ICompanyToolRegistry`).

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

## Tools and Tool Handling

### Dynamic tool registry — the extensibility backbone

The MVP ships with **four native** tools (see _Canonical MVP Tools_), but
the contract is designed from day one to support **dynamic tools registered
per company**, including future kinds such as:

- REST API calls into a company's own backend.
- Excel sheet lookups (read-only initially).
- SQL queries against a company's database.
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
        ToolExecutionContext toolExecutionContext,
        JsonElement parameters,
        CancellationToken cancellationToken);
}

public sealed record ToolExecutionContext(
    Guid CompanyId,
    Guid ConversationId,
    Guid CustomerId,
    Guid ExecutionId,
    DateTimeOffset NowInCompanyTz);

public abstract record ToolResult
{
    public sealed record Success(JsonElement Output) : ToolResult;
    public sealed record Denied(string Code, string Message) : ToolResult;
    public sealed record Failure(string Code, string Message) : ToolResult;
}
```

```csharp
public interface ICompanyToolRegistry
{
    Task<IReadOnlyList<ToolDescriptor>> GetEnabledToolsAsync(
        Guid companyId, CancellationToken cancellationToken);
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
        Guid companyId, string toolKey, CancellationToken cancellationToken);
}
```

### How native tools register

Each native handler is registered as a singleton keyed by its `ToolKey`:

```csharp
services.AddKeyedSingleton<IToolHandler, CheckAvailabilityToolHandler>("check_availability");
services.AddKeyedSingleton<IToolHandler, RequestHumanHandoffToolHandler>("request_human_handoff");
```

`company_tool` rows control which tools are exposed to the model for each
company. `IToolHandlerFactory` resolves a handler by checking that
`(company_id, tool_key)` is enabled and then pulling the keyed singleton.

### How dynamic tools will register (post-MVP)

A `company_tool` row carries a `kind` column (`native | rest_api | excel | sql`)
and a `config` jsonb. For non-native kinds, `IToolHandlerFactory` instantiates
a generic handler (e.g., `RestApiToolHandler`) parameterized by the config:
endpoint URL, auth reference, request schema, response shape. The
tool handlers, the agent loop, the prompt builder, and the canonical schemas do
**not** change to accommodate new tool kinds.

This is the deliberate extensibility point.

A model requesting a tool call is **not** authorization to execute it. Each
handler enforces its own business rules (capacity, working hours,
confirmation required, etc.).

### Tool ↔ Mediator command relationship

**must dispatch a Mediator command** internally rather than duplicate
business rules:

```csharp
{
    /* ... schema, description ... */

    public async Task<ToolResult> ExecuteAsync(
        ToolExecutionContext toolExecutionContext, JsonElement parameters, CancellationToken cancellationToken)
    {

        try
        {
            var result = await mediator.Send(
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

```csharp
    Datetime Date,
    int PartySize,
    string CustomerName,
    string? SeatingPreference,
    string? BranchId,
    bool ConfirmedByCustomer);

    string Status,                    // "confirmed" | "pending"
    string ExternalCalendarEventId);
```

Validation:

- `ConfirmedByCustomer == true` is required to execute.
- `(Date, Time)` not in the past, within company working hours.
- `PartySize` between 1 and company capacity max.

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

```csharp
    bool ConfirmedByCustomer,
    string? Reason);

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
    CompanyChannelProvider Provider { get; }   // WhatsAppCloud for MVP.

    Task<SendResult> SendTextAsync(SendTextInput input, CancellationToken cancellationToken);
    Task<SendResult> SendAudioAsync(SendAudioInput input, CancellationToken cancellationToken);

    // Optional capabilities. Adapters that don't support them throw
    // NotSupportedException; consumers feature-detect via try/catch or
    // capability flags.
    Task<SendResult> SendInteractiveAsync(SendInteractiveInput input, CancellationToken cancellationToken);
    Task MarkAsReadAsync(string providerMessageId, CancellationToken cancellationToken);

    Task<Stream> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken);
}

public interface ICalendarIntegration
{
    Task<CalendarEvent> CreateEventAsync(CreateEventInput input, CancellationToken cancellationToken);
    Task<bool> IsSlotAvailableAsync(DateTime startUtc, TimeSpan duration, CancellationToken cancellationToken);
    Task<bool> CancelEventAsync(string externalEventId, CancellationToken cancellationToken);
}

public interface ITranscriptionIntegration
{
    Task<TranscriptionResult> TranscribeAsync(
        Stream audio, string contentType, string? languageHint, CancellationToken cancellationToken);
}

public interface ISpeechSynthesisIntegration
{
    Task<SynthesisResult> SynthesizeAsync(
        string text, string voiceProfile, string language, CancellationToken cancellationToken);
}
```

One implementation per port for MVP:

- `WhatsAppCloudAdapter` → `IMessageChannelIntegration`.
- `GoogleCalendarAdapter` → `ICalendarIntegration`.
- `OpenAITranscriptionAdapter` (Whisper) → `ITranscriptionIntegration`.
- `OpenAISpeechAdapter` (TTS) → `ISpeechSynthesisIntegration`.

Inject directly. **No keyed DI for ports yet.** When a second provider
arrives for the same capability (e.g., Outlook Calendar), introduce keyed
DI and a company-driven selector.

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
  `companies/{companyId}/inbound/{conversationId}/{messageId}.ogg`.
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
note) or the company's `agent_profile.voice_reply_enabled = true`, the
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

The configured TTS voice profile per company lives in
`agent_profile.tts_voice_profile`. Companys may have voice replies disabled
entirely.

---

## Background Processing

Webhook handlers must be fast. **Long work belongs to the Worker.**

Webhook path:

```text
1. Verify signature.
2. Resolve company.
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
    cancellationToken);
```

Beyond that, migrate to Azure Service Bus Sessions with
`SessionId = conversation_id`.

---

## Idempotency

For MVP, idempotency is enforced at three specific places:

| Origin                       | Key                                                                                                | Storage                                                                       |
| ---------------------------- | -------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| WhatsApp inbound webhook     | `provider_message_id`                                                                              | Unique constraint on `(company_id, provider_message_id)` where not null       |
| Tool call to Google Calendar | `request_id` derived from `(company_id, conversation_id, tool_key, params_canonical_json)` SHA-256 | Passed to Google Calendar's idempotency parameter; logged in `tool_execution` |
| Outbound message send        | `client_message_id` derived from `(company_id, conversation_id, message_id)`                       | Passed to WhatsApp Cloud's `biz_opaque_callback_data` and stored in `message` |

Before any side effect, the handler checks the appropriate key. If the
operation already completed, it returns the persisted result.

Other forms of idempotency (admin POST headers, hash-based dedup of business
operations) are deferred until needed.

---

## Naming Conventions

### Mediator commands and queries

### Job messages

- Suffix `Job`: `ProcessIncomingMessageJob`, `ExecuteToolCallJob`,
  `TranscribeAudioJob`, `SynthesizeAudioJob`,
  `SendOutboundMessageJob`, `CloseInactiveConversationsJob`.

### Folder placement

- Slice folders use `Endpoints/`, `Commands/`, and `Models/` subfolders.
- Mediator commands and queries live in `Commands/` inside their slice
  folder, only when a command/query is justified by reuse, Worker execution,
  cross-module dispatch, or meaningful complexity.
- FastEndpoints endpoints live in `Endpoints/` inside their slice folder.
- API request DTO classes live in `CEOAgent.Shared/Request/<Domain>/`.
- API response DTO classes live in `CEOAgent.Shared/Response/<Domain>/`.
- Each API DTO class gets its own file named after the class.
- Jobs live in `Worker/Pipelines/<Pipeline>/<JobName>.cs`.
- Tool handlers live in `Tools/<Area>/<ToolKey>ToolHandler.cs`.

### Commit message format

Commit messages use this project-specific format:

```text
<ProjectOrArea>/<ProjectOrArea>/...: [<GitHubIssueId>] <Changes performed>
```

Use the project name after `CEOAgent.` only, or the major non-project area.
Keep touched areas short but explicit. Use `[#0]` when no GitHub issue exists.

Example:

```text
ApiService/AppHost/Infrastructure/Worker/tests/docs: [#0] Add MVP persistence, admin auth, company isolation, Aspire setup, and agent rules
```

Rules:

- List every touched project or major area before the colon.
- Use concise area names such as `AGENTS`, `docs`, `tests`, `ApiService`,
  `Infrastructure`, `AppHost`, `Worker`, `Application`, `Adapters`,
  `Integrations`, `ServiceDefaults`, or `Tools`.
- Keep the summary short and concrete, without trailing punctuation.

---

## Data Access Rules

Use PostgreSQL with EF Core directly.

- No generic repository.
- No custom Unit of Work — `SaveChangesAsync` is the commit.
- Use EF Core directly inside Mediator handlers.
- Queries are no-tracking by default. Use the centralized query helper
  (`WithDefaultTracking()` in the current codebase) for query roots; pass the
  explicit tracking flag only when the query returns entities that will be
  updated in the same `DbContext`.
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
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public int PartySize { get; set; }
    public DateTime? CancelledAt { get; set; }            // UTC
    public uint RowVersion { get; set; }                  // xmin

    public bool TryCancel(string? reason, TimeProvider clock)
    {
        CancelledAt = clock.GetUtcNow().UtcDateTime;
        return true;
    }
}
```

---

## EF Core Migrations

- **One** `CEOAgentDbContext` for the whole solution.
- Code-first migrations stored in `Infrastructure/Persistence/Migrations/`.
- Migrations must **never** run automatically on application startup in any
  environment.
- AI coding agents may scaffold migration files only when a schema change
  requires them, but they must **not** apply or run migrations against any
  database.
- Applying migrations is a human/operator-controlled step. The project owner
  chooses when to run `dotnet ef database update` locally, in staging, or in
  production.
- Each migration is named `{yyyyMMddHHmmss}_{Description}` (the default
  EF Core format). Example: `20260508153012_AddCompanyChannel`.
- Migrations must be **forward-only**. Down migrations exist for local
  rollback only — never relied on in production.

---

## Database Modeling Rules

- All identifiers are `Guid` generated as **GUID v7** via
  `Guid.CreateVersion7()`.
  `company_channel`).
- Use **`snake_case`** for columns.
- Entity Framework property names use **PascalCase** in C#. Map to snake_case
  via `EFCore.NamingConventions`:

  ```csharp
  options.UseSnakeCaseNamingConvention();
  ```

- Every company-owned table includes `company_id`, `created_at`, `updated_at`.
- All timestamps are `timestamptz` and represented as `DateTime` (UTC) in
  C#.
- Never use `DateTime.Now`. Use `TimeProvider`.
- Every foreign key is explicit.
- Model closed sets such as channel providers as C# enums, not raw strings.
- Persist enums as strings (`HasConversion<string>()`), never ordinals.
- Use `jsonb` only for genuinely flexible payloads (tool parameters,
  conversation state, channel metadata). Stable queryable fields are real
  columns.
- JSONB payload classes owned by entities live in
  `Infrastructure/Persistence/Entities/JsonDocuments/`.
- Use `IEntityTypeConfiguration<T>` classes in
  `Infrastructure/Persistence/Configurations/`. The `CEOAgentDbContext` only
  declares `DbSet<T>` properties and applies all configurations via
  `modelBuilder.ApplyConfigurationsFromAssembly(...)`.

### JSON Columns With EF Core 11 + Npgsql

When modeling a property as a Postgres `jsonb` column:

- **By default**, use complex types with
  `entity.ComplexProperty(x => x.Prop, b => b.ToJson())`. This is the
  recommended EF Core 11 and Npgsql path, supports `ExecuteUpdateAsync` over
  nested properties, and allows server-side LINQ queries inside the JSON.
- **Do not use System.Text.Json polymorphism** (`[JsonPolymorphic]` +
  `[JsonDerivedType]`) for types mapped to `jsonb`. EF Core complex types do
  not support inheritance, and that pushes the mapping back to legacy POCO
  JSON mapping, which does not support `ExecuteUpdate` or server-side JSON
  queries.
- If variants are needed, such as a discriminated union shape, use the
  **wrapper pattern with nullable properties** plus a separate discriminator
  column on the root entity. Protect the "discriminator matches populated
  property" invariant with static factories, private setters, and an
  exhaustive `Match` method.
- Avoid `Property(...).HasColumnType("jsonb")` as the primary mapping for
  complex types. That is Npgsql's legacy POCO JSON mapping and is deprecated
  for this use case. Use it only for opaque `string` values or raw
  `JsonDocument` payloads.
- Do not register `JsonSerializerContext`, `ValueConverter`, or manual
  System.Text.Json configuration for types mapped with
  `ComplexProperty().ToJson()`. EF Core handles serialization internally.
- For discriminators stored as separate columns, use `HasConversion<string>()`
  rather than enum ordinals and add an index. This survives enum reordering.
- For the strongest consistency between a discriminator and JSON content,
  consider a Postgres generated column (`GENERATED ALWAYS AS (...)`) instead
  of maintaining the discriminator by hand. This is optional, but removes the
  final possibility of drift.

### Mandatory minimum indexes

Every company-owned table has at minimum:

- `(company_id, created_at DESC)` — for company-scoped recent-first listings.
- `(company_id, <natural_lookup_field>)` — when lookups by a business key
  exist (e.g., `(company_id, provider_message_id)` on `message`).

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
    public Guid CompanyId { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
```

---

methods where rules apply. Rules to enforce in handlers and entity methods:

- Cannot confirm without all required fields (date, time, partySize,
  customerName, `ConfirmedByCustomer == true`).
- Cannot double-book the same `external_calendar_event_id` (unique
  constraint in DB).
- Party size respects company capacity.

aggregate.

### Optimistic concurrency

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

Rules:

- Do not log raw secrets, access tokens, or admin API keys.
- Do not log full customer messages unless explicitly required for
  controlled debugging.
- Verify all public webhook signatures before processing.
- Apply idempotency and replay protection to webhooks.
- Company data must remain isolated via global query filters.
- Deployed/shared secrets live in Azure Key Vault. Local development uses
  Aspire parameters, user-secrets, or environment variables. Company
  integration tables store only references to secrets, never raw secrets.

---

## Observability

Use OpenTelemetry for traces and metrics. Correlate API, Worker, database,
queues, and external integrations through a shared `traceparent`.

### Required log fields when relevant

- `company_id`
- `conversation_id`
- `customer_id`
- `correlation_id`
- `provider`
- `tool_key`
- `job_id`
- `trace_id`
- `span_id`

### Metrics worth tracking from day one

- inbound messages per company
- outbound messages per company (text, audio)
- model invocation tokens per company
- tool calls by `(company, tool_key, outcome)`
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
- Log levels:
  - `Trace` — disabled outside diagnostic sessions
  - `Debug` — local and dev only
  - `Information` — meaningful events (request start/end, tool execution,
    job dispatch)
  - `Warning` — recoverable issues, retries, validation failures
  - `Error` — unhandled exceptions, dead-letter, infrastructure failure
  - `Critical` — startup failure, data corruption, company isolation breach
- Every log emitted within a request includes the ambient `correlation_id`,
  `company_id`, and `trace_id` via a logging scope.

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
- company-owned queries without company isolation

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
stubbed Microsoft Agent Framework-backed AI runtime abstraction.

### Required tests

- Tool handler denial reasons (tool not enabled, unknown tool,
  cross-company conversation).
- `ICompanyToolRegistry` enables only company's own tools.
- `PromptBuilder` snapshot tests (Verify).
- Company isolation tests (cross-company access returns 404).
- Webhook signature verification (valid + invalid).
- Webhook idempotency (duplicate `provider_message_id`).
- Adapter contract tests (Refit clients against Testcontainers / WireMock).
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
      Companies/Features/CreateCompany/CreateCompanyTests.cs
  Worker.Tests/
    Pipelines/
      ProcessIncomingMessage/ProcessIncomingMessageTests.cs
  Application.Tests/
    Agents/PromptBuilderTests.cs
  Integration.Tests/         // Aspire + Testcontainers
    Infrastructure/CEOAgentDbContextTestFactory.cs
    Infrastructure/PostgresTestDatabase.cs
    Seed/CompanySeed.cs
```

Database tests use PostgreSQL Testcontainers by default. Do not add SQLite
test databases. Centralize DbContext creation under `tests/*/Infrastructure`
and reusable seed data under `tests/*/Seed`.

---

## Practical Guidance for AI Coding Agents

When proposing or modifying code:

1. Respect the slice layout: place files under
   `Modules/<Module>/Features/<UseCase>/Endpoints`,
   `Modules/<Module>/Features/<UseCase>/Commands`, and shared DTO folders in
   `CEOAgent.Shared/Request/<Domain>` or `CEOAgent.Shared/Response/<Domain>`.
2. Use **Mediator** (martinothamar) for command/query dispatch in Worker,
   cross-module workflows, reusable workflows, and non-trivial API use cases.
   Never mix MediatR. Do not create commands for simple one-off endpoint
   logic.
3. Do not introduce generic repositories or custom Unit of Work.
4. Use FastEndpoints + FluentValidation. Add Mediator only when justified by
   the use case.
5. Prefer primary constructors whenever they fit cleanly, especially for DI
   classes, endpoints, middleware, handlers, exceptions, and simple DTO
   initialization.
6. Declare API DTOs as `class` types, not records or sealed records.
7. Put every API request/response DTO in `CEOAgent.Shared`, one class per
   file, following `Request/Company/CompanyToolRequest.cs` and
   `Response/Company/CompanyResponse.cs` style paths.
8. Keep entity JSONB document classes in
   `CEOAgent.Infrastructure/Persistence/Entities/JsonDocuments/`; Shared DTOs
   should not reference Infrastructure entity document types.
9. Use **Mapperly** only when shapes diverge; otherwise instantiate
   commands directly when a command exists.
10. Use EF Core directly. Use the centralized no-tracking query helper by
   default and DTO projection for reads; opt into tracking with the helper's
   explicit flag only for update queries.
10. Consider `ExecuteUpdateAsync()` / `ExecuteDeleteAsync()` for simple
    writes.
11. Prefer keyset pagination.
12. Use `ProblemDetails` from a single global `IExceptionHandler`. Throw
    exceptions for unexpected errors.
13. Health check at `/health` (not under `/v1/`).
14. Keep `Program.cs` minimal. Use `<Module>ServiceRegistrations`.
15. Enforce company isolation through global query filters.
16. Use Ports and Adapters for external systems with the four MVP ports
    (`IMessageChannelIntegration`, `ICalendarIntegration`,
    `ITranscriptionIntegration`, `ISpeechSynthesisIntegration`) plus the
    internal Microsoft Agent Framework-backed AI runtime abstraction.
17. Use Microsoft Agent Framework for all AI, LLM, agent, tool-calling, and
    model-provider code. Do not call OpenAI clients directly outside an Agent
    Framework adapter.
18. Implement new tools as `IToolHandler` and register them per company via
    `company_tool` rows. Do **not** add new code paths to the agent loop or
    the tool-handler routing for new tool kinds.
19. Native tools that mutate state should reuse the same business workflow as
    the API. Dispatch a Mediator command when that command exists; otherwise
    extract shared non-trivial logic to `Application/` instead of duplicating
    it.
20. Keep webhook handlers fast (under ~500ms). Long work belongs in the
    Worker.
21. Send the model only the system prompt, company context, and last 8
    turns (per _Definition of "turn"_).
22. Validate model output with Agent Framework/provider structured output +
    source-gen STJ + a
    small set of inline checks.
23. Never hardcode model names. Resolve from `agent_profile`.
24. Do not add SQLite-based tests. Use centralized test DbContext factories
    and Seed helpers.
25. Avoid MA0007 diagnostics by including trailing commas in multi-line
    initializer and collection expressions when the analyzer expects them.
24. Pause autonomous replies during human handoff.
25. Trigger handoff after **two consecutive failures of the same operation
    type** within the active turn (per _Failure Counting Rules_) or after
    `MAX_AGENT_LOOP` iterations.
26. Use ZLogger with structured fields and ambient logging scopes.
27. Use **GUID v7** (`Guid.CreateVersion7()`) for every identifier.
28. Use `timestamptz` UTC for every timestamp; `DateOnly` / `TimeOnly`
    where time-of-day matters separately.
29. Database: **singular** `snake_case`. C# entity properties: PascalCase
    mapped via `EFCore.NamingConventions`.
30. Enable nullable reference types and treat warnings as errors.
31. All API routes go under `/v1/`, except `/health`.
32. When the bot sends voice replies, do not block text replies on TTS
    failure.
33. When adding a new channel, add a new `IMessageChannelIntegration`
    implementation and a new `CompanyChannelProvider` enum value. Do not
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
  registration per company
- consistent across PostgreSQL, queues, workers, and external systems

This MVP backend must not become:

- over-engineered
- microservice-heavy too early
- obsessed with Native AOT
- dependent on provider-managed AI memory
- careless with model tool calls
- careless with company isolation
- expensive due to unnecessary model usage

The real priorities for MVP:

- strict company isolation
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
- **`IToolHandler` + `ICompanyToolRegistry` + `IToolHandlerFactory`** — new
  business capabilities (REST APIs, Excel, SQL, POS) are new tool handler
  kinds, registered per company, **without** modifying the agent loop, the
  gateway, the prompt builder, or the canonical schemas.

Patterns deliberately deferred to post-MVP (DDD aggregates, Outbox, customer
memory, summaries, escalation tiers, distributed cache, feature flags,
Keycloak, cross-channel customer identity) will be revisited only when
production pain justifies them.
