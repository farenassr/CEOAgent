# CeoAgent Codex Overlay

This file is intentionally short. Keep shared project guidance in the root guide.

The canonical project rules are in `../AGENTS.md`. Read that file first for
architecture, security, database, integration, AI/tool-safety, command, commit,
and completion rules.

## Codex-Specific Assets

- `.codex/config.toml`: Codex agent concurrency configuration.
- `.codex/agents/*.toml`: project-scoped Codex subagents.
- `.codex/prompts/*.md`: reusable Codex task prompts.

## Subagent Rules

- Prefer read-only subagents for scouting and review.
- Avoid running multiple workspace-write agents on the same module at once.
- Subagent prompts must reference `../AGENTS.md` as the rubric instead of restating project-wide rules.
