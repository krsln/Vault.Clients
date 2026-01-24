namespace Vault.SDK.Net8.Misc;

public sealed class VaultOptions
{
    public const string SectionName = "Vault";

    public string? ApiUrl { get; set; }
    public bool Debug { get; set; }

    // Should the application crash if the critical secret cannot be found?
    // It is safer to crash in startup than to work with incomplete configuration in enterprise systems.
    public bool FailOnMissingSecret { get; set; } = false;

    public int RetryCount { get; set; } = 3;
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
}