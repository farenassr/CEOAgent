namespace CeoAgent.Worker.Jobs;

/// <summary>
/// Configures queue polling, visibility timeout, idle delay, and poison queue naming for incoming message jobs.
/// </summary>
public sealed class IncomingQueueOptions
{
    /// <summary>
    /// Configuration section name used to bind incoming queue options.
    /// </summary>
    public const string SectionName = "IncomingQueue";

    /// <summary>
    /// Maximum number of messages received in each poll.
    /// </summary>
    public int MaxMessages { get; set; } = 1;

    /// <summary>
    /// Visibility timeout in minutes applied while processing a queue message.
    /// </summary>
    public int VisibilityTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Delay in milliseconds when a poll returns no messages.
    /// </summary>
    public int EmptyQueueDelayMilliseconds { get; set; } = 1_000;

    /// <summary>
    /// Suffix appended to the source queue name for poison messages.
    /// </summary>
    public string PoisonQueueSuffix { get; set; } = "-poison";
}
