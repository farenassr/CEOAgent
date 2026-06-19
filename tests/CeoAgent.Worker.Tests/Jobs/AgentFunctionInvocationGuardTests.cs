using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AI;
using CeoAgent.Shared.Enums;
using CeoAgent.Worker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class AgentFunctionInvocationGuardTests
{
    private const string ToolKey = "test_mutation";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    [Test]
    public async Task InvokeAsync_WhenToolIsDisabled_PersistsDeniedAuditWithoutExecutingTool()
    {
        await using var fixture = await GuardFixture.CreateAsync(toolEnabled: false);

        var result = await fixture.InvokeAsync("available", CancellationToken.None);

        result.ShouldContain("\"status\":\"denied\"");
        result.ShouldContain("\"failureReason\":\"tool_not_enabled\"");
        fixture.Tool.ExecutionCount.ShouldBe(0);
        var execution = await fixture.DbContext.ToolExecutions.SingleAsync();
        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionDenied);
        execution.FailureReason.ShouldBe("tool_not_enabled");
    }

    [Test]
    public async Task InvokeAsync_WhenCompanyToolRowIsMissing_PersistsDeniedMessageAuditWithoutExecutingTool()
    {
        await using var fixture = await GuardFixture.CreateAsync(
            toolEnabled: true,
            seedCompanyTool: false);

        var result = await fixture.InvokeAsync("available", CancellationToken.None);

        result.ShouldContain("\"status\":\"denied\"");
        result.ShouldContain("\"failureReason\":\"tool_not_enabled\"");
        fixture.Tool.ExecutionCount.ShouldBe(0);
        (await fixture.DbContext.ToolExecutions.CountAsync()).ShouldBe(0);
        (await fixture.DbContext.Messages.CountAsync(message =>
            message.Role == MessageRole.ToolCall
            && message.MessageText == ToolKey)).ShouldBe(1);
        (await fixture.DbContext.Messages.CountAsync(message =>
            message.Role == MessageRole.ToolResult
            && message.MessageText == result)).ShouldBe(1);
    }

    [Test]
    public async Task InvokeAsync_WhenArgumentsAreMalformed_PersistsDeniedAuditWithoutExecutingTool()
    {
        await using var fixture = await GuardFixture.CreateAsync(toolEnabled: true);

        var result = await fixture.InvokeAsync(string.Empty, CancellationToken.None);

        result.ShouldContain("\"status\":\"denied\"");
        result.ShouldContain("\"failureReason\":\"malformed_arguments\"");
        fixture.Tool.ExecutionCount.ShouldBe(0);
        var execution = await fixture.DbContext.ToolExecutions.SingleAsync();
        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionDenied);
        execution.FailureReason.ShouldBe("malformed_arguments");
    }

    [Test]
    public async Task InvokeAsync_WhenMutatingToolsAreDisabled_PersistsDeniedAuditWithoutExecutingTool()
    {
        await using var fixture = await GuardFixture.CreateAsync(
            toolEnabled: true,
            mutatingToolsEnabled: false,
            mutatingToolsDisabledReason: "llm_budget_guard_active");

        var result = await fixture.InvokeAsync("available", CancellationToken.None);

        result.ShouldContain("\"status\":\"denied\"");
        result.ShouldContain("\"failureReason\":\"llm_budget_guard_active\"");
        fixture.Tool.ExecutionCount.ShouldBe(0);
        var execution = await fixture.DbContext.ToolExecutions.SingleAsync();
        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionDenied);
        execution.FailureReason.ShouldBe("llm_budget_guard_active");
    }

    [Test]
    public async Task InvokeAsync_WhenSameInboundAndArgumentsRepeat_ReturnsPersistedResultWithoutDuplicateExecution()
    {
        await using var fixture = await GuardFixture.CreateAsync(toolEnabled: true);

        var first = await fixture.InvokeAsync("available", CancellationToken.None);
        var second = await fixture.InvokeAsync("available", CancellationToken.None);

        second.ShouldBe(first);
        fixture.Tool.ExecutionCount.ShouldBe(1);
        (await fixture.DbContext.ToolExecutions.CountAsync()).ShouldBe(1);
        (await fixture.DbContext.Messages.CountAsync(message => message.Role == MessageRole.ToolCall)).ShouldBe(1);
    }

    private sealed class GuardFixture : IAsyncDisposable
    {
        private readonly PostgresWorkerDatabase database;
        private readonly AgentTurnContextAccessor accessor;
        private readonly AgentFunctionInvocationGuard guard;
        private readonly AIFunction function;

        private GuardFixture(PostgresWorkerDatabase database)
        {
            this.database = database;
            DbContext = database.Context;
            Tool = new TestAgentTool(DbContext);
            accessor = new AgentTurnContextAccessor();
            var dispatcher = new AgentToolDispatcher(
                DbContext,
                new StaticToolCatalog(Tool),
                TimeProvider.System);
            guard = new AgentFunctionInvocationGuard(
                dispatcher,
                accessor,
                NullLogger<AgentFunctionInvocationGuard>.Instance);
            function = new TestAIFunction(ToolKey, Tool.ParametersSchema);
        }

        public CeoAgentDbContext DbContext { get; }

        public TestAgentTool Tool { get; }

        public static async Task<GuardFixture> CreateAsync(
            bool toolEnabled,
            bool seedCompanyTool = true,
            bool mutatingToolsEnabled = true,
            string? mutatingToolsDisabledReason = null)
        {
            var fixture = new GuardFixture(await PostgresWorkerDatabase.CreateAsync());
            var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
            var channelId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31");
            var profileId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32");
            var customerId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33");
            var conversationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34");
            var inboundMessageId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b35");

            fixture.database.OrganizationContext.SetOrganization(organizationId);

            var company = new Company
            {
                Id = organizationId,
                Name = "Contoso Bistro",
                TimeZoneId = "America/Bogota",
            };
            var profile = new AgentProfile
            {
                Id = profileId,
                OrganizationId = organizationId,
                ModelName = "gpt-4.1-mini",
                DisplayName = "Contoso Assistant",
                Language = "es",
            };
            var channel = CompanyChannel.ForWhatsAppCloud(
                organizationId,
                "1152556904604978",
                new WhatsAppCloudMetadata
                {
                    BusinessAccountId = "840790722416204",
                    PhoneNumberId = "1152556904604978",
                    DisplayPhoneNumber = "+15556497030",
                },
                id: channelId);
            var customer = new Customer
            {
                Id = customerId,
                OrganizationId = organizationId,
                CompanyChannelId = channelId,
                ExternalCustomerId = "15551234567",
            };
            var conversation = new Conversation
            {
                Id = conversationId,
                OrganizationId = organizationId,
                CustomerId = customerId,
                CompanyChannelId = channelId,
                AgentProfileId = profileId,
                LastMessageAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };
            var inbound = new Message
            {
                Id = inboundMessageId,
                OrganizationId = organizationId,
                ConversationId = conversationId,
                Role = MessageRole.User,
                Type = MessageType.Text,
                MessageText = "hola",
                OccurredAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };
            var companyTool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"),
                OrganizationId = organizationId,
                ToolKey = ToolKey,
                Description = "Test mutation.",
                ParametersSchema = fixture.Tool.ParametersSchema.Clone(),
                IsEnabled = toolEnabled,
            };

            fixture.DbContext.AddRange(company, profile, channel, customer, conversation, inbound);
            if (seedCompanyTool)
            {
                fixture.DbContext.Add(companyTool);
            }

            await fixture.DbContext.SaveChangesAsync();
            fixture.accessor.Set(new AgentTurnContext
            {
                OrganizationId = organizationId,
                ConversationId = conversationId,
                InboundMessageId = inboundMessageId,
                Provider = LlmProvider.OpenAI,
                ModelName = "gpt-4.1-mini",
                CorrelationId = "guard-test",
                MutatingToolsEnabled = mutatingToolsEnabled,
                MutatingToolsDisabledReason = mutatingToolsDisabledReason,
            });

            return fixture;
        }

        public async Task<string> InvokeAsync(string value, CancellationToken cancellationToken)
        {
            var result = await guard.InvokeAsync(
                new FunctionInvocationContext
                {
                    Function = function,
                    Arguments = new AIFunctionArguments(new Dictionary<string, object?>
                    {
                        ["value"] = value,
                    }),
                },
                cancellationToken);

            return result.ShouldBeOfType<string>();
        }

        public async ValueTask DisposeAsync()
        {
            accessor.Clear();
            await database.DisposeAsync();
        }
    }

    private sealed class TestAgentTool(CeoAgentDbContext dbContext) : IAgentTool
    {
        private static readonly JsonElement Schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                value = new { type = "string" },
            },
            required = new[] { "value" },
            additionalProperties = false,
        }, SerializerOptions);

        public int ExecutionCount { get; private set; }

        public string ToolKey => AgentFunctionInvocationGuardTests.ToolKey;

        public bool IsMutating => true;

        public string Description => "Test mutation.";

        public JsonElement ParametersSchema => Schema.Clone();

        public Type RequestType => typeof(TestToolRequest);

        public bool ValidateObject(object request)
        {
            return request is TestToolRequest typedRequest
                && !string.IsNullOrWhiteSpace(typedRequest.Value);
        }

        public Task<IAgentToolExecution> ExecuteAsync(
            ToolExecutionContext context,
            object request,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            var execution = new ToolExecution
            {
                OrganizationId = context.OrganizationId,
                ConversationId = context.ConversationId,
                CompanyToolId = context.CompanyToolId,
                TriggerMessageId = context.TriggerMessageId,
                ToolKey = ToolKey,
                IdempotencyKey = context.IdempotencyKey,
                Status = ToolExecutionStatus.ToolExecutionSucceeded,
            };
            dbContext.ToolExecutions.Add(execution);
            return Task.FromResult<IAgentToolExecution>(execution);
        }
    }

    private sealed class TestToolRequest
    {
        public string? Value { get; set; }
    }

    private sealed class TestAIFunction(
        string name,
        JsonElement jsonSchema) : AIFunction
    {
        public override string Name { get; } = name;

        public override string Description => "Test mutation.";

        public override JsonElement JsonSchema { get; } = jsonSchema.Clone();

        protected override ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Test functions are invoked through AgentFunctionInvocationGuard.");
        }
    }

    private sealed class StaticToolCatalog(IAgentTool tool) : IAgentToolCatalog
    {
        public Task<IReadOnlyList<IAgentTool>> GetToolsAsync(
            AgentToolCatalogContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<IAgentTool>>([tool]);
        }
    }
}
