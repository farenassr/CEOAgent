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

## Blob Storage Conventions

- Runtime connection strings stay environmental and are supplied by Aspire or
  deployment configuration.
- Provider-neutral blob DTOs and value objects live in `CeoAgent.Shared`.
- Blob upload ports live in `CeoAgent.Application.Abstractions.Storage`; Azure
  implementations live in Infrastructure.
- Canonical blob tag keys and values stay in code because they are part of the
  storage contract, not environment-specific configuration.
- Payment account QR images are uploaded from multipart form files. The API
  accepts PNG/JPEG only, stores them in the `private` container, and persists
  both the blob name and the full storage URI.
- Payment QR blob names use `filename-guid.extension`, where `filename` is a
  slug derived from the uploaded file name and the GUID is the payment account
  id.

## Harness Checks

- Use `AIHarness/scripts/aspire-smoke.ps1 -StartAppHost` for an opt-in local health
  smoke check.
- Use `AIHarness/scripts/harness-check.ps1` for static harness checks before closing
  harness changes.
