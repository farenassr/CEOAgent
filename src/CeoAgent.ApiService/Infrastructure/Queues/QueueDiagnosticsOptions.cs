using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;

namespace CeoAgent.ApiService.Infrastructure.Queues;

/// <summary>
/// Configures which Azure queues can be inspected or written through diagnostics endpoints.
/// </summary>
public sealed class QueueDiagnosticsOptions
{
    /// <summary>
    /// Configuration section name used to bind queue diagnostics options.
    /// </summary>
    public const string SectionName = "QueueDiagnostics";

    /// <summary>
    /// Queue names allowed for diagnostic reads and writes when writes are explicitly enabled.
    /// </summary>
    public string[] AllowedQueueNames { get; set; } = [IncomingMessageQueueNames.ProcessIncomingMessage];

    /// <summary>
    /// Enables administrative queue writes. Keep false outside local break-glass diagnostics.
    /// </summary>
    public bool EnableWrites { get; set; }

    /// <summary>
    /// Default number of messages to peek when a request omits or invalidates the limit.
    /// </summary>
    public int DefaultMaxMessages { get; set; } = 10;

    /// <summary>
    /// Default number of queues to return when a request omits or invalidates the limit.
    /// </summary>
    public int DefaultMaxQueues { get; set; } = 10;
}
