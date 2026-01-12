using Syncfusion.WinForms.Core;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Controls
{
    partial class AccountEditPanel
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

            // Initialize ToolTip first
            _toolTip = new System.Windows.Forms.ToolTip(this.components);
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 500;
            _toolTip.ReshowDelay = 200;
            _toolTip.ShowAlways = true;

            var padding = 16;
            var labelWidth = 140;
            var controlWidth = 320;
            var rowHeight = 40;
            var y = padding;

            // Title label
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblTitle.Text = string.Empty;
            this.lblTitle.Location = new System.Drawing.Point(padding, y);
            this.lblTitle.AutoSize = false;
            this.lblTitle.Width = labelWidth + controlWidth;
            this.lblTitle.Height = 30;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.Controls.Add(this.lblTitle);
            y += 40;

            // Account Number
            this.lblAccountNumber = new System.Windows.Forms.Label();
            this.lblAccountNumber.Text = "Account Number:";
            this.lblAccountNumber.Location = new System.Drawing.Point(padding, y + 6);
            this.lblAccountNumber.Width = labelWidth;
            this.lblAccountNumber.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(this.lblAccountNumber);

            this.txtAccountNumber = new Syncfusion.Windows.Forms.Tools.TextBoxExt();
            this.txtAccountNumber.Name = "txtAccountNumber";
            this.txtAccountNumber.Location = new System.Drawing.Point(padding + labelWidth + 10, y);
            this.txtAccountNumber.Width = controlWidth;
            this.txtAccountNumber.MaxLength = 20;
            this.txtAccountNumber.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtAccountNumber.AccessibleName = "Account Number";
            this.txtAccountNumber.AccessibleDescription = "Enter the unique account number";
            this.txtAccountNumber.TabIndex = 1;
            this.txtAccountNumber.Enabled = true;
            this.Controls.Add(this.txtAccountNumber);
            _toolTip.SetToolTip(this.txtAccountNumber, "Unique identifier for this account (e.g., 1000, 2100)");
            y += rowHeight;

            // Name
            this.lblName = new System.Windows.Forms.Label();
            this.lblName.Text = "Account Name:";
            this.lblName.Location = new System.Drawing.Point(padding, y + 6);
            this.lblName.Width = labelWidth;
            this.lblName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(this.lblName);

            this.txtName = new Syncfusion.Windows.Forms.Tools.TextBoxExt();
            this.txtName.Name = "txtName";
            this.txtName.Location = new System.Drawing.Point(padding + labelWidth + 10, y);
            this.txtName.Width = controlWidth;
            this.txtName.MaxLength = 100;
            this.txtName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtName.AccessibleName = "Account Name";
            this.txtName.AccessibleDescription = "Enter the descriptive name for this account";
            this.txtName.TabIndex = 2;
            this.Controls.Add(this.txtName);
            _toolTip.SetToolTip(this.txtName, "Descriptive name (e.g., 'Cash - General Fund')");
            y += rowHeight;

            // Description
            this.lblDescription = new System.Windows.Forms.Label();
            this.lblDescription.Text = "Description:";
            this.lblDescription.Location = new System.Drawing.Point(padding, y + 6);
            this.lblDescription.Width = labelWidth;
            this.lblDescription.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(this.lblDescription);

            this.txtDescription = new Syncfusion.Windows.Forms.Tools.TextBoxExt();
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Location = new System.Drawing.Point(padding + labelWidth + 10, y);
            this.txtDescription.Width = controlWidth;
            this.txtDescription.Height = 60;
            this.txtDescription.MaxLength = 500;
            this.txtDescription.Multiline = true;
            this.txtDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDescription.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtDescription.AccessibleName = "Description";
            this.txtDescription.AccessibleDescription = "Enter optional description";
            this.txtDescription.TabIndex = 3;
            this.Controls.Add(this.txtDescription);
            _toolTip.SetToolTip(this.txtDescription, "Optional detailed description");
            y += 70;

            // Department
            this.lblDepartment = new System.Windows.Forms.Label();
            this.lblDepartment.Text = "Department:";
            this.lblDepartment.Location = new System.Drawing.Point(padding, y + 6);
            this.lblDepartment.Width = labelWidth;
            this.lblDepartment.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(this.lblDepartment);

            this.cmbDepartment = new Syncfusion.WinForms.ListView.SfComboBox();
            this.cmbDepartment.Name = "cmbDepartment";
            this.cmbDepartment.Location = new System.Drawing.Point(padding + labelWidth + 10, y);
            this.cmbDepartment.Width = controlWidth;
            this.cmbDepartment.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            this.cmbDepartment.AccessibleName = "Department";
            this.cmbDepartment.AccessibleDescription = "Select the department this account belongs to";
            this.cmbDepartment.TabIndex = 4;
            this.Controls.Add(this.cmbDepartment);
            _toolTip.SetToolTip(this.cmbDepartment, "Select owning department");
            y += rowHeight;

            // Fund
            this.lblFund = new System.Windows.Forms.Label();
            this.lblFund.Text = "Fund:";
            this.lblFund.Location = new System.Drawing.Point(padding, y + 6);
            this.lblFund.Width = labelWidth;
            this.lblFund.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(this.lblFund);

            this.cmbFund = new Syncfusion.WinForms.ListView.SfComboBox();
            this.cmbFund.Name = "cmbFund";
            this.cmbFund.Location = new System.Drawing.Point(padding + labelWidth + 10, y);
            this.cmbFund.Width = controlWidth;
            this.cmbFund.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            this.cmbFund.AccessibleName = "Fund Type";
            this.cmbFund.AccessibleDescription = "Select the municipal fund type for this account";
            this.cmbFund.TabIndex = 5;
            this.Controls.Add(this.cmbFund);
            _toolTip.SetToolTip(this.cmbFund, "Select fund type (General, Enterprise, etc.)");
            y += rowHeight;

            // Type
            this.lblType = new System.Windows.Forms.Label();
            this.lblType.Text = "Type:";
            this.lblType.Location = new System.Drawing.Point(padding, y + 6);
            this.lblType.Width = labelWidth;
            this.lblType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(this.lblType);

            this.cmbType = new Syncfusion.WinForms.ListView.SfComboBox();
            this.cmbType.Name = "cmbType";
            this.cmbType.Location = new System.Drawing.Point(padding + labelWidth + 10, y);
            this.cmbType.Width = controlWidth;
            this.cmbType.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
            this.cmbType.AccessibleName = "Account Type";
            this.cmbType.AccessibleDescription = "Select the account type";
            this.cmbType.TabIndex = 6;
            this.Controls.Add(this.cmbType);
            _toolTip.SetToolTip(this.cmbType, "Select account type (Asset, Liability, Revenue, Expense)");
            y += rowHeight;

            // Balance
            this.lblBalance = new System.Windows.Forms.Label();
            this.lblBalance.Text = "Current Balance:";
            this.lblBalance.Location = new System.Drawing.Point(padding, y + 6);
            this.lblBalance.Width = labelWidth;
            this.lblBalance.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(this.lblBalance);

            this.numBalance = new Syncfusion.WinForms.Input.SfNumericTextBox();
            this.numBalance.Name = "numBalance";
            this.numBalance.Location = new System.Drawing.Point(padding + labelWidth + 10, y);
            this.numBalance.Width = controlWidth;
            this.numBalance.AllowNull = false;
            this.numBalance.MinValue = (double)decimal.MinValue;
            this.numBalance.MaxValue = (double)decimal.MaxValue;
            this.numBalance.AccessibleName = "Balance";
            this.numBalance.AccessibleDescription = "Enter the current account balance";
            this.numBalance.TabIndex = 7;
            this.numBalance.FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency;
            this.Controls.Add(this.numBalance);
            _toolTip.SetToolTip(this.numBalance, "Current account balance");
            y += rowHeight;

            // Budget
            this.lblBudget = new System.Windows.Forms.Label();
            this.lblBudget.Text = "Budget Amount:";
            this.lblBudget.Location = new System.Drawing.Point(padding, y + 6);
            this.lblBudget.Width = labelWidth;
            this.lblBudget.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.Controls.Add(this.lblBudget);

            this.numBudget = new Syncfusion.WinForms.Input.SfNumericTextBox();
            this.numBudget.Name = "numBudget";
            this.numBudget.Location = new System.Drawing.Point(padding + labelWidth + 10, y);
            this.numBudget.Width = controlWidth;
            this.numBudget.AllowNull = false;
            this.numBudget.MinValue = 0;
            this.numBudget.MaxValue = (double)decimal.MaxValue;
            this.numBudget.AccessibleName = "Budget Amount";
            this.numBudget.AccessibleDescription = "Enter the budgeted amount for this account";
            this.numBudget.TabIndex = 8;
            this.numBudget.FormatMode = Syncfusion.WinForms.Input.Enums.FormatMode.Currency;
            this.Controls.Add(this.numBudget);
            _toolTip.SetToolTip(this.numBudget, "Budgeted amount for this account");
            y += rowHeight;

            // Active checkbox
            this.chkActive = new Syncfusion.Windows.Forms.Tools.CheckBoxAdv();
            this.chkActive.Name = "chkActive";
            this.chkActive.Text = "Active";
            this.chkActive.Location = new System.Drawing.Point(padding + labelWidth + 10, y);
            this.chkActive.AutoSize = true;
            this.chkActive.Checked = true;
            this.chkActive.AccessibleName = "Active Status";
            this.chkActive.AccessibleDescription = "Check to mark this account as active";
            this.chkActive.TabIndex = 9;
            this.Controls.Add(this.chkActive);
            _toolTip.SetToolTip(this.chkActive, "Indicates whether this account is currently active");
            y += rowHeight + 10;

            // Button panel
            this.buttonPanel = new WileyWidget.WinForms.Controls.GradientPanelExt();
            this.buttonPanel.Location = new System.Drawing.Point(padding, y);
            this.buttonPanel.Width = labelWidth + controlWidth + 10;
            this.buttonPanel.Height = 40;
            this.buttonPanel.Dock = System.Windows.Forms.DockStyle.None;
            this.buttonPanel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.buttonPanel.BackgroundColor = new Syncfusion.Drawing.BrushInfo(Syncfusion.Drawing.GradientStyle.Vertical, System.Drawing.Color.Empty, System.Drawing.Color.Empty);
            this.buttonPanel.AccessibleName = "Account action buttons";
            Syncfusion.WinForms.Theme.SfSkinManager.SetVisualStyle(this.buttonPanel, ThemeColors.DefaultTheme);

            this.btnSave = new Syncfusion.WinForms.Controls.SfButton();
            this.btnSave.Name = "btnSave";
            this.btnSave.Text = "&Create";
            this.btnSave.AutoSize = true;
            this.btnSave.Location = new System.Drawing.Point(labelWidth + controlWidth - 210, 4);
            this.btnSave.AccessibleName = "Create Account";
            this.btnSave.AccessibleDescription = "Save the account changes";
            this.btnSave.TabIndex = 10;
            this.btnSave.Click += new System.EventHandler(this.BtnSave_Click);
            this.buttonPanel.Controls.Add(this.btnSave);

            this.btnCancel = new Syncfusion.WinForms.Controls.SfButton();
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.AutoSize = true;
            this.btnCancel.Location = new System.Drawing.Point(labelWidth + controlWidth - 100, 4);
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.AccessibleName = "Cancel";
            this.btnCancel.AccessibleDescription = "Cancel and discard changes";
            this.btnCancel.TabIndex = 11;
            this.btnCancel.Click += new System.EventHandler(this.Cancel);
            this.buttonPanel.Controls.Add(this.btnCancel);

            this.Controls.Add(this.buttonPanel);

            // Form properties
            this.Name = "AccountEditPanel";
            this.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Size = new System.Drawing.Size(520, 580);
            this.Padding = new System.Windows.Forms.Padding(16);
        }

        // Designer fields (auto-restored to match InitializeComponent)
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblAccountNumber;
        private Syncfusion.Windows.Forms.Tools.TextBoxExt txtAccountNumber;
        private System.Windows.Forms.Label lblName;
        private Syncfusion.Windows.Forms.Tools.TextBoxExt txtName;
        private System.Windows.Forms.Label lblDescription;
        private Syncfusion.Windows.Forms.Tools.TextBoxExt txtDescription;
        private System.Windows.Forms.Label lblDepartment;
        private Syncfusion.WinForms.ListView.SfComboBox cmbDepartment;
        private System.Windows.Forms.Label lblFund;
        private Syncfusion.WinForms.ListView.SfComboBox cmbFund;
        private System.Windows.Forms.Label lblType;
        private Syncfusion.WinForms.ListView.SfComboBox cmbType;
        private System.Windows.Forms.Label lblBalance;
        private Syncfusion.WinForms.Input.SfNumericTextBox numBalance;
        private System.Windows.Forms.Label lblBudget;
        private Syncfusion.WinForms.Input.SfNumericTextBox numBudget;
        private Syncfusion.Windows.Forms.Tools.CheckBoxAdv chkActive;
        private WileyWidget.WinForms.Controls.GradientPanelExt buttonPanel;
        private Syncfusion.WinForms.Controls.SfButton btnSave;
        private Syncfusion.WinForms.Controls.SfButton btnCancel;

        #endregion
    }
}
