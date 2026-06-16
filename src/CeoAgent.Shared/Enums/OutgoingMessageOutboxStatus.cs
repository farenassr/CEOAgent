using System.ComponentModel;

namespace CeoAgent.Shared.Enums;

public enum OutgoingMessageOutboxStatus
{
    [Description("The outbound message is stored durably and is waiting to be sent to the external provider.")]
    WaitingToSendToProvider = 1,

    [Description("A dispatcher has claimed the outbound message and is currently sending it to the provider.")]
    SendingToProvider = 2,

    [Description("The provider accepted the outbound message.")]
    SentToProvider = 3,

    [Description("Sending to the provider failed temporarily and a retry has been scheduled.")]
    ProviderSendRetryScheduled = 4,

    [Description("Sending to the provider failed permanently or exceeded the maximum retry count.")]
    ProviderSendFailed = 5,

    [Description("The outbound delivery was cancelled before being successfully sent to the provider.")]
    DeliveryCancelled = 6,
}
