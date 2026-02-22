using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Tests.Integration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class RightDockPanelFactoryIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [StaFact(Skip = "Obsolete: RightDockPanelFactory no longer uses tabs or modes")]
    public void CreateRightDockPanel_CreatesTabsAndDefaultMode()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

        var (rightDockPanel, activityLogPanel) =
            RightDockPanelFactory.CreateRightDockPanel(form, provider, logger);

        // initialMode.Should().Be(RightDockPanelFactory.RightPanelMode.ActivityLog);
        activityLogPanel.Should().NotBeNull();
        // rightDockPanel.Controls.OfType<TabControl>().Any().Should().BeTrue();

        var tabControl = rightDockPanel.Controls.OfType<TabControl>().First();
        tabControl.TabPages.Cast<TabPage>().Any(tp => tp.Name == "ActivityLogTab").Should().BeTrue();
        tabControl.TabPages.Cast<TabPage>().Any(tp => tp.Name == "JARVISChatTab").Should().BeTrue();
    }
}
