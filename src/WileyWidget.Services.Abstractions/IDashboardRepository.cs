using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Repository interface for dashboard data access using EF Core
    /// </summary>
    public interface IDashboardRepository
    {
        /// <summary>
        /// Gets total budget for a fiscal year
        /// </summary>
        Task<decimal> GetTotalBudgetAsync(string fiscalYear);

        /// <summary>
        /// Gets total revenue for a fiscal year
        /// </summary>
        Task<decimal> GetTotalRevenueAsync(string fiscalYear);

        /// <summary>
        /// Gets total expenses for a fiscal year
        /// </summary>
        Task<decimal> GetTotalExpensesAsync(string fiscalYear);

        /// <summary>
        /// Gets revenue trend by month for a fiscal year
        /// </summary>
        Task<List<RevenueByMonth>> GetRevenueTrendAsync(string fiscalYear);

        /// <summary>
        /// Gets expense breakdown by department
        /// </summary>
        Task<List<ExpenseByDepartment>> GetExpenseBreakdownAsync(string fiscalYear);

        /// <summary>
        /// Gets dashboard metrics with trend information
        /// </summary>
        Task<List<DashboardMetric>> GetDashboardMetricsAsync(string fiscalYear);

        /// <summary>
        /// Gets recent financial transactions
        /// </summary>
        Task<List<RecentActivity>> GetRecentActivityAsync(int limit = 10);

        /// <summary>
        /// Gets count of active municipal accounts
        /// </summary>
        Task<int> GetActiveAccountCountAsync();

        /// <summary>
        /// Gets count of pending invoices
        /// </summary>
        Task<int> GetPendingInvoiceCountAsync();
    }

    /// <summary>
    /// Revenue aggregated by month
    /// </summary>
    public class RevenueByMonth
    {
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Year { get; set; }
    }

    /// <summary>
    /// Expenses aggregated by department
    /// </summary>
    public class ExpenseByDepartment
    {
        public string Department { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal PercentOfTotal { get; set; }
    }

    /// <summary>
    /// Recent financial activity item
    /// </summary>
    public class RecentActivity
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
