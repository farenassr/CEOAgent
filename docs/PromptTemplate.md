Start this as a harness-engineered development phase.

Phase goal: <describe the goal clearly>

Context:

- This repository uses `AGENTS.md` as the root operating guide.
- This repository uses harness engineering.
- Use `AIHarness/` docs only when relevant.
- Use `scripts/` instead of ad-hoc commands when possible.
- Use the custom Codex subagents defined under `.codex/agents/`.
- Do not make code changes until you have inspected the relevant files and produced a plan.
- Make code changes once you have inspected the relevant files and produced a plan.

## Ambiguity Protocol & Workflow

* **Ambiguity Handling:** If any requirements are unclear, **do not assume**. Stop immediately and ask clarifying questions before proceeding.
* **Design Phase:** Execute `superpowers:brainstorming` to scope and design the architecture of the solution.
* **Development Phase:** Apply `superpowers:test-driven-development` to implement validation checks *before* writing any scripts.
* **Closure Phase:** Run `superpowers:verification-before-completion` to validate the final output and close the task with clear evidence.

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

1. Follow the next steps:
   1.1 Read `AGENTS.md`.
   1.2. Check `git status --short`.
   1.3. Inspect the relevant code, tests, and harness docs.
   1.4. Identify which `AIHarness/` docs are relevant and do not load unrelated docs.
   1.5. Select the relevant subagents from `.codex/agents/` based on the phase goal.

2. Codebase scout phase
   - Use codebase-scout if available.
   - Inspect relevant code, tests, scripts, and harness docs.
   - Produce a grounded summary of the current implementation.
   - Do not modify files on this phase.

3. Specialist review phase
   Use relevant subagents in parallel:
   - architecture-reviewer for architecture, module boundaries, vertical slices, project references, and design risks.
   - backend-engineer for .NET implementation, FastEndpoints, Mediator workflows, application logic, Worker jobs, and service boundaries.
   - db-specialist for EF Core, PostgreSQL, JSONB, indexes, query filters, tenant isolation, and data modeling.
   - integrations-engineer for ports/adapters, WhatsApp Cloud, Google Calendar, external APIs, provider boundaries, and tool execution.
   - ai-engineer for Microsoft Agent Framework, agent loop, prompts, structured output, tool calling, model behavior, evals, and LLM safety.
   - testing-engineer for unit tests, integration tests, Aspire Testing, Testcontainers, fixtures, evals, and regression coverage.
   - code-simplifier for redundant code, duplicated queries, LINQ/EF Core simplification, and behavior-preserving refactors.

4. Planning phase
   - Consolidate codebase-scout and specialist findings.
   - Produce a concise implementation plan.
   - List files or areas likely to change.
   - List tests/evals/docs that may need updates.
   - List risks, open questions, and validation commands.
   - Wait for approval before editing files on this phase.

5. Implementation phase
   - After approval, implement the smallest durable change.
   - Do not broaden scope.
   - Do not refactor unrelated code.
   - Do not revert unrelated user changes.
   - Do not run, scaffold, remove, or apply EF migrations unless explicitly requested.

6. Verification phase
   - Use testing-engineer if useful.
   - Run the narrowest meaningful validation command.
   - Review changed files.
   - Report exact commands run, pass/fail result, changed files, and anything not verified.

Use subagents deliberately:

- For architecture-heavy work, use `architecture-reviewer`.
- For backend implementation work, use `backend-engineer`.
- For database or persistence changes, use `db-specialist`.
- For external integrations or tool execution, use `integrations-engineer`.
- For AI runtime, prompts, tool calling, structured output, or eval behavior, use `ai-engineer`.
- For test strategy or regression coverage, use `testing-engineer`.

If relevant subagents exist, run them in parallel before producing the final plan.

Each selected subagent should return:

1. Main risks.
2. Relevant files or areas.
3. Recommended changes.
4. Tests, evals, or docs that may need updates.
5. Anything that should not be changed.

Then consolidate all findings into one implementation plan:

1. Concise phase summary.
2. Proposed implementation steps.
3. Files or areas likely to change.
4. Tests/evals/docs that may need updates.
5. Risks and open questions.
6. Validation commands to run.
7. Clear recommendation on whether to proceed.

Wait for approval before editing files.

Constraints:

- Follow `AGENTS.md`.
- Prefer small, reversible changes.
- Do not refactor unrelated code.
- Do not revert unrelated user changes.
- Do not run, scaffold, remove, or apply EF migrations unless explicitly requested.
- Do not introduce post-MVP features unless required by this task.
- Update the necessary markdown files, mermaid files inside of the folder docs.
