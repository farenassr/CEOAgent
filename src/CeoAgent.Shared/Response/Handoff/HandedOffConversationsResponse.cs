namespace CeoAgent.Shared.Response.Handoff;

/// <summary>
/// Admin pull view: conversations currently paused for human attention.
/// </summary>
public sealed class HandedOffConversationsResponse
{
    /// <summary>
    /// Conversations currently in the HandedOff state.
    /// </summary>
    public List<HandedOffConversationResponse> Conversations { get; set; } = [];

    /// <summary>
    /// Number of handed-off conversations returned. Example: 3.
    /// </summary>
    public int Count { get; set; }
}
