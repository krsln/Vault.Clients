using Vault.SDK.Net8.Configuration;

var builder = WebApplication.CreateBuilder(args);
// ***********************************************
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