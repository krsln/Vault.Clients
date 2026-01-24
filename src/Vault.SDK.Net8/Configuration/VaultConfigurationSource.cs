using Microsoft.Extensions.Configuration;
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

        var httpClient = new VaultHttpClient(new HttpClient(), options);

        var authProvider = new KubernetesAuthProvider(httpClient);
        var tokenProvider = new TokenProvider(authProvider);
        var cache = new SecretCache();
        var client = new VaultClient(httpClient, tokenProvider, cache);

        return new VaultConfigurationProvider(client, options.Debug);
    }
}