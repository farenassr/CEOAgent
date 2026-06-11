namespace CeoAgent.Shared.Response.WhatsApp;

public sealed class ReceiveWhatsAppMessageResponse
{
    /// <summary>
    /// Company that owns the inbound WhatsApp message.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Conversation selected or created for the WhatsApp customer.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Persisted inbound user message id.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Whether a Worker job was enqueued.
    /// </summary>
    public bool Enqueued { get; set; }
}
