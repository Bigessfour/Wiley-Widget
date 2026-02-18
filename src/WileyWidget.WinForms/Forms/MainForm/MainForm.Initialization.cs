using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Panels;

namespace WileyWidget.WinForms.Forms
{
    public partial class MainForm
    {
        private int _onShownExecuted = 0;

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (DesignMode || Interlocked.Exchange(ref _onShownExecuted, 1) == 1)
                return;

            _logger?.LogInformation("[ONSHOWN] Starting final initialization");

            EnsurePanelNavigatorInitialized();

            // Auto-open the panels this branch is focused on
            try
            {
                _panelNavigator?.ShowPanel<WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true);
                _panelNavigator?.ShowPanel<CustomersPanel>("Customers", DockingStyle.Left, allowFloating: true);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to auto-open WarRoom or Customers panel");
            }

            ApplyStatus("Ready");
            _logger?.LogInformation("[ONSHOWN] Initialization complete");
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            // Called by StartupOrchestrator if needed
            await Task.CompletedTask;
        }
    }
}