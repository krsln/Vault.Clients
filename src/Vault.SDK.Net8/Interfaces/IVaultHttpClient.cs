namespace Vault.SDK.Net8.Interfaces;

public interface IVaultHttpClient
{
    Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default);
}