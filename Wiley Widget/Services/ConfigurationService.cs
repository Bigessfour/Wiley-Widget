using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace WileyWidget.Services;

/// <summary>
/// Service for loading and managing application configuration.
/// </summary>
public class AppConfigurationService
{
    private IConfiguration _configuration;

    /// <summary>
    /// Loads application configuration from multiple sources.
    /// </summary>
    public void LoadConfiguration()
    {
        try
        {
            Log.Information("📄 Loading application configuration...");

            // Get the project root directory (where appsettings.json is located)
            var basePath = Path.GetDirectoryName(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory));
            Log.Debug("📂 Configuration base path: {BasePath}", basePath);

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            // Add user secrets only in development
#if DEBUG
            builder.AddUserSecrets<WileyWidget.App>();
            Log.Debug("🔐 User secrets added for development environment");
#endif

            _configuration = builder.Build();

            // Validate critical configuration exists
            ValidateConfiguration();

            Log.Information("✅ Configuration loaded successfully");
            Log.Debug("🔧 Available configuration sections: {Sections}",
                string.Join(", ", _configuration.GetChildren().Select(c => c.Key)));
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 CRITICAL: Failed to load application configuration");
            throw new InvalidOperationException("Application configuration could not be loaded", ex);
        }
    }

    /// <summary>
    /// Validates that critical configuration sections are present and valid.
    /// </summary>
    private void ValidateConfiguration()
    {
        try
        {
            // Check for database connection string
            var defaultConnection = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(defaultConnection))
            {
                Log.Warning("⚠️ DefaultConnection not found in configuration");
            }
            else
            {
                // Validate connection string format
                if (IsValidConnectionString(defaultConnection))
                {
                    Log.Debug("✅ Database connection string configured and valid");
                }
                else
                {
                    Log.Warning("⚠️ Database connection string format appears invalid: {ConnectionString}", defaultConnection);
                }
            }

            // Check for Syncfusion license configuration
            var syncfusionSection = _configuration.GetSection("Syncfusion");
            if (syncfusionSection.Exists())
            {
                Log.Debug("✅ Syncfusion configuration section found");
            }
            else
            {
                Log.Debug("ℹ️ Syncfusion configuration section not found (will use environment variables)");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Error validating configuration");
        }
    }

    /// <summary>
    /// Validates the format of a database connection string.
    /// </summary>
    private bool IsValidConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        // For SQLite connection strings, check for "Data Source="
        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            // Extract the data source path
            var dataSourcePart = connectionString.Split(';')
                .FirstOrDefault(part => part.Trim().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(dataSourcePart))
            {
                var path = dataSourcePart.Split('=')[1]?.Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    // SECURITY: Validate path to prevent directory traversal
                    if (!path.Contains("..") &&
                        !Path.GetInvalidPathChars().Any(c => path.Contains(c)) &&
                        Path.GetFullPath(path) == path) // Ensure absolute path
                    {
                        return true;
                    }
                }
            }
        }

        // For SQL Server connection strings, check for required components
        if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Gets the loaded configuration.
    /// </summary>
    public IConfiguration Configuration => _configuration;
}
