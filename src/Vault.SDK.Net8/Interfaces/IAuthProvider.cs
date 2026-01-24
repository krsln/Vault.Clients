using Vault.SDK.Net8.DTO;

namespace Vault.SDK.Net8.Interfaces;

/// <summary>
/// Provides authentication to the Vault service.
/// </summary>
public interface IAuthProvider
{
    /// <summary>
    /// Authenticates asynchronously and returns the auth response.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<AuthResponse> AuthenticateAsync(CancellationToken cancellationToken = default);
}