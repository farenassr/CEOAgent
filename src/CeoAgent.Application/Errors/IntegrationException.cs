namespace CeoAgent.Application.Errors;

public sealed class IntegrationException(string provider, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public string Provider { get; } = provider;
    public string FailureReason { get; } = "upstream_error";

    public IntegrationException(string provider, string failureReason, string message, Exception? inner = null)
        : this(provider, message, inner)
    {
        FailureReason = failureReason;
    }
}
