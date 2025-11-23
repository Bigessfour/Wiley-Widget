using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Serilog.Debugging;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using WileyWidget.Abstractions;
using WileyWidget.Data;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;

namespace WileyWidget
{
    public partial class App : Application
    {
        public static IServiceProvider? Services { get; private set; }
        private IHost? _host;
        private Window? m_window;
        private static StreamWriter? _selfLogWriter;

        public App()
        {
            EnableSerilogSelfLog();
            SetupLogging();
            this.InitializeComponent();
        }

        private void EnableSerilogSelfLog()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var projectRoot = FindProjectRootQuick(baseDir);
                var logsDir = Path.Combine(projectRoot, "logs");
                Directory.CreateDirectory(logsDir);

                var selfLogPath = Path.Combine(logsDir, "serilog-selflog.txt");
                _selfLogWriter = File.CreateText(selfLogPath);
                var syncWriter = TextWriter.Synchronized(_selfLogWriter);
                SelfLog.Enable(syncWriter);
                SelfLog.Enable(msg => System.Diagnostics.Debug.WriteLine($"[SERILOG] {msg}"));
                System.Diagnostics.Debug.WriteLine($"Serilog SelfLog enabled at: {selfLogPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create SelfLog file: {ex.Message}");
                SelfLog.Enable(Console.Error);
            }
        }

        private string FindProjectRootQuick(string startDir)
        {
            var current = new DirectoryInfo(startDir);
            while (current != null)
            {
                if (current.GetFiles("*.csproj").Any())
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
            return startDir;
        }

        private void SetupLogging()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var projectRoot = FindProjectRoot(baseDir);
                var logsDir = Path.Combine(projectRoot, "logs");
                Directory.CreateDirectory(logsDir);

                var logPath = Path.Combine(logsDir, "app.log");
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.WithThreadId()
                    .Enrich.WithProcessId()
                    .Enrich.WithProcessName()
                    .Enrich.WithEnvironmentName()
                    .Enrich.WithMachineName()
                    .WriteTo.File(
                        logPath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                        formatProvider: CultureInfo.InvariantCulture,
                        retainedFileCountLimit: 7)
                    .WriteTo.Debug(
                        outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.Console(
                        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        formatProvider: CultureInfo.InvariantCulture)
                    .CreateLogger();

                Microsoft.UI.Xaml.Application.Current.UnhandledException += (sender, e) =>
                {
                    Log.Error(e.Exception, "Unhandled exception occurred in UI thread");
                    e.Handled = true;
                };

                Log.Information("==================== APPLICATION START ====================");
                Log.Information("Logging system initialized successfully");
                Log.Information("Log file location: {LogPath}", logPath);
                Log.Information("Project root: {ProjectRoot}", projectRoot);
                Log.Information("Base directory: {BaseDir}", baseDir);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to setup logging: {ex}");
                throw;
            }
        }

        private string FindProjectRoot(string startDir)
        {
            var current = new DirectoryInfo(startDir);
            while (current != null)
            {
                if (current.GetFiles("*.csproj").Any())
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
            return startDir;
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var launchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Log.Information("==================== APPLICATION LAUNCH ====================");
                Log.Information("Launch arguments: {Args}", args?.Arguments ?? "<none>");

                await InitializeHostAsync();

                Services = _host?.Services;
                Log.Information("DI host built and services available");

                Log.Information("Creating main window");

                m_window = new MainWindow();

                try
                {
                    // Resolve MainViewModel and assign as DataContext if available
                    var vm = Services?.GetService<WileyWidget.ViewModels.MainViewModel>();
                    if (vm != null)
                    {
                        m_window.DataContext = vm;
                        Log.Debug("Assigned MainViewModel from DI to MainWindow.DataContext");
                    }
                    else
                    {
                        Log.Warning("MainViewModel not registered in DI; DataContext not set");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to resolve or assign MainViewModel");
                }

                m_window.Activate();

                launchStopwatch.Stop();
                Log.Information("Main window activated successfully");
                Log.Information("==================== LAUNCH COMPLETE (Elapsed: {ElapsedMs}ms) ====================", launchStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                launchStopwatch.Stop();
                Log.Fatal(ex, "Critical error during application launch (Elapsed: {ElapsedMs}ms)", launchStopwatch.ElapsedMilliseconds);
                Log.CloseAndFlush();
                throw;
            }
        }

        private async Task InitializeHostAsync()
        {
            if (_host != null) return;

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    // Logging
                    services.AddLogging(builder => builder.AddSerilog());

                    // Configuration
                    services.AddSingleton<IConfiguration>(context.Configuration);

                    // Memory cache + cache service
                    services.AddMemoryCache();
                    services.AddSingleton<ICacheService, MemoryCacheService>();

                    // CommunityToolkit Messenger + ViewModel registrations
                    services.AddSingleton<IMessenger>(provider => WeakReferenceMessenger.Default);
                    // Register DataService which publishes DataMessage<T> via IMessenger
                    services.AddSingleton<DataService>();
                    services.AddSingleton<WileyWidget.ViewModels.MainViewModel>();

                    // UI pages / viewmodels
                    services.AddTransient<Views.BudgetOverviewPage>();
                    services.AddTransient<ViewModels.BudgetOverviewViewModel>();

                    // Register other services as needed (repositories, db contexts, etc.)
                    // Note: Keep registrations minimal here to avoid startup failures - register more as needed
                })
                .Build();

            try
            {
                await _host.StartAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start DI host");
                throw;
            }
        }
    }
}
