#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.ViewModels;
using WileyWidget.Services;
using WileyWidget.Data;

namespace WileyWidget.Configuration;

/// <summary>
/// Factory for creating WPF components with proper dependency injection.
/// Replaces Service Locator pattern with factory-based approach.
/// </summary>
public class WpfComponentFactory
{
    private readonly IServiceProvider _serviceProvider;

    public WpfComponentFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Creates a MainViewModel with all its dependencies properly injected.
    /// </summary>
    public MainViewModel CreateMainViewModel()
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        return new MainViewModel(
            sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            sp.GetService<GrokSupercomputer>(),
            sp.GetService<Data.AppDbContext>(),
            sp.GetService<IEnterpriseRepository>(),
            sp.GetService<QuickBooksService>(),
            sp.GetService<WpfMiddlewareService>());
    }

    /// <summary>
    /// Creates a SettingsViewModel with proper dependency injection.
    /// </summary>
    public SettingsViewModel CreateSettingsViewModel()
    {
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SettingsViewModel>();
    }

    /// <summary>
    /// Gets a service instance within a scoped context.
    /// </summary>
    public T GetService<T>() where T : notnull
    {
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a service instance within a scoped context, or null if not found.
    /// </summary>
    public T? GetServiceOrDefault<T>() where T : class
    {
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetService<T>();
    }
}
