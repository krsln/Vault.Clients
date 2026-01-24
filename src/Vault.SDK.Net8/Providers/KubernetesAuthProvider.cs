using System.Text;
using System.Text.Json;
using Vault.SDK.Net8.DTO;
using Vault.SDK.Net8.Interfaces;

namespace Vault.SDK.Net8.Providers;

public sealed class KubernetesAuthProvider(
    IVaultHttpClient http,
    string tokenPath = "/var/run/secrets/kubernetes.io/serviceaccount/token")
    : IAuthProvider
{
    public async Task<AuthResponse> AuthenticateAsync(CancellationToken ct)
    {
        try
        {
            var jwt = await File.ReadAllTextAsync(tokenPath, ct);
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/k8s")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { jwt }), Encoding.UTF8, "application/json")
            };
            return await http.SendAsync<AuthResponse>(request, ct);
        }
        catch (FileNotFoundException ex)
        {
            throw new KubernetesAuthException(
                $"Kubernetes service account token not found at: {tokenPath}. This authentication method can only be used within a Kubernetes Pod.",
                ex);
        }
    }

    class KubernetesAuthException(string message, Exception innerException) : Exception(message, innerException);
}