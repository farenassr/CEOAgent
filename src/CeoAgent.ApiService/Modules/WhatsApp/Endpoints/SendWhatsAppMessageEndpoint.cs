using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Application.Errors;
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
    ICompanyContext companyContext,
    IMessageChannelIntegration messaging) : Endpoint<SendWhatsAppMessageRequest, SendWhatsAppMessageResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/channels/{companyChannelId}/whatsapp/messages");
    }

    public override async Task HandleAsync(SendWhatsAppMessageRequest request, CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        var companyChannelId = Route<Guid>("companyChannelId");

        if (companyContext.CompanyId != companyId)
        {
            throw new NotFoundException("company", companyId);
        }

        var channel = await dbContext.CompanyChannels
            .AsNoTracking()
            .Select(entity => new
            {
                entity.Id,
                entity.CompanyId,
                entity.Provider,
            })
            .FirstOrDefaultAsync(
                entity => entity.CompanyId == companyId && entity.Id == companyChannelId,
                cancellationToken) ?? throw new NotFoundException("company_channel", companyChannelId);

        if (channel.Provider != CompanyChannelProvider.WhatsAppCloud)
        {
            throw new BusinessRuleException(
                "unsupported_channel_provider",
                $"Provider '{channel.Provider}' is not supported for WhatsApp sends.");
        }

        var sent = await messaging.SendTextAsync(
            new ChannelTextMessage(
                companyId,
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
