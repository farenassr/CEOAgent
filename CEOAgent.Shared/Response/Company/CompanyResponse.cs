namespace CEOAgent.Shared.Response.Company;

public sealed class CompanyResponse(Guid id, string name, string status)
{
    /// <summary>
    /// Unique company identifier. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30.
    /// </summary>
    public Guid Id { get; set; } = id;

    /// <summary>
    /// Human-readable company name. Example: Contoso Bistro.
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// Current company lifecycle status. Example: Active.
    /// </summary>
    public string Status { get; set; } = status;
}
