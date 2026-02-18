using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Base;
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
        /// Uses reflection to invoke the generic ShowPanel&lt;TPanel&gt; overload.
        /// </summary>
        public void ShowPanel(Type panelType, string panelName, DockingStyle style = DockingStyle.Right, bool allowFloating = true)
        {
            EnsurePanelNavigatorInitialized();
            try
            {
                // Locate the generic ShowPanel<TPanel>(string, DockingStyle, bool) method by signature
                var method = typeof(IPanelNavigationService)
                    .GetMethod(nameof(IPanelNavigationService.ShowPanel),
                        new[] { typeof(string), typeof(DockingStyle), typeof(bool) });

                method?.MakeGenericMethod(panelType)
                       .Invoke(_panelNavigator, new object[] { panelName, style, allowFloating });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[NAV] ShowPanel(Type) failed for {PanelType}", panelType?.Name);
            }
        }

        /// <summary>Hides the Settings panel (called by SettingsPanel close button).</summary>
        public void CloseSettingsPanel() => ClosePanel("Settings");
    }
}
