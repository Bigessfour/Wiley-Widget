using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models.Entities;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class BudgetViewModel : ObservableRecipient
    {
        private readonly ILogger<BudgetViewModel> _logger;
        private readonly AppDbContext _dbContext;

        [ObservableProperty]
        private string title = "Budget Management";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private ObservableCollection<BudgetItemDisplay> budgetItems = new();

        [ObservableProperty]
        private decimal totalBudgeted;

        [ObservableProperty]
        private decimal totalActual;

        [ObservableProperty]
        private decimal variance;

        [ObservableProperty]
        private int selectedFiscalYear = DateTime.Now.Year;

        public BudgetViewModel(
            ILogger<BudgetViewModel> logger,
            AppDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;

            LoadBudgetCommand = new AsyncRelayCommand(LoadBudgetAsync);
            SaveBudgetCommand = new AsyncRelayCommand(SaveBudgetAsync);
        }

        public IAsyncRelayCommand LoadBudgetCommand { get; }
        public IAsyncRelayCommand SaveBudgetCommand { get; }

        private async Task LoadBudgetAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading budget data for fiscal year {FiscalYear}", SelectedFiscalYear);

                var budgetEntries = await _dbContext.BudgetEntries
                    .Include(b => b.Department)
                    .Include(b => b.Fund)
                    .Include(b => b.Transactions)
                    .Where(b => b.FiscalYear == SelectedFiscalYear)
                    .OrderBy(b => b.AccountNumber)
                    .AsNoTracking()
                    .ToListAsync();

                BudgetItems.Clear();
                
                foreach (var entry in budgetEntries)
                {
                    var actual = entry.ActualAmount;
                    BudgetItems.Add(new BudgetItemDisplay
                    {
                        Id = entry.Id,
                        AccountNumber = entry.AccountNumber,
                        Name = entry.Description,
                        BudgetedAmount = entry.BudgetedAmount,
                        ActualAmount = actual,
                        Variance = entry.BudgetedAmount - actual,
                        Department = entry.Department?.Name ?? "N/A",
                        Fund = entry.Fund?.Name ?? "N/A",
                        Category = entry.FundType.ToString()
                    });
                }

                // Calculate totals
                TotalBudgeted = BudgetItems.Sum(b => b.BudgetedAmount);
                TotalActual = BudgetItems.Sum(b => b.ActualAmount);
                Variance = TotalBudgeted - TotalActual;

                _logger.LogInformation("Budget data loaded successfully: {Count} entries, Total Budget: {Total:C}",
                    BudgetItems.Count, TotalBudgeted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load budget data");
                BudgetItems.Clear();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveBudgetAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Saving budget data");

                // Update modified budget entries
                foreach (var item in BudgetItems)
                {
                    var entry = await _dbContext.BudgetEntries.FindAsync(item.Id);
                    if (entry != null)
                    {
                        entry.BudgetedAmount = item.BudgetedAmount;
                        entry.UpdatedAt = DateTime.UtcNow;
                        _dbContext.BudgetEntries.Update(entry);
                    }
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Budget data saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save budget data");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Display model for budget items in the UI
    /// </summary>
    public class BudgetItemDisplay
    {
        public int Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal BudgetedAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal Variance { get; set; }
        public string Department { get; set; } = string.Empty;
        public string Fund { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
