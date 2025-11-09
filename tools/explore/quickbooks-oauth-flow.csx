// OAuth Token Refresh Diagnostic for QuickBooks Integration
// Tests token refresh flow, expiry handling, and authentication state
// Prerequisite: dotnet build -c Debug ensures binaries in ../src/**/bin/Debug
#nullable enable
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

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Prism.Events;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Events;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSimpleConsole(options => options.SingleLine = true).SetMinimumLevel(LogLevel.Debug));
services.AddSingleton<ISecretVaultService, FakeSecretVaultService>();
services.AddSingleton<SettingsService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SettingsService>>();
    var settings = new SettingsService(configuration: null, logger);

    // Test different token states
    settings.Current.QboAccessToken = "expired-token-123";
    settings.Current.QboRefreshToken = "valid-refresh-token-456";
    settings.Current.QboTokenExpiry = DateTime.UtcNow.AddMinutes(-5); // Expired 5 minutes ago

    return settings;
});
services.AddSingleton<IEventAggregator, EventAggregator>();

var handler = new OAuthStubHttpMessageHandler();

services.AddHttpClient("QBO", client =>
{
    client.BaseAddress = new Uri("https://sandbox-quickbooks.api.intuit.com/");
}).ConfigurePrimaryHttpMessageHandler(() => handler);

var provider = services.BuildServiceProvider();
var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
var qbLogger = loggerFactory.CreateLogger<QuickBooksService>();
var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
var httpClient = httpClientFactory.CreateClient("QBO");

// Create QuickBooksService with expired token
var quickBooksService = new QuickBooksService(
    settings: provider.GetRequiredService<SettingsService>(),
    keyVaultService: provider.GetRequiredService<ISecretVaultService>(),
    logger: qbLogger,
    httpClient: httpClient,
    serviceProvider: provider);

Console.WriteLine("=== OAuth Token Refresh Diagnostic ===\n");

// Test 1: Attempt operation with expired token (should trigger refresh)
Console.WriteLine("Test 1: Sync with expired token (should auto-refresh)");
var budgets = new List<Budget>
{
    new Budget { Id = "1", Name = "FY25", StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(12), Active = true }
};

try
{
    var result = await quickBooksService.SyncBudgetsToAppAsync(budgets, CancellationToken.None);
    Console.WriteLine($"Result: Success={result.Success}, Records={result.RecordsSynced}, Duration={result.Duration.TotalMilliseconds}ms");
    if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
    {
        Console.WriteLine($"Error: {result.ErrorMessage}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Exception: {ex.Message}");
}

// Test 2: Check if token was refreshed in settings
Console.WriteLine("\nTest 2: Verify token refresh in settings");
var settings = provider.GetRequiredService<SettingsService>();
Console.WriteLine($"Access Token: {settings.Current.QboAccessToken?.Substring(0, Math.Min(20, settings.Current.QboAccessToken.Length ?? 0))}...");
Console.WriteLine($"Token Expiry: {settings.Current.QboTokenExpiry}");
Console.WriteLine($"Is Expired: {settings.Current.QboTokenExpiry < DateTime.UtcNow}");

if (provider is IDisposable disposable)
{
    disposable.Dispose();
}

Console.WriteLine("\n=== Diagnostic Complete ===");

// Enhanced HTTP handler that simulates OAuth flow
sealed class OAuthStubHttpMessageHandler : HttpMessageHandler
{
    private bool _tokenRefreshed = false;

    protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"→ {request.Method} {request.RequestUri}");

        if (request.Headers.Authorization != null)
        {
            Console.WriteLine($"  Auth: {request.Headers.Authorization}");
        }

        // First request with expired token should fail with 401
        if (request.RequestUri!.AbsolutePath.Contains("budget") && request.Method == HttpMethod.Post && !_tokenRefreshed)
        {
            Console.WriteLine("  → Budget request with expired token - returning 401 to trigger refresh");
            _tokenRefreshed = true; // Next call will succeed
            return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"invalid_token\",\"error_description\":\"Token expired\"}")
            });
        }

        // OAuth token refresh request
        if (request.RequestUri!.AbsolutePath.Contains("oauth2") && request.Method == HttpMethod.Post)
        {
            Console.WriteLine("  → OAuth token refresh request");
            return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"new-valid-token-789\",\"token_type\":\"bearer\",\"expires_in\":3600,\"refresh_token\":\"updated-refresh-token\"}")
            });
        }

        // Successful budget sync after token refresh
        if (request.RequestUri!.AbsolutePath.Contains("budget") && request.Method == HttpMethod.Post && _tokenRefreshed)
        {
            Console.WriteLine("  → Budget sync with refreshed token");
            return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"Budget\":{\"Id\":\"QB-1001\",\"Name\":\"FY25\",\"Type\":\"ProfitAndLoss\",\"Active\":true}}")
            });
        }

        return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

sealed class FakeSecretVaultService : ISecretVaultService
{
    public string? GetSecret(string key) => key switch
    {
        "QBO_CLIENT_ID" => "test-client-id",
        "QBO_CLIENT_SECRET" => "test-client-secret",
        _ => null
    };
    public void StoreSecret(string key, string value) { }
    public Task<string?> GetSecretAsync(string key) => Task.FromResult(GetSecret(key));
    public Task SetSecretAsync(string key, string value) => Task.CompletedTask;
    public Task RotateSecretAsync(string secretName, string newValue) => Task.CompletedTask;
    public Task MigrateSecretsFromEnvironmentAsync() => Task.CompletedTask;
    public Task PopulateProductionSecretsAsync() => Task.CompletedTask;
    public Task<bool> TestConnectionAsync() => Task.FromResult(true);
    public Task<string> ExportSecretsAsync() => Task.FromResult("{}");
    public Task ImportSecretsAsync(string jsonSecrets) => Task.CompletedTask;
    public Task<IEnumerable<string>> ListSecretKeysAsync() => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    public Task DeleteSecretAsync(string secretName) => Task.CompletedTask;
}
