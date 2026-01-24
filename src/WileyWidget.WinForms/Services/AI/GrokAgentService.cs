using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Reflection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using WileyWidget.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Services.AI
{
    /// <summary>
    /// Provides a thin Semantic Kernel-backed client for Grok (xAI).
    /// - Registers a Semantic Kernel chat completion connector when an API key is present.
    /// - Exposes a simple HTTP fallback to exercise the Grok endpoint for basic tests.
    /// - Defers heavy Semantic Kernel initialization to async initialization phase.
    /// </summary>
    public sealed class GrokAgentService : IAsyncInitializable, IDisposable, IAIService
    {
        private Kernel? _kernel;
        private readonly IXaiModelDiscoveryService? _modelDiscoveryService;
        private readonly IGrokApiKeyProvider? _apiKeyProvider;  // ✅ NEW: Inject the centralized provider
        private string? _apiKey;
        private readonly Uri? _baseEndpoint;
        private readonly Uri? _endpoint;
        private string _model;
        private readonly bool _autoSelectModelOnStartup;
        private readonly ILogger<GrokAgentService>? _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory? _httpClientFactory;
        private readonly IChatBridgeService? _chatBridge;
        private readonly IJARVISPersonalityService? _jarvisPersonality;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IMemoryCache? _memoryCache;
        private readonly double? _defaultPresencePenalty;
        private readonly double? _defaultFrequencyPenalty;
        private bool _isInitialized = false;
        private bool _initializationFailed = false;
        private readonly CancellationTokenSource _serviceCts = new();
        private bool _disposed = false;
        private readonly bool _ownsHttpClient;
        private const string ChatHistoryCacheKeyPrefix = "grok_chat_history_";
        private const int ChatHistoryCacheDurationMinutes = 30;

        private const string ResponsesEndpointSuffix = "/responses";
        private const string ChatCompletionSuffix = "/chat/completions"; // Legacy, deprecated
        private const string ApiKeyEnvironmentVariable = "XAI_API_KEY";
        private static readonly (EnvironmentVariableTarget Target, string Source)[] ApiKeyEnvironmentTargets =
        {
            (EnvironmentVariableTarget.Process, "process env"),
            (EnvironmentVariableTarget.User, "user env"),
            (EnvironmentVariableTarget.Machine, "machine env")
        };
        internal static Func<EnvironmentVariableTarget, string?>? EnvironmentVariableGetterOverride { get; set; }
        private const string DefaultArchitectPrompt = "You are a senior Syncfusion WinForms architect. Enforce SfSkinManager theming rules and repository conventions: prefer SfSkinManager.LoadAssembly and SfSkinManager.SetVisualStyle, avoid manual BackColor/ForeColor assignments except for semantic status colors (Color.Red/Color.Green/Color.Orange), favor MVVM patterns and ThemeColors.ApplyTheme(this) on forms. Provide concise, actionable guidance and C# examples that follow the project's coding standards.";

        private const string JarvisSystemPrompt = "You are JARVIS, the dry-witted, hyper-competent AI for municipal utility finance. Speak with confidence and slight British sarcasm. Be proactive: suggest scenarios, flag risks, roast bad budgets when asked. End bold recommendations with 'MORE COWBELL!' Never bland corporate speak.";

        // Track response IDs for later retrieval and conversation continuation (per X.ai new Responses API)
        private readonly Dictionary<string, string> _conversationResponseIds = new();

        public GrokAgentService(
            IGrokApiKeyProvider apiKeyProvider,  // ✅ NEW: Inject centralized provider first
            IConfiguration config,
            ILogger<GrokAgentService>? logger = null,
            IHttpClientFactory? httpClientFactory = null,
            IXaiModelDiscoveryService? modelDiscoveryService = null,
            IChatBridgeService? chatBridge = null,
            IServiceProvider? serviceProvider = null,
            IJARVISPersonalityService? jarvisPersonality = null,
            IMemoryCache? memoryCache = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (apiKeyProvider == null) throw new ArgumentNullException(nameof(apiKeyProvider));
            
            _apiKeyProvider = apiKeyProvider;  // ✅ Store provider reference
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
            _modelDiscoveryService = modelDiscoveryService;
            _chatBridge = chatBridge;
            _serviceProvider = serviceProvider;
            _jarvisPersonality = jarvisPersonality;
            _memoryCache = memoryCache;

            // Subscribe to chat bridge events if available
            if (_chatBridge != null)
            {
                _chatBridge.PromptSubmitted += OnChatPromptSubmitted;
                _logger?.LogInformation("[XAI] ChatBridgeService subscribed for prompt events");
            }

            // ✅ FIXED: Use injected IGrokApiKeyProvider instead of reading environment variables directly
            _apiKey = apiKeyProvider.ApiKey;  // Get API key from centralized provider
            var source = apiKeyProvider.GetConfigurationSource();
            _logger?.LogInformation(
                "[XAI] Using API key from {Source} (length: {Length}, validated: {IsValidated})",
                source,
                _apiKey?.Length ?? 0,
                apiKeyProvider.IsValidated);

            _model = config["Grok:Model"] ?? config["XAI:Model"] ?? "grok-4";

            // Read default penalties from configuration (XAI/Grok keys). Reasoning models may not support these penalties.
            var presenceStr = config["XAI:DefaultPresencePenalty"] ?? config["Grok:DefaultPresencePenalty"];
            if (!string.IsNullOrWhiteSpace(presenceStr) && double.TryParse(presenceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pVal))
            {
                _defaultPresencePenalty = pVal;
                _logger?.LogDebug("[XAI] Default presence_penalty set from config: {Val}", pVal);
            }

            var frequencyStr = config["XAI:DefaultFrequencyPenalty"] ?? config["Grok:DefaultFrequencyPenalty"];
            if (!string.IsNullOrWhiteSpace(frequencyStr) && double.TryParse(frequencyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fVal))
            {
                _defaultFrequencyPenalty = fVal;
                _logger?.LogDebug("[XAI] Default frequency_penalty set from config: {Val}", fVal);
            }

            // Auto-select model on startup (optional; enabled via XAI:AutoSelectModelOnStartup or Grok:AutoSelectModelOnStartup)
            var autoSelectStr = config["XAI:AutoSelectModelOnStartup"] ?? config["Grok:AutoSelectModelOnStartup"];
            _autoSelectModelOnStartup = bool.TryParse(autoSelectStr, out var tmpAuto) && tmpAuto;

            var endpointStr = (config["Grok:Endpoint"] ?? config["XAI:Endpoint"] ?? "https://api.x.ai/v1").Trim();
            if (string.IsNullOrEmpty(endpointStr))
            {
                endpointStr = "https://api.x.ai/v1";
            }

            var baseEndpointCandidate = endpointStr.TrimEnd('/');
            if (baseEndpointCandidate.EndsWith(ChatCompletionSuffix, StringComparison.OrdinalIgnoreCase))
            {
                baseEndpointCandidate = baseEndpointCandidate.Substring(0, baseEndpointCandidate.Length - ChatCompletionSuffix.Length);
            }

            baseEndpointCandidate = baseEndpointCandidate.TrimEnd('/');
            var normalizedBase = baseEndpointCandidate + '/';

            _baseEndpoint = new Uri(normalizedBase, UriKind.Absolute);
            // Use new /responses endpoint instead of deprecated /chat/completions
            _endpoint = new Uri(_baseEndpoint, ResponsesEndpointSuffix); // /v1/responses

            // Log the API key presence for diagnostics (do not log the full key)
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                logger?.LogInformation("[XAI] API key detected in configuration/environment.");
                var preview = _apiKey.Length > 8 ? _apiKey.Substring(0, 4) + "..." + _apiKey.Substring(_apiKey.Length - 4) : _apiKey;
                logger?.LogDebug("[XAI] API key preview: {Preview} (length {Length})", preview, _apiKey.Length);
            }
            else
            {
                logger?.LogWarning("[XAI] API key NOT found in configuration/environment. Chat will not function.");
            }

            // Log environment and configuration details for diagnostics
            logger?.LogInformation("[XAI] Environment variable {EnvVar} length: {EnvLength}, Config API key length: {ConfigLength}", ApiKeyEnvironmentVariable, Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable)?.Length ?? 0, _apiKey?.Length ?? 0);
            logger?.LogInformation("[XAI] Using model={Model}, endpoint={Endpoint} (NEW /v1/responses API)", _model, _endpoint);

            // Initialize HttpClient early (lightweight, non-blocking)
            _ownsHttpClient = httpClientFactory == null;
            _httpClient = httpClientFactory?.CreateClient("WileyWidgetDefault") ?? new HttpClient();
            _httpClient.BaseAddress = _baseEndpoint;
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            _logger?.LogDebug("[XAI] GrokAgentService instantiated - heavy Semantic Kernel initialization deferred to InitializeAsync()");
            _logger?.LogInformation("[XAI] GrokAgentService constructed - API key present: {HasKey}, Model: {Model}", !string.IsNullOrWhiteSpace(_apiKey), _model);
        }

        /// <summary>
        /// Async initialization - deferred from constructor to avoid blocking startup.
        /// Initializes Semantic Kernel, builds client, and registers plugins.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _serviceCts.Token);
            var ct = linkedCts.Token;

            if (_isInitialized)
            {
                _logger?.LogDebug("[XAI] GrokAgentService already initialized");
                return;
            }

            if (_initializationFailed)
            {
                _logger?.LogWarning("[XAI] GrokAgentService initialization previously failed, skipping");
                return;
            }

            try
            {
                _logger?.LogDebug("[XAI] Beginning async initialization of Grok service");

                // Optionally auto-select a model (short timeout) before building the kernel so the kernel uses the selected model
                if (_autoSelectModelOnStartup && !string.IsNullOrWhiteSpace(_apiKey))
                {
                    try
                    {
                        using var ctsAuto = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        ctsAuto.CancelAfter(TimeSpan.FromSeconds(3));
                        var selected = await AutoSelectModelAsync(ctsAuto.Token);
                        if (!string.IsNullOrWhiteSpace(selected) && !string.Equals(selected, _model, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.LogInformation("[XAI] Auto-selected model '{Selected}' replacing configured '{Configured}'", selected, _model);
                            _model = selected;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogWarning("[XAI] Auto model selection timed out");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[XAI] Auto model selection failed");
                    }
                }

                // Build Semantic Kernel on background thread
                await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();

                    var builder = Kernel.CreateBuilder();

                    if (!string.IsNullOrWhiteSpace(_apiKey))
                    {
                        try
                        {
                            // Use the OpenAI-compatible connector to target xAI's Grok endpoint
                            // Note: Semantic Kernel's OpenAI connector still uses /chat/completions format
                            // For the new Responses API format, use direct HTTP calls via GetSimpleResponse or GetStreamingResponseAsync
                            // The direct HTTP methods (GetSimpleResponse, GetStreamingResponseAsync) use the new /v1/responses endpoint
                            // Add serviceId for better service identification and multi-model support
                            var serviceId = $"grok-{_model}";
                            _logger?.LogInformation("[XAI] Configuring Grok chat completion: model={Model}, endpoint={Endpoint}, serviceId={ServiceId}, apiKeyLength={KeyLength}",
                                _model, _endpoint, serviceId, _apiKey.Length);
#pragma warning disable SKEXP0010
                            // NOTE: This uses /chat/completions format for Semantic Kernel compatibility.
                            // New /v1/responses endpoint is used for direct HTTP calls (GetSimpleResponse, GetStreamingResponseAsync, etc.)
                            var legacyEndpoint = new Uri(_baseEndpoint!, "chat/completions");
                            builder.AddOpenAIChatCompletion(
                                modelId: _model,
                                apiKey: _apiKey,
                                endpoint: legacyEndpoint,
                                serviceId: serviceId);
#pragma warning restore SKEXP0010
                            _logger?.LogInformation("[XAI] Semantic Kernel configured with Grok chat completion successfully (serviceId: {ServiceId})", serviceId);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "[XAI] Failed to configure Semantic Kernel chat completion. Falling back to direct HTTP calls with new /responses endpoint.");
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("[XAI] Grok API key not configured; Semantic Kernel chat connector will not be registered.");
                    }

                    _kernel = builder.Build();

                    // Auto-register kernel plugins discovered in the executing assembly (types with [KernelFunction]).
                    // Keeping this scoped avoids scanning unrelated testhost/runtime assemblies.
                    // Pass IServiceProvider to enable DI-aware plugin instantiation (fixes MissingMethodException for plugins with constructor dependencies)
                    try
                    {
                        var assemblyToScan = typeof(GrokAgentService).Assembly;
                        KernelPluginRegistrar.ImportPluginsFromAssemblies(_kernel, new[] { assemblyToScan }, _logger, _serviceProvider);
                        _logger?.LogDebug("[XAI] Auto-registered kernel plugins from assembly: {Assembly}", assemblyToScan.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[XAI] Failed to auto-register kernel plugins from executing assembly.");
                    }
                }, ct);

                _isInitialized = true;
                _logger?.LogInformation("[XAI] Grok service async initialization complete");
                _logger?.LogInformation("[XAI] GrokAgentService kernel initialized successfully - Plugins registered: {Count}", _kernel?.Plugins.Count ?? 0);

                // Optional: run a quick validation if configured
                var validateOnStartupStr = _config["XAI:ValidateOnStartup"] ?? _config["Grok:ValidateOnStartup"];
                if (bool.TryParse(validateOnStartupStr, out var validateOnStartup) && validateOnStartup && !string.IsNullOrWhiteSpace(_apiKey))
                {
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(TimeSpan.FromSeconds(5));
                        var (success, msg) = await ValidateApiKeyAsync(cts.Token);
                        if (success)
                            _logger?.LogInformation("[XAI] Async initialization validation: API key OK");
                        else
                            _logger?.LogWarning("[XAI] Async initialization validation: API key check failed: {Msg}", msg);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogWarning("[XAI] Async initialization validation timed out");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[XAI] Async initialization validation failed unexpectedly");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _initializationFailed = true;
                _logger?.LogWarning("[XAI] GrokAgentService initialization canceled");
                throw;
            }
            catch (Exception ex)
            {
                _initializationFailed = true;
                _logger?.LogError(ex, "[XAI] GrokAgentService initialization failed");
                throw;
            }
        }

        /// <summary>
        /// Exposes the underlying Kernel instance (if the caller needs to register plugins or run advanced flows).
        /// Will be null until InitializeAsync() completes.
        /// </summary>
        public Kernel? Kernel => _kernel;

        /// <summary>
        /// Indicates whether an API key is configured for Grok (xAI).
        /// </summary>
        public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

        /// <summary>
        /// Indicates whether async initialization has completed successfully.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// The length of the configured API key (safe to log; value is not exposed).
        /// </summary>
        public int ApiKeyLength => _apiKey?.Length ?? 0;

        /// <summary>
        /// The model configured for Grok requests.
        /// </summary>
        public string Model => _model;

        /// <summary>
        /// The endpoint configured for Grok requests.
        /// </summary>
        public Uri? Endpoint => _endpoint;

        private bool _isApiKeyValidated = false;
        private DateTime? _lastApiKeyValidation;

        /// <summary>
        /// Indicates whether a recent API key validation succeeded.
        /// </summary>
        public bool IsApiKeyValidated => _isApiKeyValidated;

        /// <summary>
        /// Timestamp of the last API key validation attempt (UTC).
        /// </summary>
        public DateTime? LastApiKeyValidation => _lastApiKeyValidation;

        /// <summary>
        /// Get a streaming response using the new /v1/responses endpoint with SSE (Server-Sent Events).
        /// Invokes a callback for each chunk and returns complete response after streaming all chunks.
        /// Implements exponential backoff retry for 429 (rate limit) responses.
        /// Response IDs are tracked for conversation continuation and retrieval.
        /// </summary>
        public async Task<string> GetStreamingResponseAsync(string userMessage, string? systemPrompt = null, string? modelOverride = null, Action<string>? onChunk = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogInformation("GetStreamingResponseAsync called but no Grok API key configured.");
                return "No API key configured for Grok";
            }

            var model = modelOverride ?? _model ?? "grok-4";
            var sysPrompt = systemPrompt ?? "You are a helpful assistant.";

            return await SendStreamingResponseAsync(model, sysPrompt, userMessage, onChunk, ct).ConfigureAwait(false);
        }

        private async Task<string> SendStreamingResponseAsync(string model, string systemPrompt, string userMessage, Action<string>? onChunk, CancellationToken ct, int retryCount = 0, int maxRetries = 3)
        {
            // Build input array for /v1/responses endpoint (new format: input instead of messages)
            var inputArray = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            };

            var payload = CreateResponsesPayload(model, inputArray, stream: true);
            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            };

            // SSE streaming: request Accept header should be text/event-stream per x.ai docs
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            try
            {
                _logger?.LogDebug("[XAI] SendStreamingResponseAsync -> POST {Url} with stream=true (model={Model}, retryCount={RetryCount}, endpoint=responses)", _endpoint, model, retryCount);
                var resp = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                // Handle rate limit with exponential backoff
                if ((int)resp.StatusCode == 429)
                {
                    if (retryCount < maxRetries)
                    {
                        var delayMs = (int)Math.Pow(2, retryCount) * 1000; // 1s, 2s, 4s backoff
                        _logger?.LogWarning("[XAI] Rate limited (429); retry {RetryCount}/{MaxRetries} after {DelayMs}ms", retryCount + 1, maxRetries, delayMs);
                        await Task.Delay(delayMs, ct).ConfigureAwait(false);
                        return await SendStreamingResponseAsync(model, systemPrompt, userMessage, onChunk, ct, retryCount + 1, maxRetries).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger?.LogError("[XAI] Rate limit (429) exceeded max retries ({MaxRetries})", maxRetries);
                        return $"Grok API rate limited (429) - exceeded max retries";
                    }
                }

                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Grok responses API returned non-success status: {Status}", resp.StatusCode);
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    // Parse error details and log full message for diagnostics
                    try
                    {
                        using var errDoc = JsonDocument.Parse(body);
                        var errMsg = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() :
                                     errDoc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(errMsg))
                        {
                            _logger?.LogWarning("Grok responses error detail: {ErrorDetail}", errMsg);
                        }
                    }
                    catch (JsonException)
                    {
                        _logger?.LogDebug("SendStreamingResponseAsync: failed to parse error JSON for diagnostics");
                    }
                    return $"Grok API error {(int)resp.StatusCode} ({resp.StatusCode}): {body}";
                }

                // Read streaming response chunks (SSE format)
                var responseBuilder = new StringBuilder();
                var responseId = string.Empty;
                var responseStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var reader = new System.IO.StreamReader(responseStream);

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (line == null)
                        break; // End of stream

                    // SSE format: "data: {json}"
                    if (line.StartsWith("data: ", StringComparison.Ordinal))
                    {
                        var dataJson = line.Substring(6); // Remove "data: " prefix

                        if (dataJson == "[DONE]")
                        {
                            _logger?.LogDebug("Stream ended with [DONE] marker");
                            break; // Stream complete
                        }

                        try
                        {
                            using var doc = JsonDocument.Parse(dataJson);
                            // New responses API: extract from output array instead of choices
                            if (doc.RootElement.TryGetProperty("id", out var idElem))
                            {
                                responseId = idElem.GetString() ?? string.Empty;
                            }

                            if (doc.RootElement.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
                            {
                                if (output.GetArrayLength() > 0)
                                {
                                    var item = output[0];
                                    if (item.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
                                    {
                                        if (contentArray.GetArrayLength() > 0)
                                        {
                                            var contentItem = contentArray[0];
                                            if (contentItem.TryGetProperty("text", out var textElem))
                                            {
                                                var contentText = textElem.GetString();
                                                if (!string.IsNullOrEmpty(contentText))
                                                {
                                                    responseBuilder.Append(contentText);
                                                    onChunk?.Invoke(contentText);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger?.LogWarning(ex, "Failed to parse SSE chunk: {Data}", dataJson);
                        }
                    }
                    // Skip empty lines or other SSE metadata
                }

                var fullResponse = responseBuilder.ToString();
                _logger?.LogDebug("Streaming completed: {Length} chars, ResponseId: {ResponseId}", fullResponse.Length, responseId);

                // Track response ID for conversation continuation if available
                if (!string.IsNullOrWhiteSpace(responseId))
                {
                    _conversationResponseIds[userMessage.GetHashCode().ToString()] = responseId;
                }

                return fullResponse;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("SendStreamingResponseAsync canceled by token");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Grok responses streaming request failed");
                return $"Grok streaming failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Simple chat helper using the new /v1/responses endpoint.
        /// If no API key is configured this returns a clear diagnostic string.
        /// Supports providing an optional system prompt and model override and accepts a cancellation token.
        /// </summary>
        public async Task<string> GetSimpleResponse(string userMessage, string? systemPrompt = null, string? modelOverride = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogInformation("GetSimpleResponse called but no Grok API key configured.");
                return "No API key configured for Grok";
            }

            var model = modelOverride ?? _model ?? "grok-4";
            var sysPrompt = systemPrompt ?? "You are a test assistant.";

            // Use new /v1/responses endpoint with input array format
            var inputArray = new object[] {
                new { role = "system", content = sysPrompt },
                new { role = "user", content = userMessage }
            };
            var payload = CreateResponsesPayload(model, inputArray, stream: false);
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

            try
            {
                _logger?.LogDebug("[XAI] GetSimpleResponse -> POST {Url} (model={Model}, endpoint=responses)", _endpoint, model);
                var resp = await _httpClient.PostAsync(_endpoint, content, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Grok responses API returned non-success status: {Status}", resp.StatusCode);
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    // Try to parse structured error to detect model-not-found and attempt fallback once
                    try
                    {
                        using var errDoc = JsonDocument.Parse(body);
                        var errMsg = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() :
                                     errDoc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(errMsg) &&
                            resp.StatusCode == System.Net.HttpStatusCode.NotFound &&
                            errMsg.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            errMsg.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var fallback = "grok-4";
                            if (!string.Equals(model, fallback, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger?.LogWarning("Model '{Model}' not found; retrying GetSimpleResponse with fallback '{FallbackModel}'", model, fallback);
                                return await GetSimpleResponse(userMessage, systemPrompt, fallback, ct);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        _logger?.LogDebug("GetSimpleResponse: failed to parse error JSON for diagnostics");
                    }

                    return $"Grok API returned HTTP {(int)resp.StatusCode} ({resp.StatusCode}): {body}";
                }

                var respStr = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                try
                {
                    using var doc = JsonDocument.Parse(respStr);
                    // New responses API format: output[0].content[0].text instead of choices[0].message.content
                    var output = doc.RootElement.GetProperty("output");
                    if (output.ValueKind == JsonValueKind.Array && output.GetArrayLength() > 0)
                    {
                        var firstOutput = output[0];
                        if (firstOutput.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array && contentArray.GetArrayLength() > 0)
                        {
                            var firstContent = contentArray[0];
                            if (firstContent.TryGetProperty("text", out var textElem))
                            {
                                var text = textElem.GetString();
                                return text ?? "No content";
                            }
                        }
                    }
                    // Fallback for unexpected structure
                    return $"Unexpected response structure: {respStr.Substring(0, Math.Min(500, respStr.Length))}";
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse Grok responses response; returning raw payload");
                    return respStr;
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("GetSimpleResponse canceled by token");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Grok request failed");
                return $"Grok request failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Validate the configured API key by performing an exact request to the new /v1/responses endpoint.
        /// Returns a tuple of (success, responseMessage).
        /// </summary>
        public async Task<(bool Success, string Message)> ValidateApiKeyAsync(CancellationToken ct = default, string? modelOverride = null)
        {
            _logger?.LogInformation("Validating XAI API key via /v1/responses endpoint");

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogWarning("ValidateApiKeyAsync called but no API key configured.");
                return (false, "No API key configured");
            }

            var model = modelOverride ?? _model ?? "grok-4";
            var requestObj = new
            {
                input = new object[]
                {
                    new { role = "system", content = "You are a test assistant." },
                    new { role = "user", content = "Testing. Just say hi and hello world and nothing else." }
                },
                model = model,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                using var resp = await _httpClient.PostAsync(_endpoint, content, ct).ConfigureAwait(false);
                var respBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("ValidateApiKeyAsync: Grok responses API returned non-success status: {Status}", resp.StatusCode);
                    // Try to parse structured error for better diagnostics
                    try
                    {
                        using var errDoc = JsonDocument.Parse(respBody);
                        var errMsg = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() :
                                     errDoc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(errMsg))
                        {
                            _logger?.LogWarning("Grok responses API error detail: {ErrorDetail}", errMsg);
                            // Detect model-not-found and attempt fallback once
                            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound &&
                                errMsg.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                errMsg.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var fallback = "grok-4";
                                if (!string.Equals(model, fallback, StringComparison.OrdinalIgnoreCase))
                                {
                                    _logger?.LogWarning("Model '{Model}' not found; retrying validation with fallback '{FallbackModel}'", model, fallback);
                                    return await ValidateApiKeyAsync(ct, fallback);
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        _logger?.LogDebug("ValidateApiKeyAsync: failed to parse error JSON for diagnostics");
                    }

                    return (false, $"HTTP {resp.StatusCode}: {respBody}");
                }

                using var doc = JsonDocument.Parse(respBody);
                // New responses API format: output[0].content[0].text instead of choices[0].message.content
                var output = doc.RootElement.GetProperty("output");
                if (output.ValueKind == JsonValueKind.Array && output.GetArrayLength() > 0)
                {
                    var firstOutput = output[0];
                    if (firstOutput.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array && contentArray.GetArrayLength() > 0)
                    {
                        var firstContent = contentArray[0];
                        if (firstContent.TryGetProperty("text", out var textElem))
                        {
                            var text = textElem.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                var lower = text.ToLowerInvariant();
                                if (lower.Contains("hi", StringComparison.Ordinal) && lower.Contains("hello world", StringComparison.Ordinal))
                                {
                                    _isApiKeyValidated = true;
                                    _lastApiKeyValidation = DateTime.UtcNow;
                                    _logger?.LogInformation("ValidateApiKeyAsync succeeded with response preview: {Preview}", text.Substring(0, Math.Min(200, text.Length)));
                                    return (true, text);
                                }
                                else
                                {
                                    _logger?.LogWarning("ValidateApiKeyAsync: Response did not match expected text: {Resp}", text);
                                    return (false, text);
                                }
                            }
                        }
                    }
                }

                _logger?.LogWarning("ValidateApiKeyAsync: Response had no content: {Resp}", respBody);
                return (false, respBody);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger?.LogWarning("ValidateApiKeyAsync canceled");
                return (false, "Canceled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ValidateApiKeyAsync failed");
                return (false, $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Lists available models from the x.ai API. Returns model IDs (best-effort parsing).
        /// </summary>
        public async Task<IEnumerable<string>> ListAvailableModelsAsync(CancellationToken ct = default)
        {
            try
            {
                var modelsEndpoint = new Uri(_baseEndpoint!, "models");
                _logger?.LogDebug("[XAI] Listing models via {Url}", modelsEndpoint);
                using var resp = await _httpClient.GetAsync(modelsEndpoint, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("[XAI] List models returned non-success {Status}: {Body}", resp.StatusCode, body);
                    return Array.Empty<string>();
                }

                using var doc = JsonDocument.Parse(body);
                var list = new List<string>();

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in data.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                                list.Add(idProp.GetString()!);
                            else if (item.ValueKind == JsonValueKind.String)
                                list.Add(item.GetString()!);
                        }
                    }
                    else if (doc.RootElement.TryGetProperty("models", out var modelsProp) && modelsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in modelsProp.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                                list.Add(item.GetString()!);
                            else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                                list.Add(idProp.GetString()!);
                        }
                    }
                    else
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (prop.Name.StartsWith("grok-", StringComparison.OrdinalIgnoreCase))
                            {
                                list.Add(prop.Name);
                            }
                        }
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                            list.Add(idProp.GetString()!);
                        else if (item.ValueKind == JsonValueKind.String)
                            list.Add(item.GetString()!);
                    }
                }

                var distinct = list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                _logger?.LogDebug("[XAI] Discovered {Count} models", distinct.Count);
                return distinct;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[XAI] ListAvailableModelsAsync failed");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Retrieve a previously generated response by ID using the /v1/responses/{response_id} endpoint.
        /// Responses are stored for 30 days after creation.
        /// </summary>
        public async Task<string?> GetResponseByIdAsync(string responseId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(responseId))
            {
                _logger?.LogWarning("[XAI] GetResponseByIdAsync called with empty responseId");
                return null;
            }

            try
            {
                var responseEndpoint = new Uri(_endpoint!, $"/{responseId}");
                _logger?.LogDebug("[XAI] Retrieving response {ResponseId} via {Url}", responseId, responseEndpoint);

                using var resp = await _httpClient.GetAsync(responseEndpoint, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("[XAI] GetResponseByIdAsync returned non-success {Status}: {Body}", resp.StatusCode, body);
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                // Extract text from output array
                if (doc.RootElement.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
                {
                    if (output.GetArrayLength() > 0)
                    {
                        var firstOutput = output[0];
                        if (firstOutput.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array && contentArray.GetArrayLength() > 0)
                        {
                            var firstContent = contentArray[0];
                            if (firstContent.TryGetProperty("text", out var textElem))
                            {
                                return textElem.GetString();
                            }
                        }
                    }
                }

                _logger?.LogWarning("[XAI] GetResponseByIdAsync: unexpected response structure");
                return body;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[XAI] GetResponseByIdAsync failed for responseId {ResponseId}", responseId);
                return null;
            }
        }

        /// <summary>
        /// Delete a previously generated response by ID using the /v1/responses/{response_id} endpoint (DELETE).
        /// Returns true if deletion succeeded, false otherwise.
        /// </summary>
        public async Task<bool> DeleteResponseAsync(string responseId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(responseId))
            {
                _logger?.LogWarning("[XAI] DeleteResponseAsync called with empty responseId");
                return false;
            }

            try
            {
                var responseEndpoint = new Uri(_endpoint!, $"/{responseId}");
                _logger?.LogDebug("[XAI] Deleting response {ResponseId} via {Url}", responseId, responseEndpoint);

                using var resp = await _httpClient.DeleteAsync(responseEndpoint, ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    _logger?.LogWarning("[XAI] DeleteResponseAsync returned non-success {Status}: {Body}", resp.StatusCode, body);
                    return false;
                }

                // Parse response to confirm deletion
                var bodyStr = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(bodyStr);

                if (doc.RootElement.TryGetProperty("deleted", out var deletedProp) && deletedProp.ValueKind == JsonValueKind.True)
                {
                    _logger?.LogInformation("[XAI] DeleteResponseAsync succeeded for responseId {ResponseId}", responseId);
                    return true;
                }

                _logger?.LogWarning("[XAI] DeleteResponseAsync: unexpected response structure");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[XAI] DeleteResponseAsync failed for responseId {ResponseId}", responseId);
                return false;
            }
        }

        /// <summary>
        /// Continue a previous conversation by providing a previous_response_id.
        /// This uses the new /v1/responses endpoint with previous_response_id parameter.
        /// </summary>
        public async Task<string> ContinueConversationAsync(string userMessage, string previousResponseId, string? systemPrompt = null, string? modelOverride = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogInformation("ContinueConversationAsync called but no Grok API key configured.");
                return "No API key configured for Grok";
            }

            var model = modelOverride ?? _model ?? "grok-4";
            var sysPrompt = systemPrompt ?? "You are a helpful assistant.";

            try
            {
                // New responses API supports previous_response_id for conversation continuation
                var inputArray = new object[] {
                    new { role = "user", content = userMessage }
                };

                var payload = new Dictionary<string, object?>
                {
                    ["model"] = model,
                    ["input"] = inputArray,
                    ["stream"] = false,
                    ["store"] = true,
                    ["previous_response_id"] = previousResponseId  // NEW: continue conversation
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                _logger?.LogDebug("[XAI] ContinueConversationAsync -> POST {Url} (previousResponseId={PreviousId})", _endpoint, previousResponseId);
                var resp = await _httpClient.PostAsync(_endpoint, content, ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("ContinueConversationAsync returned non-success status: {Status}", resp.StatusCode);
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    return $"Grok API error {(int)resp.StatusCode}: {body}";
                }

                var respStr = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(respStr);

                // Extract response ID for potential future continuation
                if (doc.RootElement.TryGetProperty("id", out var idElem))
                {
                    var responseId = idElem.GetString();
                    _logger?.LogDebug("[XAI] ContinueConversationAsync received responseId: {ResponseId}", responseId);
                }

                // Extract text from output array
                var output = doc.RootElement.GetProperty("output");
                if (output.ValueKind == JsonValueKind.Array && output.GetArrayLength() > 0)
                {
                    var firstOutput = output[0];
                    if (firstOutput.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array && contentArray.GetArrayLength() > 0)
                    {
                        var firstContent = contentArray[0];
                        if (firstContent.TryGetProperty("text", out var textElem))
                        {
                            return textElem.GetString() ?? "No content";
                        }
                    }
                }

                return $"Unexpected response structure: {respStr.Substring(0, Math.Min(500, respStr.Length))}";
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("ContinueConversationAsync canceled by token");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ContinueConversationAsync failed");
                return $"Grok conversation continuation failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Attempts to select the best available model from the API.
        /// Returns the selected model ID or null if none found.
        /// </summary>
        public async Task<string?> AutoSelectModelAsync(CancellationToken ct = default)
        {
            // Prefer the configured discovery service when available; it queries /v1/language-models for richer metadata.
            if (_modelDiscoveryService != null)
            {
                try
                {
                    var desc = await _modelDiscoveryService.ChooseBestModelAsync(_model, ct).ConfigureAwait(false);
                    if (desc != null && !string.IsNullOrWhiteSpace(desc.Id))
                    {
                        _logger?.LogDebug("[XAI] Model discovery service selected model {Model}", desc.Id);
                        return desc.Id;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[XAI] Model discovery service failed during AutoSelectModelAsync; falling back to enumerating /models");
                }
            }

            // Fallback: enumerate models directly from the /models endpoint and pick preferred families (no grok-beta).
            var models = await ListAvailableModelsAsync(ct);
            if (!models.Any()) return null;
            var preferred = new[] { "grok-4", "grok-4-1-fast", "grok-4-1-fast-reasoning", "grok-4-1-fast-non-reasoning", "grok-4-1" };
            foreach (var p in preferred)
            {
                var found = models.FirstOrDefault(m => string.Equals(m, p, StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }
            var firstGrok = models.FirstOrDefault(m => m.StartsWith("grok-", StringComparison.OrdinalIgnoreCase));
            return firstGrok;
        }

        private static string? TryExtractMessage(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                    {
                        return content.GetString();
                    }

                    if (first.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var deltaContent) && deltaContent.ValueKind == JsonValueKind.String)
                    {
                        return deltaContent.GetString();
                    }
                }
            }
            catch
            {
                // Best-effort parsing only; ignore failures
            }

            return null;
        }

        // Helper: Detect whether a model ID refers to a reasoning model (these may not accept penalties)
        private bool IsReasoningModel(string model)
        {
            return !string.IsNullOrWhiteSpace(model) && model.IndexOf("reason", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Build chat request payload as a dictionary to ensure correct snake_case keys for x.ai API (legacy /chat/completions)
        private Dictionary<string, object?> CreateChatRequestPayload(string model, object[] messages, bool stream, double? temperature = null)
        {
            var payload = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["messages"] = messages,
                ["stream"] = stream
            };

            if (temperature.HasValue) payload["temperature"] = temperature.Value;

            // Only include presence/frequency penalties for non-reasoning models
            if (!IsReasoningModel(model))
            {
                if (_defaultPresencePenalty.HasValue) payload["presence_penalty"] = _defaultPresencePenalty.Value;
                if (_defaultFrequencyPenalty.HasValue) payload["frequency_penalty"] = _defaultFrequencyPenalty.Value;
            }

            return payload;
        }

        // Build responses endpoint payload for NEW /v1/responses API
        // This uses "input" array instead of "messages" per X.ai Responses API specification
        private Dictionary<string, object?> CreateResponsesPayload(string model, object[] input, bool stream, double? temperature = null)
        {
            var payload = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["input"] = input,  // NEW: responses API uses "input" not "messages"
                ["stream"] = stream,
                ["store"] = true    // NEW: store responses for 30 days for retrieval and continuation
            };

            if (temperature.HasValue) payload["temperature"] = temperature.Value;

            // Only include presence/frequency penalties for non-reasoning models
            if (!IsReasoningModel(model))
            {
                if (_defaultPresencePenalty.HasValue) payload["presence_penalty"] = _defaultPresencePenalty.Value;
                if (_defaultFrequencyPenalty.HasValue) payload["frequency_penalty"] = _defaultFrequencyPenalty.Value;
            }

            return payload;
        }

        /// <summary>
        /// Event handler for chat bridge prompt submissions.
        /// Routes prompts from Blazor through the agent and streams responses back via the bridge.
        /// </summary>
        private void OnChatPromptSubmitted(object? sender, ChatPromptSubmittedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e?.Prompt) || _disposed)
            {
                _logger?.LogWarning("[XAI] Chat bridge prompt ignored: prompt empty or service disposed");
                return;
            }

            _logger?.LogInformation("[XAI] Chat bridge prompt received: {PromptLength} chars (ConversationId: {ConversationId})", e.Prompt.Length, e.ConversationId ?? "N/A");

            // Queue async work on the thread pool with proper error handling
            // Don't use fire-and-forget - wrap in try/catch to log errors
            Task.Run(async () =>
            {
                try
                {
                    // Use safe token access - check if disposed first
                    var token = CancellationToken.None;
                    try
                    {
                        if (!_disposed && _serviceCts != null)
                        {
                            token = _serviceCts.Token;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Service already disposed, use default token
                    }

                    await RunAgentToChatBridgeAsync(e.Prompt, e.ConversationId, token);
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("[XAI] Chat bridge operation cancelled");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[XAI] Error in chat bridge prompt handler");
                }
            });
        }

        /// <summary>
        /// Runs an agentic chat session with streaming via ChatBridgeService.
        /// Sends chunks back to Blazor as they arrive.
        /// Sends initial "JARVIS is thinking..." message.
        /// </summary>
        private async Task RunAgentToChatBridgeAsync(string userRequest, string? conversationId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogInformation("RunAgentToChatBridgeAsync called but no Grok API key configured.");
                await _chatBridge!.NotifyMessageReceivedAsync(new ChatMessage { Content = "No API key configured for Grok", IsUser = false, Timestamp = DateTime.UtcNow });
                return;
            }

            if (string.IsNullOrWhiteSpace(userRequest))
            {
                _logger?.LogWarning("RunAgentToChatBridgeAsync called with empty user request");
                return;
            }

            if (!_isInitialized || _kernel == null)
            {
                _logger?.LogInformation("[XAI] Kernel not initialized; falling back to simple chat");
                var response = await GetSimpleResponse(userRequest, _jarvisPersonality?.GetSystemPrompt() ?? JarvisSystemPrompt);
                await _chatBridge!.NotifyMessageReceivedAsync(new ChatMessage { Content = response, IsUser = false, Timestamp = DateTime.UtcNow });
                return;
            }

            try
            {
                _logger?.LogInformation("[XAI] RunAgentToChatBridgeAsync invoked - User request length: {Length}, ConversationId: {ConversationId}", userRequest.Length, conversationId ?? "N/A");

                // Use Semantic Kernel's native streaming with FunctionChoiceBehavior.Auto() (Microsoft Docs recommended pattern)
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();

                // Configure execution settings with automatic function calling
                var settings = new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0.3,
                    MaxTokens = 4000
                };

                // Only add penalties for non-reasoning models
                if (!IsReasoningModel(_model))
                {
                    if (_defaultPresencePenalty.HasValue) settings.PresencePenalty = _defaultPresencePenalty.Value;
                    if (_defaultFrequencyPenalty.HasValue) settings.FrequencyPenalty = _defaultFrequencyPenalty.Value;
                }

                // Load existing conversation history if provided
                var systemPrompt = _jarvisPersonality?.GetSystemPrompt() ?? JarvisSystemPrompt;
                var history = await LoadChatHistoryAsync(conversationId, systemPrompt).ConfigureAwait(false);
                history.AddUserMessage(userRequest);

                _logger?.LogDebug("[XAI] Invoking streaming chat with ToolCallBehavior.AutoInvokeKernelFunctions - Plugins: {Count}", _kernel.Plugins.Count);

                // Use Semantic Kernel's native streaming (handles SSE and function calling automatically)
                var responseBuilder = new StringBuilder();

                await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                    history,
                    executionSettings: settings,
                    kernel: _kernel).ConfigureAwait(false))
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        responseBuilder.Append(chunk.Content);
                        if (!ct.IsCancellationRequested)
                        {
                            await _chatBridge!.SendResponseChunkAsync(chunk.Content);
                        }
                    }

                    // Log function calls for observability
                    if (chunk.Metadata?.TryGetValue("FunctionCall", out var functionCall) == true)
                    {
                        _logger?.LogInformation("[XAI] Function called: {FunctionCall}", functionCall);
                    }
                }

                var fullMsg = responseBuilder.ToString();

                if (string.IsNullOrWhiteSpace(fullMsg))
                {
                    _logger?.LogWarning("[XAI] Empty response from streaming chat; attempting fallback");
                    fullMsg = await GetSimpleResponse(userRequest, systemPrompt).ConfigureAwait(false);
                }

                // Add assistant response and persist history
                history.AddAssistantMessage(fullMsg);
                if (!string.IsNullOrEmpty(conversationId))
                {
                    await SaveChatHistoryAsync(conversationId, history).ConfigureAwait(false);
                }

                // Notify that the full message is received
                await _chatBridge!.NotifyMessageReceivedAsync(new ChatMessage { Content = fullMsg, IsUser = false, Timestamp = DateTime.UtcNow });

                _logger?.LogInformation("[XAI] RunAgentToChatBridgeAsync completed via Semantic Kernel streaming - Response length: {Length}", fullMsg.Length);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("[XAI] RunAgentToChatBridgeAsync was canceled");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[XAI] Semantic Kernel streaming failed; attempting fallback to simple HTTP chat");
                try
                {
                    var fallback = await GetSimpleResponse(userRequest, _jarvisPersonality?.GetSystemPrompt() ?? JarvisSystemPrompt).ConfigureAwait(false);
                    await _chatBridge!.NotifyMessageReceivedAsync(new ChatMessage { Content = fallback ?? $"Grok streaming failed: {ex.Message}", IsUser = false, Timestamp = DateTime.UtcNow });
                    _logger?.LogInformation("[XAI] RunAgentToChatBridgeAsync completed via fallback - Response length: {Length}", fallback?.Length ?? 0);
                }
                catch (Exception fallbackEx)
                {
                    _logger?.LogError(fallbackEx, "[XAI] Both Semantic Kernel and fallback failed");
                    await _chatBridge!.NotifyMessageReceivedAsync(new ChatMessage { Content = $"Grok agent error: {ex.Message}", IsUser = false, Timestamp = DateTime.UtcNow });
                }
            }
        }

        /// <summary>
        /// Runs an agentic chat session with Grok using Semantic Kernel's native streaming and automatic function calling.
        /// Uses ToolCallBehavior.AutoInvokeKernelFunctions (SK 1.16.0) to enable automatic function calling.
        /// </summary>
        public async Task<string> RunAgentAsync(string userRequest, string systemPrompt = DefaultArchitectPrompt, Action<string>? onStreamingChunk = null, string? conversationId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogInformation("RunAgentAsync called but no Grok API key configured.");
                return "No API key configured for Grok";
            }

            if (string.IsNullOrWhiteSpace(userRequest))
            {
                _logger?.LogWarning("RunAgentAsync called with empty user request");
                return "User request cannot be empty";
            }

            if (!_isInitialized || _kernel == null)
            {
                _logger?.LogInformation("[XAI] Kernel not initialized; falling back to simple chat");
                return await GetSimpleResponse(userRequest, systemPrompt).ConfigureAwait(false);
            }

            try
            {
                _logger?.LogInformation("[XAI] RunAgentAsync invoked - User request length: {Length}, ConversationId: {ConversationId}", userRequest.Length, conversationId ?? "N/A");

                // Use Semantic Kernel's native streaming with FunctionChoiceBehavior.Auto() (Microsoft Docs recommended pattern)
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();

                // Configure execution settings with automatic function calling
                var settings = new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0.3,
                    MaxTokens = 4000
                };

                // Only add penalties for non-reasoning models
                if (!IsReasoningModel(_model))
                {
                    if (_defaultPresencePenalty.HasValue) settings.PresencePenalty = _defaultPresencePenalty.Value;
                    if (_defaultFrequencyPenalty.HasValue) settings.FrequencyPenalty = _defaultFrequencyPenalty.Value;
                }

                // Load existing conversation history if provided
                var history = await LoadChatHistoryAsync(conversationId, systemPrompt).ConfigureAwait(false);
                history.AddUserMessage(userRequest);

                _logger?.LogDebug("[XAI] Invoking streaming chat with ToolCallBehavior.AutoInvokeKernelFunctions - Plugins: {Count}", _kernel.Plugins.Count);

                // Use Semantic Kernel's native streaming (handles SSE and function calling automatically)
                var responseBuilder = new StringBuilder();

                await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                    history,
                    executionSettings: settings,
                    kernel: _kernel).ConfigureAwait(false))
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        responseBuilder.Append(chunk.Content);
                        onStreamingChunk?.Invoke(chunk.Content);
                    }

                    // Log function calls for observability
                    if (chunk.Metadata?.TryGetValue("FunctionCall", out var functionCall) == true)
                    {
                        _logger?.LogInformation("[XAI] Function called: {FunctionCall}", functionCall);
                    }
                }

                var result = responseBuilder.ToString();

                if (string.IsNullOrWhiteSpace(result))
                {
                    _logger?.LogWarning("[XAI] Empty response from streaming chat; attempting fallback");
                    return await GetSimpleResponse(userRequest, systemPrompt).ConfigureAwait(false);
                }

                // Add assistant response and persist history
                history.AddAssistantMessage(result);
                if (!string.IsNullOrEmpty(conversationId))
                {
                    await SaveChatHistoryAsync(conversationId, history).ConfigureAwait(false);
                }

                _logger?.LogInformation("[XAI] RunAgentAsync completed via Semantic Kernel streaming - Response length: {Length}", result.Length);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("[XAI] RunAgentAsync was canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[XAI] Semantic Kernel streaming failed; attempting fallback to simple HTTP chat");
                try
                {
                    var fallback = await GetSimpleResponse(userRequest, systemPrompt).ConfigureAwait(false);
                    _logger?.LogInformation("[XAI] RunAgentAsync completed via fallback - Response length: {Length}", fallback?.Length ?? 0);
                    return fallback ?? $"Grok streaming failed: {ex.Message}";
                }
                catch (Exception fallbackEx)
                {
                    _logger?.LogError(fallbackEx, "[XAI] Both Semantic Kernel and fallback failed");
                    return $"Grok agent error: {ex.Message}";
                }
            }
        }

        private async Task<ChatHistory> LoadChatHistoryAsync(string? conversationId, string systemPrompt, CancellationToken cancellationToken = default)
        {
            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt);

            if (string.IsNullOrWhiteSpace(conversationId))
            {
                return history;
            }

            try
            {
                // L1 cache: check in-memory cache first (if available)
                var cacheKey = $"{ChatHistoryCacheKeyPrefix}{conversationId}";
                if (_memoryCache != null && _memoryCache.TryGetValue(cacheKey, out ChatHistory? cachedHistory))
                {
                    if (cachedHistory != null)
                    {
                        _logger?.LogDebug("[XAI] Loaded chat history from L1 memory cache for {ConversationId}", conversationId);
                        // Restore system message from current context
                        cachedHistory.AddSystemMessage(systemPrompt);
                        return cachedHistory;
                    }
                }

                using var scope = _serviceProvider?.CreateScope();
                var repo = scope != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConversationRepository>(scope.ServiceProvider) : null;
                if (repo == null) return history;

                var conversationObj = await repo.GetConversationAsync(conversationId).ConfigureAwait(false);
                if (conversationObj is ConversationHistory legacyHistory && !string.IsNullOrWhiteSpace(legacyHistory.MessagesJson))
                {
                    var messages = JsonSerializer.Deserialize<List<PersistentChatMessage>>(legacyHistory.MessagesJson);
                    if (messages != null)
                    {
                        foreach (var m in messages)
                        {
                            if (string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                                history.AddUserMessage(m.Content);
                            else if (string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                                history.AddAssistantMessage(m.Content);
                        }
                        _logger?.LogDebug("[XAI] Loaded {Count} messages for conversation {ConversationId} from database", messages.Count, conversationId);

                        // Cache in L1 for next 30 minutes
                        if (_memoryCache != null)
                        {
                            _memoryCache.Set(cacheKey, history, TimeSpan.FromMinutes(ChatHistoryCacheDurationMinutes));
                            _logger?.LogDebug("[XAI] Cached chat history for {ConversationId} in L1 memory cache", conversationId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[XAI] Failed to load chat history for {ConversationId}", conversationId);
            }

            return history;
        }

        private async Task SaveChatHistoryAsync(string conversationId, ChatHistory history, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) return;

            try
            {
                using var scope = _serviceProvider?.CreateScope();
                var repo = scope != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConversationRepository>(scope.ServiceProvider) : null;
                if (repo == null) return;

                var messages = history
                    .Where(m => m.Role == AuthorRole.User || m.Role == AuthorRole.Assistant)
                    .Select(m => new PersistentChatMessage
                    {
                        Role = m.Role.ToString().ToLowerInvariant(),
                        Content = m.Content ?? ""
                    }).ToList();

                var json = JsonSerializer.Serialize(messages);
                var conversation = new ConversationHistory
                {
                    ConversationId = conversationId,
                    MessagesJson = json,
                    MessageCount = messages.Count,
                    UpdatedAt = DateTime.UtcNow,
                    Title = history.LastOrDefault(m => m.Role == AuthorRole.User)?.Content?.Substring(0, Math.Min(50, history.LastOrDefault(m => m.Role == AuthorRole.User)?.Content?.Length ?? 0)) ?? "Chat Session"
                };

                await repo.SaveConversationAsync(conversation).ConfigureAwait(false);
                _logger?.LogDebug("[XAI] Saved conversation {ConversationId} with {Count} messages", conversationId, messages.Count);

                // Invalidate L1 cache after persistence
                if (_memoryCache != null)
                {
                    var cacheKey = $"{ChatHistoryCacheKeyPrefix}{conversationId}";
                    _memoryCache.Remove(cacheKey);
                    _logger?.LogDebug("[XAI] Invalidated L1 cache for {ConversationId}", conversationId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[XAI] Failed to save chat history for {ConversationId}", conversationId);
            }
        }

        private class PersistentChatMessage
        {
            public string Role { get; set; } = "";
            public string Content { get; set; } = "";
        }

        internal static (string? Value, string Source) TryGetEnvironmentScopedApiKey(Func<EnvironmentVariableTarget, string?>? getter = null)
        {
            getter ??= EnvironmentVariableGetterOverride ?? (target => Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable, target));

            foreach (var (target, source) in ApiKeyEnvironmentTargets)
            {
                try
                {
                    var value = getter(target);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return (value, source);
                    }
                }
                catch
                {
                    // Swallow exceptions coming from Environment.* when permissions are restricted
                }
            }

            return (null, "none");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Safely cancel and dispose the CancellationTokenSource
                    try
                    {
                        if (_serviceCts != null && !_serviceCts.IsCancellationRequested)
                        {
                            _serviceCts.Cancel();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Token source already disposed - this is fine during cleanup
                    }
                    finally
                    {
                        _serviceCts?.Dispose();
                    }

                    if (_chatBridge != null)
                    {
                        _chatBridge.PromptSubmitted -= OnChatPromptSubmitted;
                    }

                    if (_ownsHttpClient)
                    {
                        _httpClient.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        // IAIService implementation
        public async Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default)
        {
            var prompt = $"Context: {context}\nQuestion: {question}\nProvide insights.";
            return await GetSimpleResponse(prompt, ct: cancellationToken);
        }

        public async Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default)
        {
            var prompt = $"Data: {data}\nAnalysis Type: {analysisType}\nAnalyze the data.";
            return await GetSimpleResponse(prompt, ct: cancellationToken);
        }

        public async Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default)
        {
            var prompt = $"Area: {areaName}\nCurrent State: {currentState}\nReview and provide recommendations.";
            return await GetSimpleResponse(prompt, ct: cancellationToken);
        }

        public async Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default)
        {
            var prompt = $"Data Type: {dataType}\nRequirements: {requirements}\nGenerate mock data suggestions.";
            return await GetSimpleResponse(prompt, ct: cancellationToken);
        }

        public async Task<AIResponseResult> GetInsightsWithStatusAsync(string context, string question, CancellationToken cancellationToken = default)
        {
            try
            {
                var content = await GetInsightsAsync(context, question, cancellationToken);
                return new AIResponseResult(content);
            }
            catch (Exception ex)
            {
                return new AIResponseResult("", 500, "Error", ex.Message);
            }
        }

        public async Task<AIResponseResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            var originalKey = _apiKey;
            _apiKey = apiKey;
            try
            {
                var (success, message) = await ValidateApiKeyAsync(cancellationToken);
                return new AIResponseResult(message, success ? 200 : 401, success ? null : "InvalidApiKey");
            }
            finally
            {
                _apiKey = originalKey;
            }
        }

        public async Task UpdateApiKeyAsync(string newApiKey, CancellationToken cancellationToken = default)
        {
            // Note: This is a simple implementation; in a real scenario, you might need to update configuration
            _apiKey = newApiKey;
            // Reinitialize if needed
            await InitializeAsync(cancellationToken);
        }

        public async Task<AIResponseResult> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            try
            {
                var content = await GetSimpleResponse(prompt, ct: cancellationToken);
                return new AIResponseResult(content);
            }
            catch (Exception ex)
            {
                return new AIResponseResult("", 500, "Error", ex.Message);
            }
        }

        public async IAsyncEnumerable<string> StreamResponseAsync(string prompt, string? systemMessage = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                yield return "No API key configured for Grok";
                yield break;
            }

            if (!_isInitialized || _kernel == null)
            {
                yield return "Grok service not initialized";
                yield break;
            }

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            if (!string.IsNullOrWhiteSpace(systemMessage))
            {
                history.AddSystemMessage(systemMessage);
            }
            history.AddUserMessage(prompt);

            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.3,
                MaxTokens = 4000
            };

            await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                history,
                executionSettings: settings,
                kernel: _kernel,
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    yield return chunk.Content;
                }
            }
        }

        public async Task<string> SendMessageAsync(string message, object conversationHistory, CancellationToken cancellationToken = default)
        {
            // Simple implementation; conversation history is ignored for now
            return await GetSimpleResponse(message, ct: cancellationToken);
        }
    }
}
