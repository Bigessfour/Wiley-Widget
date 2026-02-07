using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Syncfusion.WinForms.Controls;
using WileyWidget.Models;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Panels;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.WinForms.Dialogs
{
    /// <summary>
    /// Dialog for creating or editing a municipal account.
    /// Hosts AccountEditPanel for consistent UI and validation.
    /// </summary>
    public sealed class AccountEditDialog : Form
    {
        private readonly ILogger? _logger;
        private readonly MunicipalAccountEditModel _editModel;
        private AccountEditPanel? _editPanel;

        /// <summary>
        /// Gets whether the dialog was saved successfully.
        /// </summary>
        public bool IsSaved { get; private set; }

        /// <summary>
        /// Creates an account create/edit dialog.
        /// </summary>
        /// <param name="editModel">The edit model to use (new for create, FromEntity for edit)</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public AccountEditDialog(MunicipalAccountEditModel editModel, ILogger? logger = null)
        {
            _logger = logger;
            _editModel = editModel ?? throw new ArgumentNullException(nameof(editModel));

            InitializeDialog();
            ThemeColors.ApplyTheme(this);

            // Hook the Shown event to initialize panel after dialog is visible
            this.Shown += OnDialogShown;

            this.PerformLayout();
            this.Refresh();
            _logger?.LogDebug("[DIALOG] {DialogName} initialized and will load data on Shown event", this.Text);
        }

        private async void OnDialogShown(object? sender, EventArgs e)
        {
            try
            {
                _logger?.LogDebug("[DIALOG] Dialog shown, initializing panel data");

                // Ensure the panel loads data asynchronously after the dialog is shown
                if (_editPanel != null)
                {
                    await _editPanel.LoadAsync(System.Threading.CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[DIALOG] Error loading panel data");
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeDialog()
        {
            Text = _editModel.Id == 0 ? "Create Municipal Account" : "Edit Municipal Account";
            Size = new Size(600, 680);
            MinimumSize = new Size(580, 680);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;  // Allow resizing for better usability
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;

            // Create main layout - panel fills entire dialog with proper padding
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Padding = new Padding(8, 8, 8, 8)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Create and configure AccountEditPanel
            var serviceProvider = Program.Services;
            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(serviceProvider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<AccountEditPanel>>(serviceProvider);

            _logger?.LogDebug("[DIALOG] Creating AccountEditPanel for {Mode} mode", _editModel.Id == 0 ? "Create" : "Edit");

            _editPanel = new AccountEditPanel(scopeFactory, logger!)
            {
                Dock = DockStyle.Fill
            };

            // Set the edit model data (convert to entity for SetExistingAccount if editing)
            if (_editModel != null && _editModel.Id > 0)
            {
                _logger?.LogDebug("[DIALOG] Setting existing account with Id: {Id}", _editModel.Id);
                var tempEntity = _editModel.ToEntity();
                _editPanel.SetExistingAccount(tempEntity);
            }
            else
            {
                _logger?.LogDebug("[DIALOG] Creating new account");
            }


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
            // Note: editModel is updated by the panel; caller uses it post-save
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
                // Unhook event handler
                this.Shown -= OnDialogShown;

                if (_editPanel != null)
                {
                    // Unsubscribe events to prevent leaks
                    _editPanel.SaveCompleted -= OnPanelSaveCompleted;
                    _editPanel.CancelRequested -= OnPanelCancelRequested;
                    _editPanel.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
