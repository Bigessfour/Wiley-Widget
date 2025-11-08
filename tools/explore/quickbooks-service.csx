// Exploratory harness for QuickBooksService focusing on SyncBudgetsToAppAsync behavior
// Prerequisite: dotnet build -c Debug ensures binaries in ../src/**/bin/Debug
#nullable enable

// === AUTO-PATH RESOLUTION FOR DOCKER & LOCAL ===
#if DOCKER
#r "/src/src/WileyWidget.Models/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Models.dll"
#r "/src/src/WileyWidget.Services.Abstractions/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Services.Abstractions.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Services.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Prism.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Prism.Events.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Intuit.Ipp.Data.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Microsoft.Extensions.DependencyInjection.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.DependencyInjection.Abstractions.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.Logging.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.Logging.Abstractions.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.Logging.Console.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.Http.dll"
#r "/src/src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Microsoft.Extensions.Diagnostics.dll"
#else
#r "../../src/WileyWidget.Models/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Models.dll"
#r "../../src/WileyWidget.Services.Abstractions/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Services.Abstractions.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/WileyWidget.Services.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Prism.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Prism.Events.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Intuit.Ipp.Data.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Microsoft.Extensions.DependencyInjection.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.DependencyInjection.Abstractions.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.Logging.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.Logging.Abstractions.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.Logging.Console.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/Microsoft.Extensions.Http.dll"
#r "../../src/WileyWidget.Services/bin/Debug/net9.0-windows10.0.19041.0/win-x64/Microsoft.Extensions.Diagnostics.dll"
#endif

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Prism.Events;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Events;

using System.IO;
using System.Runtime.Loader;

// Preload native/binary dependencies from the built project's output so runtime
// type resolution (Serilog, Prism, etc.) works when dotnet-script executes.
var binCandidates = new[] {
    "/src/src/WileyWidget/bin/Debug/net9.0-windows10.0.19041.0/win-x64",
    "../../src/WileyWidget/bin/Debug/net9.0-windows10.0.19041.0/win-x64"
};
foreach (var candidate in binCandidates)
{
    try
    {
        if (!Directory.Exists(candidate)) continue;
        foreach (var dll in Directory.GetFiles(candidate, "*.dll"))
        {
            try { AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll)); Console.WriteLine($"Preloaded: {Path.GetFileName(dll)}"); } catch { }
        }
        break;
    }
    catch { }
}

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(options => options.SingleLine = true).SetMinimumLevel(LogLevel.Debug));
services.AddSingleton<ISecretVaultService, FakeSecretVaultService>();
services.AddSingleton<SettingsService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SettingsService>>();
    var settings = new SettingsService(configuration: null, logger);
    settings.Current.QboAccessToken = "fake-token";
    settings.Current.QboRefreshToken = "fake-refresh";
    settings.Current.QboTokenExpiry = DateTime.UtcNow.AddHours(1);
    return settings;
});
services.AddSingleton<IEventAggregator, EventAggregator>();

var handler = new StubHttpMessageHandler();

services.AddHttpClient("QBO", client =>
{
    client.BaseAddress = new Uri("https://sandbox-quickbooks.api.intuit.com/");
}).ConfigurePrimaryHttpMessageHandler(() => handler);

var provider = services.BuildServiceProvider();
var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
var qbLogger = loggerFactory.CreateLogger<QuickBooksService>();
var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
var httpClient = httpClientFactory.CreateClient("QBO");

// Create QuickBooksService with proper DI setup
var quickBooksService = new QuickBooksService(
    settings: provider.GetRequiredService<SettingsService>(),
    keyVaultService: provider.GetRequiredService<ISecretVaultService>(),
    logger: qbLogger,
    httpClient: httpClient,
    serviceProvider: provider);

// Don't set private fields - let the service initialize properly
// The service will load credentials from the secret vault or environment

var budgets = new List<Budget>
{
    new Budget { Id = "1", Name = "FY25", StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(12), Active = true }
};

Console.WriteLine("Testing SyncBudgetsToAppAsync...");
try
{
    var result = await quickBooksService.SyncBudgetsToAppAsync(budgets, CancellationToken.None);
    Console.WriteLine($"Sync success: {result.Success}, records synced: {result.RecordsSynced}");
    if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
    {
        Console.WriteLine($"Error: {result.ErrorMessage}");
    }
    Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds}ms");
}
catch (Exception ex)
{
    Console.WriteLine($"Exception during sync: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

if (provider is IDisposable disposable)
{
    disposable.Dispose();
}

sealed class StubHttpMessageHandler : HttpMessageHandler
{
    protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"→ {request.Method} {request.RequestUri}");

        if (request.Headers.Authorization != null)
        {
            Console.WriteLine($"  Auth: {request.Headers.Authorization}");
        }

        // Simulate OAuth token refresh
        if (request.RequestUri!.AbsolutePath.Contains("oauth2") && request.Method == HttpMethod.Post)
        {
            Console.WriteLine("  → OAuth token refresh request");
            return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"refreshed-token-123\",\"token_type\":\"bearer\",\"expires_in\":3600,\"refresh_token\":\"new-refresh-token\"}")
            });
        }

        // Simulate budget creation/sync
        if (request.RequestUri!.AbsolutePath.Contains("budget") && request.Method == HttpMethod.Post)
        {
            Console.WriteLine("  → Budget sync request");
            return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"Budget\":{\"Id\":\"QB-1001\",\"Name\":\"FY25\",\"Type\":\"ProfitAndLoss\",\"Active\":true}}")
            });
        }

        // Simulate company info request
        if (request.RequestUri!.AbsolutePath.Contains("companyinfo") && request.Method == HttpMethod.Get)
        {
            Console.WriteLine("  → Company info request");
            return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"CompanyInfo\":{\"Id\":\"123145796822988\",\"CompanyName\":\"Wiley Widget Corp\"}}")
            });
        }

        // Default response for other requests
        Console.WriteLine("  → Unhandled request - returning 404");
        return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

sealed class FakeSecretVaultService : ISecretVaultService
{
    public string? GetSecret(string key) => null;
    public void StoreSecret(string key, string value) { }
    public System.Threading.Tasks.Task<string?> GetSecretAsync(string key) => System.Threading.Tasks.Task.FromResult<string?>(null);
    public System.Threading.Tasks.Task SetSecretAsync(string key, string value) => System.Threading.Tasks.Task.CompletedTask;
    public System.Threading.Tasks.Task RotateSecretAsync(string secretName, string newValue) => System.Threading.Tasks.Task.CompletedTask;
    public System.Threading.Tasks.Task MigrateSecretsFromEnvironmentAsync() => System.Threading.Tasks.Task.CompletedTask;
    public System.Threading.Tasks.Task PopulateProductionSecretsAsync() => System.Threading.Tasks.Task.CompletedTask;
    public System.Threading.Tasks.Task<bool> TestConnectionAsync() => System.Threading.Tasks.Task.FromResult(true);
    public System.Threading.Tasks.Task<string> ExportSecretsAsync() => System.Threading.Tasks.Task.FromResult("{}");
    public System.Threading.Tasks.Task ImportSecretsAsync(string jsonSecrets) => System.Threading.Tasks.Task.CompletedTask;
    public System.Threading.Tasks.Task<IEnumerable<string>> ListSecretKeysAsync() => System.Threading.Tasks.Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    public System.Threading.Tasks.Task DeleteSecretAsync(string secretName) => System.Threading.Tasks.Task.CompletedTask;
}

static void SetPrivateField<TValue>(object instance, string fieldName, TValue value)
{
    var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
    if (field == null)
    {
        throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");
    }

    field.SetValue(instance, value);
}
