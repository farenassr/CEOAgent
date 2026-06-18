using System.Text.Json;
using CeoAgent.Application.Abstractions.AI;
using CeoAgent.Application.Abstractions.OpenAI;
using CeoAgent.Shared.AI;
using CeoAgent.Shared.Enums;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace CeoAgent.Infrastructure.Implementation.OpenAI;

#pragma warning disable OPENAI001

public sealed class OpenAIAgentRuntime(
    IOpenAIResponsesClientFactory<ResponsesClient> clientFactory,
    IOptions<OpenAIAgentRuntimeOptions> options) : IAgentRuntime
{
    private readonly OpenAIAgentRuntimeOptions runtimeOptions = options.Value;

    public bool CanEstimateCost(LlmProvider provider, string modelName)
    {
        return provider == LlmProvider.OpenAI && runtimeOptions.TryGetPricing(modelName, out _);
    }

    public async Task<AgentRunResult> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Provider != LlmProvider.OpenAI)
        {
            throw new NotSupportedException($"LLM provider '{request.Provider}' is not supported.");
        }

        var client = await clientFactory.GetClientAsync(cancellationToken);
        var response = await client.CreateResponseAsync(
            CreateOptions(request),
            cancellationToken);

        return ToAgentRunResult(response.Value, request.ModelName);
    }

    private static CreateResponseOptions CreateOptions(AgentRunRequest request)
    {
        var options = new CreateResponseOptions
        {
            Model = request.ModelName,
            Instructions = request.SystemPrompt,
            ParallelToolCallsEnabled = false,
            StoredOutputEnabled = false,
        };
        if (request.MaxOutputTokenCount is { } maxOutputTokenCount)
        {
            options.MaxOutputTokenCount = maxOutputTokenCount;
        }

        foreach (var message in request.Messages)
        {
            options.InputItems.Add(ToResponseItem(message));
        }

        foreach (var tool in request.Tools)
        {
            options.Tools.Add(ResponseTool.CreateFunctionTool(
                tool.Name,
                BinaryData.FromString(tool.ParametersSchema.GetRawText()),
                strictModeEnabled: true,
                functionDescription: tool.Description));
        }

        return options;
    }

    private static ResponseItem ToResponseItem(AgentConversationMessage message)
    {
        return message.Role switch
        {
            "user" or "User" => ResponseItem.CreateUserMessageItem(message.Text ?? string.Empty),
            "assistant" or "Assistant" when message.ToolCallId is not null && message.ToolName is not null =>
                ResponseItem.CreateFunctionCallItem(
                    message.ToolCallId,
                    message.ToolName,
                    BinaryData.FromString(message.ToolArguments?.GetRawText() ?? "{}")),
            "assistant" or "Assistant" => ResponseItem.CreateAssistantMessageItem(message.Text ?? string.Empty),
            "tool" when message.ToolCallId is not null => ResponseItem.CreateFunctionCallOutputItem(
                message.ToolCallId,
                message.Text ?? string.Empty),
            _ => ResponseItem.CreateUserMessageItem(message.Text ?? string.Empty),
        };
    }

    private AgentRunResult ToAgentRunResult(ResponseResult response, string modelName)
    {
        var toolCalls = response.OutputItems
            .OfType<FunctionCallResponseItem>()
            .Select(ToToolCall)
            .ToArray();

        return new AgentRunResult(
            response.GetOutputText(),
            toolCalls,
            response.Id,
            response.Status?.ToString(),
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount,
            response.Usage?.TotalTokenCount,
            EstimatedCostUsd(response.Usage, modelName, runtimeOptions));
    }

    private static AgentToolCall ToToolCall(FunctionCallResponseItem item)
    {
        using var document = ParseToolArguments(item.FunctionArguments.ToString());
        return new AgentToolCall(
            item.CallId,
            item.FunctionName,
            document.RootElement.Clone());
    }

    private static JsonDocument ParseToolArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return JsonDocument.Parse("{}");
        }

        try
        {
            return JsonDocument.Parse(arguments);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
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
