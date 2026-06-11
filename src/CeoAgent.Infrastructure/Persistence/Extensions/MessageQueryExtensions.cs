using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Persistence.Extensions;

public static class MessageQueryExtensions
{
    public static IQueryable<Message> ForConversation(
        this IQueryable<Message> query,
        Guid organizationId,
        Guid conversationId)
    {
        return query
            .ForOrganization(organizationId)
            .Where(entity => entity.ConversationId == conversationId);
    }

    public static IQueryable<Message> AssistantReplyForClientMessageId(
        this IQueryable<Message> query,
        Guid organizationId,
        Guid conversationId,
        string replyClientMessageId)
    {
        return query
            .IgnoreQueryFilters()
            .ForConversation(organizationId, conversationId)
            .Where(entity => entity.Role == MessageRole.Assistant
                && entity.ProviderMessageId == replyClientMessageId);
    }

    public static IQueryable<Message> AgentEligibleHistory(
        this IQueryable<Message> query,
        IQueryable<ToolExecution> toolExecutions,
        Guid organizationId,
        Guid conversationId,
        int take)
    {
        return query
            .ForConversation(organizationId, conversationId)
            .Where(entity =>
                entity.Role == MessageRole.User
                || (entity.Role == MessageRole.Assistant
                    && !toolExecutions
                        .Any(execution => execution.OrganizationId == organizationId
                            && execution.TriggerMessageId == entity.Id)))
            .OrderByDescending(entity => entity.OccurredAt)
            .ThenByDescending(entity => entity.Id)
            .Take(take);
    }

    public static async Task<Message?> FindTrackedOrPersistedMessageAsync(
        this CeoAgentDbContext dbContext,
        Guid organizationId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var tracked = dbContext.ChangeTracker
            .Entries<Message>()
            .Where(entry => entry.State != EntityState.Deleted)
            .Select(entry => entry.Entity)
            .SingleOrDefault(entity => entity.OrganizationId == organizationId && entity.Id == messageId);

        if (tracked is not null)
        {
            return tracked;
        }

        return await dbContext.Messages
            .ForOrganization(organizationId)
            .Where(entity => entity.Id == messageId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
