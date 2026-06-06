namespace CeoAgent.Application.Abstractions.Secrets;

public interface ISecretValueProvider
{
    Task<string> GetSecretValueAsync(
        string reference,
        CancellationToken cancellationToken);
}
