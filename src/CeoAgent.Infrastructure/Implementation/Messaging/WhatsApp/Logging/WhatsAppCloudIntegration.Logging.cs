using Microsoft.Extensions.Logging;

namespace CeoAgent.Infrastructure.Implementation.Messaging.WhatsApp;

public sealed partial class WhatsAppCloudIntegration
{
    private static readonly Func<ILogger, Guid, Guid, Guid?, Guid?, IDisposable?> MessageSendScope =
        LoggerMessage.DefineScope<Guid, Guid, Guid?, Guid?>(
            "OrganizationId={OrganizationId} CompanyChannelId={CompanyChannelId} ConversationId={ConversationId} MessageId={MessageId}");

    private static IDisposable? BeginMessageSendScope(
        ILogger logger,
        Guid organizationId,
        Guid companyChannelId,
        Guid? conversationId,
        Guid? messageId)
    {
        return MessageSendScope(logger, organizationId, companyChannelId, conversationId, messageId);
    }

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Information,
        Message = "WhatsAppCloudMessageSendStarting IntegrationProvider={IntegrationProvider} MessageType={MessageType} Status={Status} HasIdempotencyKey={HasIdempotencyKey}")]
    private static partial void WhatsAppCloudMessageSendStarting(
        ILogger logger,
        string integrationProvider,
        string? messageType,
        string? status,
        bool hasIdempotencyKey);

    [LoggerMessage(
        EventId = 4104,
        Level = LogLevel.Warning,
        Message = "WhatsAppCloudMessageSendFailed StatusCode={StatusCode} IntegrationProvider={IntegrationProvider} MessageType={MessageType} HasIdempotencyKey={HasIdempotencyKey}")]
    private static partial void WhatsAppCloudMessageSendFailed(
        ILogger logger,
        Exception exception,
        int statusCode,
        string integrationProvider,
        string? messageType,
        bool hasIdempotencyKey);
}
