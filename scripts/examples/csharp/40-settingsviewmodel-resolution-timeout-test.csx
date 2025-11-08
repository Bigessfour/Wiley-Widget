#!/usr/bin/env dotnet-script
#r "nuget: DryIoc.dll, 5.4.3"
#r "nuget: Microsoft.Extensions.Logging.Abstractions, 9.0.0"
#r "nuget: Microsoft.Extensions.Options, 9.0.0"
#r "nuget: Microsoft.EntityFrameworkCore.InMemory, 9.0.0"

/*
 * SettingsViewModel Resolution Timeout Test
 *
 * Purpose: Validate DryIoc container resolution of SettingsViewModel with timeout handling
 * and error branch coverage. Simulates 10,000 ticks of resolution attempts.
 *
 * Requirements:
 * - Bootstrap minimal DryIoc container in-memory
 * - Register ISettingsService as singleton
 * - Attempt timed resolution (10K tick simulation)
 * - Collect coverage on error branches
 * - Log details if timeout expires
 *
 * References:
 * - App.xaml.cs RegisterTypes() bootstrap logic
 * - CoreModule.cs line 35 for SettingsViewModel registration pattern
 * - DISmokeTests.cs for test pattern
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// ============================================================================
// Mock Types (Minimal stubs for testing)
// ============================================================================

public class AppOptions
{
    public string Theme { get; set; } = "FluentDark";
    public string SyncfusionLicenseKey { get; set; } = string.Empty;
    public string DatabaseConnectionString { get; set; } = string.Empty;
    public string XaiApiKey { get; set; } = string.Empty;
}

public class AppSettings
{
    public int Id { get; set; }
    public string Theme { get; set; } = "FluentDark";
    public bool EnableDataCaching { get; set; } = true;
    public int CacheExpirationMinutes { get; set; } = 30;
    public string SelectedLogLevel { get; set; } = "Information";
}

public interface ISettingsService
{
    string Get(string key);
    void Set(string key, string value);
    AppSettings Current { get; }
    void Save();
}

public class MockSettingsService : ISettingsService
{
    private readonly Dictionary<string, string> _store = new();
    public AppSettings Current { get; } = new AppSettings();

    public string Get(string key) => _store.TryGetValue(key, out var value) ? value : string.Empty;
    public void Set(string key, string value) => _store[key] = value;
    public void Save() { /* No-op for test */ }
}

public interface ISecretVaultService
{
    Task<string?> GetSecretAsync(string key);
}

public class MockSecretVaultService : ISecretVaultService
{
    public Task<string?> GetSecretAsync(string key) => Task.FromResult<string?>($"mock-secret-{key}");
}

public interface IQuickBooksService { }
public class MockQuickBooksService : IQuickBooksService { }

public interface ISyncfusionLicenseService
{
    void RegisterLicense(string licenseKey);
    bool IsLicenseValid();
    Task<bool> ValidateLicenseAsync(string licenseKey);
}

public class MockSyncfusionLicenseService : ISyncfusionLicenseService
{
    public void RegisterLicense(string licenseKey) { }
    public bool IsLicenseValid() => true;
    public Task<bool> ValidateLicenseAsync(string licenseKey) => Task.FromResult(true);
}

public interface IAIService
{
    Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default);
}

public class MockAIService : IAIService
{
    public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
        => Task.FromResult("Mock AI response");
}

public interface IAuditService
{
    Task AuditAsync(string eventName, object payload);
}

public class MockAuditService : IAuditService
{
    public Task AuditAsync(string eventName, object payload) => Task.CompletedTask;
}

public interface IUnitOfWork { }
public class MockUnitOfWork : IUnitOfWork { }

// Minimal AppDbContext stub
public class AppDbContext : IDisposable
{
    public void Dispose() { }
}

// Minimal SettingsViewModel stub (matches constructor signature)
public class SettingsViewModel
{
    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        IOptions<AppOptions> appOptions,
        IOptionsMonitor<AppOptions> appOptionsMonitor,
        IUnitOfWork unitOfWork,
        AppDbContext dbContext,
        ISecretVaultService secretVaultService,
        IQuickBooksService quickBooksService,
        ISyncfusionLicenseService syncfusionLicenseService,
        IAIService aiService,
        IAuditService auditService,
        ISettingsService settingsService,
        object dialogService) // Using object to avoid Prism dependency
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        if (appOptions == null) throw new ArgumentNullException(nameof(appOptions));
        if (appOptionsMonitor == null) throw new ArgumentNullException(nameof(appOptionsMonitor));
        if (unitOfWork == null) throw new ArgumentNullException(nameof(unitOfWork));
        if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));
        if (secretVaultService == null) throw new ArgumentNullException(nameof(secretVaultService));
        if (syncfusionLicenseService == null) throw new ArgumentNullException(nameof(syncfusionLicenseService));
        if (aiService == null) throw new ArgumentNullException(nameof(aiService));
        if (auditService == null) throw new ArgumentNullException(nameof(auditService));
        if (settingsService == null) throw new ArgumentNullException(nameof(settingsService));
        if (dialogService == null) throw new ArgumentNullException(nameof(dialogService));

        Console.WriteLine("✓ SettingsViewModel constructed successfully");
    }
}

// ============================================================================
// Test Execution
// ============================================================================

Console.WriteLine("====================================================================");
Console.WriteLine("SettingsViewModel Resolution Timeout Test with Error Coverage");
Console.WriteLine("====================================================================");
Console.WriteLine();

// Test Configuration
const int TOTAL_TICKS = 10_000;
const int TIMEOUT_MS = 5000;
const int REPORT_INTERVAL = 1000;

var successCount = 0;
var failureCount = 0;
var timeoutCount = 0;
var errorBranchHits = new Dictionary<string, int>
{
    ["MissingDependency"] = 0,
    ["InvalidConfiguration"] = 0,
    ["TimeoutExpired"] = 0,
    ["ContainerException"] = 0,
    ["UnexpectedException"] = 0
};

var elapsedTimes = new List<double>();
var totalStopwatch = Stopwatch.StartNew();

Console.WriteLine($"Starting {TOTAL_TICKS:N0} resolution attempts with {TIMEOUT_MS}ms timeout...");
Console.WriteLine();

for (int tick = 1; tick <= TOTAL_TICKS; tick++)
{
    try
    {
        // Create fresh container for each tick to simulate cold start
        var rules = Rules.Default.WithMicrosoftDependencyInjectionRules();
        var container = new Container(rules);

        // Register all dependencies (per App.xaml.cs RegisterTypes pattern)
        var appOptions = Options.Create(new AppOptions
        {
            Theme = "FluentDark",
            SyncfusionLicenseKey = "test-key-" + tick,
            DatabaseConnectionString = "Server=.;Database=TestDb;Trusted_Connection=True;"
        });

        container.RegisterInstance<ILogger<SettingsViewModel>>(NullLogger<SettingsViewModel>.Instance);
        container.RegisterInstance<IOptions<AppOptions>>(appOptions);
        container.RegisterInstance<IOptionsMonitor<AppOptions>>(
            new OptionsMonitorStub<AppOptions>(appOptions.Value));
        container.RegisterInstance<IUnitOfWork>(new MockUnitOfWork());
        container.RegisterInstance<AppDbContext>(new AppDbContext());
        container.RegisterInstance<ISecretVaultService>(new MockSecretVaultService());
        container.RegisterInstance<IQuickBooksService>(new MockQuickBooksService());
        container.RegisterInstance<ISyncfusionLicenseService>(new MockSyncfusionLicenseService());
        container.RegisterInstance<IAIService>(new MockAIService());
        container.RegisterInstance<IAuditService>(new MockAuditService());

        // Register ISettingsService as SINGLETON (per requirements)
        container.RegisterInstance<ISettingsService>(new MockSettingsService());

        // Mock dialog service (using object to avoid Prism dependency)
        container.RegisterInstance<object>(new object());

        // Register SettingsViewModel as Transient (per CoreModule.cs)
        container.Register<SettingsViewModel>(reuse: Reuse.Transient);

        // Attempt timed resolution
        var sw = Stopwatch.StartNew();

        using var cts = new CancellationTokenSource(TIMEOUT_MS);
        var resolutionTask = Task.Run(() =>
        {
            try
            {
                return container.Resolve<SettingsViewModel>();
            }
            catch (ContainerException cex)
            {
                errorBranchHits["ContainerException"]++;
                throw new Exception($"Container resolution failed: {cex.Message}", cex);
            }
            catch (Exception)
            {
                errorBranchHits["UnexpectedException"]++;
                throw;
            }
        }, cts.Token);

        try
        {
            var vm = await resolutionTask;
            sw.Stop();
            elapsedTimes.Add(sw.Elapsed.TotalMilliseconds);
            successCount++;
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            errorBranchHits["TimeoutExpired"]++;
            timeoutCount++;
            Console.WriteLine($"[TIMEOUT] Tick {tick}: Resolution exceeded {TIMEOUT_MS}ms timeout");
        }
        catch (Exception ex)
        {
            sw.Stop();
            failureCount++;
            Console.WriteLine($"[ERROR] Tick {tick}: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            container.Dispose();
        }
    }
    catch (Exception ex)
    {
        failureCount++;
        errorBranchHits["UnexpectedException"]++;
        Console.WriteLine($"[CRITICAL] Tick {tick}: Unexpected outer exception - {ex.Message}");
    }

    // Progress reporting
    if (tick % REPORT_INTERVAL == 0)
    {
        var progress = (tick / (double)TOTAL_TICKS) * 100;
        var avgTime = elapsedTimes.Any() ? elapsedTimes.Average() : 0;
        Console.WriteLine($"Progress: {progress:F1}% ({tick:N0}/{TOTAL_TICKS:N0}) | Avg: {avgTime:F2}ms | Success: {successCount} | Failures: {failureCount} | Timeouts: {timeoutCount}");
    }
}

totalStopwatch.Stop();

// ============================================================================
// Results Summary
// ============================================================================

Console.WriteLine();
Console.WriteLine("====================================================================");
Console.WriteLine("TEST RESULTS");
Console.WriteLine("====================================================================");
Console.WriteLine($"Total Ticks:       {TOTAL_TICKS:N0}");
Console.WriteLine($"Success Count:     {successCount:N0} ({(successCount / (double)TOTAL_TICKS) * 100:F2}%)");
Console.WriteLine($"Failure Count:     {failureCount:N0} ({(failureCount / (double)TOTAL_TICKS) * 100:F2}%)");
Console.WriteLine($"Timeout Count:     {timeoutCount:N0} ({(timeoutCount / (double)TOTAL_TICKS) * 100:F2}%)");
Console.WriteLine();

Console.WriteLine("Performance Metrics:");
if (elapsedTimes.Any())
{
    Console.WriteLine($"  Min Resolution:  {elapsedTimes.Min():F2}ms");
    Console.WriteLine($"  Max Resolution:  {elapsedTimes.Max():F2}ms");
    Console.WriteLine($"  Avg Resolution:  {elapsedTimes.Average():F2}ms");
    Console.WriteLine($"  Med Resolution:  {elapsedTimes.OrderBy(t => t).ElementAt(elapsedTimes.Count / 2):F2}ms");
    Console.WriteLine($"  P95 Resolution:  {elapsedTimes.OrderBy(t => t).ElementAt((int)(elapsedTimes.Count * 0.95)):F2}ms");
    Console.WriteLine($"  P99 Resolution:  {elapsedTimes.OrderBy(t => t).ElementAt((int)(elapsedTimes.Count * 0.99)):F2}ms");
}
else
{
    Console.WriteLine("  No successful resolutions to analyze");
}

Console.WriteLine();
Console.WriteLine("Error Branch Coverage:");
foreach (var branch in errorBranchHits.OrderByDescending(b => b.Value))
{
    var percentage = (branch.Value / (double)TOTAL_TICKS) * 100;
    Console.WriteLine($"  {branch.Key,-25} {branch.Value,6:N0} hits ({percentage,6:F2}%)");
}

Console.WriteLine();
Console.WriteLine($"Total Test Duration: {totalStopwatch.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"Throughput:          {TOTAL_TICKS / totalStopwatch.Elapsed.TotalSeconds:F0} resolutions/sec");

// ============================================================================
// Validation & Exit Code
// ============================================================================

Console.WriteLine();
Console.WriteLine("====================================================================");
Console.WriteLine("VALIDATION");
Console.WriteLine("====================================================================");

var allPassed = true;

// Validation 1: No timeouts occurred
if (timeoutCount > 0)
{
    Console.WriteLine($"❌ FAIL: {timeoutCount} resolution(s) exceeded {TIMEOUT_MS}ms timeout");
    allPassed = false;
}
else
{
    Console.WriteLine($"✅ PASS: All resolutions completed within {TIMEOUT_MS}ms timeout");
}

// Validation 2: Success rate above 95%
var successRate = (successCount / (double)TOTAL_TICKS) * 100;
if (successRate < 95.0)
{
    Console.WriteLine($"❌ FAIL: Success rate {successRate:F2}% is below 95% threshold");
    allPassed = false;
}
else
{
    Console.WriteLine($"✅ PASS: Success rate {successRate:F2}% meets 95% threshold");
}

// Validation 3: Average resolution time < 100ms
var avgResolution = elapsedTimes.Any() ? elapsedTimes.Average() : double.MaxValue;
if (avgResolution >= 100.0)
{
    Console.WriteLine($"❌ FAIL: Average resolution {avgResolution:F2}ms exceeds 100ms target");
    allPassed = false;
}
else
{
    Console.WriteLine($"✅ PASS: Average resolution {avgResolution:F2}ms is within 100ms target");
}

// Validation 4: Error branches covered
var totalErrorBranchHits = errorBranchHits.Values.Sum();
if (totalErrorBranchHits == 0)
{
    Console.WriteLine("⚠️  WARN: No error branches hit (limited coverage)");
}
else
{
    Console.WriteLine($"✅ INFO: Error branch coverage: {totalErrorBranchHits:N0} hits across {errorBranchHits.Count} branches");
}

Console.WriteLine();
Console.WriteLine(allPassed ? "🎉 ALL VALIDATIONS PASSED" : "💥 SOME VALIDATIONS FAILED");
Console.WriteLine("====================================================================");

Environment.Exit(allPassed ? 0 : 1);

// ============================================================================
// Helper Classes
// ============================================================================

public class OptionsMonitorStub<T> : IOptionsMonitor<T>
{
    private readonly T _currentValue;

    public OptionsMonitorStub(T currentValue)
    {
        _currentValue = currentValue;
    }

    public T CurrentValue => _currentValue;
    public T Get(string? name) => _currentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
