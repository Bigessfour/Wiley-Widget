using System;
using System.Collections.Generic;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Context for personality application
    /// </summary>
    public class AnalysisContext
    {
        public string AnalysisType { get; set; } = "Municipal Finance";
        public string KeyMetric { get; set; } = "";
        public string RecommendationSeverity { get; set; } = "medium"; // critical, high, medium, low
        public bool RequiresDirectAttention { get; set; } = false;
    }

    /// <summary>
    /// Severity levels for budget analysis
    /// </summary>
    public enum BudgetSeverity
    {
        Nominal,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Interface for JARVIS personality service
    /// </summary>
    public interface IJARVISPersonalityService
    {
        string ApplyPersonality(string aiResponse, AnalysisContext context);
        string ApplyBudgetPersonality(string aiResponse, decimal variancePercent, decimal surplus, string fundName = "");
        string ApplyCompliancePersonality(string aiResponse, int complianceScore, bool isCompliant);
        string WrapWithJARVISContext(string aiResponse, string analysisType, bool includeRecommendation = true);
        string GenerateJARVISInsight(string dataType, IDictionary<string, object> metrics);
        string GetSystemPrompt();
    }
}
