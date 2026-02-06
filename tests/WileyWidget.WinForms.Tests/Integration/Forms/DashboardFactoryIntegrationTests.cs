using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Integration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
public sealed class DashboardFactoryIntegrationTests
{
    [StaFact]
    public void CreateDashboardPanel_BuildsExpectedCards()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);

        var panel = DashboardFactory.CreateDashboardPanel(null, form, Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider));

        panel.Controls.OfType<LegacyGradientPanel>().Count().Should().Be(5);
        panel.Controls.OfType<LegacyGradientPanel>().Any(card => card.Name.Contains("Accounts")).Should().BeTrue();
    }
}
