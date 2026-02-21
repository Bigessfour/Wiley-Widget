using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics.Tensors;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implements semantic search using OpenAI-compatible embeddings (Grok API).
    /// Enables natural language search by comparing query embeddings with item embeddings.
    /// </summary>
    public class SemanticSearchService : ISemanticSearchService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SemanticSearchService> _logger;
        private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingService;

        public SemanticSearchService(
            IConfiguration configuration,
            ILogger<SemanticSearchService> logger,
            IEmbeddingGenerator<string, Embedding<float>>? embeddingService = null)
        {
            _configuration = configuration;
            _logger = logger;
            _embeddingService = embeddingService;
            if (_embeddingService != null)
            {
                _logger.LogInformation("[SEMANTIC_SEARCH] Embedding service initialized successfully");
            }
            else
            {
                _logger.LogWarning("[SEMANTIC_SEARCH] Embedding service not available - will use keyword search fallback");
            }
        }

        public async Task<List<SemanticSearchResult<T>>> SearchAsync<T>(
            IEnumerable<T> items,
            string query,
            Func<T, string> textExtractor,
            double threshold = 0.7,
            CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogDebug("[SEMANTIC_SEARCH] Empty query - returning all items");
                return items.Select(item => new SemanticSearchResult<T>
                {
                    Item = item,
                    SimilarityScore = 1.0,
                    MatchReason = "All items (no filter)"
                }).ToList();
            }

            if (_embeddingService == null)
            {
                _logger.LogDebug("[SEMANTIC_SEARCH] Service unavailable - falling back to keyword search");
                return await FallbackKeywordSearchAsync(items, query, textExtractor, cancellationToken);
            }

            try
            {
                // Generate embedding for search query (new API returns IList<Embedding<float>>)
                var queryEmbeddings = await _embeddingService.GenerateAsync(new[] { query }, options: null, cancellationToken);
                var queryEmbedding = queryEmbeddings[0].Vector;

                var results = new List<SemanticSearchResult<T>>();

                foreach (var item in items)
                {
                    var itemText = textExtractor(item);
                    if (string.IsNullOrWhiteSpace(itemText))
                        continue;

                    // Generate embedding for item text
                    var itemEmbeddings = await _embeddingService.GenerateAsync(new[] { itemText }, options: null, cancellationToken);
                    var itemEmbeddingVector = itemEmbeddings[0].Vector;

                    // Calculate cosine similarity
                    var similarity = CalculateCosineSimilarity(queryEmbedding, itemEmbeddingVector);

                    if (similarity >= threshold)
                    {
                        results.Add(new SemanticSearchResult<T>
                        {
                            Item = item,
                            SimilarityScore = similarity,
                            MatchReason = $"Semantic similarity: {similarity:P0}"
                        });
                    }
                }

                _logger.LogInformation("[SEMANTIC_SEARCH] Found {Count} results for query: {Query}", results.Count, query);
                return results.OrderByDescending(r => r.SimilarityScore).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SEMANTIC_SEARCH] Embedding search failed - falling back to keyword search");
                return await FallbackKeywordSearchAsync(items, query, textExtractor, cancellationToken);
            }
        }

        private Task<List<SemanticSearchResult<T>>> FallbackKeywordSearchAsync<T>(
            IEnumerable<T> items,
            string query,
            Func<T, string> textExtractor,
            CancellationToken cancellationToken) where T : class
        {
            var queryLower = query.ToLowerInvariant();
            var keywords = queryLower.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            var results = new List<SemanticSearchResult<T>>();

            foreach (var item in items)
            {
                var itemText = textExtractor(item)?.ToLowerInvariant() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(itemText))
                    continue;

                var matchedKeywords = keywords.Count(k => itemText.Contains(k, StringComparison.OrdinalIgnoreCase));
                if (matchedKeywords > 0)
                {
                    var score = (double)matchedKeywords / keywords.Length;
                    results.Add(new SemanticSearchResult<T>
                    {
                        Item = item,
                        SimilarityScore = score,
                        MatchReason = $"Keyword match: {matchedKeywords}/{keywords.Length} keywords"
                    });
                }
            }

            return Task.FromResult(results.OrderByDescending(r => r.SimilarityScore).ToList());
        }

        private double CalculateCosineSimilarity(ReadOnlyMemory<float> embedding1, ReadOnlyMemory<float> embedding2)
        {
            return TensorPrimitives.CosineSimilarity(embedding1.Span, embedding2.Span);
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            return _embeddingService != null;
        }
    }
}
