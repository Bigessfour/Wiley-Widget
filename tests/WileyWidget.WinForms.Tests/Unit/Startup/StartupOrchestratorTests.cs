using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Startup;

[Collection(WinFormsUiCollection.CollectionName)]
public sealed class StartupOrchestratorTests
{
    private readonly WinFormsUiThreadFixture _ui;

    public StartupOrchestratorTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
    }

    [Fact]
    [Trait("Category", "Startup")]
    public void RegisterLicense_WithConfiguredKey_DoesNotThrow()
    {
        using var provider = BuildProvider();
        var orchestrator = provider.GetRequiredService<IStartupOrchestrator>();

        _ui.Run(() => orchestrator.RegisterLicenseAsync().GetAwaiter().GetResult());
    }

    [StaFact]
    [Trait("Category", "Startup")]
    public void ValidateServices_Succeeds()
    {
        using var provider = BuildProvider();
        var orchestrator = provider.GetRequiredService<IStartupOrchestrator>();

        _ui.Run(() => orchestrator.ValidateServicesAsync(provider).GetAwaiter().GetResult());
    }

    [StaFact]
    [Trait("Category", "Startup")]
    public void InitializeTheme_DoesNotThrow()
    {
        using var provider = BuildProvider();
        var orchestrator = provider.GetRequiredService<IStartupOrchestrator>();

        _ui.Run(() => orchestrator.InitializeThemeAsync().GetAwaiter().GetResult());
    }

    private static ServiceProvider BuildProvider()
    {
        var services = WileyWidget.WinForms.Configuration.DependencyInjection.CreateServiceCollection();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Syncfusion:LicenseKey"] = "ci-test-license-key",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                ["AppSettings:Environment"] = "Test",
                ["XAI:ApiKey"] = "ci-test-xai-key-0123456789-abcdefghijklmnopqrstuvwxyz-VALID"
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(builder => builder.AddDebug());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
