namespace CEOAgent.ApiService.Modules.Admin.Companies.Models.Request;

public sealed class CreateCompanyRequest
{
    /// <summary>
    /// Human-readable company name. Example: Contoso Bistro.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IANA time zone used for company-local scheduling. Example: America/Bogota.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";
}
