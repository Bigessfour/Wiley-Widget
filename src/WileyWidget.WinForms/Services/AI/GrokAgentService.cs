using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Reflection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace WileyWidget.WinForms.Services.AI
{
    /// <summary>
    /// Provides a thin Semantic Kernel-backed client for Grok (xAI).
    /// - Registers a Semantic Kernel chat completion connector when an API key is present.
    /// - Exposes a simple HTTP fallback to exercise the Grok endpoint for basic tests.
    /// </summary>
    public class GrokAgentService
    {
        private readonly Kernel _kernel;
        private readonly string? _apiKey;
        private readonly Uri _endpoint;
        private readonly string _model;
        private readonly ILogger<GrokAgentService>? _logger;
        private readonly HttpClient _httpClient;

        // Normalized completions endpoint (handles both base endpoint or full /chat/completions config)
        private Uri CompletionsEndpoint => _endpoint.AbsoluteUri.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ? _endpoint : new Uri(_endpoint, "chat/completions");
        private const string DefaultArchitectPrompt = "You are a senior Syncfusion WinForms architect. Enforce SfSkinManager theming rules and repository conventions: prefer SfSkinManager.LoadAssembly and SfSkinManager.SetVisualStyle, avoid manual BackColor/ForeColor assignments except for semantic status colors (Color.Red/Color.Green/Color.Orange), favor MVVM patterns and ThemeColors.ApplyTheme(this) on forms. Provide concise, actionable guidance and C# examples that follow the project's coding standards.";

        public GrokAgentService(IConfiguration config, ILogger<GrokAgentService>? logger = null, IHttpClientFactory? httpClientFactory = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _logger = logger;

            // Read and sanitize API key (trim whitespace and surrounding quotes that may be introduced by env config)
            var rawApiKey = config["Grok:ApiKey"] ?? config["XAI:ApiKey"] ?? config["XAI_API_KEY"];
            _apiKey = string.IsNullOrWhiteSpace(rawApiKey) ? null : rawApiKey.Trim().Trim('"');

            _model = config["Grok:Model"] ?? config["XAI:Model"] ?? "grok-4-latest";
            var endpointStr = config["Grok:Endpoint"] ?? config["XAI:Endpoint"] ?? "https://api.x.ai/v1";
            // Ensure endpoint ends with a trailing slash so relative URIs append correctly (avoids 404 when combining URIs)
            if (!endpointStr.EndsWith('/'))
            {
                endpointStr += '/';
            }
            _endpoint = new Uri(endpointStr);

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
            logger?.LogInformation("[XAI] Environment variable XAI_API_KEY length: {EnvLength}, Config API key length: {ConfigLength}", Environment.GetEnvironmentVariable("XAI_API_KEY")?.Length ?? 0, _apiKey?.Length ?? 0);
            logger?.LogInformation("[XAI] Using model={Model}, endpoint={Endpoint}", _model, _endpoint);

            var builder = Kernel.CreateBuilder();

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                try
                {
                    // Use the OpenAI-compatible connector to target xAI's Grok endpoint
                    _logger?.LogInformation("Configuring Grok chat completion: model={Model}, endpoint={Endpoint}, apiKeyLength={KeyLength}",
                        _model, _endpoint, _apiKey.Length);
#pragma warning disable SKEXP0010
                    builder.AddOpenAIChatCompletion(modelId: _model, apiKey: _apiKey, endpoint: _endpoint);
#pragma warning restore SKEXP0010
                    _logger?.LogInformation("Semantic Kernel configured with Grok chat completion successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to configure Semantic Kernel chat completion. Falling back to direct HTTP calls.");
                }
            }
            else
            {
                _logger?.LogWarning("Grok API key not configured; Semantic Kernel chat connector will not be registered.");
            }

            _kernel = builder.Build();

            // Auto-register kernel plugins discovered in the executing assembly (types with [KernelFunction]).
            // Keeping this scoped avoids scanning unrelated testhost/runtime assemblies.
            try
            {
                var assemblyToScan = typeof(GrokAgentService).Assembly;
                KernelPluginRegistrar.ImportPluginsFromAssemblies(_kernel, new[] { assemblyToScan }, _logger);
                _logger?.LogDebug("Auto-registered kernel plugins from assembly: {Assembly}", assemblyToScan.FullName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to auto-register kernel plugins from executing assembly.");
            }

            _httpClient = httpClientFactory?.CreateClient("WileyWidgetDefault") ?? new HttpClient();
            _httpClient.BaseAddress = _endpoint;
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }


            // Optional: run a quick validation on startup if configured
            var validateOnStartupStr = config["XAI:ValidateOnStartup"] ?? config["Grok:ValidateOnStartup"];
            if (bool.TryParse(validateOnStartupStr, out var validateOnStartup) && validateOnStartup && !string.IsNullOrWhiteSpace(_apiKey))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        var (success, msg) = await ValidateApiKeyAsync(cts.Token);
                        if (success)
                            _logger?.LogInformation("[XAI] Startup validation: API key OK");
                        else
                            _logger?.LogWarning("[XAI] Startup validation: API key check failed: {Msg}", msg);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[XAI] Startup validation failed unexpectedly");
                    }
                });
            }
        }

        /// <summary>
        /// Exposes the underlying Kernel instance (if the caller needs to register plugins or run advanced flows).
        /// </summary>
        public Kernel Kernel => _kernel;

        /// <summary>
        /// Indicates whether an API key is configured for Grok (xAI).
        /// </summary>
        public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

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
        public Uri Endpoint => _endpoint;

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
        /// Simple chat helper used for smoke tests and quick interactions.
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

            var model = modelOverride ?? _model ?? "grok-4-latest";
            var sysPrompt = systemPrompt ?? "You are a test assistant.";

            // Use a lightweight OpenAI-compatible chat call to the xAI (Grok) endpoint as a fallback.
            var requestObj = new
            {
                model = model,
                messages = new[] {
                    new { role = "system", content = sysPrompt },
                    new { role = "user", content = userMessage }
                },
                stream = false,
                temperature = 0
            };

            var json = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                _logger?.LogDebug("ValidateApiKeyAsync -> POST {Url} (model={Model})", CompletionsEndpoint, model);
                _logger?.LogDebug("GetSimpleResponse -> POST {Url} (model={Model})", CompletionsEndpoint, model);
                var resp = await _httpClient.PostAsync(CompletionsEndpoint, content, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Grok API returned non-success status: {Status}", resp.StatusCode);
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
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

            var model = modelOverride ?? _model ?? "grok-4-latest";
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
                using var resp = await _httpClient.PostAsync(CompletionsEndpoint, content, ct).ConfigureAwait(false);
                var respBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("ValidateApiKeyAsync: Grok API returned non-success status: {Status}", resp.StatusCode);
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
        /// Runs an agentic chat session with Grok using a ChatCompletionAgent.
        /// The default system prompt makes the agent act as a senior Syncfusion WinForms architect
        /// that enforces SfSkinManager theming rules, MVVM patterns, and project conventions.
        /// If no Grok API key is configured the method returns a clear diagnostic string.
        /// </summary>
        public async Task<string> RunAgentAsync(string userRequest, string systemPrompt = DefaultArchitectPrompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger?.LogInformation("RunAgentAsync called but no Grok API key configured.");
                return "No API key configured for Grok";
            }

            try
            {
                // Use IChatCompletionService directly from the kernel
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(systemPrompt);
                chatHistory.AddUserMessage(userRequest);

                var responseBuilder = new StringBuilder();
                await foreach (var message in chatService.GetStreamingChatMessageContentsAsync(chatHistory))
                {
                    responseBuilder.Append(message.Content);
                }
                return responseBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "RunAgentAsync failed; falling back to simple chat call");
                return await GetSimpleResponse(userRequest);
            }
        }
    }
}
