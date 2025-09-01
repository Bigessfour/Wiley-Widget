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

            // Configure HTTP client for MCP server with security headers
            _httpClient.BaseAddress = new Uri("https://mcp.brightdata.com/");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WileyWidget/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

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
                _logger.LogInformation("Performing Bright Data search for: {Query}", query);

                // Use MCP HTTP endpoint for search
                var mcpRequest = new
                {
                    jsonrpc = "2.0",
                    id = Guid.NewGuid().ToString(),
                    method = "tools/call",
                    @params = new
                    {
                        name = "web_search",
                        arguments = new
                        {
                            query = query,
                            max_results = maxResults
                        }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync("", mcpRequest);

                if (response.IsSuccessStatusCode)
                {
                    // Parse MCP JSON-RPC response
                    var mcpResponse = await response.Content.ReadFromJsonAsync<McpResponse>();
                    if (mcpResponse?.result?.content != null && mcpResponse.result.content.Count > 0)
                    {
                        // Extract search results from MCP response content
                        var result = new BrightDataSearchResult();
                        var results = new List<BrightDataSearchItem>();

                        foreach (var content in mcpResponse.result.content)
                        {
                            if (content.type == "text" && !string.IsNullOrEmpty(content.text))
                            {
                                try
                                {
                                    // Parse the JSON content to extract search results
                                    var searchData = JsonSerializer.Deserialize<JsonElement>(content.text);

                                    // Check if it's an array of results
                                    if (searchData.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var item in searchData.EnumerateArray())
                                        {
                                            var searchItem = new BrightDataSearchItem
                                            {
                                                Title = item.TryGetProperty("title", out var title) ? title.GetString() : "No Title",
                                                Url = item.TryGetProperty("url", out var url) ? url.GetString() : "",
                                                Snippet = item.TryGetProperty("snippet", out var snippet) ? snippet.GetString() : "",
                                                DisplayUrl = item.TryGetProperty("display_url", out var displayUrl) ? displayUrl.GetString() : "",
                                                Date = item.TryGetProperty("date", out var date) && date.TryGetDateTime(out var dateTime) ? dateTime : null
                                            };
                                            results.Add(searchItem);
                                        }
                                    }
                                    else if (searchData.ValueKind == JsonValueKind.Object)
                                    {
                                        // Handle single result object
                                        var searchItem = new BrightDataSearchItem
                                        {
                                            Title = searchData.TryGetProperty("title", out var title) ? title.GetString() : "No Title",
                                            Url = searchData.TryGetProperty("url", out var url) ? url.GetString() : "",
                                            Snippet = searchData.TryGetProperty("snippet", out var snippet) ? snippet.GetString() : "",
                                            DisplayUrl = searchData.TryGetProperty("display_url", out var displayUrl) ? displayUrl.GetString() : "",
                                            Date = searchData.TryGetProperty("date", out var date) && date.TryGetDateTime(out var dateTime) ? dateTime : null
                                        };
                                        results.Add(searchItem);
                                    }
                                }
                                catch (JsonException ex)
                                {
                                    _logger.LogWarning(ex, "Failed to parse search result content");
                                    // Continue processing other content items
                                }
                            }
                        }

                        result.Results = results.ToArray();
                        result.TotalResults = results.Count;
                        _logger.LogInformation("Bright Data MCP search completed successfully with {Count} results", results.Count);
                        return result;
                    }
                    else if (mcpResponse?.error != null)
                    {
                        return new BrightDataSearchResult { Error = $"MCP Error: {mcpResponse.error.message}" };
                    }
                    else
                    {
                        return new BrightDataSearchResult { Error = "No results in MCP response" };
                    }
                }
                else
                {
                    _logger.LogError("Bright Data search failed with status: {StatusCode}", response.StatusCode);
                    return new BrightDataSearchResult { Error = $"API Error: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing Bright Data search");
                return new BrightDataSearchResult { Error = ex.Message };
            }
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
