# Vault.SDK.Net8

**Vault.SDK.Net8** is a high-performance, resilient client library designed to integrate **Vault.API** secrets directly
into the .NET 8 configuration system. It allows developers to manage sensitive data centrally while consuming it through
the standard `IConfiguration` abstraction.

## 🌟 Key Features

* **Seamless Integration:** Fully hooks into the native `Microsoft.Extensions.Configuration` ecosystem.
* **Request Coalescing:** Prevents "race conditions" during startup. Multiple components requesting the same secret
  share a single background Task, eliminating redundant HTTP calls.
* **Smart Preloading:** Automatically discovers environment variables with the `vault:` prefix and resolves them
  asynchronously in the background.
* **Resilience by Default:** Powered by **Polly**, featuring exponential backoff retries for transient network errors (
  5xx, 408).
* **Thread-Safe:** Built with `ConcurrentDictionary` and asynchronous locking patterns for high-concurrency
  environments.
* **Kubernetes Ready:** Includes built-in support for Kubernetes Auth methods and JWT token handling.

---

## 📦 Installation

Install the package via the NuGet Package Manager:

```bash
dotnet add package Vault.SDK.Net8

```

---

## 🚀 Quick Start

### 1. Register the Provider

In your `Program.cs`, add the Vault configuration source to your builder:

```csharp
using Vault.SDK.Net8.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add Vault as a configuration source
builder.Configuration.AddVault();

```

### 2. Define Secrets in Environment Variables

Map your configuration keys to Vault paths using the `vault:` prefix in your environment settings (e.g., `values.yaml`
for K8s or local `.env`):

| Key           | Value                                             |
|---------------|---------------------------------------------------|
| `DB_PASSWORD` | `vault:secrets/data/production/database#password` |
| `API_KEY`     | `vault:secrets/data/common/services#stripe_key`   |

### 3. Consume Secrets

Access secrets just like any other configuration value. The SDK handles the authentication and resolution transparently.

```csharp
public class PaymentService(IConfiguration configuration)
{
    public void Process()
    {
        // Resolved automatically from Vault
        var apiKey = configuration["API_KEY"];
    }
}

```

---

## ⚙️ Configuration (VaultOptions)

The SDK looks for a `Vault` section in your configuration. You can set these via `appsettings.json` or Environment
Variables:

| Option                | Type       | Default    | Description                                                  |
|-----------------------|------------|------------|--------------------------------------------------------------|
| `ApiUrl`              | `string`   | *Required* | The base URL of your Vault.API instance.                     |
| `RetryCount`          | `int`      | `3`        | Number of retries for failed requests (Exponential Backoff). |
| `HttpTimeout`         | `TimeSpan` | `00:00:30` | Timeout for the underlying HttpClient.                       |
| `FailOnMissingSecret` | `bool`     | `false`    | If true, throws an exception if a secret cannot be resolved. |
| `Debug`               | `bool`     | `false`    | Enables detailed console logging for the resolution process. |

---

## 🛠️ Architecture

The SDK utilizes a **Task-based Cache** mechanism. When the application starts, it performs a "Preload." If a value is
requested via `TryGet` before the background task completes, the provider waits for the *existing* task rather than
spawning a new request. This ensures optimal performance and prevents hitting Vault API rate limits.

---

## 🛡️ Security

* **No Plaintext Secrets:** Sensitive data is never stored in your repository or configuration files.
* **DNS Awareness:** Uses `SocketsHttpHandler` with a managed connection lifetime to respect DNS changes in
  containerized environments (Kubernetes).
* **Token Lifecycle:** Automatically manages token retrieval and rotation via the configured Auth provider.

---

## 📄 License

This project is licensed under the MIT License.
