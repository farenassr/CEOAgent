# CeoAgent

Backend SaaS multi-tenant in .NET for business conversations assisted by AI.

CeoAgent is designed for restaurants that need to handle WhatsApp business
conversations, process text or audio, execute approved tools, and synchronize
actions with external systems such as Google Calendar. The MVP starts with
WhatsApp, while the conversational core can later support channels such as
Telegram, Instagram DM, or web chat.

## What This Builds

CeoAgent receives inbound messages, identifies the company by channel,
persists the conversation, runs an agent based on Microsoft Agent Framework,
validates every model-requested action, and replies through the relevant
channel.

MVP capabilities:

- WhatsApp Cloud API for inbound and outbound messages.
- Voice note support, transcription, and audio responses.
- Multi-tenant resolution by `(provider, provider_channel_id)`.
- Company-configurable AI profile, prompt, and model.
- Dynamic catalog of company-enabled tools.
- Safe tool execution: the LLM never executes side effects directly.
- PostgreSQL persistence with global filters by `organization_id`.
- Azure Storage Queues for background jobs.
- Azure Blob Storage for media and attachments.
- OpenTelemetry, ZLogger, and Langfuse observability.
- API, integration, and worker tests with TUnit.

## Architecture

CeoAgent is a modular monolith. The API and Worker run as separate processes,
but share the domain model, database, and internal contracts.

```text
WhatsApp Cloud
  |
  v
CeoAgent.ApiService
  |  validates webhook, resolves company, persists message, enqueues work
  v
Azure Storage Queue
  |
  v
CeoAgent.Worker
  |  runs agent, validates tools, calls integrations, sends reply
  v
PostgreSQL + Blob Storage + external integrations
```

Core principles:

- Vertical slices for use cases.
- Ports and implementations for external integrations.
- Microsoft Agent Framework for agent and LLM runtime.
- Controlled tool handlers for side effects.
- Strongly typed options and startup validation.

## Solution Shape

Production projects live under `src/`; test projects live under `tests/`.

| Project | Role |
| --- | --- |
| `src/CeoAgent.AppHost` | Local .NET Aspire orchestration for API, Worker, PostgreSQL, queues, blobs, and Key Vault parameters. |
| `src/CeoAgent.ApiService` | FastEndpoints HTTP surface, admin endpoints, webhook receiver, errors, OpenAPI, and Scalar. |
| `src/CeoAgent.Worker` | Background processing, jobs, agent execution, tools, and integrations. |
| `src/CeoAgent.ServiceDefaults` | Health checks, OpenTelemetry, service discovery, and baseline resilience. |
| `src/CeoAgent.Application` | Application abstractions and prompt behavior. |
| `src/CeoAgent.Infrastructure` | EF Core entities, persistence, provider implementations, and native MVP tool handlers. |
| `src/CeoAgent.Shared` | Public DTOs, shared enums, and provider-neutral runtime/tool models. |
| `tests/*` | API, integration, and Worker tests. |

## Agent And Harness Rules

The normative guide for agents is [AGENTS.md](./AGENTS.md). Do not duplicate
architecture, security, database, subagent, validation, or commit-message rules
in this README.

The harness index and progressive-disclosure docs are in
[AIHarness/harness-engineering.md](./AIHarness/harness-engineering.md).

Review prompts are indexed in [docs/reviewer.md](./docs/reviewer.md).

## Configuration

The project uses strongly typed options with startup validation.

Relevant sections:

- `Api`
  - CORS.
  - Rate limiting.
- `Persistence`
  - PostgreSQL mode.
- `ServiceDefaults`
  - OTLP.
  - Langfuse.

Local example:

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
    "InMemoryDatabaseName": "CeoAgent"
  }
}
```

## Local Development

Requirements:

- .NET SDK compatible with `net10.0`.
- Docker Desktop or a compatible container runtime for Aspire.
- PowerShell, Windows Terminal, or equivalent shell.

Restore and build:

```powershell
dotnet restore CeoAgent.slnx
dotnet build CeoAgent.slnx
```

Run tests:

```powershell
dotnet test CeoAgent.slnx
```

Run with Aspire:

```powershell
dotnet run --project src/CeoAgent.AppHost/CeoAgent.AppHost.csproj
```

The API exposes:

```text
/health
/scalar
```

Local infrastructure and Azure phase notes live in
[docs/azure-infrastructure.md](./docs/azure-infrastructure.md). Local
development uses Aspire with PostgreSQL and Azurite for Queue/Blob. Azure Key
Vault is reserved for shared publish/deploy secrets.

## Local Secrets

Configure the local PostgreSQL password for Aspire:

```powershell
dotnet user-secrets set "Parameters:postgres-password" "postgres" --project src/CeoAgent.AppHost
```

Configure the local EF Core design-time connection string:

```powershell
dotnet user-secrets set "ConnectionStrings:CeoAgent" "Host=localhost;Port=5432;Database=CeoAgent;Username=postgres;Password=postgres" --project src/CeoAgent.Infrastructure
```

Configure Langfuse for Aspire:

```powershell
dotnet user-secrets set "Parameters:langfuse-host" "https://cloud.langfuse.com" --project src/CeoAgent.AppHost
dotnet user-secrets set "Parameters:langfuse-public-key" "<langfuse-public-key>" --project src/CeoAgent.AppHost
dotnet user-secrets set "Parameters:langfuse-secret-key" "<langfuse-secret-key>" --project src/CeoAgent.AppHost
```

With Aspire running and the connection string configured, applying migrations
is a manual action:

```powershell
dotnet ef database update --project src\CeoAgent.Infrastructure\CeoAgent.Infrastructure.csproj
```

For publish/runtime, API and Worker receive connection strings through Aspire or
Azure via `.WithReference(...)`; they should not depend on local user-secrets.

## Testing

Test projects:

- `tests/CeoAgent.ApiService.Tests`: endpoints, errors, mappers, and HTTP contracts.
- `tests/CeoAgent.IntegrationTests`: EF Core, relational model, JSONB, and multi-tenant isolation.
- `tests/CeoAgent.Worker.Tests`: Worker processing and tool execution coverage.

Recommended command:

```powershell
dotnet test CeoAgent.slnx --no-build
```

## Observability

CeoAgent separates general observability from LLM observability:

- OpenTelemetry for standard traces, metrics, and instrumentation.
- ZLogger for structured logging.
- Langfuse for GenAI traces, prompts, tool calls, token usage, latency, and cost.

In production, textual prompt and completion content should be disabled by
default to reduce PII exposure.

## Security And Multi-Tenancy

- Organization-owned tables contain `organization_id`.
- Authenticated admin paths resolve `organization_id` from the Keycloak `organization` claim.
- Customer phone numbers do not identify companies.
- Companies are resolved by channel, such as WhatsApp `phone_number_id`.
- Webhooks are authorized by provider HMAC signatures.
- Provider credentials are stored as references such as `kv://...`, never raw secrets.

## Current Focus

The immediate backend focus remains the MVP flow:

- WhatsApp webhook.
- Conversational persistence.
- Queue processing.
- Agent runner.
- Tool handlers.
- Real integration implementations.
- Google Calendar integration.
- End-to-end LLM observability.

## Useful Links

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

## Contributing

Before changing code, read [AGENTS.md](./AGENTS.md). It contains the project
workflow, validation commands, commit-message format, and completion rules.
