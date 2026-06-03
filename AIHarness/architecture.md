# CeoAgent Architecture

This document is the short architecture reference for agents. `AGENTS.md`
is the normative root guide; this file captures the current boundaries to
check before making changes.

## Runtime Shape

CeoAgent is a .NET Aspire modular monolith:

```text
WhatsApp Cloud
  -> CeoAgent.ApiService
  -> Azure Storage Queue
  -> CeoAgent.Worker
  -> CeoAgent.Tools
  -> PostgreSQL / Blob Storage / provider adapters
```

The API receives HTTP requests, validates webhooks, persists inbound state, and
queues work. The Worker owns long-running job and agent-loop orchestration,
outbound messaging, and integration call coordination. Native business tool
execution lives in `CeoAgent.Tools`.

## Project Responsibilities

| Project | Responsibility |
| --- | --- |
| `CeoAgent.ApiService` | FastEndpoints, webhook receiver, admin endpoints, global errors, correlation, HTTP contracts. |
| `CeoAgent.Worker` | Queue-driven jobs, agent loop orchestration, outbound processing. |
| `CeoAgent.Application` | Shared application behavior, AI prompt/runner abstractions, company context contracts. |
| `CeoAgent.Infrastructure` | EF Core entities/configurations, DbContext, storage and queue infrastructure. |
| `CeoAgent.Integrations` | Port interfaces and provider-neutral DTOs only. |
| `CeoAgent.Adapters` | Provider-specific implementations for WhatsApp, Google Calendar, secrets, and external HTTP. |
| `CeoAgent.Tools` | Native business tool catalog, gateway, and handlers exposed to the agent. |
| `CeoAgent.Shared` | Public request/response DTOs and shared enums. |

## Dependency Direction

Allowed direction:

```text
ApiService / Worker
  -> Application / Infrastructure / Integrations / Adapters / Tools / Shared
Application
  -> Integrations
Infrastructure
  -> Application / Shared
Adapters
  -> Integrations
Tools
  -> Application / Integrations / Infrastructure as needed by existing patterns
```

Avoid direct provider SDK usage outside `CeoAgent.Adapters`.

## Physical Organization

Use `Abstractions`, `Implementation`, and `Models` folders when a bounded area
contains contracts, concrete runtime code, and request/result shapes that would
otherwise be mixed together. Files under those folders should declare namespaces
that include the same folder segments.

`CeoAgent.Tools` is the enforced MVP runtime example: non-root source files
belong under `Abstractions`, `Implementation`, or `Models`, while root files are
limited to assembly markers and DI registration.

## Non-Negotiable Checks

- No microservice split for MVP.
- No MVC controllers.
- No MediatR.
- No generic repositories or custom Unit of Work.
- No business logic calling external providers directly.
- Company isolation through global EF query filters.
- No migration application from agents.
- No live LLM calls in CI.
- No raw secrets in repo files or database rows.

## Review Questions

- Does this change keep long work out of the API request path?
- Does this change preserve company isolation?
- Does this change keep provider details behind ports/adapters?
- Does this change add tests proportionate to risk?
- Does this change update docs/evals when behavior changes?
