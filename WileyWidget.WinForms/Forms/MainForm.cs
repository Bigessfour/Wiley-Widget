using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.WinForms.Theming;
using Syncfusion.Windows.Forms.Tools; // docking manager
using System.IO; // layout persistence
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms
{
    internal static class MainFormResources
    {
        public const string FormTitle = "Wiley Widget — Running on WinForms + .NET 9";
        public const string FileMenu = "File";
        public const string AccountsMenu = "Accounts";
        public const string ChartsMenu = "Charts";
        public const string SettingsMenu = "Settings";
        public const string ExitMenu = "Exit";
    }

    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class MainForm : Form
    {
        private MenuStrip? _menuStrip;
        private DockingManager? _dockingManager;  // Syncfusion DockingManager
        private readonly Dictionary<string, Form> _dockedForms = new();

        private const string LayoutFile = "docking_layout.xml";

        public MainForm()
        {
            // Initialize theme system first
            ThemeManager.Initialize();

            InitializeComponent();
            Text = MainFormResources.FormTitle;

            // Initialize docking manager (adds control to main client area)
            InitializeDockingManager();

            // Load persisted layout (if present)
            LoadDockingLayout();

            // Apply theme to this form
            ApplyCurrentTheme();

            // Subscribe to theme changes
            ThemeManager.ThemeChanged += OnThemeChanged;

            // Global handlers to surface UI-thread and domain unhandled exceptions
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void InitializeComponent()
        {
            _menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem(MainFormResources.FileMenu);
            var accountsMenu = new ToolStripMenuItem(MainFormResources.AccountsMenu);
            var chartsMenu = new ToolStripMenuItem(MainFormResources.ChartsMenu);
            var settingsMenu = new ToolStripMenuItem(MainFormResources.SettingsMenu);
            var exitItem = new ToolStripMenuItem(MainFormResources.ExitMenu, null, (s, e) => Application.Exit());

            accountsMenu.Click += (s, e) => DockPanel<AccountsForm>(MainFormResources.AccountsMenu);
            chartsMenu.Click += (s, e) => DockPanel<ChartForm>(MainFormResources.ChartsMenu);
            settingsMenu.Click += (s, e) => DockPanel<SettingsForm>(MainFormResources.SettingsMenu);

            // Apply theme-aware icons to menus (best-effort). Use DI service to load matching variants.
            try
            {
                var iconService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.WinForms.Services.IThemeIconService>(Program.Services);
                var theme = WileyWidget.WinForms.Theming.ThemeManager.CurrentTheme;

                accountsMenu.Image = iconService?.GetIcon("accounts", theme, 18);
                chartsMenu.Image = iconService?.GetIcon("chart", theme, 18);
                settingsMenu.Image = iconService?.GetIcon("settings", theme, 18);
                exitItem.Image = iconService?.GetIcon("dismiss", theme, 14);

                // Ensure style and spacing are reasonable for small menu icons
                accountsMenu.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                chartsMenu.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                settingsMenu.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                exitItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;

                // Update icons when theme changes
                WileyWidget.WinForms.Theming.ThemeManager.ThemeChanged += (s, t) =>
                {
                    try
                    {
                        accountsMenu.Image = iconService?.GetIcon("accounts", t, 18);
                        chartsMenu.Image = iconService?.GetIcon("chart", t, 18);
                        settingsMenu.Image = iconService?.GetIcon("settings", t, 18);
                        exitItem.Image = iconService?.GetIcon("dismiss", t, 14);
                    }
                    catch { }
                };
            }
            catch { }

            fileMenu.DropDownItems.Add(exitItem);
            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, accountsMenu, chartsMenu, settingsMenu });

            Controls.Add(_menuStrip);
            MainMenuStrip = _menuStrip;

            Size = new Size(1200, 800);
            try { AutoScaleMode = AutoScaleMode.Dpi; } catch { }
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void InitializeDockingManager()
        {
            try
            {
                // Create and configure docking manager component
                _dockingManager = new DockingManager();
                _dockingManager.HostControl = this; // host on MainForm

                // Configure DockingManager using the pinned Syncfusion API (v31.2.15)
                try
                {
                    _dockingManager.ShowCaption = true;
                    _dockingManager.EnableContextMenu = true;
                    // Allow smooth auto-hide visuals when available
                    _dockingManager.AutoHideAnimation = true;

                    // Subscribe to docking manager events for lifecycle & lazy loading (use dynamic args to stay resilient)
                    _dockingManager.DockStateChanged += (s, e) =>
                    {
                        try
                        {
                            dynamic? a = e;
                            if (a == null) return;
                            Control? ctrl = a.Control as Control;
                            var ds = a.DockState;

                            if (ctrl is AccountsForm af && af.DataContext is AccountsViewModel avm)
                            {
                                if (ds == DockState.Document || ds == DockState.Docked)
                                    avm.LoadAccountsCommand?.Execute(null);
                            }

                            if (ctrl is ChartForm cf && cf.DataContext is ChartViewModel cvm)
                            {
                                if (ds == DockState.Document || ds == DockState.Docked)
                                    _ = cvm.LoadChartDataAsync();
                            }
                        }
                        catch { }
                    };

                    _dockingManager.ActiveWindowChanged += (s, e) =>
                    {
                        try
                        {
                            dynamic? a = e;
                            var win = a?.ActiveWindow as Control;
                            if (win is AccountsForm af && af.DataContext is AccountsViewModel avm)
                            {
                                avm.LoadAccountsCommand?.Execute(null);
                            }
                            else if (win is ChartForm cf && cf.DataContext is ChartViewModel cvm)
                            {
                                _ = cvm.LoadChartDataAsync();
                            }
                        }
                        catch { }
                    };
                }
                catch (Exception ex)
                {
                    // Some Syncfusion versions may vary in API surface; guard against runtime issues
                    Serilog.Log.Debug(ex, "DockingManager property/event wiring failed on this Syncfusion version — falling back to best-effort configuration.");
                }

                // We will perform initial lazy loads when creating docked panels. Avoid subscribing to version-dependent events here.
                // (Some Syncfusion versions provide ActiveWindowChanged/DockVisibilityChanged events — those can be wired later if desired.)

                // Add docking manager under menu (menu is top docked item)
                // Ensure menu sits above docking manager
                if (_menuStrip != null)
                {
                    _menuStrip.Dock = DockStyle.Top;
                }

                // DockingManager is a component that manages the host control (MainForm). No Controls.Add is required.
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to initialize DockingManager — falling back to non-docked layout");
            }
        }

        private void ApplyCurrentTheme()
        {
            ThemeManager.ApplyTheme(this);
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnThemeChanged(sender, theme));
                return;
            }

            ApplyCurrentTheme();
        }

        private void menuAccounts_Click(object? sender, EventArgs e)
        {
            try
            {
                // If we have a docking manager available, use docked panel, otherwise fall back to modal
                if (_dockingManager != null)
                {
                    DockPanel<AccountsForm>(MainFormResources.AccountsMenu);
                }
                else
                {
                    using var scope = Program.Services.CreateScope();
                    var provider = scope.ServiceProvider;
                    var accountsForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AccountsForm>(provider);
                    accountsForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var reporting = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.ErrorReportingService>(Program.Services);
                    reporting?.ReportError(ex, "Failed to open Accounts form", showToUser: true);
                }
                catch { }
            }
        }

        private void menuSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_dockingManager != null)
                {
                    DockPanel<SettingsForm>(MainFormResources.SettingsMenu);
                }
                else
                {
                    using var settingsForm = new SettingsForm(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SettingsViewModel>(Program.Services));
                    settingsForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to open Settings form");
            }
        }

        private void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            // Log the exception and notify the user
            Serilog.Log.Error(e.Exception, "Unhandled UI thread exception");
            MessageBox.Show(
                $"An error occurred:\n\n{e.Exception.Message}\n\nCheck logs for details.",
                "Application Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _menuStrip?.Dispose();
                _dockingManager?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveDockingLayout();
            base.OnFormClosing(e);
        }

        private void SaveDockingLayout()
        {
            if (_dockingManager != null)
            {
                try
                {
                    // Use the explicit SaveDockState API to persist layout
                    try
                    {
                        _dockingManager.PersistState = true; // enable persistence
                        _dockingManager.SaveDockState();
                    }
                    catch
                    {
                        // If XML-based API exists, call that as a fallback
                        try { _dockingManager.SaveDockStateToXml(LayoutFile); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Failed to save docking layout");
                }
            }
        }

        private void LoadDockingLayout()
        {
            if (_dockingManager == null || !File.Exists(LayoutFile)) return;

            try
            {
                try
                {
                    // Prefer LoadDockState (reads from isolated storage when PersistState was used)
                    _dockingManager.LoadDockState();
                }
                catch
                {
                    // Fallback: try XML loader
                    try { _dockingManager.LoadDockStateFromXml(LayoutFile); } catch { }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to load docking layout");
            }
        }

        // Create or show a docked panel for a transient Form type using DI
        private void DockPanel<TForm>(string panelName) where TForm : Form
        {
            if (_dockingManager == null)
            {
                // fallback to modal show — create transient form via DI scope
                using var scope = Program.Services.CreateScope();
                var provider = scope.ServiceProvider;
                var fallback = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TForm>(provider);
                fallback.ShowDialog(this);
                return;
            }

            // If already created and tracked, show it
            if (_dockedForms.TryGetValue(panelName, out var existing) && existing != null)
            {
                try
                {
                    // Bring the form to front in the host
                    existing.Show();
                    existing.BringToFront();
                }
                catch { }
                return;
            }

            try
            {
                using var scope = Program.Services.CreateScope();
                var provider = scope.ServiceProvider;
                var form = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TForm>(provider);

                // Prepare for docking if the form has that helper
                if (form is AccountsForm accountsForm)
                    accountsForm.PrepareForDocking();
                else if (form is ChartForm chartForm)
                    chartForm.PrepareForDocking();
                else if (form is SettingsForm settingsForm)
                    settingsForm.PrepareForDocking();
                else
                {
                    form.TopLevel = false;
                    form.FormBorderStyle = FormBorderStyle.None;
                    form.Dock = DockStyle.Fill;
                    form.StartPosition = FormStartPosition.Manual;
                }

                form.Name = panelName;

                // Enable docking for the window using reflection to support multiple Syncfusion API shapes
                // Use direct DockingManager API now that a pinned Syncfusion version is targeted
                try
                {
                    _dockingManager.SetEnableDocking(form, true);

                    // Preferred API: dock the control as a document tab with a default size hint
                    _dockingManager.DockControl(form, this, DockingStyle.Document, 600);

                    // Label and state for UX
                    _dockingManager.SetDockLabel(form, panelName);
                    _dockingManager.SetDockState(form, DockState.Document);
                }
                catch (Exception)
                {
                    // If the pinned API is missing or fails, fall back to adding as a regular control
                    Controls.Add(form);
                }

                // Keep track of created, non-modal forms to avoid duplicates
                _dockedForms[panelName] = form;

                // Apply theme to embedded form as well
                ThemeManager.ApplyTheme(form);

                // Trigger initial load for viewmodels where helpful
                try
                {
                    if (form is AccountsForm af && af.DataContext is AccountsViewModel avm)
                    {
                        avm.LoadAccountsCommand?.Execute(null);
                    }
                    else if (form is ChartForm cform && cform.DataContext is ChartViewModel cvm)
                    {
                        _ = cvm.LoadChartDataAsync();
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to dock {Panel}", panelName);
                MessageBox.Show($"Error docking {panelName}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Serilog.Log.Fatal(ex, "Unhandled domain exception (IsTerminating: {IsTerminating})", e.IsTerminating);

            if (e.IsTerminating)
            {
                MessageBox.Show(
                    $"Fatal error:\n\n{ex?.Message ?? "Unknown error"}\n\nApplication will close.",
                    "Fatal Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Stop);
            }
        }

        // Docking events are subscribed inline with resilient lambdas in InitializeDockingManager
    }
}
