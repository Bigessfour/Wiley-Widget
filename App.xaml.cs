using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Infrastructure.Bootstrap;
using WileyWidget.Services;
using WileyWidget.Views;
using Syncfusion.Licensing;
using Serilog;

namespace WileyWidget;

/// <summary>
/// Main application class using the service-based architecture.
/// Follows Microsoft WPF Application Startup guidelines.
/// </summary>
public partial class App : Application, IDisposable
{
    private ApplicationBootstrapper _bootstrapper;
    private ApplicationInitializationService _initializationService;
    private bool _disposed;

    /// <summary>
    /// WPF application constructor - Microsoft best practice compliant.
    /// Follows Microsoft guidelines for minimal constructor operations.
    /// </summary>
    public App()
    {
        // Essential timing for performance monitoring only
        var startupTimer = Stopwatch.StartNew();

        try
        {
            // OFFICIAL SYNCFUSION WPF 30.2.7 LICENSE REGISTRATION
            // Based on: https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application
            // Security: Uses machine-level environment variables per Microsoft best practices
            // https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider
            RegisterSyncfusionLicenseSecure();

            // DEBUG: Wait for debugger attachment to conhost.exe if requested
            if (Environment.GetEnvironmentVariable("WILEY_WIDGET_DEBUG_CONHOST") == "true")
            {
                Console.WriteLine("🔍 DEBUG MODE: Waiting for debugger to attach to conhost.exe...");
                Console.WriteLine("📋 Process Info:");
                Console.WriteLine($"   Process ID: {Process.GetCurrentProcess().Id}");
                Console.WriteLine($"   Process Name: {Process.GetCurrentProcess().ProcessName}");
                Console.WriteLine($"   Main Module: {Process.GetCurrentProcess().MainModule?.FileName}");
                Console.WriteLine("💡 In Visual Studio: Debug → Attach to Process → Select conhost.exe");
                Console.WriteLine("   Or use: dotnet run --project WileyWidget.csproj --debug-conhost");
                Console.WriteLine("🔴 Press ENTER to continue or attach debugger now...");
                Console.ReadLine();
            }

            Console.WriteLine("Starting Wiley Widget application...");

            // Initialize bootstrapper
            _bootstrapper = new ApplicationBootstrapper();

            Console.WriteLine($"Application constructor completed in {startupTimer.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL ERROR in App constructor: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Application startup event handler.
    /// Uses the ApplicationInitializationService to handle the startup sequence.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        var startupTimer = Stopwatch.StartNew();

        try
        {
            Console.WriteLine("🚀 === Core Startup Sequence Started ===");

            // Load configuration first
            var configuration = LoadConfiguration();

            // Initialize the application using the service from DI
            _initializationService = _bootstrapper.ServiceProvider.GetService<ApplicationInitializationService>();
            if (_initializationService == null)
            {
                throw new InvalidOperationException("ApplicationInitializationService not registered in DI container");
            }
            await _initializationService.InitializeApplicationAsync();

            // Get main window from service provider
            var mainWindow = _bootstrapper.ServiceProvider.GetService<Views.MainWindow>();
            if (mainWindow != null)
            {
                MainWindow = mainWindow;

                // Initialize theme system BEFORE showing main window (Syncfusion requirement)
                var themeService = _bootstrapper.ServiceProvider.GetService<Services.IThemeService>();
                if (themeService != null)
                {
                    await themeService.InitializeAsync();

                    // Apply initial theme from settings
                    var settingsService = _bootstrapper.ServiceProvider.GetService<Services.ISettingsService>();
                    if (settingsService != null)
                    {
                        var currentTheme = settingsService.GetSetting<string>("Theme") ?? "FluentDark";
                        await themeService.ApplyThemeAsync(currentTheme);
                    }
                }

                mainWindow.Show();

                // Add startup performance monitoring
                mainWindow.ContentRendered += (_, _) =>
                {
                    _initializationService.PerformanceService.RecordMainWindowRendered();
                };
            }

            // Complete startup
            startupTimer.Stop();
            Console.WriteLine($"✅ Core Startup Complete - Elapsed: {startupTimer.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 CRITICAL: Core Startup failed - {ex.Message}");

            // Use error handling service from DI for graceful degradation
            var errorHandlingService = _bootstrapper.ServiceProvider.GetService<ErrorHandlingService>();
            if (errorHandlingService != null)
            {
                errorHandlingService.HandleStartupFailure(ex);
            }
            else
            {
                // Fallback if DI is not available
                Console.WriteLine($"Fallback error handling: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Application exit event handler.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Console.WriteLine($"👋 Application exiting with code: {e.ApplicationExitCode}");

            // Dispose of resources
            Dispose();
        }
        catch (Exception ex)
        {
            // Log to console since logging might be disposed
            Console.WriteLine($"Error during application exit: {ex.Message}");
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Loads the application configuration.
    /// </summary>
    private IConfiguration LoadConfiguration()
    {
        try
        {
            // Get the project root directory (where appsettings.json is located)
            var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory));

            var builder = new ConfigurationBuilder()
                .SetBasePath(projectRoot)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            return builder.Build();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load configuration: {ex.Message}");
            // Return empty configuration as fallback
            return new ConfigurationBuilder().Build();
        }
    }

    // =====================================================================================
    // 🔒 OFFICIAL SYNCFUSION WPF 30.2.7 LICENSE REGISTRATION - DO NOT MODIFY
    // =====================================================================================
    //
    // CRITICAL WARNING: This method implements the EXACT approach documented in:
    // https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application
    //
    // DO NOT:
    // - Add custom wrapper methods
    // - Modify the registration order
    // - Add additional validation logic
    // - Use any other licensing approaches
    // - Move this method to another location
    //
    // This method MUST be called in the App constructor BEFORE any Syncfusion controls
    // are initialized. The priority order is mandated by Syncfusion documentation.
    //
    // FENCED AREA - BEGIN
    // =====================================================================================

    /// <summary>
    /// Securely registers Syncfusion license using Microsoft-recommended environment variable approach.
    /// This method follows security best practices for credential management.
    /// Based on: https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider
    /// </summary>
    private void RegisterSyncfusionLicenseSecure()
    {
        try
        {
            var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            
            Console.WriteLine("🔑 === SYNCFUSION LICENSE REGISTRATION ATTEMPT ===");
            
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                Console.WriteLine("⚠️ No SYNCFUSION_LICENSE_KEY environment variable found");
                
                // Try license file as fallback
                var licenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
                if (File.Exists(licenseFile))
                {
                    licenseKey = File.ReadAllText(licenseFile).Trim();
                    Console.WriteLine($"📄 Found license.key file ({licenseKey.Length} chars)");
                }
                else
                {
                    Console.WriteLine("📄 No license.key file found");
                }
            }
            else
            {
                Console.WriteLine($"✅ Found SYNCFUSION_LICENSE_KEY environment variable ({licenseKey.Length} chars)");
            }
            
            if (!string.IsNullOrWhiteSpace(licenseKey) && licenseKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                // Register the license
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                
                Console.WriteLine($"✅ Syncfusion license registration completed");
                Console.WriteLine($"   Key Length: {licenseKey.Length} characters");
                Console.WriteLine($"   Key Prefix: {licenseKey.Substring(0, Math.Min(10, licenseKey.Length))}...");
                
                // Test: Add structured log after registration
                Log.Information("🔑 Syncfusion license registration attempted - Key Length: {KeyLength}, Valid Format: {ValidFormat}", 
                    licenseKey.Length, 
                    licenseKey.Length > 50 && !licenseKey.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE"));
            }
            else
            {
                Console.WriteLine("⚠️ No valid license key found - application will run in trial mode");
                Console.WriteLine("💡 Expected: SYNCFUSION_LICENSE_KEY environment variable or license.key file");
                
                Log.Warning("⚠️ No valid Syncfusion license found - application will run in trial mode");
            }
            
            Console.WriteLine("🔑 === END LICENSE REGISTRATION ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Critical error during license registration: {ex.Message}");
            Log.Error(ex, "❌ Failed to register Syncfusion license");
            // Don't throw - allow application to continue in trial mode
        }
    }

    // =====================================================================================
    // 🔒 OFFICIAL SYNCFUSION WPF 30.2.7 LICENSE REGISTRATION - END OF FENCED AREA
    // =====================================================================================
    //
    // DO NOT ADD ANY CODE BETWEEN THE FENCED AREA MARKERS
    // DO NOT MODIFY THE METHOD ABOVE
    // DO NOT ADD ADDITIONAL LICENSING METHODS
    // =====================================================================================

    /// <summary>
    /// Disposes of application resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                (_bootstrapper?.ServiceProvider as IDisposable)?.Dispose();
                _initializationService?.Dispose();
                _bootstrapper = null;
                _initializationService = null;
            }
            _disposed = true;
        }
    }
}
