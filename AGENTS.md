# CeoAgent Agent Guide

This file is the root entry point for Codex and other coding agents working in
this repository. It is normative for agent behavior.

## Harness Engineering

This repository intentionally uses harness engineering: agents should work
through a repeatable, verifiable environment for planning, implementation,
review, and regression checks.

Core harness assets:

- `AGENTS.md`: always-on operating rules.
- `.codex/AGENTS.md`: Codex-specific overlay; keep it consistent with this file.
- `AIHarness/`: deeper architecture, integration, and design context.
- `AIHarness/scripts/`: stable command wrappers. Prefer these over ad-hoc commands.
- `AIHarness/evals/whatsapp/`: WhatsApp scenario fixtures and regression checks.
- `traces/examples/`: sanitized trace examples for debugging and future evals.
- `.codex/agents/*.toml`: project-scoped Codex subagents.

For complex, ambiguous, risky, or behavior-changing tasks:

1. Read this file.
2. Read only the relevant `AIHarness/` docs.
3. Inspect the current implementation.
4. Produce a short plan before editing.
5. Prefer small, reversible changes.
6. Add or update tests, fixtures, evals, or docs when behavior changes.
7. Run the narrowest meaningful validation command.
8. Report what changed, what was verified, and what could not be verified.

Do not load the entire `AIHarness/` folder by default. Read only the relevant
docs for the current task:

- `AIHarness/integration-model.md`: integration contracts, provider selection,
  tool execution, or company-specific integrations.
- `AIHarness/whatsapp-flow.md`: WhatsApp webhook ingestion, message parsing,
  media handling, or outbound replies.
- `AIHarness/google-calendar-integration.md`: calendar availability, booking,
  cancellation, or Google Calendar implementation behavior.
- `AIHarness/security-rules.md`: secrets, authentication, authorization,
  webhook verification, logging, organization isolation, or PII handling.
- Architecture docs: module boundaries, project references, background jobs,
  or AI/tool orchestration.

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

- `src/CeoAgent.AppHost`: local Aspire orchestration.
- `src/CeoAgent.ApiService`: FastEndpoints HTTP surface, admin endpoints, webhook receiver.
- `src/CeoAgent.Worker`: background jobs and agent/tool execution.
- `src/CeoAgent.Application`: application-owned abstractions and prompt behavior.
- `src/CeoAgent.Infrastructure`: EF Core, persistence, queues, blob, provider implementations,
  company context implementation, and native tool execution.
- `src/CeoAgent.Shared`: public API request/response DTOs, shared enums, provider-neutral
  runtime/tool/calendar/messaging/job models.
- `tests/*`: TUnit, Shouldly, NSubstitute, Verify, Testcontainers, Aspire Testing.

`CeoAgent.Web` is not core MVP backend unless a task explicitly targets it.

## Commands

Use wrappers when possible:

```powershell
./AIHarness/scripts/build.ps1
./AIHarness/scripts/test.ps1
./AIHarness/scripts/format.ps1
```

Canonical commands:

```powershell
dotnet restore CEOAgent.slnx
dotnet build CEOAgent.slnx --no-restore
dotnet test CEOAgent.slnx --no-build
```

Focused checks:

```powershell
./AIHarness/scripts/test.ps1 -TreeNodeFilter "/*/*/*WhatsApp*/*|/*/*/*Webhook*/*|/*/*/*/*WhatsApp*|/*/*/*/*Webhook*"
./AIHarness/scripts/test.ps1 -TreeNodeFilter "/*/*/*Calendar*/*|/*/*/*Availability*/*|/*/*/*Reservation*/*|/*/*/*/*Calendar*|/*/*/*/*Availability*|/*/*/*/*Reservation*"
./AIHarness/scripts/test.ps1 -TreeNodeFilter "/*/*/*Company*/*|/*/*/*Admin*/*|/*/*/*Persistence*/*|/*/*/*/*Company*|/*/*/*/*Admin*|/*/*/*/*Persistence*"
./AIHarness/scripts/test.ps1 -TreeNodeFilter "/*/*/*Queue*/*|/*/*/*Blob*/*|/*/*/*Job*/*|/*/*/*/*Queue*|/*/*/*/*Blob*|/*/*/*/*Job*"
```

Run the narrowest meaningful check first, then broaden when risk warrants it.

## Test Quality Rules

Tests must protect meaningful behavior, public contracts, security, organization
isolation, persistence constraints, integration boundaries, AI/tool safety, or
operational harness rules. Do not add tests that only prove an assembly loads,
mirror one-to-one DTO or record properties, check that an interface method
exists when production code already calls it, or freeze cosmetic OpenAPI/Scalar
text unless that text is an intentional public contract. Prefer one focused
behavior test over several implementation-detail assertions, and name the
regression or rule the test protects.

## Architecture Rules

- Modular monolith for MVP. Do not split into microservices.
- Vertical slices live under `src/CeoAgent.ApiService/Modules/<Module>/...`.
- FastEndpoints for HTTP. No MVC controllers.
- Use martinothamar/Mediator for non-trivial API use cases, Worker workflows,
  reusable workflows, and cross-module operations. Never use MediatR.
- API request/response DTOs live in `CeoAgent.Shared`, one public class per file.
- Use Mapperly only when shapes diverge; direct construction is fine for same-shape mapping.
- Use EF Core directly. No generic repository and no custom Unit of Work.
- Use one `CeoAgentDbContext`.
- Organization-owned data must use `organization_id` and EF Core global query filters.
- All identifiers use `Guid.CreateVersion7()`.
- Use `TimeProvider`; do not use `DateTime.Now` or direct `DateTime.UtcNow`.
- All API routes are under `/v1/` except `/health`.

## Integration Boundaries

- Ports live in `src/CeoAgent.Application/Abstractions`.
- Provider-neutral request/result models live in `src/CeoAgent.Shared`.
- Provider implementations live in `src/CeoAgent.Infrastructure/Implementation`.
- Business logic must not call external systems directly.
- WhatsApp-specific code stays in the WhatsApp implementation or WhatsApp API module.
- Google Calendar SDK usage stays in the Google Calendar implementation.
- Aspire `.WithReference(...)` supplies local runtime resource connection strings.
- Do not move PostgreSQL, queue, or blob runtime connection strings into Key Vault.

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
  `src/CeoAgent.Infrastructure/Entities/JsonDocuments/`.

## Security

- Never commit real secrets, API keys, provider tokens, signing keys, refresh
  tokens, or connection strings.
- Company credential tables store references such as `kv://...`, never raw
  secrets.
- Verify public webhook signatures before processing.
- Webhook ingestion must be idempotent and return quickly.
- Do not log raw customer message text, audio transcripts, prompts, completions,
  or secrets unless a controlled debugging task explicitly requires it.

See `AIHarness/security-rules.md` for security-specific work.

## Commit Message Format

Commit messages must follow this format:

```text
<ProjectOrArea>/<ProjectOrArea>/...: [<GitHubIssueId>] <Concise change summary>
```

Example:

```text
Adapters/ApiService/AppHost/Infrastructure: [#14] Add Google Calendar tool endpoints, SDK auth, scheduling rules, and service-account metadata
```

## Agents And Subagents

Use available project subagents when their domain matches the task:

- `phase-orchestrator`: coordinates harness-engineered phases, docs, plans, implementation boundaries, and verification.
- `codebase-scout`: inspects relevant code, tests, docs, and harness assets before planning or coding.
- `architecture-reviewer`: reviews architecture, module boundaries, vertical slices, project references, and design risks.
- `backend-engineer`: implements .NET API, FastEndpoints, Mediator workflows, Worker jobs, and service boundaries.
- `db-specialist`: handles EF Core, PostgreSQL, migrations review, indexes, JSONB, query filters, organization isolation, and data modeling.
- `integrations-engineer`: handles integration ports and implementations, WhatsApp Cloud, Google Calendar, external APIs, provider boundaries, and tool execution.
- `ai-engineer`: handles Microsoft Agent Framework, agent loop, prompts, structured output, tool calling, model behavior, evals, and LLM safety.
- `testing-engineer`: handles unit tests, integration tests, Aspire Testing, Testcontainers, fixtures, evals, and regression coverage.
- `code-simplifier`: simplifies redundant code and query logic without changing behavior.

## Completion Rules

Before claiming work is complete:

1. Re-read the user request and this file.
2. Verify every changed file is intentional.
3. Run the narrowest relevant validation command.
4. Report exact commands run and whether they passed or failed.
5. State anything that could not be verified.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, invoke the `skill` tool with `skill: "graphify"` before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
