namespace CeoAgent.Adapters.WhatsApp.Abstractions;

public interface ISecretValueProvider
{
    Task<string> GetSecretValueAsync(
        string reference,
        CancellationToken cancellationToken);
}
