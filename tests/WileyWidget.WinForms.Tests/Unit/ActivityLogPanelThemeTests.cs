using System;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit;

[Collection("SyncfusionTheme")]
public class ActivityLogPanelThemeTests
{
    [WinFormsFact]
    public void ActivityLogPanel_DeferredSplitContainer_UsesActiveTheme()
    {
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();
        var viewModel = scope.ServiceProvider.GetService(typeof(ActivityLogViewModel)) as ActivityLogViewModel;
        var factory = scope.ServiceProvider.GetService(typeof(SyncfusionControlFactory)) as SyncfusionControlFactory;

        viewModel.Should().NotBeNull();
        factory.Should().NotBeNull();

        using var hostForm = new Form
        {
            Width = 1400,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        using var panel = new ActivityLogPanel(viewModel!, factory!)
        {
            Dock = DockStyle.Fill,
        };

        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();
        panel.PerformLayout();
        hostForm.PerformLayout();
        Application.DoEvents();
        Application.DoEvents();

        var gridSplit = GetPrivateField<SplitContainerAdv>(panel, "_gridSplit");
        var activityGrid = GetPrivateField<Syncfusion.WinForms.DataGrid.SfDataGrid>(panel, "_activityGrid");

        gridSplit.ThemeName.Should().Be("Office2019Colorful",
            "the deferred ActivityLogPanel split container should be re-themed to the active application theme");
        activityGrid.ThemeName.Should().Be("Office2019Colorful",
            "the activity grid should match the same active theme as the rest of the panel");
    }

    private static TControl GetPrivateField<TControl>(object instance, string fieldName)
        where TControl : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"field {fieldName} should exist on {instance.GetType().Name}");

        var timeoutAt = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < timeoutAt)
        {
            var value = field!.GetValue(instance) as TControl;
            if (value != null)
            {
                return value;
            }

            Application.DoEvents();
        }

        throw new InvalidOperationException($"Field '{fieldName}' on {instance.GetType().Name} was not initialized.");
    }
}