using CeoAgent.Application.Errors;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Request.WhatsApp;
using CeoAgent.Shared.Response.WhatsApp;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.WhatsApp;

/// <summary>
/// Sends a manual WhatsApp text message through a registered company channel.
/// </summary>
public sealed partial class SendWhatsAppMessageEndpoint(
    CeoAgentDbContext dbContext,
    IAdminTenantGuard tenantGuard,
    IOutboundMessageDispatcher outboundMessageDispatcher,
    TimeProvider timeProvider,
    ILogger<SendWhatsAppMessageEndpoint> logger) : Endpoint<SendWhatsAppMessageRequest, SendWhatsAppMessageResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/channels/{companyChannelId}/whatsapp/messages");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.WhatsApp)
            .WithSummary("Send WhatsApp Message")
            .WithDescription("Sends a manual WhatsApp text message through a company WhatsApp channel. Use it for admin-initiated replies or controlled outbound messaging."));
        Summary(summary =>
        {
            summary.Summary = "Send WhatsApp Message";
            summary.Description = "Sends a manual WhatsApp text message through a company WhatsApp channel. Use it for admin-initiated replies or controlled outbound messaging.";
        });
    }

    public override async Task HandleAsync(SendWhatsAppMessageRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantGuard.RequireAuthenticatedOrganizationId();
        var companyChannelId = Route<Guid>("companyChannelId");
        await tenantGuard.GetAuthenticatedCompanyAsync(trackChanges: false, cancellationToken);

        var channel = await dbContext.CompanyChannels
            .AsNoTracking()
            .Select(entity => new
            {
                entity.Id,
                entity.OrganizationId,
                entity.Provider,
            })
            .FirstOrDefaultAsync(
                entity => entity.OrganizationId == organizationId && entity.Id == companyChannelId,
                cancellationToken) ?? throw new NotFoundException("company_channel", companyChannelId);

        if (channel.Provider != CompanyChannelProvider.WhatsAppCloud)
        {
            throw new BusinessRuleException(
                "unsupported_channel_provider",
                $"Provider '{channel.Provider}' is not supported for WhatsApp sends.");
        }

        var conversation = await dbContext.Conversations
            .WithDefaultTracking(trackChanges: true)
            .Include(entity => entity.Customer)
            .SingleOrDefaultAsync(
                entity => entity.OrganizationId == organizationId
                    && entity.Id == request.ConversationId
                    && entity.CompanyChannelId == companyChannelId,
                cancellationToken) ?? throw new NotFoundException("conversation", request.ConversationId);
        if (!string.Equals(conversation.Customer.ExternalCustomerId, request.RecipientExternalId, StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "conversation_recipient_mismatch",
                "RecipientExternalId must match the customer for the selected conversation.");
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"manual-whatsapp:{Guid.CreateVersion7()}"
            : request.IdempotencyKey;
        var message = await dbContext.Messages
            .ForConversation(organizationId, conversation.Id)
            .SingleOrDefaultAsync(entity =>
                entity.Role == MessageRole.Assistant
                && entity.ProviderMessageId == idempotencyKey,
                cancellationToken);
        if (message is null)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            message = new Message
            {
                OrganizationId = organizationId,
                ConversationId = conversation.Id,
                Role = MessageRole.Assistant,
                Type = MessageType.Text,
                MessageText = request.Text,
                ProviderMessageId = idempotencyKey,
                Payload = new MessagePayload
                {
                    ProviderType = "text",
                },
                OccurredAt = now,
            };
            dbContext.Messages.Add(message);
            conversation.LastMessageAt = now;
        }

        OutboundMessageDispatchResult sent;
        try
        {
            sent = await outboundMessageDispatcher.SendTextAsync(
                new OutboundTextDispatchRequest(
                    organizationId,
                    companyChannelId,
                    conversation.Id,
                    message.Id,
                    request.RecipientExternalId,
                    request.Text,
                    idempotencyKey),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WhatsAppManualMessageSendFailed(
                logger,
                exception,
                organizationId,
                companyChannelId,
                request.Text.Length);
            throw;
        }

        WhatsAppManualMessageSent(
            logger,
            organizationId,
            companyChannelId,
            sent.ProviderMessageId,
            request.Text.Length);

        await Send.OkAsync(
            new SendWhatsAppMessageResponse
            {
                ProviderMessageId = sent.ProviderMessageId,
            },
            cancellationToken);
    }

}

public sealed class SendWhatsAppMessageValidator : Validator<SendWhatsAppMessageRequest>
{
    public SendWhatsAppMessageValidator()
    {
        RuleFor(request => request.ConversationId)
            .NotEmpty();
        RuleFor(request => request.RecipientExternalId)
            .NotEmpty()
            .MaximumLength(160)
            .Matches("^[0-9]+$")
            .WithMessage("RecipientExternalId must contain only digits.");
        RuleFor(request => request.Text)
            .NotEmpty()
            .MaximumLength(4096);
        RuleFor(request => request.IdempotencyKey)
            .MaximumLength(200);
    }
}
