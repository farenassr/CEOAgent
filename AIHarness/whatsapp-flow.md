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
- Duplicate webhook delivery returns 200 and does not enqueue duplicate work.
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
  -> optionally synthesize voice
  -> send audio if synthesis succeeds
```

TTS failure must not block a text reply.

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
