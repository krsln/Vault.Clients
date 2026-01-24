using Microsoft.Extensions.Configuration;

namespace Vault.SDK.Net8.Configuration;

public static class VaultConfigurationExtensions
{
    public static IConfigurationBuilder AddVault(this IConfigurationBuilder builder)
    {
        var tempConfig = builder.Build();
        return builder.Add(new VaultConfigurationSource(tempConfig));
    }
}