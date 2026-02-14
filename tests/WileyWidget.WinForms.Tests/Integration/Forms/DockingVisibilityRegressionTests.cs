using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.Tests.Integration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
[Collection("SyncfusionTheme")]
public sealed class DockingVisibilityRegressionTests
{
    private sealed class VisibilityProbePanel : UserControl
    {
        public VisibilityProbePanel()
        {
            MinimumSize = new Size(120, 80);
        }
    }

    [StaFact]
    public void CreateDockingHost_MinimalMode_StillCreatesFullDockingSurfaces()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        var overrides = new Dictionary<string, string?>
        {
            ["UI:UseSyncfusionDocking"] = "true",
            ["UI:MinimalMode"] = "true",
            ["UI:AutoShowPanels"] = "false"
        };

        using var provider = IntegrationTestServices.BuildProvider(overrides);
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        _ = form.Handle;

        var dockingHost = new UserControl
        {
            Name = "DockingHostContainerForTests",
            Dock = DockStyle.Fill
        };
        form.Controls.Add(dockingHost);
        form.Show();
        Application.DoEvents();

        var (dockingManager, leftDockPanel, rightDockPanel, centralDocumentPanel, _, _, _) =
            DockingHostFactory.CreateDockingHost(form, provider, null, dockingHost, logger);

        dockingManager.Should().NotBeNull();
        dockingManager.HostControl.Should().Be(dockingHost);
        leftDockPanel.Should().NotBeNull();
        rightDockPanel.Should().NotBeNull();
        centralDocumentPanel.Should().NotBeNull();
        centralDocumentPanel.Parent.Should().Be(dockingHost);
        centralDocumentPanel.Visible.Should().BeTrue();
    }

    [StaFact]
    public void RightDockRequest_WithFullDockingSurface_ShowsVisibleDockedPanel()
    {
        Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
        TestThemeHelper.EnsureOffice2019Colorful();

        var overrides = new Dictionary<string, string?>
        {
            ["UI:UseSyncfusionDocking"] = "true",
            ["UI:MinimalMode"] = "true",
            ["UI:AutoShowPanels"] = "false"
        };

        using var provider = IntegrationTestServices.BuildProvider(overrides);
        using var form = IntegrationTestServices.CreateMainForm(provider);
        var dockingLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
        var navigationLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<PanelNavigationService>>(provider);
        _ = form.Handle;

        var dockingHost = new UserControl
        {
            Name = "DockingHostContainerForTests",
            Dock = DockStyle.Fill
        };
        form.Controls.Add(dockingHost);
        form.Show();
        Application.DoEvents();

        var (dockingManager, leftDockPanel, rightDockPanel, _, _, _, _) =
            DockingHostFactory.CreateDockingHost(form, provider, null, dockingHost, dockingLogger);

        leftDockPanel.Should().NotBeNull();
        rightDockPanel.Should().NotBeNull();

        using var panelNavigationService = new PanelNavigationService(dockingManager, dockingHost, provider, navigationLogger);

        panelNavigationService.ShowPanel<VisibilityProbePanel>("Visibility Probe", DockingStyle.Right, allowFloating: false);
        form.PerformLayout();
        dockingHost.PerformLayout();
        form.Refresh();
        dockingHost.Refresh();
        Application.DoEvents();

        var probePanel = FindChildByName(dockingHost, "VisibilityProbe");

        probePanel.Should().NotBeNull();
        probePanel!.Visible.Should().BeTrue();
        IsControlChainVisible(probePanel).Should().BeTrue();
        probePanel.Bounds.Width.Should().BeGreaterThan(0);
        probePanel.Bounds.Height.Should().BeGreaterThan(0);
        dockingHost.ClientRectangle.IntersectsWith(probePanel.Bounds).Should().BeTrue();
        FindChildByName(dockingHost, "RightDockPanel").Should().NotBeNull();
    }

    private static Control? FindChildByName(Control root, string name)
    {
        if (root.Name == name)
        {
            return root;
        }

        foreach (Control child in root.Controls)
        {
            var match = FindChildByName(child, name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool IsControlChainVisible(Control control)
    {
        var current = control;
        while (current != null)
        {
            if (!current.Visible)
            {
                return false;
            }

            current = current.Parent;
        }

        return true;
    }
}
