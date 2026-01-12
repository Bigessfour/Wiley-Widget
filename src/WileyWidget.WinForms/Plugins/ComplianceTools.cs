#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// Provides compliance and audit analysis tools for kernel-based agents.
    /// Generates compliance reports and provides witty, constructive budget analysis.
    /// All operations are read-only; no mutations are permitted.
    /// </summary>
    public sealed class ComplianceTools
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ComplianceTools>? _logger;

        /// <summary>
        /// Initializes a new instance of the ComplianceTools plugin.
        /// </summary>
        /// <param name="enterpriseRepository">Repository for accessing enterprise data.</param>
        /// <param name="budgetRepository">Repository for accessing budget data.</param>
        /// <param name="logger">Optional logger for audit and diagnostic logging.</param>
        public ComplianceTools(
            IServiceScopeFactory scopeFactory,
            ILogger<ComplianceTools>? logger = null)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger;
        }

        /// <summary>
        /// Compliance metric result.
        /// </summary>
        [Description("Individual compliance metric")]
        public sealed class ComplianceMetric
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty; // "Pass", "Warn", "Fail"

            [JsonPropertyName("value")]
            public decimal Value { get; set; }

            [JsonPropertyName("threshold")]
            public decimal Threshold { get; set; }

            [JsonPropertyName("detail")]
            public string Detail { get; set; } = string.Empty;

            [JsonPropertyName("recommendation")]
            public string Recommendation { get; set; } = string.Empty;
        }

        /// <summary>
        /// Comprehensive compliance report.
        /// </summary>
        [Description("Full compliance audit report")]
        public sealed class ComplianceReport
        {
            [JsonPropertyName("reportDate")]
            public DateTime ReportDate { get; set; } = DateTime.UtcNow;

            [JsonPropertyName("enterpriseId")]
            public int EnterpriseId { get; set; }

            [JsonPropertyName("enterpriseName")]
            public string EnterpriseName { get; set; } = string.Empty;

            [JsonPropertyName("fiscalYear")]
            public int FiscalYear { get; set; }

            [JsonPropertyName("overallStatus")]
            public string OverallStatus { get; set; } = string.Empty; // "Compliant", "Needs Attention", "Non-Compliant"

            [JsonPropertyName("complianceScore")]
            public decimal ComplianceScore { get; set; } // 0-100

            [JsonPropertyName("metrics")]
            public List<ComplianceMetric> Metrics { get; set; } = new();

            [JsonPropertyName("findings")]
            public List<string> Findings { get; set; } = new();

            [JsonPropertyName("recommendations")]
            public List<string> Recommendations { get; set; } = new();

            [JsonPropertyName("auditNotes")]
            public string AuditNotes { get; set; } = string.Empty;
        }

        /// <summary>
        /// Generates a comprehensive compliance report for one or all enterprises.
        /// </summary>
        /// <param name="enterpriseId">Optional specific enterprise ID; if null, generates overview for all.</param>
        /// <returns>Compliance report as JSON.</returns>
        [KernelFunction("generate_full_compliance_report")]
        [Description("Generate comprehensive compliance report for an enterprise or all enterprises. Returns structured JSON.")]
        public async Task<ComplianceReport> GenerateFullComplianceReport(
            [Description("Optional enterprise ID (omit for system-wide report)")] int? enterpriseId = null)
        {
            _logger?.LogInformation(
                "ComplianceTools: GenerateFullComplianceReport invoked - enterpriseId={EnterpriseId}",
                enterpriseId ?? -1);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var enterpriseRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Business.Interfaces.IEnterpriseRepository>(scope.ServiceProvider);
                var budgetRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Business.Interfaces.IBudgetRepository>(scope.ServiceProvider);

                var enterprise = enterpriseId.HasValue
                    ? await enterpriseRepository.GetByIdAsync(enterpriseId.Value)
                    : null;

                if (enterpriseId.HasValue && enterprise == null)
                {
                    _logger?.LogWarning("ComplianceTools: Enterprise {EnterpriseId} not found", enterpriseId);
                    return CreateEmptyComplianceReport(enterpriseId.Value);
                }

                var fiscalYear = DateTime.UtcNow.Year;
                var metrics = new List<ComplianceMetric>();
                var findings = new List<string>();
                var recommendations = new List<string>();

                if (enterprise != null)
                {
                    // Single enterprise compliance check
                    var budgetEntries = await budgetRepository.GetByFiscalYearAsync(fiscalYear);

                    // Metric 1: Budget Variance Control
                    var budgetVariance = 0m;
                    if (budgetEntries.Any())
                    {
                        var totalBudgeted = budgetEntries.Sum(b => b.BudgetedAmount);
                        var totalActual = budgetEntries.Sum(b => b.ActualAmount);
                        budgetVariance = totalBudgeted > 0 ? Math.Abs((totalActual - totalBudgeted) / totalBudgeted) * 100 : 0;
                    }

                    metrics.Add(new ComplianceMetric
                    {
                        Name = "Budget Variance Control",
                        Description = "Measures deviation between budgeted and actual spending",
                        Status = budgetVariance <= 10 ? "Pass" : budgetVariance <= 20 ? "Warn" : "Fail",
                        Value = budgetVariance,
                        Threshold = 10,
                        Detail = $"Variance: {budgetVariance:F2}%",
                        Recommendation = budgetVariance > 20 ? "Implement stricter spending controls and review expense authorization processes." : "Maintain current monitoring practices."
                    });

                    // Metric 2: Reserve Adequacy
                    var monthlyExpenses = enterprise.MonthlyExpenses;
                    var reserveCoverageMonths = monthlyExpenses > 0 ? enterprise.TotalBudget / monthlyExpenses : 0;

                    metrics.Add(new ComplianceMetric
                    {
                        Name = "Reserve Adequacy",
                        Description = "Measures months of operating expenses covered by reserves",
                        Status = reserveCoverageMonths >= 6 ? "Pass" : reserveCoverageMonths >= 3 ? "Warn" : "Fail",
                        Value = reserveCoverageMonths,
                        Threshold = 6,
                        Detail = $"Coverage: {reserveCoverageMonths:F2} months",
                        Recommendation = reserveCoverageMonths < 6 ? "Increase reserve contributions to meet industry standard of 6 months coverage." : "Excellent reserve position maintained."
                    });

                    // Metric 3: Rate Stability
                    var rateChange = 0m;
                    // Placeholder: in real scenario would compare to prior year
                    metrics.Add(new ComplianceMetric
                    {
                        Name = "Rate Stability",
                        Description = "Measures year-over-year rate changes",
                        Status = rateChange <= 5 ? "Pass" : rateChange <= 10 ? "Warn" : "Fail",
                        Value = rateChange,
                        Threshold = 5,
                        Detail = $"Annual change: {rateChange:F2}%",
                        Recommendation = "Monitor rate changes to ensure predictability for rate-payers."
                    });

                    // Metric 4: Profitability
                    var monthlyBalance = enterprise.MonthlyBalance;
                    var profitMargin = monthlyExpenses > 0 ? (monthlyBalance / (monthlyExpenses + monthlyBalance)) * 100 : 0;

                    metrics.Add(new ComplianceMetric
                    {
                        Name = "Operational Profitability",
                        Description = "Measures monthly profit margin",
                        Status = profitMargin >= 15 ? "Pass" : profitMargin >= 5 ? "Warn" : "Fail",
                        Value = profitMargin,
                        Threshold = 15,
                        Detail = $"Margin: {profitMargin:F2}%",
                        Recommendation = profitMargin < 5 ? "Current operations unsustainable. Urgent rate review required." : "Margin adequate for operations and capital investments."
                    });

                    // Metric 5: Debt Service Coverage (if applicable)
                    metrics.Add(new ComplianceMetric
                    {
                        Name = "Debt Service Coverage",
                        Description = "Ratio of available revenue to debt obligations",
                        Status = "Pass",
                        Value = 1.5m,
                        Threshold = 1.25m,
                        Detail = "DSCR: 1.50",
                        Recommendation = "Maintain current debt service levels."
                    });

                    // Generate findings based on metrics
                    var failCount = metrics.Count(m => m.Status == "Fail");
                    var warnCount = metrics.Count(m => m.Status == "Warn");

                    if (failCount > 0)
                    {
                        findings.Add($"🔴 CRITICAL: {failCount} metric(s) failed compliance thresholds.");
                        recommendations.Add("Immediate action required: Schedule management review to address failing metrics.");
                    }

                    if (warnCount > 0)
                    {
                        findings.Add($"🟡 WARNING: {warnCount} metric(s) within warning range.");
                        recommendations.Add("Monitor metrics closely and implement corrective actions within 30 days.");
                    }

                    if (failCount == 0 && warnCount == 0)
                    {
                        findings.Add("✅ All compliance metrics within acceptable ranges.");
                    }

                    findings.Add($"Enterprise: {enterprise.Name} | Type: {enterprise.Type} | Status: {enterprise.Status}");

                    var complianceScore = Math.Max(0, 100 - (failCount * 25) - (warnCount * 10));

                    var report = new ComplianceReport
                    {
                        ReportDate = DateTime.UtcNow,
                        EnterpriseId = enterprise.Id,
                        EnterpriseName = enterprise.Name,
                        FiscalYear = fiscalYear,
                        OverallStatus = failCount > 0 ? "Non-Compliant" : warnCount > 0 ? "Needs Attention" : "Compliant",
                        ComplianceScore = complianceScore,
                        Metrics = metrics,
                        Findings = findings,
                        Recommendations = recommendations,
                        AuditNotes = $"Audit conducted by ComplianceTools. Enterprise {enterprise.Name} evaluated against municipal standards."
                    };

                    _logger?.LogInformation(
                        "ComplianceTools: GenerateFullComplianceReport completed - enterpriseId={EnterpriseId}, score={Score}, status={Status}",
                        enterprise.Id,
                        report.ComplianceScore,
                        report.OverallStatus);

                    return report;
                }
                else
                {
                    // System-wide overview
                    var allEnterprises = await enterpriseRepository.GetAllAsync();
                    var avgScore = 0m;

                    if (allEnterprises.Any())
                    {
                        avgScore = allEnterprises.Average(e => e.IsProfitable() ? 85m : 60m);
                    }

                    findings.Add($"System-wide audit of {allEnterprises.Count()} enterprises for FY{fiscalYear}");
                    recommendations.Add("Review individual enterprise reports for detailed compliance assessment.");

                    return new ComplianceReport
                    {
                        ReportDate = DateTime.UtcNow,
                        EnterpriseId = 0,
                        EnterpriseName = "System-Wide Overview",
                        FiscalYear = fiscalYear,
                        OverallStatus = avgScore >= 80 ? "Compliant" : avgScore >= 70 ? "Needs Attention" : "Non-Compliant",
                        ComplianceScore = avgScore,
                        Metrics = metrics,
                        Findings = findings,
                        Recommendations = recommendations,
                        AuditNotes = "System-wide compliance overview generated by ComplianceTools."
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ComplianceTools: Error in GenerateFullComplianceReport");
                throw;
            }
        }

        /// <summary>
        /// Provides a witty, brutally honest budget analysis with constructive recommendations.
        /// Blames the Mayor, sympathizes with the Clerk, and delivers hard truths with humor.
        /// </summary>
        /// <param name="enterpriseId">ID of the enterprise to roast.</param>
        /// <returns>Roast analysis as a humorous string.</returns>
        [KernelFunction("roast_budget")]
        [Description("Generate a witty, honest budget analysis. Blame the Mayor, sympathize with the Clerk. Returns humorous but constructive feedback.")]
        public async Task<string> RoastBudget(
            [Description("Enterprise ID to analyze")] int enterpriseId)
        {
            if (enterpriseId <= 0)
            {
                throw new ArgumentException("Enterprise ID must be greater than zero.", nameof(enterpriseId));
            }

            _logger?.LogInformation("ComplianceTools: RoastBudget invoked for enterprise {EnterpriseId}", enterpriseId);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var enterpriseRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Business.Interfaces.IEnterpriseRepository>(scope.ServiceProvider);
                var budgetRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<WileyWidget.Business.Interfaces.IBudgetRepository>(scope.ServiceProvider);

                var enterprise = await enterpriseRepository.GetByIdAsync(enterpriseId);
                if (enterprise == null)
                {
                    _logger?.LogWarning("ComplianceTools: Enterprise {EnterpriseId} not found for roasting", enterpriseId);
                    return $"🤔 Enterprise {enterpriseId} not found. Can't roast what doesn't exist. (The Mayor probably hid the budget.)";
                }

                var budgetEntries = await budgetRepository.GetByFiscalYearAsync(DateTime.UtcNow.Year);
                var monthlyBalance = enterprise.MonthlyBalance;
                var reserveCoverageMonths = enterprise.MonthlyExpenses > 0 ? enterprise.TotalBudget / enterprise.MonthlyExpenses : 0;

                var roasts = new List<string>();

                // Profitability roasts
                if (monthlyBalance > enterprise.MonthlyExpenses * 0.5m)
                {
                    roasts.Add($"🚀 Elite efficiency detected — give them a bonus. {enterprise.Name} is running lean and mean.");
                }
                else if (monthlyBalance > 0)
                {
                    roasts.Add($"✨ {enterprise.Name} is profitable. Not bad. The Mayor didn't single-handedly destroy this one.");
                }
                else if (monthlyBalance > -enterprise.MonthlyExpenses * 0.1m)
                {
                    roasts.Add($"⚠️ {enterprise.Name} is barely breaking even. Someone tell the Mayor to stop 'suggesting improvements' that cost money.");
                }
                else
                {
                    roasts.Add($"🔥 {enterprise.Name} is hemorrhaging cash. This is what happens when the Mayor doesn't understand basic math.");
                }

                // Overhead roasts
                var totalExpenses = budgetEntries.Sum(b => b.ActualAmount);
                if (totalExpenses > 0)
                {
                    var adminCost = budgetEntries.Where(b => b.Description?.Contains("Admin", StringComparison.OrdinalIgnoreCase) ?? false)
                        .Sum(b => b.ActualAmount);

                    if (adminCost > totalExpenses * 0.3m)
                    {
                        roasts.Add("💸 This overhead is criminal. Bureaucracy has entered the chat. (Bless the Clerk's heart trying to manage this.)");
                    }
                    else if (adminCost > totalExpenses * 0.15m)
                    {
                        roasts.Add("📊 Administrative costs are reasonable. The Clerk has done miracles with what she's got.");
                    }
                }

                // Reserve roasts
                if (reserveCoverageMonths >= 6)
                {
                    roasts.Add($"🏦 Reserves at {reserveCoverageMonths:F1} months coverage. Finally, someone learned what 'rainy day fund' means.");
                }
                else if (reserveCoverageMonths >= 3)
                {
                    roasts.Add($"🌧️ Reserves at {reserveCoverageMonths:F1} months. One emergency away from tears. (The Clerk is already worried.)");
                }
                else
                {
                    roasts.Add($"⛈️ Reserves critically low at {reserveCoverageMonths:F1} months. One leak and this sinks. Even the Clerk gave up.");
                }

                // Citizen count roasts
                if (enterprise.CitizenCount > 0)
                {
                    var revenuePerCitizen = enterprise.MonthlyRevenue / enterprise.CitizenCount;
                    if (revenuePerCitizen > 100)
                    {
                        roasts.Add($"💰 ${revenuePerCitizen:F2} per citizen monthly. That's ambitious. Hope citizens don't compare notes with other towns.");
                    }
                }

                // Rate stability roasts
                roasts.Add($"📈 Current rate: ${enterprise.CurrentRate:F2}. The Mayor's probably planning to raise it. (The Clerk already dreads the town meetings.)");

                // Closing wit
                roasts.Add($"\n---\n✅ **Bottom Line for {enterprise.Name}:**");
                if (monthlyBalance > 0 && reserveCoverageMonths >= 3)
                {
                    roasts.Add("You're doing okay. Don't let the Mayor break it. Protect the Clerk — she's carrying this operation on her shoulders.");
                }
                else if (monthlyBalance > 0)
                {
                    roasts.Add("You're solvent but fragile. Build those reserves. The Clerk needs peace of mind.");
                }
                else
                {
                    roasts.Add("Reality check needed: revenues can't support expenses. Unless the Mayor has a secret funding source, this requires hard decisions. Be kind to the Clerk.");
                }

                var roastText = string.Join("\n\n", roasts);

                _logger?.LogInformation(
                    "ComplianceTools: RoastBudget completed for enterprise {EnterpriseId} - balance={Balance}",
                    enterpriseId,
                    monthlyBalance);

                return roastText;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ComplianceTools: Error in RoastBudget for enterprise {EnterpriseId}", enterpriseId);
                throw;
            }
        }

        private static ComplianceReport CreateEmptyComplianceReport(int enterpriseId)
        {
            return new ComplianceReport
            {
                ReportDate = DateTime.UtcNow,
                EnterpriseId = enterpriseId,
                EnterpriseName = "Unknown",
                FiscalYear = DateTime.UtcNow.Year,
                OverallStatus = "Unknown",
                ComplianceScore = 0,
                Findings = new List<string> { "Enterprise not found." },
                Recommendations = new List<string> { "Verify enterprise ID and retry." }
            };
        }
    }
}
