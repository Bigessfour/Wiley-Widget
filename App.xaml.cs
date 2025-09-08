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
    
    // Track successful Syncfusion license registration for verification
    internal static bool SyncfusionLicenseRegistered { get; private set; } = false;

    /// <summary>
    /// WPF application constructor - Microsoft best practice compliant.
    /// Follows Microsoft guidelines for minimal constructor operations.
    /// </summary>
    public App()
    {
        // 🔑 CRITICAL: Load .env file FIRST to ensure SYNCFUSION_LICENSE_KEY is available
        // BEFORE attempting license registration
        LoadDotEnvIfPresent();

        // 🔑 CRITICAL: SYNCFUSION LICENSE MUST BE REGISTERED AFTER .env loading
        // NO SYNCFUSION CONTROLS CAN BE TOUCHED BEFORE THIS POINT
        // Official Syncfusion WPF licensing approach: https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application

        // Register Syncfusion license key (official pattern)
        // Check all environment scopes: Process → User → Machine
        var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ?? // Process scope
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User) ?? // User scope
                        Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine); // Machine scope

        if (!string.IsNullOrWhiteSpace(licenseKey) &&
            !licenseKey.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
        {
            // Official Syncfusion registration call
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey.Trim());
            SyncfusionLicenseRegistered = true;
            Console.WriteLine($"🔑 ✅ SYNCFUSION LICENSE REGISTERED (len={licenseKey.Length})");
        }
        else
        {
            Console.WriteLine("⚠️ SYNCFUSION LICENSE NOT REGISTERED - Running in trial mode");
        }

        // Essential timing for performance monitoring only
        var startupTimer = Stopwatch.StartNew();

        try
        {
            // Load .env (process scope) - MOVED TO CONSTRUCTOR START for license key availability
            // LoadDotEnvIfPresent(); // REMOVED - now called at constructor start

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
            
            // Initialize ServiceLocator with the service provider for legacy compatibility
            WileyWidget.Configuration.ServiceLocator.Initialize(_bootstrapper.ServiceProvider);

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

            // Initialize the bootstrapper first
            await _bootstrapper.InitializeAsync();

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

                // Post-show verification of critical global keys
                _initializationService.VerifyPostLoadResources();

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

    /// <summary>
    /// Disposes of application resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Loads a .env file from project root (one level above bin output) and sets variables in Process scope.
    /// Does NOT overwrite existing environment variables to respect explicit configuration.
    /// SPECIAL CASE: SYNCFUSION_LICENSE_KEY is loaded from .env if not set in machine/user scope.
    /// </summary>
    private void LoadDotEnvIfPresent()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory; // bin/Debug/netX
            var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(baseDir));
            if (string.IsNullOrEmpty(projectRoot)) return;
            var envPath = Path.Combine(projectRoot, ".env");
            if (!File.Exists(envPath))
            {
                Console.WriteLine("ℹ️ .env file not found (optional) – continuing");
                return;
            }

            Console.WriteLine("📄 Loading .env variables (process scope)...");
            int loaded = 0; int skipped = 0;
            foreach (var rawLine in File.ReadAllLines(envPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                if (string.IsNullOrEmpty(key)) continue;

                // SPECIAL HANDLING: Allow SYNCFUSION_LICENSE_KEY from .env if not set in machine/user scope
                if (key.Equals("SYNCFUSION_LICENSE_KEY", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's already set in machine or user scope
                    var existingKey = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine) ??
                                     Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);

                    if (!string.IsNullOrWhiteSpace(existingKey))
                    {
                        skipped++;
                        Console.WriteLine($"🔐 SYNCFUSION_LICENSE_KEY already set in machine/user scope (Azure Key Vault)");
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"📄 Loading SYNCFUSION_LICENSE_KEY from .env file");
                        // Allow loading from .env if not set elsewhere
                    }
                }

                // Do not overwrite existing (machine/user) environment variable
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key))) { skipped++; continue; }
                Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
                loaded++;
            }
            Console.WriteLine($"📄 .env variable load complete: {loaded} added, {skipped} skipped (pre-existing/excluded)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ .env load failed: {ex.Message}");
        }
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
