# Google Calendar Integration

Google Calendar is the MVP calendar provider. Calendar provider behavior must
stay behind `ICalendarIntegration` and the Google Calendar AI tool
implementation.

## Boundary

```text
Model tool call or Worker workflow
  -> CeoAgent.Infrastructure ToolExecutionGateway
  -> CeoAgent.Infrastructure AITools/GoogleCalendar executor
  -> ICalendarIntegration
  -> GoogleCalendarIntegration
  -> Google Calendar SDK/API
```

Do not use Google Calendar SDK types outside
`CeoAgent.Infrastructure/Implementation/AITools/GoogleCalendar/Integration`.
Google Calendar tool executors, scheduling policies, validators, and
tool-specific helpers belong under
`CeoAgent.Infrastructure/Implementation/AITools/GoogleCalendar`.

## Current Capabilities

- Check availability for a requested time window.
- Create an idempotent reservation/event.
- Use company tool configuration for calendar ID, time zone, slot duration,
  reservation duration, buffer, and advance booking rules.

## Required Safety Rules

- Validate company ownership before executing calendar tools.
- Validate model-requested tool names against enabled `company_tool` rows before execution.
- Validate working hours and advance booking windows before provider calls.
- Store idempotency keys so retries do not duplicate reservations.
- HTTP admin endpoints for Google Calendar are operational/admin surfaces under
  `/v1/admin`. Mutating business tool paths must remain auditable through
  `ToolExecutionGateway` or provide equivalent `ToolExecution` persistence
  before they are exposed beyond admin diagnostics.
- Map provider failures to recoverable integration errors where possible.
- Do not store Google access tokens or service account material in database
  rows; store only references.
- Adapter client caches must not use raw credential material as cache keys.
  Prefer stable hashed keys, bounded TTLs, explicit timeouts, and disposal on
  eviction.

## Review Checklist

- Is the Google SDK usage confined to `CeoAgent.Infrastructure/Implementation/AITools/GoogleCalendar/Integration`?
- Does the tool path load company, conversation, tool, and credential context
  from company-scoped data?
- Does the LLM path return only sanitized tool result JSON to the model?
- Does the operation remain idempotent under queue retries?
- Are date/time values converted with the company's time zone rules?
- Is there a focused test for boundary behavior or provider failure?
