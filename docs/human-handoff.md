# Human handoff (request_human_handoff)

Human handoff is a **tool handler plus conversation state**, not a new branch in the agent
loop. The human operator answers from the *same* WhatsApp Business number as the bot; handoff
only silences the bot for that conversation.

## Single source of truth

`Conversation.Status == HandedOff` is the only signal that pauses or resumes the agent.
No new columns or entities were added. `HandoffTicketId` and `EstimatedPickupAt` live only in
`ToolExecution.Result.RequestHumanHandoff`.

## Flow

1. **Trigger (tool).** The model calls `request_human_handoff` with `reason` (and optional
   `notes`). It runs inside `RunAgentLoopAsync → ToolExecutionGateway.ExecuteAsync` like every
   other tool. `RequestHumanHandoffExecutor : IToolExecutor` delegates to
   `HumanHandoffToolExecutor`.
2. **Escalate.** The executor validates the tool is enabled for the company, persists an
   idempotent `ToolExecution` (reusing the existing `RequestHumanHandoffRequest` /
   `RequestHumanHandoffResult` DTOs), sets `Conversation.Status = HandedOff`, and updates
   `ConversationState.Snapshot` (`CurrentIntent = "human_handoff_request"`, flag
   `human_requested`). `EstimatedPickupAt` is computed from
   `RequestHumanHandoffConfig.TimeoutMinutes`; `HandoffTicketId` is a fresh GUID.
3. **Guard (silences the bot).** In `ProcessIncomingMessageJobProcessor.ProcessAsync`, right
   after `LoadContextAsync`, if `Conversation.Status == HandedOff` the job persists nothing new
   (the inbound message is already stored by webhook ingestion), optionally sends a read
   receipt, and returns — no prompt, no agent loop. This is the only place that pauses the agent.
4. **Single confirmation.** After the tool runs, the agent's final turn may send one confirmation
   to the customer. Every later inbound message is suppressed by the guard.
5. **Auto-escalation.** When `RunAgentLoopAsync` cannot complete (loop cap reached or the runtime
   throws), it escalates to `HandedOff` and notifies staff via
   `HumanHandoffToolExecutor.AutoEscalateAsync`, then returns the existing fallback text — so the
   promise "te pondré en contacto con una persona" is backed by a real handoff.

## Push + pull notification

A handoff produces two signals:

- **Push (mandatory).** The executor emits a **sanitized** staff alert containing only
  `HandoffTicketId`, `conversationId`, `organizationId`, `companyChannelId`/provider, the categorical
  `reason`, and `EstimatedPickupAt`. It never includes the full phone number, the literal last
  message, prompts, or transcripts. The alert is always emitted as an observable, structured log
  + metric (`handoff.escalations`). When `RequestHumanHandoffConfig.NotifyUsers` contains
  E.164-style WhatsApp recipients, the alert is also sent through `IMessageChannelIntegration`
  (the existing port). If it cannot be delivered over WhatsApp, the `ToolExecution` keeps
  `Status = Succeeded` (the handoff still stands) but records
  `FailureReason = "notification_unavailable"` and increments
  `handoff.notifications.unavailable`, per the existing tool patterns.

  Message template:
  `Atencion humana requerida. Ticket: <handoffTicketId>. ConversationId: <id>. Motivo: <reason>. Canal: WhatsApp. ETA: <estimatedPickupAt>.`

- **Pull (admin).** `GET /v1/admin/companies/{organizationId}/conversations/handed-off` lists
  conversations with `Status = HandedOff` and their last `request_human_handoff` execution
  (ticket, categorical reason, ETA, requested-at). Operator outbound messages are not recorded
  yet (see MVP decisions).

## Resume

`Conversation.Status` returns to `Open` and the `human_requested` flag / handoff intent are
cleared.

- **Explicit admin resume (mandatory):**
  `POST /v1/admin/companies/{organizationId}/conversations/{conversationId}/resume`.
- **Timeout resume:** *deferred for MVP.* `EstimatedPickupAt` is persisted on the
  `ToolExecution.Result`, but the bot is **not** auto-reopened on timeout. The operator replies
  from the same WhatsApp Business number, so operator messages cannot be distinguished from
  customer messages; auto-reopening on timeout could let the bot resume mid-conversation while a
  human is still engaged. Until operator messages can be distinguished, resumption is explicit
  (admin) only. This risk is the reason the scheduled timeout worker is not implemented yet.

## MVP decisions / limitations

- **No schema changes.** No new columns or entities. Ticket and ETA live only in
  `ToolExecution.Result.RequestHumanHandoff`.
- **Conversation reuse.** `WhatsAppWebhookIngestionService` reuses an existing `Open` *or*
  `HandedOff` conversation for the customer/channel (most recent by `LastMessageAt`); it never
  creates a new conversation during handoff.
- **Operator outbound not recorded.** Messages the human operator sends from the shared WhatsApp
  number are not persisted in MVP. Because they share the business number, they are
  indistinguishable from bot output at ingestion time.
- **Notification infrastructure.** No new infrastructure was added. Staff notification reuses the
  existing `IMessageChannelIntegration` port for WhatsApp-shaped `NotifyUsers`, and otherwise
  falls back to the observable telemetry/log signal. Delivering to operators who are outside an
  active WhatsApp customer-service window would require template messaging that the current port
  does not support — a documented follow-up.
- **No PII in logs.** Per `AIHarness/security-rules.md`, alerts and telemetry carry IDs and
  categorical fields only.
