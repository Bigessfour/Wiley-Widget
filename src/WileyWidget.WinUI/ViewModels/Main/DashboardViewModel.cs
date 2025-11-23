using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class DashboardViewModel : ObservableRecipient
    {
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly AppDbContext _dbContext;

        [ObservableProperty]
        private string title = "Dashboard";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<DashboardItemDisplay> dashboardItems = new();

        [ObservableProperty]
        private decimal totalBudget;

        [ObservableProperty]
        private decimal totalRevenue;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        private int activeBudgetEntries;

        [ObservableProperty]
        private int departmentCount;

        public DashboardViewModel(
            ILogger<DashboardViewModel> logger,
            AppDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;

            LoadDashboardCommand = new AsyncRelayCommand(LoadDashboardAsync);
        }

        public IAsyncRelayCommand LoadDashboardCommand { get; }

        private async Task LoadDashboardAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading dashboard data");

                var currentYear = DateTime.Now.Year;

                // Load budget summary
                var budgetSummary = await _dbContext.BudgetEntries
                    .Where(b => b.FiscalYear == currentYear)
                    .GroupBy(b => 1)
                    .Select(g => new
                    {
                        TotalBudget = g.Sum(b => b.BudgetedAmount),
                        TotalActual = g.Sum(b => b.ActualAmount),
                        Count = g.Count()
                    })
                    .FirstOrDefaultAsync();

                TotalBudget = budgetSummary?.TotalBudget ?? 0m;
                TotalRevenue = budgetSummary?.TotalActual ?? 0m;
                TotalExpenses = TotalBudget - TotalRevenue;
                ActiveBudgetEntries = budgetSummary?.Count ?? 0;

                // Load department count
                DepartmentCount = await _dbContext.Departments
                    .CountAsync();

                // Load fund summaries for dashboard cards
                var fundSummaries = await _dbContext.BudgetEntries
                    .Include(b => b.Fund)
                    .Where(b => b.FiscalYear == currentYear)
                    .GroupBy(b => new { b.Fund!.Name, b.FundType })
                    .Select(g => new
                    {
                        FundName = g.Key.Name,
                        FundType = g.Key.FundType,
                        TotalBudget = g.Sum(b => b.BudgetedAmount),
                        TotalActual = g.Sum(b => b.ActualAmount),
                        EntryCount = g.Count()
                    })
                    .OrderByDescending(f => f.TotalBudget)
                    .Take(6)
                    .ToListAsync();

                DashboardItems.Clear();

                // Add summary card
                DashboardItems.Add(new DashboardItemDisplay
                {
                    Title = "Total Budget",
                    Description = $"Annual budget allocation for {currentYear}",
                    Icon = "Money",
                    Value = TotalBudget.ToString("C0"),
                    Count = ActiveBudgetEntries,
                    Status = "Active",
                    Category = "Summary"
                });

                // Add department card
                DashboardItems.Add(new DashboardItemDisplay
                {
                    Title = "Active Departments",
                    Description = "Departments with budget allocations",
                    Icon = "Organization",
                    Value = DepartmentCount.ToString(),
                    Count = DepartmentCount,
                    Status = "Active",
                    Category = "Departments"
                });

                // Add fund summary cards
                foreach (var fund in fundSummaries)
                {
                    var utilizationPercent = fund.TotalBudget > 0
                        ? (fund.TotalActual / fund.TotalBudget * 100)
                        : 0;

                    DashboardItems.Add(new DashboardItemDisplay
                    {
                        Title = fund.FundName ?? "Unknown Fund",
                        Description = $"{fund.FundType} - {utilizationPercent:F1}% utilized",
                        Icon = GetFundIcon(fund.FundType),
                        Value = fund.TotalBudget.ToString("C0"),
                        Count = fund.EntryCount,
                        Status = utilizationPercent > 90 ? "Warning" : "Active",
                        Category = "Fund"
                    });
                }

                _logger.LogInformation("Dashboard loaded successfully with {Count} items", DashboardItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard");
                DashboardItems.Clear();

                // Add error card
                DashboardItems.Add(new DashboardItemDisplay
                {
                    Title = "Error Loading Dashboard",
                    Description = ex.Message,
                    Icon = "ErrorBadge",
                    Value = "N/A",
                    Count = 0,
                    Status = "Error",
                    Category = "Error"
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string GetFundIcon(FundType fundType) => fundType switch
        {
            FundType.GeneralFund => "CityNext",
            FundType.EnterpriseFund => "Factory",
            FundType.SpecialRevenue => "TagSolid",
            FundType.DebtService => "Money",
            FundType.CapitalProjects => "Build",
            FundType.PermanentFund => "Savings",
            _ => "Bank"
        };
    }

    /// <summary>
    /// Display model for dashboard items
    /// </summary>
    public class DashboardItemDisplay
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
