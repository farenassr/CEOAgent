namespace CEOAgent.Infrastructure.Persistence.Entities;

public abstract class AuditableCompanyOwnedEntity : CompanyOwnedEntity
{
    /// <summary>
    /// UTC timestamp when the row was created. Example: 2026-05-22T10:15:30Z.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the row was last updated. Example: 2026-05-22T10:45:00Z.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
