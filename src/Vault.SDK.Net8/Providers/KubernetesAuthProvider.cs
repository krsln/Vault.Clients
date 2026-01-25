using System.Text;
using System.Text.Json;
using Vault.SDK.Net8.DTO;
using Vault.SDK.Net8.Interfaces;
using Vault.SDK.Net8.Misc;

namespace Vault.SDK.Net8.Providers;

public sealed class KubernetesAuthProvider(
    IVaultHttpClient http,
    VaultOptions options,
    string tokenPath = "/var/run/secrets/kubernetes.io/serviceaccount/token"
) : IAuthProvider
{
    public async Task<AuthResponse> AuthenticateAsync(CancellationToken ct)
    {
        try
        {
            if (options.Debug) Console.WriteLine("[Vault:Auth] Trying to read JWT file...");
            var jwt = await File.ReadAllTextAsync(tokenPath, ct);

            var request = new HttpRequestMessage(HttpMethod.Post, options.KubernetesAuthEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { jwt }), Encoding.UTF8, "application/json")
            };

            // if (options.Debug)
            //     Console.WriteLine($"[Vault:Auth] Sending auth request to {options.KubernetesAuthEndpoint}");
            var response = await http.SendAsync<AuthResponse>(request, ct);

            if (options.Debug) Console.WriteLine($"[Vault:Auth] Authentication successful");
            return response;
        }
        catch (FileNotFoundException ex)
        {
            if (options.Debug) Console.WriteLine($"[Vault:Auth] CRITICAL: Token file not found at {tokenPath}");
            throw new KubernetesAuthException(
                $"Kubernetes service account token not found at: {tokenPath}. This authentication method can only be used within a Kubernetes Pod.",
                ex);
        }
        catch (Exception ex)
        {
            if (options.Debug)
                Console.WriteLine($"[Vault:Auth] Authentication failed: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"[Vault:Auth] Inner: {ex.InnerException.Message}");
            throw;
        }
    }

    class KubernetesAuthException(string message, Exception innerException) : Exception(message, innerException);
}