using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using System.Windows;
using Syncfusion.Licensing; // Official Syncfusion licensing namespace
using WileyWidget.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using WileyWidget.Configuration;

namespace WileyWidget;

/// <summary>
/// Application bootstrap: configures structured logging, loads Syncfusion license, applies
/// global exception handlers, and loads persisted user settings before main window shows.
/// </summary>
public partial class App : Application
{
    private IConfiguration _configuration;

    /// <summary>
    /// WPF doc-compliant constructor: registers Syncfusion license BEFORE any controls are created.
    /// Logging configured immediately after so registration path can still emit messages on subsequent calls.
    /// </summary>
    public App()
    {
        // Load configuration first
        LoadConfiguration();

        // Configure logging first so license registration path is logged.
        ConfigureLogging();
        Log.Information("=== Application Constructor (pre-license) ===");

        // Register Syncfusion license using configuration
        RegisterSyncfusionLicense();
        Log.Information("=== Application Constructor Initialized ===");
    }
	protected override void OnStartup(StartupEventArgs e)
	{
		// License + logging already handled in constructor; calls here are intentionally omitted to avoid duplicate logs.
		Log.Information("=== Application Startup ===");

		// Configure database services
		ConfigureDatabaseServices();

		ConfigureGlobalExceptionHandling();
		SettingsService.Instance.Load();
		// Apply dark default early if none persisted (normalization happens in MainWindow).
		if (string.IsNullOrWhiteSpace(SettingsService.Instance.Current.Theme))
			SettingsService.Instance.Current.Theme = "FluentDark";
		// Optional: in automated test scenarios we may want to auto-dismiss the Syncfusion trial dialog so processes exit cleanly.
		if (string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_AUTOCLOSE_LICENSE"), "1", StringComparison.OrdinalIgnoreCase))
		{
			TryScheduleLicenseDialogAutoClose();
		}
		base.OnStartup(e);
	}

	/// <summary>
	/// Configures database services and initializes the database
	/// </summary>
	private async void ConfigureDatabaseServices()
	{
		try
		{
			Log.Information("Configuring database services...");

			// Build configuration
			var configuration = new ConfigurationBuilder()
				.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddUserSecrets<WileyWidget.App>(optional: true)
				.AddEnvironmentVariables()
				.Build();

			// Configure services
			var services = new ServiceCollection();
			services.AddDatabaseServices(configuration);

			// Build service provider
			var serviceProvider = services.BuildServiceProvider();

			// Initialize database
			await DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider);

			Log.Information("Database services configured successfully");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to configure database services");
			// In development, you might want to show a message box or handle this differently
			// For now, we'll log the error and continue - the app can still run without database
		}
	}

	/// <summary>
	/// Schedules a dispatcher timer that scans for the Syncfusion trial license dialog and attempts to close it (used only in test automation modes).
	/// </summary>
	private void TryScheduleLicenseDialogAutoClose()
	{
		try
		{
			var timer = new System.Windows.Threading.DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(500)
			};
			int attempts = 0;
			timer.Tick += (_, _) =>
			{
				try
				{
					attempts++;
					for (int i = 0; i < System.Windows.Application.Current.Windows.Count; i++)
					{
						if (System.Windows.Application.Current.Windows[i] is Window w && w.Title.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
						{
							w.Close();
							Log.Information("Auto-closed Syncfusion trial dialog (test mode).");
							break;
						}
					}
					if (attempts > 12) // ~6 seconds then stop
					{
						timer.Stop();
					}
				}
				catch { }
			};
			timer.Start();
		}
		catch { }
	}

	protected override void OnExit(ExitEventArgs e)
	{
		try { Log.Information("=== Application Exit ==="); Log.CloseAndFlush(); } catch { }
		base.OnExit(e);
	}

	/// <summary>
	/// Registers the Syncfusion license using precedence: environment variable (SYNCFUSION_LICENSE_KEY) > side-by-side license.key file.
	/// Falls back silently if neither is present so the app can still run in development (will show trial banner).
	/// </summary>
    /// <summary>
    /// Loads application configuration from appsettings.json and environment variables
    /// </summary>
    private void LoadConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        _configuration = builder.Build();
    }

    /// <summary>
    /// Registers Syncfusion license using configuration system with fallback methods
    /// </summary>
    private void RegisterSyncfusionLicense()
    {
        // 0. Configuration-based license (highest priority)
        try
        {
            var configKey = _configuration["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(configKey) && configKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(configKey.Trim());
                Log.Information("Syncfusion license registered from configuration.");
                return;
            }
        }
        catch { /* ignore and continue */ }

        // 1. Optional embedded license hook (implemented in user-created partial file not committed).
        // If the partial method returns true, registration succeeded and we skip other sources.
        try
        {
            if (TryRegisterEmbeddedLicense())
            {
                Log.Information("Syncfusion license registered from embedded partial.");
                return;
            }
        }
        catch { /* ignore and continue */ }

        // 2. Environment variable (User or Machine scope). User sets via: [System.Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY","<key>","User")
        try
        {
            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(envKey) && envKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(envKey.Trim());
                Log.Information("Syncfusion license registered from environment variable.");
                return;
            }
            else
            {
                Log.Information("No SYNCFUSION_LICENSE_KEY environment variable set – attempting file fallback.");
            }
        }
        catch { /* ignore and continue to file fallback */ }

        // 3. File fallback
        if (!TryLoadLicenseFromFile())
        {
            Log.Warning("Syncfusion license NOT registered (no config, no env var, no license.key). Application will run in trial mode.");
        }
    }

	/// <summary>
	/// Partial hook allowing a private, untracked file (e.g. LicenseKey.Private.cs) to embed the license.
	/// Return true if a key was registered. Default (no implementation) returns false.
	/// </summary>
	private partial bool TryRegisterEmbeddedLicense();

	/// <summary>
	/// Configure Serilog (daily rolling file in AppData, 7 file retention, enriched with process/thread/machine).
	/// Swallows internal logging setup exceptions to avoid blocking application startup.
	/// </summary>
	private void ConfigureLogging()
	{
		try
		{
			var logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WileyWidget", "logs");
			Directory.CreateDirectory(logRoot);
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
				.Enrich.WithProcessId()
				.Enrich.WithThreadId()
				.Enrich.WithMachineName()
				.Enrich.FromLogContext()
				.WriteTo.File(
					path: Path.Combine(logRoot, "app-.log"),
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: 7,
					shared: false,
					restrictedToMinimumLevel: LogEventLevel.Debug,
					outputTemplate: "{Timestamp:O} [{Level:u3}] (pid:{ProcessId} tid:{ThreadId}) {Message:lj}{NewLine}{Exception}")
				.CreateLogger();
		}
		catch
		{
			// fallback silent; existing manual LogException still handles exceptions.
		}
	}

	/// <summary>
	/// Hooks unhandled exception events (AppDomain, Dispatcher, TaskScheduler) and routes them to the logger.
	/// Dispatcher exceptions are marked handled to keep the app alive—adjust if a fail-fast policy is desired.
	/// </summary>
	private void ConfigureGlobalExceptionHandling()
	{
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
			LogException("AppDomain", args.ExceptionObject as Exception);
		DispatcherUnhandledException += (_, args) =>
		{
			LogException("Dispatcher", args.Exception);
			args.Handled = true; // prevent crash; adjust if you want the app to exit
		};
		TaskScheduler.UnobservedTaskException += (_, args) =>
		{
			LogException("TaskScheduler", args.Exception);
			args.SetObserved();
		};
	}

	/// <summary>
	/// Central exception logging wrapper – isolates pattern so additional metadata or policies can be added later.
	/// </summary>
	private void LogException(string source, Exception ex)
	{
		if (ex == null) return;
		try { Log.Error(ex, "Unhandled exception ({SourceTag})", source); }
		catch { /* ignore */ }
	}

	/// <summary>
	/// Attempts to load Syncfusion license from a side-by-side 'license.key' file; silent on failure to allow
	/// development without a license while avoiding noisy user-facing errors (will show trial notice if unlicensed).
	/// </summary>
	private bool TryLoadLicenseFromFile()
	{
		try
		{
			var exeDir = AppDomain.CurrentDomain.BaseDirectory;
			var licensePath = Path.Combine(exeDir, "license.key");
			if (File.Exists(licensePath))
			{
				var key = File.ReadAllText(licensePath).Trim();
				if (!string.IsNullOrWhiteSpace(key))
				{
					SyncfusionLicenseProvider.RegisterLicense(key);
					Log.Information("Syncfusion license loaded from file.");
					return true;
				}
			}
		}
		catch { /* fail silent */ }
		return false;
	}
}

