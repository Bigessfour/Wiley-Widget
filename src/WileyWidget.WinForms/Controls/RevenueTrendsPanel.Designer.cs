using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Drawing;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    partial class RevenueTrendsPanel
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
                this.Name = "RevenueTrendsPanel";
                this.Dock = System.Windows.Forms.DockStyle.Fill;
                this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
                this.Size = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1400f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(900f));
                this.MinimumSize = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(1000f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
                this.Padding = new System.Windows.Forms.Padding(standardPadding);
                this.AccessibleName = "Revenue Trends";
                this.AccessibleDescription = "Revenue analysis and trend visualization with forecasting and detailed metrics";

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

                // Row 1: Summary metrics
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(80f)));

                // Row 2: Chart + Grid (split)
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
                    Title = "Revenue Trends Analysis",
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f),
                    AccessibleName = "Panel header",
                    AccessibleDescription = "Revenue trends analysis with historical data and forecasting"
                };
                mainLayout.Controls.Add(panelHeader, 0, 0);
                SfSkinManager.SetVisualStyle(panelHeader, ThemeColors.DefaultTheme);

                // Summary metrics panel
                var summaryPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Revenue metrics summary",
                    AccessibleDescription = "Key revenue metrics: current, YTD, forecast, and growth rate"
                };

                var summaryTable = new System.Windows.Forms.TableLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    ColumnCount = 4,
                    RowCount = 1,
                    CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single,
                    Padding = System.Windows.Forms.Padding.Empty
                };

                for (int i = 0; i < 4; i++)
                    summaryTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                        System.Windows.Forms.SizeType.Percent, 25f));

                var createMetricLabel = new System.Func<string, System.Windows.Forms.Label>((text) =>
                {
                    return new System.Windows.Forms.Label
                    {
                        Text = text,
                        Font = new System.Drawing.Font("Segoe UI",
                            Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(10f),
                            System.Drawing.FontStyle.Bold),
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                        AutoSize = false
                    };
                });

                summaryTable.Controls.Add(createMetricLabel("Current Revenue: $0"), 0, 0);
                summaryTable.Controls.Add(createMetricLabel("YTD Revenue: $0"), 1, 0);
                summaryTable.Controls.Add(createMetricLabel("Forecast: $0"), 2, 0);
                summaryTable.Controls.Add(createMetricLabel("Growth Rate: 0%"), 3, 0);

                summaryPanel.Controls.Add(summaryTable);
                mainLayout.Controls.Add(summaryPanel, 0, 1);
                SfSkinManager.SetVisualStyle(summaryPanel, ThemeColors.DefaultTheme);

                // Chart + Grid split container
                var contentSplit = new System.Windows.Forms.SplitContainer
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Orientation = System.Windows.Forms.Orientation.Horizontal,
                    TabStop = false,
                    SplitterWidth = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(4f)
                };

                // Top: Trend chart
                var chartPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Revenue trend chart",
                    AccessibleDescription = "Line chart showing revenue trends over time with forecast projection"
                };

                var chart = new System.Windows.Forms.Label
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Text = "📊 Revenue Trend Chart\r\n\r\nChart showing historical revenue data and forecast trend will be displayed here.",
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    Font = new System.Drawing.Font("Segoe UI",
                        Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(9f)),
                    AccessibleName = "Revenue trend chart",
                    AccessibleDescription = "Visual representation of revenue trends and forecast"
                };

                chartPanel.Controls.Add(chart);
                contentSplit.Panel1.Controls.Add(chartPanel);
                SfSkinManager.SetVisualStyle(chartPanel, ThemeColors.DefaultTheme);

                // Bottom: Detailed metrics grid
                var gridPanel = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding(controlSpacing),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Revenue metrics grid",
                    AccessibleDescription = "Detailed revenue metrics by period with variance analysis"
                };

                var metricsGrid = new SfDataGrid
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
                    GridLinesVisibility = GridLinesVisibility.Horizontal,
                    RowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(28f),
                    HeaderRowHeight = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(35f),
                    TabIndex = 1,
                    AccessibleName = "Revenue metrics grid",
                    AccessibleDescription = "Table showing detailed revenue metrics by period with variance"
                };

                // Grid columns
                metricsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "Period",
                    HeaderText = "Period",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f),
                    AllowSorting = true
                });

                metricsGrid.Columns.Add(new GridNumericColumn
                {
                    MappingName = "ActualRevenue",
                    HeaderText = "Actual Revenue",
                    Format = "C2",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(130f),
                    AllowSorting = true
                });

                metricsGrid.Columns.Add(new GridNumericColumn
                {
                    MappingName = "ForecastedRevenue",
                    HeaderText = "Forecasted Revenue",
                    Format = "C2",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(140f),
                    AllowSorting = true
                });

                metricsGrid.Columns.Add(new GridNumericColumn
                {
                    MappingName = "Variance",
                    HeaderText = "Variance",
                    Format = "C2",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(100f),
                    AllowSorting = true
                });

                metricsGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = "Trend",
                    HeaderText = "Trend",
                    Width = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(80f),
                    AllowSorting = true
                });

                gridPanel.Controls.Add(metricsGrid);
                contentSplit.Panel2.Controls.Add(gridPanel);
                SfSkinManager.SetVisualStyle(metricsGrid, ThemeColors.DefaultTheme);

                mainLayout.Controls.Add(contentSplit, 0, 2);

                // Status bar
                var statusStrip = new System.Windows.Forms.StatusStrip
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(25f),
                    AccessibleName = "Status bar",
                    AccessibleDescription = "Shows records count and last update time"
                };

                var statusLabel = new System.Windows.Forms.ToolStripStatusLabel
                {
                    Text = "0 periods | Last updated: Never",
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

        private System.ComponentModel.IContainer components;
    }
}
