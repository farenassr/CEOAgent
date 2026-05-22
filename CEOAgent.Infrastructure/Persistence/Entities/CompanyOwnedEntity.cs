namespace CEOAgent.Infrastructure.Persistence.Entities;

public abstract class CompanyOwnedEntity
{
    /// <summary>
    /// Company that owns this row. Example: 018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30.
    /// </summary>
    public Guid CompanyId { get; set; }
}
