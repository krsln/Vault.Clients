using Vault.SDK.Net8.Configuration;

var builder = WebApplication.CreateBuilder(args);
// ***********************************************
// if (builder.Environment.IsDevelopment())
// {
//     builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
//     {
//         ["Vault:Debug"] = "true",
//         ["Vault:FailOnMissingSecret"] = "false",
//         ["Vault:RetryCount"] = "3",
//         ["Vault:CacheTtl"] = "00:05:00",
//         ["Vault:HttpTimeout"] = "00:00:30",
//         ["Vault:ApiUrl"] = "http://localhost:5149",
//         ["Vault:KubernetesAuthEndpoint"] = "/api/auth/k8s",
//         ["Vault:SecretReadEndpoint"] = "/api/v1/Secret/read",
//     });
// }

builder.Configuration.AddVault();
// ***********************************************

var app = builder.Build();

// ***********************************************
// Dynamic: Print all configuration key-value pairs
Console.WriteLine($"=== All Configuration Values ===");

void PrintConfiguration(IConfiguration config, string parentKey = "")
{
    foreach (var child in config.GetChildren())
    {
        string key = string.IsNullOrEmpty(parentKey) ? child.Key : $"{parentKey}:{child.Key}";
        if (child.GetChildren().Any())
        {
            // Recursive for nested sections
            PrintConfiguration(child, key);
        }
        else
        {
            Console.WriteLine($"{key} = {child.Value}");
        }
    }
}

PrintConfiguration(app.Configuration);
// ***********************************************

app.MapGet("/", () => "Hello World!");

app.Run();