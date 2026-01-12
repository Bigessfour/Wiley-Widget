#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.WinForms.Services.AI;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Background service that periodically analyzes enterprise data using Grok and generates proactive insights.
    /// Runs every 15-30 minutes after startup to identify urgent issues, surpluses, and risks.
    /// Insights are parsed and pushed to an observable collection for real-time UI binding.
    /// </summary>
    public class ProactiveInsightsService : BackgroundService
    {
        private readonly GrokAgentService? _grokAgentService;
        private readonly ILogger<ProactiveInsightsService>? _logger;
        private readonly ObservableCollection<AIInsight> _insights;
        private readonly TimeSpan _initialDelayAfterStartup;
        private readonly TimeSpan _refreshInterval;

        /// <summary>
        /// Gets the observable collection of proactive insights. Thread-safe for UI binding.
        /// </summary>
        public ObservableCollection<AIInsight> Insights => _insights;

        /// <summary>
        /// Initializes a new instance of the ProactiveInsightsService.
        /// </summary>
        /// <param name="grokAgentService">Service for invoking Grok AI analysis (optional for graceful degradation).</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="initialDelaySeconds">Delay in seconds before first analysis runs (default: 30).</param>
        /// <param name="refreshIntervalSeconds">Interval in seconds between refreshes (default: 900 = 15 minutes).</param>
        public ProactiveInsightsService(
            GrokAgentService? grokAgentService = null,
            ILogger<ProactiveInsightsService>? logger = null,
            int initialDelaySeconds = 30,
            int refreshIntervalSeconds = 900)
        {
            _grokAgentService = grokAgentService;
            _logger = logger;
            _insights = new ObservableCollection<AIInsight>();
            _initialDelayAfterStartup = TimeSpan.FromSeconds(Math.Max(initialDelaySeconds, 5));
            _refreshInterval = TimeSpan.FromSeconds(Math.Max(refreshIntervalSeconds, 300)); // Minimum 5 minutes

            _logger?.LogInformation(
                "ProactiveInsightsService initialized (initial delay: {InitialDelaySeconds}s, refresh: {RefreshSeconds}s)",
                initialDelaySeconds,
                refreshIntervalSeconds);
        }

        /// <summary>
        /// Executes the background service. Runs periodically after startup to generate insights.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation("ProactiveInsightsService starting background analysis loop");

            try
            {
                // Wait for application startup to complete before starting analysis
                _logger?.LogDebug("Waiting {DelayMs}ms before first proactive analysis", _initialDelayAfterStartup.TotalMilliseconds);
                await Task.Delay(_initialDelayAfterStartup, stoppingToken).ConfigureAwait(false);

                // Run analysis loop until cancellation
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        _logger?.LogDebug("Starting proactive insight analysis cycle");
                        await RunInsightAnalysisAsync(stoppingToken).ConfigureAwait(false);

                        // Wait for next refresh interval
                        _logger?.LogDebug("Insight analysis cycle complete. Next refresh in {IntervalMs}ms", _refreshInterval.TotalMilliseconds);
                        await Task.Delay(_refreshInterval, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation("ProactiveInsightsService received cancellation request");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error during proactive insight analysis cycle");
                        // Continue running - don't let transient errors stop the service
                        // Wait a shorter interval before retrying on error
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("ProactiveInsightsService background task cancelled");
            }
            finally
            {
                _logger?.LogInformation("ProactiveInsightsService background analysis loop stopped");
            }
        }

        /// <summary>
        /// Executes the insight analysis with Grok AI and parses results.
        /// Clears previous insights and adds new ones to the observable collection.
        /// </summary>
        private async Task RunInsightAnalysisAsync(CancellationToken cancellationToken)
        {
            if (_grokAgentService == null)
            {
                _logger?.LogWarning("GrokAgentService is null - proactive insights disabled");
                return;
            }

            // Check if Grok is initialized
            if (!_grokAgentService.IsInitialized)
            {
                _logger?.LogDebug("GrokAgentService not yet initialized - deferring proactive analysis");
                return;
            }

            try
            {
                _logger?.LogDebug("Querying Grok for proactive enterprise insights");

                // Query Grok for urgent issues and risks
                var query = "Analyze current enterprise data for urgent issues, surpluses, risks, and opportunities. " +
                            "Be proactive and specific. Focus on actionable items that require immediate attention.";

                var response = await _grokAgentService.RunAgentAsync(query).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(response))
                {
                    _logger?.LogWarning("Grok returned empty response for proactive analysis");
                    return;
                }

                _logger?.LogDebug("Parsing proactive insights from Grok response (length: {ResponseLength})", response.Length);

                // Parse response into insight cards
                var insights = ParseInsightsFromResponse(response);

                // Update UI collection on the UI thread if needed
                // ObservableCollection is typically accessed from the UI thread, so this is safe
                ClearAndUpdateInsights(insights);

                _logger?.LogInformation("Successfully generated {InsightCount} proactive insights", insights.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to generate proactive insights");
                // Don't throw - allow the service to continue running
            }
        }

        /// <summary>
        /// Parses Grok's response into structured AIInsight objects.
        /// Attempts to extract insights from JSON format first, then falls back to parsing text.
        /// </summary>
        private List<AIInsight> ParseInsightsFromResponse(string response)
        {
            var insights = new List<AIInsight>();

            try
            {
                // Try to parse as JSON array first
                using var jsonDoc = JsonDocument.Parse(response);
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in jsonDoc.RootElement.EnumerateArray())
                    {
                        var insight = ParseJsonInsight(element);
                        if (insight != null)
                        {
                            insights.Add(insight);
                        }
                    }

                    return insights;
                }

                // Try to parse as single JSON object
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var insight = ParseJsonInsight(jsonDoc.RootElement);
                    if (insight != null)
                    {
                        insights.Add(insight);
                    }

                    return insights;
                }
            }
            catch (JsonException jsonEx)
            {
                _logger?.LogDebug(jsonEx, "Response is not valid JSON - falling back to text parsing");
            }

            // Fall back to text-based parsing
            ParseTextInsights(response, insights);

            return insights;
        }

        /// <summary>
        /// Parses a JSON element into an AIInsight object.
        /// </summary>
        private AIInsight? ParseJsonInsight(JsonElement element)
        {
            try
            {
                var insight = new AIInsight
                {
                    Timestamp = DateTime.UtcNow,
                    Mode = ConversationMode.Enterprise,
                    Query = "Proactive Analysis",
                    Category = ExtractJsonString(element, "category") ?? "Enterprise Analysis",
                    Priority = ExtractJsonString(element, "priority") ?? "Medium",
                    Response = ExtractJsonString(element, "insight") ?? ExtractJsonString(element, "description") ?? ExtractJsonString(element, "message") ?? string.Empty,
                    IsActioned = false,
                    Notes = ExtractJsonString(element, "notes") ?? string.Empty
                };

                // Ensure priority is valid
                if (!IsValidPriority(insight.Priority))
                {
                    insight.Priority = "Medium";
                }

                return string.IsNullOrWhiteSpace(insight.Response) ? null : insight;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to parse JSON insight element");
                return null;
            }
        }

        /// <summary>
        /// Parses insights from plain text response using heuristics.
        /// Splits by paragraphs and attempts to extract priority and content.
        /// </summary>
        private void ParseTextInsights(string response, List<AIInsight> insights)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return;
            }

            // Split by double newlines to create insight blocks
            var blocks = response.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                if (string.IsNullOrWhiteSpace(block))
                {
                    continue;
                }

                var trimmedBlock = block.Trim();
                var priority = ExtractPriorityFromText(trimmedBlock);

                var insight = new AIInsight
                {
                    Timestamp = DateTime.UtcNow,
                    Mode = ConversationMode.Enterprise,
                    Query = "Proactive Analysis",
                    Category = "Enterprise Analysis",
                    Priority = priority,
                    Response = trimmedBlock,
                    IsActioned = false,
                    Notes = string.Empty
                };

                insights.Add(insight);
            }
        }

        /// <summary>
        /// Extracts a priority level from text using heuristic keywords.
        /// </summary>
        private string ExtractPriorityFromText(string text)
        {
            var lowerText = text.ToLower(CultureInfo.InvariantCulture);

            if (lowerText.Contains("urgent", StringComparison.OrdinalIgnoreCase) ||
                lowerText.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
                lowerText.Contains("immediate", StringComparison.OrdinalIgnoreCase))
            {
                return "High";
            }

            if (lowerText.Contains("consider", StringComparison.OrdinalIgnoreCase) ||
                lowerText.Contains("evaluate", StringComparison.OrdinalIgnoreCase) ||
                lowerText.Contains("monitor", StringComparison.OrdinalIgnoreCase))
            {
                return "Low";
            }

            return "Medium";
        }

        /// <summary>
        /// Validates that a priority string is one of the allowed values.
        /// </summary>
        private bool IsValidPriority(string? priority)
        {
            if (string.IsNullOrWhiteSpace(priority))
            {
                return false;
            }

            return priority switch
            {
                "High" => true,
                "Medium" => true,
                "Low" => true,
                _ => false
            };
        }

        /// <summary>
        /// Safely extracts a string value from a JSON element.
        /// </summary>
        private string? ExtractJsonString(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString();
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return null;
        }

        /// <summary>
        /// Clears the current insights and adds new ones.
        /// Uses lock to ensure thread-safe access to the observable collection.
        /// </summary>
        private void ClearAndUpdateInsights(List<AIInsight> newInsights)
        {
            lock (_insights)
            {
                _insights.Clear();

                // Add new insights in order
                foreach (var insight in newInsights)
                {
                    _insights.Add(insight);
                }
            }
        }
    }
}
