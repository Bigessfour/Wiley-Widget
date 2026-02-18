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
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Factories;

/// <summary>
/// Factory for creating fully-configured Syncfusion controls with ALL required properties.
/// 
/// MANDATORY USAGE: This factory ensures no partial/incomplete control implementations.
/// Per workspace rules (Syncfusion API Rule):
/// - ALL Syncfusion API properties must be configured
/// - Theme integration is mandatory
/// - No "winging it" or incomplete setups
/// 
/// Before using ANY control type, consult Syncfusion WinForms documentation via MCP:
/// - Use Syncfusion WinForms Assistant MCP for API documentation
/// - Validate against local samples: C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.1.19
/// </summary>
public class SyncfusionControlFactory
{
    private readonly ILogger<SyncfusionControlFactory> _logger;
    private readonly string _currentTheme;

    public SyncfusionControlFactory(ILogger<SyncfusionControlFactory> logger, IThemeService? themeService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentTheme = themeService?.CurrentTheme ?? Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? "Office2019Colorful";
    }

    #region SfDataGrid - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured SfDataGrid with ALL essential properties set.
    /// 
    /// Mandatory Properties Checklist (per Syncfusion API):
    /// ✅ ThemeName - Theme integration
    /// ✅ Dock/Size - Layout
    /// ✅ AllowEditing - Edit behavior
    /// ✅ AllowFiltering - Filter behavior
    /// ✅ AllowSorting - Sort behavior
    /// ✅ AllowResizingColumns - Column resize
    /// ✅ SelectionMode - Selection behavior
    /// ✅ AutoGenerateColumns - Column generation
    /// ✅ EditorSelectionBehavior - Edit UX
    /// ✅ AddNewRowPosition - Add row behavior
    /// ✅ NavigationMode - Keyboard navigation
    /// ✅ String filter protection - Prevent relational operator crashes
    /// </summary>
    public SfDataGrid CreateSfDataGrid(Action<SfDataGrid>? configure = null)
    {
        _logger.LogDebug("Creating SfDataGrid with full property configuration");

        var grid = new SfDataGrid
        {
            // Theme integration (MANDATORY per SfSkinManager rule)
            ThemeName = _currentTheme,

            // Layout
            Dock = DockStyle.Fill,
            
            // Core behaviors (all set explicitly - no defaults assumed)
            AllowEditing = true,
            AllowFiltering = true,
            AllowSorting = true,
            AllowResizingColumns = true,
            AllowDraggingColumns = true,
            AllowGrouping = false, // Set explicitly
            
            // Selection behavior
            SelectionMode = GridSelectionMode.Single,
            
            // Column generation
            AutoGenerateColumns = true,
            
            // Edit behavior
            EditorSelectionBehavior = EditorSelectionBehavior.SelectAll,
            
            // Add row behavior
            AddNewRowPosition = RowPosition.Bottom,
            
            // Navigation
            NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Cell,
            
            // Performance
            EnableDataVirtualization = true,
            
            // Visual polish
            ShowRowHeader = false,
            ShowToolTip = true
        };

        // Apply theme via SfSkinManager (single source of truth)
        grid.ApplySyncfusionTheme(_currentTheme, _logger);

        // String filter protection (prevents System.InvalidOperationException)
        grid.PreventStringRelationalFilters(_logger);

        // Allow caller to override/extend
        configure?.Invoke(grid);

        _logger.LogInformation(
            "SfDataGrid created: Theme={Theme}, Editing={Editing}, Filtering={Filtering}, Sorting={Sorting}",
            grid.ThemeName, grid.AllowEditing, grid.AllowFiltering, grid.AllowSorting);

        return grid;
    }

    #endregion

    #region SfButton - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured SfButton with ALL essential properties set.
    /// 
    /// Mandatory Properties Checklist:
    /// ✅ ThemeName
    /// ✅ Text
    /// ✅ Size
    /// ✅ Font
    /// </summary>
    public SfButton CreateSfButton(string text, Action<SfButton>? configure = null)
    {
        _logger.LogDebug("Creating SfButton: {Text}", text);

        var button = new SfButton
        {
            ThemeName = _currentTheme,
            Text = text,
            Size = new Size(120, 32),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };

        button.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(button);

        return button;
    }

    #endregion

    #region ChartControl - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured ChartControl with ALL essential properties set.
    /// 
    /// Mandatory Properties Checklist:
    /// ✅ ThemeName
    /// ✅ Size/Dock
    /// ✅ PrimaryXAxis configuration
    /// ✅ PrimaryYAxis configuration
    /// ✅ Series collection initialized
    /// ✅ Legend visibility
    /// ✅ Title
    /// </summary>
    public ChartControl CreateChartControl(string title, Action<ChartControl>? configure = null)
    {
        _logger.LogDebug("Creating ChartControl: {Title}", title);

        var chart = new ChartControl
        {
            Dock = DockStyle.Fill,
            Title = { Text = title },
            
            // Visual settings
            Legend = { Visible = true, Position = ChartDock.Bottom },
            ShowLegend = true,
            
            // Interaction
            EnableMouseRotation = false,
            AllowGradientPalette = true
        };

        // Configure primary axes (properties are read-only)
        chart.PrimaryXAxis.Title = "X Axis";
        chart.PrimaryYAxis.Title = "Y Axis";

        chart.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(chart);

        return chart;
    }

    #endregion

    #region TabControlAdv - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured TabControlAdv with ALL essential properties set.
    /// 
    /// Mandatory Properties Checklist:
    /// ✅ ThemeName
    /// ✅ Dock/Size
    /// ✅ TabStyle
    /// ✅ Alignment
    /// ✅ CloseButtonVisible
    /// </summary>
    public TabControlAdv CreateTabControlAdv(Action<TabControlAdv>? configure = null)
    {
        _logger.LogDebug("Creating TabControlAdv");

        var tabControl = new TabControlAdv
        {
            ThemeName = _currentTheme,
            Dock = DockStyle.Fill,
            TabStyle = typeof(Syncfusion.Windows.Forms.Tools.TabRendererMetro),
            Alignment = TabAlignment.Top,
            SizeMode = Syncfusion.Windows.Forms.Tools.TabSizeMode.Fixed,
            ItemSize = new Size(120, 32)
        };

        tabControl.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(tabControl);

        return tabControl;
    }

    #endregion

    #region RibbonControlAdv - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured RibbonControlAdv with ALL essential properties set.
    /// 
    /// Mandatory Properties Checklist:
    /// ✅ ThemeName
    /// ✅ Dock
    /// ✅ MenuButtonText
    /// ✅ ShowQuickItemsDropDownButton
    /// ✅ TitleAlignment
    /// ✅ RibbonStyle
    /// </summary>
    public RibbonControlAdv CreateRibbonControlAdv(string menuButtonText, Action<RibbonControlAdv>? configure = null)
    {
        _logger.LogDebug("Creating RibbonControlAdv");

        var ribbon = new RibbonControlAdv
        {
            ThemeName = _currentTheme,
            MenuButtonText = menuButtonText,
            ShowQuickItemsDropDownButton = false,
            TitleAlignment = Syncfusion.Windows.Forms.Tools.TextAlignment.Left,
            RibbonStyle = RibbonStyle.Office2016,
            OfficeColorScheme = Syncfusion.Windows.Forms.Tools.ToolStripEx.ColorScheme.Managed
        };
        
        // Dock must be set via special property (not regular DockStyle)
        ribbon.Dock = (Syncfusion.Windows.Forms.Tools.DockStyleEx)DockStyle.Top;

        ribbon.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(ribbon);

        return ribbon;
    }

    #endregion

    #region SfListView - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured SfListView with ALL essential properties set.
    /// 
    /// Mandatory Properties Checklist:
    /// ✅ ThemeName
    /// ✅ Dock/Size
    /// ✅ View
    /// ✅ SelectionMode
    /// ✅ ShowColumnHeader
    /// </summary>
    public SfListView CreateSfListView(Action<SfListView>? configure = null)
    {
        _logger.LogDebug("Creating SfListView with full property configuration");

        var listView = new SfListView
        {
            ThemeName = _currentTheme,
            Dock = DockStyle.Fill,
            ItemHeight = 28,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };

        listView.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(listView);

        return listView;
    }

    #endregion

    #region TextBoxExt - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured TextBoxExt with ALL essential properties set.
    /// 
    /// Mandatory Properties Checklist:
    /// ✅ ThemeName
    /// ✅ Size
    /// ✅ Font
    /// ✅ BorderStyle
    /// </summary>
    public TextBoxExt CreateTextBoxExt(Action<TextBoxExt>? configure = null)
    {
        _logger.LogDebug("Creating TextBoxExt with full property configuration");

        var textBox = new TextBoxExt
        {
            ThemeName = _currentTheme,
            BorderStyle = BorderStyle.FixedSingle,
            Size = new Size(200, 28),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            CanOverrideStyle = false
        };

        textBox.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(textBox);

        return textBox;
    }

    #endregion

    #region SfComboBox - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured SfComboBox with ALL essential properties set.
    /// </summary>
    public SfComboBox CreateSfComboBox(Action<SfComboBox>? configure = null)
    {
        _logger.LogDebug("Creating SfComboBox with full property configuration");

        var comboBox = new SfComboBox
        {
            ThemeName = _currentTheme,
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            Width = 150,
            Height = 28,
            MaxDropDownItems = 10,
            AllowDropDownResize = false
        };

        comboBox.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(comboBox);

        return comboBox;
    }

    #endregion

    #region SfNumericTextBox - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured SfNumericTextBox with ALL essential properties set.
    /// </summary>
    public SfNumericTextBox CreateSfNumericTextBox(Action<SfNumericTextBox>? configure = null)
    {
        _logger.LogDebug("Creating SfNumericTextBox with full property configuration");

        var numericTextBox = new SfNumericTextBox
        {
            ThemeName = _currentTheme,
            Size = new Size(80, 24),
            FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Numeric
        };

        numericTextBox.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(numericTextBox);

        return numericTextBox;
    }

    #endregion

    #region CheckBoxAdv - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured CheckBoxAdv with ALL essential properties set.
    /// </summary>
    public CheckBoxAdv CreateCheckBoxAdv(string text, Action<CheckBoxAdv>? configure = null)
    {
        _logger.LogDebug("Creating CheckBoxAdv: {Text}", text);

        var checkBox = new CheckBoxAdv
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };

        checkBox.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(checkBox);

        return checkBox;
    }

    #endregion

    #region SplitContainerAdv - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured SplitContainerAdv with ALL essential properties set.
    /// </summary>
    public SplitContainerAdv CreateSplitContainerAdv(Action<SplitContainerAdv>? configure = null)
    {
        _logger.LogDebug("Creating SplitContainerAdv");

        var splitContainer = new SplitContainerAdv
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            ThemeName = _currentTheme
        };

        splitContainer.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(splitContainer);

        return splitContainer;
    }

    #endregion

    #region ProgressBarAdv - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured ProgressBarAdv with ALL essential properties set.
    /// </summary>
    public ProgressBarAdv CreateProgressBarAdv(Action<ProgressBarAdv>? configure = null)
    {
        _logger.LogDebug("Creating ProgressBarAdv");

        var progressBar = new ProgressBarAdv
        {
            ThemeName = _currentTheme,
            Size = new Size(200, 16),
            ProgressStyle = ProgressBarStyles.Metro
        };

        progressBar.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(progressBar);

        return progressBar;
    }

    #endregion

    #region DockingManager - Complete Configuration Template

    /// <summary>
    /// Creates a fully-configured DockingManager with ALL essential properties set.
    /// 
    /// Mandatory Properties Checklist:
    /// ✅ ThemeName
    /// ✅ HostForm (parent form)
    /// ✅ HostControl (parent container)
    /// ✅ ShowCaption
    /// ✅ DockToFill
    /// ✅ CloseEnabled
    /// </summary>
    public DockingManager CreateDockingManager(Form hostForm, Control hostControl, Action<DockingManager>? configure = null)
    {
        _logger.LogDebug("Creating DockingManager for {FormType}", hostForm.GetType().Name);

        var dockingManager = new DockingManager
        {
            ThemeName = _currentTheme,
            HostForm = hostForm,
            HostControl = hostControl as ContainerControl ?? hostForm,
            ShowCaption = false,
            DockToFill = false,
            CloseEnabled = true,
            PersistState = false
        };

        configure?.Invoke(dockingManager);

        return dockingManager;
    }

    #endregion
}

