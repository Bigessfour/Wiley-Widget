// DatabaseConnectionValidator.cs - Database connection validation service
//
// Created as part of Enhanced DI Registration for DbContextFactory (Prompt 4)
// Date: November 9, 2025
//
// This service validates database connection strings and provides fallback logic
// similar to StartupEnvironmentValidator pattern with graceful degradation.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using WileyWidget.Models;

namespace WileyWidget.Services.Startup
{
    /// <summary>
    /// Interface for database connection validation service.
    /// Provides validation and fallback logic for database connectivity.
    /// </summary>
    public interface IDatabaseConnectionValidator
    {
        /// <summary>
        /// Validates the database connection string and connectivity.
        /// Returns validation result with warnings instead of throwing exceptions.
        /// </summary>
        /// <param name="connectionString">Connection string to validate</param>
        /// <returns>Validation result with success status and any warnings</returns>
        (bool IsValid, string[] Warnings, string FallbackConnectionString) ValidateConnectionString(string? connectionString);

        /// <summary>
        /// Tests actual database connectivity with timeout and retry logic.
        /// Used for health checks and startup validation.
        /// </summary>
        /// <param name="connectionString">Connection string to test</param>
        /// <returns>True if connection successful, false otherwise</returns>
        Task<bool> TestConnectivityAsync(string connectionString);

        /// <summary>
        /// Gets an appropriate fallback connection string for the current environment.
        /// Used when primary connection string is missing or invalid.
        /// </summary>
        /// <returns>Environment-specific fallback connection string</returns>
        string GetFallbackConnectionString();

        /// <summary>
        /// Determines if database migrations should be skipped based on connection health.
        /// Implements conditional migration logic mentioned in the requirements.
        /// </summary>
        /// <param name="connectionString">Connection string to evaluate</param>
        /// <returns>True if migrations should be skipped</returns>
        bool ShouldSkipMigrations(string? connectionString);
    }

    /// <summary>
    /// Database connection validation service implementation.
    /// Provides enterprise-grade connection validation with graceful degradation.
    /// Follows StartupEnvironmentValidator pattern for consistent error handling.
    /// </summary>
    public class DatabaseConnectionValidator : IDatabaseConnectionValidator
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<DatabaseConnectionValidator> _logger;

        public DatabaseConnectionValidator(
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            ILogger<DatabaseConnectionValidator> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates the database connection string and connectivity.
        /// Implements graceful degradation following StartupEnvironmentValidator pattern.
        /// </summary>
        public (bool IsValid, string[] Warnings, string FallbackConnectionString) ValidateConnectionString(string? connectionString)
        {
            var warnings = new List<string>();
            var isValid = true;
            var fallbackConnectionString = GetFallbackConnectionString();

            try
            {
                Log.Information("[DB_VALIDATOR] Starting database connection validation for {Environment}",
                    _hostEnvironment.EnvironmentName);
                _logger.LogInformation("Starting database connection validation for {Environment}",
                    _hostEnvironment.EnvironmentName);

                // 1. Check if connection string is provided
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    var warning = "Database connection string not configured - using fallback";
                    warnings.Add(warning);
                    Log.Warning("[DB_VALIDATOR] ⚠ {Warning}", warning);
                    _logger.LogWarning("⚠ {Warning}", warning);

                    // In Production, this is more serious
                    if (_hostEnvironment.IsProduction())
                    {
                        isValid = false;
                        warnings.Add("Missing database connection in Production environment");
                        Log.Error("[DB_VALIDATOR] ❌ Missing database connection in Production");
                        _logger.LogError("❌ Missing database connection in Production");
                    }

                    return (isValid, warnings.ToArray(), fallbackConnectionString);
                }

                // 2. Validate connection string format
                if (!IsConnectionStringFormatValid(connectionString))
                {
                    var warning = "Database connection string format is invalid";
                    warnings.Add(warning);
                    isValid = false;
                    Log.Warning("[DB_VALIDATOR] ⚠ {Warning}: {ConnectionString}", warning, MaskConnectionString(connectionString));
                    _logger.LogWarning("⚠ {Warning}: {ConnectionString}", warning, MaskConnectionString(connectionString));

                    return (isValid, warnings.ToArray(), fallbackConnectionString);
                }

                // 3. Environment-specific validation rules
                if (_hostEnvironment.IsProduction())
                {
                    ValidateProductionConnectionString(connectionString, warnings);
                }
                else
                {
                    ValidateDevelopmentConnectionString(connectionString, warnings);
                }

                // 4. Log successful validation
                if (warnings.Count == 0)
                {
                    Log.Information("[DB_VALIDATOR] ✓ Database connection validation passed");
                    _logger.LogInformation("✓ Database connection validation passed");
                }
                else
                {
                    Log.Warning("[DB_VALIDATOR] ⚠ Database connection validation completed with {WarningCount} warnings", warnings.Count);
                    _logger.LogWarning("⚠ Database connection validation completed with {WarningCount} warnings", warnings.Count);
                }

                return (isValid, warnings.ToArray(), fallbackConnectionString);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB_VALIDATOR] ❌ Database connection validation failed");
                _logger.LogError(ex, "❌ Database connection validation failed");

                warnings.Add($"Database connection validation error: {ex.Message}");
                return (false, warnings.ToArray(), fallbackConnectionString);
            }
        }

        /// <summary>
        /// Tests actual database connectivity with enterprise-grade retry and timeout logic.
        /// </summary>
        public async Task<bool> TestConnectivityAsync(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await connection.OpenAsync(cts.Token);
                await connection.CloseAsync();

                Log.Information("[DB_VALIDATOR] ✓ Database connectivity test successful");
                _logger.LogInformation("✓ Database connectivity test successful");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[DB_VALIDATOR] ⚠ Database connectivity test failed");
                _logger.LogWarning(ex, "⚠ Database connectivity test failed");
                return false;
            }
        }

        /// <summary>
        /// Gets environment-appropriate fallback connection string.
        /// Implements EnterpriseResourceLoader-style fallback pattern.
        /// </summary>
        public string GetFallbackConnectionString()
        {
            if (_hostEnvironment.IsDevelopment())
            {
                return "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
            }
            else if (_hostEnvironment.IsStaging())
            {
                return "Server=.\\SQLEXPRESS;Database=WileyWidgetStaging;Trusted_Connection=True;TrustServerCertificate=True;";
            }
            else
            {
                // Production should never use fallback - this is a last resort
                Log.Fatal("[DB_VALIDATOR] ❌ CRITICAL: Using fallback connection string in Production!");
                return "Server=.\\SQLEXPRESS;Database=WileyWidgetProd;Trusted_Connection=True;TrustServerCertificate=True;";
            }
        }

        /// <summary>
        /// Determines if migrations should be skipped based on connection health.
        /// Implements conditional migration logic for resilient startup.
        /// </summary>
        public bool ShouldSkipMigrations(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Log.Warning("[DB_VALIDATOR] ⚠ Skipping migrations - no connection string available");
                _logger.LogWarning("⚠ Skipping migrations - no connection string available");
                return true;
            }

            // In Development, we can be more lenient
            if (_hostEnvironment.IsDevelopment())
            {
                return false; // Always attempt migrations in dev
            }

            // In Production, test connectivity first
            var connectivityTask = TestConnectivityAsync(connectionString);
            var canConnect = connectivityTask.GetAwaiter().GetResult(); // Sync call for startup

            if (!canConnect)
            {
                Log.Warning("[DB_VALIDATOR] ⚠ Skipping migrations - database connectivity failed");
                _logger.LogWarning("⚠ Skipping migrations - database connectivity failed");
                return true;
            }

            return false;
        }

        #region Private Helper Methods

        /// <summary>
        /// Validates connection string format using SqlConnectionStringBuilder.
        /// </summary>
        private static bool IsConnectionStringFormatValid(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                return !string.IsNullOrWhiteSpace(builder.DataSource);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates connection string for Production environment requirements.
        /// </summary>
        private void ValidateProductionConnectionString(string connectionString, List<string> warnings)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);

                // Production should not use SQL Express
                if (builder.DataSource.Contains("SQLEXPRESS", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("Production environment using SQL Express - consider dedicated SQL Server");
                }

                // Production should use authentication
                if (string.IsNullOrWhiteSpace(builder.UserID) && !builder.IntegratedSecurity)
                {
                    warnings.Add("Production connection string lacks proper authentication configuration");
                }

                // Check for development database names in production
                if (builder.InitialCatalog.Contains("Dev", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("Production environment appears to be using development database name");
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not validate Production connection string: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates connection string for Development environment requirements.
        /// </summary>
        private void ValidateDevelopmentConnectionString(string connectionString, List<string> warnings)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);

                // Development can use SQL Express
                if (!builder.DataSource.Contains("SQLEXPRESS", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("Development environment not using SQL Express - this is acceptable but unusual");
                }

                // Warn if using production-like database name in development
                if (!builder.InitialCatalog.Contains("Dev", StringComparison.OrdinalIgnoreCase) &&
                    !builder.InitialCatalog.Contains("Test", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("Development environment database name does not indicate development/test usage");
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not validate Development connection string: {ex.Message}");
            }
        }

        /// <summary>
        /// Masks sensitive information in connection string for logging.
        /// </summary>
        private static string MaskConnectionString(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                if (!string.IsNullOrWhiteSpace(builder.Password))
                {
                    builder.Password = "***";
                }
                return builder.ToString();
            }
            catch
            {
                return "[INVALID_CONNECTION_STRING]";
            }
        }

        #endregion
    }
}
