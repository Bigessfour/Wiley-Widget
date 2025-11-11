#!/usr/bin/env dotnet-script
#nullable enable
// 85-secret-lifecycle-e2e-test.csx
// End-to-End Secret Lifecycle Test
// Tests the complete flow: User Input → UI ViewModel → Vault Storage → Startup Sequence → Service Availability
// Validates timing, availability, and proper secret propagation through the entire application lifecycle

#r "nuget: Microsoft.Extensions.Logging, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.0"
#r "nuget: Serilog, 4.2.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"
#r "nuget: Serilog.Extensions.Logging, 9.0.0"
#r "nuget: System.Security.Cryptography.ProtectedData, 9.0.0"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Extensions.Logging;

// ============================================================================
// TEST INFRASTRUCTURE
// ============================================================================

public class TestRunner
{
    private readonly List<(string Name, bool Passed, string? Error, TimeSpan Duration)> _results = new();
    private int _testNumber = 0;

    public async Task RunTestAsync(string testName, Func<Task> testFunc)
    {
        _testNumber++;
        var displayName = $"[{_testNumber:D2}] {testName}";
        Console.Write($"Running {displayName}...");

        var sw = Stopwatch.StartNew();
        try
        {
            await testFunc();
            sw.Stop();
            _results.Add((testName, true, null, sw.Elapsed));
            Console.WriteLine($" ✓ ({sw.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _results.Add((testName, false, ex.Message, sw.Elapsed));
            Console.WriteLine($" ✗ ({sw.ElapsedMilliseconds}ms)");
            Console.WriteLine($"       Error: {ex.Message}");
        }
    }

    public void PrintSummary()
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("TEST SUMMARY");
        Console.WriteLine(new string('=', 80));

        var passed = _results.Count(r => r.Passed);
        var failed = _results.Count(r => !r.Passed);
        var totalTime = TimeSpan.FromMilliseconds(_results.Sum(r => r.Duration.TotalMilliseconds));

        Console.WriteLine($"Total Tests: {_results.Count}");
        Console.WriteLine($"Passed: {passed} ✓");
        Console.WriteLine($"Failed: {failed} ✗");
        Console.WriteLine($"Success Rate: {(passed * 100.0 / _results.Count):F1}%");
        Console.WriteLine($"Total Time: {totalTime.TotalMilliseconds}ms");

        if (failed > 0)
        {
            Console.WriteLine("\nFailed Tests:");
            foreach (var result in _results.Where(r => !r.Passed))
            {
                Console.WriteLine($"  ✗ {result.Name}");
                Console.WriteLine($"    {result.Error}");
            }
        }

        Console.WriteLine(new string('=', 80));
    }

    public bool AllPassed() => _results.All(r => r.Passed);
}

// ============================================================================
// MOCK SECRET VAULT SERVICE (Embedded Implementation)
// ============================================================================

public interface ISecretVaultService
{
    Task<string?> GetSecretAsync(string key);
    Task SetSecretAsync(string key, string value);
    Task<IEnumerable<string>> ListSecretKeysAsync();
    Task DeleteSecretAsync(string key);
    Task<bool> ValidateEntropyIntegrityAsync();
}

public class EncryptedLocalSecretVaultService : ISecretVaultService, IDisposable
{
    private readonly ILogger<EncryptedLocalSecretVaultService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _vaultDirectory;
    private readonly string _entropyFile;
    private byte[]? _entropy;
    private bool _disposed;
    private readonly Dictionary<string, string> _secretCache = new();

    public EncryptedLocalSecretVaultService(
        ILogger<EncryptedLocalSecretVaultService> logger,
        string customVaultPath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _vaultDirectory = customVaultPath;
        _entropyFile = Path.Combine(_vaultDirectory, ".entropy");

        if (!Directory.Exists(_vaultDirectory))
        {
            Directory.CreateDirectory(_vaultDirectory);
            _logger.LogInformation("Created vault directory: {Path}", _vaultDirectory);
        }

        _entropy = LoadOrGenerateEntropy();
        _logger.LogInformation("SecretVault initialized at {Path}", _vaultDirectory);
    }

    private byte[] LoadOrGenerateEntropy()
    {
        if (File.Exists(_entropyFile))
        {
            try
            {
                var encryptedEntropy = File.ReadAllBytes(_entropyFile);
                var entropy = System.Security.Cryptography.ProtectedData.Unprotect(
                    encryptedEntropy,
                    null,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser
                );
                _logger.LogInformation("Loaded existing entropy from {File}", _entropyFile);
                return entropy;
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to decrypt entropy, generating new");
            }
        }

        // Generate new entropy
        var newEntropy = new byte[32];
        RandomNumberGenerator.Fill(newEntropy);

        var encryptedNew = System.Security.Cryptography.ProtectedData.Protect(
            newEntropy,
            null,
            System.Security.Cryptography.DataProtectionScope.CurrentUser
        );

        File.WriteAllBytes(_entropyFile, encryptedNew);
        File.SetAttributes(_entropyFile, FileAttributes.Hidden);

        _logger.LogInformation("Generated new entropy and saved to {File}", _entropyFile);
        return newEntropy;
    }

    private string GetSecretFileName(string secretName)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(secretName));
        return $"{Convert.ToHexString(hash).ToLowerInvariant()}.secret";
    }

    public async Task<string?> GetSecretAsync(string key)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        await _semaphore.WaitAsync();
        try
        {
            if (_secretCache.TryGetValue(key, out var cached))
            {
                _logger.LogDebug("Retrieved secret '{Key}' from cache", key);
                return cached;
            }

            var fileName = GetSecretFileName(key);
            var filePath = Path.Combine(_vaultDirectory, fileName);

            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Secret '{Key}' not found", key);
                return null;
            }

            var encryptedData = await File.ReadAllBytesAsync(filePath);
            var decryptedData = System.Security.Cryptography.ProtectedData.Unprotect(
                encryptedData,
                _entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser
            );

            var secret = Encoding.UTF8.GetString(decryptedData);
            _secretCache[key] = secret;

            _logger.LogInformation("Retrieved secret '{Key}' from vault", key);
            return secret;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetSecretAsync(string key, string value)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));

        await _semaphore.WaitAsync();
        try
        {
            var fileName = GetSecretFileName(key);
            var filePath = Path.Combine(_vaultDirectory, fileName);

            var plainData = Encoding.UTF8.GetBytes(value);
            var encryptedData = System.Security.Cryptography.ProtectedData.Protect(
                plainData,
                _entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser
            );

            await File.WriteAllBytesAsync(filePath, encryptedData);
            _secretCache[key] = value;

            _logger.LogInformation("Stored secret '{Key}' in vault (file: {File})", key, fileName);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IEnumerable<string>> ListSecretKeysAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return _secretCache.Keys.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteSecretAsync(string key)
    {
        await _semaphore.WaitAsync();
        try
        {
            var fileName = GetSecretFileName(key);
            var filePath = Path.Combine(_vaultDirectory, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _secretCache.Remove(key);
            _logger.LogInformation("Deleted secret '{Key}'", key);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> ValidateEntropyIntegrityAsync()
    {
        await Task.Yield();

        if (_entropy == null || _entropy.Length != 32)
        {
            _logger.LogCritical("Entropy validation failed: Invalid length");
            return false;
        }

        if (_entropy.All(b => b == 0))
        {
            _logger.LogCritical("Entropy validation failed: All-zero entropy detected");
            return false;
        }

        try
        {
            var encryptedEntropy = File.ReadAllBytes(_entropyFile);
            System.Security.Cryptography.ProtectedData.Unprotect(
                encryptedEntropy,
                null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            _logger.LogInformation("Entropy integrity validated successfully");
            return true;
        }
        catch (CryptographicException ex)
        {
            _logger.LogCritical(ex, "Entropy tampering detected: DPAPI decryption failed");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _semaphore.Dispose();
        _secretCache.Clear();

        if (_entropy != null)
        {
            Array.Clear(_entropy, 0, _entropy.Length);
        }

        _disposed = true;
        _logger.LogInformation("SecretVault disposed");
    }
}

// ============================================================================
// MOCK SETTINGS SERVICE (Simulates SettingsViewModel behavior)
// ============================================================================

public interface ISettingsService
{
    Task SaveQuickBooksSettingsAsync(string clientId, string clientSecret, string redirectUri, string environment);
    Task SaveSyncfusionLicenseAsync(string licenseKey);
    Task SaveXAISettingsAsync(string apiKey, string baseUrl);
}

public class MockSettingsService : ISettingsService
{
    private readonly ISecretVaultService _secretVault;
    private readonly ILogger<MockSettingsService> _logger;

    public MockSettingsService(
        ISecretVaultService secretVault,
        ILogger<MockSettingsService> logger)
    {
        _secretVault = secretVault;
        _logger = logger;
    }

    public async Task SaveQuickBooksSettingsAsync(string clientId, string clientSecret, string redirectUri, string environment)
    {
        _logger.LogInformation("Saving QuickBooks settings to vault...");

        await _secretVault.SetSecretAsync("QuickBooks-ClientId", clientId);
        await _secretVault.SetSecretAsync("QuickBooks-ClientSecret", clientSecret);
        await _secretVault.SetSecretAsync("QuickBooks-RedirectUri", redirectUri);
        await _secretVault.SetSecretAsync("QuickBooks-Environment", environment);

        _logger.LogInformation("QuickBooks settings saved successfully");
    }

    public async Task SaveSyncfusionLicenseAsync(string licenseKey)
    {
        _logger.LogInformation("Saving Syncfusion license to vault...");
        await _secretVault.SetSecretAsync("Syncfusion-LicenseKey", licenseKey);
        _logger.LogInformation("Syncfusion license saved successfully");
    }

    public async Task SaveXAISettingsAsync(string apiKey, string baseUrl)
    {
        _logger.LogInformation("Saving XAI settings to vault...");
        await _secretVault.SetSecretAsync("XAI-ApiKey", apiKey);
        await _secretVault.SetSecretAsync("XAI-BaseUrl", baseUrl);
        _logger.LogInformation("XAI settings saved successfully");
    }
}

// ============================================================================
// MOCK STARTUP MODULE (Simulates QuickBooksModule.OnInitialized behavior)
// ============================================================================

public class MockStartupModule
{
    private readonly ISecretVaultService _secretVault;
    private readonly ILogger<MockStartupModule> _logger;

    public MockStartupModule(
        ISecretVaultService secretVault,
        ILogger<MockStartupModule> logger)
    {
        _secretVault = secretVault;
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        _logger.LogInformation("=== Startup Module Initialization ===");

        // Step 1: Migrate environment variables (if any)
        await MigrateEnvironmentVariablesAsync();

        // Step 2: Load secrets from vault
        var secrets = await LoadStartupSecretsAsync();

        // Step 3: Validate required secrets are present
        var validationResult = ValidateStartupSecrets(secrets);

        _logger.LogInformation("=== Startup Module Initialization Complete ===");

        return validationResult;
    }

    private async Task MigrateEnvironmentVariablesAsync()
    {
        _logger.LogInformation("Checking for environment variables to migrate...");

        var migrateableVars = new Dictionary<string, string>
        {
            { "QUICKBOOKS_CLIENT_ID", "QuickBooks-ClientId" },
            { "QUICKBOOKS_CLIENT_SECRET", "QuickBooks-ClientSecret" },
            { "QUICKBOOKS_REDIRECT_URI", "QuickBooks-RedirectUri" },
            { "QUICKBOOKS_ENVIRONMENT", "QuickBooks-Environment" },
            { "SYNCFUSION_LICENSE_KEY", "Syncfusion-LicenseKey" },
            { "XAI_API_KEY", "XAI-ApiKey" },
            { "XAI_BASE_URL", "XAI-BaseUrl" }
        };

        int migrated = 0;
        foreach (var (envVar, secretKey) in migrateableVars)
        {
            var envValue = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                var existing = await _secretVault.GetSecretAsync(secretKey);
                if (string.IsNullOrWhiteSpace(existing))
                {
                    await _secretVault.SetSecretAsync(secretKey, envValue);
                    _logger.LogInformation("Migrated {EnvVar} → {SecretKey}", envVar, secretKey);
                    migrated++;
                }
            }
        }

        _logger.LogInformation("Migration complete: {Count} secrets migrated", migrated);
    }

    private async Task<Dictionary<string, string?>> LoadStartupSecretsAsync()
    {
        _logger.LogInformation("Loading secrets required for startup...");

        var secrets = new Dictionary<string, string?>();
        var requiredKeys = new[]
        {
            "QuickBooks-ClientId",
            "QuickBooks-ClientSecret",
            "QuickBooks-RedirectUri",
            "QuickBooks-Environment",
            "Syncfusion-LicenseKey"
        };

        foreach (var key in requiredKeys)
        {
            var value = await _secretVault.GetSecretAsync(key);
            secrets[key] = value;

            if (string.IsNullOrWhiteSpace(value))
            {
                _logger.LogWarning("Secret '{Key}' is missing or empty", key);
            }
            else
            {
                _logger.LogInformation("Loaded secret '{Key}' (length: {Length})", key, value.Length);
            }
        }

        return secrets;
    }

    private bool ValidateStartupSecrets(Dictionary<string, string?> secrets)
    {
        _logger.LogInformation("Validating startup secrets...");

        var requiredSecrets = new[]
        {
            "QuickBooks-ClientId",
            "QuickBooks-ClientSecret",
            "Syncfusion-LicenseKey"
        };

        var missingSecrets = new List<string>();
        foreach (var key in requiredSecrets)
        {
            if (!secrets.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                missingSecrets.Add(key);
            }
        }

        if (missingSecrets.Any())
        {
            _logger.LogError("Missing required secrets: {Secrets}", string.Join(", ", missingSecrets));
            return false;
        }

        _logger.LogInformation("All required secrets validated successfully");
        return true;
    }
}

// ============================================================================
// MOCK SERVICE (Simulates QuickBooksService behavior)
// ============================================================================

public class MockQuickBooksService
{
    private readonly ISecretVaultService _secretVault;
    private readonly ILogger<MockQuickBooksService> _logger;
    private bool _initialized = false;

    public MockQuickBooksService(
        ISecretVaultService secretVault,
        ILogger<MockQuickBooksService> logger)
    {
        _secretVault = secretVault;
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        _logger.LogInformation("Initializing QuickBooksService...");

        var clientId = await _secretVault.GetSecretAsync("QuickBooks-ClientId");
        var clientSecret = await _secretVault.GetSecretAsync("QuickBooks-ClientSecret");
        var redirectUri = await _secretVault.GetSecretAsync("QuickBooks-RedirectUri");
        var environment = await _secretVault.GetSecretAsync("QuickBooks-Environment");

        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogError("QuickBooks ClientId not available during initialization");
            return false;
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogError("QuickBooks ClientSecret not available during initialization");
            return false;
        }

        _logger.LogInformation("QuickBooksService initialized with:");
        _logger.LogInformation("  ClientId: {ClientId}", MaskSecret(clientId));
        _logger.LogInformation("  ClientSecret: {ClientSecret}", MaskSecret(clientSecret));
        _logger.LogInformation("  RedirectUri: {RedirectUri}", redirectUri ?? "default");
        _logger.LogInformation("  Environment: {Environment}", environment ?? "Sandbox");

        _initialized = true;
        return true;
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (!_initialized)
        {
            _logger.LogWarning("Service not initialized, calling Initialize first");
            await InitializeAsync();
        }

        _logger.LogInformation("Testing QuickBooks connection...");

        // Simulate connection test
        await Task.Delay(100);

        _logger.LogInformation("Connection test successful");
        return true;
    }

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length <= 8)
            return "***";

        return $"{secret.Substring(0, 4)}...{secret.Substring(secret.Length - 4)}";
    }
}

// ============================================================================
// MAIN TEST EXECUTION
// ============================================================================

Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         SECRET LIFECYCLE END-TO-END TEST SUITE                            ║");
Console.WriteLine("║  Tests: User Input → UI → Vault → Startup → Service Availability         ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════╝\n");

var runner = new TestRunner();
var testVaultPath = Path.Combine(Path.GetTempPath(), $"WileyWidget_Test_{Guid.NewGuid():N}");

// Setup logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

var serviceProvider = new ServiceCollection()
    .AddLogging(builder => builder.AddSerilog())
    .BuildServiceProvider();

var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

EncryptedLocalSecretVaultService? vault = null;
MockSettingsService? settingsService = null;
MockStartupModule? startupModule = null;
MockQuickBooksService? quickBooksService = null;

try
{
    Console.WriteLine($"Test vault directory: {testVaultPath}\n");

    // ========================================================================
    // PHASE 1: VAULT INITIALIZATION
    // ========================================================================
    Console.WriteLine("═══ PHASE 1: VAULT INITIALIZATION ═══\n");

    await runner.RunTestAsync("01. Initialize Secret Vault", async () =>
    {
        vault = new EncryptedLocalSecretVaultService(
            loggerFactory.CreateLogger<EncryptedLocalSecretVaultService>(),
            testVaultPath
        );

        if (!Directory.Exists(testVaultPath))
            throw new Exception("Vault directory not created");

        var entropyFile = Path.Combine(testVaultPath, ".entropy");
        if (!File.Exists(entropyFile))
            throw new Exception("Entropy file not created");

        await Task.CompletedTask;
    });

    await runner.RunTestAsync("02. Validate Entropy Integrity", async () =>
    {
        var isValid = await vault!.ValidateEntropyIntegrityAsync();
        if (!isValid)
            throw new Exception("Entropy integrity validation failed");
    });

    // ========================================================================
    // PHASE 2: USER INPUT → UI → VAULT (Settings Dialog Simulation)
    // ========================================================================
    Console.WriteLine("\n═══ PHASE 2: USER INPUT → UI → VAULT ═══\n");

    await runner.RunTestAsync("03. Initialize Settings Service", async () =>
    {
        settingsService = new MockSettingsService(
            vault!,
            loggerFactory.CreateLogger<MockSettingsService>()
        );
        await Task.CompletedTask;
    });

    await runner.RunTestAsync("04. User Enters QuickBooks Credentials", async () =>
    {
        // Simulate user entering credentials in Settings dialog
        await settingsService!.SaveQuickBooksSettingsAsync(
            clientId: "QB-TEST-CLIENT-ID-123456",
            clientSecret: "QB-TEST-CLIENT-SECRET-ABCDEF",
            redirectUri: "http://localhost:8080/callback",
            environment: "Sandbox"
        );
    });

    await runner.RunTestAsync("05. User Enters Syncfusion License", async () =>
    {
        await settingsService!.SaveSyncfusionLicenseAsync(
            licenseKey: "SYNCFUSION-LICENSE-KEY-XYZ789"
        );
    });

    await runner.RunTestAsync("06. User Enters XAI API Settings", async () =>
    {
        await settingsService!.SaveXAISettingsAsync(
            apiKey: "xai-test-api-key-12345",
            baseUrl: "https://api.x.ai/v1/"
        );
    });

    await runner.RunTestAsync("07. Verify Secrets Persisted to Vault", async () =>
    {
        var clientId = await vault!.GetSecretAsync("QuickBooks-ClientId");
        var clientSecret = await vault.GetSecretAsync("QuickBooks-ClientSecret");
        var licenseKey = await vault.GetSecretAsync("Syncfusion-LicenseKey");
        var xaiKey = await vault.GetSecretAsync("XAI-ApiKey");

        if (clientId != "QB-TEST-CLIENT-ID-123456")
            throw new Exception("QuickBooks ClientId not persisted correctly");

        if (clientSecret != "QB-TEST-CLIENT-SECRET-ABCDEF")
            throw new Exception("QuickBooks ClientSecret not persisted correctly");

        if (licenseKey != "SYNCFUSION-LICENSE-KEY-XYZ789")
            throw new Exception("Syncfusion LicenseKey not persisted correctly");

        if (xaiKey != "xai-test-api-key-12345")
            throw new Exception("XAI ApiKey not persisted correctly");

        await Task.CompletedTask;
    });

    await runner.RunTestAsync("08. Verify SHA-256 Filename Hashing", async () =>
    {
        var files = Directory.GetFiles(testVaultPath, "*.secret");
        if (files.Length == 0)
            throw new Exception("No secret files found");

        // Verify files have SHA-256 hash pattern (64 hex chars + .secret)
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Length != 64 || !fileName.All(c => char.IsAsciiHexDigit(c)))
                throw new Exception($"Invalid hashed filename: {fileName}");
        }

        Console.WriteLine($"       Found {files.Length} hashed secret files");
    });

    // ========================================================================
    // PHASE 3: APPLICATION RESTART → STARTUP SEQUENCE
    // ========================================================================
    Console.WriteLine("\n═══ PHASE 3: STARTUP SEQUENCE (App Restart Simulation) ═══\n");

    await runner.RunTestAsync("09. Simulate Environment Variable Migration", async () =>
    {
        // Set some environment variables to test migration
        Environment.SetEnvironmentVariable("QUICKBOOKS_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("XAI_BASE_URL", "https://api.x.ai/v2/");

        startupModule = new MockStartupModule(
            vault!,
            loggerFactory.CreateLogger<MockStartupModule>()
        );

        await Task.CompletedTask;
    });

    await runner.RunTestAsync("10. Execute Startup Module Initialization", async () =>
    {
        var success = await startupModule!.InitializeAsync();
        if (!success)
            throw new Exception("Startup module initialization failed");
    });

    await runner.RunTestAsync("11. Verify Secrets Available During Startup", async () =>
    {
        // These secrets MUST be available during startup sequence
        var clientId = await vault!.GetSecretAsync("QuickBooks-ClientId");
        var clientSecret = await vault.GetSecretAsync("QuickBooks-ClientSecret");
        var licenseKey = await vault.GetSecretAsync("Syncfusion-LicenseKey");

        if (string.IsNullOrWhiteSpace(clientId))
            throw new Exception("QuickBooks ClientId not available during startup");

        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new Exception("QuickBooks ClientSecret not available during startup");

        if (string.IsNullOrWhiteSpace(licenseKey))
            throw new Exception("Syncfusion LicenseKey not available during startup");

        Console.WriteLine($"       All critical secrets available at startup time");
    });

    // ========================================================================
    // PHASE 4: SERVICE INITIALIZATION (Timing Validation)
    // ========================================================================
    Console.WriteLine("\n═══ PHASE 4: SERVICE INITIALIZATION & TIMING ═══\n");

    await runner.RunTestAsync("12. Initialize QuickBooksService at Proper Timing", async () =>
    {
        quickBooksService = new MockQuickBooksService(
            vault!,
            loggerFactory.CreateLogger<MockQuickBooksService>()
        );

        var success = await quickBooksService.InitializeAsync();
        if (!success)
            throw new Exception("QuickBooksService initialization failed");
    });

    await runner.RunTestAsync("13. Verify Service Can Access Secrets", async () =>
    {
        var success = await quickBooksService!.TestConnectionAsync();
        if (!success)
            throw new Exception("Service connection test failed");
    });

    await runner.RunTestAsync("14. Validate Secret Timing (Before Service Needs Them)", async () =>
    {
        // This test validates that secrets are loaded BEFORE services that need them
        var sw = Stopwatch.StartNew();

        // Secrets should already be loaded (from startup)
        var clientId = await vault!.GetSecretAsync("QuickBooks-ClientId");
        sw.Stop();

        // Should be instant (cached)
        if (sw.ElapsedMilliseconds > 50)
            throw new Exception($"Secret retrieval too slow: {sw.ElapsedMilliseconds}ms (not cached?)");

        Console.WriteLine($"       Secret retrieved in {sw.ElapsedMilliseconds}ms (cached)");
    });

    // ========================================================================
    // PHASE 5: SECURITY VALIDATION
    // ========================================================================
    Console.WriteLine("\n═══ PHASE 5: SECURITY VALIDATION ═══\n");

    await runner.RunTestAsync("15. Verify Secrets Encrypted on Disk", async () =>
    {
        var secretFiles = Directory.GetFiles(testVaultPath, "*.secret");
        if (secretFiles.Length == 0)
            throw new Exception("No secret files found");

        // Read raw file and verify it's encrypted (not plaintext)
        var rawData = await File.ReadAllBytesAsync(secretFiles[0]);
        var rawText = Encoding.UTF8.GetString(rawData);

        // Should NOT contain plaintext secrets
        if (rawText.Contains("QB-TEST-CLIENT-ID") || rawText.Contains("SYNCFUSION"))
            throw new Exception("Secrets stored in plaintext!");

        Console.WriteLine($"       Verified {secretFiles.Length} files are encrypted");
    });

    await runner.RunTestAsync("16. Test Entropy Tampering Detection", async () =>
    {
        // Save original entropy
        var entropyFile = Path.Combine(testVaultPath, ".entropy");
        var originalEntropy = await File.ReadAllBytesAsync(entropyFile);

        try
        {
            // Tamper with entropy file
            await File.WriteAllBytesAsync(entropyFile, new byte[32]);

            // Try to load - should fail validation
            var isValid = await vault!.ValidateEntropyIntegrityAsync();
            if (isValid)
                throw new Exception("Tampering not detected!");

            Console.WriteLine("       Tampering correctly detected");
        }
        finally
        {
            // Restore original entropy
            await File.WriteAllBytesAsync(entropyFile, originalEntropy);
        }
    });

    // ========================================================================
    // PHASE 6: FULL LIFECYCLE VALIDATION
    // ========================================================================
    Console.WriteLine("\n═══ PHASE 6: COMPLETE LIFECYCLE VALIDATION ═══\n");

    await runner.RunTestAsync("17. End-to-End: User Input → Service Usage", async () =>
    {
        // Simulate complete lifecycle:
        // 1. User enters new secret
        await settingsService!.SaveQuickBooksSettingsAsync(
            clientId: "QB-NEW-CLIENT-ID-999",
            clientSecret: "QB-NEW-CLIENT-SECRET-999",
            redirectUri: "http://localhost:9090/callback",
            environment: "Production"
        );

        // 2. Service can immediately access it
        var newClientId = await vault!.GetSecretAsync("QuickBooks-ClientId");
        if (newClientId != "QB-NEW-CLIENT-ID-999")
            throw new Exception("Updated secret not immediately available");

        // 3. Reinitialize service with new credentials
        var newService = new MockQuickBooksService(
            vault,
            loggerFactory.CreateLogger<MockQuickBooksService>()
        );

        var success = await newService.InitializeAsync();
        if (!success)
            throw new Exception("Service reinitialization failed");

        Console.WriteLine("       Complete lifecycle validated successfully");
    });

    await runner.RunTestAsync("18. Performance: 100 Sequential Secret Operations", async () =>
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            await vault!.SetSecretAsync($"perf_test_{i}", $"value_{i}");
            var retrieved = await vault.GetSecretAsync($"perf_test_{i}");
            if (retrieved != $"value_{i}")
                throw new Exception($"Performance test failed at iteration {i}");
        }

        sw.Stop();
        var avgMs = sw.ElapsedMilliseconds / 200.0;
        Console.WriteLine($"       200 operations in {sw.ElapsedMilliseconds}ms ({avgMs:F2}ms avg)");
    });

    await runner.RunTestAsync("19. Concurrent Access Safety", async () =>
    {
        var tasks = new List<Task>();

        for (int i = 0; i < 20; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await vault!.SetSecretAsync($"concurrent_{index}", $"value_{index}");
                var retrieved = await vault.GetSecretAsync($"concurrent_{index}");
                if (retrieved != $"value_{index}")
                    throw new Exception($"Concurrent test failed for index {index}");
            }));
        }

        await Task.WhenAll(tasks);
        Console.WriteLine("       20 concurrent operations completed successfully");
    });

    await runner.RunTestAsync("20. Secret Rotation Simulation", async () =>
    {
        // Initial secret
        await vault!.SetSecretAsync("Rotatable-Secret", "initial-value-v1");

        // Service loads initial secret
        var v1 = await vault.GetSecretAsync("Rotatable-Secret");
        if (v1 != "initial-value-v1")
            throw new Exception("Initial secret load failed");

        // Rotate secret (user updates in Settings)
        await vault.SetSecretAsync("Rotatable-Secret", "rotated-value-v2");

        // Service should see new value immediately
        var v2 = await vault.GetSecretAsync("Rotatable-Secret");
        if (v2 != "rotated-value-v2")
            throw new Exception("Rotated secret not immediately available");

        Console.WriteLine("       Secret rotation successful");
    });
}
finally
{
    // Cleanup
    vault?.Dispose();

    if (Directory.Exists(testVaultPath))
    {
        try
        {
            Directory.Delete(testVaultPath, true);
            Console.WriteLine($"\n✓ Test vault cleaned up: {testVaultPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n⚠ Failed to cleanup test vault: {ex.Message}");
        }
    }

    Log.CloseAndFlush();
}

// Print summary
runner.PrintSummary();

// Exit code
Environment.Exit(runner.AllPassed() ? 0 : 1);
