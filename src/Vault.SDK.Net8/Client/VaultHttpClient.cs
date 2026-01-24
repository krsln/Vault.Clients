using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Vault.SDK.Net8.Interfaces;
using Vault.SDK.Net8.Misc;

namespace Vault.SDK.Net8.Client;

public sealed class VaultHttpClient : IVaultHttpClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ILogger<VaultHttpClient> _logger;

    public VaultHttpClient(HttpClient http, VaultOptions options, ILogger<VaultHttpClient> logger)
    {
        if (string.IsNullOrWhiteSpace(options.ApiUrl))
            throw new ArgumentException("Vault apiUrl must be provided", nameof(options.ApiUrl));

        _logger = logger;
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress = new Uri(options.ApiUrl.TrimEnd('/'));
        _http.Timeout = options.HttpTimeout;

        _json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        _retryPolicy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(options.RetryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (result, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Vault request failed. Retry {Count} after {Seconds}s. StatusCode: {Status}",
                        retryCount, timespan.TotalSeconds, result.Result?.StatusCode);
                });
    }

    public async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await _retryPolicy.ExecuteAsync(() => _http.SendAsync(request, ct));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, _json)!;
    }
}