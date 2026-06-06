# Persistence Query Extensions

Shared EF Core query shapes live in `CeoAgent.Infrastructure/Persistence/Extensions`
under the namespace `CeoAgent.Infrastructure.Persistence.Extensions`.

Use these extensions for reusable `IQueryable<TEntity>` filters and includes that
are tied to infrastructure entities, such as company scoping, enabled company
tools, conversation messages, and tool execution idempotency lookups.

Rules:

- Keep query extensions entity-focused. Do not move Worker orchestration,
  provider SDK calls, tool execution policy, or DTO construction into
  Infrastructure.
- Keep queries composable by returning `IQueryable<TEntity>` unless the helper
  explicitly needs tracked entities from `CeoAgentDbContext.ChangeTracker`
  before querying PostgreSQL.
- Keep explicit `CompanyId` predicates for company-owned reads even when global
  EF Core filters also apply.
- Use `IgnoreQueryFilters()` only in narrowly named helpers that immediately
  re-apply company and aggregate predicates.
- Keep `AsNoTracking()` at the call site when the caller needs read-only entity
  context. Do not hide tracking decisions inside broad query helpers.
- Do not add repositories, generic repositories, or custom unit-of-work
  abstractions.

Provider implementation code under `CeoAgent.Infrastructure/Implementation` must
not move SDK response shaping into `CeoAgent.Infrastructure`; SDK response
objects are not part of this query-extension surface.
