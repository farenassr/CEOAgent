# CeoAgent Plans

Use this file as the index for implementation plans and harness proposals.

## Active Harness Plan

Goal: improve long-running Codex reliability through root instructions,
stable scripts, architecture docs, WhatsApp eval fixtures, and sanitized trace
examples without changing business logic.

Current low-risk scaffolding:

- Root `AGENTS.md`.
- `scripts/build.ps1`, `scripts/test.ps1`, `scripts/format.ps1`.
- Architecture and integration docs under `docs/`.
- WhatsApp eval fixtures under `evals/whatsapp/`.
- Sanitized trace examples under `traces/examples/`.

## Future Plans

Create detailed task plans under `docs/superpowers/plans/` when a change
requires code, test, or migration work. Each plan should include exact files,
commands, expected verification, and rollback notes.
