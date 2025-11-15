// App.Prism.cs - Prism Integration for Uno Platform
// This partial class adapts the WPF Prism bootstrapping logic to Uno Platform

using DryIoc;
using WileyWidget.Uno.Views;
using WileyWidget.Services;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using Prism.Navigation.Regions;

namespace WileyWidget.Uno;

/// <summary>
/// Prism integration partial class for WileyWidget Uno application.
/// Adapts WPF PrismApplication patterns to Uno Platform.
/// Uses Prism.DryIoc.PrismApplication (resolved via GlobalUsings.cs).
/// </summary>
public sealed partial class App : PrismApplication
{
    /// <summary>
    /// Configures the host builder for Uno Platform dependency injection.
    /// This is the Uno Platform equivalent of WPF's RegisterTypes method.
    /// </summary>
    protected override void ConfigureHost(IHostBuilder builder)
    {
        builder
            .UseLogging(configure => configure.AddSerilog(dispose: true))
            .ConfigureServices((context, services) =>
            {
                Log.Information("[Prism] Starting ConfigureHost service registrations");

                // Critical infrastructure services (ported from WPF RegisterCoreInfrastructure)
                RegisterCoreInfrastructure(services);
                
                // Essential app services (ported from WPF RegisterTypes)
                RegisterEssentialServices(services);
                
                // ViewModels and Views (convention-based registration)
                RegisterConventionTypes(services);

                Log.Information("[Prism] ConfigureHost service registrations completed");
            });
    }

    /// <summary>
    /// Registers core infrastructure services.
    /// Ported from WPF App.DependencyInjection.cs RegisterCoreInfrastructure()
    /// </summary>
    private void RegisterCoreInfrastructure(IServiceCollection services)
    {
        Log.Information("[DI] Registering core infrastructure services");

        // Configuration (required for app settings)
        services.AddSingleton<IConfiguration>(sp => BuildConfiguration());
        Log.Debug("  ✓ IConfiguration registered");

        // Memory cache with 100MB limit (ported from WPF)
        services.AddMemoryCache(options => options.SizeLimit = 100 * 1024 * 1024);
        Log.Debug("  ✓ IMemoryCache registered (100MB limit)");

        // HTTP clients (3 named clients: Default, QuickBooks, XAI)
        services.AddHttpClient("Default");
        services.AddHttpClient("QuickBooks");
        services.AddHttpClient("XAI");
        // IHttpClientFactory is automatically registered by AddHttpClient
        Log.Debug("  ✓ IHttpClientFactory registered (3 named clients)");

        // Database context factory (conditional on connection string)
        var config = BuildConfiguration();
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContextFactory<WileyWidget.Data.AppDbContext>(options =>
                options.UseSqlServer(connectionString));
            Log.Debug("  ✓ IDbContextFactory<AppDbContext> registered");
        }
        else
        {
            Log.Warning("  ⚠️ Database connection string not found - DbContextFactory not registered");
        }

        Log.Information("[DI] Core infrastructure services registered");
    }

    /// <summary>
    /// Registers essential application services.
    /// Ported from WPF App.DependencyInjection.cs RegisterTypes() critical services
    /// </summary>
    private void RegisterEssentialServices(IServiceCollection services)
    {
        Log.Information("[DI] Registering essential application services");

        // Shell (main window) - critical for app startup
        services.AddTransient<ShellWindow>();
        Log.Debug("  ✓ ShellWindow registered");

        // Error reporting and exception handling
        services.AddSingleton<WileyWidget.Services.ErrorReportingService>();
        Log.Debug("  ✓ ErrorReportingService registered (Singleton)");

        // Telemetry startup service
        services.AddSingleton<WileyWidget.Services.Telemetry.TelemetryStartupService>();
        Log.Debug("  ✓ TelemetryStartupService registered (Singleton)");

        // Module health service
        services.AddSingleton<WileyWidget.Services.IModuleHealthService, WileyWidget.Services.ModuleHealthService>();
        Log.Debug("  ✓ IModuleHealthService registered (Singleton)");

        // Service scope factory (required for scoped services)
        services.AddSingleton<IServiceScopeFactory, ServiceScopeFactory>();
        Log.Debug("  ✓ IServiceScopeFactory registered");

        // Environment validator
        services.AddSingleton<WileyWidget.Services.StartupEnvironmentValidator>();
        Log.Debug("  ✓ StartupEnvironmentValidator registered (Singleton)");

        // Enterprise resource loader (Syncfusion-safe)
        services.AddSingleton<WileyWidget.Services.EnterpriseResourceLoader>();
        Log.Debug("  ✓ EnterpriseResourceLoader registered (Singleton)");

        Log.Information("[DI] Essential application services registered");
    }

    /// <summary>
    /// Registers types using convention-based discovery.
    /// Ported from WPF App.DependencyInjection.cs RegisterConventionTypes()
    /// </summary>
    private void RegisterConventionTypes(IServiceCollection services)
    {
        Log.Information("[DI] Registering types using convention-based discovery");

        // Register repositories from WileyWidget.Data assembly (Scoped lifetime)
        var dataAssembly = typeof(WileyWidget.Data.AppDbContext).Assembly;
        var repositoryTypes = dataAssembly.GetTypes()
            .Where(t => t.Name.EndsWith("Repository") && t.IsClass && !t.IsAbstract);

        foreach (var repoType in repositoryTypes)
        {
            var interfaceType = repoType.GetInterfaces()
                .FirstOrDefault(i => i.Name == $"I{repoType.Name}");
            
            if (interfaceType != null)
            {
                services.AddScoped(interfaceType, repoType);
                Log.Debug("  ✓ {Interface} -> {Implementation} (Scoped)", interfaceType.Name, repoType.Name);
            }
        }

        // Register business services from WileyWidget.Business assembly (Singleton)
        var businessAssembly = typeof(WileyWidget.Business.Services.AuditService).Assembly;
        var businessServiceTypes = businessAssembly.GetTypes()
            .Where(t => t.Name.EndsWith("Service") && t.IsClass && !t.IsAbstract);

        foreach (var serviceType in businessServiceTypes)
        {
            var interfaceType = serviceType.GetInterfaces()
                .FirstOrDefault(i => i.Name == $"I{serviceType.Name}");
            
            if (interfaceType != null)
            {
                services.AddSingleton(interfaceType, serviceType);
                Log.Debug("  ✓ {Interface} -> {Implementation} (Singleton)", interfaceType.Name, serviceType.Name);
            }
        }

        // Register ViewModels from WileyWidget assembly (Transient)
        var mainAssembly = typeof(App).Assembly;
        var viewModelTypes = mainAssembly.GetTypes()
            .Where(t => t.Name.EndsWith("ViewModel") && t.IsClass && !t.IsAbstract);

        foreach (var vmType in viewModelTypes)
        {
            services.AddTransient(vmType);
            Log.Debug("  ✓ {ViewModel} registered (Transient)", vmType.Name);
        }

        Log.Information("[DI] Convention-based type registration completed");
    }

    /// <summary>
    /// Builds configuration from various sources.
    /// Ported from WPF App.DependencyInjection.cs BuildConfiguration()
    /// </summary>
    private IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(Environment.GetCommandLineArgs());

        return builder.Build();
    }
    /// <summary>
    /// Creates and configures the DryIoc container extension.
    /// Ported from WPF App.DependencyInjection.cs CreateContainerExtension()
    /// </summary>
    protected override IContainerExtension CreateContainerExtension()
    {
        var container = new Container(Rules.Default
            .WithConcreteTypeDynamicRegistrations(reuse: Reuse.Transient)
            .With(Made.Of(FactoryMethod.ConstructorWithResolvableArguments))
            .WithFuncAndLazyWithoutRegistration()
            .WithTrackingDisposableTransients()
            .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace));

        Log.Information("[Prism] DryIoc container created with custom rules");
        
        return new DryIocContainerExtension(container);
    }

    /// <summary>
    /// Registers types with the container.
    /// Minimal registrations - most services now registered in ConfigureHost().
    /// </summary>
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        Log.Information("[Prism] Starting minimal type registrations");

        // Most registrations moved to ConfigureHost() for Uno Platform compatibility
        // Only register Prism-specific services here if needed

        Log.Information("[Prism] Minimal type registrations completed");
    }

    /// <summary>
    /// Configures the module catalog.
    /// Registers essential modules for Uno Platform.
    /// </summary>
    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        Log.Information("[Prism] Configuring module catalog");

        // Register CoreModule - essential for basic app functionality
        try
        {
            moduleCatalog.AddModule<WileyWidget.Startup.Modules.CoreModule>();
            Log.Debug("  ✓ CoreModule registered successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "  ⚠️ CoreModule registration failed - app may have limited functionality");
        }

        // QuickBooksModule can be added later if needed
        // For now, focus on core functionality

        Log.Information("[Prism] Module catalog configured");
    }

    /// <summary>
    /// Configures region adapter mappings for Uno/WinUI controls.
    /// Simplified for Uno Platform - no custom Syncfusion adapters needed initially.
    /// </summary>
    protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
    {
        base.ConfigureRegionAdapterMappings(regionAdapterMappings);

        Log.Information("[Prism] Configuring region adapter mappings for WinUI controls");

        // Register standard region adapters for WinUI controls
        regionAdapterMappings.RegisterMapping(typeof(Microsoft.UI.Xaml.Controls.ContentControl),
            Container.Resolve<Prism.Navigation.Regions.ContentControlRegionAdapter>());
        Log.Debug("  ✓ ContentControlRegionAdapter registered");

        regionAdapterMappings.RegisterMapping(typeof(Microsoft.UI.Xaml.Controls.ItemsControl),
            Container.Resolve<Prism.Navigation.Regions.ItemsControlRegionAdapter>());
        Log.Debug("  ✓ ItemsControlRegionAdapter registered");

        // Note: Custom Syncfusion adapters can be added later if needed
        // For now, use built-in ContentControlRegionAdapter for SfDataGrid

        Log.Information("[Prism] Region adapter mappings configured");
    }

    /// <summary>
    /// Configures default region behaviors.
    /// Ported from WPF App.DependencyInjection.cs ConfigureDefaultRegionBehaviors()
    /// </summary>
    protected override void ConfigureDefaultRegionBehaviors(IRegionBehaviorFactory regionBehaviors)
    {
        base.ConfigureDefaultRegionBehaviors(regionBehaviors);

        Log.Information("[Prism] Configuring custom region behaviors");

        // TODO: Port custom region behaviors from WPF if any

        Log.Information("[Prism] Region behaviors configured");
    }

    /// <summary>
    /// Creates the shell UIElement.
    /// Adapted from WPF App.Lifecycle.cs CreateShell()
    /// Note: Prism.Uno expects UIElement, not Window
    /// </summary>
    protected override UIElement CreateShell()
    {
        Log.Information("[Prism] Creating shell window");

        // Create main application window
        var shellWindow = Container.Resolve<ShellWindow>();
        
        return shellWindow;
    }

    /// <summary>
    /// Called when the Prism application has completed initialization.
    /// Ported from WPF App.Lifecycle.cs OnInitialized()
    /// </summary>
    protected override void OnInitialized()
    {
        Log.Information("[Prism] Application initialized, starting module initialization");

        try
        {
            base.OnInitialized();

            // TODO: Port custom initialization logic from WPF:
            // - Module initialization with retry
            // - Service warmup
            // - Initial navigation

            Log.Information("[Prism] Application initialization completed successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[Prism] Fatal error during application initialization");
            throw;
        }
    }
}
