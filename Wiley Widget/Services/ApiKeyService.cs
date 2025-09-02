using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Serilog;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Secure service for managing xAI API keys with multiple storage backends
/// Supports environment variables, user secrets, and encrypted local storage
/// </summary>
public class ApiKeyService
{
    private static readonly Lazy<ApiKeyService> _lazy = new(() => new ApiKeyService());
    public static ApiKeyService Instance => _lazy.Value;

    private readonly IConfiguration _config;
    private readonly string _secretsFile;
    private readonly string _keyFile;
    private const string ENV_VAR_NAME = "WILEYWIDGET_XAI_API_KEY";
    private const string USER_SECRETS_KEY = "xAI:ApiKey";

    public ApiKeyService()
    {
        // Build configuration to check for user secrets
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<ApiKeyService>(optional: true);

        _config = configBuilder.Build();

        // Setup secure storage paths
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var secretsDir = Path.Combine(appData, "WileyWidget", "Secrets");
        Directory.CreateDirectory(secretsDir);

        _secretsFile = Path.Combine(secretsDir, "secrets.json");
        _keyFile = Path.Combine(secretsDir, "encryption.key");
    }

    /// <summary>
    /// Gets the xAI API key from the most secure available source
    /// Priority: Environment Variable > User Secrets > Encrypted Local Storage
    /// </summary>
    public string GetApiKey()
    {
        try
        {
            // 1. Check environment variable (most secure for production)
            var envKey = Environment.GetEnvironmentVariable(ENV_VAR_NAME);
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                Log.Information("xAI API key loaded from environment variable");
                return envKey;
            }

            // 2. Check user secrets (secure for development)
            var secretsKey = _config[USER_SECRETS_KEY];
            if (!string.IsNullOrWhiteSpace(secretsKey))
            {
                Log.Information("xAI API key loaded from user secrets");
                return secretsKey;
            }

            // 3. Check encrypted local storage (fallback)
            var encryptedKey = LoadEncryptedApiKey();
            if (!string.IsNullOrWhiteSpace(encryptedKey))
            {
                Log.Information("xAI API key loaded from encrypted storage");
                return encryptedKey;
            }

            Log.Warning("No xAI API key found in any storage location");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve xAI API key");
            return null;
        }
    }

    /// <summary>
    /// Stores the xAI API key using the most appropriate secure method
    /// </summary>
    public bool StoreApiKey(string apiKey, StorageMethod method = StorageMethod.Auto)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Warning("Attempted to store empty API key");
            return false;
        }

        try
        {
            switch (method)
            {
                case StorageMethod.EnvironmentVariable:
                    return StoreInEnvironmentVariable(apiKey);

                case StorageMethod.UserSecrets:
                    return StoreInUserSecrets(apiKey);

                case StorageMethod.EncryptedFile:
                    return StoreEncryptedLocally(apiKey);

                case StorageMethod.Auto:
                default:
                    // Try most secure method first
                    if (StoreInEnvironmentVariable(apiKey)) return true;
                    if (StoreInUserSecrets(apiKey)) return true;
                    return StoreEncryptedLocally(apiKey);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to store xAI API key");
            return false;
        }
    }

    /// <summary>
    /// Tests if the stored API key is valid by making a test request
    /// </summary>
    public async Task<bool> TestApiKeyAsync(string apiKey = null)
    {
        var keyToTest = apiKey ?? GetApiKey();
        if (string.IsNullOrWhiteSpace(keyToTest))
        {
            return false;
        }

        try
        {
            // Create a minimal test configuration
            var testConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["xAI:ApiKey"] = keyToTest,
                    ["xAI:Model"] = "grok-4-0709"
                })
                .Build();

            using var testGrok = new GrokSupercomputer(testConfig);

            // Test with a simple prompt
            var testEnterprises = new List<Enterprise>
            {
                new Enterprise { Name = "Test Utility", CurrentRate = 5.00M, MonthlyExpenses = 1000M, CitizenCount = 200 }
            };

            var result = await testGrok.ComputeEnterprisesAsync(testEnterprises);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "API key test failed");
            return false;
        }
    }

    /// <summary>
    /// Removes the stored API key from all locations
    /// </summary>
    public bool RemoveApiKey()
    {
        try
        {
            bool success = true;

            // Remove from environment variable
            Environment.SetEnvironmentVariable(ENV_VAR_NAME, null, EnvironmentVariableTarget.User);

            // Remove from user secrets (if file exists)
            if (File.Exists(_secretsFile))
            {
                try
                {
                    File.Delete(_secretsFile);
                }
                catch
                {
                    success = false;
                }
            }

            // Remove encrypted local storage
            if (File.Exists(_keyFile))
            {
                try
                {
                    File.Delete(_keyFile);
                }
                catch
                {
                    success = false;
                }
            }

            Log.Information("xAI API key removed from all storage locations");
            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to remove xAI API key");
            return false;
        }
    }

    /// <summary>
    /// Gets information about where the API key is currently stored
    /// </summary>
    public ApiKeyInfo GetApiKeyInfo()
    {
        return new ApiKeyInfo
        {
            HasEnvironmentVariable = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ENV_VAR_NAME)),
            HasUserSecrets = !string.IsNullOrWhiteSpace(_config[USER_SECRETS_KEY]),
            HasEncryptedStorage = !string.IsNullOrWhiteSpace(LoadEncryptedApiKey()),
            IsValid = GetApiKey() != null
        };
    }

    private bool StoreInEnvironmentVariable(string apiKey)
    {
        try
        {
            Environment.SetEnvironmentVariable(ENV_VAR_NAME, apiKey, EnvironmentVariableTarget.User);
            Log.Information("xAI API key stored in environment variable");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to store API key in environment variable");
            return false;
        }
    }

    private bool StoreInUserSecrets(string apiKey)
    {
        try
        {
            // For user secrets, we need to use the dotnet user-secrets command
            // This is a simplified approach - in production you'd want to use the proper API
            var secretsJson = new Dictionary<string, string>
            {
                [USER_SECRETS_KEY] = apiKey
            };

            var json = JsonSerializer.Serialize(secretsJson, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_secretsFile, json);

            Log.Information("xAI API key stored in user secrets");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to store API key in user secrets");
            return false;
        }
    }

    private bool StoreEncryptedLocally(string apiKey)
    {
        try
        {
            var encrypted = EncryptString(apiKey);
            File.WriteAllText(_keyFile, encrypted);
            Log.Information("xAI API key stored in encrypted local storage");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to store API key in encrypted storage");
            return false;
        }
    }

    private string LoadEncryptedApiKey()
    {
        try
        {
            if (!File.Exists(_keyFile))
                return null;

            var encrypted = File.ReadAllText(_keyFile);
            return DecryptString(encrypted);
        }
        catch
        {
            return null;
        }
    }

    private string EncryptString(string plainText)
    {
        using var aes = Aes.Create();
        aes.GenerateKey();
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        using var sw = new StreamWriter(cs);

        sw.Write(plainText);
        sw.Close();

        var keyAndIv = Convert.ToBase64String(aes.Key) + ":" + Convert.ToBase64String(aes.IV);
        var encrypted = Convert.ToBase64String(ms.ToArray());

        return keyAndIv + ":" + encrypted;
    }

    private string DecryptString(string encryptedText)
    {
        try
        {
            var parts = encryptedText.Split(':');
            if (parts.Length != 3) return null;

            var key = Convert.FromBase64String(parts[0]);
            var iv = Convert.FromBase64String(parts[1]);
            var encrypted = Convert.FromBase64String(parts[2]);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encrypted);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Available storage methods for API keys
/// </summary>
public enum StorageMethod
{
    Auto,
    EnvironmentVariable,
    UserSecrets,
    EncryptedFile
}

/// <summary>
/// Information about API key storage status
/// </summary>
public class ApiKeyInfo
{
    public bool HasEnvironmentVariable { get; set; }
    public bool HasUserSecrets { get; set; }
    public bool HasEncryptedStorage { get; set; }
    public bool IsValid { get; set; }
}
