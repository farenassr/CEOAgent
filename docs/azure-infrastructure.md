# Azure Infrastructure

This document anchors the README link for local and Azure infrastructure
notes. The current MVP uses Aspire for local orchestration and keeps runtime
connection strings supplied by Aspire references.

## Local Development

- Start local dependencies through `CeoAgent.AppHost`.
- PostgreSQL, Azure Storage Queues, and Azure Blob Storage are supplied by
  Aspire resources.
- Local user-secrets may hold development-only parameters such as PostgreSQL
  password, Langfuse keys, WhatsApp test credentials, and design-time EF
  connection strings.
- Agents must not run `dotnet ef database update`; migration application is a
  manual human action.

## Shared Or Deployed Environments

- Store provider credentials, webhook secrets, and API keys in Azure Key Vault
  or equivalent managed secret storage.
- Do not store PostgreSQL, queue, or blob runtime connection strings in Key
  Vault when Aspire `.WithReference(...)` supplies them.
- Database rows store credential references such as `kv://...`, never raw
  secret values.

## Harness Checks

- Use `scripts/aspire-smoke.ps1 -StartAppHost` for an opt-in local health
  smoke check.
- Use `scripts/harness-check.ps1` for static harness checks before closing
  harness changes.
