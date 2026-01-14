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

        // Internal child controls (kept as fields for disposal and layout control)
        private GradientPanelExt? _topPanel;
        private PanelHeader? _panelHeader;
        private ToolStrip? _toolStrip;
        private ToolStripButton? _btnRefresh;
        private ToolStripButton? _btnClear;

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
            ApplyTheme();

            _logger?.LogInformation("ProactiveInsightsPanel initialized successfully");
        }

        /// <summary>
        /// Initializes the UI controls with gradient panel header and insights feed.
        /// </summary>
        private void InitializeComponent()
        {
            // Control-level padding and minimums for breathing room (requirement 4,5)
            this.Padding = new Padding(8);
            this.MinimumSize = new Size(320, 240);
            this.AccessibleName = "Proactive Insights Panel";
            this.AccessibleDescription = "Displays proactive AI insights with header and actions";

            // Create gradient top panel with header
            _topPanel = new GradientPanelExt
            {
                // Removed fixed Height to allow growth; MinimumSize ensures a reasonable min height
                MinimumSize = new Size(0, 60), // ensures header can't collapse below 60px
                Dock = DockStyle.Top,
                Padding = new Padding(12, 8, 12, 8),
                Name = "ProactiveTopPanel",
                AccessibleName = "Proactive Insights Header",
                AccessibleDescription = "Header area containing title and action toolbar",
                AccessibleRole = AccessibleRole.Pane
            };
            Controls.Add(_topPanel);

            // Header layout: title (fills) + right-aligned toolbar (auto-size)
            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                AutoSize = false
            };
            headerLayout.ColumnStyles.Clear();
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _topPanel.Controls.Add(headerLayout);

            // Panel header with title (fills the left column)
            _panelHeader = new PanelHeader
            {
                Dock = DockStyle.Fill,
                Text = "Proactive AI Insights",
                AccessibleName = "Proactive Insights Title",
                AccessibleDescription = "Title of the Proactive Insights panel"
            };
            headerLayout.Controls.Add(_panelHeader, 0, 0);

            // Right-side flow to keep toolbar items right-aligned and spaced
            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft, // right-align contents
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                AccessibleName = "Proactive Actions Flow",
                AccessibleDescription = "Container for right-aligned toolbar actions"
            };

            // Toolbar for actions
            _toolStrip = new ToolStrip
            {
                Height = 32,
                AutoSize = true,
                GripStyle = ToolStripGripStyle.Hidden,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
                Margin = new Padding(0),
                Padding = new Padding(4, 2, 4, 2),
                Name = "ProactiveToolStrip",
                AccessibleName = "Proactive Actions Toolbar",
                AccessibleDescription = "Toolbar for proactive insights actions"
            };

            // Refresh button
            _btnRefresh = new ToolStripButton("🔄 Refresh")
            {
                ToolTipText = "Refresh insights",
                Name = "ProactiveRefresh",
                AccessibleName = "Refresh Insights Button",
                AccessibleDescription = "Click to refresh proactive insights",
                Margin = new Padding(4, 0, 4, 0)
            };
            _toolStrip.Items.Add(_btnRefresh);

            // Clear button
            _btnClear = new ToolStripButton("🗑️ Clear")
            {
                ToolTipText = "Clear all insights",
                Name = "ProactiveClear",
                AccessibleName = "Clear Insights Button",
                AccessibleDescription = "Click to clear all proactive insights",
                Margin = new Padding(4, 0, 4, 0)
            };
            _toolStrip.Items.Add(_btnClear);

            rightFlow.Controls.Add(_toolStrip);

            headerLayout.Controls.Add(rightFlow, 1, 0);

            // Insights feed panel (displays grid and status)
            _insightFeedPanel = new InsightFeedPanel
            {
                Dock = DockStyle.Fill,
                Name = "InsightFeedPanel",
                AccessibleName = "Insight Feed",
                AccessibleDescription = "Displays the list of proactive insights and statuses"
            };
            Controls.Add(_insightFeedPanel);

            // Hook up toolbar actions to named handlers so we can unsubscribe later
            _btnRefresh.Click += BtnRefresh_Click;
            _btnClear.Click += BtnClear_Click;
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

                // Ensure theme cascades to key child containers as well
                if (_topPanel != null)
                {
                    SfSkinManager.SetVisualStyle(_topPanel, AppThemeColors.DefaultTheme);
                }

                if (_insightFeedPanel != null)
                {
                    SfSkinManager.SetVisualStyle(_insightFeedPanel, AppThemeColors.DefaultTheme);
                }

                _logger?.LogDebug("Theme applied successfully to ProactiveInsightsPanel");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply theme to ProactiveInsightsPanel");
            }
        }

        /// <summary>
        /// Handles clicks on the Refresh toolbar button.
        /// </summary>
        private void BtnRefresh_Click(object? sender, EventArgs e)
        {
            try
            {
                _logger?.LogInformation("[PROACTIVE_INSIGHTS] Refresh clicked");
                // Intentionally do not change public API; the child panel/ViewModel handles refresh mechanics
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PROACTIVE_INSIGHTS] Refresh action failed");
                MessageBox.Show($"Failed to refresh insights: {ex.Message}", "Refresh Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Handles clicks on the Clear toolbar button.
        /// </summary>
        private void BtnClear_Click(object? sender, EventArgs e)
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

        // InitializeComponent moved to ProactiveInsightsPanel.Designer.cs for designer support
        // Dispose moved to ProactiveInsightsPanel.Designer.cs for designer support
    }
}

