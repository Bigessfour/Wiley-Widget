using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class QuickBooksPanelIntegrationTests
{
    [StaFact]
    public void ShowQuickBooksPanel_AddsPanelToDockingHost()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);
        _ = form.Handle;

        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var (dockingManager, _, _, _, _, _, _) = DockingHostFactory.CreateDockingHost(form, provider, null, form, logger);
        var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<PanelNavigationService>>(provider);
        var navigator = new PanelNavigationService(dockingManager, form, provider, navLogger);

        navigator.ShowPanel<QuickBooksPanel>("QuickBooks Synchronization", DockingStyle.Right);

        var panel = FindControl<QuickBooksPanel>(form);
        panel.Should().NotBeNull();
    }

    private static TPanel? FindControl<TPanel>(Control root) where TPanel : Control
    {
        if (root is TPanel match)
        {
            return match;
        }

        foreach (Control child in root.Controls)
        {
            var found = FindControl<TPanel>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
