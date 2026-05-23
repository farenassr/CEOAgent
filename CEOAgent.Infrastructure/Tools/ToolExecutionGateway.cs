using CEOAgent.Application.Errors;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Infrastructure.Persistence.Entities;
using CEOAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CEOAgent.Infrastructure.Tools;

public sealed class ToolExecutionGateway(CEOAgentDbContext dbContext)
{
    public async Task<ToolExecution> CreatePendingExecutionAsync(
        CreateToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var companyTool = await dbContext.CompanyTools
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Id == request.CompanyToolId, cancellationToken);

        if (companyTool is null)
        {
            throw new BusinessRuleException(
                "company_tool_not_found",
                $"Company tool {request.CompanyToolId} was not found.");
        }

        if (companyTool.CompanyId != request.CompanyId)
        {
            throw new BusinessRuleException(
                "company_tool_mismatch",
                "The requested company tool does not belong to the tool execution company.");
        }

        var conversationBelongsToCompany = await dbContext.Conversations
            .AnyAsync(
                entity => entity.Id == request.ConversationId && entity.CompanyId == request.CompanyId,
                cancellationToken);

        if (!conversationBelongsToCompany)
        {
            throw new BusinessRuleException(
                "conversation_mismatch",
                "The requested conversation does not belong to the tool execution company.");
        }

        var triggerMessageBelongsToConversation = await dbContext.Messages
            .AnyAsync(
                entity => entity.Id == request.TriggerMessageId
                    && entity.CompanyId == request.CompanyId
                    && entity.ConversationId == request.ConversationId
                    && entity.Role == MessageRole.Assistant,
                cancellationToken);

        if (!triggerMessageBelongsToConversation)
        {
            throw new BusinessRuleException(
                "trigger_message_mismatch",
                "The trigger message must be an assistant message in the target conversation.");
        }

        var execution = new ToolExecution
        {
            CompanyId = request.CompanyId,
            ConversationId = request.ConversationId,
            CompanyToolId = request.CompanyToolId,
            TriggerMessageId = request.TriggerMessageId,
            ToolKey = request.ToolKey,
            IdempotencyKey = request.IdempotencyKey,
            Request = request.Request,
            Status = ToolExecutionStatus.Pending
        };

        dbContext.ToolExecutions.Add(execution);
        await dbContext.SaveChangesAsync(cancellationToken);

        return execution;
    }
}
