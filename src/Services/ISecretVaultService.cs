using System.Collections.Generic;
using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Minimal contract used throughout the application for secret storage and retrieval.
/// Provides a local secret vault abstraction now that Azure services have been removed.
/// </summary>
public interface ISecretVaultService
{
    Task<string?> GetSecretAsync(string secretName);
    string? GetSecret(string secretName);
    Task SetSecretAsync(string secretName, string value);
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Migrates secrets from environment variables to the local vault.
    /// </summary>
    Task MigrateSecretsFromEnvironmentAsync();

    /// <summary>
    /// Populates the vault with production-ready default secrets.
    /// </summary>
    Task PopulateProductionSecretsAsync();

    /// <summary>
    /// Exports all secrets to a JSON string for backup.
    /// </summary>
    Task<string> ExportSecretsAsync();

    /// <summary>
    /// Imports secrets from a JSON string.
    /// </summary>
    Task ImportSecretsAsync(string jsonSecrets);

    /// <summary>
    /// Lists all secret keys for inventory purposes.
    /// </summary>
    Task<IEnumerable<string>> ListSecretKeysAsync();
    
    /// <summary>
    /// Deletes a secret from the vault. Use when rotating or removing secrets.
    /// </summary>
    Task DeleteSecretAsync(string secretName);
    
    /// <summary>
    /// Atomically rotate an existing secret value. Writes the new value, verifies it, then removes the old value.
    /// </summary>
    Task RotateSecretAsync(string secretName, string newValue);
}
