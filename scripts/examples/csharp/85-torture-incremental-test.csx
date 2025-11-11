#!/usr/bin/env dotnet-script
#nullable disable
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Extensions.Logging;

Console.WriteLine("Step 1: Basic imports work");

// Add CrossPlatformProtectedData
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

Console.WriteLine("Step 2: CrossPlatformProtectedData added");

// Add FaultInjector
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
}

Console.WriteLine("Step 3: FaultInjector added");

// Add SecureSecretCache
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
                _expirations.TryGetValue(key, out var expiration) &&
                DateTime.UtcNow < expiration)
            {
                return value;
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
            _cache[key] = value;
            _expirations[key] = DateTime.UtcNow.Add(_ttl);
        }
        finally
        {
            _lock.Release();
        }
    }
}

Console.WriteLine("Step 4: SecureSecretCache added");

// Add TortureTestRunner with its record
public class TortureTestRunner
{
    private readonly List<TestResult> _results = new();
    private int _testNumber = 0;

    public record TestResult(
        string Name,
        bool Passed,
        string Error,
        TimeSpan Duration,
        bool FaultInjected = false,
        string FaultType = null
    );

    public async Task RunTestAsync(string testName, Func<Task> testFunc)
    {
        _testNumber++;
        Console.WriteLine($"Test {_testNumber}: {testName}");
        await testFunc();
    }

    public void PrintSummary()
    {
        Console.WriteLine($"Total tests: {_testNumber}");
    }
}

Console.WriteLine("Step 5: TortureTestRunner added");

// Add MockSyncfusionLicenseValidator
public class MockSyncfusionLicenseValidator
{
    private readonly ILogger<MockSyncfusionLicenseValidator> _logger;

    public MockSyncfusionLicenseValidator(ILogger<MockSyncfusionLicenseValidator> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Valid, string Error)> ValidateAsync(string licenseKey)
    {
        await Task.Delay(10);
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return (false, "License key is empty");
        }
        if (licenseKey.Contains("EXPIRED"))
        {
            return (false, "License has expired");
        }
        return (true, null);
    }
}

Console.WriteLine("Step 6: MockSyncfusionLicenseValidator added");

// Test instantiation
var runner = new TortureTestRunner();
Console.WriteLine("Step 7: TortureTestRunner instantiated successfully!");

// Setup logger for testing
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

var serviceProvider = new ServiceCollection()
    .AddLogging(builder => builder.AddSerilog())
    .BuildServiceProvider();

var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
var mockLogger = loggerFactory.CreateLogger<MockSyncfusionLicenseValidator>();
var validator = new MockSyncfusionLicenseValidator(mockLogger);
Console.WriteLine("Step 8: MockSyncfusionLicenseValidator instantiated successfully!");

var cache = new SecureSecretCache(TimeSpan.FromMinutes(5));
Console.WriteLine("Step 9: Cache instantiated successfully!");

var injector = new FaultInjector();
Console.WriteLine("Step 10: FaultInjector instantiated successfully!");

Console.WriteLine("\nAll classes load and instantiate successfully!");
