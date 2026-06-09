namespace CeoAgent.ApiService.Modules.WhatsApp;

public interface IWhatsAppSignatureValidator
{
    bool IsValid(byte[] requestBody, string? signatureHeader, string appSecret);
}
