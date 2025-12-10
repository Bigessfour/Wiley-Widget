using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Production-ready local secret store that persists values to the local application data directory.
/// Provides a secure, file-based alternative to environment variables for sensitive configuration.
/// Includes migration utilities for production deployment.
/// </summary>
[Obsolete("LocalSecretVaultService is deprecated. Use EncryptedLocalSecretVaultService for secure encrypted storage instead.")]
public sealed class LocalSecretVaultService : ISecretVaultService, IDisposable
{
    private readonly string _secretsPath;
    private readonly ILogger<LocalSecretVaultService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public LocalSecretVaultService(ILogger<LocalSecretVaultService> logger)
    {
        _logger = logger;

        _logger.LogWarning("⚠️ LocalSecretVaultService is deprecated and stores secrets in plaintext. Consider migrating to EncryptedLocalSecretVaultService for secure encrypted storage.");

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDirectory = Path.Combine(appData, "WileyWidget", "Secrets");
        Directory.CreateDirectory(baseDirectory);
        _secretsPath = Path.Combine(baseDirectory, "secrets.json");
    }

    public async Task<string?> GetSecretAsync(string secretName)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new ArgumentException("Secret name is required", nameof(secretName));
        }

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var secrets = await LoadSecretsAsync().ConfigureAwait(false);
            return secrets.TryGetValue(secretName, out var value) ? value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read secret {SecretName} from local vault", secretName);
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public string? GetSecret(string secretName)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new ArgumentException("Secret name is required", nameof(secretName));
        }

        _fileLock.Wait();
        try
        {
            var secrets = LoadSecrets();
            return secrets.TryGetValue(secretName, out var value) ? value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read secret {SecretName} from local vault", secretName);
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void StoreSecret(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key is required", nameof(key));
        }

        _fileLock.Wait();
        try
        {
            var secrets = LoadSecrets();
            secrets[key] = value;
            SaveSecretsAsync(secrets).Wait();
            _logger.LogInformation("Secret {SecretKey} stored in local vault", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist secret {SecretKey} to local vault", key);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SetSecretAsync(string secretName, string value)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new ArgumentException("Secret name is required", nameof(secretName));
        }

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var secrets = await LoadSecretsAsync().ConfigureAwait(false);
            secrets[secretName] = value;
            await SaveSecretsAsync(secrets).ConfigureAwait(false);
            _logger.LogInformation("Secret {SecretName} stored in local vault", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist secret {SecretName} to local vault", secretName);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await LoadSecretsAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local secret vault verification failed");
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadSecretsAsync()
    {
        if (!File.Exists(_secretsPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = new FileStream(_secretsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream).ConfigureAwait(false)
               ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> LoadSecrets()
    {
        if (!File.Exists(_secretsPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var stream = new FileStream(_secretsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
               ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveSecretsAsync(Dictionary<string, string> secrets)
    {
        // Write atomically: write to temp file then replace
        var tmp = _secretsPath + ".tmp";
        var options = new JsonSerializerOptions { WriteIndented = true };
        await using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, secrets, options).ConfigureAwait(false);
        }

        // Ensure file permissions are restricted to current user
        try
        {
            var fileInfo = new FileInfo(tmp);
            var security = fileInfo.GetAccessControl();
            // Remove existing rules and add current user full control
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var user = identity?.User;
            if (user != null)
            {
                // Clear existing and add allow for current user only
                var rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
                foreach (System.Security.AccessControl.FileSystemAccessRule r in rules)
                {
                    security.RemoveAccessRule(r);
                }
                security.SetOwner(user);
                security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(user,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));
                fileInfo.SetAccessControl(security);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set file ACL on secrets tmp file");
        }

        // Replace existing file atomically
        File.Replace(tmp, _secretsPath, null);
    }

    public void Dispose()
    {
        _fileLock.Dispose();
    }

    /// <summary>
    /// Migrates secrets from environment variables and .env file to the local vault.
    /// This method is called automatically on service initialization for production convenience.
    /// </summary>
    public async Task MigrateSecretsFromEnvironmentAsync()
    {
        try
        {
            var secretsToMigrate = new Dictionary<string, string>
            {
                // Syncfusion
                ["syncfusion-license-key"] = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ?? "",

                // QuickBooks
                ["QuickBooks-ClientId"] = Environment.GetEnvironmentVariable("QUICKBOOKS_CLIENT_ID") ?? "",
                ["QuickBooks-ClientSecret"] = Environment.GetEnvironmentVariable("QUICKBOOKS_CLIENT_SECRET") ?? "",
                ["QuickBooks-RedirectUri"] = Environment.GetEnvironmentVariable("QUICKBOOKS_REDIRECT_URI") ?? "",
                ["QuickBooks-Environment"] = Environment.GetEnvironmentVariable("QUICKBOOKS_ENVIRONMENT") ?? "Sandbox",

                // XAI
                ["XAI-ApiKey"] = Environment.GetEnvironmentVariable("XAI_API_KEY") ?? "",
                ["XAI-BaseUrl"] = Environment.GetEnvironmentVariable("XAI_BASE_URL") ?? "https://api.x.ai",

                // Prism
                ["Prism-LicenseKey"] = Environment.GetEnvironmentVariable("PRISM_LICENSE_KEY") ?? "",

                // Database (if needed)
                ["Database-ConnectionString"] = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING") ?? "",
            };

            bool migratedAny = false;
            foreach (var (key, value) in secretsToMigrate)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Only set if not already in vault
                    var existing = await GetSecretAsync(key);
                    if (string.IsNullOrEmpty(existing))
                    {
                        await SetSecretAsync(key, value);
                        migratedAny = true;
                        _logger.LogInformation("Migrated secret '{SecretKey}' from environment to local vault", key);
                    }
                }
            }

            if (migratedAny)
            {
                _logger.LogInformation("Secret migration from environment variables completed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to migrate secrets from environment variables");
        }
    }

    /// <summary>
    /// Production utility method to populate all required secrets.
    /// Call this method during application setup or from admin tools.
    /// </summary>
    public async Task PopulateProductionSecretsAsync()
    {
        var productionSecrets = new Dictionary<string, string>
        {
            // Core application secrets - UPDATE THESE WITH REAL PRODUCTION VALUES
            ["syncfusion-license-key"] = "YOUR_SYNCFUSION_LICENSE_KEY_HERE",
            ["XAI-ApiKey"] = "YOUR_XAI_API_KEY_HERE",
            ["XAI-BaseUrl"] = "https://api.x.ai",
            ["Prism-LicenseKey"] = "1ZIj71/sRfsl56M/51s3Bc2FjO02xQUpXEI4H2cXo2Qet6irr+1ojRbPIDMCPkjqjG7zV07GuG3ZxskMzkqE/QlXk7vSzhw9DCi9NQGfZqc=",

            // QuickBooks integration (Sandbox defaults - update for production)
            ["QuickBooks-ClientId"] = "YOUR_QUICKBOOKS_CLIENT_ID",
            ["QuickBooks-ClientSecret"] = "YOUR_QUICKBOOKS_CLIENT_SECRET",
            ["QuickBooks-RedirectUri"] = "http://localhost:8080/callback",
            ["QuickBooks-Environment"] = "Sandbox",

            // Database connection (if using external database)
            ["Database-ConnectionString"] = "",
        };

        foreach (var (key, defaultValue) in productionSecrets)
        {
            var existing = await GetSecretAsync(key);
            if (string.IsNullOrEmpty(existing) && !string.IsNullOrEmpty(defaultValue) && !defaultValue.Contains("YOUR_", StringComparison.Ordinal))
            {
                await SetSecretAsync(key, defaultValue);
                _logger.LogInformation("Set production secret: {SecretKey}", key);
            }
            else if (string.IsNullOrEmpty(existing))
            {
                _logger.LogWarning("Production secret '{SecretKey}' not set - update with real value", key);
            }
        }

        _logger.LogInformation("Production secrets population completed");
    }

    /// <summary>
    /// Exports all secrets to a JSON string for backup purposes.
    /// WARNING: This contains sensitive data - handle with care.
    /// </summary>
    public async Task<string> ExportSecretsAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var secrets = await LoadSecretsAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export secrets");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Imports secrets from a JSON string.
    /// WARNING: This will overwrite existing secrets with the same keys.
    /// </summary>
    public async Task ImportSecretsAsync(string jsonSecrets)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var importedSecrets = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonSecrets);
            if (importedSecrets != null)
            {
                var existingSecrets = await LoadSecretsAsync().ConfigureAwait(false);
                foreach (var (key, value) in importedSecrets)
                {
                    existingSecrets[key] = value;
                }
                await SaveSecretsAsync(existingSecrets).ConfigureAwait(false);
                _logger.LogInformation("Imported {Count} secrets from JSON", importedSecrets.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import secrets from JSON");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Lists all secret keys (without values) for inventory purposes.
    /// </summary>
    public async Task<IEnumerable<string>> ListSecretKeysAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var secrets = await LoadSecretsAsync().ConfigureAwait(false);
            return secrets.Keys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list secret keys");
            return Array.Empty<string>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteSecretAsync(string secretName)
    {
        if (string.IsNullOrWhiteSpace(secretName)) throw new ArgumentException("Secret name is required", nameof(secretName));

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var secrets = await LoadSecretsAsync().ConfigureAwait(false);
            if (secrets.Remove(secretName))
            {
                await SaveSecretsAsync(secrets).ConfigureAwait(false);
                _logger.LogInformation("Deleted secret {SecretName} from local vault", secretName);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task RotateSecretAsync(string secretName, string newValue)
    {
        if (string.IsNullOrWhiteSpace(secretName)) throw new ArgumentException("Secret name is required", nameof(secretName));

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var secrets = await LoadSecretsAsync().ConfigureAwait(false);
            var oldValue = secrets.TryGetValue(secretName, out var existed) ? existed : null;

            // write new value
            secrets[secretName] = newValue;
            await SaveSecretsAsync(secrets).ConfigureAwait(false);

            // verify by reloading
            var reloaded = await LoadSecretsAsync().ConfigureAwait(false);
            if (!reloaded.TryGetValue(secretName, out var verified) || verified != newValue)
            {
                throw new InvalidOperationException("Failed to verify new secret after rotation");
            }

            // no further action needed (old value replaced)
            _logger.LogInformation("Rotated secret {SecretName}", secretName);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Get diagnostic information about the local secret vault.
    /// </summary>
    public async Task<string> GetDiagnosticsAsync()
    {
        var sb = new System.Text.StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        sb.AppendLine("=== Local Secret Vault Diagnostics ===");
        sb.AppendLine($"Secrets File Path: {_secretsPath}");
        sb.AppendLine($"Directory Exists: {Directory.Exists(Path.GetDirectoryName(_secretsPath) ?? string.Empty).ToString(culture)}");
        sb.AppendLine($"Secrets File Exists: {File.Exists(_secretsPath).ToString(culture)}");

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var secrets = await LoadSecretsAsync().ConfigureAwait(false);
            sb.AppendLine($"Secret Keys Count: {secrets.Count.ToString(culture)}");
            sb.AppendLine($"Secret Keys: {string.Join(", ", secrets.Keys)}");

            // Test write permissions
            var testFile = _secretsPath + ".diag";
            try
            {
                await File.WriteAllTextAsync(testFile, "diag").ConfigureAwait(false);
                File.Delete(testFile);
                sb.AppendLine("Write Permissions: OK");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Write Permissions: FAILED - {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Error reading secrets: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }

        return sb.ToString();
    }
}
