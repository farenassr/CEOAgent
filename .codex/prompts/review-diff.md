# Codex Diff Review Prompt

Use this prompt for ordinary branch reviews.

Review only the current diff against the project rules in `AGENTS.md`.
Prioritize correctness, security, company isolation, integration boundaries,
missing tests, and production reliability. Lead with actionable findings and
file/line references. If there are no issues, say that clearly and note any
remaining test gaps.
