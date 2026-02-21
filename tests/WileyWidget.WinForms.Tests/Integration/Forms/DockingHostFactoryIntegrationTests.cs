using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class DockingHostFactoryIntegrationTests
{
    [StaFact]
    public void CreateDockingHost_WiresPanelsAndLayoutManager()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        _ = form.Handle;

        var (dockingManager, left, right, central, _, _, layoutManager) =
            DockingHostFactory.CreateDockingHost(form, provider, null, logger);

        dockingManager.Should().NotBeNull();
        dockingManager.HostControl.Should().Be(form);
        left!.Name.Should().Be("LeftDockPanel");
        right!.Name.Should().Be("RightDockPanel");
        central!.Name.Should().Be("CentralDocumentPanel");
        form.Controls.Contains(left).Should().BeTrue();
        form.Controls.Contains(right).Should().BeTrue();
        form.Controls.Contains(central).Should().BeTrue();
        layoutManager.Should().NotBeNull();
    }
}
