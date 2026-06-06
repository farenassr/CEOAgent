using CeoAgent.Shared.Messaging;

namespace CeoAgent.Application.Abstractions.Messaging;

public interface IWhatsAppChannelCredentialResolver
{
    Task<WhatsAppChannelCredentialReference> ResolveAsync(
        Guid companyChannelId,
        CancellationToken cancellationToken);
}

public sealed record WhatsAppChannelCredentialReference(
    string PhoneNumberId,
    string? BusinessAccountId,
    string CredentialReference);
