using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for integrating with Bright Data API for web search functionality
    /// </summary>
    public class BrightDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BrightDataService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;

    public BrightDataService(HttpClient httpClient, ILogger<BrightDataService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Securely get API key from configuration (environment variables)
            _apiKey = _configuration["BrightData:ApiKey"] ?? throw new InvalidOperationException("BRIGHTDATA_API_KEY environment variable is not set");

            // Validate API key format (should be a JWT token or API key)
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Length < 20)
            {
                throw new InvalidOperationException("BRIGHTDATA_API_KEY appears to be invalid or too short");
            }

            // Determine mode: MCP (JSON-RPC) or REST (direct search API)
            var requestedMode = _configuration["BrightData:Mode"] ?? Environment.GetEnvironmentVariable("BRIGHTDATA_MODE") ?? "MCP";
            _mode = requestedMode.Equals("REST", StringComparison.OrdinalIgnoreCase) ? BrightDataMode.REST : BrightDataMode.MCP;

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WileyWidget/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            if (_mode == BrightDataMode.MCP)
            {
                _httpClient.BaseAddress = new Uri("https://mcp.brightdata.com/");
                _logger.LogInformation("BrightDataService initialized in MCP mode (JSON-RPC)");
            }
            else
            {
                _httpClient.BaseAddress = new Uri("https://api.brightdata.com/");
                _logger.LogInformation("BrightDataService initialized in REST mode (direct /search endpoint)");
            }
        }

        private enum BrightDataMode { MCP, REST }
        private readonly BrightDataMode _mode;

        /// <summary>
        /// Perform a web search using Bright Data API
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>Search results</returns>
        public async Task<BrightDataSearchResult> SearchAsync(string query, int maxResults = 10)
        {
            try
            {
                _logger.LogInformation("Bright Data search ({Mode}) for: {Query}", _mode, query);

                return _mode == BrightDataMode.MCP
                    ? await SearchViaMcpAsync(query, maxResults)
                    : await SearchViaRestAsync(query, maxResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing Bright Data search");
                return new BrightDataSearchResult { Error = ex.Message };
            }
        }

        private async Task<BrightDataSearchResult> SearchViaRestAsync(string query, int maxResults)
        {
            var payload = new
            {
                query,
                limit = maxResults,
                country = "US",
                language = "en"
            };
            var response = await _httpClient.PostAsJsonAsync("search", payload);
            if (!response.IsSuccessStatusCode)
            {
                return new BrightDataSearchResult { Error = $"REST API Error: {response.StatusCode}" };
            }
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                var resultsProp = root.TryGetProperty("results", out var resultsEl) && resultsEl.ValueKind == JsonValueKind.Array
                    ? resultsEl : default;
                var list = new List<BrightDataSearchItem>();
                if (resultsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in resultsProp.EnumerateArray())
                    {
                        list.Add(new BrightDataSearchItem
                        {
                            Title = item.TryGetProperty("title", out var t) ? t.GetString() : "No Title",
                            Url = item.TryGetProperty("url", out var u) ? u.GetString() : string.Empty,
                            Snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() : string.Empty,
                            DisplayUrl = item.TryGetProperty("display_url", out var d) ? d.GetString() : string.Empty,
                            Date = item.TryGetProperty("date", out var dt) && dt.TryGetDateTime(out var parsed) ? parsed : null
                        });
                    }
                }
                return new BrightDataSearchResult { Results = list.ToArray(), TotalResults = list.Count };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed parsing REST Bright Data response");
                return new BrightDataSearchResult { Error = "Parse failure" };
            }
        }

        private async Task<BrightDataSearchResult> SearchViaMcpAsync(string query, int maxResults)
        {
            var mcpRequest = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/call",
                @params = new
                {
                    name = "web_search",
                    arguments = new { query, max_results = maxResults }
                }
            };
            var response = await _httpClient.PostAsJsonAsync("", mcpRequest);
            if (!response.IsSuccessStatusCode)
                return new BrightDataSearchResult { Error = $"MCP HTTP Error: {response.StatusCode}" };

            var mcpResponse = await response.Content.ReadFromJsonAsync<McpResponse>();
            if (mcpResponse?.result?.content == null || mcpResponse.result.content.Count == 0)
            {
                if (mcpResponse?.error != null)
                    return new BrightDataSearchResult { Error = $"MCP Error: {mcpResponse.error.message}" };
                return new BrightDataSearchResult { Error = "Empty MCP response" };
            }

            var results = new List<BrightDataSearchItem>();
            foreach (var content in mcpResponse.result.content)
            {
                if (content.type != "text" || string.IsNullOrWhiteSpace(content.text)) continue;
                try
                {
                    var searchData = JsonSerializer.Deserialize<JsonElement>(content.text);
                    if (searchData.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in searchData.EnumerateArray())
                        {
                            results.Add(new BrightDataSearchItem
                            {
                                Title = item.TryGetProperty("title", out var title) ? title.GetString() : "No Title",
                                Url = item.TryGetProperty("url", out var url) ? url.GetString() : string.Empty,
                                Snippet = item.TryGetProperty("snippet", out var snippet) ? snippet.GetString() : string.Empty,
                                DisplayUrl = item.TryGetProperty("display_url", out var displayUrl) ? displayUrl.GetString() : string.Empty,
                                Date = item.TryGetProperty("date", out var date) && date.TryGetDateTime(out var dt) ? dt : null
                            });
                        }
                    }
                    else if (searchData.ValueKind == JsonValueKind.Object)
                    {
                        results.Add(new BrightDataSearchItem
                        {
                            Title = searchData.TryGetProperty("title", out var title) ? title.GetString() : "No Title",
                            Url = searchData.TryGetProperty("url", out var url) ? url.GetString() : string.Empty,
                            Snippet = searchData.TryGetProperty("snippet", out var snippet) ? snippet.GetString() : string.Empty,
                            DisplayUrl = searchData.TryGetProperty("display_url", out var displayUrl) ? displayUrl.GetString() : string.Empty,
                            Date = searchData.TryGetProperty("date", out var date) && date.TryGetDateTime(out var dt) ? dt : null
                        });
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse MCP content segment");
                }
            }
            return new BrightDataSearchResult { Results = results.ToArray(), TotalResults = results.Count };
        }

        /// <summary>
        /// Search for Syncfusion documentation specifically
        /// </summary>
        /// <param name="query">Search query related to Syncfusion</param>
        /// <returns>Syncfusion-specific search results</returns>
        public async Task<BrightDataSearchResult> SearchSyncfusionDocsAsync(string query)
        {
            var syncfusionQuery = $"site:help.syncfusion.com {query}";
            return await SearchAsync(syncfusionQuery, 15);
        }

        /// <summary>
        /// Search for WPF/.NET documentation
        /// </summary>
        /// <param name="query">Search query related to WPF/.NET</param>
        /// <returns>WPF/.NET specific search results</returns>
        public async Task<BrightDataSearchResult> SearchWpfDocsAsync(string query)
        {
            var wpfQuery = $"site:learn.microsoft.com/dotnet/desktop/wpf {query}";
            return await SearchAsync(wpfQuery, 15);
        }
    }

    /// <summary>
    /// Represents the result of a Bright Data search
    /// </summary>
    public class BrightDataSearchResult
    {
        public bool Success => string.IsNullOrEmpty(Error);
        public string Error { get; set; }
        public BrightDataSearchItem[] Results { get; set; } = Array.Empty<BrightDataSearchItem>();
        public int TotalResults { get; set; }
    }

    /// <summary>
    /// Represents a single search result item
    /// </summary>
    public class BrightDataSearchItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Snippet { get; set; }
        public string DisplayUrl { get; set; }
        public DateTime? Date { get; set; }
    }

    /// <summary>
    /// Represents the MCP JSON-RPC response
    /// </summary>
    public class McpResponse
    {
        public string jsonrpc { get; set; }
        public string id { get; set; }
        public McpResult result { get; set; }
        public McpError error { get; set; }
    }

    /// <summary>
    /// Represents the MCP result content
    /// </summary>
    public class McpResult
    {
        public List<McpContent> content { get; set; }
    }

    /// <summary>
    /// Represents MCP content
    /// </summary>
    public class McpContent
    {
        public string type { get; set; }
        public string text { get; set; }
    }

    /// <summary>
    /// Represents MCP error
    /// </summary>
    public class McpError
    {
        public int code { get; set; }
        public string message { get; set; }
    }
}
