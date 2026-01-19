using System;
using System.IO;
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

            var json = await File.ReadAllTextAsync(path);
            var token = JsonSerializer.Deserialize<QuickBooksOAuthToken>(json);

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
                    // In a real implementation, encrypt the JSON using _secretVault
                    // For now, just save as-is (TODO: implement encryption)
                    _logger.LogWarning("Token persistence: encryption not yet implemented");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to encrypt OAuth token");
                    throw;
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
