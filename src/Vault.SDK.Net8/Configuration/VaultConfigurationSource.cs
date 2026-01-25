using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions; // Added for NullLogger
using Vault.SDK.Net8.Client;
using Vault.SDK.Net8.Misc;
using Vault.SDK.Net8.Providers;

namespace Vault.SDK.Net8.Configuration;

public sealed class VaultConfigurationSource(IConfiguration configuration) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var options = new VaultOptions();
        configuration.GetSection(VaultOptions.SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.ApiUrl))
        {
            throw new InvalidOperationException(
                "Vault ApiUrl must be provided. Ensure 'Vault:ApiUrl' is set.");
        }

        // HttpClient setup with SocketsHttpHandler (for DNS refresh)
        // Configuration phase, so we can't use IHttpClientFactory; this manual handler is important.
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5) // To catch DNS changes
        };

        var httpClientImpl = new HttpClient(handler);

        // Fixed: Use NullLogger for a no-op implementation
        var vaultHttpClient = new VaultHttpClient(httpClientImpl, options);

        var authProvider = new KubernetesAuthProvider(vaultHttpClient, options);
        var tokenProvider = new TokenProvider(authProvider);
        var cache = new SecretCache();

        var client = new VaultClient(vaultHttpClient, tokenProvider, cache, options);

        return new VaultConfigurationProvider(client, options);
    }
}