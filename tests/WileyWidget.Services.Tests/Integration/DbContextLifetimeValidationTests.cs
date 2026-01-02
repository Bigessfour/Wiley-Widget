using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Data;
using WileyWidget.WinForms.Configuration;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Services.Tests.Integration;

/// <summary>
/// Integration tests to validate DI container configuration and DbContext lifetime management.
/// These tests ensure that singleton services do not hold references to scoped DbContext instances,
/// preventing ObjectDisposedException and cross-thread access violations.
/// </summary>
public class DbContextLifetimeValidationTests
{
    [Fact]
    public void ServiceProvider_ShouldNotHaveSingletonsWithScopedDependencies()
    {
        // Arrange - Build the real DI container
        var services = DependencyInjection.CreateServiceCollection(includeDefaults: true);

        // Override DbContext with in-memory for testing
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AppDbContext));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("LifetimeTest"));

        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Act & Assert - Attempt to resolve all singleton services
        var singletonDescriptors = services
            .Where(d => d.Lifetime == ServiceLifetime.Singleton)
            .Where(d => !d.ServiceType.IsGenericType) // Skip generic types like IOptions<T>
            .ToList();

        foreach (var singletonDescriptor in singletonDescriptors)
        {
            try
            {
                // Attempt to resolve from root provider (should not fail for singletons)
                var instance = serviceProvider.GetService(singletonDescriptor.ServiceType);

                // If the singleton depends on scoped services, BuildServiceProvider with ValidateScopes=true
                // would have already thrown, but we can also check constructor parameters
                if (instance != null)
                {
                    var constructorParams = instance.GetType().GetConstructors()
                        .SelectMany(c => c.GetParameters())
                        .Select(p => p.ParameterType)
                        .ToList();

                    // Check for known scoped types in constructor
                    var hasScopedDependency = constructorParams.Any(t =>
                        t == typeof(AppDbContext) ||
                        t == typeof(IDbContextFactory<AppDbContext>) && singletonDescriptor.Lifetime == ServiceLifetime.Singleton ||
                        t.Name.EndsWith("Repository", StringComparison.Ordinal));

                    hasScopedDependency.Should().BeFalse(
                        $"Singleton service {singletonDescriptor.ServiceType.Name} should not directly depend on scoped DbContext or repositories");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("scope", StringComparison.OrdinalIgnoreCase))
            {
                // If we get here, it means ValidateScopes caught the issue
                throw new InvalidOperationException(
                    $"Singleton service {singletonDescriptor.ServiceType.Name} has invalid scoped dependencies: {ex.Message}",
                    ex);
            }
        }
    }

    [Fact]
    public async Task DashboardService_ShouldWorkWithProperScoping_InConcurrentScenarios()
    {
        // Arrange
        var services = DependencyInjection.CreateServiceCollection(includeDefaults: true);

        // Use in-memory database
        var dbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AppDbContext));
        if (dbDescriptor != null) services.Remove(dbDescriptor);
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("ConcurrentTest"));

        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Seed test data
        using (var seedScope = serviceProvider.CreateScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();
        }

        // Act - Simulate concurrent dashboard service calls from different scopes (like startup)
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            using var scope = serviceProvider.CreateScope();
            var dashboardService = scope.ServiceProvider.GetService<IDashboardService>();

            if (dashboardService != null)
            {
                var data = await dashboardService.GetDashboardDataAsync(CancellationToken.None);
                return data.Count();
            }
            return 0;
        }).ToArray();

        // Assert - Should complete without ObjectDisposedException
        var results = await Task.WhenAll(tasks);
        results.Should().AllSatisfy(count => count.Should().BeGreaterOrEqualTo(0));
    }

    [Fact]
    public async Task BudgetRepository_ShouldNotBeUsedAcrossScopes()
    {
        // Arrange
        var services = DependencyInjection.CreateServiceCollection(includeDefaults: true);

        var dbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AppDbContext));
        if (dbDescriptor != null) services.Remove(dbDescriptor);
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("ScopeTest"));

        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Seed data
        using (var seedScope = serviceProvider.CreateScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();
        }

        IBudgetRepository? repositoryFromFirstScope;

        // Act - Create repository in one scope
        using (var scope1 = serviceProvider.CreateScope())
        {
            repositoryFromFirstScope = scope1.ServiceProvider.GetService<IBudgetRepository>();
            repositoryFromFirstScope.Should().NotBeNull();
        }
        // scope1 disposed here

        // Try to use repository from disposed scope (should fail or be inaccessible)
        // This test documents the expected behavior: scoped services should not leak across scopes
        Func<Task> act = async () =>
        {
            if (repositoryFromFirstScope != null)
            {
                // This SHOULD throw ObjectDisposedException because the DbContext is disposed
                await repositoryFromFirstScope.GetByFiscalYearAsync(2025, CancellationToken.None);
            }
        };

        // Assert - Should throw because context is disposed
        await act.Should().ThrowAsync<ObjectDisposedException>("repository's DbContext should be disposed after scope ends");
    }
}
