using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Background service that warms AI service caches with common queries on startup
/// to improve initial response times and reduce cold-start latency
/// </summary>
public class AICacheWarmingService : IHostedService
{
    private readonly IAIService _aiService;
    private readonly IGrokRecommendationService? _recommendationService;
    private readonly ILogger<AICacheWarmingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly bool _enabled;
    private readonly int _delaySeconds;

    /// <summary>
    /// Initializes the cache warming service
    /// </summary>
    /// <param name="aiService">AI service instance</param>
    /// <param name="recommendationService">Recommendation service instance (optional)</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">Configuration</param>
    public AICacheWarmingService(
        IAIService aiService,
        ILogger<AICacheWarmingService> logger,
        IConfiguration configuration,
        IGrokRecommendationService? recommendationService = null)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _recommendationService = recommendationService;

        // Check if cache warming is enabled
        _enabled = bool.Parse(configuration["AI:CacheWarming:Enabled"] ?? "true");
        _delaySeconds = int.Parse(configuration["AI:CacheWarming:DelaySeconds"] ?? "10", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Starts the cache warming process after application startup
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("AI cache warming is disabled");
            return;
        }

        _logger.LogInformation("AI cache warming scheduled to start in {Delay} seconds", _delaySeconds);

        // Run cache warming in background after delay to not block startup
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait for application to finish initializing
                await Task.Delay(TimeSpan.FromSeconds(_delaySeconds), cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Cache warming cancelled before start");
                    return;
                }

                await WarmCachesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Cache warming cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache warming failed with exception");
            }
        }, cancellationToken);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the cache warming service
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cache warming service stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Warms caches with common queries
    /// </summary>
    private async Task WarmCachesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔥 Starting AI cache warming...");
        var startTime = DateTime.UtcNow;

        // Common queries for budget analysis
        var budgetQueries = GetBudgetQueries();

        // Common queries for general insights
        var generalQueries = GetGeneralQueries();

        // Warm XAIService cache
        await WarmXAIServiceCacheAsync(budgetQueries.Concat(generalQueries), cancellationToken);

        // Warm recommendation service cache if available
        if (_recommendationService != null)
        {
            await WarmRecommendationServiceCacheAsync(cancellationToken);
        }

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "✓ AI cache warming completed in {Duration:F1} seconds",
            duration.TotalSeconds);
    }

    /// <summary>
    /// Warms XAIService cache with common queries
    /// </summary>
    private async Task WarmXAIServiceCacheAsync(
        IEnumerable<(string Context, string Question)> queries,
        CancellationToken cancellationToken)
    {
        var queriesList = queries.ToList();
        _logger.LogInformation("Warming XAIService cache with {Count} queries...", queriesList.Count);

        var successCount = 0;
        var failureCount = 0;

        // Execute queries in parallel with controlled concurrency
        var tasks = queriesList.Select(async query =>
        {
            try
            {
                var result = await _aiService.GetInsightsAsync(
                    query.Context,
                    query.Question,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(result))
                {
                    Interlocked.Increment(ref successCount);
                    _logger.LogDebug("Cached query: {Question}", query.Question);
                }
                else
                {
                    Interlocked.Increment(ref failureCount);
                    _logger.LogWarning("Empty result for query: {Question}", query.Question);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failureCount);
                _logger.LogWarning(ex, "Failed to cache query: {Question}", query.Question);
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "XAIService cache warming complete: {Success} succeeded, {Failures} failed",
            successCount, failureCount);
    }

    /// <summary>
    /// Warms recommendation service cache with common scenarios
    /// </summary>
    private async Task WarmRecommendationServiceCacheAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Warming recommendation service cache...");

        var scenarios = GetRecommendationScenarios();
        var successCount = 0;
        var failureCount = 0;

        foreach (var scenario in scenarios)
        {
            try
            {
                // Create department expenses dictionary from scenario
                var expenses = new Dictionary<string, decimal>
                {
                    { scenario.Context, scenario.ActualSpent }
                };

                var result = await _recommendationService!.GetRecommendedAdjustmentFactorsAsync(
                    expenses,
                    15.0m, // Default target profit margin
                    cancellationToken);

                if (result != null)
                {
                    Interlocked.Increment(ref successCount);
                }
                else
                {
                    Interlocked.Increment(ref failureCount);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failureCount);
                _logger.LogWarning(ex, "Failed to warm recommendation cache for scenario: {Context}", scenario.Context);
            }
        }

        _logger.LogInformation(
            "Recommendation service cache warming complete: {Success} succeeded, {Failures} failed",
            successCount, failureCount);
    }

    /// <summary>
    /// Gets common budget analysis queries
    /// </summary>
    private static List<(string Context, string Question)> GetBudgetQueries()
    {
        return new List<(string, string)>
        {
            ("Budget Analysis", "What are our top spending categories this month?"),
            ("Budget Analysis", "Are we over budget in any departments?"),
            ("Budget Analysis", "What is our budget utilization rate?"),
            ("Revenue Analysis", "What are our main revenue sources?"),
            ("Revenue Analysis", "How does this month compare to last year?"),
            ("Compliance", "Are we meeting all municipal reporting requirements?")
        };
    }

    /// <summary>
    /// Gets common general insight queries
    /// </summary>
    private static List<(string Context, string Question)> GetGeneralQueries()
    {
        return new List<(string, string)>
        {
            ("Dashboard", "Summarize our financial health"),
            ("Dashboard", "What alerts should I be aware of?"),
            ("Operations", "What operational improvements can we make?")
        };
    }

    /// <summary>
    /// Gets common recommendation scenarios
    /// </summary>
    private static List<RecommendationScenario> GetRecommendationScenarios()
    {
        return new List<RecommendationScenario>
        {
            new()
            {
                Baseline = 100000m,
                ActualSpent = 107000m,
                Context = "Typical 7% overspend scenario",
                Metadata = new Dictionary<string, string>
                {
                    { "department", "operations" },
                    { "category", "general" }
                }
            },
            new()
            {
                Baseline = 50000m,
                ActualSpent = 48000m,
                Context = "Typical 4% underspend scenario",
                Metadata = new Dictionary<string, string>
                {
                    { "department", "administration" },
                    { "category", "general" }
                }
            },
            new()
            {
                Baseline = 75000m,
                ActualSpent = 75500m,
                Context = "Near-budget scenario",
                Metadata = new Dictionary<string, string>
                {
                    { "department", "maintenance" },
                    { "category", "general" }
                }
            }
        };
    }

    /// <summary>
    /// Represents a recommendation scenario for cache warming
    /// </summary>
    private class RecommendationScenario
    {
        public decimal Baseline { get; init; }
        public decimal ActualSpent { get; init; }
        public string Context { get; init; } = string.Empty;
        public Dictionary<string, string> Metadata { get; init; } = new();
    }
}
