# WhatsApp Flow

This document captures the WhatsApp Cloud MVP flow for review, tests, and
future evals.

## Inbound Text Flow

Webhook verification uses `GET /v1/whatsapp/webhook` with Meta's
`hub.mode`, `hub.verify_token`, and `hub.challenge` query parameters.

```text
WhatsApp Cloud webhook
  -> ApiService /v1/whatsapp
  -> signature validation
  -> parse payload
  -> resolve company by phone_number_id
  -> resolve or create customer by wa_id/from
  -> resolve or create open conversation
  -> persist inbound message with provider_message_id
  -> persist incoming message outbox row in the same DB transaction
  -> dispatch pending outbox row to enqueue ProcessIncomingMessageJob
  -> return 200 quickly
```

## Required Invariants

- Validate `X-Hub-Signature-256` before processing POST bodies.
- Use constant-time signature comparison.
- Resolve company by provider channel ID, not customer phone number.
- Persist inbound messages idempotently by provider message ID.
- Persist a durable incoming-message outbox row in the same transaction as the
  inbound message before any queue side effect.
- Duplicate webhook delivery returns 200. If the inbound message already has a
  persisted assistant reply, do not enqueue duplicate work. If the inbound
  message exists but no assistant reply exists, dispatch its pending or failed
  outbox row as a recovery path. Already dispatched outbox rows must not enqueue
  duplicate work.
- Queue dispatch failure must leave the outbox row retryable by the dispatcher;
  recovery must not depend on provider redelivery.
- Keep webhook response fast; long work belongs in Worker jobs.
- Do not log secrets or raw payloads by default.

## Inbound Audio Flow

```text
WhatsApp voice note
  -> signature validation
  -> persist message/audio metadata
  -> store media in blob storage when media download is implemented
  -> enqueue transcription job
  -> on transcription success, continue agent processing as user turn
  -> on transcription failure, hand off to human
```

## Outbound Flow

```text
Worker result
  -> persist assistant message
  -> send text via IMessageChannelIntegration
  -> persist provider send reference
  -> optionally synthesize voice
  -> send audio if synthesis succeeds
```

TTS failure must not block a text reply.
Outbound sends must not rely on provider retries for idempotency. The Worker
uses deterministic client message ids, but a durable outbound-send ledger or
outbox is still required before claiming exactly-once customer-visible sends.

## Admin WhatsApp Entry Flow

Admin WhatsApp entry messages use the normal Worker prompt and tool-loop path
and are treated as real WhatsApp inbound turns:

- `POST /v1/admin/companies/{organizationId}/whatsapp` accepts `messageText` and
  `externalCustomerId`,
- the inbound message is persisted with `providerType` set to `whatsapp_cloud`,
- no read receipt is sent because there is no provider-side `wamid`,
- the Worker sends the assistant reply through `IMessageChannelIntegration`,
- mutating tools are enabled the same way as normal WhatsApp webhook traffic.

The company must have a registered WhatsApp Cloud channel with credentials for
the outbound reply to reach the provider.

## Existing Regression Tests

Current focused tests live in `tests/CeoAgent.ApiService.Tests`:

- `WhatsAppWebhookEndpointTests`
- `WhatsAppWebhookIngestionServiceTests`
- `WhatsAppWebhookVerificationServiceTests`
- `WhatsAppSendMessageEndpointTests`

## Eval Fixtures

Scenario fixtures live in `AIHarness/evals/whatsapp/`. Run `AIHarness/scripts/whatsapp-eval.ps1`
to validate their schema, expected invariants, and high-level outcomes. Run
`AIHarness/scripts/whatsapp-behavior-eval.ps1` to combine fixture validation
with focused WhatsApp/API behavior tests. These fixtures keep WhatsApp behavior
stable while the Worker and agent loop evolve.
