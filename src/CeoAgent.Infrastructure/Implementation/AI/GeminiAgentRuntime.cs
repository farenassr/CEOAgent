using CeoAgent.Infrastructure.Implementation.Gemini;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal sealed class GeminiAgentRuntime(
    CeoAgentDbContext dbContext,
    IGeminiChatClientFactory clientFactory,
    AgentFunctionCatalog functionCatalog,
    AgentFunctionInvocationGuard invocationGuard,
    AgentTurnContextAccessor turnContextAccessor,
    TimeProvider timeProvider,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory,
    IOptions<AgentRuntimeOptions> options) : IAgentRuntimeProvider
{
    private readonly AgentRuntimeOptions runtimeOptions = options.Value;

    public LlmProvider Provider => LlmProvider.Gemini;

    public bool CanEstimateCost(string modelName)
    {
        return false;
    }

    public async Task<AgentTurnResult> RunTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Provider != Provider)
        {
            throw new NotSupportedException($"LLM provider '{request.Provider}' is not supported.");
        }

        if (!IsSupportedModel(request.ModelName))
        {
            throw new NotSupportedException($"Gemini model '{request.ModelName}' is not supported. Use a model name that starts with 'gemini-'.");
        }

        var conversation = await dbContext.Conversations
            .ForOrganization(request.OrganizationId)
            .SingleAsync(entity => entity.Id == request.ConversationId, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var resetReason = AgentRuntimeSessionState.ResolveSessionResetReason(
            conversation,
            request,
            now,
            runtimeOptions);
        AgentRuntimeSessionState.ApplyConversationSnapshot(conversation, request);

        var chatClient = await clientFactory.GetClientAsync(request.ModelName, cancellationToken);
        var tools = await functionCatalog.GetEnabledFunctionsAsync(
            request.OrganizationId,
            cancellationToken);
        var guardedClient = new FunctionInvokingChatClient(chatClient, loggerFactory, serviceProvider)
        {
            MaximumIterationsPerRequest = runtimeOptions.MaximumToolIterationsPerRequest,
            AllowConcurrentInvocation = runtimeOptions.AllowConcurrentToolInvocation,
            FunctionInvoker = invocationGuard.InvokeAsync,
            TerminateOnUnknownCalls = true,
        };
        var agent = new ChatClientAgent(
            guardedClient,
            instructions: request.SystemPrompt,
            name: "ceoagent",
            description: "CeoAgent restaurant customer assistant.",
            tools: tools,
            loggerFactory: loggerFactory,
            services: serviceProvider);

        var session = await AgentRuntimeSessionState.CreateSessionAsync(
            agent,
            conversation,
            resetReason,
            cancellationToken);
        var turnContext = new AgentTurnContext
        {
            OrganizationId = request.OrganizationId,
            ConversationId = request.ConversationId,
            InboundMessageId = request.InboundMessageId,
            Provider = request.Provider,
            ModelName = request.ModelName,
            CorrelationId = request.CorrelationId,
            MutatingToolsEnabled = request.MutatingToolsEnabled,
            MutatingToolsDisabledReason = request.MutatingToolsDisabledReason,
        };

        AgentResponse response;
        turnContextAccessor.Set(turnContext);
        try
        {
            response = await agent.RunAsync(
                request.UserMessage,
                session,
                new ChatClientAgentRunOptions(CreateChatOptions(request, runtimeOptions)),
                cancellationToken);
        }
        finally
        {
            turnContextAccessor.Clear();
        }

        var providerConversationId = session.ConversationId;
        var sessionJson = await AgentRuntimeSessionState.SerializeSessionAsync(
            agent,
            session,
            cancellationToken);
        AgentRuntimeSessionState.ApplyConversationSession(
            conversation,
            providerConversationId,
            response.ResponseId,
            sessionJson,
            resetReason,
            now,
            runtimeOptions);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AgentTurnResult(
            response.Text,
            response.ResponseId,
            providerConversationId,
            response.FinishReason?.ToString(),
            ToNullableInt(response.Usage?.InputTokenCount),
            ToNullableInt(response.Usage?.OutputTokenCount),
            ToNullableInt(response.Usage?.TotalTokenCount),
            null,
            turnContext.ToolInvocationCount,
            resetReason is not null,
            resetReason);
    }

    internal static ChatOptions CreateChatOptions(
        AgentTurnRequest request,
        AgentRuntimeOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        return new ChatOptions
        {
            AllowMultipleToolCalls = runtimeOptions.AllowMultipleToolCalls,
            MaxOutputTokens = request.MaxOutputTokenCount,
            ModelId = request.ModelName,
        };
    }

    private static bool IsSupportedModel(string modelName)
    {
        return modelName.StartsWith("gemini-", StringComparison.Ordinal);
    }

    private static int? ToNullableInt(long? value)
    {
        return value is null
            ? null
            : checked((int)value.Value);
    }
}
