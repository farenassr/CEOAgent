using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.Filters;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure;

public sealed class CeoAgentDbContext(
    DbContextOptions<CeoAgentDbContext> options,
    ICompanyContext companyContext,
    TimeProvider timeProvider) : DbContext(options)
{
    internal Guid? CurrentCompanyId => companyContext.CompanyId;

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanyChannel> CompanyChannels => Set<CompanyChannel>();

    public DbSet<AgentProfile> AgentProfiles => Set<AgentProfile>();

    public DbSet<CompanyTool> CompanyTools => Set<CompanyTool>();

    public DbSet<IntegrationCredentialReference> IntegrationCredentialReferences => Set<IntegrationCredentialReference>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<ConversationState> ConversationStates => Set<ConversationState>();

    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CeoAgentDbContext).Assembly);

        CompanyQueryFilterApplier.ApplyCompanyFilters(modelBuilder, this);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditableEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditableEntities();
        return base.SaveChanges();
    }

    private void StampAuditableEntities()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditableCompanyOwnedEntity companyOwned
                && entry.State is EntityState.Added or EntityState.Modified)
            {
                if (companyOwned.CompanyId == Guid.Empty)
                {
                    throw new InvalidOperationException("Company-owned entity requires a non-empty CompanyId.");
                }

                companyOwned.UpdatedAt = now;

                if (entry.State == EntityState.Added)
                {
                    companyOwned.CreatedAt = now;
                }
            }

            if (entry.Entity is Company company
                && entry.State is EntityState.Added or EntityState.Modified)
            {
                company.UpdatedAt = now;

                if (entry.State == EntityState.Added)
                {
                    company.CreatedAt = now;
                }
            }
        }
    }
}
