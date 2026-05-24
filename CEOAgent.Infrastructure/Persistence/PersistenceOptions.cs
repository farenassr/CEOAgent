namespace CEOAgent.Infrastructure.Persistence;

public sealed class PersistenceOptions
{
    public bool UseInMemoryDatabase { get; set; }

    public string InMemoryDatabaseName { get; set; } = "CEOAgent";
}
