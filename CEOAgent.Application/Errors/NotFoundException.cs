namespace CeoAgent.Application.Errors;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base("Resource not found.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentNullException.ThrowIfNull(key);
    }
}
