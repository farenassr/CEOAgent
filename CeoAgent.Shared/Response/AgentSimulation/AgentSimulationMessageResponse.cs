namespace CeoAgent.Shared.Response.AgentSimulation;

public sealed class AgentSimulationMessageResponse
{
    /// <summary>
    /// Company that owns the simulated message.
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Conversation selected or created for the simulated customer.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Persisted synthetic user message id.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Whether a Worker job was enqueued.
    /// </summary>
    public bool Enqueued { get; set; }
}
