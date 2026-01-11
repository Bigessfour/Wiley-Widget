using Syncfusion.WinForms.Core;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls
{
    partial class SettingsPanel
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
            this.Name = "SettingsPanel";
            this.AccessibleName = "Settings";
            this.Size = new System.Drawing.Size(500, 400);
            this.MinimumSize = new System.Drawing.Size(
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(800f),
                (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f));
            this.AutoScroll = true;
            this.Padding = new System.Windows.Forms.Padding(8);
            this.AccessibleDescription = "Application settings and preferences";
        }

        #endregion

        // Field declarations
        private GradientPanelExt _mainPanel;
        private GradientPanelExt _themeGroup;
        private GradientPanelExt _aboutGroup;
        private GradientPanelExt _aiGroup;
        private Syncfusion.WinForms.ListView.SfComboBox _themeCombo;
        private Syncfusion.WinForms.ListView.SfComboBox _fontCombo;
        private Syncfusion.WinForms.ListView.SfComboBox _cmbLogLevel;
        private Syncfusion.WinForms.Input.SfNumericTextBox _numAutoSaveInterval;
        private Syncfusion.WinForms.Input.SfNumericTextBox _numXaiTimeout;
        private Syncfusion.WinForms.Input.SfNumericTextBox _numXaiMaxTokens;
        private Syncfusion.WinForms.Input.SfNumericTextBox _numXaiTemperature;
        private Syncfusion.WinForms.Controls.SfButton _btnClose;
        private Syncfusion.WinForms.Controls.SfButton _btnBrowseExportPath;
        private Syncfusion.WinForms.Controls.SfButton _btnShowApiKey;
        private TextBoxExt _txtAppTitle;
        private TextBoxExt _txtExportPath;
        private TextBoxExt _txtDateFormat;
        private TextBoxExt _txtCurrencyFormat;
        private TextBoxExt _txtXaiApiEndpoint;
        private TextBoxExt _txtXaiApiKey;
        private CheckBoxAdv _chkUseDemoData;
        private CheckBoxAdv _chkOpenEditFormsDocked;
        private CheckBoxAdv _chkEnableAi;
        private Syncfusion.WinForms.ListView.SfComboBox _cmbXaiModel;
        private System.Windows.Forms.Label _lblVersion;
        private System.Windows.Forms.Label _lblDbStatus;
        private System.Windows.Forms.Label _lblAiHelp;
        private System.Windows.Forms.LinkLabel _lnkAiLearnMore;
        private System.Windows.Forms.ErrorProvider _error_provider;
    }
}
