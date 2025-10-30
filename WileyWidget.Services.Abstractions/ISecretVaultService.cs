namespace WileyWidget.Services
{
    public interface ISecretVaultService
    {
        // Synchronous helpers kept for compatibility
        string? GetSecret(string key);
        void StoreSecret(string key, string value);

        // Async APIs used by services that perform IO or network access
        System.Threading.Tasks.Task<string?> GetSecretAsync(string key);
        System.Threading.Tasks.Task SetSecretAsync(string key, string value);
        System.Threading.Tasks.Task RotateSecretAsync(string secretName, string newValue);

        /// <summary>
        /// Migrate any secrets present in environment variables into the configured vault.
        /// This is used at startup to move developer-supplied environment secrets into the
        /// local encrypted vault so runtime code can uniformly read from the vault.
        /// </summary>
        System.Threading.Tasks.Task MigrateSecretsFromEnvironmentAsync();

        /// <summary>
        /// Populate production-only secrets into the vault when running in Production environment.
        /// Implementations may be a no-op for local file-based vaults.
        /// </summary>
        System.Threading.Tasks.Task PopulateProductionSecretsAsync();

        /// <summary>
        /// Test whether the secret vault is available and operational.
        /// Returns true when accessible; false when not.
        /// </summary>
        System.Threading.Tasks.Task<bool> TestConnectionAsync();

        /// <summary>
        /// Export all secrets to a JSON string for backup purposes.
        /// WARNING: This contains sensitive data - handle with care.
        /// </summary>
        System.Threading.Tasks.Task<string> ExportSecretsAsync();

        /// <summary>
        /// Import secrets from a JSON string.
        /// WARNING: This will overwrite existing secrets with the same keys.
        /// </summary>
        System.Threading.Tasks.Task ImportSecretsAsync(string jsonSecrets);

        /// <summary>
        /// List all secret keys (without values) for inventory purposes.
        /// </summary>
        System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<string>> ListSecretKeysAsync();

        /// <summary>
        /// Delete a secret from the vault.
        /// </summary>
        System.Threading.Tasks.Task DeleteSecretAsync(string secretName);
    }
}
