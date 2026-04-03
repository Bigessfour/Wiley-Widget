using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.Data;
using Syncfusion.WinForms.AIAssistView;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Controls.Styles;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.ListView;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.Windows.Forms.PdfViewer;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Factories;

/// <summary>
/// Factory for creating fully-configured Syncfusion controls with ALL required properties.
/// MANDATORY USAGE: This factory ensures no partial/incomplete control implementations.
/// Per workspace rules (Syncfusion API Rule):
/// - ALL Syncfusion API properties must be configured
/// - Theme integration is mandatory
/// </summary>
public class SyncfusionControlFactory
{
    public enum SfButtonLayoutProfile
    {
        Standard,
        Toolbar,
        Compact,
        HeaderAction,
    }

    private readonly record struct SfButtonLayoutSpec(
        int MinimumWidth,
        int PreferredWidth,
        int MaximumWidth,
        int Height,
        Padding Margin,
        Padding Padding,
        Padding TextMargin,
        Padding ImageMargin,
        bool AccessibilityEnabled,
        bool AllowRichText,
        bool AllowWrapText,
        bool AutoEllipsis,
        bool FocusRectangleVisible);

    private readonly ILogger<SyncfusionControlFactory> _logger;
    private readonly IThemeService? _themeService;

    private string CurrentTheme => _themeService?.CurrentTheme
        ?? SfSkinManager.ApplicationVisualTheme
        ?? "Office2019Colorful";

    public SyncfusionControlFactory(ILogger<SyncfusionControlFactory> logger, IThemeService? themeService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _themeService = themeService;
    }

    #region SfDataGrid

    public SfDataGrid CreateSfDataGrid(Action<SfDataGrid>? configure = null)
    {
        _logger.LogDebug("Creating SfDataGrid");

        var grid = new SfDataGrid();
        ApplySfDataGridDefaults(grid);

        grid.ApplySyncfusionTheme(CurrentTheme, _logger);
        grid.PreventStringRelationalFilters(_logger);
        configure?.Invoke(grid);

        _logger.LogInformation("SfDataGrid created: Theme={Theme}", grid.ThemeName);
        return grid;
    }

    #endregion

    #region SfButton

    public SfButton CreateSfButton(
        string text,
        Action<SfButton>? configure = null,
        SfButtonLayoutProfile layoutProfile = SfButtonLayoutProfile.Standard)
    {
        _logger.LogDebug("Creating SfButton: {Text}", text);

        var button = new SfButton
        {
            AccessibleName = "Button",
            AccessibleDescription = $"{text} button",
            Text = text,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            AutoSize = false,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            TextAlign = ContentAlignment.MiddleCenter,
            ImageAlign = ContentAlignment.MiddleLeft,
        };

        ApplySfButtonLayout(button, layoutProfile, text);
        button.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(button);
        return button;
    }

    public void ApplySfButtonLayout(
        SfButton button,
        SfButtonLayoutProfile layoutProfile,
        string? textOverride = null)
    {
        ArgumentNullException.ThrowIfNull(button);

        var spec = GetSfButtonLayoutSpec(layoutProfile);
        var recommendedWidth = GetRecommendedButtonWidth(textOverride ?? button.Text, button.Font, spec);

        button.AutoSize = false;
        button.Margin = spec.Margin;
        button.Padding = spec.Padding;
        button.MinimumSize = new Size(spec.MinimumWidth, spec.Height);
        button.Size = new Size(recommendedWidth, spec.Height);
        button.AccessibilityEnabled = spec.AccessibilityEnabled;
        button.CanOverrideStyle = false;
        button.AllowRichText = spec.AllowRichText;
        button.AllowWrapText = spec.AllowWrapText;
        button.AutoEllipsis = spec.AutoEllipsis;
        button.FocusRectangleVisible = spec.FocusRectangleVisible;
        button.AllowImageAnimation = false;
        button.Style ??= new ButtonVisualStyle();
        button.ImageSize = Size.Empty;
        button.ImageLayout = ImageLayout.None;
        SetOptionalProperty(button, "UseCompatibleTextRendering", true);
        button.TextMargin = spec.TextMargin;
        button.ImageMargin = spec.ImageMargin;
    }

    private static SfButtonLayoutSpec GetSfButtonLayoutSpec(SfButtonLayoutProfile layoutProfile)
        => layoutProfile switch
        {
            SfButtonLayoutProfile.Toolbar => new SfButtonLayoutSpec(
                MinimumWidth: 104,
                PreferredWidth: 112,
                MaximumWidth: 196,
                Height: 34,
                Margin: new Padding(0, 0, 6, 6),
                Padding: new Padding(12, 0, 12, 0),
                TextMargin: new Padding(8, 0, 8, 0),
                ImageMargin: new Padding(4, 0, 4, 0),
                AccessibilityEnabled: true,
                AllowRichText: false,
                AllowWrapText: false,
                AutoEllipsis: true,
                FocusRectangleVisible: true),
            SfButtonLayoutProfile.Compact => new SfButtonLayoutSpec(
                MinimumWidth: 72,
                PreferredWidth: 88,
                MaximumWidth: 156,
                Height: 32,
                Margin: new Padding(0, 0, 6, 6),
                Padding: new Padding(8, 0, 8, 0),
                TextMargin: new Padding(6, 0, 6, 0),
                ImageMargin: new Padding(4, 0, 4, 0),
                AccessibilityEnabled: true,
                AllowRichText: false,
                AllowWrapText: false,
                AutoEllipsis: true,
                FocusRectangleVisible: true),
            SfButtonLayoutProfile.HeaderAction => new SfButtonLayoutSpec(
                MinimumWidth: 40,
                PreferredWidth: 80,
                MaximumWidth: 120,
                Height: 32,
                Margin: new Padding(6, 6, 6, 6),
                Padding: new Padding(10, 0, 10, 0),
                TextMargin: new Padding(6, 0, 6, 0),
                ImageMargin: new Padding(4, 0, 4, 0),
                AccessibilityEnabled: true,
                AllowRichText: false,
                AllowWrapText: false,
                AutoEllipsis: true,
                FocusRectangleVisible: true),
            _ => new SfButtonLayoutSpec(
                MinimumWidth: 96,
                PreferredWidth: 120,
                MaximumWidth: 220,
                Height: 34,
                Margin: new Padding(0, 0, 8, 8),
                Padding: new Padding(12, 0, 12, 0),
                TextMargin: new Padding(8, 0, 8, 0),
                ImageMargin: new Padding(4, 0, 4, 0),
                AccessibilityEnabled: true,
                AllowRichText: false,
                AllowWrapText: false,
                AutoEllipsis: true,
                FocusRectangleVisible: true),
        };

    private static int GetRecommendedButtonWidth(string? text, Font? font, SfButtonLayoutSpec spec)
    {
        var measuredWidth = TextRenderer.MeasureText(
            text ?? string.Empty,
            font ?? SystemFonts.MessageBoxFont,
            Size.Empty,
            TextFormatFlags.SingleLine).Width;

        var paddedWidth = measuredWidth + spec.Padding.Horizontal + spec.TextMargin.Horizontal + 18;
        var preferredWidth = Math.Max(spec.PreferredWidth, paddedWidth);
        return Math.Min(spec.MaximumWidth, Math.Max(spec.MinimumWidth, preferredWidth));
    }

    #endregion

    #region ChartControl

    public ChartControl CreateChartControl(string title, Action<ChartControl>? configure = null)
    {
        _logger.LogDebug("Creating ChartControl: {Title}", title);

        var chart = new ChartControl();
        ApplyChartDefaults(chart, title, "X Axis", "Y Axis", showLegend: true);

        chart.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(chart);
        return chart;
    }

    /// <summary>Creates a chart configured for reserve projections (alias entry point).</summary>
    public ChartControl CreateSfChart(string title, Action<ChartControl>? configure = null)
    {
        _logger.LogDebug("Creating SfChart wrapper: {Title}", title);

        var chart = new ChartControl();
        ApplyChartDefaults(chart, title, "Fiscal Year", "Projected Reserves", showLegend: true);

        chart.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(chart);
        return chart;
    }

    #endregion

    #region TabControlAdv

    public TabControlAdv CreateTabControlAdv(Action<TabControlAdv>? configure = null)
    {
        _logger.LogDebug("Creating TabControlAdv");

        var tabControl = new TabControlAdv();
        ApplyTabControlAdvDefaults(tabControl);

        tabControl.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(tabControl);
        return tabControl;
    }

    #endregion

    #region RibbonControlAdv

    public RibbonControlAdv CreateRibbonControlAdv(string menuButtonText, Action<RibbonControlAdv>? configure = null)
    {
        _logger.LogDebug("Creating RibbonControlAdv");

        var ribbon = new RibbonControlAdv();
        ApplyRibbonControlAdvDefaults(ribbon, menuButtonText);

        ribbon.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(ribbon);
        return ribbon;
    }

    #endregion

    #region SfListView

    public SfListView CreateSfListView(Action<SfListView>? configure = null)
    {
        _logger.LogDebug("Creating SfListView");

        var listView = new SfListView();
        ApplySfListViewDefaults(listView);

        listView.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(listView);
        return listView;
    }

    #endregion

    #region AutoComplete

    public AutoComplete CreateAutoComplete(Action<AutoComplete>? configure = null)
    {
        _logger.LogDebug("Creating AutoComplete");

        var autoComplete = new AutoComplete();
        ApplyAutoCompleteDefaults(autoComplete);

        configure?.Invoke(autoComplete);
        return autoComplete;
    }

    #endregion

    #region PdfViewerControl

    public PdfViewerControl CreatePdfViewerControl(Action<PdfViewerControl>? configure = null)
    {
        _logger.LogDebug("Creating PdfViewerControl");

        var pdfViewer = new PdfViewerControl();
        ApplyPdfViewerDefaults(pdfViewer);

        pdfViewer.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(pdfViewer);
        return pdfViewer;
    }

    #endregion

    #region TextBoxExt

    public TextBoxExt CreateTextBoxExt(Action<TextBoxExt>? configure = null)
    {
        _logger.LogDebug("Creating TextBoxExt");

        var textBox = new TextBoxExt();
        ApplyTextBoxExtDefaults(textBox);

        textBox.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(textBox);
        return textBox;
    }

    #endregion

    #region SfComboBox

    public SfComboBox CreateSfComboBox(Action<SfComboBox>? configure = null)
    {
        _logger.LogDebug("Creating SfComboBox");

        var comboBox = new SfComboBox();
        ApplySfComboBoxDefaults(comboBox);

        comboBox.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(comboBox);
        return comboBox;
    }

    #endregion

    #region SfDateTimeEdit

    public SfDateTimeEdit CreateSfDateTimeEdit(Action<SfDateTimeEdit>? configure = null)
    {
        _logger.LogDebug("Creating SfDateTimeEdit");

        var dateTimeEdit = new SfDateTimeEdit();
        ApplySfDateTimeEditDefaults(dateTimeEdit);

        dateTimeEdit.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(dateTimeEdit);
        return dateTimeEdit;
    }

    #endregion

    #region SfNumericTextBox

    public SfNumericTextBox CreateSfNumericTextBox(Action<SfNumericTextBox>? configure = null)
    {
        _logger.LogDebug("Creating SfNumericTextBox");

        var numericTextBox = new SfNumericTextBox();
        ApplySfNumericTextBoxDefaults(numericTextBox);

        numericTextBox.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(numericTextBox);
        return numericTextBox;
    }

    #endregion

    #region CheckBoxAdv

    public CheckBoxAdv CreateCheckBoxAdv(string text, Action<CheckBoxAdv>? configure = null)
    {
        _logger.LogDebug("Creating CheckBoxAdv: {Text}", text);

        var checkBox = new CheckBoxAdv();
        ApplyCheckBoxAdvDefaults(checkBox, text);

        checkBox.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(checkBox);
        return checkBox;
    }

    #endregion

    #region SplitContainerAdv

    public SplitContainerAdv CreateSplitContainerAdv(Action<SplitContainerAdv>? configure = null)
    {
        _logger.LogDebug("Creating SplitContainerAdv");

        var splitContainer = new SplitContainerAdv();
        ApplySplitContainerAdvDefaults(splitContainer);

        splitContainer.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(splitContainer);
        return splitContainer;
    }

    #endregion

    #region ProgressBarAdv

    public ProgressBarAdv CreateProgressBarAdv(Action<ProgressBarAdv>? configure = null)
    {
        _logger.LogDebug("Creating ProgressBarAdv");

        var progressBar = new ProgressBarAdv();
        ApplyProgressBarAdvDefaults(progressBar);

        progressBar.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(progressBar);
        return progressBar;
    }

    #endregion

    #region SfAIAssistView

    public SfAIAssistView CreateSfAIAssistView(Action<SfAIAssistView>? configure = null)
    {
        _logger.LogDebug("Creating SfAIAssistView");

        var assistView = new SfAIAssistView();
        ApplySfAIAssistViewDefaults(assistView);

        assistView.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(assistView);
        return assistView;
    }

    #endregion

    #region DockingManager

    public DockingManager CreateDockingManager(Form hostForm, Control hostControl, Action<DockingManager>? configure = null)
    {
        _logger.LogDebug("Creating DockingManager for {FormType}", hostForm.GetType().Name);

        var hostContainer = hostControl as ContainerControl ?? hostForm;

        DockingManager dockingManager;
        try
        {
            // Prefer constructor-time host binding so DockingManager computes layout from the
            // intended host container immediately (avoids defaulting to form-level client bounds).
            dockingManager = Activator.CreateInstance(typeof(DockingManager), hostContainer) as DockingManager
                ?? new DockingManager();
        }
        catch
        {
            dockingManager = new DockingManager();
        }

        ApplyDockingManagerDefaults(dockingManager, hostForm, hostContainer);

        configure?.Invoke(dockingManager);
        return dockingManager;
    }

    #endregion

    #region RichTextBoxExt

    public RichTextBox CreateRichTextBoxExt(Action<RichTextBox>? configure = null)
    {
        _logger.LogDebug("Creating RichTextBoxExt");

        var rtb = new RichTextBox();
        ApplyRichTextBoxDefaults(rtb);

        rtb.ApplySyncfusionTheme(CurrentTheme, _logger);
        configure?.Invoke(rtb);
        return rtb;
    }

    #endregion

    #region LoadingOverlay

    public LoadingOverlay CreateLoadingOverlay(Action<LoadingOverlay>? configure = null)
    {
        _logger.LogDebug("Creating LoadingOverlay");

        var overlay = new LoadingOverlay();
        ApplyLoadingOverlayDefaults(overlay);

        configure?.Invoke(overlay);
        return overlay;
    }

    #endregion

    // ── Static helpers (used by EnterpriseVitalSignsPanel without DI) ─────

    // ── Static helpers (used by EnterpriseVitalSignsPanel without DI) ─────

    /// <summary>
    /// Creates a RadialGauge showing break-even ratio for an enterprise.
    /// </summary>
    public RadialGauge CreateCircularGauge(double currentRatio, string enterpriseName)
    {
        _logger.LogDebug("Creating CircularGauge: {Enterprise} = {Ratio:F1}%", enterpriseName, currentRatio);

        var gauge = new RadialGauge
        {
            Dock = DockStyle.Fill,
            AccessibleName = $"{enterpriseName} break-even gauge",
            AccessibleDescription = "Radial gauge showing the current fiscal year break-even ratio",
            Value = (float)currentRatio,
            MinimumValue = 0,
            MaximumValue = 150,
            MajorDifference = 10F,
            MinorDifference = 2F,
            ShowTicks = true,
            ShowScaleLabel = true,
            NeedleStyle = NeedleStyle.Pointer,
            ReadOnly = true,
            VisualStyle = ThemeStyle.Silver,
            GaugeLableColor = Color.Black,
            ThemeName = CurrentTheme,
            GaugeLabel = $"{currentRatio:F1}%",
        };

        gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
        {
            StartValue = 0,
            EndValue = 99.9f,
            Color = Color.Red,
            InRange = true,
            Height = 10,
        });
        gauge.Ranges.Add(new Syncfusion.Windows.Forms.Gauge.Range
        {
            StartValue = 100,
            EndValue = 150,
            Color = Color.Green,
            InRange = true,
            Height = 10,
        });

        gauge.ApplySyncfusionTheme(CurrentTheme, _logger);

        _logger.LogInformation("CircularGauge created for {Enterprise}", enterpriseName);
        return gauge;
    }

    /// <summary>
    /// Creates a fully composed enterprise gauge container including title, radial gauge, and value label.
    /// </summary>
    public Control CreateEnterpriseGauge(double currentRatio, string enterpriseName)
    {
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            AccessibleName = $"{enterpriseName} gauge summary",
        };

        container.SuspendLayout();
        try
        {
            var titleLabel = new Label
            {
                Text = enterpriseName,
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            var subtitleLabel = new Label
            {
                Text = "Break-even ratio",
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                AccessibleName = $"{enterpriseName} gauge subtitle",
            };

            var valueLabel = new Label
            {
                Text = $"{currentRatio:F1}%",
                Dock = DockStyle.Bottom,
                Height = 32,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = currentRatio >= 100 ? ThemeColors.Success : ThemeColors.Error,
            };

            var gauge = CreateCircularGauge(currentRatio, enterpriseName);

            gauge.SuspendLayout();
            try
            {
                container.Controls.Add(gauge);
                container.Controls.Add(valueLabel);
                container.Controls.Add(subtitleLabel);
                container.Controls.Add(titleLabel);
                container.ApplySyncfusionTheme(CurrentTheme, _logger);
            }
            finally
            {
                gauge.ResumeLayout(true);
            }
        }
        finally
        {
            container.ResumeLayout(true);
        }

        _logger.LogDebug("Created enterprise gauge container for {Enterprise}", enterpriseName);
        return container;
    }

    /// <summary>
    /// Creates a ChartControl for one enterprise. Uses a 12-point fiscal trend when monthly data is available;
    /// otherwise falls back to a single-snapshot comparison chart.
    /// </summary>
    public ChartControl CreateEnterpriseChart(EnterpriseSnapshot snapshot)
    {
        _logger.LogDebug("Creating EnterpriseChart: {Enterprise}", snapshot.Name);

        var chart = new ChartControl();
        chart.SuspendLayout();
        try
        {
            ChartControlDefaults.Apply(chart, new ChartControlDefaults.Options
            {
                TransparentChartArea = true,
                EnableZooming = false,
                EnableAxisScrollBar = false,
            }, _logger);
            ApplyChartDefaults(
                chart,
                $"{snapshot.Name} — Current FY Snapshot",
                "Fiscal Year",
                "Amount",
                showLegend: false,
                accessibleName: $"{snapshot.Name} current fiscal year chart",
                accessibleDescription: "Chart showing current fiscal year revenue, expenses, net position, and break-even reference");

            var monthlyTrend = snapshot.MonthlyTrend
                .OrderBy(point => point.MonthStart)
                .ToList();

            if (monthlyTrend.Count > 0)
            {
                chart.Title.Text = $"{snapshot.Name} — 12-Month Fiscal Trend";
                chart.PrimaryXAxis.Title = "Month";
                chart.PrimaryYAxis.Title = "Amount";
                chart.PrimaryXAxis.LabelRotate = true;
                chart.PrimaryXAxis.LabelRotateAngle = 45;
                chart.PrimaryXAxis.DrawGrid = true;
                chart.PrimaryYAxis.DrawGrid = true;

                var revenueSeries = new ChartSeries("Revenue", ChartSeriesType.Line);
                revenueSeries.Style.Border.Width = 2;
                revenueSeries.Style.Symbol.Size = new Size(8, 8);
                revenueSeries.PointsToolTipFormat = "{1:C0}";

                var expenseSeries = new ChartSeries("Expenses", ChartSeriesType.Line);
                expenseSeries.Style.Border.Width = 2;
                expenseSeries.Style.Border.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                expenseSeries.Style.Symbol.Size = new Size(8, 8);
                expenseSeries.PointsToolTipFormat = "{1:C0}";

                var monthlyNetSeries = new ChartSeries("Net Position", ChartSeriesType.Column);
                monthlyNetSeries.PointsToolTipFormat = "{1:C0}";

                for (int index = 0; index < monthlyTrend.Count; index++)
                {
                    var point = monthlyTrend[index];
                    var chartLabel = $"{index + 1:00} {point.MonthStart:MMM}";

                    revenueSeries.Points.Add(chartLabel, (double)point.Revenue);
                    expenseSeries.Points.Add(chartLabel, (double)point.Expenses);
                    monthlyNetSeries.Points.Add(chartLabel, (double)point.NetPosition);
                }

                chart.Series.Add(revenueSeries);
                chart.Series.Add(expenseSeries);
                chart.Series.Add(monthlyNetSeries);

                chart.ApplySyncfusionTheme(CurrentTheme, _logger);

                _logger.LogInformation("EnterpriseChart created for {Enterprise} using 12-point fiscal trend", snapshot.Name);
                return chart;
            }

            var fiscalYear = DateTime.Now.Year;

            var revSeries = new ChartSeries("Revenue", ChartSeriesType.Column);
            revSeries.Points.Add(fiscalYear, (double)snapshot.Revenue);
            chart.Series.Add(revSeries);

            var expSeries = new ChartSeries("Expenses", ChartSeriesType.Column);
            expSeries.Points.Add(fiscalYear, (double)snapshot.Expenses);
            chart.Series.Add(expSeries);

            var netSeries = new ChartSeries("Net Position", ChartSeriesType.Area);
            netSeries.Points.Add(fiscalYear, (double)snapshot.NetPosition);
            chart.Series.Add(netSeries);

            var breakEvenSeries = new ChartSeries("Break Even", ChartSeriesType.Line);
            breakEvenSeries.Points.Add(fiscalYear, (double)snapshot.Expenses);
            breakEvenSeries.Style.Border.Width = 3;
            breakEvenSeries.Style.Border.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            chart.Series.Add(breakEvenSeries);

            chart.ApplySyncfusionTheme(CurrentTheme, _logger);

            _logger.LogInformation("EnterpriseChart created for {Enterprise}", snapshot.Name);
            return chart;
        }
        finally
        {
            chart.ResumeLayout(true);
        }
    }

    /// <summary>
    /// Creates a full multi-series enterprise chart alias for dashboard usage.
    /// </summary>
    public ChartControl CreateEnterpriseSnapshotChart(EnterpriseSnapshot snapshot)
    {
        var chart = CreateEnterpriseChart(snapshot);
        _logger.LogDebug("Created enterprise snapshot chart for {Enterprise}", snapshot.Name);
        return chart;
    }

    private void ApplySfDataGridDefaults(SfDataGrid grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AccessibleName = "Data Grid";
        grid.AccessibleDescription = "Tabular data grid";
        grid.AllowEditing = true;
        grid.AllowFiltering = true;
        grid.AllowSorting = true;
        grid.AllowResizingColumns = true;
        grid.AllowResizingHiddenColumns = true;
        grid.AllowDraggingColumns = true;
        grid.AllowGrouping = false;
        grid.SelectionMode = GridSelectionMode.Single;
        grid.AutoGenerateColumns = true;
        grid.AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells;
        grid.EditorSelectionBehavior = EditorSelectionBehavior.SelectAll;
        grid.AddNewRowPosition = RowPosition.Bottom;
        grid.FilterRowPosition = RowPosition.Top;
        grid.HeaderRowHeight = 32;
        grid.NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Cell;
        // Keep row content visible while users live-resize columns.
        // Panels with very large data sets can opt back in explicitly where the performance tradeoff is worth it.
        grid.EnableDataVirtualization = false;
        grid.LiveDataUpdateMode = LiveDataUpdateMode.AllowDataShaping;
        grid.NotificationSubscriptionMode = NotificationSubscriptionMode.CollectionChange;
        grid.ValidationMode = GridValidationMode.InView;
        grid.ShowErrorIcon = true;
        grid.ShowValidationErrorToolTip = true;
        grid.CopyOption = CopyOptions.CopyData;
        grid.PasteOption = PasteOptions.PasteData;
        grid.ShowBusyIndicator = true;
        grid.UsePLINQ = true;
        grid.RowHeight = 24;
        grid.ShowGroupDropArea = false;
        grid.ShowRowHeader = false;
        grid.ShowToolTip = true;
        SetOptionalProperty(grid, "AllowDeleting", true);
        SetOptionalProperty(grid, "AllowTriStateSorting", true);
        SetOptionalProperty(grid, "AllowSelectionOnMouseDown", true);
        SetOptionalProperty(grid, "ShowPreviewRow", false);
        SetOptionalProperty(grid, "IndentColumnWidth", 20);
        SetOptionalProperty(grid, "HideEmptyGridViewDefinition", false);
    }

    private void ApplyChartDefaults(
        ChartControl chart,
        string title,
        string xAxisTitle,
        string yAxisTitle,
        bool showLegend,
        string? accessibleName = null,
        string? accessibleDescription = null)
    {
        chart.Dock = DockStyle.Fill;
        chart.AccessibleName = accessibleName ?? title;
        chart.AccessibleDescription = accessibleDescription ?? $"Chart for {title}";
        chart.ShowLegend = showLegend;
        chart.EnableMouseRotation = false;
        chart.AllowGradientPalette = true;
        chart.Palette = ChartColorPalette.Office2016;
        chart.CustomPalette = null;
        chart.EnableXZooming = false;
        chart.EnableYZooming = false;
        chart.SmoothingMode = SmoothingMode.AntiAlias;
        chart.TextRenderingHint = TextRenderingHint.SystemDefault;
        chart.ElementsSpacing = 0;
        chart.ChartAreaShadow = false;
        chart.ChartInterior = new BrushInfo(Color.White);
        chart.ChartAreaBackImage = null;
        chart.ChartInteriorBackImage = null;
        chart.Spacing = 10;
        chart.SpacingBetweenSeries = 10;
        chart.SpacingBetweenPoints = 10;
        chart.Title.Text = title;
        chart.Legend.Visible = showLegend;
        chart.Legend.Position = ChartDock.Bottom;
        chart.PrimaryXAxis.Title = xAxisTitle;
        chart.PrimaryYAxis.Title = yAxisTitle;
        chart.PrimaryXAxis.TitleFont = new Font("Segoe UI", 9F, FontStyle.Regular);
        chart.PrimaryYAxis.TitleFont = new Font("Segoe UI", 9F, FontStyle.Regular);
        SetOptionalProperty(chart, "ShowToolTips", true);
        SetOptionalProperty(chart, "ShowContextMenu", false);
        SetOptionalProperty(chart, "DisplayChartContextMenu", false);
        SetOptionalProperty(chart, "DisplaySeriesContextMenu", false);
        SetOptionalProperty(chart, "ShowContextMenuInLegend", false);
        SetOptionalProperty(chart, "AutoHighlight", false);
        SetOptionalProperty(chart, "SeriesHighlight", false);
        SetOptionalProperty(chart, "AllowGapForEmptyPoints", false);
        SetOptionalProperty(chart, "IsPanningEnabled", false);
        SetOptionalProperty(chart, "Series3D", false);
        SetOptionalProperty(chart, "Rotation", 0);
        SetOptionalProperty(chart, "Tilt", 0);
        SetOptionalProperty(chart, "Depth", 0);
        SetOptionalProperty(chart, "RoundingPlaces", 0);
    }

    private void ApplyTabControlAdvDefaults(TabControlAdv tabControl)
    {
        tabControl.Dock = DockStyle.Fill;
        tabControl.AccessibleName = "Tabbed Navigation";
        tabControl.TabStyle = typeof(TabRendererMetro);
        tabControl.Alignment = TabAlignment.Top;
        tabControl.SizeMode = Syncfusion.Windows.Forms.Tools.TabSizeMode.Fixed;
        tabControl.ItemSize = new Size(120, 32);
        tabControl.ThemeName = CurrentTheme;
    }

    private void ApplyRibbonControlAdvDefaults(RibbonControlAdv ribbon, string menuButtonText)
    {
        ribbon.Dock = DockStyleEx.Top;
        ribbon.AccessibleName = "Application Ribbon";
        ribbon.AccessibleDescription = "Application ribbon navigation and commands";
        ribbon.MenuButtonText = menuButtonText;
        ribbon.MenuButtonAutoSize = false;
        ribbon.MenuButtonFont = new Font("Segoe UI", 9F, FontStyle.Regular);
        ribbon.ShowQuickItemsDropDownButton = false;
        ribbon.ShowCaption = true;
        ribbon.ShowLauncher = true;
        ribbon.CaptionStyle = CaptionStyle.Bottom;
        ribbon.CaptionTextStyle = CaptionTextStyle.Plain;
        ribbon.TitleAlignment = TextAlignment.Left;
        ribbon.RibbonStyle = RibbonStyle.Office2016;
        ribbon.OfficeColorScheme = ToolStripEx.ColorScheme.Managed;
        ribbon.BorderStyle = ToolStripBorderStyle.Etched;
        ribbon.QuickPanelAlignment = QuickPanelAlignment.Left;
        ribbon.DisplayOption = RibbonDisplayOption.ShowTabsAndCommands;
        ribbon.ShowMinimizeButton = true;
        ribbon.AllowCollapse = true;
        ribbon.EnableSimplifiedLayoutMode = false;
        ribbon.LayoutMode = RibbonLayoutMode.Normal;
        ribbon.ShowRibbonDisplayOptionButton = true;
        ribbon.QuickPanelVisible = true;
        ribbon.ShowQuickPanelBelowRibbon = false;
        ribbon.HideToolTip = false;
        ribbon.TouchMode = false;
        ribbon.EnableQATCustomization = false;
        ribbon.EnableRibbonCustomization = false;
        ribbon.EnableRibbonStateAccelerator = true;
        ribbon.CanReduceCaptionLength = true;
        ribbon.BackStageNavigationButtonStyle = BackStageNavigationButtonStyles.Touch;
        ribbon.MenuButtonVisible = true;
        ribbon.CaptionFont = new Font("Segoe UI", 9F, FontStyle.Regular);
        ribbon.TitleFont = new Font("Segoe UI", 9F, FontStyle.Bold);
        ribbon.ThemeName = CurrentTheme;
        SetOptionalProperty(ribbon, "MenuButtonEnabled", true);
        SetOptionalProperty(ribbon, "ShowContextMenu", false);
        SetOptionalProperty(ribbon, "BackStageNavigationButtonEnabled", true);
        SetOptionalProperty(ribbon, "ShowQuickItemInQAT", false);
        SetOptionalProperty(ribbon, "MenuButtonWidth", 40);
        SetOptionalProperty(ribbon, "TitleColor", SystemColors.ActiveCaptionText);
    }

    private void ApplySfListViewDefaults(SfListView listView)
    {
        listView.Dock = DockStyle.Fill;
        listView.AccessibleName = "List View";
        listView.AccessibleDescription = "List view navigation and selection";
        listView.SelectionMode = SelectionMode.One;
        listView.HotTracking = true;
        listView.ItemHeight = 28;
        listView.ThemeName = CurrentTheme;
        listView.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
    }

    private void ApplyAutoCompleteDefaults(AutoComplete autoComplete)
    {
        autoComplete.AccessibleName = "Auto Complete";
        autoComplete.AccessibleDescription = "Auto-complete suggestions for text entry";
        autoComplete.IgnoreCase = true;
        autoComplete.AutoSortList = true;
        autoComplete.AutoSerialize = false;
        autoComplete.AutoAddItem = false;
        autoComplete.AdjustHeightToItemCount = false;
        autoComplete.ShowCloseButton = false;
        autoComplete.ShowGripper = false;
        autoComplete.MaxNumberofSuggestion = 12;
        autoComplete.ShowColumnHeader = true;
        autoComplete.BorderType = AutoCompleteBorderTypes.Sizable;
        autoComplete.ThemeName = CurrentTheme;
    }

    private void ApplyPdfViewerDefaults(PdfViewerControl pdfViewer)
    {
        pdfViewer.Dock = DockStyle.Fill;
        pdfViewer.AccessibleName = "PDF Viewer";
        pdfViewer.AccessibleDescription = "Document viewer for PDF content";
        pdfViewer.Name = "PdfViewerControl";
        pdfViewer.CursorMode = PdfViewerCursorMode.SelectTool;
    }

    private void ApplyTextBoxExtDefaults(TextBoxExt textBox)
    {
        textBox.AccessibleName = "Text Box";
        textBox.AccessibleDescription = "Single-line text entry";
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Size = new Size(200, 28);
        textBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        SetOptionalProperty(textBox, "CanOverrideStyle", true);
    }

    private void ApplySfComboBoxDefaults(SfComboBox comboBox)
    {
        comboBox.AccessibleName = "Combo Box";
        comboBox.AccessibleDescription = "Drop-down selection list";
        comboBox.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
        comboBox.Width = 200;
        comboBox.Height = 28;
        comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        comboBox.MaxDropDownItems = 10;
        comboBox.AllowDropDownResize = false;
        SetOptionalProperty(comboBox, "CanOverrideStyle", true);
        comboBox.ThemeName = CurrentTheme;
    }

    private void ApplySfDateTimeEditDefaults(SfDateTimeEdit dateTimeEdit)
    {
        dateTimeEdit.AccessibleName = "Date Time Picker";
        dateTimeEdit.AccessibleDescription = "Date and time selection control";
        dateTimeEdit.Width = 140;
        dateTimeEdit.DateTimePattern = Syncfusion.WinForms.Input.Enums.DateTimePattern.ShortDate;
        SetOptionalProperty(dateTimeEdit, "CanOverrideStyle", true);
        dateTimeEdit.ThemeName = CurrentTheme;
    }

    private void ApplySfNumericTextBoxDefaults(SfNumericTextBox numericTextBox)
    {
        numericTextBox.AccessibleName = "Numeric Text Box";
        numericTextBox.AccessibleDescription = "Numeric entry field";
        numericTextBox.Size = new Size(80, 24);
        numericTextBox.FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Numeric;
        numericTextBox.MinValue = 0D;
        numericTextBox.Value = 0D;
        SetOptionalProperty(numericTextBox, "CanOverrideStyle", true);
        numericTextBox.ThemeName = CurrentTheme;
    }

    private void ApplyCheckBoxAdvDefaults(CheckBoxAdv checkBox, string text)
    {
        checkBox.AccessibleName = text;
        checkBox.AccessibleDescription = text;
        checkBox.Text = text;
        checkBox.AutoSize = true;
        checkBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        SetOptionalProperty(checkBox, "CanOverrideStyle", true);
        checkBox.ThemeName = CurrentTheme;
    }

    private void ApplySplitContainerAdvDefaults(SplitContainerAdv splitContainer)
    {
        splitContainer.Dock = DockStyle.Fill;
        splitContainer.AccessibleName = "Split Container";
        splitContainer.AccessibleDescription = "Container for split-pane layouts";
        splitContainer.Orientation = Orientation.Horizontal;
        splitContainer.SplitterWidth = 6;
        splitContainer.ThemeName = CurrentTheme;
    }

    private void ApplyProgressBarAdvDefaults(ProgressBarAdv progressBar)
    {
        progressBar.AccessibleName = "Progress Bar";
        progressBar.AccessibleDescription = "Progress indicator";
        progressBar.Size = new Size(200, 16);
        progressBar.ProgressStyle = ProgressBarStyles.Metro;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
    }

    private void ApplySfAIAssistViewDefaults(SfAIAssistView assistView)
    {
        assistView.Dock = DockStyle.Fill;
        assistView.AccessibleName = "AI Assist View";
        assistView.AccessibleDescription = "AI chat and response surface";
        assistView.ThemeName = CurrentTheme;
        assistView.ShowTypingIndicator = false;
        SetOptionalProperty(assistView, "EnableStopResponding", true);
        SetOptionalProperty(assistView, "IsResponseToolbarVisible", true);
    }

    private void SetOptionalProperty(object instance, string propertyName, object? value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.CanWrite == true)
        {
            property.SetValue(instance, value);
        }
    }

    private void ApplyDockingManagerDefaults(DockingManager dockingManager, Form hostForm, ContainerControl hostContainer)
    {
        dockingManager.HostForm = hostForm;
        dockingManager.HostControl = hostContainer;
        dockingManager.ThemeName = CurrentTheme;
        dockingManager.EnableDocumentMode = false;
        dockingManager.ShowCaption = false;
        dockingManager.DockToFill = false;
        dockingManager.CloseEnabled = true;
        dockingManager.AnimateAutoHiddenWindow = true;
        dockingManager.ReduceFlickeringInRtl = false;
        dockingManager.PersistState = false;
    }

    private void ApplyRichTextBoxDefaults(RichTextBox rtb)
    {
        rtb.Dock = DockStyle.Fill;
        rtb.AccessibleName = "Rich Text Box";
        rtb.AccessibleDescription = "Formatted text viewer";
        rtb.ReadOnly = true;
        rtb.BorderStyle = BorderStyle.FixedSingle;
        rtb.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        rtb.DetectUrls = true;
        rtb.WordWrap = true;
    }

    private void ApplyLoadingOverlayDefaults(LoadingOverlay overlay)
    {
        overlay.Dock = DockStyle.Fill;
        overlay.AccessibleName = "Loading Overlay";
        overlay.AccessibleDescription = "Loading state overlay";
        overlay.Visible = false;
    }
}

