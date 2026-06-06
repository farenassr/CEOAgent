namespace CeoAgent.Infrastructure.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public bool UseInMemoryDatabase { get; set; }

    public string InMemoryDatabaseName { get; set; } = "CeoAgent";

    public static bool IsValid(PersistenceOptions options)
    {
        return !options.UseInMemoryDatabase || !string.IsNullOrWhiteSpace(options.InMemoryDatabaseName);
    }
}
