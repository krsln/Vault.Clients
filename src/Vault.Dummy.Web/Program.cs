var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ***********************************************
var secretsPath = "/vault/env/secrets.json";
if (File.Exists(secretsPath))
{
    builder.Configuration
        .AddJsonFile(secretsPath, optional: false, reloadOnChange: true);
}

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