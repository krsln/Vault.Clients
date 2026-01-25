using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Configuration;
using Vault.SDK.Net8.Interfaces;
using Vault.SDK.Net8.Misc;

namespace Vault.SDK.Net8.Providers;

public sealed class VaultConfigurationProvider(ISecretClient client, VaultOptions options) : ConfigurationProvider
{
    // Vault refs: config key → vault path
    private readonly ConcurrentDictionary<string, string> _vaultRefs = new();
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
                    Data[name] = rawValue; // Placeholder  

                    if (options.Debug)
                        Console.WriteLine($"[VaultEnv] Registered '{name}' → vault:{secretKey}");
                }
            }
            else if (!string.IsNullOrEmpty(rawValue))
            {
                Data[name] = rawValue;
            }
        }

        // Start the preload process but do not swallow errors (logging will be handled in context)
        // Note: Calling directly instead of Task.Run might be safer in a synchronous context,
        // but we are preserving the existing asynchronous structure.
        _ = PreloadSecretsAsync().ContinueWith(task =>
        {
            if (task.IsFaulted && options.Debug)
            {
                foreach (var ex in task.Exception?.Flatten().InnerExceptions ?? Enumerable.Empty<Exception>())
                {
                    Console.Error.WriteLine($"[VaultEnv] Background preload failed: {ex.Message}");
                }
            }
        }, TaskScheduler.Default);
    }

    private async Task PreloadSecretsAsync()
    {
        // Limiting the number of parallel requests is a good practice (SemaphoreSlim could be added),
        // but for simplicity, we continue with Task.WhenAll.
        var tasks = _vaultRefs.Select(kvp => ResolveSecretAsync(kvp.Key, kvp.Value)).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task ResolveSecretAsync(string configKey, string vaultKey)
    {
        try
        {
            var value = await client.GetSecretAsync(vaultKey, CancellationToken.None);

            Data[configKey] = value ?? string.Empty;
            _resolved[configKey] = value;
            _vaultRefs.TryRemove(configKey, out _);

            if (options.Debug)
                Console.WriteLine($"[VaultEnv] Resolved '{configKey}'");

            OnReload();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            HandleMissingSecret(configKey, vaultKey, ex);
        }
        catch (Exception ex)
        {
            HandleResolutionError(configKey, vaultKey, ex);
        }
    }

    public override bool TryGet(string key, out string? value)
    {
        if (_resolved.TryGetValue(key, out value))
            return true;

        if (_vaultRefs.TryGetValue(key, out var vaultKey))
        {
            // Sync-over-async: Acceptable in startup scenarios where necessary.
            try
            {
                value = client.GetSecretAsync(vaultKey, CancellationToken.None)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                Data[key] = value ?? string.Empty;
                _resolved[key] = value;
                _vaultRefs.TryRemove(key, out _);

                return true;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                HandleMissingSecret(key, vaultKey, ex);
                // If no exception is thrown (FailOnMissingSecret is false), return placeholder.
                return Data.TryGetValue(key, out value);
            }
            catch (Exception ex)
            {
                HandleResolutionError(key, vaultKey, ex);
                return Data.TryGetValue(key, out value);
            }
        }

        return Data.TryGetValue(key, out value);
    }

    private void HandleMissingSecret(string configKey, string vaultKey, Exception ex)
    {
        var msg = $"[VaultEnv] Secret not found: '{vaultKey}' for config '{configKey}'";

        if (options.FailOnMissingSecret)
        {
            throw new InvalidOperationException($"CRITICAL: {msg}", ex);
        }

        if (options.Debug) Console.WriteLine(msg);
    }

    private void HandleResolutionError(string configKey, string vaultKey, Exception ex)
    {
        var msg = $"[VaultEnv] Error resolving '{configKey}': {ex.Message}";

        if (options.FailOnMissingSecret)
        {
            throw new InvalidOperationException($"CRITICAL: {msg}", ex);
        }

        if (options.Debug) Console.WriteLine(msg);
    }
}