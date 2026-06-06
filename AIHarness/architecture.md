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
  -> CeoAgent.Infrastructure tool execution
  -> PostgreSQL / Blob Storage / provider implementations
```

The API receives HTTP requests, validates webhooks, persists inbound state, and
queues work. The Worker owns long-running job and agent-loop orchestration,
outbound messaging, and integration call coordination. Native business tool
execution lives in `CeoAgent.Infrastructure/Implementation/AITools`.

## Project Responsibilities

| Project | Responsibility |
| --- | --- |
| `CeoAgent.ApiService` | FastEndpoints, webhook receiver, admin endpoints, global errors, correlation, HTTP contracts. |
| `CeoAgent.Worker` | Queue-driven jobs, agent loop orchestration, outbound processing. |
| `CeoAgent.Application` | Application-owned abstractions, prompt behavior, and company context contracts. |
| `CeoAgent.Infrastructure` | EF Core entities/configurations, DbContext, storage and queue infrastructure, provider implementations, company context implementation, and native tool execution. |
| `CeoAgent.Shared` | Public request/response DTOs, shared enums, provider-neutral runtime/tool/calendar/messaging/job models. |

## Dependency Direction

Allowed direction:

```text
ApiService / Worker
  -> Application / Infrastructure / Shared
Application
  -> Shared
Infrastructure
  -> Application / Shared
```

Avoid direct provider SDK usage outside `CeoAgent.Infrastructure/Implementation`.

## Physical Organization

Use `Abstractions`, `Implementation`, and `Models` folders when a bounded area
contains contracts, concrete runtime code, and request/result shapes that would
otherwise be mixed together. Files under those folders should declare namespaces
that include the same folder segments.

Application abstractions, Infrastructure implementations, and Shared models use
folders and namespaces that include the same segments.

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
- Does this change keep provider details behind integration ports and implementations?
- Does this change add tests proportionate to risk?
- Does this change update docs/evals when behavior changes?
