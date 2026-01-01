using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Business.Services;

/// <summary>
/// Service for querying department-specific expenses from QuickBooks.
/// Implements QuickBooks API integration for real expense data.
/// Falls back to sample data when QuickBooks is unavailable (dev/testing).
/// </summary>
public class DepartmentExpenseService : IDepartmentExpenseService
{
    private readonly ILogger<DepartmentExpenseService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IQuickBooksService _quickBooksService;
    private readonly bool _useQuickBooksApi;

    // Known departments (case-insensitive)
    private static readonly HashSet<string> _knownDepartments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Water",
        "Sewer",
        "Trash",
        "Apartments",
        "Electric",
        "Gas"
    };

    /// <summary>
    /// Simple representation of an expense line returned from QuickBooks (Amount only).
    /// </summary>
    private record ExpenseLine(decimal Amount);

    public DepartmentExpenseService(
        ILogger<DepartmentExpenseService> logger,
        IConfiguration configuration,
        IQuickBooksService quickBooksService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _quickBooksService = quickBooksService ?? throw new ArgumentNullException(nameof(quickBooksService));

        // Check if QuickBooks integration is enabled
        _useQuickBooksApi = _configuration.GetValue<bool>("QuickBooks:Enabled", false);

        if (_useQuickBooksApi)
        {
            _logger.LogInformation("QuickBooks integration enabled.");
        }
        else
        {
            _logger.LogWarning("QuickBooks integration disabled - using sample data (set QuickBooks:Enabled=true in config)");
        }
    }

    /// <summary>
    /// Gets the total expenses for the specified department between the given dates.
    /// Uses QuickBooks when enabled and available; falls back to realistic sample data on error or when disabled.
    /// </summary>
    /// <param name="departmentName">Department name (case-insensitive)</param>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total expense amount</returns>
    public async Task<decimal> GetDepartmentExpensesAsync(
        string departmentName,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            throw new ArgumentException("Department name cannot be null or empty", nameof(departmentName));

        if (startDate > endDate)
            throw new ArgumentException("Start date must be less than or equal to end date", nameof(startDate));

        if (!_knownDepartments.Contains(departmentName))
            throw new ArgumentException($"Unknown department '{departmentName}'. Known departments are: {string.Join(", ", _knownDepartments)}", nameof(departmentName));

        var canonicalDepartment = _knownDepartments.First(d => string.Equals(d, departmentName, StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation("Querying expenses for {Department} from {StartDate:O} to {EndDate:O} (QuickBooksEnabled={QuickBooksEnabled})",
            canonicalDepartment, startDate, endDate, _useQuickBooksApi);

        // Try QuickBooks when enabled
        if (_useQuickBooksApi)
        {
            try
            {
                var expenseLines = await _quickBooksService.QueryExpensesByDepartmentAsync(canonicalDepartment, startDate, endDate, cancellationToken).ConfigureAwait(false);

                if (expenseLines != null)
                {
                    var total = expenseLines.Sum(e => e.Amount);
                    var count = expenseLines.Count();
                    _logger.LogInformation("QuickBooks returned {Count} expense lines for {Department} totaling {Amount:C2}", count, canonicalDepartment, total);
                    return total;
                }

                _logger.LogWarning("QuickBooks returned no data for {Department}; falling back to sample data", canonicalDepartment);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetDepartmentExpensesAsync for {Department} was canceled", canonicalDepartment);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QuickBooks query failed for department {Department}; falling back to sample data", canonicalDepartment);
            }
        }

        // Sample data fallback
        var monthsDiff = Math.Max(1, (endDate - startDate).Days / 30.0);
        var monthlyExpense = canonicalDepartment switch
        {
            "Water" => 45000m,
            "Sewer" => 68000m,
            "Trash" => 28000m,
            "Apartments" => 95000m,
            "Electric" => 120000m,
            "Gas" => 85000m,
            _ => 10000m
        };

        var totalExpense = monthlyExpense * (decimal)monthsDiff;

        var source = _useQuickBooksApi ? "Sample (after QuickBooks failure)" : "Sample";
        _logger.LogInformation("{Source} expenses for {Department}: {Amount:C2} ({Months:F1} months between {StartDate:yyyy-MM-dd} and {EndDate:yyyy-MM-dd})",
            source, canonicalDepartment, totalExpense, monthsDiff, startDate, endDate);

        return totalExpense;
    }

    /// <summary>
    /// Queries expenses for all known departments in parallel and returns a dictionary of totals.
    /// </summary>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping department name to total expense</returns>
    public async Task<Dictionary<string, decimal>> GetAllDepartmentExpensesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            throw new ArgumentException("Start date must be less than or equal to end date", nameof(startDate));

        _logger.LogInformation("Querying expenses for all departments from {StartDate:O} to {EndDate:O} (QuickBooksEnabled={QuickBooksEnabled})",
            startDate, endDate, _useQuickBooksApi);

        var departments = _knownDepartments.ToArray(); // snapshot for deterministic ordering

        var tasks = departments.Select(d => GetDepartmentExpensesAsync(d, startDate, endDate, cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var dict = new Dictionary<string, decimal>(departments.Length, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < departments.Length; i++)
        {
            dict[departments[i]] = results[i];
        }

        return dict;
    }

    /// <summary>
    /// Calculates the 12-month rolling average monthly expenses for the specified department.
    /// Uses QuickBooks when enabled.
    /// </summary>
    /// <param name="departmentName">Department name (case-insensitive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Monthly rolling average (decimal)</returns>
    public async Task<decimal> GetRollingAverageExpensesAsync(
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            throw new ArgumentException("Department name cannot be null or empty", nameof(departmentName));

        _logger.LogInformation("Calculating 12-month rolling average for {Department} (QuickBooksEnabled={QuickBooksEnabled})", departmentName, _useQuickBooksApi);

        var endDate = DateTime.Now;
        var startDate = endDate.AddMonths(-12);

        var totalExpenses = await GetDepartmentExpensesAsync(departmentName, startDate, endDate, cancellationToken).ConfigureAwait(false);

        // Return monthly average
        return totalExpenses / 12m;
    }
}
