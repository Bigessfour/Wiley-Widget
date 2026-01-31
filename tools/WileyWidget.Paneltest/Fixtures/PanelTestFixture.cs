using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Paneltest.Helpers;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.Plugins;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.Paneltest.Fixtures;

/// <summary>
/// Test fixture for creating configured service providers for panel testing.
/// Handles DI setup, mock service creation, and scope factory initialization.
/// </summary>
public class PanelTestFixture
{
    private readonly IServiceCollection _services;
    private IServiceProvider? _provider;

    public PanelTestFixture()
    {
        _services = new ServiceCollection();
        ConfigureBasicServices();
    }

    /// <summary>
    /// Configure basic services (logging, DI, etc.)
    /// </summary>
    private void ConfigureBasicServices()
    {
        _services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Register ViewModels and their dependencies
        ConfigureViewModelServices();
    }

    /// <summary>
    /// Configure ViewModel services and their dependencies.
    /// </summary>
    private void ConfigureViewModelServices()
    {
        // For testing, we'll use null services since WarRoomViewModel accepts optional parameters
        // Register the WarRoomViewModel with null dependencies
        _services.AddScoped<WarRoomViewModel>(sp =>
        {
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<WarRoomViewModel>>(sp);
            return new WarRoomViewModel(
                grokService: null,  // Optional parameter
                rateScenarioTools: null,  // Optional parameter
                logger: logger
            );
        });
    }

    /// <summary>
    /// Add a mock service to the fixture.
    /// </summary>
    public void AddMockService<TInterface>(Mock<TInterface> mockService)
        where TInterface : class
    {
        _services.AddScoped(_ => mockService.Object);
    }

    /// <summary>
    /// Add a scoped implementation of a service.
    /// </summary>
    public void AddService<TInterface, TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface
    {
        _services.AddScoped<TInterface, TImplementation>();
    }

    /// <summary>
    /// Build the service provider (called once).
    /// </summary>
    public IServiceProvider BuildServiceProvider()
    {
        _provider ??= _services.BuildServiceProvider();
        return _provider;
    }

    /// <summary>
    /// Create a test scope factory from the provider.
    /// </summary>
    public TestScopeFactory CreateTestScopeFactory()
    {
        var provider = BuildServiceProvider();
        return new TestScopeFactory(provider);
    }

    /// <summary>
    /// Get a logger for a specific type.
    /// </summary>
    public ILogger<T> GetLogger<T>() where T : class
    {
        var provider = BuildServiceProvider();
        var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILoggerFactory>(provider);
        return factory.CreateLogger<T>();
    }

    /// <summary>
    /// Get a service from the provider.
    /// </summary>
    public T GetService<T>() where T : notnull
    {
        var provider = BuildServiceProvider();
        return provider.GetRequiredService<T>();
    }

    /// <summary>
    /// Get or create a mock for a service type.
    /// </summary>
    public Mock<T> GetMockService<T>() where T : class
    {
        return new Mock<T>();
    }

    /// <summary>
    /// Dispose the service provider.
    /// </summary>
    public void Dispose()
    {
        (_provider as IDisposable)?.Dispose();
    }
}

/// <summary>
/// Fixture for theme testing across multiple theme variants.
/// </summary>
public class ThemeTestFixture
{
    public static readonly string[] SupportedThemes = new[]
    {
        "Office2019Colorful",
        "Office2019Black",
        "Office2019DarkGray",
        "Office2019HighContrast",
        "Office2016Colorful",
        "Office2016Black",
        "Office2016DarkGray"
    };

    /// <summary>
    /// Create fixtures for all supported themes for iterative testing.
    /// </summary>
    public static Dictionary<string, PanelTestFixture> CreateFixturesForAllThemes()
    {
        var fixtures = new Dictionary<string, PanelTestFixture>();

        foreach (var theme in SupportedThemes)
        {
            var fixture = new PanelTestFixture();
            fixtures[theme] = fixture;
        }

        return fixtures;
    }

    /// <summary>
    /// Get a mock service for theme-related functionality.
    /// </summary>
    public static Mock<T> CreateThemeMock<T>(string themeName) where T : class
    {
        if (!SupportedThemes.Contains(themeName))
        {
            throw new ArgumentException($"Theme '{themeName}' not supported.", nameof(themeName));
        }

        return new Mock<T>();
    }
}
