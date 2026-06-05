# WhatsApp Flow

This document captures the WhatsApp Cloud MVP flow for review, tests, and
future evals.

## Inbound Text Flow

```text
WhatsApp Cloud webhook
  -> ApiService /v1/webhooks/whatsapp
  -> signature validation
  -> parse payload
  -> resolve company by phone_number_id
  -> resolve or create customer by wa_id/from
  -> resolve or create open conversation
  -> persist inbound message with provider_message_id
  -> enqueue ProcessIncomingMessageJob
  -> return 200 quickly
```

## Required Invariants

- Validate `X-Hub-Signature-256` before processing POST bodies.
- Use constant-time signature comparison.
- Resolve company by provider channel ID, not customer phone number.
- Persist inbound messages idempotently by provider message ID.
- Duplicate webhook delivery returns 200. If the inbound message already has a
  persisted assistant reply, do not enqueue duplicate work. If the inbound
  message exists but no assistant reply exists, re-enqueue the existing message
  as a recovery path for a prior enqueue failure.
- The recovery path is not a substitute for a durable outbox. If webhook save
  and queue enqueue must be guaranteed without provider retry, add a
  transactionally persisted processing marker or outbox row.
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

## Simulation Flow

Agent simulation messages use the normal Worker prompt and tool-loop path, but
they are a dry run for external side effects:

- no WhatsApp read receipt is sent,
- no outbound WhatsApp text or audio is sent,
- mutating tools are denied by the tool gateway,
- assistant output is persisted with a synthetic `simulation:*` provider result.

Simulation can still call non-mutating tools when they are enabled for the
company, because those calls are useful for validating prompt and availability
behavior without sending customer-visible messages.

## Existing Regression Tests

Current focused tests live in `tests/CeoAgent.ApiService.Tests`:

- `WhatsAppWebhookEndpointTests`
- `WhatsAppWebhookIngestionServiceTests`
- `WhatsAppWebhookVerificationServiceTests`
- `WhatsAppSendMessageEndpointTests`

## Eval Fixtures

Scenario fixtures live in `evals/whatsapp/`. Run `scripts/whatsapp-eval.ps1`
to validate their schema, expected invariants, and high-level outcomes. These
fixtures keep WhatsApp behavior stable while the Worker and agent loop evolve.
