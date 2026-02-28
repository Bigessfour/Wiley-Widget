using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
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
    private readonly ILogger<SyncfusionControlFactory> _logger;
    private readonly string _currentTheme;

    public SyncfusionControlFactory(ILogger<SyncfusionControlFactory> logger, IThemeService? themeService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentTheme = themeService?.CurrentTheme
            ?? SfSkinManager.ApplicationVisualTheme
            ?? "Office2019Colorful";
    }

    #region SfDataGrid

    public SfDataGrid CreateSfDataGrid(Action<SfDataGrid>? configure = null)
    {
        _logger.LogDebug("Creating SfDataGrid");

        var grid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = true,
            AllowFiltering = true,
            AllowSorting = true,
            AllowResizingColumns = true,
            AllowDraggingColumns = true,
            AllowGrouping = false,
            SelectionMode = GridSelectionMode.Single,
            AutoGenerateColumns = true,
            EditorSelectionBehavior = EditorSelectionBehavior.SelectAll,
            AddNewRowPosition = RowPosition.Bottom,
            NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Cell,
            EnableDataVirtualization = true,
            ShowRowHeader = false,
            ShowToolTip = true,
        };

        grid.ApplySyncfusionTheme(_currentTheme, _logger);
        grid.PreventStringRelationalFilters(_logger);
        configure?.Invoke(grid);

        _logger.LogInformation("SfDataGrid created: Theme={Theme}", grid.ThemeName);
        return grid;
    }

    #endregion

    #region SfButton

    public SfButton CreateSfButton(string text, Action<SfButton>? configure = null)
    {
        _logger.LogDebug("Creating SfButton: {Text}", text);

        var button = new SfButton
        {
            Text = text,
            Size = new Size(120, 32),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
        };

        button.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(button);
        return button;
    }

    #endregion

    #region ChartControl

    public ChartControl CreateChartControl(string title, Action<ChartControl>? configure = null)
    {
        _logger.LogDebug("Creating ChartControl: {Title}", title);

        var chart = new ChartControl
        {
            Dock = DockStyle.Fill,
            ShowLegend = true,
            EnableMouseRotation = false,
            AllowGradientPalette = true,
        };
        chart.Title.Text = title;
        chart.Legend.Visible = true;
        chart.Legend.Position = ChartDock.Bottom;
        chart.PrimaryXAxis.Title = "X Axis";
        chart.PrimaryYAxis.Title = "Y Axis";

        chart.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(chart);
        return chart;
    }

    /// <summary>Creates a chart configured for reserve projections (alias entry point).</summary>
    public ChartControl CreateSfChart(string title, Action<ChartControl>? configure = null)
    {
        _logger.LogDebug("Creating SfChart wrapper: {Title}", title);

        var chart = new ChartControl
        {
            Dock = DockStyle.Fill,
            ShowLegend = true,
            EnableMouseRotation = false,
            AllowGradientPalette = true,
        };
        chart.Title.Text = title;
        chart.Legend.Visible = true;
        chart.Legend.Position = ChartDock.Bottom;
        chart.PrimaryXAxis.Title = "Fiscal Year";
        chart.PrimaryYAxis.Title = "Projected Reserves";

        chart.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(chart);
        return chart;
    }

    #endregion

    #region TabControlAdv

    public TabControlAdv CreateTabControlAdv(Action<TabControlAdv>? configure = null)
    {
        _logger.LogDebug("Creating TabControlAdv");

        var tabControl = new TabControlAdv
        {
            Dock = DockStyle.Fill,
            TabStyle = typeof(TabRendererMetro),
            Alignment = TabAlignment.Top,
            SizeMode = Syncfusion.Windows.Forms.Tools.TabSizeMode.Fixed,
            ItemSize = new Size(120, 32),
        };

        tabControl.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(tabControl);
        return tabControl;
    }

    #endregion

    #region RibbonControlAdv

    public RibbonControlAdv CreateRibbonControlAdv(string menuButtonText, Action<RibbonControlAdv>? configure = null)
    {
        _logger.LogDebug("Creating RibbonControlAdv");

        var ribbon = new RibbonControlAdv
        {
            MenuButtonText = menuButtonText,
            ShowQuickItemsDropDownButton = false,
            TitleAlignment = TextAlignment.Left,
            RibbonStyle = RibbonStyle.Office2016,
            OfficeColorScheme = ToolStripEx.ColorScheme.Managed,
        };
        ribbon.Dock = DockStyleEx.Top;

        ribbon.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(ribbon);
        return ribbon;
    }

    #endregion

    #region SfListView

    public SfListView CreateSfListView(Action<SfListView>? configure = null)
    {
        _logger.LogDebug("Creating SfListView");

        var listView = new SfListView
        {
            Dock = DockStyle.Fill,
            ItemHeight = 28,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
        };

        listView.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(listView);
        return listView;
    }

    #endregion

    #region AutoComplete

    public AutoComplete CreateAutoComplete(Action<AutoComplete>? configure = null)
    {
        _logger.LogDebug("Creating AutoComplete");

        var autoComplete = new AutoComplete
        {
            IgnoreCase = true,
            AutoSortList = true,
            AutoSerialize = false,
            AutoAddItem = false,
            AdjustHeightToItemCount = true,
            ShowCloseButton = false,
            ShowGripper = false,
            MaxNumberofSuggestion = 12,
            BorderType = AutoCompleteBorderTypes.Sizable,
            ThemeName = _currentTheme,
        };

        configure?.Invoke(autoComplete);
        return autoComplete;
    }

    #endregion

    #region PdfViewerControl

    public PdfViewerControl CreatePdfViewerControl(Action<PdfViewerControl>? configure = null)
    {
        _logger.LogDebug("Creating PdfViewerControl");

        var pdfViewer = new PdfViewerControl
        {
            Dock = DockStyle.Fill,
            Name = "PdfViewerControl",
        };

        pdfViewer.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(pdfViewer);
        return pdfViewer;
    }

    #endregion

    #region TextBoxExt

    public TextBoxExt CreateTextBoxExt(Action<TextBoxExt>? configure = null)
    {
        _logger.LogDebug("Creating TextBoxExt");

        var textBox = new TextBoxExt
        {
            BorderStyle = BorderStyle.FixedSingle,
            Size = new Size(200, 28),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            CanOverrideStyle = false,
        };

        textBox.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(textBox);
        return textBox;
    }

    #endregion

    #region SfComboBox

    public SfComboBox CreateSfComboBox(Action<SfComboBox>? configure = null)
    {
        _logger.LogDebug("Creating SfComboBox");

        var comboBox = new SfComboBox
        {
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            Width = 150,
            Height = 28,
            MaxDropDownItems = 10,
            AllowDropDownResize = false,
        };

        comboBox.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(comboBox);
        return comboBox;
    }

    #endregion

    #region SfDateTimeEdit

    public SfDateTimeEdit CreateSfDateTimeEdit(Action<SfDateTimeEdit>? configure = null)
    {
        _logger.LogDebug("Creating SfDateTimeEdit");

        var dateTimeEdit = new SfDateTimeEdit
        {
            Width = 140,
            DateTimePattern = Syncfusion.WinForms.Input.Enums.DateTimePattern.ShortDate,
        };

        dateTimeEdit.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(dateTimeEdit);
        return dateTimeEdit;
    }

    #endregion

    #region SfNumericTextBox

    public SfNumericTextBox CreateSfNumericTextBox(Action<SfNumericTextBox>? configure = null)
    {
        _logger.LogDebug("Creating SfNumericTextBox");

        var numericTextBox = new SfNumericTextBox
        {
            Size = new Size(80, 24),
            FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Numeric,
        };

        numericTextBox.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(numericTextBox);
        return numericTextBox;
    }

    #endregion

    #region CheckBoxAdv

    public CheckBoxAdv CreateCheckBoxAdv(string text, Action<CheckBoxAdv>? configure = null)
    {
        _logger.LogDebug("Creating CheckBoxAdv: {Text}", text);

        var checkBox = new CheckBoxAdv
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
        };

        checkBox.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(checkBox);
        return checkBox;
    }

    #endregion

    #region SplitContainerAdv

    public SplitContainerAdv CreateSplitContainerAdv(Action<SplitContainerAdv>? configure = null)
    {
        _logger.LogDebug("Creating SplitContainerAdv");

        var splitContainer = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            ThemeName = _currentTheme,
        };

        splitContainer.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(splitContainer);
        return splitContainer;
    }

    #endregion

    #region ProgressBarAdv

    public ProgressBarAdv CreateProgressBarAdv(Action<ProgressBarAdv>? configure = null)
    {
        _logger.LogDebug("Creating ProgressBarAdv");

        var progressBar = new ProgressBarAdv
        {
            Size = new Size(200, 16),
            ProgressStyle = ProgressBarStyles.Metro,
        };

        progressBar.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(progressBar);
        return progressBar;
    }

    #endregion

    #region DockingManager

    public DockingManager CreateDockingManager(Form hostForm, Control hostControl, Action<DockingManager>? configure = null)
    {
        _logger.LogDebug("Creating DockingManager for {FormType}", hostForm.GetType().Name);

        var dockingManager = new DockingManager
        {
            HostForm = hostForm,
            HostControl = hostControl as ContainerControl ?? hostForm,
            ShowCaption = false,
            DockToFill = false,
            CloseEnabled = true,
            PersistState = false,
        };

        configure?.Invoke(dockingManager);
        return dockingManager;
    }

    #endregion

    #region RichTextBoxExt

    public RichTextBox CreateRichTextBoxExt(Action<RichTextBox>? configure = null)
    {
        _logger.LogDebug("Creating RichTextBoxExt");

        var rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            DetectUrls = true,
            WordWrap = true,
        };

        rtb.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(rtb);
        return rtb;
    }

    #endregion

    #region LoadingOverlay

    public LoadingOverlay CreateLoadingOverlay(Action<LoadingOverlay>? configure = null)
    {
        _logger.LogDebug("Creating LoadingOverlay");

        var overlay = new LoadingOverlay
        {
            Dock = DockStyle.Fill,
            Visible = false,
        };

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
            Value = (float)currentRatio,
            MinimumValue = 0,
            MaximumValue = 150,
            ShowTicks = true,
            ShowScaleLabel = true,
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

        gauge.ApplySyncfusionTheme(_currentTheme, _logger);

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
        };

        var titleLabel = new Label
        {
            Text = enterpriseName,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
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

        container.Controls.Add(gauge);
        container.Controls.Add(valueLabel);
        container.Controls.Add(titleLabel);
        container.ApplySyncfusionTheme(_currentTheme, _logger);

        _logger.LogDebug("Created enterprise gauge container for {Enterprise}", enterpriseName);
        return container;
    }

    /// <summary>
    /// Creates a ChartControl with Revenue, Expenses, Net Position, and Break Even series for one enterprise.
    /// </summary>
    public ChartControl CreateEnterpriseChart(EnterpriseSnapshot snapshot)
    {
        _logger.LogDebug("Creating EnterpriseChart: {Enterprise}", snapshot.Name);

        var chart = new ChartControl
        {
            Dock = DockStyle.Fill,
            ShowLegend = true,
            EnableMouseRotation = false,
            AllowGradientPalette = true,
        };
        chart.Title.Text = $"{snapshot.Name} — Financial Snapshot";
        chart.PrimaryXAxis.Title = "Fiscal Year";
        chart.PrimaryYAxis.Title = "Amount";

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

        chart.ApplySyncfusionTheme(_currentTheme, _logger);

        _logger.LogInformation("EnterpriseChart created for {Enterprise}", snapshot.Name);
        return chart;
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
}

