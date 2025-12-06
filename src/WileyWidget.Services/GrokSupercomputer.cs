#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
public class GrokSupercomputer : IGrokSupercomputer
{
    private readonly ILogger<GrokSupercomputer> _logger;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly IAILoggingService _aiLoggingService;
    private readonly IAIService _aiService;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private readonly Microsoft.Extensions.Options.IOptions<WileyWidget.Models.AppOptions> _appOptions;

    // Analysis thresholds and defaults
    private decimal VarianceHighThresholdPercent => _appOptions.Value.BudgetVarianceHighThresholdPercent;
    private decimal VarianceLowThresholdPercent => _appOptions.Value.BudgetVarianceLowThresholdPercent;
    private int HighConfidence => _appOptions.Value.AIHighConfidence;
    private int LowConfidence => _appOptions.Value.AILowConfidence;

    // Static JSON serialization options to avoid repeated allocations
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the GrokSupercomputer class
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="enterpriseRepository">Repository for enterprise data</param>
    /// <param name="budgetRepository">Repository for budget data</param>
    /// <param name="auditRepository">Repository for audit data</param>
    /// <param name="aiLoggingService">AI logging service for tracking operations</param>
    /// <param name="aiService">AI service for Grok API integration</param>
    public GrokSupercomputer(
        ILogger<GrokSupercomputer> logger,
        IEnterpriseRepository enterpriseRepository,
        IBudgetRepository budgetRepository,
        IAuditRepository auditRepository,
        IAILoggingService aiLoggingService,
        IAIService aiService,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
        Microsoft.Extensions.Options.IOptions<WileyWidget.Models.AppOptions> appOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _appOptions = appOptions ?? throw new ArgumentNullException(nameof(appOptions));
    }

    private async Task<T> SafeCall<T>(string operation, Func<Task<T>> action, T fallback, int maxRetries = 2)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= maxRetries)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransientError(ex))
            {
                lastException = ex;
                attempt++;
                var delayMs = (int)Math.Pow(2, attempt) * 100; // Exponential backoff: 200ms, 400ms
                _logger.LogWarning(ex, "{Operation} failed (attempt {Attempt}/{MaxRetries}). Retrying in {DelayMs}ms.",
                    operation, attempt, maxRetries, delayMs);
                await Task.Delay(delayMs);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        // All retries exhausted or non-transient error
        _aiLoggingService.LogError(operation, lastException!);
        _logger.LogWarning(lastException, "{Operation} failed after {Attempts} attempts. Returning fallback.",
            operation, attempt);
        return fallback;
    }

    private static bool IsTransientError(Exception ex)
    {
        // Identify transient errors that warrant retry (network, timeout, etc.)
        return ex is TaskCanceledException
            || ex is TimeoutException
            || (ex.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true)
            || (ex.InnerException is TaskCanceledException or TimeoutException);
    }

    /// <summary>
    /// Calculates budget variance as a percentage
    /// </summary>
    private static decimal CalculateVariancePercent(decimal budgeted, decimal actual)
    {
        if (budgeted == 0) return 0;
        return ((actual - budgeted) / budgeted) * 100;
    }

    /// <summary>
    /// Calculates health score (0-100) based on variance percentage
    /// </summary>
    private static int CalculateHealthScore(decimal variancePercent)
    {
        // Health score decreases as absolute variance increases
        // Perfect score (100) at 0% variance, decreasing linearly
        var absVariance = Math.Abs(variancePercent);
        var score = 100 - (int)absVariance;
        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// Calculates monthly burn rate and projects end-of-year spending
    /// </summary>
    private static (decimal monthlyBurnRate, decimal projectedEndOfYear) CalculateBurnRateProjection(
        decimal totalExpenditures, int currentMonth = 0)
    {
        var month = currentMonth > 0 ? currentMonth : DateTime.UtcNow.Month;
        var monthsElapsed = Math.Max(1, month);
        var remainingMonths = Math.Max(0, 12 - month);
        var monthlyBurnRate = totalExpenditures / monthsElapsed;
        var projectedEndOfYear = totalExpenditures + (monthlyBurnRate * remainingMonths);
        return (monthlyBurnRate, projectedEndOfYear);
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
        // Input validation
        if (enterpriseId.HasValue && enterpriseId.Value <= 0)
        {
            throw new ArgumentException("Enterprise ID must be positive", nameof(enterpriseId));
        }

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
            var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "" : filter.Trim().ToLowerInvariant();
            var cacheKey = $"Grok.FetchEnterpriseData:{enterpriseId?.ToString(CultureInfo.InvariantCulture) ?? "all"}:{effectiveStartDate:yyyyMMdd}:{effectiveEndDate:yyyyMMdd}:{normalizedFilter}";

            if (_appOptions.Value.EnableDataCaching && _cache.TryGetValue(cacheKey, out object? cachedObj) && cachedObj is ReportData cached)
            {
                _logger.LogInformation("Cache hit for FetchEnterpriseData: {Key}", cacheKey);
                _aiLoggingService.LogMetric("GrokSupercomputer.FetchEnterpriseData.CacheHit", 1, new Dictionary<string, object>
                {
                    ["CacheKey"] = cacheKey
                });
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
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
                };
                _cache.Set(cacheKey, reportData, entryOptions);
                _logger.LogDebug("Cached FetchEnterpriseData result for {Seconds}s: {Key}", ttlSeconds, cacheKey);
            }

            return reportData;
        }
        catch (Exception ex)
        {
            _aiLoggingService.LogError("FetchEnterpriseData", ex);
            _logger.LogError(ex, "Error fetching enterprise data for enterprise {EnterpriseId}", enterpriseId);
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

            // Check cache first
            var cacheKey = $"Grok.RunReportCalcs:{data.Title?.GetHashCode() ?? 0}:{data.GeneratedAt:yyyyMMddHHmmss}";
            if (_appOptions.Value.EnableDataCaching && _cache.TryGetValue(cacheKey, out object? cachedObj) && cachedObj is AnalyticsData cachedAnalytics)
            {
                _logger.LogDebug("Cache hit for RunReportCalcs: {Key}", cacheKey);
                return cachedAnalytics;
            }

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
                var variancePercent = CalculateVariancePercent(totalBudgeted, totalActual);

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

            // Cache the result
            if (_appOptions.Value.EnableDataCaching)
            {
                var entryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_appOptions.Value.CacheExpirationMinutes)
                };
                _cache.Set(cacheKey, analytics, entryOptions);
            }

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
        if (budget.FiscalYear < 1900 || budget.FiscalYear > 2100)
        {
            throw new ArgumentException($"Invalid fiscal year: {budget.FiscalYear}", nameof(budget));
        }

        try
        {
            _logger.LogInformation("Analyzing budget data for enterprise {EnterpriseId}, fiscal year {FiscalYear}",
                budget.EnterpriseId, budget.FiscalYear);

            // Check cache
            var cacheKey = $"Grok.AnalyzeBudget:{budget.EnterpriseId}:{budget.FiscalYear}:{budget.TotalBudget}:{budget.TotalExpenditures}";
            if (_appOptions.Value.EnableDataCaching && _cache.TryGetValue(cacheKey, out object? cachedObj) && cachedObj is BudgetInsights cachedInsights)
            {
                _logger.LogDebug("Cache hit for AnalyzeBudgetData: {Key}", cacheKey);
                return cachedInsights;
            }

            var insights = new BudgetInsights();

            // Calculate variance using explicit method
            var variance = budget.TotalExpenditures - budget.TotalBudget;
            var variancePercent = CalculateVariancePercent(budget.TotalBudget, budget.TotalExpenditures);

            insights.Variances.Add(new WileyWidget.Models.BudgetVariance
            {
                Category = "Overall Budget",
                Budgeted = budget.TotalBudget,
                Actual = budget.TotalExpenditures,
                Variance = variance
            });

            // Calculate projections using explicit method
            var (monthlyBurnRate, projectedEndOfYear) = CalculateBurnRateProjection(budget.TotalExpenditures);

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

            // Calculate health score using explicit method
            insights.HealthScore = CalculateHealthScore(variancePercent);

            // Enhance with AI-powered insights
            try
            {
                var aiInsights = await GenerateBudgetInsightsWithAIAsync(budget, variancePercent);
                if (!string.IsNullOrEmpty(aiInsights))
                {
                    insights.Recommendations.Add($"AI Analysis: {aiInsights}");
                }
            }
            catch (Exception aiEx)
            {
                _logger.LogWarning(aiEx, "AI budget analysis failed, continuing with basic analysis");
            }

            _logger.LogInformation("Successfully analyzed budget data with variance {VariancePercent:P2} and health score {HealthScore}",
                variancePercent / 100, insights.HealthScore);

            // Cache the result
            if (_appOptions.Value.EnableDataCaching)
            {
                var entryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_appOptions.Value.CacheExpirationMinutes)
                };
                _cache.Set(cacheKey, insights, entryOptions);
            }

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
        if (enterprise.Id <= 0)
        {
            throw new ArgumentException("Enterprise ID must be positive", nameof(enterprise));
        }

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
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrWhiteSpace(context))
        {
            throw new ArgumentException("Context cannot be empty", nameof(context));
        }

        try
        {
            _logger.LogInformation("Analyzing municipal data with context: {Context}", context);

            // Serialize data for AI analysis
            var dataJson = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            // Check serialized size to avoid excessive API calls
            if (dataJson.Length > 50000)
            {
                _logger.LogWarning("Municipal data JSON exceeds 50KB ({Size} chars). Truncating for AI analysis.", dataJson.Length);
                dataJson = dataJson.Substring(0, 50000) + "...[truncated]";
            }

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
        if (municipalAccounts == null) throw new ArgumentNullException(nameof(municipalAccounts));
        if (budgetData == null) throw new ArgumentNullException(nameof(budgetData));

        try
        {
            _logger.LogInformation("Analyzing municipal accounts with AI for enterprise {EnterpriseId}", budgetData?.EnterpriseId);

            var accountsList = municipalAccounts.ToList();
            if (accountsList.Count == 0)
            {
                return "No municipal accounts provided for analysis.";
            }

            var accountsJson = System.Text.Json.JsonSerializer.Serialize(accountsList, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            // Limit JSON size for API
            if (accountsJson.Length > 40000)
            {
                _logger.LogWarning("Accounts JSON exceeds 40KB ({Size} chars). Truncating for AI analysis.", accountsJson.Length);
                accountsJson = accountsJson.Substring(0, 40000) + "...[truncated]";
            }

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
        if (data == null) throw new ArgumentNullException(nameof(data));

        try
        {
            _logger.LogInformation("Generating AI-powered recommendations based on analyzed data");

            // Serialize data for AI analysis
            var dataJson = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            // Limit size for AI processing
            if (dataJson.Length > 45000)
            {
                _logger.LogWarning("Recommendation data JSON exceeds 45KB ({Size} chars). Truncating.", dataJson.Length);
                dataJson = dataJson.Substring(0, 45000) + "...[truncated]";
            }

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

            var startTime = DateTime.UtcNow;
            var response = await _aiService.SendPromptAsync(prompt);
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _aiLoggingService.LogMetric("GrokSupercomputer.QueryAsync.ResponseTime", elapsedMs, new Dictionary<string, object>
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

    /// <summary>
    /// Analyzes budget data for a specific fiscal year using AI
    /// </summary>
    /// <param name="fiscalYear">The fiscal year to analyze</param>
    /// <returns>AI-generated budget insights</returns>
    public async Task<string> AnalyzeBudgetAsync(int fiscalYear)
    {
        if (fiscalYear < 1900 || fiscalYear > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(fiscalYear), "Fiscal year must be between 1900 and 2100");
        }

        return await SafeCall(
            "AnalyzeBudget",
            async () =>
            {
                _logger.LogInformation("Analyzing budget for fiscal year {FiscalYear}", fiscalYear);

                // Fetch budget entries for the fiscal year
                var budgetEntries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);
                var budgetEntriesList = budgetEntries?.ToList() ?? new List<BudgetEntry>();

                // Create a budget overview object
                var budgetOverview = new
                {
                    FiscalYear = fiscalYear,
                    Entries = budgetEntriesList,
                    TotalBudgeted = budgetEntriesList.Sum(e => e.BudgetedAmount),
                    TotalActual = budgetEntriesList.Sum(e => e.ActualAmount),
                    EntryCount = budgetEntriesList.Count
                };

                var dataJson = System.Text.Json.JsonSerializer.Serialize(budgetOverview, JsonOptions);

                var question = $"Analyze this budget data for fiscal year {fiscalYear} and provide insights: {dataJson}";
                var insights = await _aiService.GetInsightsAsync($"Budget Analysis - FY {fiscalYear}", question);

                _aiLoggingService.LogQuery($"AnalyzeBudget-{fiscalYear}", question, dataJson);
                _aiLoggingService.LogResponse($"AnalyzeBudget-{fiscalYear}", insights, 0, (insights?.Length ?? 0));

                _logger.LogInformation("AI-powered budget analysis completed for fiscal year {FiscalYear}", fiscalYear);
                return insights;
            },
            GenerateFallbackBudgetAnalysis(fiscalYear, null)
        );
    }

    /// <summary>
    /// Analyzes enterprise data using AI
    /// </summary>
    /// <param name="enterpriseId">The enterprise ID to analyze</param>
    /// <returns>AI-generated enterprise insights</returns>
    public async Task<string> AnalyzeEnterpriseAsync(int enterpriseId)
    {
        if (enterpriseId <= 0)
        {
            throw new ArgumentException("Enterprise ID must be positive", nameof(enterpriseId));
        }

        return await SafeCall(
            "AnalyzeEnterprise",
            async () =>
            {
                _logger.LogInformation("Analyzing enterprise {EnterpriseId}", enterpriseId);

                // Fetch enterprise data
                var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId);

                if (enterprise == null)
                {
                    throw new InvalidOperationException($"Enterprise with ID {enterpriseId} not found");
                }

                // Create an enterprise overview
                var enterpriseOverview = new
                {
                    EnterpriseId = enterprise.Id,
                    Name = enterprise.Name,
                    Description = enterprise.Description,
                    CurrentRate = enterprise.CurrentRate,
                    MonthlyExpenses = enterprise.MonthlyExpenses,
                    Type = enterprise.Type
                };

                var dataJson = System.Text.Json.JsonSerializer.Serialize(enterpriseOverview, JsonOptions);

                var question = $"Analyze this enterprise data and provide operational insights: {dataJson}";
                var insights = await _aiService.GetInsightsAsync($"Enterprise Analysis - ID {enterpriseId}", question);

                _logger.LogInformation("AI-powered enterprise analysis completed for enterprise {EnterpriseId}", enterpriseId);
                return insights;
            },
            GenerateFallbackEnterpriseAnalysis(enterpriseId, null)
        );
    }

    /// <summary>
    /// Analyzes audit findings using AI
    /// </summary>
    /// <param name="startDate">Optional start date for audit data</param>
    /// <param name="endDate">Optional end date for audit data</param>
    /// <returns>AI-generated audit analysis</returns>
    public async Task<string> AnalyzeAuditAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        return await SafeCall(
            "AnalyzeAudit",
            async () =>
            {
                _logger.LogInformation("Analyzing audit data from {StartDate} to {EndDate}", startDate, endDate);

                var effectiveStartDate = startDate ?? DateTime.Now.AddYears(-1);
                var effectiveEndDate = endDate ?? DateTime.Now;

                // Fetch audit entries
                var auditEntries = await _auditRepository.GetAuditTrailAsync(effectiveStartDate, effectiveEndDate);
                var auditEntriesList = auditEntries?.ToList() ?? new List<AuditEntry>();

                // Create audit findings object
                var auditFindings = new
                {
                    StartDate = effectiveStartDate,
                    EndDate = effectiveEndDate,
                    Findings = auditEntriesList,
                    TotalFindings = auditEntriesList.Count,
                    EntitiesAudited = auditEntriesList.Select(e => e.EntityType).Distinct().Count()
                };

                var dataJson = System.Text.Json.JsonSerializer.Serialize(auditFindings, JsonOptions);

                var question = $"Analyze these audit findings and provide compliance insights: {dataJson}";
                var insights = await _aiService.GetInsightsAsync("Audit Analysis", question);

                _logger.LogInformation("AI-powered audit analysis completed");
                return insights;
            },
            GenerateFallbackAuditAnalysis(null)
        );
    }

    /// <summary>
    /// Analyzes all municipal accounts using AI
    /// </summary>
    /// <returns>AI-generated analysis of municipal accounts</returns>
    public async Task<string> AnalyzeMunicipalAccountsAsync()
    {
        return await SafeCall(
            "AnalyzeMunicipalAccounts",
            async () =>
            {
                _logger.LogInformation("Analyzing municipal accounts");

                // Fetch all enterprises (which contain municipal account information)
                var enterprises = await _enterpriseRepository.GetAllAsync();
                var enterprisesList = enterprises?.ToList() ?? new List<Enterprise>();

                // Create accounts summary
                var accountsSummary = new
                {
                    TotalAccounts = enterprisesList.Count,
                    Accounts = enterprisesList.Select(e => new
                    {
                        Id = e.Id,
                        Name = e.Name,
                        Type = e.Type,
                        CurrentRate = e.CurrentRate,
                        MonthlyExpenses = e.MonthlyExpenses
                    }).ToList()
                };

                var dataJson = System.Text.Json.JsonSerializer.Serialize(accountsSummary, JsonOptions);

                if (dataJson.Length > 50000)
                {
                    _logger.LogWarning("Municipal accounts JSON exceeds 50KB. Truncating for AI analysis.");
                    dataJson = dataJson.Substring(0, 50000) + "...[truncated]";
                }

                var question = $"Analyze these municipal accounts and provide insights on spending patterns and optimization: {dataJson}";
                var insights = await _aiService.GetInsightsAsync("Municipal Accounts Analysis", question);

                _logger.LogInformation("AI-powered municipal accounts analysis completed");
                return insights;
            },
            GenerateFallbackAccountsAnalysis(null)
        );
    }

    /// <summary>
    /// Generates fallback budget analysis when AI is unavailable
    /// </summary>
    private string GenerateFallbackBudgetAnalysis(int fiscalYear, object? budgetData)
    {
        return GenerateFallbackAnalysis(
            "Budget",
            new Dictionary<string, object?>
            {
                ["Fiscal Year"] = fiscalYear,
                ["Review Period"] = fiscalYear.ToString(CultureInfo.InvariantCulture),
                ["Data Points"] = ExtractCollectionCount(budgetData, "Entries")
            },
            "Conduct detailed variance analysis and review budget allocation by department"
        );
    }

    /// <summary>
    /// Generates fallback enterprise analysis when AI is unavailable
    /// </summary>
    private string GenerateFallbackEnterpriseAnalysis(int enterpriseId, object? enterpriseData)
    {
        return GenerateFallbackAnalysis(
            "Enterprise",
            new Dictionary<string, object?>
            {
                ["Enterprise ID"] = enterpriseId,
                ["Status"] = "Metrics available for review"
            },
            "Review operational efficiency, budget allocation, and service delivery metrics"
        );
    }

    /// <summary>
    /// Generates fallback audit analysis when AI is unavailable
    /// </summary>
    private string GenerateFallbackAuditAnalysis(object? auditData)
    {
        return GenerateFallbackAnalysis(
            "Audit",
            new Dictionary<string, object?>
            {
                ["Total Findings"] = ExtractCollectionCount(auditData, "Findings"),
                ["Review Period"] = "Last 12 months (default)"
            },
            "Address all high-priority findings and implement corrective action plans"
        );
    }

    /// <summary>
    /// Generates fallback municipal accounts analysis when AI is unavailable
    /// </summary>
    private string GenerateFallbackAccountsAnalysis(object? accountsData)
    {
        return GenerateFallbackAnalysis(
            "Municipal Accounts",
            new Dictionary<string, object?>
            {
                ["Total Accounts"] = accountsData is System.Collections.ICollection coll ? coll.Count : 0
            },
            "Review high-expenditure accounts for optimization and consolidation opportunities"
        );
    }

    /// <summary>
    /// Standardized fallback message generator for all analysis types
    /// </summary>
    private string GenerateFallbackAnalysis(string analysisType, IDictionary<string, object?> metrics, string recommendation)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📊 Basic {analysisType} Analysis (AI Unavailable)");
        sb.AppendLine();
        sb.AppendLine("Metrics:");

        foreach (var kvp in metrics)
        {
            sb.AppendLine($"  • {kvp.Key}: {kvp.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("Recommendation:");
        sb.AppendLine($"  → {recommendation}");
        sb.AppendLine();
        sb.AppendLine("⚠️  Note: AI-powered analysis failed. This fallback provides basic metrics only.");
        sb.AppendLine("   For detailed insights, ensure xAI service is configured and operational.");

        return sb.ToString();
    }

    /// <summary>
    /// Safely extracts collection count from object using reflection
    /// </summary>
    private int ExtractCollectionCount(object? data, string propertyName)
    {
        if (data == null) return 0;

        try
        {
            var property = data.GetType().GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(data);
                if (value is System.Collections.ICollection collection)
                {
                    return collection.Count;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract collection count for property {PropertyName}", propertyName);
        }

        return 0;
    }
}
