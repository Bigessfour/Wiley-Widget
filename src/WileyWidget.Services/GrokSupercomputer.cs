#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of IGrokSupercomputer for AI-powered municipal analysis
/// </summary>
/// <remarks>
/// Initializes a new instance of the GrokSupercomputer class
/// </remarks>
/// <param name="logger">The logger instance</param>
/// <param name="enterpriseRepository">Repository for enterprise data</param>
/// <param name="budgetRepository">Repository for budget data</param>
/// <param name="auditRepository">Repository for audit data</param>
/// <param name="aiLoggingService">AI logging service for tracking operations</param>
/// <param name="aiService">AI service for Grok API integration</param>
public class GrokSupercomputer(
    ILogger<GrokSupercomputer> logger,
    IEnterpriseRepository enterpriseRepository,
    IBudgetRepository budgetRepository,
    IAuditRepository auditRepository,
    IAILoggingService aiLoggingService,
    IAIService aiService,
    IJARVISPersonalityService jarvismPersonality,
    Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
    Microsoft.Extensions.Options.IOptions<WileyWidget.Models.AppOptions> appOptions) : IGrokSupercomputer
{
    private readonly ILogger<GrokSupercomputer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IEnterpriseRepository _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
    private readonly IBudgetRepository _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
    private readonly IAuditRepository _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
    private readonly IAILoggingService _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
    private readonly IAIService _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
    private readonly IJARVISPersonalityService _jarvismPersonality = jarvismPersonality ?? throw new ArgumentNullException(nameof(jarvismPersonality));
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly Microsoft.Extensions.Options.IOptions<WileyWidget.Models.AppOptions> _appOptions = appOptions ?? throw new ArgumentNullException(nameof(appOptions));

    // Analysis thresholds and defaults
    private decimal VarianceHighThresholdPercent => _appOptions.Value.BudgetVarianceHighThresholdPercent;
    private decimal VarianceLowThresholdPercent => _appOptions.Value.BudgetVarianceLowThresholdPercent;
    private int HighConfidence => _appOptions.Value.AIHighConfidence;
    private int LowConfidence => _appOptions.Value.AILowConfidence;

    private async Task<T> SafeCall<T>(string operation, Func<Task<T>> action, T fallback)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Operation} failed. Returning fallback.", operation);
            try { _aiLoggingService.LogError(operation, ex); } catch { /* best-effort */ }
            return fallback;
        }
    }

    /// <summary>
    /// Fetches enterprise data for municipal utilities within specified parameters.
    /// Used in municipal finance to retrieve operational data for analysis, reporting, and decision-making.
    /// </summary>
    /// <param name="enterpriseId">Optional specific enterprise identifier. If null, fetches data for all enterprises.</param>
    /// <param name="startDate">Optional start date for data filtering. If null, no start date filter applied.</param>
    /// <param name="endDate">Optional end date for data filtering. If null, no end date filter applied.</param>
    /// <param name="filter">Optional string filter for additional data filtering criteria.</param>
    /// <returns>A Task containing ReportData with enterprise operational information for municipal utilities.</returns>
    public async Task<WileyWidget.Models.ReportData> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? startDate = null, DateTime? endDate = null, string filter = "")
    {
        try
        {
            var operationStart = DateTime.UtcNow;
            _logger.LogInformation("Fetching enterprise data for enterprise {EnterpriseId} with filters: startDate={StartDate}, endDate={EndDate}, filter={Filter}",
                enterpriseId, startDate, endDate, filter);

            // Log operation metrics
            _aiLoggingService.LogMetric("GrokSupercomputer.FetchEnterpriseData", 1, new Dictionary<string, object>
            {
                ["EnterpriseId"] = enterpriseId?.ToString(CultureInfo.InvariantCulture) ?? "All",
                ["HasDateFilter"] = startDate.HasValue || endDate.HasValue,
                ["HasTextFilter"] = !string.IsNullOrEmpty(filter)
            });

            var reportData = new ReportData
            {
                Title = $"Enterprise Data Report{(enterpriseId.HasValue ? $" - Enterprise {enterpriseId}" : "")}",
                GeneratedAt = DateTime.Now
            };

            // Set default dates if not provided
            var effectiveStartDate = startDate ?? DateTime.Now.AddMonths(-12);
            var effectiveEndDate = endDate ?? DateTime.Now;

            // Normalize invalid ranges
            if (effectiveStartDate > effectiveEndDate)
            {
                _logger.LogWarning("Start date {StartDate} is after end date {EndDate}. Swapping.", effectiveStartDate, effectiveEndDate);
                (effectiveStartDate, effectiveEndDate) = (effectiveEndDate, effectiveStartDate);
            }

            // Cache key includes enterpriseId/start/end/filter minimal
            var cacheKey = $"Grok.FetchEnterpriseData:{enterpriseId?.ToString(CultureInfo.InvariantCulture) ?? "all"}:{effectiveStartDate:yyyyMMdd}:{effectiveEndDate:yyyyMMdd}:{filter?.Trim().ToLowerInvariant()}";
            if (_appOptions.Value.EnableDataCaching && _cache.TryGetValue(cacheKey, out object? cachedObj) && cachedObj is ReportData cached)
            {
                _logger.LogInformation("Cache hit for FetchEnterpriseData: {Key}", cacheKey);
                return cached;
            }

            // Parallel fetch with resilience
            var budgetSummaryTask = SafeCall(
                nameof(IBudgetRepository.GetBudgetSummaryAsync),
                () => _budgetRepository.GetBudgetSummaryAsync(effectiveStartDate, effectiveEndDate),
                new BudgetVarianceAnalysis());

            var varianceAnalysisTask = SafeCall(
                nameof(IBudgetRepository.GetVarianceAnalysisAsync),
                () => _budgetRepository.GetVarianceAnalysisAsync(effectiveStartDate, effectiveEndDate),
                new BudgetVarianceAnalysis());

            var departmentsTask = SafeCall(
                nameof(IBudgetRepository.GetDepartmentBreakdownAsync),
                () => _budgetRepository.GetDepartmentBreakdownAsync(effectiveStartDate, effectiveEndDate),
                new List<DepartmentSummary>());

            var fundsTask = SafeCall(
                nameof(IBudgetRepository.GetFundAllocationsAsync),
                () => _budgetRepository.GetFundAllocationsAsync(effectiveStartDate, effectiveEndDate),
                new List<FundSummary>());

            Task<IEnumerable<AuditEntry>> auditTask = enterpriseId.HasValue
                ? SafeCall(
                    nameof(IAuditRepository.GetAuditTrailForEntityAsync),
                    () => _auditRepository.GetAuditTrailForEntityAsync("Enterprise", enterpriseId.Value, effectiveStartDate, effectiveEndDate),
                    Enumerable.Empty<AuditEntry>())
                : SafeCall(
                    nameof(IAuditRepository.GetAuditTrailAsync),
                    () => _auditRepository.GetAuditTrailAsync(effectiveStartDate, effectiveEndDate),
                    Enumerable.Empty<AuditEntry>());

            var yearEndTask = SafeCall(
                nameof(IBudgetRepository.GetYearEndSummaryAsync),
                () => _budgetRepository.GetYearEndSummaryAsync(effectiveEndDate.Year),
                new BudgetVarianceAnalysis());

            Task<ObservableCollection<Enterprise>> enterprisesTask = enterpriseId.HasValue
                ? SafeCall(
                    nameof(IEnterpriseRepository.GetByIdAsync),
                    async () =>
                    {
                        var entity = await _enterpriseRepository.GetByIdAsync(enterpriseId.Value);
                        return new ObservableCollection<Enterprise>(entity != null ? new[] { entity } : Array.Empty<Enterprise>());
                    },
                    new ObservableCollection<Enterprise>())
                : SafeCall(
                    nameof(IEnterpriseRepository.GetAllAsync),
                    async () => new ObservableCollection<Enterprise>((await _enterpriseRepository.GetAllAsync()) ?? Array.Empty<Enterprise>()),
                    new ObservableCollection<Enterprise>());

            await Task.WhenAll(budgetSummaryTask, varianceAnalysisTask, departmentsTask, fundsTask, auditTask, yearEndTask, enterprisesTask);

            // Assign results
            reportData.BudgetSummary = await budgetSummaryTask;
            reportData.VarianceAnalysis = await varianceAnalysisTask;
            reportData.Departments = new ObservableCollection<DepartmentSummary>(await departmentsTask);
            reportData.Funds = new ObservableCollection<FundSummary>(await fundsTask);
            reportData.AuditEntries = new ObservableCollection<AuditEntry>(await auditTask);
            reportData.YearEndSummary = await yearEndTask;
            reportData.Enterprises = await enterprisesTask;

            // Apply enterprise filter if specified
            if (enterpriseId.HasValue)
            {
                // Filter data for specific enterprise if needed
                _logger.LogInformation("Applying enterprise filter for ID {EnterpriseId}", enterpriseId);
            }

            // Apply additional filter if provided
            if (!string.IsNullOrEmpty(filter))
            {
                _logger.LogInformation("Applying additional filter: {Filter}", filter);
                var f = filter.Trim();
                var comp = StringComparison.OrdinalIgnoreCase;

                if (reportData.Departments != null)
                {
                    reportData.Departments = new ObservableCollection<DepartmentSummary>(
                        reportData.Departments.Where(d =>
                            (!string.IsNullOrEmpty(d.DepartmentName) && d.DepartmentName.Contains(f, comp)) ||
                            (d.Department?.Name?.Contains(f, comp) == true))
                    );
                }

                if (reportData.Funds != null)
                {
                    reportData.Funds = new ObservableCollection<FundSummary>(
                        reportData.Funds.Where(fs =>
                            (!string.IsNullOrEmpty(fs.FundName) && fs.FundName.Contains(f, comp)) ||
                            (fs.Fund?.Name?.Contains(f, comp) == true))
                    );
                }

                if (reportData.AuditEntries != null)
                {
                    reportData.AuditEntries = new ObservableCollection<AuditEntry>(
                        reportData.AuditEntries.Where(ae =>
                            (!string.IsNullOrEmpty(ae.User) && ae.User.Contains(f, comp)) ||
                            (!string.IsNullOrEmpty(ae.Action) && ae.Action.Contains(f, comp)) ||
                            (!string.IsNullOrEmpty(ae.EntityType) && ae.EntityType.Contains(f, comp)) ||
                            (!string.IsNullOrEmpty(ae.Changes) && ae.Changes.Contains(f, comp))
                        )
                    );
                }

                if (reportData.Enterprises != null)
                {
                    reportData.Enterprises = new ObservableCollection<Enterprise>(
                        reportData.Enterprises.Where(e =>
                            (!string.IsNullOrEmpty(e.Name) && e.Name.Contains(f, comp)) ||
                            (!string.IsNullOrEmpty(e.Description) && e.Description.Contains(f, comp))
                        )
                    );
                }
            }

            var operationTime = (long)(DateTime.UtcNow - operationStart).TotalMilliseconds;

            // Log performance metrics
            _aiLoggingService.LogMetric("GrokSupercomputer.FetchEnterpriseData.ResponseTime", operationTime, new Dictionary<string, object>
            {
                ["DepartmentCount"] = reportData.Departments?.Count ?? 0,
                ["FundCount"] = reportData.Funds?.Count ?? 0,
                ["AuditCount"] = (reportData.AuditEntries as System.Collections.ICollection)?.Count ?? reportData.AuditEntries?.Count() ?? 0,
                ["Success"] = true
            });

            _logger.LogInformation("Successfully fetched enterprise data with {DepartmentCount} departments, {FundCount} funds, {AuditCount} audit entries in {Duration}ms",
                reportData.Departments?.Count ?? 0, reportData.Funds?.Count ?? 0,
                (reportData.AuditEntries as System.Collections.ICollection)?.Count ?? reportData.AuditEntries?.Count() ?? 0,
                operationTime);

            // store in cache with short TTL
            if (_appOptions.Value.EnableDataCaching)
            {
                var ttlSeconds = Math.Max(5, _appOptions.Value.EnterpriseDataCacheSeconds);
                var entryOptions = new MemoryCacheEntryOptions
                {
                    Size = 1,
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
                };
                _cache.Set(cacheKey, reportData, entryOptions);
                _logger.LogDebug("Cached FetchEnterpriseData result for {Seconds}s: {Key}", ttlSeconds, cacheKey);
            }

            return reportData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching enterprise data for enterprise {EnterpriseId}", enterpriseId);
            _aiLoggingService.LogError("FetchEnterpriseData", ex);
            throw;
        }
    }

    /// <summary>
    /// Runs analytical calculations on report data for municipal utility performance metrics.
    /// Processes enterprise data to generate insights for municipal finance management and operational efficiency.
    /// </summary>
    /// <param name="data">The ReportData containing enterprise information to analyze.</param>
    /// <returns>A Task containing AnalyticsData with calculated metrics and performance indicators.</returns>
    public async Task<AnalyticsData> RunReportCalcsAsync(ReportData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        try
        {
            _logger.LogInformation("Running report calculations on data: {Title}", data.Title);

            var analytics = new AnalyticsData
            {
                ChartType = "bar",
                Categories = new List<string>(),
                SummaryStats = new Dictionary<string, double>(),
                ChartData = new Dictionary<string, double>()
            };

            // Calculate KPIs from departments
            if (data.Departments != null && data.Departments.Any())
            {
                var totalBudgeted = data.Departments.Sum(d => d.TotalBudgeted);
                var totalActual = data.Departments.Sum(d => d.TotalActual);
                var variance = totalActual - totalBudgeted;
                var variancePercent = totalBudgeted != 0 ? (variance / totalBudgeted) * 100 : 0;

                analytics.Categories.AddRange(new[] { "Budgeted", "Actual", "Variance" });
                analytics.SummaryStats["Total Budgeted"] = (double)totalBudgeted;
                analytics.SummaryStats["Total Actual"] = (double)totalActual;
                analytics.SummaryStats["Total Variance"] = (double)variance;
                analytics.SummaryStats["Variance %"] = (double)variancePercent;

                // Create chart series for each department
                foreach (var dept in data.Departments)
                {
                    var deptBudgeted = dept.TotalBudgeted;
                    var deptActual = dept.TotalActual;
                    var series = new ChartSeries
                    {
                        Name = dept.DepartmentName ?? "Unknown"
                    };
                    series.DataPoints.Add(new ChartDataPoint { XValue = "Budgeted", YValue = (double)deptBudgeted });
                    series.DataPoints.Add(new ChartDataPoint { XValue = "Actual", YValue = (double)deptActual });
                    series.DataPoints.Add(new ChartDataPoint { XValue = "Variance", YValue = (double)(deptActual - deptBudgeted) });
                    analytics.ChartData.Add(series.Name, (double)(deptActual - deptBudgeted));
                }
            }

            // Calculate from funds if available
            if (data.Funds != null && data.Funds.Any())
            {
                var totalFundBudget = data.Funds.Sum(f => f.TotalBudgeted);
                var totalFundActual = data.Funds.Sum(f => f.TotalActual);
                analytics.SummaryStats["Fund Budget"] = (double)totalFundBudget;
                analytics.SummaryStats["Fund Actual"] = (double)totalFundActual;
            }

            // Calculate audit metrics
            if (data.AuditEntries != null)
            {
                var auditCount = (data.AuditEntries as System.Collections.ICollection)?.Count ?? data.AuditEntries.Count();
                analytics.SummaryStats["Audit Entries"] = auditCount;
            }

            _logger.LogInformation("Successfully calculated analytics with {CategoryCount} categories and {SeriesCount} series",
                analytics.Categories.Count, analytics.ChartData.Count);

            return await Task.FromResult(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running report calculations on data: {Title}", data.Title);
            throw;
        }
    }

    /// <summary>
    /// Analyzes budget data to provide insights for municipal utility financial planning.
    /// Evaluates budget allocations, expenditures, and projections for municipal finance optimization.
    /// </summary>
    /// <param name="budget">The BudgetData containing financial information to analyze.</param>
    /// <returns>A Task containing BudgetInsights with recommendations and analysis results.</returns>
    public async Task<BudgetInsights> AnalyzeBudgetDataAsync(BudgetData budget)
    {
        if (budget == null) throw new ArgumentNullException(nameof(budget));

        try
        {
            _logger.LogInformation("Analyzing budget data for enterprise {EnterpriseId}, fiscal year {FiscalYear}",
                budget.EnterpriseId, budget.FiscalYear);

            var insights = new BudgetInsights();

            // Calculate variance
            var variance = budget.TotalExpenditures - budget.TotalBudget;
            var variancePercent = budget.TotalBudget != 0 ? (variance / budget.TotalBudget) * 100 : 0;

            insights.Variances.Add(new WileyWidget.Models.BudgetVariance
            {
                Category = "Overall Budget",
                Budgeted = budget.TotalBudget,
                Actual = budget.TotalExpenditures,
                Variance = variance
            });

            // Calculate projections (simple trend analysis)
            var remainingMonths = Math.Max(0, 12 - DateTime.Now.Month + 1);
            var monthsElapsed = Math.Max(1, 12 - remainingMonths + 1);
            var monthlyBurnRate = monthsElapsed > 0 ? budget.TotalExpenditures / monthsElapsed : 0;
            var projectedEndOfYear = budget.TotalExpenditures + (monthlyBurnRate * remainingMonths);

            insights.Projections.Add(new WileyWidget.Models.BudgetProjection
            {
                Period = "End of Year",
                ProjectedAmount = projectedEndOfYear,
                ConfidenceLevel = variancePercent < VarianceHighThresholdPercent ? HighConfidence : LowConfidence
            });

            // Generate recommendations based on variance
            if (variancePercent > VarianceHighThresholdPercent)
            {
                insights.Recommendations.Add("Budget variance exceeds 10%. Review expense controls.");
                insights.Recommendations.Add("Consider cost reduction measures to align with budget.");
            }
            else if (variancePercent < VarianceLowThresholdPercent)
            {
                insights.Recommendations.Add("Budget performance is better than expected. Consider reallocating surplus funds.");
            }
            else
            {
                insights.Recommendations.Add("Budget performance is within acceptable range. Continue monitoring.");
            }

            // Calculate health score based on variance
            insights.HealthScore = Math.Max(0, Math.Min(100, 100 - (int)Math.Abs(variancePercent)));

            // Enhance with AI-powered insights
            try
            {
                var aiInsights = await GenerateBudgetInsightsWithAIAsync(budget, variancePercent);
                if (!string.IsNullOrEmpty(aiInsights))
                {
                    // Apply JARVIS personality to AI insights
                    var personalizedInsights = _jarvismPersonality.ApplyBudgetPersonality(
                        aiInsights,
                        variancePercent,
                        budget.RemainingBudget,
                        "General Budget");
                    insights.Recommendations.Add(personalizedInsights);
                }
            }
            catch (Exception aiEx)
            {
                _logger.LogWarning(aiEx, "AI budget analysis failed, continuing with basic analysis");
            }

            _logger.LogInformation("Successfully analyzed budget data with variance {VariancePercent:P2} and health score {HealthScore}",
                variancePercent / 100, insights.HealthScore);

            return insights;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing budget data for enterprise {EnterpriseId}", budget.EnterpriseId);
            throw;
        }
    }    /// <summary>
         /// Generates compliance reports for municipal utility enterprises.
         /// Ensures regulatory compliance and provides documentation for municipal finance auditing and reporting requirements.
         /// </summary>
         /// <param name="enterprise">The Enterprise object containing information about the municipal utility to evaluate.</param>
         /// <returns>A Task containing ComplianceReport with regulatory compliance status and recommendations.</returns>
    public Task<WileyWidget.Models.ComplianceReport> GenerateComplianceReportAsync(Enterprise enterprise)
    {
        if (enterprise == null) throw new ArgumentNullException(nameof(enterprise));

        try
        {
            _logger.LogInformation("Generating compliance report for enterprise {EnterpriseId}: {EnterpriseName}",
                enterprise.Id, enterprise.Name);

            var report = new WileyWidget.Models.ComplianceReport
            {
                EnterpriseId = enterprise.Id,
                GeneratedDate = DateTime.Now,
                Violations = new List<WileyWidget.Models.ComplianceViolation>(),
                Recommendations = new List<string>(),
                ComplianceScore = 100
            };

            // Check basic compliance requirements
            var violations = new List<WileyWidget.Models.ComplianceViolation>();

            // Check if enterprise has required fields
            if (string.IsNullOrEmpty(enterprise.Name))
            {
                violations.Add(new WileyWidget.Models.ComplianceViolation
                {
                    Regulation = "Enterprise Registration",
                    Description = "Enterprise name is required",
                    Severity = WileyWidget.Models.ViolationSeverity.High,
                    CorrectiveAction = "Provide a valid enterprise name"
                });
            }

            if (enterprise.CurrentRate <= 0)
            {
                violations.Add(new WileyWidget.Models.ComplianceViolation
                {
                    Regulation = "Rate Regulation",
                    Description = "Current rate must be positive",
                    Severity = WileyWidget.Models.ViolationSeverity.Medium,
                    CorrectiveAction = "Set a valid current rate"
                });
            }

            if (enterprise.MonthlyExpenses < 0)
            {
                violations.Add(new WileyWidget.Models.ComplianceViolation
                {
                    Regulation = "Financial Reporting",
                    Description = "Monthly expenses cannot be negative",
                    Severity = WileyWidget.Models.ViolationSeverity.Medium,
                    CorrectiveAction = "Correct monthly expenses value"
                });
            }

            report.Violations.AddRange(violations);

            // Determine overall status
            if (violations.Any(v => v.Severity == WileyWidget.Models.ViolationSeverity.Critical))
            {
                report.OverallStatus = WileyWidget.Models.ComplianceStatus.Critical;
                report.ComplianceScore = 0;
            }
            else if (violations.Any(v => v.Severity == WileyWidget.Models.ViolationSeverity.High))
            {
                report.OverallStatus = WileyWidget.Models.ComplianceStatus.NonCompliant;
                report.ComplianceScore = 40;
            }
            else if (violations.Any(v => v.Severity == WileyWidget.Models.ViolationSeverity.Medium))
            {
                report.OverallStatus = WileyWidget.Models.ComplianceStatus.Warning;
                report.ComplianceScore = 70;
            }
            else
            {
                report.OverallStatus = WileyWidget.Models.ComplianceStatus.Compliant;
                report.ComplianceScore = 100;
            }

            // Generate recommendations
            if (report.OverallStatus != WileyWidget.Models.ComplianceStatus.Compliant)
            {
                report.Recommendations.Add("Address all compliance violations immediately");
                report.Recommendations.Add("Schedule a compliance review within 30 days");
                report.Recommendations.Add("Consult with regulatory authorities if needed");
            }
            else
            {
                report.Recommendations.Add("Continue maintaining current compliance standards");
                report.Recommendations.Add("Schedule next annual compliance audit");
                report.Recommendations.Add("Monitor regulatory changes that may affect operations");
            }

            // Set next audit date
            report.NextAuditDate = DateTime.Now.AddYears(1);

            _logger.LogInformation("Successfully generated compliance report with status {OverallStatus} and score {ComplianceScore}",
                report.OverallStatus, report.ComplianceScore);

            return Task.FromResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report for enterprise {EnterpriseId}", enterprise.Id);
            throw;
        }
    }

    /// <summary>
    /// Analyzes municipal data using AI to provide insights and recommendations.
    /// </summary>
    /// <param name="data">The data to analyze.</param>
    /// <param name="context">Additional context for the analysis.</param>
    /// <returns>A Task containing the analysis results as a string.</returns>
    public async Task<string> AnalyzeMunicipalDataAsync(object data, string context)
    {
        try
        {
            _logger.LogInformation("Analyzing municipal data with context: {Context}", context);

            // Serialize data for AI analysis
            var dataJson = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var question = $"Please analyze this municipal utility data and provide insights. Context: {context}. Data: {dataJson}";

            var analysis = await _aiService.GetInsightsAsync("Municipal Data Analysis", question);

            _logger.LogInformation("Municipal data analysis completed using AI");
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing municipal data with AI service");
            // Fallback to basic analysis if AI fails
            return $"Basic analysis of municipal data indicates potential for optimization in {context}. " +
                   $"Data type: {data?.GetType().Name ?? "Unknown"}. " +
                   $"Note: AI analysis failed due to: {ex.Message}";
        }
    }

    /// <summary>
    /// Generates AI-powered budget insights using xAI analysis
    /// </summary>
    /// <param name="budget">The budget data to analyze</param>
    /// <param name="variancePercent">The calculated variance percentage</param>
    /// <returns>AI-generated insights as a string</returns>
    private async Task<string> GenerateBudgetInsightsWithAIAsync(BudgetData budget, decimal variancePercent)
    {
        try
        {
            var context = $"Budget Analysis for Enterprise {budget.EnterpriseId}, Fiscal Year {budget.FiscalYear}";
            var question = $@"
Analyze this municipal utility budget data and provide specific insights:

Budget Details:
- Total Budget: ${budget.TotalBudget:N2}
- Total Expenditures: ${budget.TotalExpenditures:N2}
- Remaining Budget: ${budget.RemainingBudget:N2}
- Variance: {(variancePercent >= 0 ? "Over" : "Under")} by {Math.Abs(variancePercent):N2}%

Please provide:
1. Analysis of spending patterns and efficiency
2. Risk assessment for budget overruns
3. Recommendations for cost optimization
4. Suggestions for budget reallocation if applicable
5. Long-term financial planning insights

Focus on municipal utility operations and provide actionable insights.";

            var aiResponse = await _aiService.GetInsightsAsync(context, question);
            return aiResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI budget insights generation failed");
            return string.Empty;
        }
    }

    /// <summary>
    /// Analyzes municipal accounts and provides AI-powered insights on account structures and spending patterns
    /// </summary>
    /// <param name="municipalAccounts">Collection of municipal accounts to analyze</param>
    /// <param name="budgetData">Associated budget data for context</param>
    /// <returns>AI-powered analysis of municipal accounts</returns>
    public async Task<string> AnalyzeMunicipalAccountsWithAIAsync(IEnumerable<WileyWidget.Models.MunicipalAccount> municipalAccounts, BudgetData budgetData)
    {
        try
        {
            _logger.LogInformation("Analyzing municipal accounts with AI for enterprise {EnterpriseId}", budgetData?.EnterpriseId);

            var accountsJson = System.Text.Json.JsonSerializer.Serialize(municipalAccounts, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var context = $"Municipal Account Analysis for Enterprise {budgetData?.EnterpriseId ?? 0}";
            var question = $@"
Analyze these municipal accounts for a utility enterprise and provide insights on:

Account Data: {accountsJson}

Budget Context:
- Total Budget: ${budgetData?.TotalBudget:N2 ?? 0}
- Total Expenditures: ${budgetData?.TotalExpenditures:N2 ?? 0}

Please provide:
1. Analysis of account structure and categorization
2. Identification of high-spending accounts and potential cost centers
3. Recommendations for account consolidation or restructuring
4. Insights on spending patterns by account type
5. Suggestions for budget allocation optimization across accounts
6. Risk assessment for accounts showing unusual spending patterns

Focus on municipal finance best practices and operational efficiency.";

            var analysis = await _aiService.GetInsightsAsync(context, question);
            _logger.LogInformation("Municipal account analysis completed with AI insights");
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing municipal accounts with AI");
            return $"Basic municipal account analysis indicates {municipalAccounts?.Count() ?? 0} accounts to review. " +
                   $"AI analysis failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Generates recommendations based on analyzed data.
    /// </summary>
    /// <param name="data">The data to generate recommendations for.</param>
    /// <returns>A Task containing the recommendations as a string.</returns>
    public async Task<string> GenerateRecommendationsAsync(object data)
    {
        try
        {
            _logger.LogInformation("Generating AI-powered recommendations based on analyzed data");

            // Serialize data for AI analysis
            var dataJson = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var question = $"Based on this municipal utility data, please generate specific, actionable recommendations for improving efficiency, reducing costs, and optimizing operations. Data: {dataJson}";

            var recommendations = await _aiService.GetInsightsAsync("Recommendation Generation", question);

            _logger.LogInformation("AI-powered recommendations generated successfully");
            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI-powered recommendations");
            // Fallback to basic recommendations if AI fails
            return $"Recommended actions: " +
                   $"1. Implement data-driven decision making to reduce operational costs. " +
                   $"2. Optimize resource allocation based on usage patterns. " +
                   $"3. Establish automated monitoring systems. " +
                   $"Data type analyzed: {data?.GetType().Name ?? "Unknown"}. " +
                   $"Note: AI recommendations failed due to: {ex.Message}";
        }
    }

    /// <summary>
    /// Executes a direct AI query using the configured AI service
    /// </summary>
    /// <param name="prompt">The query prompt to send to the AI service</param>
    /// <returns>The AI response as a string</returns>
    public async Task<string> QueryAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

        try
        {
            _logger.LogInformation("Executing AI query with prompt length: {Length}", prompt.Length);

            var response = await _aiService.SendPromptAsync(prompt);

            _aiLoggingService.LogMetric("GrokSupercomputer.QueryAsync.ResponseTime", 0, new Dictionary<string, object>
            {
                ["PromptLength"] = prompt.Length,
                ["ResponseLength"] = response?.Content?.Length ?? 0,
                ["Success"] = true
            });

            _logger.LogInformation("AI query completed successfully");
            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing AI query");
            _aiLoggingService.LogError("QueryAsync", ex);
            throw;
        }
    }
}
