using System.Text.Json;
using CeoAgent.Integrations.AI;
using CeoAgent.Shared.Enums;
using OpenAI.Responses;

namespace CeoAgent.Adapters.OpenAI;

#pragma warning disable OPENAI001

public sealed class OpenAIAgentRuntime(
    IOpenAIResponsesClientFactory clientFactory) : IAgentRuntime
{
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

        return ToAgentRunResult(response.Value);
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

    private static AgentRunResult ToAgentRunResult(ResponseResult response)
    {
        var toolCalls = response.OutputItems
            .OfType<FunctionCallResponseItem>()
            .Select(ToToolCall)
            .ToArray();

        return new AgentRunResult(
            response.GetOutputText(),
            toolCalls,
            response.Id,
            response.Status?.ToString());
    }

    private static AgentToolCall ToToolCall(FunctionCallResponseItem item)
    {
        using var document = JsonDocument.Parse(item.FunctionArguments.ToString());
        return new AgentToolCall(
            item.CallId,
            item.FunctionName,
            document.RootElement.Clone());
    }
}

#pragma warning restore OPENAI001
