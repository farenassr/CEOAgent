using System.Text.Json;
using CeoAgent.ApiService.Infrastructure.Json;
using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Shared.Enums;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.Companies.Commands;

public sealed record RegisterCompanyChannelCommand(
    Guid CompanyId,
    CompanyChannelProvider Provider,
    string ProviderChannelId,
    JsonElement? Metadata,
    Guid? CredentialReferenceId) : ICommand<CompanyChannel>;

public sealed class RegisterCompanyChannelCommandHandler(
    CeoAgentDbContext dbContext,
    ICompanyContext companyContext) : ICommandHandler<RegisterCompanyChannelCommand, CompanyChannel>
{
    public async ValueTask<CompanyChannel> Handle(RegisterCompanyChannelCommand command, CancellationToken cancellationToken)
    {
        await EnsureCompanyIsAccessibleAsync(command.CompanyId, cancellationToken);
        await EnsureCredentialReferenceIsAccessibleAsync(command.CredentialReferenceId, cancellationToken);

        var channel = CreateCompanyChannel(command);

        dbContext.CompanyChannels.Add(channel);
        await dbContext.SaveChangesAsync(cancellationToken);

        return channel;
    }

    private async Task EnsureCompanyIsAccessibleAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (companyContext.CompanyId != companyId
            || !await dbContext.Companies
                .WithDefaultTracking()
                .AnyAsync(entity => entity.Id == companyId, cancellationToken))
        {
            throw new NotFoundException("company", companyId);
        }
    }

    private async Task EnsureCredentialReferenceIsAccessibleAsync(
        Guid? credentialReferenceId,
        CancellationToken cancellationToken)
    {
        if (credentialReferenceId is { } id
            && !await dbContext.IntegrationCredentialReferences
                .WithDefaultTracking()
                .AnyAsync(entity => entity.Id == id, cancellationToken))
        {
            throw new NotFoundException("integration_credential_reference", id);
        }
    }

    private static CompanyChannel CreateCompanyChannel(RegisterCompanyChannelCommand command)
    {
        var metadata = command.Metadata.DeserializeOptional<ChannelMetadata>() ?? new ChannelMetadata();

        return command.Provider switch
        {
            CompanyChannelProvider.WhatsAppCloud when metadata.WhatsAppCloud is { } whatsAppCloud =>
                CompanyChannel.ForWhatsAppCloud(
                    command.CompanyId,
                    command.ProviderChannelId,
                    whatsAppCloud,
                    command.CredentialReferenceId),
            CompanyChannelProvider.WhatsAppCloud => throw new BusinessRuleException(
                "invalid_channel_metadata",
                $"Provider '{command.Provider}' requires matching wrapper metadata."),
            CompanyChannelProvider.Instagram or CompanyChannelProvider.Telegram => throw new BusinessRuleException(
                "unsupported_channel_provider",
                $"Provider '{command.Provider}' is not supported in the MVP."),
            _ => throw new BusinessRuleException(
                "invalid_channel_provider",
                $"Provider '{command.Provider}' is not valid."),
        };
    }
}
