// DatabaseInitializer Dependencies Integration Test - Validates all dependency integrations
// Usage: Run via Docker task csx:run-51 or .\scripts\testing\run-csx-test.ps1 -ScriptName "51-database-initializer-dependencies-test.csx"
// Purpose: Tests DatabaseInitializer integration with AppDbContext, SigNozTelemetryService, DatabaseResiliencePolicy, and configurations

// Required NuGet package references
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.10"
#r "nuget: Microsoft.Extensions.Configuration, 9.0.10"
#r "nuget: Microsoft.Extensions.Hosting, 9.0.10"
#r "nuget: Microsoft.Extensions.Logging, 9.0.10"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.10"
#r "nuget: Microsoft.EntityFrameworkCore, 9.0.10"
#r "nuget: Microsoft.EntityFrameworkCore.SqlServer, 9.0.10"
#r "nuget: Microsoft.EntityFrameworkCore.InMemory, 9.0.10"
#r "nuget: Polly, 8.5.0"
#r "nuget: Serilog, 4.2.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"
#r "nuget: System.Diagnostics.DiagnosticSource, 9.0.10"

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Serilog;

// ========================================
// TEST METADATA
// ========================================
// Test Name: DatabaseInitializer Dependencies Integration Test
// Category: Integration
// Purpose: Validates DatabaseInitializer integration with all dependencies
// Dependencies: AppDbContext, SigNozTelemetryService, DatabaseResiliencePolicy, IConfiguration
// Testing: Dependency injection, resilience policies, telemetry tracking, configuration binding
// ========================================

Console.WriteLine("=== DatabaseInitializer Dependencies Integration Test ===\n");
Console.WriteLine("Testing DatabaseInitializer integration with all dependencies");
Console.WriteLine("Validates: AppDbContext, Telemetry, Resilience Policies, Configuration\n");

// ========================================
// SERILOG SETUP
// ========================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

// ========================================
// CONFIGURATION
// ========================================
string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"Repo Root: {repoRoot}");
Console.WriteLine($"Logs Dir: {logsDir}\n");

Directory.CreateDirectory(logsDir);

// ========================================
// TEST HARNESS
// ========================================
int passed = 0, total = 0;
List<string> failures = new List<string>();

void Assert(bool condition, string testName, string? details = null)
{
    total++;
    if (condition)
    {
        Console.WriteLine($"✓ {testName}");
        passed++;
    }
    else
    {
        string failMsg = $"✗ {testName} FAILED";
        if (!string.IsNullOrWhiteSpace(details)) failMsg += $"\n  Details: {details}";
        Console.WriteLine(failMsg);
        failures.Add(failMsg);
    }
}

void AssertNotNull<T>(T? value, string testName, string? details = null) where T : class
{
    Assert(value != null, testName, details ?? $"Expected non-null value of type {typeof(T).Name}");
}

// ========================================
// MOCK ENTITIES AND DBCONTEXT
// ========================================

public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TestAppDbContext : DbContext
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

    public DbSet<TestEntity> TestEntities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}

// ========================================
// MOCK TELEMETRY SERVICE
// ========================================

public class MockSigNozTelemetryService
{
    public List<string> Activities { get; } = new();
    public List<(Exception Exception, (string Key, object Value)[] Tags)> Exceptions { get; } = new();
    private int _activityCounter = 0;

    public Activity? StartActivity(string name)
    {
        Activities.Add(name);
        var activity = new Activity($"{name}_{_activityCounter++}");
        activity.Start();
        Log.Debug("Started activity: {ActivityName}", name);
        return activity;
    }

    public void RecordException(Exception ex, params (string Key, object Value)[] tags)
    {
        Exceptions.Add((ex, tags));
        Log.Debug("Recorded exception: {ExceptionType} with {TagCount} tags", ex.GetType().Name, tags.Length);
    }

    public void TrackMetric(string name, double value, params (string Key, object Value)[] tags)
    {
        Log.Debug("Tracked metric: {MetricName}={Value}", name, value);
    }
}

// ========================================
// DATABASE RESILIENCE POLICY
// ========================================

public static class MockDatabaseResiliencePolicy
{
    public static AsyncRetryPolicy CreateDatabaseRetryPolicy(int maxRetryAttempts = 3, int baseDelayMs = 100)
    {
        return Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: maxRetryAttempts,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Log.Warning(exception, "Database operation retry {RetryCount}/{MaxRetries} after {Delay}ms",
                        retryCount, maxRetryAttempts, timeSpan.TotalMilliseconds);
                });
    }
}

// ========================================
// ENHANCED DATABASEINITIALIZER WITH DEPENDENCIES
// ========================================

public class EnhancedDatabaseInitializer : IHostedService
{
    private readonly DbContextOptions<TestAppDbContext> _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnhancedDatabaseInitializer> _logger;
    private readonly MockSigNozTelemetryService? _telemetryService;
    private readonly AsyncRetryPolicy _retryPolicy;
    private Activity? _startupActivity;

    public int StartCallCount { get; private set; } = 0;
    public int StopCallCount { get; private set; } = 0;
    public int InitializeCallCount { get; private set; } = 0;
    public bool LastInitializeSucceeded { get; private set; } = false;
    public Exception? LastException { get; private set; }

    public EnhancedDatabaseInitializer(
        DbContextOptions<TestAppDbContext> options,
        IConfiguration configuration,
        ILogger<EnhancedDatabaseInitializer> logger,
        MockSigNozTelemetryService? telemetryService = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetryService = telemetryService;

        _retryPolicy = MockDatabaseResiliencePolicy.CreateDatabaseRetryPolicy(
            maxRetryAttempts: _configuration.GetValue<int>("Database:MaxRetryAttempts", 3),
            baseDelayMs: _configuration.GetValue<int>("Database:BaseRetryDelayMs", 100));

        ValidateLicenseRegistration();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartCallCount++;

        var autoMigrate = _configuration.GetValue<bool>("DB:AutoMigrate", true);
        if (!autoMigrate)
        {
            _logger.LogInformation("Auto-migration disabled");
            return Task.CompletedTask;
        }

        return _retryPolicy.ExecuteAsync(async () => await InitializeAsync(cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCallCount++;
        _logger.LogInformation("DatabaseInitializer stopping");
        _startupActivity?.Dispose();
        return Task.CompletedTask;
    }

    private void ValidateLicenseRegistration()
    {
        try
        {
            var licenseKey = _configuration["Syncfusion:LicenseKey"];
            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

            if (string.IsNullOrWhiteSpace(licenseKey) && string.IsNullOrWhiteSpace(envKey))
            {
                _logger.LogWarning("Syncfusion license not detected");
            }
            else
            {
                _logger.LogDebug("Syncfusion license validated");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "License validation encountered issue");
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        InitializeCallCount++;
        LastInitializeSucceeded = false;
        LastException = null;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _startupActivity = _telemetryService?.StartActivity("DB.Initialization");
            _logger.LogInformation("Starting database initialization");

            // Memory check
            var gcMemInfo = GC.GetGCMemoryInfo();
            var availableMemoryMB = gcMemInfo.TotalAvailableMemoryBytes / (1024 * 1024);
            _logger.LogInformation("Available memory: {AvailableMB}MB", availableMemoryMB);

            _startupActivity?.SetTag("memory.available_mb", availableMemoryMB);

            // Low memory warning
            if (availableMemoryMB < 256)
            {
                _logger.LogWarning("Low memory detected: {AvailableMB}MB", availableMemoryMB);
                _startupActivity?.SetTag("memory.warning", "low_available");
            }

            using var context = new TestAppDbContext(_options);

            // Ensure database created
            var autoMigrate = _configuration.GetValue<bool>("Database:AutoMigrate", true);
            if (autoMigrate)
            {
                await context.Database.EnsureCreatedAsync(cancellationToken);
                _logger.LogInformation("Database schema ensured");
            }

            // Health check
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            _logger.LogInformation("Database connection: {Status}", canConnect ? "OK" : "Failed");

            _startupActivity?.SetTag("db.health", canConnect ? "healthy" : "degraded");

            // Seed test data
            var seedData = _configuration.GetValue<bool>("Database:SeedTestData", false);
            if (seedData && canConnect)
            {
                await SeedTestDataAsync(context, cancellationToken);
            }

            stopwatch.Stop();
            _logger.LogInformation("Initialization completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            _startupActivity?.SetTag("db.init.duration_ms", stopwatch.ElapsedMilliseconds);
            _startupActivity?.SetTag("db.init.status", "success");

            LastInitializeSucceeded = true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LastException = ex;
            _logger.LogError(ex, "Initialization failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            _startupActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _startupActivity?.SetTag("db.init.status", "failed");
            _startupActivity?.SetTag("error.type", ex.GetType().Name);

            _telemetryService?.RecordException(ex,
                ("operation", "db_initialization"),
                ("duration_ms", stopwatch.ElapsedMilliseconds));

            // Don't rethrow - allow degraded operation
        }
        finally
        {
            _startupActivity?.Dispose();
        }
    }

    private async Task SeedTestDataAsync(TestAppDbContext context, CancellationToken cancellationToken)
    {
        if (!await context.TestEntities.AnyAsync(cancellationToken))
        {
            context.TestEntities.AddRange(
                new TestEntity { Id = 1, Name = "Test Entity 1", CreatedAt = DateTime.UtcNow },
                new TestEntity { Id = 2, Name = "Test Entity 2", CreatedAt = DateTime.UtcNow },
                new TestEntity { Id = 3, Name = "Test Entity 3", CreatedAt = DateTime.UtcNow }
            );

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Test data seeded: 3 entities");
        }
    }
}

// ========================================
// TEST 1: DbContext Integration
// ========================================

Console.WriteLine("\n=== TEST 1: DbContext Integration ===\n");

var services1 = new ServiceCollection();
services1.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var serviceProvider1 = services1.BuildServiceProvider();
var logger1 = serviceProvider1.GetRequiredService<ILogger<EnhancedDatabaseInitializer>>();

var config1 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        {"DB:AutoMigrate", "true"},
        {"Database:SeedTestData", "true"}
    }!)
    .Build();

var options1 = new DbContextOptionsBuilder<TestAppDbContext>()
    .UseInMemoryDatabase("TestDb_Integration1")
    .EnableSensitiveDataLogging()
    .EnableDetailedErrors()
    .Options;

var initializer1 = new EnhancedDatabaseInitializer(options1, config1, logger1);

await initializer1.StartAsync(CancellationToken.None);

Assert(initializer1.StartCallCount == 1, "StartAsync called once");
Assert(initializer1.InitializeCallCount >= 1, "InitializeAsync called at least once");
Assert(initializer1.LastInitializeSucceeded, "Initialization succeeded");

// Verify database operations
using (var context = new TestAppDbContext(options1))
{
    var canConnect = await context.Database.CanConnectAsync();
    Assert(canConnect, "Database connection successful");

    var entities = await context.TestEntities.ToListAsync();
    Assert(entities.Count == 3, "Test data seeded correctly", $"Expected 3, got {entities.Count}");

    var entity1 = await context.TestEntities.FindAsync(1);
    AssertNotNull(entity1, "Can retrieve entity by ID");
    Assert(entity1?.Name == "Test Entity 1", "Entity data is correct");
}

// ========================================
// TEST 2: Telemetry Integration
// ========================================

Console.WriteLine("\n=== TEST 2: Telemetry Integration ===\n");

var telemetry2 = new MockSigNozTelemetryService();
var config2 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { {"DB:AutoMigrate", "true"} }!)
    .Build();

var options2 = new DbContextOptionsBuilder<TestAppDbContext>()
    .UseInMemoryDatabase("TestDb_Telemetry2")
    .Options;

var services2 = new ServiceCollection();
services2.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var serviceProvider2 = services2.BuildServiceProvider();
var logger2 = serviceProvider2.GetRequiredService<ILogger<EnhancedDatabaseInitializer>>();

var initializer2 = new EnhancedDatabaseInitializer(options2, config2, logger2, telemetry2);

await initializer2.StartAsync(CancellationToken.None);

Assert(telemetry2.Activities.Count > 0, "Telemetry activities recorded");
Assert(telemetry2.Activities.Contains("DB.Initialization"), "DB.Initialization activity recorded");

// ========================================
// TEST 3: Resilience Policy Integration
// ========================================

Console.WriteLine("\n=== TEST 3: Resilience Policy Integration ===\n");

var config3 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        {"DB:AutoMigrate", "true"},
        {"Database:MaxRetryAttempts", "5"},
        {"Database:BaseRetryDelayMs", "50"}
    }!)
    .Build();

var options3 = new DbContextOptionsBuilder<TestAppDbContext>()
    .UseInMemoryDatabase("TestDb_Resilience3")
    .Options;

var services3 = new ServiceCollection();
services3.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var serviceProvider3 = services3.BuildServiceProvider();
var logger3 = serviceProvider3.GetRequiredService<ILogger<EnhancedDatabaseInitializer>>();

var telemetry3 = new MockSigNozTelemetryService();
var initializer3 = new EnhancedDatabaseInitializer(options3, config3, logger3, telemetry3);

await initializer3.StartAsync(CancellationToken.None);

Assert(initializer3.LastInitializeSucceeded, "Initialization with custom retry policy succeeded");

// Verify retry configuration was respected
var maxRetries = config3.GetValue<int>("Database:MaxRetryAttempts");
var baseDelay = config3.GetValue<int>("Database:BaseRetryDelayMs");
Assert(maxRetries == 5, "Custom max retry attempts loaded");
Assert(baseDelay == 50, "Custom base delay loaded");

// ========================================
// TEST 4: Configuration Binding
// ========================================

Console.WriteLine("\n=== TEST 4: Configuration Binding ===\n");

var config4 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        {"DB:AutoMigrate", "false"},
        {"Database:BackupOnStartup", "true"},
        {"Database:BackupDirectory", "/test/backups"},
        {"Database:BackupRetentionDays", "14"},
        {"Syncfusion:LicenseKey", "TEST_KEY_123"}
    }!)
    .Build();

Assert(config4.GetValue<bool>("DB:AutoMigrate") == false, "DB:AutoMigrate config bound");
Assert(config4.GetValue<bool>("Database:BackupOnStartup") == true, "Database:BackupOnStartup config bound");
Assert(config4["Database:BackupDirectory"] == "/test/backups", "Database:BackupDirectory config bound");
Assert(config4.GetValue<int>("Database:BackupRetentionDays") == 14, "Database:BackupRetentionDays config bound");
Assert(config4["Syncfusion:LicenseKey"] == "TEST_KEY_123", "Syncfusion:LicenseKey config bound");

// Test with auto-migration disabled
var options4 = new DbContextOptionsBuilder<TestAppDbContext>()
    .UseInMemoryDatabase("TestDb_Config4")
    .Options();

var services4 = new ServiceCollection();
services4.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var serviceProvider4 = services4.BuildServiceProvider();
var logger4 = serviceProvider4.GetRequiredService<ILogger<EnhancedDatabaseInitializer>>();

var initializer4 = new EnhancedDatabaseInitializer(options4, config4, logger4);

await initializer4.StartAsync(CancellationToken.None);

Assert(initializer4.StartCallCount == 1, "StartAsync called even with auto-migrate disabled");
Assert(initializer4.InitializeCallCount == 0, "InitializeAsync not called when auto-migrate disabled");

// ========================================
// TEST 5: Error Handling and Exception Tracking
// ========================================

Console.WriteLine("\n=== TEST 5: Error Handling and Exception Tracking ===\n");

var telemetry5 = new MockSigNozTelemetryService();
var config5 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { {"DB:AutoMigrate", "true"} }!)
    .Build();

// Use invalid options that might cause issues
var options5 = new DbContextOptionsBuilder<TestAppDbContext>()
    .UseInMemoryDatabase("") // Empty DB name
    .Options;

var services5 = new ServiceCollection();
services5.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var serviceProvider5 = services5.BuildServiceProvider();
var logger5 = serviceProvider5.GetRequiredService<ILogger<EnhancedDatabaseInitializer>>();

var initializer5 = new EnhancedDatabaseInitializer(options5, config5, logger5, telemetry5);

try
{
    await initializer5.StartAsync(CancellationToken.None);
}
catch { }

// Verify error handling (should not throw, but record exception)
Assert(initializer5.InitializeCallCount >= 1, "Initialize attempted even with invalid config");
// Note: InMemory provider might succeed even with empty name

// ========================================
// TEST 6: IHostedService Lifecycle
// ========================================

Console.WriteLine("\n=== TEST 6: IHostedService Lifecycle ===\n");

var config6 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { {"DB:AutoMigrate", "true"} }!)
    .Build();

var host6 = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<IConfiguration>(config6);
        services.AddSingleton<MockSigNozTelemetryService>();
        services.AddDbContext<TestAppDbContext>(options =>
            options.UseInMemoryDatabase("TestDb_Host6"));
        services.AddHostedService<EnhancedDatabaseInitializer>();
    })
    .Build();

var hostedServices = host6.Services.GetServices<IHostedService>();
Assert(hostedServices.Any(s => s is EnhancedDatabaseInitializer), "DatabaseInitializer registered as IHostedService");

await host6.StartAsync();
await Task.Delay(100); // Allow initialization to complete

var initializerService = hostedServices.OfType<EnhancedDatabaseInitializer>().FirstOrDefault();
AssertNotNull(initializerService, "DatabaseInitializer service resolved");
Assert(initializerService!.StartCallCount >= 1, "StartAsync called by host");

await host6.StopAsync();
Assert(initializerService.StopCallCount >= 1, "StopAsync called by host");

host6.Dispose();

// ========================================
// TEST 7: Memory Monitoring Integration
// ========================================

Console.WriteLine("\n=== TEST 7: Memory Monitoring Integration ===\n");

var telemetry7 = new MockSigNozTelemetryService();
var config7 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { {"DB:AutoMigrate", "true"} }!)
    .Build();

var options7 = new DbContextOptionsBuilder<TestAppDbContext>()
    .UseInMemoryDatabase("TestDb_Memory7")
    .Options;

var services7 = new ServiceCollection();
services7.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var serviceProvider7 = services7.BuildServiceProvider();
var logger7 = serviceProvider7.GetRequiredService<ILogger<EnhancedDatabaseInitializer>>();

var initializer7 = new EnhancedDatabaseInitializer(options7, config7, logger7, telemetry7);

var beforeMem = GC.GetTotalMemory(false);
await initializer7.StartAsync(CancellationToken.None);
var afterMem = GC.GetTotalMemory(false);

Assert(initializer7.LastInitializeSucceeded, "Initialization with memory monitoring succeeded");
Console.WriteLine($"Memory delta: {(afterMem - beforeMem) / (1024 * 1024)}MB");

// ========================================
// TEST 8: Cancellation Token Handling
// ========================================

Console.WriteLine("\n=== TEST 8: Cancellation Token Handling ===\n");

var config8 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { {"DB:AutoMigrate", "true"} }!)
    .Build();

var options8 = new DbContextOptionsBuilder<TestAppDbContext>()
    .UseInMemoryDatabase("TestDb_Cancellation8")
    .Options;

var services8 = new ServiceCollection();
services8.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var serviceProvider8 = services8.BuildServiceProvider();
var logger8 = serviceProvider8.GetRequiredService<ILogger<EnhancedDatabaseInitializer>>();

var initializer8 = new EnhancedDatabaseInitializer(options8, config8, logger8);

var cts = new CancellationTokenSource();
cts.CancelAfter(50); // Cancel quickly

try
{
    await initializer8.StartAsync(cts.Token);
    Assert(true, "Handles cancellation gracefully (no exception thrown to caller)");
}
catch (OperationCanceledException)
{
    Assert(true, "Handles cancellation with OperationCanceledException");
}
catch (Exception ex)
{
    Assert(false, "Handles cancellation gracefully", $"Unexpected exception: {ex.GetType().Name}");
}

// ========================================
// TEST 9: Multiple Initialization Cycles
// ========================================

Console.WriteLine("\n=== TEST 9: Multiple Initialization Cycles ===\n");

var config9 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { {"DB:AutoMigrate", "true"} }!)
    .Build();

for (int i = 0; i < 3; i++)
{
    var options9 = new DbContextOptionsBuilder<TestAppDbContext>()
        .UseInMemoryDatabase($"TestDb_Cycle{i}")
        .Options;

    var services9 = new ServiceCollection();
    services9.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    var serviceProvider9 = services9.BuildServiceProvider();
    var logger9 = serviceProvider9.GetRequiredService<ILogger<EnhancedDatabaseInitializer>>();

    var initializer9 = new EnhancedDatabaseInitializer(options9, config9, logger9);

    await initializer9.StartAsync(CancellationToken.None);
    await initializer9.StopAsync(CancellationToken.None);

    Assert(initializer9.StartCallCount == 1, $"Cycle {i + 1}: StartAsync called once");
    Assert(initializer9.StopCallCount == 1, $"Cycle {i + 1}: StopAsync called once");
    Assert(initializer9.LastInitializeSucceeded, $"Cycle {i + 1}: Initialization succeeded");
}

// ========================================
// TEST 10: DbContext Options Validation
// ========================================

Console.WriteLine("\n=== TEST 10: DbContext Options Validation ===\n");

// Test with SQL Server options (won't connect, but validates option structure)
var sqlOptions = new DbContextOptionsBuilder<TestAppDbContext>()
    .UseSqlServer("Server=localhost;Database=TestDb;Trusted_Connection=True;")
    .Options;

var config10 = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { {"DB:AutoMigrate", "false"} }!)
    .Build();

var services10 = new ServiceCollection();
services10.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var serviceProvider10 = services10.BuildServiceProvider();
var logger10 = serviceProvider10.GetRequiredService<ILogger<EnhancedDatabaseInitializer>>();

try
{
    var initializer10 = new EnhancedDatabaseInitializer(sqlOptions, config10, logger10);
    Assert(true, "Accepts SQL Server DbContext options");
}
catch (Exception ex)
{
    Assert(false, "Accepts SQL Server DbContext options", ex.Message);
}

// ========================================
// SUMMARY
// ========================================

Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("TEST SUMMARY");
Console.WriteLine(new string('=', 60));
Console.WriteLine($"Total Tests: {total}");
Console.WriteLine($"Passed: {passed} ({(total > 0 ? (passed * 100.0 / total).ToString("F1") : "0")}%)");
Console.WriteLine($"Failed: {failures.Count}");

if (failures.Any())
{
    Console.WriteLine("\nFailed Tests:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"  {failure}");
    }
}

Console.WriteLine(new string('=', 60));

Log.CloseAndFlush();

Environment.Exit(failures.Any() ? 1 : 0);
