using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using WileyWidget.WinForms.ViewModels;
using Syncfusion.Windows.Forms.Chart;
using WileyWidget.WinForms.Theming;
// using Syncfusion.Pdf;  // TODO: Add Syncfusion.Pdf.WinForms package for PDF export
// using Syncfusion.Pdf.Graphics;
using System.IO;
using System.Threading.Tasks;
using WileyWidget.WinForms.Extensions;

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
    /// Budget analytics panel (UserControl) with Syncfusion ChartControl.
    /// Designed for embedding in DockingManager.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class ChartPanel : UserControl
    {
        private readonly ChartViewModel _vm;
        private readonly WileyWidget.Services.Threading.IDispatcherHelper? _dispatcherHelper;
        private ChartControl? _chartControl;
        private PanelHeader? _panelHeader;
        private LoadingOverlay? _loadingOverlay;
        private NoDataOverlay? _noDataOverlay;
        private Syncfusion.WinForms.Controls.SfButton? _btnExportPng;
        private Syncfusion.WinForms.Controls.SfButton? _btnExportPdf;
        private Syncfusion.WinForms.ListView.SfComboBox? _comboDepartmentFilter;
        private Syncfusion.WinForms.Controls.SfButton? _btnRefresh;
        private Panel? _topPanel;
        private ErrorProvider? _errorProvider;
        private readonly List<ToolTip> _toolTips = new();

        // Stored event handlers for proper cleanup
        private EventHandler? _comboSelectedIndexChangedHandler;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
        private EventHandler<AppTheme>? _themeChangedHandler;
        private EventHandler<AppTheme>? _btnRefreshThemeChangedHandler;
        private EventHandler<AppTheme>? _btnExportPngThemeChangedHandler;
        private EventHandler<AppTheme>? _btnExportPdfThemeChangedHandler;
        // Named event handlers for PanelHeader (stored for proper unsubscription)
        private EventHandler? _panelHeaderRefreshHandler;
        private EventHandler? _panelHeaderCloseHandler;

        /// <summary>
        /// Simple DataContext wrapper for host compatibility.
        /// </summary>
        public new object? DataContext { get; private set; }

        /// <summary>
        /// Parameterless constructor for DI/designer support.
        /// Guards against null Program.Services and provides safe fallback.
        /// </summary>
        public ChartPanel() : this(ResolveChartViewModel()) { }

        private static ChartViewModel ResolveChartViewModel()
        {
            if (Program.Services == null)
            {
                Serilog.Log.Error("ChartPanel: Program.Services is null - cannot resolve ChartViewModel");
                throw new InvalidOperationException("ChartPanel requires DI services to be initialized. Ensure Program.Services is set before creating ChartPanel.");
            }
            try
            {
                var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ChartViewModel>(Program.Services);
                Serilog.Log.Debug("ChartPanel: ChartViewModel resolved from DI container");
                return vm;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "ChartPanel: Failed to resolve ChartViewModel from DI");
                throw;
            }
        }

        public ChartPanel(ChartViewModel vm, WileyWidget.Services.Threading.IDispatcherHelper? dispatcherHelper = null)
        {
            _dispatcherHelper = dispatcherHelper;
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = vm;
            InitializeComponent();

            // Apply current theme
            ApplyCurrentTheme();

            // Subscribe to theme changes
            _themeChangedHandler = OnThemeChanged;
            ThemeManager.ThemeChanged += _themeChangedHandler;
        }

        private void InitializeComponent()
        {
            Name = "ChartPanel";
            Size = new Size(900, 500);
            Dock = DockStyle.Fill;
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }

            // Shared header + top toolbar (consistent header at 44px)
            _panelHeader = new PanelHeader { Dock = DockStyle.Top };
            _topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };

            _comboDepartmentFilter = new Syncfusion.WinForms.ListView.SfComboBox
            {
                Width = 220,
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AllowDropDownResize = false, // Per demos: prevent dropdown resize
                MaxDropDownItems = 10, // Per demos: limit dropdown height
                AllowNull = true, // Per demos: allow null selection for "All"
                Watermark = "Select Department", // Per demos: placeholder text
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
                Width = 100,
                Height = 28,
                AccessibleName = "Refresh chart data",
                AccessibleDescription = "Reload chart data from the database"
            };
            // Add tooltip for better UX
            var refreshToolTip = new ToolTip();
            _toolTips.Add(refreshToolTip);
            refreshToolTip.SetToolTip(_btnRefresh, "Reload chart data from database (F5)");
            // Add icon from theme icon service
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;
                _btnRefresh.Image = iconService?.GetIcon("refresh", theme, 14);
                _btnRefresh.ImageAlign = ContentAlignment.MiddleLeft;
                _btnRefresh.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnRefreshThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change to avoid stale closure
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => _btnRefresh.Image = svc?.GetIcon("refresh", t, 14));
                        }
                        else if (_btnRefresh.InvokeRequired)
                        {
                            _btnRefresh.Invoke(() => _btnRefresh.Image = svc?.GetIcon("refresh", t, 14));
                        }
                        else
                        {
                            _btnRefresh.Image = svc?.GetIcon("refresh", t, 14);
                        }
                    }
                    catch { }
                };
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += _btnRefreshThemeChangedHandler;
            }
            catch { }

            _btnRefresh.Click += BtnRefresh_Click;

            // Add "Go to Dashboard" navigation button per Syncfusion demos pattern
            var btnGoToDashboard = new Syncfusion.WinForms.Controls.SfButton
            {
                Text = "Dashboard",
                Width = 100,
                Height = 28,
                AccessibleName = "Go to Dashboard",
                AccessibleDescription = "Navigate to the Dashboard panel"
            };
            var dashToolTip = new ToolTip();
            _toolTips.Add(dashToolTip);
            dashToolTip.SetToolTip(btnGoToDashboard, "Open the Dashboard panel (Ctrl+Shift+D)");
            try
            {
                var iconSvc = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                btnGoToDashboard.Image = iconSvc?.GetIcon("home", ThemeManager.CurrentTheme, 14);
                btnGoToDashboard.ImageAlign = ContentAlignment.MiddleLeft;
                btnGoToDashboard.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch { }
            btnGoToDashboard.Click += (s, e) => NavigateToDashboard();

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true
            };
            flow.Controls.Add(_comboDepartmentFilter);
            flow.Controls.Add(_btnRefresh);
            // Export buttons
            _btnExportPng = new Syncfusion.WinForms.Controls.SfButton { Text = "Export PNG", Width = 100, Height = 28, AccessibleName = "Export chart as PNG" };
            var exportPngTip = new ToolTip();
            _toolTips.Add(exportPngTip);
            exportPngTip.SetToolTip(_btnExportPng, "Export the current chart view to a PNG image");
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = ThemeManager.CurrentTheme;
                _btnExportPng.Image = iconService?.GetIcon("export", theme, 14);
                _btnExportPng.ImageAlign = ContentAlignment.MiddleLeft;
                _btnExportPng.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnExportPngThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => _btnExportPng.Image = svc?.GetIcon("export", t, 14));
                        }
                        else if (_btnExportPng.InvokeRequired)
                        {
                            _btnExportPng.Invoke(() => _btnExportPng.Image = svc?.GetIcon("export", t, 14));
                        }
                        else
                        {
                            _btnExportPng.Image = svc?.GetIcon("export", t, 14);
                        }
                    }
                    catch { }
                };
                ThemeManager.ThemeChanged += _btnExportPngThemeChangedHandler;
            }
            catch { }
            _btnExportPng.Click += ExportPng_Click;
            flow.Controls.Add(_btnExportPng);

            _btnExportPdf = new Syncfusion.WinForms.Controls.SfButton { Text = "Export PDF", Width = 100, Height = 28, AccessibleName = "Export chart as PDF" };
            var exportPdfTip = new ToolTip();
            _toolTips.Add(exportPdfTip);
            exportPdfTip.SetToolTip(_btnExportPdf, "Export the current chart view embedded in a PDF");
            try
            {
                var iconService = Program.Services != null
                    ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                    : null;
                var theme = ThemeManager.CurrentTheme;
                _btnExportPdf.Image = iconService?.GetIcon("pdf", theme, 14);
                _btnExportPdf.ImageAlign = ContentAlignment.MiddleLeft;
                _btnExportPdf.TextImageRelation = TextImageRelation.ImageBeforeText;
                _btnExportPdfThemeChangedHandler = (s, t) =>
                {
                    try
                    {
                        // Re-resolve icon service on theme change
                        var svc = Program.Services != null
                            ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services)
                            : null;
                        if (_dispatcherHelper != null)
                        {
                            _ = _dispatcherHelper.InvokeAsync(() => _btnExportPdf.Image = svc?.GetIcon("pdf", t, 14));
                        }
                        else if (_btnExportPdf.InvokeRequired)
                        {
                            _btnExportPdf.Invoke(() => _btnExportPdf.Image = svc?.GetIcon("pdf", t, 14));
                        }
                        else
                        {
                            _btnExportPdf.Image = svc?.GetIcon("pdf", t, 14);
                        }
                    }
                    catch { }
                };
                ThemeManager.ThemeChanged += _btnExportPdfThemeChangedHandler;
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
            // Let SfSkinManager handle theming instead of hardcoded colors
            _chartControl = new ChartControl { Dock = DockStyle.Fill };

            // Apply Metro skin first per demos (chart.Skins = Skins.Metro)
            _chartControl.Skins = Syncfusion.Windows.Forms.Chart.Skins.Metro;

            // Chart appearance per demos: SmoothingMode, ElementsSpacing, BorderAppearance
            _chartControl.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            _chartControl.ElementsSpacing = 5;
            _chartControl.BorderAppearance.SkinStyle = Syncfusion.Windows.Forms.Chart.ChartBorderSkinStyle.None;

            // Use transparent BackInterior to allow theme background to show through
            _chartControl.ChartArea.BackInterior = new Syncfusion.Drawing.BrushInfo(Color.Transparent);
            _chartControl.ChartArea.PrimaryXAxis.HidePartialLabels = true;

            // Configure axes per demo patterns (Categorical Axis demo)
            _chartControl.PrimaryXAxis.ValueType = ChartValueType.Category;
            _chartControl.PrimaryXAxis.Title = ChartPanelResources.Axis_Department;
            _chartControl.PrimaryXAxis.TitleFont = new Font("Segoe UI", 10F);
            _chartControl.PrimaryXAxis.Font = new Font("Segoe UI", 10F);
            _chartControl.PrimaryXAxis.LabelRotate = true;
            _chartControl.PrimaryXAxis.LabelRotateAngle = 45;
            _chartControl.PrimaryXAxis.DrawGrid = false;

            // Enable scrollbar on X axis and zooming for better exploration
            try
            {
                // Some Syncfusion versions name these properties differently. Use reflection to set if available.
                var px = _chartControl.PrimaryXAxis;
                var propEnableScroll = px?.GetType().GetProperty("EnableScrollBar");
                if (propEnableScroll != null && propEnableScroll.CanWrite) propEnableScroll.SetValue(px, true);
            }
            catch { }
            try
            {
                var chartType = _chartControl.GetType();
                var propEnableZoom = chartType.GetProperty("EnableZooming");
                if (propEnableZoom != null && propEnableZoom.CanWrite) propEnableZoom.SetValue(_chartControl, true);
            }
            catch { }

            _chartControl.PrimaryYAxis.Title = ChartPanelResources.Axis_Variance;
            _chartControl.PrimaryYAxis.TitleFont = new Font("Segoe UI", 10F);
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
                var secAxis = new Syncfusion.Windows.Forms.Chart.ChartAxis();
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

            // Enable tooltips per demos
            _chartControl.ShowToolTips = true;

            // Configure legend per demo patterns (ChartLegend demo)
            _chartControl.ShowLegend = true;
            _chartControl.LegendsPlacement = Syncfusion.Windows.Forms.Chart.ChartPlacement.Outside;
            _chartControl.LegendPosition = ChartDock.Bottom;
            _chartControl.LegendAlignment = ChartAlignment.Center;
            _chartControl.Legend.Font = new Font("Segoe UI", 10F);
            _chartControl.Legend.BackColor = Color.Transparent;

            // Column width mode per demos
            _chartControl.Spacing = 5;

            Controls.Add(_chartControl);

            // Add overlays (loading spinner and no-data friendly message)
            _loadingOverlay = new LoadingOverlay { Message = "Loading chart data..." };
            Controls.Add(_loadingOverlay);

            _noDataOverlay = new NoDataOverlay { Message = "No chart data available" };
            Controls.Add(_noDataOverlay);

            _errorProvider = new ErrorProvider() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

            // Wiring: load data and set up simple bindings
            Load += ChartPanel_Load;

            try
            {
                if (_vm.ChartData != null)
                {
                    _comboDepartmentFilter.DataSource = _vm.ChartData.Select(k => k.Key).ToList();
                    _comboSelectedIndexChangedHandler = ComboFilter_SelectedIndexChanged;
                    _comboDepartmentFilter.SelectedIndexChanged += _comboSelectedIndexChangedHandler;
                }
            }
            catch { }

            // Watch for view model changes to update chart UI
            if (_vm is INotifyPropertyChanged npc)
            {
                _viewModelPropertyChangedHandler = ViewModel_PropertyChanged;
                npc.PropertyChanged += _viewModelPropertyChangedHandler;
            }

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
                if (parentForm is WileyWidget.WinForms.Forms.MainForm mf)
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
                if (IsDisposed) return;

                await (_vm.RefreshCommand?.ExecuteAsync(null) ?? Task.CompletedTask).ConfigureAwait(false);

                if (!IsDisposed)
                {
                    if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                    {
                        try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                    }
                    else
                    {
                        if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                        {
                            try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                        }
                        else
                        {
                            if (InvokeRequired) BeginInvoke(new Action(UpdateChartFromData)); else UpdateChartFromData();
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
                if (IsDisposed) return;

                await _vm.LoadChartDataAsync().ConfigureAwait(false);

                if (!IsDisposed)
                {
                    if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                    {
                        try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                    }
                    else
                    {
                        if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                        {
                            try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                        }
                        else
                        {
                            if (InvokeRequired) BeginInvoke(new Action(UpdateChartFromData)); else UpdateChartFromData();
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
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (IsDisposed) return;

                if (e.PropertyName == nameof(_vm.ChartData) || e.PropertyName == nameof(_vm.ErrorMessage))
                {
                    if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                    {
                        try { _ = _dispatcherHelper.InvokeAsync(() => ViewModel_PropertyChanged(sender, e)); } catch { }
                        return;
                    }
                    if (InvokeRequired)
                    {
                        try { BeginInvoke(new Action(() => ViewModel_PropertyChanged(sender, e))); } catch { }
                        return;
                    }

                    try { UpdateChartFromData(); } catch { }
                    try
                    {
                        if (_comboDepartmentFilter != null)
                            _comboDepartmentFilter.DataSource = _vm.ChartData?.Select(k => k.Key).ToList();
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
        private void OnThemeChanged(object? sender, AppTheme theme)
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
                    try { BeginInvoke(new Action(() => OnThemeChanged(sender, theme))); } catch { }
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
                ThemeManager.ApplyThemeToControl(this);
                // Apply Syncfusion skin using global theme (FluentDark default, FluentLight fallback)
                // Per-form Syncfusion theming is already applied by ThemeManager.ApplyTheme(this) above
            }
            catch { }
        }

        private void UpdateChartFromData()
        {
            try
            {
                if (_dispatcherHelper != null && !_dispatcherHelper.CheckAccess())
                {
                    try { _ = _dispatcherHelper.InvokeAsync(UpdateChartFromData); } catch { }
                    return;
                }
                if (InvokeRequired)
                {
                    Invoke(new Action(UpdateChartFromData));
                    return;
                }

                _chartControl?.Series.Clear();

                var data = _vm.ChartData;
                if (data == null || !data.Any())
                {
                    // Show fallback message when no data
                    if (_chartControl != null)
                    {
                        _chartControl.Visible = false;
                    }

                    var existingLabel = Controls.OfType<Label>().FirstOrDefault(l => l.Name == "NoDataLabel");
                    if (existingLabel == null)
                    {
                        var message = !string.IsNullOrEmpty(_vm.ErrorMessage) ? _vm.ErrorMessage : "No department budget data available to display.";
                        var noDataLabel = new Label
                        {
                            Name = "NoDataLabel",
                            Text = message,
                            AutoSize = false,
                            Dock = DockStyle.Fill,
                            TextAlign = ContentAlignment.MiddleCenter
                        };
                        Controls.Add(noDataLabel);
                    }
                    return;
                }

                // Remove no data label if present
                var labelToRemove = Controls.OfType<Label>().FirstOrDefault(l => l.Name == "NoDataLabel");
                if (labelToRemove != null)
                {
                    Controls.Remove(labelToRemove);
                    labelToRemove.Dispose();
                }

                if (_chartControl != null)
                {
                    _chartControl.Visible = true;
                }

                var list = data.OrderByDescending(k => k.Value).ToList();

                // Configure series per Syncfusion demo best practices (Column Charts demo)
                var series = new ChartSeries("Variance", ChartSeriesType.Column);
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
                // Use ThemeManager.Colors for theme-aware accent color
                var colors = ThemeManager.Colors;
                series.Style.Interior = new Syncfusion.Drawing.BrushInfo(colors.Accent);
                // Make border transparent per demos
                series.Style.Border.Color = Color.Transparent;

                // Enable tooltips per demos
                series.PointsToolTipFormat = "{0}: {1:C0}";
                series.ShowTicks = true;

                _chartControl?.Series.Add(series);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "ChartPanel: failed updating chart from data");
            }
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
                try { Invoke(new Action(() => { bmp = new Bitmap(_chartControl.Width, _chartControl.Height); _chartControl.DrawToBitmap(bmp, new Rectangle(0, 0, _chartControl.Width, _chartControl.Height)); })); }
                catch { bmp = null; }
            }
            else
            {
                try { bmp = new Bitmap(_chartControl.Width, _chartControl.Height); _chartControl.DrawToBitmap(bmp, new Rectangle(0, 0, _chartControl.Width, _chartControl.Height)); } catch { bmp = null; }
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

                MessageBox.Show($"Chart exported to {path}", "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Export PNG failed");
                MessageBox.Show($"Export failed: {ex.Message}", "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        // TODO: Add Syncfusion.Pdf.WinForms package to enable PDF export
                        throw new NotImplementedException("PDF export requires Syncfusion.Pdf.WinForms package");
                        // using var doc = new PdfDocument();
                        // var page = doc.Pages.Add();
                        // using var pdfImage = new PdfBitmap(bmp);

                        // // Fit the image into page width while preserving aspect
                        // var client = page.GetClientSize();
                        // var scale = Math.Min(client.Width / (float)bmp.Width, client.Height / (float)bmp.Height);
                        // var drawW = bmp.Width * scale;
                        // var drawH = bmp.Height * scale;
                        // var rect = new System.Drawing.RectangleF(0, 0, drawW, drawH);
                        // page.Graphics.DrawImage(pdfImage, rect);

                        // using var fs = File.OpenWrite(path);
                        // doc.Save(fs);
                        // doc.Close(true);
                    }
                    finally
                    {
                        bmp.Dispose();
                    }
                }).ConfigureAwait(true);

                MessageBox.Show($"Chart exported to {path}", "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Export PDF failed");
                MessageBox.Show($"Export failed: {ex.Message}", "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Releases managed resources and unsubscribes from events.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe event handlers
                try { if (_themeChangedHandler != null) ThemeManager.ThemeChanged -= _themeChangedHandler; } catch { }
                try { if (_btnRefreshThemeChangedHandler != null) ThemeManager.ThemeChanged -= _btnRefreshThemeChangedHandler; } catch { }
                try { if (_btnExportPngThemeChangedHandler != null) ThemeManager.ThemeChanged -= _btnExportPngThemeChangedHandler; } catch { }
                try { if (_btnExportPdfThemeChangedHandler != null) ThemeManager.ThemeChanged -= _btnExportPdfThemeChangedHandler; } catch { }
                try { if (_viewModelPropertyChangedHandler != null && _vm is INotifyPropertyChanged npc) npc.PropertyChanged -= _viewModelPropertyChangedHandler; } catch { }
                try { if (_comboSelectedIndexChangedHandler != null && _comboDepartmentFilter != null) _comboDepartmentFilter.SelectedIndexChanged -= _comboSelectedIndexChangedHandler; } catch { }
                try { if (_btnRefresh != null) _btnRefresh.Click -= BtnRefresh_Click; } catch { }
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
                    Serilog.Log.Debug(ex, "ChartPanel: Failed to unsubscribe PanelHeader events");
                }

                // Dispose controls
                try { _chartControl?.Dispose(); } catch { }

                // Clear DataSource and dispose Syncfusion combo safely via helper
                try { _comboDepartmentFilter.SafeClearDataSource(); } catch { }
                try { _comboDepartmentFilter.SafeDispose(); } catch { }
                try { _btnRefresh?.Dispose(); } catch { }
                try { if (_btnExportPng != null) _btnExportPng.Click -= ExportPng_Click; } catch { }
                try { if (_btnExportPdf != null) _btnExportPdf.Click -= ExportPdf_Click; } catch { }
                try { _btnExportPngThemeChangedHandler = null; } catch { }
                try { _btnExportPdfThemeChangedHandler = null; } catch { }
                try { _btnExportPng?.Dispose(); } catch { }
                try { _btnExportPdf?.Dispose(); } catch { }
                try { _panelHeader?.Dispose(); } catch { }
                try { _loadingOverlay?.Dispose(); } catch { }
                try { _noDataOverlay?.Dispose(); } catch { }
                try { _topPanel?.Dispose(); } catch { }
                try { _errorProvider?.Dispose(); } catch { }
                try { foreach (var t in _toolTips) { try { t?.Dispose(); } catch { } } _toolTips.Clear(); } catch { }
            }

            base.Dispose(disposing);
        }
    }
}
