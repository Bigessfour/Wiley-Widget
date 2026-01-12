using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    partial class WarRoomPanel
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
                this.Name = "WarRoomPanel";
                this.Dock = System.Windows.Forms.DockStyle.Fill;
                this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
                this.Size = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1400f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(900f));
                this.MinimumSize = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
                this.Padding = new System.Windows.Forms.Padding(standardPadding);
                this.AccessibleName = "War Room";
                this.AccessibleDescription = "Emergency response and scenario analysis dashboard with real-time metrics and situation overview";

                // Tooltip
                var toolTip = new System.Windows.Forms.ToolTip(this.components);
                toolTip.AutoPopDelay = 5000;
                toolTip.InitialDelay = 500;
                toolTip.ReshowDelay = 200;

                // Main layout table
                var mainLayout = new System.Windows.Forms.TableLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    RowCount = 4,
                    ColumnCount = 1,
                    AutoSize = false,
                    Padding = System.Windows.Forms.Padding.Empty
                };

                // Row 0: Panel Header
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f)));

                // Row 1: Status indicators
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(60f)));

                // Row 2: Main content (split: scenarios + status)
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Percent, 100f));

                // Row 3: Status bar
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f)));

                this.Controls.Add(mainLayout);

                // Panel header
                var panelHeader = new PanelHeader
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Title = "War Room - Emergency Response",
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f),
                    AccessibleName = "Panel header",
                    AccessibleDescription = "War Room dashboard for emergency scenario analysis and response"
                };
                mainLayout.Controls.Add(panelHeader, 0, 0);
                SfSkinManager.SetVisualStyle(panelHeader, ThemeColors.DefaultTheme);

                // Status indicators panel
                var statusPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Status indicators",
                    AccessibleDescription = "Current emergency status, active scenarios, and alert levels"
                };

                var statusFlow = new System.Windows.Forms.FlowLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoSize = true
                };

                // Status indicator labels
                var indicator1 = new System.Windows.Forms.Label
                {
                    Text = "🔴 Status: STANDBY",
                    Font = new System.Drawing.Font("Segoe UI",
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f),
                        System.Drawing.FontStyle.Bold),
                    AutoSize = true,
                    Margin = new System.Windows.Forms.Padding(controlSpacing)
                };

                var indicator2 = new System.Windows.Forms.Label
                {
                    Text = "📊 Active Scenarios: 0",
                    Font = new System.Drawing.Font("Segoe UI",
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f),
                        System.Drawing.FontStyle.Bold),
                    AutoSize = true,
                    Margin = new System.Windows.Forms.Padding(controlSpacing)
                };

                var indicator3 = new System.Windows.Forms.Label
                {
                    Text = "⚠️ Alerts: 0",
                    Font = new System.Drawing.Font("Segoe UI",
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f),
                        System.Drawing.FontStyle.Bold),
                    AutoSize = true,
                    Margin = new System.Windows.Forms.Padding(controlSpacing)
                };

                statusFlow.Controls.Add(indicator1);
                statusFlow.Controls.Add(indicator2);
                statusFlow.Controls.Add(indicator3);

                statusPanel.Controls.Add(statusFlow);
                mainLayout.Controls.Add(statusPanel, 0, 1);
                SfSkinManager.SetVisualStyle(statusPanel, ThemeColors.DefaultTheme);

                // Main content split container (scenarios + details)
                var contentSplit = new System.Windows.Forms.SplitContainer
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Orientation = System.Windows.Forms.Orientation.Horizontal,
                    TabStop = false,
                    SplitterWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(4f)
                };

                // Top: Scenarios grid
                var scenariosPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Scenarios grid",
                    AccessibleDescription = "Emergency response scenarios and analysis"
                };
                SfSkinManager.SetVisualStyle(scenariosPanel, ThemeColors.DefaultTheme);

                var scenariosGrid = new SfDataGrid
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    AutoGenerateColumns = false,
                    AllowEditing = false,
                    AllowFiltering = true,
                    AllowSorting = true,
                    AllowResizingColumns = true,
                    SelectionMode = GridSelectionMode.Single,
                    NavigationMode = NavigationMode.Row,
                    ShowRowHeader = true,
                    ShowBorder = true,
                    RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28f),
                    HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(35f),
                    TabIndex = 1,
                    AccessibleName = "Scenarios grid",
                    AccessibleDescription = "Table of emergency response scenarios"
                };

                scenariosGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "ScenarioName",
                    HeaderText = "Scenario",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(200f)
                });

                scenariosGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "Status",
                    HeaderText = "Status",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f)
                });

                scenariosGrid.Columns.Add(new GridNumericColumn
                {
                    MappingName = "ImpactLevel",
                    HeaderText = "Impact",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(80f)
                });

                scenariosPanel.Controls.Add(scenariosGrid);
                contentSplit.Panel1.Controls.Add(scenariosPanel);
                SfSkinManager.SetVisualStyle(scenariosGrid, ThemeColors.DefaultTheme);

                // Bottom: Situation overview / details
                var detailsPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Situation details",
                    AccessibleDescription = "Detailed situation overview and analysis metrics"
                };
                SfSkinManager.SetVisualStyle(detailsPanel, ThemeColors.DefaultTheme);

                var detailsText = new System.Windows.Forms.RichTextBox
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    ReadOnly = true,
                    BorderStyle = System.Windows.Forms.BorderStyle.None,
                    Text = "Situation Overview\r\n\r\nNo active scenarios. War Room is on standby.\r\n\r\n" +
                           "When an emergency is activated, situation details and analysis will appear here.",
                    Font = new System.Drawing.Font("Segoe UI",
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(9f)),
                    TabIndex = 2,
                    AccessibleName = "Situation details",
                    AccessibleDescription = "Detailed information about current emergency scenarios"
                };

                detailsPanel.Controls.Add(detailsText);
                contentSplit.Panel2.Controls.Add(detailsPanel);

                mainLayout.Controls.Add(contentSplit, 0, 2);

                // Status bar
                var statusStrip = new System.Windows.Forms.StatusStrip
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    AccessibleName = "Status bar",
                    AccessibleDescription = "Shows current operation status"
                };

                var statusLabel = new System.Windows.Forms.ToolStripStatusLabel
                {
                    Text = "War Room Ready",
                    Spring = true,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                statusStrip.Items.Add(statusLabel);
                mainLayout.Controls.Add(statusStrip, 0, 3);

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
    }
}


