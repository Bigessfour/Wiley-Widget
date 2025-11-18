#!/usr/bin/env dotnet-script
#nullable disable
#r "nuget: Microsoft.Extensions.Logging, 8.0.0"
#r "nuget: Microsoft.Extensions.DependencyInjection, 8.0.0"
#r "nuget: Serilog, 4.1.0"
#r "nuget: Serilog.Extensions.Logging, 8.0.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

// Cross-platform encryption wrapper (no Windows DPAPI dependency)
public static class CrossPlatformProtectedData
{
    private static byte[] DeriveKey(byte[] entropy)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(entropy);
    }

    public static byte[] Protect(byte[] userData, byte[] optionalEntropy)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey(optionalEntropy ?? new byte[32]);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(userData, 0, userData.Length);

        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return result;
    }

    public static byte[] Unprotect(byte[] encryptedData, byte[] optionalEntropy)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey(optionalEntropy ?? new byte[32]);

        // Extract IV from first 16 bytes
        var iv = new byte[16];
        Buffer.BlockCopy(encryptedData, 0, iv, 0, 16);
        aes.IV = iv;

        // Decrypt remaining data
        var cipherText = new byte[encryptedData.Length - 16];
        Buffer.BlockCopy(encryptedData, 16, cipherText, 0, cipherText.Length);

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }
}

// Simplified cache without SecureString
public class SimpleSecretCache
{
    private readonly Dictionary<string, string> _cache = new();
    private readonly Dictionary<string, DateTime> _expirations = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TimeSpan _ttl;
    private readonly int _maxEntries;

    public SimpleSecretCache(TimeSpan ttl, int maxEntries = 100)
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
}

// Test runner
Console.WriteLine("Starting simplified torture test...");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Logger initialized");

var cache = new SimpleSecretCache(TimeSpan.FromMinutes(5));
await cache.SetAsync("test-key", "test-value");
var retrieved = await cache.GetAsync("test-key");

Log.Information($"Cache test: {(retrieved == "test-value" ? "PASS" : "FAIL")}");

// Test encryption
var testData = Encoding.UTF8.GetBytes("Hello World");
var entropy = new byte[32];
RandomNumberGenerator.Fill(entropy);

var encrypted = CrossPlatformProtectedData.Protect(testData, entropy);
var decrypted = CrossPlatformProtectedData.Unprotect(encrypted, entropy);
var decryptedText = Encoding.UTF8.GetString(decrypted);

Log.Information($"Encryption test: {(decryptedText == "Hello World" ? "PASS" : "FAIL")}");

Console.WriteLine("Simplified torture test completed!");
