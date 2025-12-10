using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Dialogs
{
    /// <summary>
    /// Reusable confirmation dialog for delete operations.
    /// Logs user decision without using MessageBox.
    /// </summary>
    public sealed class DeleteConfirmationDialog : Form
    {
        private readonly ILogger<DeleteConfirmationDialog>? _logger;
        private Button? _deleteButton;
        private Button? _cancelButton;
        private Label? _messageLabel;
        private Label? _detailLabel;
        private PictureBox? _iconPictureBox;

        /// <summary>
        /// Creates a delete confirmation dialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Primary message (e.g., "Delete this account?")</param>
        /// <param name="detail">Optional detail text (e.g., account name/number)</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public DeleteConfirmationDialog(
            string title,
            string message,
            string? detail = null,
            ILogger<DeleteConfirmationDialog>? logger = null)
        {
            _logger = logger;
            InitializeDialog(title, message, detail);

            _logger?.LogDebug("DeleteConfirmationDialog created: {Message}", message);
        }

        private void InitializeDialog(string title, string message, string? detail)
        {
            // Form properties
            Text = title;
            Size = new Size(450, detail != null ? 200 : 170);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;

            // Main layout panel
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(15),
                BackColor = Color.White
            };

            // Icon column (fixed 48px)
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            // Content column (fill remaining)
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Message rows (auto-size)
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (detail != null)
            {
                mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            // Button row (auto-size)
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

            // Warning icon
            _iconPictureBox = new PictureBox
            {
                Image = SystemIcons.Warning.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(32, 32),
                Margin = new Padding(0, 5, 10, 10)
            };
            mainPanel.Controls.Add(_iconPictureBox, 0, 0);

            // Message label
            _messageLabel = new Label
            {
                Text = message,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.Black,
                Padding = new Padding(0, 8, 0, 5),
                TextAlign = ContentAlignment.MiddleLeft
            };
            mainPanel.Controls.Add(_messageLabel, 1, 0);

            // Detail label (if provided)
            int buttonRow = 1;
            if (detail != null)
            {
                _detailLabel = new Label
                {
                    Text = detail,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(64, 64, 64),
                    Padding = new Padding(0, 0, 0, 10),
                    TextAlign = ContentAlignment.TopLeft
                };
                mainPanel.SetColumnSpan(_detailLabel, 2);
                mainPanel.Controls.Add(_detailLabel, 0, 1);
                buttonRow = 2;
            }

            // Button panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };

            // Cancel button
            _cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 32),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = true,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 0)
            };
            _cancelButton.Click += (s, e) =>
            {
                _logger?.LogDebug("Delete operation canceled by user");
                Close();
            };
            buttonPanel.Controls.Add(_cancelButton);

            // Delete button
            _deleteButton = new Button
            {
                Text = "Delete",
                Size = new Size(90, 32),
                DialogResult = DialogResult.OK,
                UseVisualStyleBackColor = false,
                BackColor = Color.FromArgb(220, 53, 69),  // Bootstrap danger color
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            _deleteButton.FlatAppearance.BorderSize = 0;
            _deleteButton.Click += (s, e) =>
            {
                _logger?.LogDebug("Delete operation confirmed by user");
                Close();
            };
            buttonPanel.Controls.Add(_deleteButton);

            mainPanel.SetColumnSpan(buttonPanel, 2);
            mainPanel.Controls.Add(buttonPanel, 0, buttonRow);

            Controls.Add(mainPanel);

            // Set default buttons
            AcceptButton = _deleteButton;
            CancelButton = _cancelButton;
        }

        /// <summary>
        /// Shows a delete confirmation dialog.
        /// </summary>
        /// <param name="owner">Parent form</param>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Confirmation message</param>
        /// <param name="detail">Optional detail text</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>True if user confirmed deletion, false otherwise</returns>
        public static bool Show(
            IWin32Window? owner,
            string title,
            string message,
            string? detail = null,
            ILogger? logger = null)
        {
            using var dialog = new DeleteConfirmationDialog(title, message, detail, logger as ILogger<DeleteConfirmationDialog>);
            var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
            return result == DialogResult.OK;
        }

        /// <summary>
        /// Shows a delete confirmation dialog with a default title.
        /// </summary>
        public static bool Show(
            IWin32Window? owner,
            string message,
            string? detail = null,
            ILogger? logger = null)
        {
            return Show(owner, "Confirm Delete", message, detail, logger);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _iconPictureBox?.Dispose();
                _messageLabel?.Dispose();
                _detailLabel?.Dispose();
                _deleteButton?.Dispose();
                _cancelButton?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
