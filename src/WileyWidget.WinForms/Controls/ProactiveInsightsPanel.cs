#nullable enable

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Controls;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms.Tools;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Extensions;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Panel for displaying proactive AI insights with gradient header and data grid.
    /// Acts as a container for the InsightFeedPanel with toolbar and navigation controls.
    /// Integrates with SfSkinManager for theme cascading to all child controls.
    /// </summary>
    public partial class ProactiveInsightsPanel : UserControl
    {
        private readonly ILogger<ProactiveInsightsPanel>? _logger;
        private InsightFeedPanel? _insightFeedPanel;

        /// <summary>
        /// Creates a new instance of the ProactiveInsightsPanel.
        /// </summary>
        public ProactiveInsightsPanel() : this(ResolveLogger())
        {
        }

        /// <summary>
        /// Creates a new instance with explicit logger.
        /// </summary>
        public ProactiveInsightsPanel(ILogger<ProactiveInsightsPanel>? logger = null)
        {
            InitializeComponent();

            _logger = logger ?? ResolveLogger();
            _logger?.LogInformation("ProactiveInsightsPanel initializing");

            InitializeUI();
            ApplyTheme();

            _logger?.LogInformation("ProactiveInsightsPanel initialized successfully");
        }

        /// <summary>
        /// Initializes the UI controls with gradient panel header and insights feed.
        /// </summary>
        private void InitializeUI()
        {
            // Create gradient top panel with header
            var topPanel = new GradientPanelExt
            {
                Height = 60,
                Dock = DockStyle.Top,
                Padding = new Padding(8)
            };
            Controls.Add(topPanel);

            // Panel header with title
            var panelHeader = new PanelHeader
            {
                Dock = DockStyle.Fill,
                Text = "Proactive AI Insights"
            };
            topPanel.Controls.Add(panelHeader);

            // Toolbar for actions
            var toolStrip = new ToolStrip
            {
                Height = 32,
                Dock = DockStyle.Right,
                AutoSize = false,
                GripStyle = ToolStripGripStyle.Hidden,
                Margin = new Padding(0),
                Padding = new Padding(4, 2, 4, 2)
            };

            var btnRefresh = new ToolStripButton("🔄 Refresh")
            {
                ToolTipText = "Refresh insights",
                AutoSize = true
            };
            toolStrip.Items.Add(btnRefresh);

            var btnClear = new ToolStripButton("🗑️ Clear")
            {
                ToolTipText = "Clear all insights",
                AutoSize = true
            };
            toolStrip.Items.Add(btnClear);

            topPanel.Controls.Add(toolStrip);

            // Insights feed panel (displays grid and status)
            _insightFeedPanel = new InsightFeedPanel
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(_insightFeedPanel);

            // Wire up toolbar actions
            btnRefresh.Click += (s, e) =>
            {
                try
                {
                    _logger?.LogInformation("[PROACTIVE_INSIGHTS] Refresh clicked");
                    // Trigger refresh via ViewModel if needed
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[PROACTIVE_INSIGHTS] Refresh action failed");
                    MessageBox.Show($"Failed to refresh insights: {ex.Message}", "Refresh Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            btnClear.Click += (s, e) =>
            {
                try
                {
                    _logger?.LogInformation("[PROACTIVE_INSIGHTS] Clear clicked");
                    MessageBox.Show("Clear insights action - placeholder", "Clear", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[PROACTIVE_INSIGHTS] Clear action failed");
                }
            };
        }

        /// <summary>
        /// Applies Office2019Colorful theme to the panel using SfSkinManager.
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                // Apply theme via SfSkinManager (authoritative theme source)
                SfSkinManager.SetVisualStyle(this, AppThemeColors.DefaultTheme);

                // Theme cascades to all child controls automatically
                _logger?.LogDebug("Theme applied successfully to ProactiveInsightsPanel");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply theme to ProactiveInsightsPanel");
            }
        }

        /// <summary>
        /// Resolves the logger from DI.
        /// </summary>
        private static ILogger<ProactiveInsightsPanel>? ResolveLogger()
        {
            if (Program.Services == null)
            {
                return null;
            }

            try
            {
                return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetService<ILogger<ProactiveInsightsPanel>>(Program.Services);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Initializes the designer component. Auto-generated code.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "ProactiveInsightsPanel";
            this.Size = new System.Drawing.Size(800, 600);
            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _insightFeedPanel?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
