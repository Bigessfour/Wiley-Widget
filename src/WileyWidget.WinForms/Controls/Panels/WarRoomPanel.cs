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
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utilities;
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

        // Canonical skeleton fields
        private readonly SyncfusionControlFactory? _factory;
        private TableLayoutPanel? _content;
        private LoadingOverlay? _loader;

        /// <summary>
        /// Canonical constructor with direct dependencies.
        /// </summary>
        public WarRoomPanel(WarRoomViewModel vm, SyncfusionControlFactory factory)
            : base(vm, ResolveLogger())
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1100, 760);
            MinimumSize = new Size(1024, 720);
            SafeSuspendAndLayout(InitializeControls);
        }

        private static ILogger ResolveLogger()
        {
            return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<WarRoomPanel>>(Program.Services)
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WarRoomPanel>.Instance;
        }

        private void InitializeControls()
        {
            SuspendLayout();

            Name = "WarRoomPanel";
            AccessibleName = "War Room"; // Panel title for UI automation
            Size = new Size(1100, 760);
            MinimumSize = new Size(1024, 720);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = Padding.Empty;

            // Apply theme for cascade to all child controls
            SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme);

            // Panel header
            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Top,
                Title = "War Room",
                Height = LayoutTokens.HeaderHeight
            };
            Controls.Add(_panelHeader);

            // Canonical _content root
            _content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                AutoSize = false,
                Name = "WarRoomPanelContent"
            };
            _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Top panel
            _topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = LayoutTokens.Dp(128),
                Padding = new Padding(LayoutTokens.PanelPadding, LayoutTokens.PanelPadding, LayoutTokens.PanelPadding, 0)
            };

            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Top,
                Title = "War Room"
            };
            _topPanel.Controls.Add(_panelHeader);

            var actionsRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = LayoutTokens.Dp(48),
                ColumnCount = 3,
                RowCount = 1
            };
            actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _scenarioInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Height = LayoutTokens.Dp(LayoutTokens.StandardControlHeight),
                Margin = new Padding(0, LayoutTokens.ContentMargin, LayoutTokens.ContentMargin, LayoutTokens.ContentMargin)
            };
            actionsRow.Controls.Add(_scenarioInput, 0, 0);

            _btnRunScenario = _factory!.CreateSfButton("Run Scenario", button =>
            {
                button.AutoSize = true;
                button.Margin = new Padding(0, LayoutTokens.ContentMargin, LayoutTokens.ContentMargin, LayoutTokens.ContentMargin);
            });
            actionsRow.Controls.Add(_btnRunScenario, 1, 0);

            _btnExportForecast = _factory!.CreateSfButton("Export Forecast", button =>
            {
                button.AutoSize = true;
                button.Margin = new Padding(0, LayoutTokens.ContentMargin, 0, LayoutTokens.ContentMargin);
            });
            actionsRow.Controls.Add(_btnExportForecast, 2, 0);

            _topPanel.Controls.Add(actionsRow);

            // Content panel
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(LayoutTokens.PanelPadding)
            };

            _resultsPanel = new Panel
            {
                Dock = DockStyle.Fill
            };
            _contentPanel.Controls.Add(_resultsPanel);

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Padding = new Padding(4, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Ready",
                AccessibleName = "War Room Status"
            };
            _contentPanel.Controls.Add(_statusLabel);
            _statusLabel.BringToFront();

            _content.Controls.Add(_contentPanel, 0, 0);

            // Add content root to controls
            Controls.Add(_content);

            // Loading overlay
            _loader = _factory!.CreateLoadingOverlay(overlay =>
            {
                overlay.Dock = DockStyle.Fill;
                overlay.Visible = false;
            });
            Controls.Add(_loader);

            // Tooltips
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
                _btnRunScenario.AccessibleName = "Run Scenario";
                _toolTip.SetToolTip(_btnRunScenario, "Run what-if analysis for the current scenario.");
            }

            if (_btnExportForecast != null)
            {
                _btnExportForecast.AccessibleName = "Export Forecast";
                _toolTip.SetToolTip(_btnExportForecast, "Export forecast results for reporting.");
            }

            // Wire events
            if (_panelHeader != null)
            {
                _panelHeader.CloseClicked += (s, e) => ClosePanel();
                _panelHeader.RefreshClicked += async (s, e) => await LoadAsync();
            }

            if (_btnRunScenario != null)
                _btnRunScenario.Click += OnRunScenarioClicked;

            if (_btnExportForecast != null)
                _btnExportForecast.Click += OnExportForecastClicked;

            ResumeLayout(false);
        }

        /// <summary>
        /// Constructor that accepts required dependencies from DI container.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public WarRoomPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase<WarRoomViewModel>> logger)
            : base(scopeFactory, logger)
        {
            _factory = ControlFactory;
            SafeSuspendAndLayout(InitializeControls);
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
            _loader!.Visible = true;
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
                _loader!.Visible = false;
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
