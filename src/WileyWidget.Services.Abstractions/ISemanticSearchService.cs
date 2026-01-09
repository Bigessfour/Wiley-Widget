using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Provides semantic search capabilities using AI embeddings for natural language queries.
    /// Enables users to find data by meaning rather than exact keyword matches.
    /// </summary>
    public interface ISemanticSearchService
    {
        /// <summary>
        /// Performs semantic search on a collection of items using AI embeddings.
        /// </summary>
        /// <typeparam name="T">The type of items to search</typeparam>
        /// <param name="items">Collection of items to search</param>
        /// <param name="query">Natural language search query</param>
        /// <param name="textExtractor">Function to extract searchable text from each item</param>
        /// <param name="threshold">Similarity threshold (0.0 to 1.0, default 0.7)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of items ranked by relevance with similarity scores</returns>
        Task<List<SemanticSearchResult<T>>> SearchAsync<T>(
            IEnumerable<T> items,
            string query,
            Func<T, string> textExtractor,
            double threshold = 0.7,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Checks if semantic search is available (API key configured, model accessible).
        /// </summary>
        /// <returns>True if semantic search can be performed</returns>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a semantic search result with the matched item and similarity score.
    /// </summary>
    /// <typeparam name="T">Type of the matched item</typeparam>
    public class SemanticSearchResult<T> where T : class
    {
        /// <summary>
        /// The matched item from the search.
        /// </summary>
        public required T Item { get; init; }

        /// <summary>
        /// Similarity score between 0.0 and 1.0, where 1.0 is perfect match.
        /// </summary>
        public required double SimilarityScore { get; init; }

        /// <summary>
        /// Explanation of why this item was matched (optional).
        /// </summary>
        public string? MatchReason { get; init; }
    }
}
