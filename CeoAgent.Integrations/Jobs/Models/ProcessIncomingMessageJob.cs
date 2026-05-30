namespace CeoAgent.Integrations.Jobs;

public sealed record ProcessIncomingMessageJob(
    Guid CompanyId,
    Guid ConversationId,
    Guid MessageId,
    string? CorrelationId)
{
    public Guid JobId { get; init; } = Guid.CreateVersion7();
}
