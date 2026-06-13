using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Response.Handoff;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.Handoff.Endpoints;

/// <summary>
/// Admin pull view that lists conversations paused for human attention, each annotated with the last
/// request_human_handoff execution. Returns sanitized identifiers and categorical metadata only.
/// </summary>
public sealed class ListHandedOffConversationsEndpoint(
    CeoAgentDbContext dbContext,
    IAdminTenantGuard tenantGuard) : EndpointWithoutRequest<HandedOffConversationsResponse>
{
    public override void Configure()
    {
        Get("/v1/admin/conversations/handed-off");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Conversations)
            .WithSummary("List Handed-Off Conversations")
            .WithDescription("Lists conversations currently paused for human attention with sanitized handoff metadata. Use it for admin review queues without exposing raw customer message content."));
        Summary(summary =>
        {
            summary.Summary = "List Handed-Off Conversations";
            summary.Description = "Lists conversations currently paused for human attention with sanitized handoff metadata. Use it for admin review queues without exposing raw customer message content.";
        });
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var organizationId = tenantGuard.RequireAuthenticatedOrganizationId();
        await tenantGuard.GetAuthenticatedCompanyAsync(trackChanges: false, cancellationToken);

        var items = await dbContext.Conversations
            .AsNoTracking()
            .ForOrganization(organizationId)
            .Where(entity => entity.Status == ConversationStatus.HandedOff)
            .OrderByDescending(entity => entity.LastMessageAt)
            .Select(entity => new
            {
                Conversation = entity,
                LatestHandoff = dbContext.ToolExecutions
                    .AsNoTracking()
                    .ForOrganization(organizationId)
                    .Where(execution => execution.ToolKey == MvpToolKeys.RequestHumanHandoff
                        && execution.ConversationId == entity.Id)
                    .OrderByDescending(execution => execution.CreatedAt)
                    .Select(execution => new
                    {
                        execution.CreatedAt,
                        Request = execution.Request!.RequestHumanHandoff,
                        Result = execution.Result!.RequestHumanHandoff,
                    })
                    .FirstOrDefault(),
            })
            .Select(item => new HandedOffConversationResponse
            {
                ConversationId = item.Conversation.Id,
                CustomerId = item.Conversation.CustomerId,
                CompanyChannelId = item.Conversation.CompanyChannelId,
                LastMessageAt = item.Conversation.LastMessageAt,
                HandoffTicketId = item.LatestHandoff == null ? null : item.LatestHandoff.Result!.HandoffTicketId,
                Reason = item.LatestHandoff == null ? null : item.LatestHandoff.Request!.Reason,
                EstimatedPickupAt = item.LatestHandoff == null ? null : item.LatestHandoff.Result!.EstimatedPickupAt,
                RequestedAt = item.LatestHandoff == null ? null : item.LatestHandoff.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        await Send.OkAsync(
            new HandedOffConversationsResponse
            {
                Conversations = items,
                Count = items.Count,
            },
            cancellationToken);
    }
}
