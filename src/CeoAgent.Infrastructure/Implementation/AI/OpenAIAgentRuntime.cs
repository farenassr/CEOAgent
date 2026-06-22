using CeoAgent.Application.Abstractions.OpenAI;
using CeoAgent.Infrastructure.Implementation.OpenAI;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace CeoAgent.Infrastructure.Implementation.AI;

#pragma warning disable OPENAI001

internal sealed class OpenAIAgentRuntime(
    CeoAgentDbContext dbContext,
    IOpenAIResponsesClientFactory<ResponsesClient> clientFactory,
    AgentFunctionCatalog functionCatalog,
    AgentFunctionInvocationGuard invocationGuard,
    AgentTurnContextAccessor turnContextAccessor,
    TimeProvider timeProvider,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory,
    IOptions<AgentRuntimeOptions> options,
    IOptions<OpenAIAgentRuntimeOptions> openAiOptions) : IAgentRuntimeProvider
{
    private readonly AgentRuntimeOptions runtimeOptions = options.Value;
    private readonly OpenAIAgentRuntimeOptions openAiRuntimeOptions = openAiOptions.Value;

    public LlmProvider Provider => LlmProvider.OpenAI;

    public bool CanEstimateCost(string modelName)
    {
        return openAiRuntimeOptions.TryGetPricing(modelName, out _);
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

        var client = await clientFactory.GetClientAsync(cancellationToken);
        var tools = await functionCatalog.GetEnabledFunctionsAsync(
            request.OrganizationId,
            cancellationToken);
        var agent = client.AsAIAgent(
            model: request.ModelName,
            instructions: request.SystemPrompt,
            name: "ceoagent",
            description: "CeoAgent restaurant customer assistant.",
            tools: tools,
            clientFactory: innerClient => new FunctionInvokingChatClient(innerClient, loggerFactory, serviceProvider)
            {
                MaximumIterationsPerRequest = runtimeOptions.MaximumToolIterationsPerRequest,
                AllowConcurrentInvocation = runtimeOptions.AllowConcurrentToolInvocation,
                FunctionInvoker = invocationGuard.InvokeAsync,
                TerminateOnUnknownCalls = true,
            },
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
                new ChatClientAgentRunOptions(new ChatOptions
                {
                    AllowMultipleToolCalls = runtimeOptions.AllowMultipleToolCalls,
                    MaxOutputTokens = request.MaxOutputTokenCount,
                }),
                cancellationToken);
        }
        finally
        {
            turnContextAccessor.Clear();
        }

        var nativeResponse = response.AsOpenAIResponse();
        var assistantText = nativeResponse.GetOutputText();
        var providerConversationId = session.ConversationId;
        var sessionJson = (await agent.SerializeSessionAsync(
            session,
            AgentRuntimeSessionState.SessionJsonOptions,
            cancellationToken)).GetRawText();
        AgentRuntimeSessionState.ApplyConversationSession(
            conversation,
            providerConversationId,
            nativeResponse.Id,
            sessionJson,
            resetReason,
            now,
            runtimeOptions);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AgentTurnResult(
            assistantText,
            nativeResponse.Id,
            providerConversationId,
            nativeResponse.Status?.ToString(),
            nativeResponse.Usage?.InputTokenCount,
            nativeResponse.Usage?.OutputTokenCount,
            nativeResponse.Usage?.TotalTokenCount,
            EstimatedCostUsd(nativeResponse.Usage, request.ModelName, openAiRuntimeOptions),
            turnContext.ToolInvocationCount,
            resetReason is not null,
            resetReason);
    }

    private static double? EstimatedCostUsd(
        ResponseTokenUsage? usage,
        string modelName,
        OpenAIAgentRuntimeOptions options)
    {
        if (usage is null || !options.TryGetPricing(modelName, out var pricing))
        {
            return null;
        }

        return (usage.InputTokenCount / 1_000_000d * pricing.InputTokenCostPerMillion)
            + (usage.OutputTokenCount / 1_000_000d * pricing.OutputTokenCostPerMillion);
    }
}

#pragma warning restore OPENAI001
