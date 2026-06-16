using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.Filters;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure;

public sealed class CeoAgentDbContext(
    DbContextOptions<CeoAgentDbContext> options,
    IOrganizationContextProvider organizationContext,
    TimeProvider timeProvider) : DbContext(options)
{
    internal Guid? CurrentOrganizationId => organizationContext.OrganizationId;

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Bank> Banks => Set<Bank>();

    public DbSet<CompanyPaymentAccount> CompanyPaymentAccounts => Set<CompanyPaymentAccount>();

    public DbSet<CompanyChannel> CompanyChannels => Set<CompanyChannel>();

    public DbSet<AgentProfile> AgentProfiles => Set<AgentProfile>();

    public DbSet<CompanyTool> CompanyTools => Set<CompanyTool>();

    public DbSet<IntegrationCredentialReference> IntegrationCredentialReferences => Set<IntegrationCredentialReference>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<IncomingMessageOutbox> IncomingMessageOutbox => Set<IncomingMessageOutbox>();

    public DbSet<OutgoingMessageOutbox> OutgoingMessageOutbox => Set<OutgoingMessageOutbox>();

    public DbSet<ProviderSendLedger> ProviderSendLedger => Set<ProviderSendLedger>();

    public DbSet<ConversationState> ConversationStates => Set<ConversationState>();

    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CeoAgentDbContext).Assembly);

        OrganizationQueryFilterApplier.ApplyOrganizationFilters(modelBuilder, this);
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
        throw new NotSupportedException("Use SaveChangesAsync in CEOAgent. Synchronous SaveChanges is not supported.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new NotSupportedException("Use SaveChangesAsync in CEOAgent. Synchronous SaveChanges is not supported.");
    }

    private async Task ValidateTenantRelationshipsAsync(CancellationToken cancellationToken)
    {
        foreach (var tool in Changed<CompanyTool>())
        {
            if (tool.CredentialReferenceId is { } credentialReferenceId)
            {
                await EnsureSameOrganizationAsync<IntegrationCredentialReference>(
                    tool.OrganizationId,
                    credentialReferenceId,
                    "CompanyTool.CredentialReferenceId",
                    cancellationToken);
            }
        }

        foreach (var channel in Changed<CompanyChannel>())
        {
            if (channel.CredentialReferenceId is { } credentialReferenceId)
            {
                await EnsureSameOrganizationAsync<IntegrationCredentialReference>(
                    channel.OrganizationId,
                    credentialReferenceId,
                    "CompanyChannel.CredentialReferenceId",
                    cancellationToken);
            }
        }

        foreach (var customer in Changed<Customer>())
        {
            await EnsureSameOrganizationAsync<CompanyChannel>(
                customer.OrganizationId,
                customer.CompanyChannelId,
                "Customer.CompanyChannelId",
                cancellationToken);
        }

        foreach (var conversation in Changed<Conversation>())
        {
            await EnsureSameOrganizationAsync<Customer>(
                conversation.OrganizationId,
                conversation.CustomerId,
                "Conversation.CustomerId",
                cancellationToken);
            await EnsureSameOrganizationAsync<CompanyChannel>(
                conversation.OrganizationId,
                conversation.CompanyChannelId,
                "Conversation.CompanyChannelId",
                cancellationToken);
            await EnsureSameOrganizationAsync<AgentProfile>(
                conversation.OrganizationId,
                conversation.AgentProfileId,
                "Conversation.AgentProfileId",
                cancellationToken);
        }

        foreach (var state in Changed<ConversationState>())
        {
            await EnsureSameOrganizationAsync<Conversation>(
                state.OrganizationId,
                state.ConversationId,
                "ConversationState.ConversationId",
                cancellationToken);
        }

        foreach (var message in Changed<Message>())
        {
            await EnsureSameOrganizationAsync<Conversation>(
                message.OrganizationId,
                message.ConversationId,
                "Message.ConversationId",
                cancellationToken);
        }

        foreach (var outbox in Changed<IncomingMessageOutbox>())
        {
            await EnsureSameOrganizationAsync<Conversation>(
                outbox.OrganizationId,
                outbox.ConversationId,
                "IncomingMessageOutbox.ConversationId",
                cancellationToken);
            await EnsureSameOrganizationAsync<Message>(
                outbox.OrganizationId,
                outbox.MessageId,
                "IncomingMessageOutbox.MessageId",
                cancellationToken);
        }

        foreach (var outbox in Changed<OutgoingMessageOutbox>())
        {
            await EnsureSameOrganizationAsync<Conversation>(
                outbox.OrganizationId,
                outbox.ConversationId,
                "OutgoingMessageOutbox.ConversationId",
                cancellationToken);
            await EnsureSameOrganizationAsync<Message>(
                outbox.OrganizationId,
                outbox.MessageId,
                "OutgoingMessageOutbox.MessageId",
                cancellationToken);
        }

        foreach (var ledger in Changed<ProviderSendLedger>())
        {
            await EnsureSameOrganizationAsync<OutgoingMessageOutbox>(
                ledger.OrganizationId,
                ledger.OutgoingMessageOutboxId,
                "ProviderSendLedger.OutgoingMessageOutboxId",
                cancellationToken);
        }

        foreach (var execution in Changed<ToolExecution>())
        {
            await EnsureSameOrganizationAsync<Conversation>(
                execution.OrganizationId,
                execution.ConversationId,
                "ToolExecution.ConversationId",
                cancellationToken);
            await EnsureSameOrganizationAsync<CompanyTool>(
                execution.OrganizationId,
                execution.CompanyToolId,
                "ToolExecution.CompanyToolId",
                cancellationToken);
            await EnsureSameOrganizationAsync<Message>(
                execution.OrganizationId,
                execution.TriggerMessageId,
                "ToolExecution.TriggerMessageId",
                cancellationToken);
            if (execution.ResultMessageId is { } resultMessageId)
            {
                await EnsureSameOrganizationAsync<Message>(
                    execution.OrganizationId,
                    resultMessageId,
                    "ToolExecution.ResultMessageId",
                    cancellationToken);
            }
        }
    }

    private IEnumerable<TEntity> Changed<TEntity>()
        where TEntity : OrganizationOwnedEntity
    {
        return ChangeTracker.Entries<TEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity);
    }

    private async Task EnsureSameOrganizationAsync<TEntity>(
        Guid dependentOrganizationId,
        Guid principalId,
        string relationshipName,
        CancellationToken cancellationToken)
        where TEntity : OrganizationOwnedEntity
    {
        var principalOrganizationId = TrackedOrganizationId<TEntity>(principalId);
        if (principalOrganizationId is null)
        {
            principalOrganizationId = await Set<TEntity>()
                .IgnoreQueryFilters()
                .Where(entity => EF.Property<Guid>(entity, "Id") == principalId)
                .Select(entity => (Guid?)entity.OrganizationId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (principalOrganizationId is not null && principalOrganizationId != dependentOrganizationId)
        {
            throw new InvalidOperationException(
                $"{relationshipName} creates a cross-organization relationship.");
        }
    }

    private Guid? TrackedOrganizationId<TEntity>(Guid id)
        where TEntity : OrganizationOwnedEntity
    {
        return ChangeTracker.Entries<TEntity>()
            .Where(entry => entry.State != EntityState.Deleted
                && entry.Property("Id").CurrentValue is Guid entityId
                && entityId == id)
            .Select(entry => (Guid?)entry.Entity.OrganizationId)
            .FirstOrDefault();
    }

    private void StampAuditableEntities()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditableOrganizationOwnedEntity organizationOwned
                && entry.State is EntityState.Added or EntityState.Modified)
            {
                if (organizationOwned.OrganizationId == Guid.Empty)
                {
                    throw new InvalidOperationException("Organization-owned entity requires a non-empty OrganizationId.");
                }

                organizationOwned.UpdatedAt = now;

                if (entry.State == EntityState.Added)
                {
                    organizationOwned.CreatedAt = now;
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

            if (entry.Entity is Bank bank
                && entry.State is EntityState.Added or EntityState.Modified)
            {
                bank.UpdatedAt = now;

                if (entry.State == EntityState.Added)
                {
                    bank.CreatedAt = now;
                }
            }
        }
    }
}
