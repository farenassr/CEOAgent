namespace CeoAgent.Adapters.Secrets;

public interface ISecretValueProvider
{
    Task<string> GetSecretValueAsync(
        string reference,
        CancellationToken cancellationToken);
}
