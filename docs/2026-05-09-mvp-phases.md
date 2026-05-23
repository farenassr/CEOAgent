# CEOAgent MVP Phased Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.


**Architecture:** The MVP is a modular monolith with a separate API process and Worker process, coordinated locally through Aspire. HTTP use cases are implemented as FastEndpoints vertical slices that dispatch Mediator commands/queries; background work is handled by Worker pipelines; side effects go through ports, adapters, and `ToolExecutionGateway`.

**Tech Stack:** .NET, Aspire, FastEndpoints, martinothamar/Mediator, FluentValidation, EF Core PostgreSQL, Azure Storage Queues, Azure Blob Storage, Semantic Kernel, OpenAI direct, Refit, ZLogger, OpenTelemetry, Langfuse, TUnit, Shouldly, NSubstitute, Verify, Testcontainers.

---

## Current Repository Baseline

The repository currently has the default Aspire-style starter projects:

- `CEOAgent.AppHost`
- `CEOAgent.ApiService`
- `CEOAgent.ServiceDefaults`
- `CEOAgent.Web`

The target MVP layout from `AGENTS.md` is:

- `CEOAgent.AppHost`
- `CEOAgent.ServiceDefaults`
- `CEOAgent.ApiService`
- `CEOAgent.Worker`
- `CEOAgent.Application`
- `CEOAgent.Infrastructure`
- `CEOAgent.Integrations`
- `CEOAgent.Adapters`
- `CEOAgent.Tools`
- `tests/*`

The first phase must therefore convert the starter shape into the target backend shape before feature work begins. `CEOAgent.Web` is not part of the MVP backend unless a separate admin UI is later requested.

## Phase Principles

- Each phase must leave the solution building.
- Each phase must include tests for the highest-risk behavior introduced in that phase.
- Use Mediator for every new use case in API and Worker.
- Keep `Program.cs` small; register modules through extension classes.
- Do not call WhatsApp, Google Calendar, OpenAI, queues, blobs, or databases directly from business logic. Use ports/adapters or infrastructure services.
- Do not hardcode model names. Resolve them from `agent_profile`.
- Do not send the full conversation transcript to the model. Send the last 8 eligible turns.
- Every company-owned table and query must use company isolation from the beginning.

---

## Phase 0: Foundation and Project Shape

**Purpose:** Replace the starter template layout with the target backend solution structure and shared build rules.

**Deliverables:**

- Add class library projects:
  - `CEOAgent.Application`
  - `CEOAgent.Infrastructure`
  - `CEOAgent.Integrations`
  - `CEOAgent.Adapters`
  - `CEOAgent.Tools`
- Add worker project:
  - `CEOAgent.Worker`
- Add test projects under `tests/`:
  - `tests/Api.Tests`
  - `tests/Worker.Tests`
  - `tests/Application.Tests`
  - `tests/Integration.Tests`
- Add root `Directory.Build.props`.
- Add root `.editorconfig`.
- Update `CEOAgent.slnx` to reference the target projects.

**Required packages by area:**

- API: FastEndpoints, FluentValidation, Mediator, ZLogger, OpenTelemetry, Aspire PostgreSQL/Queues/Blobs/OpenAI client integrations.
- Worker: Mediator, FluentValidation, ZLogger, OpenTelemetry, Aspire PostgreSQL/Queues/Blobs/OpenAI client integrations.
- Infrastructure: EF Core PostgreSQL, EFCore.NamingConventions, Azure Storage Queue/Blob clients.
- Adapters: Refit, Polly where adapters build their own HTTP stack.
- Tests: TUnit, Shouldly, NSubstitute, Verify, Testcontainers, Aspire Testing.

**Tasks:**

- [x] Create the target project structure.
- [x] Wire project references according to dependency direction:
  - API references Application, Infrastructure, Integrations, Adapters, Tools, ServiceDefaults.
  - Worker references Application, Infrastructure, Integrations, Adapters, Tools, ServiceDefaults.
  - Application references Integrations.
  - Infrastructure references Application as needed for persistence abstractions and entity access.
  - Adapters references Integrations.
  - Tools references Application and Integrations.
- [x] Add nullable, warnings-as-errors, latest language version, code style enforcement.
- [x] Add a minimal build test for the solution.

**Verification:**

```powershell
dotnet build CEOAgent.slnx
```

Expected: build succeeds with zero warnings.

---

## Phase 1: Runtime Shell, Observability, and Health

**Purpose:** Establish the API and Worker runtime shells, global error handling, correlation, logging, health checks, and Aspire orchestration.

**Deliverables:**

- `CEOAgent.AppHost/AppHost.cs` references PostgreSQL, Azurite queues/blobs, OpenAI connection string, API, and Worker (If they are already there ignore).
- API exposes `/health`.
- Worker exposes `/health` if hosted with an HTTP surface; otherwise health is verified through Aspire resource status and logs.
- API has a single global `IExceptionHandler` producing `ProblemDetails`.
- API has correlation middleware for `X-Correlation-Id`.
- API and Worker use ZLogger and OpenTelemetry.
- Langfuse OTLP exporter is configured from:
  - `LANGFUSE_HOST`
  - `LANGFUSE_PUBLIC_KEY`
  - `LANGFUSE_SECRET_KEY`

**Tasks (Check and if they are complete then ignore and continue with the next one):**

- [x] Implement `ProblemDetails` exception mapping for 404, 409, 422, 499, 500, and 503.
- [x] Add `NotFoundException`, `BusinessRuleException`, and `IntegrationException`.
- [x] Add correlation ID middleware and logging scope.
- [x] Register OpenTelemetry sources:
  - `Microsoft.SemanticKernel*`
  - `OpenAI.*`
  - `CeoAgent.*`
- [x] Add `/health` with PostgreSQL, Queue, and Blob checks.
- [x] Update AppHost resources and environment variable pass-through.
- [x] Add tests for exception mapping and correlation response extensions.

**Verification:**

```powershell
dotnet build CEOAgent.slnx
dotnet test CEOAgent.slnx
dotnet run --project CEOAgent.AppHost/CEOAgent.AppHost.csproj
```

Expected: solution builds, tests pass, Aspire starts API and Worker, `/health` returns healthy when dependencies are available.

---

## Phase 1.5: Secrets and Azure Key Vault Foundation

**Purpose:** Establish the MVP secret/configuration boundary without expanding
into a production identity system. Runtime resource connection strings remain
Aspire-managed; deployed/shared application secrets are represented through
Azure Key Vault; local developer inputs use user-secrets or environment
variables.

**Deliverables:**

- `CEOAgent.AppHost` includes the Aspire Azure Key Vault hosting package.
- AppHost defines a Key Vault resource for deployed/shared secrets.
- AppHost passes selected secrets/config values to API and Worker:
  - API receives `Authentication:AdminApiKey`.
  - API and Worker receive Langfuse keys.
- Aspire-managed PostgreSQL, queue, and blob connection strings continue to
  flow through existing `.WithReference(...)` calls.
- `AppDbContextFactory` reads `ConnectionStrings:CEOAgent` from configuration
  instead of hardcoding a local PostgreSQL connection string.
- Company integration credential tables store references such as `kv://...`
  only. They never store raw provider secrets.
- Local setup documentation explains user-secrets for EF design-time usage.

**Rules:**

- Aspire `.WithReference(...)` manages internal runtime resource connection
  strings for PostgreSQL, Azure Storage Queues, and Azure Blob Storage.
- Azure Key Vault is the target store for deployed/shared secrets.
- User-secrets and environment variables are local development inputs only.
- `AppDbContextFactory` reads `ConnectionStrings:CEOAgent` from
  `appsettings.json`, `appsettings.Development.json`, user-secrets, or
  environment variables, and fails fast when the value is missing.
- Company integration credential rows store references only, for example
  `kv://company/provider/credential`.
- This phase does not add Keycloak, JWT, users, roles, or a broader production
  identity/security system. Static admin API key authentication remains the
  MVP mechanism.

**Tasks:**

- [x] Add `Aspire.Hosting.Azure.KeyVault` to `CEOAgent.AppHost`.
- [x] Add a Key Vault resource in AppHost for publish/deployed mode.
- [x] Pass selected Key Vault secrets to API and Worker as environment/config
  values.
- [x] Keep Aspire-managed PostgreSQL connection string via
  `.WithReference(postgres)`.
- [x] Keep Aspire-managed queue/blob wiring via `.WithReference(queues)` and
  `.WithReference(blobs)`.
- [x] Update `AppDbContextFactory` to read `ConnectionStrings:CEOAgent` from
  configuration.
- [x] Add focused tests for design-time factory configuration behavior.
- [x] Add documentation for local user-secrets setup.

### Local EF design-time connection string

For local `dotnet ef` and other design-time EF commands, set the
`ConnectionStrings:CEOAgent` value in user-secrets for the Infrastructure
project:

```powershell
dotnet user-secrets set "ConnectionStrings:CEOAgent" "<postgres-connection-string>" --project CEOAgent.Infrastructure
```

This setting is only for local design-time EF usage. Runtime local connection
strings come from Aspire when running AppHost and must continue to flow through
`.WithReference(postgres)`.

Design-time EF configuration enables model inspection and migration
scaffolding. Applying migrations is intentionally manual: AI agents must not
run `dotnet ef database update` or otherwise apply migrations to a database.
The project owner chooses when to run migrations locally, in staging, or in
production.

**Verification:**

```powershell
dotnet build CEOAgent.slnx
dotnet test tests/Integration.Tests/Integration.Tests.csproj
dotnet run --project CEOAgent.AppHost/CEOAgent.AppHost.csproj
```

Expected: solution builds, design-time factory tests pass, AppHost starts
locally, and API/Worker receive their configured local parameter values. Full
Azure Key Vault access requires deployed Azure resources and credentials, so
live Key Vault behavior is not verified by local tests.

---

## Phase 2: Persistence, Company, and Admin Authentication

**Purpose:** Build the database foundation, company isolation model, and static admin API key authentication for manual onboarding.

**Deliverables:**

- One `AppDbContext`.
- EF Core snake_case naming convention.
- Company context abstraction and middleware.
- EF global query filters for company-owned entities.
- Admin API key authentication scheme: `AdminApiKey`.
- MVP secrets foundation for admin auth:
  - Local run mode uses Aspire parameters/user-secrets.
  - Publish mode uses the existing Azure Key Vault.
- Initial entities and configurations:
  - `Company`
  - `CompanyChannel`
  - `AgentProfile`
  - `CompanyTool`
  - `IntegrationCredentialReference`
  - `Customer`
  - `Conversation`
  - `Message`
  - `ConversationState`
  - `ToolExecution`
  - `AudioAsset`
- Initial migration in `CEOAgent.Infrastructure/Persistence/Migrations/`.

**Admin endpoint slices:**

- `POST /v1/admin/companies`
- `POST /v1/admin/companies/{companyId}/channels`
- `POST /v1/admin/companies/{companyId}/agent-profile`
- `POST /v1/admin/companies/{companyId}/integration-credentials`
- `POST /v1/admin/companies/{companyId}/tools`

**Tasks:**

- [x] Create company context interfaces and middleware.
- [x] Create entities with `Guid.CreateVersion7()`, `company_id`, `created_at`, and `updated_at` where required.
- [x] Configure singular table names and enum-as-string conversions.
- [x] Add global query filters for company-owned entities.
- [x] Add admin API key authentication handler.
- [x] Add Key Vault publish-mode wiring while keeping local secrets in Aspire parameters/user-secrets.
- [x] Implement admin onboarding slices with FastEndpoints, FluentValidation, and Mediator.
- [x] Add company isolation tests proving cross-company access returns 404.
- [x] Add migration file. Applying it remains a manual operator step.

Implementation comment: Phase 2 added the shared company context, `AppDbContext`, company-owned entities and EF configurations, scoped company query filters, static `AdminApiKey` authentication, MVP secret wiring with local Aspire parameters/user-secrets and publish-mode Azure Key Vault, FastEndpoints/Mediator admin onboarding routes, focused admin-auth and company-isolation tests, and the initial persistence migration under `CEOAgent.Infrastructure/Persistence/Migrations/`.

**Verification:**

```powershell
dotnet build CEOAgent.slnx
dotnet test CEOAgent.slnx --filter "Company|Admin|Persistence"
```

Expected: company-owned queries are filtered by ambient company context; admin endpoints require `X-Admin-Api-Key`.

---

## Phase 3: Integration Ports, Queue, Blob, and Job Contracts

**Purpose:** Define the durable async processing contracts and external integration seams before implementing provider-specific behavior.

**Deliverables:**

- Port contracts in `CEOAgent.Integrations`:
  - `IMessageChannelIntegration`
  - `ICalendarIntegration`
  - `ITranscriptionIntegration`
  - `ISpeechSynthesisIntegration`
  - `IChatCompletionFactory`
- Queue abstractions in Infrastructure.
- Blob media storage abstraction in Infrastructure.
- Worker job contracts:
  - `ProcessIncomingMessageJob`
  - `TranscribeAudioJob`
  - `SynthesizeAudioJob`
  - `SendOutboundMessageJob`
  - `CloseInactiveConversationsJob`
- Queue concurrency set to 1 per queue for MVP.
- Dead-letter behavior for high dequeue count.

**Tasks:**

- [ ] Define port DTOs without provider-specific leakage except provider enum/string identifiers.
- [ ] Implement Azure Storage Queue sender/receiver wrappers.
- [ ] Implement Blob media storage for audio assets.
- [ ] Add source-generated JSON serialization metadata for job payloads.
- [ ] Add Worker hosted services for queue polling.
- [ ] Add idempotent job handler skeletons.
- [ ] Add tests for queue message serialization and duplicate job handling.

**Verification:**

```powershell
dotnet build CEOAgent.slnx
dotnet test CEOAgent.slnx --filter "Queue|Blob|Job"
```

Expected: queue payloads serialize deterministically and Worker can receive known job types.

---

## Phase 4: WhatsApp Webhook Ingestion

**Purpose:** Receive WhatsApp Cloud messages securely, resolve companies by channel, persist inbound messages, enqueue processing, and return quickly.

**Deliverables:**

- `POST /v1/webhooks/whatsapp`
- Raw-body HMAC SHA-256 verification using `X-Hub-Signature-256`.
- Constant-time signature comparison.
- Company resolution by `("whatsapp_cloud", metadata.phone_number_id)`.
- Customer resolution by `messages[0].from` or `contacts[0].wa_id`.
- Open conversation lookup or creation.
- Inbound message persistence.
- Unique constraint on `(company_id, provider_message_id)` filtered to non-null provider message IDs.
- Duplicate webhook returns `200 OK` without enqueueing.
- Text and voice-note inbound recognition.

**Tasks:**

- [ ] Implement raw request body capture before JSON deserialization.
- [ ] Implement WhatsApp signature verifier.
- [ ] Implement webhook payload models with source-generated JSON metadata.
- [ ] Implement company lookup through `CompanyChannel`.
- [ ] Implement customer creation by `(company_channel_id, external_customer_id)` and conversation creation by `(company_id, customer_id, company_channel_id)`, snapshotting `agent_profile_id`.
- [ ] Persist inbound text messages.
- [ ] Persist inbound audio metadata and enqueue transcription when voice note media is present.
- [ ] Enqueue `ProcessIncomingMessageJob` for text messages.
- [ ] Add tests for valid signature, invalid signature, missing channel, duplicate message, and fast response path.

**Verification:**

```powershell
dotnet test CEOAgent.slnx --filter "WhatsApp|Webhook"
```

Expected: invalid signatures return 401; duplicate provider messages return 200 and do not enqueue again.

---

## Phase 5: Conversation, Prompt, and Agent Runtime

**Purpose:** Build the AI turn orchestration without enabling side effects directly from the model.

**Deliverables:**

- `PromptBuilder` in `CEOAgent.Application`.
- `AgentRunner` in `CEOAgent.Application`.
- Structured output contract:
  - assistant message
  - optional tool call request
  - conversation state patch
  - handoff request signal
- Last 8 eligible turns only:
  - `user`
  - `assistant`
  - `tool_call`
  - `tool_result`
- System messages excluded from model context.
- Per-company model resolved from `agent_profile.model_name`.
- One structured-output retry before human handoff.
- Agent loop iteration cap.
- Activity tags for company, conversation, customer, correlation, provider, model, and prompt version.

**Tasks:**

- [ ] Implement conversation turn projection query.
- [ ] Implement `PromptBuilder` with company context, local date/time, branch settings, and full enabled tool catalog.
- [ ] Implement `IChatCompletionFactory` adapter for OpenAI/Semantic Kernel.
- [ ] Implement structured output validation and post-deserialization checks.
- [ ] Implement `AgentRunner` loop without executing side effects directly.
- [ ] Persist assistant messages, tool calls, tool results, and state patches.
- [ ] Add Verify snapshot tests for prompts.
- [ ] Add tests for last-8-turn selection, model-name resolution, malformed output rejection, retry, and loop cap handoff.

**Verification:**

```powershell
dotnet test CEOAgent.slnx --filter "Agent|Prompt|StructuredOutput"
```

Expected: prompt snapshots are stable; no full transcript is sent; malformed model output triggers retry then handoff.

---

## Phase 6: Tool Registry and Tool Execution Gateway

**Purpose:** Add the single chokepoint for every model-requested side effect.

**Deliverables:**

- `IToolHandler`
- `ICompanyToolRegistry`
- `IToolHandlerFactory`
- `ToolExecutionGateway`
- Company-enabled tool catalog from `company_tool`.
- Tool execution persistence.
- Denial reasons:
  - unknown tool
  - tool disabled for company
  - invalid parameters
  - cross-company conversation
  - idempotent duplicate
- Failure counter for two consecutive failures of the same operation type within the same conversation turn.

**Native MVP tools:**

- `check_availability`
- `request_human_handoff`

**Tasks:**

- [ ] Define canonical tool request and result schemas.
- [ ] Implement company tool registry query.
- [ ] Implement handler factory with one implementation per tool key.
- [ ] Implement gateway authorization, validation, idempotency, execution, and persistence.
- [ ] Implement failure counting and handoff trigger.
- [ ] Add tests for all denial reasons, company-specific registry behavior, duplicate request handling, and two-failure handoff.

**Verification:**

```powershell
dotnet test CEOAgent.slnx --filter "ToolExecutionGateway|CompanyToolRegistry"
```

Expected: the gateway denies unsafe calls before invoking handlers and triggers handoff after repeated same-operation failures.

---



**Deliverables:**

  - check availability
  - close conversation manually if needed by staff
  - cannot create outside working hours
  - cannot create in the past
  - cannot confirm without date, time, party size, name, and customer confirmation
  - cannot double-book `external_calendar_event_id`
  - party size must respect company capacity
- `ICalendarIntegration` Google Calendar adapter.
- Refit clients for Google Calendar where appropriate.
- Polly only inside adapter-owned HTTP stack.

**Tasks:**

- [ ] Implement Google Calendar adapter with idempotency key derived from company, conversation, tool key, and canonical parameters.
- [ ] Implement `check_availability` tool handler.
- [ ] Add tests for business rules, concurrency conflict mapping, and adapter contract behavior.

**Verification:**

```powershell
```


---

## Phase 8: Audio Inbound, Transcription, TTS, and Outbound Messaging

**Purpose:** Complete the WhatsApp text/audio loop, including voice-note transcription and optional voice replies.

**Deliverables:**

- WhatsApp adapter implements inbound media download and outbound text/audio send.
- Transcription adapter implements `ITranscriptionIntegration`.
- Speech synthesis adapter implements `ISpeechSynthesisIntegration`.
- Inbound voice notes:
  - download media to Blob Storage
  - persist `AudioAsset`
  - transcribe once
  - on transcription failure, trigger human handoff
  - on success, continue agent processing as user turn
- Outbound replies:
  - always send text reply when available
  - synthesize voice reply when needed
  - TTS failure does not block text reply
  - store outbound TTS media in Blob Storage
- Outbound idempotency using `client_message_id`.

**Tasks:**

- [ ] Implement WhatsApp media download and send methods behind `IMessageChannelIntegration`.
- [ ] Implement transcription job handler.
- [ ] Implement synthesis job handler.
- [ ] Implement outbound message job handler.
- [ ] Store inbound and outbound audio assets in Blob Storage.
- [ ] Add tests for transcription failure handoff, TTS failure text fallback, and outbound idempotency.

**Verification:**

```powershell
dotnet test CEOAgent.slnx --filter "Audio|Transcription|Speech|Outbound"
```

Expected: inbound audio can become an agent turn; failed transcription hands off; failed TTS still sends text.

---

## Phase 9: Worker Pipelines and Conversation Lifecycle

**Purpose:** Connect webhook ingestion, agent execution, tool execution, outbound sending, and conversation lifecycle into reliable background flows.

**Deliverables:**

- `ProcessIncomingMessageJob` loads conversation, runs agent loop, executes tool requests through gateway, persists results, and queues outbound sends.
- `CloseInactiveConversationsJob` closes open conversations with no inbound or outbound activity for 24 hours.
- Handed-off conversations are not auto-closed.
- Explicit staff close endpoint:
  - `POST /v1/conversations/{id}/close`
- Conversation state cleared on closure.
- Poison messages dead-letter after configured threshold.

**Tasks:**

- [ ] Implement process-incoming-message pipeline.
- [ ] Implement trace/correlation propagation from queue payloads.
- [ ] Implement inactive conversation closure query and job.
- [ ] Implement explicit close endpoint.
- [ ] Ensure handoff pauses autonomous replies.
- [ ] Add tests for conversation closure, handed-off exclusion, state clearing, and dead-letter behavior.

**Verification:**

```powershell
dotnet test CEOAgent.slnx --filter "ProcessIncomingMessage|Conversation|CloseInactive"
```

Expected: full text message flow runs through Worker without API doing long-running work; conversation lifecycle rules match `AGENTS.md`.

---

## Phase 10: End-to-End Integration, Hardening, and MVP Acceptance

**Purpose:** Prove the MVP path works end to end and close reliability, security, and observability gaps.

**Deliverables:**

- Aspire integration test for local stack:
  - PostgreSQL
  - Queue
  - Blob
  - API
  - Worker
- Stubbed external provider tests:
  - WhatsApp Cloud
  - Google Calendar
  - OpenAI chat completion
  - transcription
  - TTS
- No live LLM calls in CI.
- Operational runbook for local onboarding and smoke test.
- Metrics emitted for:
  - inbound messages per company
  - outbound messages per company
  - model token usage per company
  - tool calls by company/tool/outcome
  - handoff rate
  - queue lag
  - dead-letter counts
  - TTS/transcription success rate

**Tasks:**

- [ ] Add integration test harness with Aspire Testing and Testcontainers.
- [ ] Add deterministic OpenAI structured-output fixtures.
- [ ] Add provider stubs or WireMock-style adapters for integration tests.
- [ ] Add a scripted manual onboarding smoke test document.
- [ ] Verify no company-owned query bypasses global filters.
- [ ] Verify no business logic calls external systems outside ports.
- [ ] Verify all routes are under `/v1/` except `/health`.
- [ ] Verify sensitive prompt/completion tracing is disabled by default in production.

**Verification:**

```powershell
dotnet build CEOAgent.slnx
dotnet test CEOAgent.slnx
dotnet run --project CEOAgent.AppHost/CEOAgent.AppHost.csproj
```


---

## MVP Acceptance Checklist

- [ ] API and Worker are separate runnable processes.
- [ ] Aspire starts PostgreSQL, Queue, Blob, OpenAI connection, API, and Worker.
- [ ] Admin onboarding works through static API key protected endpoints.
- [ ] WhatsApp webhook verifies HMAC signatures before deserialization.
- [ ] Company is resolved by provider channel ID, never customer phone number.
- [ ] Duplicate WhatsApp messages do not enqueue duplicate work.
- [ ] Company-owned data uses global EF query filters.
- [ ] Conversation history is persisted as raw turns.
- [ ] Model context includes only the last 8 eligible turns.
- [ ] Company model name is read from `agent_profile`.
- [ ] Model output is structured and validated.
- [ ] Every side-effect tool call goes through `ToolExecutionGateway`.
- [ ] Four native tools are available through company tool registry.
- [ ] Inbound voice notes are stored, transcribed, and processed.
- [ ] Outbound TTS voice replies are supported.
- [ ] TTS failure does not block text replies.
- [ ] Two consecutive failures of the same operation type trigger handoff.
- [ ] Agent loop cap triggers handoff.
- [ ] Handed-off conversations pause autonomous replies.
- [ ] Open inactive conversations close after 24 hours.
- [ ] Handed-off conversations are not auto-closed.
- [ ] `/health` is unversioned and checks required dependencies.
- [ ] Langfuse receives LLM spans without production prompt/completion text by default.
- [ ] No live LLM calls run in CI.

## Suggested Commit Sequence

1. `CEOAgent.ApiService/CEOAgent.Tools/..etc.. :[{githubIssueId}] establish mvp solution structure`

## Deferred Until After MVP

- Keycloak or user dashboard authentication.
- Self-service company onboarding.
- Dedicated database per company.
- Outbox pattern.
- Customer long-term memory.
- Conversation rolling summaries.
- RAG or knowledge retrieval.
- Payments, CRM, POS, or Excel integrations.
- Multiple calendar providers.
- Multiple providers per port with keyed DI.
- Model escalation tiers.
- Distributed cache.
- Feature flags table.
- Cross-channel customer identity unification.
