#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// Provides safe, read-only enterprise and budget data tools for kernel-based agents.
    /// All operations are restricted to data retrieval and analysis; no mutations are permitted.
    /// Injects repositories and performs async data access with comprehensive logging.
    /// </summary>
    public sealed class EnterpriseDataTools
    {
        private readonly IEnterpriseRepository _enterpriseRepository;
        private readonly IBudgetRepository _budgetRepository;
        private readonly ILogger<EnterpriseDataTools>? _logger;

        /// <summary>
        /// Initializes a new instance of the EnterpriseDataTools plugin.
        /// </summary>
        /// <param name="enterpriseRepository">Repository for accessing enterprise data.</param>
        /// <param name="budgetRepository">Repository for accessing budget data.</param>
        /// <param name="logger">Optional logger for audit and diagnostic logging.</param>
        public EnterpriseDataTools(
            IEnterpriseRepository enterpriseRepository,
            IBudgetRepository budgetRepository,
            ILogger<EnterpriseDataTools>? logger = null)
        {
            _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _logger = logger;
        }

        /// <summary>
        /// Represents a summary of an enterprise's key financial metrics.
        /// </summary>
        [Description("Summary information for an enterprise")]
        public sealed class EnterpriseSummary
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("monthlyRevenue")]
            public decimal MonthlyRevenue { get; set; }

            [JsonPropertyName("monthlyExpenses")]
            public decimal MonthlyExpenses { get; set; }

            [JsonPropertyName("monthlyBalance")]
            public decimal MonthlyBalance { get; set; }

            [JsonPropertyName("citizenCount")]
            public int CitizenCount { get; set; }

            [JsonPropertyName("currentRate")]
            public decimal CurrentRate { get; set; }

            [JsonPropertyName("breakEvenRate")]
            public decimal BreakEvenRate { get; set; }

            [JsonPropertyName("isProfitable")]
            public bool IsProfitable { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }
        }

        /// <summary>
        /// Represents a monthly revenue or expense trend.
        /// </summary>
        [Description("Monthly financial trend data")]
        public sealed class MonthlyTrend
        {
            [JsonPropertyName("month")]
            public string Month { get; set; } = string.Empty;

            [JsonPropertyName("year")]
            public int Year { get; set; }

            [JsonPropertyName("revenue")]
            public decimal Revenue { get; set; }

            [JsonPropertyName("expenses")]
            public decimal Expenses { get; set; }

            [JsonPropertyName("balance")]
            public decimal Balance { get; set; }
        }

        /// <summary>
        /// Gets all enterprises as a JSON-serializable list.
        /// </summary>
        /// <returns>List of enterprise summaries.</returns>
        [KernelFunction("get_all_enterprises")]
        [Description("Retrieve a list of all municipal enterprises with their current financial metrics.")]
        public async Task<List<EnterpriseSummary>> GetAllEnterprises()
        {
            _logger?.LogInformation("EnterpriseDataTools: GetAllEnterprises invoked");

            try
            {
                var enterprises = await _enterpriseRepository.GetAllAsync();
                var summaries = enterprises
                    .Select(e => MapToSummary(e))
                    .OrderBy(s => s.Name)
                    .ToList();

                _logger?.LogInformation("EnterpriseDataTools: GetAllEnterprises returned {Count} enterprises", summaries.Count);

                return summaries;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "EnterpriseDataTools: Error in GetAllEnterprises");
                throw;
            }
        }

        /// <summary>
        /// Gets department expense breakdown for a specific enterprise.
        /// </summary>
        /// <param name="enterpriseName">Name of the enterprise to query.</param>
        /// <returns>Dictionary mapping department names to their total expenses.</returns>
        [KernelFunction("get_department_expenses")]
        [Description("Get department-level expense breakdown for a specific enterprise by name.")]
        public async Task<Dictionary<string, decimal>> GetDepartmentExpenses(
            [Description("Enterprise name (e.g., 'Water', 'Sewer')")] string enterpriseName)
        {
            if (string.IsNullOrWhiteSpace(enterpriseName))
            {
                throw new ArgumentException("Enterprise name cannot be null or empty.", nameof(enterpriseName));
            }

            _logger?.LogInformation("EnterpriseDataTools: GetDepartmentExpenses invoked for enterprise '{EnterpriseName}'", enterpriseName);

            try
            {
                var enterprises = await _enterpriseRepository.GetAllAsync();
                var enterprise = enterprises.FirstOrDefault(e => string.Equals(e.Name, enterpriseName, StringComparison.OrdinalIgnoreCase));

                if (enterprise == null)
                {
                    _logger?.LogWarning("EnterpriseDataTools: Enterprise '{EnterpriseName}' not found", enterpriseName);
                    return new Dictionary<string, decimal>();
                }

                // Retrieve budget entries for this enterprise by fiscal year
                var currentYear = DateTime.UtcNow.Year;
                var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(currentYear);

                // Group by department and sum expenses
                var expenses = budgetEntries
                    .GroupBy(b => b.Department?.Name ?? "Unknown")
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(b => b.ActualAmount))
                    .OrderByDescending(kvp => kvp.Value)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                _logger?.LogInformation("EnterpriseDataTools: GetDepartmentExpenses returned {Count} departments for '{EnterpriseName}'", expenses.Count, enterpriseName);

                return expenses;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "EnterpriseDataTools: Error in GetDepartmentExpenses for '{EnterpriseName}'", enterpriseName);
                throw;
            }
        }

        /// <summary>
        /// Gets budget variance analysis for a specific enterprise.
        /// </summary>
        /// <param name="enterpriseId">ID of the enterprise.</param>
        /// <param name="year">Optional fiscal year (defaults to current year).</param>
        /// <returns>Budget variance analysis object.</returns>
        [KernelFunction("get_budget_variance")]
        [Description("Get budget variance analysis for an enterprise for a specific fiscal year.")]
        public async Task<BudgetVarianceAnalysis> GetBudgetVariance(
            [Description("Enterprise ID")] int enterpriseId,
            [Description("Fiscal year (optional, defaults to current year)")] int? year = null)
        {
            if (enterpriseId <= 0)
            {
                throw new ArgumentException("Enterprise ID must be greater than zero.", nameof(enterpriseId));
            }

            var fiscalYear = year ?? DateTime.UtcNow.Year;

            _logger?.LogInformation("EnterpriseDataTools: GetBudgetVariance invoked for enterprise {EnterpriseId}, year {Year}", enterpriseId, fiscalYear);

            try
            {
                var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);
                if (enterprise == null)
                {
                    _logger?.LogWarning("EnterpriseDataTools: Enterprise {EnterpriseId} not found", enterpriseId);
                    return new BudgetVarianceAnalysis
                    {
                        AnalysisDate = DateTime.UtcNow,
                        BudgetPeriod = $"FY{fiscalYear}",
                        TotalBudgeted = 0,
                        TotalActual = 0,
                        TotalVariance = 0,
                        TotalVariancePercentage = 0,
                    };
                }

                // Retrieve variance analysis for the enterprise
                var startDate = new DateTime(fiscalYear, 1, 1);
                var endDate = new DateTime(fiscalYear, 12, 31);

                var analysis = await _budgetRepository.GetVarianceAnalysisByEnterpriseAsync(
                    enterpriseId,
                    startDate,
                    endDate);

                _logger?.LogInformation(
                    "EnterpriseDataTools: GetBudgetVariance returned analysis for enterprise {EnterpriseId}: TotalBudgeted={TotalBudgeted}, TotalActual={TotalActual}, Variance={Variance}",
                    enterpriseId,
                    analysis.TotalBudgeted,
                    analysis.TotalActual,
                    analysis.TotalVariance);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "EnterpriseDataTools: Error in GetBudgetVariance for enterprise {EnterpriseId}, year {Year}", enterpriseId, fiscalYear);
                throw;
            }
        }

        /// <summary>
        /// Gets monthly revenue and expense trends for an enterprise over a specified number of years.
        /// </summary>
        /// <param name="enterpriseId">ID of the enterprise.</param>
        /// <param name="years">Number of historical years to retrieve (default: 2).</param>
        /// <returns>List of monthly trend objects.</returns>
        [KernelFunction("get_monthly_revenue_trends")]
        [Description("Get historical monthly revenue and expense trends for an enterprise over multiple years.")]
        public async Task<List<MonthlyTrend>> GetMonthlyRevenueTrends(
            [Description("Enterprise ID")] int enterpriseId,
            [Description("Number of years to retrieve (default: 2)")] int years = 2)
        {
            if (enterpriseId <= 0)
            {
                throw new ArgumentException("Enterprise ID must be greater than zero.", nameof(enterpriseId));
            }

            if (years < 1)
            {
                throw new ArgumentException("Years must be at least 1.", nameof(years));
            }

            var currentYear = DateTime.UtcNow.Year;
            var startYear = currentYear - years + 1;

            _logger?.LogInformation(
                "EnterpriseDataTools: GetMonthlyRevenueTrends invoked for enterprise {EnterpriseId}, years={Years} (from {StartYear} to {CurrentYear})",
                enterpriseId,
                years,
                startYear,
                currentYear);

            try
            {
                var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);
                if (enterprise == null)
                {
                    _logger?.LogWarning("EnterpriseDataTools: Enterprise {EnterpriseId} not found", enterpriseId);
                    return new List<MonthlyTrend>();
                }

                var trends = new List<MonthlyTrend>();

                // Generate monthly trend data for the requested period
                for (int y = startYear; y <= currentYear; y++)
                {
                    for (int m = 1; m <= 12; m++)
                    {
                        // Skip future months in the current year
                        if (y == currentYear && m > DateTime.UtcNow.Month)
                        {
                            break;
                        }

                        var startDate = new DateTime(y, m, 1);
                        var endDate = new DateTime(y, m, DateTime.DaysInMonth(y, m));

                        var budgetEntries = await _budgetRepository.GetByDateRangeAsync(startDate, endDate);

                        var monthRevenue = enterprise.MonthlyRevenue; // Could be more nuanced with actual transactions
                        var monthExpenses = budgetEntries.Sum(b => b.ActualAmount);

                        trends.Add(new MonthlyTrend
                        {
                            Month = startDate.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture),
                            Year = y,
                            Revenue = monthRevenue,
                            Expenses = monthExpenses,
                            Balance = monthRevenue - monthExpenses,
                        });
                    }
                }

                _logger?.LogInformation(
                    "EnterpriseDataTools: GetMonthlyRevenueTrends returned {Count} months for enterprise {EnterpriseId}",
                    trends.Count,
                    enterpriseId);

                return trends;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "EnterpriseDataTools: Error in GetMonthlyRevenueTrends for enterprise {EnterpriseId}", enterpriseId);
                throw;
            }
        }

        /// <summary>
        /// Maps an Enterprise entity to an EnterpriseSummary DTO.
        /// </summary>
        private static EnterpriseSummary MapToSummary(Enterprise enterprise)
        {
            return new EnterpriseSummary
            {
                Id = enterprise.Id,
                Name = enterprise.Name,
                Type = enterprise.Type,
                Status = enterprise.Status.ToString(),
                MonthlyRevenue = enterprise.MonthlyRevenue,
                MonthlyExpenses = enterprise.MonthlyExpenses,
                MonthlyBalance = enterprise.MonthlyBalance,
                CitizenCount = enterprise.CitizenCount,
                CurrentRate = enterprise.CurrentRate,
                BreakEvenRate = enterprise.BreakEvenRate,
                IsProfitable = enterprise.IsProfitable(),
                Description = enterprise.Description,
            };
        }
    }
}
