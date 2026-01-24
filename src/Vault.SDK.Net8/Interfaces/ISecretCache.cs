namespace Vault.SDK.Net8.Interfaces;

public interface ISecretCache
{
    bool TryGet(string key, out string? value);
    void Set(string key, string value, TimeSpan ttl);
}