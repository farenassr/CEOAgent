using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Application.Errors;
using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.Infrastructure;
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
public sealed class SendWhatsAppMessageEndpoint(
    CeoAgentDbContext dbContext,
    IOrganizationContextProvider companyContext,
    IMessageChannelIntegration messaging) : Endpoint<SendWhatsAppMessageRequest, SendWhatsAppMessageResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{organizationId}/channels/{companyChannelId}/whatsapp/messages");
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
        var organizationId = Route<Guid>("organizationId");
        var companyChannelId = Route<Guid>("companyChannelId");

        if (companyContext.OrganizationId != organizationId)
        {
            throw new NotFoundException("company", organizationId);
        }

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

        var sent = await messaging.SendTextAsync(
            new ChannelTextMessage(
                organizationId,
                companyChannelId,
                Guid.Empty,
                Guid.CreateVersion7(),
                request.RecipientExternalId,
                request.Text,
                string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? $"manual-whatsapp:{Guid.CreateVersion7()}"
                    : request.IdempotencyKey),
            cancellationToken);

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
        RuleFor(request => request.RecipientExternalId)
            .NotEmpty()
            .MaximumLength(160);
        RuleFor(request => request.Text)
            .NotEmpty()
            .MaximumLength(4096);
        RuleFor(request => request.IdempotencyKey)
            .MaximumLength(200);
    }
}
