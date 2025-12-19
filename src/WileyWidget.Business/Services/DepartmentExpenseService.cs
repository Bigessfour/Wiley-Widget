using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Business.Services;

/// <summary>
/// Service for querying department-specific expenses from QuickBooks.
/// TODO: Integrate with actual QuickBooks API (IppDotNetSdkForQuickBooksApiV3).
/// </summary>
public class DepartmentExpenseService : IDepartmentExpenseService
{
    private readonly ILogger<DepartmentExpenseService> _logger;
    // TODO: Inject QuickBooks service/repository

    public DepartmentExpenseService(ILogger<DepartmentExpenseService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal> GetDepartmentExpensesAsync(
        string departmentName,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Querying expenses for {Department} from {StartDate} to {EndDate}",
                departmentName, startDate, endDate);

            // TODO: Query QuickBooks API for department expenses
            // Example: Filter by account categories or department codes
            // var qbService = ...; var expenses = await qbService.QueryExpenses(...)

            // Stub implementation: return sample data
            return departmentName switch
            {
                "Water" => 45000m,
                "Sewer" => 68000m,
                "Trash" => 28000m,
                "Apartments" => 95000m,
                _ => 0m
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses for department {Department}", departmentName);
            throw;
        }
    }

    public async Task<Dictionary<string, decimal>> GetAllDepartmentExpensesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Querying expenses for all departments from {StartDate} to {EndDate}",
                startDate, endDate);

            var departments = new[] { "Water", "Sewer", "Trash", "Apartments" };
            var expenses = new Dictionary<string, decimal>();

            foreach (var dept in departments)
            {
                expenses[dept] = await GetDepartmentExpensesAsync(dept, startDate, endDate, cancellationToken);
            }

            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses for all departments");
            throw;
        }
    }

    public async Task<decimal> GetRollingAverageExpensesAsync(
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calculating 12-month rolling average for {Department}", departmentName);

            var endDate = DateTime.Now;
            var startDate = endDate.AddMonths(-12);

            var totalExpenses = await GetDepartmentExpensesAsync(
                departmentName, startDate, endDate, cancellationToken);

            // Return monthly average
            return totalExpenses / 12;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating rolling average for {Department}", departmentName);
            throw;
        }
    }
}
