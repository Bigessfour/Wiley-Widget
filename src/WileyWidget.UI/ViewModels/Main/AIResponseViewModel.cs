using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Prism.Mvvm;
using WileyWidget.Models;

namespace WileyWidget.ViewModels.Main {
    /// <summary>
    /// ViewModel for displaying XAI AI responses with structured insights.
    /// Designed for municipal finance value delivery through visual presentation in SfDataGrid.
    /// Parses XAI responses to extract actionable municipal utility insights.
    /// </summary>
    public partial class AIResponseViewModel : BindableBase
    {
        private readonly ILogger<AIResponseViewModel> _logger;

        private string responseText;
        public string ResponseText
        {
            get => responseText;
            set => SetProperty(ref responseText, value);
        }

        private DateTime responseTimestamp;
        public DateTime ResponseTimestamp
        {
            get => responseTimestamp;
            set => SetProperty(ref responseTimestamp, value);
        }

        private string queryText;
        public string QueryText
        {
            get => queryText;
            set => SetProperty(ref queryText, value);
        }

        private int tokensUsed;
        public int TokensUsed
        {
            get => tokensUsed;
            set => SetProperty(ref tokensUsed, value);
        }

        private long responseTimeMs;
        public long ResponseTimeMs
        {
            get => responseTimeMs;
            set => SetProperty(ref responseTimeMs, value);
        }

        private bool hasInsights;
        public bool HasInsights
        {
            get => hasInsights;
            set => SetProperty(ref hasInsights, value);
        }

        private bool hasRecommendations;
        public bool HasRecommendations
        {
            get => hasRecommendations;
            set => SetProperty(ref hasRecommendations, value);
        }

        private bool hasWarnings;
        public bool HasWarnings
        {
            get => hasWarnings;
            set => SetProperty(ref hasWarnings, value);
        }

        // Prism DelegateCommand property for UI bindings
        public Prism.Commands.DelegateCommand ClearCommand { get; private set; }

        /// <summary>
        /// Collection of extracted insights from XAI response.
        /// Bound to SfDataGrid for visual display.
        /// </summary>
        public ObservableCollection<AIResponseInsight> Insights { get; }

        /// <summary>
        /// Collection of recommendations from XAI response.
        /// </summary>
        public ObservableCollection<AIRecommendation> Recommendations { get; }

        /// <summary>
        /// Collection of warnings or alerts from XAI response.
        /// </summary>
        public ObservableCollection<AIWarning> Warnings { get; }

        /// <summary>
        /// Summary statistics extracted from XAI response.
        /// </summary>
        private ResponseSummary summary;
        public ResponseSummary Summary
        {
            get => summary;
            set => SetProperty(ref summary, value);
        }

        /// <summary>
        /// Initializes a new instance of AIResponseViewModel.
        /// </summary>
        public AIResponseViewModel(ILogger<AIResponseViewModel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Insights = new ObservableCollection<AIResponseInsight>();
            Recommendations = new ObservableCollection<AIRecommendation>();
            Warnings = new ObservableCollection<AIWarning>();
            ResponseTimestamp = DateTime.Now;
            Summary = new ResponseSummary();

            // Initialize Prism commands
            ClearCommand = new Prism.Commands.DelegateCommand(Clear);

            _logger.LogDebug("AIResponseViewModel initialized");
        }

        /// <summary>
        /// Parameterless constructor for design-time support.
        /// </summary>
        public AIResponseViewModel() : this(null)
        {
            // Design-time sample data
            ResponseText = "Sample XAI response for municipal utility analysis...";
            QueryText = "Sample query";
            ResponseTimestamp = DateTime.Now;
            TokensUsed = 150;
            ResponseTimeMs = 1250;
        }

        /// <summary>
        /// Parses XAI response text to extract structured insights.
        /// Identifies municipal-specific patterns, financial data, and recommendations.
        /// </summary>
        /// <param name="responseText">Raw XAI response text</param>
        public void ParseResponse(string responseText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    _logger?.LogWarning("Attempted to parse null or empty response");
                    return;
                }

                ResponseText = responseText;
                ResponseTimestamp = DateTime.Now;

                _logger?.LogInformation("Parsing XAI response: {Length} characters", responseText.Length);

                // Clear existing collections
                Insights.Clear();
                Recommendations.Clear();
                Warnings.Clear();

                // Parse structured insights
                ParseInsights(responseText);

                // Parse recommendations
                ParseRecommendations(responseText);

                // Parse warnings and alerts
                ParseWarnings(responseText);

                // Extract summary statistics
                ExtractSummary(responseText);

                // Update flags
                HasInsights = Insights.Any();
                HasRecommendations = Recommendations.Any();
                HasWarnings = Warnings.Any();

                _logger?.LogInformation("Response parsed: {InsightCount} insights, {RecommendationCount} recommendations, {WarningCount} warnings",
                    Insights.Count, Recommendations.Count, Warnings.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error parsing XAI response");
            }
        }

        /// <summary>
        /// Parses insights from XAI response text.
        /// Looks for patterns like "Insight:", bullet points, numbered lists, and financial data.
        /// </summary>
        private void ParseInsights(string text)
        {
            try
            {
                // Pattern 1: Explicit "Insight:" sections
                var insightMatches = Regex.Matches(text, @"(?:Insight|Finding|Analysis):\s*(.+?)(?=\n\n|\n[A-Z]|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match match in insightMatches)
                {
                    var insightText = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(insightText))
                    {
                        Insights.Add(new AIResponseInsight
                        {
                            Title = "AI Insight",
                            Description = insightText,
                            Category = DetermineCategory(insightText),
                            Confidence = 0.85,
                            Timestamp = DateTime.Now
                        });
                    }
                }

                // Pattern 2: Numbered insights (1., 2., 3., etc.)
                var numberedMatches = Regex.Matches(text, @"^\d+\.\s+(.+?)(?=\n\d+\.|\n\n|$)",
                    RegexOptions.Multiline);

                foreach (Match match in numberedMatches)
                {
                    var insightText = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(insightText) && insightText.Length > 20)
                    {
                        Insights.Add(new AIResponseInsight
                        {
                            Title = "Municipal Finance Insight",
                            Description = insightText,
                            Category = DetermineCategory(insightText),
                            Confidence = 0.80,
                            Timestamp = DateTime.Now
                        });
                    }
                }

                // Pattern 3: Financial metrics (e.g., "$25,000", "15% increase")
                var financialMatches = Regex.Matches(text,
                    @"(\$[\d,]+(?:\.\d{2})?|[\d,]+(?:\.\d+)?%)\s+(?:in|for|from|to)\s+([^.]+?)\.?(?=\n|$)",
                    RegexOptions.Multiline);

                foreach (Match match in financialMatches)
                {
                    var amount = match.Groups[1].Value;
                    var context = match.Groups[2].Value.Trim();

                    if (!string.IsNullOrWhiteSpace(context))
                    {
                        Insights.Add(new AIResponseInsight
                        {
                            Title = $"Financial Metric: {amount}",
                            Description = context,
                            Category = "Financial",
                            Confidence = 0.90,
                            Timestamp = DateTime.Now,
                            Value = amount
                        });
                    }
                }

                _logger?.LogDebug("Extracted {Count} insights from response", Insights.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error parsing insights");
            }
        }

        /// <summary>
        /// Parses recommendations from XAI response text.
        /// </summary>
        private void ParseRecommendations(string text)
        {
            try
            {
                // Pattern: "Recommend:", "Recommendation:", or "Action:"
                var recommendationMatches = Regex.Matches(text,
                    @"(?:Recommend|Recommendation|Action|Suggested Action):\s*(.+?)(?=\n\n|\n[A-Z]|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match match in recommendationMatches)
                {
                    var recommendationText = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(recommendationText))
                    {
                        Recommendations.Add(new AIRecommendation
                        {
                            Title = "AI Recommendation",
                            Description = recommendationText,
                            Priority = DeterminePriority(recommendationText),
                            ImpactLevel = "Medium",
                            Timestamp = DateTime.Now
                        });
                    }
                }

                // Pattern: "Should", "Must", "Consider" statements
                var actionMatches = Regex.Matches(text,
                    @"(?:should|must|consider|recommend)\s+([^.]+?)\.(?=\s|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);

                foreach (Match match in actionMatches)
                {
                    var actionText = match.Value.Trim();
                    if (actionText.Length > 30 && !Recommendations.Any(r => r.Description.Contains(actionText, StringComparison.OrdinalIgnoreCase)))
                    {
                        Recommendations.Add(new AIRecommendation
                        {
                            Title = "Suggested Action",
                            Description = actionText,
                            Priority = DeterminePriority(actionText),
                            ImpactLevel = actionText.Contains("must", StringComparison.OrdinalIgnoreCase) ? "High" : "Medium",
                            Timestamp = DateTime.Now
                        });
                    }
                }

                _logger?.LogDebug("Extracted {Count} recommendations from response", Recommendations.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error parsing recommendations");
            }
        }

        /// <summary>
        /// Parses warnings and alerts from XAI response text.
        /// </summary>
        private void ParseWarnings(string text)
        {
            try
            {
                // Pattern: "Warning:", "Alert:", "Caution:", "Risk:"
                var warningMatches = Regex.Matches(text,
                    @"(?:Warning|Alert|Caution|Risk|Note):\s*(.+?)(?=\n\n|\n[A-Z]|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match match in warningMatches)
                {
                    var warningText = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(warningText))
                    {
                        Warnings.Add(new AIWarning
                        {
                            Title = "AI Warning",
                            Message = warningText,
                            Severity = DetermineSeverity(warningText),
                            Timestamp = DateTime.Now
                        });
                    }
                }

                _logger?.LogDebug("Extracted {Count} warnings from response", Warnings.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error parsing warnings");
            }
        }

        /// <summary>
        /// Extracts summary statistics from XAI response.
        /// </summary>
        private void ExtractSummary(string text)
        {
            try
            {
                Summary = new ResponseSummary
                {
                    TotalInsights = Insights.Count,
                    TotalRecommendations = Recommendations.Count,
                    TotalWarnings = Warnings.Count,
                    HighPriorityItems = Recommendations.Count(r => r.Priority == "High"),
                    FinancialMetrics = Insights.Count(i => i.Category == "Financial"),
                    ResponseLength = text.Length,
                    Timestamp = DateTime.Now
                };

                _logger?.LogDebug("Summary extracted: {InsightCount} insights, {RecommendationCount} recommendations",
                    Summary.TotalInsights, Summary.TotalRecommendations);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error extracting summary");
            }
        }

        /// <summary>
        /// Determines the category of an insight based on keywords.
        /// </summary>
        private string DetermineCategory(string text)
        {
            if (text.Contains("budget", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("financial", StringComparison.OrdinalIgnoreCase))
                return "Financial";

            if (text.Contains("compliance", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("regulation", StringComparison.OrdinalIgnoreCase))
                return "Compliance";

            if (text.Contains("efficiency", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("performance", StringComparison.OrdinalIgnoreCase))
                return "Performance";

            if (text.Contains("risk", StringComparison.OrdinalIgnoreCase))
                return "Risk";

            return "General";
        }

        /// <summary>
        /// Determines the priority of a recommendation based on keywords.
        /// </summary>
        private string DeterminePriority(string text)
        {
            if (text.Contains("urgent", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("must", StringComparison.OrdinalIgnoreCase))
                return "High";

            if (text.Contains("should", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("important", StringComparison.OrdinalIgnoreCase))
                return "Medium";

            return "Low";
        }

        /// <summary>
        /// Determines the severity of a warning based on keywords.
        /// </summary>
        private string DetermineSeverity(string text)
        {
            if (text.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("severe", StringComparison.OrdinalIgnoreCase))
                return "Critical";

            if (text.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("risk", StringComparison.OrdinalIgnoreCase))
                return "Warning";

            return "Information";
        }

        /// <summary>
        /// Clears all parsed data.
        /// </summary>
        public void Clear()
        {
            ResponseText = string.Empty;
            QueryText = string.Empty;
            TokensUsed = 0;
            ResponseTimeMs = 0;
            Insights.Clear();
            Recommendations.Clear();
            Warnings.Clear();
            HasInsights = false;
            HasRecommendations = false;
            HasWarnings = false;
            Summary = new ResponseSummary();

            _logger?.LogDebug("AIResponseViewModel cleared");
        }
    }

    /// <summary>
    /// Represents a single AI insight for municipal finance.
    /// </summary>
    public class AIResponseInsight
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public double Confidence { get; set; }
        public DateTime Timestamp { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// Represents an AI recommendation for action.
    /// </summary>
    public class AIRecommendation
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Priority { get; set; }
        public string ImpactLevel { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Represents an AI warning or alert.
    /// </summary>
    public class AIWarning
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Summary statistics for AI response.
    /// </summary>
    public class ResponseSummary
    {
        public int TotalInsights { get; set; }
        public int TotalRecommendations { get; set; }
        public int TotalWarnings { get; set; }
        public int HighPriorityItems { get; set; }
        public int FinancialMetrics { get; set; }
        public int ResponseLength { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
