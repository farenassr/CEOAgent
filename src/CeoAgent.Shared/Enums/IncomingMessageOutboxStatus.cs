using System.ComponentModel;

namespace CeoAgent.Shared.Enums;

public enum IncomingMessageOutboxStatus
{
    [Description("The inbound message is stored durably and is waiting to be dispatched to the queue.")]
    WaitingToBeQueued = 1,

    [Description("A dispatcher has claimed the inbound message and is currently trying to enqueue it.")]
    QueueDispatchInProgress = 2,

    [Description("The inbound message was successfully queued for worker processing.")]
    QueuedForWorkerProcessing = 3,

    [Description("Dispatch to the queue failed temporarily and a retry has been scheduled.")]
    QueueDispatchRetryScheduled = 4,

    [Description("Dispatch to the queue failed permanently or exceeded the maximum retry count.")]
    QueueDispatchFailed = 5,
}
