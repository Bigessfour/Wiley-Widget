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
    partial class ChatPanel
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
                
                // Panel properties
                this.Name = "ChatPanel";
                this.Dock = System.Windows.Forms.DockStyle.Fill;
                this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
                this.Size = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
                this.MinimumSize = new System.Drawing.Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(400f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(400f));
                this.Padding = new System.Windows.Forms.Padding(0);
                this.AccessibleName = "AI Chat";
                this.AccessibleDescription = "Conversational AI chat panel powered by JARVIS Grok integration";

                // Main layout
                var mainLayout = new System.Windows.Forms.TableLayoutPanel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1,
                    AutoSize = false,
                    Padding = System.Windows.Forms.Padding.Empty
                };

                // Row 0: Header
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Absolute,
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f)));

                // Row 1: Chat content (Blazor)
                mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(
                    System.Windows.Forms.SizeType.Percent, 100f));

                // Panel header
                var panelHeader = new PanelHeader
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Title = "AI Chat",
                    Height = (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(50f),
                    AccessibleName = "Panel header",
                    AccessibleDescription = "AI Chat panel header with refresh and close actions"
                };
                mainLayout.Controls.Add(panelHeader, 0, 0);
                SfSkinManager.SetVisualStyle(panelHeader, ThemeColors.DefaultTheme);

                // Chat container (Blazor WebView hosted here)
                var chatContainer = new GradientPanelExt
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Padding = new System.Windows.Forms.Padding((int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(4f)),
                    BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                    BackgroundColor = new Syncfusion.Drawing.BrushInfo(
                        Syncfusion.Drawing.GradientStyle.Vertical,
                        System.Drawing.Color.Empty,
                        System.Drawing.Color.Empty),
                    AccessibleName = "Chat content area",
                    AccessibleDescription = "Blazor-based chat interface with JARVIS AI assistant"
                };
                mainLayout.Controls.Add(chatContainer, 0, 1);
                SfSkinManager.SetVisualStyle(chatContainer, ThemeColors.DefaultTheme);

                this.Controls.Add(mainLayout);

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
