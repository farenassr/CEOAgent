using CEOAgent.Application.Company;
using CEOAgent.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CEOAgent.Infrastructure.Persistence;

public sealed class CEOAgentDbContext(
    DbContextOptions<CEOAgentDbContext> options,
    ICompanyContext companyContext,
    TimeProvider timeProvider) : DbContext(options)
{
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

    public DbSet<AudioAsset> AudioAssets => Set<AudioAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CEOAgentDbContext).Assembly);

        modelBuilder.Entity<CompanyChannel>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
        modelBuilder.Entity<AgentProfile>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
        modelBuilder.Entity<CompanyTool>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
        modelBuilder.Entity<IntegrationCredentialReference>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
        modelBuilder.Entity<Customer>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
        modelBuilder.Entity<Conversation>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
        modelBuilder.Entity<Message>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
        modelBuilder.Entity<ConversationState>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
        modelBuilder.Entity<ToolExecution>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
        modelBuilder.Entity<AudioAsset>().HasQueryFilter(entity => companyContext.CompanyId.HasValue && entity.CompanyId == companyContext.CompanyId);
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

        foreach (var entry in ChangeTracker.Entries().ToArray())
        {
            if (entry.Entity is Conversation
                && entry.State == EntityState.Modified
                && entry.Property(nameof(Conversation.AgentProfileId)).IsModified)
            {
                throw new InvalidOperationException("Conversation.AgentProfileId is immutable after conversation creation.");
            }

            if (entry.Entity is AuditableCompanyOwnedEntity companyOwned
                && entry.State is EntityState.Added or EntityState.Modified)
            {
                if (companyOwned.CompanyId == Guid.Empty && companyContext.CompanyId is { } companyId)
                {
                    companyOwned.CompanyId = companyId;
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
