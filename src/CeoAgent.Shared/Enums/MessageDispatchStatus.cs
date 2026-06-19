namespace CeoAgent.Shared.Enums;

public enum MessageDispatchStatus
{
    Pending = 0,
    InProgress = 1,
    Succeeded = 2,
    RetryScheduled = 3,
    Failed = 4,
    Cancelled = 5,
}
