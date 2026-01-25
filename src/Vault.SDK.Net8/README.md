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

In your `Program.cs`, add the Vault configuration source:

```csharp
using Vault.SDK.Net8.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add Vault as a configuration source
builder.Configuration.AddVault();

```

### 2. Kubernetes Deployment Example

Define your secret references using the `vault:` prefix. Use `Vault__` prefix to override SDK settings via environment
variables.

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-dotnet-app
spec:
  template:
    spec:
      containers:
        - name: app
          image: my-registry/my-app:latest
          env:
            # SDK Configuration (Mapped to VaultOptions)
            - name: Vault__ApiUrl
              value: "http://vault-internal.namespace.svc.cluster.local"
            - name: Vault__Debug
              value: "true"
            - name: Vault__RetryCount
              value: "3"
            
            # Secret References
            - name: POSTGRES_PASSWORD
              value: "vault:db/postgres/password"
            - name: REDIS_CONNECTION
              value: "vault:cache/redis/connection-string"

```


---

## 🛡️ Kubernetes Authentication

The SDK is designed to work seamlessly within Kubernetes clusters. It automatically attempts to authenticate using the
Pod's service account JWT token.

**How it works:**

1. Reads the JWT from `/var/run/secrets/kubernetes.io/serviceaccount/token`.
2. Authenticates via the Vault endpoint.
3. Securely handles token lifecycle and renewal.


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

## 🛠️ Internal Architecture

The provider uses a **Task-based Synchronization** pattern:

1. **Discovery:** Scans environment variables for the `vault:` prefix.
2. **Preload:** Initiates asynchronous `Task` objects for each secret.
3. **Atomic Resolution:** If a key is accessed via `TryGet` before the background task finishes, the caller waits for
   the *existing* task, ensuring only one HTTP request is ever made.

---

## 🛡️ Security

* **No Plaintext Secrets:** Sensitive data is never stored in your repository or configuration files.
* **DNS Awareness:** Uses `SocketsHttpHandler` with a managed connection lifetime to respect DNS changes in
  containerized environments (Kubernetes).
* **Token Lifecycle:** Automatically manages token retrieval and rotation via the configured Auth provider.

---

## 📄 License

This project is licensed under the MIT License.
