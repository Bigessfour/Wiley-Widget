using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.Drawing;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    partial class ProactiveInsightsPanel
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            
            this.SuspendLayout();
            
            try
            {
                var standardPadding = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(16f);
                var controlSpacing = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f);
                
                // Core panel properties
                this.Name = "ProactiveInsightsPanel";
                this.Dock = System.Windows.Forms.DockStyle.Fill;
                this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
                this.Size = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1400f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(900f));
                this.MinimumSize = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
                this.Padding = new System.Windows.Forms.Padding(standardPadding);
                this.AccessibleName = "Proactive Insights";
                this.AccessibleDescription = "AI-generated insights feed with actionable recommendations and trend analysis";

                // Tooltip
                var toolTip = new System.Windows.Forms.ToolTip(this.components);
                toolTip.AutoPopDelay = 5000;
                toolTip.InitialDelay = 500;
                toolTip.ReshowDelay = 200;

                // Main layout table
                var mainLayout = new System.Windows.Forms.TableLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    RowCount = 5,
                    ColumnCount = 1,
                    AutoSize = false,
                    Padding = System.Windows.Forms.Padding.Empty
                };

                // Row 0: Panel Header
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f)));

                // Row 1: Insight categories / filters
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(45f)));

                // Row 2: Insights feed (scrollable)
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Percent, 100f));

                // Row 3: Action buttons
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(45f)));

                // Row 4: Status bar
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f)));

                this.Controls.Add(mainLayout);

                // Panel header
                var panelHeader = new PanelHeader
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Title = "Proactive Insights",
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f),
                    AccessibleName = "Panel header",
                    AccessibleDescription = "Proactive Insights dashboard with AI-generated recommendations"
                };
                mainLayout.Controls.Add(panelHeader, 0, 0);
                SfSkinManager.SetVisualStyle(panelHeader, ThemeColors.DefaultTheme);

                // Filter / Category panel
                var filterPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Insight filters",
                    AccessibleDescription = "Filter insights by category and priority"
                };

                var filterFlow = new System.Windows.Forms.FlowLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoSize = true
                };

                // Category combo
                var categoryLabel = new System.Windows.Forms.Label
                {
                    Text = "Category:",
                    AutoSize = true,
                    Margin = new System.Windows.Forms.Padding(controlSpacing / 2, 10, 0, 0)
                };

                var categoryCombo = new SfComboBox
                {
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(150f),
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                    TabIndex = 1,
                    AccessibleName = "Insight category filter"
                };
                toolTip.SetToolTip(categoryCombo, "Filter insights by category");

                // Priority checkbox
                var priorityCheck = new CheckBoxAdv
                {
                    Text = "High Priority Only",
                    AutoSize = true,
                    Checked = false,
                    TabIndex = 2,
                    Margin = new System.Windows.Forms.Padding(controlSpacing, 6, 0, 0),
                    AccessibleName = "Show high priority only"
                };
                toolTip.SetToolTip(priorityCheck, "Show only high priority insights");

                // Sort order combo
                var sortLabel = new System.Windows.Forms.Label
                {
                    Text = "Sort By:",
                    AutoSize = true,
                    Margin = new System.Windows.Forms.Padding(controlSpacing, 10, 0, 0)
                };

                var sortCombo = new SfComboBox
                {
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(120f),
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                    TabIndex = 3,
                    AccessibleName = "Sort order"
                };
                toolTip.SetToolTip(sortCombo, "Sort insights by date, priority, or impact");

                filterFlow.Controls.Add(categoryLabel);
                filterFlow.Controls.Add(categoryCombo);
                filterFlow.Controls.Add(priorityCheck);
                filterFlow.Controls.Add(sortLabel);
                filterFlow.Controls.Add(sortCombo);

                filterPanel.Controls.Add(filterFlow);
                mainLayout.Controls.Add(filterPanel, 0, 1);
                SfSkinManager.SetVisualStyle(filterPanel, ThemeColors.DefaultTheme);

                // Insights feed panel
                var feedPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Insights feed",
                    AccessibleDescription = "Scrollable list of AI-generated insights and recommendations"
                };

                var feedContent = new System.Windows.Forms.RichTextBox
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    ReadOnly = true,
                    BorderStyle = System.Windows.Forms.BorderStyle.None,
                    Text = "💡 Insights Feed\r\n\r\nAI-generated actionable insights will appear here.\r\n\r\n" +
                           "Examples:\r\n" +
                           "• Budget Alert: Water utility costs increasing 15% YoY\r\n" +
                           "• Opportunity: Optimize streetlight maintenance schedule\r\n" +
                           "• Trend: Municipal revenue trend +5% this quarter",
                    Font = new System.Drawing.Font("Segoe UI",
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(9f)),
                    TabIndex = 4,
                    AccessibleName = "Insights feed",
                    AccessibleDescription = "List of proactive insights and recommendations"
                };

                feedPanel.Controls.Add(feedContent);
                mainLayout.Controls.Add(feedPanel, 0, 2);
                SfSkinManager.SetVisualStyle(feedPanel, ThemeColors.DefaultTheme);

                // Action buttons panel
                var actionPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Action buttons",
                    AccessibleDescription = "Actions for insights: acknowledge, dismiss, export"
                };

                var actionFlow = new System.Windows.Forms.FlowLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoSize = true
                };

                var buttonSize = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(32f));

                var acknowledgeBtn = new Syncfusion.WinForms.Controls.SfButton
                {
                    Text = "✓ &Acknowledge",
                    Size = buttonSize,
                    TabIndex = 5,
                    AutoSize = false,
                    Margin = new System.Windows.Forms.Padding(controlSpacing / 2, 0, 0, 0)
                };
                toolTip.SetToolTip(acknowledgeBtn, "Mark this insight as acknowledged");

                var dismissBtn = new Syncfusion.WinForms.Controls.SfButton
                {
                    Text = "✗ &Dismiss",
                    Size = buttonSize,
                    TabIndex = 6,
                    AutoSize = false,
                    Margin = new System.Windows.Forms.Padding(controlSpacing / 2, 0, 0, 0)
                };
                toolTip.SetToolTip(dismissBtn, "Dismiss this insight");

                var exportBtn = new Syncfusion.WinForms.Controls.SfButton
                {
                    Text = "⬇ E&xport",
                    Size = buttonSize,
                    TabIndex = 7,
                    AutoSize = false,
                    Margin = new System.Windows.Forms.Padding(controlSpacing / 2, 0, 0, 0)
                };
                toolTip.SetToolTip(exportBtn, "Export insights to file");

                actionFlow.Controls.Add(acknowledgeBtn);
                actionFlow.Controls.Add(dismissBtn);
                actionFlow.Controls.Add(exportBtn);

                actionPanel.Controls.Add(actionFlow);
                mainLayout.Controls.Add(actionPanel, 0, 3);
                SfSkinManager.SetVisualStyle(actionPanel, ThemeColors.DefaultTheme);

                // Status bar
                var statusStrip = new System.Windows.Forms.StatusStrip
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    AccessibleName = "Status bar",
                    AccessibleDescription = "Shows insights count and last update time"
                };

                var statusLabel = new System.Windows.Forms.ToolStripStatusLabel
                {
                    Text = "0 insights | Last updated: Never",
                    Spring = true,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                statusStrip.Items.Add(statusLabel);
                mainLayout.Controls.Add(statusStrip, 0, 4);

                // Apply theme to panel
                Syncfusion.WinForms.Core.SfSkinManager.SetVisualStyle(this, ThemeColors.DefaultTheme);
            }
            finally
            {
                this.ResumeLayout(false);
                this.PerformLayout();
            }
        }

        #endregion

        private System.ComponentModel.IContainer components;
    }
}
