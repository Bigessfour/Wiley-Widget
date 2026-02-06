using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Manages persistence and retrieval of QuickBooks OAuth tokens.
/// Supports encrypted local file storage to avoid re-authentication on app restart.
/// Also stores the realm ID (tenant ID) needed for API calls.
/// </summary>
public sealed class QuickBooksTokenStore : IDisposable
{
    private readonly ILogger<QuickBooksTokenStore> _logger;
    private readonly QuickBooksOAuthOptions _options;
    private readonly ISecretVaultService? _secretVault;
    private QuickBooksOAuthToken? _cachedToken;
    private string? _cachedRealmId;

    public QuickBooksTokenStore(
        ILogger<QuickBooksTokenStore> logger,
        IOptions<QuickBooksOAuthOptions> options,
        ISecretVaultService? secretVault = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _secretVault = secretVault;
    }

    /// <summary>
    /// Gets the cached token, loading from disk if necessary.
    /// Returns null if no token is cached or persisted.
    /// </summary>
    public async Task<QuickBooksOAuthToken?> GetTokenAsync()
    {
        // Return in-memory cache if available
        if (_cachedToken is not null && !_cachedToken.IsExpiredOrSoonToExpire(_options.TokenExpiryBufferSeconds))
        {
            _logger.LogDebug("Returning cached OAuth token (not expired)");
            return _cachedToken;
        }

        // Load from disk if persistence is enabled
        if (_options.EnableTokenPersistence && !string.IsNullOrWhiteSpace(_options.TokenCachePath))
        {
            var token = await LoadFromDiskAsync();
            if (token?.IsValid == true)
            {
                _cachedToken = token;
                _logger.LogInformation("Loaded OAuth token from disk cache");
                return token;
            }
        }

        _logger.LogDebug("No valid cached OAuth token available");
        return null;
    }

    /// <summary>
    /// Saves the token to memory cache and optionally to disk.
    /// </summary>
    public async Task SaveTokenAsync(QuickBooksOAuthToken token)
    {
        if (!token.IsValid)
        {
            _logger.LogWarning("Attempt to save invalid OAuth token");
            return;
        }

        _cachedToken = token;

        if (_options.EnableTokenPersistence && !string.IsNullOrWhiteSpace(_options.TokenCachePath))
        {
            await SaveToDiskAsync(token);
            _logger.LogInformation("Saved OAuth token to disk cache");
        }
    }

    /// <summary>
    /// Clears the cached token and optionally removes from disk.
    /// </summary>
    public async Task ClearTokenAsync()
    {
        _cachedToken = null;

        if (_options.EnableTokenPersistence && !string.IsNullOrWhiteSpace(_options.TokenCachePath))
        {
            try
            {
                if (File.Exists(_options.TokenCachePath))
                {
                    File.Delete(_options.TokenCachePath);
                    _logger.LogInformation("Cleared OAuth token from disk cache");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear disk-cached OAuth token at {Path}", _options.TokenCachePath);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the cached realm ID (tenant/company ID).
    /// </summary>
    public string? GetRealmId()
    {
        return _cachedRealmId;
    }

    /// <summary>
    /// Sets the realm ID for API requests.
    /// Called during OAuth flow when realm ID is extracted from callback.
    /// </summary>
    public void SetRealmId(string realmId)
    {
        _cachedRealmId = realmId;
        _logger.LogInformation("RealmId cached: {RealmId}", realmId);
    }

    private async Task<QuickBooksOAuthToken?> LoadFromDiskAsync()
    {
        try
        {
            var path = _options.TokenCachePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            var content = await File.ReadAllTextAsync(path);

            // Try to decrypt if a secret vault is available and content looks like base64
            if (_secretVault is not null && IsBase64String(content))
            {
                try
                {
                    // Decrypt using DPAPI
                    var encryptedBytes = Convert.FromBase64String(content);
                    byte[]? entropy = null;

                    // Try to get entropy from secret vault if available
                    try
                    {
                        var entropyValue = await _secretVault.GetSecretAsync("QuickBooksTokenStore_Entropy");
                        if (!string.IsNullOrEmpty(entropyValue))
                        {
                            entropy = Encoding.UTF8.GetBytes(entropyValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not retrieve entropy from secret vault - proceeding without additional entropy");
                    }

                    // Decrypt
                    var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, entropy, DataProtectionScope.CurrentUser);
                    content = Encoding.UTF8.GetString(decryptedBytes);
                    _logger.LogDebug("Decrypted OAuth token from disk");
                }
                catch (CryptographicException ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt OAuth token - it may be corrupted or encrypted with different credentials. Treating as unencrypted.");
                    // Fall through - content remains as-is, attempt to deserialize as JSON
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt OAuth token");
                    return null;
                }
            }

            // Deserialize JSON
            var token = JsonSerializer.Deserialize<QuickBooksOAuthToken>(content);

            if (token?.IsValid == true && !token.IsExpired)
            {
                _logger.LogDebug("Loaded valid OAuth token from {Path}", path);
                return token;
            }

            _logger.LogWarning("Cached OAuth token is invalid or expired");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load OAuth token from disk cache");
            return null;
        }
    }

    /// <summary>
    /// Helper to determine if a string is likely base64-encoded.
    /// </summary>
    private static bool IsBase64String(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        // Check if string contains only valid base64 characters
        return value.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=');
    }

    private async Task SaveToDiskAsync(QuickBooksOAuthToken token)
    {
        try
        {
            var path = _options.TokenCachePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Serialize token to JSON
            var json = JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = true });

            // Encrypt before writing to disk (if secret vault available)
            var content = json;
            if (_secretVault is not null)
            {
                try
                {
                    // Encrypt the JSON using DPAPI with optional entropy from secret vault
                    var jsonBytes = Encoding.UTF8.GetBytes(json);
                    byte[]? entropy = null;

                    // Try to get entropy from secret vault if available
                    try
                    {
                        var entropyValue = await _secretVault.GetSecretAsync("QuickBooksTokenStore_Entropy");
                        if (!string.IsNullOrEmpty(entropyValue))
                        {
                            entropy = Encoding.UTF8.GetBytes(entropyValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not retrieve entropy from secret vault - proceeding without additional entropy");
                    }

                    // Encrypt using Windows DPAPI
                    var encryptedBytes = ProtectedData.Protect(jsonBytes, entropy, DataProtectionScope.CurrentUser);
                    content = Convert.ToBase64String(encryptedBytes);
                    _logger.LogInformation("OAuth token encrypted using DPAPI before disk persistence");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to encrypt OAuth token - saving unencrypted as fallback");
                    // Fall back to unencrypted storage to avoid losing the token
                    content = json;
                }
            }

            await File.WriteAllTextAsync(path, content);
            _logger.LogDebug("Saved OAuth token to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save OAuth token to disk");
            throw;
        }
    }

    public void Dispose()
    {
        _cachedToken = null;
    }
}
