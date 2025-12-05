using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Syncfusion.Licensing;
using WileyWidget.Services;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // === GLOBAL EXCEPTION HANDLERS (MUST BE FIRST) ===
            // Wire up unhandled exception handlers BEFORE any other code runs
            // to ensure we catch and log ALL exceptions that escape normal handling
            SetupGlobalExceptionHandlers();

            // === Register Syncfusion License FIRST ===
            // CRITICAL: Must be called BEFORE ApplicationConfiguration.Initialize()
            // and BEFORE any Syncfusion control is initiated
            // Per Syncfusion docs: "Register the licensing code in static void main method
            // before calling Application.Run() method"
            RegisterSyncfusionLicense();

            ApplicationConfiguration.Initialize();  // Required for WinForms + .NET 9

            // Build the host — this is safe, nothing gets constructed yet
            var host = CreateHostBuilder().Build();

            // === MIGRATE SECRETS TO VAULT ===
            // Migrate machine-scope environment variables to encrypted vault for secure storage
            MigrateSecretsToVaultAsync(host).Wait();

            // === RUN STARTUP DIAGNOSTICS ===
            // This verifies all services can be resolved before the app tries to use them
            RunStartupDiagnosticsAsync(host).Wait();

            // Now we have a running message pump — safe to resolve MainForm + its dependencies
            // IMPORTANT: MainForm is Scoped, so resolve it within a service scope
            using (var scope = host.Services.CreateScope())
            {
                // Check for test mode to provide better error handling during UI tests
                var isTestMode = Environment.GetEnvironmentVariable("WILEY_UI_TEST_MODE") == "true";

                try
                {
                    var mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(scope.ServiceProvider);
                    Application.Run(mainForm);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, "Critical error running MainForm");
                    System.Diagnostics.Debug.WriteLine($"FATAL: MainForm failed: {ex}");

                    if (isTestMode)
                    {
                        // In test mode, show a dialog that FlaUI can detect
                        MessageBox.Show(
                            $"Startup failed: {ex.Message}\n\nDetails: {ex.GetType().Name}",
                            "Wiley Widget - Startup Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    else
                    {
                        // In normal mode, show user-friendly error
                        MessageBox.Show(
                            $"The application encountered an error during startup:\n\n{ex.Message}\n\nPlease check the logs for more details.",
                            "Wiley Widget - Startup Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    throw; // Re-throw to terminate the application
                }
            }
        }

        /// <summary>
        /// Set up global exception handlers to catch and log all unhandled exceptions.
        /// This includes:
        /// - AppDomain.CurrentDomain.UnhandledException: Non-UI thread exceptions
        /// - TaskScheduler.UnobservedTaskException: Unobserved Task exceptions
        /// - Application.ThreadException: WinForms UI thread exceptions
        /// - AppDomain.CurrentDomain.FirstChanceException: ALL exceptions (for debugging)
        /// </summary>
        private static void SetupGlobalExceptionHandlers()
        {
            // First-chance exception handler - logs ALL exceptions as they are thrown,
            // even if they will be caught later. Essential for debugging DI issues.
            AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
            {
                var ex = args.Exception;

                // Filter out common noise exceptions that are expected
                if (ex is OperationCanceledException)
                {
                    Serilog.Log.Debug(ex, "[FirstChance] OperationCanceledException: {Message}", ex.Message);
                }
                else if (ex is InvalidOperationException ioe && ioe.Source == "Microsoft.Extensions.DependencyInjection")
                {
                    // This is a DI exception - log with full details
                    Serilog.Log.Warning(ex, "[FirstChance][DI] InvalidOperationException from DI container: {Message}", ex.Message);
                    System.Diagnostics.Debug.WriteLine($"[FirstChance][DI] {ex.GetType().Name}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"  StackTrace: {ex.StackTrace}");
                }
                else if (ex is InvalidOperationException)
                {
                    Serilog.Log.Debug(ex, "[FirstChance] InvalidOperationException: {Message}", ex.Message);
                }
                else if (ex is System.Net.Sockets.SocketException || ex is System.Net.Http.HttpRequestException)
                {
                    // Network exceptions are common and may be handled - log at Debug
                    Serilog.Log.Debug(ex, "[FirstChance] Network exception: {Type} - {Message}", ex.GetType().Name, ex.Message);
                }
                else
                {
                    // Log other exceptions at Debug level to avoid noise
                    Serilog.Log.Debug(ex, "[FirstChance] {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
                }
            };

            // Unhandled exception handler - catches exceptions that escape all handlers
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                if (ex != null)
                {
                    Serilog.Log.Fatal(ex, "[UNHANDLED] AppDomain.UnhandledException - IsTerminating: {IsTerminating}, Type: {ExceptionType}",
                        args.IsTerminating, ex.GetType().FullName);
                    System.Diagnostics.Debug.WriteLine($"[UNHANDLED] {ex.GetType().Name}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"  StackTrace: {ex.StackTrace}");
                }
                else
                {
                    Serilog.Log.Fatal("[UNHANDLED] AppDomain.UnhandledException with non-Exception object: {Object}",
                        args.ExceptionObject?.ToString());
                }

                // Ensure logs are flushed
                Serilog.Log.CloseAndFlush();
            };

            // Task scheduler exception handler - catches unobserved Task exceptions
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Serilog.Log.Error(args.Exception, "[UNOBSERVED] TaskScheduler.UnobservedTaskException: {Message}",
                    args.Exception?.Message);
                System.Diagnostics.Debug.WriteLine($"[UNOBSERVED] Task exception: {args.Exception?.Message}");
                System.Diagnostics.Debug.WriteLine($"  StackTrace: {args.Exception?.StackTrace}");

                // Mark as observed to prevent application crash
                args.SetObserved();
            };

            // WinForms UI thread exception handler
            Application.ThreadException += (sender, args) =>
            {
                var ex = args.Exception;
                Serilog.Log.Error(ex, "[UI-THREAD] Application.ThreadException: {ExceptionType} - {Message}",
                    ex.GetType().Name, ex.Message);
                System.Diagnostics.Debug.WriteLine($"[UI-THREAD] {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  StackTrace: {ex.StackTrace}");

                // Show user-friendly error dialog
                MessageBox.Show(
                    $"An error occurred:\n\n{ex.Message}\n\nPlease check the logs for details.",
                    "Wiley Widget - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            // Set the unhandled exception mode for Windows Forms
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Serilog.Log.Debug("Global exception handlers configured successfully");
            System.Diagnostics.Debug.WriteLine("Global exception handlers configured");
        }

        /// <summary>
        /// Register Syncfusion license key from configuration.
        /// CRITICAL: This MUST be called BEFORE ApplicationConfiguration.Initialize()
        /// and BEFORE any Syncfusion control is instantiated.
        /// Per Syncfusion documentation: "Register the licensing code in static void main
        /// method before calling Application.Run() method"
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Startup diagnostic messages; not localized.")]
        private static void RegisterSyncfusionLicense()
        {
            try
            {
                // Try environment variable first (highest priority)
                var syncfusionKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

                // If not in environment, try configuration file
                if (string.IsNullOrEmpty(syncfusionKey))
                {
                    var config = new ConfigurationBuilder()
                        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                        .Build();

                    syncfusionKey = config["Syncfusion:LicenseKey"];
                }

                if (!string.IsNullOrEmpty(syncfusionKey) && !syncfusionKey.StartsWith("${", StringComparison.Ordinal))
                {
                    SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
                    System.Diagnostics.Debug.WriteLine("SUCCESS: Syncfusion license registered successfully.");
                }
                else
                {
                    // License key not found - Syncfusion controls will show trial watermark
                    System.Diagnostics.Debug.WriteLine("WARNING: Syncfusion license key not found in configuration. Controls will run in trial mode.");
                    Serilog.Log.Warning("Syncfusion license key not found in configuration. Set SYNCFUSION_LICENSE_KEY environment variable or add to appsettings.json. Controls will run in trial mode.");
                }
            }
            catch (Exception ex)
            {
                // Log to Debug output since we don't have logger yet
                System.Diagnostics.Debug.WriteLine($"ERROR: Failed to register Syncfusion license: {ex.Message}");
                Serilog.Log.Error(ex, "Failed to register Syncfusion license");
                // Don't throw - allow app to continue in trial mode
            }
        }

        /// <summary>
        /// Migrate machine-scope environment variables to encrypted vault for secure storage.
        /// This ensures sensitive keys are encrypted at rest rather than stored in plain text.
        /// Implements robust error handling with per-secret retry logic and detailed logging.
        /// </summary>
        private static async Task MigrateSecretsToVaultAsync(IHost host)
        {
            Serilog.ILogger? logger = null;
            try
            {
                // Get Serilog logger from host
                logger = Serilog.Log.ForContext("SourceContext", "Startup.SecretMigration");

                var secretVault = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ISecretVaultService>(host.Services);
                if (secretVault == null)
                {
                    logger.Warning("ISecretVaultService not available - skipping secret migration. Services will fall back to configuration/environment variables.");
                    return;
                }

                // Test vault availability before attempting migration
                try
                {
                    var vaultAvailable = await secretVault.TestConnectionAsync();
                    if (!vaultAvailable)
                    {
                        logger.Warning("Secret vault connection test failed - skipping migration. Vault may not be properly initialized.");
                        return;
                    }
                }
                catch (Exception testEx)
                {
                    logger.Warning(testEx, "Failed to test vault connection - attempting migration anyway");
                }

                logger.Information("Starting secret migration from environment variables to encrypted vault...");

                // List of critical secrets to migrate from environment variables to vault
                var secretsToMigrate = new Dictionary<string, bool>
                {
                    { "XAI_API_KEY", false },                      // Optional - AI features
                    { "SYNCFUSION_LICENSE_KEY", false },           // Optional - will show trial watermark
                    { "BOLDREPORTS_LICENSE_KEY", false },          // Optional - reporting features
                    { "QBO_CLIENT_ID", false },                    // Optional - QuickBooks integration
                    { "QBO_CLIENT_SECRET", false },                // Optional - QuickBooks integration
                    { "QBO_REALM_ID", false },                     // Optional - QuickBooks integration
                    { "APPLICATIONINSIGHTS_CONNECTION_STRING", false } // Optional - telemetry
                };

                int migratedCount = 0;
                int skippedCount = 0;
                int failedCount = 0;
                var failedSecrets = new List<string>();

                foreach (var (secretName, isRequired) in secretsToMigrate)
                {
                    try
                    {
                        // Check if already in vault (with null safety)
                        string? existingSecret = null;
                        try
                        {
                            existingSecret = secretVault.GetSecret(secretName);
                        }
                        catch (Exception ex)
                        {
                            logger.Warning(ex, "Failed to check existing secret '{SecretName}' in vault", secretName);
                        }

                        if (!string.IsNullOrWhiteSpace(existingSecret))
                        {
                            logger.Debug("Secret '{SecretName}' already exists in vault (length: {Length}), skipping",
                                secretName, existingSecret.Length);
                            skippedCount++;
                            continue;
                        }

                        // Try to get from environment variables with fallback chain
                        string? envValue = null;
                        try
                        {
                            envValue = Environment.GetEnvironmentVariable(secretName, EnvironmentVariableTarget.Machine);
                            if (string.IsNullOrWhiteSpace(envValue))
                            {
                                envValue = Environment.GetEnvironmentVariable(secretName, EnvironmentVariableTarget.User);
                            }
                            if (string.IsNullOrWhiteSpace(envValue))
                            {
                                envValue = Environment.GetEnvironmentVariable(secretName, EnvironmentVariableTarget.Process);
                            }
                        }
                        catch (Exception envEx)
                        {
                            logger.Warning(envEx, "Failed to read environment variable '{SecretName}'", secretName);
                        }

                        // Validate and migrate
                        if (string.IsNullOrWhiteSpace(envValue))
                        {
                            if (isRequired)
                            {
                                logger.Warning("Required secret '{SecretName}' not found in environment variables", secretName);
                            }
                            else
                            {
                                logger.Debug("Optional secret '{SecretName}' not found in environment - skipping", secretName);
                            }
                            continue;
                        }

                        // Skip placeholder values from configuration
                        if (envValue.StartsWith("${", StringComparison.Ordinal) && envValue.EndsWith("}", StringComparison.Ordinal))
                        {
                            logger.Debug("Secret '{SecretName}' contains placeholder value - skipping", secretName);
                            continue;
                        }

                        // Validate secret value isn't obviously invalid
                        if (envValue.Length < 8)
                        {
                            logger.Warning("Secret '{SecretName}' appears too short (length: {Length}) - may be invalid",
                                secretName, envValue.Length);
                        }

                        // Attempt migration with retry logic
                        const int maxRetries = 3;
                        for (int retry = 0; retry < maxRetries; retry++)
                        {
                            try
                            {
                                await secretVault.SetSecretAsync(secretName, envValue);
                                logger.Information("✓ Migrated secret '{SecretName}' to encrypted vault (length: {Length}, attempt: {Attempt})",
                                    secretName, envValue.Length, retry + 1);
                                migratedCount++;
                                break; // Success - exit retry loop
                            }
                            catch (ArgumentException argEx)
                            {
                                logger.Error(argEx, "Invalid argument while storing '{SecretName}' - skipping", secretName);
                                failedSecrets.Add(secretName);
                                failedCount++;
                                break; // Don't retry on validation errors
                            }
                            catch (ObjectDisposedException objEx)
                            {
                                logger.Error(objEx, "Vault service disposed while storing '{SecretName}'", secretName);
                                failedSecrets.Add(secretName);
                                failedCount++;
                                break; // Don't retry if service is disposed
                            }
                            catch (Exception setEx)
                            {
                                if (retry == maxRetries - 1)
                                {
                                    logger.Error(setEx, "Failed to migrate '{SecretName}' after {MaxRetries} attempts",
                                        secretName, maxRetries);
                                    failedSecrets.Add(secretName);
                                    failedCount++;
                                }
                                else
                                {
                                    logger.Warning(setEx, "Retry {Retry}/{MaxRetries} for '{SecretName}'",
                                        retry + 1, maxRetries, secretName);
                                    await Task.Delay(TimeSpan.FromMilliseconds(100 * (retry + 1))); // Exponential backoff
                                }
                            }
                        }
                    }
                    catch (Exception secretEx)
                    {
                        logger.Error(secretEx, "Unexpected error processing secret '{SecretName}'", secretName);
                        failedSecrets.Add(secretName);
                        failedCount++;
                    }
                }

                // Summary logging
                logger.Information("Secret migration complete: {Migrated} migrated, {Skipped} already in vault, {Failed} failed",
                    migratedCount, skippedCount, failedCount);

                if (failedSecrets.Any())
                {
                    logger.Warning("Failed to migrate secrets: {FailedSecrets}. These services may need manual configuration.",
                        string.Join(", ", failedSecrets));
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex, "Critical error during secret migration - continuing with startup. Some services may not have access to required secrets.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"CRITICAL: Secret migration failed without logger: {ex}");
                }
                // Don't throw - allow app to continue even if migration completely fails
                // Services will fall back to reading from configuration/environment directly
            }
        }

        /// <summary>
        /// Run startup diagnostics to catch DI configuration errors early
        /// </summary>
        private static async Task RunStartupDiagnosticsAsync(IHost host)
        {
            var logger = Serilog.Log.ForContext("SourceContext", "Startup");
            try
            {
                var diagnostics = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupDiagnostics>(host.Services);

                logger.Information("═══════════════════════════════════════════════════════════");
                logger.Information("Starting Wiley Widget Startup Diagnostics");
                logger.Information("═══════════════════════════════════════════════════════════");

                var report = await diagnostics.RunDiagnosticsAsync();

                // Log the full report
                logger.Information(report.ToString());

                if (!report.AllChecksPassed)
                {
                    logger.Warning("⚠ Some diagnostics checks failed. The application may not function correctly.");
                    foreach (var failure in report.Results.Where(r => !r.IsSuccess))
                    {
                        logger.Error("  FAILED: {Service} - {Message}", failure.ServiceName, failure.Message);
                        if (failure.Exception != null)
                        {
                            logger.Error(failure.Exception, "Exception details for {Service}", failure.ServiceName);
                        }
                    }
                    // Optionally, you could throw here to prevent startup if critical services fail
                    // throw new InvalidOperationException("Startup diagnostics detected critical issues");
                }
                else
                {
                    logger.Information("✓ All startup diagnostics checks passed!");
                }

                logger.Information("═══════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to run startup diagnostics");
                throw;
            }
        }

        private static IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    loggerConfiguration
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext();
                })
                .ConfigureServices((context, services) =>
                {
                    // Register all WileyWidget services
                    DependencyInjection.ConfigureServices(services, context.Configuration);

                    // === REGISTER STARTUP DIAGNOSTICS ===
                    services.AddSingleton<IStartupDiagnostics, StartupDiagnostics>();
                });
        }
    }
}
