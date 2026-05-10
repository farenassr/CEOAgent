# CEOAgent

CEOAgent is a .NET multi-tenant SaaS backend for AI-assisted business conversations. The MVP targets restaurants that receive reservations through WhatsApp Cloud, use an AI agent to handle the conversation, and synchronize confirmed bookings with Google Calendar.

The long-term direction is broader than restaurants: the core should support more channels, tools, and integrations without rewriting the conversation engine.

## What We Are Building

The MVP backend receives inbound WhatsApp text or voice messages, resolves the tenant from the WhatsApp phone number ID, stores the conversation, processes the customer request through an AI agent, executes approved business actions through a tool gateway, and sends the reply back through WhatsApp.

Core MVP capabilities:

- WhatsApp Cloud inbound and outbound messaging, including voice notes.
- Tenant resolution by channel, not by customer phone number.
- Conversation history stored as raw turns.
- AI agent execution with tenant-specific model and prompt settings.
- Tool execution through a controlled `ToolExecutionGateway`.
- Google Calendar reservation creation, availability checks, and cancellation.
- Background processing through a Worker and Azure Storage Queues.
- PostgreSQL persistence with tenant isolation.
- Azure Blob Storage for inbound and outbound media.
- Basic observability through OpenTelemetry, ZLogger, and Langfuse for LLM traces.

## Architecture

CEOAgent is a modular monolith, not a microservice system. The API and Worker are separate processes, but they share the same application model and database.

Runtime shape:

```text
WhatsApp Cloud
    |
    v
CEOAgent.ApiService
    |  verifies webhook, resolves tenant, persists message, enqueues job
    v
Azure Storage Queue
    |
    v
CEOAgent.Worker
    |  runs agent, executes tools, calls adapters, sends outbound messages
    v
PostgreSQL / Blob Storage / Google Calendar / OpenAI
```

The important architectural rule is that business logic does not call external systems directly. External systems are reached through ports and adapters, and model-requested side effects must pass through the tool execution gateway.

## Project Layout

Current repository baseline:

- `CEOAgent.AppHost` - Aspire local orchestration for API, Worker, PostgreSQL, queues, blobs, and OpenAI configuration.
- `CEOAgent.ApiService` - HTTP surface, health endpoint, correlation middleware, global error handling, and future FastEndpoints slices.
- `CEOAgent.Worker` - background process for queued jobs and future agent/integration pipelines.
- `CEOAgent.ServiceDefaults` - shared Aspire service defaults, health checks, telemetry, and resilience setup.
- `CEOAgent.Web` - starter web project from the template. It is not part of the backend MVP unless an admin UI is explicitly added later.
- `tests/` - automated tests.
- `docs/superpowers/plans/2026-05-09-mvp-phases.md` - phased implementation plan.
- `AGENTS.md` - authoritative engineering and architecture rules for this project.

Target backend projects from `AGENTS.md`:

- `CEOAgent.Application` - shared business logic such as `AgentRunner`, `PromptBuilder`, and `ToolExecutionGateway`.
- `CEOAgent.Infrastructure` - EF Core persistence, queues, blob storage, and infrastructure glue.
- `CEOAgent.Integrations` - port contracts only.
- `CEOAgent.Adapters` - implementations for WhatsApp, Google Calendar, OpenAI, transcription, and TTS.
- `CEOAgent.Tools` - native tool handlers such as reservation creation and human handoff.

## Main Message Flow

1. WhatsApp sends a webhook to the API.
2. The API verifies the raw-body HMAC signature.
3. The tenant is resolved from `(provider, provider_channel_id)`.
4. The customer and open conversation are found or created.
5. The inbound message is persisted idempotently.
6. A processing job is placed on Azure Storage Queue.
7. The Worker loads the conversation and calls the agent.
8. The agent may request a tool call.
9. `ToolExecutionGateway` validates and executes the tool if allowed.
10. The Worker persists the result and sends the outbound reply.

## Key Rules

- Use FastEndpoints for HTTP use cases.
- Use `martinothamar/Mediator` for all new commands and queries.
- Use FluentValidation for request and command validation.
- Store identifiers as GUID v7.
- Store timestamps in UTC and use `TimeProvider`; do not use `DateTime.Now`.
- Every tenant-owned table must include `tenant_id`.
- Tenant isolation must be enforced through EF Core global query filters.
- Send only the last 8 eligible conversation turns to the model.
- Do not hardcode model names; resolve them from the tenant agent profile.
- Do not add direct external calls inside business logic.

## Local Development

Prerequisites:

- .NET SDK capable of building the solution target framework.
- Docker or another container runtime for Aspire-managed local dependencies.

Build:

```powershell
dotnet build CEOAgent.slnx
```

Test:

```powershell
dotnet test CEOAgent.slnx
```

Run the local Aspire app:

```powershell
dotnet run --project CEOAgent.AppHost/CEOAgent.AppHost.csproj
```

The API exposes the operational health endpoint at:

```text
/health
```

## Current Status

The repository is in the early runtime-shell stage. AppHost, API, Worker, shared service defaults, error handling, correlation handling, and basic tests exist. The full MVP domain model, persistence layer, ports/adapters, tool gateway, WhatsApp ingestion, agent runtime, and reservation workflows are still to be implemented according to `AGENTS.md` and the phased plan in `docs/superpowers/plans/2026-05-09-mvp-phases.md`.
