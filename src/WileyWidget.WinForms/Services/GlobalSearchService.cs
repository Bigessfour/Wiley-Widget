using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Service for global search across accounts, budgets, and reports.
    /// Provides unified search capability from ribbon search box.
    /// </summary>
    public interface IGlobalSearchService
    {
        /// <summary>
        /// Search across all available modules (accounts, budgets, reports).
        /// </summary>
        /// <param name="query">Search query string</param>
        /// <returns>Aggregated search results</returns>
        Task<GlobalSearchResult> SearchAsync(string query);

        /// <summary>
        /// Event raised when search results are available.
        /// </summary>
        event EventHandler<GlobalSearchResultsEventArgs>? SearchResultsAvailable;
    }

    /// <summary>
    /// Default implementation of global search service.
    /// </summary>
    public class GlobalSearchService : IGlobalSearchService
    {
        private readonly IActivityLogService _activityLogService;
        private readonly ILogger<GlobalSearchService> _logger;

        public event EventHandler<GlobalSearchResultsEventArgs>? SearchResultsAvailable;

        public GlobalSearchService(IActivityLogService activityLogService, ILogger<GlobalSearchService> logger)
        {
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<GlobalSearchResult> SearchAsync(string query)
        {
            var normalizedQuery = query?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query))
            {
                return new GlobalSearchResult { Query = normalizedQuery, TotalResults = 0 };
            }

            try
            {
                _logger.LogInformation("Global search started: '{Query}'", normalizedQuery);

                var result = new GlobalSearchResult
                {
                    Query = normalizedQuery,
                    SearchedAt = DateTime.UtcNow,
                    TotalResults = 0
                };

                var activities = await _activityLogService.GetActivityEntriesAsync().ConfigureAwait(false);
                var activityMatches = activities
                    .Where(activity =>
                        activity.Activity.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        activity.Details.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        activity.Category.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        activity.Status.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(activity => activity.Timestamp)
                    .Take(50)
                    .Select(activity => new GlobalSearchMatch
                    {
                        Title = activity.Activity,
                        Category = "Activity",
                        Description = $"{activity.Category} • {activity.Status} • {activity.Timestamp:g} — {activity.Details}",
                        TargetPanelName = "Activity Log"
                    });

                var panelMatches = PanelRegistry.Panels
                    .Where(panel =>
                        panel.DisplayName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                        panel.DefaultGroup.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                    .Select(panel => new GlobalSearchMatch
                    {
                        Title = panel.DisplayName,
                        Category = "Panel",
                        Description = $"Open panel in {panel.DefaultGroup}",
                        TargetPanelName = panel.DisplayName
                    });

                result.Matches = activityMatches
                    .Concat(panelMatches)
                    .GroupBy(match => $"{match.Category}|{match.Title}", StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();

                result.TotalResults = result.Matches.Count;

                _logger.LogInformation("Global search completed for '{Query}': {ResultCount} results", normalizedQuery, result.TotalResults);

                // Raise event for UI updates
                SearchResultsAvailable?.Invoke(this, new GlobalSearchResultsEventArgs(result));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Global search failed for query '{Query}'", normalizedQuery);
                throw;
            }
        }
    }

    /// <summary>
    /// Represents a single global-search match.
    /// </summary>
    public class GlobalSearchMatch
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? TargetPanelName { get; set; }
    }

    /// <summary>
    /// Aggregated search result across multiple modules.
    /// </summary>
    public class GlobalSearchResult
    {
        public string? Query { get; set; }
        public int TotalResults { get; set; }
        public DateTime SearchedAt { get; set; }
        public System.Collections.Generic.List<GlobalSearchMatch> Matches { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for search results.
    /// </summary>
    public class GlobalSearchResultsEventArgs : EventArgs
    {
        public GlobalSearchResult Results { get; set; }

        public GlobalSearchResultsEventArgs(GlobalSearchResult results)
        {
            Results = results ?? throw new ArgumentNullException(nameof(results));
        }
    }
}
