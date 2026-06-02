# Harness Engineering

This document tracks proposals for making Codex reliable in this repository
over time.

## Recommended Custom Subagents

Existing project-scoped Codex agents live in `.codex/agents/`:

- `architecture-reviewer`
- `backend-engineer`
- `db-specialist`
- `integrations-engineer`
- `ai-engineer`
- `testing-engineer`

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
| `ceoagent-ef-migration-review` | Entity, DbContext, JSONB, or migration changes. | Reviews tenant filters, JSONB mapping, migration SQL, and no-apply policy. |
| `ceoagent-aspire-smoke` | Local runtime or health check work. | Starts AppHost or reads status, verifies `/health`, reports dependencies. |
| `ceoagent-provider-contract-review` | Adapter or integration port changes. | Confirms provider SDK isolation, retries, secrets, and no-live-call test strategy. |

## Harness Backlog

- Automate eval fixture validation once the agent loop has a stable API.
- Add a sanitized trace schema validator.
- Add a script for focused WhatsApp regression checks.
- Add CI documentation mapping scripts to pipeline jobs.
