using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;

namespace WileyWidget.WinForms.ViewModels
{
    // Lightweight view model that loads simple chart data from the AppDbContext.
    // Uses IDbContextFactory so the view model can create short-lived DbContexts safely from the WinForms DI container.
    public class ChartViewModel
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;

        public ChartViewModel(IDbContextFactory<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        // Simple dictionary where key = department name and value = variance (actual - budget)
        public Dictionary<string, double> ChartData { get; private set; } = new Dictionary<string, double>();

        // Error message if data loading fails
        public string? ErrorMessage { get; private set; }

        public async Task LoadChartDataAsync()
        {
            ErrorMessage = null;
            try
            {
                using var db = _dbFactory.CreateDbContext();

                var query = await db.Departments
                    .Select(d => new
                    {
                        Name = d.Name,
                        Budgeted = d.MunicipalAccounts.Sum(a => (decimal?)a.BudgetAmount) ?? 0m,
                        Actual = d.MunicipalAccounts.Sum(a => (decimal?)a.Balance) ?? 0m
                    })
                    .ToListAsync();

                var dict = query
                    .Select(x => new { x.Name, Variance = (double)(x.Actual - x.Budgeted) })
                    .ToDictionary(k => string.IsNullOrWhiteSpace(k.Name) ? "Unknown" : k.Name, v => v.Variance);

                ChartData = dict;
            }
            catch (Exception ex)
            {
                // Capture error for UI display
                ErrorMessage = $"Unable to load chart data: {ex.Message}";
                ChartData = new Dictionary<string, double>();
            }
        }
    }
}
