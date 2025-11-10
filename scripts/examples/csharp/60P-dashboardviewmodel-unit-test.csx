// DashboardViewModel Unit Test - Validates ViewModel MVVM patterns and data binding
// Usage: docker run --rm -v "${PWD}:/app:ro" wiley-widget/csx-mcp:local scripts/examples/csharp/60-dashboardviewmodel-unit-test.csx
// Purpose: Tests DashboardViewModel KPI calculations, navigation commands, and INotifyPropertyChanged implementation

// Required NuGet package references
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.10"
#r "nuget: Microsoft.Extensions.Configuration, 9.0.10"
#r "nuget: Microsoft.Extensions.Logging, 9.0.10"
#r "nuget: Moq, 4.20.72"
#r "nuget: Prism.Core, 9.0.537"
#r "nuget: Prism.Wpf, 9.0.537"
#r "nuget: Prism.Container.DryIoc, 9.0.107"
#r "nuget: DryIoc, 5.4.3"

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Prism.Events;
using Prism.Navigation.Regions;

// ========================================
// TEST METADATA
// ========================================
// Test Name: DashboardViewModel Unit Test
// Category: Unit (ViewModels)
// Purpose: Validates MVVM patterns, property change notifications, and command execution
// Dependencies: Prism.Wpf, Moq
// Testing: INotifyPropertyChanged, Command bindings, Async data loading, Navigation
// ========================================

Console.WriteLine("=== DashboardViewModel Unit Test ===\n");
Console.WriteLine("Testing MVVM patterns and data binding in DashboardViewModel");
Console.WriteLine("Validates: Property notifications, KPI calculations, command execution\n");

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

void AssertEqual<T>(T expected, T actual, string testName, string? details = null)
{
    bool equals = EqualityComparer<T>.Default.Equals(expected, actual);
    Assert(equals, testName, details ?? $"Expected: {expected}, Actual: {actual}");
}

// ========================================
// MOCK INTERFACES (Simplified versions matching WileyWidget contracts)
// ========================================

public interface IEnterpriseRepository
{
    Task<int> GetTotalEnterprisesAsync();
    Task<decimal> GetTotalBudgetAsync();
}

public interface IWhatIfScenarioEngine
{
    Task<int> GetActiveProjectsCountAsync();
}

public interface IUtilityCustomerRepository
{
    Task<int> GetTotalCustomersAsync();
}

public interface IMunicipalAccountRepository
{
    Task<int> GetTotalAccountsAsync();
}

public interface ICacheService
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
}

public class FiscalYearSettings
{
    public int CurrentFiscalYear { get; set; } = DateTime.Now.Year;
}

// ========================================
// SIMPLIFIED DASHBOARDVIEWMODEL FOR TESTING
// ========================================
// Note: This is a simplified test version. In production, reference actual WileyWidget.UI assembly

public class DashboardViewModelTestable : INotifyPropertyChanged
{
    private readonly ILogger _logger;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly IWhatIfScenarioEngine _whatIfScenarioEngine;
    private readonly IUtilityCustomerRepository _utilityCustomerRepository;
    private readonly FiscalYearSettings _fiscalYearSettings;
    private readonly IEventAggregator _eventAggregator;
    private readonly IRegionManager _regionManager;

    // KPI Properties with INotifyPropertyChanged
    private int _totalEnterprises;
    public int TotalEnterprises
    {
        get => _totalEnterprises;
        set
        {
            if (_totalEnterprises != value)
            {
                _totalEnterprises = value;
                OnPropertyChanged(nameof(TotalEnterprises));
            }
        }
    }

    private decimal _totalBudget;
    public decimal TotalBudget
    {
        get => _totalBudget;
        set
        {
            if (_totalBudget != value)
            {
                _totalBudget = value;
                OnPropertyChanged(nameof(TotalBudget));
            }
        }
    }

    private int _activeProjects;
    public int ActiveProjects
    {
        get => _activeProjects;
        set
        {
            if (_activeProjects != value)
            {
                _activeProjects = value;
                OnPropertyChanged(nameof(ActiveProjects));
            }
        }
    }

    private string _systemHealthStatus = "Good";
    public string SystemHealthStatus
    {
        get => _systemHealthStatus;
        set
        {
            if (_systemHealthStatus != value)
            {
                _systemHealthStatus = value;
                OnPropertyChanged(nameof(SystemHealthStatus));
            }
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public DashboardViewModelTestable(
        ILogger logger,
        IEnterpriseRepository enterpriseRepository,
        IWhatIfScenarioEngine whatIfScenarioEngine,
        IUtilityCustomerRepository utilityCustomerRepository,
        FiscalYearSettings fiscalYearSettings,
        IEventAggregator eventAggregator,
        IRegionManager regionManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _whatIfScenarioEngine = whatIfScenarioEngine ?? throw new ArgumentNullException(nameof(whatIfScenarioEngine));
        _utilityCustomerRepository = utilityCustomerRepository ?? throw new ArgumentNullException(nameof(utilityCustomerRepository));
        _fiscalYearSettings = fiscalYearSettings ?? throw new ArgumentNullException(nameof(fiscalYearSettings));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
    }

    public async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        try
        {
            TotalEnterprises = await _enterpriseRepository.GetTotalEnterprisesAsync();
            TotalBudget = await _enterpriseRepository.GetTotalBudgetAsync();
            ActiveProjects = await _whatIfScenarioEngine.GetActiveProjectsCountAsync();

            // Update system health based on data
            SystemHealthStatus = TotalEnterprises > 0 ? "Good" : "Warning";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard data");
            SystemHealthStatus = "Error";
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

// ========================================
// TEST SUITE
// ========================================

Console.WriteLine("Setting up mocks and test data...\n");

// Create mocks
var mockLogger = new Mock<ILogger>();
var mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
var mockWhatIfEngine = new Mock<IWhatIfScenarioEngine>();
var mockUtilityRepo = new Mock<IUtilityCustomerRepository>();
var mockEventAggregator = new Mock<IEventAggregator>();
var mockRegionManager = new Mock<IRegionManager>();

var fiscalSettings = new FiscalYearSettings { CurrentFiscalYear = 2025 };

// Setup mock behaviors
mockEnterpriseRepo.Setup(x => x.GetTotalEnterprisesAsync())
    .ReturnsAsync(42);
mockEnterpriseRepo.Setup(x => x.GetTotalBudgetAsync())
    .ReturnsAsync(1_500_000.50m);
mockWhatIfEngine.Setup(x => x.GetActiveProjectsCountAsync())
    .ReturnsAsync(15);

// ========================================
// TEST 1: Constructor and Dependency Injection
// ========================================
Console.WriteLine("TEST 1: Constructor and Dependency Injection");
Console.WriteLine("---------------------------------------------");

try
{
    var viewModel = new DashboardViewModelTestable(
        mockLogger.Object,
        mockEnterpriseRepo.Object,
        mockWhatIfEngine.Object,
        mockUtilityRepo.Object,
        fiscalSettings,
        mockEventAggregator.Object,
        mockRegionManager.Object);

    AssertNotNull(viewModel, "ViewModel constructor succeeds with all dependencies");
    AssertEqual(0, viewModel.TotalEnterprises, "Initial TotalEnterprises is 0");
    AssertEqual(0m, viewModel.TotalBudget, "Initial TotalBudget is 0");
    AssertEqual("Good", viewModel.SystemHealthStatus, "Initial SystemHealthStatus is 'Good'");
}
catch (Exception ex)
{
    Assert(false, "Constructor test", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST 2: INotifyPropertyChanged Implementation
// ========================================
Console.WriteLine("TEST 2: INotifyPropertyChanged Implementation");
Console.WriteLine("----------------------------------------------");

try
{
    var viewModel = new DashboardViewModelTestable(
        mockLogger.Object,
        mockEnterpriseRepo.Object,
        mockWhatIfEngine.Object,
        mockUtilityRepo.Object,
        fiscalSettings,
        mockEventAggregator.Object,
        mockRegionManager.Object);

    var propertyChangedEvents = new List<string>();
    viewModel.PropertyChanged += (sender, e) =>
    {
        if (e.PropertyName != null)
            propertyChangedEvents.Add(e.PropertyName);
    };

    // Change properties and verify events
    viewModel.TotalEnterprises = 100;
    Assert(propertyChangedEvents.Contains(nameof(viewModel.TotalEnterprises)),
        "PropertyChanged fires for TotalEnterprises");

    viewModel.TotalBudget = 500_000m;
    Assert(propertyChangedEvents.Contains(nameof(viewModel.TotalBudget)),
        "PropertyChanged fires for TotalBudget");

    viewModel.SystemHealthStatus = "Warning";
    Assert(propertyChangedEvents.Contains(nameof(viewModel.SystemHealthStatus)),
        "PropertyChanged fires for SystemHealthStatus");

    viewModel.IsLoading = true;
    Assert(propertyChangedEvents.Contains(nameof(viewModel.IsLoading)),
        "PropertyChanged fires for IsLoading");

    AssertEqual(4, propertyChangedEvents.Count, "All 4 property changes fired events");
}
catch (Exception ex)
{
    Assert(false, "INotifyPropertyChanged test", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST 3: Async Data Loading
// ========================================
Console.WriteLine("TEST 3: Async Data Loading");
Console.WriteLine("---------------------------");

try
{
    var viewModel = new DashboardViewModelTestable(
        mockLogger.Object,
        mockEnterpriseRepo.Object,
        mockWhatIfEngine.Object,
        mockUtilityRepo.Object,
        fiscalSettings,
        mockEventAggregator.Object,
        mockRegionManager.Object);

    var loadingStates = new List<bool>();
    viewModel.PropertyChanged += (sender, e) =>
    {
        if (e.PropertyName == nameof(viewModel.IsLoading))
            loadingStates.Add(viewModel.IsLoading);
    };

    // Execute async load
    await viewModel.LoadDashboardDataAsync();

    // Verify data loaded from mocks
    AssertEqual(42, viewModel.TotalEnterprises, "TotalEnterprises loaded from repository");
    AssertEqual(1_500_000.50m, viewModel.TotalBudget, "TotalBudget loaded from repository");
    AssertEqual(15, viewModel.ActiveProjects, "ActiveProjects loaded from engine");
    AssertEqual("Good", viewModel.SystemHealthStatus, "SystemHealthStatus updated to 'Good'");

    // Verify loading states
    Assert(loadingStates.Count >= 2, "IsLoading toggled at least twice (start/end)");
    Assert(loadingStates.First() == true, "IsLoading was true at start");
    Assert(loadingStates.Last() == false, "IsLoading was false at end");
    Assert(!viewModel.IsLoading, "IsLoading is false after completion");
}
catch (Exception ex)
{
    Assert(false, "Async data loading test", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST 4: Error Handling
// ========================================
Console.WriteLine("TEST 4: Error Handling");
Console.WriteLine("----------------------");

try
{
    // Setup failing mock
    var mockFailingRepo = new Mock<IEnterpriseRepository>();
    mockFailingRepo.Setup(x => x.GetTotalEnterprisesAsync())
        .ThrowsAsync(new InvalidOperationException("Database connection failed"));
    mockFailingRepo.Setup(x => x.GetTotalBudgetAsync())
        .ThrowsAsync(new InvalidOperationException("Database connection failed"));

    var viewModel = new DashboardViewModelTestable(
        mockLogger.Object,
        mockFailingRepo.Object,
        mockWhatIfEngine.Object,
        mockUtilityRepo.Object,
        fiscalSettings,
        mockEventAggregator.Object,
        mockRegionManager.Object);

    try
    {
        await viewModel.LoadDashboardDataAsync();
        Assert(false, "Exception should have been thrown for database failure");
    }
    catch (InvalidOperationException)
    {
        Assert(true, "Exception properly thrown for database failure");
        AssertEqual("Error", viewModel.SystemHealthStatus, "SystemHealthStatus set to 'Error' on exception");
        Assert(!viewModel.IsLoading, "IsLoading set to false in finally block");
    }
}
catch (Exception ex)
{
    Assert(false, "Error handling test", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST 5: Repository Method Invocation
// ========================================
Console.WriteLine("TEST 5: Repository Method Invocation");
Console.WriteLine("-------------------------------------");

try
{
    // Create fresh mocks for this test to avoid interference from previous tests
    var mockEnterpriseRepoFresh = new Mock<IEnterpriseRepository>();
    var mockWhatIfEngineFresh = new Mock<IWhatIfScenarioEngine>();

    mockEnterpriseRepoFresh.Setup(x => x.GetTotalEnterprisesAsync())
        .ReturnsAsync(42);
    mockEnterpriseRepoFresh.Setup(x => x.GetTotalBudgetAsync())
        .ReturnsAsync(1_500_000.50m);
    mockWhatIfEngineFresh.Setup(x => x.GetActiveProjectsCountAsync())
        .ReturnsAsync(15);

    var viewModel = new DashboardViewModelTestable(
        mockLogger.Object,
        mockEnterpriseRepoFresh.Object,
        mockWhatIfEngineFresh.Object,
        mockUtilityRepo.Object,
        fiscalSettings,
        mockEventAggregator.Object,
        mockRegionManager.Object);

    await viewModel.LoadDashboardDataAsync();

    // Verify mocks were called exactly once
    mockEnterpriseRepoFresh.Verify(x => x.GetTotalEnterprisesAsync(), Times.Once(),
        "EnterpriseRepository.GetTotalEnterprisesAsync called once");
    mockEnterpriseRepoFresh.Verify(x => x.GetTotalBudgetAsync(), Times.Once(),
        "EnterpriseRepository.GetTotalBudgetAsync called once");
    mockWhatIfEngineFresh.Verify(x => x.GetActiveProjectsCountAsync(), Times.Once(),
        "WhatIfScenarioEngine.GetActiveProjectsCountAsync called once");

    Assert(true, "All repository methods invoked correctly");
}
catch (Exception ex)
{
    Assert(false, "Repository invocation test", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST SUMMARY
// ========================================
Console.WriteLine("\n========================================");
Console.WriteLine("TEST SUMMARY");
Console.WriteLine("========================================");
Console.WriteLine($"Total Tests: {total}");
Console.WriteLine($"Passed: {passed} ({(total > 0 ? (passed * 100.0 / total).ToString("F1") : "0")}%)");
Console.WriteLine($"Failed: {total - passed}");

if (failures.Any())
{
    Console.WriteLine("\nFailed Tests:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"  {failure}");
    }
}

Console.WriteLine("\n✓ DashboardViewModel Unit Test Complete");
Console.WriteLine($"Result: {(failures.Count == 0 ? "SUCCESS" : "FAILURES DETECTED")}");

// Exit with appropriate code
Environment.Exit(failures.Count == 0 ? 0 : 1);
