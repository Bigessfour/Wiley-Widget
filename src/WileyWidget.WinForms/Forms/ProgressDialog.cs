using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Forms
{
    internal static class ProgressDialogResources
    {
        public const string DefaultTitle = "Progress";
        public const string DefaultMessage = "Please wait...";
        public const string CancelButtonText = "Cancel";
    }

    /// <summary>
    /// Progress dialog using Syncfusion ProgressBarAdv control.
    /// Displays progress percentage, status message, and optional cancel button.
    /// All controls are Syncfusion components with proper theming via SfSkinManager.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class ProgressDialog : Form
    {
        private ProgressBarAdv? _progressBar;
        private Label? _lblMessage;
        private Label? _lblPercentage;
        private Syncfusion.WinForms.Controls.SfButton? _btnCancel;
        private TableLayoutPanel? _mainLayout;

        private bool _cancelled = false;

        public bool IsCancelled => _cancelled;

        /// <summary>
        /// Gets or sets the progress value (0-100).
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ProgressValue
        {
            get => _progressBar?.Value ?? 0;
            set
            {
                if (_progressBar != null)
                {
                    _progressBar.Value = Math.Max(0, Math.Min(100, value));
                    UpdatePercentageLabel();
                }
            }
        }

        /// <summary>
        /// Gets or sets the status message displayed above the progress bar.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string StatusMessage
        {
            get => _lblMessage?.Text ?? string.Empty;
            set
            {
                if (_lblMessage != null)
                {
                    _lblMessage.Text = value ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the cancel button is visible.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowCancelButton
        {
            get => _btnCancel?.Visible ?? false;
            set
            {
                if (_btnCancel != null)
                {
                    _btnCancel.Visible = value;
                }
            }
        }

        public ProgressDialog() : this(ProgressDialogResources.DefaultMessage)
        {
        }

        public ProgressDialog(string message) : this(ProgressDialogResources.DefaultTitle, message)
        {
        }

        public ProgressDialog(string title, string message) : this(title, message, showCancelButton: false)
        {
        }

        public ProgressDialog(string title, string message, bool showCancelButton)
        {
            InitializeComponent();

            Text = title ?? ProgressDialogResources.DefaultTitle;
            StatusMessage = message ?? ProgressDialogResources.DefaultMessage;
            ShowCancelButton = showCancelButton;

            // Apply Syncfusion theme to form and all child controls
            ThemeColors.ApplyTheme(this);
        }

        private void InitializeComponent()
        {
            // Form properties
            Name = "ProgressDialog";
            Text = ProgressDialogResources.DefaultTitle;
            Size = new Size(450, 180);
            MinimumSize = new Size(400, 180);
            MaximumSize = new Size(600, 180);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ControlBox = false; // No close button - must complete or cancel

            // Main layout
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                RowCount = 4,
                ColumnCount = 1,
                AutoSize = true
            };

            _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Message label
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10)); // Spacer
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Progress bar
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Percentage label
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10)); // Spacer
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Cancel button

            // Status message label
            _lblMessage = new Label
            {
                Name = "lblMessage",
                Text = ProgressDialogResources.DefaultMessage,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                AccessibleName = "Status message",
                AccessibleDescription = "Current operation status"
            };
            _mainLayout.Controls.Add(_lblMessage, 0, 0);

            // Syncfusion ProgressBarAdv control
            _progressBar = new ProgressBarAdv
            {
                Name = "progressBar",
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                ProgressStyle = ProgressBarStyles.Gradient,
                FontColor = Color.Black,
                TextVisible = false, // We'll show percentage separately
                AccessibleName = "Progress bar",
                AccessibleDescription = "Operation progress indicator"
            };

            // NOTE: Theme cascades from form-level SetVisualStyle - no need for individual control theming
            // REMOVED: Manual color assignments - SfSkinManager owns all color decisions
            // REMOVED: _progressBar.ForeColor = ThemeColors.PrimaryAccent;
            // REMOVED: _progressBar.BackColor = ThemeColors.Background;

            _mainLayout.Controls.Add(_progressBar, 0, 2);

            // Percentage label
            _lblPercentage = new Label
            {
                Name = "lblPercentage",
                Text = "0%",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                // REMOVED: ForeColor = Color.Gray; - SfSkinManager owns all colors
                AccessibleName = "Percentage complete",
                AccessibleDescription = "Percentage of operation completed"
            };
            _mainLayout.Controls.Add(_lblPercentage, 0, 3);

            // Cancel button (Syncfusion SfButton)
            _btnCancel = new Syncfusion.WinForms.Controls.SfButton
            {
                Name = "btnCancel",
                Text = ProgressDialogResources.CancelButtonText,
                Size = new Size(100, 32),
                Anchor = AnchorStyles.None,
                Visible = false, // Hidden by default
                AccessibleName = "Cancel operation",
                AccessibleDescription = "Cancel the current operation"
            };
            _btnCancel.Click += BtnCancel_Click;
            // NOTE: Theme cascades from form-level SetVisualStyle - no need for individual control theming
            // REMOVED: try { SfSkinManager.SetVisualStyle(_btnCancel, ThemeColors.DefaultTheme); } catch { }

            _mainLayout.Controls.Add(_btnCancel, 0, 5);

            Controls.Add(_mainLayout);
        }

        private void UpdatePercentageLabel()
        {
            if (_lblPercentage != null && _progressBar != null)
            {
                _lblPercentage.Text = $"{_progressBar.Value}%";
            }
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            _cancelled = true;

            if (_btnCancel != null)
            {
                _btnCancel.Enabled = false;
                _btnCancel.Text = "Cancelling...";
            }

            // Raise event for parent to handle cancellation
            OnCancelled(EventArgs.Empty);
        }

        /// <summary>
        /// Event raised when the user clicks the Cancel button.
        /// </summary>
        public event EventHandler? Cancelled;

        protected virtual void OnCancelled(EventArgs e)
        {
            Cancelled?.Invoke(this, e);
        }

        /// <summary>
        /// Updates progress and message in a thread-safe manner.
        /// </summary>
        public void UpdateProgress(int value, string? message = null)
        {
            if (InvokeRequired)
            {
                Invoke(() => UpdateProgress(value, message));
                return;
            }

            ProgressValue = value;

            if (message != null)
            {
                StatusMessage = message;
            }

            Application.DoEvents(); // Allow UI to update
        }

        /// <summary>
        /// Completes the progress dialog and closes it.
        /// </summary>
        public void Complete(string? completionMessage = null)
        {
            if (InvokeRequired)
            {
                Invoke(() => Complete(completionMessage));
                return;
            }

            if (completionMessage != null)
            {
                StatusMessage = completionMessage;
            }

            ProgressValue = 100;
            Application.DoEvents();

            // Close after brief delay to show 100%
            System.Threading.Thread.Sleep(300);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _progressBar?.Dispose();
                _lblMessage?.Dispose();
                _lblPercentage?.Dispose();
                _btnCancel?.Dispose();
                _mainLayout?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
