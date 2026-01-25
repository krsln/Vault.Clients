using Vault.SDK.Net8.DTO;
using Vault.SDK.Net8.Interfaces;
using Vault.SDK.Net8.Misc;

namespace Vault.SDK.Net8.Providers;

public sealed class TokenProvider(IAuthProvider auth) : ITokenProvider
{
    private VaultToken? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<VaultToken> GetTokenAsync(VaultOptions options, CancellationToken ct)
    {
        if (options.Debug)
            Console.WriteLine(
                $"[Vault:Token] GetTokenAsync called. Current cached: {(_cached != null ? "yes" : "no")}, Expired: {(_cached?.IsExpired ?? true)}");

        if (_cached is not null && !_cached.IsExpired)
        {
            if (options.Debug) Console.WriteLine("[Vault:Token] Fast-path hit - returning cached token");
            return _cached;
        }

        if (options.Debug) Console.WriteLine("[Vault:Token] Entering lock...");
        await _lock.WaitAsync(ct);
        try
        {
            if (options.Debug)
                Console.WriteLine(
                    $"[Vault:Token] Inside lock. Cached now: {(_cached != null ? "yes" : "no")}, Expired: {(_cached?.IsExpired ?? true)}");

            if (_cached is not null && !_cached.IsExpired)
            {
                if (options.Debug) Console.WriteLine("[Vault:Token] Double-check hit - returning cached");
                return _cached;
            }

            Console.WriteLine("[Vault:Token] Performing real auth...");
            var result = await auth.AuthenticateAsync(ct);

            if (options.Debug)
                Console.WriteLine(
                    $"[Vault:Token] Auth succeeded. New ExpiresAt: {result.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}");

            _cached = new VaultToken(result.Token, result.ExpiresAt);

            return _cached;
        }
        finally
        {
            _lock.Release();
            if (options.Debug) Console.WriteLine("[Vault:Token] Lock released");
        }
    }
}