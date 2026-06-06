namespace CeoAgent.Application.Abstractions.Jobs;

public static class ProcessIncomingMessageJobRetryPolicy
{
    public const int MaxRetries = 1;

    public const int MaxAttempts = MaxRetries + 1;
}
