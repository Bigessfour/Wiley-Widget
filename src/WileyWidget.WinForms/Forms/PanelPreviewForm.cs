using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;
using Serilog;
using Microsoft.Extensions.Configuration;
using WileyWidget.Services;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Themes;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Standalone preview form for testing individual panels in isolation.
    /// Useful for rapid UI iteration without launching the full application.
    /// </summary>
    public partial class PanelPreviewForm : Form
    {
        private UserControl? _currentPanel;

        public PanelPreviewForm()
        {
            InitializeComponent();

            this.Text = "Panel Preview - WarRoomPanel";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized; // Fullscreen for complete panel view
            this.StartPosition = FormStartPosition.CenterScreen;

            // Apply theme for consistent visual styling
            SfSkinManager.SetVisualStyle(this, "Office2019Colorful");

            // Create panel instance using DI services
            var services = Program.Services;
            if (services != null)
            {
                // WarRoomPanel is registered as scoped, but we need to construct it manually
                // since ScopedPanelBase manages its own scope internally
                using (var scope = services.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(scopedServices);
                    var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WileyWidget.WinForms.Controls.ScopedPanelBase<WileyWidget.WinForms.ViewModels.WarRoomViewModel>>>(scopedServices);
                    _currentPanel = new WarRoomPanel(scopeFactory, logger);
                }
            }
            else
            {
                // Fallback: create without DI (may not work for complex panels)
                // This would require a parameterless constructor, which most panels don't have
                throw new InvalidOperationException("Services not available. Run the full application first or add DI support.");
            }

            // Panel is now constructed
            _currentPanel.Dock = DockStyle.Fill;
            this.Controls.Add(_currentPanel);

            // When previewing, auto-run a safe demo scenario so charts/grids render
            // This uses reflection to access the resolved ViewModel and invoke its RunScenarioCommand
            this.Shown += async (s, e) =>
            {
                try
                {
                    var vmProp = _currentPanel.GetType().GetProperty("ViewModel");
                    var vm = vmProp?.GetValue(_currentPanel);
                    if (vm != null)
                    {
                        var runCmdProp = vm.GetType().GetProperty("RunScenarioCommand");
                        var runCmd = runCmdProp?.GetValue(vm);
                        if (runCmd != null)
                        {
                            // Try ExecuteAsync(CancellationToken) first
                            var executeAsync = runCmd.GetType().GetMethod("ExecuteAsync", new[] { typeof(System.Threading.CancellationToken) });
                            System.Threading.Tasks.Task? execTask = null;
                            if (executeAsync != null)
                            {
                                execTask = executeAsync.Invoke(runCmd, new object[] { System.Threading.CancellationToken.None }) as System.Threading.Tasks.Task;
                            }
                            else
                            {
                                // Fallback to parameterless Execute if present
                                var exec = runCmd.GetType().GetMethod("Execute", Type.EmptyTypes);
                                exec?.Invoke(runCmd, null);
                            }

                            if (execTask != null)
                            {
                                await execTask.ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Preview auto-run failed: {ex.Message}");
                }
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _currentPanel?.Dispose();
            base.OnFormClosing(e);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // PanelPreviewForm
            //
            this.ClientSize = new Size(1400, 900);
            this.Name = "PanelPreviewForm";
            this.Text = "Panel Preview";
            this.ResumeLayout(false);
        }
    }
}
