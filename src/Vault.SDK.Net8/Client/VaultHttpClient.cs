using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Vault.SDK.Net8.Interfaces;
using Vault.SDK.Net8.Misc;

namespace Vault.SDK.Net8.Client;

public class VaultHttpClient : IVaultHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly VaultOptions _options;
    private readonly ILogger<VaultHttpClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public VaultHttpClient(HttpClient httpClient, VaultOptions options, ILogger<VaultHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Set global timeout
        _httpClient.Timeout = _options.HttpTimeout;

        // Define retry policy: Retry on transient HTTP errors (5xx, 408) or timeouts, up to RetryCount times, with exponential backoff
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(response =>
                (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(_options.RetryCount,
                retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff: 1s, 2s, 4s, etc.
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    _logger?.LogWarning(
                        $"Retry {retryAttempt} after {timespan.TotalSeconds}s due to: {outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase}");
                });
    }

    public async Task<T?> SendAsync<T>(HttpRequestMessage requestTemplate, CancellationToken ct)
    {
        // Use Polly to execute with retries, recreating the request each time
        var response = await _retryPolicy.ExecuteAsync(async () =>
        {
            // IMPORTANT: Clone or recreate the request for each attempt to avoid "already sent" error
            var request = await CloneRequestAsync(requestTemplate); // Clone method below
            return await _httpClient.SendAsync(request, ct);
        });

        response.EnsureSuccessStatusCode(); // Throw if not 2xx

        // Assuming deserialization logic here (e.g., using System.Text.Json)
        var content = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(content);
    }

    // Helper to clone HttpRequestMessage (including content if any)
    private async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        // Copy headers
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content if present (e.g., for POST with body)
        if (original.Content != null)
        {
            var contentStream = await original.Content.ReadAsStreamAsync();
            contentStream.Position = 0; // Reset stream if reusable
            clone.Content = new StreamContent(contentStream);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        clone.Version = original.Version;
        clone.VersionPolicy = original.VersionPolicy;

        return clone;
    }
}