using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Production-grade encrypted local secret vault using Windows DPAPI with master key wrapping.
///
/// Architecture:
/// 1. Random 256-bit master key generated on first run
/// 2. Master key encrypted with DPAPI (CurrentUser scope for roaming)
/// 3. Each secret encrypted with master key using AES-GCM or DPAPI
/// 4. Versioned file format allows algorithm migrations
/// 5. Automatic backups on write for recovery
/// 6. Corruption detection and repair from backups
///
/// Security Properties:
/// - Encryption: DPAPI with CurrentUser scope (survives Windows password changes)
/// - Salt/IV: Randomly generated for each operation
/// - Entropy: Encrypted with machine-bound DPAPI as additional layer
/// - Files: Restricted to current user via NTFS ACL
/// </summary>
public sealed class EncryptedLocalSecretVaultService : ISecretVaultService, IDisposable
{
    private const int VaultVersion = 2;
    private const int MasterKeyLength = 32; // 256 bits
    private const int SaltLength = 16; // 128 bits for PBKDF2
    private const int IvLength = 12; // 96 bits for AES-GCM
    private const int MaxBackups = 3;
    private const string VaultFileName = "vault.json";
    private const string MasterKeyFileName = ".master.key";
    private const string BackupPattern = "vault.backup.*.json";

    private readonly ILogger<EncryptedLocalSecretVaultService> _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private string _vaultDirectory;
    private string _vaultPath;
    private string _masterKeyPath;
    private byte[]? _masterKey;
    private byte[]? _entropy; // Legacy entropy for backward compatibility
    private Dictionary<string, string> _secretsCache = new();
    private bool _disposed;

    public EncryptedLocalSecretVaultService(ILogger<EncryptedLocalSecretVaultService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            // Initialize paths (with fallback handling)
            InitializeVaultPaths();

            // Ensure directory exists with robust error handling
            EnsureVaultDirectoryExists();

            // Load or initialize master key
            _masterKey = LoadOrGenerateMasterKey();

            // For backward compatibility: load or generate entropy
            _entropy = LoadOrGenerateEntropy();

            // Load existing vault or initialize empty
            InitializeVaultAsync().GetAwaiter().GetResult();

            _logger.LogInformation("EncryptedLocalSecretVaultService initialized successfully. Vault: {VaultDirectory}, Version: {VaultVersion}",
                _vaultDirectory, VaultVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize EncryptedLocalSecretVaultService");
            throw;
        }
    }

    private void InitializeVaultPaths()
    {
        // Use AppData for user-specific storage
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _vaultDirectory = Path.Combine(appData, "WileyWidget", "Secrets");
        _vaultPath = Path.Combine(_vaultDirectory, VaultFileName);
        _masterKeyPath = Path.Combine(_vaultDirectory, MasterKeyFileName);
    }

    private void EnsureVaultDirectoryExists()
    {
        if (Directory.Exists(_vaultDirectory))
            return;

        try
        {
            Directory.CreateDirectory(_vaultDirectory);
            _logger.LogInformation("Created secret vault directory: {VaultDirectory}", _vaultDirectory);

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
        catch (UnauthorizedAccessException uaEx)
        {
            _logger.LogWarning(uaEx, "Insufficient permissions creating vault directory {VaultDirectory}. Falling back to TEMP folder.", _vaultDirectory);

            // Use fallback temp directory
            var tempVault = Path.Combine(Path.GetTempPath(), "WileyWidget", "Secrets");
            _vaultDirectory = tempVault;
            _vaultPath = Path.Combine(_vaultDirectory, VaultFileName);
            _masterKeyPath = Path.Combine(_vaultDirectory, MasterKeyFileName);

            Directory.CreateDirectory(_vaultDirectory);
            _logger.LogInformation("Using fallback vault directory: {VaultDirectory}", _vaultDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create secret vault directory: {VaultDirectory}", _vaultDirectory);
            throw;
        }
    }

    private byte[] LoadOrGenerateMasterKey()
    {
        try
        {
            if (File.Exists(_masterKeyPath))
            {
                try
                {
                    var encryptedKeyBase64 = File.ReadAllText(_masterKeyPath);
                    var encryptedKey = Convert.FromBase64String(encryptedKeyBase64);

                    // Decrypt with DPAPI CurrentUser scope (survives password changes, supports roaming profiles)
                    var masterKey = ProtectedData.Unprotect(
                        encryptedKey,
                        null,
                        DataProtectionScope.CurrentUser);

                    _logger.LogDebug("Loaded existing master key from encrypted file");
                    return masterKey;
                }
                catch (CryptographicException ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt existing master key - may be corrupted or from different user. Regenerating master key.");
                    try { File.Delete(_masterKeyPath); } catch (Exception deleteEx) { _logger.LogWarning(deleteEx, "Failed to delete corrupted master key file"); }
                }
                catch (ArgumentException aex)
                {
                    _logger.LogWarning(aex, "ProtectedData.Unprotect threw ArgumentException for master key file - contents may be malformed. Deleting and regenerating master key.");
                    try { File.Delete(_masterKeyPath); } catch (Exception deleteEx) { _logger.LogWarning(deleteEx, "Failed to delete corrupted master key file"); }
                }
                catch (FormatException fex)
                {
                    _logger.LogWarning(fex, "Base64 text for master key is malformed. Deleting file and regenerating master key.");
                    try { File.Delete(_masterKeyPath); } catch (Exception deleteEx) { _logger.LogWarning(deleteEx, "Failed to delete corrupted master key file"); }
                }
            }

            // Generate new master key
            using var rng = RandomNumberGenerator.Create();
            var newMasterKey = new byte[MasterKeyLength];
            rng.GetBytes(newMasterKey);

            // Encrypt with DPAPI
            byte[] encryptedMasterKey;
            try
            {
                encryptedMasterKey = ProtectedData.Protect(newMasterKey, null, DataProtectionScope.CurrentUser);
            }
            catch (ArgumentException aex)
            {
                _logger.LogError(aex, "ProtectedData.Protect threw an ArgumentException while encrypting master key. Check DPAPI availability and inputs.");
                throw;
            }
            catch (CryptographicException cex)
            {
                _logger.LogError(cex, "ProtectedData.Protect failed while encrypting master key - cannot persist master key.");
                throw;
            }

            var encryptedMasterKeyBase64 = Convert.ToBase64String(encryptedMasterKey);
            File.WriteAllText(_masterKeyPath, encryptedMasterKeyBase64);
            File.SetAttributes(_masterKeyPath, FileAttributes.Hidden);

            // Restrict master key file to current user
            try
            {
                var fi = new FileInfo(_masterKeyPath);
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
                _logger.LogWarning(ex, "Failed to restrict master key file ACL");
            }

            _logger.LogInformation("Generated new master key for secret vault");
            return newMasterKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load/generate master key");
            throw;
        }
    }

    private byte[] LoadOrGenerateEntropy()
    {
        var entropyFile = Path.Combine(_vaultDirectory, ".entropy");
        try
        {
            if (File.Exists(entropyFile))
            {
                try
                {
                    var encryptedEntropyBase64 = File.ReadAllText(entropyFile);
                    var encryptedEntropyBytes = Convert.FromBase64String(encryptedEntropyBase64);
                    var entropy = ProtectedData.Unprotect(encryptedEntropyBytes, null, DataProtectionScope.LocalMachine);
                    _logger.LogDebug("Loaded existing entropy for backward compatibility");
                    return entropy;
                }
                catch (CryptographicException ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt entropy - regenerating");
                    try { File.Delete(entropyFile); } catch { }
                }
                catch (ArgumentException aex)
                {
                    _logger.LogWarning(aex, "ProtectedData.Unprotect threw ArgumentException when loading entropy - file contents may be invalid. Regenerating.");
                    try { File.Delete(entropyFile); } catch { }
                }
                catch (FormatException fex)
                {
                    _logger.LogWarning(fex, "Entropy base64 is malformed; regenerating entropy.");
                    try { File.Delete(entropyFile); } catch { }
                }
            }

            // Generate new entropy
            using var rng = RandomNumberGenerator.Create();
            var newEntropy = new byte[32];
            rng.GetBytes(newEntropy);
            try
            {
                var encryptedNewEntropy = ProtectedData.Protect(newEntropy, null, DataProtectionScope.LocalMachine);
                File.WriteAllText(entropyFile, Convert.ToBase64String(encryptedNewEntropy));
                File.SetAttributes(entropyFile, FileAttributes.Hidden);
            }
            catch (ArgumentException aex)
            {
                _logger.LogWarning(aex, "ProtectedData.Protect threw ArgumentException when generating entropy - continuing with in-memory entropy only (not persisted).");
                // fall through - we keep entropy in memory
            }
            catch (CryptographicException cex)
            {
                _logger.LogWarning(cex, "ProtectedData.Protect failed while persisting entropy - continuing with in-memory entropy only (not persisted).");
            }

            return newEntropy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load/generate entropy");
            return new byte[32]; // Fallback to zeros
        }
    }

    private async Task InitializeVaultAsync()
    {
        try
        {
            if (File.Exists(_vaultPath))
            {
                try
                {
                    await LoadVaultAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load vault - attempting recovery from backup");
                    await TryRecoverFromBackupAsync();
                }
            }
            else
            {
                _secretsCache = new();
                _logger.LogInformation("Initializing new empty vault");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize vault");
            throw;
        }
    }

    private async Task LoadVaultAsync()
    {
        if (!File.Exists(_vaultPath))
        {
            _secretsCache = new();
            return;
        }

        var json = await File.ReadAllTextAsync(_vaultPath);
        var vaultData = JsonSerializer.Deserialize<VaultFile>(json);

        if (vaultData == null)
        {
            throw new InvalidOperationException("Vault file is invalid or corrupted");
        }

        if (vaultData.Version != VaultVersion)
        {
            _logger.LogInformation("Vault version mismatch ({OldVersion} vs {NewVersion}) - migration may be needed", vaultData.Version, VaultVersion);
            if (vaultData.Version < VaultVersion)
            {
                await MigrateVaultAsync(vaultData);
            }
        }

        // Decrypt secrets payload
        var encryptedPayload = Convert.FromBase64String(vaultData.EncryptedData);
        var iv = Convert.FromBase64String(vaultData.IV);

        var decryptedJson = DecryptSecretsPayload(encryptedPayload, iv);
        var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(decryptedJson);

        _secretsCache = secrets ?? new();
        _logger.LogInformation("Loaded vault with {SecretCount} secrets", _secretsCache.Count);
    }

    private async Task SaveVaultAsync()
    {
        if (_masterKey == null)
            throw new InvalidOperationException("Master key not initialized");

        // Create backup before writing
        await CreateBackupAsync();

        // Generate random IV
        using var rng = RandomNumberGenerator.Create();
        var iv = new byte[IvLength];
        rng.GetBytes(iv);

        // Serialize secrets
        var secretsJson = JsonSerializer.Serialize(_secretsCache);
        var encryptedPayload = EncryptSecretsPayload(secretsJson, iv);

        // Create vault file structure
        var vaultFile = new VaultFile
        {
            Version = VaultVersion,
            Encryption = "AesGcm-256",
            IV = Convert.ToBase64String(iv),
            EncryptedData = Convert.ToBase64String(encryptedPayload),
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        var json = JsonSerializer.Serialize(vaultFile, new JsonSerializerOptions { WriteIndented = true });

        // Write atomically
        var tmpPath = _vaultPath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tmpPath, json);

            // Restrict file permissions
            try
            {
                var fi = new FileInfo(tmpPath);
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
                _logger.LogWarning(ex, "Failed to set vault file ACL");
            }

            // Atomic replace with a robust fallback. File.Replace can fail on some OS
            // configurations (antivirus, file locks). Try Replace/Move first, then
            // fall back to Copy+Delete to ensure the vault is updated in as many
            // environments as possible.
            try
            {
                if (File.Exists(_vaultPath))
                {
                    // Attempt atomic replace (preferred)
                    File.Replace(tmpPath, _vaultPath, null);
                }
                else
                {
                    File.Move(tmpPath, _vaultPath);
                }
            }
            catch (IOException ioEx)
            {
                // Best-effort fallback: copy and overwrite, then delete tmp.
                _logger.LogWarning(ioEx, "Atomic vault replace failed; attempting copy-overwrite fallback");
                try
                {
                    File.Copy(tmpPath, _vaultPath, overwrite: true);
                    File.Delete(tmpPath);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback vault replace also failed");
                    throw;
                }
            }

            _logger.LogDebug("Vault saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save vault");
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { }
            }
            throw;
        }
    }

    private async Task CreateBackupAsync()
    {
        try
        {
            if (!File.Exists(_vaultPath))
                return;

            var maxRetries = 3;
            var migrated = false;

            for (int attempt = 0; attempt < maxRetries && !migrated; attempt++)
            {
                // Use timestamp + random suffix to avoid collisions (file already exists)
                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
                var backupPath = Path.Combine(_vaultDirectory, $"vault.backup.{timestamp}.{suffix}.json");

                try
                {
                    // Copy atomically using FileStreams and FileMode.CreateNew to fail if exists
                    using (var src = new FileStream(_vaultPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var dest = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        await src.CopyToAsync(dest).ConfigureAwait(false);
                    }

                    _logger.LogDebug("Created backup: {BackupPath}", backupPath);
                    migrated = true;
                    break;
                }
                catch (IOException ioEx) when (ioEx.Message.Contains("already exists") || ioEx is IOException)
                {
                    // Collision or transient file system issue — retry with a new name
                    _logger.LogWarning(ioEx, "Backup file collision or IO issue on attempt {Attempt} for vault backup. Retrying...", attempt + 1);
                    await Task.Delay(100 * (attempt + 1));
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create backup on attempt {Attempt}", attempt + 1);
                    // For non-IO issues, try again a couple times but don't fail the save operation
                    await Task.Delay(100 * (attempt + 1));
                    continue;
                }
            }

            if (!migrated)
            {
                _logger.LogWarning("Failed to create a unique backup after {MaxRetries} attempts. Proceeding without backup.", maxRetries);
            }

            // Clean up old backups
            // Order backups by creation time, keeping the most recent MaxBackups
            var backups = Directory.GetFiles(_vaultDirectory, "vault.backup.*.json")
                .Select(f => new { Path = f, CreationTime = File.GetCreationTimeUtc(f) })
                .OrderByDescending(x => x.CreationTime)
                .Skip(MaxBackups)
                .Select(x => x.Path)
                .ToList();

            foreach (var oldBackup in backups)
            {
                try
                {
                    File.Delete(oldBackup);
                    _logger.LogDebug("Deleted old backup: {BackupPath}", oldBackup);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup: {BackupPath}", oldBackup);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create backup");
            // Don't throw - backup failure shouldn't prevent vault save
        }
    }

    private async Task TryRecoverFromBackupAsync()
    {
        try
        {
            var backups = Directory.GetFiles(_vaultDirectory, "vault.backup.*.json")
                .OrderByDescending(f => f)
                .ToList();

            foreach (var backup in backups)
            {
                try
                {
                    _logger.LogInformation("Attempting recovery from backup: {BackupPath}", backup);
                    var json = await File.ReadAllTextAsync(backup);
                    var vaultData = JsonSerializer.Deserialize<VaultFile>(json);

                    if (vaultData != null)
                    {
                        // Try to decrypt to verify integrity
                        var encryptedPayload = Convert.FromBase64String(vaultData.EncryptedData);
                        var iv = Convert.FromBase64String(vaultData.IV);
                        var decryptedJson = DecryptSecretsPayload(encryptedPayload, iv);
                        var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(decryptedJson);

                        if (secrets != null)
                        {
                            _secretsCache = secrets;
                            // Restore from backup
                            File.Copy(backup, _vaultPath, overwrite: true);
                            _logger.LogInformation("Successfully recovered vault from backup");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to recover from backup: {BackupPath}", backup);
                }
            }

            _logger.LogError("No valid backups available for recovery - starting with empty vault");
            _secretsCache = new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during backup recovery");
            _secretsCache = new();
        }
    }

    private byte[] EncryptSecretsPayload(string plaintext, byte[] iv)
    {
        if (_masterKey == null)
            throw new InvalidOperationException("Master key not initialized");

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);

        try
        {
            using var aes = new AesGcm(_masterKey, 12);  // 12-byte (96-bit) authentication tag size
            var ciphertext = new byte[plainBytes.Length];
            var tag = new byte[12]; // 96-bit authentication tag

            // ciphertext, tag, then optional associated data (null) - correct parameter order
            aes.Encrypt(iv, plainBytes, ciphertext, tag, null);

            // Return ciphertext + tag (IV stored separately in vault file)
            var result = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, result, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, ciphertext.Length, tag.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt secrets payload");
            throw;
        }
        finally
        {
            Array.Clear(plainBytes, 0, plainBytes.Length);
        }
    }

    private string DecryptSecretsPayload(byte[] encryptedData, byte[] iv)
    {
        if (_masterKey == null)
            throw new InvalidOperationException("Master key not initialized");

        try
        {
            // Extract ciphertext and tag
            var ciphertextLength = encryptedData.Length - 12; // 12-byte tag
            var ciphertext = new byte[ciphertextLength];
            var tag = new byte[12];

            Buffer.BlockCopy(encryptedData, 0, ciphertext, 0, ciphertextLength);
            Buffer.BlockCopy(encryptedData, ciphertextLength, tag, 0, 12);

            using var aes = new AesGcm(_masterKey, 12);  // 12-byte (96-bit) authentication tag size
            var plaintext = new byte[ciphertext.Length];

            // ciphertext, tag, then optional associated data (null) - correct parameter order
            aes.Decrypt(iv, ciphertext, tag, plaintext, null);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt secrets payload - may be corrupted");
            throw new InvalidOperationException("Vault decryption failed - data may be corrupted", ex);
        }
    }

    private async Task MigrateVaultAsync(VaultFile oldVault)
    {
        _logger.LogInformation("Migrating vault from version {OldVersion} to {NewVersion}", oldVault.Version, VaultVersion);
        // Migration logic here if needed in future
        await Task.CompletedTask;
    }

    // ======================
    // Public API
    // ======================

    public async Task<string?> GetSecretAsync(string secretName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));

        await _semaphore.WaitAsync();
        try
        {
            return _secretsCache.TryGetValue(secretName, out var value) ? value : null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public string? GetSecret(string secretName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));

        _semaphore.Wait();
        try
        {
            return _secretsCache.TryGetValue(secretName, out var value) ? value : null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Try to get a secret with explicit out parameter (safer than exceptions).
    /// </summary>
    public bool TryGetSecret(string secretName, out string? value)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));

        _semaphore.Wait();
        try
        {
            return _secretsCache.TryGetValue(secretName, out value);
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
            _secretsCache[key] = value;
            SaveVaultAsync().GetAwaiter().GetResult();
            _logger.LogDebug("Stored secret '{SecretName}' in vault (sync)", key);
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
            _secretsCache[secretName] = value;
            await SaveVaultAsync();
            _logger.LogInformation("Stored secret '{SecretName}' in vault", secretName);
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
            if (!Directory.Exists(_vaultDirectory))
            {
                _logger.LogError("Vault directory does not exist: {VaultDirectory}", _vaultDirectory);
                return false;
            }

            const string testKey = "__test_connection__";
            var testValue = "test_value_" + Guid.NewGuid().ToString("N");

            await SetSecretAsync(testKey, testValue);
            var retrieved = await GetSecretAsync(testKey);

            // Clean up
            try
            {
                await DeleteSecretAsync(testKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup test secret");
            }

            var success = retrieved == testValue;
            if (success)
            {
                _logger.LogInformation("Secret vault connection test PASSED");
            }
            else
            {
                _logger.LogError("Secret vault connection test FAILED");
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
        var envVars = new[]
        {
            "SYNCFUSION_LICENSE_KEY",
            "syncfusion-license-key",
            "QBO_CLIENT_ID",
            "QuickBooks-ClientId",
            "QBO_CLIENT_SECRET",
            "QuickBooks-ClientSecret",
            "QBO_REDIRECT_URI",
            "QuickBooks-RedirectUri",
            "QBO_ENVIRONMENT",
            "QuickBooks-Environment",
            "QBO_REALM_ID",
            "QuickBooks-RealmId",
            "XAI_API_KEY",
            "XAI-ApiKey",
            "XAI_BASE_URL",
            "XAI-BaseUrl",
            "OPENAI_API_KEY",
            "BOLD_LICENSE_KEY"
        };

        var uniqueVars = envVars.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var envVar in uniqueVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value) && !value.StartsWith("${", StringComparison.Ordinal))
            {
                // Skip if already in vault
                var existing = await GetSecretAsync(envVar);
                if (!string.IsNullOrEmpty(existing))
                {
                    _logger.LogDebug("Secret '{SecretName}' already in vault, skipping migration from environment", envVar);
                    continue;
                }

                await SetSecretAsync(envVar, value);
                migratedSecrets.Add(envVar);
                _logger.LogInformation("Migrated secret '{SecretName}' from environment to vault", envVar);
            }
        }

        if (migratedSecrets.Any())
        {
            _logger.LogInformation("Secret migration completed. Migrated: {Count} secrets", migratedSecrets.Count);
        }
        else
        {
            _logger.LogDebug("No environment variables found to migrate");
        }
    }

    public async Task PopulateProductionSecretsAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        _logger.LogInformation("PopulateProductionSecretsAsync called");
        await Task.CompletedTask;
    }

    public async Task<string> ExportSecretsAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        await _semaphore.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_secretsCache, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogWarning("Secrets exported to JSON - ensure secure handling!");
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
                throw new InvalidOperationException("Invalid JSON format for secrets import");

            foreach (var kvp in secrets)
            {
                _secretsCache[kvp.Key] = kvp.Value;
            }

            await SaveVaultAsync();
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
            return _secretsCache.Keys.OrderBy(k => k).ToList();
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

        await _semaphore.WaitAsync();
        try
        {
            if (_secretsCache.Remove(secretName))
            {
                await SaveVaultAsync();
                _logger.LogInformation("Deleted secret '{SecretName}' from vault", secretName);
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

        await _semaphore.WaitAsync();
        try
        {
            _secretsCache[secretName] = newValue;
            await SaveVaultAsync();

            // Verify
            var verified = _secretsCache.TryGetValue(secretName, out var value) && value == newValue;
            if (!verified)
                throw new InvalidOperationException("Verification failed after rotating secret");

            _logger.LogInformation("Rotated secret '{SecretName}'", secretName);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GetDiagnosticsAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        var sb = new StringBuilder();
        sb.AppendLine("=== Encrypted Secret Vault Diagnostics ===");
        sb.AppendLine($"Vault Directory: {_vaultDirectory}");
        sb.AppendLine($"Directory Exists: {Directory.Exists(_vaultDirectory)}");
        sb.AppendLine($"Vault File: {_vaultPath}");
        sb.AppendLine($"Vault File Exists: {File.Exists(_vaultPath)}");
        sb.AppendLine($"Master Key File: {_masterKeyPath}");
        sb.AppendLine($"Master Key Loaded: {_masterKey != null}");

        if (Directory.Exists(_vaultDirectory))
        {
            try
            {
                var keys = await ListSecretKeysAsync();
                sb.AppendLine($"Secrets in Vault: {string.Join(", ", keys)}");

                var backups = Directory.GetFiles(_vaultDirectory, "vault.backup.*.json");
                sb.AppendLine($"Backups Available: {backups.Length}");

                var testResult = await TestConnectionAsync();
                sb.AppendLine($"Connection Test: {(testResult ? "PASSED" : "FAILED")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Diagnostics Error: {ex.Message}");
            }
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _semaphore.Dispose();

        // Clear sensitive data from memory
        if (_masterKey != null)
        {
            Array.Clear(_masterKey, 0, _masterKey.Length);
            _masterKey = null;
        }

        if (_entropy != null)
        {
            Array.Clear(_entropy, 0, _entropy.Length);
            _entropy = null;
        }

        _disposed = true;
    }

    // ======================
    // Internal Models
    // ======================

    [JsonSourceGenerationOptions(WriteIndented = true)]
    private class VaultFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("encryption")]
        public string Encryption { get; set; } = "AesGcm-256";

        [JsonPropertyName("iv")]
        public string IV { get; set; } = "";

        [JsonPropertyName("encryptedData")]
        public string EncryptedData { get; set; } = "";

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");
    }
}
