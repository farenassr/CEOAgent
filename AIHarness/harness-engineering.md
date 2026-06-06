# Harness Engineering

This document is the index for the repository harness. It points agents to
the smallest useful source of truth, the executable checks, and the current
backlog for improving repeatability.

## Harness Doc Index

| Document | Use when |
| --- | --- |
| `AIHarness/harness-engineering.md` | Choosing harness scripts, checks, subagents, and next reliability work. |
| `AIHarness/architecture.md` | Changing project boundaries, dependency direction, API shape, Worker flow, or tool orchestration. |
| `AIHarness/integration-model.md` | Changing integration ports, provider implementations, provider selection, credentials, or external SDK usage. |
| `AIHarness/whatsapp-flow.md` | Changing WhatsApp webhook ingestion, idempotency, message parsing, media, or outbound replies. |
| `AIHarness/google-calendar-integration.md` | Changing availability, booking, calendar tool execution, or Google Calendar implementation behavior. |
| `AIHarness/security-rules.md` | Changing secrets, authentication, authorization, webhook signatures, company isolation, logging, AI safety, or PII handling. |

## Executable Harness Checks

Run focused checks first, then broaden when risk warrants it.

| Script | Purpose |
| --- | --- |
| `AIHarness/scripts/architecture-check.ps1` | Enforces project references, no MediatR, no MVC controllers, provider SDK isolation, `/v1` FastEndpoints routes, critical contract uniqueness, scoped folder conventions, and namespace/folder alignment. |
| `AIHarness/scripts/doc-gardening.ps1` | Checks markdown links, stale references to missing instruction files, scratch-doc references, and AIHarness index freshness. |
| `AIHarness/scripts/whatsapp-eval.ps1` | Validates WhatsApp eval fixture structure, required invariants, and expected outcomes. |
| `AIHarness/scripts/harness-check.ps1` | Runs doc gardening, WhatsApp eval validation, and architecture checks. Use `-IncludeFormat`, `-IncludeBuild`, or `-IncludeTests` to broaden. |
| `AIHarness/scripts/review-current-branch.ps1` | Generates a review context file for `docs/reviewer.md` using the current branch diff. |
| `AIHarness/scripts/aspire-smoke.ps1` | Optionally starts AppHost, checks API health, captures AppHost logs, and points agents to local Aspire logs, traces, and metrics. |

## Local Observability Loop

Use `AIHarness/scripts/aspire-smoke.ps1 -StartAppHost` when a change needs runtime
confidence. The script starts the AppHost with the HTTP launch profile, probes
the API health endpoint, writes AppHost logs to `TestResults/`, and leaves a
clear path for inspecting Aspire dashboard logs, traces, and metrics.

For code-only harness changes, do not start Aspire by default. Use the static
checks instead.

## Recommended Custom Subagents

Existing project-scoped Codex agents live in `.codex/agents/`:

- `phase-orchestrator`
- `codebase-scout`
- `architecture-reviewer`
- `backend-engineer`
- `db-specialist`
- `integrations-engineer`
- `ai-engineer`
- `testing-engineer`
- `code-simplifier`

Recommended future agents:

| Agent | Purpose | Sandbox |
| --- | --- | --- |
| `harness-engineer` | Maintains AGENTS.md, docs, scripts, eval fixtures, traces, and repeatable workflows. | `workspace-write` |
| `whatsapp-flow-reviewer` | Read-only reviewer for WhatsApp signature validation, idempotency, company resolution, queue handoff, and audio edge cases. | `read-only` |
| `aspire-smoke-runner` | Starts AppHost, checks health endpoints, captures local resource status, and reports environment blockers. | `workspace-write` |

Do not add new agents until the workflow repeats enough to justify them.

## Recommended Skills

Create repo-specific skills only for workflows that repeat and have stable
steps:

| Skill | Trigger | Outcome |
| --- | --- | --- |
| `ceoagent-whatsapp-eval` | Review or test WhatsApp flow changes. | Runs focused tests, compares eval fixtures, checks idempotency/signature invariants. |
| `ceoagent-ef-migration-review` | Entity, DbContext, JSONB, or migration changes. | Reviews company filters, JSONB mapping, migration SQL, and no-apply policy. |
| `ceoagent-aspire-smoke` | Local runtime or health check work. | Starts AppHost or reads status, verifies `/health`, reports dependencies. |
| `ceoagent-provider-contract-review` | Provider implementation or integration port changes. | Confirms provider SDK isolation, retries, secrets, and no-live-call test strategy. |

## Harness Backlog

- Add a sanitized trace schema validator for `traces/examples/`.
- Add CI documentation mapping harness scripts to pipeline jobs.
- Add a recurring doc-gardening task once CI or automations are available.
