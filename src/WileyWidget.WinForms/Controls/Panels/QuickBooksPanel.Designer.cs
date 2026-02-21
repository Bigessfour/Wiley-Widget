using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls.Supporting;

namespace WileyWidget.WinForms.Controls.Panels
{
    partial class QuickBooksPanel
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// This method mirrors the layout that would normally be produced
        /// by the Visual Studio designer but uses the project's existing
        /// field names (prefixed with `_`) so it is safe to add to the
        /// partial class without creating duplicate members.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new Container();

            // Instantiate controls (assign into fields declared in the other partial file)
            this._panelHeader = new PanelHeader();
            this._splitContainerMain = new Syncfusion.Windows.Forms.Tools.SplitContainerAdv();
            this._splitContainerTop = new Syncfusion.Windows.Forms.Tools.SplitContainerAdv();
            this._splitContainerBottom = new Syncfusion.Windows.Forms.Tools.SplitContainerAdv();

            this._connectionPanel = new Panel();
            this._operationsPanel = new Panel();
            this._summaryPanel = new Panel();
            this._historyPanel = new Panel();

            this._syncProgressBar = new Syncfusion.Windows.Forms.Tools.ProgressBarAdv();
            this._syncHistoryGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid();
            this._filterTextBox = new Syncfusion.Windows.Forms.Tools.TextBoxExt();

            this._loadingOverlay = new LoadingOverlay();
            this._noDataOverlay = new NoDataOverlay();

            this._statusStrip = new StatusStrip();
            this._statusLabel = new ToolStripStatusLabel();
            this._sharedTooltip = new ToolTip(this.components);

            // Panel header
            this._panelHeader.AccessibleDescription = "QuickBooks Integration Panel Header";
            this._panelHeader.AccessibleName = "QuickBooks Header";
            this._panelHeader.Dock = DockStyle.Top;
            this._panelHeader.Height = 52;
            this._panelHeader.Name = "panelHeader";
            this._panelHeader.Title = "QuickBooks Integration";

            // Main split container (Horizontal)
            this._splitContainerMain.Name = "splitContainerMain";
            this._splitContainerMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this._splitContainerMain.SplitterWidth = 6;
            this._splitContainerMain.BorderStyle = BorderStyle.FixedSingle;
            this._splitContainerMain.Dock = DockStyle.Fill;

            // Top split (connection | operations)
            this._splitContainerTop.Name = "splitContainerTop";
            this._splitContainerTop.Orientation = System.Windows.Forms.Orientation.Vertical;
            this._splitContainerTop.SplitterWidth = 6;
            this._splitContainerTop.Dock = DockStyle.Fill;

            // Bottom split (summary / history)
            this._splitContainerBottom.Name = "splitContainerBottom";
            this._splitContainerBottom.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this._splitContainerBottom.SplitterWidth = 6;
            this._splitContainerBottom.Dock = DockStyle.Fill;


            // Panels
            this._connectionPanel.Name = "connectionPanel";
            this._connectionPanel.Dock = DockStyle.Fill;
            this._connectionPanel.Padding = new Padding(12, 8, 12, 8);
            this._connectionPanel.BorderStyle = BorderStyle.FixedSingle;

            this._operationsPanel.Name = "operationsPanel";
            this._operationsPanel.Dock = DockStyle.Fill;
            this._operationsPanel.Padding = new Padding(12, 8, 12, 8);
            this._operationsPanel.BorderStyle = BorderStyle.FixedSingle;

            this._summaryPanel.Name = "summaryPanel";
            this._summaryPanel.Dock = DockStyle.Fill;
            this._summaryPanel.Padding = new Padding(12, 8, 12, 8);
            this._summaryPanel.BorderStyle = BorderStyle.FixedSingle;

            this._historyPanel.Name = "historyPanel";
            this._historyPanel.Dock = DockStyle.Fill;
            this._historyPanel.Padding = new Padding(12, 8, 12, 8);
            this._historyPanel.BorderStyle = BorderStyle.FixedSingle;

            // Progress bar (operations)
            ((ISupportInitialize)this._syncProgressBar).BeginInit();
            // Use the waiting gradient style consistent with code-behind
            this._syncProgressBar.ProgressStyle = Syncfusion.Windows.Forms.Tools.ProgressBarStyles.WaitingGradient;
            this._syncProgressBar.WaitingGradientWidth = 20;
            this._syncProgressBar.Size = new Size(440, 28);
            this._syncProgressBar.Visible = false;
            ((ISupportInitialize)this._syncProgressBar).EndInit();

            // Grid
            ((ISupportInitialize)this._syncHistoryGrid).BeginInit();
            this._syncHistoryGrid.AllowResizingColumns = true;
            this._syncHistoryGrid.AllowSorting = true;
            this._syncHistoryGrid.AutoGenerateColumns = false;
            this._syncHistoryGrid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.AllCellsWithLastColumnFill;
            this._syncHistoryGrid.RowHeight = 28;
            ((ISupportInitialize)this._syncHistoryGrid).EndInit();

            // Filter textbox
            ((ISupportInitialize)this._filterTextBox).BeginInit();
            this._filterTextBox.Size = new Size(220, 28);
            ((ISupportInitialize)this._filterTextBox).EndInit();

            // Status strip
            this._statusStrip.Items.AddRange(new ToolStripItem[] { this._statusLabel });
            this._statusLabel.Text = "Ready";

            // Build hierarchy: Top split -> connection / operations
            this._splitContainerTop.Panel1.Controls.Add(this._connectionPanel);
            this._splitContainerTop.Panel2.Controls.Add(this._operationsPanel);

            // Bottom split -> summary / history
            this._splitContainerBottom.Panel1.Controls.Add(this._summaryPanel);
            this._splitContainerBottom.Panel2.Controls.Add(this._historyPanel);

            // Main split -> top / bottom
            this._splitContainerMain.Panel1.Controls.Add(this._splitContainerTop);
            this._splitContainerMain.Panel2.Controls.Add(this._splitContainerBottom);

            // Add grid & filter into history panel (basic placement - concrete layout is handled in code-behind)
            this._historyPanel.Controls.Add(this._syncHistoryGrid);
            this._historyPanel.Controls.Add(this._filterTextBox);

            // Add progress into operations
            this._operationsPanel.Controls.Add(this._syncProgressBar);

            // Add top-level controls to user control
            this.Controls.Add(this._splitContainerMain);
            this.Controls.Add(this._panelHeader);
            this.Controls.Add(this._statusStrip);
            this.Controls.Add(this._loadingOverlay);
            this.Controls.Add(this._noDataOverlay);

            // Final control properties
            this.Name = "Panel_QuickBooks";
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.MinimumSize = new Size(720, 520);
            this.Padding = new Padding(4);

            // Perform any end-init style resume layout
            this._splitContainerMain.ResumeLayout(false);
            this._splitContainerTop.ResumeLayout(false);
            this._splitContainerBottom.ResumeLayout(false);
            this._historyPanel.ResumeLayout(false);
            this._operationsPanel.ResumeLayout(false);
            this._connectionPanel.ResumeLayout(false);
            this._summaryPanel.ResumeLayout(false);

        }

        #endregion
    }
}
