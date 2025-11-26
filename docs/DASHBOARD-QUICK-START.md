# Dashboard E2E - Quick Start Guide

**🎯 Goal**: Get the Dashboard feature running in 30 minutes

---

## ⚡ Quick Setup (5 minutes)

```powershell
# 1. Verify branch and build
git checkout upgrade-to-NET10
git pull origin upgrade-to-NET10
dotnet build --no-restore

# 2. Run current app (should show simple window)
dotnet run --project WileyWidget.WinForms/WileyWidget.WinForms.csproj

# 3. Create test project
dotnet new xunit -n WileyWidget.WinForms.Tests -o tests/WileyWidget.WinForms.Tests
dotnet sln add tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj
dotnet add tests/WileyWidget.WinForms.Tests reference WileyWidget.WinForms/WileyWidget.WinForms.csproj

# 4. Add test packages
dotnet add tests/WileyWidget.WinForms.Tests package Moq
dotnet add tests/WileyWidget.WinForms.Tests package FluentAssertions
dotnet add tests/WileyWidget.WinForms.Tests package Microsoft.NET.Test.Sdk
```

**✅ Success**: Build passes, app runs, test project compiles

---

## 🚀 Phase 1: Models & Service (20 minutes)

### Step 1: Create DashboardMetric Model (5 min)

**File**: `src/WileyWidget.Models/Models/DashboardMetric.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Models;

public class DashboardMetric
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public MetricTrend Trend { get; set; } = MetricTrend.Neutral;
}

public enum MetricTrend { Up, Down, Neutral }
```

**File**: `src/WileyWidget.Models/Models/DashboardSummary.cs`

```csharp
namespace WileyWidget.Models;

public class DashboardSummary
{
    public IReadOnlyList<DashboardMetric> Metrics { get; init; } = Array.Empty<DashboardMetric>();
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public string MunicipalityName { get; init; } = "Town of Wiley";
    public int FiscalYear { get; init; } = DateTime.UtcNow.Year;
}
```

```powershell
dotnet build src/WileyWidget.Models
```

### Step 2: Create Service Interface (5 min)

**File**: `src/WileyWidget.Services.Abstractions/IDashboardService.cs`

```csharp
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions;

public interface IDashboardService
{
    Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
    Task RefreshDataAsync(CancellationToken cancellationToken = default);
}
```

```powershell
dotnet build src/WileyWidget.Services.Abstractions
```

### Step 3: Implement Mock Service (10 min)

**File**: `src/WileyWidget.Services/DashboardService.cs`

```csharp
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

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
        await Task.Delay(500, cancellationToken);

        var metrics = new List<DashboardMetric>
        {
            new DashboardMetric { Name = "Total Revenues", Value = 2_450_000, Unit = "$", Description = "FY 2025 municipal revenues", Trend = MetricTrend.Up },
            new DashboardMetric { Name = "Total Expenditures", Value = 2_200_000, Unit = "$", Description = "FY 2025 municipal expenditures", Trend = MetricTrend.Down },
            new DashboardMetric { Name = "Budget Balance", Value = 250_000, Unit = "$", Description = "Surplus/Deficit", Trend = MetricTrend.Up },
            new DashboardMetric { Name = "Active Accounts", Value = 127, Unit = "Count", Description = "Municipal account count" },
            new DashboardMetric { Name = "Budget Utilization", Value = 89.8, Unit = "%", Description = "Percentage of budget used" }
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
        await Task.Delay(200, cancellationToken);
    }
}
```

```powershell
dotnet build src/WileyWidget.Services
```

**✅ Checkpoint**: Models and service compile, full solution builds

---

## 🎨 Phase 2: Update ViewModel (15 minutes)

**File**: `WileyWidget.WinForms/ViewModels/DashboardViewModel.cs` (Replace entire file)

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

            var summary = await _dashboardService.GetDashboardSummaryAsync();

            Metrics.Clear();
            foreach (var metric in summary.Metrics)
            {
                Metrics.Add(metric);
            }

            MunicipalityName = summary.MunicipalityName;
            FiscalYear = summary.FiscalYear;
            LastUpdated = summary.GeneratedAt;

            _logger.LogInformation("Dashboard loaded with {Count} metrics", Metrics.Count);
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
            await _dashboardService.RefreshDataAsync();
            await LoadDashboardAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh failed");
            ErrorMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

**Update project references** in `WileyWidget.WinForms/WileyWidget.WinForms.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\src\WileyWidget.Models\WileyWidget.Models.csproj" />
  <ProjectReference Include="..\src\WileyWidget.Services.Abstractions\WileyWidget.Services.Abstractions.csproj" />
  <ProjectReference Include="..\src\WileyWidget.Services\WileyWidget.Services.csproj" />
</ItemGroup>
```

```powershell
dotnet build WileyWidget.WinForms
```

**✅ Checkpoint**: ViewModel compiles with service injection

---

## 📋 Next Steps

You now have:

- ✅ Dashboard models (DashboardMetric, DashboardSummary)
- ✅ Service interface (IDashboardService)
- ✅ Mock service implementation (DashboardService)
- ✅ Enhanced ViewModel with async commands

**Continue with**:

1. Phase 3: Create WinForms UI (see full plan)
2. Phase 4: Write unit tests
3. Phase 5: Integration testing

**Full Documentation**:

- `docs/DASHBOARD-E2E-DEVELOPMENT-PLAN.md` - Complete implementation guide
- `docs/DASHBOARD-TASK-CHECKLIST.md` - Detailed task list

---

## 🔧 Troubleshooting

**Issue**: Project reference not found
**Fix**: Ensure all projects are added to solution: `dotnet sln list`

**Issue**: Namespace errors
**Fix**: Check `using` statements match project namespaces

**Issue**: Build errors
**Fix**: Run `dotnet clean && dotnet build --no-restore`

---

## 📞 Need Help?

Check these resources:

- Microsoft Learn: WinForms data binding
- CommunityToolkit.Mvvm documentation
- Project-specific: `.vscode/copilot-instructions.md`

**MCP Enforcement**: Remember to use `mcp_filesystem_*` tools for all file operations!
