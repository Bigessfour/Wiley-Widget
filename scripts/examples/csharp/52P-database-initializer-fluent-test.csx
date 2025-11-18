// DatabaseInitializer FluentAssertions-Style Test - Enterprise-grade CSX testing
// Usage: docker run --rm -v "${PWD}:/app:ro" -e WW_REPO_ROOT=/app wiley-widget/csx-mcp:local scripts/examples/csharp/52-database-initializer-fluent-test.csx
// Purpose: Demonstrates FluentAssertions-style testing in CSX with full xUnit feature parity

// Required NuGet package references
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.10"
#r "nuget: Microsoft.Extensions.Configuration, 9.0.10"
#r "nuget: Microsoft.Extensions.Hosting, 9.0.10"
#r "nuget: Microsoft.Extensions.Logging, 9.0.10"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.10"
#r "nuget: Microsoft.EntityFrameworkCore, 9.0.10"
#r "nuget: Microsoft.EntityFrameworkCore.InMemory, 9.0.10"
#r "nuget: FluentAssertions, 7.0.0"
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
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using FluentAssertions.Execution;
using Polly;
using Polly.Retry;
using Serilog;

// ========================================
// TEST METADATA
// ========================================
// Test Name: DatabaseInitializer FluentAssertions-Style Test
// Category: Integration, Unit, Performance
// Purpose: Demonstrates enterprise-grade CSX testing with FluentAssertions feature parity
// Dependencies: FluentAssertions, xUnit-style patterns, parameterized tests
// Testing: All DatabaseInitializer methods with fluent syntax and comprehensive assertions
// ========================================

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   DatabaseInitializer FluentAssertions-Style Test Suite      ║");
Console.WriteLine("║   Enterprise-Grade CSX Testing with xUnit Feature Parity     ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

// ========================================
// SERILOG SETUP
// ========================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// ========================================
// CONFIGURATION
// ========================================
string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"📁 Repo Root: {repoRoot}");
Console.WriteLine($"📂 Logs Dir: {logsDir}\n");

Directory.CreateDirectory(logsDir);

// ========================================
// FLUENT TEST HARNESS - xUnit Style
// ========================================

public enum TestCategory
{
    Unit,
    Integration,
    Performance,
    Security,
    Resilience
}

public class TestResult
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public bool Passed { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public long DurationMs { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class FluentTestHarness
{
    private readonly List<TestResult> _results = new();
    private readonly Stopwatch _suiteTimer = Stopwatch.StartNew();
    private TestResult? _currentTest;
    private Stopwatch? _testTimer;

    public int TotalTests => _results.Count;
    public int PassedTests => _results.Count(r => r.Passed);
    public int FailedTests => _results.Count(r => !r.Passed);
    public double PassRate => TotalTests > 0 ? (PassedTests * 100.0 / TotalTests) : 0;

    // xUnit-style [Fact] equivalent
    public async Task Fact(string name, TestCategory category, Func<Task> testAction)
    {
        await ExecuteTest(name, category.ToString(), testAction);
    }

    // xUnit-style [Theory] equivalent with parameters
    public async Task Theory<T>(string name, TestCategory category, T[] testData, Func<T, Task> testAction)
    {
        for (int i = 0; i < testData.Length; i++)
        {
            var paramName = $"{name} [Case {i + 1}: {testData[i]}]";
            await ExecuteTest(paramName, category.ToString(), async () => await testAction(testData[i]));
        }
    }

    // xUnit-style async test execution
    private async Task ExecuteTest(string name, string category, Func<Task> testAction)
    {
        _currentTest = new TestResult { Name = name, Category = category };
        _testTimer = Stopwatch.StartNew();

        try
        {
            Console.WriteLine($"\n▶ Running: {name}");
            await testAction();

            _testTimer.Stop();
            _currentTest.Passed = true;
            _currentTest.DurationMs = _testTimer.ElapsedMilliseconds;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ PASSED: {name} ({_testTimer.ElapsedMilliseconds}ms)");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            _testTimer?.Stop();
            _currentTest.Passed = false;
            _currentTest.ErrorMessage = ex.Message;
            _currentTest.StackTrace = ex.StackTrace;
            _currentTest.DurationMs = _testTimer?.ElapsedMilliseconds ?? 0;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ FAILED: {name}");
            Console.WriteLine($"  Error: {ex.Message}");
            if (ex.StackTrace != null)
            {
                Console.WriteLine($"  Stack: {ex.StackTrace.Split('\n').FirstOrDefault()}");
            }
            Console.ResetColor();
        }

        _results.Add(_currentTest);
    }

    public void AddMetadata(string key, object value)
    {
        _currentTest?.Metadata.Add(key, value);
    }

    public void PrintSummary()
    {
        _suiteTimer.Stop();

        Console.WriteLine("\n" + new string('═', 70));
        Console.WriteLine("TEST EXECUTION SUMMARY");
        Console.WriteLine(new string('═', 70));
        Console.WriteLine($"Total Tests:    {TotalTests}");
        Console.WriteLine($"✓ Passed:       {PassedTests} ({PassRate:F1}%)");
        Console.WriteLine($"✗ Failed:       {FailedTests}");
        Console.WriteLine($"⏱ Total Time:   {_suiteTimer.ElapsedMilliseconds}ms");
        Console.WriteLine($"⚡ Avg Time:     {(TotalTests > 0 ? _suiteTimer.ElapsedMilliseconds / TotalTests : 0)}ms per test");
        Console.WriteLine(new string('═', 70));

        // Group by category
        var byCategory = _results.GroupBy(r => r.Category);
        Console.WriteLine("\nRESULTS BY CATEGORY:");
        foreach (var group in byCategory)
        {
            var passed = group.Count(r => r.Passed);
            var total = group.Count();
            Console.WriteLine($"  {group.Key,-15} {passed}/{total} passed");
        }

        // Failed tests detail
        if (FailedTests > 0)
        {
            Console.WriteLine("\nFAILED TESTS:");
            foreach (var failed in _results.Where(r => !r.Passed))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ {failed.Name}");
                Console.WriteLine($"    {failed.ErrorMessage}");
                Console.ResetColor();
            }
        }

        // Performance insights
        Console.WriteLine("\nPERFORMANCE INSIGHTS:");
        var slowTests = _results.OrderByDescending(r => r.DurationMs).Take(3);
        foreach (var test in slowTests)
        {
            Console.WriteLine($"  {test.DurationMs}ms - {test.Name}");
        }

        Console.WriteLine(new string('═', 70));
    }

    public int GetExitCode() => FailedTests > 0 ? 1 : 0;
}

// ========================================
// TEST SETUP - Reusable Fixtures
// ========================================

public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    public DbSet<TestEntity> TestEntities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>().HasKey(e => e.Id);
        base.OnModelCreating(modelBuilder);
    }
}

public class MockTelemetryService
{
    public List<string> Activities { get; } = new();
    public List<Exception> Exceptions { get; } = new();

    public Activity? StartActivity(string name)
    {
        Activities.Add(name);
        return new Activity(name).Start();
    }

    public void RecordException(Exception ex, params (string, object)[] tags)
    {
        Exceptions.Add(ex);
    }
}

public static class TestResiliencePolicy
{
    public static AsyncRetryPolicy CreatePolicy(int retries = 3, int delayMs = 100)
    {
        return Policy.Handle<Exception>()
            .WaitAndRetryAsync(retries, attempt => TimeSpan.FromMilliseconds(delayMs * Math.Pow(2, attempt - 1)));
    }
}

public class SimpleDatabaseInitializer : IHostedService
{
    private readonly DbContextOptions<TestDbContext> _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SimpleDatabaseInitializer> _logger;
    private readonly MockTelemetryService? _telemetry;
    private readonly AsyncRetryPolicy _retryPolicy;

    public bool StartCalled { get; private set; }
    public bool StopCalled { get; private set; }
    public bool InitializeSucceeded { get; private set; }
    public long InitializeDurationMs { get; private set; }

    public SimpleDatabaseInitializer(
        DbContextOptions<TestDbContext> options,
        IConfiguration configuration,
        ILogger<SimpleDatabaseInitializer> logger,
        MockTelemetryService? telemetry = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetry = telemetry;
        _retryPolicy = TestResiliencePolicy.CreatePolicy();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        StartCalled = true;

        if (!_configuration.GetValue<bool>("DB:AutoMigrate", true))
        {
            return;
        }

        await _retryPolicy.ExecuteAsync(async () => await InitializeAsync(cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCalled = true;
        return Task.CompletedTask;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var activity = _telemetry?.StartActivity("DB.Initialization");

            using var context = new TestDbContext(_options);
            await context.Database.EnsureCreatedAsync(cancellationToken);

            var canConnect = await context.Database.CanConnectAsync(cancellationToken);

            sw.Stop();
            InitializeDurationMs = sw.ElapsedMilliseconds;
            InitializeSucceeded = canConnect;

            activity?.Dispose();
        }
        catch (Exception ex)
        {
            sw.Stop();
            InitializeDurationMs = sw.ElapsedMilliseconds;
            InitializeSucceeded = false;
            _telemetry?.RecordException(ex, ("operation", "initialization"));
            throw;
        }
    }
}

// ========================================
// TEST FIXTURES AND BUILDERS
// ========================================

public class DatabaseInitializerBuilder
{
    private DbContextOptions<TestDbContext>? _options;
    private IConfiguration? _configuration;
    private ILogger<SimpleDatabaseInitializer>? _logger;
    private MockTelemetryService? _telemetry;

    public static DatabaseInitializerBuilder Create() => new();

    public DatabaseInitializerBuilder WithInMemoryDatabase(string dbName)
    {
        _options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return this;
    }

    public DatabaseInitializerBuilder WithConfiguration(Dictionary<string, string?> config)
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
        return this;
    }

    public DatabaseInitializerBuilder WithDefaultConfiguration()
    {
        return WithConfiguration(new Dictionary<string, string?>
        {
            {"DB:AutoMigrate", "true"}
        });
    }

    public DatabaseInitializerBuilder WithTelemetry()
    {
        _telemetry = new MockTelemetryService();
        return this;
    }

    public DatabaseInitializerBuilder WithLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var provider = services.BuildServiceProvider();
        _logger = provider.GetRequiredService<ILogger<SimpleDatabaseInitializer>>();
        return this;
    }

    public SimpleDatabaseInitializer Build()
    {
        if (_logger == null) WithLogger();
        if (_configuration == null) WithDefaultConfiguration();
        if (_options == null) WithInMemoryDatabase($"TestDb_{Guid.NewGuid()}");

        return new SimpleDatabaseInitializer(_options!, _configuration!, _logger!, _telemetry);
    }

    public MockTelemetryService? GetTelemetry() => _telemetry;
}

// ========================================
// INITIALIZE TEST HARNESS
// ========================================

var harness = new FluentTestHarness();

// ========================================
// UNIT TESTS - Constructor Validation
// ========================================

await harness.Fact("Constructor_WithValidParameters_ShouldSucceed", TestCategory.Unit, async () =>
{
    // Arrange & Act
    var initializer = DatabaseInitializerBuilder.Create()
        .WithInMemoryDatabase("ValidConstructor")
        .WithDefaultConfiguration()
        .WithLogger()
        .Build();

    // Assert - FluentAssertions style
    initializer.Should().NotBeNull();
    initializer.StartCalled.Should().BeFalse();
    initializer.StopCalled.Should().BeFalse();
});

await harness.Fact("Constructor_WithNullOptions_ShouldThrowArgumentNullException", TestCategory.Unit, async () =>
{
    // Arrange
    var config = new ConfigurationBuilder().Build();
    var logger = new ServiceCollection()
        .AddLogging(b => b.AddConsole())
        .BuildServiceProvider()
        .GetRequiredService<ILogger<SimpleDatabaseInitializer>>();

    // Act & Assert
    Action act = () => new SimpleDatabaseInitializer(null!, config, logger);
    act.Should().Throw<ArgumentNullException>()
        .WithParameterName("options");
});

await harness.Fact("Constructor_WithNullConfiguration_ShouldThrowArgumentNullException", TestCategory.Unit, async () =>
{
    // Arrange
    var options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase("NullConfig")
        .Options;
    var logger = new ServiceCollection()
        .AddLogging(b => b.AddConsole())
        .BuildServiceProvider()
        .GetRequiredService<ILogger<SimpleDatabaseInitializer>>();

    // Act & Assert
    Action act = () => new SimpleDatabaseInitializer(options, null!, logger);
    act.Should().Throw<ArgumentNullException>()
        .WithParameterName("configuration");
});

// ========================================
// INTEGRATION TESTS - Lifecycle
// ========================================

await harness.Fact("StartAsync_WithAutoMigrateEnabled_ShouldInitializeDatabase", TestCategory.Integration, async () =>
{
    // Arrange
    var initializer = DatabaseInitializerBuilder.Create()
        .WithInMemoryDatabase("StartAsyncTest")
        .WithDefaultConfiguration()
        .Build();

    // Act
    await initializer.StartAsync(CancellationToken.None);

    // Assert
    initializer.StartCalled.Should().BeTrue();
    initializer.InitializeSucceeded.Should().BeTrue();
    initializer.InitializeDurationMs.Should().BeGreaterThan(0);
});

await harness.Fact("StartAsync_WithAutoMigrateDisabled_ShouldSkipInitialization", TestCategory.Integration, async () =>
{
    // Arrange
    var initializer = DatabaseInitializerBuilder.Create()
        .WithInMemoryDatabase("SkipInitTest")
        .WithConfiguration(new Dictionary<string, string?> { {"DB:AutoMigrate", "false"} })
        .Build();

    // Act
    await initializer.StartAsync(CancellationToken.None);

    // Assert
    initializer.StartCalled.Should().BeTrue();
    initializer.InitializeSucceeded.Should().BeFalse("initialization should be skipped");
});

await harness.Fact("StopAsync_AfterStart_ShouldSetStopCalled", TestCategory.Integration, async () =>
{
    // Arrange
    var initializer = DatabaseInitializerBuilder.Create()
        .WithInMemoryDatabase("StopTest")
        .Build();

    await initializer.StartAsync(CancellationToken.None);

    // Act
    await initializer.StopAsync(CancellationToken.None);

    // Assert
    initializer.StopCalled.Should().BeTrue();
});

// ========================================
// INTEGRATION TESTS - Telemetry
// ========================================

await harness.Fact("Initialize_WithTelemetry_ShouldRecordActivity", TestCategory.Integration, async () =>
{
    // Arrange
    var builder = DatabaseInitializerBuilder.Create()
        .WithInMemoryDatabase("TelemetryTest")
        .WithTelemetry();

    var telemetry = builder.GetTelemetry();
    var initializer = builder.Build();

    // Act
    await initializer.StartAsync(CancellationToken.None);

    // Assert
    telemetry.Should().NotBeNull();
    telemetry!.Activities.Should().Contain("DB.Initialization");
    telemetry.Activities.Should().HaveCountGreaterThan(0);
});

// ========================================
// THEORY TESTS - Parameterized
// ========================================

var databaseNames = new[] { "ParamTest1", "ParamTest2", "ParamTest3" };

await harness.Theory("Initialize_WithDifferentDatabases_ShouldSucceed", TestCategory.Integration,
    databaseNames,
    async (dbName) =>
    {
        // Arrange
        var initializer = DatabaseInitializerBuilder.Create()
            .WithInMemoryDatabase(dbName)
            .Build();

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Assert
        initializer.InitializeSucceeded.Should().BeTrue($"database {dbName} should initialize");
    });

// ========================================
// PERFORMANCE TESTS
// ========================================

await harness.Fact("Initialize_Performance_ShouldCompleteUnder1Second", TestCategory.Performance, async () =>
{
    // Arrange
    var initializer = DatabaseInitializerBuilder.Create()
        .WithInMemoryDatabase("PerfTest")
        .Build();

    // Act
    var sw = Stopwatch.StartNew();
    await initializer.StartAsync(CancellationToken.None);
    sw.Stop();

    // Assert
    sw.ElapsedMilliseconds.Should().BeLessThan(1000, "initialization should be fast");
    initializer.InitializeDurationMs.Should().BeLessThan(1000);

    harness.AddMetadata("InitDurationMs", initializer.InitializeDurationMs);
    harness.AddMetadata("TotalDurationMs", sw.ElapsedMilliseconds);
});

await harness.Fact("Initialize_Parallel_ShouldHandleMultipleConcurrentCalls", TestCategory.Performance, async () =>
{
    // Arrange
    var tasks = Enumerable.Range(0, 5).Select(i =>
        Task.Run(async () =>
        {
            var initializer = DatabaseInitializerBuilder.Create()
                .WithInMemoryDatabase($"ParallelTest{i}")
                .Build();
            await initializer.StartAsync(CancellationToken.None);
            return initializer.InitializeSucceeded;
        }));

    // Act
    var results = await Task.WhenAll(tasks);

    // Assert
    results.Should().AllBeEquivalentTo(true, "all parallel initializations should succeed");
});

// ========================================
// RESILIENCE TESTS
// ========================================

await harness.Fact("Initialize_WithCancellation_ShouldHandleGracefully", TestCategory.Resilience, async () =>
{
    // Arrange
    var initializer = DatabaseInitializerBuilder.Create()
        .WithInMemoryDatabase("CancellationTest")
        .Build();

    var cts = new CancellationTokenSource();
    cts.CancelAfter(10);

    // Act
    Func<Task> act = async () => await initializer.StartAsync(cts.Token);

    // Assert - Should either complete or handle cancellation
    await act.Should().NotThrowAsync<InvalidOperationException>();
});

await harness.Fact("Initialize_MultipleSequentialCalls_ShouldMaintainState", TestCategory.Resilience, async () =>
{
    // Arrange
    var initializer = DatabaseInitializerBuilder.Create()
        .WithInMemoryDatabase("SequentialTest")
        .Build();

    // Act
    await initializer.StartAsync(CancellationToken.None);
    var firstCallCount = initializer.StartCalled ? 1 : 0;

    await initializer.StartAsync(CancellationToken.None);
    var secondCallCount = initializer.StartCalled ? 1 : 0;

    // Assert
    firstCallCount.Should().Be(1);
    secondCallCount.Should().Be(1);
    initializer.InitializeSucceeded.Should().BeTrue();
});

// ========================================
// COMPLEX ASSERTIONS - Collections & Objects
// ========================================

await harness.Fact("DbContext_Operations_ShouldPersistData", TestCategory.Integration, async () =>
{
    // Arrange
    var dbName = "DataPersistenceTest";
    var options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase(dbName)
        .Options;

    var initializer = DatabaseInitializerBuilder.Create()
        .WithInMemoryDatabase(dbName)
        .Build();

    await initializer.StartAsync(CancellationToken.None);

    // Act - Insert data
    using (var context = new TestDbContext(options))
    {
        context.TestEntities.AddRange(
            new TestEntity { Id = 1, Name = "Entity1", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 2, Name = "Entity2", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 3, Name = "Entity3", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();
    }

    // Assert - Verify data
    using (var context = new TestDbContext(options))
    {
        var entities = await context.TestEntities.ToListAsync();

        entities.Should().NotBeEmpty();
        entities.Should().HaveCount(3);
        entities.Should().Contain(e => e.Name == "Entity1");
        entities.Should().OnlyContain(e => e.CreatedAt != default);
        entities.Select(e => e.Name).Should().BeEquivalentTo(new[] { "Entity1", "Entity2", "Entity3" });
    }
});

// ========================================
// PRINT SUMMARY
// ========================================

harness.PrintSummary();

Log.CloseAndFlush();

// Exit with appropriate code
Environment.Exit(harness.GetExitCode());
