using Microsoft.Extensions.Logging;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class SendWhatsAppMessageEndpoint
{
    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Information,
        Message = "WhatsAppManualMessageSent OrganizationId={OrganizationId} CompanyChannelId={CompanyChannelId} ProviderMessageId={ProviderMessageId} TextLength={TextLength}")]
    private static partial void WhatsAppManualMessageSent(
        ILogger logger,
        Guid organizationId,
        Guid companyChannelId,
        string providerMessageId,
        int textLength);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Warning,
        Message = "WhatsAppManualMessageSendFailed OrganizationId={OrganizationId} CompanyChannelId={CompanyChannelId} TextLength={TextLength}")]
    private static partial void WhatsAppManualMessageSendFailed(
        ILogger logger,
        Exception exception,
        Guid organizationId,
        Guid companyChannelId,
        int textLength);
}
