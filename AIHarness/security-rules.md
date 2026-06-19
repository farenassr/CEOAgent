# Security Rules

This document is the short security checklist for agents.

## Secrets

Never commit:

- API keys
- provider access tokens
- webhook secrets
- refresh tokens
- passwords
- signing keys
- raw connection strings

Database rows store credential references only, such as `kv://...`.
Accepted credential reference formats are:

- `kv://...` aliases resolved through configuration-backed Key Vault mappings.
- `config://...` keys for local or test configuration secrets.
- `https://*.vault.azure.net/secrets/...` Azure Key Vault secret URIs.

Reject raw tokens, bearer strings, inline JSON, unsupported schemes, and
provider credential material in metadata at the API edge.

## Webhooks

- Verify provider signatures before processing.
- Use constant-time comparison for HMAC signatures.
- Apply idempotency and replay protection.
- Return quickly; enqueue long work.
- Do not log raw webhook payloads by default.
- Do not log signature prefixes, raw query strings, remote IP addresses, user
  agents, or provider response bodies by default on noisy webhook/provider
  paths.

## Multi-Tenancy

- Every organization-owned table includes `organization_id`.
- Organization-owned queries must rely on EF Core global query filters.
- Never read `organization_id` from a public request body.
- Resolve inbound WhatsApp organization context from provider channel ID.

## AI Safety

- The model never executes side effects directly.
- SDK automatic function invocation is allowed only through
  `FunctionInvokingChatClient` wired to `AgentFunctionInvocationGuard`.
- Tool calls must be validated by `AgentFunctionInvocationGuard` against the
  enabled `company_tool` policy before a handler runs.
- The guard must derive organization, conversation, inbound message, credential
  references, and idempotency from backend context, never from model arguments.
- Idempotency keys for model-requested mutations must include conversation id,
  inbound message id, function name, and a normalized argument hash.
- Configure strict SDK loop limits with `AgentRuntime:MaximumToolIterationsPerRequest`
  and disable parallel tool execution unless a use case is explicitly designed
  for it.
- Return only sanitized tool result JSON to the model; never return credential material.
- No live LLM calls in CI.
- Do not log prompt/completion text in production by default.
- Do not send full conversation history to the model.
- `messages` is audit/outbox history. LLM memory must use the provider session
  state stored on `conversation`, with idle and turn-count expiration configured
  under `AgentRuntime`.
- GenAI and tool telemetry may include non-sensitive tags such as tenant,
  conversation, channel, provider, model, tool key, status, retry count, and
  failure reason. It must not include prompts, completions, customer message
  text, audio transcripts, raw tool arguments, or credential material by
  default.

## Logging

Allowed by default:

- correlation IDs
- organization IDs
- conversation IDs
- provider names
- tool keys
- status and failure reason codes
- message IDs and channel IDs

Avoid by default:

- customer phone numbers
- raw customer message text
- audio transcripts
- prompt/completion text
- raw tool arguments or tool result content
- provider secrets
- raw external payloads
