using Microsoft.UI.Xaml;
using Serilog;
using Serilog.Debugging;
using System;
using System.IO;
using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using WileyWidget.WinUI.Configuration;

namespace WileyWidget.WinUI
{
    public partial class App : Application
    {
        static App()
        {
            // Generated Program.Main handles ComWrappers + DispatcherQueue initialization
            // Configure logging only
            try
            {
                // Calculate logs directory using entry assembly location (more reliable than BaseDirectory)
                var exePath = Assembly.GetEntryAssembly()?.Location ?? AppContext.BaseDirectory;
                var exeDir = Path.GetDirectoryName(exePath) ?? Directory.GetCurrentDirectory();
                
                // Navigate from bin/Debug/net9.0-windows10.0.26100.0/win-x64 to repo root logs folder
                // Or use absolute path as fallback
                string logDir;
                var repoRootCandidate = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", "..", "logs"));
                if (Directory.Exists(Path.GetDirectoryName(repoRootCandidate)))
                {
                    logDir = repoRootCandidate;
                }
                else
                {
                    // Fallback: create logs next to exe
                    logDir = Path.Combine(exeDir, "logs");
                }
                
                Directory.CreateDirectory(logDir);

                // Enable Serilog self-log to capture internal errors
                try
                {
                    var selfLogPath = Path.Combine(logDir, "serilog-selflog.txt");
                    var selfWriter = TextWriter.Synchronized(new StreamWriter(selfLogPath, true) { AutoFlush = true });
                    SelfLog.Enable(selfWriter);
                    selfWriter.WriteLine($"[{DateTime.UtcNow:O}] SelfLog enabled - exePath={exePath}, logDir={logDir}");
                }
                catch { }

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("AppVersion", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown")
                    .WriteTo.Console()
                    .WriteTo.File(
                        Path.Combine(logDir, "startup-.log"), 
                        rollingInterval: RollingInterval.Day, 
                        retainedFileCountLimit: 7,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                Log.Information("[APP STATIC] Serilog configured - exePath={ExePath}, logDir={LogDir}", exePath, logDir);
                Log.Information("[APP STATIC] Static constructor complete - App initialization starting");
            }
            catch (Exception ex)
            {
                try { 
                    SelfLog.WriteLine($"[{DateTime.UtcNow:O}] FATAL: Serilog configuration failed: {ex}"); 
                    Console.WriteLine($"FATAL: Logging configuration failed: {ex}");
                } catch { }
            }
        }
        public static StartupDiagnostics Diagnostics { get; } = new StartupDiagnostics();
        public static IServiceProvider? Services { get; private set; }
        
        public new static App Current => (App)Application.Current;

        public class StartupDiagnostics
        {
            public bool BootstrapperAttempted { get; set; }
            public bool BootstrapperInitialized { get; set; }
            public bool DispatcherAvailable { get; set; }
            public bool XamlInitialized { get; set; }
            public Exception? LastException { get; set; }
            public DateTime UtcStarted { get; } = DateTime.UtcNow;
        }
        public App()
        {
            Log.Information("[APP] Constructor start - {Utc}", Diagnostics.UtcStarted);

            // Configure Dependency Injection FIRST (before XAML)
            try
            {
                Log.Information("[APP] Configuring dependency injection");
                Services = DependencyInjection.ConfigureServices();
                Log.Information("[APP] Dependency injection configured successfully");
                
                // Validate critical dependencies
                if (!DependencyInjection.ValidateDependencies(Services))
                {
                    Log.Warning("[APP] Some dependencies failed validation - check logs");
                }
            }
            catch (Exception ex)
            {
                Diagnostics.LastException = ex;
                Log.Fatal(ex, "[APP] Failed to configure dependency injection");
                throw;
            }

            // Initialize XAML
            try
            {
                this.InitializeComponent();
                Diagnostics.XamlInitialized = true;
                Log.Information("[APP] InitializeComponent succeeded");
            }
            catch (Exception ex)
            {
                Diagnostics.LastException = ex;
                Log.Error(ex, "[APP] InitializeComponent failed - presenting diagnostic fallback window");

                try
                {
                    var fallback = new Window();
                    var tb = new TextBlock { Text = "Startup error: see logs for details", FontSize = 16 };
                    fallback.Content = tb;
                    fallback.Activate();
                }
                catch (Exception inner)
                {
                    Log.Error(inner, "[APP] Failed to show diagnostic fallback window");
                }

                throw;
            }

            // Add unhandled exception handler
            this.UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "[APP] Unhandled exception");
            e.Handled = true;
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Log.Information("[APP] OnLaunched called - args={Args}", args);
            
            try
            {
                Log.Information("[APP] Creating MainWindow");
                m_window = new MainWindow();
                Log.Information("[APP] MainWindow constructed, activating");
                m_window.Activate();
                Log.Information("[APP] MainWindow activated successfully");
            }
            catch (Exception ex)
            {
                Diagnostics.LastException = ex;
                Log.Fatal(ex, "[APP] Exception during OnLaunched");
                try
                {
                    var path = Path.Combine("logs", "startup-fatal.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "logs");
                    File.AppendAllText(path, DateTime.UtcNow + " - " + ex + Environment.NewLine);
                }
                catch { }
                throw;
            }
        }

        private Window? m_window;
    }
}
