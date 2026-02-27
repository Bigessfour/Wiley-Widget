using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Services;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class SettingsSecretsPersistenceServiceIntegrationTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string AppDataEnvironmentVariable = "APPDATA";
    private const string SyncfusionEnvironmentVariable = "SYNCFUSION_LICENSE_KEY";
    private const string XaiPrimaryEnvironmentVariable = "XAI__ApiKey";
    private const string XaiSecondaryEnvironmentVariable = "XAI_API_KEY";

    [Fact]
    public async Task LoadCurrentAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var service = new SettingsSecretsPersistenceService(NullLogger<SettingsSecretsPersistenceService>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.LoadCurrentAsync(cts.Token));
    }

    [Fact]
    public async Task LoadCurrentAsync_WhenUserSecretsExist_PrefersUserSecretsOverEnvironmentAndVault()
    {
        using var isolation = new SecretEnvironmentIsolation();

        var appDataRoot = CreateIsolatedAppDataRoot();
        Environment.SetEnvironmentVariable(AppDataEnvironmentVariable, appDataRoot, EnvironmentVariableTarget.Process);

        WriteUserSecrets(appDataRoot, new Dictionary<string, string>
        {
            ["Syncfusion:LicenseKey"] = "\"user-secret-sync\"",
            ["XAI:ApiKey"] = " 'user-secret-xai' "
        });

        Environment.SetEnvironmentVariable(SyncfusionEnvironmentVariable, "env-sync", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(XaiPrimaryEnvironmentVariable, "env-xai", EnvironmentVariableTarget.Process);

        var vault = new InMemorySecretVaultService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SYNCFUSION_LICENSE_KEY"] = "vault-sync",
            ["XAI:ApiKey"] = "vault-xai"
        });

        var service = new SettingsSecretsPersistenceService(NullLogger<SettingsSecretsPersistenceService>.Instance, vault);

        var snapshot = await service.LoadCurrentAsync();

        Assert.Equal("user-secret-sync", snapshot.SyncfusionLicenseKey);
        Assert.Equal("user-secret-xai", snapshot.XaiApiKey);
    }

    [Fact]
    public async Task LoadCurrentAsync_WhenEnvironmentIsPlaceholder_FallsBackToVaultAndNormalizes()
    {
        using var isolation = new SecretEnvironmentIsolation();

        var appDataRoot = CreateIsolatedAppDataRoot();
        Environment.SetEnvironmentVariable(AppDataEnvironmentVariable, appDataRoot, EnvironmentVariableTarget.Process);

        Environment.SetEnvironmentVariable(SyncfusionEnvironmentVariable, "YOUR_SYNCFUSION_LICENSE_KEY_HERE", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(XaiPrimaryEnvironmentVariable, "PLACEHOLDER_VALUE", EnvironmentVariableTarget.Process);

        var vault = new InMemorySecretVaultService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Syncfusion:LicenseKey"] = " \"vault-sync-key\" ",
            ["XAI:ApiKey"] = " 'vault-xai-key' "
        });

        var service = new SettingsSecretsPersistenceService(NullLogger<SettingsSecretsPersistenceService>.Instance, vault);

        var snapshot = await service.LoadCurrentAsync();

        var expectedSyncfusion = ResolveExpectedFromEnvironmentOrVault(
            new[] { SyncfusionEnvironmentVariable },
            "vault-sync-key");

        var expectedXai = ResolveExpectedFromEnvironmentOrVault(
            new[] { XaiPrimaryEnvironmentVariable, XaiSecondaryEnvironmentVariable },
            "vault-xai-key");

        Assert.Equal(expectedSyncfusion, snapshot.SyncfusionLicenseKey);
        Assert.Equal(expectedXai, snapshot.XaiApiKey);
    }

    [Fact]
    public async Task LoadCurrentAsync_WhenHigherEnvironmentScopeIsValid_UsesEnvironmentBeforeVault()
    {
        using var isolation = new SecretEnvironmentIsolation();

        var appDataRoot = CreateIsolatedAppDataRoot();
        Environment.SetEnvironmentVariable(AppDataEnvironmentVariable, appDataRoot, EnvironmentVariableTarget.Process);

        Environment.SetEnvironmentVariable(SyncfusionEnvironmentVariable, "YOUR_SYNCFUSION_LICENSE_KEY_HERE", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(XaiPrimaryEnvironmentVariable, "PLACEHOLDER_VALUE", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(XaiSecondaryEnvironmentVariable, null, EnvironmentVariableTarget.Process);

        var syncUserValue = $"user-sync-{Guid.NewGuid():N}";
        var xaiUserValue = $"user-xai-{Guid.NewGuid():N}";

        var syncUserSet = TrySetEnvironmentVariable(SyncfusionEnvironmentVariable, syncUserValue, EnvironmentVariableTarget.User);
        var xaiUserSet = TrySetEnvironmentVariable(XaiPrimaryEnvironmentVariable, xaiUserValue, EnvironmentVariableTarget.User);

        var vault = new InMemorySecretVaultService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SYNCFUSION_LICENSE_KEY"] = "vault-sync-key",
            ["XAI:ApiKey"] = "vault-xai-key"
        });

        var service = new SettingsSecretsPersistenceService(NullLogger<SettingsSecretsPersistenceService>.Instance, vault);
        var snapshot = await service.LoadCurrentAsync();

        if (syncUserSet && xaiUserSet)
        {
            Assert.Equal(syncUserValue, snapshot.SyncfusionLicenseKey);
            Assert.Equal(xaiUserValue, snapshot.XaiApiKey);
            Assert.NotEqual("vault-sync-key", snapshot.SyncfusionLicenseKey);
            Assert.NotEqual("vault-xai-key", snapshot.XaiApiKey);
            return;
        }

        var expectedSyncfusion = ResolveExpectedFromEnvironmentOrVault(
            new[] { SyncfusionEnvironmentVariable },
            "vault-sync-key");

        var expectedXai = ResolveExpectedFromEnvironmentOrVault(
            new[] { XaiPrimaryEnvironmentVariable, XaiSecondaryEnvironmentVariable },
            "vault-xai-key");

        Assert.Equal(expectedSyncfusion, snapshot.SyncfusionLicenseKey);
        Assert.Equal(expectedXai, snapshot.XaiApiKey);
    }

    [Fact]
    public async Task PersistAsync_WhenValidSecretsProvided_PersistsAndCanBeReloaded()
    {
        using var isolation = new SecretEnvironmentIsolation();
        using var workingDirectory = new WorkingDirectoryScope(ResolveRepositoryRoot());

        var appDataRoot = CreateIsolatedAppDataRoot();
        Environment.SetEnvironmentVariable(AppDataEnvironmentVariable, appDataRoot, EnvironmentVariableTarget.Process);

        var vault = new InMemorySecretVaultService();
        var service = new SettingsSecretsPersistenceService(NullLogger<SettingsSecretsPersistenceService>.Instance, vault);

        var result = await service.PersistAsync("  \"sync-persist-key\"  ", "  'xai-persist-key'  ");

        Assert.True(result.Success);
        Assert.Equal("sync-persist-key", Environment.GetEnvironmentVariable(SyncfusionEnvironmentVariable, EnvironmentVariableTarget.Process));
        Assert.Equal("xai-persist-key", Environment.GetEnvironmentVariable(XaiPrimaryEnvironmentVariable, EnvironmentVariableTarget.Process));
        Assert.Equal("xai-persist-key", Environment.GetEnvironmentVariable(XaiSecondaryEnvironmentVariable, EnvironmentVariableTarget.Process));

        Assert.Equal("sync-persist-key", await vault.GetSecretAsync("SYNCFUSION_LICENSE_KEY"));
        Assert.Equal("sync-persist-key", await vault.GetSecretAsync("Syncfusion:LicenseKey"));
        Assert.Equal("xai-persist-key", await vault.GetSecretAsync("XAI:ApiKey"));
        Assert.Equal("xai-persist-key", await vault.GetSecretAsync("XAI__ApiKey"));

        var secretsFilePath = ResolveSecretsFilePath(appDataRoot);
        Assert.True(File.Exists(secretsFilePath));

        var secretsJson = await File.ReadAllTextAsync(secretsFilePath);
        Assert.Contains("sync-persist-key", secretsJson, StringComparison.Ordinal);
        Assert.Contains("xai-persist-key", secretsJson, StringComparison.Ordinal);

        Environment.SetEnvironmentVariable(SyncfusionEnvironmentVariable, null, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(XaiPrimaryEnvironmentVariable, null, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(XaiSecondaryEnvironmentVariable, null, EnvironmentVariableTarget.Process);

        var snapshot = await service.LoadCurrentAsync();
        Assert.Equal("sync-persist-key", snapshot.SyncfusionLicenseKey);
        Assert.Equal("xai-persist-key", snapshot.XaiApiKey);
    }

    private static string CreateIsolatedAppDataRoot()
    {
        var isolatedAppData = Path.Combine(Path.GetTempPath(), "WileyWidget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolatedAppData);
        return isolatedAppData;
    }

    private static string? ResolveExpectedFromEnvironmentOrVault(IEnumerable<string> aliases, string fallbackVaultValue)
    {
        foreach (var alias in aliases)
        {
            var processValue = NormalizeForAssertion(Environment.GetEnvironmentVariable(alias, EnvironmentVariableTarget.Process));
            if (!string.IsNullOrWhiteSpace(processValue))
            {
                return processValue;
            }

            var userValue = NormalizeForAssertion(Environment.GetEnvironmentVariable(alias, EnvironmentVariableTarget.User));
            if (!string.IsNullOrWhiteSpace(userValue))
            {
                return userValue;
            }

            var machineValue = NormalizeForAssertion(Environment.GetEnvironmentVariable(alias, EnvironmentVariableTarget.Machine));
            if (!string.IsNullOrWhiteSpace(machineValue))
            {
                return machineValue;
            }
        }

        return fallbackVaultValue;
    }

    private static string? NormalizeForAssertion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();
        if ((trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
            || (trimmed.StartsWith("'", StringComparison.Ordinal) && trimmed.EndsWith("'", StringComparison.Ordinal)))
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
        }

        if (trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("SYNCFUSION_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    private static bool TrySetEnvironmentVariable(string name, string? value, EnvironmentVariableTarget target)
    {
        try
        {
            Environment.SetEnvironmentVariable(name, value, target);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveRepositoryRoot()
    {
        var candidates = new[]
        {
            new DirectoryInfo(Directory.GetCurrentDirectory()),
            new DirectoryInfo(AppContext.BaseDirectory)
        };

        foreach (var start in candidates)
        {
            var current = start;
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "WileyWidget.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException("Unable to resolve repository root for integration tests.");
    }

    private static string ResolveSecretsFilePath(string appDataRoot)
    {
        var userSecretsId = typeof(WileyWidget.WinForms.Program).Assembly
            .GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId;

        if (string.IsNullOrWhiteSpace(userSecretsId))
        {
            throw new InvalidOperationException("Unable to resolve UserSecretsId from WileyWidget.WinForms assembly.");
        }

        return Path.Combine(appDataRoot, "Microsoft", "UserSecrets", userSecretsId, "secrets.json");
    }

    private static void WriteUserSecrets(string appDataRoot, Dictionary<string, string> values)
    {
        var secretsFilePath = ResolveSecretsFilePath(appDataRoot);
        var secretsDirectory = Path.GetDirectoryName(secretsFilePath)
            ?? throw new InvalidOperationException("Unable to resolve secrets directory path.");

        Directory.CreateDirectory(secretsDirectory);

        var json = JsonSerializer.Serialize(values, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(secretsFilePath, json);
    }

    private sealed class WorkingDirectoryScope : IDisposable
    {
        private readonly string _originalDirectory = Directory.GetCurrentDirectory();

        public WorkingDirectoryScope(string targetDirectory)
        {
            Directory.SetCurrentDirectory(targetDirectory);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalDirectory);
        }
    }

    private sealed class SecretEnvironmentIsolation : IDisposable
    {
        private readonly Dictionary<(string Name, EnvironmentVariableTarget Target), string?> _snapshot = new();

        private readonly string[] _trackedVariableNames =
        {
            AppDataEnvironmentVariable,
            SyncfusionEnvironmentVariable,
            XaiPrimaryEnvironmentVariable,
            XaiSecondaryEnvironmentVariable
        };

        private readonly EnvironmentVariableTarget[] _targets =
        {
            EnvironmentVariableTarget.Process,
            EnvironmentVariableTarget.User,
            EnvironmentVariableTarget.Machine
        };

        public SecretEnvironmentIsolation()
        {
            foreach (var name in _trackedVariableNames)
            {
                foreach (var target in _targets)
                {
                    try
                    {
                        _snapshot[(name, target)] = Environment.GetEnvironmentVariable(name, target);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public void Dispose()
        {
            var isolatedFolder = Environment.GetEnvironmentVariable(AppDataEnvironmentVariable, EnvironmentVariableTarget.Process);

            foreach (var entry in _snapshot)
            {
                try
                {
                    Environment.SetEnvironmentVariable(entry.Key.Name, entry.Value, entry.Key.Target);
                }
                catch
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(isolatedFolder)
                && isolatedFolder.Contains("WileyWidget.Tests", StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(isolatedFolder))
            {
                try
                {
                    Directory.Delete(isolatedFolder, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    private sealed class InMemorySecretVaultService : ISecretVaultService
    {
        private readonly Dictionary<string, string> _secrets;

        public InMemorySecretVaultService()
        {
            _secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public InMemorySecretVaultService(Dictionary<string, string> initialValues)
        {
            _secrets = new Dictionary<string, string>(initialValues, StringComparer.OrdinalIgnoreCase);
        }

        public string? GetSecret(string key)
        {
            return _secrets.TryGetValue(key, out var value) ? value : null;
        }

        public void StoreSecret(string key, string value)
        {
            _secrets[key] = value;
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetSecret(key));
        }

        public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _secrets[key] = value;
            return Task.CompletedTask;
        }

        public Task RotateSecretAsync(string secretName, string newValue, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _secrets[secretName] = newValue;
            return Task.CompletedTask;
        }

        public Task MigrateSecretsFromEnvironmentAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task PopulateProductionSecretsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }

        public Task<string> ExportSecretsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = JsonSerializer.Serialize(_secrets);
            return Task.FromResult(json);
        }

        public Task ImportSecretsAsync(string jsonSecrets, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonSecrets)
                ?? new Dictionary<string, string>();

            foreach (var item in parsed)
            {
                _secrets[item.Key] = item.Value;
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> ListSecretKeysAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IEnumerable<string>>(_secrets.Keys.ToArray());
        }

        public Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _secrets.Remove(secretName);
            return Task.CompletedTask;
        }

        public Task<string> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult($"InMemorySecretVaultService: {_secrets.Count} secrets");
        }
    }
}
