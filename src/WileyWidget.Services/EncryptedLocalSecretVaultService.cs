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

        try
        {
            // Use AppData for user-specific storage
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _vaultDirectory = Path.Combine(appData, "WileyWidget", "Secrets");

            // Ensure directory exists with robust error handling
            if (!Directory.Exists(_vaultDirectory))
            {
                try
                {
                    Directory.CreateDirectory(_vaultDirectory);
                    _logger.LogInformation("Created secret vault directory: {VaultDirectory}", _vaultDirectory);
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    // Attempt to fall back to a less-restricted temp location
                    _logger.LogWarning(uaEx, "Insufficient permissions creating vault directory {VaultDirectory}. Falling back to TEMP folder.", _vaultDirectory);
                    var tempVault = Path.Combine(Path.GetTempPath(), "WileyWidget", "Secrets");
                    _vaultDirectory = tempVault;
                    try
                    {
                        if (!Directory.Exists(_vaultDirectory))
                        {
                            Directory.CreateDirectory(_vaultDirectory);
                        }
                        _logger.LogInformation("Using fallback vault directory: {VaultDirectory}", _vaultDirectory);
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "Failed to create fallback vault directory: {VaultDirectory}", _vaultDirectory);
                        throw; // Re-throw: can't proceed without a writable folder
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create secret vault directory: {VaultDirectory}", _vaultDirectory);
                    throw;
                }

                // Set directory permissions (Windows only - restrict to current user)
                try
                {
                    var dirInfo = new DirectoryInfo(_vaultDirectory);
                    var security = dirInfo.GetAccessControl();
                    var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var user = identity?.User;
                    if (user != null)
                    {
                        security.SetOwner(user);
                        security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(user,
                            System.Security.AccessControl.FileSystemRights.FullControl,
                            System.Security.AccessControl.AccessControlType.Allow));
                        dirInfo.SetAccessControl(security);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set directory ACL on secret vault");
                }
            }

            // Ensure _entropyFile is aligned to final vault path (in case of fallback)
            _entropyFile = Path.Combine(_vaultDirectory, ".entropy");

            // Load or generate entropy
            _entropy = LoadOrGenerateEntropy();

            _logger.LogInformation("EncryptedLocalSecretVaultService initialized successfully. Vault: {VaultDirectory}", _vaultDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize EncryptedLocalSecretVaultService");
            throw;
        }
    }

    private byte[] LoadOrGenerateEntropy()
    {
        try
        {
            if (File.Exists(_entropyFile))
            {
                try
                {
                    // Load existing entropy (stored encrypted with DPAPI LocalMachine scope)
                    var encryptedEntropyBase64 = File.ReadAllText(_entropyFile);
                    var encryptedEntropy = Convert.FromBase64String(encryptedEntropyBase64);

                    // Decrypt entropy using machine-bound DPAPI for additional protection
                    var entropy = ProtectedData.Unprotect(
                        encryptedEntropy,
                        null, // No additional entropy for entropy itself (avoid recursion)
                        DataProtectionScope.LocalMachine); // Machine-bound

                    _logger.LogDebug("Loaded entropy from encrypted file");
                    return entropy;
                }
                catch (CryptographicException ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt existing entropy file - it may be corrupted or from a different machine/user. Regenerating entropy.");
                    // Delete corrupted entropy file so we generate new entropy
                    try
                    {
                        File.Delete(_entropyFile);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Failed to delete corrupted entropy file");
                    }
                    // Fall through to generate new entropy
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load existing entropy file. Regenerating entropy.");
                    // Fall through to generate new entropy
                }
            }

            // Generate new entropy (executed when file doesn't exist OR loading failed)
            using var rng = RandomNumberGenerator.Create();
            var newEntropy = new byte[32]; // 256 bits
            rng.GetBytes(newEntropy);

            // Encrypt entropy with machine-bound DPAPI before saving
            var newEncryptedEntropy = ProtectedData.Protect(
                newEntropy,
                null,
                DataProtectionScope.LocalMachine);

            var newEncryptedEntropyBase64 = Convert.ToBase64String(newEncryptedEntropy);

            // Save encrypted entropy (hidden file)
            File.WriteAllText(_entropyFile, newEncryptedEntropyBase64);
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
                _logger.LogWarning(ex, "Failed to restrict entropy file ACL - continuing with default permissions");
            }

            _logger.LogInformation("Generated new encryption entropy for secret vault");
            return newEntropy;
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

                // write atomically with proper error handling
                var tmp = filePath + ".tmp";

                // Clean up any stale tmp file from previous failed attempts
                if (File.Exists(tmp))
                {
                    try
                    {
                        File.Delete(tmp);
                        _logger.LogDebug("Cleaned up stale temporary file for '{SecretName}'", secretName);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(cleanupEx, "Failed to cleanup stale tmp file for '{SecretName}'", secretName);
                    }
                }

                // Explicitly create the tmp file to ensure proper ACL and to avoid Replace/FileNotFound issues
                try
                {
                    using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        var bytes = Encoding.UTF8.GetBytes(encryptedBase64);
                        await fs.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                        await fs.FlushAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception createTmpEx)
                {
                    _logger.LogWarning(createTmpEx, "Failed to create tmp file '{TmpFile}' atomically - falling back to WriteAllTextAsync", tmp);
                    await File.WriteAllTextAsync(tmp, encryptedBase64).ConfigureAwait(false);
                }

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

                // Atomic write operation with proper error handling
                try
                {
                    // Use Replace only if destination exists, otherwise Move
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            File.Replace(tmp, filePath, null);
                            _logger.LogDebug("Replaced existing secret file for '{SecretName}'", secretName);
                        }
                        catch (FileNotFoundException fnf)
                        {
                            _logger.LogWarning(fnf, "Replace failed - falling back to Move for '{SecretName}'", secretName);
                            File.Move(tmp, filePath);
                        }
                        catch (NotSupportedException nsEx)
                        {
                            _logger.LogWarning(nsEx, "Replace not supported on this filesystem for '{SecretName}' - using Move", secretName);
                            File.Move(tmp, filePath);
                        }
                    }
                    else
                    {
                        File.Move(tmp, filePath);
                        _logger.LogDebug("Created new secret file for '{SecretName}'", secretName);
                    }
                }
                catch (Exception moveEx)
                {
                    _logger.LogError(moveEx, "Failed to complete atomic write for '{SecretName}' - attempting cleanup", secretName);

                    // Cleanup tmp file on failure
                    if (File.Exists(tmp))
                    {
                        try
                        {
                            File.Delete(tmp);
                        }
                        catch (Exception cleanupEx)
                        {
                            _logger.LogWarning(cleanupEx, "Failed to cleanup tmp file after failed write");
                        }
                    }

                    throw; // Re-throw to propagate error
                }

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
            // Verify vault directory exists and is writable
            if (!Directory.Exists(_vaultDirectory))
            {
                _logger.LogError("Vault directory does not exist: {VaultDirectory}", _vaultDirectory);
                return false;
            }

            // Test directory write permissions
            var testPermFile = Path.Combine(_vaultDirectory, ".test_permissions");
            try
            {
                File.WriteAllText(testPermFile, "test");
                File.Delete(testPermFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vault directory is not writable: {VaultDirectory}", _vaultDirectory);
                return false;
            }

            // Test by trying to store and retrieve a test secret
            const string testKey = "__test_connection__";
            var testValue = "test_value_" + Guid.NewGuid().ToString("N");

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
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to cleanup test secret file");
            }

            var success = retrieved == testValue;
            if (success)
            {
                _logger.LogInformation("Secret vault connection test PASSED");
            }
            else
            {
                _logger.LogError("Secret vault connection test FAILED - retrieved value does not match");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Secret vault connection test FAILED with exception");
            return false;
        }
    }

    public async Task MigrateSecretsFromEnvironmentAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        var migratedSecrets = new List<string>();

        // Define environment variables to migrate
        // Wiley Widget specific: Syncfusion license, QuickBooks OAuth, XAI API
        var envVars = new[]
        {
            "SYNCFUSION_LICENSE_KEY",        // Syncfusion licensing
            "syncfusion-license-key",        // Alternative format
            "QBO_CLIENT_ID",                 // QuickBooks OAuth
            "QuickBooks-ClientId",           // Alternative format
            "QBO_CLIENT_SECRET",
            "QuickBooks-ClientSecret",
            "QBO_REDIRECT_URI",
            "QuickBooks-RedirectUri",
            "QBO_ENVIRONMENT",
            "QuickBooks-Environment",
            "XAI_API_KEY",                   // xAI Grok API
            "XAI-ApiKey",
            "XAI_BASE_URL",
            "XAI-BaseUrl",
            "OPENAI_API_KEY",                // OpenAI API (if used)
            "BOLD_LICENSE_KEY"               // Bold Reports
        };

        // Remove duplicates (case-insensitive) and migrate
        var uniqueVars = envVars.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var envVar in uniqueVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value) && !value.StartsWith("${", StringComparison.Ordinal)) // Skip placeholders
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

                // Create the tmp file explicitly to ensure ACL and atomic Replace compatibility
                try
                {
                    using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        var bytes = Encoding.UTF8.GetBytes(encryptedBase64);
                        await fs.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                        await fs.FlushAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception createTmpEx)
                {
                    _logger.LogWarning(createTmpEx, "Failed to create tmp file '{TmpFile}' in RotateSecretAsync using FileStream - falling back to WriteAllTextAsync", tmp);
                    await File.WriteAllTextAsync(tmp, encryptedBase64).ConfigureAwait(false);
                }
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

                // Use Replace only if destination exists, otherwise Move
                if (File.Exists(filePath))
                {
                    File.Replace(tmp, filePath, null);
                }
                else
                {
                    File.Move(tmp, filePath);
                }

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
        // Sanitize filename and add hash fragment to prevent collisions
        // e.g., "Quick Books-Id" and "QuickBooks_Id" both sanitize to "QuickBooks_Id"
        // Adding hash ensures uniqueness while maintaining readability
        var safeName = string.Join("_", secretName.Split(Path.GetInvalidFileNameChars()));

        // Add first 8 chars of SHA256 hash for collision prevention
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(secretName));
        var hashFragment = Convert.ToHexString(hashBytes)[..8];

        return Path.Combine(_vaultDirectory, $"{safeName}_{hashFragment}.secret");
    }

    /// <summary>
    /// Verify entropy file integrity and attempt to regenerate if tampered.
    /// </summary>
    private bool VerifyEntropyIntegrity()
    {
        try
        {
            if (!File.Exists(_entropyFile))
            {
                _logger.LogWarning("Entropy file missing - will regenerate on next operation");
                return false;
            }

            // Try to decrypt entropy - if it fails, it's been tampered with or corrupted
            var encryptedEntropyBase64 = File.ReadAllText(_entropyFile);
            var encryptedEntropy = Convert.FromBase64String(encryptedEntropyBase64);
            var testEntropy = ProtectedData.Unprotect(encryptedEntropy, null, DataProtectionScope.LocalMachine);

            // If we get here, entropy is valid
            return testEntropy.Length == 32; // Verify expected size
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Entropy file appears to be tampered or corrupted - regeneration required");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify entropy integrity");
            return false;
        }
    }

    /// <summary>
    /// Get diagnostic information about the secret vault.
    /// </summary>
    public async Task<string> GetDiagnosticsAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        var diagnostics = new StringBuilder();
        diagnostics.AppendLine("=== Encrypted Secret Vault Diagnostics ===");
        diagnostics.AppendLine($"Vault Directory: {_vaultDirectory}");
        diagnostics.AppendLine($"Directory Exists: {Directory.Exists(_vaultDirectory)}");
        diagnostics.AppendLine($"Entropy File: {_entropyFile}");
        diagnostics.AppendLine($"Entropy File Exists: {File.Exists(_entropyFile)}");
        diagnostics.AppendLine($"Entropy Loaded: {_entropy != null}");

        if (Directory.Exists(_vaultDirectory))
        {
            try
            {
                var secretFiles = Directory.GetFiles(_vaultDirectory, "*.secret");
                diagnostics.AppendLine($"Secret Files Found: {secretFiles.Length}");

                var keys = await ListSecretKeysAsync();
                diagnostics.AppendLine($"Secret Keys: {string.Join(", ", keys)}");

                // Test write permissions
                var testFile = Path.Combine(_vaultDirectory, ".diagnostic_test");
                try
                {
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    diagnostics.AppendLine("Write Permissions: OK");
                }
                catch (Exception ex)
                {
                    diagnostics.AppendLine($"Write Permissions: FAILED - {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                diagnostics.AppendLine($"Directory Access Error: {ex.Message}");
            }
        }

        // Test connection
        var connectionTest = await TestConnectionAsync();
        diagnostics.AppendLine($"Connection Test: {(connectionTest ? "PASSED" : "FAILED")}");

        return diagnostics.ToString();
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
