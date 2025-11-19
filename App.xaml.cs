using System;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.ViewModels;
using WileyWidget.Services;
using Serilog;
using Serilog.Events;
using Syncfusion.Licensing;

namespace WileyWidget
{
    public partial class App : Application
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int MessageBoxW(System.IntPtr hWnd, string text, string caption, uint type);

        public static IServiceProvider? Services { get; private set; }
        private Window? m_window;

        public App()
        {
            // Register Syncfusion license
            SyncfusionLicenseProvider.RegisterLicense("YOUR_SYNCFUSION_COMMUNITY_LICENSE_KEY");

            // Configure Serilog FIRST - before anything else
            // Get workspace root directory (assume bin/x64/Debug/... structure)
            var appDir = AppContext.BaseDirectory;
            var workspaceRoot = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(appDir, "..", "..", "..", ".."));
            var logsDir = System.IO.Path.Combine(workspaceRoot, "logs");
            
            // Ensure logs directory exists
            System.IO.Directory.CreateDirectory(logsDir);
            
            var logPath = System.IO.Path.Combine(logsDir, "app-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithProcessId()
                .Enrich.WithProcessName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .WriteTo.Console(
                    formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(logPath,
                    rollingInterval: RollingInterval.Day,
                    formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("========== WileyWidget Application Starting ==========");
            Log.Information("Log file: {LogPath}", logPath);

            // Request Windows App SDK runtime - try 1.8 first, then fall back to 1.0
            try
            {
                Log.Information("Initializing Windows App SDK Bootstrap for version 1.8...");
                // Version 1.8 = 0x00010008
                Bootstrap.Initialize(0x00010008);
                Log.Information("Windows App SDK Bootstrap 1.8 initialized successfully");
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80040014))
            {
                Log.Warning(ex, "Bootstrap 1.8 not found, trying 1.0...");
                try
                {
                    Bootstrap.Initialize(0x00010000);
                    Log.Information("Windows App SDK Bootstrap 1.0 initialized successfully");
                }
                catch (System.Runtime.InteropServices.COMException ex2)
                {
                    var hint = "Windows App SDK runtime initialization failed. " +
                               "This usually means the Windows App Runtime is not installed, or the installed runtime's architecture/version " +
                               "doesn't match the app (x86/x64/ARM64). Install the appropriate Windows App SDK runtime (recommended 1.8+) or check your RID.\n" +
                               $"Original error: {ex2.Message}";
                    
                    Log.Fatal(ex2, "Bootstrap initialization failed: {Hint}", hint);
                    System.Diagnostics.Debug.WriteLine(hint);
                    
                    try
                    {
                        // Show a native message box so GUI-only apps surface the error immediately.
                        var result = MessageBoxW(System.IntPtr.Zero, hint, "Wiley-Widget - Runtime Error", 0);
                        if (result == 0)
                        {
                            Log.Warning("Failed to display error message box");
                        }
                    }
                    catch { /* swallow any failures showing the dialog */ }

                    throw new InvalidOperationException(hint, ex2);
                }
            }

            Log.Information("Configuring dependency injection container...");
            var host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    // Register services required by MainViewModel
                    services.AddSingleton<AILoggingService>();
                    services.AddSingleton<QuickBooksService>();

                    // Register ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<QuickBooksDashboardViewModel>();
                    
                    // Register Windows
                    services.AddSingleton<MainWindow>(sp =>
                        new MainWindow(sp.GetRequiredService<MainViewModel>()));
                    
                    Log.Information("Services registered: AILoggingService, QuickBooksService, MainViewModel, QuickBooksDashboardViewModel, MainWindow");
                })
                .Build();

            Services = host.Services;
            Log.Information("Service provider configured successfully");

            InitializeComponent();
            Log.Information("App.xaml components initialized");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Log.Information("OnLaunched called");
            try
            {
                Log.Information("Resolving MainWindow from service provider...");
                m_window = Services!.GetRequiredService<MainWindow>();
                Log.Information("MainWindow resolved: {WindowType}", m_window.GetType().Name);
                
                Log.Information("Activating window...");
                m_window.Activate();
                Log.Information("Window activated successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to launch window");
                throw;
            }
        }
    }
}