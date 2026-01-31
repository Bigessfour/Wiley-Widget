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

namespace WileyWidget.WinForms.Controls.Analytics
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
        private FlowLayoutPanel? _buttonContainer;
        private SfButton? _btnRefresh;
        private SfButton? _btnClear;
        private EventHandler? _btnRefreshClickHandler;
        private EventHandler? _btnClearClickHandler;

        /// <summary>
        /// Creates a new instance of the ProactiveInsightsPanel.
        /// </summary>
        internal ProactiveInsightsPanel() : this(ResolveLogger())
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

            this.PerformLayout();
            this.Refresh();

            _logger?.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);

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

            // Wire PanelHeader events
            _panelHeader.RefreshClicked += (s, e) => BtnRefresh_Click(s, e);
            _panelHeader.CloseClicked += (s, e) => ClosePanel();
            _panelHeader.HelpClicked += (s, e) => { MessageBox.Show("Proactive Insights Help: AI-generated insights for budget optimization.", "Help", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            _panelHeader.PinToggled += (s, e) => { /* Pin logic */ };

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

            // Button container for actions (using FlowLayoutPanel instead of ToolStrip for consistency)
            _buttonContainer = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(4, 2, 4, 2),
                Name = "ProactiveToolStrip",
                AccessibleName = "Proactive Actions Toolbar",
                AccessibleDescription = "Toolbar for proactive insights actions"
            };

            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;

            // Refresh button (using SfButton for Syncfusion consistency)
            _btnRefresh = new SfButton
            {
                Text = "🔄 Refresh",
                AutoSize = false,
                Size = new Size(100, 32),
                Name = "ProactiveRefresh",
                AccessibleName = "Refresh Insights Button",
                AccessibleDescription = "Click to refresh proactive insights",
                Margin = new Padding(4, 0, 4, 0),
                TabIndex = 1,
                TabStop = true
            };
            var refreshTooltip = new ToolTip();
            refreshTooltip.SetToolTip(_btnRefresh, "Refresh insights");
            SfSkinManager.SetVisualStyle(_btnRefresh, currentTheme);
            _btnRefresh.ThemeName = currentTheme;
            _buttonContainer.Controls.Add(_btnRefresh);

            // Clear button (using SfButton for Syncfusion consistency)
            _btnClear = new SfButton
            {
                Text = "🗑️ Clear",
                AutoSize = false,
                Size = new Size(85, 32),
                Name = "ProactiveClear",
                AccessibleName = "Clear Insights Button",
                AccessibleDescription = "Click to clear all proactive insights",
                Margin = new Padding(4, 0, 4, 0),
                TabIndex = 2,
                TabStop = true
            };
            var clearTooltip = new ToolTip();
            clearTooltip.SetToolTip(_btnClear, "Clear all insights");
            SfSkinManager.SetVisualStyle(_btnClear, currentTheme);
            _btnClear.ThemeName = currentTheme;
            _buttonContainer.Controls.Add(_btnClear);

            rightFlow.Controls.Add(_buttonContainer);

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
            _btnRefreshClickHandler = (s, e) => BtnRefresh_Click(s, e);
            _btnClearClickHandler = (s, e) => BtnClear_Click(s, e);
            _btnRefresh.Click += _btnRefreshClickHandler;
            _btnClear.Click += _btnClearClickHandler;
        }

        private void ClosePanel()
        {
            try
            {
                var form = FindForm();
                if (form is WileyWidget.WinForms.Forms.MainForm mainForm && mainForm.PanelNavigator != null)
                {
                    if (mainForm.PanelNavigator.HidePanel("Proactive Insights"))
                    {
                        return;
                    }
                    if (mainForm.PanelNavigator.HidePanel("Proactive AI Insights"))
                    {
                        return;
                    }
                }

                var dockingManagerField = form?.GetType()
                    .GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dockingManagerField?.GetValue(form) is Syncfusion.Windows.Forms.Tools.DockingManager dockingManager)
                {
                    dockingManager.SetDockVisibility(this, false);
                }
                else
                {
                    Visible = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to close ProactiveInsightsPanel via docking manager");
                Visible = false;
            }
        }

        /// <summary>
        /// Dispose implementation - clean up event handlers and child controls.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe event handlers
                if (_btnRefresh != null && _btnRefreshClickHandler != null)
                    _btnRefresh.Click -= _btnRefreshClickHandler;
                if (_btnClear != null && _btnClearClickHandler != null)
                    _btnClear.Click -= _btnClearClickHandler;

                // Dispose controls
                _btnRefresh?.SafeDispose();
                _btnClear?.SafeDispose();
                _buttonContainer?.SafeDispose();
                _panelHeader?.SafeDispose();
                _topPanel?.SafeDispose();
                _insightFeedPanel?.SafeDispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Applies current application theme to the panel using SfSkinManager.
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                // Apply current application theme via SfSkinManager (authoritative theme source)
                var currentTheme = SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(this, currentTheme);

                // Ensure theme cascades to key child containers as well
                if (_topPanel != null)
                {
                    SfSkinManager.SetVisualStyle(_topPanel, currentTheme);
                }

                if (_insightFeedPanel != null)
                {
                    SfSkinManager.SetVisualStyle(_insightFeedPanel, currentTheme);
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
    }
}
