namespace CeoAgent.ApiService.Modules.Queues.Contracts;

public sealed class SendQueueMessageRequest
{
    public string MessageText { get; set; } = string.Empty;
}
