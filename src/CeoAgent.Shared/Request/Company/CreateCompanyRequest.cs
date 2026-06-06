using System.ComponentModel;

namespace CeoAgent.Shared.Request.Company;

public sealed class CreateCompanyRequest
{
    /// <summary>
    /// Human-readable company name.
    /// </summary>
    [Description("Human-readable company name.")]
    [DefaultValue("Contoso Bistro")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IANA time zone used for company-local scheduling.
    /// </summary>
    [Description("IANA time zone used for company-local scheduling.")]
    [DefaultValue("America/Bogota")]
    public string TimeZoneId { get; set; } = "UTC";
}
