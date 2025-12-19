namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Service interface for querying department-specific expenses from QuickBooks.
/// Provides aggregated expense data for Water, Sewer, Trash, and Apartments departments.
/// </summary>
public interface IDepartmentExpenseService
{
    /// <summary>
    /// Gets monthly expenses for a specific department from QuickBooks.
    /// </summary>
    /// <param name="departmentName">Department name (Water, Sewer, Trash, Apartments)</param>
    /// <param name="startDate">Start date for expense period</param>
    /// <param name="endDate">End date for expense period</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total expenses for the period</returns>
    Task<decimal> GetDepartmentExpensesAsync(
        string departmentName,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets monthly expenses for all departments.
    /// </summary>
    /// <param name="startDate">Start date for expense period</param>
    /// <param name="endDate">End date for expense period</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of department name to total expenses</returns>
    Task<Dictionary<string, decimal>> GetAllDepartmentExpensesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets rolling 12-month average expenses for a department.
    /// </summary>
    /// <param name="departmentName">Department name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>12-month rolling average</returns>
    Task<decimal> GetRollingAverageExpensesAsync(
        string departmentName,
        CancellationToken cancellationToken = default);
}
