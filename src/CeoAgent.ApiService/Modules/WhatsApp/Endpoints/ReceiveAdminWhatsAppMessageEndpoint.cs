using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.ApiService.Infrastructure.OpenApi;
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
        Post("/v1/admin/whatsapp");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.WhatsApp)
            .WithSummary("Receive Admin WhatsApp Message")
            .WithDescription("Persists an inbound WhatsApp text message submitted by an admin caller and enqueues the normal worker processing flow. Use it for controlled ingestion tests and operator-driven message injection."));
        Summary(summary =>
        {
            summary.Summary = "Receive Admin WhatsApp Message";
            summary.Description = "Persists an inbound WhatsApp text message submitted by an admin caller and enqueues the normal worker processing flow. Use it for controlled ingestion tests and operator-driven message injection.";
        });
    }

    public override async Task HandleAsync(ReceiveWhatsAppMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantGuard.RequireAuthenticatedOrganizationId();
        await tenantGuard.GetAuthenticatedCompanyAsync(trackChanges: false, cancellationToken);

        var channel = await dbContext.CompanyChannels
            .WithDefaultTracking(trackChanges: true)
            .OrderBy(entity => entity.CreatedAt)
            .FirstOrDefaultAsync(
                entity => entity.OrganizationId == organizationId
                    && entity.Provider == CompanyChannelProvider.WhatsAppCloud,
                cancellationToken)
            ?? throw new BusinessRuleException("company_channel_required", "Company requires a WhatsApp channel before receiving WhatsApp messages.");

        var customer = await ResolveCustomerAsync(organizationId, channel.Id, request.ExternalCustomerId, cancellationToken);
        var conversation = await ResolveConversationAsync(organizationId, channel.Id, customer.Id, cancellationToken);
        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        var inbound = new Message
        {
            OrganizationId = organizationId,
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

        var job = new ProcessIncomingMessageJob(organizationId, conversation.Id, inbound.Id, HttpContext.TraceIdentifier);
        await incomingMessageJobEnqueuer.EnqueueAsync(job, cancellationToken);

        await Send.OkAsync(
            new ReceiveWhatsAppMessageResponse
            {
                OrganizationId = organizationId,
                ConversationId = conversation.Id,
                MessageId = inbound.Id,
                Enqueued = true,
            },
            cancellationToken);
    }

    private async Task<Customer> ResolveCustomerAsync(
        Guid organizationId,
        Guid channelId,
        string externalCustomerId,
        CancellationToken cancellationToken)
    {
        var normalizedExternalCustomerId = externalCustomerId.Trim();
        var customer = await dbContext.Customers
            .WithDefaultTracking(trackChanges: true)
            .SingleOrDefaultAsync(
                entity => entity.OrganizationId == organizationId
                    && entity.CompanyChannelId == channelId
                    && entity.ExternalCustomerId == normalizedExternalCustomerId,
                cancellationToken);

        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer
        {
            OrganizationId = organizationId,
            CompanyChannelId = channelId,
            ExternalCustomerId = normalizedExternalCustomerId,
            DisplayName = normalizedExternalCustomerId,
        };
        dbContext.Customers.Add(customer);
        return customer;
    }

    private async Task<Conversation> ResolveConversationAsync(
        Guid organizationId,
        Guid channelId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .WithDefaultTracking(trackChanges: true)
            .SingleOrDefaultAsync(
                entity => entity.OrganizationId == organizationId
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
            .Where(entity => entity.OrganizationId == organizationId)
            .Select(entity => entity.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (agentProfileId == Guid.Empty)
        {
            throw new BusinessRuleException("agent_profile_required", "Company requires an agent profile before receiving WhatsApp messages.");
        }

        conversation = new Conversation
        {
            OrganizationId = organizationId,
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
