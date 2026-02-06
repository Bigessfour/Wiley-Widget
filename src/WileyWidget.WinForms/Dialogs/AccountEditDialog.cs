using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using WileyWidget.Models;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Panels;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.WinForms.Dialogs
{
    /// <summary>
    /// Dialog for editing an existing municipal account.
    /// Hosts AccountEditPanel for consistent UI and validation.
    /// </summary>
    public sealed class AccountEditDialog : Form
    {
        private readonly ILogger? _logger;
        private readonly MunicipalAccount _account;
        private AccountEditPanel? _editPanel;

        /// <summary>
        /// Gets whether the dialog was saved successfully.
        /// </summary>
        public bool IsSaved { get; private set; }

        /// <summary>
        /// Creates an account edit dialog.
        /// </summary>
        /// <param name="account">The account to edit (will be modified if saved)</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public AccountEditDialog(MunicipalAccount account, ILogger? logger = null)
        {
            _logger = logger;
            _account = account ?? throw new ArgumentNullException(nameof(account));

            InitializeDialog();
            ThemeColors.ApplyTheme(this);

            this.PerformLayout();
            this.Refresh();
            _logger?.LogDebug("[DIALOG] {DialogName} content anchored and refreshed", this.Name);
        }

        private void InitializeDialog()
        {
            Text = "Edit Municipal Account";
            Size = new Size(600, 650);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Create main layout - panel fills entire dialog
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Create and configure AccountEditPanel
            var serviceProvider = Program.Services;
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(serviceProvider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<AccountEditPanel>>(serviceProvider);
            _editPanel = new AccountEditPanel(scopeFactory, logger!);
            _editPanel.SetExistingAccount(_account);
            _editPanel.Dock = DockStyle.Fill;

            // Subscribe to panel events
            _editPanel.SaveCompleted += OnPanelSaveCompleted;
            _editPanel.CancelRequested += OnPanelCancelRequested;

            mainLayout.Controls.Add(_editPanel, 0, 0);

            Controls.Add(mainLayout);

            this.PerformLayout();
            this.Refresh();
            _logger?.LogDebug("[DIALOG] {DialogName} content anchored and refreshed", this.Name);
        }

        private void OnPanelSaveCompleted(object? sender, EventArgs e)
        {
            IsSaved = true;
            DialogResult = DialogResult.OK;
            Close();
            _logger?.LogInformation("Account edit completed successfully");
        }

        private void OnPanelCancelRequested(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_editPanel != null)
                {
                    _editPanel.SaveCompleted -= OnPanelSaveCompleted;
                    _editPanel.CancelRequested -= OnPanelCancelRequested;
                    _editPanel.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
