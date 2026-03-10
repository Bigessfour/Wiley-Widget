using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Tests.Integration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit;

[Collection("SyncfusionTheme")]
public sealed class ReportsPanelTests
{
    [WinFormsFact]
    public void ReportsPanel_EmptyState_StaysInsidePreviewSurface_AndBodyRendersBelowHeader()
    {
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.ViewModels.ReportsViewModel>(scope.ServiceProvider);
        var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.Factories.SyncfusionControlFactory>(scope.ServiceProvider);

        using var hostForm = new Form
        {
            Width = 1400,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        using var panel = new ReportsPanel(viewModel, factory)
        {
            Dock = DockStyle.Fill,
        };

        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();
        panel.PerformLayout();
        hostForm.PerformLayout();
        Application.DoEvents();

        var header = GetPrivateField<Control>(panel, "_panelHeader");
        var content = GetPrivateField<TableLayoutPanel>(panel, "_content");
        var parametersSplitContainer = GetPrivateField<Control>(panel, "_parametersSplitContainer");
        var reportViewerContainer = GetPrivateField<Panel>(panel, "_reportViewerContainer");
        var noDataOverlay = GetPrivateField<Control>(panel, "_noDataOverlay");
        var loadingOverlay = GetPrivateField<Control>(panel, "_loadingOverlay");

        content.Controls.Contains(header).Should().BeTrue("the reports header should remain in the root content layout");
        content.GetRow(header).Should().Be(0);
        content.Controls.Contains(parametersSplitContainer).Should().BeTrue("the reports workspace should occupy the body row below the header");
        content.GetRow(parametersSplitContainer).Should().Be(1);

        parametersSplitContainer.Top.Should().BeGreaterThanOrEqualTo(header.Bottom,
            "the reports workspace should render below the header so the top of the UI is not clipped");

        noDataOverlay.Parent.Should().BeSameAs(reportViewerContainer,
            "the empty-state overlay should stay inside the preview surface and not cover the toolbar or header");
        loadingOverlay.Parent.Should().BeSameAs(reportViewerContainer,
            "the loading overlay should stay inside the preview surface and not cover the toolbar or header");
    }

    [WinFormsFact]
    public void ReportsPanel_LoadAvailableReports_BindsFriendlyTemplateNames_AndSynchronizesSelectedReportType()
    {
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.ViewModels.ReportsViewModel>(scope.ServiceProvider);
        var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.Factories.SyncfusionControlFactory>(scope.ServiceProvider);

        using var hostForm = new Form
        {
            Width = 1400,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        using var panel = new ReportsPanel(viewModel, factory)
        {
            Dock = DockStyle.Fill,
        };

        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();
        panel.PerformLayout();
        hostForm.PerformLayout();
        Application.DoEvents();

        var reportSelector = GetPrivateField<Control>(panel, "_reportSelector");
        var dataSource = reportSelector.GetType().GetProperty("DataSource", BindingFlags.Instance | BindingFlags.Public)?.GetValue(reportSelector) as IEnumerable;

        dataSource.Should().NotBeNull();

        var boundReports = dataSource!
            .Cast<object>()
            .Select(item => item?.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

        boundReports.Should().Equal(viewModel.ReportTemplateDisplayNames,
            "the selector should expose the same friendly report names the view model maps to actual templates");
        boundReports.Should().NotBeEmpty();
        viewModel.SelectedReportType.Should().Be(boundReports[0],
            "the initial selector choice should also become the active report type for data preparation and export");
    }

    [WinFormsFact]
    public async Task ReportsPanel_ApplyParameters_UpdatesViewModelDateRange()
    {
        TestThemeHelper.EnsureOffice2019Colorful();

        using var provider = IntegrationTestServices.BuildProvider();
        using var scope = provider.CreateScope();

        var viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.ViewModels.ReportsViewModel>(scope.ServiceProvider);
        var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WileyWidget.WinForms.Factories.SyncfusionControlFactory>(scope.ServiceProvider);

        using var hostForm = new Form
        {
            Width = 1400,
            Height = 900,
            StartPosition = FormStartPosition.Manual,
            Left = -32000,
            Top = -32000,
        };

        using var panel = new ReportsPanel(viewModel, factory)
        {
            Dock = DockStyle.Fill,
        };

        hostForm.Controls.Add(panel);
        hostForm.Show();
        panel.CreateControl();
        Application.DoEvents();

        var parametersGrid = GetPrivateField<Control>(panel, "_parametersGrid");
        var dataSourceProperty = parametersGrid.GetType().GetProperty("DataSource", BindingFlags.Instance | BindingFlags.Public);

        dataSourceProperty.Should().NotBeNull();

        var fromDate = new DateTime(2024, 1, 15);
        var toDate = new DateTime(2024, 12, 20);

        dataSourceProperty!.SetValue(parametersGrid, new List<ReportParameter>
        {
            new() { Name = "FromDate", Value = fromDate.ToString("yyyy-MM-dd"), Type = "Date" },
            new() { Name = "ToDate", Value = toDate.ToString("yyyy-MM-dd"), Type = "Date" },
        });

        var applyParametersMethod = typeof(ReportsPanel).GetMethod("ApplyParametersAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        applyParametersMethod.Should().NotBeNull();

        var task = applyParametersMethod!.Invoke(panel, new object[] { default(System.Threading.CancellationToken) }) as Task;
        task.Should().NotBeNull();

        await task!;

        viewModel.FromDate.Should().Be(fromDate);
        viewModel.ToDate.Should().Be(toDate);
        viewModel.Parameters.Should().ContainKey("FromDate");
        viewModel.Parameters.Should().ContainKey("ToDate");
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
        where T : class
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.Should().NotBeNull($"field {fieldName} should exist on {instance.GetType().Name}");
        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"field {fieldName} should be initialized");
        return value!;
    }
}
