namespace Vault.SDK.Net8.DTO;

public sealed class VaultToken(string value, DateTimeOffset expiresAt)
{
    public string Value { get; } = value;
    private DateTimeOffset ExpiresAt { get; } = expiresAt;

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}