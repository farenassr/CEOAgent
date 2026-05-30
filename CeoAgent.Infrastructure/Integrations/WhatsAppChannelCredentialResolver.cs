using CeoAgent.Integrations.Messaging;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Integrations;

public sealed class WhatsAppChannelCredentialResolver(CeoAgentDbContext dbContext) : IWhatsAppChannelCredentialResolver
{
    public async Task<WhatsAppChannelCredentialReference> ResolveAsync(
        Guid companyChannelId,
        CancellationToken cancellationToken)
    {
        var channel = await dbContext.CompanyChannels
            .Include(entity => entity.CredentialReference)
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
            channel.CredentialReference.Reference);
    }
}
