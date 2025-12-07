using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Small modal dialog showing progress and allowing cancellation.
    /// </summary>
    public partial class ProgressDialog : Form
    {
        private readonly Button _cancelButton;
        private readonly ProgressBar _progressBar;
        private readonly Label _label;
        private readonly Label _statusLabel;

        public bool IsCancelled { get; private set; }

        public ProgressDialog(string title = "Processing", string initialMessage = "Working...")
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 420;
            Height = 140;

            _label = new Label
            {
                AutoSize = false,
                Text = initialMessage,
                Left = 12,
                Top = 12,
                Width = ClientSize.Width - 24,
                Height = 20
            };
            Controls.Add(_label);

            _progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Left = 12,
                Top = 40,
                Width = ClientSize.Width - 24,
                Height = 24,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            Controls.Add(_progressBar);

            // Small status label below the progress bar for step messages (e.g., "Writing rows...", "Saving file...")
            _statusLabel = new Label
            {
                AutoSize = false,
                Text = string.Empty,
                Left = 12,
                Top = 70,
                Width = ClientSize.Width - 24,
                Height = 18,
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular)
            };
            Controls.Add(_statusLabel);

            _cancelButton = new Button
            {
                Text = "Cancel",
                Left = (ClientSize.Width - 80) / 2,
                Top = 76,
                Width = 80,
                Height = 28
            };
            _cancelButton.Click += CancelButton_Click;
            Controls.Add(_cancelButton);

            // Adjust controls on resize
            this.Resize += ProgressDialog_Resize;
        }

        private void ProgressDialog_Resize(object? sender, EventArgs e)
        {
            _label.Width = ClientSize.Width - 24;
            _progressBar.Width = ClientSize.Width - 24;
            _statusLabel.Width = ClientSize.Width - 24;
            _cancelButton.Left = (ClientSize.Width - _cancelButton.Width) / 2;
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            IsCancelled = true;
            _cancelButton.Enabled = false;
            _label.Text = "Cancelling...";
        }

        public void SetMessage(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => _label.Text = message));
                return;
            }

            _label.Text = message;
        }

        public void SetProgress(int percent)
        {
            if (percent < _progressBar.Minimum) percent = _progressBar.Minimum;
            if (percent > _progressBar.Maximum) percent = _progressBar.Maximum;

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => _progressBar.Value = percent));
                return;
            }

            _progressBar.Value = percent;
        }

        /// <summary>
        /// Set the small status text (step-level message) shown under the progress bar.
        /// </summary>
        public void SetStatus(string status)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => _statusLabel.Text = status));
                return;
            }

            _statusLabel.Text = status;
        }

        /// <summary>
        /// Switch the progress bar into indeterminate (marquee) mode when true, or back to a determinate bar when false.
        /// </summary>
        public void SetIndeterminate(bool indeterminate)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => SetIndeterminate(indeterminate)));
                return;
            }

            if (indeterminate)
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                try { _progressBar.MarqueeAnimationSpeed = 30; } catch { }
            }
            else
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                try { _progressBar.MarqueeAnimationSpeed = 0; } catch { }
            }
        }
    }
}
