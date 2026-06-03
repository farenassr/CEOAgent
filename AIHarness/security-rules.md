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

## Webhooks

- Verify provider signatures before processing.
- Use constant-time comparison for HMAC signatures.
- Apply idempotency and replay protection.
- Return quickly; enqueue long work.
- Do not log raw webhook payloads by default.

## Multi-Tenancy

- Every company-owned table includes `company_id`.
- Company-owned queries must rely on EF Core global query filters.
- Never read `company_id` from a public request body.
- Resolve inbound WhatsApp company context from provider channel ID.

## AI Safety

- The model never executes side effects directly.
- Tool calls must be validated by the tool gateway against the enabled company
  tool catalog before a handler runs.
- Do not enable SDK automatic function invocation for business side effects.
- Return only sanitized tool result JSON to the model; never return credential material.
- No live LLM calls in CI.
- Do not log prompt/completion text in production by default.
- Do not send full conversation history to the model.

## Logging

Allowed by default:

- correlation IDs
- company IDs
- conversation IDs
- provider names
- tool keys
- status and failure reason codes

Avoid by default:

- customer phone numbers
- raw customer message text
- audio transcripts
- prompt/completion text
- provider secrets
- raw external payloads
