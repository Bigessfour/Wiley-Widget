using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for building dynamic context in municipal finance operations.
    /// Implements IWileyWidgetContextService to provide contextual information for AI and system operations.
    /// Enhanced with data anonymization for privacy-compliant AI integration.
    /// </summary>
    public class WileyWidgetContextService : IWileyWidgetContextService
    {
        private readonly ILogger<WileyWidgetContextService> _logger;
        private readonly IEnterpriseRepository _enterpriseRepository;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IAuditRepository _auditRepository;
        private readonly IDataAnonymizerService _anonymizerService;

        /// <summary>
        /// Initializes a new instance of the <see cref="WileyWidgetContextService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging operations.</param>
        /// <param name="enterpriseRepository">The enterprise repository for data access.</param>
        /// <param name="budgetRepository">The budget repository for data access.</param>
        /// <param name="auditRepository">The audit repository for operational metrics.</param>
        /// <param name="anonymizerService">The data anonymizer service for privacy protection.</param>
        public WileyWidgetContextService(
            ILogger<WileyWidgetContextService> logger,
            IEnterpriseRepository enterpriseRepository,
            IBudgetRepository budgetRepository,
            IAuditRepository auditRepository,
            IDataAnonymizerService anonymizerService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
            _anonymizerService = anonymizerService ?? throw new ArgumentNullException(nameof(anonymizerService));

            _logger.LogInformation("WileyWidgetContextService initialized with data anonymization support");
        }

        /// <summary>
        /// Builds the current system context asynchronously for municipal finance systems.
        /// Includes current system status, configuration, and operational parameters.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A string representing the current system context for municipal finance operations.</returns>
        public async Task<string> BuildCurrentSystemContextAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Building current system context.");

            var sb = new StringBuilder();
            sb.AppendLine("=== WileyWidget Municipal Finance System Context ===");
            sb.AppendLine($"System Name: WileyWidget Municipal Finance System");
            sb.AppendLine($"Version: 1.0.0");
            sb.AppendLine($"Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}");
            sb.AppendLine($"Machine Name: {Environment.MachineName}");
            sb.AppendLine($"OS Version: {Environment.OSVersion}");
            sb.AppendLine($"Processor Count: {Environment.ProcessorCount}");
            sb.AppendLine($"Current Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Time Zone: {TimeZoneInfo.Local.DisplayName}");
            sb.AppendLine();

            // Aggregate active enterprises (with anonymization for privacy)
            var enterprises = await _enterpriseRepository.GetAllAsync();
            var activeEnterprises = enterprises.Where(e => e.Status == EnterpriseStatus.Active).ToList();

            _logger.LogInformation("Anonymizing {Count} active enterprises for AI context", activeEnterprises.Count);
            var anonymizedEnterprises = _anonymizerService.AnonymizeEnterprises(activeEnterprises).ToList();

            sb.AppendLine("Active Enterprises (Anonymized for Privacy):");
            foreach (var ent in anonymizedEnterprises)
            {
                sb.AppendLine($"- {ent.Name} (ID: {ent.Id}, Type: {ent.Type})");
            }
            sb.AppendLine($"Total Active Enterprises: {anonymizedEnterprises.Count}");
            sb.AppendLine();

            // Aggregate budgets for current fiscal year
            var currentYear = DateTime.Now.Year;
            var budgets = await _budgetRepository.GetByFiscalYearAsync(currentYear);
            sb.AppendLine($"Budgets for Fiscal Year {currentYear}:");
            var totalBudget = budgets.Sum(b => b.TotalBudget);
            var totalSpent = budgets.Sum(b => b.ActualSpent);
            sb.AppendLine($"- Total Budget: ${totalBudget:N2}");
            sb.AppendLine($"- Total Spent: ${totalSpent:N2}");
            sb.AppendLine($"- Remaining: ${(totalBudget - totalSpent):N2}");
            sb.AppendLine($"Budget Entries: {budgets.Count()}");
            sb.AppendLine();

            _logger.LogInformation("System context built successfully.");
            return sb.ToString();
        }

        /// <summary>
        /// Gets the enterprise context for a specific enterprise ID in municipal finance.
        /// Includes enterprise-specific data such as financial entities, departments, and organizational structure.
        /// </summary>
        /// <param name="enterpriseId">The ID of the enterprise within the municipal finance system.</param>
        /// <returns>A string representing the enterprise context for the specified ID.</returns>
        public async Task<string> GetEnterpriseContextAsync(int enterpriseId)
        {
            _logger.LogInformation("Getting enterprise context for ID: {EnterpriseId}", enterpriseId);

            var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);
            if (enterprise == null)
            {
                return $"Enterprise with ID {enterpriseId} not found.";
            }

            // Anonymize enterprise data for privacy
            var anonymizedEnterprise = _anonymizerService.AnonymizeEnterprise(enterprise);
            _logger.LogInformation("Enterprise data anonymized for AI context: {EnterpriseId}", enterpriseId);

            var sb = new StringBuilder();
            sb.AppendLine($"=== Enterprise Context (Anonymized): {anonymizedEnterprise.Name} ===");
            sb.AppendLine($"ID: {anonymizedEnterprise.Id}");
            sb.AppendLine($"Name: {anonymizedEnterprise.Name}");
            sb.AppendLine($"Type: {anonymizedEnterprise.Type}");
            sb.AppendLine($"Description: {anonymizedEnterprise.Description ?? "N/A"}");
            sb.AppendLine($"Current Rate: ${enterprise.CurrentRate:N2}");
            sb.AppendLine($"Monthly Expenses: ${enterprise.MonthlyExpenses:N2}");
            sb.AppendLine($"Monthly Revenue: ${enterprise.MonthlyRevenue:N2}");
            sb.AppendLine($"Status: {anonymizedEnterprise.Status}");
            sb.AppendLine($"Created: {anonymizedEnterprise.CreatedDate:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Last Modified: {anonymizedEnterprise.ModifiedDate:yyyy-MM-dd HH:mm:ss}");

            _logger.LogInformation("Enterprise context retrieved for ID: {EnterpriseId}", enterpriseId);
            return sb.ToString();
        }

        /// <summary>
        /// Gets the budget context for a specified date range in municipal finance.
        /// Includes budget allocations, expenditures, and financial planning data for the given period.
        /// </summary>
        /// <param name="startDate">The start date of the budget period (optional).</param>
        /// <param name="endDate">The end date of the budget period (optional).</param>
        /// <returns>A string representing the budget context for the specified date range.</returns>
        public async Task<string> GetBudgetContextAsync(DateTime? startDate, DateTime? endDate)
        {
            _logger.LogInformation("Getting budget context for date range: {StartDate} to {EndDate}", startDate, endDate);

            // Default to current year if not specified
            var start = startDate ?? new DateTime(DateTime.Now.Year, 1, 1);
            var end = endDate ?? new DateTime(DateTime.Now.Year, 12, 31);

            var budgetSummary = await _budgetRepository.GetBudgetSummaryAsync(start, end);

            var sb = new StringBuilder();
            sb.AppendLine($"=== Budget Context (Anonymized): {start:yyyy-MM-dd} to {end:yyyy-MM-dd} ===");
            sb.AppendLine($"Analysis Date: {budgetSummary.AnalysisDate:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Budget Period: {budgetSummary.BudgetPeriod ?? "N/A"}");
            sb.AppendLine($"Total Budgeted: ${budgetSummary.TotalBudgeted:N2}");
            sb.AppendLine($"Total Actual: ${budgetSummary.TotalActual:N2}");
            sb.AppendLine($"Total Variance: ${budgetSummary.TotalVariance:N2} ({budgetSummary.TotalVariancePercentage:N2}%)");
            sb.AppendLine();
            sb.AppendLine("Fund Summaries (Anonymized):");
            foreach (var fund in budgetSummary.FundSummaries)
            {
                var anonymizedFundName = Anonymize(fund.FundName ?? "Unknown");
                sb.AppendLine($"- {anonymizedFundName}: Budgeted ${fund.Budgeted:N2}, Actual ${fund.Actual:N2}, Variance ${fund.Variance:N2}");
            }
            sb.AppendLine();
            sb.AppendLine("Department Summaries (Anonymized):");
            foreach (var dept in budgetSummary.DepartmentSummaries)
            {
                var anonymizedDeptName = Anonymize(dept.DepartmentName ?? "Unknown");
                sb.AppendLine($"- {anonymizedDeptName}: Budgeted ${dept.Budgeted:N2}, Actual ${dept.Actual:N2}, Variance ${dept.Variance:N2}");
            }

            _logger.LogInformation("Budget context retrieved and anonymized for period: {StartDate} to {EndDate}", start, end);
            return sb.ToString();
        }

        /// <summary>
        /// Gets the operational context asynchronously for municipal finance operations.
        /// Includes current operational status, active processes, and system performance metrics.
        /// </summary>
        /// <returns>A string representing the operational context for municipal finance systems.</returns>
        public async Task<string> GetOperationalContextAsync()
        {
            _logger.LogInformation("Getting operational context.");

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-1); // Last 24 hours

            var auditEntries = await _auditRepository.GetAuditTrailAsync(startDate, endDate);
            var auditList = auditEntries.ToList();

            var sb = new StringBuilder();
            sb.AppendLine("=== Operational Context ===");
            sb.AppendLine($"Period: {startDate:yyyy-MM-dd HH:mm:ss} to {endDate:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Total Audit Entries (24h): {auditList.Count}");
            sb.AppendLine();

            // Group by entity type
            var entityTypes = auditList.GroupBy(a => a.EntityType)
                .Select(g => new { EntityType = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);

            sb.AppendLine("Activity by Entity Type:");
            foreach (var type in entityTypes)
            {
                sb.AppendLine($"- {type.EntityType}: {type.Count} operations");
            }
            sb.AppendLine();

            // Group by action
            var actions = auditList.GroupBy(a => a.Action)
                .Select(g => new { Action = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);

            sb.AppendLine("Activity by Action:");
            foreach (var action in actions)
            {
                sb.AppendLine($"- {action.Action}: {action.Count} operations");
            }
            sb.AppendLine();

            // System metrics
            sb.AppendLine("System Metrics:");
            sb.AppendLine($"- Processor Count: {Environment.ProcessorCount}");
            sb.AppendLine($"- OS Version: {Environment.OSVersion}");
            sb.AppendLine($"- Machine Name: {Environment.MachineName}");
            sb.AppendLine($"- Current Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

            _logger.LogInformation("Operational context retrieved.");
            return sb.ToString();
        }

        /// <summary>
        /// Anonymizes sensitive data by masking it.
        /// </summary>
        /// <param name="data">The data to anonymize.</param>
        /// <returns>The anonymized data.</returns>
        private string Anonymize(string data)
        {
            if (string.IsNullOrEmpty(data))
                return data;

            // Simple anonymization: replace with asterisks, keeping first and last characters if long enough
            if (data.Length <= 2)
                return new string('*', data.Length);

            return data[0] + new string('*', data.Length - 2) + data[^1];
        }
    }
}
