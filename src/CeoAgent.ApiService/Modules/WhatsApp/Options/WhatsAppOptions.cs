namespace CeoAgent.ApiService.Modules.WhatsApp;

/// <summary>
/// Configures WhatsApp webhook verification, signing, and request body limits.
/// </summary>
public sealed class WhatsAppOptions
{
    /// <summary>
    /// Configuration section name used to bind WhatsApp options.
    /// </summary>
    public const string SectionName = "WhatsApp";

    /// <summary>
    /// Optional configured access token for local or legacy WhatsApp operations.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// App secret used to validate signed webhook payloads.
    /// </summary>
    public string? AppSecret { get; set; }

    /// <summary>
    /// Verify token expected during webhook subscription challenge validation.
    /// </summary>
    public string? VerifyToken { get; set; }

    /// <summary>
    /// Maximum accepted WhatsApp webhook request body size in bytes.
    /// </summary>
    public int MaxWebhookBodyBytes { get; set; } = 256 * 1024;
}
