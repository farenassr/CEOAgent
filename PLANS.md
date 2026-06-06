# CeoAgent Plans

Use this file as the index for implementation plans, harness proposals,
completed work, technical debt, and decision history.

## Active Plans

| Plan | Status | Owner | Notes |
| --- | --- | --- | --- |
| Harness reliability pass | Active | Agents | Add executable eval checks, architecture lints, doc gardening, and local observability scripts. |
| MVP backend completion | Active | Agents | Historical phase docs were removed; track active backend work in issues or new focused plans here. |

## Completed Plans

| Plan | Completed | Evidence |
| --- | --- | --- |
| Foundation harness scaffold | 2026-06-03 | Root `AGENTS.md`, `AIHarness/`, `AIHarness/scripts/`, `AIHarness/evals/`, `traces/`, and project subagents exist. |

## Technical Debt

| Debt | Impact | Next step |
| --- | --- | --- |
| Trace examples are not schema-validated | Repeated production-like failures can drift from eval fixtures. | Add a trace schema validator under `AIHarness/scripts/`. |
| Harness scripts are not mapped to CI jobs | Agents can run checks locally, but merge gates are not visible in repo docs. | Add CI documentation or workflows when the pipeline is finalized. |
| Aspire smoke loop is opt-in | Runtime observability is available but not part of default harness checks. | Use `AIHarness/scripts/aspire-smoke.ps1 -StartAppHost` for runtime-sensitive changes. |

## Decision Log

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-06-03 | Treat `AGENTS.md` as the single normative root guide. | The previously referenced historical guide file does not exist. |
| 2026-06-03 | Keep the old prompt template ignored as scratch material. | Prevent conflicting task-start instructions from entering agent context. |
| 2026-06-03 | Validate WhatsApp eval fixtures with `AIHarness/scripts/whatsapp-eval.ps1`. | Move evals from passive documentation toward executable harness checks. |
| 2026-06-03 | Enforce architecture rules with scripts and tests. | Convert repo rules into repeatable checks instead of relying on agent memory. |
| 2026-06-04 | Keep GitHub Actions workflows empty for now. | Local harness scripts remain the preferred validation path until a pipeline is chosen. |
