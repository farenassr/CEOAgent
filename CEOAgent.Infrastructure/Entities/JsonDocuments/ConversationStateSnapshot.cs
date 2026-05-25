namespace CeoAgent.Infrastructure.Entities.JsonDocuments;

public sealed class ConversationStateSnapshot
{
    public string? CurrentIntent { get; set; }

    public string? PendingAction { get; set; }

    public Dictionary<string, object> Slots { get; set; } = [];

    public List<string> ConversationFlags { get; set; } = [];

    public int TurnCount { get; set; }
}
