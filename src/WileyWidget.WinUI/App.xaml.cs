// App.xaml.cs - WinUI 3 Prism Application Bootstrapper
//
// Ported from WPF App.xaml.cs (November 13, 2025)
// This is the main entry point for the WinUI 3 application with Prism MVVM framework
//
// Key differences from WPF:
// 1. Base class: Microsoft.UI.Xaml.Application → Prism.DryIoc.PrismApplication
// 2. Window management: Uses Microsoft.UI.Xaml.Window instead of System.Windows.Window
// 3. Resource dictionaries: Uses ms-appx:/// URI syntax instead of pack://
// 4. Licensing: Same Syncfusion.Licensing API, registered in static constructor
// 5. Navigation: Uses Frame-based navigation with Prism regions

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using Prism.DryIoc;
using DryIoc;
using Serilog;
using Serilog.Events;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Startup;
using WileyWidget.Abstractions;
using WileyWidget.Configuration;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Business.Interfaces;
using WileyWidget.WinUI.Behaviors;
using WileyWidget.Views;
using WileyWidget.Views.Main;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Prism Application class.
    /// Handles Prism bootstrapping, DI container setup, licensing, and diagnostics.
    /// </summary>
    public partial class App : PrismApplication
    {
        #region Assembly Resolution Infrastructure (Ported from WPF)

        private static readonly ConcurrentDictionary<string, Assembly?> _resolvedAssemblies = new();
        private static readonly ConcurrentDictionary<string, bool> _failedAssemblies = new();
        internal static bool _enableNuGetScanning = false;

        private static readonly HashSet<string> _knownPackagePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Prism", "DryIoc", "Syncfusion", "Bold", "Serilog",
            "Microsoft.Extensions", "Microsoft.EntityFrameworkCore",
            "System.Text.Json", "Polly", "Microsoft.Data"
        };

        #endregion

        #region License Management (Ported from WPF)

        public enum LicenseRegistrationStatus
        {
            NotAttempted,
            Success,
            TrialMode,
            Failed,
            InvalidKey,
            NetworkError
        }

        private static LicenseRegistrationStatus _syncfusionLicenseStatus = LicenseRegistrationStatus.NotAttempted;
        private static LicenseRegistrationStatus _boldLicenseStatus = LicenseRegistrationStatus.NotAttempted;
        private static string _syncfusionLicenseError = string.Empty;
        private static string _boldLicenseError = string.Empty;

        public static LicenseRegistrationStatus SyncfusionLicenseStatus => _syncfusionLicenseStatus;
        public static LicenseRegistrationStatus BoldLicenseStatus => _boldLicenseStatus;
        public static string SyncfusionLicenseError => _syncfusionLicenseError;
        public static string BoldLicenseError => _boldLicenseError;

        #endregion

        #region Static Constructor - License Registration

        /// <summary>
        /// Static constructor: Register Syncfusion licenses BEFORE any instance members or controls.
        /// Per Syncfusion docs: https://help.syncfusion.com/winui/licensing/how-to-register-in-an-application
        /// </summary>
        static App()
        {
            // Initialize Serilog logger FIRST
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "logs/wiley-widget-winui-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    shared: true,
                    flushToDiskInterval: TimeSpan.Zero)
                .WriteTo.File(
                    path: "logs/startup-diagnostic-winui-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u4}] {MachineName} {ProcessId}:{ThreadId} {SourceContext}{NewLine}    {Message:lj}{NewLine}{Exception}",
                    shared: true,
                    flushToDiskInterval: TimeSpan.Zero)
                .CreateLogger();

            Log.Information("[WINUI_STARTUP] Initializing WinUI 3 application with Prism");

            // Register Syncfusion license
            var syncfusionKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            var boldKey = Environment.GetEnvironmentVariable("BOLD_LICENSE_KEY");

            if (!string.IsNullOrWhiteSpace(syncfusionKey) && !syncfusionKey.StartsWith("${"))
            {
                try
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
                    _syncfusionLicenseStatus = LicenseRegistrationStatus.Success;
                    Log.Information("[LICENSE] ✓ Syncfusion license registered successfully");
                }
                catch (ArgumentException argEx)
                {
                    _syncfusionLicenseStatus = LicenseRegistrationStatus.InvalidKey;
                    _syncfusionLicenseError = argEx.Message;
                    Log.Warning("[LICENSE] ✗ Invalid Syncfusion license key: {Error}", argEx.Message);
                }
                catch (Exception ex)
                {
                    _syncfusionLicenseStatus = LicenseRegistrationStatus.Failed;
                    _syncfusionLicenseError = ex.Message;
                    Log.Error(ex, "[LICENSE] ✗ Syncfusion license registration failed");
                }
            }
            else
            {
                _syncfusionLicenseStatus = LicenseRegistrationStatus.TrialMode;
                _syncfusionLicenseError = "License key not configured";
                Log.Warning("[LICENSE] ⚠ Syncfusion running in trial mode - set SYNCFUSION_LICENSE_KEY");
            }

            // Bold Reports license (fallback to Syncfusion key)
            var boldLicenseKey = !string.IsNullOrWhiteSpace(boldKey) && !boldKey.StartsWith("${") ? boldKey : syncfusionKey;
            if (!string.IsNullOrWhiteSpace(boldLicenseKey) && !boldLicenseKey.StartsWith("${"))
            {
                try
                {
                    Bold.Licensing.BoldLicenseProvider.RegisterLicense(boldLicenseKey);
                    _boldLicenseStatus = LicenseRegistrationStatus.Success;
                    Log.Information("[LICENSE] ✓ Bold Reports license registered successfully");
                }
                catch (Exception ex)
                {
                    _boldLicenseStatus = LicenseRegistrationStatus.Failed;
                    _boldLicenseError = ex.Message;
                    Log.Warning(ex, "[LICENSE] ⚠ Bold Reports license registration failed");
                }
            }
            else
            {
                _boldLicenseStatus = LicenseRegistrationStatus.TrialMode;
                Log.Information("[LICENSE] Bold Reports using trial mode or Syncfusion license");
            }

            Log.Information("[LICENSE] Summary - Syncfusion: {SfStatus}, Bold: {BoldStatus}",
                _syncfusionLicenseStatus, _boldLicenseStatus);
        }

        #endregion

        private Window? m_window;
        private readonly string _startupId;
        private IConfiguration? _configuration;

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            _startupId = Guid.NewGuid().ToString("N")[..8];
            
            Log.Information("[APP_CTOR] WinUI App constructor - Startup ID: {StartupId}", _startupId);

            this.InitializeComponent();

            // Wire up global exception handling
            this.UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            Log.Information("[APP_CTOR] WinUI App initialization complete");
        }

        #region Prism Overrides

        /// <summary>
        /// Creates and returns the shell window (main window) for the application.
        /// </summary>
        protected override Window CreateShell()
        {
            Log.Information("[PRISM] Creating shell window");
            
            // TODO: Replace with actual MainWindow once ported
            m_window = new Window
            {
                Title = "Wiley Widget - WinUI 3"
            };

            return m_window;
        }

        /// <summary>
        /// Registers types with the DI container.
        /// Ported from WPF App.DependencyInjection.cs RegisterTypes method.
        /// </summary>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Log.Information("[PRISM] Registering types with DI container (WinUI)");

            // Configuration instance
            _configuration = BuildConfiguration();
            containerRegistry.RegisterInstance(_configuration);

            // -----------------------------------------------------------
            // EF Core (Use existing factory approach – ensures scoped DbContexts)
            // -----------------------------------------------------------
            try
            {
                var dbFactory = new AppDbContextFactory(_configuration);
                containerRegistry.RegisterSingleton<IDbContextFactory<AppDbContext>>(dbFactory);
                containerRegistry.Register<AppDbContext>(() => dbFactory.CreateDbContext());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PRISM] Failed to configure EF Core DbContext factory (falling back to in-memory)");
                var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("WileyWidget_Degraded").Options;
                containerRegistry.RegisterSingleton<IDbContextFactory<AppDbContext>>(new AppDbContextFactory(options));
                containerRegistry.Register<AppDbContext>(() => new AppDbContext(options));
            }

            // -----------------------------------------------------------
            // Repositories (explicit, repository interfaces used in this solution)
            // -----------------------------------------------------------
            containerRegistry.Register<IMunicipalAccountRepository, MunicipalAccountRepository>();
            containerRegistry.Register<IBudgetRepository, BudgetRepository>();

            // -----------------------------------------------------------
            // Core Services
            // -----------------------------------------------------------
            containerRegistry.RegisterSingleton<IQuickBooksService, QuickBooksService>();
            // Health & diagnostics: prefer the Startup service implementations when present
            containerRegistry.RegisterSingleton<WileyWidget.Services.Startup.IHealthReportingService, WileyWidget.Services.Startup.HealthReportingService>();
            containerRegistry.RegisterSingleton<WileyWidget.Services.Startup.IDiagnosticsService, WileyWidget.Services.Startup.DiagnosticsService>();
            // AI: register the Grok-backed implementation if present; otherwise retain a no-op implementation
            try
            {
                // Attempt to resolve a Grok AI service type dynamically to avoid hard compile dependency
                var grokType = Type.GetType("WileyWidget.Services.AI.GrokAIService, WileyWidget.Services");
                if (grokType != null)
                {
                    containerRegistry.RegisterSingleton(typeof(IAIService), grokType);
                }
                else
                {
                    containerRegistry.RegisterSingleton<IAIService, WileyWidget.Services.NullAIService>();
                }
            }
            catch
            {
                containerRegistry.RegisterSingleton<IAIService, WileyWidget.Services.NullAIService>();
            }

            // Register Serilog logger instance
            containerRegistry.RegisterInstance<Serilog.ILogger>(Log.Logger);

            // -----------------------------------------------------------
            // ViewModel -> View mappings (Prism ViewModelLocator will use)
            // Keep the existing, working registrations and add WinUI-specific view types
            // -----------------------------------------------------------
            containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();
            containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();
            // QuickBooks Sync view – fall back to existing QuickBooks view if WinUI port not present
            containerRegistry.RegisterForNavigation<QuickBooksView, QuickBooksViewModel>();
            containerRegistry.RegisterForNavigation<MunicipalAccountView, MunicipalAccountViewModel>();
            containerRegistry.RegisterForNavigation<BudgetView, BudgetViewModel>();
            containerRegistry.RegisterForNavigation<AnalyticsView, AnalyticsViewModel>();
            containerRegistry.RegisterForNavigation<ReportsView, ReportsViewModel>();

            // Additional WinUI view registrations referenced in resource dictionaries
            try
            {
                // Use reflection to register optional types if they exist in WinUI project
                var revenueChartViewType = Type.GetType("WileyWidget.Views.RevenueChartView, WileyWidget.Views");
                var revenueChartViewModelType = Type.GetType("WileyWidget.ViewModels.RevenueChartViewModel, WileyWidget.ViewModels");
                if (revenueChartViewType != null && revenueChartViewModelType != null)
                {
                    containerRegistry.RegisterForNavigation((Type)revenueChartViewType, (Type)revenueChartViewModelType);
                }

                var quickBooksSyncViewType = Type.GetType("WileyWidget.Views.QuickBooksSyncView, WileyWidget.Views");
                var quickBooksSyncViewModelType = Type.GetType("WileyWidget.ViewModels.QuickBooksSyncViewModel, WileyWidget.ViewModels");
                if (quickBooksSyncViewType != null && quickBooksSyncViewModelType != null)
                {
                    containerRegistry.RegisterForNavigation((Type)quickBooksSyncViewType, (Type)quickBooksSyncViewModelType);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Optional WinUI view registrations skipped (port not present)");
            }

            // -----------------------------------------------------------
            // Custom Region Adapter for WinUI Frame
            // -----------------------------------------------------------
            containerRegistry.RegisterSingleton<IRegionAdapter, FrameRegionAdapter>();

            Log.Information("[PRISM] Type registration complete (WinUI)");
        }

        /// <summary>
        /// Configures the module catalog for modular application architecture.
        /// Ported from WPF App.DependencyInjection.cs ConfigureModuleCatalog method.
        /// </summary>
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            Log.Information("[PRISM] Configuring module catalog");

            // TODO: Port module registration from WPF
            // moduleCatalog.AddModule<CoreModule>();
            // moduleCatalog.AddModule<QuickBooksModule>();

            Log.Information("[PRISM] Module catalog configured");
        }

        /// <summary>
        /// Creates the DryIoc container extension with WinUI-specific rules.
        /// Ported from WPF App.DependencyInjection.cs CreateContainerExtension method.
        /// </summary>
        protected override IContainerExtension CreateContainerExtension()
        {
            Log.Information("[PRISM] Creating DryIoc container extension");

            var container = new Container(rules => rules
                .WithAutoConcreteTypeResolution()
                .With(FactoryMethod.ConstructorWithResolvableArguments)
                .WithTrackingDisposableTransients()
                .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace));

            return new DryIocContainerExtension(container);
        }

        /// <summary>
        /// Called after the application has been initialized.
        /// Used for post-initialization setup and diagnostics.
        /// </summary>
        protected override void OnInitialized()
        {
            Log.Information("[PRISM] OnInitialized - Running post-initialization setup");

            base.OnInitialized();

            // TODO: Port initialization logic from WPF App.Lifecycle.cs
            // - Initialize modules
            // - Run diagnostics
            // - Setup telemetry

            Log.Information("[PRISM] OnInitialized complete");
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Builds the IConfiguration instance with environment-specific settings.
        /// Ported from WPF App.DependencyInjection.cs BuildConfiguration method.
        /// </summary>
        private IConfiguration BuildConfiguration()
        {
            Log.Information("[CONFIG] Building configuration");

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<App>(optional: true);

            var config = configBuilder.Build();

            Log.Information("[CONFIG] Configuration built for environment: {Environment}", environment);

            return config;
        }

        #endregion

        #region Exception Handling

        /// <summary>
        /// Handles unhandled exceptions from the UI thread.
        /// </summary>
        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "[EXCEPTION] Unhandled UI exception");
            
            File.AppendAllText("logs/critical-startup-failures.log",
                $"[{DateTime.UtcNow:O}] UI Exception: {e.Exception}\n==========\n\n");

            e.Handled = true; // Prevent app crash for debugging
        }

        /// <summary>
        /// Handles unhandled exceptions from background threads.
        /// </summary>
        private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Log.Fatal(ex, "[EXCEPTION] AppDomain unhandled exception (Terminating: {IsTerminating})", e.IsTerminating);

            File.AppendAllText("logs/critical-startup-failures.log",
                $"[{DateTime.UtcNow:O}] Domain Exception: {ex}\n==========\n\n");
        }

        #endregion
    }
}
