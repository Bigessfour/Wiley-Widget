using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastReport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// MDI child form for displaying FastReport Open Source ReportViewer.
    /// Follows MDI child form guidelines with defensive IsMdiContainer check.
    /// THREAD SAFETY: All operations are called on the UI thread.
    /// </summary>
    public partial class ReportViewerForm : Form
    {
        private readonly MainForm _mainForm;
        private readonly IReportService _reportService;
        private readonly ILogger<ReportViewerForm> _logger;
        private readonly string _reportPath;

#pragma warning disable CS0649 // Field '_reportViewer' is never assigned to, and will always have its default value null
        private Report? _reportViewer;
#pragma warning restore CS0649
        private ToolStrip? _toolStrip;
        private ToolStripButton? _btnRefresh;

        /// <summary>
        /// Creates a new ReportViewerForm following MDI child form pattern.
        /// CRITICAL: Checks IsMdiContainer before setting MdiParent per guidelines.
        /// </summary>
        /// <param name="mainForm">Parent MainForm instance</param>
        /// <param name="reportPath">Path to RDL/RDLC report file</param>
        public ReportViewerForm(MainForm mainForm, string reportPath)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _reportPath = !string.IsNullOrWhiteSpace(reportPath)
                ? reportPath
                : throw new ArgumentException("Report path cannot be null or whitespace.", nameof(reportPath));

            InitializeComponent();

            // Defensive MDI pattern per guidelines - CRITICAL for test compatibility
            if (mainForm.IsMdiContainer)
            {
                MdiParent = mainForm;
            }

            // Resolve services from DI
            _reportService = ServiceProviderServiceExtensions.GetRequiredService<IReportService>(mainForm.ServiceProvider);
            _logger = ServiceProviderServiceExtensions.GetRequiredService<ILogger<ReportViewerForm>>(mainForm.ServiceProvider);

            // Load report asynchronously after form is shown
            Load += ReportViewerForm_Load;
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // Form properties
            Text = "Report Viewer";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;

            // Create toolbar
            _toolStrip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(24, 24)
            };

            _btnRefresh = new ToolStripButton
            {
                Text = "Refresh",
                ToolTipText = "Refresh report data"
            };
            _btnRefresh.Click += BtnRefresh_Click;

            _toolStrip.Items.Add(_btnRefresh);

            Controls.Add(_toolStrip);

            // Create FastReport WinForms ReportViewer
            // Note: FastReport.OpenSource doesn't include a ReportViewer control
            // TODO: Implement report viewing using FastReport.Report directly or upgrade to FastReport.Net
            // _reportViewer = new ReportViewer { Dock = DockStyle.Fill };
            // Controls.Add(_reportViewer);

            var placeholder = new Label
            {
                Text = "Report viewer not available in FastReport.OpenSource.\nConsider upgrading to FastReport.Net for viewer functionality.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(placeholder);

            ResumeLayout(false);
            PerformLayout();
        }

        private async void ReportViewerForm_Load(object? sender, EventArgs e)
        {
            if (_reportViewer == null)
            {
                _logger.LogError("ReportViewer control not initialized");
                return;
            }

            try
            {
                _logger.LogInformation("Loading report: {ReportPath}", _reportPath);

                // Show loading indicator
                Text = "Report Viewer - Loading...";

                // Load report directly on UI thread
                await _reportService.LoadReportAsync(_reportViewer, _reportPath);

                _logger.LogInformation("Report loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load report: {ReportPath}", _reportPath);
                MessageBox.Show(
                    $"Failed to load report: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                Text = "Report Viewer - Error";
            }
        }

        private async void BtnRefresh_Click(object? sender, EventArgs e)
        {
            if (_reportViewer == null) return;

            try
            {
                // Refresh report by reloading it
                await _reportService.LoadReportAsync(_reportViewer, _reportPath);
                _logger.LogInformation("Report refreshed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh report");
                MessageBox.Show(
                    $"Failed to refresh report: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolStrip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
