using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using System;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls.Panels;

namespace WileyWidget.WinForms.Dialogs
{
    public partial class AccountEditDialog : SfForm
    {
        private readonly AccountEditPanel _editPanel;

        public AccountEditDialog(AccountEditPanel editPanel)
        {
            _editPanel = editPanel ?? throw new ArgumentNullException(nameof(editPanel));

            // Host the panel
            Controls.Add(_editPanel);
            _editPanel.Dock = DockStyle.Fill;

            // Wire the panel's events (already in your panel — just make sure they fire)
            _editPanel.SaveCompleted += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            _editPanel.CancelRequested += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            Text = "Municipal Account Editor";
            Size = new Size(680, 780);
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _editPanel.FocusFirstError(); // or just txtAccountNumber.Focus()
        }
    }
}
