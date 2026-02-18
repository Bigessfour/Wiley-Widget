#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// War Room — interactive scenario analysis panel.
    /// </summary>
    public partial class WarRoomPanel : ScopedPanelBase<WarRoomViewModel>, ICompletablePanel
    {
        // Fields declared here and used by InitializeComponent in Designer.cs
        private LegacyGradientPanel? _topPanel;
        private PanelHeader? _panelHeader;
        private TextBox? _scenarioInput;
        private SfButton? _btnRunScenario;
        private SfButton? _btnExportForecast;
        private Panel? _contentPanel;
        private Panel? _resultsPanel;

        public WarRoomPanel(IServiceScopeFactory scopeFactory, ILogger<ScopedPanelBase<WarRoomViewModel>> logger)
            : base(scopeFactory, logger)
        {
            InitializeComponent();
            Logger.LogDebug("[WarRoomPanel] Initialized");
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
    }
}
