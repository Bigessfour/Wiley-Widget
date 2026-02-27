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
using WileyWidget.WinForms.Services.AI.XAI;

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
        // High-performance LoggerMessage delegates (CA1848 fix)
        private static readonly Action<ILogger, string, Exception?> LogApiKeyValidationFailed =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(1001, nameof(LogApiKeyValidationFailed)),
                "[XAI] API key validation failed: {Message}");

        // API Key Retrieval Logging
        private static readonly Action<ILogger, string, int, bool, Exception?> LogApiKeyRetrievedFromSource =
            LoggerMessage.Define<string, int, bool>(
                LogLevel.Information,
                new EventId(1002, nameof(LogApiKeyRetrievedFromSource)),
                "[XAI] API key retrieved from {Source} (length: {Length}, validated: {IsValidated})");

        private static readonly Action<ILogger, string, Exception?> LogApiKeyRetrievalFailed =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(1003, nameof(LogApiKeyRetrievalFailed)),
                "[XAI] API key retrieval failed: {Message}");

        private static readonly Action<ILogger, string, int, Exception?> LogApiKeyPreview =
            LoggerMessage.Define<string, int>(
                LogLevel.Debug,
                new EventId(1004, nameof(LogApiKeyPreview)),
                "[XAI] API key preview: {Preview} (length {Length})");

        private static readonly Action<ILogger, string, Exception?> LogApiKeyNotFound =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(1005, nameof(LogApiKeyNotFound)),
                "[XAI] API key NOT found in configuration/environment. Checked sources: {Sources}");

        // HTTP Request/Response Logging
        private static readonly Action<ILogger, string, string, Exception?> LogHttpRequestStarted =
            LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                new EventId(2001, nameof(LogHttpRequestStarted)),
                "[XAI] HTTP {Method} {Endpoint} - request headers configured");

        private static readonly Action<ILogger, string, int, Exception?> LogHttpResponseStatus =
            LoggerMessage.Define<string, int>(
                LogLevel.Information,
                new EventId(2002, nameof(LogHttpResponseStatus)),
                "[XAI] HTTP response from {Endpoint}: {StatusCode}");

        private static readonly Action<ILogger, string, string, string, Exception?> LogHttpRequestHeaders =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Trace,
                new EventId(2003, nameof(LogHttpRequestHeaders)),
                "[XAI] HTTP {Method} {Endpoint}: Content-Type=application/json, Authorization=Bearer ***masked***, Accept={Accept}");
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
        private bool _skConnectorDisabled = false;  // Track if SK connector registration was disabled
        private readonly CancellationTokenSource _serviceCts = new();
        private bool _disposed = false;
        private readonly bool _ownsHttpClient;
        private readonly XAIBuiltInTools.XAIToolConfiguration? _toolConfiguration;  // xAI built-in tools configuration
        private readonly bool TestMode;  // Test mode flag for bypassing heavy initialization
        private const string ChatHistoryCacheKeyPrefix = "grok_chat_history_";
        private const int ChatHistoryCacheDurationMinutes = 30;

        private const string ResponsesEndpointSuffix = "responses";
        private const string ChatCompletionSuffix = "/chat/completions"; // Legacy, deprecated

        // API Key environment variables (per Microsoft configuration conventions):
        // - XAI__ApiKey (recommended, double underscore - maps to XAI:ApiKey in configuration)
        // - XAI_API_KEY (legacy single underscore - still supported for backward compatibility)
        private const string ApiKeyEnvironmentVariable = "XAI_API_KEY";
        private const string ApiKeyHierarchicalEnvironmentVariable = "XAI__ApiKey";
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
            if (config == null) throw new ArgumentNullException(nameof(config), "[XAI] Configuration is required for GrokAgentService");
            if (apiKeyProvider == null) throw new ArgumentNullException(nameof(apiKeyProvider), "[XAI] IGrokApiKeyProvider is required for GrokAgentService");

            _apiKeyProvider = apiKeyProvider;  // ✅ Store provider reference
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
            _modelDiscoveryService = modelDiscoveryService;
            _chatBridge = chatBridge;
            _serviceProvider = serviceProvider;
            _jarvisPersonality = jarvisPersonality;
            _memoryCache = memoryCache;

            // Initialize test mode flag
            TestMode = IsTruthy(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS")) ||
                       IsTruthy(Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS"));

            static bool IsTruthy(string? value) =>
                !string.IsNullOrWhiteSpace(value) &&
                (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));

            // Load xAI tools configuration
            _toolConfiguration = LoadToolConfiguration(config, logger);

            // ✅ FIXED: Use injected IGrokApiKeyProvider instead of reading environment variables directly
            _apiKey = apiKeyProvider.ApiKey;  // Get API key from centralized provider
            var source = apiKeyProvider.GetConfigurationSource();
            if (_logger != null)
            {
                LogApiKeyRetrievedFromSource(_logger, source, _apiKey?.Length ?? 0, apiKeyProvider.IsValidated, null);
            }

            // Log API key preview (masked) for diagnostics
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                var preview = _apiKey.Length > 8 ? _apiKey.Substring(0, 4) + "..." + _apiKey.Substring(_apiKey.Length - 4) : _apiKey;
                if (_logger != null)
                {
                    LogApiKeyPreview(_logger, preview, _apiKey.Length, null);
                }
            }

            _model = config["Grok:Model"] ?? config["XAI:Model"] ?? "grok-4-1-fast-reasoning";

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

            // Validate endpoint URI
            if (!Uri.TryCreate(endpointStr, UriKind.Absolute, out var _))
            {
                throw new ArgumentException($"[XAI] Invalid endpoint URI configured: {endpointStr}", nameof(config));
            }

            var baseEndpointCandidate = endpointStr.TrimEnd('/');
            if (baseEndpointCandidate.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
            {
                baseEndpointCandidate = baseEndpointCandidate.Substring(0, baseEndpointCandidate.Length - "/responses".Length);
            }
            if (baseEndpointCandidate.EndsWith(ChatCompletionSuffix, StringComparison.OrdinalIgnoreCase))
            {
                baseEndpointCandidate = baseEndpointCandidate.Substring(0, baseEndpointCandidate.Length - ChatCompletionSuffix.Length);
            }

            baseEndpointCandidate = baseEndpointCandidate.TrimEnd('/');
            var normalizedBase = baseEndpointCandidate + '/';

            // Final validation
            if (!Uri.TryCreate(normalizedBase, UriKind.Absolute, out var validatedBase))
            {
                throw new ArgumentException($"[XAI] Failed to construct valid endpoint URI from: {endpointStr}", nameof(config));
            }

            _baseEndpoint = validatedBase;
            // Use new /responses endpoint instead of deprecated /chat/completions
            _endpoint = new Uri(_baseEndpoint, ResponsesEndpointSuffix); // /v1/responses

            // Additional diagnostics about API key sources checked
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                var sourcesChecked = string.Join(", ", ApiKeyEnvironmentTargets.Select(t => $"{t.Source}"));
                if (_logger != null)
                {
                    LogApiKeyNotFound(_logger, sourcesChecked, null);
                }
            }
            logger?.LogInformation("[XAI] Using model={Model}, endpoint={Endpoint} (NEW /v1/responses API)", _model, _endpoint);

            // Initialize HttpClient early (lightweight, non-blocking)
            _ownsHttpClient = httpClientFactory == null;
            _httpClient = httpClientFactory?.CreateClient("GrokClient") ?? new HttpClient();
            _httpClient.BaseAddress = _baseEndpoint;

            // Set long timeout for reasoning models (streaming can take minutes)
            // WILEY SOCKET FIX: Use 2-minute timeout to prevent debugger-induced socket aborts
            // while still allowing long-running reasoning completions
            var configuredTimeout = TimeSpan.FromSeconds(120); // 2 minutes default
            var timeoutConfig = config["XAI:HttpTimeoutSeconds"] ?? config["Grok:HttpTimeoutSeconds"];
            if (!string.IsNullOrWhiteSpace(timeoutConfig) && int.TryParse(timeoutConfig, out var timeoutSec) && timeoutSec > 0)
            {
                configuredTimeout = TimeSpan.FromSeconds(timeoutSec);
            }
            _httpClient.Timeout = configuredTimeout;
            _logger?.LogDebug("[XAI] HttpClient timeout configured: {Timeout}s", configuredTimeout.TotalSeconds);

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                _logger?.LogDebug("[XAI] Authorization header configured (Bearer token set)");
            }
            else
            {
                _logger?.LogWarning("[XAI] No API key available - Authorization header not configured");
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

            // === TEST MODE BYPASS (eliminates ~1.7s delay in FLAUI runs) ===
            if (TestMode)
            {
                _logger?.LogInformation("🔬 GrokAgentService skipped - TEST MODE");
                _isInitialized = true;
                _kernel = Kernel.CreateBuilder().Build(); // minimal empty kernel
                return;
            }

            try
            {
                _logger?.LogDebug("[XAI] Beginning async initialization of Grok service");

                // Validate injected dependencies
                if (_baseEndpoint == null || _endpoint == null)
                {
                    _logger?.LogError("[XAI] Endpoint configuration is invalid during initialization");
                    _initializationFailed = true;
                    throw new InvalidOperationException("[XAI] Endpoint configuration not properly initialized");
                }

                // Add API key validation before Semantic Kernel setup (with timeout)
                try
                {
                    using var validationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    validationCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10-second timeout for validation

                    _logger?.LogDebug("[XAI] 🔑 Starting API key validation with 10s timeout");
                    var validationStart = System.Diagnostics.Stopwatch.StartNew();

                    var (success, message) = await _apiKeyProvider.ValidateAsync().ConfigureAwait(false);

                    validationStart.Stop();
                    _logger?.LogDebug("[XAI] 🔑 API key validation completed in {ElapsedMs}ms", validationStart.ElapsedMilliseconds);

                    if (!success)
                    {
                        _logger?.LogWarning("[XAI] ❌ API key validation failed: {Message}", message);
                        _initializationFailed = true;
                        // Don't throw - allow service to operate in degraded mode
                        return;
                    }
                    _logger?.LogInformation("[XAI] ✅ API key validated successfully");
                }
                catch (HttpRequestException hex) when (hex.InnerException is System.IO.IOException ioex)
                {
                    _logger?.LogWarning(hex, "[XAI] 🔌❌ Socket error during API key validation - likely debugger interference. Inner: {InnerMsg}. Service will operate in degraded mode.", ioex.Message);
                    _initializationFailed = true;
                    return;
                }
                catch (TaskCanceledException tcex) when (!ct.IsCancellationRequested)
                {
                    _logger?.LogWarning(tcex, "[XAI] ⏱️ API key validation timed out (10s) - check network connectivity");
                    _initializationFailed = true;
                    return;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogWarning("[XAI] 🛑 API key validation cancelled");
                    _initializationFailed = true;
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[XAI] ❌ Unexpected error during API key validation: {ExceptionType}", ex.GetType().Name);
                    _initializationFailed = true;
                    return;
                }

                // Optionally auto-select a model (short timeout) before building the kernel so the kernel uses the selected model
                if (_autoSelectModelOnStartup && !string.IsNullOrWhiteSpace(_apiKey))
                {
                    try
                    {
                        using var ctsAuto = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        ctsAuto.CancelAfter(TimeSpan.FromSeconds(3)); // 3-second timeout for model selection
                        var selected = await AutoSelectModelAsync(ctsAuto.Token).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(selected) && !string.Equals(selected, _model, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.LogInformation("[XAI] Auto-selected model '{Selected}' replacing configured '{Configured}'", selected, _model);
                            _model = selected;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogWarning("[XAI] Auto model selection timed out (3s)");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[XAI] Auto model selection failed, continuing with configured model '{Model}'", _model);
                    }
                }

                // Build Semantic Kernel on background thread (with timeout)
                using var kernelBuildCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                kernelBuildCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30-second timeout for kernel build

                await Task.Run(() =>
                {
                    kernelBuildCts.Token.ThrowIfCancellationRequested();

                    var builder = Kernel.CreateBuilder();

                    var disableSkConnector = bool.TryParse(_config["XAI:DisableSemanticKernelConnector"] ?? _config["Grok:DisableSemanticKernelConnector"], out var tmpDisable) && tmpDisable;
                    _skConnectorDisabled = disableSkConnector;  // Store for runtime checks
                    _logger?.LogInformation("[XAI] SK connector disabled flag set to: {Disabled}", _skConnectorDisabled);

                    if (disableSkConnector)
                    {
                        _logger?.LogInformation("[XAI] Semantic Kernel connector registration DISABLED via config - using HTTP-only mode with /v1/responses endpoint (xAI-native format)");
                    }
                    else if (!string.IsNullOrWhiteSpace(_apiKey))
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

                    try
                    {
                        _kernel = builder.Build() ?? throw new InvalidOperationException("[XAI] Failed to build Semantic Kernel instance");

                        _logger?.LogInformation("[XAI] ✅ Semantic Kernel built successfully - beginning plugin discovery");

                        // Auto-register kernel plugins discovered in the executing assembly (types with [KernelFunction]).
                        // Keeping this scoped avoids scanning unrelated testhost/runtime assemblies.
                        // Pass IServiceProvider to enable DI-aware plugin instantiation (fixes MissingMethodException for plugins with constructor dependencies)
                        try
                        {
                            var assemblyToScan = typeof(GrokAgentService).Assembly;
                            _logger?.LogDebug("[XAI] Scanning assembly for plugins: {Assembly}", assemblyToScan.FullName);

                            // Discover plugin types before registration
                            var pluginTypes = assemblyToScan.GetTypes()
                                .Where(t => t.IsClass && !t.IsAbstract)
                                .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                    .Any(m => m.GetCustomAttributes(inherit: true).Any(a => a.GetType().Name == "KernelFunctionAttribute")))
                                .ToList();

                            _logger?.LogInformation("[XAI] 🔍 Discovered {Count} plugin types in assembly", pluginTypes.Count);
                            foreach (var pluginType in pluginTypes)
                            {
                                _logger?.LogDebug("[XAI]    → Plugin type discovered: {PluginType}", pluginType.FullName);
                            }

                            // Check specifically for CSharpEvaluationPlugin
                            var csharpEvalPlugin = pluginTypes.FirstOrDefault(t => t.Name == "CSharpEvaluationPlugin");
                            if (csharpEvalPlugin != null)
                            {
                                _logger?.LogInformation("[XAI] 🎯 CSharpEvaluationPlugin discovered - JARVIS will have C# evaluation capabilities");
                                var functions = csharpEvalPlugin.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(m => m.GetCustomAttributes(inherit: true).Any(a => a.GetType().Name == "KernelFunctionAttribute"))
                                    .Select(m => m.Name)
                                    .ToList();
                                _logger?.LogDebug("[XAI]    → CSharpEvaluationPlugin functions: {Functions}", string.Join(", ", functions));
                            }
                            else
                            {
                                _logger?.LogWarning("[XAI] ⚠️ CSharpEvaluationPlugin NOT discovered - JARVIS C# eval capabilities unavailable");
                            }

                            // Perform actual registration
                            KernelPluginRegistrar.ImportPluginsFromAssemblies(_kernel, new[] { assemblyToScan }, _logger, _serviceProvider);

                            // Verify registration
                            _logger?.LogInformation("[XAI] ✅ Plugin registration complete - {Count} plugins registered", _kernel.Plugins.Count);
                            foreach (var plugin in _kernel.Plugins)
                            {
                                _logger?.LogDebug("[XAI]    ✓ Plugin registered: {PluginName} ({FunctionCount} functions)",
                                    plugin.Name, plugin.Count());
                            }

                            // Explicitly verify CSharpEvaluationPlugin registration
                            var csharpEvalRegistered = _kernel.Plugins.Any(p => p.Name == "CSharpEvaluationPlugin");
                            if (csharpEvalRegistered)
                            {
                                var plugin = _kernel.Plugins.First(p => p.Name == "CSharpEvaluationPlugin");
                                var functionNames = plugin.Select(f => f.Name).ToList();
                                _logger?.LogInformation("[XAI] ✅ CSharpEvaluationPlugin SUCCESSFULLY REGISTERED - Functions: {Functions}",
                                    string.Join(", ", functionNames));
                            }
                            else
                            {
                                _logger?.LogError("[XAI] ❌ CSharpEvaluationPlugin FAILED TO REGISTER - JARVIS cannot evaluate C# code");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "[XAI] ❌ Failed to auto-register kernel plugins from executing assembly");
                        }
                    }
                    catch (TypeInitializationException tiex)
                    {
                        // Handle Semantic Kernel type initializer failures (e.g., KernelJsonSchemaBuilder)
                        _logger?.LogError(tiex, "[XAI] ✗ CRITICAL: Kernel type initializer failed (likely KernelJsonSchemaBuilder)");
                        if (tiex.InnerException != null)
                        {
                            _logger?.LogError(tiex.InnerException, "[XAI] Inner exception details: {Type}: {Message}",
                                tiex.InnerException.GetType().Name, tiex.InnerException.Message);
                        }
                        _logger?.LogWarning("[XAI] Semantic Kernel initialization failed - service will operate in HTTP-only mode (degraded)");

                        // Allow service to continue in degraded mode - direct HTTP calls will still work
                        _kernel = null;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[XAI] ✗ Unexpected error during kernel build: {Type}: {Message}",
                            ex.GetType().Name, ex.Message);
                        _logger?.LogWarning("[XAI] Semantic Kernel initialization failed - service will operate in HTTP-only mode (degraded)");
                        _kernel = null;
                    }
                }, kernelBuildCts.Token).ConfigureAwait(false);

                _isInitialized = true;
                if (_kernel != null)
                {
                    _logger?.LogInformation("[XAI] Grok service async initialization complete - Semantic Kernel + plugins ready");
                    _logger?.LogInformation("[XAI] GrokAgentService kernel initialized successfully - Plugins registered: {Count}", _kernel.Plugins.Count);
                }
                else
                {
                    _logger?.LogWarning("[XAI] Grok service async initialization complete - Operating in HTTP-only mode (Semantic Kernel unavailable)");
                }

                // Optional: run a quick validation if configured (with timeout)
                var validateOnStartupStr = _config["XAI:ValidateOnStartup"] ?? _config["Grok:ValidateOnStartup"];
                if (bool.TryParse(validateOnStartupStr, out var validateOnStartup) && validateOnStartup && !string.IsNullOrWhiteSpace(_apiKey))
                {
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(TimeSpan.FromSeconds(5)); // 5-second timeout for startup validation
                        var (success, msg) = await ValidateApiKeyAsync(cts.Token).ConfigureAwait(false);
                        if (success)
                            _logger?.LogInformation("[XAI] Async initialization validation: API key OK");
                        else
                            _logger?.LogWarning("[XAI] Async initialization validation: API key check failed: {Msg}", msg);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogWarning("[XAI] Async initialization validation timed out (5s)");
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
                _logger?.LogError(ex, "[XAI] GrokAgentService initialization failed critically: {Message}", ex.Message);
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
        /// Gets a list of registered plugin names and their function counts.
        /// Useful for debugging and verifying plugin registration.
        /// </summary>
        public Dictionary<string, int> GetRegisteredPlugins()
        {
            if (_kernel == null || !_isInitialized)
            {
                return new Dictionary<string, int>();
            }

            return _kernel.Plugins.ToDictionary(p => p.Name, p => p.Count());
        }

        /// <summary>
        /// Gets detailed information about a specific plugin.
        /// Returns null if plugin not found or kernel not initialized.
        /// </summary>
        public IReadOnlyList<string>? GetPluginFunctions(string pluginName)
        {
            if (_kernel == null || !_isInitialized)
            {
                return null;
            }

            var plugin = _kernel.Plugins.FirstOrDefault(p =>
                string.Equals(p.Name, pluginName, StringComparison.OrdinalIgnoreCase));

            return plugin?.Select(f => f.Name).ToList();
        }

        /// <summary>
        /// Checks if the CSharpEvaluationPlugin is registered and available.
        /// Returns true if JARVIS can evaluate C# code, false otherwise.
        /// </summary>
        public bool IsCSharpEvaluationAvailable()
        {
            if (_kernel == null || !_isInitialized)
            {
                return false;
            }

            var hasPlugin = _kernel.Plugins.Any(p => p.Name == "CSharpEvaluationPlugin");

            if (hasPlugin)
            {
                _logger?.LogDebug("[XAI] CSharpEvaluationPlugin availability check: AVAILABLE");
            }
            else
            {
                _logger?.LogDebug("[XAI] CSharpEvaluationPlugin availability check: NOT AVAILABLE");
            }

            return hasPlugin;
        }

        /// <summary>
        /// Gets comprehensive diagnostics about the Semantic Kernel and plugin state.
        /// Useful for troubleshooting and runtime inspection.
        /// </summary>
        public string GetKernelDiagnostics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("🔍 Semantic Kernel Diagnostics:");
            sb.AppendLine();
            sb.AppendLine($"   Initialized: {_isInitialized}");
            sb.AppendLine($"   Initialization Failed: {_initializationFailed}");
            sb.AppendLine($"   Kernel Instance: {(_kernel != null ? "Available" : "Null")}");
            sb.AppendLine($"   SK Connector Disabled: {_skConnectorDisabled}");
            sb.AppendLine();

            if (_kernel != null && _isInitialized)
            {
                sb.AppendLine($"🔌 Registered Plugins ({_kernel.Plugins.Count}):");
                foreach (var plugin in _kernel.Plugins)
                {
                    sb.AppendLine($"   ✓ {plugin.Name} ({plugin.Count()} functions)");
                    foreach (var function in plugin)
                    {
                        sb.AppendLine($"      → {function.Name}");
                    }
                }
                sb.AppendLine();

                sb.AppendLine("🎯 CSharpEvaluationPlugin Status:");
                var csharpEvalAvailable = IsCSharpEvaluationAvailable();
                sb.AppendLine($"   Available: {(csharpEvalAvailable ? "✅ YES" : "❌ NO")}");

                if (csharpEvalAvailable)
                {
                    var functions = GetPluginFunctions("CSharpEvaluationPlugin");
                    if (functions != null)
                    {
                        sb.AppendLine($"   Functions: {string.Join(", ", functions)}");
                    }
                }
            }
            else
            {
                sb.AppendLine("⚠️ Kernel not initialized - no plugin information available");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get a streaming response using /v1/chat/completions endpoint (OpenAI-compatible).
        /// Uses proper Server-Sent Events (SSE) streaming with real-time delta accumulation.
        /// Invokes a callback for each content chunk as it arrives.
        /// Implements exponential backoff retry for 429 (rate limit) responses.
        /// </summary>
        public async Task<string> GetStreamingResponseAsync(string userMessage, string? systemPrompt = null, string? modelOverride = null, Action<string>? onChunk = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogInformation("GetStreamingResponseAsync called but no Grok API key configured.");
                return "No API key configured for Grok";
            }

            if (_baseEndpoint == null)
            {
                _logger?.LogError("[XAI] Base endpoint not properly initialized in GetStreamingResponseAsync");
                return "Grok endpoint not configured";
            }

            var model = modelOverride ?? _model ?? "grok-4-1-fast-reasoning";
            var sysPrompt = systemPrompt ?? "You are a helpful assistant.";

            return await SendStreamingChatCompletionAsync(model, sysPrompt, userMessage, onChunk, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Internal method to send streaming chat completion request using /v1/chat/completions (OpenAI-compatible).
        /// Implements proper SSE parsing with delta accumulation and exponential backoff for rate limits.
        /// </summary>
        private async Task<string> SendStreamingChatCompletionAsync(string model, string systemPrompt, string userMessage, Action<string>? onChunk, CancellationToken ct, int retryCount = 0, int maxRetries = 3)
        {
            // Use /chat/completions for streaming (documented, proven SSE support)
            var streamingEndpoint = new Uri(_baseEndpoint!, "chat/completions");

            // Build messages array for /chat/completions endpoint (OpenAI-compatible format)
            var messagesArray = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            };

            var payload = CreateChatRequestPayload(model, messagesArray, stream: true);
            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, streamingEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            };

            // SSE streaming: request Accept header should be text/event-stream
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            try
            {
                _logger?.LogDebug("[XAI] SendStreamingChatCompletionAsync -> POST {Url} with stream=true (model={Model}, retryCount={RetryCount})", streamingEndpoint, model, retryCount);

                // SOCKET DIAGNOSTIC: Log before HTTP call to track timing
                var requestStartTime = System.Diagnostics.Stopwatch.StartNew();
                _logger?.LogDebug("[XAI] 🔌 Initiating HTTP request to {Endpoint}", streamingEndpoint);

                var resp = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                requestStartTime.Stop();
                _logger?.LogDebug("[XAI] ✅ HTTP request completed in {ElapsedMs}ms", requestStartTime.ElapsedMilliseconds);

                if (_logger != null)
                {
                    LogHttpResponseStatus(_logger, streamingEndpoint.ToString(), (int)resp.StatusCode, null);
                }

                // Handle rate limit with exponential backoff
                if ((int)resp.StatusCode == 429)
                {
                    if (retryCount < maxRetries)
                    {
                        var delayMs = (int)Math.Pow(2, retryCount) * 1000; // 1s, 2s, 4s backoff
                        _logger?.LogWarning("[XAI] Rate limited (429); retry {RetryCount}/{MaxRetries} after {DelayMs}ms", retryCount + 1, maxRetries, delayMs);
                        await Task.Delay(delayMs, ct).ConfigureAwait(false);
                        return await SendStreamingChatCompletionAsync(model, systemPrompt, userMessage, onChunk, ct, retryCount + 1, maxRetries).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger?.LogError("[XAI] Rate limit (429) exceeded max retries ({MaxRetries})", maxRetries);
                        return $"Grok API rate limited (429) - exceeded max retries";
                    }
                }

                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Grok chat/completions API returned non-success status: {Status}", resp.StatusCode);
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    return $"Grok API error {(int)resp.StatusCode} ({resp.StatusCode}): {body}";
                }

                // Read streaming response chunks (SSE format: "data: {json}\n\n")
                var responseBuilder = new StringBuilder();
                var responseStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var reader = new System.IO.StreamReader(responseStream);
                var lineCount = 0;

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (line == null)
                        break; // End of stream

                    lineCount++;
                    _logger?.LogTrace("[XAI SSE] Line {LineNum}: {Line}", lineCount, line.Length > 200 ? line.Substring(0, 200) + "..." : line);

                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // SSE format: "data: {json}"
                    if (line.StartsWith("data: ", StringComparison.Ordinal))
                    {
                        var dataJson = line.Substring(6); // Remove "data: " prefix

                        if (dataJson == "[DONE]")
                        {
                            _logger?.LogDebug("[XAI SSE] Stream ended with [DONE] marker");
                            break; // Stream complete
                        }

                        try
                        {
                            using var doc = JsonDocument.Parse(dataJson);

                            // Standard OpenAI/X.ai streaming format: choices[0].delta.content
                            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                                choices.ValueKind == JsonValueKind.Array &&
                                choices.GetArrayLength() > 0)
                            {
                                var firstChoice = choices[0];
                                if (firstChoice.TryGetProperty("delta", out var delta) &&
                                    delta.TryGetProperty("content", out var contentElem))
                                {
                                    var contentText = contentElem.GetString();
                                    if (!string.IsNullOrEmpty(contentText))
                                    {
                                        responseBuilder.Append(contentText);
                                        onChunk?.Invoke(contentText);
                                    }
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger?.LogWarning(ex, "[XAI SSE] Failed to parse SSE chunk: {Data}", dataJson.Length > 100 ? dataJson.Substring(0, 100) + "..." : dataJson);
                        }
                    }
                }

                var fullResponse = responseBuilder.ToString();
                _logger?.LogDebug("[XAI SSE] Streaming completed: {Length} chars, Lines read: {LineCount}", fullResponse.Length, lineCount);

                return fullResponse;
            }
            catch (HttpRequestException hex) when (hex.InnerException is System.IO.IOException ioex && ioex.Message.Contains("aborted"))
            {
                // SOCKET ABORT: Connection closed mid-flight (likely debugger, HttpClient disposal, or OS socket timeout)
                _logger?.LogWarning(hex, "[XAI] 🔌❌ Socket connection aborted during Grok streaming - likely debugger interference or HttpClient lifetime issue. Inner: {InnerMsg}", ioex.Message);

                // Retry once for transient socket issues
                if (retryCount == 0)
                {
                    _logger?.LogInformation("[XAI] Retrying after socket abort (attempt 1/1)");
                    await Task.Delay(500, ct).ConfigureAwait(false); // Brief delay before retry
                    return await SendStreamingChatCompletionAsync(model, systemPrompt, userMessage, onChunk, ct, retryCount: 1, maxRetries: 1).ConfigureAwait(false);
                }

                return $"Socket connection aborted (debugger or network issue): {ioex.Message}";
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // TIMEOUT: Request exceeded HttpClient.Timeout (not user-initiated cancellation)
                _logger?.LogWarning("[XAI] ⏱️ HTTP request timed out (HttpClient.Timeout exceeded, not user cancellation)");
                return "Grok request timed out - check network connectivity and HttpClient.Timeout configuration";
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                // USER CANCELLATION: Explicit cancellation via CancellationToken
                _logger?.LogInformation("[XAI] 🛑 SendStreamingChatCompletionAsync cancelled by user (CancellationToken)");
                throw; // Propagate user cancellation
            }
            catch (System.IO.IOException ioex)
            {
                // RAW SOCKET ERROR: Direct IOException (not wrapped in HttpRequestException)
                _logger?.LogError(ioex, "[XAI] 🔌❌ Raw socket IOException during Grok streaming: {Msg}", ioex.Message);
                return $"Network socket error: {ioex.Message}";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[XAI] ❌ Grok streaming request failed with unexpected exception: {ExceptionType}", ex.GetType().Name);
                return $"Grok streaming failed: {ex.GetType().Name} - {ex.Message}";
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

            if (_endpoint == null)
            {
                _logger?.LogError("[XAI] Endpoint not properly initialized in GetSimpleResponse");
                return "Grok endpoint not configured";
            }

            var model = modelOverride ?? _model ?? "grok-4-1-fast-reasoning";
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
                if (_logger != null)
                {
                    LogHttpRequestHeaders(_logger, "POST", _endpoint?.ToString() ?? "unknown", "application/json", null);
                }

                // SOCKET DIAGNOSTIC: Track request timing
                var requestStartTime = System.Diagnostics.Stopwatch.StartNew();
                _logger?.LogDebug("[XAI] 🔌 Initiating HTTP POST to {Endpoint}", _endpoint);

                var resp = await _httpClient.PostAsync(_endpoint, content, ct).ConfigureAwait(false);

                requestStartTime.Stop();
                _logger?.LogDebug("[XAI] ✅ HTTP POST completed in {ElapsedMs}ms", requestStartTime.ElapsedMilliseconds);

                // Log response status code
                if (_logger != null)
                {
                    LogHttpResponseStatus(_logger, _endpoint?.ToString() ?? "unknown", (int)resp.StatusCode, null);
                }

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
                            var fallback = "grok-4-1-fast-reasoning";
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
            catch (HttpRequestException hex) when (hex.InnerException is System.IO.IOException ioex && ioex.Message.Contains("aborted"))
            {
                _logger?.LogWarning(hex, "[XAI] 🔌❌ Socket aborted during GetSimpleResponse - debugger or HttpClient lifetime issue. Inner: {InnerMsg}", ioex.Message);
                return $"Socket connection aborted: {ioex.Message}";
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger?.LogWarning("[XAI] ⏱️ GetSimpleResponse timed out (HttpClient.Timeout exceeded)");
                return "Request timed out - check network connectivity";
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                _logger?.LogInformation("[XAI] 🛑 GetSimpleResponse cancelled by user");
                throw;
            }
            catch (System.IO.IOException ioex)
            {
                _logger?.LogError(ioex, "[XAI] 🔌❌ Raw IOException in GetSimpleResponse: {Msg}", ioex.Message);
                return $"Network error: {ioex.Message}";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[XAI] ❌ GetSimpleResponse failed: {ExceptionType}", ex.GetType().Name);
                return $"Grok request failed: {ex.GetType().Name} - {ex.Message}";
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

            var model = modelOverride ?? _model ?? "grok-4-1-fast-reasoning";
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
                if (_logger != null)
                {
                    LogHttpRequestHeaders(_logger, "POST", _endpoint?.ToString() ?? "unknown", "application/json", null);
                }

                using var resp = await _httpClient.PostAsync(_endpoint, content, ct).ConfigureAwait(false);

                // Log response status code
                if (_logger != null)
                {
                    LogHttpResponseStatus(_logger, _endpoint?.ToString() ?? "unknown", (int)resp.StatusCode, null);
                }

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
                                var fallback = "grok-4-1-fast-reasoning";
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
                if (_logger != null)
                {
                    LogHttpRequestHeaders(_logger, "GET", modelsEndpoint.ToString(), "application/json", null);
                }

                using var resp = await _httpClient.GetAsync(modelsEndpoint, ct).ConfigureAwait(false);

                // Log response status code
                if (_logger != null)
                {
                    LogHttpResponseStatus(_logger, modelsEndpoint.ToString(), (int)resp.StatusCode, null);
                }

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
                var responseEndpoint = new Uri(_endpoint!, responseId);
                _logger?.LogDebug("[XAI] Retrieving response {ResponseId} via {Url}", responseId, responseEndpoint);
                if (_logger != null)
                {
                    LogHttpRequestHeaders(_logger, "GET", responseEndpoint.ToString(), "application/json", null);
                }

                using var resp = await _httpClient.GetAsync(responseEndpoint, ct).ConfigureAwait(false);

                // Log response status code
                if (_logger != null)
                {
                    LogHttpResponseStatus(_logger, responseEndpoint.ToString(), (int)resp.StatusCode, null);
                }

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
                var responseEndpoint = new Uri(_endpoint!, responseId);
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

            var model = modelOverride ?? _model ?? "grok-4.1";
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
                if (_logger != null)
                {
                    LogHttpRequestHeaders(_logger, "POST", _endpoint?.ToString() ?? "unknown", "application/json", null);
                }

                var resp = await _httpClient.PostAsync(_endpoint, content, ct).ConfigureAwait(false);

                // Log response status code
                if (_logger != null)
                {
                    LogHttpResponseStatus(_logger, _endpoint?.ToString() ?? "unknown", (int)resp.StatusCode, null);
                }

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
            var preferred = new[] { "grok-4.1", "grok-4-1-fast", "grok-4-1-fast-reasoning", "grok-4-1-fast-non-reasoning", "grok-4-1" };
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

            // Add xAI built-in tools if configured
            if (_toolConfiguration?.Enabled == true)
            {
                var tools = XAIBuiltInTools.CreateToolDefinitions(_toolConfiguration);
                if (tools.Count > 0)
                {
                    payload["tools"] = tools;
                    _logger?.LogInformation("[XAI] Added {Count} built-in tools to request: {Tools}",
                        tools.Count,
                        string.Join(", ", tools.Select(t => ((Dictionary<string, object>)((Dictionary<string, object>)t)["function"])["name"])));
                }
            }

            return payload;
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

            // Reinforce initialization: Ensure service is initialized before processing
            if (!_isInitialized)
            {
                _logger?.LogInformation("[XAI] GrokAgentService not initialized; initializing now before processing chat request");
                try
                {
                    await InitializeAsync(ct).ConfigureAwait(false);
                }
                catch (Exception initEx)
                {
                    _logger?.LogError(initEx, "[XAI] Failed to initialize GrokAgentService during chat request; falling back to simple response");
                    var fallback = await GetSimpleResponse(userRequest, _jarvisPersonality?.GetSystemPrompt() ?? JarvisSystemPrompt).ConfigureAwait(false);
                    await _chatBridge!.NotifyMessageReceivedAsync(new ChatMessage { Content = fallback ?? $"Grok initialization failed: {initEx.Message}", IsUser = false, Timestamp = DateTime.UtcNow });
                    return;
                }
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
                _logger?.LogWarning(ex, "[XAI] Semantic Kernel streaming failed; attempting fallback to direct HTTP streaming");
                try
                {
                    var responseBuilder = new StringBuilder();
                    var fallback = await GetStreamingResponseAsync(
                        userRequest,
                        _jarvisPersonality?.GetSystemPrompt() ?? JarvisSystemPrompt,
                        onChunk: async (chunk) =>
                        {
                            if (!ct.IsCancellationRequested)
                            {
                                await _chatBridge!.SendResponseChunkAsync(chunk);
                            }
                            responseBuilder.Append(chunk);
                        },
                        ct: ct
                    ).ConfigureAwait(false);

                    var fullMessage = fallback ?? responseBuilder.ToString();
                    await _chatBridge!.NotifyMessageReceivedAsync(new ChatMessage { Content = fullMessage ?? $"Grok streaming failed: {ex.Message}", IsUser = false, Timestamp = DateTime.UtcNow });
                    _logger?.LogInformation("[XAI] RunAgentToChatBridgeAsync completed via streaming fallback - Response length: {Length}", fullMessage?.Length ?? 0);
                }
                catch (Exception fallbackEx)
                {
                    _logger?.LogError(fallbackEx, "[XAI] Both Semantic Kernel and streaming fallback failed");
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

                // Build human-readable content from messages
                var contentBuilder = new StringBuilder();
                foreach (var msg in messages)
                {
                    contentBuilder.AppendLine($"[{msg.Role.ToUpperInvariant()}]: {msg.Content}");
                }

                var conversation = new ConversationHistory
                {
                    ConversationId = conversationId,
                    MessagesJson = json,
                    Content = contentBuilder.ToString(),
                    MessageCount = messages.Count,
                    CreatedAt = DateTime.UtcNow,
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

        /// <summary>
        /// Return a single, non-streaming chat completion suitable for UI components
        /// such as Syncfusion's SfAIAssistView which expect a single response string.
        /// </summary>
        public async Task<string> GetChatCompletionAsync(string prompt, CancellationToken cancellationToken = default)
        {
            try
            {
                var systemPrompt = _jarvisPersonality?.GetSystemPrompt() ?? JarvisSystemPrompt;
                // Delegate to existing simple responses helper which calls the /v1/responses endpoint
                return await GetSimpleResponse(prompt, systemPrompt, _model, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetChatCompletionAsync failed");
                return $"Error: {ex.Message}";
            }
        }

        public async IAsyncEnumerable<string> StreamResponseAsync(string prompt, string? systemMessage = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                yield return "⚠️ No API key configured for Grok";
                yield break;
            }

            if (!_isInitialized)
            {
                yield return "⚠️ Grok service still initializing... please retry in a moment";
                yield break;
            }

            // Get response either via Semantic Kernel or HTTP fallback
            var response = await GetStreamResponseInternalAsync(prompt, systemMessage, cancellationToken).ConfigureAwait(false);
            yield return response;
        }

        /// <summary>
        /// Internal helper: Gets streaming response with SK primary path and HTTP fallback.
        /// Returns complete response as string (avoids yield-in-try-catch C# limitation).
        /// </summary>
        private async Task<string> GetStreamResponseInternalAsync(string prompt, string? systemMessage, CancellationToken cancellationToken)
        {
            // If SK connector was disabled or kernel is not available, use HTTP-only streaming
            _logger?.LogDebug("[XAI] GetStreamResponseInternalAsync: _skConnectorDisabled={Disabled}, _kernel={KernelNull}", _skConnectorDisabled, _kernel == null);

            if (_skConnectorDisabled || _kernel == null)
            {
                if (_skConnectorDisabled)
                {
                    _logger?.LogInformation("[XAI] SK connector disabled via config - using direct HTTP streaming with /v1/responses endpoint");
                }
                else
                {
                    _logger?.LogWarning("[XAI] GetStreamResponseInternalAsync: kernel is null - using direct HTTP streaming");
                }
                return await TryGetStreamingResponseWithFallbackAsync(prompt, systemMessage, cancellationToken).ConfigureAwait(false);
            }

            // Prefer non-streaming for tool-heavy prompts to improve tool call reliability.
            if (ShouldPreferToolCalling(prompt))
            {
                _logger?.LogInformation("[XAI] Tool-heavy prompt detected - using non-streaming mode for reliable tool calls");
                try
                {
                    var nonStreaming = await TryGetNonStreamingWithSemanticKernelAsync(prompt, systemMessage, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(nonStreaming))
                    {
                        return nonStreaming;
                    }

                    _logger?.LogWarning("[XAI] Non-streaming tool call returned empty content; falling back to streaming");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[XAI] Non-streaming tool call failed; falling back to streaming");
                }
            }

            // Try Semantic Kernel streaming first; if it fails, fall back to direct HTTP
            try
            {
                return await TryStreamWithSemanticKernelAsync(prompt, systemMessage, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException iex) when (iex.Message.Contains("not registered"))
            {
                _logger?.LogWarning(iex, "[XAI] SK chat service not registered - using direct HTTP streaming");
                return await TryGetStreamingResponseWithFallbackAsync(prompt, systemMessage, cancellationToken).ConfigureAwait(false);
            }
            catch (KernelException kex) when (kex.Message.Contains("not registered"))
            {
                _logger?.LogWarning(kex, "[XAI] SK chat service not registered (KernelException) - using direct HTTP streaming");
                return await TryGetStreamingResponseWithFallbackAsync(prompt, systemMessage, cancellationToken).ConfigureAwait(false);
            }
            catch (TypeInitializationException tiex) when (tiex.InnerException is MissingMethodException mex)
            {
                _logger?.LogError(tiex, "[XAI] SK type init error during streaming: {Message}", mex.Message);
                var result = await TryGetStreamingResponseWithFallbackAsync(prompt, systemMessage, cancellationToken).ConfigureAwait(false);
                return "⚠️ Semantic Kernel incompatibility detected - switched to direct HTTP...\n" + result;
            }
            catch (OperationCanceledException ex) when (ex.InnerException is IOException ioEx && ioEx.Message.Contains("aborted"))
            {
                _logger?.LogWarning(ex, "[XAI] Connection aborted during SK streaming - retrying with backoff");
                try
                {
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    var result = await TryGetStreamingResponseWithFallbackAsync(prompt, systemMessage, cancellationToken).ConfigureAwait(false);
                    return "🔄 Reconnecting...\n" + result;
                }
                catch (Exception retryEx)
                {
                    _logger?.LogError(retryEx, "[XAI] Retry failed after connection abort");
                    return "❌ Connection retry failed - please try again";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[XAI] GetStreamResponseInternalAsync failed: {Type}: {Message}", ex.GetType().Name, ex.Message);
                return $"❌ Error: {ex.Message}";
            }
        }

        private static readonly string[] ToolHeavyKeywords =
        {
            "variance",
            "budget",
            "audit",
            "calculate",
            "forecast",
            "what-if",
            "scenario",
            "fund",
            "enterprise",
            "rate",
            "allocation",
            "compliance"
        };

        private bool ShouldPreferToolCalling(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return false;
            }

            if (_kernel == null || _kernel.Plugins.Count == 0)
            {
                return false;
            }

            return ToolHeavyKeywords.Any(keyword =>
                prompt.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Attempts a non-streaming response using Semantic Kernel to improve tool call reliability.
        /// </summary>
        private async Task<string> TryGetNonStreamingWithSemanticKernelAsync(string prompt, string? systemMessage, CancellationToken cancellationToken)
        {
            var chatService = _kernel!.GetRequiredService<IChatCompletionService>();
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

            if (!IsReasoningModel(_model))
            {
                if (_defaultPresencePenalty.HasValue) settings.PresencePenalty = _defaultPresencePenalty.Value;
                if (_defaultFrequencyPenalty.HasValue) settings.FrequencyPenalty = _defaultFrequencyPenalty.Value;
            }

            var messages = await chatService.GetChatMessageContentsAsync(
                history,
                executionSettings: settings,
                kernel: _kernel,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var content = string.Concat(messages
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => message.Content));

            return content;
        }

        /// <summary>
        /// Attempts to stream response using Semantic Kernel chat completion service.
        /// </summary>
        private async Task<string> TryStreamWithSemanticKernelAsync(string prompt, string? systemMessage, CancellationToken cancellationToken)
        {
            try
            {
                var chatService = _kernel!.GetRequiredService<IChatCompletionService>();
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

                var responseBuilder = new StringBuilder();
                await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                    history,
                    executionSettings: settings,
                    kernel: _kernel,
                    cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        responseBuilder.Append(chunk.Content);
                    }
                }

                return responseBuilder.ToString();
            }
            catch (InvalidOperationException iex) when (iex.Message.Contains("not registered"))
            {
                _logger?.LogWarning(iex, "[XAI] IChatCompletionService not registered in kernel (SK connector disabled) - falling back to HTTP");
                throw; // Re-throw to be caught by caller for HTTP fallback
            }
        }

        /// <summary>
        /// Fallback: Uses direct HTTP streaming via GetStreamingResponseAsync with callback.
        /// </summary>
        private async Task<string> TryGetStreamingResponseWithFallbackAsync(string prompt, string? systemMessage, CancellationToken cancellationToken)
        {
            var chunks = new List<string>();
            var result = await GetStreamingResponseAsync(
                prompt,
                systemMessage,
                modelOverride: null,
                onChunk: chunk => chunks.Add(chunk),
                ct: cancellationToken).ConfigureAwait(false);

            // If the result is an error, return it; otherwise return collected chunks
            if (result.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                result.StartsWith("No API", StringComparison.OrdinalIgnoreCase) ||
                result.StartsWith("Grok", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            // Prefer chunks if collected, otherwise use result
            return chunks.Count > 0 ? string.Concat(chunks) : result;
        }

        public async Task<string> SendMessageAsync(string message, object conversationHistory, CancellationToken cancellationToken = default)
        {
            // Simple implementation; conversation history is ignored for now
            return await GetSimpleResponse(message, ct: cancellationToken);
        }

        /// <summary>
        /// Loads xAI tools configuration from appsettings.json
        /// </summary>
        private static XAIBuiltInTools.XAIToolConfiguration? LoadToolConfiguration(IConfiguration config, ILogger? logger)
        {
            try
            {
                var toolsSection = config.GetSection("XAI:Tools");
                if (!toolsSection.Exists())
                {
                    logger?.LogInformation("[XAI] No tools configuration found in XAI:Tools section - built-in tools disabled");
                    return null;
                }

                var toolConfig = new XAIBuiltInTools.XAIToolConfiguration
                {
                    Enabled = toolsSection.GetValue<bool>("Enabled", false)
                };

                if (!toolConfig.Enabled)
                {
                    logger?.LogInformation("[XAI] Built-in tools disabled via configuration (XAI:Tools:Enabled=false)");
                    return toolConfig;
                }

                // Load Web Search configuration
                var webSearchSection = toolsSection.GetSection("WebSearch");
                if (webSearchSection.Exists())
                {
                    toolConfig.WebSearch = new XAIBuiltInTools.WebSearchConfig
                    {
                        Enabled = webSearchSection.GetValue<bool>("Enabled", false),
                        EnableImageUnderstanding = webSearchSection.GetValue<bool>("EnableImageUnderstanding", false),
                        AllowedDomains = webSearchSection.GetSection("AllowedDomains").Get<List<string>>() ?? new List<string>(),
                        ExcludedDomains = webSearchSection.GetSection("ExcludedDomains").Get<List<string>>() ?? new List<string>()
                    };

                    if (toolConfig.WebSearch.Enabled)
                    {
                        logger?.LogInformation("[XAI] Web Search tool enabled (allowed_domains: {AllowedCount}, excluded_domains: {ExcludedCount})",
                            toolConfig.WebSearch.AllowedDomains.Count,
                            toolConfig.WebSearch.ExcludedDomains.Count);
                    }
                }

                // Load X Search configuration
                var xSearchSection = toolsSection.GetSection("XSearch");
                if (xSearchSection.Exists())
                {
                    toolConfig.XSearch = new XAIBuiltInTools.XSearchConfig
                    {
                        Enabled = xSearchSection.GetValue<bool>("Enabled", false),
                        EnableImageUnderstanding = xSearchSection.GetValue<bool>("EnableImageUnderstanding", false)
                    };

                    if (toolConfig.XSearch.Enabled)
                    {
                        logger?.LogInformation("[XAI] X Search tool enabled (image_understanding: {ImageUnderstanding})",
                            toolConfig.XSearch.EnableImageUnderstanding);
                    }
                }

                // Load Code Execution configuration
                var codeExecSection = toolsSection.GetSection("CodeExecution");
                if (codeExecSection.Exists())
                {
                    toolConfig.CodeExecution = new XAIBuiltInTools.CodeExecutionConfig
                    {
                        Enabled = codeExecSection.GetValue<bool>("Enabled", false),
                        TimeoutSeconds = codeExecSection.GetValue<int>("TimeoutSeconds", 30)
                    };

                    if (toolConfig.CodeExecution.Enabled)
                    {
                        logger?.LogInformation("[XAI] Code Execution tool enabled (Python sandbox with pandas, numpy, scipy, matplotlib - timeout: {TimeoutSeconds}s)",
                            toolConfig.CodeExecution.TimeoutSeconds);
                    }
                }

                // Load Collections Search configuration
                var collectionsSection = toolsSection.GetSection("CollectionsSearch");
                if (collectionsSection.Exists())
                {
                    toolConfig.CollectionsSearch = new XAIBuiltInTools.CollectionsSearchConfig
                    {
                        Enabled = collectionsSection.GetValue<bool>("Enabled", false),
                        CollectionIds = collectionsSection.GetSection("CollectionIds").Get<List<string>>() ?? new List<string>()
                    };

                    if (toolConfig.CollectionsSearch.Enabled)
                    {
                        logger?.LogInformation("[XAI] Collections Search tool enabled (collection_ids: {CollectionCount})",
                            toolConfig.CollectionsSearch.CollectionIds.Count);
                    }
                }

                // Validate configuration before returning
                var (isValid, errors) = XAIBuiltInTools.ValidateConfiguration(toolConfig);
                if (!isValid)
                {
                    logger?.LogWarning("[XAI] Tool configuration validation failed: {Errors}", string.Join("; ", errors));
                    return null;
                }

                var enabledTools = new List<string>();
                if (toolConfig.WebSearch?.Enabled == true) enabledTools.Add("web_search");
                if (toolConfig.XSearch?.Enabled == true) enabledTools.Add("x_search");
                if (toolConfig.CodeExecution?.Enabled == true) enabledTools.Add("code_execution");
                if (toolConfig.CollectionsSearch?.Enabled == true) enabledTools.Add("collections_search");

                if (enabledTools.Count > 0)
                {
                    logger?.LogInformation("[XAI] ✅ Loaded xAI built-in tools configuration - Enabled tools: {Tools}",
                        string.Join(", ", enabledTools));
                }
                else
                {
                    logger?.LogInformation("[XAI] No xAI built-in tools enabled in configuration");
                }

                return toolConfig;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[XAI] Failed to load tools configuration from appsettings: {Message}", ex.Message);
                return null;
            }
        }
    }
}
