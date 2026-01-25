using Vault.SDK.Net8.DTO;
using Vault.SDK.Net8.Misc;

namespace Vault.SDK.Net8.Interfaces;

public interface ITokenProvider
{
    Task<VaultToken> GetTokenAsync(VaultOptions options, CancellationToken ct);
}