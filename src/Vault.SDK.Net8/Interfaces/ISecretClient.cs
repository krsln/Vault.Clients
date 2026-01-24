namespace Vault.SDK.Net8.Interfaces;

public interface ISecretClient
{
    Task<string?> GetSecretAsync(string key, CancellationToken ct);
}