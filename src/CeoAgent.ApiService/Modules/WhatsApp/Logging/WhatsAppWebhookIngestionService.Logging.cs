using Microsoft.Extensions.Logging;

namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class WhatsAppWebhookIngestionService
{
    [LoggerMessage(
        EventId = 4205,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookIngestionStarted CorrelationId={CorrelationId} BodyLength={BodyLength}")]
    private static partial void WhatsAppWebhookIngestionStarted(
        ILogger logger,
        string? correlationId,
        int bodyLength);

    [LoggerMessage(
        EventId = 4206,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookInvalidJson CorrelationId={CorrelationId} BodyLength={BodyLength}")]
    private static partial void WhatsAppWebhookInvalidJson(
        ILogger logger,
        string? correlationId,
        int bodyLength);

    [LoggerMessage(
        EventId = 4207,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookMessageNotFound CorrelationId={CorrelationId} BodyLength={BodyLength}")]
    private static partial void WhatsAppWebhookMessageNotFound(
        ILogger logger,
        string? correlationId,
        int bodyLength);

    [LoggerMessage(
        EventId = 4208,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookMessageParsed CorrelationId={CorrelationId} PhoneNumberId={PhoneNumberId} ProviderMessageId={ProviderMessageId} FromLength={FromLength} HasContactName={HasContactName} MessageType={MessageType} TextLength={TextLength} OccurredAtUtc={OccurredAtUtc}")]
    private static partial void WhatsAppWebhookMessageParsed(
        ILogger logger,
        string? correlationId,
        string phoneNumberId,
        string providerMessageId,
        int fromLength,
        bool hasContactName,
        string messageType,
        int? textLength,
        DateTime occurredAtUtc);

    [LoggerMessage(
        EventId = 4209,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookUnknownChannel CorrelationId={CorrelationId} PhoneNumberId={PhoneNumberId} ProviderMessageId={ProviderMessageId}")]
    private static partial void WhatsAppWebhookUnknownChannel(
        ILogger logger,
        string? correlationId,
        string phoneNumberId,
        string providerMessageId);

    [LoggerMessage(
        EventId = 4210,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookChannelResolved CorrelationId={CorrelationId} OrganizationId={OrganizationId} CompanyChannelId={CompanyChannelId} PhoneNumberId={PhoneNumberId}")]
    private static partial void WhatsAppWebhookChannelResolved(
        ILogger logger,
        string? correlationId,
        Guid organizationId,
        Guid companyChannelId,
        string phoneNumberId);

    [LoggerMessage(
        EventId = 4213,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookDuplicateMessage CorrelationId={CorrelationId} OrganizationId={OrganizationId} CompanyChannelId={CompanyChannelId} ProviderMessageId={ProviderMessageId}")]
    private static partial void WhatsAppWebhookDuplicateMessage(
        ILogger logger,
        string? correlationId,
        Guid organizationId,
        Guid companyChannelId,
        string providerMessageId);

    [LoggerMessage(
        EventId = 4202,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookMessagePersisted CorrelationId={CorrelationId} OrganizationId={OrganizationId} CompanyChannelId={CompanyChannelId} ConversationId={ConversationId} MessageId={MessageId} OutboxId={OutboxId} ProviderMessageId={ProviderMessageId} TextLength={TextLength}")]
    private static partial void WhatsAppWebhookMessagePersisted(
        ILogger logger,
        string? correlationId,
        Guid organizationId,
        Guid companyChannelId,
        Guid conversationId,
        Guid messageId,
        Guid outboxId,
        string providerMessageId,
        int textLength);

    [LoggerMessage(
        EventId = 4203,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookMessageEnqueued CorrelationId={CorrelationId} OrganizationId={OrganizationId} CompanyChannelId={CompanyChannelId} ConversationId={ConversationId} MessageId={MessageId} ProviderMessageId={ProviderMessageId} QueueCorrelationId={QueueCorrelationId}")]
    private static partial void WhatsAppWebhookMessageEnqueued(
        ILogger logger,
        string? correlationId,
        Guid organizationId,
        Guid companyChannelId,
        Guid conversationId,
        Guid messageId,
        string providerMessageId,
        string? queueCorrelationId);

    [LoggerMessage(
        EventId = 4214,
        Level = LogLevel.Information,
        Message = "WhatsAppWebhookDuplicateMessageRecoveryRequested OrganizationId={OrganizationId} ConversationId={ConversationId} MessageId={MessageId} Reason={Reason}")]
    private static partial void WhatsAppWebhookDuplicateMessageRecoveryRequested(
        ILogger logger,
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        string reason);
}
