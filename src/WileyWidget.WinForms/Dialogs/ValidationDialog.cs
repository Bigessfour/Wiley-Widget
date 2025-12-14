using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Dialogs
{
    /// <summary>
    /// Custom validation dialog for displaying validation errors in a scrollable list.
    /// Replaces MessageBox.Show() for better UX with multiple validation messages.
    /// </summary>
    public sealed class ValidationDialog : Form
    {
        private System.Windows.Forms.Timer? _copyTimer;
        private readonly ILogger<ValidationDialog>? _logger;
        private ListBox? _errorListBox;
        private Button? _okButton;
        private Button? _copyButton;
        private Label? _headerLabel;
        private PictureBox? _iconPictureBox;

        /// <summary>
        /// Creates a validation dialog with a list of error messages.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="headerMessage">Header message displayed above error list</param>
        /// <param name="errors">Collection of validation error messages</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public ValidationDialog(
            string title,
            string headerMessage,
            IEnumerable<string> errors,
            ILogger<ValidationDialog>? logger = null)
        {
            _logger = logger;

            var errorList = errors?.ToList() ?? new List<string>();
            if (errorList.Count == 0)
            {
                errorList.Add("No validation errors provided.");
            }

            InitializeDialog(title, headerMessage, errorList);

            _logger?.LogDebug("ValidationDialog created with {ErrorCount} errors", errorList.Count);
        }

        private void InitializeDialog(string title, string headerMessage, List<string> errors)
        {
            // Form properties
            Text = title;
            Size = new Size(500, 350);
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
                BackColor = ThemeColors.Background
            };

            // Icon column (fixed 48px)
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            // Content column (fill remaining)
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Header row (auto-size)
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            // Error list row (fill)
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            // Button row (auto-size)
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

            // Error icon
            _iconPictureBox = new PictureBox
            {
                Image = SystemIcons.Error.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(32, 32),
                Margin = new Padding(0, 5, 10, 10)
            };
            mainPanel.Controls.Add(_iconPictureBox, 0, 0);

            // Header label
            _headerLabel = new Label
            {
                Text = headerMessage,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ThemeColors.Error,
                Padding = new Padding(0, 5, 0, 10),
                TextAlign = ContentAlignment.MiddleLeft
            };
            mainPanel.Controls.Add(_headerLabel, 1, 0);

            // Error list box (spans both columns)
            _errorListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(40, ThemeColors.Error),
                Font = new Font("Segoe UI", 9F),
                SelectionMode = SelectionMode.MultiExtended,
                IntegralHeight = false,
                Margin = new Padding(0, 5, 0, 10)
            };

            foreach (var error in errors)
            {
                _errorListBox.Items.Add($"ΓÇó {error}");
            }

            mainPanel.SetColumnSpan(_errorListBox, 2);
            mainPanel.Controls.Add(_errorListBox, 0, 1);

            // Button panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };

            // OK button
            _okButton = new Button
            {
                Text = "OK",
                Size = new Size(90, 32),
                DialogResult = DialogResult.OK,
                UseVisualStyleBackColor = true,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 0)
            };
            _okButton.Click += (s, e) => Close();
            buttonPanel.Controls.Add(_okButton);

            // Copy button
            _copyButton = new Button
            {
                Text = "Copy All",
                Size = new Size(90, 32),
                UseVisualStyleBackColor = true,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 8, 0)
            };
            _copyButton.Click += CopyButton_Click;
            buttonPanel.Controls.Add(_copyButton);

            mainPanel.SetColumnSpan(buttonPanel, 2);
            mainPanel.Controls.Add(buttonPanel, 0, 2);

            Controls.Add(mainPanel);

            // Set accept button
            AcceptButton = _okButton;
        }

        private void CopyButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_errorListBox?.Items != null && _errorListBox.Items.Count > 0)
                {
                    var errorText = string.Join(Environment.NewLine,
                        _errorListBox.Items.Cast<string>());

                    Clipboard.SetText(errorText);

                    _logger?.LogDebug("Validation errors copied to clipboard ({Count} errors)",
                        _errorListBox.Items.Count);

                    // Visual feedback
                    if (_copyButton != null)
                    {
                        var originalText = _copyButton.Text;
                        _copyButton.Text = "Copied!";
                        _copyButton.Enabled = false;

                        _copyTimer = new System.Windows.Forms.Timer { Interval = 1500 };
                        _copyTimer.Tick += (s, args) =>
                        {
                            _copyButton.Text = originalText;
                            _copyButton.Enabled = true;
                            _copyTimer?.Stop();
                            _copyTimer?.Dispose();
                            _copyTimer = null;
                        };
                        _copyTimer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to copy validation errors to clipboard");
            }
        }

        /// <summary>
        /// Shows a validation dialog with the specified errors.
        /// </summary>
        /// <param name="owner">Parent form</param>
        /// <param name="title">Dialog title</param>
        /// <param name="headerMessage">Header message</param>
        /// <param name="errors">Validation errors</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>DialogResult</returns>
        public static DialogResult Show(
            IWin32Window? owner,
            string title,
            string headerMessage,
            IEnumerable<string> errors,
            ILogger? logger = null)
        {
            using var dialog = new ValidationDialog(title, headerMessage, errors, logger as ILogger<ValidationDialog>);
            return owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        }

        /// <summary>
        /// Shows a validation dialog with a default header message.
        /// </summary>
        public static DialogResult Show(
            IWin32Window? owner,
            string title,
            IEnumerable<string> errors,
            ILogger? logger = null)
        {
            return Show(owner, title, "The following validation errors occurred:", errors, logger);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _iconPictureBox?.Dispose();
                _errorListBox?.Dispose();
                _okButton?.Dispose();
                _copyButton?.Dispose();
                _copyTimer?.Dispose();
                _headerLabel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
