namespace CEOAgent.ApiService;

public sealed class IntegrationException(string provider, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public string Provider { get; } = provider;
}
