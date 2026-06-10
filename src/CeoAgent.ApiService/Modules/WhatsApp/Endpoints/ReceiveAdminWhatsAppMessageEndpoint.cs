using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Request.WhatsApp;
using CeoAgent.Shared.Response.WhatsApp;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.WhatsApp;

/// <summary>
/// Persists an inbound WhatsApp text message supplied by an admin caller and enqueues the normal Worker flow.
/// </summary>
public sealed class ReceiveAdminWhatsAppMessageEndpoint(
    CeoAgentDbContext dbContext,
    IAdminTenantGuard tenantGuard,
    IIncomingMessageJobEnqueuer incomingMessageJobEnqueuer,
    TimeProvider timeProvider) : Endpoint<ReceiveWhatsAppMessageRequest, ReceiveWhatsAppMessageResponse>
{
    private const string ProviderType = "whatsapp_cloud";

    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/whatsapp");
    }

    public override async Task HandleAsync(ReceiveWhatsAppMessageRequest request, CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        await tenantGuard.GetAccessibleCompanyAsync(companyId, trackChanges: false, cancellationToken);

        var channel = await dbContext.CompanyChannels
            .WithDefaultTracking(trackChanges: true)
            .OrderBy(entity => entity.CreatedAt)
            .FirstOrDefaultAsync(
                entity => entity.CompanyId == companyId
                    && entity.Provider == CompanyChannelProvider.WhatsAppCloud,
                cancellationToken)
            ?? throw new BusinessRuleException("company_channel_required", "Company requires a WhatsApp channel before receiving WhatsApp messages.");

        var customer = await ResolveCustomerAsync(companyId, channel.Id, request.ExternalCustomerId, cancellationToken);
        var conversation = await ResolveConversationAsync(companyId, channel.Id, customer.Id, cancellationToken);
        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        var inbound = new Message
        {
            CompanyId = companyId,
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Type = MessageType.Text,
            MessageText = request.MessageText,
            ProviderMessageId = null,
            Payload = new MessagePayload
            {
                ProviderType = ProviderType,
            },
            OccurredAt = occurredAt,
        };

        conversation.LastMessageAt = occurredAt;
        dbContext.Messages.Add(inbound);
        await dbContext.SaveChangesAsync(cancellationToken);

        var job = new ProcessIncomingMessageJob(companyId, conversation.Id, inbound.Id, HttpContext.TraceIdentifier);
        await incomingMessageJobEnqueuer.EnqueueAsync(job, cancellationToken);

        await Send.OkAsync(
            new ReceiveWhatsAppMessageResponse
            {
                CompanyId = companyId,
                ConversationId = conversation.Id,
                MessageId = inbound.Id,
                Enqueued = true,
            },
            cancellationToken);
    }

    private async Task<Customer> ResolveCustomerAsync(
        Guid companyId,
        Guid channelId,
        string externalCustomerId,
        CancellationToken cancellationToken)
    {
        var normalizedExternalCustomerId = externalCustomerId.Trim();
        var customer = await dbContext.Customers
            .WithDefaultTracking(trackChanges: true)
            .SingleOrDefaultAsync(
                entity => entity.CompanyId == companyId
                    && entity.CompanyChannelId == channelId
                    && entity.ExternalCustomerId == normalizedExternalCustomerId,
                cancellationToken);

        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer
        {
            CompanyId = companyId,
            CompanyChannelId = channelId,
            ExternalCustomerId = normalizedExternalCustomerId,
            DisplayName = normalizedExternalCustomerId,
        };
        dbContext.Customers.Add(customer);
        return customer;
    }

    private async Task<Conversation> ResolveConversationAsync(
        Guid companyId,
        Guid channelId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .WithDefaultTracking(trackChanges: true)
            .SingleOrDefaultAsync(
                entity => entity.CompanyId == companyId
                    && entity.CustomerId == customerId
                    && entity.CompanyChannelId == channelId
                    && entity.Status == ConversationStatus.Open,
                cancellationToken);

        if (conversation is not null)
        {
            return conversation;
        }

        var agentProfileId = await dbContext.AgentProfiles
            .WithDefaultTracking()
            .Where(entity => entity.CompanyId == companyId)
            .Select(entity => entity.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (agentProfileId == Guid.Empty)
        {
            throw new BusinessRuleException("agent_profile_required", "Company requires an agent profile before receiving WhatsApp messages.");
        }

        conversation = new Conversation
        {
            CompanyId = companyId,
            CustomerId = customerId,
            CompanyChannelId = channelId,
            AgentProfileId = agentProfileId,
            LastMessageAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.Conversations.Add(conversation);
        return conversation;
    }
}

public sealed class ReceiveWhatsAppMessageValidator : Validator<ReceiveWhatsAppMessageRequest>
{
    public ReceiveWhatsAppMessageValidator()
    {
        RuleFor(request => request.MessageText).NotEmpty().MaximumLength(4000);
        RuleFor(request => request.ExternalCustomerId).NotEmpty().MaximumLength(160);
    }
}
