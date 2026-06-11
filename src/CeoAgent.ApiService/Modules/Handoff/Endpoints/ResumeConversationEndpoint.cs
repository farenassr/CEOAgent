using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Response.Handoff;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.Handoff.Endpoints;

/// <summary>
/// Explicit admin resume of a handed-off conversation: returns the bot to control by setting the
/// conversation back to Open and clearing the human handoff state. This is the mandatory resume path.
/// </summary>
public sealed class ResumeConversationEndpoint(
    CeoAgentDbContext dbContext,
    IOrganizationContextProvider companyContext) : EndpointWithoutRequest<ResumeConversationResponse>
{
    private const string HumanRequestedFlag = "human_requested";
    private const string HandoffIntent = "human_handoff_request";

    public override void Configure()
    {
        Post("/v1/admin/companies/{organizationId}/conversations/{conversationId}/resume");
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var organizationId = Route<Guid>("organizationId");
        var conversationId = Route<Guid>("conversationId");
        EnsureCompanyAccess(organizationId);

        var conversation = await dbContext.Conversations
            .WithDefaultTracking(trackChanges: true)
            .ForOrganization(organizationId)
            .FirstOrDefaultAsync(entity => entity.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("conversation", conversationId);

        var resumed = conversation.Status == ConversationStatus.HandedOff;
        if (resumed)
        {
            conversation.Status = ConversationStatus.Open;
            await ClearHandoffStateAsync(organizationId, conversationId, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await Send.OkAsync(
            new ResumeConversationResponse
            {
                ConversationId = conversation.Id,
                Status = conversation.Status,
                Resumed = resumed,
            },
            cancellationToken);
    }

    private async Task ClearHandoffStateAsync(
        Guid organizationId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.ConversationStates
            .WithDefaultTracking(trackChanges: true)
            .ForOrganization(organizationId)
            .FirstOrDefaultAsync(entity => entity.ConversationId == conversationId, cancellationToken);

        if (state is null)
        {
            return;
        }

        var flags = state.Snapshot.ConversationFlags
            .Where(flag => !string.Equals(flag, HumanRequestedFlag, StringComparison.Ordinal))
            .ToList();

        // Reassign the complex property so the JSON column is detected as modified.
        state.Snapshot = new ConversationStateSnapshot
        {
            CurrentIntent = string.Equals(state.Snapshot.CurrentIntent, HandoffIntent, StringComparison.Ordinal)
                ? null
                : state.Snapshot.CurrentIntent,
            PendingAction = state.Snapshot.PendingAction,
            Slots = state.Snapshot.Slots,
            ConversationFlags = flags,
            TurnCount = state.Snapshot.TurnCount,
        };
    }

    private void EnsureCompanyAccess(Guid organizationId)
    {
        if (companyContext.OrganizationId != organizationId)
        {
            throw new NotFoundException("company", organizationId);
        }
    }
}
