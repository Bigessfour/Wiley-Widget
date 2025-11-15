using Serilog;
using Serilog.Events;
using System;

namespace WileyWidget.Uno;

/// <summary>
/// WileyWidget Uno Platform application with Prism MVVM framework.
/// Inherits from PrismApplication (resolved via global using Prism.DryIoc).
/// </summary>
public sealed partial class App : PrismApplication
{
    /// <summary>
    /// Initializes the singleton application object and sets up Serilog logging.
    /// This is the first line of authored code executed.
    /// </summary>
    public App()
    {
        // Initialize Serilog before anything else
        InitializeSerilog();
        
        Log.Information("[Startup] WileyWidget Uno application initializing");
        
        this.InitializeComponent();
        
        // Register Syncfusion license
        RegisterSyncfusionLicense();
    }
    
    /// <summary>
    /// Initializes Serilog for structured logging.
    /// </summary>
    private void InitializeSerilog()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/wiley-widget-uno-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
    
    /// <summary>
    /// Registers Syncfusion license from environment variable.
    /// </summary>
    private void RegisterSyncfusionLicense()
    {
        try
        {
            var syncfusionKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(syncfusionKey))
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
                Log.Information("[License] Syncfusion license registered successfully");
            }
            else
            {
                Log.Warning("[License] SYNCFUSION_LICENSE_KEY not set - running in trial mode");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[License] Failed to register Syncfusion license");
        }
    }

    // Note: OnLaunched is sealed in PrismApplicationBase and cannot be overridden.
    // Prism.Uno.WinUI handles application launch internally.
    // Use OnInitialized() in App.Prism.cs for custom initialization logic.
    
    /// <summary>
    /// Called when the application is exiting.
    /// Ensures proper cleanup of resources.
    /// </summary>
    // Note: Prism.Uno.WinUI does not expose OnExit in the same way as WPF.
    // Cleanup is handled by Serilog sinks and process shutdown, so we avoid overriding OnExit here.

}

// Remove old Uno.Extensions code below
#if REMOVE_OLD_CODE
    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected async override void OnLaunched_OLD(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            // Add navigation support for toolkit controls such as TabBar and NavigationView
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)

                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning);

                    // Uno Platform namespace filter groups
                    // Uncomment individual methods to see more detailed logging
                    //// Generic Xaml events
                    //logBuilder.XamlLogLevel(LogLevel.Debug);
                    //// Layout specific messages
                    //logBuilder.XamlLayoutLogLevel(LogLevel.Debug);
                    //// Storage messages
                    //logBuilder.StorageLogLevel(LogLevel.Debug);
                    //// Binding related messages
                    //logBuilder.XamlBindingLogLevel(LogLevel.Debug);
                    //// Binder memory references tracking
                    //logBuilder.BinderMemoryReferenceLogLevel(LogLevel.Debug);
                    //// DevServer and HotReload related
                    //logBuilder.HotReloadCoreLogLevel(LogLevel.Information);
                    //// Debug JS interop
                    //logBuilder.WebAssemblyLogLevel(LogLevel.Debug);

                }, enableUnoLogging: true)
                .UseSerilog(consoleLoggingEnabled: true, fileLoggingEnabled: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                .ConfigureServices((context, services) =>
                {
                    // TODO: Register your services
                    //services.AddSingleton<IMyService, MyService>();
                })
                .UseNavigation(RegisterRoutes)
            );
        MainWindow = builder.Window;

        #if DEBUG
        MainWindow.UseStudio();
#endif
                // MainWindow.SetWindowIcon(); // TODO: Fix SetWindowIcon method

        Host = await builder.NavigateAsync<Shell>();
    }

    private static void RegisterRoutes_OLD(global::Uno.Extensions.Navigation.IViewRegistry views, global::Uno.Extensions.Navigation.IRouteRegistry routes)
    {
        views.Register(
            new ViewMap(ViewModel: typeof(ShellViewModel)),
            new ViewMap<MainPage, MainViewModel>(),
            new DataViewMap<SecondPage, SecondViewModel, Entity>()
        );

        routes.Register(
            new RouteMap("", View: views.FindByViewModel<ShellViewModel>(),
                Nested:
                [
                    new ("Main", View: views.FindByViewModel<MainViewModel>(), IsDefault:true),
                    new ("Second", View: views.FindByViewModel<SecondViewModel>()),
                ]
            )
        );
    }
#endif
