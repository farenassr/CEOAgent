using CeoAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CeoAgent.Infrastructure.Persistence;

public sealed class ConversationAgentProfileImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowIfAgentProfileChanged(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfAgentProfileChanged(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void ThrowIfAgentProfileChanged(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<Conversation>())
        {
            if (entry.State == EntityState.Modified
                && entry.Property(entity => entity.AgentProfileId).IsModified)
            {
                throw new InvalidOperationException("Conversation.AgentProfileId is immutable after conversation creation.");
            }
        }
    }
}
