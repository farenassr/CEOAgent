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
| `IGoogleCalendarIntegration` | `CeoAgent.Application/Abstractions/AITools/GoogleCalendar` | Calendar availability and reservation operations. Google Calendar is the MVP provider. |
| `IPaymentQrImageProvider` | `CeoAgent.Application/Abstractions/Payments` | Reads backend-owned QR image bytes for reservation payment instructions. |
| `IAgentRuntime` | `CeoAgent.Application/Abstractions/AI` | Provider-neutral model runtime. OpenAI, Gemini, and local Ollama implementations live in Infrastructure. |
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
- The Worker must not build LLM history arrays from `messages` or run a manual
  tool loop. It calls `IAgentRuntime.RunTurnAsync(...)` once per inbound text
  message.
- The LLM runtime owns model sessions and tool-loop orchestration through
  Microsoft Agent Framework, but business side effects still run only after the
  backend guard validates company policy and idempotency.
- `send_payment_instructions` is an internal AI tool under
  `CeoAgent.Infrastructure/Implementation/AITools/Payments`. It has no model
  arguments; it derives the current conversation, latest successful reservation,
  default active payment account, QR blob reference, and WhatsApp recipient from
  backend state.
- The SDK-first runtime flow is:

```text
Microsoft Agent SDK
  -> FunctionInvokingChatClient
      -> AgentFunctionInvocationGuard
          -> AIFunction
              -> Application service / use case
```

- `messages` remains local audit/outbox state, not the LLM memory source.
  Conversation memory is represented by provider/session fields on
  `conversation` such as `provider_conversation_id`, `provider_last_response_id`,
  `agent_session_json`, and session timestamps.
- `agent_profile` stores company-level model policy. A new `conversation`
  snapshots the effective provider/model so later company config changes apply
  to future conversations, not active sessions.
- OpenAI Responses and Gemini Developer API run behind `IAgentRuntime`.
  Foundry, Claude, and DeepSeek may be added behind the same boundary without
  changing Worker orchestration.

## Credential Rules

- Credential rows store references, not values.
- Accepted reference style: `kv://company/provider/purpose`.
- Local development uses user-secrets or Aspire parameters.
- Deployed/shared secrets belong in Azure Key Vault.
- LLM provider API keys are exposed to the app as stable secret references such
  as `kv://llm/openai/api-key`; raw keys must not be stored in appsettings,
  database rows, messages, prompts, traces, or migrations.
- Aspire AppHost should define LLM keys with `AddParameter(..., secret: true)`
  for local/user-secret input and copy published values to Key Vault with
  `AddSecret(...)`.

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
