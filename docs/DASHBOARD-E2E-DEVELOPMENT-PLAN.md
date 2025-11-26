# Dashboard E2E Development Plan

**Project**: Wiley Widget  
**Scope**: Dashboard Feature - End-to-End Implementation  
**Target Branch**: `upgrade-to-NET10`  
**Last Updated**: November 26, 2025

---

## 🎯 Executive Summary

This document defines the **focused development scope** for the Wiley Widget project: **Dashboard Feature Only (E2E)**. All other features (Charts, Budgets, QuickBooks, xAI integrations) are **OUT OF SCOPE** until the Dashboard slice is complete and validated.

### Why Dashboard First?

1. ✅ **Core functionality** - Central to the app's purpose (municipal budget metrics)
2. ✅ **Full stack slice** - Touches all layers (UI → ViewModel → Service → Data)
3. ✅ **Self-contained** - No complex external dependencies initially
4. ✅ **Quick wins** - Visible, demo-able results in 1-2 days
5. ✅ **Pattern setter** - Establishes MVVM patterns for future features

---

## 📐 Current State Analysis

### Existing Dashboard Components

| Component                 | Location                           | Status              | Notes                        |
| ------------------------- | ---------------------------------- | ------------------- | ---------------------------- |
| **DashboardViewModel**    | `WileyWidget.WinForms/ViewModels/` | ✅ Basic            | Has mock data (3 metrics)    |
| **DashboardMetric Model** | Inline in ViewModel                | ⚠️ Needs extraction | Should be in Models project  |
| **Dashboard View**        | ❌ Missing                         | 🆕 To create        | Need WinForms implementation |
| **Dashboard Service**     | ❌ Missing                         | 🆕 To create        | IDashboardService interface  |
| **Dashboard Tests**       | `tests/WileyWidget.WinUI.Tests/`   | ⚠️ Legacy WinUI     | Need WinForms-aligned tests  |
| **DataService**           | `Services/DataService.cs`          | ✅ Exists           | Generic messaging service    |

### Current Mock Data

```csharp
// WileyWidget.WinForms/ViewModels/DashboardViewModel.cs
Metrics = new ObservableCollection<DashboardMetric>
{
    new DashboardMetric { Name = "Total Sales", Value = 150000.50 },
    new DashboardMetric { Name = "Growth Rate", Value = 12.34 },
    new DashboardMetric { Name = "Customer Count", Value = 1234 }
};
```

---

## 🎯 Development Scope Definition

### ✅ IN SCOPE - Dashboard E2E

1. **Dashboard UI (WinForms)**
   - Main dashboard panel with metric cards
   - Refresh button for data reload
   - Loading indicators
   - Error handling UI

2. **Dashboard ViewModel**
   - Enhanced with async commands
   - Service integration
   - Error handling
   - Loading states

3. **Dashboard Service Layer**
   - `IDashboardService` interface
   - `DashboardService` implementation
   - Mock data provider
   - Future: EF Core integration

4. **Dashboard Models**
   - `DashboardMetric` (extracted from ViewModel)
   - `DashboardSummary` (aggregate container)
   - Municipal-specific metrics

5. **Dashboard Tests**
   - Unit tests for ViewModel
   - Service layer tests
   - Integration tests (optional)

6. **Data Layer (Simplified)**
   - Static/mock data initially
   - SQLite local database (Phase 2)
   - Simple repository pattern

### ❌ OUT OF SCOPE (Deferred)

- ❌ Chart visualizations (LiveCharts)
- ❌ Budget management features
- ❌ QuickBooks integration
- ❌ xAI Grok API integration
- ❌ Advanced telemetry (SigNoz)
- ❌ Authentication/Authorization
- ❌ Multi-tenant support
- ❌ WinUI legacy code migration (focus on WinForms only)

---

## 📋 Phased Implementation Plan

### Phase 0: Setup & Validation (1-2 hours)

**Goal**: Ensure clean build, validate current state, set up testing infrastructure

**Tasks**:

```powershell
# 1. Verify branch and clean build
git checkout upgrade-to-NET10
git pull origin upgrade-to-NET10
dotnet clean
dotnet build --no-restore

# 2. Run current app (verify simple window)
dotnet run --project WileyWidget.WinForms/WileyWidget.WinForms.csproj

# 3. Check for tests project (create if missing)
dotnet new xunit -n WileyWidget.WinForms.Tests -o tests/WileyWidget.WinForms.Tests
dotnet sln add tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj
dotnet add tests/WileyWidget.WinForms.Tests reference WileyWidget.WinForms/WileyWidget.WinForms.csproj

# 4. Add test dependencies
dotnet add tests/WileyWidget.WinForms.Tests package Moq
dotnet add tests/WileyWidget.WinForms.Tests package FluentAssertions
dotnet add tests/WileyWidget.WinForms.Tests package Microsoft.NET.Test.Sdk

# 5. Regenerate manifest
python scripts/tools/generate_repo_urls.py -o ai-fetchable-manifest.json

# 6. Run Trunk checks (MCP enforcement)
trunk check --ci
```

**Success Criteria**:

- ✅ Clean build (0 errors, 0 warnings)
- ✅ App runs and shows window
- ✅ Test project compiles
- ✅ Manifest is fresh

---

### Phase 1: Model & Service Layer (2-4 hours)

**Goal**: Create proper domain models and service interfaces

#### 1.1 Extract & Enhance Dashboard Models

**File**: `src/WileyWidget.Models/Models/DashboardMetric.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Models;

/// <summary>
/// Represents a single dashboard metric (e.g., Total Revenues, Expenditures)
/// </summary>
public class DashboardMetric
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    public string? Unit { get; set; } // e.g., "$", "%", "Count"

    public string? Description { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public MetricTrend Trend { get; set; } = MetricTrend.Neutral;
}

public enum MetricTrend
{
    Up,
    Down,
    Neutral
}
```

**File**: `src/WileyWidget.Models/Models/DashboardSummary.cs`

```csharp
namespace WileyWidget.Models;

/// <summary>
/// Aggregated dashboard summary for municipal budget data
/// </summary>
public class DashboardSummary
{
    public IReadOnlyList<DashboardMetric> Metrics { get; init; } = Array.Empty<DashboardMetric>();

    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    public string MunicipalityName { get; init; } = "Town of Wiley";

    public int FiscalYear { get; init; } = DateTime.UtcNow.Year;
}
```

#### 1.2 Create Dashboard Service Interface

**File**: `src/WileyWidget.Services.Abstractions/IDashboardService.cs`

```csharp
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for fetching dashboard metrics and summaries
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Fetches the current dashboard summary with all metrics
    /// </summary>
    Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes cached data (if applicable)
    /// </summary>
    Task RefreshDataAsync(CancellationToken cancellationToken = default);
}
```

#### 1.3 Implement Mock Dashboard Service

**File**: `src/WileyWidget.Services/DashboardService.cs`

```csharp
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Dashboard service implementation with mock data (Phase 1)
/// TODO: Replace with EF Core repository in Phase 2
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(ILogger<DashboardService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching dashboard summary...");

        // Simulate async data fetch
        await Task.Delay(500, cancellationToken);

        var metrics = new List<DashboardMetric>
        {
            new DashboardMetric
            {
                Name = "Total Revenues",
                Value = 2_450_000,
                Unit = "$",
                Description = "FY 2025 municipal revenues",
                Trend = MetricTrend.Up
            },
            new DashboardMetric
            {
                Name = "Total Expenditures",
                Value = 2_200_000,
                Unit = "$",
                Description = "FY 2025 municipal expenditures",
                Trend = MetricTrend.Down
            },
            new DashboardMetric
            {
                Name = "Budget Balance",
                Value = 250_000,
                Unit = "$",
                Description = "Surplus/Deficit",
                Trend = MetricTrend.Up
            },
            new DashboardMetric
            {
                Name = "Active Accounts",
                Value = 127,
                Unit = "Count",
                Description = "Municipal account count"
            },
            new DashboardMetric
            {
                Name = "Budget Utilization",
                Value = 89.8,
                Unit = "%",
                Description = "Percentage of budget used"
            }
        };

        return new DashboardSummary
        {
            Metrics = metrics,
            GeneratedAt = DateTime.UtcNow,
            MunicipalityName = "Town of Wiley",
            FiscalYear = 2025
        };
    }

    public async Task RefreshDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing dashboard data...");
        await Task.Delay(200, cancellationToken); // Mock refresh
    }
}
```

**Success Criteria**:

- ✅ Models compile and pass validation
- ✅ Service interface is clean and testable
- ✅ Mock service returns realistic data

---

### Phase 2: Enhanced ViewModel (2-3 hours)

**Goal**: Upgrade DashboardViewModel with service integration, commands, error handling

**File**: `WileyWidget.WinForms/ViewModels/DashboardViewModel.cs` (Enhanced)

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<DashboardMetric> metrics = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string municipalityName = "Loading...";

    [ObservableProperty]
    private int fiscalYear;

    [ObservableProperty]
    private DateTime lastUpdated;

    public DashboardViewModel(IDashboardService dashboardService, ILogger<DashboardViewModel> logger)
    {
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task LoadDashboardAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            _logger.LogInformation("Loading dashboard data...");

            var summary = await _dashboardService.GetDashboardSummaryAsync();

            Metrics.Clear();
            foreach (var metric in summary.Metrics)
            {
                Metrics.Add(metric);
            }

            MunicipalityName = summary.MunicipalityName;
            FiscalYear = summary.FiscalYear;
            LastUpdated = summary.GeneratedAt;

            _logger.LogInformation("Dashboard loaded successfully with {Count} metrics", Metrics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard");
            ErrorMessage = $"Failed to load dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshDashboardAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            await _dashboardService.RefreshDataAsync();
            await LoadDashboardAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh dashboard");
            ErrorMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

**Success Criteria**:

- ✅ ViewModel compiles with service injection
- ✅ Commands are async and handle errors
- ✅ Observable properties trigger UI updates

---

### Phase 3: WinForms Dashboard UI (3-4 hours)

**Goal**: Create a clean, functional dashboard panel in WinForms

#### 3.1 Create DashboardForm

**File**: `WileyWidget.WinForms/Forms/DashboardForm.cs`

```csharp
using System.Windows.Forms;
using WileyWidget.ViewModels;

namespace WileyWidget.WinForms.Forms;

public partial class DashboardForm : Form
{
    private readonly DashboardViewModel _viewModel;
    private TableLayoutPanel _metricsPanel;
    private Button _refreshButton;
    private Label _headerLabel;
    private ProgressBar _loadingIndicator;
    private Label _errorLabel;

    public DashboardForm(DashboardViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SetupDataBindings();
        LoadInitialData();
    }

    private void InitializeComponent()
    {
        Text = "Wiley Widget - Dashboard";
        Size = new System.Drawing.Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // Header
        _headerLabel = new Label
        {
            Text = "Municipal Budget Dashboard",
            Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 50,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        };

        // Refresh button
        _refreshButton = new Button
        {
            Text = "Refresh",
            Dock = DockStyle.Top,
            Height = 40
        };
        _refreshButton.Click += async (s, e) => await RefreshDashboard();

        // Loading indicator
        _loadingIndicator = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            Dock = DockStyle.Top,
            Height = 10,
            Visible = false
        };

        // Error label
        _errorLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = System.Drawing.Color.Red,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Visible = false
        };

        // Metrics panel (grid)
        _metricsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(20),
            AutoScroll = true
        };

        // Layout
        var containerPanel = new Panel { Dock = DockStyle.Fill };
        containerPanel.Controls.Add(_metricsPanel);
        containerPanel.Controls.Add(_errorLabel);
        containerPanel.Controls.Add(_loadingIndicator);
        containerPanel.Controls.Add(_refreshButton);
        containerPanel.Controls.Add(_headerLabel);

        Controls.Add(containerPanel);
    }

    private void SetupDataBindings()
    {
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (InvokeRequired)
            {
                Invoke(() => HandlePropertyChanged(e.PropertyName));
            }
            else
            {
                HandlePropertyChanged(e.PropertyName);
            }
        };

        _viewModel.Metrics.CollectionChanged += (s, e) =>
        {
            if (InvokeRequired)
            {
                Invoke(RenderMetrics);
            }
            else
            {
                RenderMetrics();
            }
        };
    }

    private void HandlePropertyChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(_viewModel.IsLoading):
                _loadingIndicator.Visible = _viewModel.IsLoading;
                _refreshButton.Enabled = !_viewModel.IsLoading;
                break;

            case nameof(_viewModel.ErrorMessage):
                _errorLabel.Text = _viewModel.ErrorMessage ?? string.Empty;
                _errorLabel.Visible = !string.IsNullOrEmpty(_viewModel.ErrorMessage);
                break;

            case nameof(_viewModel.MunicipalityName):
                _headerLabel.Text = $"{_viewModel.MunicipalityName} - FY {_viewModel.FiscalYear}";
                break;
        }
    }

    private void RenderMetrics()
    {
        _metricsPanel.Controls.Clear();
        _metricsPanel.RowStyles.Clear();

        var rowCount = (_viewModel.Metrics.Count + 1) / 2; // 2 columns
        _metricsPanel.RowCount = Math.Max(rowCount, 1);

        for (int i = 0; i < _viewModel.Metrics.Count; i++)
        {
            var metric = _viewModel.Metrics[i];
            var metricCard = CreateMetricCard(metric);

            var row = i / 2;
            var col = i % 2;

            _metricsPanel.Controls.Add(metricCard, col, row);
        }
    }

    private Panel CreateMetricCard(Models.DashboardMetric metric)
    {
        var card = new Panel
        {
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(15),
            Margin = new Padding(10),
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.WhiteSmoke
        };

        var nameLabel = new Label
        {
            Text = metric.Name,
            Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 30
        };

        var valueLabel = new Label
        {
            Text = $"{metric.Value:N2} {metric.Unit}",
            Font = new System.Drawing.Font("Segoe UI", 16),
            Dock = DockStyle.Top,
            Height = 40,
            ForeColor = System.Drawing.Color.DarkBlue
        };

        var descLabel = new Label
        {
            Text = metric.Description ?? string.Empty,
            Font = new System.Drawing.Font("Segoe UI", 9),
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = System.Drawing.Color.Gray
        };

        var trendLabel = new Label
        {
            Text = GetTrendSymbol(metric.Trend),
            Dock = DockStyle.Bottom,
            Height = 20,
            Font = new System.Drawing.Font("Segoe UI", 10),
            ForeColor = GetTrendColor(metric.Trend)
        };

        card.Controls.Add(trendLabel);
        card.Controls.Add(descLabel);
        card.Controls.Add(valueLabel);
        card.Controls.Add(nameLabel);

        return card;
    }

    private string GetTrendSymbol(Models.MetricTrend trend)
    {
        return trend switch
        {
            Models.MetricTrend.Up => "▲ Trending Up",
            Models.MetricTrend.Down => "▼ Trending Down",
            _ => "― Stable"
        };
    }

    private System.Drawing.Color GetTrendColor(Models.MetricTrend trend)
    {
        return trend switch
        {
            Models.MetricTrend.Up => System.Drawing.Color.Green,
            Models.MetricTrend.Down => System.Drawing.Color.Red,
            _ => System.Drawing.Color.Gray
        };
    }

    private async void LoadInitialData()
    {
        await _viewModel.LoadDashboardCommand.ExecuteAsync(null);
    }

    private async Task RefreshDashboard()
    {
        await _viewModel.RefreshDashboardCommand.ExecuteAsync(null);
    }
}
```

#### 3.2 Update Program.cs for DI

**File**: `WileyWidget.WinForms/Program.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var host = CreateHostBuilder().Build();

        using (var scope = host.Services.CreateScope())
        {
            var dashboardForm = scope.ServiceProvider.GetRequiredService<DashboardForm>();
            Application.Run(dashboardForm);
        }
    }

    static IHostBuilder CreateHostBuilder() =>
        Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Services
                services.AddSingleton<IDashboardService, DashboardService>();

                // ViewModels
                services.AddTransient<DashboardViewModel>();

                // Forms
                services.AddTransient<DashboardForm>();

                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            });
}
```

**Success Criteria**:

- ✅ Form displays metric cards in a grid
- ✅ Refresh button works
- ✅ Loading indicator shows during data fetch
- ✅ Errors display in red label

---

### Phase 4: Unit Testing (2-3 hours)

**Goal**: Write robust unit tests for ViewModel and Service

#### 4.1 DashboardService Tests

**File**: `tests/WileyWidget.WinForms.Tests/Services/DashboardServiceTests.cs`

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Services;

public class DashboardServiceTests
{
    private readonly Mock<ILogger<DashboardService>> _mockLogger;
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _mockLogger = new Mock<ILogger<DashboardService>>();
        _service = new DashboardService(_mockLogger.Object);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ReturnsValidSummary()
    {
        // Act
        var summary = await _service.GetDashboardSummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary.Metrics.Should().HaveCount(5);
        summary.MunicipalityName.Should().Be("Town of Wiley");
        summary.FiscalYear.Should().Be(2025);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ReturnsCorrectMetrics()
    {
        // Act
        var summary = await _service.GetDashboardSummaryAsync();

        // Assert
        summary.Metrics.Should().Contain(m => m.Name == "Total Revenues");
        summary.Metrics.Should().Contain(m => m.Name == "Total Expenditures");
        summary.Metrics.Should().Contain(m => m.Name == "Budget Balance");
        summary.Metrics.Should().Contain(m => m.Name == "Active Accounts");
        summary.Metrics.Should().Contain(m => m.Name == "Budget Utilization");
    }

    [Fact]
    public async Task RefreshDataAsync_CompletesSuccessfully()
    {
        // Act
        Func<Task> act = async () => await _service.RefreshDataAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
```

#### 4.2 DashboardViewModel Tests

**File**: `tests/WileyWidget.WinForms.Tests/ViewModels/DashboardViewModelTests.cs`

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.ViewModels;

public class DashboardViewModelTests
{
    private readonly Mock<IDashboardService> _mockService;
    private readonly Mock<ILogger<DashboardViewModel>> _mockLogger;
    private readonly DashboardViewModel _viewModel;

    public DashboardViewModelTests()
    {
        _mockService = new Mock<IDashboardService>();
        _mockLogger = new Mock<ILogger<DashboardViewModel>>();
        _viewModel = new DashboardViewModel(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task LoadDashboardAsync_PopulatesMetrics()
    {
        // Arrange
        var mockSummary = new DashboardSummary
        {
            Metrics = new List<DashboardMetric>
            {
                new DashboardMetric { Name = "Test Metric", Value = 100 }
            },
            MunicipalityName = "Test Town",
            FiscalYear = 2025
        };

        _mockService.Setup(s => s.GetDashboardSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSummary);

        // Act
        await _viewModel.LoadDashboardCommand.ExecuteAsync(null);

        // Assert
        _viewModel.Metrics.Should().HaveCount(1);
        _viewModel.MunicipalityName.Should().Be("Test Town");
        _viewModel.FiscalYear.Should().Be(2025);
        _viewModel.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task LoadDashboardAsync_HandlesErrors()
    {
        // Arrange
        _mockService.Setup(s => s.GetDashboardSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        await _viewModel.LoadDashboardCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ErrorMessage.Should().Contain("Test error");
        _viewModel.Metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadDashboardAsync_SetsLoadingState()
    {
        // Arrange
        var loadingStates = new List<bool>();
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsLoading))
                loadingStates.Add(_viewModel.IsLoading);
        };

        _mockService.Setup(s => s.GetDashboardSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardSummary { Metrics = Array.Empty<DashboardMetric>() });

        // Act
        await _viewModel.LoadDashboardCommand.ExecuteAsync(null);

        // Assert
        loadingStates.Should().Contain(true);
        loadingStates.Should().Contain(false);
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshDashboardAsync_CallsServiceAndReloads()
    {
        // Arrange
        _mockService.Setup(s => s.RefreshDataAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockService.Setup(s => s.GetDashboardSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardSummary { Metrics = Array.Empty<DashboardMetric>() });

        // Act
        await _viewModel.RefreshDashboardCommand.ExecuteAsync(null);

        // Assert
        _mockService.Verify(s => s.RefreshDataAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockService.Verify(s => s.GetDashboardSummaryAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

**Run Tests**:

```powershell
dotnet test tests/WileyWidget.WinForms.Tests --logger "console;verbosity=detailed"
```

**Success Criteria**:

- ✅ All tests pass (5/5 service tests, 4/4 ViewModel tests)
- ✅ Code coverage > 80% for Dashboard slice
- ✅ No test flakiness

---

### Phase 5: Integration & Validation (1-2 hours)

**Goal**: End-to-end validation of Dashboard feature

#### 5.1 Manual Testing Checklist

```markdown
## Dashboard E2E Test Plan

### Startup

- [ ] App launches without errors
- [ ] Dashboard form opens as main window
- [ ] Header shows "Town of Wiley - FY 2025"

### Data Display

- [ ] 5 metric cards display in 2-column grid
- [ ] Metric names are bold and readable
- [ ] Values display with correct formatting (e.g., $2,450,000.00)
- [ ] Units display correctly ($, %, Count)
- [ ] Descriptions show in gray text
- [ ] Trend indicators show correct colors (green/red/gray)

### Interactions

- [ ] Refresh button is enabled on startup
- [ ] Clicking Refresh disables button temporarily
- [ ] Loading indicator shows during refresh
- [ ] Metrics update after refresh (timestamps change)
- [ ] Refresh button re-enables after completion

### Error Handling

- [ ] (Manual test) Modify service to throw exception
- [ ] Error message displays in red label
- [ ] Metrics do not clear on error
- [ ] Refresh button remains enabled after error

### Performance

- [ ] Dashboard loads in < 1 second
- [ ] Refresh completes in < 1 second
- [ ] No UI freezing during data load
- [ ] Memory usage stays reasonable (< 100 MB)
```

#### 5.2 CI/CD Integration

**Add to `.github/workflows/ci-optimized.yml`**:

```yaml
- name: Run Dashboard Tests
  run: |
    dotnet test tests/WileyWidget.WinForms.Tests `
      --filter FullyQualifiedName~Dashboard `
      --logger "trx;LogFileName=dashboard-tests.trx" `
      --collect:"XPlat Code Coverage" `
      --results-directory ./TestResults

- name: Upload Dashboard Test Results
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: dashboard-test-results
    path: TestResults/dashboard-tests.trx
```

#### 5.3 Documentation Update

**Update `README.md`**:

````markdown
## Dashboard Feature (✅ Complete)

The Wiley Widget dashboard displays municipal budget metrics in a clean, interactive UI.

### Features

- 5 key metrics (Revenues, Expenditures, Balance, Accounts, Utilization)
- Real-time refresh capability
- Trend indicators with color coding
- Error handling and loading states

### Running the Dashboard

```powershell
dotnet run --project WileyWidget.WinForms/WileyWidget.WinForms.csproj
```
````

### Testing

```powershell
dotnet test --filter FullyQualifiedName~Dashboard
```

### Architecture

- **ViewModel**: `DashboardViewModel` (MVVM pattern with CommunityToolkit.Mvvm)
- **Service**: `IDashboardService` → `DashboardService` (mock data)
- **UI**: `DashboardForm` (WinForms with data binding)
- **Models**: `DashboardMetric`, `DashboardSummary`

````

**Success Criteria**:
- ✅ All manual tests pass
- ✅ CI/CD pipeline runs tests automatically
- ✅ Documentation is updated and accurate

---

## 📊 Success Metrics & Validation

### Definition of Done (DoD)

- ✅ **Code Complete**: All files created and compile without errors
- ✅ **Tests Pass**: 9/9 unit tests pass (service + ViewModel)
- ✅ **Coverage**: > 80% code coverage for Dashboard slice
- ✅ **Manual Testing**: All checklist items pass
- ✅ **CI/CD**: Tests run automatically in GitHub Actions
- ✅ **Documentation**: README and inline docs updated
- ✅ **MCP Compliance**: All file operations use `mcp_filesystem_*` tools
- ✅ **Trunk Validation**: `trunk check --ci` passes with 0 errors
- ✅ **No Warnings**: Zero compiler/analyzer warnings

### Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Dashboard Load Time | < 1 second | Startup to first render |
| Refresh Time | < 1 second | Button click to updated UI |
| Memory Usage | < 100 MB | After 5 refreshes |
| Test Execution | < 5 seconds | Full Dashboard test suite |

### Quality Gates

```powershell
# Pre-commit validation (MANDATORY)
trunk fmt --all
trunk check --fix
trunk check --ci

# Test validation
dotnet test --filter FullyQualifiedName~Dashboard --logger "console;verbosity=detailed"

# Build validation
dotnet build WileyWidget.sln --no-restore --configuration Release
````

---

## 🚀 Next Steps (Post-Dashboard)

**After Dashboard is complete and validated**, consider these next slices:

1. **Chart View** - Integrate LiveCharts for visual metrics
2. **Budget Management** - CRUD for budget entries
3. **Database Layer** - Replace mock data with EF Core + SQLite
4. **QuickBooks Sync** - Integration with IPP SDK
5. **Settings Panel** - Configuration UI

**But NOT until Dashboard E2E is ✅ DONE!**

---

## 📝 Commit Strategy

Use conventional commits for all Dashboard work:

```bash
# Phase 1
git commit -m "feat(dashboard): extract DashboardMetric model to Models project"
git commit -m "feat(dashboard): create IDashboardService interface"
git commit -m "feat(dashboard): implement mock DashboardService"

# Phase 2
git commit -m "feat(dashboard): enhance DashboardViewModel with service injection"
git commit -m "feat(dashboard): add async load/refresh commands to ViewModel"

# Phase 3
git commit -m "feat(dashboard): create WinForms DashboardForm UI"
git commit -m "feat(dashboard): implement metric card rendering"
git commit -m "feat(dashboard): setup DI in Program.cs"

# Phase 4
git commit -m "test(dashboard): add DashboardService unit tests"
git commit -m "test(dashboard): add DashboardViewModel unit tests"

# Phase 5
git commit -m "docs(dashboard): update README with Dashboard feature info"
git commit -m "ci(dashboard): add Dashboard tests to CI pipeline"
```

---

## 🔧 Troubleshooting

### Common Issues

**Issue**: `IDashboardService` not found in ViewModel
**Fix**: Ensure `WileyWidget.Services.Abstractions` is referenced in `WileyWidget.WinForms.csproj`

```xml
<ItemGroup>
  <ProjectReference Include="..\src\WileyWidget.Services.Abstractions\WileyWidget.Services.Abstractions.csproj" />
  <ProjectReference Include="..\src\WileyWidget.Services\WileyWidget.Services.csproj" />
  <ProjectReference Include="..\src\WileyWidget.Models\WileyWidget.Models.csproj" />
</ItemGroup>
```

**Issue**: Tests fail with `NullReferenceException`
**Fix**: Check mock setup in test constructors - ensure all dependencies are mocked

**Issue**: UI freezes during data load
**Fix**: Verify `async/await` is used correctly - never block on `.Result`

---

## 📚 Reference Documentation

- [WinForms Data Binding](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/windows-forms-data-binding)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [xUnit Testing](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions](https://fluentassertions.com/)

---

**Document Status**: ✅ APPROVED for Dashboard E2E Development  
**Last Reviewed**: November 26, 2025  
**Next Review**: After Phase 5 completion
