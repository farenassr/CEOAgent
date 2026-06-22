using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CeoAgent.Infrastructure.Implementation.AI;

internal sealed class OllamaAgentRuntime(
    CeoAgentDbContext dbContext,
    IChatClient chatClient,
    AgentFunctionCatalog functionCatalog,
    AgentFunctionInvocationGuard invocationGuard,
    AgentTurnContextAccessor turnContextAccessor,
    TimeProvider timeProvider,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory,
    IOptions<AgentRuntimeOptions> options) : IAgentRuntimeProvider
{
    internal const string ConnectionName = "ollama-gemma-4-e2b-it-q4-k-m";
    internal const string LocalModelName = "hf.co/unsloth/gemma-4-E2B-it-GGUF:Q4_K_M";

    private readonly AgentRuntimeOptions runtimeOptions = options.Value;

    public LlmProvider Provider => LlmProvider.Ollama;

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

        if (!string.Equals(request.ModelName, LocalModelName, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Ollama model '{request.ModelName}' is not supported. Use '{LocalModelName}'.");
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

        var tools = await functionCatalog.GetEnabledFunctionsAsync(
            request.OrganizationId,
            cancellationToken,
            OllamaToolJsonSchema.Normalize);
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

    private static int? ToNullableInt(long? value)
    {
        return value is null
            ? null
            : checked((int)value.Value);
    }

    internal static ChatOptions CreateChatOptions(
        AgentTurnRequest request,
        AgentRuntimeOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        var chatOptions = new ChatOptions
        {
            AllowMultipleToolCalls = runtimeOptions.AllowMultipleToolCalls,
            MaxOutputTokens = request.MaxOutputTokenCount,
            ModelId = request.ModelName,
        };
        chatOptions.AdditionalProperties ??= [];
        chatOptions.AdditionalProperties["think"] = false;

        return chatOptions;
    }
}
