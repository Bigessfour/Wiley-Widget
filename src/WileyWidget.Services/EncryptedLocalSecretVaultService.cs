using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Encrypted local secret vault service using Windows DPAPI.
/// Provides secure storage of secrets encrypted with user-specific keys.
/// </summary>
public sealed class EncryptedLocalSecretVaultService : ISecretVaultService, IDisposable
{
    private readonly ILogger<EncryptedLocalSecretVaultService> _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly string _vaultDirectory;
    private readonly string _entropyFile;
    private byte[]? _entropy;
    private bool _disposed;

    public EncryptedLocalSecretVaultService(ILogger<EncryptedLocalSecretVaultService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Use AppData for user-specific storage
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _vaultDirectory = Path.Combine(appData, "WileyWidget", "Secrets");
        _entropyFile = Path.Combine(_vaultDirectory, ".entropy");

        // Ensure directory exists
        Directory.CreateDirectory(_vaultDirectory);

        // Load or generate entropy
        _entropy = LoadOrGenerateEntropy();
    }

    private byte[] LoadOrGenerateEntropy()
    {
        try
        {
            if (File.Exists(_entropyFile))
            {
                // Load existing entropy
                var entropyBase64 = File.ReadAllText(_entropyFile);
                return Convert.FromBase64String(entropyBase64);
            }
            else
            {
                // Generate new entropy
                using var rng = RandomNumberGenerator.Create();
                var entropy = new byte[32]; // 256 bits
                rng.GetBytes(entropy);

                // Save entropy (hidden file)
                File.WriteAllText(_entropyFile, Convert.ToBase64String(entropy));
                File.SetAttributes(_entropyFile, FileAttributes.Hidden);

                // Restrict entropy file to current user
                try
                {
                    var fi = new FileInfo(_entropyFile);
                    var sec = fi.GetAccessControl();
                    var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var user = identity?.User;
                    if (user != null)
                    {
                        var rules = sec.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
                        foreach (System.Security.AccessControl.FileSystemAccessRule r in rules)
                        {
                            sec.RemoveAccessRule(r);
                        }
                        sec.SetOwner(user);
                        sec.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(user,
                            System.Security.AccessControl.FileSystemRights.FullControl,
                            System.Security.AccessControl.AccessControlType.Allow));
                        fi.SetAccessControl(sec);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restrict entropy file ACL");
                }

                _logger.LogInformation("Generated new encryption entropy for secret vault");
                return entropy;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load/generate entropy");
            throw;
        }
    }

    public async Task<string?> GetSecretAsync(string secretName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));

        await _semaphore.WaitAsync();
        try
        {
            var filePath = GetSecretFilePath(secretName);
            if (!File.Exists(filePath))
            {
                return null;
            }

            var encryptedBase64 = await File.ReadAllTextAsync(filePath);
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);

            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                _entropy,
                DataProtectionScope.CurrentUser);

            var secret = Encoding.UTF8.GetString(decryptedBytes);
            _logger.LogDebug("Retrieved secret '{SecretName}' from encrypted vault", secretName);
            return secret;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt secret '{SecretName}' - may be corrupted or from different user/machine", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}'", secretName);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Synchronous variant used by configuration code paths where async is not possible.
    /// Uses blocking file I/O and semaphore waits to maintain thread-safety.
    /// </summary>
    public string? GetSecret(string secretName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));

        _semaphore.Wait();
        try
        {
            var filePath = GetSecretFilePath(secretName);
            if (!File.Exists(filePath))
            {
                return null;
            }

            var encryptedBase64 = File.ReadAllText(filePath);
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);

            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                _entropy,
                DataProtectionScope.CurrentUser);

            var secret = Encoding.UTF8.GetString(decryptedBytes);
            _logger.LogDebug("Retrieved secret '{SecretName}' from encrypted vault (sync)", secretName);
            return secret;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt secret '{SecretName}' - may be corrupted or from different user/machine", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret '{SecretName}' (sync)", secretName);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void StoreSecret(string key, string value)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        _semaphore.Wait();
        try
        {
            var filePath = GetSecretFilePath(key);
            var secretBytes = Encoding.UTF8.GetBytes(value);
            var encryptedBytes = ProtectedData.Protect(
                secretBytes,
                _entropy,
                DataProtectionScope.CurrentUser);
            var encryptedBase64 = Convert.ToBase64String(encryptedBytes);
            File.WriteAllText(filePath, encryptedBase64);
            _logger.LogDebug("Stored secret '{SecretName}' in encrypted vault (sync)", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store secret '{SecretName}' (sync)", key);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetSecretAsync(string secretName, string value)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));
        if (value == null) throw new ArgumentNullException(nameof(value));

        await _semaphore.WaitAsync();
        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(value);
            try
            {
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                var encryptedBase64 = Convert.ToBase64String(encryptedBytes);
                var filePath = GetSecretFilePath(secretName);

                // write atomically
                var tmp = filePath + ".tmp";
                await File.WriteAllTextAsync(tmp, encryptedBase64);

                try
                {
                    var fileInfo = new FileInfo(tmp);
                    var security = fileInfo.GetAccessControl();
                    var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var user = identity?.User;
                    if (user != null)
                    {
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
                    _logger.LogWarning(ex, "Failed to set file ACL on secret tmp file");
                }

                File.Replace(tmp, filePath, null);

                _logger.LogInformation("Secret '{SecretName}' stored in encrypted vault", secretName);
            }
            finally
            {
                // Clear plaintext bytes from memory
                if (plainBytes != null)
                {
                    Array.Clear(plainBytes, 0, plainBytes.Length);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store secret '{SecretName}'", secretName);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        try
        {
            // Test by trying to store and retrieve a test secret
            const string testKey = "__test_connection__";
            const string testValue = "test_value";

            await SetSecretAsync(testKey, testValue);
            var retrieved = await GetSecretAsync(testKey);

            // Clean up test secret
            try
            {
                var testFile = GetSecretFilePath(testKey);
                if (File.Exists(testFile))
                {
                    File.Delete(testFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            return retrieved == testValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            return false;
        }
    }

    public async Task MigrateSecretsFromEnvironmentAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        var migratedSecrets = new List<string>();

        // Define environment variables to migrate
        var envVars = new[]
        {
            "syncfusion-license-key",
            "QuickBooks-ClientId",
            "QuickBooks-ClientSecret",
            "QuickBooks-RedirectUri",
            "QuickBooks-Environment",
            "XAI-ApiKey",
            "XAI-BaseUrl"
        };

        foreach (var envVar in envVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value))
            {
                await SetSecretAsync(envVar, value);
                migratedSecrets.Add(envVar);
                _logger.LogInformation("Migrated secret '{SecretName}' from environment to encrypted vault", envVar);
            }
        }

        if (migratedSecrets.Any())
        {
            _logger.LogInformation("Secret migration from environment variables completed. Migrated: {Count} secrets",
                migratedSecrets.Count);
        }
        else
        {
            _logger.LogDebug("No environment variables found to migrate");
        }
    }

    public async Task PopulateProductionSecretsAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        // This would populate default production secrets
        // For now, just log that it's not implemented
        _logger.LogInformation("PopulateProductionSecretsAsync called - no default secrets to populate");
        await Task.CompletedTask;
    }

    public async Task<string> ExportSecretsAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        await _semaphore.WaitAsync();
        try
        {
            var secrets = new Dictionary<string, string>();
            var secretFiles = Directory.GetFiles(_vaultDirectory, "*.secret");

            foreach (var file in secretFiles)
            {
                var secretName = Path.GetFileNameWithoutExtension(file);
                if (secretName != ".entropy") // Skip entropy file
                {
                    var value = await GetSecretAsync(secretName);
                    if (value != null)
                    {
                        secrets[secretName] = value;
                    }
                }
            }

            var json = JsonSerializer.Serialize(secrets, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            _logger.LogWarning("Secrets exported to JSON - ensure secure handling of this data!");
            return json;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ImportSecretsAsync(string jsonSecrets)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(jsonSecrets)) throw new ArgumentNullException(nameof(jsonSecrets));

        await _semaphore.WaitAsync();
        try
        {
            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonSecrets);
            if (secrets == null)
            {
                throw new InvalidOperationException("Invalid JSON format for secrets import");
            }

            foreach (var kvp in secrets)
            {
                await SetSecretAsync(kvp.Key, kvp.Value);
            }

            _logger.LogInformation("Imported {Count} encrypted secrets from JSON", secrets.Count);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IEnumerable<string>> ListSecretKeysAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        await _semaphore.WaitAsync();
        try
        {
            var secretFiles = Directory.GetFiles(_vaultDirectory, "*.secret");
            var keys = secretFiles
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(k => k != ".entropy") // Exclude entropy file
                .OrderBy(k => k)
                .ToList();

            return keys;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteSecretAsync(string secretName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var filePath = GetSecretFilePath(secretName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted secret '{SecretName}' from encrypted vault", secretName);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RotateSecretAsync(string secretName, string newValue)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(newValue);
            try
            {
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    _entropy,
                    DataProtectionScope.CurrentUser);

                var encryptedBase64 = Convert.ToBase64String(encryptedBytes);
                var filePath = GetSecretFilePath(secretName);
                var tmp = filePath + ".tmp";

                await File.WriteAllTextAsync(tmp, encryptedBase64).ConfigureAwait(false);
                // try set ACL
                try
                {
                    var fileInfo = new FileInfo(tmp);
                    var security = fileInfo.GetAccessControl();
                    var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var user = identity?.User;
                    if (user != null)
                    {
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
                    _logger.LogWarning(ex, "Failed to set file ACL on secret tmp file during rotation");
                }

                File.Replace(tmp, filePath, null);

                // verify by reading back
                var decrypted = await GetSecretAsync(secretName).ConfigureAwait(false);
                if (decrypted != newValue)
                {
                    throw new InvalidOperationException("Verification failed after rotating secret");
                }

                _logger.LogInformation("Rotated secret '{SecretName}'", secretName);
            }
            finally
            {
                if (plainBytes != null)
                    Array.Clear(plainBytes, 0, plainBytes.Length);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private string GetSecretFilePath(string secretName)
    {
        // Sanitize filename
        var safeName = string.Join("_", secretName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_vaultDirectory, $"{safeName}.secret");
    }

    public void Dispose()
    {
        if (_disposed) return;

        _semaphore.Dispose();

        // Clear sensitive data from memory
        if (_entropy != null)
        {
            Array.Clear(_entropy, 0, _entropy.Length);
            _entropy = null;
        }

        _disposed = true;
    }
}
