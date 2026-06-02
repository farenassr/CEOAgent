# Google Calendar Integration

Google Calendar is the MVP calendar provider. Calendar provider behavior must
stay behind `ICalendarIntegration` and the Google Calendar adapter.

## Boundary

```text
Tool or Worker workflow
  -> ICalendarIntegration
  -> GoogleCalendarIntegration
  -> Google Calendar SDK/API
```

Do not use Google Calendar SDK types outside `CeoAgent.Adapters`.

## Current Capabilities

- Check availability for a requested time window.
- Create an idempotent reservation/event.
- Use company tool configuration for calendar ID, time zone, slot duration,
  reservation duration, buffer, and advance booking rules.

## Required Safety Rules

- Validate company ownership before executing calendar tools.
- Validate working hours and advance booking windows before provider calls.
- Store idempotency keys so retries do not duplicate reservations.
- Map provider failures to recoverable integration errors where possible.
- Do not store Google access tokens or service account material in database
  rows; store only references.

## Review Checklist

- Is the Google SDK usage confined to `CeoAgent.Adapters/GoogleCalendar`?
- Does the tool path load company, conversation, tool, and credential context
  from company-scoped data?
- Does the operation remain idempotent under queue retries?
- Are date/time values converted with the company's time zone rules?
- Is there a focused test for boundary behavior or provider failure?
