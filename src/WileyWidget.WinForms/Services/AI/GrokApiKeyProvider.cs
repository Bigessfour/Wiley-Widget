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
        /// Initialize API key following Microsoft's configuration hierarchy:
        /// User Secrets > Environment Variables (XAI__ApiKey per Microsoft convention) > appsettings.json
        /// Note: XAI_API_KEY (single underscore) is still supported for backward compatibility.
        ///
        /// See: https://learn.microsoft.com/aspnet/core/fundamentals/configuration/#environment-variables
        /// Environment variables use __ (double underscore) which maps to : (colon) in hierarchical configuration.
        /// </summary>
        private void InitializeApiKey()
        {
            _logger?.LogInformation("[Grok] Initializing API key from configuration hierarchy...");

            // Configuration key candidates (in order of priority)
            // These work through the IConfiguration system (user secrets, appsettings.json, environment variables)
            var candidates = new[]
            {
                ("XAI:ApiKey", "configuration: XAI:ApiKey (user-secrets or XAI__ApiKey env var)"),
                ("Grok:ApiKey", "configuration: Grok:ApiKey (legacy)"),
            };

            // 1. Try User Secrets first (highest priority, secure)
            foreach (var (configKey, source) in candidates)
            {
                var value = _configuration[configKey];
                if (!string.IsNullOrWhiteSpace(value))
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
            // Try all scopes: Process > User > Machine
            var hierarchicalEnvKey = Environment.GetEnvironmentVariable("XAI__ApiKey", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrWhiteSpace(hierarchicalEnvKey))
            {
                _apiKey = hierarchicalEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI__ApiKey - process scope) [RECOMMENDED for env vars]";
                _isFromUserSecrets = false;
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
                _configurationSource = "environment variable (XAI__ApiKey - user scope) [RECOMMENDED for env vars]";
                _isFromUserSecrets = false;
                _logger?.LogInformation(
                    "[Grok] API key loaded from {Source} (length: {Length})",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            hierarchicalEnvKey = Environment.GetEnvironmentVariable("XAI__ApiKey", EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(hierarchicalEnvKey))
            {
                _apiKey = hierarchicalEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI__ApiKey - machine scope) [RECOMMENDED for env vars]";
                _isFromUserSecrets = false;
                _logger?.LogInformation(
                    "[Grok] API key loaded from {Source} (length: {Length})",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            // 3. LEGACY: Try XAI_API_KEY (single underscore) - still supported for backward compatibility
            var legacyEnvKey = Environment.GetEnvironmentVariable("XAI_API_KEY", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrWhiteSpace(legacyEnvKey))
            {
                _apiKey = legacyEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI_API_KEY - process scope) [LEGACY - use XAI__ApiKey instead]";
                _isFromUserSecrets = false;
                _logger?.LogWarning(
                    "[Grok] API key loaded from LEGACY {Source} (length: {Length}). " +
                    "Prefer 'XAI__ApiKey' environment variable (double underscore) per Microsoft configuration conventions.",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            legacyEnvKey = Environment.GetEnvironmentVariable("XAI_API_KEY", EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(legacyEnvKey))
            {
                _apiKey = legacyEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI_API_KEY - user scope) [LEGACY - use XAI__ApiKey instead]";
                _isFromUserSecrets = false;
                _logger?.LogWarning(
                    "[Grok] API key loaded from LEGACY {Source} (length: {Length}). " +
                    "Prefer 'XAI__ApiKey' environment variable (double underscore) per Microsoft configuration conventions.",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            legacyEnvKey = Environment.GetEnvironmentVariable("XAI_API_KEY", EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(legacyEnvKey))
            {
                _apiKey = legacyEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI_API_KEY - machine scope) [LEGACY - use XAI__ApiKey instead]";
                _isFromUserSecrets = false;
                _logger?.LogWarning(
                    "[Grok] API key loaded from LEGACY {Source} (length: {Length}). " +
                    "Prefer 'XAI__ApiKey' environment variable (double underscore) per Microsoft configuration conventions.",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            // 4. API key not found
            _configurationSource = "NOT CONFIGURED";
            _logger?.LogWarning(
                "[Grok] No API key found in user secrets or environment variables. " +
                "JARVIS Chat will not function. " +
                "Configure via one of these methods:\n" +
                "  1. User Secrets: dotnet user-secrets set XAI:ApiKey <your-key>\n" +
                "  2. Environment Variable (recommended): setx XAI__ApiKey <your-key> (double underscore, per Microsoft conventions)\n" +
                "  3. Environment Variable (legacy): setx XAI_API_KEY <your-key> (single underscore, deprecated)");
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
                _logger?.LogInformation("[Grok] Validating API key via minimal test request to /v1/chat/completions...");

                var httpClient = _httpClientFactory?.CreateClient("GrokClient") ?? new HttpClient();
                var endpoint = new Uri(GetBaseEndpoint(), "chat/completions");

                // Create minimal test request payload
                var testPayload = new
                {
                    model = "grok-beta",
                    messages = new[]
                    {
                        new { role = "user", content = "test" }
                    },
                    max_tokens = 1,
                    stream = false
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(testPayload);
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new System.Net.Http.StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var response = await httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    _isValidated = true;
                    var msg = $"✅ API key validated successfully ({MaskedApiKey}). Grok service ready.";
                    _logger?.LogInformation("[Grok] {Message}", msg);
                    return (true, msg);
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
                var msg = "❌ API key validation timed out (10s). Check network connectivity.";
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
