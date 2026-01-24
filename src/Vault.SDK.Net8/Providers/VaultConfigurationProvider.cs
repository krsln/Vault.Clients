using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Configuration;
using Vault.SDK.Net8.Interfaces;

namespace Vault.SDK.Net8.Providers;

public sealed class VaultConfigurationProvider(ISecretClient client, bool debug = false) : ConfigurationProvider
{
    // Vault refs: config key → vault path
    private readonly ConcurrentDictionary<string, string> _vaultRefs = new();

    // resolved secrets
    private readonly ConcurrentDictionary<string, string?> _resolved = new();

    public override void Load()
    {
        foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
        {
            var name = (string)env.Key;
            var rawValue = env.Value?.ToString() ?? string.Empty;

            if (rawValue.StartsWith("vault:", StringComparison.OrdinalIgnoreCase))
            {
                var secretKey = rawValue["vault:".Length..].Trim();

                if (!string.IsNullOrEmpty(secretKey))
                {
                    _vaultRefs[name] = secretKey;
                    Data[name] = rawValue; // placeholder

                    if (debug)
                        Console.WriteLine($"[VaultSDK] Registered '{name}' → vault:{secretKey}");
                }
            }
            else if (!string.IsNullOrEmpty(rawValue))
            {
                Data[name] = rawValue;
            }
        }

        // Arka planda paralel preload başlat
        _ = PreloadSecretsAsync().ContinueWith(task =>
        {
            if (task.IsFaulted && debug)
                Console.WriteLine($"[VaultSDK] Preload failed: {task.Exception?.Flatten().Message}");
        }, TaskScheduler.Default);
    }

    private async Task PreloadSecretsAsync()
    {
        var tasks = _vaultRefs.Select(kvp => ResolveSecretAsync(kvp.Key, kvp.Value)).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task ResolveSecretAsync(string configKey, string vaultKey)
    {
        try
        {
            if (debug)
                Console.WriteLine($"[VaultSDK] Getting secret '{vaultKey}'");

            var value = await client.GetSecretAsync(vaultKey, CancellationToken.None);

            Data[configKey] = value ?? string.Empty;
            _resolved[configKey] = value;
            _vaultRefs.TryRemove(configKey, out _);

            if (debug)
                Console.WriteLine($"[VaultSDK] Resolved '{configKey}' → '{vaultKey}'");

            OnReload(); // Configuration root'un güncellendiğini bildir
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            if (debug)
                Console.WriteLine($"[VaultSDK] Secret not found: '{vaultKey}'");
            // Placeholder kalır, reload gerekmez
        }
        catch (Exception ex) when (debug)
        {
            Console.WriteLine($"[VaultSDK] Error resolving '{configKey}': {ex.Message}");
        }
    }

    public override bool TryGet(string key, out string? value)
    {
        // 1. Zaten çözülmüşse dön
        if (_resolved.TryGetValue(key, out value))
            return true;

        // 2. Hâlâ vault referansı varsa → sync olarak çöz (startup sırasında ihtiyaç duyulursa)
        if (_vaultRefs.TryGetValue(key, out var vaultKey))
        {
            try
            {
                // Sync bekleme (deadlock riskini kabul ederek – alternatif Task.Run(...).Result kullanılabilir)
                value = client.GetSecretAsync(vaultKey, CancellationToken.None)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                Data[key] = value ?? string.Empty;
                _resolved[key] = value;
                _vaultRefs.TryRemove(key, out _);

                if (debug)
                    Console.WriteLine($"[VaultSDK] Resolved '{key}' → '{vaultKey}' (sync)");

                OnReload();
                return true;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                if (debug)
                    Console.WriteLine($"[VaultSDK] Secret not found (sync): '{vaultKey}'");

                return Data.TryGetValue(key, out value); // placeholder dön
            }
            catch (Exception ex) when (debug)
            {
                Console.WriteLine($"[VaultSDK] Error resolving '{key}' (sync): {ex.Message}");
                return Data.TryGetValue(key, out value);
            }
        }

        // 3. Normal configuration değeri
        return Data.TryGetValue(key, out value);
    }
}