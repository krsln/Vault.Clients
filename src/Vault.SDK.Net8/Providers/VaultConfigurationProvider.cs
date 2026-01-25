using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Configuration;
using Vault.SDK.Net8.Interfaces;
using Vault.SDK.Net8.Misc;

namespace Vault.SDK.Net8.Providers;

public sealed class VaultConfigurationProvider(ISecretClient client, VaultOptions options) : ConfigurationProvider
{
    // We store a Task instead of a string. This represents the ongoing resolution process.
    // Multiple callers will await the same Task instance, preventing duplicate HTTP requests.
    private readonly ConcurrentDictionary<string, Task<string?>> _tasks = new();
    private readonly ConcurrentDictionary<string, string> _vaultRefs = new();

    public override void Load()
    {
        foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
        {
            var name = (string)env.Key;
            var rawValue = env.Value?.ToString() ?? string.Empty;

            if (rawValue.StartsWith("vault:", StringComparison.OrdinalIgnoreCase))
            {
                var vaultPath = rawValue["vault:".Length..].Trim();
                if (!string.IsNullOrEmpty(vaultPath))
                {
                    _vaultRefs[name] = vaultPath;

                    // Start preload: Add the task to the dictionary to trigger it in the background.
                    _tasks.TryAdd(name, ResolveSecretInternalAsync(name, vaultPath));

                    if (options.Debug)
                        Console.WriteLine($"[Vault:Config] Registered '{name}' → vault:{vaultPath}");
                }
            }
            else if (!string.IsNullOrEmpty(rawValue))
            {
                Data[name] = rawValue;
            }
        }
    }

    private async Task<string?> ResolveSecretInternalAsync(string configKey, string vaultKey)
    {
        try
        {
            if (options.Debug)
                Console.WriteLine($"[Vault:Config] Resolving '{configKey}'");

            var value = await client.GetSecretAsync(vaultKey, CancellationToken.None);

            // Update the underlying Data dictionary of the ConfigurationProvider
            Data[configKey] = value ?? string.Empty;

            if (options.Debug)
                Console.WriteLine($"[Vault:Config] Resolved '{configKey}'");

            return value;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            HandleMissingSecret(configKey, vaultKey, ex);
            return null;
        }
        catch (Exception ex)
        {
            HandleResolutionError(configKey, vaultKey, ex);
            return null;
        }
    }

    public override bool TryGet(string key, out string? value)
    {
        // If this is a vault reference and a resolution task exists
        if (_tasks.TryGetValue(key, out var task))
        {
            // Sync-over-async: If the task is already running (from Load or previous TryGet),
            // wait for the same task object instead of starting a new one.
            value = task.GetAwaiter().GetResult();
            return true;
        }

        // Standard configuration values
        return Data.TryGetValue(key, out value);
    }

    private void HandleMissingSecret(string configKey, string vaultKey, Exception ex)
    {
        var msg = $"[Vault:Config] Secret not found: '{vaultKey}' for config key '{configKey}'";

        if (options.FailOnMissingSecret)
        {
            throw new InvalidOperationException($"CRITICAL: {msg}", ex);
        }

        if (options.Debug) Console.WriteLine(msg);
    }

    private void HandleResolutionError(string configKey, string vaultKey, Exception ex)
    {
        var msg = $"[Vault:Config] Error resolving '{configKey}': {ex.Message}";

        if (options.FailOnMissingSecret)
        {
            throw new InvalidOperationException($"CRITICAL: {msg}", ex);
        }

        if (options.Debug) Console.WriteLine(msg);
    }
}