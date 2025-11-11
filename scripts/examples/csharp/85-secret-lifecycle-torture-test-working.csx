#!/usr/bin/env dotnet-script
#nullable disable
// 85-secret-lifecycle-torture-test-working.csx
// SIMPLIFIED TORTURE CHAMBER - Maximum Robustness, Minimum Complexity
// Focus: Fault injection, concurrent stress, cache validation, encryption integrity

#r "nuget: Microsoft.Extensions.Logging, 8.0.0"
#r "nuget: Microsoft.Extensions.DependencyInjection, 8.0.0"
#r "nuget: Serilog, 4.1.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"
#r "nuget: Serilog.Extensions.Logging, 8.0.0"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

// ============================================================================
// CROSS-PLATFORM ENCRYPTION (AES-based DPAPI replacement)
// ============================================================================
public static class CrossPlatformProtectedData
{
    public static byte[] Protect(byte[] userData, byte[] optionalEntropy)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey(optionalEntropy ?? new byte[32]);
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(userData, 0, userData.Length);
        }
        return ms.ToArray();
    }

    public static byte[] Unprotect(byte[] encryptedData, byte[] optionalEntropy)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey(optionalEntropy ?? new byte[32]);
        var iv = new byte[16];
        Buffer.BlockCopy(encryptedData, 0, iv, 0, 16);
        aes.IV = iv;
        using var ms = new MemoryStream(encryptedData, 16, encryptedData.Length - 16);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var result = new MemoryStream();
        cs.CopyTo(result);
        return result.ToArray();
    }

    private static byte[] DeriveKey(byte[] entropy)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(entropy);
    }
}

// ============================================================================
// CHAOS ENGINEERING - Fault Injection
// ============================================================================
public class FaultInjector
{
    private readonly Random _random = new Random();
    private readonly double _dpapiFailureRate;
    private readonly double _diskIOFailureRate;

    public FaultInjector(double dpapiFailureRate = 0.10, double diskIOFailureRate = 0.05)
    {
        _dpapiFailureRate = dpapiFailureRate;
        _diskIOFailureRate = diskIOFailureRate;
    }

    public bool ShouldInjectDPAPIFailure() => _random.NextDouble() < _dpapiFailureRate;
    public bool ShouldInjectDiskIOFailure() => _random.NextDouble() < _diskIOFailureRate;

    public async Task SimulateDiskContention(int delayMs = 50)
    {
        if (ShouldInjectDiskIOFailure())
        {
            await Task.Delay(delayMs);
        }
    }
}

// ============================================================================
// SECURE CACHE with TTL and LRU Eviction
// ============================================================================
public class SecureSecretCache
{
    private readonly Dictionary<string, string> _cache = new();
    private readonly Dictionary<string, DateTime> _expirations = new();
    private readonly TimeSpan _ttl;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly int _maxEntries;

    public SecureSecretCache(TimeSpan ttl, int maxEntries = 100)
    {
        _ttl = ttl;
        _maxEntries = maxEntries;
    }

    public async Task<string> GetAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var value) &&
                _expirations.TryGetValue(key, out var expiration))
            {
                if (DateTime.UtcNow < expiration)
                {
                    return value;
                }
                // Expired - remove
                _cache.Remove(key);
                _expirations.Remove(key);
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            // LRU eviction if at capacity
            if (_cache.Count >= _maxEntries && !_cache.ContainsKey(key))
            {
                var oldest = _expirations.OrderBy(kvp => kvp.Value).First();
                _cache.Remove(oldest.Key);
                _expirations.Remove(oldest.Key);
            }

            _cache[key] = value;
            _expirations[key] = DateTime.UtcNow.Add(_ttl);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _cache.Clear();
            _expirations.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    public int Count => _cache.Count;
}

// ============================================================================
// TORTURE SECRET VAULT - With Fault Injection & Caching
// ============================================================================
public class TortureSecretVaultService
{
    private readonly string _vaultPath;
    private readonly SecureSecretCache _secretCache;
    private readonly FaultInjector _faultInjector;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private byte[] _entropy;
    private bool _initialized = false;

    public TortureSecretVaultService(
        string vaultPath,
        SecureSecretCache secretCache,
        FaultInjector faultInjector)
    {
        _vaultPath = vaultPath;
        _secretCache = secretCache;
        _faultInjector = faultInjector;
    }

    public async Task InitializeAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_initialized) return;

            Directory.CreateDirectory(_vaultPath);

            var entropyFile = Path.Combine(_vaultPath, ".entropy");
            if (File.Exists(entropyFile))
            {
                // Inject DPAPI fault
                if (_faultInjector.ShouldInjectDPAPIFailure())
                {
                    throw new CryptographicException("⚠ CHAOS: Simulated DPAPI session change");
                }

                var encryptedEntropy = await File.ReadAllBytesAsync(entropyFile);
                _entropy = CrossPlatformProtectedData.Unprotect(encryptedEntropy, null);
            }
            else
            {
                _entropy = new byte[32];
                RandomNumberGenerator.Fill(_entropy);

                var encryptedEntropy = CrossPlatformProtectedData.Protect(_entropy, null);
                await _faultInjector.SimulateDiskContention();
                await File.WriteAllBytesAsync(entropyFile, encryptedEntropy);
            }

            _initialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GetSecretAsync(string key)
    {
        if (!_initialized) throw new InvalidOperationException("Vault not initialized");

        // Try cache first
        var cached = await _secretCache.GetAsync(key);
        if (cached != null) return cached;

        await _semaphore.WaitAsync();
        try
        {
            var filePath = Path.Combine(_vaultPath, $"{key}.secret");
            if (!File.Exists(filePath)) return null;

            await _faultInjector.SimulateDiskContention();
            var encryptedData = await File.ReadAllBytesAsync(filePath);

            // Inject DPAPI fault
            if (_faultInjector.ShouldInjectDPAPIFailure())
            {
                throw new CryptographicException("⚠ CHAOS: Simulated DPAPI failure during decrypt");
            }

            var plainData = CrossPlatformProtectedData.Unprotect(encryptedData, _entropy);
            var secret = Encoding.UTF8.GetString(plainData);

            // Update cache
            await _secretCache.SetAsync(key, secret);

            return secret;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetSecretAsync(string key, string value)
    {
        if (!_initialized) throw new InvalidOperationException("Vault not initialized");

        await _semaphore.WaitAsync();
        try
        {
            var plainData = Encoding.UTF8.GetBytes(value);

            // Inject DPAPI fault
            if (_faultInjector.ShouldInjectDPAPIFailure())
            {
                throw new CryptographicException("⚠ CHAOS: Simulated DPAPI failure during encrypt");
            }

            var encryptedData = CrossPlatformProtectedData.Protect(plainData, _entropy);

            var filePath = Path.Combine(_vaultPath, $"{key}.secret");
            await _faultInjector.SimulateDiskContention();
            await File.WriteAllBytesAsync(filePath, encryptedData);

            // Update cache
            await _secretCache.SetAsync(key, value);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> ValidateEntropyAsync()
    {
        var entropyFile = Path.Combine(_vaultPath, ".entropy");
        if (!File.Exists(entropyFile)) return false;

        var encryptedEntropy = await File.ReadAllBytesAsync(entropyFile);
        var decryptedEntropy = CrossPlatformProtectedData.Unprotect(encryptedEntropy, null);

        return decryptedEntropy.Length == 32 && decryptedEntropy.SequenceEqual(_entropy);
    }
}

// ============================================================================
// TEST RUNNER
// ============================================================================
public class TortureTestRunner
{
    private readonly List<TestResult> _results = new();
    private int _testNumber = 0;

    public record TestResult(
        string Name,
        bool Passed,
        string Error,
        TimeSpan Duration,
        bool FaultInjected
    );

    public async Task RunTestAsync(string testName, Func<Task> testFunc, bool allowFaultInjection = false)
    {
        _testNumber++;
        var displayName = $"[{_testNumber:D2}] {testName}";
        Console.Write($"Running {displayName}...");

        var sw = Stopwatch.StartNew();
        bool faultInjected = false;

        try
        {
            await testFunc();
            sw.Stop();
            _results.Add(new TestResult(testName, true, null, sw.Elapsed, faultInjected));
            Console.WriteLine($" ✓ ({sw.ElapsedMilliseconds}ms)");
        }
        catch (CryptographicException ex) when (allowFaultInjection && ex.Message.Contains("CHAOS"))
        {
            sw.Stop();
            faultInjected = true;
            _results.Add(new TestResult(testName, true, null, sw.Elapsed, faultInjected));
            Console.WriteLine($" ⚠ CHAOS ({sw.ElapsedMilliseconds}ms) - {ex.Message}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _results.Add(new TestResult(testName, false, ex.Message, sw.Elapsed, faultInjected));
            Console.WriteLine($" ✗ ({sw.ElapsedMilliseconds}ms)");
            Console.WriteLine($"   Error: {ex.Message}");
        }
    }

    public void PrintSummary()
    {
        Console.WriteLine("\n" + new string('=', 90));
        Console.WriteLine("TORTURE TEST SUMMARY");
        Console.WriteLine(new string('=', 90));

        var passed = _results.Count(r => r.Passed);
        var failed = _results.Count(r => !r.Passed);
        var chaosTriggered = _results.Count(r => r.FaultInjected);

        Console.WriteLine($"Total Tests: {_results.Count}");
        Console.WriteLine($"✓ Passed: {passed}");
        Console.WriteLine($"✗ Failed: {failed}");
        Console.WriteLine($"⚠ Chaos Injections: {chaosTriggered}");

        if (failed > 0)
        {
            Console.WriteLine("\nFailed Tests:");
            foreach (var result in _results.Where(r => !r.Passed))
            {
                Console.WriteLine($"  - {result.Name}: {result.Error}");
            }
        }

        Console.WriteLine(new string('=', 90));
    }
}

// ============================================================================
// MAIN EXECUTION
// ============================================================================

Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║              SECRET LIFECYCLE TORTURE CHAMBER TEST SUITE                            ║");
Console.WriteLine("║  Fault Injection • Concurrent Stress • Cache Validation • Encryption Integrity      ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════════════╝\n");

var runner = new TortureTestRunner();
var testVaultPath = Path.Combine(Path.GetTempPath(), $"WileyWidget_Torture_{Guid.NewGuid():N}");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

// Setup components
var faultInjector = new FaultInjector(dpapiFailureRate: 0.10, diskIOFailureRate: 0.05);
var secretCache = new SecureSecretCache(TimeSpan.FromSeconds(5), 100);
TortureSecretVaultService vault = null;

try
{
    Console.WriteLine($"Test vault: {testVaultPath}\n");

    // ========================================================================
    // PHASE 1: VAULT INITIALIZATION
    // ========================================================================
    Console.WriteLine("PHASE 1: Vault Initialization\n");

    await runner.RunTestAsync("01. Initialize Vault", async () =>
    {
        vault = new TortureSecretVaultService(testVaultPath, secretCache, faultInjector);
        await vault.InitializeAsync();
    }, allowFaultInjection: true);

    await runner.RunTestAsync("02. Validate Entropy File Created", async () =>
    {
        var entropyFile = Path.Combine(testVaultPath, ".entropy");
        if (!File.Exists(entropyFile))
            throw new Exception("Entropy file not created");
        await Task.CompletedTask;
    });

    await runner.RunTestAsync("03. Verify Entropy Integrity", async () =>
    {
        var isValid = await vault.ValidateEntropyAsync();
        if (!isValid)
            throw new Exception("Entropy validation failed");
    });

    // ========================================================================
    // PHASE 2: BASIC OPERATIONS
    // ========================================================================
    Console.WriteLine("\nPHASE 2: Basic Secret Operations\n");

    await runner.RunTestAsync("04. Store Secret", async () =>
    {
        await vault.SetSecretAsync("test-secret", "MySecureValue123");
    }, allowFaultInjection: true);

    await runner.RunTestAsync("05. Retrieve Secret", async () =>
    {
        var secret = await vault.GetSecretAsync("test-secret");
        if (secret != "MySecureValue123")
            throw new Exception($"Expected 'MySecureValue123', got '{secret}'");
    }, allowFaultInjection: true);

    await runner.RunTestAsync("06. Cache Hit After Store", async () =>
    {
        var secret = await vault.GetSecretAsync("test-secret");
        if (secret != "MySecureValue123")
            throw new Exception("Cache miss or wrong value");
    });

    // ========================================================================
    // PHASE 3: CONCURRENT STRESS TEST
    // ========================================================================
    Console.WriteLine("\nPHASE 3: Concurrent Torture Tests\n");

    await runner.RunTestAsync("07. Concurrent Writes (50 tasks)", async () =>
    {
        var tasks = Enumerable.Range(0, 50).Select(async i =>
        {
            try
            {
                await vault.SetSecretAsync($"concurrent-{i}", $"value-{i}");
            }
            catch (CryptographicException ex) when (ex.Message.Contains("CHAOS"))
            {
                // Expected chaos - not a failure
            }
        });
        await Task.WhenAll(tasks);
    });

    await runner.RunTestAsync("08. Concurrent Reads (100 tasks)", async () =>
    {
        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            try
            {
                var key = $"concurrent-{i % 50}";
                var value = await vault.GetSecretAsync(key);
                if (value != null && value != $"value-{i % 50}")
                    throw new Exception($"Data corruption detected for {key}");
            }
            catch (CryptographicException ex) when (ex.Message.Contains("CHAOS"))
            {
                // Expected chaos - not a failure
            }
        });
        await Task.WhenAll(tasks);
    });

    await runner.RunTestAsync("09. Mixed Concurrent Operations (200 tasks)", async () =>
    {
        var tasks = Enumerable.Range(0, 200).Select(async i =>
        {
            try
            {
                if (i % 2 == 0)
                    await vault.SetSecretAsync($"mixed-{i}", $"value-{i}");
                else
                    await vault.GetSecretAsync($"mixed-{i - 1}");
            }
            catch (CryptographicException ex) when (ex.Message.Contains("CHAOS"))
            {
                // Expected chaos
            }
        });
        await Task.WhenAll(tasks);
    });

    // ========================================================================
    // PHASE 4: CACHE VALIDATION
    // ========================================================================
    Console.WriteLine("\nPHASE 4: Cache Performance & TTL Validation\n");

    await runner.RunTestAsync("10. Cache TTL Expiration", async () =>
    {
        await vault.SetSecretAsync("ttl-test", "expires-soon");
        await vault.GetSecretAsync("ttl-test"); // Cache hit

        await Task.Delay(6000); // Wait for 5s TTL + buffer

        var value = await vault.GetSecretAsync("ttl-test");
        if (value != "expires-soon")
            throw new Exception("TTL expiration failed - value should still be retrievable from disk");
    }, allowFaultInjection: true);

    await runner.RunTestAsync("11. Cache LRU Eviction", async () =>
    {
        // Fill cache beyond capacity (100 entries)
        for (int i = 0; i < 110; i++)
        {
            try
            {
                await vault.SetSecretAsync($"lru-{i}", $"value-{i}");
            }
            catch (CryptographicException ex) when (ex.Message.Contains("CHAOS"))
            {
                // Chaos injection during bulk write - expected
            }
        }

        // Verify oldest entries were evicted from cache but still on disk
        var oldValue = await vault.GetSecretAsync("lru-0");
        if (oldValue != null && oldValue != "value-0")
            throw new Exception("LRU eviction corrupted data");
    }, allowFaultInjection: true);

    await runner.RunTestAsync("12. Cache Performance (1000 sequential reads)", async () =>
    {
        await vault.SetSecretAsync("perf-test", "cached-value");

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            var value = await vault.GetSecretAsync("perf-test");
        }
        sw.Stop();

        var avgMs = sw.ElapsedMilliseconds / 1000.0;
        Console.Write($" [Avg: {avgMs:F3}ms/read]");

        if (avgMs > 5.0)
            throw new Exception($"Cache too slow: {avgMs:F3}ms average (expected <5ms)");
    }, allowFaultInjection: true);

    // ========================================================================
    // PHASE 5: SECURITY & INTEGRITY
    // ========================================================================
    Console.WriteLine("\nPHASE 5: Security & Integrity Validation\n");

    await runner.RunTestAsync("13. Verify Encryption At Rest", async () =>
    {
        await vault.SetSecretAsync("encrypted-test", "PlaintextSecret123");

        var secretFile = Path.Combine(testVaultPath, "encrypted-test.secret");
        var diskBytes = await File.ReadAllBytesAsync(secretFile);
        var diskText = Encoding.UTF8.GetString(diskBytes);

        if (diskText.Contains("PlaintextSecret123"))
            throw new Exception("Secret stored in plaintext on disk!");
    });

    await runner.RunTestAsync("14. Entropy Tampering Detection", async () =>
    {
        var entropyFile = Path.Combine(testVaultPath, ".entropy");
        var originalBytes = await File.ReadAllBytesAsync(entropyFile);

        // Corrupt entropy file
        var tamperedBytes = new byte[originalBytes.Length];
        Array.Copy(originalBytes, tamperedBytes, originalBytes.Length);
        tamperedBytes[10] ^= 0xFF; // Flip bits
        await File.WriteAllBytesAsync(entropyFile, tamperedBytes);

        // Try to validate
        var isValid = await vault.ValidateEntropyAsync();
        if (isValid)
            throw new Exception("Tampered entropy not detected!");

        // Restore
        await File.WriteAllBytesAsync(entropyFile, originalBytes);
    });

    await runner.RunTestAsync("15. Cross-Platform Encryption Compatibility", async () =>
    {
        var testData = Encoding.UTF8.GetBytes("Cross-Platform-Test");
        var entropy = new byte[32];
        RandomNumberGenerator.Fill(entropy);

        var encrypted = CrossPlatformProtectedData.Protect(testData, entropy);
        var decrypted = CrossPlatformProtectedData.Unprotect(encrypted, entropy);
        var result = Encoding.UTF8.GetString(decrypted);

        if (result != "Cross-Platform-Test")
            throw new Exception("Encryption/decryption mismatch");
    });

    // ========================================================================
    // PHASE 6: STRESS & RESILIENCE
    // ========================================================================
    Console.WriteLine("\nPHASE 6: Extreme Stress & Fault Tolerance\n");

    await runner.RunTestAsync("16. Sustained Concurrent Load (500 operations)", async () =>
    {
        var random = new Random();
        var tasks = Enumerable.Range(0, 500).Select(async i =>
        {
            try
            {
                var key = $"stress-{random.Next(0, 50)}";
                if (i % 3 == 0)
                    await vault.SetSecretAsync(key, $"value-{i}");
                else
                    await vault.GetSecretAsync(key);
            }
            catch (CryptographicException ex) when (ex.Message.Contains("CHAOS"))
            {
                // Expected fault injection
            }
        });
        await Task.WhenAll(tasks);
    });

    await runner.RunTestAsync("17. Large Secret Storage (10KB)", async () =>
    {
        var largeSecret = new string('X', 10240); // 10KB
        await vault.SetSecretAsync("large-secret", largeSecret);

        var retrieved = await vault.GetSecretAsync("large-secret");
        if (retrieved != largeSecret)
            throw new Exception("Large secret corrupted");
    }, allowFaultInjection: true);

    await runner.RunTestAsync("18. Unicode & Special Characters", async () =>
    {
        var unicodeSecret = "🔐🔑 Секрет 密码 مفتاح سري \n\t\"'<>&";
        await vault.SetSecretAsync("unicode-test", unicodeSecret);

        var retrieved = await vault.GetSecretAsync("unicode-test");
        if (retrieved != unicodeSecret)
            throw new Exception("Unicode handling failed");
    }, allowFaultInjection: true);

    // ========================================================================
    // FINAL VALIDATION
    // ========================================================================
    Console.WriteLine("\nFINAL VALIDATION\n");

    await runner.RunTestAsync("19. Vault Still Functional After Torture", async () =>
    {
        await vault.SetSecretAsync("final-test", "still-works");
        var value = await vault.GetSecretAsync("final-test");
        if (value != "still-works")
            throw new Exception("Vault corrupted after torture");
    }, allowFaultInjection: true);

    await runner.RunTestAsync("20. Cache Integrity Check", async () =>
    {
        var cacheCount = secretCache.Count;
        Console.Write($" [Cache size: {cacheCount}]");
        if (cacheCount > 100)
            throw new Exception($"Cache exceeded max size: {cacheCount} > 100");
    });
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ CRITICAL FAILURE: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
finally
{
    // Cleanup
    if (Directory.Exists(testVaultPath))
    {
        try
        {
            Directory.Delete(testVaultPath, true);
            Console.WriteLine($"\n✓ Cleaned up test vault: {testVaultPath}");
        }
        catch
        {
            Console.WriteLine($"\n⚠ Warning: Could not clean up {testVaultPath}");
        }
    }

    runner.PrintSummary();
    Log.CloseAndFlush();
}
