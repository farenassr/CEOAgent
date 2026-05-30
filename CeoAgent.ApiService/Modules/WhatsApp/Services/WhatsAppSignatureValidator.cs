using System.Security.Cryptography;
using System.Text;

namespace CeoAgent.ApiService.Modules.WhatsApp;

/// <summary>
/// Validates WhatsApp webhook signatures using HMAC-SHA256 and fixed-time comparison.
/// </summary>
public sealed class WhatsAppSignatureValidator : IWhatsAppSignatureValidator
{
    private const string Prefix = "sha256=";

    /// <summary>
    /// Recomputes the expected x-hub-signature-256 value and compares it to the supplied header.
    /// </summary>
    public bool IsValid(string requestBody, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(appSecret))
        {
            return false;
        }

        var signature = signatureHeader.Trim();
        if (signature.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            signature = signature[Prefix.Length..];
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
        var expectedHex = Convert.ToHexString(hash);

        var expectedBytes = Encoding.ASCII.GetBytes(expectedHex.ToLowerInvariant());
        var signatureBytes = Encoding.ASCII.GetBytes(signature.ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(expectedBytes, signatureBytes);
    }
}
