using System.Text.Json;

namespace CeoAgent.Shared.Response.Company;

public sealed class CompanyResponse
{
    /// <summary>
    /// Unique company identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable company name. Example: Contoso Bistro.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current company lifecycle status. Example: Active.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// IANA time zone used for company-local scheduling. Example: America/Bogota.
    /// </summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>
    /// Working hours configuration as JSON.
    /// </summary>
    public JsonElement? WorkingHours { get; set; }
}
