# Integration Model

This document defines how external systems enter CeoAgent.

## Boundary Rule

Business logic talks to ports. Provider-specific code lives behind integration implementations.

```text
Business workflow
  -> CeoAgent.Application abstraction
  -> CeoAgent.Infrastructure provider implementation
  -> external provider
```

Put port interfaces in `CeoAgent.Application/Abstractions`, provider-neutral
request/result records in `CeoAgent.Shared`, and provider-specific runtime code
in `CeoAgent.Infrastructure/Implementation`. Namespaces must include the same
folder segments.

For AI tool capabilities, keep the complete provider capability under
`CeoAgent.Infrastructure/Implementation/AITools/<ProviderOrCapability>/`.
Provider-backed integration code goes in an `Integration` subfolder, and tool
executors, validators, scheduling policies, and tool-specific helpers stay in
the same provider/capability folder. For example, Google Calendar code belongs
under `Implementation/AITools/GoogleCalendar`, with external provider calls and
SDK factories under `Implementation/AITools/GoogleCalendar/Integration`.
Keep provider SDK clients, Refit DTOs, and provider-specific implementation
abstractions inside Infrastructure implementation folders.

## Current Ports

| Port | Project | Purpose |
| --- | --- | --- |
| `IMessageChannelIntegration` | `CeoAgent.Application/Abstractions/Messaging` | Send and receive channel messages. WhatsApp Cloud is the MVP provider. |
| `IWhatsAppChannelCredentialResolver` | `CeoAgent.Application/Abstractions/Messaging` | Resolve WhatsApp credential references for a company channel. |
| `ICalendarIntegration` | `CeoAgent.Application/Abstractions/AITools/GoogleCalendar` | Calendar availability and reservation operations. Google Calendar is the MVP provider. |
| `IAgentRuntime` | `CeoAgent.Application/Abstractions/AI` | Provider-neutral model runtime. OpenAI is the MVP LLM provider and is implemented in Infrastructure. |
| Job constants/retry contracts | `CeoAgent.Application/Abstractions/Jobs` | Queue names and retry policy shared by API and Worker. |
| Job payloads | `CeoAgent.Shared/Jobs` | Queue payload models shared by API and Worker. |

## Implementation Rules

- Keep provider SDKs and Refit clients in `CeoAgent.Infrastructure/Implementation`
  or implementation-owned API client folders; AI tool provider SDK usage belongs
  under `Implementation/AITools/<ProviderOrCapability>/Integration`.
- Keep LLM SDK types in `CeoAgent.Infrastructure/Implementation`; Worker receives only provider-neutral runtime DTOs.
- Use Refit for typed HTTP clients when practical.
- Use raw `HttpClient` only for streaming, multipart, media download/upload,
  or provider-specific needs.
- Polly is allowed inside implementation-owned HTTP stacks.
- Do not add Polly to Aspire-wired clients.
- Keep one implementation per port until a second provider requires keyed DI.
- The LLM runtime implementation must not execute business side effects automatically.
  Model-requested tools are routed by Worker orchestration into the
  the Infrastructure tool gateway.
- The OpenAI MVP runtime currently uses the OpenAI Responses SDK surface. Keep
  that experimental SDK usage isolated behind `IAgentRuntime`, and reuse
  implementation-owned client instances instead of constructing SDK clients
  inside each agent loop iteration.

## Credential Rules

- Credential rows store references, not values.
- Accepted reference style: `kv://company/provider/purpose`.
- Local development uses user-secrets or Aspire parameters.
- Deployed/shared secrets belong in Azure Key Vault.

## Contract Test Template

When adding or changing a provider implementation, add a focused contract test plan:

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
