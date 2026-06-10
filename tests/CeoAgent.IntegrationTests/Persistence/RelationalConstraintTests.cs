using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Shared.Enums;
using CeoAgent.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace CeoAgent.IntegrationTests.Persistence;

public sealed class RelationalConstraintTests
{
    /// <summary>
    /// Verifies that a company, customer, and channel can have only one open conversation.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenTwoOpenConversationsExistForSameCompanyCustomerAndChannel_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);

        database.Context.Conversations.AddRange(
            CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId),
            CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that closed conversations do not participate in the one-open-conversation uniqueness rule.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenTwoClosedConversationsExistForSameCompanyCustomerAndChannel_AllowsBoth()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);

        database.Context.Conversations.AddRange(
            CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId, ConversationStatus.Closed),
            CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId, ConversationStatus.Closed));

        await database.Context.SaveChangesAsync();

        var count = await database.Context.Conversations.IgnoreQueryFilters().CountAsync();
        count.ShouldBe(2);
    }

    /// <summary>
    /// Verifies that provider message IDs are unique within a company.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenDuplicateProviderMessageIdExistsInCompany_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);
        var conversation = CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        database.Context.Conversations.Add(conversation);

        database.Context.Messages.AddRange(
            CreateMessage(companyId, conversation.Id, "wamid.duplicate"),
            CreateMessage(companyId, conversation.Id, "wamid.duplicate"));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that messages without provider IDs can be stored more than once.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenProviderMessageIdIsNull_AllowsMultipleMessages()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);
        var conversation = CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        database.Context.Conversations.Add(conversation);

        database.Context.Messages.AddRange(
            CreateMessage(companyId, conversation.Id, null),
            CreateMessage(companyId, conversation.Id, null));

        await database.Context.SaveChangesAsync();

        var count = await database.Context.Messages.IgnoreQueryFilters().CountAsync();
        count.ShouldBe(2);
    }

    /// <summary>
    /// Verifies that a customer external ID is unique within the same company channel.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenDuplicateCustomerExistsForCompanyChannel_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);

        database.Context.Customers.AddRange(
            CreateCustomer(companyId, seed.ChannelId, "573001112233"),
            CreateCustomer(companyId, seed.ChannelId, "573001112233"));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that each conversation can have only one conversation state row.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenDuplicateConversationStateExists_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);
        var conversation = CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        database.Context.Conversations.Add(conversation);

        database.Context.ConversationStates.Add(
            new ConversationState { CompanyId = companyId, ConversationId = conversation.Id, Snapshot = new ConversationStateSnapshot() });
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        database.Context.ConversationStates.Add(
            new ConversationState { CompanyId = companyId, ConversationId = conversation.Id, Snapshot = new ConversationStateSnapshot() });

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that tool execution idempotency keys are unique within a company.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenDuplicateToolExecutionIdempotencyKeyExistsForCompany_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);
        var conversation = CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        var triggerMessage = CreateMessage(companyId, conversation.Id, null, MessageRole.Assistant);

        database.Context.Conversations.Add(conversation);
        database.Context.Messages.Add(triggerMessage);
        database.Context.ToolExecutions.AddRange(
            CreateToolExecution(companyId, conversation.Id, seed.ToolId, triggerMessage.Id, "same-key"),
            CreateToolExecution(companyId, conversation.Id, seed.ToolId, triggerMessage.Id, "same-key"));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    [Test]
    public async Task SaveChanges_WhenCompanyToolReferencesCredentialFromDifferentCompany_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var otherCompanyId = Guid.CreateVersion7();
        await database.SeedCompanyGraphAsync(companyId);
        await database.SeedCompanyGraphAsync(otherCompanyId);
        var otherCredential = new IntegrationCredentialReference
        {
            CompanyId = otherCompanyId,
            Provider = IntegrationProvider.GoogleCalendar,
            Purpose = "calendar",
            Reference = "kv://other-company/google-calendar",
        };
        database.Context.IntegrationCredentialReferences.Add(otherCredential);
        await database.Context.SaveChangesAsync();

        database.Context.CompanyTools.Add(new CompanyTool
        {
            CompanyId = companyId,
            ToolKey = "cross_company_tool",
            CredentialReferenceId = otherCredential.Id,
        });

        var exception = await Should.ThrowAsync<InvalidOperationException>(database.Context.SaveChangesAsync());
        exception.Message.ShouldContain("cross-company relationship");
    }

    [Test]
    public async Task SaveChanges_WhenConversationReferencesCustomerFromDifferentCompany_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var otherCompanyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);
        var otherSeed = await database.SeedCompanyGraphAsync(otherCompanyId);

        database.Context.Conversations.Add(CreateConversation(
            companyId,
            otherSeed.CustomerId,
            seed.ChannelId,
            seed.AgentProfileId));

        var exception = await Should.ThrowAsync<InvalidOperationException>(database.Context.SaveChangesAsync());
        exception.Message.ShouldContain("cross-company relationship");
    }

    [Test]
    public async Task SaveChanges_WhenMessageReferencesConversationFromDifferentCompany_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var otherCompanyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);
        var otherSeed = await database.SeedCompanyGraphAsync(otherCompanyId);
        var otherConversation = CreateConversation(otherCompanyId, otherSeed.CustomerId, otherSeed.ChannelId, otherSeed.AgentProfileId);
        database.Context.Conversations.Add(otherConversation);
        await database.Context.SaveChangesAsync();

        database.Context.Messages.Add(CreateMessage(companyId, otherConversation.Id, "wamid.cross-company"));

        var exception = await Should.ThrowAsync<InvalidOperationException>(database.Context.SaveChangesAsync());
        exception.Message.ShouldContain("cross-company relationship");
    }

    /// <summary>
    /// Verifies that a conversation cannot change its agent profile after creation.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenConversationAgentProfileIdChangesAfterCreation_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);
        var conversation = CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        database.Context.Conversations.Add(conversation);
        await database.Context.SaveChangesAsync();

        conversation.AgentProfileId = Guid.CreateVersion7();

        var exception = Should.Throw<InvalidOperationException>(() => database.Context.SaveChangesAsync());
        exception.Message.ShouldContain("Conversation.AgentProfileId is immutable");
    }

    private static Conversation CreateConversation(
        Guid companyId,
        Guid customerId,
        Guid channelId,
        Guid agentProfileId,
        ConversationStatus status = ConversationStatus.Open)
    {
        return new Conversation
        {
            CompanyId = companyId,
            CustomerId = customerId,
            CompanyChannelId = channelId,
            AgentProfileId = agentProfileId,
            Status = status,
            LastMessageAt = TimeProvider.System.GetUtcNow().UtcDateTime
        };
    }

    private static Message CreateMessage(
        Guid companyId,
        Guid conversationId,
        string? providerMessageId,
        MessageRole role = MessageRole.User)
    {
        return new Message
        {
            CompanyId = companyId,
            ConversationId = conversationId,
            Role = role,
            MessageText = "hello",
            ProviderMessageId = providerMessageId,
            OccurredAt = TimeProvider.System.GetUtcNow().UtcDateTime
        };
    }

    private static Customer CreateCustomer(Guid companyId, Guid channelId, string externalCustomerId)
    {
        return new Customer
        {
            CompanyId = companyId,
            CompanyChannelId = channelId,
            ExternalCustomerId = externalCustomerId
        };
    }

    private static ToolExecution CreateToolExecution(
        Guid companyId,
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        string idempotencyKey)
    {
        return new ToolExecution
        {
            CompanyId = companyId,
            ConversationId = conversationId,
            CompanyToolId = companyToolId,
            TriggerMessageId = triggerMessageId,
            ToolKey = "request_human_handoff",
            IdempotencyKey = idempotencyKey,
            Status = ToolExecutionStatus.Pending
        };
    }

}
