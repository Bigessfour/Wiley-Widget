using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using System.Windows;
using Syncfusion.Licensing; // Official Syncfusion licensing namespace
using WileyWidget.Services;

namespace WileyWidget;

/// <summary>
/// Application bootstrap: configures structured logging, loads Syncfusion license, applies
/// global exception handlers, and loads persisted user settings before main window shows.
/// </summary>
public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		// TODO: Replace the placeholder with your actual Syncfusion license key string.
		// Documentation: https://help.syncfusion.com/common/essential-studio/licensing/how-to-register-in-an-application
		// Example (DO NOT KEEP): SyncfusionLicenseProvider.RegisterLicense("YOUR LICENSE KEY");
		// SyncfusionLicenseProvider.RegisterLicense("REPLACE_WITH_YOUR_LICENSE_KEY");
		ConfigureLogging();
		Log.Information("=== Application Startup ===");
		TryLoadLicenseFromFile();
		ConfigureGlobalExceptionHandling();
		SettingsService.Instance.Load();
		base.OnStartup(e);
	}

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
	/// development without a license while avoiding noisy user-facing errors.
	/// </summary>
	private void TryLoadLicenseFromFile()
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
				}
			}
		}
		catch { /* fail silent */ }
	}
}

