using CEOAgent.Application.Company;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Infrastructure.Persistence.Entities;
using CEOAgent.Infrastructure.Persistence.Entities.Json;
using CEOAgent.Shared.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Integration.Tests.Persistence;

public sealed class RelationalConstraintTests
{
    [Test]
    public async Task SaveChanges_WhenTwoOpenConversationsExistForSameCompanyCustomerAndChannel_Throws()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);

        database.Context.Conversations.AddRange(
            CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId),
            CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    [Test]
    public async Task SaveChanges_WhenTwoClosedConversationsExistForSameCompanyCustomerAndChannel_AllowsBoth()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);

        database.Context.Conversations.AddRange(
            CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId, ConversationStatus.Closed),
            CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId, ConversationStatus.Closed));

        await database.Context.SaveChangesAsync();

        var count = await database.Context.Conversations.IgnoreQueryFilters().CountAsync();
        count.ShouldBe(2);
    }

    [Test]
    public async Task SaveChanges_WhenDuplicateProviderMessageIdExistsInCompany_Throws()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);
        var conversation = CreateConversation(companyId, seed.CustomerId, seed.ChannelId, seed.AgentProfileId);
        database.Context.Conversations.Add(conversation);

        database.Context.Messages.AddRange(
            CreateMessage(companyId, conversation.Id, "wamid.duplicate"),
            CreateMessage(companyId, conversation.Id, "wamid.duplicate"));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    [Test]
    public async Task SaveChanges_WhenProviderMessageIdIsNull_AllowsMultipleMessages()
    {
        await using var database = await SqliteDatabase.CreateAsync();
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

    [Test]
    public async Task SaveChanges_WhenDuplicateCustomerExistsForCompanyChannel_Throws()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        var seed = await database.SeedCompanyGraphAsync(companyId);

        database.Context.Customers.AddRange(
            CreateCustomer(companyId, seed.ChannelId, "573001112233"),
            CreateCustomer(companyId, seed.ChannelId, "573001112233"));

        await Should.ThrowAsync<DbUpdateException>(database.Context.SaveChangesAsync());
    }

    [Test]
    public async Task SaveChanges_WhenDuplicateConversationStateExists_Throws()
    {
        await using var database = await SqliteDatabase.CreateAsync();
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

    [Test]
    public async Task SaveChanges_WhenDuplicateToolExecutionIdempotencyKeyExistsForCompany_Throws()
    {
        await using var database = await SqliteDatabase.CreateAsync();
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
    public async Task SaveChanges_WhenConversationAgentProfileIdChangesAfterCreation_Throws()
    {
        await using var database = await SqliteDatabase.CreateAsync();
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
            LastMessageAt = DateTime.UtcNow
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
            Text = "hello",
            ProviderMessageId = providerMessageId,
            OccurredAt = DateTime.UtcNow
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

    private sealed record SeedIds(Guid ChannelId, Guid AgentProfileId, Guid CustomerId, Guid ToolId);

    private sealed class SqliteDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly CompanyContextAccessor companyContext;

        private SqliteDatabase(SqliteConnection connection, CompanyContextAccessor companyContext, AppDbContext context)
        {
            this.connection = connection;
            this.companyContext = companyContext;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<SqliteDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var companyContext = new CompanyContextAccessor();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .UseSnakeCaseNamingConvention()
                .Options;

            var context = new AppDbContext(options, companyContext, TimeProvider.System);
            await context.Database.EnsureCreatedAsync();

            return new SqliteDatabase(connection, companyContext, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }

        public async Task<SeedIds> SeedCompanyGraphAsync(Guid companyId)
        {
            var channelId = Guid.CreateVersion7();
            var agentProfileId = Guid.CreateVersion7();
            var customerId = Guid.CreateVersion7();
            var toolId = Guid.CreateVersion7();

            Context.Companies.Add(new CEOAgent.Infrastructure.Persistence.Entities.Company
            {
                Id = companyId,
                Name = "Contoso Bistro",
                TimeZoneId = "America/Bogota"
            });
            Context.CompanyChannels.Add(new CompanyChannel
            {
                Id = channelId,
                CompanyId = companyId,
                Provider = "whatsapp_cloud",
                ProviderChannelId = $"channel-{Guid.CreateVersion7()}"
            });
            Context.AgentProfiles.Add(new AgentProfile
            {
                Id = agentProfileId,
                CompanyId = companyId,
                ModelName = "gpt-4.1-mini",
                DisplayName = "Contoso Assistant",
                Language = "es"
            });
            Context.Customers.Add(new Customer
            {
                Id = customerId,
                CompanyId = companyId,
                CompanyChannelId = channelId,
                ExternalCustomerId = "573001112233"
            });
            Context.CompanyTools.Add(new CompanyTool
            {
                Id = toolId,
                CompanyId = companyId,
                ToolKey = "request_human_handoff"
            });

            await Context.SaveChangesAsync();
            companyContext.SetCompany(companyId);
            return new SeedIds(channelId, agentProfileId, customerId, toolId);
        }
    }
}
