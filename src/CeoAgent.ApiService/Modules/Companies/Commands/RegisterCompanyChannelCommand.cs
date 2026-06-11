using System.Text.Json;
using CeoAgent.ApiService.Infrastructure.Json;
using CeoAgent.ApiService.Infrastructure.Security;
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
    Guid OrganizationId,
    CompanyChannelProvider Provider,
    string ProviderChannelId,
    JsonElement? Metadata,
    Guid? CredentialReferenceId) : ICommand<CompanyChannel>;

public sealed class RegisterCompanyChannelCommandHandler(
    CeoAgentDbContext dbContext,
    IAdminTenantGuard tenantGuard) : ICommandHandler<RegisterCompanyChannelCommand, CompanyChannel>
{
    public async ValueTask<CompanyChannel> Handle(RegisterCompanyChannelCommand command, CancellationToken cancellationToken)
    {
        await tenantGuard.GetAccessibleCompanyAsync(command.OrganizationId, trackChanges: false, cancellationToken);
        await tenantGuard.EnsureCredentialReferenceAccessibleAsync(command.OrganizationId, command.CredentialReferenceId, cancellationToken);

        var channel = CreateCompanyChannel(command);

        dbContext.CompanyChannels.Add(channel);
        await dbContext.SaveChangesAsync(cancellationToken);

        return channel;
    }
    private static CompanyChannel CreateCompanyChannel(RegisterCompanyChannelCommand command)
    {
        var metadata = command.Metadata.DeserializeOptional<ChannelMetadata>() ?? new ChannelMetadata();

        return command.Provider switch
        {
            CompanyChannelProvider.WhatsAppCloud when metadata.WhatsAppCloud is { } whatsAppCloud =>
                CompanyChannel.ForWhatsAppCloud(
                    command.OrganizationId,
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
