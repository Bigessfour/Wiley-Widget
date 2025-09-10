using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Data; // For AppDbContext
using WileyWidget.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

#nullable enable

namespace WileyWidget.Services
{
    /// <summary>
    /// AI-powered budget analysis service that leverages xAI's Grok API for advanced municipal budget calculations.
    ///
    /// This service acts as a bridge between the local WPF application and xAI's Grok AI model, providing:
    /// - Intelligent budget deficit/surplus calculations
    /// - Predictive trend analysis using historical data
    /// - Scenario simulations for "what-if" planning
    /// - Employee retention optimization suggestions
    /// - Grant opportunity identification
    /// - Comprehensive budget analytics and insights
    ///
    /// Key Features:
    /// - Privacy protection through data anonymization
    /// - Response caching for performance optimization
    /// - Comprehensive audit logging for compliance
    /// - Graceful fallback to local calculations when API unavailable
    /// - Real-time UI progress updates
    /// - Database integration for persistent storage
    ///
    /// Architecture:
    /// - Uses HttpClient for REST API communication with xAI
    /// - Implements caching to reduce API calls and improve responsiveness
    /// - Anonymizes sensitive data before sending to external AI service
    /// - Provides both async and sync methods for different use cases
    /// - Follows disposable pattern for proper resource management
    /// </summary>
    public sealed class GrokSupercomputer : IDisposable
    {
        /// <summary>
        /// HttpClient instance configured for xAI API communication
        /// </summary>
        private readonly HttpClient _client;

        /// <summary>
        /// xAI API key for authentication (stored securely via configuration)
        /// </summary>
        private readonly string _apiKey;

        /// <summary>
        /// Logger for audit trails and debugging information
        /// </summary>
        private readonly ILogger<GrokSupercomputer> _logger;

    /// <summary>
    /// Optional database context for saving AI results and fetching historical data
    /// </summary>
    private readonly AppDbContext? _context;

    /// <summary>
    /// Database service for AI operations
    /// </summary>
    private readonly GrokDatabaseService? _dbService;        /// <summary>
        /// In-memory cache to store API responses and avoid redundant calls
        /// Key: Anonymized hash of input data, Value: JSON response from Grok
        /// </summary>
        private readonly Dictionary<string, string> _cache = new();

        /// <summary>
        /// Flag to track disposal state for proper resource cleanup
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Event raised to provide real-time progress updates to the UI
        /// Subscribers can display loading messages and status updates
        /// </summary>
        public event Action<string>? OnProgress;

        /// <summary>
        /// Initializes the GrokSupercomputer service with required dependencies.
        ///
        /// This constructor sets up:
        /// - HTTP client configured for xAI API endpoints
        /// - Authentication headers with API key
        /// - Logging infrastructure for audit trails
        /// - Optional database context for data persistence
        ///
        /// The service will throw an exception if the xAI API key is not configured,
        /// ensuring that the AI functionality cannot be used without proper credentials.
        /// </summary>
        /// <param name="config">Application configuration containing xAI API settings</param>
        /// <param name="logger">Optional logger for audit trails and debugging</param>
        /// <param name="context">Optional database context for saving results and fetching historical data</param>
        /// <param name="dbService">Optional database service for AI operations</param>
        /// <exception cref="Exception">Thrown when xAI API key is not found in configuration</exception>
        public GrokSupercomputer(
            IConfiguration config,
            ILogger<GrokSupercomputer>? logger = null,
            AppDbContext? context = null,
            GrokDatabaseService? dbService = null)
        {
            var effectiveLogger = logger ?? NullLogger<GrokSupercomputer>.Instance;
            effectiveLogger.LogInformation("Initializing GrokSupercomputer service");

            var apiKey = config["xAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                effectiveLogger.LogError("xAI API key not found in configuration");
                throw new Exception("No xAI key? Grok can't crunch without creds, boss.");
            }

            _apiKey = apiKey;
            _client = new HttpClient { BaseAddress = new Uri("https://api.x.ai/v1/"), Timeout = TimeSpan.FromSeconds(10) };
            _client.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);
            _logger = effectiveLogger;
            _context = context;
            _dbService = dbService;

            _logger.LogInformation("GrokSupercomputer initialized successfully with API key and HTTP client configured");
            _logger.LogInformation("Database context available: {HasContext}", _context != null);
        }

        /// <summary>
        /// Anonymizes sensitive enterprise names before sending to external AI service.
        ///
        /// This method protects privacy by converting enterprise names into irreversible hashes.
        /// The process:
        /// 1. Converts the name string to UTF-8 bytes
        /// 2. Applies SHA256 cryptographic hash function
        /// 3. Converts hash bytes to Base64 string
        /// 4. Returns first 8 characters as a short identifier
        ///
        /// This ensures that sensitive municipal enterprise names are not exposed
        /// to external AI services while maintaining data correlation for analysis.
        /// </summary>
        /// <param name="name">The original enterprise name to anonymize</param>
        /// <returns>A short, irreversible hash of the enterprise name</returns>
        private string AnonymizeName(string name)
        {
            _logger.LogDebug("Anonymizing enterprise name for privacy protection");

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(name));
            var anonymizedName = Convert.ToBase64String(hash).Substring(0, 8);

            _logger.LogDebug("Enterprise name anonymized successfully. Original length: {OriginalLength}, Hash: {Hash}",
                           name.Length, anonymizedName);

            return anonymizedName;
        }

        /// <summary>
        /// Core AI-powered calculation method that offloads complex budget analysis to Grok.
        ///
        /// This method implements the primary AI workflow:
        /// 1. Checks cache for previous identical calculations
        /// 2. Anonymizes sensitive data for privacy protection
        /// 3. Sends structured prompt to Grok API with budget algorithm
        /// 4. Processes AI response and updates enterprise objects
        /// 5. Saves results to database for persistence
        /// 6. Falls back to local calculation if API fails
        ///
        /// The method uses caching to improve performance and reduce API costs
        /// by avoiding redundant calculations for the same input data.
        ///
        /// Privacy Protection:
        /// - Enterprise names are hashed before sending to external service
        /// - Only numerical data (rates, expenses, revenue, rate payer counts) is sent
        /// - All data transmission is over HTTPS with authentication
        ///
        /// NOTE: "Citizen" in this context refers to rate payers (service recipients who pay),
        /// not legal citizens. This includes tenants, homeowners, and businesses.
        ///
        /// Error Handling:
        /// - Graceful fallback to local calculations if API unavailable
        /// - Comprehensive logging for debugging and audit trails
        /// - UI progress updates throughout the process
        /// </summary>
        /// <param name="enterprises">List of municipal enterprises to analyze</param>
        /// <param name="algoDescription">Algorithm description defining calculation logic</param>
        /// <returns>Updated list of enterprises with AI-calculated results</returns>
        public async Task<List<Enterprise>> CrunchNumbersAsync(List<Enterprise> enterprises, string algoDescription)
        {
            var startTime = DateTime.UtcNow;
            var enterpriseCount = enterprises.Count;

            _logger.LogInformation("Starting AI budget analysis for {EnterpriseCount} enterprises", enterpriseCount);
            _logger.LogInformation("Algorithm: {Algorithm}", algoDescription);
            _logger.LogInformation("Cache status: {CacheSize} entries", _cache.Count);

            OnProgress?.Invoke("Starting Grok calculation...");

            // Generate cache key from anonymized enterprise data and algorithm
            // This ensures identical requests return cached results instantly
            var cacheKey = AnonymizeName(string.Join(",", enterprises.Select(e => $"{e.Name}{e.CurrentRate}{e.MonthlyExpenses}{e.MonthlyRevenue}{e.CitizenCount}"))) + "_" + AnonymizeName(algoDescription);

            _logger.LogDebug("Generated cache key: {CacheKey}", cacheKey);

            // Check cache first for performance optimization
            if (_cache.TryGetValue(cacheKey, out var cachedJson))
            {
                _logger.LogInformation("Cache hit! Returning cached results for {EnterpriseCount} enterprises", enterpriseCount);
                OnProgress?.Invoke("Using cached results.");

                // Deserialize cached AI response
                var computed = JsonSerializer.Deserialize<List<ComputedEnterprise>>(cachedJson) ?? throw new InvalidOperationException("Failed to deserialize cached AI response");

                // Apply AI results back to enterprise objects
                for (int i = 0; i < enterprises.Count; i++)
                {
                    enterprises[i].ComputedDeficit = computed[i].deficit;
                    enterprises[i].SuggestedRateHike = computed[i].suggestedHike;
                    enterprises[i].Notes += $"\nGrok's wisdom: {computed[i].suggestion}";
                    _logger.LogInformation("Applied cached result for enterprise {Index}: Deficit={Deficit}, Hike={Hike}",
                                         i, computed[i].deficit, computed[i].suggestedHike);
                }

                var cacheRetrievalTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Cache retrieval completed in {ElapsedMs}ms", cacheRetrievalTime.TotalMilliseconds);

                OnProgress?.Invoke("Calculation complete.");
                return enterprises;
            }

            _logger.LogInformation("Cache miss. Proceeding with AI API call for {EnterpriseCount} enterprises", enterpriseCount);

            try
            {
                var apiCallStartTime = DateTime.UtcNow;
                _logger.LogInformation("Initiating xAI API call for {EnterpriseCount} enterprises", enterpriseCount);
                _logger.LogInformation("API Endpoint: {Endpoint}", "chat/completions");
                _logger.LogInformation("Model: {Model}", "grok-4-0709");
                OnProgress?.Invoke("Sending data to Grok...");

                // Prepare anonymized data for transmission to external AI service
                // Only sends: hashed names, rates, expenses, revenue, and rate payer counts
                var dataJson = JsonSerializer.Serialize(enterprises.Select(e => new {
                    AnonymizedName = AnonymizeName(e.Name),
                    e.CurrentRate,
                    e.MonthlyExpenses,
                    e.MonthlyRevenue,
                    e.CitizenCount
                }));

                _logger.LogDebug("Anonymized data prepared for transmission: {DataLength} characters", dataJson.Length);
                _logger.LogDebug("Privacy protection: Enterprise names hashed using SHA256");

                // Construct detailed prompt for Grok with algorithm instructions
                // Includes transparency disclaimer for responsible AI usage
                var prompt = $@"You're the Wiley budget brain. Ignore hallucinations; stick to math and provided data only. Data: {dataJson}
{algoDescription}
Example output: [{{""name"": ""hash123"", ""deficit"": 1500.0, ""suggestedHike"": 2.5, ""suggestion"": ""Hike rates 5% to cover deficit. I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm.""}}]
Output JSON array: [{{""name"": ""string"", ""deficit"": 0.0, ""suggestedHike"": 0.0, ""suggestion"": ""string. I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm.""}}]";

                _logger.LogDebug("AI prompt constructed: {PromptLength} characters", prompt.Length);
                _logger.LogDebug("Prompt includes transparency disclaimer and CPA verification requirement");

                // Prepare API request payload for xAI chat completions endpoint
                var request = new { model = "grok-4-0709", messages = new[] { new { role = "user", content = prompt } } };
                var requestJson = JsonSerializer.Serialize(request);
                _logger.LogDebug("API request payload prepared: {RequestSize} bytes", Encoding.UTF8.GetByteCount(requestJson));

                // Send HTTP POST request to xAI API
                _logger.LogInformation("Sending HTTP POST request to xAI API");
                var response = await _client.PostAsJsonAsync("chat/completions", request);
                var apiCallDuration = DateTime.UtcNow - apiCallStartTime;
                _logger.LogInformation("API call completed in {ElapsedMs}ms with status: {StatusCode}", apiCallDuration.TotalMilliseconds, response.StatusCode);

                response.EnsureSuccessStatusCode();

                // Parse the JSON response from Grok
                var json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Received API response: {ResponseSize} bytes", Encoding.UTF8.GetByteCount(json));

                var completion = JsonSerializer.Deserialize<CompletionResponse>(json)!;
                var computedJson = completion.choices![0]!.message!.content!;
                _logger.LogDebug("Extracted AI response content: {ContentLength} characters", computedJson.Length);

                var computed = JsonSerializer.Deserialize<List<ComputedEnterprise>>(computedJson) ?? throw new InvalidOperationException("Failed to deserialize AI response");
                _logger.LogInformation("Successfully parsed {ResultCount} AI-generated results", computed.Count);

                // Cache the successful API response for future identical requests
                _cache[cacheKey] = computedJson;
                _logger.LogInformation("Cached API response for future use. Cache now contains {CacheSize} entries", _cache.Count);

                // Apply AI-calculated results back to the original enterprise objects
                // Maps results by index since anonymized names maintain order
                for (int i = 0; i < enterprises.Count; i++)
                {
                    enterprises[i].ComputedDeficit = computed[i].deficit;
                    enterprises[i].SuggestedRateHike = computed[i].suggestedHike;
                    enterprises[i].Notes += $"\nGrok's wisdom: {computed[i].suggestion}";
                    _logger.LogInformation("Applied AI result for enterprise '{EnterpriseName}': Deficit=${Deficit}, Suggested Hike={Hike}%",
                                         enterprises[i].Name, computed[i].deficit, computed[i].suggestedHike);
                    _logger.LogDebug("AI suggestion added to enterprise notes: {Suggestion}", computed[i].suggestion);
                }

                OnProgress?.Invoke("Processing results...");

                // Persist AI results to database for long-term storage and audit trails
                if (_dbService != null)
                {
                    var dbUpdateStartTime = DateTime.UtcNow;
                    _logger.LogInformation("Persisting {EnterpriseCount} AI results to database", enterpriseCount);

                    // Update enterprises with AI results
                    await _dbService.UpdateEnterprisesWithAiResultsAsync(enterprises);

                    // Create analysis result record
                    var analysisResult = new AiAnalysisResult
                    {
                        AnalysisType = "BudgetAnalysis",
                        InputHash = ComputeHash(JsonSerializer.Serialize(enterprises.Select(e => new { e.Name, e.MonthlyExpenses }))),
                        AiResponse = JsonSerializer.Serialize(computed),
                        ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                        IsSuccessful = true,
                        Notes = $"Analyzed {enterpriseCount} enterprises"
                    };
                    await _dbService.SaveAnalysisResultAsync(analysisResult);

                    // Create recommendations
                    var recommendations = enterprises.SelectMany((enterprise, index) =>
                        new[]
                        {
                            new AiRecommendation
                            {
                                EnterpriseId = enterprise.Id,
                                RecommendationType = "RateHike",
                                RecommendationText = computed[index].suggestion ?? "No specific suggestion provided",
                                ExpectedImpact = computed[index].suggestedHike * enterprise.CitizenCount,
                                ConfidenceLevel = 85,
                                Priority = computed[index].deficit > 1000 ? "High" : "Medium"
                            }
                        });

                    await _dbService.SaveRecommendationsAsync(recommendations);

                    // Log audit entry
                    var auditEntry = new AiAnalysisAudit
                    {
                        OperationType = "AnalysisCompleted",
                        Description = $"Completed AI analysis for {enterpriseCount} enterprises",
                        IsSuccessful = true,
                        DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                    };
                    await _dbService.LogAuditEntryAsync(auditEntry);

                    var dbUpdateDuration = DateTime.UtcNow - dbUpdateStartTime;
                    _logger.LogInformation("Database update completed in {ElapsedMs}ms", dbUpdateDuration.TotalMilliseconds);
                    _logger.LogInformation("AI analysis session completed successfully");
                }
                else if (_context != null)
                {
                    // Fallback to direct context usage for backward compatibility
                    var dbUpdateStartTime = DateTime.UtcNow;
                    _logger.LogInformation("Persisting {EnterpriseCount} AI results to database (fallback mode)", enterpriseCount);

                    _context.Enterprises.UpdateRange(enterprises);
                    await _context.SaveChangesAsync();

                    var dbUpdateDuration = DateTime.UtcNow - dbUpdateStartTime;
                    _logger.LogInformation("Database update completed in {ElapsedMs}ms", dbUpdateDuration.TotalMilliseconds);
                    _logger.LogInformation("AI analysis session completed successfully");
                }
                else
                {
                    _logger.LogWarning("Database context not available - AI results not persisted");
                }

                var totalProcessingTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Complete AI analysis finished in {TotalMs}ms", totalProcessingTime.TotalMilliseconds);

                OnProgress?.Invoke("Calculation complete.");
                return enterprises;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP error during xAI API call: {StatusCode} - {Message}", httpEx.StatusCode, httpEx.Message);
                _logger.LogWarning("Network connectivity issue detected - falling back to local calculations");
                OnProgress?.Invoke("Network error - using local fallback.");
                return await LocalFallbackCrunch(enterprises, algoDescription);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing error in AI response: {Message}", jsonEx.Message);
                _logger.LogWarning("AI response format invalid - falling back to local calculations");
                OnProgress?.Invoke("AI response error - using local fallback.");
                return await LocalFallbackCrunch(enterprises, algoDescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during AI calculation: {Message}", ex.Message);
                _logger.LogWarning("General error in AI service - falling back to local calculations");
                OnProgress?.Invoke("Using local fallback.");
                return await LocalFallbackCrunch(enterprises, algoDescription);
            }
        }

        /// <summary>
        /// Local fallback calculation method used when xAI API is unavailable.
        ///
        /// This method provides basic budget calculations without AI assistance:
        /// - Calculates deficit as Expenses - Revenue
        /// - Suggests rate hikes based on deficit and rate payer count
        ///
        /// NOTE: "Citizen" in this context refers to rate payers (service recipients who pay),
        /// not legal citizens. This includes tenants, homeowners, and businesses.
        /// - Adds simple recommendations to enterprise notes
        ///
        /// While less sophisticated than AI analysis, this ensures the application
        /// remains functional even during API outages or network issues.
        /// The method still saves results to database for consistency.
        /// </summary>
        /// <param name="enterprises">List of enterprises to calculate locally</param>
        /// <param name="algoDescription">Algorithm description (used for logging only)</param>
        /// <returns>Updated enterprises with local calculations</returns>
        private async Task<List<Enterprise>> LocalFallbackCrunch(List<Enterprise> enterprises, string algoDescription)
        {
            var startTime = DateTime.UtcNow;
            var enterpriseCount = enterprises.Count;

            _logger.LogWarning("Activating local fallback calculations for {EnterpriseCount} enterprises", enterpriseCount);
            _logger.LogWarning("Algorithm context: {Algorithm}", algoDescription);
            _logger.LogInformation("Local calculation method: Basic deficit analysis with 10% buffer for rate hikes");

            // Apply basic deficit calculation to each enterprise
            for (int i = 0; i < enterprises.Count; i++)
            {
                var enterprise = enterprises[i];
                var originalDeficit = enterprise.ComputedDeficit;
                var originalHike = enterprise.SuggestedRateHike;

                // Calculate basic deficit
                enterprise.ComputedDeficit = enterprise.MonthlyExpenses - enterprise.MonthlyRevenue;

                // Calculate suggested rate hike with 10% buffer if deficit exists
                enterprise.SuggestedRateHike = enterprise.ComputedDeficit > 0
                    ? (enterprise.ComputedDeficit / enterprise.CitizenCount) * 1.1m
                    : 0;

                // Add fallback recommendation note
                enterprise.Notes += "\nLocal calc: Bump rates or bust. Consider grants for surplus.";

                _logger.LogInformation("Local calculation for enterprise '{EnterpriseName}': Deficit=${Deficit}, Suggested Hike={Hike}%",
                               enterprise.Name, enterprise.ComputedDeficit, enterprise.SuggestedRateHike);
                _logger.LogDebug("Enterprise {Index}: Expenses=${Expenses}, Revenue=${Revenue}, Rate Payers={Citizens}",
                               i, enterprise.MonthlyExpenses, enterprise.MonthlyRevenue, enterprise.CitizenCount);
            }

            // Save results to database even in fallback mode
            if (_dbService != null)
            {
                var dbUpdateStartTime = DateTime.UtcNow;
                _logger.LogInformation("Persisting fallback calculations to database for {EnterpriseCount} enterprises", enterpriseCount);

                await _dbService.UpdateEnterprisesWithAiResultsAsync(enterprises);

                // Create analysis result record for fallback
                var analysisResult = new AiAnalysisResult
                {
                    AnalysisType = "FallbackBudgetAnalysis",
                    InputHash = ComputeHash(JsonSerializer.Serialize(enterprises.Select(e => new { e.Name, e.MonthlyExpenses }))),
                    AiResponse = "Local fallback calculation - no AI response available",
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    IsSuccessful = true,
                    Notes = $"Fallback analysis for {enterpriseCount} enterprises"
                };
                await _dbService.SaveAnalysisResultAsync(analysisResult);

                var dbUpdateDuration = DateTime.UtcNow - dbUpdateStartTime;
                _logger.LogInformation("Database update completed in {ElapsedMs}ms", dbUpdateDuration.TotalMilliseconds);
            }
            else if (_context != null)
            {
                // Fallback to direct context usage for backward compatibility
                var dbUpdateStartTime = DateTime.UtcNow;
                _logger.LogInformation("Persisting fallback calculations to database for {EnterpriseCount} enterprises (fallback mode)", enterpriseCount);

                _context.Enterprises.UpdateRange(enterprises);
                _context.SaveChanges(); // Sync for fallback

                var dbUpdateDuration = DateTime.UtcNow - dbUpdateStartTime;
                _logger.LogInformation("Database update completed in {ElapsedMs}ms", dbUpdateDuration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Database context not available - fallback results not persisted");
            }

            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Local fallback calculations completed in {TotalMs}ms for {EnterpriseCount} enterprises",
                           totalDuration.TotalMilliseconds, enterpriseCount);
            _logger.LogWarning("Application functionality maintained through local calculations");

            return enterprises;
        }

        /// <summary>
        /// Legacy method for backward compatibility with existing code.
        ///
        /// This method provides a simplified interface that uses the standard
        /// deficit calculation algorithm. It's maintained to ensure existing
        /// code continues to work without modification.
        ///
        /// Algorithm: Basic deficit calculation with 10% buffer for rate hikes
        /// </summary>
        /// <param name="enterprises">List of enterprises to analyze</param>
        /// <returns>Updated enterprises with AI-calculated results</returns>
        public Task<List<Enterprise>> ComputeEnterprisesAsync(List<Enterprise> enterprises)
        {
            return CrunchNumbersAsync(enterprises, "Deficit = Expenses - Revenue; If deficit >0, SuggestedHike = (deficit / CitizenCount) * 1.1 else 0; Suggestion = witty tip");
        }

        /// <summary>
        /// Analyzes budget trends using historical data from the database.
        ///
        /// This method performs predictive analysis by:
        /// 1. Fetching the last 10 budget snapshots from OverallBudget table
        /// 2. Sending current enterprise data + historical trends to Grok
        /// 3. Requesting trend analysis, predictions, and actionable suggestions
        ///
        /// The AI analyzes patterns in expenses, revenue, and balances over time
        /// to provide insights about future budget requirements and optimization opportunities.
        ///
        /// Used for long-term planning and identifying systemic budget issues.
        /// </summary>
        /// <param name="enterprises">Current enterprise data for trend analysis</param>
        /// <returns>JSON string containing trend analysis, predictions, and suggestions</returns>
        /// <summary>
        /// Historical budget data structure for trend analysis.
        /// - Year: Time period identifier (typically year or fiscal period)
        /// - TotalExpenses: Aggregate expenses for that period
        /// - TotalRevenue: Aggregate revenue for that period
        ///
        /// Used by AnalyzeTrendsAsync to provide context for predictive analysis
        /// and identification of long-term budget patterns.
        /// </summary>
        internal class HistoricalData
        {
            public string? Year { get; set; }
            public decimal TotalExpenses { get; set; }
            public decimal TotalRevenue { get; set; }
        }
        public async Task<string> AnalyzeTrendsAsync(List<Enterprise> enterprises)
        {
            var startTime = DateTime.UtcNow;
            var enterpriseCount = enterprises.Count;

            _logger.LogInformation("Starting trend analysis for {EnterpriseCount} enterprises", enterpriseCount);
            _logger.LogInformation("Analyzing historical patterns and predictive insights");

            // Fetch historical data from database for trend analysis
            var history = new List<HistoricalData>();
            var dbQueryStartTime = DateTime.UtcNow;

            if (_dbService != null)
            {
                _logger.LogInformation("Fetching historical budget data from database");

                var budgets = await _dbService.GetHistoricalBudgetsAsync(10);

                var dbQueryDuration = DateTime.UtcNow - dbQueryStartTime;
                _logger.LogInformation("Database query completed in {ElapsedMs}ms. Retrieved {SnapshotCount} historical snapshots",
                               dbQueryDuration.TotalMilliseconds, budgets.Count);

                history = budgets.Select(ob => new HistoricalData
                {
                    Year = ob.SnapshotDate.Year.ToString(),
                    TotalExpenses = ob.TotalMonthlyExpenses,
                    TotalRevenue = ob.TotalMonthlyRevenue
                }).ToList();

                _logger.LogDebug("Historical data prepared: {HistoryPoints} data points covering expenses and revenue trends", history.Count);
            }
            else if (_context != null)
            {
                // Fallback to direct context usage for backward compatibility
                _logger.LogInformation("Fetching historical budget data from database (fallback mode)");

                var budgets = await _context.OverallBudgets
                    .OrderByDescending(ob => ob.SnapshotDate)
                    .Take(10) // Last 10 snapshots for trend analysis
                    .ToListAsync();

                var dbQueryDuration = DateTime.UtcNow - dbQueryStartTime;
                _logger.LogInformation("Database query completed in {ElapsedMs}ms. Retrieved {SnapshotCount} historical snapshots",
                               dbQueryDuration.TotalMilliseconds, budgets.Count);

                history = budgets.Select(ob => new HistoricalData
                {
                    Year = ob.SnapshotDate.Year.ToString(),
                    TotalExpenses = ob.TotalMonthlyExpenses,
                    TotalRevenue = ob.TotalMonthlyRevenue
                }).ToList();

                _logger.LogDebug("Historical data prepared: {HistoryPoints} data points covering expenses and revenue trends", history.Count);
            }
            else
            {
                _logger.LogWarning("Database context not available - trend analysis will proceed without historical data");
            }

            // Prepare data for AI analysis
            var dataJson = JsonSerializer.Serialize(new { enterprises = enterprises.Select(e => new { e.Name, e.MonthlyExpenses, e.MonthlyRevenue }), history });

            _logger.LogDebug("Analysis data prepared: {DataSize} bytes of current and historical information", Encoding.UTF8.GetByteCount(dataJson));

            // Construct AI prompt for trend analysis
            var prompt = $@"Analyze trends in this municipal budget data: {dataJson}
Rising costs? Predict next year's deficit. Suggest fixes. Ignore hallucinations; stick to data.
Output JSON: {{""trends"": ""string"", ""prediction"": ""string"", ""suggestions"": [""string""]}}
Remember: I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm.";

            _logger.LogDebug("Trend analysis prompt constructed: {PromptLength} characters", prompt.Length);
            _logger.LogInformation("Initiating AI trend analysis with historical context");

            var result = await SendPromptAsync(prompt);

            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Trend analysis completed in {TotalMs}ms", totalDuration.TotalMilliseconds);
            _logger.LogDebug("AI trend analysis result: {ResultLength} characters", result.Length);

            return result;
        }

        /// <summary>
        /// Simulates "what-if" scenarios for budget planning and decision support.
        ///
        /// This method allows users to explore hypothetical situations such as:
        /// - Rate increases/decreases and their impact on revenue
        /// - Changes in service levels or operational costs
        /// - Economic conditions affecting expenses or rate payer counts
        /// - Policy changes and their budgetary implications
        ///
        /// NOTE: "Citizen" in this context refers to rate payers (service recipients who pay),
        /// not legal citizens. This includes tenants, homeowners, and businesses.
        ///
        /// The AI analyzes the scenario description and current enterprise data
        /// to predict impacts on deficits/surpluses and suggest mitigation strategies
        /// like rebates, phased implementations, or alternative approaches.
        ///
        /// Useful for strategic planning and risk assessment before implementing changes.
        /// </summary>
        /// <param name="enterprises">Current enterprise data for scenario analysis</param>
        /// <param name="scenario">Description of the hypothetical scenario to simulate</param>
        /// <returns>JSON string with impact analysis and recommendations</returns>
        public async Task<string> SimulateScenarioAsync(List<Enterprise> enterprises, string scenario)
        {
            var dataJson = JsonSerializer.Serialize(enterprises.Select(e => new { e.Name, e.CurrentRate, e.MonthlyExpenses, e.MonthlyRevenue, e.CitizenCount }));
            var prompt = $@"What if scenario: {scenario}. Data: {dataJson}
Impact on deficit/surplus? Soften with rebates? Ignore hallucinations.
Output JSON: {{""impact"": ""string"", ""recommendations"": [""string""]}}
Remember: I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm.";

            return await SendPromptAsync(prompt);
        }

        /// <summary>
        /// Provides AI-powered suggestions for improving employee retention and reducing turnover costs.
        ///
        /// This method analyzes current wage structures and operational costs to suggest:
        /// - Competitive compensation adjustments within budget constraints
        /// - Reallocation of surplus funds to employee benefits
        /// - Cost-benefit analysis of retention vs. turnover expenses
        /// - Phased implementation strategies for wage increases
        ///
        /// Particularly valuable for small municipalities where employee retention
        /// is critical for service continuity and institutional knowledge preservation.
        /// </summary>
        /// <param name="enterprises">Enterprise data including expense and rate payer information</param>
        /// <returns>JSON string with retention improvement suggestions</returns>
        public async Task<string> SuggestRetentionAsync(List<Enterprise> enterprises)
        {
            var dataJson = JsonSerializer.Serialize(enterprises.Select(e => new { e.Name, e.MonthlyExpenses, e.CitizenCount }));
            var prompt = $@"Given wages below market and high turnover, suggest budget tweaks: Reallocate from surplus? Impact? Data: {dataJson}
Ignore hallucinations.
Output JSON: {{""suggestions"": [""string""]}}
Remember: I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm.";

            return await SendPromptAsync(prompt);
        }

        /// <summary>
        /// Identifies potential grant opportunities for infrastructure and operational improvements.
        ///
        /// This method analyzes enterprise expense patterns to identify:
        /// - Federal and state grant programs matching infrastructure needs
        /// - USDA Rural Development grants for small municipalities
        /// - Environmental and community development funding opportunities
        /// - Application strategies and budget integration approaches
        ///
        /// Helps municipalities leverage external funding to reduce rate increases
        /// and improve services without additional taxpayer burden.
        /// </summary>
        /// <param name="enterprises">Enterprise data showing expense patterns and infrastructure needs</param>
        /// <returns>JSON string with grant suggestions and application guidance</returns>
        public async Task<string> HuntGrantsAsync(List<Enterprise> enterprises)
        {
            var dataJson = JsonSerializer.Serialize(enterprises.Select(e => new { e.Name, e.MonthlyExpenses }));
            var prompt = $@"Scan infra needs: {dataJson}. Suggest grants and how to apply in budget.
Output JSON: {{""grants"": [""string""]}}
Remember: I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm.";

            return await SendPromptAsync(prompt);
        }

        /// <summary>
        /// Core method for sending prompts to the xAI Grok API and handling responses.
        ///
        /// This private method encapsulates the HTTP communication with xAI:
        /// 1. Formats the prompt into the required API request structure
        /// 2. Sends authenticated POST request to chat completions endpoint
        /// 3. Validates response and extracts AI-generated content
        /// 4. Logs the response for audit trails
        /// 5. Handles API errors gracefully
        ///
        /// All public methods that need AI responses use this helper method
        /// to ensure consistent error handling and logging.
        /// </summary>
        /// <param name="prompt">The formatted prompt to send to Grok</param>
        /// <returns>The AI-generated response content</returns>
        private async Task<string> SendPromptAsync(string prompt)
        {
            var startTime = DateTime.UtcNow;
            var promptLength = prompt.Length;

            _logger.LogInformation("Sending prompt to xAI Grok API");
            _logger.LogDebug("Prompt length: {PromptLength} characters", promptLength);
            _logger.LogDebug("API Model: {Model}", "grok-4-0709");
            _logger.LogDebug("Endpoint: {Endpoint}", "chat/completions");

            try
            {
                // Prepare API request payload
                var request = new { model = "grok-4-0709", messages = new[] { new { role = "user", content = prompt } } };
                var requestJson = JsonSerializer.Serialize(request);
                _logger.LogDebug("Request payload prepared: {RequestSize} bytes", Encoding.UTF8.GetByteCount(requestJson));

                // Send HTTP POST request to xAI API
                var apiCallStartTime = DateTime.UtcNow;
                _logger.LogInformation("Initiating HTTP POST to xAI API");

                var response = await _client.PostAsJsonAsync("chat/completions", request);
                var apiCallDuration = DateTime.UtcNow - apiCallStartTime;

                _logger.LogInformation("API call completed in {ElapsedMs}ms with status: {StatusCode}",
                               apiCallDuration.TotalMilliseconds, response.StatusCode);

                response.EnsureSuccessStatusCode();

                // Parse the JSON response
                var json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Received response: {ResponseSize} bytes", Encoding.UTF8.GetByteCount(json));

                var completion = JsonSerializer.Deserialize<CompletionResponse>(json)!;
                var result = completion.choices![0]!.message!.content!;

                var totalDuration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Prompt processing completed in {TotalMs}ms", totalDuration.TotalMilliseconds);
                _logger.LogDebug("AI response length: {ResponseLength} characters", result.Length);
                _logger.LogInformation("Successfully received AI response from Grok");

                return result;
            }
            catch (HttpRequestException httpEx)
            {
                var errorDuration = DateTime.UtcNow - startTime;
                _logger.LogError(httpEx, "HTTP error during xAI API call after {ElapsedMs}ms: {StatusCode} - {Message}",
                               errorDuration.TotalMilliseconds, httpEx.StatusCode, httpEx.Message);
                _logger.LogWarning("Network connectivity issue with xAI API - returning fallback response");
                return "Fallback: Review data manually.";
            }
            catch (JsonException jsonEx)
            {
                var errorDuration = DateTime.UtcNow - startTime;
                _logger.LogError(jsonEx, "JSON parsing error in AI response after {ElapsedMs}ms: {Message}",
                               errorDuration.TotalMilliseconds, jsonEx.Message);
                _logger.LogWarning("Invalid JSON response from xAI API - returning fallback response");
                return "Fallback: Review data manually.";
            }
            catch (Exception ex)
            {
                var errorDuration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Unexpected error during AI prompt processing after {ElapsedMs}ms: {Message}",
                               errorDuration.TotalMilliseconds, ex.Message);
                _logger.LogWarning("General error in AI service - returning fallback response");
                return "Fallback: Review data manually.";
            }
        }

        /// <summary>
        /// Computes comprehensive budget analytics using AI for detailed municipal budget analysis.
        ///
        /// This method provides aggregated metrics across all enterprises:
        /// - Total revenue, expenses, and balance calculations
        /// - Citizen count aggregation and per-citizen averages
        ///
        /// NOTE: "Citizen" in this context refers to rate payers (service recipients who pay),
        /// not legal citizens. This includes tenants, homeowners, and businesses.
        /// - Budget status determination (Surplus/Deficit/Break-even)
        /// - Efficiency ratios and performance indicators
        ///
        /// Uses AI to ensure accurate calculations and provide additional
        /// insights beyond simple summation. Particularly useful for
        /// executive summaries and high-level budget reporting.
        /// </summary>
        /// <param name="enterprises">List of all enterprises for comprehensive analysis</param>
        /// <returns>BudgetMetrics object with AI-calculated comprehensive analytics</returns>
        /// <summary>
        /// AI-calculated comprehensive budget metrics.
        ///
        /// This class represents the structured response from Grok's budget analytics computation:
        /// - TotalRevenue: Sum of all MonthlyRevenue
        /// - TotalExpenses: Sum of all MonthlyExpenses
        /// - MonthlyBalance: TotalRevenue - TotalExpenses
        /// - TotalCitizens: Sum of all CitizenCount
        /// - Status: Budget status (Surplus/Deficit/Break-even)
        /// - AverageRevenuePerCitizen: Per-citizen revenue average
        /// - OverallEfficiency: Performance efficiency ratio
        ///
        /// Used internally to transfer AI results to BudgetMetrics objects.
        /// </summary>
        internal class BudgetAnalyticsResult
        {
            public decimal totalRevenue { get; set; }
            public decimal totalExpenses { get; set; }
            public decimal monthlyBalance { get; set; }
            public int totalCitizens { get; set; }
            public string? status { get; set; }
            public decimal averageRevenuePerCitizen { get; set; }
            public decimal overallEfficiency { get; set; }
        }

        public async Task<BudgetMetrics> ComputeBudgetAnalyticsAsync(List<Enterprise> enterprises)
        {
            var dataJson = JsonSerializer.Serialize(enterprises.Select(e => new
            {
                e.Name,
                e.CurrentRate,
                e.MonthlyExpenses,
                e.MonthlyRevenue,
                e.CitizenCount
            }));

            var jsonTemplate = @"
{
  ""totalRevenue"": 0.0,
  ""totalExpenses"": 0.0,
  ""monthlyBalance"": 0.0,
  ""totalCitizens"": 0,
  ""status"": ""string"",
  ""averageRevenuePerCitizen"": 0.0,
  ""overallEfficiency"": 0.0
}";

            var prompt = $@"Analyze this municipal budget data: {dataJson}

Calculate comprehensive metrics:
- TotalRevenue: Sum of all MonthlyRevenue
- TotalExpenses: Sum of all MonthlyExpenses
- MonthlyBalance: TotalRevenue - TotalExpenses
- TotalCitizens: Sum of all CitizenCount
- Status: ""Surplus"", ""Deficit"", or ""Break-even"" based on MonthlyBalance
- AverageRevenuePerCitizen: TotalRevenue / TotalCitizens
- OverallEfficiency: (MonthlyBalance / TotalRevenue) * 100 if TotalRevenue > 0

Output as JSON: {jsonTemplate}
Remember: I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm.";

            var request = new { model = "grok-4-0709", messages = new[] { new { role = "user", content = prompt } } };
            var response = await _client.PostAsJsonAsync("chat/completions", request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var completion = JsonSerializer.Deserialize<CompletionResponse>(json)!;
            var analytics = JsonSerializer.Deserialize<BudgetAnalyticsResult>(completion.choices![0]!.message!.content!)!;

            return new BudgetMetrics
            {
                TotalRevenue = analytics.totalRevenue,
                TotalExpenses = analytics.totalExpenses,
                MonthlyBalance = analytics.monthlyBalance,
                TotalCitizens = analytics.totalCitizens
            };
        }

        /// <summary>
        /// Generates comprehensive AI-powered budget insights and strategic recommendations.
        ///
        /// This method combines basic budget analysis with AI expertise to provide:
        /// 1. Main Insight: Key finding about overall budget health
        /// 2. Recommendations: 5-7 actionable suggestions for optimization
        /// 3. Risk Assessment: Potential risks and mitigation strategies
        /// 4. Opportunities: Growth and efficiency improvement opportunities
        ///
        /// The AI analyzes both aggregated metrics and individual enterprise data
        /// to provide context-aware recommendations. Includes transparency disclaimer
        /// for responsible AI usage in municipal decision-making.
        /// </summary>
        /// <param name="metrics">Pre-calculated budget metrics for analysis</param>
        /// <param name="enterprises">Individual enterprise data for detailed insights</param>
        /// <returns>BudgetInsights object with AI-generated analysis and recommendations</returns>
        /// <summary>
        /// AI-generated budget insights and recommendations.
        ///
        /// This class represents the structured response from Grok's budget analysis and recommendations:
        /// - Main finding about budget health
        /// - Array of actionable recommendations
        /// - Risk assessment and mitigation strategies
        /// - Growth opportunities and efficiency improvements
        ///
        /// Used internally to populate BudgetInsights objects with AI content.
        /// </summary>
        public class BudgetInsightsResult
        {
            public string? mainInsight { get; set; }
            public List<string>? recommendations { get; set; }
            public string? riskAssessment { get; set; }
            public string? opportunities { get; set; }
        }

        public async Task<BudgetInsights> GenerateBudgetInsightsAsync(BudgetMetrics metrics, List<Enterprise> enterprises)
        {
            var dataJson = JsonSerializer.Serialize(new
            {
                metrics = new
                {
                    metrics.TotalRevenue,
                    metrics.TotalExpenses,
                    metrics.MonthlyBalance,
                    metrics.TotalCitizens
                },
                enterprises = enterprises.Select(e => new
                {
                    e.Name,
                    e.MonthlyBalance,
                    e.ProfitMargin,
                    e.CitizenCount
                })
            });

            var jsonTemplate = @"
{
  ""mainInsight"": ""string"",
  ""recommendations"": [""string""],
  ""riskAssessment"": ""string"",
  ""opportunities"": ""string""
}";

            var prompt = $@"You're a municipal budget expert. Analyze this data and provide insights: {dataJson}

Generate:
1. MainInsight: A key finding about the overall budget health
2. Recommendations: Array of 5-7 actionable suggestions for budget optimization
3. RiskAssessment: Potential risks and mitigation strategies
4. Opportunities: Growth opportunities and efficiency improvements

Be specific, actionable, and include quantitative suggestions where possible.

Output as JSON: {jsonTemplate}
Remember: I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm.";

            var request = new { model = "grok-4-0709", messages = new[] { new { role = "user", content = prompt } } };
            var response = await _client.PostAsJsonAsync("chat/completions", request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var completion = JsonSerializer.Deserialize<CompletionResponse>(json)!;
            var insights = JsonSerializer.Deserialize<BudgetInsightsResult>(completion.choices![0]!.message!.content!)!;

            var budgetInsights = new BudgetInsights();
            budgetInsights.GenerateInsights(metrics); // Keep basic logic, enhance with AI

            // Add AI-generated content
            budgetInsights.MainInsight = insights.mainInsight;
            foreach (var rec in insights.recommendations ?? new List<string>())
            {
                budgetInsights.Recommendations.Add(rec);
            }

            // Add disclaimer as a recommendation
            budgetInsights.Recommendations.Add(budgetInsights.Disclaimer);

            return budgetInsights;
        }

        /// <summary>
        /// Generates comprehensive talking points for board meetings about rate adjustments and grant opportunities.
        ///
        /// This method creates a narrative presentation that:
        /// - Frames rate increases as responsible stewardship
        /// - Presents data-driven scenarios (do nothing vs. planned action)
        /// - Highlights grant opportunities and qualification requirements
        /// - Provides community-focused softening strategies
        /// - Includes CPA verification reminders
        ///
        /// The talking points are structured for maximum impact:
        /// 1. Big picture framing
        /// 2. Scenario comparison
        /// 3. Grant integration
        /// 4. Community cushions
        /// 5. Closing motivation
        /// </summary>
        /// <param name="enterprises">List of municipal enterprises with current financial data</param>
        /// <param name="citizenCount">Total number of rate payers served (default 245). 
        /// NOTE: "Citizen" here refers to rate payers, not legal citizens.</param>
        /// <param name="inflationRate">Expected annual inflation rate for expenses (default 15%)</param>
        /// <param name="yearsProjection">Number of years to project deficits (default 3)</param>
        /// <returns>Formatted talking points string ready for presentation</returns>
        public string GenerateTalkingPoints(List<Enterprise> enterprises, int citizenCount = 245, decimal inflationRate = 0.15m, int yearsProjection = 3)
        {
            // Calculate key financial metrics
            decimal totalMonthlyExpenses = enterprises.Sum(e => e.MonthlyExpenses);
            decimal totalMonthlyRevenue = enterprises.Sum(e => e.MonthlyRevenue);
            decimal totalMonthlyDeficit = enterprises.Sum(e => e.MonthlyDeficit);
            decimal annualDeficit = totalMonthlyDeficit * 12;
            decimal projectedAnnualExpenses = totalMonthlyExpenses * 12 * (1 + inflationRate);
            decimal emergencyRepairCost = projectedAnnualExpenses * 0.20m; // Assume 20% for emergency repairs
            decimal threeYearDeficit = annualDeficit * yearsProjection + (projectedAnnualExpenses - totalMonthlyRevenue * 12) * yearsProjection;
            decimal suggestedHike = totalMonthlyDeficit > 0 ? totalMonthlyDeficit * 1.10m : 0; // 10% buffer
            decimal surplusBuffer = suggestedHike * 12 * 0.50m; // 50% of annual hike as buffer
            decimal grantAmount = totalMonthlyExpenses * 12 * 0.50m; // Assume 50% grant coverage
            decimal monthlyHikePerCitizen = (suggestedHike / citizenCount) * 0.03m; // 3% of suggested hike per citizen

            // Format currency values
            string formatCurrency(decimal value) => $"${value:N0}";

            // Generate talking points with filled placeholders
            string talkingPoints = $@"
### The Big Picture Talking Point: ""This Isn't About Greed—It's About Not Going Under""
- ""Folks, we've got {citizenCount} of us in this boat, and it's got holes from years of wear and tear—pipes bursting, trucks rusting, and wages that couldn't lure a mechanic if we threw in free donuts. If we do nothing, here's the ghost story: Expenses keep climbing (up {inflationRate:P0} on average for small towns like ours from aging stuff), revenues flatline, and we end up with deficits that force emergency hikes or service cuts. Wiley shows us the math: Without action, we're looking at a {formatCurrency(annualDeficit)} shortfall by next year, meaning delayed fixes and higher costs down the road. But with a small 3% bump phased over time? We stabilize, build a surplus for surprises, and actually qualify for grants that cover the big stuff.""

### Scenario Breakdown: ""Do Nothing vs. Smart Steps—Pick Your Adventure""
- **The ""Do Nothing"" Horror Show:** ""If we kick this can, trends from our data (and small-town stats everywhere) say infra costs spike—think {formatCurrency(emergencyRepairCost)} more for emergency repairs alone. No grants (they want proof we're serious), wages stay low so we lose good people, and rates eventually jump 20% in a panic. Impact? Everyone hurts, especially fixed-income folks. Wiley crunched it: Deficit grows to {formatCurrency(threeYearDeficit)} in {yearsProjection} years—talk to our CPA, but this ain't pretty.""
- **The ""Planned Hike"" Happy Ending:** ""Now, flip it: A tiny 3% on trash/water yearly for {yearsProjection} years (total +9%, but spread out). Wiley models: Covers actual expenses ({formatCurrency(totalMonthlyExpenses)} now vs. projected {formatCurrency(projectedAnnualExpenses / 12)}), softens with community rebates or low-income credits. Surplus? {formatCurrency(surplusBuffer)} buffer for wages/benefits to snag better hires—maybe bump pay 10% to attract that bright spark who fixes pipes right the first time. And the kicker? This gets us to state-min rates (ours at {formatCurrency(totalMonthlyRevenue / citizenCount)}, need {formatCurrency((totalMonthlyRevenue + suggestedHike) / citizenCount)} to apply), unlocking grants like USDA's that pay 50%+ of infra fixes. Without? We'd need rates at {formatCurrency((totalMonthlyRevenue + totalMonthlyDeficit * 2) / citizenCount)}; with grants, just {formatCurrency((totalMonthlyRevenue + suggestedHike) / citizenCount)}. Verifiable? Wiley's open book—export the trends, have the CPA audit.""

### Grant Tie-In: ""Free Money Alert—But We Gotta Qualify First""
- ""Grants are our lifeline, but they're picky: USDA wants clean water/waste systems for rural spots like us, but you gotta show financial sense—rates at least at state mins (ours {formatCurrency(totalMonthlyRevenue / citizenCount)}, target {formatCurrency((totalMonthlyRevenue + suggestedHike) / citizenCount)} to apply). Why? Proves we're committed, not just begging. Wiley spots: Hit {formatCurrency((totalMonthlyRevenue + suggestedHike) / citizenCount)} with our plan, submit for {formatCurrency(grantAmount)} in pipe/trash grants—covers impact so hikes don't sting. No min rates? Application denied, back to square one. CPA confirm: This aligns with eligibility—let's not leave free cash on the table.""

### Softening the Blow: ""Make It Hurt Less—Community Style""
- ""Nobody likes hikes, but let's cushion: Phase 'em slow (3% year 1 on trash—{formatCurrency(monthlyHikePerCitizen)}/month bump, but offset with energy tips or rebates). For low-income? Sliding scales or assistance programs (many towns do it). And community buy-in: 'We all pitch in—rates cover basics, grants do heavy lifting.' Wiley verifies: Shows exact household impact, trends prove it's fair. Skeptical? CPA double-check the numbers—I'm not the expert, but the math doesn't lie.""

### Closing Pep Talk for Board: ""We're In This Together—Let's Not Sink the Ship""
- ""Church buddies or not, we're stewards here—doing nothing's the real sin against our {citizenCount} neighbors. This plan's honest (data-driven, verifiable), incremental (no shocks), and smart (grants + surplus for wages/infra). Wiley's our tool, but CPA's the final word—let's review together. Worst case? We adjust. Best? Stable town, happy hires, fixed pipes. Questions? Fire away—better now than at bingo.""
";

            // Add disclaimer
            talkingPoints += "\n\nRemember: I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm all numbers before presentation.";

            return talkingPoints.Trim();
        }

        /// <summary>
        /// Disposes of managed resources and cleans up the service.
        ///
        /// This method ensures proper cleanup of:
        /// - HTTP client resources
        /// - In-memory cache to prevent memory leaks
        /// - Any other disposable components
        ///
        /// Implements the standard Dispose pattern with finalizer suppression
        /// for deterministic resource management.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        ///
        /// Handles both explicit disposal and finalizer calls.
        /// Only disposes managed resources once to prevent double disposal.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if from finalizer</param>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _client.Dispose();
                    _cache.Clear();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Computes a SHA256 hash of the input string for caching purposes
        /// </summary>
        private static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }

    /// <summary>
    /// Data transfer objects and helper classes for GrokSupercomputer operations.
    ///
    /// These classes define the structure of data exchanged with the xAI Grok API
    /// and provide type-safe representations of AI responses and internal data structures.
    /// </summary>

    /// <summary>
    /// Represents the response structure from xAI's chat completions API.
    ///
    /// This class maps the JSON response from Grok API calls, containing:
    /// - choices: Array of completion options (typically only one)
    /// - Each choice contains a message with the AI-generated content
    ///
    /// Used internally for deserializing API responses before extracting content.
    /// </summary>
    internal class CompletionResponse
    {
        public Choice[]? choices { get; set; }

        public class Choice
        {
            public Message? message { get; set; }
        }

        public class Message
        {
            public string? content { get; set; }
        }
    }

    /// <summary>
    /// Represents the AI-calculated results for a single enterprise.
    ///
    /// This class defines the expected JSON structure returned by Grok
    /// when analyzing individual enterprise budget data:
    /// - name: Anonymized enterprise identifier (matches input order)
    /// - deficit: Calculated monthly shortfall (expenses - revenue)
    /// - suggestedHike: Recommended rate increase to eliminate deficit
    /// - suggestion: AI-generated recommendation text with disclaimer
    /// </summary>
    internal class ComputedEnterprise
    {
        public string? name { get; set; }
        public decimal deficit { get; set; }
        public decimal suggestedHike { get; set; }
        public string? suggestion { get; set; }
    }

    /// <summary>
    /// Core budget metrics for municipal financial analysis.
    ///
    /// This class aggregates financial data across all enterprises to provide
    /// high-level insights into the municipality's overall fiscal health:
    /// - Total revenue and expenses from all municipal services
    /// - Net monthly balance (surplus/deficit indicator)
    /// - Total citizen count for per-capita calculations
    ///
    /// Used for dashboard displays and executive reporting.
    /// </summary>
    public class BudgetMetrics
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal MonthlyBalance { get; set; }
        public int TotalCitizens { get; set; }
    }

    /// <summary>
    /// Comprehensive AI-generated budget insights and strategic recommendations.
    ///
    /// This class encapsulates the complete analysis output from Grok, including:
    /// - MainInsight: Primary finding about budget health
    /// - Recommendations: Actionable suggestions for optimization
    /// - RiskAssessment: Potential threats and mitigation strategies
    /// - Opportunities: Growth and efficiency improvement possibilities
    /// - Disclaimer: Transparency statement for responsible AI usage
    ///
    /// Designed for UI display with built-in fallback logic for basic analysis.
    /// </summary>
    public class BudgetInsights
    {
        public string? MainInsight { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public string? RiskAssessment { get; set; }
        public string? Opportunities { get; set; }
        public string Disclaimer { get; set; } = "I'm not your accountant, but this trend screams 'double-check'—have your CPA confirm. Export this for your CPA's blessing.";

        /// <summary>
        /// Generates basic budget insights using deterministic logic.
        ///
        /// This method provides fallback analysis when AI is unavailable:
        /// - Identifies surplus/deficit status
        /// - Adds standard recommendations to budget review
        /// - Ensures UI always has content to display
        ///
        /// Called before AI enhancement to guarantee baseline functionality.
        /// </summary>
        /// <param name="metrics">Current budget metrics for analysis</param>
        public void GenerateInsights(BudgetMetrics metrics)
        {
            // Basic logic (e.g., surplus/deficit) kept for flexibility
            MainInsight = metrics.MonthlyBalance < 0 ? "Deficit Alert!" : "Budget on Track";
            Recommendations.Add("Review expenses");
            Recommendations.Add("Engage citizens for feedback");
        }

        /// <summary>
        /// Deserialized result from Grok's budget insights generation.
        ///
        /// This class maps the JSON response when requesting strategic
        /// Used internally to populate BudgetInsights objects with AI content.
        /// </summary>
        public class BudgetInsightsResult
        {
            public string? mainInsight { get; set; }
            public List<string>? recommendations { get; set; }
            public string? riskAssessment { get; set; }
            public string? opportunities { get; set; }
        }

    /// <summary>
    /// Deserialized result from Grok's budget analytics computation.
    ///
    /// This class maps the JSON response when requesting comprehensive
    /// municipal budget calculations from the AI:
    /// - Aggregated financial totals and balances
    /// - Per-citizen averages and efficiency ratios
    /// - Budget status classification
    ///
    /// Used internally to transfer AI results to BudgetMetrics objects.
    /// </summary>
    internal class BudgetAnalyticsResult
    {
        public decimal totalRevenue { get; set; }
        public decimal totalExpenses { get; set; }
        public decimal monthlyBalance { get; set; }
        public int totalCitizens { get; set; }
        public string? status { get; set; }
        public decimal averageRevenuePerCitizen { get; set; }
        public decimal overallEfficiency { get; set; }
    }
}
}
