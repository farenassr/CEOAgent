using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Implementation.Messaging.WhatsApp;

public sealed class WhatsAppChannelCredentialResolver(CeoAgentDbContext dbContext) : IWhatsAppChannelCredentialResolver
{
    public async Task<WhatsAppChannelCredentialReference> ResolveAsync(
        Guid companyChannelId,
        CancellationToken cancellationToken)
    {
        var channel = await dbContext.CompanyChannels
            .AsNoTracking()
            .Select(entity => new
            {
                entity.Id,
                entity.Provider,
                entity.ProviderChannelId,
                entity.Metadata,
                CredentialReference = entity.CredentialReference == null
                    ? null
                    : entity.CredentialReference.Reference,
            })
            .SingleAsync(entity => entity.Id == companyChannelId, cancellationToken);

        if (channel.Provider != CompanyChannelProvider.WhatsAppCloud)
        {
            throw new InvalidOperationException($"Channel provider '{channel.Provider}' is not supported by WhatsApp Cloud.");
        }

        if (channel.CredentialReference is null)
        {
            throw new InvalidOperationException("WhatsApp channel requires a credential reference.");
        }

        return new WhatsAppChannelCredentialReference(
            channel.Metadata.WhatsAppCloud?.PhoneNumberId ?? channel.ProviderChannelId,
            channel.Metadata.WhatsAppCloud?.BusinessAccountId,
            channel.CredentialReference);
    }
}
