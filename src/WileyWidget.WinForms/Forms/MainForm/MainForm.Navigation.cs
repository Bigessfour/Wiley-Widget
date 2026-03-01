using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Panels;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms
{
    public partial class MainForm
    {
        public IPanelNavigationService? PanelNavigator => _panelNavigator;

        private IPanelNavigationService? _panelNavigator;

        private void EnsurePanelNavigatorInitialized()
        {
            if (_panelNavigator != null) return;

            _logger?.LogDebug("[NAV] Creating PanelNavigationService");

            var navLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetService<ILogger<PanelNavigationService>>(_serviceProvider!);

            _panelNavigator = new PanelNavigationService(
                this,
                _serviceProvider!,
                navLogger ?? NullLogger<PanelNavigationService>.Instance);
        }

        public void ShowPanel<TPanel>(string panelName, DockingStyle style = DockingStyle.Right, bool allowFloating = true)
            where TPanel : UserControl
        {
            EnsurePanelNavigatorInitialized();
            _panelNavigator?.ShowPanel<TPanel>(panelName, style, allowFloating);
        }

        public void ShowForm<TForm>(string panelName, DockingStyle style = DockingStyle.Right, bool allowFloating = true)
            where TForm : Form
        {
            EnsurePanelNavigatorInitialized();
            _panelNavigator?.ShowForm<TForm>(panelName, style, allowFloating);
        }

        public void ClosePanel(string panelName)
        {
            _panelNavigator?.HidePanel(panelName);
        }

        /// <summary>
        /// Non-generic panel show for runtime-type-based navigation (e.g. layout restore, global search).
        /// </summary>
        public bool ShowPanel(Type panelType, string panelName, DockingStyle style = DockingStyle.Right, bool allowFloating = true)
        {
            EnsurePanelNavigatorInitialized();
            try
            {
                if (panelType == null || !typeof(UserControl).IsAssignableFrom(panelType))
                {
                    _logger?.LogWarning("[NAV] ShowPanel(Type) rejected invalid panel type {PanelType}", panelType?.FullName ?? "<null>");
                    return false;
                }

                if (panelType == typeof(FormHostPanel))
                {
                    if (string.Equals(panelName, "Rates", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowForm<RatesPage>(panelName, style, allowFloating: true);
                        return true;
                    }
                }

                if (_panelNavigator == null)
                {
                    _logger?.LogWarning("[NAV] ShowPanel(Type) failed because panel navigator is unavailable");
                    return false;
                }

                _panelNavigator.ShowPanel(panelType, panelName, style, allowFloating);

                var activePanelName = _panelNavigator.GetActivePanelName();
                if (string.IsNullOrWhiteSpace(activePanelName))
                {
                    _logger?.LogDebug("[NAV] ShowPanel(Type) completed but active panel is null for {PanelType}", panelType.Name);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[NAV] ShowPanel(Type) failed for {PanelType}", panelType?.Name);
                return false;
            }
        }

        /// <summary>Hides the Settings panel (called by SettingsPanel close button).</summary>
        public void CloseSettingsPanel() => ClosePanel("Settings");
    }
}
