# CeoAgent Agent Guide

This file is the root entry point for Codex and other coding agents working in
this repository. It is normative for agent behavior.

Ignore `Ideas.txt`, `PromptTemplate.md`; it is scratch material and must not be used as
project context.

## Harness Engineering Approach

This repository intentionally uses harness engineering.

The goal is not only to ask coding agents to write code, but to give them a
repeatable, verifiable environment for planning, implementation, review, and
regression checks.

Agents must treat the repository harness as part of the product:

- `AGENTS.md` defines the always-on operating rules.
- `AIHarness/` contains deeper architecture, integration, and design context.
- `scripts/` contains stable commands that agents should prefer over ad-hoc shell commands.
- `evals/` contains scenario fixtures and regression checks for agent behavior.
- `traces/` contains sanitized examples of real or simulated failures that can be converted into evals.
- `.codex/agents/` contains specialized Codex subagents for review and implementation support.

## Harness Assets

Use these assets deliberately:

- `scripts/`: stable command wrappers for agents and humans. Prefer these over ad-hoc commands.
- `AIHarness/`: architecture and integration working docs. Read only the relevant docs for the current task.
- `evals/whatsapp/`: scenario fixtures for WhatsApp flow regression checks. Add or update fixtures when chatbot behavior changes.
- `traces/examples/`: sanitized trace examples for debugging and future evals. Convert repeated failures into regression fixtures when useful.
- `.codex/agents/*.toml`: project-scoped custom Codex subagents. Use them for parallel review of architecture, database, security, integrations, and tests.

For feature work, prefer this loop:

1. Understand the current behavior.
2. Plan the smallest durable change.
3. Implement the change.
4. Add or update tests/evals/docs when behavior changes.
5. Run focused validation.
6. Summarize results with exact commands.

## When To Read AIHarness Docs

Do not load the entire `AIHarness/` folder by default. Read only what is relevant:

- Read `AIHarness/integration-model.md` when changing integration contracts, provider selection, tool execution, or company-specific integrations.
- Read `AIHarness/whatsapp-flow.md` when changing WhatsApp webhook ingestion, message parsing, media handling, or outbound replies.
- Read `AIHarness/google-calendar-integration.md` when changing calendar availability, booking, cancellation, or Google Calendar adapter behavior.
- Read `AIHarness/security-rules.md` when changing secrets, authentication, authorization, webhook verification, logging, or tenant isolation.
- Read architecture docs before changing module boundaries, project references, background job flow, or AI/tool orchestration.

If no relevant harness doc exists for a repeated pattern, propose creating one instead of spreading tribal knowledge through code comments.

When a task is complex, ambiguous, risky, or touches architecture, integrations,
AI behavior, security, database design, or background processing, agents should
work in harness mode:

1. Read this root `AGENTS.md`.
2. Read only the relevant files under `AIHarness/`.
3. Inspect the current implementation before proposing changes.
4. Produce a short plan before editing.
5. Prefer small, reversible changes.
6. Add or update tests, fixtures, evals, or docs when the change affects behavior.
7. Run the narrowest meaningful validation command.
8. Report what was changed, what was verified, and what could not be verified.

Do not treat harness assets as optional notes. They are the control surface that
keeps agent work consistent across sessions.

## First Inspection Checklist

Before changing files:

1. Read this file.
2. Check `git status --short` and do not revert unrelated user changes.
3. Inspect the relevant project and test files before proposing edits.
4. Prefer `rg` and `rg --files` for search.
5. For OpenAI, .NET, Aspire, EF Core, FastEndpoints, Refit, Google Calendar,
   WhatsApp Cloud, or library-specific behavior, verify current docs before
   relying on memory.

## Repository Shape

The solution is a .NET Aspire modular monolith:

- `CeoAgent.AppHost`: local Aspire orchestration.
- `CeoAgent.ApiService`: FastEndpoints HTTP surface, admin endpoints, webhook receiver.
- `CeoAgent.Worker`: background jobs and agent/tool execution.
- `CeoAgent.Application`: shared application logic and AI runtime orchestration.
- `CeoAgent.Infrastructure`: EF Core, persistence, queues, blob, infrastructure glue.
- `CeoAgent.Integrations`: integration port contracts only.
- `CeoAgent.Adapters`: external provider implementations.
- `CeoAgent.Tools`: native tool handlers.
- `CeoAgent.Shared`: public API request/response DTOs and shared enums.
- `tests/*`: TUnit, Shouldly, NSubstitute, Verify, Testcontainers, Aspire Testing.

`CeoAgent.Web` is not core MVP backend unless a task explicitly targets it.

## Commands

Use the wrappers when possible:

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/format.ps1
```

The underlying canonical commands are:

```powershell
dotnet restore CEOAgent.slnx
dotnet build CEOAgent.slnx --no-restore
dotnet test CEOAgent.slnx --no-build
```

Focused checks:

```powershell
dotnet test CEOAgent.slnx --filter "WhatsApp|Webhook"
dotnet test CEOAgent.slnx --filter "Calendar|Availability|Reservation"
dotnet test CEOAgent.slnx --filter "Company|Admin|Persistence"
dotnet test CEOAgent.slnx --filter "Queue|Blob|Job"
```

Run the narrowest meaningful check first, then broaden when risk warrants it.

## Architecture Rules

- Modular monolith for MVP. Do not split into microservices.
- Vertical slices under `CeoAgent.ApiService/Modules/<Module>/...`.
- FastEndpoints for HTTP. No MVC controllers.
- Use martinothamar/Mediator for non-trivial API use cases, Worker workflows,
  reusable workflows, and cross-module operations. Never use MediatR.
- API request/response DTOs live in `CeoAgent.Shared`, one public class per file.
- Use Mapperly only when shapes diverge; direct construction is fine for same-shape mapping.
- Use EF Core directly. No generic repository and no custom Unit of Work.
- One `CeoAgentDbContext`.
- Company-owned data must use `company_id` and EF Core global query filters.
- All identifiers use `Guid.CreateVersion7()`.
- Use `TimeProvider`; do not use `DateTime.Now` or direct `DateTime.UtcNow`.
- All API routes are under `/v1/` except `/health`.

## Integration Boundaries

- Ports live in `CeoAgent.Integrations`.
- Provider implementations live in `CeoAgent.Adapters`.
- Business logic must not call external systems directly.
- WhatsApp-specific code stays in the WhatsApp adapter or WhatsApp API module.
- Google Calendar SDK usage stays in the Google Calendar adapter.
- Aspire `.WithReference(...)` supplies local runtime resource connection strings.
- Do not move PostgreSQL, queue, or blob runtime connection strings into Key Vault.

See:

- `AIHarness/integration-model.md`
- `AIHarness/whatsapp-flow.md`
- `AIHarness/google-calendar-integration.md`

## AI And Tool Safety

- The model never executes side effects directly.
- New business capabilities are tool handlers or workflows, not new branches in
  the agent loop.
- Validate model/tool output through provider structured output where available,
  source-generated JSON where relevant, and post-deserialization checks.
- Send only the last 8 eligible raw turns to the model.
- Do not hardcode model names in code; resolve the company model from
  `agent_profile`.
- No live LLM calls in CI.

## Database And Migrations

- Agents may inspect migrations and propose migration changes.
- Agents must not apply migrations or run `dotnet ef database update`.
- Do not scaffold or remove EF migrations unless the user explicitly requests
  migration work.
- JSONB entity document types live under
  `CeoAgent.Infrastructure/Entities/JsonDocuments/`.

## Security

- Never commit real secrets, API keys, provider tokens, signing keys, refresh
  tokens, or connection strings.
- Company credential tables store references such as `kv://...`, never raw
  secrets.
- Verify public webhook signatures before processing.
- Webhook ingestion must be idempotent and return quickly.
- Do not log raw customer message text, audio transcripts, prompts, completions,
  or secrets unless a controlled debugging task explicitly requires it.

See `AIHarness/security-rules.md`.

## Harness Assets

- `scripts/`: stable command wrappers for agents and humans.
- `AIHarness/`: architecture and integration working docs.
- `evals/whatsapp/`: scenario fixtures for WhatsApp flow regression checks.
- `traces/examples/`: sanitized trace examples for debugging and future evals.
- `.codex/agents/*.toml`: project-scoped custom Codex subagents.

## Completion Rules

Before claiming work is complete:

1. Re-read the user request and this file.
2. Verify every changed file is intentional.
3. Run the narrowest relevant validation command.
4. Report exact commands run and whether they passed or failed.
5. State anything you could not verify.

## Agents and subagents

You can use the available project subagents:

- `phase-orchestrator`: Coordinates harness-engineered development phases by selecting docs, subagents, plans, implementation boundaries, and verification steps.
- `architecture-reviewer`: use for architecture, module boundaries, vertical slices, project references, and overall design risks.
- `backend-engineer`: use for .NET implementation, FastEndpoints, Mediator workflows, application logic, Worker jobs, and service boundaries.
- `db-specialist`: use for EF Core, PostgreSQL, migrations review, indexes, JSONB, query filters, tenant isolation, and data modeling.
- `integrations-engineer`: use for ports/adapters, WhatsApp Cloud, Google Calendar, external APIs, provider boundaries, and tool execution.
- `ai-engineer`: use for Microsoft Agent Framework, agent loop, prompts, structured output, tool calling, model behavior, evals, and LLM safety.
- `testing-engineer`: use for unit tests, integration tests, Aspire Testing, Testcontainers, fixtures, evals, and regression coverage.
- `code-simplifier`: Simplifies redundant code, improves query readability, removes duplication, and proposes safer refactors without changing behavior.
- `codebase-scout` : Inspects relevant code, tests, docs, and harness assets, then summarizes the current implementation before planning or coding.
