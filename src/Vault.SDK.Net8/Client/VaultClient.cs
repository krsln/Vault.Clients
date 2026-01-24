using System.Net.Http.Headers;
using Vault.SDK.Net8.DTO;
using Vault.SDK.Net8.Interfaces;
using Vault.SDK.Net8.Misc;

namespace Vault.SDK.Net8.Client;

public sealed class VaultClient(
    IVaultHttpClient http,
    ITokenProvider tokens,
    ISecretCache cache,
    VaultOptions options)
    : ISecretClient
{
    public async Task<string?> GetSecretAsync(string key, CancellationToken ct)
    {
        var encodedKey = Uri.EscapeDataString(key);

        if (cache.TryGet(encodedKey, out var cached))
            return cached;

        var token = await tokens.GetTokenAsync(ct);


        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/Secret/read/{encodedKey}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        var response = await http.SendAsync<SecretResponse>(request, ct);

        cache.Set(key, response.Value, options.CacheTtl);

        return response.Value;
    }
}