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
            if (string.IsNullOrWhiteSpace(query))
            {
                return new GlobalSearchResult { Query = query, TotalResults = 0 };
            }

            try
            {
                _logger.LogInformation("Global search started: '{Query}'", query);

                var result = new GlobalSearchResult
                {
                    Query = query,
                    SearchedAt = DateTime.UtcNow,
                    TotalResults = 0
                };

                // Search in activity log
                // Future: Add searches in accounts, budgets, reports repositories
                // For now, provide placeholder structure for expansion

                _logger.LogInformation("Global search completed for '{Query}': {ResultCount} results", query, result.TotalResults);

                // Raise event for UI updates
                SearchResultsAvailable?.Invoke(this, new GlobalSearchResultsEventArgs(result));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Global search failed for query '{Query}'", query);
                throw;
            }
        }
    }

    /// <summary>
    /// Aggregated search result across multiple modules.
    /// </summary>
    public class GlobalSearchResult
    {
        public string? Query { get; set; }
        public int TotalResults { get; set; }
        public DateTime SearchedAt { get; set; }

        // Future: Add properties for AccountSearchResults, BudgetSearchResults, ReportSearchResults
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
