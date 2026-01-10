using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Extensions.Logging;
using Syncfusion.Licensing;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.DataGrid;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Net.Http;
using Action = System.Action;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Windows.Forms;
using WileyWidget.Data;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms
{
    internal static class Program
    {
        public static IServiceProvider Services { get; private set; } = null!;
        private static IServiceScope? _applicationScope; // Application-lifetime scope
        private static IStartupTimelineService? _timelineService;

        private const string FallbackConnectionString = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";

        private sealed record SyncfusionLicenseDiagnostics(string Source, bool IsDevelopment, int Length, string Hash);

        private static SyncfusionLicenseDiagnostics? _syncfusionLicenseDiagnostics;

        private static void ConfigureBootstrapLogger()
        {
            try
            {
                var basePath = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                var logsPath = Path.Combine(basePath, "logs");
                Directory.CreateDirectory(logsPath);
                var logFileTemplate = Path.Combine(logsPath, "app-.log");

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.File(logFileTemplate,
                        formatProvider: CultureInfo.InvariantCulture,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        fileSizeLimitBytes: 10 * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        shared: true)
                    .CreateLogger();

                Log.Debug("✓ Bootstrap logger initialized - CENTRALIZED LOGS (path: {LogsPath})", logsPath);
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                        .CreateLogger();

                    Log.Warning(ex, "Bootstrap logger fallback initialized");
                }
                catch
                {
                    // Swallow: if logging cannot be configured at all, keep default silent logger.
                }
            }
        }

        private static void EnsureTimelineService()
        {
            if (_timelineService == null)
            {
                _timelineService = new StartupTimelineService(NullLogger<StartupTimelineService>.Instance);
            }
        }

        private static void RegisterSyncfusionLicense()
        {
            var licenseStopwatch = Stopwatch.StartNew();
            var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? "Production";
            var isDevelopment = string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase);

            // Prefer canonical environment variable first, then configuration keys (top-level SYNCFUSION_LICENSE_KEY or Syncfusion:LicenseKey)
            var licenseKeySource = "";
            string licenseKey = null;

            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            var envKeyAlt = Environment.GetEnvironmentVariable("Syncfusion__LicenseKey");

            if (!string.IsNullOrWhiteSpace(envKey))
            {
                licenseKey = envKey;
                licenseKeySource = "env:SYNCFUSION_LICENSE_KEY";
            }
            else if (!string.IsNullOrWhiteSpace(envKeyAlt))
            {
                licenseKey = envKeyAlt;
                licenseKeySource = "env:Syncfusion__LicenseKey";
            }
            else
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    // Support running from repo root (before files are copied to output)
                    .AddJsonFile(Path.Combine("src", "WileyWidget.WinForms", "appsettings.json"), optional: true, reloadOnChange: false);

                if (isDevelopment)
                {
                    builder.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly(), optional: true);
                }

                builder.AddEnvironmentVariables();
                var cfg = builder.Build();

                // Check top-level key first for consistent custody chain
                var cfgTop = cfg["SYNCFUSION_LICENSE_KEY"];
                var cfgNested = cfg["Syncfusion:LicenseKey"];

                if (!string.IsNullOrWhiteSpace(cfgTop))
                {
                    licenseKey = cfgTop;
                    licenseKeySource = "cfg:SYNCFUSION_LICENSE_KEY";
                }
                else if (!string.IsNullOrWhiteSpace(cfgNested))
                {
                    licenseKey = cfgNested;
                    licenseKeySource = "cfg:Syncfusion:LicenseKey";
                }
            }

            var rawKey = licenseKey;
            licenseKey = licenseKey?.Trim();
            if (!string.IsNullOrEmpty(licenseKey) &&
                ((licenseKey.Length >= 2 && licenseKey[0] == '"' && licenseKey[^1] == '"') ||
                 (licenseKey.Length >= 2 && licenseKey[0] == '\'' && licenseKey[^1] == '\'')))
            {
                licenseKey = licenseKey.Substring(1, licenseKey.Length - 2);
            }

            // If the key was pasted with line breaks/spaces (common when copying from portals), remove whitespace.
            // This keeps the operation simple while preventing the Base64 FormatException you saw.
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                licenseKey = string.Concat(licenseKey.Where(c => !char.IsWhiteSpace(c)));
            }

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                // In Development, allow running without a key (evaluation/trial behavior). In Production, fail fast.
                if (!isDevelopment)
                {
                    throw new InvalidOperationException("Syncfusion license key is missing. Set SYNCFUSION_LICENSE_KEY or Syncfusion:LicenseKey.");
                }

                Log.Warning("Syncfusion license key not found (Development={IsDev}).", isDevelopment);
            }
            else
            {
                _syncfusionLicenseDiagnostics = new SyncfusionLicenseDiagnostics(
                    string.IsNullOrWhiteSpace(licenseKeySource) ? "unknown" : licenseKeySource,
                    isDevelopment,
                    licenseKey.Length,
                    ShortHash(licenseKey));

                try
                {
                    // SECURITY: Only log safe diagnostics (length + hash) - never write full license key to disk
                    Log.Debug("Syncfusion license loaded (source={Source}, len={Length}, hash={Hash})",
                        licenseKeySource, licenseKey.Length, ShortHash(licenseKey));

                    // This is the only required Syncfusion licensing operation. Use fully-qualified method to be explicit.
                    try
                    {
                        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                        Log.Debug("Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense() completed successfully");
                    }
                    catch (Exception regEx)
                    {
                        throw new InvalidOperationException(
                            $"Failed to register Syncfusion license. The license key may be invalid, expired, or not valid for version 32.1.19. Exception: {regEx.Message}",
                            regEx);
                    }
                }
                catch (FormatException ex)
                {
                    // Syncfusion troubleshooting: invalid/quoted/truncated license key can trigger Base64 FormatException.
                    throw new InvalidOperationException(
                        "Syncfusion license key appears invalid (Base-64 parse failed). " +
                        "Verify the key has no surrounding quotes and no extra whitespace/newlines, and that it is complete.",
                        ex);
                }
            }

            licenseStopwatch.Stop();
            if (licenseStopwatch.ElapsedMilliseconds > 750)
            {
                Log.Information("RegisterSyncfusionLicense completed in {Elapsed}ms (monitor for I/O growth)", licenseStopwatch.ElapsedMilliseconds);
            }
            else
            {
                Log.Debug("RegisterSyncfusionLicense completed in {Elapsed}ms", licenseStopwatch.ElapsedMilliseconds);
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            ConfigureBootstrapLogger();
            SetInvariantCulture();

            // Load environment variables early (optional). Guard against malformed .env lines which can throw FormatException.
            TryLoadDotNetEnv();
            RunMain(args);
        }

        private static void RunMain(string[] args)
        {
            Log.Debug("Main method started");

            EnsureTimelineService();
            var verifyStartup = IsVerifyStartup(args);

            // No SynchronizationContext installation - we rely on the STA main thread for all WinForms operations.
            // Background work uses Task.Run() but we wait synchronously when required to preserve STA thread safety.

            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                if (resolveArgs.Name != null && resolveArgs.Name.StartsWith("Microsoft.WinForms.Utilities.Shared", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                return null;
            };

            SplashForm? splash = null;
            try
            {
                using (_timelineService?.BeginPhaseScope("License Registration"))
                {
                    RegisterSyncfusionLicense();
                    Log.Information("Startup milestone: License Registration complete");
                }

                if (_syncfusionLicenseDiagnostics != null)
                {
                    var d = _syncfusionLicenseDiagnostics;
                    if (verifyStartup)
                    {
                        Log.Information(
                            "Syncfusion license key resolved in Main (source={Source}, len={Length}, hash={Hash}, isDev={IsDev})",
                            d.Source,
                            d.Length,
                            d.Hash,
                            d.IsDevelopment);
                    }
                    else
                    {
                        Log.Debug(
                            "Syncfusion license key resolved in Main (source={Source}, len={Length}, hash={Hash}, isDev={IsDev})",
                            d.Source,
                            d.Length,
                            d.Hash,
                            d.IsDevelopment);
                    }
                }
                else if (verifyStartup)
                {
                    Log.Warning("Syncfusion license key was not resolved in Main.");
                }

                using (_timelineService?.BeginPhaseScope("WinForms Initialization"))
                {
                    InitializeWinForms();
                    Log.Information("Startup milestone: WinForms Initialization complete");
                }

                using (_timelineService?.BeginPhaseScope("Theme Initialization"))
                {
                    InitializeTheme();
                    Log.Information("Startup milestone: Theme Initialization complete");
                }

                // Detect WebView2 before showing main form (required for JARVIS Chat)
                using (_timelineService?.BeginPhaseScope("WebView2 Detection"))
                {
                    DetectAndPromptWebView2();
                    Log.Information("Startup milestone: WebView2 Detection complete");
                }

                splash = new SplashForm();
                splash.ShowSplash();

                void SplashReport(double progress, string message, bool isIndeterminate = false)
                {
                    splash?.Report(progress, message, isIndeterminate);
                }

                void SplashComplete(string message)
                {
                    splash?.Complete(message);
                }

                SplashReport(0.02, "Initializing application...", isIndeterminate: true);
                SplashReport(0.05, "Building dependency injection container...", isIndeterminate: true);

                IHost host;
                using (_timelineService?.BeginPhaseScope("DI Container Build"))
                {
                    var hostBuildScope = System.Diagnostics.Stopwatch.StartNew();
                    host = BuildHost(args);
                    hostBuildScope.Stop();
                    Log.Debug("DI Container built in {Elapsed}ms", hostBuildScope.ElapsedMilliseconds);
                }
                Log.Information("Startup milestone: DI Container Build complete");
                SplashReport(0.20, "DI container ready");

                // Get required services early
                var getOrchestratorStopwatch = System.Diagnostics.Stopwatch.StartNew();
                Log.Debug("DIAGNOSTIC: About to call GetRequiredService<IStartupOrchestrator>");
                var startupOrchestrator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStartupOrchestrator>(host.Services);
                getOrchestratorStopwatch.Stop();
                Log.Information("DIAGNOSTIC: GetRequiredService<IStartupOrchestrator> completed in {Elapsed}ms", getOrchestratorStopwatch.ElapsedMilliseconds);

                // CRITICAL: Create application-lifetime scope (do NOT use 'using' - must live until app exits)
                // This scope provides scoped services (DbContext, repositories) for the entire UI lifetime
                _applicationScope = host.Services.CreateScope();
                Services = _applicationScope.ServiceProvider;

                _timelineService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IStartupTimelineService>(Services) ?? _timelineService;
                if (_timelineService != null && _timelineService.IsEnabled)
                {
                    Log.Debug("[TIMELINE] StartupTimelineService enabled - tracking startup phases");
                }

                SplashReport(0.30, "Validating configuration...", isIndeterminate: true);
                using (_timelineService?.BeginPhaseScope("Secret Validation"))
                {
                    ValidateSecrets(host.Services);
                }
                Log.Debug("Secret validation complete");

                // Microsoft guidance: Defer DI validation to background - it's diagnostic, not functional
                // "ValidateOnBuild is false by default in non-Development modes for performance reasons"
                // Move validation off UI thread to prevent 985ms startup blocking
#if DEBUG
                _ = Task.Run(async () =>
                {
                    try
                    {
                        Log.Debug("Starting background DI validation (Development mode only)");
                        await startupOrchestrator.ValidateServicesAsync(Services, CancellationToken.None).ConfigureAwait(false);
                        Log.Information("Background DI validation complete - all services valid");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Background DI validation failed - check service registrations");
                    }
                });
#else
                Log.Debug("DI validation skipped (Release mode - validates at build time via ValidateOnBuild)");
#endif
                SplashReport(0.50, "Validation complete");

                // Verification mode: run deeper checks before exiting
                if (verifyStartup)
                {
                    Log.Information("Startup verification mode active; running verification and exiting.");

                    SplashReport(0.70, "Verifying database connectivity...", isIndeterminate: true);
                    using (var healthScope = host.Services.CreateScope())
                    {
                        using (_timelineService?.BeginPhaseScope("Database Health Check"))
                        {
                            SplashReport(0.80, "Checking database health...", isIndeterminate: true);
                            Task.Run(async () => await RunStartupHealthCheckAsync(healthScope.ServiceProvider).ConfigureAwait(false)).GetAwaiter().GetResult();
                            SplashReport(0.90, "Database verified");
                        }
                    }

                    SplashComplete("Startup verification complete");
                    RunVerifyStartup(host).GetAwaiter().GetResult();
                    return;
                }

                // Create MainForm early - this is the most visible milestone
                SplashReport(0.60, "Initializing main window...", isIndeterminate: true);
                MainForm mainForm;
                try
                {
                    using (_timelineService?.BeginPhaseScope("MainForm Creation"))
                    {
                        mainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MainForm>(Services);
                        Log.Debug("MainForm instance created");
                    }
                    Log.Information("Startup milestone: MainForm Creation complete");
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Failed to create MainForm - likely Syncfusion license or control instantiation issue");
                    throw;
                }
                SplashReport(0.70, "Main window created");

                using (_timelineService?.BeginPhaseScope("Chrome Initialization"))
                {
                    _timelineService?.RecordOperation("Configure error reporting", "Chrome Initialization");
                    ConfigureErrorReporting();
                    WireGlobalExceptionHandlers();
                }
                Log.Debug("Exception handlers wired");
                Log.Information("Startup milestone: Chrome Initialization complete");

                SplashReport(0.90, "Finalizing startup...");
                SplashReport(0.95, "Launching application...");
                using (_timelineService?.BeginPhaseScope("Splash Screen Hide"))
                {
                    SplashComplete("Ready");
                    Log.Information("Splash screen hidden");
                }
                Log.Information("Startup milestone: Ready");

                // Generate comprehensive startup timeline report with dependency validation
                Log.Debug("[DIAGNOSTIC] About to call GenerateStartupReport()");
                startupOrchestrator.GenerateStartupReport();
                Log.Debug("[DIAGNOSTIC] GenerateStartupReport() completed");

                Log.Debug("[DIAGNOSTIC] About to call ScheduleAutoCloseIfRequested()");
                ScheduleAutoCloseIfRequested(args, mainForm);
                Log.Debug("[DIAGNOSTIC] ScheduleAutoCloseIfRequested() completed");

                Log.Debug("[DIAGNOSTIC] About to enter UI message loop");
                Log.Debug(
                    """
                    [DIAGNOSTIC] ABOUT TO ENTER UI MESSAGE LOOP
                    Timestamp: { Timestamp}
                    MainFormPresent: { Present}
                    IsDisposed: { Disposed}
                    """,
                    DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    mainForm != null,
                    mainForm?.IsDisposed);
                Log.Debug("Entering UI message loop");
                using (_timelineService?.BeginPhaseScope("UI Message Loop"))
                {
                    if (mainForm == null)
                    {
                        Log.Fatal("MainForm is null. Cannot start UI message loop.");
                        throw new ArgumentNullException(nameof(args), "MainForm cannot be null when starting the UI message loop.");
                    }
                    RunUiLoop(mainForm);
                }
                Log.Debug("UI message loop exited");
            }
            catch (NullReferenceException nreEx)
            {
                Log.Fatal(nreEx, "═══ NULLREFERENCEEXCEPTION DURING STARTUP ═══\n" +
                    "Exception Type: {ExceptionType}\n" +
                    "Message: {Message}\n" +
                    "StackTrace:\n{StackTrace}\n" +
                    "Source: {Source}\n" +
                    "TargetSite: {TargetSite}\n" +
                    "HResult: {HResult}",
                    nreEx.GetType().FullName,
                    nreEx.Message,
                    nreEx.StackTrace ?? "(no stack trace)",
                    nreEx.Source ?? "(unknown)",
                    nreEx.TargetSite?.ToString() ?? "(unknown)",
                    nreEx.HResult);

                HandleStartupFailure(nreEx);
                throw;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "═══ UNHANDLED EXCEPTION DURING STARTUP ═══\n" +
                    "Exception Type: {ExceptionType}\n" +
                    "Message: {Message}\n" +
                    "StackTrace:\n{StackTrace}\n" +
                    "Source: {Source}\n" +
                    "TargetSite: {TargetSite}\n" +
                    "HResult: {HResult}\n" +
                    "InnerException: {InnerException}",
                    ex.GetType().FullName,
                    ex.Message,
                    ex.StackTrace ?? "(no stack trace)",
                    ex.Source ?? "(unknown)",
                    ex.TargetSite?.ToString() ?? "(unknown)",
                    ex.HResult,
                    ex.InnerException?.ToString() ?? "(none)");

                HandleStartupFailure(ex);
                throw;
            }
            finally
            {
                splash?.Dispose();
            }
        }

        private static void SetInvariantCulture()
        {
            try
            {
                var invariant = CultureInfo.InvariantCulture;
                CultureInfo.DefaultThreadCurrentCulture = invariant;
                CultureInfo.DefaultThreadCurrentUICulture = invariant;
                Thread.CurrentThread.CurrentCulture = invariant;
                Thread.CurrentThread.CurrentUICulture = invariant;
                Log.Debug("Default culture set to InvariantCulture for deterministic parsing");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to set invariant culture; continuing with system defaults");
            }
        }

        private static bool IsRunningInTestEnvironment()
        {
            // Check for test environment indicators
            return Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS") == "true" ||
                   AppDomain.CurrentDomain.GetAssemblies()
                       .Any(asm => asm.FullName?.Contains("test", StringComparison.OrdinalIgnoreCase) == true ||
                                   asm.FullName?.Contains("xunit", StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Initialize the Syncfusion theme system at application startup.
        /// AUTHORITATIVE SOURCE: This is the ONLY location where theme should be set during startup.
        /// Uses SkinManager.LoadAssembly and SkinManager.ApplicationVisualTheme (CORRECT API).
        /// Child forms automatically inherit this theme - do NOT call SetVisualStyle in form constructors.
        /// Reference: https://help.syncfusion.com/windowsforms/skins/getting-started
        /// </summary>
        private static void InitializeTheme()
        {
            try
            {
                Log.Debug("Starting theme initialization");

                // Null guard for timeline service
                if (_timelineService != null)
                {
                    _timelineService.RecordOperation("Load Office2019Theme assembly", "Theme Initialization");
                }

                // CRITICAL: Load theme assembly FIRST before setting ApplicationVisualTheme
                // Reference: Syncfusion WinForms Skins documentation
                try
                {
                    SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                    Log.Debug("Office2019Theme assembly loaded successfully");
                }
                catch (Exception loadEx)
                {

                    Log.Error(loadEx, "Failed to load Office2019Theme assembly");
                    throw; // Rethrow to outer catch for comprehensive handling
                }

                // Apply global theme - use default from ThemeColors (fallback to Office2019Colorful)
                var themeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                Log.Debug("Setting ApplicationVisualTheme to: {ThemeName}", themeName);

                if (_timelineService != null)
                {
                    _timelineService.RecordOperation($"Set ApplicationVisualTheme: {themeName}", "Theme Initialization");
                }

                // CRITICAL: Set ApplicationVisualTheme AFTER assembly load and BEFORE any form creation
                // This must be set after Application.EnableVisualStyles() for proper rendering
                try
                {
                    SfSkinManager.ApplicationVisualTheme = themeName;  // Global application-wide theme
                    Log.Debug("ApplicationVisualTheme set to: {ThemeName}", themeName);
                }
                catch (Exception setEx)
                {

                    Log.Error(setEx, "Failed to set ApplicationVisualTheme to {ThemeName}", themeName);
                    throw; // Rethrow to outer catch for comprehensive handling
                }

                Log.Debug("Theme initialization completed successfully");
            }
            catch (Exception ex)
            {
                // COMPREHENSIVE ERROR LOGGING

                if (ex.InnerException != null)
                {
                    Log.Error(ex.InnerException, "[THEME FATAL] InnerException during theme initialization");
                }

                Log.Error(ex, """
[THEME] Theme initialization failed; continuing with default Windows theme(no Syncfusion theming)
""");
            }
        }

        private static void ConfigureErrorReporting()
        {
            // Configure error reporting service if needed
        }

        private static void TryLoadDotNetEnv()
        {
            // Microsoft guidance: production secrets should come from secure stores (env vars, Key Vault, etc.).
            // DotNetEnv-style files are dev conveniences; keep them Development-only.
            var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? "Production";

            if (!string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var envStopwatch = Stopwatch.StartNew();

            // Load optional secrets file first (repo-root relative), then traverse for a .env.
            // Treat parsing errors (FormatException) as non-fatal so startup can continue.
            try
            {
                var secretsPath = ResolveSecretsPath();
                if (File.Exists(secretsPath))
                {
                    DotNetEnv.Env.Load(secretsPath);
                }
            }
            catch (Exception ex)
            {
                // Failsafe: If logging fails (e.g., Serilog not initialized), silently continue
                // This is early startup - logging infrastructure may not be ready
                try { Log.Warning(ex, "Failed to load optional secrets file via DotNetEnv"); } catch { /* Intentionally empty */ }
            }

            try
            {
                DotNetEnv.Env.TraversePath().Load();
            }
            catch (Exception ex)
            {
                // Failsafe: If logging fails (e.g., Serilog not initialized), silently continue
                // This is early startup - logging infrastructure may not be ready
                try { Log.Warning(ex, "Failed to load .env via DotNetEnv"); } catch { /* Intentionally empty */ }
            }

            envStopwatch.Stop();
            if (envStopwatch.ElapsedMilliseconds > 500)
            {
                Log.Information("DotNetEnv load completed in {Elapsed}ms (consider async if this grows)", envStopwatch.ElapsedMilliseconds);
            }
            else
            {
                Log.Debug("DotNetEnv load completed in {Elapsed}ms", envStopwatch.ElapsedMilliseconds);
            }
        }

        private static string ResolveSecretsPath()
        {
            var overridePath = Environment.GetEnvironmentVariable("WW_SECRETS_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                return overridePath;
            }

            return Path.Combine(Directory.GetCurrentDirectory(), "secrets", "my.secrets");
        }

        private static string ShortHash(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret)) return string.Empty;
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(secret);
            var hash = sha.ComputeHash(bytes);
            var hex = BitConverter.ToString(hash).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
            return hex.Substring(0, Math.Min(8, hex.Length));
        }

        /// <summary>
        /// Initialize Windows Forms application settings and high-DPI support.
        /// </summary>
        private static void InitializeWinForms()
        {
            // Order matters: DPI mode -> visual styles -> text rendering
            // Use PerMonitorV2 for best high-DPI support (Windows 10 1703+)
            // This matches app.manifest dpiAwareness setting
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Set default font for all new controls
            try
            {
                Application.SetDefaultFont(WileyWidget.WinForms.Services.FontService.Instance.CurrentFont);
                Log.Debug("Default font set successfully: {FontName} {FontSize}pt",
                    WileyWidget.WinForms.Services.FontService.Instance.CurrentFont.Name,
                    WileyWidget.WinForms.Services.FontService.Instance.CurrentFont.Size);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set default font");
            }
        }

        /// <summary>
        /// Detects WebView2 runtime availability and displays a prompt if missing.
        /// Required for Blazor Hybrid JARVIS Chat component.
        /// Runs synchronously during startup to allow graceful fallback.
        /// </summary>
        /// <remarks>
        /// WebView2 runtime is bundled with Windows 11+, but may be missing on Windows 10.
        /// If missing, user is prompted to download from Microsoft's official URL.
        /// </remarks>
        private static void DetectAndPromptWebView2()
        {
            try
            {
                Log.Debug("[WEBVIEW2] Checking WebView2 runtime availability...");

                // Synchronous check - uses Win32 registry (fast)
                var versionString = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    Log.Information("[WEBVIEW2] WebView2 runtime detected: version {Version}", versionString);
                    return; // WebView2 available - proceed
                }

                Log.Warning("[WEBVIEW2] WebView2 runtime not found. User will see download prompt.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[WEBVIEW2] Error detecting WebView2 runtime (Blazor features may be unavailable)");
            }

            // If we reach here, WebView2 is missing - show user-friendly prompt
            PromptWebView2Download();
        }

        /// <summary>
        /// Shows a message box prompting user to download and install WebView2 runtime.
        /// </summary>
        private static void PromptWebView2Download()
        {
            try
            {
                const string downloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
                var message = "WebView2 Runtime is required for JARVIS Chat and other advanced features.\n\n" +
                    "Would you like to open the download page?\n\n" +
                    "You can also manually download from:\n" +
                    "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

                var result = MessageBox.Show(
                    message,
                    "WebView2 Runtime Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = downloadUrl,
                            UseShellExecute = true
                        });
                        Log.Information("[WEBVIEW2] User initiated WebView2 download");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[WEBVIEW2] Failed to open download URL");
                        MessageBox.Show(
                            $"Failed to open browser. Please visit the URL manually:",
                            "Browser Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[WEBVIEW2] Unexpected error in WebView2 prompt");
            }
        }

        private static IHost BuildHost(string[] args)
        {
            var reportViewerLaunchOptions = CreateReportViewerLaunchOptions(args);
            var builder = Host.CreateApplicationBuilder(args);

            if (_timelineService != null)
            {
                builder.Services.AddSingleton(typeof(IStartupTimelineService), _timelineService);
            }

            AddConfiguration(builder);
            ConfigureLogging(builder);
            ConfigureDatabase(builder);
            ConfigureHealthChecks(builder);
            CaptureDiFirstChanceExceptions();
            AddDependencyInjection(builder);
            ConfigureUiServices(builder);

            builder.Services.AddSingleton(reportViewerLaunchOptions);

            // DEBUG: Check config BEFORE Build()
            var preBuildXai = builder.Configuration["XAI:ApiKey"];
            Log.Debug("[PRE-BUILD] XAI:ApiKey present={Present}, length={Length}", preBuildXai != null, preBuildXai?.Length ?? 0);

            // Register a global HttpClient with a sensible default timeout to avoid blocking external calls during startup
            try
            {
                var httpTimeoutSeconds = builder.Configuration.GetValue<int>("HttpClient:TimeoutSeconds", 30);
                // Register a named default HttpClient with configured timeout
                builder.Services.AddHttpClient("WileyWidgetDefault", c => c.Timeout = TimeSpan.FromSeconds(httpTimeoutSeconds));
                Log.Debug("[CONFIG] Registered global (named) HttpClient 'WileyWidgetDefault' with {Timeout}s timeout", httpTimeoutSeconds);
            }
            catch (Exception httpRegEx)
            {
                Log.Warning(httpRegEx, "Failed to register global HttpClient");
            }

            return builder.Build();
        }

        private static void AddConfiguration(HostApplicationBuilder builder)
        {
            // Provider order per Microsoft guidance (lowest -> highest):
            // JSON files -> (Development) user secrets -> environment variables.
            // (Command line args are already handled by Host.CreateApplicationBuilder(args).)

            // DotNetEnv-style files are a development convenience; keep them Development-only.
            if (builder.Environment.IsDevelopment())
            {
                TryLoadDotNetEnv();

                // User secrets are intended for development-time only and are only loaded in Development.
                builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly(), optional: true);
            }

            // Load primary appsettings.json from project directory
            try
            {
                builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load appsettings.json");
            }

            // CRITICAL: Also load config/development/appsettings.json which has the full configuration
            try
            {
                var devConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "development", "appsettings.json");
                if (File.Exists(devConfigPath))
                {
                    builder.Configuration.AddJsonFile(devConfigPath, optional: false, reloadOnChange: true);
                    Log.Debug("[CONFIG] Loaded development config from: {DevConfigPath}", devConfigPath);
                }
                else
                {
                    Log.Warning("Development config not found at: {DevConfigPath}", devConfigPath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load config/development/appsettings.json");
            }

            builder.Configuration.AddEnvironmentVariables();

            // CRITICAL: Expand environment variable placeholders from appsettings.json
            // appsettings.json uses ${VAR_NAME} syntax which .NET doesn't expand automatically
            // Read environment variables and override config values that have ${...} placeholders
            try
            {
                var xaiApiKeyEnv = Environment.GetEnvironmentVariable("XAI_API_KEY");
                Log.Debug("[ENV VAR DEBUG] XAI_API_KEY present={Present}, length={Length}", xaiApiKeyEnv != null, xaiApiKeyEnv?.Length ?? 0);

                var openAiKeyEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                var syncfusionKeyEnv = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                var qboClientIdEnv = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
                var qboClientSecretEnv = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");

                var overrides = new Dictionary<string, string?>();

                if (!string.IsNullOrWhiteSpace(xaiApiKeyEnv))
                {
                    overrides["XAI:ApiKey"] = xaiApiKeyEnv;
                    overrides["Grok:ApiKey"] = xaiApiKeyEnv;  // Also set Grok:ApiKey for GrokAgentService
                    Log.Debug("[CONFIG] Overriding XAI:ApiKey and Grok:ApiKey from environment variable (length: {Length})", xaiApiKeyEnv.Length);
                }
                else
                {
                    Log.Warning("XAI_API_KEY environment variable is NULL or empty!");
                }

                if (!string.IsNullOrWhiteSpace(openAiKeyEnv))
                {
                    overrides["OpenAI:ApiKey"] = openAiKeyEnv;
                }

                if (!string.IsNullOrWhiteSpace(syncfusionKeyEnv))
                {
                    overrides["Syncfusion:LicenseKey"] = syncfusionKeyEnv;
                }

                if (!string.IsNullOrWhiteSpace(qboClientIdEnv))
                {
                    overrides["QuickBooks:ClientId"] = qboClientIdEnv;
                }

                if (!string.IsNullOrWhiteSpace(qboClientSecretEnv))
                {
                    overrides["QuickBooks:ClientSecret"] = qboClientSecretEnv;
                }

                if (overrides.Count > 0)
                {
                    builder.Configuration.AddInMemoryCollection(overrides);
                    Log.Debug("[CONFIG] Added {Count} configuration overrides via AddInMemoryCollection", overrides.Count);

                    // Verify the override was applied
                    var verifyXai = builder.Configuration["XAI:ApiKey"];
                    Log.Debug("[CONFIG VERIFY] After AddInMemoryCollection: XAI:ApiKey present={Present}, length={Length}", verifyXai != null, verifyXai?.Length ?? 0);
                }
                else
                {
                    Log.Debug("No configuration overrides to add");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error expanding environment variable placeholders");
            }

            try
            {
                var existingConn = builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(existingConn))
                {
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = FallbackConnectionString
                    });
                    Log.Warning("DefaultConnection not found; using development fallback connection string");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error ensuring default connection in configuration");
            }
        }

        private static void ConfigureLogging(HostApplicationBuilder builder)
        {
            try
            {
                // Make Serilog self-logging forward internal errors to a simple debug output
                // CRITICAL: Do NOT use Log.Warning() or any Serilog method inside SelfLog callback
                // This creates infinite recursion if logging encounters any error.
                // Instead, use Debug.WriteLine() which is primitive and won't trigger Serilog.
                // NOTE: ObjectDisposedException here is expected during shutdown when file sinks
                // are closing while async operations complete. Suppress these in output.
                Serilog.Debugging.SelfLog.Enable(msg =>
                {
                    try
                    {
                        // Suppress disposal exceptions - they're expected during graceful shutdown
                        // and are handled properly by the shutdown sequence in RunUiLoop
                        if (!msg.Contains("disposed", StringComparison.OrdinalIgnoreCase) &&
                            !msg.Contains("Cannot access a disposed object", StringComparison.OrdinalIgnoreCase))
                        {
                            System.Diagnostics.Debug.WriteLine($"[SERILOG INTERNAL] {msg}");
                        }
                    }
                    catch
                    {
                        // If even Debug.WriteLine fails, silently swallow to avoid cascading errors
                    }
                });

                // CRITICAL: ALL LOGS go to project root src/logs directory
                var projectRoot = Directory.GetCurrentDirectory();
                var logsPath = Path.Combine(projectRoot, "logs");

                // Always use root logs folder for centralized logging
                Log.Debug("Creating logs directory at: {LogsPath}", logsPath);
                Directory.CreateDirectory(logsPath);

                // Template used by Serilog's rolling file sink (daily rolling uses a date suffix)
                var logFileTemplate = Path.Combine(logsPath, "app-.log");
                Log.Debug("Log file pattern: {LogFileTemplate}", logFileTemplate);

                // Resolve the current daily log file that Serilog will write to for today's date
                // Serilog's daily rolling file uses the yyyyMMdd date format (e.g., app-20251215.log)
                var logFileCurrent = Path.Combine(logsPath, $"app-{DateTime.Now:yyyyMMdd}.log");
                Log.Debug("Current daily log file: {LogFileCurrent}", logFileCurrent);

                // Check for SQL logging override environment variable
                var enableSqlLogging = Environment.GetEnvironmentVariable("WILEYWIDGET_LOG_SQL");
                var sqlLogLevel = string.Equals(enableSqlLogging, "true", StringComparison.OrdinalIgnoreCase)
                    ? Serilog.Events.LogEventLevel.Information
                    : Serilog.Events.LogEventLevel.Warning;

                Log.Debug("SQL logging level: {SqlLogLevel} (WILEYWIDGET_LOG_SQL={EnableSqlLogging})", sqlLogLevel, enableSqlLogging ?? "not set");

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(builder.Configuration)
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.File(logFileTemplate, formatProvider: CultureInfo.InvariantCulture, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30, fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true, shared: true)
                    .Enrich.FromLogContext()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", sqlLogLevel)
                    .CreateLogger();

                Log.Debug("Logging configured successfully");
                // Log both the resolved current file and the template used by the rolling sink so it's clear
                Log.Debug("Logging system initialized - writing to {LogPath} (pattern: {LogPattern})", logFileCurrent, logFileTemplate);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "CRITICAL: Failed to configure logging");

                // Fallback to console-only logging
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
                    .WriteTo.Debug(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
                    .MinimumLevel.Information()
                    .CreateLogger();

                Log.Warning("Logging fallback to console-only mode");
            }
        }

        private static void ConfigureDatabase(HostApplicationBuilder builder)
        {
            void ConfigureSqlOptions(DbContextOptionsBuilder options)
            {
                // CRITICAL: Only use in-memory database when explicitly running UI tests via environment variable.
                // Production runs should ALWAYS use SQL Server connection.
                var isUiTestRun = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

                // DO NOT use configuration UseInMemoryForTests - removed to prevent accidental in-memory usage
                // DO NOT check WILEYWIDGET_USE_INMEMORY - this should only be set during actual test execution

                if (isUiTestRun)
                {
                    options.UseInMemoryDatabase("WileyWidgetUiTests");
                    Log.Debug("Using InMemory database for UI tests (WILEYWIDGET_UI_TESTS=true)");
                    return;
                }

                // PRODUCTION PATH: Use SQL Server connection string
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;";
                    Log.Warning("DefaultConnection missing; using fallback SQL Server connection string");
                }

                connectionString = Environment.ExpandEnvironmentVariables(connectionString);

                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly("WileyWidget.Data");
                    sql.CommandTimeout(builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 60));
                    sql.EnableRetryOnFailure(
                        maxRetryCount: builder.Configuration.GetValue("Database:MaxRetryCount", 3),
                        maxRetryDelay: TimeSpan.FromSeconds(builder.Configuration.GetValue("Database:MaxRetryDelaySeconds", 10)),
                        errorNumbersToAdd: null);
                });

                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging(builder.Configuration.GetValue("Database:EnableSensitiveDataLogging", false));

                options.UseLoggerFactory(new SerilogLoggerFactory(Log.Logger));

                Log.Debug("Using SQL Server database: {Database}", connectionString.Split(';').FirstOrDefault(s => s.Contains("Database", StringComparison.OrdinalIgnoreCase)) ?? "WileyWidgetDev");
            }

            // CRITICAL: Use Scoped lifetime for DbContextFactory (NOT Singleton)
            // Reason: DbContextOptions internally resolves IDbContextOptionsConfiguration which is scoped.
            // Using Singleton would cause "Cannot resolve scoped service from root provider" errors.
            // EF Core best practice: Factory should be Scoped, DbContext is implicitly Scoped.
            builder.Services.AddDbContextFactory<AppDbContext>(ConfigureSqlOptions, ServiceLifetime.Scoped);
            builder.Services.AddDbContext<AppDbContext>(ConfigureSqlOptions, ServiceLifetime.Transient);
        }

        private static void ConfigureHealthChecks(HostApplicationBuilder builder)
        {
            try
            {
                var healthChecksSection = builder.Configuration.GetSection("HealthChecks");
                var healthConfig = healthChecksSection.Get<HealthCheckConfiguration>() ?? new HealthCheckConfiguration();
                builder.Services.AddSingleton(healthConfig);

                // Register health checks
                builder.Services.AddHealthChecks()
                    .AddCheck<WileyWidget.Services.HealthChecks.SyncfusionLicenseHealthCheck>(
                        "syncfusion_license",
                        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                        tags: new[] { "license", "startup" });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to configure HealthCheckConfiguration from appsettings - using default configuration");
                builder.Services.AddSingleton(new HealthCheckConfiguration());
            }
        }

        private static void CaptureDiFirstChanceExceptions()
        {
            AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
            {
                var ex = eventArgs.Exception;
                if ((ex is InvalidOperationException || ex is AggregateException) &&
                    ex.Source != null && ex.Source.Contains("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
                {
                    Log.Warning("First-chance DI exception: {Message}", ex.Message);
                }
            };
        }

        private static void AddDependencyInjection(HostApplicationBuilder builder)
        {
            var diServices = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection(includeDefaults: false);

            // CRITICAL: Skip IConfiguration descriptor - use the host builder's configuration instead
            // The DependencyInjection.CreateServiceCollection() adds a default IConfiguration for test scenarios
            // but we want to use the real configuration from the host builder which includes .env and appsettings.json
            foreach (var descriptor in diServices)
            {
                if (descriptor.ServiceType == typeof(IConfiguration))
                {
                    Log.Information("[DI] Skipping IConfiguration from CreateServiceCollection - using host builder's configuration");
                    continue; // Skip - use host builder's configuration
                }

                builder.Services.Add(descriptor);
            }
        }

        private static void ConfigureUiServices(HostApplicationBuilder builder)
        {
            // UI configuration is now handled via UIConfiguration.FromConfiguration in DependencyInjection.cs
            // Register GrokAgentService (Semantic Kernel Grok integration)
            try
            {
                builder.Services.AddSingleton<GrokAgentService>();
                Log.Debug("Registered GrokAgentService as Singleton");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register GrokAgentService");
            }
        }

        /// <summary>
        /// Runs a startup health check against the database to validate connectivity before the main UI is shown.
        /// This method is <c>internal</c> to allow automated tests to invoke the startup health check logic
        /// without launching the full application. It should not be called directly by production code outside <c>Program.cs</c>.
        /// </summary>
        internal static async Task RunStartupHealthCheckAsync(IServiceProvider services)
        {
            Log.Debug("[DIAGNOSTIC] Entered RunStartupHealthCheckAsync");
            try
            {
                // Create a scope for scoped services (DbContext)
                Log.Debug("[DIAGNOSTIC] Creating scope for health check");
                using var scope = services.CreateScope();
                var scopedServices = scope.ServiceProvider;

                Log.Debug("[DIAGNOSTIC] Getting AppDbContext from DI");
                var dbContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scopedServices);

                _timelineService?.RecordOperation("Test database connectivity", "Database Health Check");
                Log.Debug("[DIAGNOSTIC] Testing database connectivity with CanConnectAsync (10s timeout)");
                var connectTask = dbContext.Database.CanConnectAsync();
                var connectTimeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var connectCompletedTask = await Task.WhenAny(connectTask, connectTimeoutTask).ConfigureAwait(false);

                if (connectCompletedTask == connectTask)
                {
                    await connectTask.ConfigureAwait(false); // Ensure the task completed successfully
                    Log.Debug("Startup health check passed: Database connection successful");
                    Log.Debug("Database CanConnectAsync succeeded");
                }
                else
                {
                    Log.Warning("Database connectivity test timed out after 10 seconds");
                    Log.Warning("Database CanConnectAsync timed out");
                    throw new TimeoutException("Database connectivity test timed out after 10 seconds");
                }

                // Get data statistics for diagnostic purposes — run on threadpool to avoid sync-over-async deadlock
                Log.Debug("[DIAGNOSTIC] Starting data statistics check");
                try
                {
                    using (var diagnosticScope = services.CreateScope())
                    {
                        var diagnosticScopedServices = diagnosticScope.ServiceProvider;
                        Log.Debug("[DIAGNOSTIC] Getting IDashboardService from DI");
                        var dashboardService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Abstractions.IDashboardService>(diagnosticScopedServices);
                        if (dashboardService != null)
                        {
                            try
                            {
                                _timelineService?.RecordOperation("Query data statistics", "Database Health Check");
                                // Use Task.WhenAny for proper async timeout pattern instead of blocking .Wait()
                                Log.Debug("[DIAGNOSTIC] Calling GetDataStatisticsAsync with 30s timeout");
                                var statsTask = dashboardService.GetDataStatisticsAsync();
                                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                                var completedTask = await Task.WhenAny(statsTask, timeoutTask).ConfigureAwait(false);

                                if (completedTask == statsTask)
                                {
                                    Log.Debug("[DIAGNOSTIC] GetDataStatisticsAsync completed, awaiting result");
                                    var stats = await statsTask.ConfigureAwait(false);
                                    Log.Debug("Diagnostic: Database contains {RecordCount} budget entries (Oldest: {Oldest}, Newest: {Newest})",
                                        stats.TotalRecords,
                                        stats.OldestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A",
                                        stats.NewestRecord?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "N/A");
                                    Log.Debug("Stats: {TotalRecords} records", stats.TotalRecords);

                                    if (stats.TotalRecords == 0)
                                    {
                                        Log.Warning("Diagnostic: Database has no budget entries. Dashboard will show empty data. Consider running data seeding scripts.");
                                    }
                                }
                                else
                                {
                                    Log.Warning("Diagnostic: GetDataStatisticsAsync timed out after {TimeoutSeconds}s", 30);
                                    Log.Warning("GetDataStatisticsAsync timed out");
                                }
                            }
                            catch (Exception innerDiagEx)
                            {
                                Log.Warning(innerDiagEx, "Diagnostic: Failed to retrieve data statistics (threadpool execution)");
                                Log.Warning(innerDiagEx, "GetDataStatisticsAsync exception");
                            }
                        }
                        else
                        {
                            Log.Warning("Diagnostic: IDashboardService not available for data statistics check");
                            Log.Debug("IDashboardService not available");
                        }
                    }
                    Log.Debug("[DIAGNOSTIC] Data statistics check completed");
                }
                catch (Exception diagEx)
                {
                    Log.Warning(diagEx, "Diagnostic: Failed to retrieve data statistics");
                    Log.Warning(diagEx, "Data statistics outer exception");
                }

                Log.Debug("Exiting RunStartupHealthCheckAsync successfully");

                // Run registered health checks
                Log.Debug("[DIAGNOSTIC] Running registered health checks");
                try
                {
                    var healthCheckService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>(scopedServices);
                    if (healthCheckService != null)
                    {
                        var healthResult = await healthCheckService.CheckHealthAsync(
                            predicate: check => check.Tags.Contains("startup"),
                            cancellationToken: CancellationToken.None).ConfigureAwait(false);

                        foreach (var entry in healthResult.Entries)
                        {
                            var status = entry.Value.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "Healthy" :
                                        entry.Value.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded ? "Degraded" : "Unhealthy";

                            Log.Information("Health Check '{Name}': {Status}", entry.Key, status);

                            if (entry.Value.Data != null && entry.Value.Data.Count > 0)
                            {
                                foreach (var data in entry.Value.Data)
                                {
                                    Log.Debug("  {Key}: {Value}", data.Key, data.Value);
                                }
                            }

                            if (!string.IsNullOrEmpty(entry.Value.Description))
                            {
                                Log.Information("  Description: {Description}", entry.Value.Description);
                            }
                        }
                    }
                    else
                    {
                        Log.Debug("HealthCheckService not available");
                    }
                }
                catch (Exception hcEx)
                {
                    Log.Warning(hcEx, "Failed to run health checks");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Startup health check failed: Database connection issue");
                Log.Debug("[DIAGNOSTIC] RunStartupHealthCheckAsync caught exception: {Message}", ex.Message);
                // Don't throw here, let the app start and log the issue
            }

            Log.Debug("RunStartupHealthCheckAsync method exit");
        }

        private static bool IsVerifyStartup(string[] args)
        {
            return args != null && Array.Exists(args, a => string.Equals(a, "--verify-startup", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task RunVerifyStartup(IHost host)
        {
            // Prevent indefinite startup hang by timing out the StartAsync call
            using var startupCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await host.StartAsync(startupCts.Token).ConfigureAwait(false);
                // Immediately stop after successful start for verification mode
                await host.StopAsync().ConfigureAwait(false);
                Log.CloseAndFlush();
            }
            catch (OperationCanceledException oce)
            {
                Log.Fatal(oce, "Verify-startup timed out after 30 seconds");
                Log.CloseAndFlush();
                throw new InvalidOperationException("Verify-startup timed out", oce);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Verify-startup run failed");
                Log.CloseAndFlush();
                throw new InvalidOperationException("Verify-startup orchestration failed", ex);
            }
        }

        private static void WireGlobalExceptionHandlers()
        {
            _timelineService?.RecordOperation("Wire Application.ThreadException handler", "Error Handlers");
            Application.ThreadException += (sender, e) =>
            {
                try
                {
                    Log.Fatal(e.Exception, "Unhandled UI thread exception");
                }
                catch (Exception fatalLogEx)
                {
                    Log.Error(fatalLogEx, "Log.Fatal failed for UI thread exception");
                }

                try
                {
                    (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(Services))?.ReportError(e.Exception, "UI Thread Exception", showToUser: false);
                }
                catch (Exception reportEx)
                {
                    Log.Warning(reportEx, "Failed to report UI thread exception to ErrorReportingService");
                }

                try
                {
                    MessageBox.Show($"UI Error: {e.Exception.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch
                {
                    // Swallow UI notification failures
                }
            };

            _timelineService?.RecordOperation("Wire AppDomain.UnhandledException handler", "Error Handlers");
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                try
                {
                    Log.Fatal(ex, "Unhandled AppDomain exception");
                }
                catch (Exception fatalLogEx)
                {
                    Log.Error(fatalLogEx, "Log.Fatal failed for AppDomain exception");
                }

                try
                {
                    (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(Services))?.ReportError(ex ?? new InvalidOperationException("Unhandled domain exception"), "Domain exception", showToUser: false);
                }
                catch (Exception reportEx)
                {
                    Log.Warning(reportEx, "Failed to report AppDomain exception to ErrorReportingService");
                }
            };
        }



        private static void ScheduleAutoCloseIfRequested(string[] args, Form mainForm)
        {
            var autoCloseMs = ParseAutoCloseMs(args);
            if (autoCloseMs <= 0)
            {
                return;
            }

            // Keep the UI open during interactive runs unless explicitly allowed
            if (Environment.UserInteractive && !IsAutoCloseAllowed(args))
            {
                Log.Information("Auto-close argument detected but ignored in interactive mode. Remove --auto-close-ms to keep the window open.");
                return;
            }

            try
            {
                ScheduleAutoClose(mainForm, autoCloseMs);
                Log.Debug("Auto-close scheduled in {Ms}ms", autoCloseMs);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to schedule auto-close");
            }
        }

        private static int ParseAutoCloseMs(string[] args)
        {
            var autoCloseArg = args?.FirstOrDefault(a => a != null && a.StartsWith("--auto-close-ms=", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(autoCloseArg))
            {
                return -1;
            }

            return int.TryParse(autoCloseArg.Split('=', 2).LastOrDefault(), out var autoCloseMs) && autoCloseMs > 0
                ? autoCloseMs
                : -1;
        }

        private static bool IsAutoCloseAllowed(string[] args)
        {
            if (IsCiEnvironment())
            {
                return true;
            }

            return args != null && Array.Exists(args, a => string.Equals(a, "--force-auto-close", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCiEnvironment()
        {
            return string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
        }

        private static void ScheduleAutoClose(Form mainForm, int autoCloseMs)
        {
            try
            {
                if (autoCloseMs <= 0) return;
                using var timer = new System.Timers.Timer(autoCloseMs) { AutoReset = false };
                timer.Elapsed += (_, _) =>
                {
                    try
                    {
                        if (mainForm != null && !mainForm.IsDisposed)
                        {
                            mainForm.BeginInvoke(new System.Action(() =>
                            {
                                try
                                {
                                    if (!mainForm.IsDisposed)
                                    {
                                        mainForm.Close();
                                    }
                                }
                                catch (Exception closeEx)
                                {
                                    Log.Debug(closeEx, "Auto-close failed to close main form");
                                }
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Auto-close timer failed");
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to schedule auto-close");
            }
        }

        private static void RunUiLoop(Form mainForm)
        {
            try
            {
                Log.Debug("[DIAGNOSTIC] RunUiLoop: ENTERED - mainForm type={Type}, IsDisposed={IsDisposed}, Visible={Visible}",
                    mainForm?.GetType().Name ?? "(null)",
                    mainForm?.IsDisposed,
                    mainForm?.Visible);
                Log.Debug("""
                [DIAGNOSTIC] RunUiLoop: About to call Application.Run(mainForm)
                Timestamp: { Timestamp}
                """,
                    DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));

                // CRITICAL: Explicitly ensure form is visible and properly positioned
                // OnLoad may have completed but form visibility needs explicit enforcement
                try
                {
                    // Force normal window state (not minimized/maximized)
                    mainForm.WindowState = System.Windows.Forms.FormWindowState.Normal;

                    // Ensure form is shown in taskbar
                    mainForm.ShowInTaskbar = true;

                    // Ensure proper positioning - center on primary screen
                    mainForm.StartPosition = FormStartPosition.CenterScreen;

                    // Explicitly show the form - DO NOT rely on Application.Run to show it
                    // Some initialization patterns can prevent automatic showing
                    if (!mainForm.Visible)
                    {
                        Log.Debug("Form not visible before Application.Run - calling Show()");
                        mainForm.Show();
                    }

                    // Force form to front and activate
                    mainForm.BringToFront();
                    mainForm.Activate();

                    // Force immediate repaint
                    mainForm.Refresh();

                    Log.Debug("mainForm visibility enforced: Visible={Visible}, WindowState={WindowState}, ShowInTaskbar={ShowInTaskbar}",
                        mainForm.Visible, mainForm.WindowState, mainForm.ShowInTaskbar);
                    Log.Debug(
                        """
                        [DIAGNOSTIC] Form state before message loop
                        Timestamp: { Timestamp}
                        Visible: { Visible}
                        WindowState: { WindowState}
                        Size: { Size}
                        Location: { Location}
                        """,
                        DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                        mainForm.Visible,
                        mainForm.WindowState,
                        mainForm.Size,
                        mainForm.Location);
                }
                catch (Exception visEx)
                {
                    Log.Error(visEx, "Failed to enforce form visibility");
                    throw;
                }

                Log.Debug(
                    """
                    [DIAGNOSTIC] RunUiLoop: About to enter message loop
                    Timestamp: { Timestamp}
                    """,
                    DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                try { Log.Fatal(ex, "Application.Run aborted with exception"); } catch (Exception logEx) { Log.Error(logEx, "Failed to log Application.Run fatal during shutdown"); }
                try { (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(Services))?.ReportError(ex, "UI message loop aborted", showToUser: false); } catch (Exception reportEx) { Log.Warning(reportEx, "Failed to report Application.Run abort to ErrorReportingService"); }
                throw new InvalidOperationException("UI message loop aborted", ex);
            }
            finally
            {
                // CRITICAL: Graceful shutdown sequence per Serilog best practices
                // Reference: https://github.com/serilog/serilog/wiki/Disposing
                //
                // The issue: DI container disposal triggers finalizers and dispose handlers
                // which may try to log. If we dispose the scope while Serilog has pending
                // log operations, file sinks can be disposed before writes complete.
                //
                // Solution: Log the exit message BEFORE disposing the scope to prevent
                // new log calls during scope disposal, then wait for async operations.

                // Step 1: Log exit before disposing scope (avoids logs during disposal)
                try
                {
                    Log.Information("Application exiting - beginning graceful shutdown.");
                }
                catch (Exception logEx)
                {
                    // If logging already failed, continue with shutdown
                    System.Diagnostics.Debug.WriteLine($"Failed to log exit message: {logEx.Message}");
                }

                // Step 2: Dispose application-lifetime scope (may trigger dispose handlers)
                // These handlers should avoid logging or use try-catch internally
                try
                {
                    _applicationScope?.Dispose();
                }
                catch (Exception disposeEx)
                {
                    // Log any errors during scope disposal, but don't let them prevent shutdown
                    try { Log.Error(disposeEx, "Error during DI container disposal"); } catch { /* Ignore */ }
                }

                // Step 3: CRITICAL - Allow time for pending operations per Serilog documentation:
                // - Pending log writes to complete (batching, disk I/O, async sinks)
                // - File handles to flush and release
                // - Background threads (RollingFileSink timer, async batching) to finish
                // Increased from 500ms to 1000ms to ensure rolling file sink completes flush
                // See: https://github.com/serilog/serilog-sinks-file/blob/main/src/Serilog.Sinks.File/Sinks/File/RollingFileSink.cs
                try
                {
                    System.Threading.Thread.Sleep(1000);
                }
                catch
                {
                    // Timing not critical, continue with flush
                }

                // Step 4: Graceful shutdown of Serilog per official documentation
                // CloseAndFlush() performs (in order):
                // 1. Waits for pending events in the async pipeline
                // 2. Calls Close() on each sink (orderly shutdown signal)
                // 3. Waits for all sinks to finish their final writes
                // 4. Disposes all sinks
                // 5. Stops background threads
                // Reference: https://github.com/serilog/serilog/blob/main/src/Serilog/Log.cs
                try
                {
                    Log.CloseAndFlush();
                }
                catch (Exception flushEx)
                {
                    // Even if flush fails, the sinks should still be disposed
                    System.Diagnostics.Debug.WriteLine($"[SHUTDOWN] Serilog.CloseAndFlush failed: {flushEx.Message}");
                    // Note: We cannot dispose Log.Logger directly as ILogger is not IDisposable
                    // CloseAndFlush handles all sink disposal internally
                }
            }
        }

        private static ReportViewerLaunchOptions CreateReportViewerLaunchOptions(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return ReportViewerLaunchOptions.Disabled;
            }

            var requestArg = args.FirstOrDefault(arg => string.Equals(arg, "--show-report-viewer", StringComparison.OrdinalIgnoreCase));
            if (requestArg == null)
            {
                return ReportViewerLaunchOptions.Disabled;
            }

            var rawPath = ExtractArgumentValue(args, "--report-path");
            var normalized = NormalizeReportPath(rawPath);
            if (string.IsNullOrWhiteSpace(normalized) || !File.Exists(normalized))
            {
                return ReportViewerLaunchOptions.Disabled;
            }

            return new ReportViewerLaunchOptions(true, normalized);
        }

        private static string? ExtractArgumentValue(string[] args, string prefix)
        {
            var match = args.FirstOrDefault(arg => arg.StartsWith(prefix + "=", StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                return null;
            }

            var value = match[(prefix.Length + 1)..].Trim();
            return TrimQuotes(value);
        }

        private static string? TrimQuotes(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                return value[1..^1];
            }

            return value;
        }

        private static string? NormalizeReportPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                var trimmed = path.Trim();
                if (Path.IsPathRooted(trimmed))
                {
                    return Path.GetFullPath(trimmed);
                }

                var combined = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, trimmed));
                return combined;
            }
            catch
            {
                return path;
            }
        }

        private static void HandleStartupFailure(Exception ex)
        {
            try
            {
                Log.Fatal(ex, "Application failed to start");
            }
            catch (Exception logEx)
            {
                Log.Error(logEx, "Failed to log startup fatal error");
            }
            finally
            {
                Log.CloseAndFlush();
            }

            try
            {
                // Only try to report to ErrorReportingService if Services is available
                if (Services != null)
                {
                    (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ErrorReportingService>(Services))?.ReportError(ex, "Startup Failure", showToUser: false);
                }
            }
            catch (Exception reportEx)
            {
                Log.Warning(reportEx, "Failed to report startup failure to ErrorReportingService");
            }

            // Show user-friendly error dialog for startup failures
            try
            {
                var projectRoot = Directory.GetCurrentDirectory();
                var logPath = Path.Combine(projectRoot, "logs");
                var message = "Startup failed: Check logs at " + logPath;

                // Check if we have UI initialized
                if (Application.MessageLoop)
                {
                    MessageBox.Show(message, "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Log.Fatal(ex, "Startup Error without message loop");
                }
            }
            catch (Exception uiEx)
            {
                Log.Fatal(uiEx, "UI error display failed during startup error reporting");
            }
        }

        #region Critical Service Validation

        /// <summary>
        /// Validates all critical DI registrations using the dedicated validation service.
        /// This offloads validation logic to a testable, reusable service.
        /// Logs comprehensive details about each validation category and service registration.
        /// </summary>
        private static void ValidateCriticalServices(IServiceProvider services)
        {
            var startTime = DateTime.Now;
            try
            {
                Log.Debug("Starting DI validation at {Timestamp}", startTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

                // Use the WinForms-specific validator which provides categorized validation
                var validationService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<IWinFormsDiValidator>(services);

                // Note: The validator itself logs detailed category-by-category progress
                // including the formatted banner output, so we don't duplicate logging here
                // Use a scoped provider for validation to allow resolving scoped services
                DiValidationResult result;
                using (var validationScope = services.CreateScope())
                {
                    var scopedServices = validationScope.ServiceProvider;
                    result = validationService.ValidateAll(scopedServices);
                }

                var endTime = DateTime.Now;
                var totalDuration = endTime - startTime;

                if (!result.IsValid)
                {
                    Log.Fatal("╔════════════════════════════════════════════════════════════════╗");
                    Log.Fatal("║   ✗ DI VALIDATION FAILED - STARTUP CANNOT PROCEED             ║");
                    Log.Fatal("╠════════════════════════════════════════════════════════════════╣");
                    Log.Fatal("║ Total Errors:   {Count,4}                                          ║", result.Errors.Count);
                    Log.Fatal("║ Total Warnings: {Count,4}                                          ║", result.Warnings.Count);
                    Log.Fatal("║ Duration:       {Duration,4:F0}ms                                    ║", totalDuration.TotalMilliseconds);
                    Log.Fatal("╚════════════════════════════════════════════════════════════════╝");

                    foreach (var error in result.Errors)
                    {
                        Log.Fatal("  ✗ {Error}", error);
                    }

                    throw new InvalidOperationException(
                        $"DI Validation failed with {result.Errors.Count} errors:{Environment.NewLine}" +
                        string.Join(Environment.NewLine, result.Errors));
                }

                // SUCCESS PATH - concise summary
                Log.Information("DI validation successful: {ServicesValidated} services validated, {Warnings} warnings, validation time: {ValidationMs}ms, total startup time: {TotalMs}ms",
                    result.SuccessMessages.Count, result.Warnings.Count, result.ValidationDuration.TotalMilliseconds, totalDuration.TotalMilliseconds);

                // Log any warnings if present
                if (result.Warnings.Count > 0)
                {
                    Log.Warning("DI Validation Warnings ({Count}):", result.Warnings.Count);
                    foreach (var warning in result.Warnings)
                    {
                        Log.Warning("  ⚠ {Warning}", warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "╔════════════════════════════════════════════════════════════════╗");
                Log.Fatal("║   ✗ CRITICAL FAILURE DURING DI VALIDATION                      ║");
                Log.Fatal("╠════════════════════════════════════════════════════════════════╣");
                Log.Fatal("║ Exception Type: {Type,-44} ║", ex.GetType().Name);
                Log.Fatal("║ Exception Msg:  {Message,-44} ║", ex.Message.Length > 44 ? ex.Message.Substring(0, 41) + "..." : ex.Message);
                Log.Fatal("╚════════════════════════════════════════════════════════════════╝");
                throw;
            }
        }

        private static void ValidateSecrets(IServiceProvider services)
        {
            var config = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(services);

            var environment = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IHostEnvironment>(services);
            var isDevelopment = environment?.IsDevelopment() ?? false;
            var isProduction = environment?.IsProduction() ?? !isDevelopment;

            static void FailFast(string message)
            {
                Log.Fatal(message);

                try
                {
                    if (Environment.UserInteractive && !Application.MessageLoop)
                    {
                        MessageBox.Show(message, "Startup configuration error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch
                {
                    // Ignore UI notification failures; the exception path will still stop startup.
                }

                throw new InvalidOperationException(message);
            }

            // Check database connection string
            var connectionString = config.GetConnectionString("DefaultConnection");
            var usingFallbackConnection = string.Equals(connectionString, FallbackConnectionString, StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(connectionString) || (isProduction && usingFallbackConnection))
            {
                var message = usingFallbackConnection
                    ? "DefaultConnection is using the development fallback connection string. Configure a production SQL Server connection string before launching."
                    : "DefaultConnection is not configured. Set ConnectionStrings:DefaultConnection to a valid SQL Server connection string.";

                if (isDevelopment)
                {
                    Log.Warning(message);
                }
                else
                {
                    FailFast(message);
                }
            }
            else
            {
                Log.Debug("Database connection string configured (length: {Length})", connectionString!.Length);
            }

            // Check Syncfusion license key
            var syncfusionKey = config["Syncfusion:LicenseKey"];
            if (string.IsNullOrWhiteSpace(syncfusionKey))
            {
                var message = "Syncfusion license key is missing. Set Syncfusion:LicenseKey or SYNCFUSION_LICENSE_KEY.";

                if (isDevelopment)
                {
                    Log.Warning(message);
                }
                else
                {
                    FailFast(message);
                }
            }
            else
            {
                Log.Debug("Syncfusion license key configured");
            }

            // Check xAI API key
            var xaiKey = config["XAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(xaiKey))
            {
                var message = "xAI API key is missing. Set XAI:ApiKey or XAI_API_KEY.";

                if (isDevelopment)
                {
                    Log.Warning(message);
                }
                else
                {
                    FailFast(message);
                }
            }
            else
            {
                Log.Debug("xAI API key configured (length: {Length})", xaiKey.Length);
            }
        }
        #endregion
    }
}
