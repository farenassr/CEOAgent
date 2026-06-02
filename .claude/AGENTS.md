# CeoAgent Agent Guide

This file is the root entry point for Codex and other coding agents working in
this repository. It is normative for agent behavior.

The longer historical rules live in `.agents/AGENTS.md`. If this file and
`.agents/AGENTS.md` conflict, follow the stricter rule and update this file if
the project contract changed.

Ignore `Ideas.txt`; it is scratch material and must not be used as project
context.

## Harness Engineering

This repository intentionally uses harness engineering: agents should work
through a repeatable, verifiable environment for planning, implementation,
review, and regression checks.

Core harness assets:

- `AGENTS.md`: always-on operating rules.
- `AIHarness/`: deeper architecture, integration, and design context.
- `scripts/`: stable command wrappers. Prefer these over ad-hoc commands.
- `evals/whatsapp/`: WhatsApp scenario fixtures and regression checks.
- `traces/examples/`: sanitized trace examples for debugging and future evals.
- `.codex/agents/*.toml`: project-scoped Codex subagents for specialized review.

Do not load the entire `AIHarness/` folder by default. Read only the relevant
docs for the current task:

- `AIHarness/integration-model.md`: integration contracts, provider selection,
  tool execution, or company-specific integrations.
- `AIHarness/whatsapp-flow.md`: WhatsApp webhook ingestion, message parsing,
  media handling, or outbound replies.
- `AIHarness/google-calendar-integration.md`: calendar availability, booking,
  cancellation, or Google Calendar adapter behavior.
- `AIHarness/security-rules.md`: secrets, authentication, authorization,
  webhook verification, logging, or tenant isolation.
- Architecture docs: module boundaries, project references, background jobs,
  or AI/tool orchestration.

For complex, ambiguous, risky, or behavior-changing tasks:

1. Read this file.
2. Read only the relevant `AIHarness/` docs.
3. Inspect the current implementation.
4. Produce a short plan before editing.
5. Prefer small, reversible changes.
6. Add or update tests, fixtures, evals, or docs when behavior changes.
7. Run the narrowest meaningful validation command.
8. Report what changed, what was verified, and what could not be verified.

If a repeated pattern has no harness doc, propose creating one instead of
spreading tribal knowledge through code comments.

## First Inspection Checklist

Before changing files:

1. Read this file.
2. Check `git status --short` and do not revert unrelated user changes.
3. Inspect relevant project and test files before proposing edits.
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

Use wrappers when possible:

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/format.ps1
```

Canonical commands:

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
- Vertical slices live under `CeoAgent.ApiService/Modules/<Module>/...`.
- FastEndpoints for HTTP. No MVC controllers.
- Use martinothamar/Mediator for non-trivial API use cases, Worker workflows,
  reusable workflows, and cross-module operations. Never use MediatR.
- API request/response DTOs live in `CeoAgent.Shared`, one public class per file.
- Use Mapperly only when shapes diverge; direct construction is fine for same-shape mapping.
- Use EF Core directly. No generic repository and no custom Unit of Work.
- Use one `CeoAgentDbContext`.
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

Relevant docs:

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

Relevant doc:

- `AIHarness/security-rules.md`

## Completion Rules

Before claiming work is complete:

1. Re-read the user request and this file.
2. Verify every changed file is intentional.
3. Run the narrowest relevant validation command.
4. Report exact commands run and whether they passed or failed.
5. State anything that could not be verified.

## Commit Message Format

Commit messages must follow this format:
<ProjectOrArea>/<ProjectOrArea>/...: [<GitHubIssueId>] <Concise change summary>
Examples:
Adapters/ApiService/AppHost/Infrastructure: [#14] Add Google Calendar tool endpoints, SDK auth, scheduling rules, and service-account metadata
