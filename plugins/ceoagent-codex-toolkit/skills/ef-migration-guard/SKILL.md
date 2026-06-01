---
name: ef-migration-guard
description: Review and create CeoAgent EF Core PostgreSQL migrations safely. Use when modifying CeoAgent.Infrastructure entities/configurations, JSONB mappings, global tenant filters, DbContext setup, migration files, model snapshots, or persistence tests, especially around company isolation and secret-reference storage.
---

# EF Migration Guard

Use this skill for EF Core work in CeoAgent. The DbContext is `CeoAgent.Infrastructure/CEOAgentDbContext.cs`; migrations live under `CeoAgent.Infrastructure/Persistence/Migrations`.

## Hard Rules

- Do not run `dotnet ef database update` unless the user explicitly asks for a database mutation.
- Do not store raw secrets in entities, migrations, snapshots, JSONB metadata, appsettings, or tests. Credential rows store opaque references such as `config://...` or Azure Key Vault secret URIs.
- Preserve multi-tenancy: company-owned rows must keep `CompanyId` and the EF global filters must continue to fail closed when no company context is set.
- Review both migration `.cs` files and `CeoAgentDbContextModelSnapshot.cs`; snapshot drift is a bug.
- Prefer PostgreSQL/Testcontainers coverage for relational behavior. Use InMemory only for narrow unit-style tests.

## Migration Workflow

1. Inspect changed entities, `IEntityTypeConfiguration<>` classes, and `CEOAgentDbContext`.
2. Determine whether a migration is actually required. Mapping-only code may still require a snapshot change.
3. If scaffolding is requested, use the Infrastructure project as the migrations project and the API/AppHost only when needed as startup context.
4. After scaffolding or editing, inspect generated operations for destructive changes, secret-bearing columns, accidental table splits, enum/string conversion drift, and JSON property names.
5. Run focused tests before broad tests:

```powershell
dotnet build CEOAgent.slnx
dotnet test tests/CeoAgent.IntegrationTests/CeoAgent.IntegrationTests.csproj --filter "Persistence|Company|Json"
```

## Review Checklist

- No column, JSON property, response DTO, log message, or seed value contains `private_key`, access tokens, passwords, API keys, or full service account JSON.
- `IntegrationCredentialReference.Reference` remains an opaque pointer, not materialized credential content.
- JSONB mappings use stable snake_case property names and preserve existing data shape unless a deliberate migration is present.
- Foreign keys, unique indexes, and tenant columns match `docs/data-model.md`.
- New endpoints or workers that load company-owned data set company context before querying.
- Tests include at least one failure/negative path when a persistence change could weaken tenant isolation or credential boundaries.

## Useful Searches

```powershell
rg -n "private_key|access_token|api[_-]?key|secret|password" CeoAgent.Infrastructure CeoAgent.ApiService CeoAgent.Adapters tests
rg -n "HasQueryFilter|CompanyId|ICompanyOwned|IntegrationCredentialReference|HasJsonPropertyName" CeoAgent.Infrastructure tests
```
