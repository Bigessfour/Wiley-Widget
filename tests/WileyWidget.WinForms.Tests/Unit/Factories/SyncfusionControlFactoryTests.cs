using System.Drawing;
using System.Reflection;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Drawing;
using Syncfusion.Data;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using WileyWidget.WinForms.Factories;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Factories;

public class SyncfusionControlFactoryTests
{
    [StaFact]
    public void CreateSfButton_DefaultProfile_DefinesStableButtonBoundaries()
    {
        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance);

        using var button = factory.CreateSfButton("Export Chat");

        button.AutoSize.Should().BeFalse();
        button.MinimumSize.Should().Be(new Size(96, 34));
        button.Size.Height.Should().Be(34);
        button.Size.Width.Should().BeGreaterThanOrEqualTo(button.MinimumSize.Width);
        button.Margin.Should().Be(new Padding(0, 0, 8, 8));
        button.Padding.Should().Be(new Padding(12, 0, 12, 0));
        button.TextMargin.Should().Be(new Padding(8, 0, 8, 0));
        button.ImageMargin.Should().Be(new Padding(4, 0, 4, 0));
        button.AccessibleDescription.Should().Be("Export Chat button");
        button.CanOverrideStyle.Should().BeFalse();
        button.AllowImageAnimation.Should().BeFalse();
        button.ImageSize.Should().Be(Size.Empty);
        button.ImageLayout.Should().Be(System.Windows.Forms.ImageLayout.None);
        GetOptionalProperty<bool>(button, "UseCompatibleTextRendering")?.Should().BeTrue();
        button.Style.Should().NotBeNull();
        button.AllowWrapText.Should().BeFalse();
        button.AutoEllipsis.Should().BeTrue();
        button.FocusRectangleVisible.Should().BeTrue();
    }

    [StaFact]
    public void CreateSfButton_ToolbarProfile_ExpandsForLongerLabelsWithinSharedBounds()
    {
        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance);

        using var button = factory.CreateSfButton(
            "Open Reply",
            layoutProfile: SyncfusionControlFactory.SfButtonLayoutProfile.Toolbar);

        button.MinimumSize.Should().Be(new Size(104, 34));
        button.Size.Height.Should().Be(34);
        button.Size.Width.Should().BeGreaterThanOrEqualTo(104);
        button.Size.Width.Should().BeLessThanOrEqualTo(196);
        button.Margin.Should().Be(new Padding(0, 0, 6, 6));
        button.Padding.Should().Be(new Padding(12, 0, 12, 0));
        button.TextMargin.Should().Be(new Padding(8, 0, 8, 0));
        button.ImageMargin.Should().Be(new Padding(4, 0, 4, 0));
        button.AllowWrapText.Should().BeFalse();
        button.AutoEllipsis.Should().BeTrue();
        button.FocusRectangleVisible.Should().BeTrue();
    }

    [StaFact]
    public void CreateSfDataGrid_Defaults_UseSharedGridBehavior()
    {
        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance);

        using var grid = factory.CreateSfDataGrid();

        grid.Dock.Should().Be(DockStyle.Fill);
        grid.AccessibleName.Should().Be("Data Grid");
        grid.AccessibleDescription.Should().Be("Tabular data grid");
        grid.AllowEditing.Should().BeTrue();
        grid.AllowFiltering.Should().BeTrue();
        grid.AllowSorting.Should().BeTrue();
        grid.AutoGenerateColumns.Should().BeTrue();
        grid.SelectionMode.Should().Be(GridSelectionMode.Single);
        grid.RowHeight.Should().Be(24);
        grid.HeaderRowHeight.Should().Be(32);
        grid.ValidationMode.Should().Be(GridValidationMode.InView);
        grid.ShowErrorIcon.Should().BeTrue();
        grid.ShowValidationErrorToolTip.Should().BeTrue();
        grid.CopyOption.Should().Be(CopyOptions.CopyData);
        grid.PasteOption.Should().Be(PasteOptions.PasteData);
        grid.ShowBusyIndicator.Should().BeTrue();
        grid.UsePLINQ.Should().BeTrue();
        grid.LiveDataUpdateMode.Should().Be(LiveDataUpdateMode.AllowDataShaping);
        grid.NotificationSubscriptionMode.Should().Be(NotificationSubscriptionMode.CollectionChange);
        grid.ShowToolTip.Should().BeTrue();
        GetOptionalProperty<bool>(grid, "AllowDeleting")?.Should().BeTrue();
        GetOptionalProperty<bool>(grid, "AllowTriStateSorting")?.Should().BeTrue();
        GetOptionalProperty<bool>(grid, "AllowSelectionOnMouseDown")?.Should().BeTrue();
        GetOptionalProperty<bool>(grid, "ShowPreviewRow")?.Should().BeFalse();
        GetOptionalProperty<int>(grid, "IndentColumnWidth")?.Should().Be(20);
        GetOptionalProperty<bool>(grid, "HideEmptyGridViewDefinition")?.Should().BeFalse();
    }

    [StaFact]
    public void CreateRibbonControlAdv_Defaults_UseManagedOfficeChrome()
    {
        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance);

        using var ribbon = factory.CreateRibbonControlAdv("File");

        ribbon.Dock.Should().Be(DockStyleEx.Top);
        ribbon.MenuButtonText.Should().Be("File");
        ribbon.MenuButtonAutoSize.Should().BeFalse();
        ribbon.MenuButtonFont.Should().Be(new Font("Segoe UI", 9F, FontStyle.Regular));
        ribbon.ShowQuickItemsDropDownButton.Should().BeFalse();
        ribbon.ShowCaption.Should().BeTrue();
        ribbon.ShowLauncher.Should().BeTrue();
        ribbon.CaptionStyle.Should().Be(CaptionStyle.Bottom);
        ribbon.CaptionTextStyle.Should().Be(CaptionTextStyle.Plain);
        ribbon.RibbonStyle.Should().Be(RibbonStyle.Office2016);
        ribbon.OfficeColorScheme.Should().Be(ToolStripEx.ColorScheme.Managed);
        ribbon.BorderStyle.Should().Be(ToolStripBorderStyle.Etched);
        ribbon.QuickPanelAlignment.Should().Be(QuickPanelAlignment.Left);
        ribbon.DisplayOption.Should().Be(RibbonDisplayOption.ShowTabsAndCommands);
        ribbon.ShowMinimizeButton.Should().BeTrue();
        ribbon.AllowCollapse.Should().BeTrue();
        ribbon.EnableSimplifiedLayoutMode.Should().BeFalse();
        ribbon.LayoutMode.Should().Be(RibbonLayoutMode.Normal);
        ribbon.ShowRibbonDisplayOptionButton.Should().BeTrue();
        ribbon.QuickPanelVisible.Should().BeTrue();
        ribbon.ShowQuickPanelBelowRibbon.Should().BeFalse();
        ribbon.HideToolTip.Should().BeFalse();
        ribbon.TouchMode.Should().BeFalse();
        ribbon.EnableQATCustomization.Should().BeFalse();
        ribbon.EnableRibbonCustomization.Should().BeFalse();
        ribbon.EnableRibbonStateAccelerator.Should().BeTrue();
        ribbon.CanReduceCaptionLength.Should().BeTrue();
        ribbon.BackStageNavigationButtonStyle.Should().Be(BackStageNavigationButtonStyles.Touch);
        ribbon.MenuButtonVisible.Should().BeTrue();
        ribbon.AccessibleName.Should().Be("Application Ribbon");
        ribbon.AccessibleDescription.Should().Be("Application ribbon navigation and commands");
        GetOptionalProperty<bool>(ribbon, "MenuButtonEnabled")?.Should().BeTrue();
        GetOptionalProperty<bool>(ribbon, "ShowContextMenu")?.Should().BeFalse();
        GetOptionalProperty<bool>(ribbon, "BackStageNavigationButtonEnabled")?.Should().BeTrue();
        GetOptionalProperty<bool>(ribbon, "ShowQuickItemInQAT")?.Should().BeFalse();
        GetOptionalProperty<int>(ribbon, "MenuButtonWidth")?.Should().Be(40);
        GetOptionalProperty<Color>(ribbon, "TitleColor")?.Should().Be(SystemColors.ActiveCaptionText);
    }

    [StaFact]
    public void CreateSfComboBox_Defaults_UseDropDownListBehavior()
    {
        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance);

        using var comboBox = factory.CreateSfComboBox();

        comboBox.Width.Should().Be(200);
        comboBox.Height.Should().Be(28);
        comboBox.DropDownStyle.Should().Be(Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList);
        comboBox.AutoCompleteMode.Should().Be(AutoCompleteMode.SuggestAppend);
        comboBox.MaxDropDownItems.Should().Be(10);
        comboBox.AllowDropDownResize.Should().BeFalse();
        comboBox.AccessibleName.Should().Be("Combo Box");
    }

    [StaFact]
    public void CreateProgressBarAdv_Defaults_UseMetroWaitingSurface()
    {
        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance);

        using var progressBar = factory.CreateProgressBarAdv();

        progressBar.Size.Should().Be(new Size(200, 16));
        progressBar.ProgressStyle.Should().Be(ProgressBarStyles.Metro);
        progressBar.Minimum.Should().Be(0);
        progressBar.Maximum.Should().Be(100);
        progressBar.Value.Should().Be(0);
        progressBar.AccessibleName.Should().Be("Progress Bar");
    }

    [StaFact]
    public void CreateChartControl_Defaults_UseBottomLegendAndAxisTitles()
    {
        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance);

        using var chart = factory.CreateChartControl("Revenue Snapshot");

        chart.Dock.Should().Be(DockStyle.Fill);
        chart.ShowLegend.Should().BeTrue();
        chart.Legend.Visible.Should().BeTrue();
        chart.Legend.Position.Should().Be(ChartDock.Bottom);
        chart.EnableXZooming.Should().BeFalse();
        chart.EnableYZooming.Should().BeFalse();
        chart.SmoothingMode.Should().Be(SmoothingMode.AntiAlias);
        chart.TextRenderingHint.Should().Be(TextRenderingHint.SystemDefault);
        chart.ElementsSpacing.Should().Be(0);
        chart.ChartAreaShadow.Should().BeFalse();
        chart.ChartInterior.Should().NotBeNull();
        chart.Title.Text.Should().Be("Revenue Snapshot");
        chart.PrimaryXAxis.Title.Should().Be("X Axis");
        chart.PrimaryYAxis.Title.Should().Be("Y Axis");
        chart.Palette.Should().Be(ChartColorPalette.Office2016);
        GetOptionalProperty<bool>(chart, "ShowToolTips")?.Should().BeTrue();
        GetOptionalProperty<bool>(chart, "ShowContextMenu")?.Should().BeFalse();
        GetOptionalProperty<bool>(chart, "DisplayChartContextMenu")?.Should().BeFalse();
        GetOptionalProperty<bool>(chart, "DisplaySeriesContextMenu")?.Should().BeFalse();
        GetOptionalProperty<bool>(chart, "ShowContextMenuInLegend")?.Should().BeFalse();
        GetOptionalProperty<bool>(chart, "AutoHighlight")?.Should().BeFalse();
        GetOptionalProperty<bool>(chart, "SeriesHighlight")?.Should().BeFalse();
        GetOptionalProperty<bool>(chart, "AllowGapForEmptyPoints")?.Should().BeFalse();
        GetOptionalProperty<bool>(chart, "IsPanningEnabled")?.Should().BeFalse();
        GetOptionalProperty<bool>(chart, "Series3D")?.Should().BeFalse();
        GetOptionalProperty<int>(chart, "Rotation")?.Should().Be(0);
        GetOptionalProperty<int>(chart, "Tilt")?.Should().Be(0);
        GetOptionalProperty<int>(chart, "Depth")?.Should().Be(0);
        GetOptionalProperty<int>(chart, "RoundingPlaces")?.Should().Be(0);
    }

    [StaFact]
    public void CreateSfAIAssistView_Defaults_StartWithTypingIndicatorHidden()
    {
        var factory = new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance);

        using var assistView = factory.CreateSfAIAssistView();

        assistView.Dock.Should().Be(DockStyle.Fill);
        assistView.AccessibleName.Should().Be("AI Assist View");
        assistView.AccessibleDescription.Should().Be("AI chat and response surface");
        assistView.ShowTypingIndicator.Should().BeFalse();
        GetOptionalProperty<bool>(assistView, "EnableStopResponding")?.Should().BeTrue();
        GetOptionalProperty<bool>(assistView, "IsResponseToolbarVisible")?.Should().BeTrue();
    }

    private static T? GetOptionalProperty<T>(object instance, string propertyName)
        where T : struct
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
        {
            return null;
        }

        return property.GetValue(instance) is T value ? value : null;
    }
}
