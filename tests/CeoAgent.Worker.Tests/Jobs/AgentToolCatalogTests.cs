using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class AgentToolCatalogTests
{
    [Test]
    public void Constructor_WhenStaticToolKeysAreDuplicated_ThrowsClearException()
    {
        var duplicateTools = new IAgentTool[]
        {
            new FakeAgentTool(MvpToolKeys.CheckGoogleCalendarAvailability, isMutating: false),
            new FakeAgentTool(MvpToolKeys.CheckGoogleCalendarAvailability, isMutating: true),
        };

        var exception = Should.Throw<InvalidOperationException>(() =>
            new CompositeAgentToolCatalog(duplicateTools, []));

        exception.Message.ShouldContain(MvpToolKeys.CheckGoogleCalendarAvailability);
    }

    [Test]
    public async Task GetToolsAsync_WhenDynamicToolCollidesWithStaticTool_ThrowsClearException()
    {
        var catalog = new CompositeAgentToolCatalog(
            [new FakeAgentTool(MvpToolKeys.RequestHumanHandoff, isMutating: true)],
            [new StaticDynamicAgentToolProvider([new FakeAgentTool(MvpToolKeys.RequestHumanHandoff, isMutating: false)])]);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await catalog.GetToolsAsync(
                new AgentToolCatalogContext(Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30")),
                CancellationToken.None));

        exception.Message.ShouldContain(MvpToolKeys.RequestHumanHandoff);
    }

    [Test]
    public async Task GetToolsAsync_WithNoOpDynamicProvider_ReturnsStaticTools()
    {
        var staticTool = new FakeAgentTool(MvpToolKeys.FindGoogleCalendarReservations, isMutating: false);
        var catalog = new CompositeAgentToolCatalog([staticTool], [new NoOpDynamicAgentToolProvider()]);

        var tools = await catalog.GetToolsAsync(
            new AgentToolCatalogContext(Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30")),
            CancellationToken.None);

        tools.ShouldBe([staticTool]);
    }

    private sealed class StaticDynamicAgentToolProvider(IReadOnlyList<IAgentTool> tools) : IDynamicAgentToolProvider
    {
        public Task<IReadOnlyList<IAgentTool>> GetToolsAsync(
            AgentToolCatalogContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(tools);
        }
    }

    private sealed class FakeAgentTool(
        string toolKey,
        bool isMutating) : AgentTool<FakeRequest>
    {
        public override string ToolKey => toolKey;

        public override bool IsMutating => isMutating;

        public override string Description => $"Description for {toolKey}.";

        protected override Task<ToolExecution> ExecuteToolAsync(
            ToolExecutionContext context,
            FakeRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ToolExecution
            {
                OrganizationId = context.OrganizationId,
                ConversationId = context.ConversationId,
                CompanyToolId = context.CompanyToolId,
                TriggerMessageId = context.TriggerMessageId,
                ToolKey = ToolKey,
                IdempotencyKey = context.IdempotencyKey,
                Status = ToolExecutionStatus.Succeeded,
            });
        }
    }

    private sealed class FakeRequest;
}
