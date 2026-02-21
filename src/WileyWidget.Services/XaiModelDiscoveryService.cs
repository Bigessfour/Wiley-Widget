using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services
{
    public class XaiModelDiscoveryService : IXaiModelDiscoveryService
    {
        private const string CacheKey = "XaiModelDiscoveryService.Models";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IGrokApiKeyProvider _apiKeyProvider;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<XaiModelDiscoveryService> _logger;

        public XaiModelDiscoveryService(IHttpClientFactory httpClientFactory, IGrokApiKeyProvider apiKeyProvider, IMemoryCache cache, IConfiguration configuration, ILogger<XaiModelDiscoveryService>? logger = null)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<XaiModelDiscoveryService>.Instance;
        }

        public async Task<IEnumerable<XaiModelDescriptor>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue<IEnumerable<XaiModelDescriptor>>(CacheKey, out var cached) && cached != null)
            {
                return cached;
            }

            var models = await FetchModelsAsync(cancellationToken).ConfigureAwait(false);

            var cacheSeconds = _configuration.GetValue<int>("XAI:ModelDiscovery:CacheSeconds", 86400);
            _cache.Set(CacheKey, models, TimeSpan.FromSeconds(Math.Max(60, cacheSeconds)));

            return models;
        }

        public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
        {
            _cache.Remove(CacheKey);
            await GetAvailableModelsAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<XaiModelDescriptor?> ChooseBestModelAsync(string? configuredModelOrAlias = null, CancellationToken cancellationToken = default)
        {
            var models = (await GetAvailableModelsAsync(cancellationToken).ConfigureAwait(false)).ToList();

            if (!string.IsNullOrWhiteSpace(configuredModelOrAlias))
            {
                var normalized = configuredModelOrAlias.Trim();

                // Exact id match
                var exact = models.FirstOrDefault(m => string.Equals(m.Id, normalized, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;

                // Alias match
                var aliasMatch = models
                    .Where(m => m.Aliases != null && m.Aliases.Any(a => string.Equals(a, normalized, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(m => m.CreatedUnix ?? 0)
                    .FirstOrDefault();
                if (aliasMatch != null) return aliasMatch;

                // Fallback: name-contained match
                var containedMatch = models
                    .Where(m => m.Aliases != null && m.Aliases.Any(a => a.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0) || m.Id.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(m => m.CreatedUnix ?? 0)
                    .FirstOrDefault();
                if (containedMatch != null) return containedMatch;
            }

            // No configured model: prefer grok-4.1 family, then grok-4-1, then grok-4, then grok-3
            var preferredFamilies = _configuration.GetValue<string>("XAI:ModelDiscovery:PreferredFamilies")
                                   ?? "grok-4.1,grok-4-1,grok-4-1-fast,grok-4,grok-4-latest,grok-3";
            var families = preferredFamilies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var fam in families)
            {
                var candidate = models
                    .Where(m => (m.Aliases != null && m.Aliases.Any(a => string.Equals(a, fam, StringComparison.OrdinalIgnoreCase)))
                                || (!string.IsNullOrEmpty(m.Id) && m.Id.IndexOf(fam, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderByDescending(m => m.CreatedUnix ?? 0)
                    .FirstOrDefault();
                if (candidate != null) return candidate;
            }

            // Last-resort: return newest model overall
            return models.OrderByDescending(m => m.CreatedUnix ?? 0).FirstOrDefault();
        }

        private async Task<IEnumerable<XaiModelDescriptor>> FetchModelsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var endpoint = _configuration["XAI:Endpoint"] ?? _configuration["Grok:Endpoint"] ?? "https://api.x.ai/v1";
                var normalizedBase = NormalizeBaseEndpoint(endpoint);

                var client = _httpClientFactory.CreateClient("GrokClient") ?? _httpClientFactory.CreateClient();
                try { client.BaseAddress = new Uri(normalizedBase, UriKind.Absolute); } catch { }

                var apiKey = _apiKeyProvider.ApiKey;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }
                else
                {
                    _logger.LogWarning("No API key provided by IGrokApiKeyProvider for model discovery");
                }

                // First attempt: language-models (full info)
                var lmResp = await client.GetAsync(new Uri("/v1/language-models", UriKind.Relative), cancellationToken).ConfigureAwait(false);
                if (lmResp.IsSuccessStatusCode)
                {
                    var body = await lmResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<XaiModelDescriptor>();
                            foreach (var item in arr.EnumerateArray())
                            {
                                var id = item.TryGetProperty("id", out var idp) ? idp.GetString() ?? string.Empty : string.Empty;
                                var aliases = item.TryGetProperty("aliases", out var ap) && ap.ValueKind == JsonValueKind.Array
                                    ? ap.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
                                    : Array.Empty<string>();

                                var input = item.TryGetProperty("input_modalities", out var im) && im.ValueKind == JsonValueKind.Array
                                    ? im.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
                                    : null;

                                var output = item.TryGetProperty("output_modalities", out var om) && om.ValueKind == JsonValueKind.Array
                                    ? om.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
                                    : null;

                                long? created = null;
                                if (item.TryGetProperty("created", out var cp) && cp.ValueKind == JsonValueKind.Number && cp.TryGetInt64(out var cval)) created = cval;

                                var version = item.TryGetProperty("version", out var vp) && vp.ValueKind == JsonValueKind.String ? vp.GetString() : null;

                                list.Add(new XaiModelDescriptor(id, aliases, input, output, created, version));
                            }
                            _logger.LogDebug("Fetched {Count} models from /v1/language-models", list.Count);
                            return list;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse /v1/language-models response");
                    }
                }

                // Fallback: /v1/models (minimal info)
                var mResp = await client.GetAsync(new Uri("/v1/models", UriKind.Relative), cancellationToken).ConfigureAwait(false);
                if (mResp.IsSuccessStatusCode)
                {
                    var body = await mResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<XaiModelDescriptor>();
                            foreach (var item in arr.EnumerateArray())
                            {
                                var id = item.TryGetProperty("id", out var idp) ? idp.GetString() ?? string.Empty : string.Empty;
                                long? created = null;
                                if (item.TryGetProperty("created", out var cp) && cp.ValueKind == JsonValueKind.Number && cp.TryGetInt64(out var cval)) created = cval;
                                list.Add(new XaiModelDescriptor(id, Array.Empty<string>(), null, null, created, null));
                            }
                            _logger.LogDebug("Fetched {Count} models from /v1/models (minimal)", list.Count);
                            return list;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse /v1/models response");
                    }
                }

                _logger.LogWarning("Model discovery did not return any models from x.ai endpoints");
                return Array.Empty<XaiModelDescriptor>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model discovery failed");
                return Array.Empty<XaiModelDescriptor>();
            }
        }

        private static string NormalizeBaseEndpoint(string endpoint)
        {
            var baseEndpointCandidate = (endpoint ?? string.Empty).TrimEnd('/');
            const string chatSuffix = "/chat/completions";
            if (baseEndpointCandidate.EndsWith(chatSuffix, StringComparison.OrdinalIgnoreCase))
            {
                baseEndpointCandidate = baseEndpointCandidate.Substring(0, baseEndpointCandidate.Length - chatSuffix.Length);
            }

            baseEndpointCandidate = baseEndpointCandidate.TrimEnd('/');
            return baseEndpointCandidate + '/';
        }
    }
}
