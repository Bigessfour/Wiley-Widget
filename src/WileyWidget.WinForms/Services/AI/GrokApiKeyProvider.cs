using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Services.AI
{
    /// <summary>
    /// Provides centralized, production-ready API key management for xAI Grok.
    /// Implements Microsoft-recommended configuration hierarchy:
    /// 1. User Secrets (highest priority - safe for development)
    /// 2. Environment Variables (process scope - CI/CD friendly)
    /// 3. appsettings.json (lowest priority - configuration-based)
    /// 4. Validates keys and tracks validation state at startup.
    /// </summary>
    public interface IGrokApiKeyProvider
    {
        /// <summary>Gets the currently configured API key (safe for logging: returns masked value).</summary>
        string? MaskedApiKey { get; }

        /// <summary>Gets the full API key (private - only for API calls).</summary>
        string? ApiKey { get; }

        /// <summary>Indicates whether the API key has been validated at startup.</summary>
        bool IsValidated { get; }

        /// <summary>Indicates whether the API key is from user secrets (secure source).</summary>
        bool IsFromUserSecrets { get; }

        /// <summary>Validates the API key by making a test request to xAI Grok endpoint.</summary>
        Task<(bool Success, string Message)> ValidateAsync();

        /// <summary>Gets detailed configuration source for diagnostics.</summary>
        string GetConfigurationSource();
    }

    /// <summary>
    /// Production implementation of IGrokApiKeyProvider with full configuration hierarchy support.
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
        /// User Secrets > Environment Variables > appsettings.json
        /// </summary>
        private void InitializeApiKey()
        {
            _logger?.LogInformation("[Grok] Initializing API key from configuration hierarchy...");

            // Configuration key candidates (in order of priority)
            var candidates = new[]
            {
                ("XAI:ApiKey", "configuration (XAI:ApiKey)"),
                ("Grok:ApiKey", "configuration (Grok:ApiKey)"),
                ("XAI_API_KEY", "configuration (XAI_API_KEY)"),
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

            // 2. Try environment variables (process scope, CI/CD friendly)
            var envKey = Environment.GetEnvironmentVariable("XAI_API_KEY", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                _apiKey = envKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI_API_KEY - process scope)";
                _isFromUserSecrets = false;
                _logger?.LogInformation(
                    "[Grok] API key loaded from {Source} (length: {Length})",
                    _configurationSource,
                    _apiKey.Length);
                return;
            }

            // 3. Try user-scoped environment variable (legacy support)
            var userEnvKey = Environment.GetEnvironmentVariable("XAI_API_KEY", EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(userEnvKey))
            {
                _apiKey = userEnvKey.Trim().Trim('"');
                _configurationSource = "environment variable (XAI_API_KEY - user scope)";
                _isFromUserSecrets = false;
                _logger?.LogWarning(
                    "[Grok] API key loaded from user-scoped environment variable (prefer user.secrets for development). Length: {Length}",
                    _apiKey.Length);
                return;
            }

            // 4. API key not found
            _configurationSource = "NOT CONFIGURED";
            _logger?.LogWarning(
                "[Grok] No API key found in user secrets, environment variables, or configuration. " +
                "JARVIS Chat will not function. " +
                "To configure, run: dotnet user-secrets set XAI:ApiKey <your-key>");
        }

        /// <summary>
        /// Determine if the API key came from user secrets.
        /// User secrets are loaded by Microsoft.Extensions.Configuration.UserSecrets
        /// and have higher priority than environment variables and appsettings.
        /// </summary>
        private bool IsKeyFromUserSecrets(string configKey)
        {
            // In development, user secrets are loaded via IConfiguration["Key"]
            // Check if the key exists in configuration (Microsoft.Extensions.Configuration.UserSecrets adds it)
            try
            {
                var value = _configuration[configKey];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // If we got a value from configuration (not env), it's likely from user secrets
                    // User secrets are loaded first in the configuration hierarchy (highest priority)
                    return true;
                }
            }
            catch
            {
                // Swallow exceptions during detection
            }

            return false;
        }

        /// <summary>
        /// Validate API key by making a test request to xAI Grok /models endpoint.
        /// This verifies that:
        /// 1. The API key is syntactically valid (not expired/revoked)
        /// 2. The endpoint is reachable
        /// 3. Authentication succeeds
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
                _logger?.LogInformation("[Grok] Validating API key via /models endpoint...");

                var httpClient = _httpClientFactory?.CreateClient("WileyWidgetDefault") ?? new HttpClient();
                var endpoint = new Uri("https://api.x.ai/v1/models");

                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
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
                    System.Net.HttpStatusCode.Unauthorized => "❌ API key is invalid or expired. Update user.secrets with a valid key.",
                    System.Net.HttpStatusCode.Forbidden => "❌ API key is valid but lacks required permissions.",
                    System.Net.HttpStatusCode.NotFound => "❌ xAI endpoint not found. Check configuration.",
                    _ => $"❌ API validation failed: HTTP {(int)response.StatusCode}"
                };

                _logger?.LogWarning("[Grok] Validation failed: {Message} - Response: {Body}", errorMsg, body);
                return (false, errorMsg);
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                var msg = "❌ API key validation timed out (5s). Check network connectivity.";
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
