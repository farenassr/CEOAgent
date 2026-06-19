using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CeoAgent.Application.Abstractions.AI;
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
using ConversationEntity = CeoAgent.Infrastructure.Entities.Conversation;

namespace CeoAgent.Infrastructure.Implementation.AI;

#pragma warning disable OPENAI001

internal sealed class MicrosoftAgentRuntime(
    CeoAgentDbContext dbContext,
    IOpenAIResponsesClientFactory<ResponsesClient> clientFactory,
    AgentFunctionCatalog functionCatalog,
    AgentFunctionInvocationGuard invocationGuard,
    AgentTurnContextAccessor turnContextAccessor,
    TimeProvider timeProvider,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory,
    IOptions<AgentRuntimeOptions> options,
    IOptions<OpenAIAgentRuntimeOptions> openAiOptions) : IAgentRuntime
{
    private static readonly JsonSerializerOptions SessionJsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };
    private readonly AgentRuntimeOptions runtimeOptions = options.Value;
    private readonly OpenAIAgentRuntimeOptions openAiRuntimeOptions = openAiOptions.Value;

    public bool CanEstimateCost(LlmProvider provider, string modelName)
    {
        return provider == LlmProvider.OpenAI && openAiRuntimeOptions.TryGetPricing(modelName, out _);
    }

    public async Task<AgentTurnResult> RunTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Provider != LlmProvider.OpenAI)
        {
            throw new NotSupportedException($"LLM provider '{request.Provider}' is not supported.");
        }

        var conversation = await dbContext.Conversations
            .ForOrganization(request.OrganizationId)
            .SingleAsync(entity => entity.Id == request.ConversationId, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var resetReason = ResolveSessionResetReason(conversation, request, now);
        ApplyConversationSnapshot(conversation, request);

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

        var session = await CreateSessionAsync(agent, conversation, resetReason, cancellationToken);
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
            SessionJsonOptions,
            cancellationToken)).GetRawText();

        conversation.ProviderConversationId = providerConversationId;
        conversation.ProviderLastResponseId = nativeResponse.Id;
        conversation.AgentSessionJson = sessionJson;
        conversation.AgentSessionStartedAt = resetReason is null && conversation.AgentSessionStartedAt is not null
            ? conversation.AgentSessionStartedAt
            : now;
        conversation.AgentSessionLastUsedAt = now;
        conversation.AgentSessionExpiresAt = now.AddHours(runtimeOptions.SessionIdleExpirationHours);
        conversation.AgentSessionTurnCount = resetReason is null
            ? conversation.AgentSessionTurnCount + 1
            : 1;
        conversation.AgentSessionResetReason = resetReason;

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

    private static async Task<ChatClientAgentSession> CreateSessionAsync(
        ChatClientAgent agent,
        ConversationEntity conversation,
        string? resetReason,
        CancellationToken cancellationToken)
    {
        if (resetReason is not null)
        {
            return (ChatClientAgentSession)await agent.CreateSessionAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(conversation.AgentSessionJson))
        {
            using var document = JsonDocument.Parse(conversation.AgentSessionJson);
            return (ChatClientAgentSession)await agent.DeserializeSessionAsync(
                document.RootElement,
                SessionJsonOptions,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(conversation.ProviderConversationId))
        {
            var session = await agent.CreateSessionAsync(
                conversation.ProviderConversationId,
                cancellationToken);
            return (ChatClientAgentSession)session;
        }

        return (ChatClientAgentSession)await agent.CreateSessionAsync(cancellationToken);
    }

    private string? ResolveSessionResetReason(
        ConversationEntity conversation,
        AgentTurnRequest request,
        DateTime now)
    {
        if (conversation.LlmProvider is not null && conversation.LlmProvider != request.Provider)
        {
            return "provider_changed";
        }

        if (!string.IsNullOrWhiteSpace(conversation.ModelName)
            && !string.Equals(conversation.ModelName, request.ModelName, StringComparison.Ordinal))
        {
            return "model_changed";
        }

        if (conversation.AgentSessionTurnCount <= 0
            || string.IsNullOrWhiteSpace(conversation.AgentSessionJson)
                && string.IsNullOrWhiteSpace(conversation.ProviderConversationId))
        {
            return "new_session";
        }

        if (conversation.AgentSessionExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return "idle_expired";
        }

        if (conversation.AgentSessionLastUsedAt is { } lastUsed
            && lastUsed.AddHours(runtimeOptions.SessionIdleExpirationHours) <= now)
        {
            return "idle_expired";
        }

        if (runtimeOptions.MaxSessionTurns > 0
            && conversation.AgentSessionTurnCount >= runtimeOptions.MaxSessionTurns)
        {
            return "max_turns";
        }

        return null;
    }

    private static void ApplyConversationSnapshot(
        ConversationEntity conversation,
        AgentTurnRequest request)
    {
        conversation.LlmProvider ??= request.Provider;
        conversation.ModelName ??= request.ModelName;
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
