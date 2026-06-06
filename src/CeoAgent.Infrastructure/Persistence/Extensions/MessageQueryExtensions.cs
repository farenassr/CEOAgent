using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class MessageQueryExtensions
{
    public static IQueryable<Message> ForConversation(
        this IQueryable<Message> query,
        Guid companyId,
        Guid conversationId)
    {
        return query
            .ForCompany(companyId)
            .Where(entity => entity.ConversationId == conversationId);
    }

    public static IQueryable<Message> AssistantReplyForClientMessageId(
        this IQueryable<Message> query,
        Guid companyId,
        Guid conversationId,
        string replyClientMessageId)
    {
        return query
            .IgnoreQueryFilters()
            .ForConversation(companyId, conversationId)
            .Where(entity => entity.Role == MessageRole.Assistant
                && entity.ProviderMessageId == replyClientMessageId);
    }

    public static IQueryable<Message> AgentEligibleHistory(
        this IQueryable<Message> query,
        IQueryable<ToolExecution> toolExecutions,
        Guid companyId,
        Guid conversationId,
        int take)
    {
        return query
            .ForConversation(companyId, conversationId)
            .Where(entity =>
                entity.Role == MessageRole.User
                || (entity.Role == MessageRole.Assistant
                    && !toolExecutions
                        .Any(execution => execution.CompanyId == companyId
                            && execution.TriggerMessageId == entity.Id)))
            .OrderByDescending(entity => entity.OccurredAt)
            .ThenByDescending(entity => entity.Id)
            .Take(take);
    }

    public static async Task<Message?> FindTrackedOrPersistedMessageAsync(
        this CeoAgentDbContext dbContext,
        Guid companyId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var tracked = dbContext.ChangeTracker
            .Entries<Message>()
            .Where(entry => entry.State != EntityState.Deleted)
            .Select(entry => entry.Entity)
            .SingleOrDefault(entity => entity.CompanyId == companyId && entity.Id == messageId);

        if (tracked is not null)
        {
            return tracked;
        }

        return await dbContext.Messages
            .ForCompany(companyId)
            .Where(entity => entity.Id == messageId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
