Start this as a harness-engineered development phase.

## Phase Goal

<describe the goal clearly>

## Rules

- Follow `AGENTS.md`.
- Use `.codex/AGENTS.md` only when relevant.
- Use `AIHarness/` docs only when relevant.
- Prefer `AIHarness/scripts/` over ad-hoc commands.
- Use relevant `.codex/agents/` subagents only when they add value.
- Keep changes small, scoped, reversible, and production-safe.
- Do not refactor unrelated code.
- Do not revert unrelated user changes.
- Do not run, scaffold, remove, or apply EF migrations unless explicitly requested.
- Update relevant docs under `docs/` when needed.

## Required Workflow

1. Read `AGENTS.md`.
2. Check `git status --short`.
3. Inspect relevant code, tests, scripts, and docs.
4. Use `codebase-scout` if available.
5. Use `superpowers:brainstorming` to understand the problem, explore options, and identify clarifying questions.
6. If requirements are ambiguous or materially incomplete, ask the necessary questions before planning.
7. Use only relevant specialist subagents.
8. Produce a concise plan before editing.
9. Wait for approval before modifying files.
10. After approval, implement only the approved scope.
11. Run the narrowest meaningful validation.
12. Use `superpowers:verification-before-completion` before closing.
13. Report commands run, pass/fail result, changed files, and anything not verified.

## Clarifying Questions Rule

During `superpowers:brainstorming`, identify whether the task has blocking ambiguities.

Ask questions only when the answer would materially change:

- architecture,
- data model,
- public contracts,
- security behavior,
- external integrations,
- migration strategy,
- user-visible behavior,
- test strategy,
- or production risk.

Do not ask questions for minor implementation details that can be reasonably inferred from existing project patterns.
If there are no blocking ambiguities, state the assumptions and continue to the plan.

## Subagents

Use only when relevant:

- `phase-orchestrator`
- `codebase-scout`
- `architecture-reviewer`
- `backend-engineer`
- `db-specialist`
- `integrations-engineer`
- `ai-engineer`
- `testing-engineer`
- `code-simplifier`

Each selected subagent should return:

1. Risks.
2. Relevant files.
3. Recommended changes.
4. Tests/docs/evals to update.
5. What should not change.

## Plan Format

Before editing, return:

1. Phase summary.
2. Key findings from inspection.
3. Assumptions.
4. Blocking questions, if any.
5. Implementation steps.
6. Files likely to change.
7. Tests/docs/evals to update.
8. Risks and open questions.
9. Validation commands.
10. Recommendation to proceed or not.

Use:

- `superpowers:brainstorming` for discovery, design, alternatives, and clarifying questions.
- `superpowers:test-driven-development` for testable behavior changes.
- `superpowers:verification-before-completion` before closing.
