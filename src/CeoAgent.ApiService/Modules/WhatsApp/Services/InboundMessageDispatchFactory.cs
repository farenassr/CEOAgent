using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Enums;

namespace CeoAgent.ApiService.Modules.WhatsApp;

internal static class InboundMessageDispatchFactory
{
    private const string AzureQueueProvider = "azure_queue";

    public static MessageDispatch Create(
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        string? correlationId)
    {
        return new MessageDispatch
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            MessageId = messageId,
            Operation = MessageDispatchOperation.InboundQueueDispatch,
            Provider = AzureQueueProvider,
            Status = MessageDispatchStatus.Pending,
            IdempotencyKey = IdempotencyKey(messageId),
            CorrelationId = correlationId,
        };
    }

    public static string IdempotencyKey(Guid messageId)
    {
        return $"inbound-queue:{messageId:N}";
    }
}
