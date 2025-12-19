using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Business.Services;

/// <summary>
/// Service for querying department-specific expenses from QuickBooks.
/// TODO: Integrate with actual QuickBooks API (IppDotNetSdkForQuickBooksApiV3).
/// </summary>
public class DepartmentExpenseService : IDepartmentExpenseService
{
    private readonly ILogger<DepartmentExpenseService> _logger;
    private readonly IQuickBooksService _quickBooksService;

    public DepartmentExpenseService(
        ILogger<DepartmentExpenseService> logger,
        IQuickBooksService quickBooksService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));
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

            // Query QuickBooks API for bills (expenses) filtered by department/class
            var bills = await _quickBooksService.GetBillsAsync();

            // Filter bills by date range and department
            // TODO: Implement department filtering once ClassRef property is confirmed
            var departmentBills = bills.Where(bill =>
                bill.TxnDate >= startDate &&
                bill.TxnDate <= endDate);
                // bill.ClassRef?.Name == departmentName); // Commented out until ClassRef property confirmed

            // Sum the total amounts
            var totalExpenses = departmentBills.Sum(bill => (decimal)bill.TotalAmt);

            _logger.LogInformation("Found {BillCount} bills for department {Department}, total expenses: {Total}",
                departmentBills.Count(), departmentName, totalExpenses);

            return totalExpenses;
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
