namespace CEOAgent.Shared.Response.Company;

public sealed class CreatedResourceResponse(Guid id)
{
    /// <summary>
    /// Identifier of the created or updated resource. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b43.
    /// </summary>
    public Guid Id { get; set; } = id;
}
