using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.AI;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.DependencyInjection;

[Trait("Category", "Integration")]
public sealed class DependencyInjectionIntegrationTests
{
    [Fact]
    public void AddWinFormsServices_Builds_WithScopeAndBuildValidation()
    {
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();

        services.AddWinFormsServices(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddWinFormsServices_Resolves_CriticalSingletonServices()
    {
        using var provider = BuildRealisticServiceProvider();

        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IGrokApiKeyProvider>(provider).Should().NotBeNull();
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider).Should().NotBeNull();
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IQuickBooksAuthService>(provider).Should().NotBeNull();
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ITelemetryService>(provider).Should().NotBeNull();
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IStatusProgressService>(provider).Should().NotBeNull();
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<RoleBasedAccessControl>(provider).Should().NotBeNull();
    }

    [Fact]
    public void AddWinFormsServices_Resolves_CriticalScopedServices_WithinScope()
    {
        using var provider = BuildRealisticServiceProvider();
        using var scope = provider.CreateScope();

        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scope.ServiceProvider).Should().NotBeNull();
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IBudgetRepository>(scope.ServiceProvider).Should().NotBeNull();
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<GrokAgentService>(scope.ServiceProvider).Should().NotBeNull();
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IGrokRecommendationService>(scope.ServiceProvider).Should().NotBeNull();
    }

    private static ServiceProvider BuildRealisticServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddWinFormsServices(BuildTestConfiguration());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static IConfiguration BuildTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                ["UI:IsUiTestHarness"] = "true",
                ["XAI:ApiKey"] = "gsk_test_1234567890abcdef",
                ["OPENAI_API_KEY"] = "sk-fake-openai-key",
                ["Services:QuickBooks:OAuth:RedirectUri"] = "http://localhost:9876/callback"
            })
            .Build();
    }
}
