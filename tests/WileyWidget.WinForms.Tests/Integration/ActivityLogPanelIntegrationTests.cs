using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.DataGrid;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Diagnostics;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class ActivityLogPanelIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static ActivityLogPanel CreatePanel(IServiceProvider services, ActivityLogViewModel? viewModel = null)
    {
        var factory = ServiceProviderServiceExtensions.GetRequiredService<SyncfusionControlFactory>(services);
        return new ActivityLogPanel(viewModel ?? new ActivityLogViewModel(NullLogger<ActivityLogViewModel>.Instance), factory);
    }

    private static T? FindDescendantControlByAccessibleName<T>(Control root, string accessibleName) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match && string.Equals(match.AccessibleName, accessibleName, StringComparison.Ordinal))
            {
                return match;
            }

            var nested = FindDescendantControlByAccessibleName<T>(child, accessibleName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void PumpUi(int milliseconds = 250)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static Form CreateHostForm(Size size)
        => new()
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            Size = size,
            ShowInTaskbar = false
        };

    [StaFact]
    public void Panel_SurfacesViewModelState_AndUsesReadOnlyManualGrid()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var viewModel = new ActivityLogViewModel(NullLogger<ActivityLogViewModel>.Instance);
        using var panel = CreatePanel(scope.ServiceProvider, viewModel);
        using var host = CreateHostForm(new Size(640, 420));

        host.Controls.Add(panel);
        host.Show();
        panel.Show();
        PumpUi();

        viewModel.Title = "Activity Pulse";
        viewModel.StatusText = "Loaded 2 activities";
        viewModel.ActivityEntries.Add(new ActivityLog { Timestamp = DateTime.UtcNow.AddMinutes(-5), Activity = "Account Updated", Details = "GL-1000", Status = "Success" });
        viewModel.ActivityEntries.Add(new ActivityLog { Timestamp = DateTime.UtcNow.AddMinutes(-1), Activity = "Report Generated", Details = "Budget Overview", Status = "Queued" });
        PumpUi();

        var header = FindDescendantControlByAccessibleName<PanelHeader>(panel, "Activity Log Header");
        var statusLabel = FindDescendantControlByAccessibleName<Label>(panel, "Activity Log Status");
        var grid = FindDescendantControlByAccessibleName<SfDataGrid>(panel, "Activity Log Grid");

        header.Should().NotBeNull();
        header!.Title.Should().Be("Activity Pulse");
        statusLabel.Should().NotBeNull();
        statusLabel!.Text.Should().Be("Loaded 2 activities");
        grid.Should().NotBeNull();
        grid!.AutoGenerateColumns.Should().BeFalse();
        grid.AllowEditing.Should().BeFalse();
        grid.Columns.Count.Should().Be(4);
    }

    [StaFact]
    public void Panel_NarrowHost_DoesNotReportHeaderOrActionsAsClipped()
    {
        TestThemeHelper.EnsureOffice2019Colorful();
        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        using var panel = CreatePanel(scope.ServiceProvider);
        using var host = CreateHostForm(new Size(320, 520));

        host.Controls.Add(panel);
        host.Show();
        panel.Show();
        PumpUi();

        var result = PanelLayoutDiagnostics.Capture(panel);
        var clippedIdentifiers = result.ClippedVisibleControls.Select(finding => finding.Identifier).ToList();

        clippedIdentifiers.Should().NotContain("Activity Log Header");
        clippedIdentifiers.Should().NotContain("Activity Log Actions");
        clippedIdentifiers.Should().NotContain("ButtonPanel");
    }
}
