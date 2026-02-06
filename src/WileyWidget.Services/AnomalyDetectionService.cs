using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implements AI-powered anomaly detection using Grok LLM for financial data analysis.
    /// </summary>
    public class AnomalyDetectionService : IAnomalyDetectionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AnomalyDetectionService> _logger;
        private readonly Lazy<IChatCompletionService?> _chatService;

        public AnomalyDetectionService(
            IConfiguration configuration,
            ILogger<AnomalyDetectionService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _chatService = new Lazy<IChatCompletionService?>(() => InitializeChatService());
        }

        private IChatCompletionService? InitializeChatService()
        {
            try
            {
                var apiKey = _configuration["XAI_API_KEY"] ?? _configuration["XAI:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogWarning("[ANOMALY_DETECTION] API key not configured");
                    return null;
                }

                var kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: "grok-beta",
                    apiKey: apiKey,
                    endpoint: new Uri("https://api.x.ai/v1"));

                // Load the anomaly detection plugin locally so this service can use it
                kernelBuilder.Plugins.AddFromType<Plugins.AnomalyDetectionPlugin>();

                var kernel = kernelBuilder.Build();
                _logger.LogInformation("[ANOMALY_DETECTION] Chat service initialized successfully");
                return kernel.GetRequiredService<IChatCompletionService>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ANOMALY_DETECTION] Failed to initialize chat service");
                return null;
            }
        }

        public async Task<List<AnomalyDetectionResult<T>>> DetectAnomaliesAsync<T>(
            IEnumerable<T> items,
            Func<T, double> valueExtractor,
            Func<T, string> contextExtractor,
            CancellationToken cancellationToken = default) where T : class
        {
            var chatService = _chatService.Value;
            if (chatService == null)
            {
                _logger.LogWarning("[ANOMALY_DETECTION] Service unavailable - using statistical detection");
                return await FallbackStatisticalDetectionAsync(items, valueExtractor, contextExtractor);
            }

            var itemsList = items.ToList();
            if (itemsList.Count == 0)
                return new List<AnomalyDetectionResult<T>>();

            var values = itemsList.Select(valueExtractor).ToList();
            var mean = values.Average();
            var stdDev = Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));

            // Build context for AI analysis
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Analyze the following dataset for anomalies. For each anomalous item, provide:");
            promptBuilder.AppendLine("1. The item identifier/context");
            promptBuilder.AppendLine("2. Severity (0.0 to 1.0)");
            promptBuilder.AppendLine("3. Brief explanation of why it's anomalous");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Dataset statistics: Mean={mean:F2}, StdDev={stdDev:F2}");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Data items:");

            for (int i = 0; i < itemsList.Count && i < 100; i++) // Limit to 100 items for token constraints
            {
                var value = valueExtractor(itemsList[i]);
                var context = contextExtractor(itemsList[i]);
                promptBuilder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"[{i}] {context}: {value:F2}");
            }

            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Respond in format: INDEX|SEVERITY|EXPLANATION (one per line)");

            try
            {
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage("You are a financial data analyst expert at detecting anomalies.");
                chatHistory.AddUserMessage(promptBuilder.ToString());

                var response = await chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
                var responseText = response.Content ?? string.Empty;

                return ParseAnomalyResponse(responseText, itemsList);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ANOMALY_DETECTION] AI analysis failed - falling back to statistical method");
                return await FallbackStatisticalDetectionAsync(items, valueExtractor, contextExtractor);
            }
        }

        public async Task<AnomalyAnalysis> AnalyzeBudgetVarianceAsync(
            decimal budgetedAmount,
            decimal actualAmount,
            string accountName,
            string period,
            CancellationToken cancellationToken = default)
        {
            var variance = actualAmount - budgetedAmount;
            var variancePercent = budgetedAmount != 0 ? (variance / budgetedAmount) * 100 : 0;

            var chatService = _chatService.Value;
            if (chatService == null)
            {
                return new AnomalyAnalysis
                {
                    IsAnomaly = Math.Abs(variancePercent) > 15,
                    Severity = Math.Min(Math.Abs((double)variancePercent) / 100.0, 1.0),
                    Explanation = $"Variance of {variancePercent:F1}% detected (statistical analysis only)",
                    VariancePercent = variancePercent,
                    RecommendedActions = GetStatisticalRecommendations(variancePercent)
                };
            }

            var prompt = $@"Analyze this budget variance for anomalies:

Account: {accountName}
Period: {period}
Budgeted: {budgetedAmount:C}
Actual: {actualAmount:C}
Variance: {variance:C} ({variancePercent:F1}%)

Provide:
1. Is this an anomaly? (YES/NO)
2. Severity score (0.0-1.0)
3. Brief explanation (2-3 sentences)
4. Up to 3 recommended actions

Format: ANOMALY|SEVERITY|EXPLANATION|ACTION1;ACTION2;ACTION3";

            try
            {
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage("You are a municipal budget analyst expert.");
                chatHistory.AddUserMessage(prompt);

                var response = await chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
                var responseText = response.Content ?? string.Empty;

                return ParseVarianceAnalysis(responseText, variancePercent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ANOMALY_DETECTION] Variance analysis failed");
                return new AnomalyAnalysis
                {
                    IsAnomaly = Math.Abs(variancePercent) > 15,
                    Severity = Math.Min(Math.Abs((double)variancePercent) / 100.0, 1.0),
                    Explanation = $"Analysis unavailable. Variance: {variancePercent:F1}%",
                    VariancePercent = variancePercent
                };
            }
        }

        private List<AnomalyDetectionResult<T>> ParseAnomalyResponse<T>(string response, List<T> items) where T : class
        {
            var results = new List<AnomalyDetectionResult<T>>();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 3 && int.TryParse(parts[0].Trim(), out int index) && index < items.Count)
                {
                    if (double.TryParse(parts[1].Trim(), out double severity))
                    {
                        results.Add(new AnomalyDetectionResult<T>
                        {
                            Item = items[index],
                            ActualValue = 0, // Not provided in response
                            Severity = severity,
                            Explanation = parts[2].Trim()
                        });
                    }
                }
            }

            return results;
        }

        private AnomalyAnalysis ParseVarianceAnalysis(string response, decimal variancePercent)
        {
            var parts = response.Split('|');
            if (parts.Length >= 3)
            {
                var isAnomaly = parts[0].Trim().Equals("YES", StringComparison.OrdinalIgnoreCase);
                var severityParsed = double.TryParse(parts[1].Trim(), out double severity);
                var explanation = parts[2].Trim();
                var actions = parts.Length > 3
                    ? parts[3].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToList()
                    : new List<string>();

                return new AnomalyAnalysis
                {
                    IsAnomaly = isAnomaly,
                    Severity = severityParsed ? severity : 0.0,
                    Explanation = explanation,
                    VariancePercent = variancePercent,
                    RecommendedActions = actions
                };
            }

            return new AnomalyAnalysis
            {
                IsAnomaly = Math.Abs(variancePercent) > 15,
                Severity = 0.5,
                Explanation = "Unable to parse AI response",
                VariancePercent = variancePercent
            };
        }

        private Task<List<AnomalyDetectionResult<T>>> FallbackStatisticalDetectionAsync<T>(
            IEnumerable<T> items,
            Func<T, double> valueExtractor,
            Func<T, string> contextExtractor) where T : class
        {
            var itemsList = items.ToList();
            var values = itemsList.Select(valueExtractor).ToList();

            var mean = values.Average();
            var stdDev = Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));

            var results = new List<AnomalyDetectionResult<T>>();

            for (int i = 0; i < itemsList.Count; i++)
            {
                var value = values[i];
                var zScore = Math.Abs((value - mean) / stdDev);

                if (zScore > 2.5) // More than 2.5 standard deviations
                {
                    results.Add(new AnomalyDetectionResult<T>
                    {
                        Item = itemsList[i],
                        ActualValue = value,
                        Severity = Math.Min(zScore / 5.0, 1.0),
                        Explanation = $"Statistical outlier: {zScore:F1} standard deviations from mean",
                        ExpectedRange = $"{mean - 2 * stdDev:F2} to {mean + 2 * stdDev:F2}"
                    });
                }
            }

            return Task.FromResult(results);
        }

        private List<string> GetStatisticalRecommendations(decimal variancePercent)
        {
            var recommendations = new List<string>();

            if (Math.Abs(variancePercent) > 50)
            {
                recommendations.Add("Immediate investigation required - variance exceeds 50%");
                recommendations.Add("Review account classification and fund allocation");
            }
            else if (Math.Abs(variancePercent) > 25)
            {
                recommendations.Add("Monitor closely - significant variance detected");
                recommendations.Add("Verify transaction categorization");
            }
            else if (Math.Abs(variancePercent) > 15)
            {
                recommendations.Add("Review for potential seasonal or timing factors");
            }

            return recommendations;
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            return _chatService.Value != null;
        }
    }
}
