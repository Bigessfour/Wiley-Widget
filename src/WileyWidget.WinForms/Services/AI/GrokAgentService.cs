using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Reflection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using WileyWidget.Abstractions;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Services.AI
{
    /// <summary>
    /// Provides a thin Semantic Kernel-backed client for Grok (xAI).
    /// - Registers a Semantic Kernel chat completion connector when an API key is present.
    /// - Exposes a simple HTTP fallback to exercise the Grok endpoint for basic tests.
    /// - Defers heavy Semantic Kernel initialization to async initialization phase.
    /// </summary>
    public class GrokAgentService : IAsyncInitializable
    {
        private Kernel? _kernel;
        private readonly IXaiModelDiscoveryService? _modelDiscoveryService;
        private readonly string? _apiKey;
        private readonly Uri? _baseEndpoint;
        private readonly Uri? _endpoint;
        private string _model;
        private readonly bool _autoSelectModelOnStartup;
        private readonly ILogger<GrokAgentService>? _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory? _httpClientFactory;
        private readonly IChatBridgeService? _chatBridge;
        private readonly double? _defaultPresencePenalty;
        private readonly double? _defaultFrequencyPenalty;
        private bool _isInitialized = false;
        private bool _initializationFailed = false;

        private const string ChatCompletionSuffix = "/chat/completions";
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

        public GrokAgentService(IConfiguration config, ILogger<GrokAgentService>? logger = null, IHttpClientFactory? httpClientFactory = null, IXaiModelDiscoveryService? modelDiscoveryService = null, IChatBridgeService? chatBridge = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
            _modelDiscoveryService = modelDiscoveryService;
            _chatBridge = chatBridge;

            // Subscribe to chat bridge events if available
            if (_chatBridge != null)
            {
                _chatBridge.PromptSubmitted += OnChatPromptSubmitted;
                _logger?.LogInformation("[XAI] ChatBridgeService subscribed for prompt events");
            }

            // Read API key candidates
            var configApiKey = config["Grok:ApiKey"] ?? config["XAI:ApiKey"] ?? config[ApiKeyEnvironmentVariable];
            var (envApiKey, envSource) = TryGetEnvironmentScopedApiKey();
            var selectedKey = configApiKey;
            var selectedSource = "config";

            if (!string.IsNullOrWhiteSpace(envApiKey))
            {
                if (string.IsNullOrWhiteSpace(configApiKey))
                {
                    selectedKey = envApiKey;
                    selectedSource = envSource;
                }
                else if (envApiKey.Length != configApiKey.Length)
                {
                    _logger?.LogWarning("XAI API key length mismatch: config={ConfigLength}, env={EnvLength}. Using env value from {EnvSource}.", configApiKey.Length, envApiKey.Length, envSource);
                    selectedKey = envApiKey;
                    selectedSource = envSource;
                }
                else
                {
                    _logger?.LogDebug("XAI API key lengths match between config and env ({Length}).", envApiKey.Length);
                }
            }

            _logger?.LogInformation("[XAI] Environment variable length: {EnvLength}, Config API key length: {ConfigLength}", envApiKey?.Length ?? 0, configApiKey?.Length ?? 0);
            _logger?.LogInformation("[XAI] Using API key from {Source}", selectedSource);

            _apiKey = string.IsNullOrWhiteSpace(selectedKey) ? null : selectedKey.Trim().Trim('"');

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
            _endpoint = new Uri(_baseEndpoint, "chat/completions");

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
            logger?.LogInformation("[XAI] Using model={Model}, endpoint={Endpoint}", _model, _endpoint);

            // Initialize HttpClient early (lightweight, non-blocking)
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
                        using var ctsAuto = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
                    cancellationToken.ThrowIfCancellationRequested();

                    var builder = Kernel.CreateBuilder();

                    if (!string.IsNullOrWhiteSpace(_apiKey))
                    {
                        try
                        {
                            // Use the OpenAI-compatible connector to target xAI's Grok endpoint
                            _logger?.LogInformation("[XAI] Configuring Grok chat completion: model={Model}, endpoint={Endpoint}, apiKeyLength={KeyLength}",
                                _model, _endpoint, _apiKey.Length);
#pragma warning disable SKEXP0010
                            builder.AddOpenAIChatCompletion(modelId: _model, apiKey: _apiKey, endpoint: _endpoint!);
#pragma warning restore SKEXP0010
                            _logger?.LogInformation("[XAI] Semantic Kernel configured with Grok chat completion successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "[XAI] Failed to configure Semantic Kernel chat completion. Falling back to direct HTTP calls.");
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("[XAI] Grok API key not configured; Semantic Kernel chat connector will not be registered.");
                    }

                    _kernel = builder.Build();

                    // Auto-register kernel plugins discovered in the executing assembly (types with [KernelFunction]).
                    // Keeping this scoped avoids scanning unrelated testhost/runtime assemblies.
                    try
                    {
                        var assemblyToScan = typeof(GrokAgentService).Assembly;
                        KernelPluginRegistrar.ImportPluginsFromAssemblies(_kernel, new[] { assemblyToScan }, _logger);
                        _logger?.LogDebug("[XAI] Auto-registered kernel plugins from assembly: {Assembly}", assemblyToScan.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[XAI] Failed to auto-register kernel plugins from executing assembly.");
                    }
                }, cancellationToken);

                _isInitialized = true;
                _logger?.LogInformation("[XAI] Grok service async initialization complete");
                _logger?.LogInformation("[XAI] GrokAgentService kernel initialized successfully - Plugins registered: {Count}", _kernel?.Plugins.Count ?? 0);

                // Optional: run a quick validation if configured
                var validateOnStartupStr = _config["XAI:ValidateOnStartup"] ?? _config["Grok:ValidateOnStartup"];
                if (bool.TryParse(validateOnStartupStr, out var validateOnStartup) && validateOnStartup && !string.IsNullOrWhiteSpace(_apiKey))
                {
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
        /// Get a streaming response using direct HTTP with SSE (Server-Sent Events).
        /// Returns complete response after streaming all chunks.
        /// </summary>
        public async Task<string> GetStreamingResponseAsync(string userMessage, string? systemPrompt = null, string? modelOverride = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogInformation("GetStreamingResponseAsync called but no Grok API key configured.");
                return "No API key configured for Grok";
            }

            var model = modelOverride ?? _model ?? "grok-4";
            var sysPrompt = systemPrompt ?? "You are a helpful assistant.";

            // Use OpenAI-compatible chat call to xAI (Grok) endpoint with streaming enabled
            var messagesArray = new[]
            {
                new { role = "system", content = sysPrompt },
                new { role = "user", content = userMessage }
            };

            var payload = CreateChatRequestPayload(model, messagesArray, stream: true, temperature: 0.7);
            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // SSE streaming: request Accept header should be text/event-stream per x.ai docs
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            try
            {
                _logger?.LogDebug("[XAI] GetStreamingResponseAsync -> POST {Url} with streaming=true (model={Model})", _endpoint, model);
                var resp = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Grok streaming API returned non-success status: {Status}", resp.StatusCode);
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    // Parse error details and log full message for diagnostics
                    try
                    {
                        using var errDoc = JsonDocument.Parse(body);
                        var errMsg = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() :
                                     errDoc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(errMsg))
                        {
                            _logger?.LogWarning("Grok streaming error detail: {ErrorDetail}", errMsg);
                        }
                    }
                    catch (JsonException)
                    {
                        _logger?.LogDebug("GetStreamingResponseAsync: failed to parse error JSON for diagnostics");
                    }
                    return $"Grok API error {resp.StatusCode}: {body}";
                }

                // Read streaming response chunks (SSE format)
                var responseBuilder = new StringBuilder();
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
                            var choices = doc.RootElement.GetProperty("choices");
                            if (choices.GetArrayLength() > 0)
                            {
                                var delta = choices[0].GetProperty("delta");
                                if (delta.TryGetProperty("content", out var contentElem))
                                {
                                    var contentText = contentElem.GetString();
                                    if (!string.IsNullOrEmpty(contentText))
                                    {
                                        responseBuilder.Append(contentText);
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
                _logger?.LogDebug("Streaming completed: {Length} chars", fullResponse.Length);
                return fullResponse;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("GetStreamingResponseAsync canceled by token");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Grok streaming request failed");
                return $"Grok streaming failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Simple chat helper used for smoke tests and quick interactions.
        /// If no API key is configured this returns a clear diagnostic string.
        /// Supports providing an optional system prompt and model override and accepts a cancellation token.
        /// </summary>
        /// <summary>
        /// Creates a request payload for the x.ai OpenAI-compatible /v1/chat/completions API.
        /// Per x.ai spec: stream=true enables SSE (Server-Sent Events) format.
        /// Tool support via tools array and tool_choice parameter (auto|required|specific tool).
        /// </summary>
        private object CreateChatRequestPayload(string model, object[] messages, bool stream = false, double temperature = 0.3, object? tools = null, string? toolChoice = null)
        {
            // CRITICAL FIX: Only include tool_choice when tools are provided
            // Grok API rejects requests with tool_choice but no tools array
            if (tools != null)
            {
                var payloadWithTools = new
                {
                    model,
                    messages,
                    stream,
                    temperature,
                    tools,
                    tool_choice = toolChoice ?? "auto"
                };
                return payloadWithTools;
            }
            else
            {
                var payloadNoTools = new
                {
                    model,
                    messages,
                    stream,
                    temperature
                };
                return payloadNoTools;
            }
        }

        public async Task<string> GetSimpleResponse(string userMessage, string? systemPrompt = null, string? modelOverride = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogInformation("GetSimpleResponse called but no Grok API key configured.");
                return "No API key configured for Grok";
            }

            var model = modelOverride ?? _model ?? "grok-4";
            var sysPrompt = systemPrompt ?? "You are a test assistant.";

            // Use a lightweight OpenAI-compatible chat call to the xAI (Grok) endpoint as a fallback.
            var messagesArray = new[] {
                new { role = "system", content = sysPrompt },
                new { role = "user", content = userMessage }
            };
            var payload = CreateChatRequestPayload(model, messagesArray, stream: false, temperature: 0);
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                _logger?.LogDebug("[XAI] GetSimpleResponse -> POST {Url} (model={Model})", _endpoint, model);
                var resp = await _httpClient.PostAsync(_endpoint, content, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Grok API returned non-success status: {Status}", resp.StatusCode);
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

                    return $"Grok API returned HTTP {resp.StatusCode}: {body}";
                }

                var respStr = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                try
                {
                    using var doc = JsonDocument.Parse(respStr);
                    var contentElem = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content");
                    var text = contentElem.GetString();
                    return text ?? "No content";
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse Grok response; returning raw payload");
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
        /// Validate the configured API key by performing an exact chat/completions request similar to the curl activation test.
        /// Returns a tuple of (success, responseMessage).
        /// </summary>
        public async Task<(bool Success, string Message)> ValidateApiKeyAsync(CancellationToken ct = default, string? modelOverride = null)
        {
            _logger?.LogInformation("Validating XAI API key via chat/completions endpoint");

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogWarning("ValidateApiKeyAsync called but no API key configured.");
                return (false, "No API key configured");
            }

            var model = modelOverride ?? _model ?? "grok-4";
            var requestObj = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "You are a test assistant." },
                    new { role = "user", content = "Testing. Just say hi and hello world and nothing else." }
                },
                stream = false,
                temperature = 0
            };

            var json = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                using var resp = await _httpClient.PostAsync(_endpoint, content, ct).ConfigureAwait(false);
                var respBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("ValidateApiKeyAsync: Grok API returned non-success status: {Status}", resp.StatusCode);
                    // Try to parse structured error for better diagnostics
                    try
                    {
                        using var errDoc = JsonDocument.Parse(respBody);
                        var errMsg = errDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() :
                                     errDoc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(errMsg))
                        {
                            _logger?.LogWarning("Grok API error detail: {ErrorDetail}", errMsg);
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
                var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
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
                else
                {
                    _logger?.LogWarning("ValidateApiKeyAsync: Response had no content: {Resp}", respBody);
                    return (false, respBody);
                }
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

        // Helper: Detect whether a model ID refers to a reasoning model (these may not accept penalties)
        private bool IsReasoningModel(string model)
        {
            return !string.IsNullOrWhiteSpace(model) && model.IndexOf("reason", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Build chat request payload as a dictionary to ensure correct snake_case keys for x.ai API
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

        /// <summary>
        /// Event handler for chat bridge prompt submissions.
        /// Routes prompts from Blazor through the agent and streams responses back via the bridge.
        /// </summary>
        private void OnChatPromptSubmitted(object? sender, ChatPromptSubmittedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e?.Prompt))
            {
                _logger?.LogWarning("[XAI] Chat bridge received empty prompt");
                return;
            }

            _logger?.LogInformation("[XAI] Chat bridge prompt received: {PromptLength} chars", e.Prompt.Length);

            // Fire-and-forget: route through agent and stream back to bridge
            _ = RunAgentToChatBridgeAsync(e.Prompt);
        }

        /// <summary>
        /// Runs an agentic chat session with streaming via ChatBridgeService.
        /// Sends chunks back to Blazor as they arrive.
        /// Sends initial "JARVIS is thinking..." message.
        /// </summary>
        private async Task RunAgentToChatBridgeAsync(string userRequest)
        {
            try
            {
                // Send initial thinking message via bridge
                await _chatBridge!.SendResponseChunkAsync("JARVIS is thinking...");

                // Run agent with JARVIS personality + streaming callback that sends chunks to bridge
                await RunAgentAsync(userRequest, JarvisSystemPrompt, chunk =>
                {
                    // Send each chunk back to bridge (fire-and-forget)
                    _ = _chatBridge!.SendResponseChunkAsync(chunk);
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[XAI] Error in chat bridge agent loop");
                await _chatBridge!.SendResponseChunkAsync($"[Error: {ex.Message}]");
            }
        }

        /// <summary>
        /// Runs an agentic chat session with Grok using streaming via x.ai's OpenAI-compatible API.
        /// The default system prompt makes the agent act as a senior Syncfusion WinForms architect
        /// that enforces SfSkinManager theming rules, MVVM patterns, and project conventions.
        /// If no Grok API key is configured the method returns a clear diagnostic string.
        /// The optional onStreamingChunk callback enables UI to display streaming progress during execution.
        /// Supports tool calling via the tools parameter per x.ai API specification.
        /// </summary>
        public async Task<string> RunAgentAsync(string userRequest, string systemPrompt = DefaultArchitectPrompt, Action<string>? onStreamingChunk = null)
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

            try
            {
                _logger?.LogInformation("[XAI] RunAgentAsync invoked - User request length: {Length}", userRequest.Length);

                // Use OpenAI-compatible streaming with system prompt
                // Per x.ai API spec: /v1/chat/completions with stream=true enables streaming responses
                var model = _model ?? "grok-4";
                var messagesArray = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userRequest }
                };

                var payload = CreateChatRequestPayload(model, messagesArray, stream: true, temperature: 0.3);
                var json = JsonSerializer.Serialize(payload);

                using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                try
                {
                    _logger?.LogDebug("[XAI] RunAgentAsync -> POST {Url} with streaming=true (model={Model})", _endpoint, model);
                    var resp = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger?.LogWarning("[XAI] Grok streaming API returned non-success status: {Status}", resp.StatusCode);
                        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return $"Grok API error {resp.StatusCode}: {body}";
                    }

                    // Read streaming response chunks (SSE format per x.ai spec)
                    var responseBuilder = new StringBuilder();
                    var responseStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using var reader = new System.IO.StreamReader(responseStream);

                    while (true)
                    {
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null)
                            break;

                        // SSE format: "data: {json}"
                        if (line.StartsWith("data: ", StringComparison.Ordinal))
                        {
                            var dataJson = line.Substring(6);

                            if (dataJson == "[DONE]")
                            {
                                _logger?.LogDebug("[XAI] Stream ended with [DONE] marker");
                                break;
                            }

                            try
                            {
                                using var doc = JsonDocument.Parse(dataJson);
                                var choices = doc.RootElement.GetProperty("choices");
                                if (choices.GetArrayLength() > 0)
                                {
                                    var delta = choices[0].GetProperty("delta");

                                    // Check for tool calls per x.ai API specification
                                    if (delta.TryGetProperty("tool_calls", out var toolCallsElem))
                                    {
                                        _logger?.LogInformation("[XAI] Tool calls detected in response");
                                        responseBuilder.Append("```\nTool Calls:\n");
                                        responseBuilder.Append(toolCallsElem.GetRawText());
                                        responseBuilder.Append("\n```\n");
                                    }

                                    // Extract content delta for streaming text
                                    if (delta.TryGetProperty("content", out var contentElem))
                                    {
                                        var contentText = contentElem.GetString();
                                        if (!string.IsNullOrEmpty(contentText))
                                        {
                                            responseBuilder.Append(contentText);
                                            onStreamingChunk?.Invoke(contentText);
                                        }
                                    }
                                }
                            }
                            catch (JsonException ex)
                            {
                                _logger?.LogWarning(ex, "[XAI] Failed to parse SSE chunk: {Data}", dataJson);
                            }
                        }
                    }

                    var result = responseBuilder.ToString();
                    _logger?.LogInformation("[XAI] RunAgentAsync completed via streaming - Response length: {Length}", result.Length);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogWarning("[XAI] RunAgentAsync streaming was canceled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[XAI] Streaming failed; attempting fallback to simple HTTP chat");
                    var fallback = await GetSimpleResponse(userRequest, systemPrompt).ConfigureAwait(false);
                    _logger?.LogInformation("[XAI] RunAgentAsync completed via fallback - Response length: {Length}", fallback?.Length ?? 0);
                    return fallback ?? $"Grok streaming failed: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[XAI] RunAgentAsync failed completely");
                return $"Grok agent error: {ex.Message}";
            }
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
    }
}
