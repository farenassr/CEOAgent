using System.ComponentModel;

namespace CeoAgent.Shared.Request.AgentSimulation;

public sealed class AgentSimulationMessageRequest
{
    /// <summary>
    /// Text to persist as a synthetic user message.
    /// </summary>
    [Description("Text to persist as a synthetic user message.")]
    public string MessageText { get; set; } = string.Empty;

    /// <summary>
    /// Optional stable external customer id for repeat simulations.
    /// </summary>
    [Description("Optional stable external customer id for repeat simulations.")]
    public string? ExternalCustomerId { get; set; }
}
