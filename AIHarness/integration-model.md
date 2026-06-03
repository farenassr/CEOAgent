# Integration Model

This document defines how external systems enter CeoAgent.

## Boundary Rule

Business logic talks to ports. Provider-specific code lives behind adapters.

```text
Business workflow
  -> CeoAgent.Integrations port
  -> CeoAgent.Adapters provider implementation
  -> external provider
```

Within integration areas, put port interfaces in `Abstractions` and
provider-neutral request/result records in `Models` when the folder split helps
readability. When a folder split is used, namespaces should include the same
folder segment. Keep provider SDK clients, Refit DTOs, and provider-specific
implementation abstractions inside `CeoAgent.Adapters`.

## Current Ports

| Port | Project | Purpose |
| --- | --- | --- |
| `IMessageChannelIntegration` | `CeoAgent.Integrations/Messaging` | Send and receive channel messages. WhatsApp Cloud is the MVP provider. |
| `IWhatsAppChannelCredentialResolver` | `CeoAgent.Integrations/Messaging` | Resolve WhatsApp credential references for a company channel. |
| `ICalendarIntegration` | `CeoAgent.Integrations/Calendar` | Calendar availability and reservation operations. Google Calendar is the MVP provider. |
| `IAgentRuntime` | `CeoAgent.Integrations/AI` | Provider-neutral model runtime. OpenAI is the MVP LLM provider and is implemented in adapters. |
| Speech ports | `CeoAgent.Integrations/Speech` | Transcription and speech synthesis workflows when enabled. |
| Job contracts | `CeoAgent.Integrations/Jobs` | Queue payload contracts shared by API and Worker. |

## Adapter Rules

- Keep provider SDKs and Refit clients in `CeoAgent.Adapters`.
- Keep LLM SDK types in `CeoAgent.Adapters`; Worker receives only provider-neutral runtime DTOs.
- Use Refit for typed HTTP clients when practical.
- Use raw `HttpClient` only for streaming, multipart, media download/upload,
  or provider-specific needs.
- Polly is allowed inside adapter-owned HTTP stacks.
- Do not add Polly to Aspire-wired clients.
- Keep one implementation per port until a second provider requires keyed DI.
- The LLM runtime adapter must not execute business side effects automatically.
  Model-requested tools are routed by Worker orchestration into the
  `CeoAgent.Tools` tool gateway.

## Credential Rules

- Credential rows store references, not values.
- Accepted reference style: `kv://company/provider/purpose`.
- Local development uses user-secrets or Aspire parameters.
- Deployed/shared secrets belong in Azure Key Vault.

## Contract Test Template

When adding or changing an adapter, add a focused contract test plan:

```text
Provider:
Port:
Happy path:
Provider error:
Timeout/retry expectation:
Idempotency expectation:
Secret handling expectation:
No-live-call CI strategy:
```
