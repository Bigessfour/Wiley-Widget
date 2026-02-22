using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Services.AI
{
    /// <summary>
    /// Production implementation of IGrokApiKeyProvider with full configuration hierarchy support.
    /// See IGrokApiKeyProvider (WileyWidget.Services.Abstractions) for interface documentation.
    /// </summary>
    public sealed class GrokApiKeyProvider : IGrokApiKeyProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GrokApiKeyProvider>? _logger;
        private readonly IHttpClientFactory? _httpClientFactory;

        private string? _apiKey;
        private bool _isValidated = false;
        private bool _isFromUserSecrets = false;
        private string _configurationSource = "not configured";

        public string? ApiKey => _apiKey;
        public string? MaskedApiKey => MaskApiKey(_apiKey);
        public bool IsValidated => _isValidated;
        public bool IsFromUserSecrets => _isFromUserSecrets;

        public GrokApiKeyProvider(
            IConfiguration configuration,
            ILogger<GrokApiKeyProvider>? logger = null,
            IHttpClientFactory? httpClientFactory = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            // Initialize API key from configuration hierarchy
            InitializeApiKey();
        }

        /// <summary>
        /// Initialize API key with machine-scope environment variables as canonical source.
        /// Compatibility fallback remains enabled for configuration and user/process-scoped aliases.
        /// Note: XAI_API_KEY (single underscore) is still supported for backward compatibility.
        ///
        /// See: https://learn.microsoft.com/aspnet/core/fundamentals/configuration/#environment-variables
        /// Environment variables use __ (double underscore) which maps to : (colon) in hierarchical configuration.
        /// </summary>
        private void InitializeApiKey()
        {
            _logger?.LogInformation("[Grok] Initializing API key from configuration hierarchy...");

            // 0. Canonical source: machine-scoped environment variables
            var machineHierarchicalEnvKey = Environment.GetEnvironmentVariable("XAI__ApiKey", EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(machineHierarchicalEnvKey))
            {
                _apiKey = machineHierarchicalEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI__ApiKey - machine scope) [CANONICAL]";
                _isFromUserSecrets = false;
                _logger?.LogInformation(
                    "[Grok] API key loaded from {Source} (length: {Length})",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            var machineLegacyEnvKey = Environment.GetEnvironmentVariable("XAI_API_KEY", EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(machineLegacyEnvKey))
            {
                _apiKey = machineLegacyEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI_API_KEY - machine scope) [LEGACY ALIAS]";
                _isFromUserSecrets = false;
                _logger?.LogWarning(
                    "[Grok] API key loaded from legacy alias {Source}. Prefer machine-scoped XAI__ApiKey.",
                    _configurationSource);
                return;
            }

            // Configuration key candidates (in order of priority)
            // These work through the IConfiguration system (user secrets, appsettings.json, environment variables)
            var candidates = new[]
            {
                ("XAI:ApiKey", "configuration: XAI:ApiKey (user-secrets or XAI__ApiKey env var)"),
                ("xAI:ApiKey", "configuration: xAI:ApiKey"),
                ("Grok:ApiKey", "configuration: Grok:ApiKey (legacy)"),
            };

            // 1. Try Configuration first (includes User Secrets, appsettings.json, and mapped Environment Variables)
            foreach (var (configKey, source) in candidates)
            {
                var value = _configuration[configKey];
                if (!string.IsNullOrWhiteSpace(value) && !value.Contains("YOUR_") && value.Trim().Length > 10)
                {
                    _apiKey = value.Trim().Trim('"');
                    _configurationSource = source;
                    _isFromUserSecrets = IsKeyFromUserSecrets(configKey);
                    _logger?.LogInformation(
                        "[Grok] API key loaded from {Source} (length: {Length}, fromUserSecrets: {IsUserSecrets})",
                        source,
                        _apiKey.Length,
                        _isFromUserSecrets);
                    return;
                }
            }

            // 2. Try environment variables with proper hierarchical naming (XAI__ApiKey)
            // According to Microsoft docs, __ (double underscore) maps to : (colon) in configuration
            // Compatibility scopes: Process > User
            var hierarchicalEnvKey = Environment.GetEnvironmentVariable("XAI__ApiKey", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrWhiteSpace(hierarchicalEnvKey))
            {
                _apiKey = hierarchicalEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI__ApiKey - process scope) [COMPATIBILITY]";
                _isFromUserSecrets = false;
                _logger?.LogWarning("[Grok] API key loaded from process scope. Machine scope is canonical.");
                _logger?.LogInformation(
                    "[Grok] API key loaded from {Source} (length: {Length})",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            hierarchicalEnvKey = Environment.GetEnvironmentVariable("XAI__ApiKey", EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(hierarchicalEnvKey))
            {
                _apiKey = hierarchicalEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI__ApiKey - user scope) [COMPATIBILITY]";
                _isFromUserSecrets = false;
                _logger?.LogWarning("[Grok] API key loaded from user scope. Machine scope is canonical.");
                _logger?.LogInformation(
                    "[Grok] API key loaded from {Source} (length: {Length})",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            var processLegacyEnvKey = Environment.GetEnvironmentVariable("XAI_API_KEY", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrWhiteSpace(processLegacyEnvKey))
            {
                _apiKey = processLegacyEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI_API_KEY - process scope) [LEGACY ALIAS]";
                _isFromUserSecrets = false;
                _logger?.LogWarning("[Grok] API key loaded from legacy process alias. Prefer machine-scoped XAI__ApiKey.");
                _logger?.LogInformation(
                    "[Grok] API key loaded from {Source} (length: {Length})",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            var userLegacyEnvKey = Environment.GetEnvironmentVariable("XAI_API_KEY", EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(userLegacyEnvKey))
            {
                _apiKey = userLegacyEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI_API_KEY - user scope) [LEGACY ALIAS]";
                _isFromUserSecrets = false;
                _logger?.LogWarning("[Grok] API key loaded from legacy user alias. Prefer machine-scoped XAI__ApiKey.");
                _logger?.LogInformation(
                    "[Grok] API key loaded from {Source} (length: {Length})",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            // 3. API key not found
            _configurationSource = "NOT CONFIGURED";
            _logger?.LogWarning(
                "[Grok] No API key found in machine/user/process environment variables or user secrets. " +
                "JARVIS Chat will not function. " +
                "Configure via one of these methods:\n" +
                "  1. Machine environment (canonical): setx /M XAI__ApiKey <your-key>\n" +
                "  2. User Secrets (compatibility): dotnet user-secrets set XAI:ApiKey <your-key>\n" +
                "  3. User environment (compatibility): setx XAI__ApiKey <your-key>");
        }

        /// <summary>
        /// Determine if the API key came from user secrets.
        /// User secrets are loaded by Microsoft.Extensions.Configuration.UserSecrets
        /// and have higher priority than environment variables and appsettings.
        /// </summary>
        private bool IsKeyFromUserSecrets(string configKey)
        {
            try
            {
                if (_configuration is IConfigurationRoot root)
                {
                    foreach (var provider in root.Providers)
                    {
                        if (provider.TryGet(configKey, out var value) && !string.IsNullOrWhiteSpace(value))
                        {
                            return provider.GetType().FullName == "Microsoft.Extensions.Configuration.UserSecrets.UserSecretsConfigurationProvider";
                        }
                    }
                }
            }
            catch
            {
                // Swallow exceptions during detection
            }

            return false;
        }

        /// <summary>
        /// Validate API key by making a minimal test request to xAI Grok /v1/chat/completions endpoint.
        /// This verifies that:
        /// 1. The API key is syntactically valid (not expired/revoked)
        /// 2. The endpoint is reachable
        /// 3. Authentication succeeds
        /// Note: xAI doesn't provide a /models endpoint like OpenAI, so we make a minimal chat completion request.
        /// </summary>
        public async Task<(bool Success, string Message)> ValidateAsync()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                var msg = "API key not configured. JARVIS Chat will not function.";
                _logger?.LogWarning("[Grok] Validation failed: {Message}", msg);
                return (false, msg);
            }

            try
            {
                _logger?.LogInformation("[Grok] Validating API key via minimal test request to /v1/responses endpoint...");

                var httpClient = _httpClientFactory?.CreateClient("GrokClient") ?? new HttpClient();
                var endpoint = new Uri(GetBaseEndpoint(), "responses");

                // Create minimal test request payload using x.ai /v1/responses API format
                var testPayload = new
                {
                    model = _configuration["XAI:Model"] ?? "grok-4-1-fast-reasoning",
                    input = new[]
                    {
                        new { role = "system", content = "You are a test assistant." },
                        new { role = "user", content = "Say 'Test successful!' briefly." }
                    },
                    stream = false
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(testPayload);
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new System.Net.Http.StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                // [FIX] Increased timeout from 10s to 30s to handle slower network connections
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var response = await httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    // Verify response structure matches /v1/responses format
                    var content = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("output", out var output) && output.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            _isValidated = true;
                            var msg = $"✅ API key validated successfully ({MaskedApiKey}). Grok service ready.";
                            _logger?.LogInformation("[Grok] {Message}", msg);
                            return (true, msg);
                        }
                        else
                        {
                            var msg = $"❌ API validation failed: Unexpected response format. Expected /v1/responses format with 'output' field. Response: {content.Substring(0, Math.Min(200, content.Length))}";
                            _logger?.LogWarning("[Grok] {Message}", msg);
                            return (false, msg);
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        var msg = $"❌ API validation failed: Could not parse response JSON. {ex.Message}";
                        _logger?.LogWarning("[Grok] {Message}", msg);
                        return (false, msg);
                    }
                }

                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                var errorMsg = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => $"❌ API key is invalid or expired. Update user.secrets with a valid key. Response: {body}",
                    System.Net.HttpStatusCode.Forbidden => $"❌ API key is valid but lacks required permissions. Response: {body}",
                    System.Net.HttpStatusCode.BadRequest => $"❌ API validation failed: Bad Request. This may indicate an invalid API key format. Response: {body}",
                    _ => $"❌ API validation failed: HTTP {(int)response.StatusCode} - Response: {body}"
                };

                _logger?.LogWarning("[Grok] Validation failed: {ErrorMsg}", errorMsg);
                return (false, errorMsg);
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                var msg = "❌ API key validation timed out (30s). Check network connectivity or try again later. JARVIS Chat will work once connection is restored.";
                _logger?.LogWarning("[Grok] {Message}", msg);
                return (false, msg);
            }
            catch (HttpRequestException ex)
            {
                var msg = $"❌ API key validation failed: Network error - {ex.Message}";
                _logger?.LogWarning(ex, "[Grok] {Message}", msg);
                return (false, msg);
            }
            catch (Exception ex)
            {
                var msg = $"❌ API key validation failed: {ex.Message}";
                _logger?.LogError(ex, "[Grok] {Message}", msg);
                return (false, msg);
            }
        }

        /// <summary>
        /// Get detailed configuration source for diagnostics.
        /// Safe to log - does not expose the actual API key.
        /// </summary>
        public string GetConfigurationSource()
        {
            return $"API Key: {_configurationSource} | Validated: {_isValidated} | Source: {(IsFromUserSecrets ? "User Secrets (Secure)" : "Configuration/Environment")}";
        }

        private Uri GetBaseEndpoint()
        {
            var endpointStr = (_configuration["Grok:Endpoint"] ?? _configuration["XAI:Endpoint"] ?? "https://api.x.ai/v1").Trim().TrimEnd('/');
            if (endpointStr.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
            {
                endpointStr = endpointStr.Substring(0, endpointStr.Length - "/responses".Length);
            }

            if (endpointStr.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                endpointStr = endpointStr.Substring(0, endpointStr.Length - "/chat/completions".Length);
            }

            return new Uri(endpointStr + '/', UriKind.Absolute);
        }

        /// <summary>
        /// Mask API key for safe logging.
        /// Shows first 4 and last 4 characters, hides the middle.
        /// </summary>
        private static string? MaskApiKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length <= 8)
                return "****";

            return $"{key.Substring(0, 4)}...{key.Substring(key.Length - 4)}";
        }
    }
}
