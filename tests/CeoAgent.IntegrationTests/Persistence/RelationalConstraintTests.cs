using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Payment;
using CeoAgent.Shared.Storage;
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
        var organizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);

        database.Context.Conversations.AddRange(
            CreateConversation(organizationId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId),
            CreateConversation(organizationId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that closed conversations do not participate in the one-open-conversation uniqueness rule.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenTwoClosedConversationsExistForSameCompanyCustomerAndChannel_AllowsBoth()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);

        database.Context.Conversations.AddRange(
            CreateConversation(organizationId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId, ConversationStatus.Closed),
            CreateConversation(organizationId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId, ConversationStatus.Closed));

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
        var organizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);
        var conversation = CreateConversation(organizationId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        database.Context.Conversations.Add(conversation);

        database.Context.Messages.AddRange(
            CreateMessage(organizationId, conversation.Id, "wamid.duplicate"),
            CreateMessage(organizationId, conversation.Id, "wamid.duplicate"));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that messages without provider IDs can be stored more than once.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenProviderMessageIdIsNull_AllowsMultipleMessages()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);
        var conversation = CreateConversation(organizationId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        database.Context.Conversations.Add(conversation);

        database.Context.Messages.AddRange(
            CreateMessage(organizationId, conversation.Id, null),
            CreateMessage(organizationId, conversation.Id, null));

        await database.Context.SaveChangesAsync();

        var count = await database.Context.Messages.IgnoreQueryFilters().CountAsync();
        count.ShouldBe(2);
    }

    /// <summary>
    /// Verifies that a customer external ID is unique within the same company channel.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenDuplicateCustomerExistsForOrganizationChannel_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);

        database.Context.Customers.AddRange(
            CreateCustomer(organizationId, seed.ChannelId, "573001112233"),
            CreateCustomer(organizationId, seed.ChannelId, "573001112233"));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that each conversation can have only one conversation state row.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenDuplicateConversationStateExists_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);
        var conversation = CreateConversation(organizationId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        database.Context.Conversations.Add(conversation);

        database.Context.ConversationStates.Add(
            new ConversationState { OrganizationId = organizationId, ConversationId = conversation.Id, Snapshot = new ConversationStateSnapshot() });
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        database.Context.ConversationStates.Add(
            new ConversationState { OrganizationId = organizationId, ConversationId = conversation.Id, Snapshot = new ConversationStateSnapshot() });

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that tool execution idempotency keys are unique within a company.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenDuplicateToolExecutionIdempotencyKeyExistsForOrganization_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);
        var conversation = CreateConversation(organizationId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        var triggerMessage = CreateMessage(organizationId, conversation.Id, null, MessageRole.Assistant);

        database.Context.Conversations.Add(conversation);
        database.Context.Messages.Add(triggerMessage);
        database.Context.ToolExecutions.AddRange(
            CreateToolExecution(organizationId, conversation.Id, seed.ToolId, triggerMessage.Id, "same-key"),
            CreateToolExecution(organizationId, conversation.Id, seed.ToolId, triggerMessage.Id, "same-key"));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that one company can have only one active default payment account per currency.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenTwoActiveDefaultPaymentAccountsExistForSameOrganizationCurrency_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        await database.SeedCompanyGraphAsync(organizationId);
        var bank = CreateBank();
        database.Context.Banks.Add(bank);
        await database.Context.SaveChangesAsync();

        database.Context.CompanyPaymentAccounts.AddRange(
            CreatePaymentAccount(organizationId, bank.Id, "001", isDefault: true, isActive: true),
            CreatePaymentAccount(organizationId, bank.Id, "002", isDefault: true, isActive: true));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    [Test]
    public async Task SaveChanges_WhenDefaultPaymentAccountsUseDifferentCurrencies_AllowsBoth()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        await database.SeedCompanyGraphAsync(organizationId);
        var bank = CreateBank();
        database.Context.Banks.Add(bank);
        await database.Context.SaveChangesAsync();

        database.Context.CompanyPaymentAccounts.AddRange(
            CreatePaymentAccount(organizationId, bank.Id, "001", "COP", isDefault: true, isActive: true),
            CreatePaymentAccount(organizationId, bank.Id, "002", "USD", isDefault: true, isActive: true));

        await database.Context.SaveChangesAsync();

        var count = await database.Context.CompanyPaymentAccounts.IgnoreQueryFilters().CountAsync();
        count.ShouldBe(2);
    }

    [Test]
    public async Task OrganizationQueryFilter_ForCompanyPaymentAccounts_ReturnsOnlyCurrentOrganizationRows()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var otherOrganizationId = Guid.CreateVersion7();
        await database.SeedCompanyGraphAsync(organizationId);
        await database.SeedCompanyGraphAsync(otherOrganizationId);
        var bank = CreateBank();
        database.Context.Banks.Add(bank);
        await database.Context.SaveChangesAsync();

        database.Context.CompanyPaymentAccounts.AddRange(
            CreatePaymentAccount(organizationId, bank.Id, "001"),
            CreatePaymentAccount(otherOrganizationId, bank.Id, "999"));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        database.OrganizationContext.SetOrganization(organizationId);
        var accounts = await database.Context.CompanyPaymentAccounts
            .Select(account => account.AccountNumber)
            .ToListAsync();

        accounts.ShouldBe(["001"]);
    }

    [Test]
    public async Task SaveChanges_WhenCompanyToolReferencesCredentialFromDifferentCompany_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var otherOrganizationId = Guid.CreateVersion7();
        await database.SeedCompanyGraphAsync(organizationId);
        await database.SeedCompanyGraphAsync(otherOrganizationId);
        var otherCredential = new IntegrationCredentialReference
        {
            OrganizationId = otherOrganizationId,
            Provider = IntegrationProvider.GoogleCalendar,
            Purpose = "calendar",
            Reference = "kv://other-company/google-calendar",
        };
        database.Context.IntegrationCredentialReferences.Add(otherCredential);
        await database.Context.SaveChangesAsync();

        database.Context.CompanyTools.Add(new CompanyTool
        {
            OrganizationId = organizationId,
            ToolKey = "cross_company_tool",
            CredentialReferenceId = otherCredential.Id,
        });

        var exception = await Should.ThrowAsync<InvalidOperationException>(database.Context.SaveChangesAsync());
        exception.Message.ShouldContain("cross-organization relationship");
    }

    [Test]
    public async Task SaveChanges_WhenConversationReferencesCustomerFromDifferentCompany_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var otherOrganizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);
        var otherSeed = await database.SeedCompanyGraphAsync(otherOrganizationId);

        database.Context.Conversations.Add(CreateConversation(
            organizationId,
            otherSeed.CustomerId,
            seed.ChannelId,
            seed.AgentProfileId));

        var exception = await Should.ThrowAsync<InvalidOperationException>(database.Context.SaveChangesAsync());
        exception.Message.ShouldContain("cross-organization relationship");
    }

    [Test]
    public async Task SaveChanges_WhenMessageReferencesConversationFromDifferentCompany_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var otherOrganizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);
        var otherSeed = await database.SeedCompanyGraphAsync(otherOrganizationId);
        var otherConversation = CreateConversation(otherOrganizationId, otherSeed.CustomerId, otherSeed.ChannelId, otherSeed.AgentProfileId);
        database.Context.Conversations.Add(otherConversation);
        await database.Context.SaveChangesAsync();

        database.Context.Messages.Add(CreateMessage(organizationId, otherConversation.Id, "wamid.cross-company"));

        var exception = await Should.ThrowAsync<InvalidOperationException>(database.Context.SaveChangesAsync());
        exception.Message.ShouldContain("cross-organization relationship");
    }

    /// <summary>
    /// Verifies that a conversation cannot change its agent profile after creation.
    /// </summary>
    [Test]
    public async Task SaveChanges_WhenConversationAgentProfileIdChangesAfterCreation_Throws()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(organizationId);
        var conversation = CreateConversation(organizationId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        database.Context.Conversations.Add(conversation);
        await database.Context.SaveChangesAsync();

        conversation.AgentProfileId = Guid.CreateVersion7();

        var exception = Should.Throw<InvalidOperationException>(() => database.Context.SaveChangesAsync());
        exception.Message.ShouldContain("Conversation.AgentProfileId is immutable");
    }

    private static Conversation CreateConversation(
        Guid organizationId,
        Guid customerId,
        Guid channelId,
        Guid agentProfileId,
        ConversationStatus status = ConversationStatus.Open)
    {
        return new Conversation
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            CompanyChannelId = channelId,
            AgentProfileId = agentProfileId,
            Status = status,
            LastMessageAt = TimeProvider.System.GetUtcNow().UtcDateTime
        };
    }

    private static Message CreateMessage(
        Guid organizationId,
        Guid conversationId,
        string? providerMessageId,
        MessageRole role = MessageRole.User)
    {
        return new Message
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            Role = role,
            MessageText = "hello",
            ProviderMessageId = providerMessageId,
            OccurredAt = TimeProvider.System.GetUtcNow().UtcDateTime
        };
    }

    private static Customer CreateCustomer(Guid organizationId, Guid channelId, string externalCustomerId)
    {
        return new Customer
        {
            OrganizationId = organizationId,
            CompanyChannelId = channelId,
            ExternalCustomerId = externalCustomerId
        };
    }

    private static ToolExecution CreateToolExecution(
        Guid organizationId,
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        string idempotencyKey)
    {
        return new ToolExecution
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            CompanyToolId = companyToolId,
            TriggerMessageId = triggerMessageId,
            ToolKey = "request_human_handoff",
            IdempotencyKey = idempotencyKey,
            Status = ToolExecutionStatus.Pending
        };
    }

    private static Bank CreateBank()
    {
        return new Bank
        {
            Name = "Banco Uno",
            CountryCode = "CO",
            IsActive = true,
        };
    }

    private static CompanyPaymentAccount CreatePaymentAccount(
        Guid organizationId,
        Guid bankId,
        string accountNumber,
        string currency = "COP",
        bool isDefault = false,
        bool isActive = true)
    {
        var account = new CompanyPaymentAccount
        {
            OrganizationId = organizationId,
            BankId = bankId,
            AccountNumber = accountNumber,
            AccountType = PaymentAccountType.Ahorros,
            AccountHolderName = "Contoso Bistro",
            Currency = currency,
            ReservationPaymentAmount = 50000m,
            QrBlobContainer = string.Empty,
            QrBlobName = string.Empty,
            IsDefault = isDefault,
            IsActive = isActive,
        };
        var qrReference = BlobStorageNaming.ForPaymentQr("qr.png", account.Id);
        account.QrBlobContainer = qrReference.ContainerName;
        account.QrBlobName = qrReference.BlobName;
        return account;
    }

}
