using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Services.AI
{
    /// <summary>
    /// Health check for xAI Grok API connectivity at startup.
    /// Verifies:
    /// 1. API key is configured (from user.secrets or config)
    /// 2. API endpoint is reachable
    /// 3. Authentication succeeds
    /// 4. Service is ready to handle chat requests
    ///
    /// Invoked during Application.RunStartupHealthCheckAsync() before showing main window.
    /// Implements IHealthCheck for .NET health check framework compatibility.
    /// </summary>
    public sealed class GrokHealthCheck : IHealthCheck
    {
        private readonly IGrokApiKeyProvider _keyProvider;
        private readonly ILogger<GrokHealthCheck>? _logger;

        public GrokHealthCheck(IGrokApiKeyProvider keyProvider, ILogger<GrokHealthCheck>? logger = null)
        {
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            _logger = logger;
        }

        /// <summary>
        /// Execute health check: Validate API key and connectivity.
        /// Returns Healthy if API is ready, Degraded if configured but unreachable,
        /// Unhealthy if not configured or authentication fails.
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("[HealthCheck] Starting Grok API health check...");

            try
            {
                // Check 1: API key is configured
                if (string.IsNullOrWhiteSpace(_keyProvider.ApiKey))
                {
                    var msg = "Grok API key not configured. JARVIS Chat will not function. " +
                              "Set via: dotnet user-secrets set XAI:ApiKey <your-key> " +
                              "OR environment variable: setx XAI__ApiKey <your-key>";
                    _logger?.LogError("[HealthCheck] {Message}", msg);
                    return HealthCheckResult.Unhealthy(
                        description: "API key not configured",
                        data: new Dictionary<string, object>
                        {
                            ["status"] = "UNCONFIGURED",
                            ["message"] = msg,
                            ["configSource"] = _keyProvider.GetConfigurationSource()
                        });
                }

                // Check 2: API key is from secure source
                var securityNote = _keyProvider.IsFromUserSecrets
                    ? "OK (from user.secrets - secure)"
                    : "WARNING (from environment/config - ensure no secrets in appsettings.json)";

                _logger?.LogInformation("[HealthCheck] API key configured {SecurityNote}", securityNote);

                // Check 3: Validate API key connectivity
                var (isValid, validationMsg) = await _keyProvider.ValidateAsync().ConfigureAwait(false);
                var safeValidationMsg = validationMsg ?? string.Empty;
                var safeMaskedKey = _keyProvider.MaskedApiKey ?? "[hidden]";
                var safeConfigSource = _keyProvider.GetConfigurationSource() ?? "unknown";

                if (isValid && _keyProvider.IsValidated)
                {
                    var displayMsg = string.IsNullOrEmpty(safeValidationMsg) ? "Validation successful" : safeValidationMsg;
                    _logger?.LogInformation("[HealthCheck] OK Grok API health check PASSED - {Message}", displayMsg);
                    return HealthCheckResult.Healthy(
                        description: "Grok API is reachable and authenticated",
                        data: new Dictionary<string, object>
                        {
                            ["status"] = "HEALTHY",
                            ["message"] = displayMsg,
                            ["apiKey"] = safeMaskedKey,
                            ["configSource"] = safeConfigSource
                        });
                }

                if (!isValid)
                {
                    var failMsg = string.IsNullOrEmpty(safeValidationMsg) ? "Validation failed" : safeValidationMsg;
                    _logger?.LogWarning("[HealthCheck] WARNING Grok API validation failed: {Message}", failMsg);
                    return HealthCheckResult.Degraded(
                        description: "Grok API configuration issue",
                        data: new Dictionary<string, object>
                        {
                            ["status"] = "DEGRADED",
                            ["message"] = failMsg,
                            ["apiKey"] = safeMaskedKey,
                            ["configSource"] = safeConfigSource,
                            ["recommendation"] = "Update via: dotnet user-secrets set XAI:ApiKey <your-key> OR setx XAI__ApiKey <your-key>"
                        });
                }

                // Check passed but not marked as validated (defensive)
                var responseMsg = string.IsNullOrEmpty(safeValidationMsg) ? "API is responsive" : safeValidationMsg;
                _logger?.LogInformation("[HealthCheck] Grok API is responsive (not yet fully validated) - {Message}", responseMsg);
                return HealthCheckResult.Healthy(
                    description: "Grok API is responsive",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "RESPONSIVE",
                        ["apiKey"] = safeMaskedKey,
                        ["configSource"] = safeConfigSource,
                        ["message"] = responseMsg
                    });
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("[HealthCheck] Grok API health check timed out");
                return HealthCheckResult.Unhealthy(
                    description: "Grok API health check timed out",
                    data: new Dictionary<string, object> { ["status"] = "TIMEOUT" });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[HealthCheck] Grok API health check failed with exception");
                return HealthCheckResult.Unhealthy(
                    description: $"Grok API health check failed: {ex.Message}",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "ERROR",
                        ["error"] = ex.GetType().Name,
                        ["message"] = ex.Message
                    },
                    exception: ex);
            }
        }
    }

    /// <summary>
    /// Health check for Chat History persistence readiness.
    /// Verifies database connectivity and schema readiness for conversation storage.
    /// </summary>
    public sealed class ChatHistoryHealthCheck : IHealthCheck
    {
        private readonly WileyWidget.Services.Abstractions.IConversationRepository? _conversationRepository;
        private readonly ILogger<ChatHistoryHealthCheck>? _logger;

        public ChatHistoryHealthCheck(
            WileyWidget.Services.Abstractions.IConversationRepository? conversationRepository = null,
            ILogger<ChatHistoryHealthCheck>? logger = null)
        {
            _conversationRepository = conversationRepository;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("[HealthCheck] Checking chat history persistence...");

            if (_conversationRepository == null)
            {
                _logger?.LogWarning("[HealthCheck] IConversationRepository not registered - chat history persistence disabled");
                return HealthCheckResult.Degraded(
                    description: "Chat history persistence not configured",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "DEGRADED",
                        ["message"] = "IConversationRepository not registered"
                    });
            }

            try
            {
                // Simple connectivity ping: Get count of conversations (fails if schema/DB missing)
                _ = await _conversationRepository.GetConversationsAsync(0, 1, cancellationToken).ConfigureAwait(false);

                _logger?.LogInformation("[HealthCheck] Chat history persistence PASSED - Database accessible");
                return HealthCheckResult.Healthy(
                    description: "Chat history persistence is ready",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "HEALTHY",
                        ["message"] = "Database accessible, conversation history can be persisted"
                    });
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("[HealthCheck] Chat history persistence check timed out");
                return HealthCheckResult.Degraded(
                    description: "Chat history persistence check timed out",
                    data: new Dictionary<string, object> { ["status"] = "TIMEOUT" });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[HealthCheck] Chat history persistence check failed");
                return HealthCheckResult.Degraded(
                    description: "Chat history persistence unavailable",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "DEGRADED",
                        ["error"] = ex.GetType().Name,
                        ["message"] = ex.Message,
                        ["recommendation"] = "Chat will continue without persistence. Check database connectivity."
                    },
                    exception: ex);
            }
        }
    }
}
