using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Abstractions;

namespace WileyWidget.Configuration;

/// <summary>
/// Provides secure configuration resolution with multi-tier fallback strategies.
/// Integrates with EncryptedLocalSecretVaultService, IConfiguration, and environment variables.
/// </summary>
/// <remarks>
/// Resolution Order:
/// 1. Secret Vault (encrypted AES-256 storage)
/// 2. IConfiguration (appsettings.json, user secrets, env vars)
/// 3. Environment Variables (process-level → user-level)
/// 4. Fallback value (if provided)
/// 5. Throws InvalidOperationException if required and not found
/// </remarks>
public class SecureConfigurationHelper
{
    private readonly IConfiguration _configuration;
    private readonly ISecretVaultService _secretVault;
    private readonly ILogger<SecureConfigurationHelper> _logger;

    public SecureConfigurationHelper(
        IConfiguration configuration,
        ISecretVaultService secretVault,
        ILogger<SecureConfigurationHelper> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _secretVault = secretVault ?? throw new ArgumentNullException(nameof(secretVault));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves a configuration value with the following precedence:
    /// 1. Secret Vault (encrypted storage)
    /// 2. IConfiguration (appsettings.json, user secrets, env vars)
    /// 3. Environment Variables (process and user scope)
    /// 4. Fallback value (if provided)
    /// 5. Throws if required and not found
    /// </summary>
    /// <param name="key">Configuration key to resolve (e.g., "QBO_CLIENT_ID")</param>
    /// <param name="vaultKeyAliases">Additional vault keys to try (e.g., ["QBO-CLIENT-ID", "QuickBooks-ClientId"])</param>
    /// <param name="fallbackValue">Value to use if not found and not required</param>
    /// <param name="required">If true, throws InvalidOperationException when value not found</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Resolved configuration value</returns>
    /// <exception cref="InvalidOperationException">Thrown when required value not found</exception>
    public async Task<string> GetSecureValueAsync(
        string key,
        string[]? vaultKeyAliases = null,
        string? fallbackValue = null,
        bool required = true,
        CancellationToken cancellationToken = default)
    {
        // 1. Try secret vault (with aliases)
        var vaultKeys = new[] { key }.Concat(vaultKeyAliases ?? Array.Empty<string>());
        foreach (var vaultKey in vaultKeys)
        {
            try
            {
                var vaultValue = await _secretVault.GetSecretAsync(vaultKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(vaultValue))
                {
                    _logger.LogDebug(
                        "Resolved '{Key}' from secret vault (alias: {VaultKey})",
                        key,
                        vaultKey
                    );
                    return vaultValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(
                    ex,
                    "Failed to retrieve '{Key}' from vault (alias: {VaultKey}) - continuing fallback chain",
                    key,
                    vaultKey
                );
            }
        }

        // 2. Try IConfiguration (appsettings + env vars)
        var configValue = _configuration[key];
        if (!string.IsNullOrWhiteSpace(configValue) && !IsPlaceholder(configValue))
        {
            _logger.LogDebug("Resolved '{Key}' from IConfiguration", key);
            return configValue;
        }

        // 3. Try environment variables directly (cross-platform)
        var envValue = GetEnvironmentVariableAnyScope(key);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            _logger.LogDebug("Resolved '{Key}' from environment variable", key);
            return envValue;
        }

        // 4. Fallback value
        if (!required && fallbackValue != null)
        {
            _logger.LogInformation(
                "Using fallback value for optional configuration key '{Key}'",
                key
            );
            return fallbackValue;
        }

        // 5. Required but not found
        var message = $"Required configuration key '{key}' not found in secret vault, IConfiguration, or environment variables.";
        _logger.LogError(message);
        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Retrieves environment variable from process scope, then user scope (cross-platform).
    /// Mirrors QuickBooksService.GetEnvironmentVariableAnyScope() for consistency.
    /// </summary>
    private static string? GetEnvironmentVariableAnyScope(string key)
    {
        return Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
    }

    /// <summary>
    /// Checks if a configuration value is an unresolved placeholder (e.g., "${VAR_NAME}").
    /// </summary>
    /// <param name="value">Configuration value to check</param>
    /// <returns>True if value is a placeholder pattern</returns>
    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.StartsWith("${", StringComparison.Ordinal)
            && value.EndsWith("}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates that all required environment variables/configuration keys are present.
    /// Logs warnings for missing optional variables.
    /// </summary>
    /// <param name="requiredKeys">Keys that must be present</param>
    /// <param name="optionalKeys">Keys that are optional but recommended</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with missing key details</returns>
    public async Task<ValidationResult> ValidateRequiredSecretsAsync(
        string[] requiredKeys,
        string[]? optionalKeys = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true };

        // Check required keys
        foreach (var key in requiredKeys)
        {
            try
            {
                await GetSecureValueAsync(key, required: true, cancellationToken: cancellationToken);
                _logger.LogDebug("✓ Required key '{Key}' validated", key);
            }
            catch (InvalidOperationException)
            {
                result.IsValid = false;
                result.MissingRequired.Add(key);
                _logger.LogError("✗ Required key '{Key}' is missing", key);
            }
        }

        // Check optional keys
        foreach (var key in optionalKeys ?? Array.Empty<string>())
        {
            try
            {
                await GetSecureValueAsync(key, required: false, cancellationToken: cancellationToken);
                _logger.LogDebug("✓ Optional key '{Key}' is set", key);
            }
            catch
            {
                result.MissingOptional.Add(key);
                _logger.LogWarning(
                    "⚠ Optional key '{Key}' is missing - feature may be degraded",
                    key
                );
            }
        }

        return result;
    }

    /// <summary>
    /// Validation result containing lists of missing required and optional configuration keys.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets or sets whether all required keys were found.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets list of missing required configuration keys.
        /// </summary>
        public List<string> MissingRequired { get; } = new();

        /// <summary>
        /// Gets list of missing optional configuration keys.
        /// </summary>
        public List<string> MissingOptional { get; } = new();

        /// <summary>
        /// Gets a formatted error message for missing required keys.
        /// </summary>
        public string GetErrorMessage()
        {
            if (IsValid)
                return string.Empty;

            return $"Missing required configuration keys: {string.Join(", ", MissingRequired)}";
        }
    }
}
