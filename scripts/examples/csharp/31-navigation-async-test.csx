#!/usr/bin/env dotnet-script
#r "nuget: Xunit, 2.4.2"
#r "nuget: Moq, 4.18.4"
#r "nuget: FluentAssertions, 6.12.0"

// Critical Test: Async Navigation in Prism ViewModels
// Focus: INavigationAware, Region navigation, error handling
// Tests: OnNavigatedTo, OnNavigatingFrom, navigation cancellation
// MCP Integration: Captures navigation flow, timing, error conditions

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using Moq;
using FluentAssertions;

#nullable enable

Console.WriteLine("=== Async Navigation Test (MCP) ===\n");

string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"Repo Root: {repoRoot}");
Console.WriteLine($"Logs Dir: {logsDir}\n");

int passed = 0, total = 0;
List<string> failures = new();
List<NavigationEvent> navigationLog = new();

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

public class NavigationEvent
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = "";
    public string? Details { get; set; }
    public long DurationMs { get; set; }
}

// Mock INavigationAware
public interface INavigationAware
{
    void OnNavigatedTo(NavigationContext navigationContext);
    bool IsNavigationTarget(NavigationContext navigationContext);
    void OnNavigatedFrom(NavigationContext navigationContext);
}

// Mock NavigationContext
public class NavigationContext
{
    public Uri Uri { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();

    public NavigationContext(Uri uri)
    {
        Uri = uri;
    }
}

// Mock Repository
public interface IDataRepository
{
    Task<List<string>> LoadDataAsync();
}

// Mock ViewModel with Navigation
public class NavigationTestViewModel : INavigationAware
{
    private readonly IDataRepository _repository;
    private bool _isLoading;
    private List<string> _data = new();
    private Action<NavigationEvent>? _eventLogger;

    public NavigationTestViewModel(IDataRepository repository, Action<NavigationEvent>? eventLogger = null)
    {
        _repository = repository;
        _eventLogger = eventLogger;
    }

    public bool IsLoading => _isLoading;
    public List<string> Data => _data;
    public bool NavigatedToComplete { get; private set; }
    public bool NavigatedFromComplete { get; private set; }

    public async void OnNavigatedTo(NavigationContext navigationContext)
    {
        var startTime = DateTime.UtcNow;
        _eventLogger?.Invoke(new NavigationEvent
        {
            Timestamp = startTime,
            EventType = "OnNavigatedTo_Start",
            Details = navigationContext.Uri.ToString()
        });

        try
        {
            _isLoading = true;
            _data = await _repository.LoadDataAsync();
            NavigatedToComplete = true;

            var endTime = DateTime.UtcNow;
            _eventLogger?.Invoke(new NavigationEvent
            {
                Timestamp = endTime,
                EventType = "OnNavigatedTo_Complete",
                Details = $"Loaded {_data.Count} items",
                DurationMs = (long)(endTime - startTime).TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            _eventLogger?.Invoke(new NavigationEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "OnNavigatedTo_Error",
                Details = ex.Message
            });
            throw;
        }
        finally
        {
            _isLoading = false;
        }
    }

    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        _eventLogger?.Invoke(new NavigationEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = "IsNavigationTarget",
            Details = navigationContext.Uri.ToString()
        });
        return true;
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _eventLogger?.Invoke(new NavigationEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = "OnNavigatedFrom",
            Details = navigationContext.Uri.ToString()
        });

        _data.Clear();
        NavigatedFromComplete = true;
    }
}

Console.WriteLine("--- Running Navigation Test Cases ---\n");

// TEST 1: OnNavigatedTo loads data successfully
try
{
    Console.WriteLine("Test 1: OnNavigatedTo loads data successfully");

    var mockRepo = new Mock<IDataRepository>();
    mockRepo
        .Setup(r => r.LoadDataAsync())
        .ReturnsAsync(new List<string> { "Item1", "Item2", "Item3" });

    var viewModel = new NavigationTestViewModel(
        mockRepo.Object,
        evt => navigationLog.Add(evt));

    var context = new NavigationContext(new Uri("test://BudgetView", UriKind.Absolute));

    viewModel.OnNavigatedTo(context);
    await Task.Delay(200); // Wait for async load

    Assert(viewModel.NavigatedToComplete, "Test 1: Navigation completed");
    Assert(viewModel.Data.Count == 3, "Test 1: Data loaded",
        $"Expected 3 items, got {viewModel.Data.Count}");
    Assert(!viewModel.IsLoading, "Test 1: Loading flag cleared");

    mockRepo.Verify(r => r.LoadDataAsync(), Times.Once);

    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 1", $"Exception: {ex.Message}\n{ex.StackTrace}");
    Console.WriteLine();
}

// TEST 2: OnNavigatedTo handles repository errors
try
{
    Console.WriteLine("Test 2: OnNavigatedTo handles repository errors gracefully");

    var mockRepo = new Mock<IDataRepository>();
    mockRepo
        .Setup(r => r.LoadDataAsync())
        .ThrowsAsync(new InvalidOperationException("Database connection failed"));

    var viewModel = new NavigationTestViewModel(
        mockRepo.Object,
        evt => navigationLog.Add(evt));

    var context = new NavigationContext(new Uri("test://ErrorView", UriKind.Absolute));

    bool errorCaught = false;
    try
    {
        viewModel.OnNavigatedTo(context);
        await Task.Delay(200);
    }
    catch (InvalidOperationException)
    {
        errorCaught = true;
    }

    Assert(errorCaught, "Test 2: Exception propagated correctly");
    Assert(!viewModel.IsLoading, "Test 2: Loading flag cleared on error");

    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 2", $"Exception: {ex.Message}");
    Console.WriteLine();
}

// TEST 3: OnNavigatedFrom cleans up state
try
{
    Console.WriteLine("Test 3: OnNavigatedFrom cleans up ViewModel state");

    var mockRepo = new Mock<IDataRepository>();
    mockRepo
        .Setup(r => r.LoadDataAsync())
        .ReturnsAsync(new List<string> { "Data1", "Data2" });

    var viewModel = new NavigationTestViewModel(
        mockRepo.Object,
        evt => navigationLog.Add(evt));

    var contextTo = new NavigationContext(new Uri("test://View1", UriKind.Absolute));
    viewModel.OnNavigatedTo(contextTo);
    await Task.Delay(200);

    Assert(viewModel.Data.Count == 2, "Test 3: Data loaded before navigation away");

    var contextFrom = new NavigationContext(new Uri("test://View2", UriKind.Absolute));
    viewModel.OnNavigatedFrom(contextFrom);

    Assert(viewModel.NavigatedFromComplete, "Test 3: OnNavigatedFrom executed");
    Assert(viewModel.Data.Count == 0, "Test 3: Data cleared",
        $"Expected 0 items, got {viewModel.Data.Count}");

    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 3", $"Exception: {ex.Message}");
    Console.WriteLine();
}

// TEST 4: IsNavigationTarget called before navigation
try
{
    Console.WriteLine("Test 4: IsNavigationTarget determines navigation eligibility");

    var mockRepo = new Mock<IDataRepository>();
    var viewModel = new NavigationTestViewModel(
        mockRepo.Object,
        evt => navigationLog.Add(evt));

    var context = new NavigationContext(new Uri("test://TargetView", UriKind.Absolute));
    bool isTarget = viewModel.IsNavigationTarget(context);

    Assert(isTarget, "Test 4: ViewModel accepts navigation");

    var targetEvent = navigationLog.FirstOrDefault(e => e.EventType == "IsNavigationTarget");
    Assert(targetEvent != null, "Test 4: IsNavigationTarget event logged");

    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 4", $"Exception: {ex.Message}");
    Console.WriteLine();
}

// TEST 5: Navigation timing under load
try
{
    Console.WriteLine("Test 5: Navigation performance under simulated load");

    var mockRepo = new Mock<IDataRepository>();
    mockRepo
        .Setup(r => r.LoadDataAsync())
        .Returns(async () =>
        {
            await Task.Delay(100); // Simulate network/DB delay
            return Enumerable.Range(1, 1000).Select(i => $"Item{i}").ToList();
        });

    var viewModel = new NavigationTestViewModel(
        mockRepo.Object,
        evt => navigationLog.Add(evt));

    var context = new NavigationContext(new Uri("test://PerformanceView", UriKind.Absolute));

    var startTime = DateTime.UtcNow;
    viewModel.OnNavigatedTo(context);
    await Task.Delay(300); // Wait for completion
    var endTime = DateTime.UtcNow;

    var duration = (endTime - startTime).TotalMilliseconds;

    Assert(viewModel.NavigatedToComplete, "Test 5: Navigation completed under load");
    Assert(viewModel.Data.Count == 1000, "Test 5: Large dataset loaded");
    Assert(duration < 500, "Test 5: Navigation completed within timeout",
        $"Duration: {duration}ms");

    Console.WriteLine();
}
catch (Exception ex)
{
    Assert(false, "Test 5", $"Exception: {ex.Message}");
    Console.WriteLine();
}

// Results
Console.WriteLine("\n--- Test Results ---");
Console.WriteLine($"Total Tests: {total}");
Console.WriteLine($"Passed: {passed} ✓");
Console.WriteLine($"Failed: {total - passed} ✗");

if (failures.Any())
{
    Console.WriteLine("\n--- Failures ---");
    foreach (var failure in failures)
    {
        Console.WriteLine(failure);
    }
}

// Navigation Event Log
Console.WriteLine("\n--- Navigation Event Log ---");
foreach (var evt in navigationLog)
{
    string durationInfo = evt.DurationMs > 0 ? $" ({evt.DurationMs}ms)" : "";
    Console.WriteLine($"[{evt.Timestamp:HH:mm:ss.fff}] {evt.EventType}{durationInfo}");
    if (!string.IsNullOrEmpty(evt.Details))
        Console.WriteLine($"  Details: {evt.Details}");
}

// Performance Metrics
Console.WriteLine("\n--- Performance Metrics ---");
var navEvents = navigationLog.Where(e => e.EventType.Contains("OnNavigatedTo")).ToList();
if (navEvents.Any())
{
    var avgDuration = navEvents.Where(e => e.DurationMs > 0).Average(e => e.DurationMs);
    // Avoid nullable dereference by selecting numeric values before DefaultIfEmpty
    var maxDuration = navEvents.Where(e => e.DurationMs > 0)
                               .Select(e => e.DurationMs)
                               .DefaultIfEmpty(0)
                               .Max();

    Console.WriteLine($"Average Navigation Time: {avgDuration:F2}ms");
    Console.WriteLine($"Max Navigation Time: {maxDuration}ms");
}

Console.WriteLine("\n--- Coverage Summary ---");
Console.WriteLine("Test Category: Navigation & Async Operations");
Console.WriteLine("Areas Covered:");
Console.WriteLine("  - INavigationAware.OnNavigatedTo");
Console.WriteLine("  - INavigationAware.OnNavigatedFrom");
Console.WriteLine("  - INavigationAware.IsNavigationTarget");
Console.WriteLine("  - Async data loading during navigation");
Console.WriteLine("  - Error handling in navigation callbacks");
Console.WriteLine("  - State cleanup on navigation away");
Console.WriteLine("  - Performance under load");

Console.WriteLine("\n--- MCP Context ---");
Console.WriteLine($"Logs Directory: {logsDir}");
Console.WriteLine("Next Steps:");
Console.WriteLine("  1. Review navigation event timing");
Console.WriteLine("  2. Analyze error handling patterns");
Console.WriteLine("  3. Verify state cleanup completeness");
Console.WriteLine("  4. Add to navigation integration tests");

Environment.Exit(passed == total ? 0 : 1);
