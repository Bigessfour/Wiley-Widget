using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Modal form for print preview functionality.
    /// Displays generated PDF and provides print options.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class PrintPreviewForm : Form
    {
        private readonly ILogger<PrintPreviewForm> _logger;
        private readonly string _pdfPath;
        private Button? _printButton;
        private Button? _closeButton;

        public PrintPreviewForm(ILogger<PrintPreviewForm> logger, string pdfPath)
        {
            InitializeComponent();

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pdfPath = pdfPath ?? throw new ArgumentNullException(nameof(pdfPath));
            _logger.LogInformation("PrintPreviewForm initialized for PDF: {Path}", pdfPath);
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // Form properties
            Text = "Print Preview";
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;

            // Preview panel (placeholder for PDF viewer)
            var previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var previewLabel = new Label
            {
                Text = $"PDF Preview\n\nFile: {_pdfPath}\n\n(External PDF viewer will open)",
                Font = new Font("Segoe UI", 12),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                ForeColor = Color.Gray
            };
            previewPanel.Controls.Add(previewLabel);

            // Button panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10)
            };

            _closeButton = new Button
            {
                Text = "Close",
                Width = 100,
                Height = 35,
                DialogResult = DialogResult.Cancel
            };
            _closeButton.Click += (s, e) => Close();

            _printButton = new Button
            {
                Text = "Print",
                Width = 100,
                Height = 35,
                BackColor = Color.FromArgb(52, 168, 83),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _printButton.Click += async (s, e) => await PrintAsync();

            buttonPanel.Controls.Add(_closeButton);
            buttonPanel.Controls.Add(_printButton);

            // Add controls
            Controls.Add(previewPanel);
            Controls.Add(buttonPanel);

            ResumeLayout(false);
        }

        private async Task PrintAsync()
        {
            _logger.LogInformation("Print requested from preview form");

            try
            {
                _printButton!.Enabled = false;
                _printButton.Text = "Printing...";

                // Use system print for PDF
                using (var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _pdfPath,
                        UseShellExecute = true,
                        Verb = "print"
                    }
                })
                {
                    process.Start();
                    await process.WaitForExitAsync();
                }

                _logger.LogInformation("Print completed successfully");
                MessageBox.Show("Print job sent successfully!", "Print", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Print failed");
                MessageBox.Show($"Print failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _printButton!.Enabled = true;
                _printButton.Text = "Print";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _printButton?.Dispose();
                _closeButton?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
