using Vault.SDK.Net8.DTO;
using Vault.SDK.Net8.Interfaces;

namespace Vault.SDK.Net8.Providers;

public sealed class TokenProvider(IAuthProvider auth) : ITokenProvider
{
    private VaultToken? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<VaultToken> GetTokenAsync(CancellationToken ct)
    {
        // Fast-path (without lock)
        if (_cached is not null && !_cached.IsExpired)
            return _cached;

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check
            if (_cached is not null && !_cached.IsExpired)
                return _cached;

            var result = await auth.AuthenticateAsync(ct);
            _cached = new VaultToken(result.Token, result.ExpiresAt);

            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }
}