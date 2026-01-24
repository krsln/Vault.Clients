namespace Vault.SDK.Net8.DTO;

public sealed record AuthResponse(
    string Token,
    string Identity,
    DateTimeOffset ExpiresAt
);