using Xunit;
using Xunit.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Tests.Unit.DependencyInjection;

/// <summary>
/// Comprehensive Dependency Injection Test Suite
/// Based on Microsoft .NET DI documentation and best practices
/// Covers all areas of DI to validate Wiley Widget implementation
/// </summary>
[Collection(WinFormsUiCollection.CollectionName)]
public class ComprehensiveDependencyInjectionTests
{
    private readonly WinFormsUiThreadFixture _ui;

    public ComprehensiveDependencyInjectionTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
    }

    [Fact]
    public void ServiceLifetimes_ShouldWorkCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITransientService, TestService>();
        services.AddScoped<IScopedService, TestService>();
        services.AddSingleton<ISingletonService, TestService>();

        var provider = services.BuildServiceProvider();

        // Act & Assert - Transient
        var transient1 = provider.GetRequiredService<ITransientService>();
        var transient2 = provider.GetRequiredService<ITransientService>();
        transient1.Should().NotBeSameAs(transient2, "Transient services should be different instances");

        // Act & Assert - Singleton
        var singleton1 = provider.GetRequiredService<ISingletonService>();
        var singleton2 = provider.GetRequiredService<ISingletonService>();
        singleton1.Should().BeSameAs(singleton2, "Singleton services should be same instance");

        // Act & Assert - Scoped
        using (var scope1 = provider.CreateScope())
        {
            var scoped1a = scope1.ServiceProvider.GetRequiredService<IScopedService>();
            var scoped1b = scope1.ServiceProvider.GetRequiredService<IScopedService>();
            scoped1a.Should().BeSameAs(scoped1b, "Scoped services should be same within scope");

            using (var scope2 = provider.CreateScope())
            {
                var scoped2a = scope2.ServiceProvider.GetRequiredService<IScopedService>();
                scoped1a.Should().NotBeSameAs(scoped2a, "Scoped services should be different across scopes");
            }
        }
    }

    [Fact]
    public void ConstructorInjection_ShouldWorkCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IDependencyA, DependencyA>();
        services.AddTransient<IDependencyB, DependencyB>();
        services.AddTransient<ConstructorInjectionClass>();

        var provider = services.BuildServiceProvider();

        // Act
        var instance = provider.GetRequiredService<ConstructorInjectionClass>();

        // Assert
        instance.Should().NotBeNull("Constructor injection should create instance");
        instance.DependencyA.Should().NotBeNull();
        instance.DependencyB.Should().NotBeNull();
    }

    [Fact]
    public void ServiceDisposal_ShouldWorkCorrectly()
    {
        // Arrange - Test transient disposal
        var transientServices = new ServiceCollection();
        transientServices.AddTransient<IDisposableService, DisposableService>();
        var transientProvider = transientServices.BuildServiceProvider();

        var disposedServices = new List<string>();

        // Act & Assert - Transient disposal within scope
        using (var scope = transientProvider.CreateScope())
        {
            var transientService = scope.ServiceProvider.GetRequiredService<IDisposableService>();
            ((DisposableService)transientService).Disposed += (s, e) => disposedServices.Add("Transient");
        }
        // Scope disposed, transient should be disposed
        disposedServices.Should().Contain("Transient", "Transient service should be disposed when scope ends");

        // Arrange - Test scoped disposal
        var scopedServices = new ServiceCollection();
        scopedServices.AddScoped<IDisposableService, DisposableService>();
        var scopedProvider = scopedServices.BuildServiceProvider();

        disposedServices.Clear();

        // Act & Assert - Scoped disposal
        using (var scope = scopedProvider.CreateScope())
        {
            var scopedService = scope.ServiceProvider.GetRequiredService<IDisposableService>();
            ((DisposableService)scopedService).Disposed += (s, e) => disposedServices.Add("Scoped");
        }
        disposedServices.Should().Contain("Scoped", "Scoped service should be disposed when scope ends");

        // Arrange - Test singleton disposal (singletons are disposed with the root provider)
        var singletonServices = new ServiceCollection();
        singletonServices.AddSingleton<IDisposableService, DisposableService>();
        var singletonProvider = singletonServices.BuildServiceProvider();

        disposedServices.Clear();
        var singletonService = singletonProvider.GetRequiredService<IDisposableService>();
        ((DisposableService)singletonService).Disposed += (s, e) => disposedServices.Add("Singleton");

        // Dispose the provider to trigger singleton disposal
        singletonProvider.Dispose();
        disposedServices.Should().Contain("Singleton", "Singleton service should be disposed when provider is disposed");
    }

    [Fact]
    public void CircularDependency_ShouldBeDetected()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ICircularA, CircularA>();
        services.AddTransient<ICircularB, CircularB>();

        var provider = services.BuildServiceProvider();

        // Act & Assert
        Action act = () => provider.GetRequiredService<ICircularA>();

        act.Should().Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("circular", StringComparison.OrdinalIgnoreCase),
                "DI container should detect circular dependencies");
    }

    [Fact]
    public void MultipleImplementations_ShouldBeResolved()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IMultipleService, MultipleServiceA>();
        services.AddTransient<IMultipleService, MultipleServiceB>();
        services.AddTransient<MultipleConsumer>();

        var provider = services.BuildServiceProvider();

        // Act
        var consumer = provider.GetRequiredService<MultipleConsumer>();

        // Assert
        consumer.Services.Should().HaveCount(2, "Should resolve all implementations");
        consumer.Services.Should().Contain(s => s is MultipleServiceA);
        consumer.Services.Should().Contain(s => s is MultipleServiceB);
    }

    [Fact]
    public void FactoryMethods_ShouldWorkCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IFactoryService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new FactoryService(config["TestKey"] ?? "Default");
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("TestKey", "FactoryValue") })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetRequiredService<IFactoryService>();

        // Assert
        service.Value.Should().Be("FactoryValue", "Factory method should use injected configuration");
    }

    [Fact]
    public void OptionalDependencies_ShouldWorkCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<OptionalDependencyClass>();

        var provider = services.BuildServiceProvider();

        // Act
        var instance = provider.GetRequiredService<OptionalDependencyClass>();

        // Assert
        instance.OptionalService.Should().BeNull("Optional service should be null when not registered");
    }

    [Fact]
    public void ServiceValidation_ShouldPass()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IValidatedService, ValidatedService>();

        // Act
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        var service = provider.GetRequiredService<IValidatedService>();

        // Assert
        service.Should().NotBeNull("Service validation should pass");
    }

    [StaFact]
    public void WileyWidgetDiContainer_ShouldResolveAllServices()
    {
        // Arrange - Use actual Wiley Widget DI configuration with test infrastructure
        var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();

        // Add minimal test infrastructure
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", "Data Source=:memory:"),
                new KeyValuePair<string, string?>("AppSettings:Environment", "Test"),
                new KeyValuePair<string, string?>("HealthCheck:Enabled", "false")
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // Add logging
        services.AddLogging();

        // Mock database context factory (simplified for testing)
        services.AddSingleton<IDbContextFactory<AppDbContext>>(sp =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("TestDb")
                .Options;
            return new TestDbContextFactory(options);
        });

        // Add health check configuration
        services.AddSingleton(new HealthCheckConfiguration());

        // Act
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Assert - Test singleton services can be resolved from root provider
        var mainForm = provider.GetRequiredService<WileyWidget.WinForms.Forms.MainForm>();
        mainForm.Should().NotBeNull("MainForm should be resolvable");

        var settingsService = provider.GetRequiredService<ISettingsService>();
        settingsService.Should().NotBeNull("ISettingsService should be resolvable");

        // Test scoped-dependent services within a scope
        using (var scope = provider.CreateScope())
        {
            var scopedProvider = scope.ServiceProvider;

            var dashboardVm = scopedProvider.GetRequiredService<DashboardViewModel>();
            dashboardVm.Should().NotBeNull("DashboardViewModel should be resolvable within scope");
        }
    }

    [Fact]
    public void WileyWidgetScopedServices_ShouldWorkWithinScope()
    {
        // Arrange
        var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();
        var provider = services.BuildServiceProvider();

        // Act & Assert
        using (var scope = provider.CreateScope())
        {
            var scopedProvider = scope.ServiceProvider;

            var budgetRepo = scopedProvider.GetRequiredService<WileyWidget.Business.Interfaces.IBudgetRepository>();
            budgetRepo.Should().NotBeNull("IBudgetRepository should be resolvable in scope");

            var accountsRepo = scopedProvider.GetRequiredService<WileyWidget.Business.Interfaces.IAccountsRepository>();
            accountsRepo.Should().NotBeNull("IAccountsRepository should be resolvable in scope");

            var municipalRepo = scopedProvider.GetRequiredService<WileyWidget.Business.Interfaces.IMunicipalAccountRepository>();
            municipalRepo.Should().NotBeNull("IMunicipalAccountRepository should be resolvable in scope");
        }
    }

    [Fact]
    public void WileyWidgetSingletonServices_ShouldBeSameInstance()
    {
        // Arrange
        var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();
        var provider = services.BuildServiceProvider();

        // Act
        var settings1 = provider.GetRequiredService<ISettingsService>();
        var settings2 = provider.GetRequiredService<ISettingsService>();

        // Assert
        settings1.Should().BeSameAs(settings2, "Singleton services should return same instance");
    }

    [Fact]
    public void WileyWidgetTransientServices_ShouldBeDifferentInstances()
    {
        // Arrange
        var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();
        var provider = services.BuildServiceProvider();

        // Act
        var chartVm1 = provider.GetRequiredService<ChartViewModel>();
        var chartVm2 = provider.GetRequiredService<ChartViewModel>();

        // Assert
        chartVm1.Should().NotBeSameAs(chartVm2, "Transient services should return different instances");
    }

    [Fact]
    public void ScopedServices_CannotBeResolvedFromRootProvider()
    {
        // Arrange - Create a service collection with a simple scoped service
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, TestService>();

        // Enable scope validation like in production
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Act & Assert - Scoped service CANNOT be resolved from root provider when ValidateScopes = true
        var act = () => provider.GetRequiredService<IScopedService>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*scoped service*")
            .WithMessage("*root provider*");

        // Act & Assert - Scoped service CAN be resolved from scoped provider
        using (var scope = provider.CreateScope())
        {
            var scopedProvider = scope.ServiceProvider;
            var scopedService = scopedProvider.GetRequiredService<IScopedService>();

            scopedService.Should().NotBeNull("Scoped service should resolve from scoped provider");
        }
    }
}

// Test interfaces and implementations
public interface ITransientService { }
public interface IScopedService { }
public interface ISingletonService { }
public interface IDisposableService : IDisposable { }
public interface IDependencyA { }
public interface IDependencyB { }
public interface ICircularA { }
public interface ICircularB { }
public interface IMultipleService { }
public interface IFactoryService { string Value { get; } }
public interface IValidatedService { }
public interface IOptionalService { }

public class TestService : ITransientService, IScopedService, ISingletonService { }

public class DisposableService : IDisposableService
{
    public event EventHandler? Disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            Disposed?.Invoke(this, EventArgs.Empty);
        }
        // Dispose unmanaged resources
    }
}

public class DependencyA : IDependencyA { }
public class DependencyB : IDependencyB { }

public class ConstructorInjectionClass
{
    public IDependencyA DependencyA { get; }
    public IDependencyB DependencyB { get; }

    public ConstructorInjectionClass(IDependencyA a, IDependencyB b)
    {
        DependencyA = a;
        DependencyB = b;
    }
}

public class CircularA : ICircularA
{
    public CircularA(ICircularB b) { }
}

public class CircularB : ICircularB
{
    public CircularB(ICircularA a) { }
}

public class MultipleServiceA : IMultipleService { }
public class MultipleServiceB : IMultipleService { }

public class MultipleConsumer
{
    public IEnumerable<IMultipleService> Services { get; }

    public MultipleConsumer(IEnumerable<IMultipleService> services)
    {
        Services = services;
    }
}

public class FactoryService : IFactoryService
{
    public string Value { get; }

    public FactoryService(string value) => Value = value;
}

public class OptionalDependencyClass
{
    public IOptionalService? OptionalService { get; }

    public OptionalDependencyClass(IOptionalService? optional = null)
    {
        OptionalService = optional;
    }
}

public class ValidatedService : IValidatedService { }

/// <summary>
/// Test implementation of IDbContextFactory for dependency injection tests
/// </summary>
internal class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_options);
    }
}
