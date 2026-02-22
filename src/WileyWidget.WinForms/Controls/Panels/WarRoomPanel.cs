#nullable enable

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// War Room — interactive scenario analysis panel.
    /// </summary>
    public partial class WarRoomPanel : ScopedPanelBase<WarRoomViewModel>, ICompletablePanel
    {
        // Fields declared here and used by InitializeComponent in Designer.cs
        private Panel? _topPanel;
        private PanelHeader? _panelHeader;
        private TextBox? _scenarioInput;
        private SfButton? _btnRunScenario;
        private SfButton? _btnExportForecast;
        private Panel? _contentPanel;
        private Panel? _resultsPanel;
        private Label? _statusLabel;
        private ToolTip? _toolTip;
        private bool _dataLoaded;

        public WarRoomPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase<WarRoomViewModel>> logger)
            : base(scopeFactory, logger)
        {
            SafeSuspendAndLayout(InitializeComponent);
            Dock = DockStyle.Fill;
            MinimumSize = new Size(1024, 720);
            var themeName = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(this, themeName);

            _toolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 250,
                ReshowDelay = 100,
                ShowAlways = true
            };

            if (_scenarioInput != null)
            {
                _scenarioInput.AccessibleName = "Scenario Input";
                _toolTip.SetToolTip(_scenarioInput, "Describe assumptions for the scenario analysis.");
            }

            if (_btnRunScenario != null)
            {
                _btnRunScenario.Text = "&Run Scenario";
                _btnRunScenario.ThemeName = themeName;
                _btnRunScenario.AccessibleName = "Run Scenario";
                _toolTip.SetToolTip(_btnRunScenario, "Run what-if analysis for the current scenario.");
            }

            if (_btnExportForecast != null)
            {
                _btnExportForecast.Text = "&Export Forecast";
                _btnExportForecast.ThemeName = themeName;
                _btnExportForecast.AccessibleName = "Export Forecast";
                _toolTip.SetToolTip(_btnExportForecast, "Export forecast results for reporting.");
            }

            if (_contentPanel != null)
            {
                _statusLabel = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = 24,
                    Padding = new Padding(4, 0, 0, 0),
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Text = "Ready",
                    AccessibleName = "War Room Status"
                };

                _contentPanel.Controls.Add(_statusLabel);
                _statusLabel.BringToFront();
            }

            // Wire close button — base ClosePanel() routes through MainForm.ClosePanel(string)
            if (_panelHeader != null)
            {
                _panelHeader.CloseClicked += (s, e) => ClosePanel();
                _panelHeader.RefreshClicked += async (s, e) => await LoadAsync();
            }

            if (_btnRunScenario != null)
                _btnRunScenario.Click += OnRunScenarioClicked;

            if (_btnExportForecast != null)
                _btnExportForecast.Click += OnExportForecastClicked;

            Logger.LogDebug("[WarRoomPanel] Initialized");
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            if (Visible && !_dataLoaded)
            {
                _dataLoaded = true;
                LoadAsyncSafe();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            MinimumSize = new Size(1024, 720);
            PerformLayout();
            Invalidate(true);

            if (_panelHeader != null)
            {
                _panelHeader.Title = "War Room";
            }
        }

        public override async Task LoadAsync(CancellationToken ct = default)
        {
            if (IsLoaded) return;
            IsBusy = true;
            Logger.LogDebug("[WarRoomPanel] LoadAsync starting");

            try
            {
                // WarRoomViewModel does not implement IAsyncInitializable — base handles gracefully
                await base.LoadAsync(ct);
                BindViewModel();
                Logger.LogDebug("[WarRoomPanel] LoadAsync completed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BindViewModel()
        {
            // Minimal binding — extend as WarRoomViewModel grows
            if (_scenarioInput != null && ViewModel != null)
            {
                // ScenarioInput is not yet on WarRoomViewModel — wire when added
            }
            Logger.LogDebug("[WarRoomPanel] BindViewModel complete (ViewModel={VmPresent})", ViewModel is not null);
        }

        private async void OnRunScenarioClicked(object? sender, EventArgs e)
        {
            try
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = true;
                }
                SetStatusMessage("Running scenario analysis…");

                if (ViewModel?.RunScenarioCommand?.CanExecute(null) ?? false)
                {
                    await ViewModel.RunScenarioCommand.ExecuteAsync(null);
                    SetStatusMessage("Scenario analysis complete.");
                }
                else
                {
                    Logger.LogWarning("[WarRoomPanel] RunScenarioCommand unavailable");
                    SetStatusMessage("Scenario command is not available.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[WarRoomPanel] RunScenario click failed");
                SetStatusMessage($"Unable to run scenario: {ex.Message}");
            }
            finally
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = false;
                }
            }
        }

        private async void OnExportForecastClicked(object? sender, EventArgs e)
        {
            try
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = true;
                }
                SetStatusMessage("Exporting forecast…");

                if (ViewModel?.ExportForecastCommand?.CanExecute(null) ?? false)
                {
                    await ViewModel.ExportForecastCommand.ExecuteAsync(null);
                    SetStatusMessage("Forecast export complete.");
                }
                else
                {
                    Logger.LogWarning("[WarRoomPanel] ExportForecastCommand unavailable");
                    SetStatusMessage("Export command is not available.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[WarRoomPanel] ExportForecast click failed");
                SetStatusMessage($"Unable to export forecast: {ex.Message}");
            }
            finally
            {
                if (_panelHeader != null)
                {
                    _panelHeader.IsLoading = false;
                }
            }
        }

        private void SetStatusMessage(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = message;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
