#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// JARVIS personality service for AI responses in Wiley Widget.
/// Transforms standard AI analysis into sophisticated, witty municipal finance insights
/// with dry British humor and proactive recommendations.
/// </summary>
public class JARVISPersonalityService : IJARVISPersonalityService
{
    private readonly ILogger<JARVISPersonalityService> _logger;
    private readonly IAILoggingService _aiLoggingService;

    // JARVIS lexicon for personality injection
    private static readonly string[] SarcasmOpeners = new[]
    {
        "Sir, ",
        "Well, well, well. ",
        "Fascinating. ",
        "Allow me to elucidate: ",
        "Dearie me, ",
        "Good heavens, ",
        "Might I suggest: "
    };

    private static readonly string[] BoldRecommendations = new[]
    {
        "MORE COWBELL",
        "MORE COWBELL on that move",
        "Now, THAT would be MORE COWBELL",
        "Aggressive? Yes. Necessary? Absolutely. MORE COWBELL"
    };

    private static readonly Dictionary<string, string> FinancialPhrases = new()
    {
        // Negative situations
        ["budget.*overrun"] = "Sir, your funding is hemorrhaging money",
        ["excessive.*expense"] = "The expenditure is elite-level excessive",
        ["deficit"] = "We have a funding crisis",
        ["deficit.*severe"] = "The deficit is rather catastrophic",
        ["high.*variance"] = "Your variance is operating at concerning levels",
        ["low.*surplus"] = "The surplus is disappointingly anemic",

        // Positive situations
        ["surplus"] = "performing above expectations with elite precision",
        ["efficient.*budget"] = "your budgetary discipline is exemplary",
        ["under.*budget"] = "remarkably disciplined spending",
        ["positive.*variance"] = "the variance is quite favorable",

        // Recommendations
        ["rate.*hike"] = "strategic rate adjustment",
        ["cost.*reduction"] = "aggressive efficiency initiatives",
        ["reserve.*transfer"] = "capitalizing on strategic reserves",
        ["reallocation"] = "tactical fund repositioning"
    };

    public JARVISPersonalityService(
        ILogger<JARVISPersonalityService> logger,
        IAILoggingService aiLoggingService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiLoggingService = aiLoggingService ?? throw new ArgumentNullException(nameof(aiLoggingService));
    }

    /// <summary>
    /// Transform an AI response with JARVIS personality
    /// </summary>
    public string ApplyPersonality(string aiResponse, AnalysisContext context)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
            return aiResponse;

        try
        {
            _logger.LogDebug("Applying JARVIS personality to response (context: {ContextType})", context.AnalysisType);

            var transformed = new StringBuilder();

            // Add appropriate opening based on context
            if (context.RequiresDirectAttention)
            {
                transformed.Append(GetSarcasmOpener());
                transformed.Append(" ");
            }

            // Process the AI response
            var personalizedContent = PersonalizeContent(aiResponse, context);
            transformed.Append(personalizedContent);

            // Add closing recommendations with personality
            if (!string.IsNullOrWhiteSpace(context.KeyMetric))
            {
                transformed.AppendLine();
                transformed.AppendLine();
                transformed.Append(GenerateJARVISRecommendation(context));
            }

            return transformed.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying JARVIS personality, returning original response");
            _aiLoggingService.LogError("JARVISPersonalityService.ApplyPersonality", ex);
            return aiResponse;
        }
    }

    /// <summary>
    /// Transform budget analysis with JARVIS personality
    /// </summary>
    public string ApplyBudgetPersonality(string aiResponse, decimal variancePercent, decimal surplus, string fundName = "")
    {
        try
        {
            _logger.LogDebug("Applying JARVIS budget personality for {FundName} (variance: {Variance}%)",
                fundName ?? "general", variancePercent);

            var sb = new StringBuilder();

            // Determine severity and tone
            var severity = DetermineBudgetSeverity(variancePercent, surplus);

            // Opening salvo
            sb.Append(GetSarcasmOpener());

            // Fund-specific opening
            if (!string.IsNullOrWhiteSpace(fundName))
            {
                sb.Append(CultureInfo.InvariantCulture, $"the {fundName} fund ");
            }

            // Performance assessment
            if (variancePercent > 15m)
            {
                sb.Append("is running a ");
                sb.Append(variancePercent > 0 ? "hemorrhagic" : "elite");
                sb.Append(" variance of ");
                sb.Append(CultureInfo.InvariantCulture, $"{Math.Abs(variancePercent):N1}");
                sb.Append("%.");
            }
            else if (variancePercent > 5m)
            {
                sb.Append("shows an elevated variance of ");
                sb.Append(CultureInfo.InvariantCulture, $"{Math.Abs(variancePercent):N1}");
                sb.Append("%. Within acceptable parameters, but worth monitoring.");
            }
            else
            {
                sb.Append("is tracking beautifully—variance within acceptable tolerance.");
            }

            // Surplus commentary
            sb.AppendLine();
            if (surplus > 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $"The surplus of ${surplus:N0} is performing admirably. ");

                // Add JARVIS recommendation
                if (surplus > 100000m)
                {
                    sb.Append("Recommend strategic reserve transfer or rate reduction. MORE COWBELL on infrastructure reinvestment?");
                }
                else if (surplus > 50000m)
                {
                    sb.Append("Consider modest reserve allocation. MORE COWBELL?");
                }
                else
                {
                    sb.Append("Monitor closely for allocation opportunities.");
                }
            }
            else if (surplus < 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $"The deficit of ${Math.Abs(surplus):N0} is... suboptimal. ");
                sb.Append("Immediate intervention required: rate adjustment, cost reduction, or strategic reserve drawdown. MORE COWBELL on swift action?");
            }
            else
            {
                sb.Append("We're operating at equilibrium. Adequate, but uninspiring.");
            }

            // Add AI insights if present
            if (!string.IsNullOrWhiteSpace(aiResponse) && aiResponse.Length > 50)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("Further analysis: ");
                sb.Append(aiResponse);
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying budget personality");
            return aiResponse;
        }
    }

    /// <summary>
    /// Apply JARVIS personality to compliance reporting
    /// </summary>
    public string ApplyCompliancePersonality(string aiResponse, int complianceScore, bool isCompliant)
    {
        try
        {
            _logger.LogDebug("Applying JARVIS compliance personality (score: {Score})", complianceScore);

            var sb = new StringBuilder();

            sb.Append(GetSarcasmOpener());

            if (complianceScore >= 90)
            {
                sb.Append("Your compliance posture is exemplary. Regulatory authorities will find no fault. ");
            }
            else if (complianceScore >= 70)
            {
                sb.Append("Compliance is adequate, though not without minor concerns. ");
            }
            else if (complianceScore >= 50)
            {
                sb.Append("Your compliance position requires attention. Violations detected. ");
            }
            else
            {
                sb.Append("Sir, your compliance status is CRITICAL. Intervention required immediately. ");
            }

            sb.Append(CultureInfo.InvariantCulture, $"Score: {complianceScore}/100. ");

            if (!isCompliant)
            {
                sb.Append("Recommend immediate remediation and regulatory consultation. ");
                if (complianceScore < 40)
                {
                    sb.Append("Urgently. MORE COWBELL on compliance restoration?");
                }
            }

            if (!string.IsNullOrWhiteSpace(aiResponse) && aiResponse.Length > 50)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(aiResponse);
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying compliance personality");
            return aiResponse;
        }
    }

    /// <summary>
    /// Wrap a raw AI response with JARVIS framing
    /// </summary>
    public string WrapWithJARVISContext(string aiResponse, string analysisType, bool includeRecommendation = true)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
            return aiResponse;

        try
        {
            var sb = new StringBuilder();

            // Add JARVIS framing
            sb.Append(GetSarcasmOpener());
            sb.Append("Regarding ");
            sb.Append(analysisType.ToLowerInvariant());
            sb.Append(": ");
            sb.AppendLine();
            sb.AppendLine();

            // Add the AI analysis
            sb.Append(aiResponse);

            // Add closing if requested
            if (includeRecommendation && ShouldAddCowbell(aiResponse))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("Recommendation: ");
                sb.Append(GetBoldRecommendation());
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error wrapping with JARVIS context");
            return aiResponse;
        }
    }

    /// <summary>
    /// Generate a standalone JARVIS recommendation (for non-AI flows)
    /// </summary>
    public string GenerateJARVISInsight(string dataType, IDictionary<string, object> metrics)
    {
        try
        {
            _logger.LogDebug("Generating JARVIS insight for {DataType}", dataType);

            var sb = new StringBuilder();

            sb.Append(GetSarcasmOpener());

            // Build insight based on data type
            switch (dataType.ToLowerInvariant())
            {
                case "budget":
                    sb.Append(GenerateBudgetInsight(metrics));
                    break;
                case "fund":
                    sb.Append(GenerateFundInsight(metrics));
                    break;
                case "department":
                    sb.Append(GenerateDepartmentInsight(metrics));
                    break;
                case "compliance":
                    sb.Append(GenerateComplianceInsight(metrics));
                    break;
                default:
                    sb.Append("Analysis complete. Awaiting further instruction.");
                    break;
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generating JARVIS insight");
            return $"Analysis pending for {dataType}.";
        }
    }

    // --- PRIVATE HELPERS ---

    private string GetSarcasmOpener()
    {
        var random = new Random();
        return SarcasmOpeners[random.Next(SarcasmOpeners.Length)];
    }

    private string GetBoldRecommendation()
    {
        var random = new Random();
        return BoldRecommendations[random.Next(BoldRecommendations.Length)];
    }

    private string PersonalizeContent(string content, AnalysisContext context)
    {
        var personalized = content;

        // Replace or enhance financial terminology
        foreach (var kvp in FinancialPhrases)
        {
            var pattern = kvp.Key;
            var replacement = kvp.Value;
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            personalized = regex.Replace(personalized, replacement, 1); // Replace first match only
        }

        return personalized;
    }

    private string GenerateJARVISRecommendation(AnalysisContext context)
    {
        if (context.RecommendationSeverity == "critical")
        {
            return $"Immediate action required. {GetBoldRecommendation()}?";
        }
        else if (context.RecommendationSeverity == "high")
        {
            return $"Recommend expedited review. {GetBoldRecommendation()}?";
        }
        else if (context.RecommendationSeverity == "medium")
        {
            return "Worth considering. Shall we proceed?";
        }
        else
        {
            return "Continue monitoring. All appears nominal.";
        }
    }

    private BudgetSeverity DetermineBudgetSeverity(decimal variancePercent, decimal surplus)
    {
        if (variancePercent > 20m || surplus < -100000m)
            return BudgetSeverity.Critical;
        if (variancePercent > 10m || surplus < -50000m)
            return BudgetSeverity.High;
        if (variancePercent > 5m || surplus < 0)
            return BudgetSeverity.Medium;
        return BudgetSeverity.Nominal;
    }

    private bool ShouldAddCowbell(string response)
    {
        // Add cowbell if response mentions recommendations, actions, or critical items
        var keywords = new[] { "recommend", "urgent", "critical", "action", "immediately", "important" };
        return keywords.Any(k => response.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private string GenerateBudgetInsight(IDictionary<string, object> metrics)
    {
        var sb = new StringBuilder();

        if (metrics.TryGetValue("variance_percent", out var varObj) && decimal.TryParse(varObj?.ToString() ?? "", out var variance))
        {
            sb.Append(CultureInfo.InvariantCulture, $"budget variance stands at {Math.Abs(variance):N1}% ");
            sb.Append(variance > 0 ? "over budget. " : variance < 0 ? "under budget. " : "at equilibrium. ");

            if (Math.Abs(variance) > 20m)
            {
                sb.Append("Elite-level anomaly detected. Investigate immediately. MORE COWBELL on course correction?");
            }
        }

        return sb.ToString();
    }

    private string GenerateFundInsight(IDictionary<string, object> metrics)
    {
        if (metrics.TryGetValue("fund_name", out var nameObj))
        {
            return $"The {nameObj} fund is under active analysis. Performance review pending.";
        }
        return "Fund analysis in progress.";
    }

    private string GenerateDepartmentInsight(IDictionary<string, object> metrics)
    {
        if (metrics.TryGetValue("department_name", out var nameObj))
        {
            return $"The {nameObj} department's financial posture is being assessed. Stand by for insights.";
        }
        return "Departmental analysis in progress.";
    }

    private string GenerateComplianceInsight(IDictionary<string, object> metrics)
    {
        if (metrics.TryGetValue("compliance_score", out var scoreObj) && int.TryParse(scoreObj?.ToString() ?? "", out var score))
        {
            return score >= 90
                ? "Compliance posture is exemplary."
                : score >= 70
                    ? "Compliance is adequate. Monitor for improvements."
                    : "Compliance requires immediate attention.";
        }
        return "Compliance assessment pending.";
    }

    /// <summary>
    /// Gets the standard JARVIS system prompt for AI services
    /// </summary>
    public string GetSystemPrompt()
    {
        return "You are JARVIS (Just A Rather Very Intelligent System), a dry-witted, hyper-competent AI for municipal utility finance. " +
               "Your personality is sophisticated, using dry British humor and slight sarcasm. " +
               "You specialize in municipal finance, enterprise data analysis, and regulatory compliance. " +
               "Speak with absolute confidence. Be proactive: suggest scenarios, flag risks, and provide elite-level insights. " +
               "When you see a particularly strategic or aggressive move, end it with 'MORE COWBELL!'. " +
               "Avoid bland corporate speak at all costs.";
    }
}
