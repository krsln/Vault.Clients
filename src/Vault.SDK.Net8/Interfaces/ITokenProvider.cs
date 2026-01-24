using Vault.SDK.Net8.DTO;

namespace Vault.SDK.Net8.Interfaces;

public interface ITokenProvider
{
    Task<VaultToken> GetTokenAsync(CancellationToken ct);
}