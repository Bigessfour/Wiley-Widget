using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Media;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
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
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utilities;

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
    private readonly IThemeService? _themeService;
    private readonly DpiAwareImageService? _dpiAwareImageService;

    private string _currentTheme => ThemeColors.ValidateTheme(
        _themeService?.CurrentTheme
        ?? SfSkinManager.ApplicationVisualTheme
        ?? ThemeColors.DefaultTheme,
        _logger);

    public SyncfusionControlFactory(
        ILogger<SyncfusionControlFactory> logger,
        IThemeService? themeService = null,
        IServiceProvider? serviceProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _themeService = themeService;
        _dpiAwareImageService = serviceProvider?.GetService(typeof(DpiAwareImageService)) as DpiAwareImageService;
    }

    private void ApplyThemeAndInit(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var initialize = control as ISupportInitialize;
        try
        {
            initialize?.BeginInit();
            control.RightToLeft = RightToLeft.No;
            if (string.IsNullOrWhiteSpace(control.AccessibleName))
            {
                control.AccessibleName = string.IsNullOrWhiteSpace(control.Name)
                    ? control.GetType().Name
                    : control.Name;
            }

            control.ApplySyncfusionTheme(_currentTheme, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize themed control {ControlType}", control.GetType().Name);
        }
        finally
        {
            try
            {
                initialize?.EndInit();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "EndInit failed for {ControlType}", control.GetType().Name);
            }
        }
    }

    private static void PropagateComboAccessibility(SfComboBox comboBox)
    {
        static void ApplyToChildren(Control parent, string ownerName, string ownerDescription)
        {
            foreach (Control child in parent.Controls)
            {
                var typeName = child.GetType().Name;

                if (typeName.Contains("SfButton", StringComparison.OrdinalIgnoreCase)
                    || typeName.Contains("DropDownButton", StringComparison.OrdinalIgnoreCase))
                {
                    child.AccessibleName = $"{ownerName} drop-down";
                    child.AccessibleDescription = string.IsNullOrWhiteSpace(ownerDescription)
                        ? $"Open the {ownerName} options"
                        : $"Open the {ownerDescription.ToLowerInvariant()} options";
                }
                else if (typeName.Contains("TextBox", StringComparison.OrdinalIgnoreCase))
                {
                    child.AccessibleName = $"{ownerName} input";
                    child.AccessibleDescription = string.IsNullOrWhiteSpace(ownerDescription)
                        ? $"Type or review the {ownerName} value"
                        : ownerDescription;
                }

                if (child.HasChildren)
                {
                    ApplyToChildren(child, ownerName, ownerDescription);
                }
            }
        }

        void Apply()
        {
            var ownerName = string.IsNullOrWhiteSpace(comboBox.AccessibleName)
                ? (!string.IsNullOrWhiteSpace(comboBox.Name) ? comboBox.Name : "Selection")
                : comboBox.AccessibleName;

            var ownerDescription = string.IsNullOrWhiteSpace(comboBox.AccessibleDescription)
                ? $"Select {ownerName}"
                : comboBox.AccessibleDescription;

            comboBox.AccessibleName = ownerName;
            comboBox.AccessibleDescription = ownerDescription;
            ApplyToChildren(comboBox, ownerName, ownerDescription);
        }

        comboBox.HandleCreated += (_, _) => Apply();
        comboBox.ControlAdded += (_, _) => Apply();
        Apply();
    }

    private Image? TryGetIcon(string icon)
    {
        if (string.IsNullOrWhiteSpace(icon) || _dpiAwareImageService == null)
        {
            return null;
        }

        try
        {
            return _dpiAwareImageService.GetImage(icon);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to resolve icon '{Icon}'", icon);
            return null;
        }
    }

    #region SfDataGrid

    public SfDataGrid CreateSfDataGrid(Action<SfDataGrid>? configure = null)
    {
        _logger.LogDebug("Creating SfDataGrid");

        var grid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AllowEditing = true,
            AllowDeleting = true,
            AllowFiltering = true,
            AllowSorting = true,
            AllowResizingColumns = true,
            AllowResizingHiddenColumns = true,
            AllowDraggingColumns = true,
            AllowGrouping = true,
            AllowSelectionOnMouseDown = true,
            AllowStandardTab = true,
            AutoExpandGroups = false,
            AutoFitGroupDropAreaItem = true,
            SelectionMode = GridSelectionMode.Single,
            AutoGenerateColumns = true,
            AutoGenerateRelations = true,
            AutoSizeColumnsMode = AutoSizeColumnsMode.AllCells,
            EditorSelectionBehavior = EditorSelectionBehavior.SelectAll,
            AddNewRowPosition = RowPosition.Bottom,
            AddNewRowText = "Click here to add a new row",
            FilterRowPosition = RowPosition.Top,
            NavigationMode = Syncfusion.WinForms.DataGrid.Enums.NavigationMode.Cell,
            EnableDataVirtualization = true,
            RowHeight = LayoutTokens.GridRowHeight,
            HeaderRowHeight = LayoutTokens.GridHeaderRowHeight,
            FrozenColumnCount = 0,
            FrozenRowCount = 0,
            ShowGroupDropArea = true,
            ShowRowHeader = false,
            ShowToolTip = true,
            ShowHeaderToolTip = true,
            ShowValidationErrorToolTip = true,
            AllowTriStateSorting = true,
        };

        ApplyThemeAndInit(grid);
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

        var defaultButtonHeight = LayoutTokens.StandardControlHeightLarge;

        var button = new SfButton
        {
            AccessibleName = "Button",
            AccessibleDescription = text,
            Text = text,
            Size = new Size(LayoutTokens.DefaultButtonSize.Width, defaultButtonHeight),
            MinimumSize = new Size(0, defaultButtonHeight),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            TextImageRelation = TextImageRelation.ImageBeforeText,
            TextAlign = ContentAlignment.MiddleCenter,
            ImageAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            AutoEllipsis = true,
            AccessibilityEnabled = true,
            FocusRectangleVisible = true,
            TabStop = true,
            AllowImageAnimation = true,
            AllowRichText = false,
            AllowWrapText = false,
            Cursor = Cursors.Hand,
        };

        ApplyThemeAndInit(button);
        configure?.Invoke(button);
        return button;
    }

    public SfButton CreateSfButton(string text, string? icon, bool flat, Action<SfButton>? configure = null)
    {
        _logger.LogDebug("Creating SfButton overload: Text={Text}, Icon={Icon}, Flat={Flat}", text, icon, flat);

        return CreateSfButton(text, button =>
        {
            if (!string.IsNullOrWhiteSpace(icon))
            {
                button.AccessibleDescription = $"Icon hint: {icon}";
                var resolvedIcon = TryGetIcon(icon);
                if (resolvedIcon != null)
                {
                    button.Image = resolvedIcon;
                    button.TextImageRelation = TextImageRelation.ImageBeforeText;
                    button.ImageAlign = ContentAlignment.MiddleLeft;
                    button.TextAlign = ContentAlignment.MiddleCenter;
                }
            }

            if (flat)
            {
                button.AutoSize = false;
                button.FlatStyle = FlatStyle.Flat;
            }
            else
            {
                button.FlatStyle = FlatStyle.Standard;
            }

            configure?.Invoke(button);
        });
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
            ShowToolTips = true,
            AutoHighlight = true,
            ShowScrollBars = false,
            ShowToolbar = false,
            ImprovePerformance = true,
        };
        chart.Title.Text = title;
        chart.Legend.Visible = true;
        chart.Legend.Position = ChartDock.Bottom;
        chart.LegendsPlacement = ChartPlacement.Outside;
        chart.AccessibleName = string.IsNullOrWhiteSpace(title)
            ? "Chart"
            : $"{title} Chart";
        chart.AccessibleDescription = "Chart visualization";
        chart.PrimaryXAxis.Title = "X Axis";
        chart.PrimaryXAxis.DrawGrid = false;
        chart.PrimaryXAxis.LabelIntersectAction = ChartLabelIntersectAction.MultipleRows;
        chart.PrimaryXAxis.RangePaddingType = ChartAxisRangePaddingType.None;
        chart.PrimaryXAxis.DesiredIntervals = 6;
        chart.PrimaryYAxis.Title = "Y Axis";
        chart.PrimaryYAxis.DrawGrid = true;
        chart.PrimaryYAxis.RangePaddingType = ChartAxisRangePaddingType.Calculate;

        ChartControlDefaults.Apply(chart, logger: _logger);
        ApplyThemeAndInit(chart);
        configure?.Invoke(chart);
        return chart;
    }

    /// <summary>Creates a chart configured for reserve projections (alias entry point).</summary>
    public ChartControl CreateSfChart(string title, Action<ChartControl>? configure = null)
    {
        _logger.LogDebug("Creating SfChart wrapper: {Title}", title);

        return CreateChartControl(title, chart =>
        {
            chart.PrimaryXAxis.Title = "Fiscal Year";
            chart.PrimaryYAxis.Title = "Projected Reserves";
            configure?.Invoke(chart);
        });
    }

    #endregion

    #region SparkLine

    public SparkLine CreateSfSparkline(
        SparkLineType type = SparkLineType.Line,
        Action<SparkLine>? configure = null)
    {
        _logger.LogDebug("Creating SfSparkline: Type={Type}", type);

        var sparkLine = new SparkLine
        {
            Dock = DockStyle.Fill,
            Size = new Size(160, 40),
            Type = type,
        };

        ApplyThemeAndInit(sparkLine);
        configure?.Invoke(sparkLine);
        return sparkLine;
    }

    public SparkLine CreateSfSparkline(
        IEnumerable<double> values,
        SparkLineType type = SparkLineType.Line,
        Action<SparkLine>? configure = null)
    {
        _logger.LogDebug("Creating SfSparkline with data source: Type={Type}", type);

        return CreateSfSparkline(type, sparkLine =>
        {
            sparkLine.Source = values;
            configure?.Invoke(sparkLine);
        });
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
            ItemSize = LayoutTokens.TabItemSize,
            Multiline = false,
            ShowScroll = true,
            ShowToolTips = true,
            ShowTabCloseButton = false,
            ShowCloseButtonForActiveTabOnly = false,
            SwitchPagesForDialogKeys = true,
            RotateTextWhenVertical = false,
            CloseTabOnMiddleClick = false,
            ShowSeparator = false,
            HotTrack = true,
            FocusOnTabClick = true,
        };

        ApplyThemeAndInit(tabControl);
        configure?.Invoke(tabControl);
        return tabControl;
    }

    public TabControlAdv CreateTabControlAdv(
        TabAlignment alignment,
        bool multiline = false,
        bool showScroll = true,
        bool showTabCloseButton = false,
        Action<TabControlAdv>? configure = null)
    {
        _logger.LogDebug(
            "Creating TabControlAdv overload: Alignment={Alignment}, Multiline={Multiline}, ShowScroll={ShowScroll}, ShowClose={ShowClose}",
            alignment,
            multiline,
            showScroll,
            showTabCloseButton);

        return CreateTabControlAdv(tabControl =>
        {
            tabControl.Alignment = alignment;
            tabControl.Multiline = multiline;
            tabControl.ShowScroll = showScroll;
            tabControl.ShowTabCloseButton = showTabCloseButton;
            configure?.Invoke(tabControl);
        });
    }

    public TabPageAdv CreateTabPageAdv(string text, Control content, Action<TabPageAdv>? configure = null)
    {
        _logger.LogDebug("Creating TabPageAdv: {Text}", text);

        var page = new TabPageAdv
        {
            Text = text,
            Name = $"TabPage_{text.Replace(" ", string.Empty, StringComparison.Ordinal)}",
            AccessibleName = text,
            AccessibleDescription = $"{text} tab page",
            Padding = Padding.Empty,
            AutoScroll = true
        };

        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        page.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(page);
        return page;
    }

    #endregion

    #region RibbonControlAdv

    public RibbonControlAdv CreateSfRibbon(
        bool showQuickPanel = true,
        string menuButtonText = "File",
        Action<RibbonControlAdv>? configure = null)
    {
        _logger.LogDebug("Creating SfRibbon wrapper (RibbonControlAdv)");

        return CreateRibbonControlAdv(menuButtonText, ribbon =>
        {
            ribbon.QuickPanelVisible = showQuickPanel;
            ribbon.ShowQuickItemsDropDownButton = showQuickPanel;
            ribbon.MenuButtonVisible = true;
            ribbon.MenuButtonEnabled = true;
            configure?.Invoke(ribbon);
        });
    }

    public RibbonControlAdv CreateRibbonControlAdv(string menuButtonText, Action<RibbonControlAdv>? configure = null)
    {
        _logger.LogDebug("Creating RibbonControlAdv");

        var ribbon = new RibbonControlAdv
        {
            Name = "Ribbon_Main",
            Dock = DockStyleEx.Top,
            MenuButtonText = menuButtonText,
            MenuButtonVisible = true,
            MenuButtonEnabled = true,
            MenuButtonAutoSize = false,
            MenuButtonWidth = 56,
            QuickPanelVisible = true,
            QuickPanelImageLayout = PictureBoxSizeMode.StretchImage,
            ShowQuickItemsDropDownButton = false,
            ShowRibbonDisplayOptionButton = true,
            TitleAlignment = TextAlignment.Left,
            RibbonStyle = RibbonStyle.Office2016,
            OfficeColorScheme = ToolStripEx.ColorScheme.Managed,
            EnableSimplifiedLayoutMode = true,
            LayoutMode = RibbonLayoutMode.Normal,
            AutoLayoutToolStrip = true,
            AllowCollapse = true,
            TouchMode = false,
            ShowLauncher = true,
            ShowCaption = true,
            ThemeName = _currentTheme,
        };

        ApplyThemeAndInit(ribbon);
        configure?.Invoke(ribbon);
        return ribbon;
    }

    /// <summary>
    /// Creates a pre-configured ToolStripGallery suitable for dynamic ribbon panel navigation.
    /// Syncfusion WinForms 32.2.3 uses ToolStripGallery for ribbon gallery scenarios.
    /// </summary>
    public ToolStripGallery CreateRibbonGallery(string title = "Panels", int maxColumns = 1)
    {
        var normalizedColumns = Math.Max(1, maxColumns);

        var gallery = new ToolStripGallery
        {
            Name = "RibbonPanelsGallery",
            CaptionText = string.IsNullOrWhiteSpace(title) ? "Panels" : title,
            ShowCaption = true,
            ShowToolTip = true,
            FitToSize = false,
            CheckOnClick = false,
            ScrollerType = ToolStripGalleryScrollerType.Standard,
            Dimensions = new Size(normalizedColumns, 6),
            DropDownDimensions = new Size(normalizedColumns, 16),
            ItemSize = new Size(220, 28),
            ItemTextImageRelation = TextImageRelation.ImageBeforeText,
            ItemDisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Size = new Size(240, 118),
            Margin = new Padding(2, 1, 2, 1),
            Padding = new Padding(2),
        };

        _logger.LogDebug("ToolStripGallery created for {Title}", title);
        return gallery;
    }

    #endregion

    #region StatusBarAdv

    public StatusBarAdv CreateSfStatusBar(Action<StatusBarAdv>? configure = null)
    {
        _logger.LogDebug("Creating SfStatusBar (StatusBarAdv)");

        var statusBar = new StatusBarAdv
        {
            Name = "StatusBarAdv",
            Dock = DockStyle.Bottom,
            Height = LayoutTokens.StatusBarHeight,
            Alignment = FlowAlignment.Far,
            Spacing = new Size(0, 0),
            SizingGrip = false,
            AutoHeightControls = true,
            ThemesEnabled = true,
            BorderStyle = BorderStyle.None,
            ThemeName = _currentTheme,
        };

        ApplyThemeAndInit(statusBar);
        configure?.Invoke(statusBar);
        return statusBar;
    }

    public StatusBarAdv CreateSfStatusBar(
        bool useSizingGrip,
        IReadOnlyCollection<StatusBarAdvPanel>? panels = null,
        Action<StatusBarAdv>? configure = null)
    {
        _logger.LogDebug("Creating SfStatusBar overload: SizingGrip={SizingGrip}", useSizingGrip);

        return CreateSfStatusBar(statusBar =>
        {
            statusBar.SizingGrip = useSizingGrip;
            if (panels != null && panels.Count > 0)
            {
                var panelArray = new StatusBarAdvPanel[panels.Count];
                var index = 0;
                foreach (var panel in panels)
                {
                    panelArray[index++] = panel;
                }

                statusBar.Panels = panelArray;
            }

            configure?.Invoke(statusBar);
        });
    }

    public StatusBarAdvPanel CreateStatusBarAdvPanel(
        string name,
        string text,
        int width,
        HorzFlowAlign alignment = HorzFlowAlign.Left,
        Action<StatusBarAdvPanel>? configure = null)
    {
        _logger.LogDebug("Creating StatusBarAdvPanel: {Name}", name);

        var panel = new StatusBarAdvPanel
        {
            Name = name,
            Text = text,
            Width = width,
            HAlign = alignment,
            AutoSize = false,
            BorderStyle = BorderStyle.None,
        };

        panel.AccessibleName = name;
        panel.AccessibleDescription = text;

        configure?.Invoke(panel);
        return panel;
    }

    #endregion

    #region SfListView

    public SfListView CreateSfListView(Action<SfListView>? configure = null)
    {
        _logger.LogDebug("Creating SfListView");

        var listView = new SfListView
        {
            Dock = DockStyle.Fill,
            SelectionMode = SelectionMode.One,
            AccessibilityEnabled = true,
            AllowSelectAll = false,
            AllowRecursiveChecking = false,
            AllowTriStateMode = false,
            HotTracking = true,
            ItemHeight = 28,
            ThemeName = _currentTheme,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
        };

        ApplyThemeAndInit(listView);
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
            CaseSensitive = false,
            AutoSortList = true,
            AutoSerialize = false,
            AutoAddItem = false,
            AllowListDelete = false,
            EnableDuplicateValues = false,
            AutoPersistentDropDownSize = true,
            AdjustHeightToItemCount = false,
            ShowCloseButton = false,
            ShowGripper = false,
            MaxNumberofSuggestion = 12,
            ShowColumnHeader = true,
            BorderType = AutoCompleteBorderTypes.Sizable,
            ThemeName = _currentTheme,
            AccessibleName = "AutoComplete",
            AccessibleDescription = "Auto-complete text input",
            HeaderFont = new Font("Segoe UI", 9F, FontStyle.Regular),
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
            ThemeName = _currentTheme,
            CursorMode = PdfViewerCursorMode.SelectTool,
            EnableContextMenu = true,
            EnableNotificationBar = true,
            IsTextSelectionEnabled = true,
            IsTextSearchEnabled = true,
            IsBookmarkEnabled = true,
            MinimumZoomPercentage = 25,
        };

        ApplyThemeAndInit(pdfViewer);
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
            Size = new Size(200, LayoutTokens.StandardControlHeight),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ThemeName = _currentTheme,
            CanOverrideStyle = true,
            MaxLength = short.MaxValue,
            CharacterCasing = CharacterCasing.Normal,
            TextAlign = HorizontalAlignment.Left,
            RightToLeft = RightToLeft.No,
            ShortcutsEnabled = true,
            AcceptsTab = false,
            AcceptsReturn = false,
            EnableTouchMode = false,
        };

        ApplyThemeAndInit(textBox);
        configure?.Invoke(textBox);
        return textBox;
    }

    public TextBoxExt CreateTextBoxExt(bool multiline, bool readOnly, Action<TextBoxExt>? configure = null)
    {
        _logger.LogDebug("Creating TextBoxExt overload: Multiline={Multiline}, ReadOnly={ReadOnly}", multiline, readOnly);

        return CreateTextBoxExt(textBox =>
        {
            textBox.Multiline = multiline;
            textBox.ReadOnly = readOnly;

            if (multiline)
            {
                textBox.ScrollBars = ScrollBars.Vertical;
                textBox.Height = Math.Max(textBox.Height, 80);
                textBox.AcceptsReturn = true;
                textBox.AcceptsTab = true;
            }
            else
            {
                textBox.ScrollBars = ScrollBars.None;
            }

            configure?.Invoke(textBox);
        });
    }

    public TextBoxExt CreateTextBoxExt(
        bool multiline,
        bool readOnly,
        bool passwordChar,
        Action<TextBoxExt>? configure = null)
    {
        _logger.LogDebug(
            "Creating TextBoxExt advanced overload: Multiline={Multiline}, ReadOnly={ReadOnly}, Password={Password}",
            multiline,
            readOnly,
            passwordChar);

        return CreateTextBoxExt(multiline, readOnly, textBox =>
        {
            textBox.UseSystemPasswordChar = passwordChar;
            configure?.Invoke(textBox);
        });
    }

    #endregion

    #region Dialog and Layout Controls

    public Form CreateDialogForm(Action<Form>? configure = null)
    {
        _logger.LogDebug("Creating dialog Form");

        var form = new Form
        {
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
        };

        form.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(form);
        return form;
    }

    public TableLayoutPanel CreateTableLayoutPanel(Action<TableLayoutPanel>? configure = null)
    {
        _logger.LogDebug("Creating TableLayoutPanel");

        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
        };

        tableLayout.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(tableLayout);
        return tableLayout;
    }

    public FlowLayoutPanel CreateFlowLayoutPanel(Action<FlowLayoutPanel>? configure = null)
    {
        _logger.LogDebug("Creating FlowLayoutPanel");

        var flowLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
        };

        flowLayout.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(flowLayout);
        return flowLayout;
    }

    public Label CreateLabel(Action<Label>? configure = null)
    {
        _logger.LogDebug("Creating Label");

        var label = new Label
        {
            AutoEllipsis = true,
        };

        label.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(label);
        return label;
    }

    public Label CreateLabel(string text, Action<Label>? configure = null)
    {
        _logger.LogDebug("Creating Label with text: {Text}", text);

        return CreateLabel(label =>
        {
            label.Text = text;
            configure?.Invoke(label);
        });
    }

    public GroupBox CreateGroupBox(Action<GroupBox>? configure = null)
    {
        _logger.LogDebug("Creating GroupBox");

        var groupBox = new GroupBox
        {
            AutoSize = true,
        };

        groupBox.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(groupBox);
        return groupBox;
    }

    public GroupBox CreateGroupBox(string text, Action<GroupBox>? configure = null)
    {
        _logger.LogDebug("Creating GroupBox with text: {Text}", text);

        return CreateGroupBox(groupBox =>
        {
            groupBox.Text = text;
            configure?.Invoke(groupBox);
        });
    }

    public LinkLabel CreateLinkLabel(Action<LinkLabel>? configure = null)
    {
        _logger.LogDebug("Creating LinkLabel");

        var linkLabel = new LinkLabel
        {
            AutoSize = true,
        };

        linkLabel.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(linkLabel);
        return linkLabel;
    }

    public ToolTip CreateToolTip(Action<ToolTip>? configure = null)
    {
        _logger.LogDebug("Creating ToolTip");

        var toolTip = new ToolTip();
        configure?.Invoke(toolTip);
        return toolTip;
    }

    public ErrorProvider CreateErrorProvider(Action<ErrorProvider>? configure = null)
    {
        _logger.LogDebug("Creating ErrorProvider");

        var errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink,
        };

        configure?.Invoke(errorProvider);
        return errorProvider;
    }

    public BindingSource CreateBindingSource(object? dataSource = null, Action<BindingSource>? configure = null)
    {
        _logger.LogDebug("Creating BindingSource");

        var bindingSource = new BindingSource();
        if (dataSource != null)
        {
            bindingSource.DataSource = dataSource;
        }

        configure?.Invoke(bindingSource);
        return bindingSource;
    }

    public FolderBrowserDialog CreateFolderBrowserDialog(Action<FolderBrowserDialog>? configure = null)
    {
        _logger.LogDebug("Creating FolderBrowserDialog");

        var dialog = new FolderBrowserDialog
        {
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };

        configure?.Invoke(dialog);
        return dialog;
    }

    public Panel CreatePanel(Action<Panel>? configure = null)
    {
        _logger.LogDebug("Creating Panel");

        var panel = new Panel
        {
            BorderStyle = BorderStyle.None,
        };

        panel.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(panel);
        return panel;
    }

    public Panel CreateAuthenticationSurfacePanel(Action<Panel>? configure = null)
    {
        _logger.LogDebug("Creating authentication surface panel");

        return CreatePanel(panel =>
        {
            panel.Dock = DockStyle.Fill;
            panel.Padding = LayoutTokens.GetScaled(LayoutTokens.PanelOuterPadding);
            panel.Margin = Padding.Empty;
            panel.AccessibleName = "Authentication surface";
            configure?.Invoke(panel);
        });
    }

    public TextBoxExt CreateAuthenticationTextBox(string accessibleName, bool password = false, Action<TextBoxExt>? configure = null)
    {
        _logger.LogDebug("Creating authentication TextBoxExt: {AccessibleName}", accessibleName);

        return CreateTextBoxExt(multiline: false, readOnly: false, passwordChar: password, textBox =>
        {
            textBox.Dock = DockStyle.Top;
            textBox.Width = 360;
            textBox.AccessibleName = accessibleName;
            textBox.AccessibleDescription = accessibleName;
            textBox.Margin = new Padding(0, 4, 0, 4);
            configure?.Invoke(textBox);
        });
    }

    public Label CreateAuthenticationStatusLabel(Action<Label>? configure = null)
    {
        _logger.LogDebug("Creating authentication status label");

        return CreateLabel(label =>
        {
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            label.Height = LayoutTokens.GetScaled(28);
            label.Padding = new Padding(0, 8, 0, 0);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AccessibleName = "Authentication status";
            configure?.Invoke(label);
        });
    }

    public PanelHeader CreatePanelHeader(Action<PanelHeader>? configure = null)
    {
        _logger.LogDebug("Creating PanelHeader");

        var header = new PanelHeader(this);
        header.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(header);
        return header;
    }

    public KpiCardControl CreateKpiCardControl(Action<KpiCardControl>? configure = null)
    {
        _logger.LogDebug("Creating KpiCardControl");

        var card = new KpiCardControl();
        card.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(card);
        return card;
    }

    public StatusStrip CreateStatusStrip(Action<StatusStrip>? configure = null)
    {
        _logger.LogDebug("Creating StatusStrip");

        var statusStrip = new StatusStrip();
        statusStrip.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(statusStrip);
        return statusStrip;
    }

    public ToolStripStatusLabel CreateToolStripStatusLabel(Action<ToolStripStatusLabel>? configure = null)
    {
        _logger.LogDebug("Creating ToolStripStatusLabel");

        var statusLabel = new ToolStripStatusLabel();
        configure?.Invoke(statusLabel);
        return statusLabel;
    }

    public NoDataOverlay CreateNoDataOverlay(Action<NoDataOverlay>? configure = null)
    {
        _logger.LogDebug("Creating NoDataOverlay");

        var overlay = new NoDataOverlay(this)
        {
            Dock = DockStyle.Fill,
            Visible = false,
        };

        configure?.Invoke(overlay);
        return overlay;
    }

    public CsvMappingWizardPanel CreateCsvMappingWizardPanel(Action<CsvMappingWizardPanel>? configure = null)
    {
        _logger.LogDebug("Creating CsvMappingWizardPanel");

        var wizardPanel = new CsvMappingWizardPanel(_logger, this)
        {
            Dock = DockStyle.Fill,
        };

        wizardPanel.ApplySyncfusionTheme(_currentTheme, _logger);
        configure?.Invoke(wizardPanel);
        return wizardPanel;
    }

    /// <summary>
    /// Shows a Syncfusion message box. Falls back to the WinForms message box if Syncfusion dialog rendering fails.
    /// Reference: https://help.syncfusion.com/windowsforms/messagebox/overview
    /// </summary>
    public DialogResult ShowMessageBox(
        IWin32Window? owner,
        string message,
        string title,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.Information,
        bool playNotificationSound = false)
    {
        return ShowMessageBox(new SyncfusionMessageBoxOptions
        {
            Owner = owner,
            Message = message,
            Title = title,
            Buttons = buttons,
            Icon = icon,
            PlayNotificationSound = playNotificationSound,
        });
    }

    /// <summary>
    /// Shows a Syncfusion MessageBoxAdv with semantic colors for notification intent.
    /// </summary>
    public DialogResult ShowSemanticMessageBox(
        IWin32Window? owner,
        string message,
        string title,
        MessageSemanticKind semanticKind,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1,
        bool playNotificationSound = false,
        string? details = null,
        bool enableDropShadow = true)
    {
        var icon = semanticKind switch
        {
            MessageSemanticKind.Success => MessageBoxIcon.Information,
            MessageSemanticKind.Warning => MessageBoxIcon.Warning,
            MessageSemanticKind.Error => MessageBoxIcon.Error,
            _ => MessageBoxIcon.Information,
        };

        return ShowMessageBox(new SyncfusionMessageBoxOptions
        {
            Owner = owner,
            Message = message,
            Title = title,
            Buttons = buttons,
            Icon = icon,
            DefaultButton = defaultButton,
            PlayNotificationSound = playNotificationSound,
            Details = details,
            SemanticKind = semanticKind,
            DropShadow = enableDropShadow,
        });
    }

    /// <summary>
    /// Shows a Syncfusion MessageBoxAdv with advanced options from documented API features.
    /// Wrapper sets and restores static MessageBoxAdv properties to avoid leaking style/state changes.
    /// </summary>
    public DialogResult ShowMessageBox(SyncfusionMessageBoxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PlayNotificationSound)
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to play notification sound for message box '{Title}'", options.Title);
            }
        }

        var messageBoxType = typeof(Syncfusion.Windows.Forms.MessageBoxAdv);
        Dictionary<string, object?>? originalStaticValues = null;

        try
        {
            originalStaticValues = CaptureMessageBoxStaticValues(messageBoxType);
            ApplyMessageBoxStaticOptions(messageBoxType, options);

            if (TryInvokeMessageBoxAdvShow(messageBoxType, options, out var syncfusionResult))
            {
                return syncfusionResult;
            }

            _logger.LogWarning("MessageBoxAdv overload resolution failed; falling back to WinForms MessageBox for '{Title}'", options.Title);
            return ShowWinFormsFallback(options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Syncfusion MessageBoxAdv failed; falling back to WinForms MessageBox for '{Title}'", options.Title);
            return ShowWinFormsFallback(options);
        }
        finally
        {
            if (originalStaticValues != null)
            {
                RestoreMessageBoxStaticValues(messageBoxType, originalStaticValues);
            }
        }
    }

    private static readonly string[] MessageBoxStaticPropertyNames =
    {
        "MessageBoxStyle",
        "Office2007Theme",
        "Office2010Theme",
        "Office2013Theme",
        "Office2016Theme",
        "MetroColorTable",
        "CanResize",
        "DropShadow",
        "RightToLeft",
        "Details",
        "Font",
    };

    private static DialogResult ShowWinFormsFallback(SyncfusionMessageBoxOptions options)
    {
        return options.Owner == null
            ? MessageBox.Show(options.Message, options.Title, options.Buttons, options.Icon, options.DefaultButton, options.Options)
            : MessageBox.Show(options.Owner, options.Message, options.Title, options.Buttons, options.Icon, options.DefaultButton, options.Options);
    }

    private static Dictionary<string, object?> CaptureMessageBoxStaticValues(Type messageBoxType)
    {
        var snapshot = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var propertyName in MessageBoxStaticPropertyNames)
        {
            var property = messageBoxType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property is { CanRead: true })
            {
                snapshot[propertyName] = property.GetValue(null);
            }
        }

        return snapshot;
    }

    private void ApplyMessageBoxStaticOptions(Type messageBoxType, SyncfusionMessageBoxOptions options)
    {
        ApplySemanticMessageBoxOptions(messageBoxType, options);

        SetStaticPropertyIfExists(messageBoxType, "MessageBoxStyle", options.MessageBoxStyle);
        SetStaticPropertyIfExists(messageBoxType, "Office2007Theme", options.Office2007Theme);
        SetStaticPropertyIfExists(messageBoxType, "Office2010Theme", options.Office2010Theme);
        SetStaticPropertyIfExists(messageBoxType, "Office2013Theme", options.Office2013Theme);
        SetStaticPropertyIfExists(messageBoxType, "Office2016Theme", options.Office2016Theme);
        SetStaticPropertyIfExists(messageBoxType, "CanResize", options.CanResize);
        SetStaticPropertyIfExists(messageBoxType, "DropShadow", options.DropShadow);
        SetStaticPropertyIfExists(messageBoxType, "RightToLeft", options.RightToLeft);
        SetStaticPropertyIfExists(messageBoxType, "Details", options.Details);
        SetStaticPropertyIfExists(messageBoxType, "Font", options.Font);
    }

    private void ApplySemanticMessageBoxOptions(Type messageBoxType, SyncfusionMessageBoxOptions options)
    {
        if (options.SemanticKind == MessageSemanticKind.None)
        {
            return;
        }

        // Semantic intent uses Metro style so caption/button colors are consistent and obvious.
        if (string.IsNullOrWhiteSpace(options.MessageBoxStyle))
        {
            SetStaticPropertyIfExists(messageBoxType, "MessageBoxStyle", "Metro");
        }

        var metroColorProperty = messageBoxType.GetProperty("MetroColorTable", BindingFlags.Public | BindingFlags.Static);
        if (metroColorProperty is not { CanWrite: true })
        {
            return;
        }

        var metroColorTable = Activator.CreateInstance(metroColorProperty.PropertyType);
        if (metroColorTable == null)
        {
            return;
        }

        var palette = GetSemanticPalette(options.SemanticKind);

        // MessageBoxAdv exposes MetroColorTable as its own styling surface. Treat this as
        // a semantic dialog override, not a general child-control theme replacement.
        SetInstancePropertyIfExists(metroColorTable, "CaptionBarColor", palette.CaptionBarColor);
        SetInstancePropertyIfExists(metroColorTable, "CaptionForeColor", palette.CaptionForeColor);
        SetInstancePropertyIfExists(metroColorTable, "BackColor", palette.BackColor);
        SetInstancePropertyIfExists(metroColorTable, "ForeColor", palette.ForeColor);
        SetInstancePropertyIfExists(metroColorTable, "BorderColor", palette.BorderColor);

        SetInstancePropertyIfExists(metroColorTable, "OkButtonBackColor", palette.ButtonColor);
        SetInstancePropertyIfExists(metroColorTable, "YesButtonBackColor", palette.ButtonColor);
        SetInstancePropertyIfExists(metroColorTable, "NoButtonBackColor", palette.SecondaryButtonColor);
        SetInstancePropertyIfExists(metroColorTable, "CancelButtonBackColor", palette.SecondaryButtonColor);
        SetInstancePropertyIfExists(metroColorTable, "AbortButtonBackColor", palette.SecondaryButtonColor);
        SetInstancePropertyIfExists(metroColorTable, "RetryButtonBackColor", palette.ButtonColor);
        SetInstancePropertyIfExists(metroColorTable, "IgnoreButtonBackColor", palette.SecondaryButtonColor);

        SetStaticPropertyIfExists(messageBoxType, "MetroColorTable", metroColorTable);
    }

    private static SemanticColorPalette GetSemanticPalette(MessageSemanticKind semanticKind)
    {
        return semanticKind switch
        {
            MessageSemanticKind.Success => new SemanticColorPalette(
                CaptionBarColor: Color.Green,
                CaptionForeColor: Color.White,
                BackColor: Color.Honeydew,
                ForeColor: Color.Black,
                BorderColor: Color.ForestGreen,
                ButtonColor: Color.ForestGreen,
                SecondaryButtonColor: Color.DarkSeaGreen),
            MessageSemanticKind.Warning => new SemanticColorPalette(
                CaptionBarColor: Color.Orange,
                CaptionForeColor: Color.Black,
                BackColor: Color.LemonChiffon,
                ForeColor: Color.Black,
                BorderColor: Color.DarkOrange,
                ButtonColor: Color.DarkOrange,
                SecondaryButtonColor: Color.Khaki),
            MessageSemanticKind.Error => new SemanticColorPalette(
                CaptionBarColor: Color.Red,
                CaptionForeColor: Color.White,
                BackColor: Color.MistyRose,
                ForeColor: Color.Black,
                BorderColor: Color.DarkRed,
                ButtonColor: Color.DarkRed,
                SecondaryButtonColor: Color.IndianRed),
            _ => new SemanticColorPalette(
                CaptionBarColor: Color.SteelBlue,
                CaptionForeColor: Color.White,
                BackColor: Color.AliceBlue,
                ForeColor: Color.Black,
                BorderColor: Color.DodgerBlue,
                ButtonColor: Color.DodgerBlue,
                SecondaryButtonColor: Color.LightSteelBlue),
        };
    }

    private static void SetInstancePropertyIfExists(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is not { CanWrite: true })
        {
            return;
        }

        if (!property.PropertyType.IsInstanceOfType(value))
        {
            return;
        }

        property.SetValue(target, value);
    }

    private readonly record struct SemanticColorPalette(
        Color CaptionBarColor,
        Color CaptionForeColor,
        Color BackColor,
        Color ForeColor,
        Color BorderColor,
        Color ButtonColor,
        Color SecondaryButtonColor);

    private void RestoreMessageBoxStaticValues(Type messageBoxType, IReadOnlyDictionary<string, object?> originalValues)
    {
        foreach (var pair in originalValues)
        {
            SetStaticPropertyIfExists(messageBoxType, pair.Key, pair.Value);
        }
    }

    private void SetStaticPropertyIfExists(Type messageBoxType, string propertyName, object? value)
    {
        if (value == null)
        {
            return;
        }

        var property = messageBoxType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        if (property is not { CanWrite: true })
        {
            return;
        }

        try
        {
            var converted = ConvertMessageBoxPropertyValue(value, property.PropertyType);
            if (converted != null)
            {
                property.SetValue(null, converted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to set MessageBoxAdv static property {PropertyName}", propertyName);
        }
    }

    private static object? ConvertMessageBoxPropertyValue(object value, Type targetType)
    {
        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType.IsEnum && value is string enumName && !string.IsNullOrWhiteSpace(enumName))
        {
            return Enum.Parse(targetType, enumName, ignoreCase: true);
        }

        if (targetType == typeof(bool) && value is bool boolValue)
        {
            return boolValue;
        }

        if (targetType == typeof(string) && value is string stringValue)
        {
            return stringValue;
        }

        if (targetType == typeof(Font) && value is Font fontValue)
        {
            return fontValue;
        }

        return null;
    }

    private static bool TryInvokeMessageBoxAdvShow(Type messageBoxType, SyncfusionMessageBoxOptions options, out DialogResult result)
    {
        result = DialogResult.None;

        if (options.Owner != null)
        {
            if (TryInvokeShow(messageBoxType,
                new[] { typeof(IWin32Window), typeof(string), typeof(string), typeof(MessageBoxButtons), typeof(MessageBoxIcon), typeof(MessageBoxDefaultButton), typeof(MessageBoxOptions) },
                new object[] { options.Owner, options.Message, options.Title, options.Buttons, options.Icon, options.DefaultButton, options.Options },
                out result))
            {
                return true;
            }

            if (TryInvokeShow(messageBoxType,
                new[] { typeof(IWin32Window), typeof(string), typeof(string), typeof(MessageBoxButtons), typeof(MessageBoxIcon), typeof(MessageBoxDefaultButton) },
                new object[] { options.Owner, options.Message, options.Title, options.Buttons, options.Icon, options.DefaultButton },
                out result))
            {
                return true;
            }

            if (TryInvokeShow(messageBoxType,
                new[] { typeof(IWin32Window), typeof(string), typeof(string), typeof(MessageBoxButtons), typeof(MessageBoxIcon) },
                new object[] { options.Owner, options.Message, options.Title, options.Buttons, options.Icon },
                out result))
            {
                return true;
            }
        }
        else
        {
            if (TryInvokeShow(messageBoxType,
                new[] { typeof(string), typeof(string), typeof(MessageBoxButtons), typeof(MessageBoxIcon), typeof(MessageBoxDefaultButton), typeof(MessageBoxOptions) },
                new object[] { options.Message, options.Title, options.Buttons, options.Icon, options.DefaultButton, options.Options },
                out result))
            {
                return true;
            }

            if (TryInvokeShow(messageBoxType,
                new[] { typeof(string), typeof(string), typeof(MessageBoxButtons), typeof(MessageBoxIcon), typeof(MessageBoxDefaultButton) },
                new object[] { options.Message, options.Title, options.Buttons, options.Icon, options.DefaultButton },
                out result))
            {
                return true;
            }

            if (TryInvokeShow(messageBoxType,
                new[] { typeof(string), typeof(string), typeof(MessageBoxButtons), typeof(MessageBoxIcon) },
                new object[] { options.Message, options.Title, options.Buttons, options.Icon },
                out result))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryInvokeShow(Type messageBoxType, Type[] parameterTypes, object[] args, out DialogResult result)
    {
        result = DialogResult.None;

        var method = messageBoxType.GetMethod("Show", BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
        if (method == null)
        {
            return false;
        }

        var invocationResult = method.Invoke(null, args);
        if (invocationResult is DialogResult dialogResult)
        {
            result = dialogResult;
            return true;
        }

        return false;
    }

    public sealed class SyncfusionMessageBoxOptions
    {
        public IWin32Window? Owner { get; init; }

        public required string Message { get; init; }

        public string Title { get; init; } = "Wiley Widget";

        public MessageBoxButtons Buttons { get; init; } = MessageBoxButtons.OK;

        public MessageBoxIcon Icon { get; init; } = MessageBoxIcon.Information;

        public MessageBoxDefaultButton DefaultButton { get; init; } = MessageBoxDefaultButton.Button1;

        public MessageBoxOptions Options { get; init; } = 0;

        public bool PlayNotificationSound { get; init; }

        public MessageSemanticKind SemanticKind { get; init; } = MessageSemanticKind.None;

        // Syncfusion MessageBoxAdv styling and behavior options.
        public string? MessageBoxStyle { get; init; }

        public string? Office2007Theme { get; init; }

        public string? Office2010Theme { get; init; }

        public string? Office2013Theme { get; init; }

        public string? Office2016Theme { get; init; }

        public bool? CanResize { get; init; }

        public bool? DropShadow { get; init; }

        public bool? RightToLeft { get; init; }

        public string? Details { get; init; }

        public Font? Font { get; init; }
    }

    public enum MessageSemanticKind
    {
        None,
        Information,
        Success,
        Warning,
        Error,
    }

    #endregion

    #region SfComboBox

    public SfComboBox CreateSfComboBox(Action<SfComboBox>? configure = null)
    {
        _logger.LogDebug("Creating SfComboBox");

        var defaultControlHeight = LayoutTokens.StandardControlHeightLarge;

        var comboBox = new SfComboBox
        {
            DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
            Width = 220,
            Height = defaultControlHeight,
            MinimumSize = new Size(0, defaultControlHeight),
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSuggestMode = Syncfusion.WinForms.ListView.Enums.AutoCompleteSuggestMode.Contains,
            AllowCaseSensitiveOnAutoComplete = false,
            AllowSelectAll = false,
            MaxDropDownItems = 10,
            AllowDropDownResize = false,
            AllowNull = true,
            ShowClearButton = true,
            ShowToolTip = true,
            AutoCompleteSuggestDelay = 200,
            DelimiterChar = ",",
            Watermark = "Select",
        };

        ApplyThemeAndInit(comboBox);
        configure?.Invoke(comboBox);
        PropagateComboAccessibility(comboBox);
        return comboBox;
    }

    public SfComboBox CreateSfComboBox<TItem>(
        IEnumerable<TItem>? dataSource = null,
        string? displayMember = "Name",
        string? valueMember = "Id",
        bool allowFiltering = true,
        bool autoComplete = true,
        Action<SfComboBox>? configure = null)
    {
        var comboBox = CreateSfComboBox(combo =>
        {
            if (dataSource != null)
            {
                combo.DataSource = new List<TItem>(dataSource);
            }

            if (!string.IsNullOrWhiteSpace(displayMember))
            {
                combo.DisplayMember = displayMember;
            }

            if (!string.IsNullOrWhiteSpace(valueMember))
            {
                combo.ValueMember = valueMember;
            }

            combo.AutoCompleteMode = autoComplete ? AutoCompleteMode.SuggestAppend : AutoCompleteMode.None;
            combo.AutoCompleteSuggestMode = allowFiltering
                ? Syncfusion.WinForms.ListView.Enums.AutoCompleteSuggestMode.Contains
                : Syncfusion.WinForms.ListView.Enums.AutoCompleteSuggestMode.StartsWith;
            combo.DropDownStyle = allowFiltering
                ? Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDown
                : Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            configure?.Invoke(combo);
        });

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
            DateTimeEditingMode = Syncfusion.WinForms.Input.Enums.DateTimeEditingMode.Mask,
            AllowNull = true,
            AllowValueChangeOnMouseWheel = false,
            MinDateTime = DateTime.MinValue,
            MaxDateTime = DateTime.MaxValue,
            ShowDropDown = true,
            ShowToolTip = true,
            ShowUpDown = false,
            Watermark = "Select date...",
        };

        ApplyThemeAndInit(dateTimeEdit);
        configure?.Invoke(dateTimeEdit);
        return dateTimeEdit;
    }

    public SfDateTimeEdit CreateSfDateTimeEdit(
        Syncfusion.WinForms.Input.Enums.DateTimePattern dateTimePattern,
        DateTime? minDate = null,
        DateTime? maxDate = null,
        string? format = null,
        bool allowNull = true,
        bool readOnly = false,
        Action<SfDateTimeEdit>? configure = null)
    {
        _logger.LogDebug(
            "Creating SfDateTimeEdit overload: Pattern={Pattern}, AllowNull={AllowNull}, ReadOnly={ReadOnly}",
            dateTimePattern,
            allowNull,
            readOnly);

        return CreateSfDateTimeEdit(dateTimeEdit =>
        {
            dateTimeEdit.DateTimePattern = dateTimePattern;
            dateTimeEdit.MinDateTime = minDate ?? DateTime.MinValue;
            dateTimeEdit.MaxDateTime = maxDate ?? DateTime.MaxValue;
            dateTimeEdit.AllowNull = allowNull;
            dateTimeEdit.ReadOnly = readOnly;

            if (!string.IsNullOrWhiteSpace(format))
            {
                dateTimeEdit.DateTimePattern = Syncfusion.WinForms.Input.Enums.DateTimePattern.Custom;
                dateTimeEdit.Format = format;
            }

            configure?.Invoke(dateTimeEdit);
        });
    }

    #endregion

    #region DateTimePickerAdv

    public DateTimePickerAdv CreateDateTimePickerAdv(Action<DateTimePickerAdv>? configure = null)
    {
        _logger.LogDebug("Creating DateTimePickerAdv");

        var datePicker = new DateTimePickerAdv
        {
            Format = DateTimePickerFormat.Short,
            ShowCheckBox = false,
            ShowUpDown = false,
            Width = 165,
            Height = LayoutTokens.StandardControlHeight,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ThemeName = _currentTheme,
        };

        ApplyThemeAndInit(datePicker);
        configure?.Invoke(datePicker);
        return datePicker;
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
            AllowNull = false,
            MinValue = 0D,
            MaxValue = 1000000000D,
            Value = 0D,
            InterceptArrowKeys = true,
            HideTrailingZeros = false,
        };

        ApplyThemeAndInit(numericTextBox);
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
            AutoCheck = true,
            Checked = false,
            CheckState = CheckState.Unchecked,
            AccessibilityEnabled = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
        };

        ApplyThemeAndInit(checkBox);
        configure?.Invoke(checkBox);
        return checkBox;
    }

    #endregion

    #region NumericUpDownExt

    public NumericUpDownExt CreateNumericUpDownExt(Action<NumericUpDownExt>? configure = null)
    {
        _logger.LogDebug("Creating NumericUpDownExt");

        var numericUpDown = new NumericUpDownExt
        {
            Width = 80,
            Height = LayoutTokens.StandardControlHeight,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Increment = 1,
            DecimalPlaces = 2,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right,
            BorderStyle = BorderStyle.FixedSingle,
            EnableTouchMode = false,
            ReadOnly = false,
            MaxLength = 12,
            TabStop = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
        };

        ApplyThemeAndInit(numericUpDown);
        configure?.Invoke(numericUpDown);
        return numericUpDown;
    }

    public NumericUpDownExt CreateNumericUpDownExt(
        decimal minValue,
        decimal maxValue,
        decimal increment = 1M,
        int decimalPlaces = 0,
        bool readOnly = false,
        Action<NumericUpDownExt>? configure = null)
    {
        _logger.LogDebug(
            "Creating NumericUpDownExt overload: Min={Min}, Max={Max}, Increment={Increment}, DecimalPlaces={DecimalPlaces}",
            minValue,
            maxValue,
            increment,
            decimalPlaces);

        return CreateNumericUpDownExt(numericUpDown =>
        {
            numericUpDown.Minimum = minValue;
            numericUpDown.Maximum = maxValue;
            numericUpDown.Increment = increment;
            numericUpDown.DecimalPlaces = decimalPlaces;
            numericUpDown.ReadOnly = readOnly;
            configure?.Invoke(numericUpDown);
        });
    }

    public NumericUpDownExt CreateSfNumericUpDown(Action<NumericUpDownExt>? configure = null)
    {
        _logger.LogDebug("Creating SfNumericUpDown alias (NumericUpDownExt)");
        return CreateNumericUpDownExt(configure);
    }

    public NumericUpDownExt CreateSfNumericUpDown(
        decimal minValue,
        decimal maxValue,
        decimal increment = 1M,
        int decimalPlaces = 0,
        bool readOnly = false,
        Action<NumericUpDownExt>? configure = null)
    {
        _logger.LogDebug("Creating SfNumericUpDown overload alias (NumericUpDownExt)");
        return CreateNumericUpDownExt(minValue, maxValue, increment, decimalPlaces, readOnly, configure);
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
            SplitterWidth = 13,
            BorderStyle = BorderStyle.None,
            ThemeName = _currentTheme,
        };

        ApplyThemeAndInit(splitContainer);
        configure?.Invoke(splitContainer);
        return splitContainer;
    }

    #endregion

    #region ProgressBarAdv

    public ProgressBarAdv CreateSfProgressBar(
        bool marquee = false,
        int min = 0,
        int max = 100,
        Action<ProgressBarAdv>? configure = null)
    {
        _logger.LogDebug("Creating SfProgressBar: Marquee={Marquee}, Min={Min}, Max={Max}", marquee, min, max);

        var minimum = min;
        var maximum = max <= min ? min + 1 : max;

        return CreateProgressBarAdv(progressBar =>
        {
            progressBar.Dock = DockStyle.Fill;
            progressBar.Minimum = minimum;
            progressBar.Maximum = maximum;
            progressBar.Value = minimum;
            progressBar.TextVisible = false;
            progressBar.BorderStyle = BorderStyle.FixedSingle;
            progressBar.ProgressStyle = marquee ? ProgressBarStyles.WaitingGradient : ProgressBarStyles.Metro;
            configure?.Invoke(progressBar);
        });
    }

    public ProgressBarAdv CreateProgressBarAdv(Action<ProgressBarAdv>? configure = null)
    {
        _logger.LogDebug("Creating ProgressBarAdv");

        var progressBar = new ProgressBarAdv
        {
            Size = new Size(200, 16),
            ProgressStyle = ProgressBarStyles.Metro,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Step = 1,
            TextVisible = false,
            BorderStyle = BorderStyle.FixedSingle,
            ThemeName = _currentTheme,
            TabStop = false,
        };

        ApplyThemeAndInit(progressBar);
        configure?.Invoke(progressBar);
        return progressBar;
    }

    public ProgressBarAdv CreateProgressBarAdv(bool marquee, int min = 0, int max = 100, Action<ProgressBarAdv>? configure = null)
    {
        _logger.LogDebug("Creating ProgressBarAdv overload: Marquee={Marquee}, Min={Min}, Max={Max}", marquee, min, max);
        return CreateSfProgressBar(marquee, min, max, configure);
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

    public static void ApplyThemeToAllControls(Control container, string? themeName = null, ILogger? logger = null)
    {
        if (container == null || container.IsDisposed)
        {
            return;
        }

        var resolvedTheme = string.IsNullOrWhiteSpace(themeName)
            ? SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme
            : themeName;

        resolvedTheme = WileyWidget.WinForms.Themes.ThemeColors.ValidateTheme(resolvedTheme, logger);
        WileyWidget.WinForms.Themes.ThemeColors.EnsureThemeAssemblyLoadedForTheme(resolvedTheme, logger);

        ApplyThemeRecursive(container, resolvedTheme, logger);
    }

    private static void ApplyThemeRecursive(Control control, string themeName, ILogger? logger)
    {
        if (control.IsDisposed)
        {
            return;
        }

        ISupportInitialize? initialize = control as ISupportInitialize;

        try
        {
            initialize?.BeginInit();
            ApplyExplicitThemeName(control, themeName);
            SfSkinManager.SetVisualStyle(control, themeName);
            control.Invalidate(true);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to apply theme to control {ControlType}", control.GetType().Name);
        }
        finally
        {
            try
            {
                initialize?.EndInit();
            }
            catch
            {
                // Best-effort cleanup for controls that reject EndInit after failed BeginInit.
            }
        }

        try
        {
            // Some deferred Syncfusion containers restore their previous theme during EndInit.
            // Reassert the active theme after initialization so runtime theme switches stay consistent.
            ApplyExplicitThemeName(control, themeName);
            SfSkinManager.SetVisualStyle(control, themeName);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to reassert theme after initialization for control {ControlType}", control.GetType().Name);
        }

        foreach (Control child in control.Controls)
        {
            ApplyThemeRecursive(child, themeName, logger);
        }
    }

    private static void ApplyExplicitThemeName(Control control, string themeName)
    {
        var themeNameProperty = control.GetType().GetProperty(
            "ThemeName",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.IgnoreCase);

        if (themeNameProperty?.CanWrite == true && themeNameProperty.PropertyType == typeof(string))
        {
            try
            {
                themeNameProperty.SetValue(control, themeName);
            }
            catch
            {
                // Best-effort only; SetVisualStyle below remains authoritative.
            }
        }

        switch (control)
        {
            case SfDataGrid dataGrid:
                dataGrid.ThemeName = themeName;
                break;
            case SfButton button:
                button.ThemeName = themeName;
                break;
            case TextBoxExt textBox:
                textBox.ThemeName = themeName;
                break;
            case SfComboBox comboBox:
                comboBox.ThemeName = themeName;
                break;
            case RibbonControlAdv ribbon:
                ribbon.ThemeName = themeName;
                break;
            case SfDateTimeEdit dateTimeEdit:
                dateTimeEdit.ThemeName = themeName;
                break;
            case SfNumericTextBox numericTextBox:
                numericTextBox.ThemeName = themeName;
                break;
            case DateTimePickerAdv dateTimePicker:
                dateTimePicker.ThemeName = themeName;
                break;
            case CheckBoxAdv checkBox:
                checkBox.ThemeName = themeName;
                break;
            case PdfViewerControl pdfViewer:
                pdfViewer.ThemeName = themeName;
                break;
            case SfListView listView:
                listView.ThemeName = themeName;
                break;
            case SplitContainerAdv splitContainer:
                splitContainer.ThemeName = themeName;
                break;
            case ProgressBarAdv progressBar:
                progressBar.ThemeName = themeName;
                break;
            case StatusBarAdv statusBar:
                statusBar.ThemeName = themeName;
                break;
        }
    }

    // ── Static helpers (used by EnterpriseVitalSignsPanel without DI) ─────

    // ── Static helpers (used by EnterpriseVitalSignsPanel without DI) ─────

    /// <summary>
    /// Creates a RadialGauge showing break-even ratio for an enterprise.
    /// </summary>
    public RadialGauge CreateRadialGauge(
        float value = 0,
        float minimum = 0,
        float maximum = 100,
        Action<RadialGauge>? configure = null)
    {
        _logger.LogDebug("Creating RadialGauge: Value={Value}, Min={Min}, Max={Max}", value, minimum, maximum);

        var clampedMaximum = maximum <= minimum ? minimum + 1 : maximum;
        var gauge = new RadialGauge
        {
            Dock = DockStyle.Fill,
            Value = Math.Clamp(value, minimum, clampedMaximum),
            MinimumValue = minimum,
            MaximumValue = clampedMaximum,
            MajorDifference = 10F,
            MinorDifference = 2F,
            ShowTicks = true,
            ShowScaleLabel = true,
            ShowGaugeValue = true,
            NeedleStyle = NeedleStyle.Pointer,
            ReadOnly = true,
            ThemeName = _currentTheme,
        };

        ApplyThemeAndInit(gauge);
        configure?.Invoke(gauge);
        return gauge;
    }

    public LinearGauge CreateLinearGauge(
        int value = 0,
        int minimum = 0,
        int maximum = 100,
        Action<LinearGauge>? configure = null)
    {
        _logger.LogDebug("Creating LinearGauge: Value={Value}, Min={Min}, Max={Max}", value, minimum, maximum);

        var clampedMaximum = maximum <= minimum ? minimum + 1 : maximum;
        var gauge = new LinearGauge
        {
            Dock = DockStyle.Fill,
            Value = Math.Clamp(value, minimum, clampedMaximum),
            MinimumValue = minimum,
            MaximumValue = clampedMaximum,
            MajorDifference = 10,
            MinorTickCount = 4,
            ShowNeedle = true,
            ShowScaleLabel = true,
            ReadOnly = true,
            ThemeName = _currentTheme,
        };

        ApplyThemeAndInit(gauge);
        configure?.Invoke(gauge);
        return gauge;
    }

    public RadialGauge CreateCircularGauge(double currentRatio, string enterpriseName)
    {
        _logger.LogDebug("Creating CircularGauge: {Enterprise} = {Ratio:F1}%", enterpriseName, currentRatio);

        var gauge = CreateRadialGauge((float)currentRatio, 0, 150, radialGauge =>
        {
            radialGauge.GaugeLabel = $"{currentRatio:F1}%";
        });

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
            ForeColor = currentRatio >= 100
                ? WileyWidget.WinForms.Themes.ThemeColors.Success
                : WileyWidget.WinForms.Themes.ThemeColors.Error,
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
    /// Creates a detailed multi-series enterprise line chart for one enterprise.
    /// </summary>
    public ChartControl CreateEnterpriseChart(EnterpriseSnapshot snapshot)
    {
        _logger.LogDebug("Creating EnterpriseChart: {Enterprise}", snapshot.Name);

        var priorRevenue = snapshot.PriorYearRevenue != 0 ? snapshot.PriorYearRevenue : snapshot.Revenue;
        var priorExpenses = snapshot.PriorYearExpenses != 0 ? snapshot.PriorYearExpenses : snapshot.Expenses;
        var currentRevenue = snapshot.CurrentYearEstimatedRevenue != 0 ? snapshot.CurrentYearEstimatedRevenue : snapshot.Revenue;
        var currentExpenses = snapshot.CurrentYearEstimatedExpenses != 0 ? snapshot.CurrentYearEstimatedExpenses : snapshot.Expenses;
        var budgetRevenue = snapshot.BudgetYearRevenue != 0 ? snapshot.BudgetYearRevenue : snapshot.Revenue;
        var budgetExpenses = snapshot.BudgetYearExpenses != 0 ? snapshot.BudgetYearExpenses : snapshot.Expenses;

        var chart = CreateChartControl($"{snapshot.Name} — Enterprise Snapshot", configuredChart =>
        {
            configuredChart.PrimaryXAxis.Title = "Rate Study View";
            configuredChart.PrimaryXAxis.DrawGrid = false;
            configuredChart.PrimaryXAxis.LabelIntersectAction = ChartLabelIntersectAction.None;
            configuredChart.PrimaryXAxis.RangePaddingType = ChartAxisRangePaddingType.None;
            configuredChart.PrimaryXAxis.ValueType = ChartValueType.Category;
            configuredChart.PrimaryYAxis.Title = "Amount ($)";
            configuredChart.PrimaryYAxis.DrawGrid = true;
            configuredChart.PrimaryYAxis.RangePaddingType = ChartAxisRangePaddingType.Calculate;
            configuredChart.ShowLegend = true;
            configuredChart.Legend.Visible = true;
            configuredChart.Legend.Position = ChartDock.Top;
            configuredChart.Legend.Alignment = ChartAlignment.Center;
            configuredChart.LegendsPlacement = ChartPlacement.Outside;
            configuredChart.AccessibleName = $"{snapshot.Name} enterprise line chart";
            configuredChart.AccessibleDescription = "Income, expenses, and break-even comparison across prior actual, current estimate, and budget goal.";
        });

        var incomeSeries = new ChartSeries("Income", ChartSeriesType.Column);
        incomeSeries.Style.Border.Width = 2;
        incomeSeries.Style.Border.Color = Color.ForestGreen;
        incomeSeries.Style.Interior = new BrushInfo(Color.ForestGreen);
        incomeSeries.Style.Symbol.Size = LayoutTokens.GetScaled(LayoutTokens.ChartMarkerSize);
        incomeSeries.Points.Add("Prior Actual", (double)priorRevenue);
        incomeSeries.Points.Add("Current Estimate", (double)currentRevenue);
        incomeSeries.Points.Add("FY Goal", (double)budgetRevenue);
        incomeSeries.PointsToolTipFormat = "{1:C0}";
        chart.Series.Add(incomeSeries);

        var expenseSeries = new ChartSeries("Expenses", ChartSeriesType.Column);
        expenseSeries.Style.Border.Width = 2;
        expenseSeries.Style.Border.Color = Color.IndianRed;
        expenseSeries.Style.Interior = new BrushInfo(Color.IndianRed);
        expenseSeries.Style.Symbol.Size = LayoutTokens.GetScaled(LayoutTokens.ChartMarkerSize);
        expenseSeries.Points.Add("Prior Actual", (double)priorExpenses);
        expenseSeries.Points.Add("Current Estimate", (double)currentExpenses);
        expenseSeries.Points.Add("FY Goal", (double)budgetExpenses);
        expenseSeries.PointsToolTipFormat = "{1:C0}";
        chart.Series.Add(expenseSeries);

        var breakEvenSeries = new ChartSeries("Break Even", ChartSeriesType.Line);
        breakEvenSeries.Style.Border.Width = 3;
        breakEvenSeries.Style.Border.Color = Color.DimGray;
        breakEvenSeries.Style.Interior = new BrushInfo(Color.DimGray);
        breakEvenSeries.Style.Symbol.Size = LayoutTokens.GetScaled(LayoutTokens.ChartMarkerSize);
        breakEvenSeries.Style.Border.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
        breakEvenSeries.Style.DisplayShadow = false;
        breakEvenSeries.Points.Add("Prior Actual", (double)priorExpenses);
        breakEvenSeries.Points.Add("Current Estimate", (double)currentExpenses);
        breakEvenSeries.Points.Add("FY Goal", (double)budgetExpenses);
        breakEvenSeries.PointsToolTipFormat = "{1:C0}";
        chart.Series.Add(breakEvenSeries);

        _logger.LogInformation("EnterpriseChart created for {Enterprise}", snapshot.Name);
        return chart;
    }

    /// <summary>
    /// Creates a complete enterprise financial card with a detailed line chart and metric footer.
    /// </summary>
    public Control CreateEnterpriseFinancialCard(EnterpriseSnapshot snapshot)
    {
        _logger.LogDebug("Creating EnterpriseFinancialCard: {Enterprise}", snapshot.Name);

        var titleRowHeight = LayoutTokens.GetScaled(40);
        var gaugeBandHeight = LayoutTokens.GetScaled(124);
        var metricsBandHeight = LayoutTokens.GetScaled(198);
        var summaryRowHeight = LayoutTokens.GetScaled(72);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingCompact),
            Margin = Padding.Empty,
            MinimumSize = LayoutTokens.GetScaled(new Size(900, 620)),
            AccessibleName = $"{snapshot.Name} enterprise financial card"
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, titleRowHeight));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, gaugeBandHeight));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, metricsBandHeight));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, summaryRowHeight));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"{snapshot.Name} enterprise snapshot · {snapshot.DisplayCategory}",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoEllipsis = true,
            Margin = Padding.Empty,
            AccessibleName = $"{snapshot.Name} enterprise title"
        };

        var chartHost = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty,
            Padding = LayoutTokens.GetScaled(LayoutTokens.DialogContentPadding),
            MinimumSize = new Size(0, LayoutTokens.GetScaled(300))
        };

        var chart = CreateEnterpriseChart(snapshot);
        chart.Dock = DockStyle.Fill;
        chartHost.Controls.Add(chart);

        var gaugeTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = LayoutTokens.GetScaled(LayoutTokens.DialogContentPadding),
            AccessibleName = $"{snapshot.Name} enterprise gauges"
        };
        gaugeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333f));
        gaugeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333f));
        gaugeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333f));

        gaugeTable.Controls.Add(
            CreateEnterpriseGaugeTile(
                $"{snapshot.Name} cost recovery gauge",
                "Cost Recovery",
                (double)snapshot.CoveragePercent,
                0,
                150,
                $"{snapshot.CoveragePercent:F0}% vs 100% target"),
            0,
            0);
        gaugeTable.Controls.Add(
            CreateEnterpriseGaugeTile(
                $"{snapshot.Name} reserve gauge",
                "Reserve Months",
                (double)snapshot.ReserveCoverageMonths,
                0,
                12,
                snapshot.ReserveCoverageMonths > 0
                    ? $"{snapshot.ReserveCoverageMonths:F1} months vs {snapshot.TargetReserveCoverageMonths:F0}-month target"
                    : "No reserve coverage model available"),
            1,
            0);
        gaugeTable.Controls.Add(
            CreateEnterpriseGaugeTile(
                $"{snapshot.Name} rate adequacy gauge",
                "Rate Adequacy",
                snapshot.RecommendedRate > 0 ? (double)snapshot.RateAdequacyPercent : 0d,
                0,
                150,
                snapshot.RecommendedRate > 0
                    ? $"{snapshot.CurrentRate:C2} current vs {snapshot.RecommendedRate:C2} modeled"
                    : "No modeled enterprise rate available"),
            2,
            0);

        var metricTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = LayoutTokens.GetScaled(LayoutTokens.DialogContentPadding),
            AccessibleName = $"{snapshot.Name} enterprise metrics"
        };
        metricTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        metricTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        for (int i = 0; i < 3; i++)
        {
            metricTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333f));
        }

        AddEnterpriseMetric(metricTable, 0, 0, "Income", snapshot.Revenue.ToString("C0"));
        AddEnterpriseMetric(metricTable, 1, 0, "Expenses", snapshot.Expenses.ToString("C0"));
        AddEnterpriseMetric(metricTable, 0, 1, "Net Position", snapshot.NetPosition.ToString("C0"));
        AddEnterpriseMetric(metricTable, 1, 1, "Margin", $"{snapshot.OperatingMarginPercent:F1}%");
        AddEnterpriseMetric(metricTable, 0, 2, "Current Rate", snapshot.CurrentRate > 0 ? snapshot.CurrentRate.ToString("C2") : "n/a");
        AddEnterpriseMetric(metricTable, 1, 2, "Recommended", snapshot.RecommendedRate > 0 ? snapshot.RecommendedRate.ToString("C2") : "n/a");

        var noteLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            Padding = LayoutTokens.GetScaled(new Padding(4, 6, 4, 0)),
            TextAlign = ContentAlignment.TopLeft,
            Text = string.IsNullOrWhiteSpace(snapshot.InsightSummary)
                ? (snapshot.IsSelfSustaining ? "Self-supporting at current snapshot." : snapshot.CrossSubsidyNote)
                : snapshot.InsightSummary,
            AccessibleName = $"{snapshot.Name} enterprise summary"
        };

        shell.Controls.Add(titleLabel, 0, 0);
        shell.Controls.Add(chartHost, 0, 1);
        shell.Controls.Add(gaugeTable, 0, 2);
        shell.Controls.Add(metricTable, 0, 3);
        shell.Controls.Add(noteLabel, 0, 4);
        shell.ApplySyncfusionTheme(_currentTheme, _logger);

        return shell;
    }

    private Control CreateEnterpriseGaugeTile(
        string accessibleName,
        string title,
        double rawValue,
        int minimum,
        int maximum,
        string detail)
    {
        var tile = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingTight),
            MinimumSize = new Size(0, LayoutTokens.GetScaled(96)),
            AccessibleName = accessibleName
        };
        tile.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.GetScaled(24)));
        tile.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        tile.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.GetScaled(30)));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = title,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Margin = Padding.Empty
        };

        var gauge = CreateLinearGauge(
            (int)Math.Round(Math.Clamp(rawValue, minimum, maximum)),
            minimum,
            maximum,
            linearGauge =>
            {
                linearGauge.AccessibleName = accessibleName;
            });

        var detailLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = detail,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            Margin = Padding.Empty
        };

        tile.Controls.Add(titleLabel, 0, 0);
        tile.Controls.Add(gauge, 0, 1);
        tile.Controls.Add(detailLabel, 0, 2);
        return tile;
    }

    private static void AddEnterpriseMetric(TableLayoutPanel metricTable, int column, int row, string caption, string value)
    {
        var metricPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingTight),
            MinimumSize = new Size(0, LayoutTokens.GetScaled(56))
        };
        metricPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.GetScaled(22)));
        metricPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var captionLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = caption,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Margin = Padding.Empty
        };

        var valueLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = value,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Margin = Padding.Empty,
            AccessibleName = $"{caption} value"
        };

        metricPanel.Controls.Add(captionLabel, 0, 0);
        metricPanel.Controls.Add(valueLabel, 0, 1);
        metricTable.Controls.Add(metricPanel, column, row);
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

