#!/usr/bin/env dotnet-script
// 80-secret-vault-comprehensive-test.csx
// Comprehensive test suite for EncryptedLocalSecretVaultService
// Tests: initialization, CRUD operations, encryption, migration, diagnostics

#r "nuget: Microsoft.Extensions.Logging, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"
#r "nuget: Serilog, 4.2.0"
#r "nuget: Serilog.Extensions.Logging, 9.0.0"

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
using Serilog.Extensions.Logging;

// ============================================================================
// EMBEDDED IMPLEMENTATION (for standalone testing)
// ============================================================================

public interface ISecretVaultService
{
    string? GetSecret(string key);
    void StoreSecret(string key, string value);
    Task<string?> GetSecretAsync(string key);
    Task SetSecretAsync(string key, string value);
    Task RotateSecretAsync(string secretName, string newValue);
    Task MigrateSecretsFromEnvironmentAsync();
    Task PopulateProductionSecretsAsync();
    Task<bool> TestConnectionAsync();
    Task<string> ExportSecretsAsync();
    Task ImportSecretsAsync(string jsonSecrets);
    Task<IEnumerable<string>> ListSecretKeysAsync();
    Task DeleteSecretAsync(string secretName);
    Task<string> GetDiagnosticsAsync();
}

public sealed class EncryptedLocalSecretVaultService : ISecretVaultService, IDisposable
{
    private readonly ILogger<EncryptedLocalSecretVaultService> _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly string _vaultDirectory;
    private readonly string _entropyFile;
    private byte[]? _entropy;
    private bool _disposed;

    public EncryptedLocalSecretVaultService(ILogger<EncryptedLocalSecretVaultService> logger, string? customVaultPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            if (!string.IsNullOrEmpty(customVaultPath))
            {
                _vaultDirectory = customVaultPath;
            }
            else
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _vaultDirectory = Path.Combine(appData, "WileyWidget", "Secrets");
            }

            _entropyFile = Path.Combine(_vaultDirectory, ".entropy");

            if (!Directory.Exists(_vaultDirectory))
            {
                Directory.CreateDirectory(_vaultDirectory);
                _logger.LogInformation("Created secret vault directory: {VaultDirectory}", _vaultDirectory);
            }

            _entropy = LoadOrGenerateEntropy();
            _logger.LogInformation("EncryptedLocalSecretVaultService initialized. Vault: {VaultDirectory}", _vaultDirectory);
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
                var entropyBase64 = File.ReadAllText(_entropyFile);
                return Convert.FromBase64String(entropyBase64);
            }
            else
            {
                using var rng = RandomNumberGenerator.Create();
                var entropy = new byte[32];
                rng.GetBytes(entropy);
                File.WriteAllText(_entropyFile, Convert.ToBase64String(entropy));
                File.SetAttributes(_entropyFile, FileAttributes.Hidden);
                _logger.LogInformation("Generated new encryption entropy");
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
            if (!File.Exists(filePath)) return null;

            var encryptedBase64 = await File.ReadAllTextAsync(filePath);
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
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

    public string? GetSecret(string secretName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EncryptedLocalSecretVaultService));
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));

        _semaphore.Wait();
        try
        {
            var filePath = GetSecretFilePath(secretName);
            if (!File.Exists(filePath)) return null;

            var encryptedBase64 = File.ReadAllText(filePath);
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
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
            var encryptedBytes = ProtectedData.Protect(secretBytes, _entropy, DataProtectionScope.CurrentUser);
            var encryptedBase64 = Convert.ToBase64String(encryptedBytes);
            File.WriteAllText(filePath, encryptedBase64);
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
            var encryptedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
            var encryptedBase64 = Convert.ToBase64String(encryptedBytes);
            var filePath = GetSecretFilePath(secretName);
            var tmp = filePath + ".tmp";

            await File.WriteAllTextAsync(tmp, encryptedBase64);

            if (File.Exists(filePath))
            {
                File.Replace(tmp, filePath, null);
            }
            else
            {
                File.Move(tmp, filePath);
            }

            Array.Clear(plainBytes, 0, plainBytes.Length);
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
            if (!Directory.Exists(_vaultDirectory)) return false;

            const string testKey = "__test_connection__";
            var testValue = "test_value_" + Guid.NewGuid().ToString("N");

            await SetSecretAsync(testKey, testValue);
            var retrieved = await GetSecretAsync(testKey);
            await DeleteSecretAsync(testKey);

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
        var envVars = new[] { "SYNCFUSION_LICENSE_KEY", "QBO_CLIENT_ID", "QBO_CLIENT_SECRET" };
        foreach (var envVar in envVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value))
            {
                await SetSecretAsync(envVar, value);
            }
        }
    }

    public Task PopulateProductionSecretsAsync() => Task.CompletedTask;

    public async Task<string> ExportSecretsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var secrets = new Dictionary<string, string>();
            var keys = await ListSecretKeysAsync();
            foreach (var key in keys)
            {
                var value = await GetSecretAsync(key);
                if (value != null) secrets[key] = value;
            }
            return JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ImportSecretsAsync(string jsonSecrets)
    {
        var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonSecrets);
        if (secrets == null) throw new InvalidOperationException("Invalid JSON");
        foreach (var kvp in secrets)
        {
            await SetSecretAsync(kvp.Key, kvp.Value);
        }
    }

    public async Task<IEnumerable<string>> ListSecretKeysAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_vaultDirectory, "*.secret");
            return files.Select(f => Path.GetFileNameWithoutExtension(f)).OrderBy(k => k).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteSecretAsync(string secretName)
    {
        await _semaphore.WaitAsync();
        try
        {
            var filePath = GetSecretFilePath(secretName);
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RotateSecretAsync(string secretName, string newValue)
    {
        await SetSecretAsync(secretName, newValue);
        var verified = await GetSecretAsync(secretName);
        if (verified != newValue) throw new InvalidOperationException("Rotation verification failed");
    }

    public async Task<string> GetDiagnosticsAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Secret Vault Diagnostics ===");
        sb.AppendLine($"Vault Directory: {_vaultDirectory}");
        sb.AppendLine($"Directory Exists: {Directory.Exists(_vaultDirectory)}");
        sb.AppendLine($"Entropy File: {_entropyFile}");
        sb.AppendLine($"Entropy Loaded: {_entropy != null}");

        if (Directory.Exists(_vaultDirectory))
        {
            var files = Directory.GetFiles(_vaultDirectory, "*.secret");
            sb.AppendLine($"Secret Files: {files.Length}");
            var keys = await ListSecretKeysAsync();
            sb.AppendLine($"Keys: {string.Join(", ", keys)}");
        }

        var testResult = await TestConnectionAsync();
        sb.AppendLine($"Connection Test: {(testResult ? "PASSED" : "FAILED")}");
        return sb.ToString();
    }

    private string GetSecretFilePath(string secretName)
    {
        var safeName = string.Join("_", secretName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_vaultDirectory, $"{safeName}.secret");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _semaphore.Dispose();
        if (_entropy != null)
        {
            Array.Clear(_entropy, 0, _entropy.Length);
            _entropy = null;
        }
        _disposed = true;
    }
}

// ============================================================================
// TEST FRAMEWORK
// ============================================================================

class TestResult
{
    public string TestName { get; set; } = "";
    public bool Passed { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}

class TestRunner
{
    private readonly List<TestResult> _results = new();
    private readonly ILogger _logger;

    public TestRunner(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> RunTestAsync(string testName, Func<Task> testAction)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("🧪 Running: {TestName}", testName);
            await testAction();
            sw.Stop();
            _results.Add(new TestResult { TestName = testName, Passed = true, Duration = sw.Elapsed });
            _logger.LogInformation("✅ PASSED: {TestName} ({Duration}ms)", testName, sw.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _results.Add(new TestResult
            {
                TestName = testName,
                Passed = false,
                ErrorMessage = ex.Message,
                Duration = sw.Elapsed
            });
            _logger.LogError("❌ FAILED: {TestName} - {Error}", testName, ex.Message);
            return false;
        }
    }

    public void PrintSummary()
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("TEST SUMMARY");
        Console.WriteLine(new string('=', 80));

        var passed = _results.Count(r => r.Passed);
        var failed = _results.Count(r => !r.Passed);
        var total = _results.Count;

        foreach (var result in _results)
        {
            var status = result.Passed ? "✅ PASS" : "❌ FAIL";
            Console.WriteLine($"{status} | {result.TestName,-50} | {result.Duration.TotalMilliseconds,6:F1}ms");
            if (!result.Passed)
            {
                Console.WriteLine($"       Error: {result.ErrorMessage}");
            }
        }

        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"Total: {total} | Passed: {passed} | Failed: {failed} | Success Rate: {(passed * 100.0 / total):F1}%");
        Console.WriteLine(new string('=', 80));
    }

    public bool AllPassed() => _results.All(r => r.Passed);
}

// ============================================================================
// MAIN TEST EXECUTION
// ============================================================================

Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                Secret Vault Comprehensive Test Suite                      ║");
Console.WriteLine("║                     EncryptedLocalSecretVaultService                       ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════╝\n");

// Setup logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var loggerFactory = new SerilogLoggerFactory(Log.Logger);
var logger = loggerFactory.CreateLogger<EncryptedLocalSecretVaultService>();
var testLogger = loggerFactory.CreateLogger("TestRunner");

// Create test vault in temp directory
var testVaultPath = Path.Combine(Path.GetTempPath(), $"WileyWidget_Test_{Guid.NewGuid():N}");
Console.WriteLine($"Test Vault Location: {testVaultPath}\n");

var runner = new TestRunner(testLogger);
ISecretVaultService? vault = null;

try
{
    // ========================================================================
    // TEST 1: Initialization
    // ========================================================================
    await runner.RunTestAsync("1. Service Initialization", async () =>
    {
        vault = new EncryptedLocalSecretVaultService(logger, testVaultPath);
        if (!Directory.Exists(testVaultPath))
            throw new Exception("Vault directory was not created");
    });

    // ========================================================================
    // TEST 2: Connection Test
    // ========================================================================
    await runner.RunTestAsync("2. Connection Test", async () =>
    {
        var result = await vault!.TestConnectionAsync();
        if (!result) throw new Exception("Connection test failed");
    });

    // ========================================================================
    // TEST 3: Store and Retrieve Secret (Async)
    // ========================================================================
    await runner.RunTestAsync("3. Store and Retrieve Secret (Async)", async () =>
    {
        const string key = "test_secret_async";
        const string value = "super_secret_value_12345";

        await vault!.SetSecretAsync(key, value);
        var retrieved = await vault.GetSecretAsync(key);

        if (retrieved != value)
            throw new Exception($"Retrieved value '{retrieved}' does not match stored value '{value}'");
    });

    // ========================================================================
    // TEST 4: Store and Retrieve Secret (Sync)
    // ========================================================================
    await runner.RunTestAsync("4. Store and Retrieve Secret (Sync)", async () =>
    {
        const string key = "test_secret_sync";
        const string value = "another_secret_67890";

        vault!.StoreSecret(key, value);
        var retrieved = vault.GetSecret(key);

        if (retrieved != value)
            throw new Exception($"Retrieved value '{retrieved}' does not match stored value '{value}'");
    });

    // ========================================================================
    // TEST 5: Special Characters in Secret Values
    // ========================================================================
    await runner.RunTestAsync("5. Special Characters in Values", async () =>
    {
        const string key = "special_chars_test";
        const string value = "!@#$%^&*(){}[]|\\:;\"'<>,.?/~`\n\t\r 中文 🎉";

        await vault!.SetSecretAsync(key, value);
        var retrieved = await vault.GetSecretAsync(key);

        if (retrieved != value)
            throw new Exception("Special characters not preserved");
    });

    // ========================================================================
    // TEST 6: Large Secret Value
    // ========================================================================
    await runner.RunTestAsync("6. Large Secret Value (10KB)", async () =>
    {
        const string key = "large_secret";
        var value = new string('x', 10240); // 10KB

        await vault!.SetSecretAsync(key, value);
        var retrieved = await vault.GetSecretAsync(key);

        if (retrieved != value)
            throw new Exception($"Large value not preserved (expected {value.Length}, got {retrieved?.Length ?? 0})");
    });

    // ========================================================================
    // TEST 7: List Secret Keys
    // ========================================================================
    await runner.RunTestAsync("7. List Secret Keys", async () =>
    {
        var keys = await vault!.ListSecretKeysAsync();
        var keyList = keys.ToList();

        if (keyList.Count < 4) // Should have at least 4 from previous tests
            throw new Exception($"Expected at least 4 keys, found {keyList.Count}");

        Console.WriteLine($"       Found {keyList.Count} secrets: {string.Join(", ", keyList)}");
    });

    // ========================================================================
    // TEST 8: Update Existing Secret
    // ========================================================================
    await runner.RunTestAsync("8. Update Existing Secret", async () =>
    {
        const string key = "update_test";
        const string value1 = "original_value";
        const string value2 = "updated_value";

        await vault!.SetSecretAsync(key, value1);
        var retrieved1 = await vault.GetSecretAsync(key);

        await vault.SetSecretAsync(key, value2);
        var retrieved2 = await vault.GetSecretAsync(key);

        if (retrieved1 != value1 || retrieved2 != value2)
            throw new Exception("Secret update failed");
    });

    // ========================================================================
    // TEST 9: Secret Rotation
    // ========================================================================
    await runner.RunTestAsync("9. Secret Rotation", async () =>
    {
        const string key = "rotation_test";
        const string oldValue = "old_secret";
        const string newValue = "rotated_secret";

        await vault!.SetSecretAsync(key, oldValue);
        await vault.RotateSecretAsync(key, newValue);
        var retrieved = await vault.GetSecretAsync(key);

        if (retrieved != newValue)
            throw new Exception("Rotation failed");
    });

    // ========================================================================
    // TEST 10: Delete Secret
    // ========================================================================
    await runner.RunTestAsync("10. Delete Secret", async () =>
    {
        const string key = "delete_test";
        const string value = "to_be_deleted";

        await vault!.SetSecretAsync(key, value);
        var beforeDelete = await vault.GetSecretAsync(key);

        await vault.DeleteSecretAsync(key);
        var afterDelete = await vault.GetSecretAsync(key);

        if (beforeDelete != value || afterDelete != null)
            throw new Exception("Delete failed");
    });

    // ========================================================================
    // TEST 11: Non-Existent Secret
    // ========================================================================
    await runner.RunTestAsync("11. Get Non-Existent Secret", async () =>
    {
        var result = await vault!.GetSecretAsync("non_existent_key_12345");
        if (result != null)
            throw new Exception("Should return null for non-existent secret");
    });

    // ========================================================================
    // TEST 12: Export Secrets
    // ========================================================================
    string? exportedJson = null;
    await runner.RunTestAsync("12. Export Secrets to JSON", async () =>
    {
        exportedJson = await vault!.ExportSecretsAsync();

        if (string.IsNullOrEmpty(exportedJson))
            throw new Exception("Export returned empty result");

        var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(exportedJson);
        if (secrets == null || secrets.Count == 0)
            throw new Exception("Export JSON is invalid");

        Console.WriteLine($"       Exported {secrets.Count} secrets");
    });

    // ========================================================================
    // TEST 13: Import Secrets
    // ========================================================================
    await runner.RunTestAsync("13. Import Secrets from JSON", async () =>
    {
        if (string.IsNullOrEmpty(exportedJson))
            throw new Exception("No exported JSON to test import");

        // Create new vault and import
        var importVaultPath = Path.Combine(Path.GetTempPath(), $"WileyWidget_Import_{Guid.NewGuid():N}");
        var importVault = new EncryptedLocalSecretVaultService(logger, importVaultPath);

        await importVault.ImportSecretsAsync(exportedJson);
        var importedKeys = await importVault.ListSecretKeysAsync();
        var originalKeys = await vault!.ListSecretKeysAsync();

        if (importedKeys.Count() != originalKeys.Count())
            throw new Exception("Import count mismatch");

        importVault.Dispose();
        Directory.Delete(importVaultPath, true);
    });

    // ========================================================================
    // TEST 14: Environment Variable Migration
    // ========================================================================
    await runner.RunTestAsync("14. Migrate from Environment Variables", async () =>
    {
        Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "test_license_key_123");
        Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test_client_id_456");

        await vault!.MigrateSecretsFromEnvironmentAsync();

        var sfKey = await vault.GetSecretAsync("SYNCFUSION_LICENSE_KEY");
        var qboKey = await vault.GetSecretAsync("QBO_CLIENT_ID");

        if (sfKey != "test_license_key_123" || qboKey != "test_client_id_456")
            throw new Exception("Migration failed");

        Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", null);
        Environment.SetEnvironmentVariable("QBO_CLIENT_ID", null);
    });

    // ========================================================================
    // TEST 15: Diagnostics
    // ========================================================================
    await runner.RunTestAsync("15. Get Diagnostics Report", async () =>
    {
        var diagnostics = await vault!.GetDiagnosticsAsync();

        if (string.IsNullOrEmpty(diagnostics))
            throw new Exception("Diagnostics returned empty");

        if (!diagnostics.Contains("Secret Vault Diagnostics"))
            throw new Exception("Diagnostics format invalid");

        Console.WriteLine("\n" + diagnostics);
    });

    // ========================================================================
    // TEST 16: Concurrent Access
    // ========================================================================
    await runner.RunTestAsync("16. Concurrent Access Test", async () =>
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var key = $"concurrent_test_{index}";
                var value = $"concurrent_value_{index}";
                await vault!.SetSecretAsync(key, value);
                var retrieved = await vault.GetSecretAsync(key);
                if (retrieved != value)
                    throw new Exception($"Concurrent access failed for key {key}");
            }));
        }

        await Task.WhenAll(tasks);
    });

    // ========================================================================
    // TEST 17: Performance Test (100 operations)
    // ========================================================================
    await runner.RunTestAsync("17. Performance Test (100 operations)", async () =>
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            await vault!.SetSecretAsync($"perf_test_{i}", $"value_{i}");
        }

        for (int i = 0; i < 100; i++)
        {
            var value = await vault!.GetSecretAsync($"perf_test_{i}");
            if (value != $"value_{i}")
                throw new Exception($"Performance test failed at iteration {i}");
        }

        sw.Stop();
        Console.WriteLine($"       200 operations completed in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / 200.0:F2}ms per op)");
    });

}
finally
{
    // Cleanup
    if (vault is IDisposable disposable)
    {
        disposable.Dispose();
    }

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
}

// Print summary
runner.PrintSummary();

// Exit code
Environment.Exit(runner.AllPassed() ? 0 : 1);
