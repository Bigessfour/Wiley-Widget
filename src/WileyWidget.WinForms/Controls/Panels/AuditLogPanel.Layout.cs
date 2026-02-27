#nullable enable

using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.Controls.Panels
{
    public partial class AuditLogPanel
    {
        // Builds the programmatic layout: root TableLayoutPanel with 3 rows
        private void BuildProgrammaticLayout()
        {
            // Root table layout
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8),
                Margin = new Padding(0),
                AutoSize = false
            };

            // Row styles: Header (Auto), Filters (Auto), Content (*)
            root.RowStyles.Clear();
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Row 1: Header + toolbar
            _header ??= new PanelHeader { Dock = DockStyle.Fill, Name = "AuditPanelHeader" };

            // Toolbar container on the right side
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _btnRefresh ??= ControlFactory.CreateSfButton("Refresh", button =>
            {
                button.AutoSize = true;
                button.Name = "BtnRefresh";
            });
            _btnExportCsv ??= ControlFactory.CreateSfButton("Export CSV", button =>
            {
                button.AutoSize = true;
                button.Name = "BtnExportCsv";
            });
            _btnUpdateChart ??= ControlFactory.CreateSfButton("Update Chart", button =>
            {
                button.AutoSize = true;
                button.Name = "BtnUpdateChart";
            });
            _chkAutoRefresh ??= new CheckBoxAdv { Text = "Auto-refresh", AutoSize = true, Name = "ChkAutoRefresh" };

            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnExportCsv);
            toolbar.Controls.Add(_btnUpdateChart);
            toolbar.Controls.Add(_chkAutoRefresh);

            // Header row: header on left, toolbar on right inside a panel
            var headerHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            headerHost.Controls.Add(_header);
            headerHost.Controls.Add(toolbar);

            root.Controls.Add(headerHost, 0, 0);

            // Row 2: Filters in a TableLayoutPanel
            var filters = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(4)
            };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _dtpStartDate ??= new Syncfusion.WinForms.Input.SfDateTimeEdit { DateTimePattern = Syncfusion.WinForms.Input.Enums.DateTimePattern.ShortDate, Name = "DtpStart" };
            _dtpEndDate ??= new Syncfusion.WinForms.Input.SfDateTimeEdit { DateTimePattern = Syncfusion.WinForms.Input.Enums.DateTimePattern.ShortDate, Name = "DtpEnd" };
            _cmbActionType ??= new Syncfusion.WinForms.ListView.SfComboBox { DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, Name = "CmbActionType" };
            _cmbUser ??= new Syncfusion.WinForms.ListView.SfComboBox { DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, Name = "CmbUser" };
            _cmbChartGroupBy ??= new Syncfusion.WinForms.ListView.SfComboBox { DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList, Name = "CmbChartGroupBy" };

            filters.Controls.Add(_dtpStartDate, 0, 0);
            filters.Controls.Add(_dtpEndDate, 1, 0);
            filters.Controls.Add(_cmbActionType, 2, 0);
            filters.Controls.Add(_cmbUser, 3, 0);
            filters.Controls.Add(_cmbChartGroupBy, 4, 0);

            root.Controls.Add(filters, 0, 1);

            // Row 3: SplitContainer with grid and chart
            _mainSplit ??= new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6 };

            // Left: audit grid
            _auditGrid ??= ControlFactory.CreateSfDataGrid(grid =>
            {
                grid.Dock = DockStyle.Fill;
                grid.Name = "AuditGrid";
            });
            _mainSplit.Panel1.Controls.Add(_auditGrid);

            // Right: chart host panel
            _chartHostPanel ??= new Panel { Dock = DockStyle.Fill, Name = "ChartHost" };
            _mainSplit.Panel2.Controls.Add(_chartHostPanel);

            // Configure safe splitter distance
            SafeSplitterDistanceHelper.ConfigureSafeSplitContainerAdvanced(_mainSplit, 300, 300, desiredDistance: 520);

            root.Controls.Add(_mainSplit, 0, 2);

            // Overlays (floating) - created but hidden
            _loader ??= new LoadingOverlay { Dock = DockStyle.Fill, Visible = false, Name = "AuditLoadingOverlay" };
            _noDataOverlay ??= new NoDataOverlay { Dock = DockStyle.Fill, Visible = false, Name = "AuditNoDataOverlay" };
            _chartLoadingOverlay ??= new LoadingOverlay { Dock = DockStyle.Fill, Visible = false, Name = "ChartLoadingOverlay" };

            // Add root and overlays to the control
            Controls.Add(root);
            Controls.Add(_loader);
            Controls.Add(_noDataOverlay);
            Controls.Add(_chartLoadingOverlay);

            // Bring overlays to front
            _loader.BringToFront();
            _noDataOverlay.BringToFront();
            _chartLoadingOverlay.BringToFront();

            // Apply SfSkinManager theme cascade
            try
            {
                SfSkinManager.SetVisualStyle(this, SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme);
            }
            catch { }
        }
    }
}
