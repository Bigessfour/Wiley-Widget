using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Advanced search across multiple data grids and data sources.
    /// Supports full-text search, filtering, and result ranking.
    /// </summary>
    public class AdvancedSearchService
    {
        private readonly ILogger<AdvancedSearchService> _logger;
        private readonly Dictionary<string, SfDataGrid> _registeredGrids = new();

        public AdvancedSearchService(ILogger<AdvancedSearchService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registers a grid for searching.
        /// </summary>
        public void RegisterGrid(string gridName, SfDataGrid grid)
        {
            if (grid == null) return;

            _registeredGrids[gridName] = grid;
            _logger.LogDebug("Grid registered for search: {Grid}", gridName);
        }

        /// <summary>
        /// Performs a global search across all registered grids.
        /// </summary>
        public async Task<List<SearchResult>> SearchAsync(string query, SearchOptions? options = null)
        {
            options ??= new SearchOptions();
            var results = new List<SearchResult>();

            if (string.IsNullOrWhiteSpace(query))
                return results;

            await Task.Run(() =>
            {
                var searchTerms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => options.CaseSensitive ? t : t.ToLowerInvariant())
                    .ToList();

                foreach (var (gridName, grid) in _registeredGrids)
                {
                    SearchGrid(grid, gridName, searchTerms, options, results);
                }

                // Sort by relevance score
                results = results
                    .OrderByDescending(r => r.RelevanceScore)
                    .Take(options.MaxResults)
                    .ToList();
            });

            _logger.LogInformation("Search completed: '{Query}' -> {ResultCount} results", query, results.Count);
            return results;
        }

        /// <summary>
        /// Filters grids by a criteria.
        /// </summary>
        public List<object> FilterByProperty(string gridName, string propertyName, object value)
        {
            if (!_registeredGrids.TryGetValue(gridName, out var grid))
                return new List<object>();

            var result = new List<object>();
            if (grid.DataSource is System.Collections.IEnumerable dataSource)
            {
                foreach (var row in dataSource)
                {
                    var prop = row.GetType().GetProperty(propertyName);
                    if (prop?.GetValue(row)?.Equals(value) == true)
                    {
                        result.Add(row);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets search suggestions based on partial query.
        /// </summary>
        public List<string> GetSearchSuggestions(string partialQuery, int maxSuggestions = 10)
        {
            if (string.IsNullOrWhiteSpace(partialQuery))
                return new List<string>();

            var suggestions = new HashSet<string>();
            var query = partialQuery.ToLowerInvariant();

            foreach (var grid in _registeredGrids.Values)
            {
                if (grid.DataSource is System.Collections.IEnumerable dataSource)
                {
                    foreach (var row in dataSource)
                    {
                        foreach (var prop in row.GetType().GetProperties())
                        {
                            var value = prop.GetValue(row)?.ToString()?.ToLowerInvariant() ?? string.Empty;
                            if (value.Contains(query, StringComparison.OrdinalIgnoreCase) && value.Length <= 100)
                            {
                                suggestions.Add(value);
                                if (suggestions.Count >= maxSuggestions)
                                    return suggestions.Take(maxSuggestions).ToList();
                            }
                        }
                    }
                }
            }

            return suggestions.ToList();
        }

        private void SearchGrid(SfDataGrid grid, string gridName, List<string> searchTerms,
            SearchOptions options, List<SearchResult> results)
        {
            if (grid.DataSource is not System.Collections.IEnumerable dataSource)
                return;

            int rowIndex = 0;
            foreach (var row in dataSource)
            {
                var rowType = row.GetType();
                var properties = rowType.GetProperties();

                foreach (var prop in properties)
                {
                    var value = prop.GetValue(row)?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(value)) continue;

                    var searchValue = options.CaseSensitive ? value : value.ToLowerInvariant();
                    var matchScore = 0;
                    var matchCount = 0;

                    // Calculate relevance score
                    foreach (var term in searchTerms)
                    {
                        if (searchValue.Contains(term, StringComparison.OrdinalIgnoreCase))
                        {
                            matchCount++;

                            // Exact match scores higher
                            if (searchValue.Equals(term, StringComparison.OrdinalIgnoreCase))
                                matchScore += 10;
                            // Start match scores higher
                            else if (searchValue.StartsWith(term, StringComparison.OrdinalIgnoreCase))
                                matchScore += 5;
                            else
                                matchScore += 1;
                        }
                    }

                    // All terms must match if required
                    if (matchCount == searchTerms.Count || !options.RequireAllTerms)
                    {
                        results.Add(new SearchResult
                        {
                            GridName = gridName,
                            PropertyName = prop.Name,
                            Value = value,
                            RowIndex = rowIndex,
                            RelevanceScore = matchScore,
                            RowData = row
                        });
                    }
                }

                rowIndex++;
            }
        }
    }

    /// <summary>
    /// Search result item.
    /// </summary>
    public class SearchResult
    {
        public string GridName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int RowIndex { get; set; }
        public int RelevanceScore { get; set; }
        public object? RowData { get; set; }

        public override string ToString() => $"{GridName}.{PropertyName}: {Value}";
    }

    /// <summary>
    /// Search options.
    /// </summary>
    public class SearchOptions
    {
        public bool CaseSensitive { get; set; }
        public bool RequireAllTerms { get; set; } = true;
        public int MaxResults { get; set; } = 100;
    }
}
