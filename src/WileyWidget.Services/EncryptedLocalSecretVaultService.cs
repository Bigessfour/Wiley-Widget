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
using WileyWidget.Services.Abstractions;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Encrypted local secret vault service using Windows DPAPI.
/// Provides secure storage of secrets encrypted with user-specific keys.
/// </summary>
public sealed class EncryptedLocalSecretVaultService : ISecretVaultService, IInitializable, IDisposable
{
    private readonly ILogger<EncryptedLocalSecretVaultService> _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly string _vaultDirectory;
    private readonly string _entropyFile;
    private byte[]? _entropy;
    private bool _disposed = false;

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

    private byte[]? LoadOrGenerateEntropy()
    {
        // Try to load entropy from file
        try
        {
            if (File.Exists(_entropyFile))
            {
                var entropyBytes = File.ReadAllBytes(_entropyFile);
                if (entropyBytes.Length >= 32)
                {
                    _logger.LogDebug("Loaded entropy from file: {EntropyFile}", _entropyFile);
                    return entropyBytes;
                }
                else
                {
                    _logger.LogWarning("Entropy file {EntropyFile} is too small, regenerating.", _entropyFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load entropy from file: {EntropyFile}", _entropyFile);
        }

        // Generate new entropy
        var newEntropy = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(newEntropy);
        }
        try
        {
            File.WriteAllBytes(_entropyFile, newEntropy);
            _logger.LogInformation("Generated and saved new entropy to file: {EntropyFile}", _entropyFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save new entropy to file: {EntropyFile}", _entropyFile);
        }
        return newEntropy;
    }

    /// <summary>
    /// Synchronous initialization for startup (loads or generates entropy, validates vault).
    /// </summary>
    /// <summary>
    /// Performs initialize.
    /// </summary>
    public void Initialize()
    {
        // This is safe to call again; ensures entropy and vault are ready.
        _entropy = LoadOrGenerateEntropy();
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

    public string? GetSecret(string key)
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// Performs storesecret. Parameters: key, value.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>

    public void StoreSecret(string key, string value)
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// Performs rotatesecret. Parameters: secretName, newValue.
    /// </summary>
    /// <param name="secretName">The secretName.</param>
    /// <param name="newValue">The newValue.</param>

    public Task RotateSecretAsync(string secretName, string newValue)
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// Performs migratesecretsfromenvironment.
    /// </summary>

    public Task MigrateSecretsFromEnvironmentAsync()
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// Performs populateproductionsecrets.
    /// </summary>

    public Task PopulateProductionSecretsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<string> ExportSecretsAsync()
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// Performs importsecrets. Imports data or configuration. Parameters: jsonSecrets.
    /// </summary>
    /// <param name="jsonSecrets">The jsonSecrets.</param>

    public Task ImportSecretsAsync(string jsonSecrets)
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// Performs deletesecret. Parameters: secretName.
    /// </summary>
    /// <param name="secretName">The secretName.</param>

    public Task DeleteSecretAsync(string secretName)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetDiagnosticsAsync()
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        // Intentionally a no-op disposal to make tests safe during teardown.
        // Implement resource cleanup here if this service acquires unmanaged resources in the future.
    }
}
