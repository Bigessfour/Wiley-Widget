using Syncfusion.WinForms.Core;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.Windows.Forms.Gauge;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    partial class DashboardPanel
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
            
            // Panel properties
            this.Name = "DashboardPanel";
            this.AccessibleName = "Dashboard";
            this.Size = new System.Drawing.Size(1200, 800);
            this.MinimumSize = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            this.AutoScroll = true;
            this.Padding = new System.Windows.Forms.Padding(8);
            this.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AccessibleDescription = "Dashboard overview with KPIs, metrics, and financial summaries";
        }

        #endregion
    }
}


