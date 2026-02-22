using System;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Tests.Integration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class ProgramIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [StaFact]
    public void Program_Services_InitializesCorrectly()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        // Program.Services should be initialized
        var services = WileyWidget.WinForms.Program.Services;
        services.Should().NotBeNull();
    }

    [StaFact]
    public void Program_ServicesOrNull_Works()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        // ServicesOrNull should return services or null
        var services = WileyWidget.WinForms.Program.ServicesOrNull;
        services.Should().NotBeNull();
    }

    [StaFact]
    public void Program_MainFormInstance_CanBeSet()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        // Test setting MainFormInstance
        using var provider = IntegrationTestServices.BuildProvider();
        using var testForm = IntegrationTestServices.CreateMainForm(provider);
        WileyWidget.WinForms.Program.MainFormInstance = testForm;

        WileyWidget.WinForms.Program.MainFormInstance.Should().Be(testForm);
        WileyWidget.WinForms.Program.MainFormInstance = null;
    }

    [StaFact]
    public void Program_DI_Container_ResolvesServices()
    {
        // Force headless mode to prevent UI hangs
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

        var services = WileyWidget.WinForms.Program.Services;

        // Test resolving key services
        var config = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Configuration.IConfiguration>(services);
        config.Should().NotBeNull();

        var loggerFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.Extensions.Logging.ILoggerFactory>(services);
        loggerFactory.Should().NotBeNull();
    }
}
