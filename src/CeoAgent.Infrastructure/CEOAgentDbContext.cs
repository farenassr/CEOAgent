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

    public DbSet<IncomingMessageOutbox> IncomingMessageOutbox => Set<IncomingMessageOutbox>();

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
        return SaveChangesWithTenantValidationAsync(cancellationToken);
    }

    private async Task<int> SaveChangesWithTenantValidationAsync(CancellationToken cancellationToken)
    {
        await ValidateTenantRelationshipsAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditableEntities();
        ValidateTenantRelationshipsAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges();
    }

    private async Task ValidateTenantRelationshipsAsync(CancellationToken cancellationToken)
    {
        foreach (var tool in Changed<CompanyTool>())
        {
            if (tool.CredentialReferenceId is { } credentialReferenceId)
            {
                await EnsureSameCompanyAsync<IntegrationCredentialReference>(
                    tool.CompanyId,
                    credentialReferenceId,
                    "CompanyTool.CredentialReferenceId",
                    cancellationToken);
            }
        }

        foreach (var channel in Changed<CompanyChannel>())
        {
            if (channel.CredentialReferenceId is { } credentialReferenceId)
            {
                await EnsureSameCompanyAsync<IntegrationCredentialReference>(
                    channel.CompanyId,
                    credentialReferenceId,
                    "CompanyChannel.CredentialReferenceId",
                    cancellationToken);
            }
        }

        foreach (var customer in Changed<Customer>())
        {
            await EnsureSameCompanyAsync<CompanyChannel>(
                customer.CompanyId,
                customer.CompanyChannelId,
                "Customer.CompanyChannelId",
                cancellationToken);
        }

        foreach (var conversation in Changed<Conversation>())
        {
            await EnsureSameCompanyAsync<Customer>(
                conversation.CompanyId,
                conversation.CustomerId,
                "Conversation.CustomerId",
                cancellationToken);
            await EnsureSameCompanyAsync<CompanyChannel>(
                conversation.CompanyId,
                conversation.CompanyChannelId,
                "Conversation.CompanyChannelId",
                cancellationToken);
            await EnsureSameCompanyAsync<AgentProfile>(
                conversation.CompanyId,
                conversation.AgentProfileId,
                "Conversation.AgentProfileId",
                cancellationToken);
        }

        foreach (var state in Changed<ConversationState>())
        {
            await EnsureSameCompanyAsync<Conversation>(
                state.CompanyId,
                state.ConversationId,
                "ConversationState.ConversationId",
                cancellationToken);
        }

        foreach (var message in Changed<Message>())
        {
            await EnsureSameCompanyAsync<Conversation>(
                message.CompanyId,
                message.ConversationId,
                "Message.ConversationId",
                cancellationToken);
        }

        foreach (var outbox in Changed<IncomingMessageOutbox>())
        {
            await EnsureSameCompanyAsync<Conversation>(
                outbox.CompanyId,
                outbox.ConversationId,
                "IncomingMessageOutbox.ConversationId",
                cancellationToken);
            await EnsureSameCompanyAsync<Message>(
                outbox.CompanyId,
                outbox.MessageId,
                "IncomingMessageOutbox.MessageId",
                cancellationToken);
        }

        foreach (var execution in Changed<ToolExecution>())
        {
            await EnsureSameCompanyAsync<Conversation>(
                execution.CompanyId,
                execution.ConversationId,
                "ToolExecution.ConversationId",
                cancellationToken);
            await EnsureSameCompanyAsync<CompanyTool>(
                execution.CompanyId,
                execution.CompanyToolId,
                "ToolExecution.CompanyToolId",
                cancellationToken);
            await EnsureSameCompanyAsync<Message>(
                execution.CompanyId,
                execution.TriggerMessageId,
                "ToolExecution.TriggerMessageId",
                cancellationToken);
            if (execution.ResultMessageId is { } resultMessageId)
            {
                await EnsureSameCompanyAsync<Message>(
                    execution.CompanyId,
                    resultMessageId,
                    "ToolExecution.ResultMessageId",
                    cancellationToken);
            }
        }
    }

    private IEnumerable<TEntity> Changed<TEntity>()
        where TEntity : CompanyOwnedEntity
    {
        return ChangeTracker.Entries<TEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity);
    }

    private async Task EnsureSameCompanyAsync<TEntity>(
        Guid dependentCompanyId,
        Guid principalId,
        string relationshipName,
        CancellationToken cancellationToken)
        where TEntity : CompanyOwnedEntity
    {
        var principalCompanyId = TrackedCompanyId<TEntity>(principalId);
        if (principalCompanyId is null)
        {
            principalCompanyId = await Set<TEntity>()
                .IgnoreQueryFilters()
                .Where(entity => EF.Property<Guid>(entity, "Id") == principalId)
                .Select(entity => (Guid?)entity.CompanyId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (principalCompanyId is not null && principalCompanyId != dependentCompanyId)
        {
            throw new InvalidOperationException(
                $"{relationshipName} creates a cross-company relationship.");
        }
    }

    private Guid? TrackedCompanyId<TEntity>(Guid id)
        where TEntity : CompanyOwnedEntity
    {
        return ChangeTracker.Entries<TEntity>()
            .Where(entry => entry.State != EntityState.Deleted
                && entry.Property("Id").CurrentValue is Guid entityId
                && entityId == id)
            .Select(entry => (Guid?)entry.Entity.CompanyId)
            .FirstOrDefault();
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
