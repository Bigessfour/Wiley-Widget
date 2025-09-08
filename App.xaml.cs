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
        // 🔑 CRITICAL: SYNCFUSION LICENSE MUST BE REGISTERED FIRST
        // NO SYNCFUSION CONTROLS CAN BE TOUCHED BEFORE THIS POINT
        // Based on: https://help.syncfusion.com/wpf/licensing/how-to-register-in-an-application
        InitializeAndRegisterSyncfusionLicense();
        
        // Essential timing for performance monitoring only
        var startupTimer = Stopwatch.StartNew();

        try
        {
            // Load .env (process scope) AFTER license registration - EXCLUDES SYNCFUSION_LICENSE_KEY
            LoadDotEnvIfPresent();

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
    /// Acquires Syncfusion license key from Azure Key Vault (bypassing environment variables), validates, logs, and registers.
    /// No hardcoded key. Fails fast (throws) if key missing or invalid to avoid silent trial mode.
    /// Uses Azure CLI to get key from Key Vault directly to avoid confusion with GitHub tokens.
    /// </summary>
    private void InitializeAndRegisterSyncfusionLicense()
    {
        const string vaultName = "wiley-widget-secrets";
        const string secretName = "SYNCFUSION-LICENSE-KEY";
        
        string selected = null;

        // Try Azure Key Vault first (preferred method)
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "az",
                Arguments = $"keyvault secret show --vault-name {vaultName} --name {secretName} --query value -o tsv",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    selected = output.Trim();
                    Log.Information("[SyncfusionLicense] Retrieved from Azure Key Vault: {VaultName}/{SecretName}", vaultName, secretName);
                }
                else
                {
                    Log.Warning("[SyncfusionLicense] Azure Key Vault failed: {Error}", error);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SyncfusionLicense] Azure Key Vault access failed");
        }

        // Fallback to environment variables (process → user → machine) if Azure Key Vault failed
        if (string.IsNullOrWhiteSpace(selected))
        {
            const string envName = "SYNCFUSION_LICENSE_KEY";
            string processVal = Environment.GetEnvironmentVariable(envName, EnvironmentVariableTarget.Process);
            string userVal = Environment.GetEnvironmentVariable(envName, EnvironmentVariableTarget.User);
            string machineVal = Environment.GetEnvironmentVariable(envName, EnvironmentVariableTarget.Machine);

            selected = processVal ?? userVal ?? machineVal;

            // Structured diagnostics for fallback
            Log.Information("[SyncfusionLicense] Fallback Discovery: processLen={ProcessLen}, userLen={UserLen}, machineLen={MachineLen}",
                processVal?.Length, userVal?.Length, machineVal?.Length);
        }

        if (string.IsNullOrWhiteSpace(selected))
        {
            var msg = "Syncfusion license key not found in any environment scope (Process/User/Machine).";
            Log.Error("[SyncfusionLicense] MISSING: {Message}", msg);
            Console.WriteLine($"❌ {msg} Application will NOT continue.");
            throw new InvalidOperationException(msg);
        }

        if (selected.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE", StringComparison.OrdinalIgnoreCase))
        {
            var msg = "Placeholder Syncfusion license key detected. Aborting startup.";
            Log.Error("[SyncfusionLicense] PLACEHOLDER: {Message}", msg);
            throw new InvalidOperationException(msg);
        }

        // Heuristic validation (Syncfusion WPF license keys: base64-like, usually >= 60 chars, contain '@' and '=')
        bool plausible = selected.Length >= 60 && selected.Length <= 140 && selected.Contains('@') && selected.EndsWith('=');
        if (!plausible)
        {
            var warn = $"Syncfusion license key length/pattern unexpected (len={selected.Length}). Proceeding but verify.";
            Log.Warning("[SyncfusionLicense] {Warning}", warn);
            Console.WriteLine("⚠️ " + warn);
        }

        try
        {
            SyncfusionLicenseProvider.RegisterLicense(selected.Trim());
            SyncfusionLicenseRegistered = true; // Mark as successfully registered
            Log.Information("[SyncfusionLicense] Registered (len={Length}, prefix={Prefix})", selected.Length, selected.Substring(0, Math.Min(8, selected.Length)));
            Console.WriteLine($"🔑 ✅ SYNCFUSION LICENSE REGISTERED (len={selected.Length}).");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[SyncfusionLicense] Registration failed");
            Console.WriteLine($"❌ Syncfusion license registration failed: {ex.Message}");
            throw; // fail fast to avoid inconsistent state
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
    /// EXCLUDES SYNCFUSION_LICENSE_KEY to prevent confusion with Azure Key Vault.
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

                // EXCLUDE SYNCFUSION_LICENSE_KEY - managed by Azure Key Vault
                if (key.Equals("SYNCFUSION_LICENSE_KEY", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    Console.WriteLine($"🔐 SYNCFUSION_LICENSE_KEY excluded from .env (using Azure Key Vault)");
                    continue;
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
