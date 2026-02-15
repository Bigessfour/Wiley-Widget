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
            DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);

        dockingManager.Should().NotBeNull();
        dockingManager.HostControl.Should().NotBeNull();
        dockingManager.HostControl.Should().NotBe(form);
        dockingManager.HostControl.Name.Should().Be("_dockingClientPanel");
        left!.Name.Should().Be("LeftDockPanel");
        right!.Name.Should().Be("RightDockPanel");
        central!.Name.Should().Be("CentralDocumentPanel");

        var hostControl = dockingManager.HostControl;
        hostControl.Controls.Contains(left).Should().BeTrue();
        hostControl.Controls.Contains(right).Should().BeTrue();
        hostControl.Controls.Contains(central).Should().BeTrue();
        layoutManager.Should().BeNull();
    }
}
