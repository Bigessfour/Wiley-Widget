using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using System.IO;
using System.Threading.Tasks;
using WileyWidget.WinForms.Extensions;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Utils;
using WileyWidget.WinForms.Services;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;

namespace WileyWidget.WinForms.Controls
{
    internal static class ChartPanelResources
    {
        public const string PanelTitle = "Budget Analytics";
        public const string RefreshText = "Refresh";
        public const string BudgetVarianceChart_Title = "Budget variance chart";
        public const string Axis_Department = "Department";
        public const string Axis_Variance = "Variance (Actual - Budget)";
    }

    /// <summary>
    /// Budget analytics panel with Syncfusion ChartControl.
    /// Inherits from ScopedPanelBase for proper DI lifecycle management.
    /// Designed for embedding in DockingManager.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class ChartPanel : ScopedPanelBase<ChartViewModel>
    {
        private ChartViewModel? _vm;
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;
        private ChartControl? _chartControl;
        private ChartControlRegionEventWiring? _chartRegionEventWiring;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private Syncfusion.WinForms.Controls.SfButton? _btnExportPng;
        private Syncfusion.WinForms.Controls.SfButton? _btnExportPdf;
        private Syncfusion.WinForms.ListView.SfComboBox? _comboDepartmentFilter;
        private Syncfusion.WinForms.Controls.SfButton? _btnRefresh;
        private Panel? _topPanel;
        private Panel? _summaryPanel;
        private Panel? _lblTotalBudget;
        private Panel? _lblTotalActual;
        private Panel? _lblTotalVariance;
        private Panel? _lblVariancePercent;
        private ErrorProvider? _errorProvider;
        private readonly List<ToolTip> _toolTips = new();

        private ContextMenuStrip? _chartContextMenu;
        private bool _preserveUserChartAppearance;
        private bool _isDisposing; // Add flag to track disposal state

        // Stored event handlers for proper cleanup
        private EventHandler? _comboSelectedIndexChangedHandler;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;
        private EventHandler? _btnRefreshThemeChangedHandler;

        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        public new object? DataContext { get; private set; }

        /// <summary>
        /// Constructor for DI with scoped service factory.
        /// </summary>
        public ChartPanel(IServiceScopeFactory serviceScopeFactory, ILogger<ChartPanel> logger, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
            : base(serviceScopeFactory, logger)
        {
            _dispatcherHelper = dispatcherHelper;

            InitializeComponent();

            // Apply theme via SfSkinManager (single source of truth)
            try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, "Office2019Colorful"); } catch { }
        }

        /// <summary>
        /// Called when the ViewModel is resolved from DI. Initialize data bindings here.
        /// </summary>
        protected override void OnViewModelResolved(ChartViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));

            _vm = viewModel;
            DataContext = viewModel;

            // Subscribe to ViewModel property changes
            _viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
            // ChartViewModel always implements INotifyPropertyChanged
            ((INotifyPropertyChanged)_vm).PropertyChanged += _viewModelPropertyChangedHandler;

            // Defer sizing validation until layout is complete
            this.BeginInvoke(new System.Action(() => SafeControlSizeValidator.TryAdjustConstrainedSize(this, out _, out _)));

            Logger?.LogInformation("ChartPanel: ViewModel resolved and bound");
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            Name = "ChartPanel";
            Size = new Size(1000, 700);  // OPTIMIZED: Professional analytics panel size for high UX
            MinimumSize = new Size((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f), (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            AutoScroll = false;  // Disable AutoScroll: SplitContainer + Dock.Fill handles layout
            Padding = new Padding(0);  // Remove padding: Let DockStyle.Fill and internal layouts manage spacing
            Dock = DockStyle.Fill;
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }

            // Shared header + top toolbar (consistent header at 40px, toolbar buttons streamlined)
            _panelHeader = new PanelHeader { Dock = DockStyle.Top };
            try { _panelHeader.Title = ChartPanelResources.PanelTitle; } catch { }
            try
            {
                var dh = this.GetType().GetProperty("DockHandler")?.GetValue(this);
                var txtProp = dh?.GetType().GetProperty("Text");
                if (dh != null && txtProp != null) txtProp.SetValue(dh, ChartPanelResources.PanelTitle);
            }
            catch { }
            _topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };

            _comboDepartmentFilter = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Width = 180,  // OPTIMIZED: Reduced from 220 to save space
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AllowDropDownResize = false,
                MaxDropDownItems = 10,
                AllowNull = true,
                Watermark = "Department",  // OPTIMIZED: Shorter placeholder
                AccessibleName = "Department filter",
                AccessibleDescription = "Filter chart data by department"
            };
            // Add tooltip for better UX
            var comboToolTip = new ToolTip();
            _toolTips.Add(comboToolTip);
            comboToolTip.SetToolTip(_comboDepartmentFilter, "Filter chart by department (or leave blank for all)");
            // Per demos: configure DropDownListView styling
            _comboDepartmentFilter.DropDownListView.Style.ItemStyle.Font = new Font("Segoe UI", 10F);

            _btnRefresh = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = ChartPanelResources.RefreshText,
                Width = 80,  // OPTIMIZED: Reduced from 100
                Height = 26,  // OPTIMIZED: Reduced from 28
                AccessibleName = "Refresh chart data",
                AccessibleDescription = "Reload chart data from the database"
            };
            // Add tooltip for better UX
            var refreshToolTip = new ToolTip();
            _toolTips.Add(refreshToolTip);
            refreshToolTip.SetToolTip(_btnRefresh, "Reload chart data from database (F5)");
            // Add icon via DpiAwareImageService (replacement for deprecated IThemeIconService)
            try
            {
                var dpiService = Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services) : null;
                _btnRefresh.Image = dpiService?.GetImage("refresh");
                _btnRefresh.ImageAlign = ContentAlignment.MiddleLeft;
                _btnRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnRefreshThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        if (_isDisposing || IsDisposed) return; // Check disposal state

                        var svc = Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services) : null;
                        if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                        {
                            _ = _dispatcherHelper.InvokeAsync(() =>
                            {
                                if (!_isDisposing && !IsDisposed && _btnRefresh != null)
                                    _btnRefresh.Image = svc?.GetImage("refresh");
                            });
                        }
                        else if (_btnRefresh != null && _btnRefresh.InvokeRequired)
                        {
                            _btnRefresh.Invoke(() =>
                            {
                                if (!_isDisposing && !IsDisposed && _btnRefresh != null)
                                    _btnRefresh.Image = svc?.GetImage("refresh");
                            });
                        }
                        else if (_btnRefresh != null)
                        {
                            _btnRefresh.Image = svc?.GetImage("refresh");
                        }
                    }
                    catch { }
                };
                // Theme changes are handled by ThemeManager/SfSkinManager cascade; icons are DPI-aware only
            }
            catch { }

            _btnRefresh.Click += BtnRefresh_Click;

            // Add "Go to Dashboard" navigation button per Syncfusion demos pattern
            var btnGoToDashboard = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = "Dashboard",
                Width = 80,  // OPTIMIZED: Consolidated button sizing
                Height = 26,
                AccessibleName = "Go to Dashboard",
                AccessibleDescription = "Navigate to the Dashboard panel"
            };
            var dashToolTip = new ToolTip();
            _toolTips.Add(dashToolTip);
            dashToolTip.SetToolTip(btnGoToDashboard, "Open the Dashboard panel (Ctrl+Shift+D)");
            try
            {
                var dpi = Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services) : null;
                btnGoToDashboard.Image = dpi?.GetImage("home");
                btnGoToDashboard.ImageAlign = ContentAlignment.MiddleLeft;
                btnGoToDashboard.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnGoToDashboard.Click += (s, e) => NavigateToDashboard();

            // Add "Go to Budget" navigation button
            var btnGoToBudget = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = "Budget",
                Width = 80,  // OPTIMIZED: Consolidated button sizing
                Height = 26,
                AccessibleName = "Go to Budget Panel",
                AccessibleDescription = "Navigate to the Budget panel"
            };
            var budgetToolTip = new ToolTip();
            _toolTips.Add(budgetToolTip);
            budgetToolTip.SetToolTip(btnGoToBudget, "Open the Budget panel (Ctrl+B)");
            try
            {
                var dpi = Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services) : null;
                btnGoToBudget.Image = dpi?.GetImage("budget");
                btnGoToBudget.ImageAlign = ContentAlignment.MiddleLeft;
                btnGoToBudget.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnGoToBudget.Click += (s, e) => NavigateToPanel<BudgetPanel>();

            // Add "Go to Accounts" navigation button
            var btnGoToAccounts = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = "Accounts",
                Width = 80,  // OPTIMIZED: Consolidated button sizing
                Height = 26,
                AccessibleName = "Go to Accounts Panel",
                AccessibleDescription = "Navigate to the Accounts panel"
            };
            var accountsToolTip = new ToolTip();
            _toolTips.Add(accountsToolTip);
            accountsToolTip.SetToolTip(btnGoToAccounts, "Open the Accounts panel (Ctrl+A)");
            try
            {
                var dpi = Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services) : null;
                btnGoToAccounts.Image = dpi?.GetImage("account");
                btnGoToAccounts.ImageAlign = ContentAlignment.MiddleLeft;
                btnGoToAccounts.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnGoToAccounts.Click += (s, e) => NavigateToPanel<AccountsPanel>();

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true
            };
            flow.Controls.Add(_comboDepartmentFilter);
            flow.Controls.Add(_btnRefresh);
            // Navigation buttons
            flow.Controls.Add(btnGoToDashboard);
            flow.Controls.Add(btnGoToBudget);
            flow.Controls.Add(btnGoToAccounts);
            // Export buttons
            _btnExportPng = new Syncfusion.WinForms.Controls.SfButton { Text = "PNG", Width = 70, Height = 26, AccessibleName = "Export chart as PNG" };  // OPTIMIZED: Shorter text, smaller size
            var exportPngTip = new ToolTip();
            _toolTips.Add(exportPngTip);
            exportPngTip.SetToolTip(_btnExportPng, "Export the current chart view to a PNG image");
            try
            {
                var dpi = Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services) : null;
                _btnExportPng.Image = dpi?.GetImage("export");
                _btnExportPng.ImageAlign = ContentAlignment.MiddleLeft;
                _btnExportPng.TextImageRelation = TextImageRelation.ImageBeforeText;
                // Export icons are DPI-aware only; theme changes don't affect icon bitmaps in current implementation
            }
            catch { }
            _btnExportPng.Click += ExportPng_Click;
            flow.Controls.Add(_btnExportPng);

            _btnExportPdf = new Syncfusion.WinForms.Controls.SfButton { Text = "PDF", Width = 70, Height = 26, AccessibleName = "Export chart as PDF" };  // OPTIMIZED: Shorter text, smaller size
            var exportPdfTip = new ToolTip();
            _toolTips.Add(exportPdfTip);
            exportPdfTip.SetToolTip(_btnExportPdf, "Export the current chart view embedded in a PDF");
            try
            {
                var dpi = Program.Services != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<DpiAwareImageService>(Program.Services) : null;
                _btnExportPdf.Image = dpi?.GetImage("pdf");
                _btnExportPdf.ImageAlign = ContentAlignment.MiddleLeft;
                _btnExportPdf.TextImageRelation = TextImageRelation.ImageBeforeText;
                // Export icons are DPI-aware only; theme changes don't affect icon bitmaps in current implementation
            }
            catch { }
            _btnExportPdf.Click += ExportPdf_Click;
            flow.Controls.Add(_btnExportPdf);
            flow.Controls.Add(btnGoToDashboard);
            _topPanel.Controls.Add(flow);
            // Add shared header above the toolbar for consistent UX
            Controls.Add(_panelHeader);
            Controls.Add(_topPanel);

            // Chart control - configured per Syncfusion demo best practices (ChartAppearance.cs pattern)
            // Theme applied automatically by SfSkinManager cascade from parent form
            _chartControl = new ChartControl { Name = "Chart_Cartesian", Dock = DockStyle.Fill, AccessibleName = "Budget Trend" };

            // Centralized defaults (appearance + zoom/scrollbar) with theme-friendly chart area
            ChartControlDefaults.Apply(_chartControl, new ChartControlDefaults.Options { TransparentChartArea = true });
            _chartControl.ChartArea.PrimaryXAxis.HidePartialLabels = true;

            // Configure axes per demo patterns with optimized label rotation for large displays
            _chartControl.PrimaryXAxis.ValueType = ChartValueType.Category;
            _chartControl.PrimaryXAxis.Title = ChartPanelResources.Axis_Department;
            _chartControl.PrimaryXAxis.TitleFont = new Font("Segoe UI", 11F);  // OPTIMIZED: Increased from 10
            _chartControl.PrimaryXAxis.Font = new Font("Segoe UI", 10F);
            _chartControl.PrimaryXAxis.LabelRotate = true;
            _chartControl.PrimaryXAxis.LabelRotateAngle = 45;
            _chartControl.PrimaryXAxis.DrawGrid = false;  // Cleaner appearance

            _chartControl.PrimaryYAxis.Title = ChartPanelResources.Axis_Variance;
            _chartControl.PrimaryYAxis.TitleFont = new Font("Segoe UI", 11F);  // OPTIMIZED: Increased from 10
            _chartControl.PrimaryYAxis.Font = new Font("Segoe UI", 10F);
            try
            {
                var py = _chartControl.PrimaryYAxis;
                var propNum = py?.GetType().GetProperty("NumberFormat");
                if (propNum != null && propNum.CanWrite) propNum.SetValue(py, "C0");
            }
            catch { }

            // Add a secondary Y axis for percentage variance if the API is available
            try
            {
                var secAxis = new ChartAxis();
                secAxis.Title = "% Variance";
                try
                {
                    var propNum = secAxis.GetType().GetProperty("NumberFormat");
                    if (propNum != null && propNum.CanWrite) propNum.SetValue(secAxis, "P0");
                }
                catch { }
                // Use reflection to assign the axis to avoid compile-time coupling if property differs
                var chartArea = _chartControl.ChartArea;
                var prop = chartArea.GetType().GetProperty("SecondaryYAxis");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(chartArea, secAxis);
                }
            }
            catch { }

            InitializeChartUserCustomization();

            // Configure legend per demo patterns with optimized sizing for larger panels
            _chartControl.ShowLegend = true;
            _chartControl.LegendsPlacement = Syncfusion.Windows.Forms.Chart.ChartPlacement.Outside;
            _chartControl.LegendPosition = ChartDock.Bottom;
            _chartControl.LegendAlignment = ChartAlignment.Center;
            _chartControl.Legend.Font = new Font("Segoe UI", 10F);
            _chartControl.ElementsSpacing = 15;  // OPTIMIZED: Slightly reduced from default 20 for compact layout

            // Add a right-hand panel to host a small pie chart or placeholder for accessibility detection
            // Add a right-hand panel to host a small pie chart or placeholder for accessibility detection
            var piePanel = new Syncfusion.Windows.Forms.Tools.GradientPanelExt
            {
                Name = "Chart_Pie",
                Width = 320,
                Dock = DockStyle.Right,
                AccessibleName = "Chart Pie",
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(piePanel, "Office2019Colorful");

            Controls.Add(_chartControl);
            Controls.Add(piePanel);

            // Add bottom summary panel
            _summaryPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(8)
            };

            var summaryFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };

            // Create summary metric cards
            _lblTotalBudget = CreateSummaryLabel("Total Budget:", "$0");
            _lblTotalActual = CreateSummaryLabel("Total Actual:", "$0");
            _lblTotalVariance = CreateSummaryLabel("Variance:", "$0");
            _lblVariancePercent = CreateSummaryLabel("Variance %:", "0.0%");

            summaryFlow.Controls.Add(_lblTotalBudget);
            summaryFlow.Controls.Add(_lblTotalActual);
            summaryFlow.Controls.Add(_lblTotalVariance);
            summaryFlow.Controls.Add(_lblVariancePercent);

            _summaryPanel.Controls.Add(summaryFlow);
            Controls.Add(_summaryPanel);

            // Add overlays (loading spinner and no-data friendly message)
            _loadingOverlay = new LoadingOverlay { Message = "Loading chart data..." };
            Controls.Add(_loadingOverlay);

            _noDataOverlay = new NoDataOverlay { Message = "No data to visualize\r\nCreate accounts and budget entries to generate charts" };
            Controls.Add(_noDataOverlay);

            _errorProvider = new ErrorProvider() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

            // Wiring: load data and set up simple bindings
            Load += ChartPanel_Load;

            try
            {
                if (_comboDepartmentFilter != null)
                {
                    _comboDepartmentFilter.DataSource = _vm?.ChartData?.Select(k => k.Key).ToList();
                    _comboSelectedIndexChangedHandler = ComboFilter_SelectedIndexChanged;
                    _comboDepartmentFilter.SelectedIndexChanged += _comboSelectedIndexChangedHandler;
                }
            }
            catch { }

            // Wire panel header actions
            try
            {
                if (_panelHeader != null)
                {
                    _panelHeader.Title = ChartPanelResources.PanelTitle;
                    _panelHeaderRefreshHandler = OnPanelHeaderRefreshClicked;
                    _panelHeader.RefreshClicked += _panelHeaderRefreshHandler;
                    _panelHeaderCloseHandler = OnPanelHeaderCloseClicked;
                    _panelHeader.CloseClicked += _panelHeaderCloseHandler;
                }
            }
            catch { }
            this.ResumeLayout(false);

        }

        /// <summary>
        /// Creates a formatted summary label for metrics display with optimized sizing.
        /// </summary>
        private Syncfusion.Windows.Forms.Tools.GradientPanelExt CreateSummaryLabel(string caption, string value)
        {
            var panel = new Syncfusion.Windows.Forms.Tools.GradientPanelExt
            {
                Width = 160,  // OPTIMIZED: Reduced from 200 for 4-metric fit in 56px height
                Height = 48,  // OPTIMIZED: Reduced from 44 to fit better
                Margin = new Padding(2),  // OPTIMIZED: Reduced from 4
                BorderStyle = BorderStyle.None,
                BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
            };
            Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(panel, "Office2019Colorful");

            var lblCaption = new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = 16,  // OPTIMIZED: Reduced from 18
                Font = new Font("Segoe UI", 8F, FontStyle.Regular)  // OPTIMIZED: Reduced from 9
            };

            var lblValue = new Label
            {
                Text = value,
                Dock = DockStyle.Bottom,
                Height = 20,  // OPTIMIZED: Reduced from 24
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),  // OPTIMIZED: Reduced from 11
                Tag = caption // Store caption for later updates
            };

            panel.Controls.Add(lblValue);
            panel.Controls.Add(lblCaption);

            return panel;
        }


        /// <summary>
        /// Navigates to the Dashboard panel via parent form's DockingManager.
        /// Per Syncfusion demos navigation pattern.
        /// </summary>
        private void NavigateToDashboard()
        {
            try
            {
                var parentForm = this.FindForm();
                if (parentForm == null) return;

                // Prefer direct API on MainForm where available
                if (parentForm is Forms.MainForm mf)
                {
                    try { mf.ShowPanel<DashboardPanel>("Dashboard"); return; } catch { }
                }

                // Fallback to reflection
                var method = parentForm.GetType().GetMethod("DockUserControlPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(typeof(DashboardPanel));
                    genericMethod.Invoke(parentForm, new object[] { "Dashboard" });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: NavigateToDashboard failed");
            }
        }

        /// <summary>
        /// Generic navigation helper for panel navigation via DockingManager.
        /// </summary>
        private void NavigateToPanel<TPanel>() where TPanel : UserControl
        {
            try
            {
                var parentForm = this.FindForm();
                if (parentForm == null) return;

                // Extract panel name from type (e.g., "BudgetPanel" -> "Budget")
                var panelName = typeof(TPanel).Name.Replace("Panel", "", StringComparison.Ordinal);

                // Prefer direct API on MainForm where available
                if (parentForm is Forms.MainForm mf)
                {
                    try { mf.ShowPanel<TPanel>(panelName); return; } catch { }
                }

                // Fallback to reflection
                var method = parentForm.GetType().GetMethod("DockUserControlPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(typeof(TPanel));
                    genericMethod.Invoke(parentForm, new object[] { panelName });
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "ChartPanel: NavigateToPanel<{PanelType}> failed", typeof(TPanel).Name);
            }
        }

        /// <summary>
        /// Handles keyboard shortcuts for chart operations and navigation.
        /// F5: Refresh chart data
        /// Ctrl+E: Export to PNG
        /// Ctrl+Shift+E: Export to PDF
        /// Ctrl+B: Navigate to Budget panel
        /// Ctrl+A: Navigate to Accounts panel
        /// Ctrl+Shift+D: Navigate to Dashboard
        /// Esc: Close panel
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            try
            {
                switch (keyData)
                {
                    case Keys.F5:
                        BtnRefresh_Click(this, EventArgs.Empty);
                        return true;

                    case Keys.Control | Keys.E:
                        ExportPng_Click(this, EventArgs.Empty);
                        return true;

                    case Keys.Control | Keys.Shift | Keys.E:
                        ExportPdf_Click(this, EventArgs.Empty);
                        return true;

                    case Keys.Control | Keys.B:
                        NavigateToPanel<BudgetPanel>();
                        return true;

                    case Keys.Control | Keys.A:
                        NavigateToPanel<AccountsPanel>();
                        return true;

                    case Keys.Control | Keys.Shift | Keys.D:
                        NavigateToDashboard();
                        return true;

                    case Keys.Escape:
                        OnPanelHeaderCloseClicked(this, EventArgs.Empty);
                        return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "ChartPanel: ProcessCmdKey failed for key {KeyData}", keyData);
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// Named handler for PanelHeader.RefreshClicked event (enables proper unsubscription).
        /// </summary>
        private async void OnPanelHeaderRefreshClicked(object? sender, EventArgs e)
        {
            try
            {
                await (_vm.RefreshCommand?.ExecuteAsync(null) ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: Refresh failed");
            }
        }

        /// <summary>
        /// Named handler for PanelHeader.CloseClicked event (enables proper unsubscription).
        /// </summary>
        private void OnPanelHeaderCloseClicked(object? sender, EventArgs e)
        {
            try
            {
                var parent = this.FindForm();
                var method = parent?.GetType().GetMethod("ClosePanel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                method?.Invoke(parent, new object[] { this.Name });
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: Close panel failed");
            }
        }

        /// <summary>
        /// Handles refresh button click asynchronously.
        /// </summary>
        private async void BtnRefresh_Click(object? sender, EventArgs e)
        {
            try
            {
                if (IsDisposed || _vm == null) return;

                await (_vm.RefreshCommand?.ExecuteAsync(null) ?? Task.CompletedTask).ConfigureAwait(false);

                if (!IsDisposed)
                {
                    if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                    {
                        try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                    }
                    else if (InvokeRequired)
                    {
                        try { BeginInvoke(new System.Action(UpdateChartFromData)); } catch { }
                    }
                    else
                    {
                        if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                        {
                            try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                        }
                        else
                        {
                            if (InvokeRequired) BeginInvoke(new System.Action(UpdateChartFromData)); else UpdateChartFromData();
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("ChartPanel: BtnRefresh_Click - panel was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: Refresh failed");
            }
        }

        /// <summary>
        /// Handles panel load event asynchronously.
        /// </summary>
        private async void ChartPanel_Load(object? sender, EventArgs e)
        {
            try
            {
                if (IsDisposed || _vm == null) return;

                await _vm.LoadChartDataAsync().ConfigureAwait(false);

                if (!IsDisposed)
                {
                    if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                    {
                        try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                    }
                    else if (InvokeRequired)
                    {
                        if (IsHandleCreated)
                        {
                            try { BeginInvoke(new System.Action(UpdateChartFromData)); } catch { }
                        }
                    }
                    else
                    {
                        if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                        {
                            try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                        }
                        else
                        {
                            if (InvokeRequired) BeginInvoke(new System.Action(UpdateChartFromData)); else UpdateChartFromData();
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("ChartPanel: ChartPanel_Load - panel was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "ChartPanel: failed to load chart data");
            }
        }

        /// <summary>
        /// Handles combo box selection change.
        /// </summary>
        private void ComboFilter_SelectedIndexChanged(object? sender, EventArgs e)
        {
            try
            {
                if (IsDisposed) return;
                UpdateChartFromData();
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("ChartPanel: ComboFilter_SelectedIndexChanged - panel was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: ComboFilter_SelectedIndexChanged failed");
            }
        }

        /// <summary>
        /// Handles ViewModel property changes with thread safety.
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (IsDisposed || _vm == null) return;

                if (e.PropertyName == nameof(_vm.ChartData) || e.PropertyName == nameof(_vm.ErrorMessage))
                {
                    if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                    {
                        try { _ = _dispatcherHelper.InvokeAsync(() => OnViewModelPropertyChanged(sender, e)); } catch { }
                        return;
                    }
                    if (InvokeRequired)
                    {
                        try { BeginInvoke(new System.Action(() => OnViewModelPropertyChanged(sender, e))); } catch { }
                        return;
                    }

                    try { UpdateChartFromData(); } catch { }
                    try
                    {
                        if (_comboDepartmentFilter != null && _vm.ChartData != null)
                            _comboDepartmentFilter.DataSource = _vm.ChartData.Select(k => k.Key).ToList();
                    }
                    catch { }
                }
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("ChartPanel: ViewModel_PropertyChanged - panel was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: ViewModel_PropertyChanged failed");
            }
        }

        /// <summary>
        /// Handles theme changes with thread safety.
        /// </summary>
        private void OnThemeChanged(object? sender, string theme)
        {
            try
            {
                if (IsDisposed) return;

                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(() => OnThemeChanged(sender, theme)); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    try { BeginInvoke(new System.Action(() => OnThemeChanged(sender, theme))); } catch { }
                    return;
                }

                ApplyCurrentTheme();
            }
            catch (ObjectDisposedException)
            {
                Serilog.Log.Debug("ChartPanel: OnThemeChanged - panel was disposed");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: OnThemeChanged failed");
            }
        }

        private void ApplyCurrentTheme()
        {
            try
            {
                ApplySyncfusionTheme();
            }
            catch { }
        }

        /// <summary>
        /// Applies Syncfusion-specific theme styling to ChartControl.
        /// Uses Syncfusion skins for modern appearance.
        /// </summary>
        private void ApplySyncfusionTheme()
        {
            try
            {
                if (_chartControl == null) return;
                if (_preserveUserChartAppearance) return;

                // Apply Syncfusion skin per demos - ChartControl uses Office2016 skin names
                _chartControl.ChartArea.BackInterior = new Syncfusion.Drawing.BrushInfo(Color.Transparent);

                // Legend styling
                _chartControl.Legend.Font = new Font("Segoe UI", 9F);

                Logger?.LogDebug("ChartPanel: Syncfusion theme styling applied");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "ChartPanel: Failed to apply Syncfusion theme styling");
            }
        }

        private void InitializeChartUserCustomization()
        {
            try
            {
                if (_chartControl == null) return;

                // Enable Syncfusion-provided runtime UX where available (avoid compile-time coupling across versions)
                try
                {
                    var propShowToolBar = _chartControl.GetType().GetProperty("ShowToolBar");
                    if (propShowToolBar != null && propShowToolBar.CanWrite) propShowToolBar.SetValue(_chartControl, true);
                }
                catch { }
                try
                {
                    var propShowContextMenu = _chartControl.GetType().GetProperty("ShowContextMenu");
                    if (propShowContextMenu != null && propShowContextMenu.CanWrite) propShowContextMenu.SetValue(_chartControl, true);
                }
                catch { }

                _chartContextMenu = new ContextMenuStrip();

                var miFaq = new ToolStripMenuItem("Chart Wizard FAQ...");
                miFaq.Click += (s, e) => WileyWidget.WinForms.Dialogs.ChartWizardFaqDialog.ShowModal(this);

                var miWizard = new ToolStripMenuItem("Chart Wizard...");
                miWizard.Click += (s, e) => ShowChartWizard();

                var miSaveTemplate = new ToolStripMenuItem("Save Template...");
                miSaveTemplate.Click += (s, e) => SaveChartTemplate();

                var miLoadTemplate = new ToolStripMenuItem("Load Template...");
                miLoadTemplate.Click += (s, e) => LoadChartTemplate();

                var miResetTemplate = new ToolStripMenuItem("Reset Template");
                miResetTemplate.Click += (s, e) => ResetChartTemplate();

                _chartContextMenu.Items.Add(miFaq);
                _chartContextMenu.Items.Add(new ToolStripSeparator());
                _chartContextMenu.Items.Add(miWizard);
                _chartContextMenu.Items.Add(new ToolStripSeparator());
                _chartContextMenu.Items.Add(miSaveTemplate);
                _chartContextMenu.Items.Add(miLoadTemplate);
                _chartContextMenu.Items.Add(miResetTemplate);

                _chartControl.ContextMenuStrip = _chartContextMenu;
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "ChartPanel: failed to initialize chart user customization menu");
            }
        }

        private void ShowChartWizard()
        {
            try
            {
                if (_chartControl == null) return;

                _preserveUserChartAppearance = true;

                if (TryInvokeChartWizard(_chartControl))
                {
                    return;
                }

                MessageBox.Show(
                    "Chart Wizard is not available in this Syncfusion Chart build.",
                    "Chart Wizard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "ChartPanel: failed to show chart wizard");
                MessageBox.Show($"Unable to open Chart Wizard: {ex.Message}", "Chart Wizard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool TryInvokeChartWizard(ChartControl chart)
        {
            // Prefer ChartControl.DisplayWizard() when present (some Syncfusion builds expose it directly)
            try
            {
                var displayWizard = chart.GetType().GetMethod("DisplayWizard", Type.EmptyTypes);
                if (displayWizard != null)
                {
                    displayWizard.Invoke(chart, Array.Empty<object>());
                    return true;
                }
            }
            catch { }

            // Fallback to ChartWizardForm.ShowWizard(chart) when available (varies by Syncfusion assembly/version)
            try
            {
                var wizardType =
                    Type.GetType("Syncfusion.Windows.Forms.Chart.ChartWizardForm, Syncfusion.Chart.Windows", throwOnError: false)
                    ?? Type.GetType("Syncfusion.Windows.Forms.Chart.ChartWizardForm, Syncfusion.Windows.Forms.Chart", throwOnError: false)
                    ?? FindLoadedType("Syncfusion.Windows.Forms.Chart.ChartWizardForm");

                if (wizardType == null) return false;

                object? wizardInstance = null;
                foreach (var ctor in wizardType.GetConstructors())
                {
                    try
                    {
                        var parameters = ctor.GetParameters();
                        if (parameters.Length == 0)
                        {
                            wizardInstance = ctor.Invoke(Array.Empty<object>());
                            break;
                        }

                        if (parameters.Length == 1 && typeof(ISite).IsAssignableFrom(parameters[0].ParameterType))
                        {
                            wizardInstance = ctor.Invoke(new object?[] { chart.Site ?? this.Site });
                            break;
                        }
                    }
                    catch { }
                }

                if (wizardInstance == null) return false;

                var showWizard = wizardType
                    .GetMethods()
                    .FirstOrDefault(m =>
                    {
                        if (!string.Equals(m.Name, "ShowWizard", StringComparison.Ordinal)) return false;
                        var p = m.GetParameters();
                        return p.Length == 1 && p[0].ParameterType.IsAssignableFrom(chart.GetType());
                    })
                    ?? wizardType
                        .GetMethods()
                        .FirstOrDefault(m =>
                        {
                            if (!string.Equals(m.Name, "ShowWizard", StringComparison.Ordinal)) return false;
                            var p = m.GetParameters();
                            return p.Length == 1 && p[0].ParameterType.IsAssignableFrom(typeof(ChartControl));
                        });

                if (showWizard == null) return false;

                showWizard.Invoke(wizardInstance, new object[] { chart });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Type? FindLoadedType(string fullName)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
                        if (t != null) return t;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        private void SaveChartTemplate()
        {
            try
            {
                if (_chartControl == null) return;

                using var sfd = new SaveFileDialog
                {
                    Filter = "Chart Template (*.xml)|*.xml|All files (*.*)|*.*",
                    DefaultExt = "xml",
                    FileName = "ChartTemplate.xml"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                var oldStoreAll = TryGetChartTemplateFlag("StoreAllProperties");
                var oldStoreSeriesData = TryGetChartTemplateFlag("StoreSeriesData");
                var oldStoreSeriesStyle = TryGetChartTemplateFlag("StoreSeriesStyle");

                try
                {
                    TrySetChartTemplateFlag("StoreAllProperties", true);
                    // UX templates should capture styling; avoid persisting data by default.
                    TrySetChartTemplateFlag("StoreSeriesData", false);
                    TrySetChartTemplateFlag("StoreSeriesStyle", true);

                    InvokeChartTemplateMethod("Save", _chartControl, sfd.FileName);

                    MessageBox.Show($"Template saved to {sfd.FileName}", "Save Template", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    RestoreChartTemplateFlag("StoreAllProperties", oldStoreAll);
                    RestoreChartTemplateFlag("StoreSeriesData", oldStoreSeriesData);
                    RestoreChartTemplateFlag("StoreSeriesStyle", oldStoreSeriesStyle);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "ChartPanel: Save template failed");
                MessageBox.Show($"Save template failed: {ex.Message}", "Save Template", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadChartTemplate()
        {
            try
            {
                if (_chartControl == null) return;

                using var ofd = new OpenFileDialog
                {
                    Filter = "Chart Template (*.xml)|*.xml|All files (*.*)|*.*",
                    DefaultExt = "xml",
                    Multiselect = false
                };

                if (ofd.ShowDialog() != DialogResult.OK) return;

                _preserveUserChartAppearance = true;

                InvokeChartTemplateMethod("Load", _chartControl, ofd.FileName);

                try { UpdateChartFromData(); } catch { }

                MessageBox.Show($"Template loaded from {ofd.FileName}", "Load Template", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (MissingMethodException ex)
            {
                Logger?.LogWarning(ex, "ChartPanel: ChartTemplate.Load not available in this Syncfusion version");
                MessageBox.Show("This Syncfusion Chart build does not expose a ChartTemplate.Load API.", "Load Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "ChartPanel: Load template failed");
                MessageBox.Show($"Load template failed: {ex.Message}", "Load Template", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetChartTemplate()
        {
            try
            {
                if (_chartControl == null) return;

                try
                {
                    TryResetChartTemplate(_chartControl);
                }
                catch
                {
                    // If Reset isn't available, fall back to clearing user customization mode
                }

                _preserveUserChartAppearance = false;

                try { ApplySyncfusionTheme(); } catch { }
                try { UpdateChartFromData(); } catch { }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "ChartPanel: Reset template failed");
                MessageBox.Show($"Reset template failed: {ex.Message}", "Reset Template", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void InvokeChartTemplateMethod(string methodName, ChartControl chart, string path)
        {
            var ctType = typeof(ChartTemplate);

            var staticMethod = ctType.GetMethod(
                methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: new[] { typeof(ChartControl), typeof(string) },
                modifiers: null);

            if (staticMethod != null)
            {
                staticMethod.Invoke(null, new object[] { chart, path });
                return;
            }

            var templateInstance = CreateChartTemplateInstance(chart);
            if (templateInstance != null)
            {
                var instanceMethod = ctType.GetMethod(
                    methodName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(ChartControl), typeof(string) },
                    modifiers: null);

                if (instanceMethod != null)
                {
                    instanceMethod.Invoke(templateInstance, new object[] { chart, path });
                    return;
                }
            }

            throw new MissingMethodException(ctType.FullName, methodName);
        }

        private static void TryResetChartTemplate(ChartControl chart)
        {
            var ctType = typeof(ChartTemplate);

            var staticReset = ctType.GetMethod(
                "Reset",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: new[] { typeof(ChartControl) },
                modifiers: null);

            if (staticReset != null)
            {
                staticReset.Invoke(null, new object[] { chart });
                return;
            }

            var templateInstance = CreateChartTemplateInstance(chart);
            if (templateInstance == null) return;

            var instanceReset = ctType.GetMethod(
                "Reset",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(ChartControl) },
                modifiers: null);

            instanceReset?.Invoke(templateInstance, new object[] { chart });
        }

        private static object? CreateChartTemplateInstance(ChartControl chart)
        {
            try
            {
                var ctType = typeof(ChartTemplate);

                var parameterless = ctType.GetConstructor(Type.EmptyTypes);
                if (parameterless != null)
                {
                    return parameterless.Invoke(Array.Empty<object>());
                }

                var typeCtor = ctType.GetConstructor(new[] { typeof(Type) });
                if (typeCtor != null)
                {
                    return typeCtor.Invoke(new object[] { chart.GetType() });
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static bool? TryGetChartTemplateFlag(string propertyName)
        {
            try
            {
                var prop = typeof(ChartTemplate).GetProperty(propertyName);
                if (prop == null || !prop.CanRead) return null;

                var value = prop.GetValue(null);
                return value is bool b ? b : (bool?)null;
            }
            catch
            {
                return null;
            }
        }

        private static void TrySetChartTemplateFlag(string propertyName, bool value)
        {
            try
            {
                var prop = typeof(ChartTemplate).GetProperty(propertyName);
                if (prop == null || !prop.CanWrite) return;

                prop.SetValue(null, value);
            }
            catch { }
        }

        private static void RestoreChartTemplateFlag(string propertyName, bool? oldValue)
        {
            if (oldValue == null) return;

            TrySetChartTemplateFlag(propertyName, oldValue.Value);
        }

        private void UpdateChartFromData()
        {
            try
            {
                if (_vm == null) return;

                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    Invoke(new System.Action(UpdateChartFromData));
                    return;
                }

                if (_chartControl != null && !_preserveUserChartAppearance)
                {
                    _chartControl.Series.Clear();
                }

                // Show/hide overlays based on loading state and data availability
                if (_loadingOverlay != null)
                {
                    _loadingOverlay.Visible = _vm.IsLoading;
                }

                if (_vm.IsLoading)
                {
                    if (_chartControl != null) _chartControl.Visible = false;
                    if (_noDataOverlay != null) _noDataOverlay.Visible = false;
                    return;
                }

                // Show/hide overlays based on loading state and data availability
                if (_loadingOverlay != null)
                {
                    _loadingOverlay.Visible = _vm.IsLoading;
                }

                if (_vm.IsLoading)
                {
                    if (_chartControl != null) _chartControl.Visible = false;
                    if (_noDataOverlay != null) _noDataOverlay.Visible = false;
                    return;
                }

                var data = _vm.ChartData;
                if (data == null || !data.Any())
                {
                    // Show no data overlay
                    if (_chartControl != null) _chartControl.Visible = false;
                    if (_noDataOverlay != null)
                    {
                        _noDataOverlay.Visible = true;
                        _noDataOverlay.BringToFront();
                    }
                    return;
                }

                // Hide overlays and show chart
                if (_noDataOverlay != null) _noDataOverlay.Visible = false;
                if (_loadingOverlay != null) _loadingOverlay.Visible = false;
                if (_chartControl != null) _chartControl.Visible = true;

                var list = data.OrderByDescending(k => k.Value).ToList();

                // Configure series per Syncfusion demo best practices (Column Charts demo)
                var series = new ChartSeries("Budget Variance", ChartSeriesType.Column);
                try
                {
                    series = _chartControl.Series
                        .OfType<ChartSeries>()
                        .FirstOrDefault(s => string.Equals(s.Name, "Budget Variance", StringComparison.Ordinal));
                }
                catch
                {
                    series = null;
                }

                if (series == null)
                {
                    // Configure series per Syncfusion demo best practices (Column Charts demo)
                    series = new ChartSeries("Budget Variance", ChartSeriesType.Column);
                    try
                    {
                        // set series axis value types if supported across Syncfusion versions
                        var sType = series.GetType();
                        var pX = sType.GetProperty("XAxisValueType") ?? sType.GetProperty("XValueType");
                        var pY = sType.GetProperty("YAxisValueType") ?? sType.GetProperty("YValueType");
                        if (pX != null && pX.CanWrite) pX.SetValue(series, ChartValueType.Category);
                        if (pY != null && pY.CanWrite) pY.SetValue(series, ChartValueType.Double);
                    }
                    catch { }

                    if (_chartControl != null)
                    {
                        _chartControl.Series.Add(series);
                    }
                }
                else
                {
                    try { series.Points.Clear(); } catch { }
                }

                series.Text = series.Name;

                var index = 0;
                foreach (var d in list)
                {
                    series.Points.Add(index++, (double)d.Value);
                }

                // Apply series style per demo patterns (ChartStyles demo)
                series.Style.DisplayText = true;
                series.Style.TextFormat = "{0:C0}";
                series.Style.TextOrientation = Syncfusion.Windows.Forms.Chart.ChartTextOrientation.Up;
                series.Style.Font.Facename = "Segoe UI";
                series.Style.Font.Size = 8;
                series.Style.Font.Bold = true;

                // Use Syncfusion theme-aware color (Office2019Colorful accent)
                series.Style.Interior = new Syncfusion.Drawing.BrushInfo(Color.FromArgb(0, 120, 215));

                // Color individual points based on variance (reflection for compatibility)
                try
                {
                    for (int i = 0; i < list.Count && i < series.Points.Count; i++)
                    {
                        var point = series.Points[i];
                        var variance = list[i].Value;

                        // Try to set point color via reflection for compatibility across Syncfusion versions
                        var interiorProp = point.GetType().GetProperty("Interior");
                        if (interiorProp != null && interiorProp.CanWrite)
                        {
                            var color = variance >= 0
                                ? Color.FromArgb(76, 175, 80)  // Green for under budget
                                : Color.FromArgb(244, 67, 54); // Red for over budget
                            interiorProp.SetValue(point, new Syncfusion.Drawing.BrushInfo(color));
                        }
                    }
                }
                catch
                {
                    // Per-point coloring not supported - use series color
                }

                // Make border transparent per demos
                series.Style.Border.Color = Color.Transparent;

                    // Data visualization color - let theme handle via chart defaults
                    // series.Style.Interior inherited from theme

                _chartControl?.Series.Add(series);

                // Update X-axis labels with department names - simplified for compatibility
                // Note: Syncfusion ChartControl automatically uses category labels from data points

                // Update summary labels in status bar
                UpdateSummaryDisplay();

                Serilog.Log.Information("Chart updated with {SeriesCount} series and {PointCount} data points",
                    _chartControl?.Series.Count ?? 0, list.Count);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: failed updating chart from data");
            }
        }

        /// <summary>
        /// Updates summary display labels with current ViewModel data.
        /// </summary>
        private void UpdateSummaryDisplay()
        {
            try
            {
                // Update summary metric labels
                UpdateSummaryLabelValue(_lblTotalBudget, _vm.TotalBudgeted.ToString("C0", System.Globalization.CultureInfo.CurrentCulture));
                UpdateSummaryLabelValue(_lblTotalActual, _vm.TotalActual.ToString("C0", System.Globalization.CultureInfo.CurrentCulture));
                UpdateSummaryLabelValue(_lblTotalVariance, _vm.TotalVariance.ToString("C0", System.Globalization.CultureInfo.CurrentCulture));
                UpdateSummaryLabelValue(_lblVariancePercent, $"{_vm.VariancePercentage:F1}%");

                // Color variance labels based on positive/negative
                if (_lblTotalVariance != null)
                {
                    var varianceValueLabel = _lblTotalVariance.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Bottom);
                    if (varianceValueLabel != null)
                    {
                        varianceValueLabel.ForeColor = _vm.TotalVariance >= 0 ? Color.FromArgb(76, 175, 80) : Color.FromArgb(244, 67, 54);
                    }
                }

                if (_lblVariancePercent != null)
                {
                    var percentValueLabel = _lblVariancePercent.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Bottom);
                    if (percentValueLabel != null)
                    {
                        percentValueLabel.ForeColor = _vm.VariancePercentage >= 0 ? Color.FromArgb(76, 175, 80) : Color.FromArgb(244, 67, 54);
                    }
                }

                // Update panel title with summary
                if (_panelHeader != null)
                {
                    try
                    {
                        // Update title instead of subtitle (subtitle may not be available in all panel header versions)
                        var titleText = $"{ChartPanelResources.PanelTitle} - {_vm.DepartmentCount} Depts • FY {_vm.SelectedYear}";
                        _panelHeader.Title = titleText;
                    }
                    catch { }
                }

                Serilog.Log.Debug("ChartPanel: Summary labels updated");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: Failed to update summary display");
            }
        }

        /// <summary>
        /// Updates a summary label's value text.
        /// </summary>
        private void UpdateSummaryLabelValue(Panel? containerPanel, string value)
        {
            if (containerPanel == null) return;

            try
            {
                var valueLabel = containerPanel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Bottom);
                if (valueLabel != null)
                {
                    valueLabel.Text = value;
                }
            }
            catch { }
        }

        // Capture the current chart as a Bitmap on the UI thread
        private Bitmap? CaptureChartBitmap()
        {
            if (_chartControl == null) return null;

            Bitmap? bmp = null;
            if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
            {
                try
                {
                    return _dispatcherHelper.Invoke(() =>
                    {
                        try
                        {
                            if (_chartControl == null) return null;
                            var b = new Bitmap(_chartControl.Width, _chartControl.Height);
                            _chartControl.DrawToBitmap(b, new Rectangle(0, 0, _chartControl.Width, _chartControl.Height));
                            return b;
                        }
                        catch
                        {
                            return (Bitmap?)null;
                        }
                    });
                }
                catch
                {
                    bmp = null;
                }
            }
            else if (InvokeRequired)
            {
                try { Invoke(new System.Action(() => { bmp = new Bitmap(_chartControl.Width, _chartControl.Height); _chartControl.DrawToBitmap(bmp, new Rectangle(0, 0, _chartControl.Width, _chartControl.Height)); })); }
                catch { bmp = null; }
            }
            else
            {
                try
                {
                    bmp = new Bitmap(_chartControl.Width, _chartControl.Height);
                    _chartControl.DrawToBitmap(bmp, new Rectangle(0, 0, _chartControl.Width, _chartControl.Height));
                }
                catch { bmp = null; }
            }

            return bmp;
        }

        private async void ExportPng_Click(object? sender, EventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                var bmp = CaptureChartBitmap();
                if (bmp == null)
                {
                    MessageBox.Show("Unable to capture chart image.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var sfd = new SaveFileDialog { Filter = "PNG Image|*.png", DefaultExt = "png", FileName = "chart.png" };
                if (sfd.ShowDialog() != DialogResult.OK) { bmp.Dispose(); return; }
                var path = sfd.FileName;

                // Save off the UI thread
                await Task.Run(() =>
                {
                    try
                    {
                        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    finally
                    {
                        bmp.Dispose();
                    }
                }).ConfigureAwait(true);

                if (!IsDisposed)
                {
                    MessageBox.Show($"Chart exported to {path}", "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Export PNG failed");
                if (!IsDisposed)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void ExportPdf_Click(object? sender, EventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                var bmp = CaptureChartBitmap();
                if (bmp == null)
                {
                    MessageBox.Show("Unable to capture chart image.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var sfd = new SaveFileDialog { Filter = "PDF Document|*.pdf", DefaultExt = "pdf", FileName = "chart.pdf" };
                if (sfd.ShowDialog() != DialogResult.OK) { bmp.Dispose(); return; }
                var path = sfd.FileName;

                // Perform PDF creation and file write on background thread
                await Task.Run(() =>
                {
                    try
                    {
                        using var doc = new PdfDocument();
                        var page = doc.Pages.Add();
                        using var pdfImage = new PdfBitmap(bmp);

                        // Fit the image into page width while preserving aspect ratio
                        var client = page.GetClientSize();
                        var scale = Math.Min(client.Width / (float)bmp.Width, client.Height / (float)bmp.Height);
                        var drawW = bmp.Width * scale;
                        var drawH = bmp.Height * scale;
                        var rect = new RectangleF(0, 0, drawW, drawH);
                        page.Graphics.DrawImage(pdfImage, rect);

                        using var fs = File.OpenWrite(path);
                        doc.Save(fs);
                        doc.Close(true);
                    }
                    finally
                    {
                        bmp.Dispose();
                    }
                }).ConfigureAwait(true);

                if (!IsDisposed)
                {
                    MessageBox.Show($"Chart exported to {path}", "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Export PDF failed");
                if (!IsDisposed)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Releases managed resources and unsubscribes from events.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isDisposing = true; // Set flag before disposal

                Logger?.LogDebug("ChartPanel: Disposing resources");

                // Unsubscribe event handlers (ThemeManager deprecated - SfSkinManager handles theme cascade)
                try { if (_viewModelPropertyChangedHandler != null && _vm is INotifyPropertyChanged npc) npc.PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }
                try { if (_comboSelectedIndexChangedHandler != null && _comboDepartmentFilter != null) _comboDepartmentFilter.SelectedIndexChanged -= _comboSelectedIndexChangedHandler; } catch { }
                try { if (_btnRefresh != null) _btnRefresh.Click -= BtnRefresh_Click; } catch { }
                try { if (_btnExportPng != null) _btnExportPng.Click -= ExportPng_Click; } catch { }
                try { if (_btnExportPdf != null) _btnExportPdf.Click -= ExportPdf_Click; } catch { }
                try { this.Load -= ChartPanel_Load; } catch { }

                // Unsubscribe from PanelHeader events using stored named handlers
                try
                {
                    if (_panelHeader != null)
                    {
                        if (_panelHeaderRefreshHandler != null) _panelHeader.RefreshClicked -= _panelHeaderRefreshHandler;
                        if (_panelHeaderCloseHandler != null) _panelHeader.CloseClicked -= _panelHeaderCloseHandler;
                    }
                }
                catch (Exception ex)
                {
                    if (Logger != null)
                    {
                        Logger.LogDebug(ex, "ChartPanel: Failed to unsubscribe PanelHeader events");
                    }
                }

                try { _chartContextMenu?.Dispose(); } catch { }
                _chartContextMenu = null;

                // Clear event handler references
                _btnRefreshThemeChangedHandler = null;
                _viewModelPropertyChangedHandler = null;
                _comboSelectedIndexChangedHandler = null;
                _panelHeaderRefreshHandler = null;
                _panelHeaderCloseHandler = null;

                // Dispose ChartControl (specific to Syncfusion ChartControl)
                try { _chartRegionEventWiring?.Dispose(); } catch { }
                _chartRegionEventWiring = null;
                try { _chartControl?.Dispose(); } catch { }

                // Dispose Syncfusion controls safely with SafeDispose extension
                try { _comboDepartmentFilter?.SafeClearDataSource(); } catch { }
                try { _comboDepartmentFilter?.SafeDispose(); } catch { }
                try { _btnRefresh?.SafeDispose(); } catch { }
                try { _btnExportPng?.SafeDispose(); } catch { }
                try { _btnExportPdf?.SafeDispose(); } catch { }

                // Dispose other controls
                try { _panelHeader?.Dispose(); } catch { }
                try { _loadingOverlay?.Dispose(); } catch { }
                try { _noDataOverlay?.Dispose(); } catch { }
                try { _topPanel?.Dispose(); } catch { }
                try { _summaryPanel?.Dispose(); } catch { }
                try { _lblTotalBudget?.Dispose(); } catch { }
                try { _lblTotalActual?.Dispose(); } catch { }
                try { _lblTotalVariance?.Dispose(); } catch { }
                try { _lblVariancePercent?.Dispose(); } catch { }
                try { _errorProvider?.Dispose(); } catch { }
                try { foreach (var t in _toolTips) { try { t?.Dispose(); } catch { } } _toolTips.Clear(); } catch { }

                if (Logger != null)
                {
                    Logger.LogDebug("ChartPanel: Disposal complete");
                }
            }

            base.Dispose(disposing);
        }
    }
}

