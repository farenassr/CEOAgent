namespace CeoAgent.ApiService.Modules.WhatsApp;

public interface IWhatsAppSignatureValidator
{
    bool IsValid(string requestBody, string? signatureHeader, string appSecret);
}
