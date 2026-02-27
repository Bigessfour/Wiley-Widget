using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Services;

internal sealed record SettingsSecretsSnapshot(string? SyncfusionLicenseKey, string? XaiApiKey);

internal sealed class SettingsSecretsPersistResult
{
    public bool Success { get; init; }

    public List<string> Warnings { get; } = new();

    public string? ErrorMessage { get; init; }
}

internal sealed class SettingsSecretsPersistenceService
{
    private static readonly string[] SyncfusionUserSecretAliases =
    {
        "WILEY_SYNC_LIC_KEY",
        "Syncfusion:LicenseKey",
        "SYNCFUSION_LICENSE_KEY",
        "Syncfusion__LicenseKey",
        "Syncfusion-LicenseKey",
        "SyncfusionLicenseKey",
        "syncfusion-license-key"
    };

    private static readonly string[] XaiUserSecretAliases =
    {
        "XAI:ApiKey",
        "xAI:ApiKey"
    };

    private static readonly string[] SyncfusionEnvironmentAliases =
    {
        "WILEY_SYNC_LIC_KEY",
        "SYNCFUSION_LICENSE_KEY"
    };

    private static readonly string[] XaiEnvironmentAliases =
    {
        "XAI__ApiKey",
        "XAI_API_KEY"
    };

    private readonly ILogger<SettingsSecretsPersistenceService> _logger;
    private readonly ISecretVaultService? _secretVaultService;

    public SettingsSecretsPersistenceService(
        ILogger<SettingsSecretsPersistenceService> logger,
        ISecretVaultService? secretVaultService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretVaultService = secretVaultService;
    }

    public async Task<SettingsSecretsSnapshot> LoadCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userSecretsConfiguration = BuildUserSecretsConfiguration();

        var syncfusionFromUserSecrets = ResolveFirstValid(userSecretsConfiguration, SyncfusionUserSecretAliases);
        var xaiFromUserSecrets = ResolveFirstValid(userSecretsConfiguration, XaiUserSecretAliases);

        var syncfusionLicenseKey = !string.IsNullOrWhiteSpace(syncfusionFromUserSecrets)
            ? syncfusionFromUserSecrets
            : ResolveFirstValidFromEnvironment(SyncfusionEnvironmentAliases);

        var xaiApiKey = !string.IsNullOrWhiteSpace(xaiFromUserSecrets)
            ? xaiFromUserSecrets
            : ResolveFirstValidFromEnvironment(XaiEnvironmentAliases);

        if (string.IsNullOrWhiteSpace(syncfusionLicenseKey) || string.IsNullOrWhiteSpace(xaiApiKey))
        {
            try
            {
                if (_secretVaultService != null)
                {
                    if (string.IsNullOrWhiteSpace(syncfusionLicenseKey))
                    {
                        syncfusionLicenseKey = NormalizeSecret(await _secretVaultService.GetSecretAsync("SYNCFUSION_LICENSE_KEY", cancellationToken).ConfigureAwait(false))
                            ?? NormalizeSecret(await _secretVaultService.GetSecretAsync("Syncfusion:LicenseKey", cancellationToken).ConfigureAwait(false));
                    }

                    if (string.IsNullOrWhiteSpace(xaiApiKey))
                    {
                        xaiApiKey = NormalizeSecret(await _secretVaultService.GetSecretAsync("XAI:ApiKey", cancellationToken).ConfigureAwait(false))
                            ?? NormalizeSecret(await _secretVaultService.GetSecretAsync("XAI__ApiKey", cancellationToken).ConfigureAwait(false));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to read one or more secrets from encrypted vault while loading settings secrets");
            }
        }

        return new SettingsSecretsSnapshot(syncfusionLicenseKey, xaiApiKey);
    }

    public async Task<SettingsSecretsPersistResult> PersistAsync(
        string? syncfusionLicenseKey,
        string? xaiApiKey,
        CancellationToken cancellationToken = default)
    {
        var result = new SettingsSecretsPersistResult { Success = true };

        var normalizedSyncfusion = NormalizeSecret(syncfusionLicenseKey);
        var normalizedXai = NormalizeSecret(xaiApiKey);

        try
        {
            if (!string.IsNullOrWhiteSpace(normalizedSyncfusion))
            {
                await PersistToUserSecretsAsync(SyncfusionUserSecretAliases, normalizedSyncfusion, cancellationToken).ConfigureAwait(false);
                PersistToEnvironmentScopes(SyncfusionEnvironmentAliases, normalizedSyncfusion, result.Warnings);
                await PersistToSecretVaultAsync("SYNCFUSION_LICENSE_KEY", normalizedSyncfusion, cancellationToken).ConfigureAwait(false);
                await PersistToSecretVaultAsync("Syncfusion:LicenseKey", normalizedSyncfusion, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(normalizedXai))
            {
                await PersistToUserSecretsAsync(XaiUserSecretAliases, normalizedXai, cancellationToken).ConfigureAwait(false);
                PersistToEnvironmentScopes(XaiEnvironmentAliases, normalizedXai, result.Warnings);
                await PersistToSecretVaultAsync("XAI:ApiKey", normalizedXai, cancellationToken).ConfigureAwait(false);
                await PersistToSecretVaultAsync("XAI__ApiKey", normalizedXai, cancellationToken).ConfigureAwait(false);
            }

            var userSecretsConfiguration = BuildUserSecretsConfiguration();
            if (!string.IsNullOrWhiteSpace(normalizedSyncfusion)
                && string.IsNullOrWhiteSpace(ResolveFirstValid(userSecretsConfiguration, SyncfusionUserSecretAliases)))
            {
                throw new InvalidOperationException("Syncfusion key verification failed after writing to user-secrets.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedXai)
                && string.IsNullOrWhiteSpace(ResolveFirstValid(userSecretsConfiguration, XaiUserSecretAliases)))
            {
                throw new InvalidOperationException("xAI key verification failed after writing to user-secrets.");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist settings secrets to user-secrets/environment");
            return new SettingsSecretsPersistResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private IConfiguration BuildUserSecretsConfiguration()
    {
        return new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .Build();
    }

    private static string? ResolveFirstValid(IConfiguration configuration, IEnumerable<string> aliases)
    {
        foreach (var alias in aliases)
        {
            var normalized = NormalizeSecret(configuration[alias]);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? ResolveFirstValidFromEnvironment(IEnumerable<string> aliases)
    {
        foreach (var alias in aliases)
        {
            var processValue = NormalizeSecret(Environment.GetEnvironmentVariable(alias, EnvironmentVariableTarget.Process));
            if (!string.IsNullOrWhiteSpace(processValue))
            {
                return processValue;
            }

            var userValue = NormalizeSecret(Environment.GetEnvironmentVariable(alias, EnvironmentVariableTarget.User));
            if (!string.IsNullOrWhiteSpace(userValue))
            {
                return userValue;
            }

            var machineValue = NormalizeSecret(Environment.GetEnvironmentVariable(alias, EnvironmentVariableTarget.Machine));
            if (!string.IsNullOrWhiteSpace(machineValue))
            {
                return machineValue;
            }
        }

        return null;
    }

    private async Task PersistToUserSecretsAsync(
        IEnumerable<string> aliases,
        string value,
        CancellationToken cancellationToken)
    {
        var projectPath = ResolveWinFormsProjectPath();
        var userSecretsId = ResolveUserSecretsId();

        foreach (var alias in aliases.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await RunDotnetUserSecretsSetAsync(alias, value, projectPath, userSecretsId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string? ResolveWinFormsProjectPath()
    {
        var cwdCandidate = Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "WileyWidget.WinForms.csproj");
        if (File.Exists(cwdCandidate))
        {
            return cwdCandidate;
        }

        var rootFromBase = FindRepoRoot(new DirectoryInfo(AppContext.BaseDirectory));
        if (rootFromBase != null)
        {
            var fromBaseCandidate = Path.Combine(rootFromBase.FullName, "src", "WileyWidget.WinForms", "WileyWidget.WinForms.csproj");
            if (File.Exists(fromBaseCandidate))
            {
                return fromBaseCandidate;
            }
        }

        var rootFromCwd = FindRepoRoot(new DirectoryInfo(Directory.GetCurrentDirectory()));
        if (rootFromCwd != null)
        {
            var fromCwdCandidate = Path.Combine(rootFromCwd.FullName, "src", "WileyWidget.WinForms", "WileyWidget.WinForms.csproj");
            if (File.Exists(fromCwdCandidate))
            {
                return fromCwdCandidate;
            }
        }

        return null;
    }

    private static DirectoryInfo? FindRepoRoot(DirectoryInfo? start)
    {
        while (start != null)
        {
            if (File.Exists(Path.Combine(start.FullName, "WileyWidget.sln")))
            {
                return start;
            }

            start = start.Parent;
        }

        return null;
    }

    private static string? ResolveUserSecretsId()
    {
        var attr = typeof(Program).Assembly.GetCustomAttribute<UserSecretsIdAttribute>();
        return string.IsNullOrWhiteSpace(attr?.UserSecretsId) ? null : attr.UserSecretsId;
    }

    private async Task RunDotnetUserSecretsSetAsync(
        string key,
        string value,
        string? projectPath,
        string? userSecretsId,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("user-secrets");
        startInfo.ArgumentList.Add("set");
        startInfo.ArgumentList.Add(key);
        startInfo.ArgumentList.Add(value);

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(projectPath);
        }
        else if (!string.IsNullOrWhiteSpace(userSecretsId))
        {
            startInfo.ArgumentList.Add("--id");
            startInfo.ArgumentList.Add(userSecretsId);
        }
        else
        {
            throw new InvalidOperationException("Unable to resolve WinForms project path or UserSecretsId for user-secrets update.");
        }

        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet user-secrets process.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet user-secrets set failed for key '{key}'. {stdErr} {stdOut}".Trim());
        }
    }

    private static void PersistToEnvironmentScopes(
        IEnumerable<string> aliases,
        string value,
        ICollection<string> warnings)
    {
        foreach (var alias in aliases.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable(alias, value, EnvironmentVariableTarget.Process);

            try
            {
                Environment.SetEnvironmentVariable(alias, value, EnvironmentVariableTarget.Machine);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                warnings.Add($"Could not set machine environment variable '{alias}' (insufficient permission). Run the app elevated to apply machine-scope updates.");

                try
                {
                    Environment.SetEnvironmentVariable(alias, value, EnvironmentVariableTarget.User);
                }
                catch
                {
                    // Intentionally ignore here; warning already captured for machine scope issue.
                }
            }
        }
    }

    private async Task PersistToSecretVaultAsync(string key, string value, CancellationToken cancellationToken)
    {
        if (_secretVaultService == null)
        {
            return;
        }

        try
        {
            await _secretVaultService.SetSecretAsync(key, value, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to persist key '{SecretKey}' to encrypted vault", key);
        }
    }

    private static string? NormalizeSecret(string? rawValue)
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
}
