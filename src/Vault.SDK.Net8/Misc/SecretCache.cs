using System.Collections.Concurrent;
using Vault.SDK.Net8.Interfaces;

namespace Vault.SDK.Net8.Misc;

public sealed class SecretCache : ISecretCache
{
    private sealed class CacheItem(string value, TimeSpan ttl)
    {
        public string Value { get; } = value;
        private readonly DateTimeOffset _expiresAt = DateTimeOffset.UtcNow.Add(ttl);

        public bool IsExpired() => DateTimeOffset.UtcNow >= _expiresAt;
    }

    private readonly ConcurrentDictionary<string, CacheItem> _cache = new();

    public bool TryGet(string key, out string? value)
    {
        value = null;
        if (_cache.TryGetValue(key, out var item))
        {
            if (item.IsExpired())
            {
                _cache.TryRemove(key, out _);
                return false;
            }

            value = item.Value;
            return true;
        }

        return false;
    }

    public void Set(string key, string value, TimeSpan ttl)
    {
        var item = new CacheItem(value, ttl);
        _cache[key] = item;
    }
}