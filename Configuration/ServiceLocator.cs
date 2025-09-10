#nullable enable

using Microsoft.Extensions.DependencyInjection;
using System;

namespace WileyWidget.Configuration;

/// <summary>
/// Global service locator for accessing dependency injection services in WPF application.
/// Provides a way to resolve services from anywhere in the application.
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes the service locator with the application's service provider.
    /// Should be called during application startup after building the service provider.
    /// </summary>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Gets a service of the specified type from the service provider.
    /// </summary>
    public static T GetService<T>() where T : notnull
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");
        }

        return _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a service of the specified type from the service provider, or null if not found.
    /// </summary>
    public static T? GetServiceOrDefault<T>() where T : class
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");
        }

        // Special handling for SettingsService since it's a singleton
        if (typeof(T) == typeof(WileyWidget.Services.SettingsService))
        {
            return WileyWidget.Services.SettingsService.Instance as T;
        }

        return _serviceProvider.GetService<T>();
    }

    /// <summary>
    /// Creates a new scope for resolving scoped services.
    /// </summary>
    public static IServiceScope CreateScope()
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");
        }

        return _serviceProvider.CreateScope();
    }
}
