using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Application.Errors;
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
    ICompanyContext companyContext) : EndpointWithoutRequest<HandedOffConversationsResponse>
{
    public override void Configure()
    {
        Get("/v1/admin/companies/{companyId}/conversations/handed-off");
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        EnsureCompanyAccess(companyId);

        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .ForCompany(companyId)
            .Where(entity => entity.Status == ConversationStatus.HandedOff)
            .OrderByDescending(entity => entity.LastMessageAt)
            .Select(entity => new
            {
                entity.Id,
                entity.CustomerId,
                entity.CompanyChannelId,
                entity.LastMessageAt,
            })
            .ToListAsync(cancellationToken);

        var conversationIds = conversations.Select(conversation => conversation.Id).ToList();

        var executions = await dbContext.ToolExecutions
            .AsNoTracking()
            .ForCompany(companyId)
            .Where(entity => entity.ToolKey == MvpToolKeys.RequestHumanHandoff
                && conversationIds.Contains(entity.ConversationId))
            .OrderByDescending(entity => entity.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestByConversation = executions
            .GroupBy(execution => execution.ConversationId)
            .ToDictionary(group => group.Key, group => group.First());

        var items = conversations
            .ConvertAll(conversation =>
            {
                latestByConversation.TryGetValue(conversation.Id, out var execution);
                var result = execution?.Result?.RequestHumanHandoff;
                var request = execution?.Request?.RequestHumanHandoff;

                return new HandedOffConversationResponse
                {
                    ConversationId = conversation.Id,
                    CustomerId = conversation.CustomerId,
                    CompanyChannelId = conversation.CompanyChannelId,
                    LastMessageAt = conversation.LastMessageAt,
                    HandoffTicketId = result?.HandoffTicketId,
                    Reason = request?.Reason,
                    EstimatedPickupAt = result?.EstimatedPickupAt,
                    RequestedAt = execution?.CreatedAt,
                };
            });

        await Send.OkAsync(
            new HandedOffConversationsResponse
            {
                Conversations = items,
                Count = items.Count,
            },
            cancellationToken);
    }

    private void EnsureCompanyAccess(Guid companyId)
    {
        if (companyContext.CompanyId != companyId)
        {
            throw new NotFoundException("company", companyId);
        }
    }
}
