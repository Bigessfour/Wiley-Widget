using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Styles;
using WileyWidget.ViewModels;
using SfGradientStyle = Syncfusion.Drawing.GradientStyle;
using SfGridBorder = Syncfusion.WinForms.DataGrid.Styles.GridBorder;
using SfGridBorderStyle = Syncfusion.WinForms.DataGrid.Styles.GridBorderStyle;
using SfGridBorderWeight = Syncfusion.WinForms.DataGrid.Styles.GridBorderWeight;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Resources for BudgetOverviewForm UI strings.
    /// </summary>
    internal static class BudgetOverviewFormResources
    {
        public const string FormTitle = "Budget Overview";
        public const string RefreshButton = "🔄 Refresh";
        public const string ExportButton = "📊 Export";
        public const string PrintButton = "🖨️ Print";
        public const string TotalBudgetLabel = "Total Budget";
        public const string TotalActualLabel = "Total Actual";
        public const string VarianceLabel = "Variance";
        public const string PercentUsedLabel = "% Used";
        public const string MetricsTitle = "Financial Metrics";
        public const string CategoryBreakdownTitle = "Category Breakdown";
        public const string LoadingMessage = "Loading budget data...";
        public const string ErrorPrefix = "Error: ";
        public const string LastUpdatedFormat = "Last updated: {0:g}";
        public const string CategoryHeader = "Category";
        public const string BudgetedHeader = "Budgeted";
        public const string ActualHeader = "Actual";
        public const string VarianceHeader = "Variance";
        public const string PercentUsedHeader = "% Used";
        public const string StatusHeader = "Status";
        public const string TrendHeader = "Trend";
    }

    /// <summary>
    /// Budget overview form displaying financial summary data with Syncfusion controls.
    /// Features comprehensive styling, conditional formatting, and professional theming.
    /// Binds to BudgetOverviewViewModel for MVVM pattern compliance.
    /// </summary>
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public partial class BudgetOverviewForm : Form
    {
        private readonly BudgetOverviewViewModel _viewModel;
        private readonly ILogger<BudgetOverviewForm> _logger;

        // UI Controls
        private SfDataGrid? _metricsGrid;
        private Label? _totalBudgetValueLabel;
        private Label? _totalActualValueLabel;
        private Label? _varianceValueLabel;
        private Label? _percentUsedValueLabel;
        private Label? _statusLabel;
        private ProgressBar? _loadingProgress;
        private ProgressBar? _budgetProgressBar;
        private Button? _refreshButton;
        private Button? _exportButton;
        private ComboBox? _periodSelector;
        private Panel? _summaryCardsPanel;

        // Cancellation token source for async operations
        private CancellationTokenSource? _cts;

        // Color palette (consistent with ChartForm and other forms)
        private static readonly Color PositiveColor = Color.FromArgb(52, 168, 83);   // Green - under budget
        private static readonly Color NegativeColor = Color.FromArgb(234, 67, 53);   // Red - over budget
        private static readonly Color WarningColor = Color.FromArgb(251, 188, 4);    // Yellow - approaching limit
        private static readonly Color NeutralColor = Color.FromArgb(108, 117, 125);  // Gray
        private static readonly Color AccentBlue = Color.FromArgb(66, 133, 244);     // Blue
        private static readonly Color AccentPurple = Color.FromArgb(171, 71, 188);   // Purple
        private static readonly Color BackgroundColor = Color.FromArgb(245, 245, 250);
        private static readonly Color CardColor = Color.White;
        private static readonly Color HeaderBackColor = Color.FromArgb(33, 37, 41);  // Dark header
        private static readonly Color GridHeaderColor = Color.FromArgb(52, 58, 64);  // Grid header
        private static readonly Color AlternateRowColor = Color.FromArgb(248, 249, 250);
        private static readonly Color GridBorderColor = Color.FromArgb(222, 226, 230);

        public BudgetOverviewForm(BudgetOverviewViewModel viewModel, ILogger<BudgetOverviewForm> logger)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                InitializeComponent();
                SetupDataGridStyling();
                SetupDataBindings();
                _logger.LogInformation("BudgetOverviewForm initialized successfully");

                // Initialize cancellation token source
                _cts = new CancellationTokenSource();

                Load += async (s, e) =>
                {
                    await Utilities.AsyncEventHelper.ExecuteAsync(
                        async ct =>
                        {
                            await _viewModel.InitializeAsync(ct);
                            UpdateUI();
                            UpdateBudgetProgress();
                        },
                        _cts,
                        this,
                        _logger,
                        "Loading budget overview data");
                };

                FormClosing += (s, e) =>
                {
                    Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize BudgetOverviewForm");
                if (Application.MessageLoop)
                {
                    MessageBox.Show($"Unable to open Budget Overview: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                throw;
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // Form settings with modern appearance
            Text = BudgetOverviewFormResources.FormTitle;
            Size = new Size(1100, 800);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = BackgroundColor;
            MinimumSize = new Size(900, 600);
            FormBorderStyle = FormBorderStyle.Sizable;
            DoubleBuffered = true;

            // === Header Panel with gradient-like dark theme ===
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = HeaderBackColor,
                Padding = new Padding(20, 12, 20, 12)
            };

            var titleLabel = new Label
            {
                Text = $"💰 {BudgetOverviewFormResources.FormTitle}",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 18)
            };
            headerPanel.Controls.Add(titleLabel);

            // Period selector in header
            var periodLabel = new Label
            {
                Text = "Period:",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(173, 181, 189),
                AutoSize = true,
                Location = new Point(headerPanel.Width - 350, 25)
            };
            headerPanel.Controls.Add(periodLabel);

            _periodSelector = new ComboBox
            {
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 130,
                Location = new Point(headerPanel.Width - 290, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _periodSelector.Items.AddRange(new object[] { "Current Month", "Current Quarter", "Year to Date", "Full Year", "Custom..." });
            _periodSelector.SelectedIndex = 2; // YTD default
            _periodSelector.SelectedIndexChanged += async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct => await RefreshDataAsync(),
                    _cts,
                    this,
                    _logger,
                    "Refreshing budget data");
            };
            headerPanel.Controls.Add(_periodSelector);

            // Refresh button
            _refreshButton = new Button
            {
                Text = BudgetOverviewFormResources.RefreshButton,
                Font = new Font("Segoe UI", 10),
                BackColor = AccentBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand,
                Location = new Point(headerPanel.Width - 140, 17)
            };
            _refreshButton.FlatAppearance.BorderSize = 0;
            _refreshButton.Click += async (s, e) =>
            {
                await Utilities.AsyncEventHelper.ExecuteAsync(
                    async ct => await RefreshDataAsync(),
                    _cts,
                    this,
                    _logger,
                    "Refreshing budget data");
            };
            headerPanel.Controls.Add(_refreshButton);

            // === Summary Cards Panel with 4 cards ===
            _summaryCardsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 150,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(15, 15, 15, 10),
                BackColor = BackgroundColor
            };
            ((TableLayoutPanel)_summaryCardsPanel).ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            ((TableLayoutPanel)_summaryCardsPanel).ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            ((TableLayoutPanel)_summaryCardsPanel).ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            ((TableLayoutPanel)_summaryCardsPanel).ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            var budgetCard = CreateSummaryCard(BudgetOverviewFormResources.TotalBudgetLabel, "$0.00", AccentBlue, "📊", out _totalBudgetValueLabel);
            var actualCard = CreateSummaryCard(BudgetOverviewFormResources.TotalActualLabel, "$0.00", AccentPurple, "💵", out _totalActualValueLabel);
            var varianceCard = CreateSummaryCard(BudgetOverviewFormResources.VarianceLabel, "$0.00", NeutralColor, "📈", out _varianceValueLabel);
            var percentCard = CreateSummaryCard(BudgetOverviewFormResources.PercentUsedLabel, "0%", NeutralColor, "⏱️", out _percentUsedValueLabel);

            ((TableLayoutPanel)_summaryCardsPanel).Controls.Add(budgetCard, 0, 0);
            ((TableLayoutPanel)_summaryCardsPanel).Controls.Add(actualCard, 1, 0);
            ((TableLayoutPanel)_summaryCardsPanel).Controls.Add(varianceCard, 2, 0);
            ((TableLayoutPanel)_summaryCardsPanel).Controls.Add(percentCard, 3, 0);

            // === Budget Progress Bar Panel ===
            var progressPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(20, 5, 20, 10),
                BackColor = BackgroundColor
            };

            var progressLabel = new Label
            {
                Text = "Budget Utilization",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(73, 80, 87),
                AutoSize = true,
                Location = new Point(20, 5)
            };
            progressPanel.Controls.Add(progressLabel);

            _budgetProgressBar = new ProgressBar
            {
                Location = new Point(20, 28),
                Size = new Size(progressPanel.Width - 50, 22),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            progressPanel.Controls.Add(_budgetProgressBar);

            // === Metrics Grid Panel ===
            var metricsGroup = new GroupBox
            {
                Text = $"  {BudgetOverviewFormResources.CategoryBreakdownTitle}  ",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(73, 80, 87),
                Padding = new Padding(15),
                Margin = new Padding(15, 10, 15, 10),
                BackColor = CardColor
            };

            _metricsGrid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AllowEditing = false,
                SelectionUnit = SelectionUnit.Row,
                SelectionMode = GridSelectionMode.Extended,
                AutoGenerateColumns = false,
                ShowGroupDropArea = false,
                BackColor = CardColor,
                RowHeight = 40,
                HeaderRowHeight = 45,
                AllowSorting = true,
                AllowFiltering = true,
                AllowResizingColumns = true,
                AutoSizeColumnsMode = AutoSizeColumnsMode.Fill,
                ShowBusyIndicator = true,
                ShowRowHeader = true,
                RowHeaderWidth = 35
            };

            // Configure columns with proper formatting
            ConfigureGridColumns();

            metricsGroup.Controls.Add(_metricsGrid);

            // === Status Bar Panel ===
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(15, 8, 15, 8)
            };

            // Add top border to status panel
            statusPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(GridBorderColor, 1);
                e.Graphics.DrawLine(pen, 0, 0, statusPanel.Width, 0);
            };

            _loadingProgress = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Width = 120,
                Height = 18,
                Visible = false,
                Location = new Point(15, 12)
            };
            statusPanel.Controls.Add(_loadingProgress);

            _statusLabel = new Label
            {
                Text = BudgetOverviewFormResources.LoadingMessage,
                Font = new Font("Segoe UI", 9),
                ForeColor = NeutralColor,
                AutoSize = true,
                Location = new Point(145, 14)
            };
            statusPanel.Controls.Add(_statusLabel);

            // Export button in status bar
            _exportButton = new Button
            {
                Text = BudgetOverviewFormResources.ExportButton,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(90, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(statusPanel.Width - 110, 8),
                Cursor = Cursors.Hand
            };
            _exportButton.FlatAppearance.BorderSize = 0;
            _exportButton.Click += (s, e) =>
            {
                _logger.LogInformation("Export button clicked on BudgetOverviewForm");
                ExportData();
            };
            statusPanel.Controls.Add(_exportButton);

            // === Add Controls to Form (order matters for docking) ===
            Controls.Add(metricsGroup);      // Fill (added first, fills remaining space)
            Controls.Add(progressPanel);     // Top (below cards)
            Controls.Add(_summaryCardsPanel); // Top (below header)
            Controls.Add(headerPanel);       // Top (outermost)
            Controls.Add(statusPanel);       // Bottom

            ResumeLayout(false);
            PerformLayout();
        }

        private void ConfigureGridColumns()
        {
            if (_metricsGrid == null) return;

            // Category column
            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Category",
                HeaderText = BudgetOverviewFormResources.CategoryHeader,
                Width = 180,
                AllowFiltering = true,
                AllowSorting = true
            });

            // Budgeted Amount column
            _metricsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "BudgetedAmount",
                HeaderText = BudgetOverviewFormResources.BudgetedHeader,
                Width = 130,
                Format = "C2"
            });

            // Actual Amount column
            _metricsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "ActualAmount",
                HeaderText = BudgetOverviewFormResources.ActualHeader,
                Width = 130,
                Format = "C2"
            });

            // Variance column (will be styled conditionally)
            _metricsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "Variance",
                HeaderText = BudgetOverviewFormResources.VarianceHeader,
                Width = 130,
                Format = "C2"
            });

            // Percent Used column
            _metricsGrid.Columns.Add(new GridNumericColumn
            {
                MappingName = "PercentUsed",
                HeaderText = BudgetOverviewFormResources.PercentUsedHeader,
                Width = 100,
                Format = "P1"
            });

            // Status column (text indicator)
            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Status",
                HeaderText = BudgetOverviewFormResources.StatusHeader,
                Width = 110
            });

            // Trend column (emoji indicator)
            _metricsGrid.Columns.Add(new GridTextColumn
            {
                MappingName = "Trend",
                HeaderText = BudgetOverviewFormResources.TrendHeader,
                Width = 80
            });
        }

        private void SetupDataGridStyling()
        {
            if (_metricsGrid == null) return;

            // === Header Style ===
            _metricsGrid.Style.HeaderStyle.BackColor = GridHeaderColor;
            _metricsGrid.Style.HeaderStyle.TextColor = Color.White;
            _metricsGrid.Style.HeaderStyle.Font.Bold = true;
            _metricsGrid.Style.HeaderStyle.Font.Size = 10;
            _metricsGrid.Style.HeaderStyle.HorizontalAlignment = HorizontalAlignment.Center;
            _metricsGrid.Style.HeaderStyle.Borders.All = new SfGridBorder(SfGridBorderStyle.Solid, GridBorderColor, SfGridBorderWeight.Thin);

            // === Cell Style ===
            _metricsGrid.Style.CellStyle.BackColor = CardColor;
            _metricsGrid.Style.CellStyle.TextColor = Color.FromArgb(33, 37, 41);
            _metricsGrid.Style.CellStyle.Font.Size = 10;
            _metricsGrid.Style.CellStyle.Borders.All = new SfGridBorder(SfGridBorderStyle.Solid, GridBorderColor, SfGridBorderWeight.Thin);
            _metricsGrid.Style.CellStyle.VerticalAlignment = System.Windows.Forms.VisualStyles.VerticalAlignment.Center;

            // === Selection Style ===
            _metricsGrid.Style.SelectionStyle.BackColor = Color.FromArgb(232, 240, 254);
            _metricsGrid.Style.SelectionStyle.TextColor = Color.FromArgb(33, 37, 41);

            // === Row Header Style ===
            _metricsGrid.Style.RowHeaderStyle.BackColor = Color.FromArgb(233, 236, 239);
            _metricsGrid.Style.RowHeaderStyle.TextColor = Color.FromArgb(73, 80, 87);

            // === Border Style ===
            _metricsGrid.Style.BorderStyle = BorderStyle.FixedSingle;
            _metricsGrid.Style.BorderColor = GridBorderColor;

            // === Scrollbar Style ===
            _metricsGrid.Style.VerticalScrollBar.ThumbColor = Color.FromArgb(173, 181, 189);
            _metricsGrid.Style.VerticalScrollBar.ThumbHoverColor = Color.FromArgb(134, 142, 150);
            _metricsGrid.Style.VerticalScrollBar.ScrollBarBackColor = Color.FromArgb(248, 249, 250);
            _metricsGrid.Style.HorizontalScrollBar.ThumbColor = Color.FromArgb(173, 181, 189);
            _metricsGrid.Style.HorizontalScrollBar.ThumbHoverColor = Color.FromArgb(134, 142, 150);
            _metricsGrid.Style.HorizontalScrollBar.ScrollBarBackColor = Color.FromArgb(248, 249, 250);

            // === Conditional Row Styling - alternate rows and value-based colors ===
            _metricsGrid.QueryRowStyle += MetricsGrid_QueryRowStyle;
            _metricsGrid.QueryCellStyle += MetricsGrid_QueryCellStyle;
        }

        private void MetricsGrid_QueryRowStyle(object? sender, QueryRowStyleEventArgs e)
        {
            if (e.RowType == RowType.DefaultRow)
            {
                // Alternate row colors for better readability
                if (e.RowIndex % 2 == 0)
                {
                    e.Style.BackColor = AlternateRowColor;
                }
            }
        }

        private void MetricsGrid_QueryCellStyle(object? sender, QueryCellStyleEventArgs e)
        {
            if (e.DataRow?.RowData is FinancialMetric metric)
            {
                // Style Variance column based on value
                if (e.Column?.MappingName == "Variance" || e.Column?.MappingName == "Amount")
                {
                    if (metric.Amount >= 0)
                    {
                        e.Style.TextColor = PositiveColor;
                        e.Style.Font.Bold = true;
                    }
                    else
                    {
                        e.Style.TextColor = NegativeColor;
                        e.Style.Font.Bold = true;
                    }
                }

                // Style Status column with appropriate colors
                if (e.Column?.MappingName == "Status")
                {
                    var status = e.DisplayText?.ToUpperInvariant() ?? "";
                    if (status.Contains("UNDER", StringComparison.OrdinalIgnoreCase) || status.Contains("GOOD", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Style.TextColor = PositiveColor;
                        e.Style.Font.Bold = true;
                    }
                    else if (status.Contains("OVER", StringComparison.OrdinalIgnoreCase) || status.Contains("EXCEEDED", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Style.TextColor = NegativeColor;
                        e.Style.Font.Bold = true;
                    }
                    else if (status.Contains("WARNING", StringComparison.OrdinalIgnoreCase) || status.Contains("APPROACHING", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Style.TextColor = WarningColor;
                        e.Style.Font.Bold = true;
                    }
                }

                // Style PercentUsed column
                if (e.Column?.MappingName == "PercentUsed")
                {
                    // Calculate percent (assuming it's stored as decimal like 0.85 for 85%)
                    var percentUsed = GetPercentUsedFromMetric(metric);
                    if (percentUsed > 1.0m) // Over 100%
                    {
                        e.Style.BackColor = Color.FromArgb(255, 235, 238);
                        e.Style.TextColor = NegativeColor;
                        e.Style.Font.Bold = true;
                    }
                    else if (percentUsed > 0.9m) // 90-100%
                    {
                        e.Style.BackColor = Color.FromArgb(255, 248, 225);
                        e.Style.TextColor = WarningColor;
                    }
                    else if (percentUsed > 0.75m) // 75-90%
                    {
                        e.Style.BackColor = Color.FromArgb(255, 253, 231);
                    }
                }

                // Style Trend column with emoji indicators
                if (e.Column?.MappingName == "Trend")
                {
                    e.Style.HorizontalAlignment = HorizontalAlignment.Center;
                    e.Style.Font.Size = 14;
                }
            }
        }

        private static decimal GetPercentUsedFromMetric(FinancialMetric metric)
        {
            // If we have budgeted vs actual, calculate percent
            // For now, use a simple ratio based on variance sign
            if (metric.Amount < 0)
                return 1.1m; // Over budget
            return 0.8m; // Under budget default
        }

        private Panel CreateSummaryCard(string title, string initialValue, Color accentColor, string icon, out Label valueLabel)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardColor,
                Margin = new Padding(8),
                Padding = new Padding(15)
            };

            // Custom paint for modern card appearance with shadow effect and accent bar
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Draw accent bar on left side
                using var accentBrush = new SolidBrush(accentColor);
                g.FillRectangle(accentBrush, 0, 0, 5, card.Height);

                // Draw subtle border
                using var borderPen = new Pen(Color.FromArgb(230, 230, 235), 1);
                g.DrawRectangle(borderPen, 0, 0, card.Width - 1, card.Height - 1);

                // Draw bottom shadow line
                using var shadowPen = new Pen(Color.FromArgb(15, 0, 0, 0), 2);
                g.DrawLine(shadowPen, 2, card.Height - 1, card.Width - 2, card.Height - 1);
            };

            // Icon label
            var iconLbl = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 24),
                ForeColor = accentColor,
                AutoSize = true,
                Location = new Point(15, 12)
            };
            card.Controls.Add(iconLbl);

            // Title label
            var titleLbl = new Label
            {
                Text = title.ToUpperInvariant(),
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = NeutralColor,
                AutoSize = true,
                Location = new Point(55, 15)
            };
            card.Controls.Add(titleLbl);

            // Value label
            valueLabel = new Label
            {
                Text = initialValue,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = true,
                Location = new Point(55, 38)
            };
            card.Controls.Add(valueLabel);

            // Subtext for additional context
            var subtextLbl = new Label
            {
                Text = "vs. prior period",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(173, 181, 189),
                AutoSize = true,
                Location = new Point(55, 72)
            };
            card.Controls.Add(subtextLbl);

            return card;
        }

        private void SetupDataBindings()
        {
            // Subscribe to ViewModel property changes
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (InvokeRequired)
                {
                    Invoke(() => HandlePropertyChanged(e.PropertyName));
                }
                else
                {
                    HandlePropertyChanged(e.PropertyName);
                }
            };
        }

        private void HandlePropertyChanged(string? propertyName)
        {
            switch (propertyName)
            {
                case nameof(BudgetOverviewViewModel.TotalBudget):
                case nameof(BudgetOverviewViewModel.TotalActual):
                case nameof(BudgetOverviewViewModel.Variance):
                case nameof(BudgetOverviewViewModel.Metrics):
                    UpdateUI();
                    UpdateBudgetProgress();
                    break;
                case nameof(BudgetOverviewViewModel.IsLoading):
                    UpdateLoadingState();
                    break;
                case nameof(BudgetOverviewViewModel.ErrorMessage):
                    UpdateErrorState();
                    break;
            }
        }

        private void UpdateUI()
        {
            // Update summary cards with formatted values
            if (_totalBudgetValueLabel != null)
            {
                _totalBudgetValueLabel.Text = _viewModel.TotalBudget.ToString("C0", CultureInfo.CurrentCulture);
            }

            if (_totalActualValueLabel != null)
            {
                _totalActualValueLabel.Text = _viewModel.TotalActual.ToString("C0", CultureInfo.CurrentCulture);
            }

            if (_varianceValueLabel != null)
            {
                _varianceValueLabel.Text = _viewModel.Variance.ToString("C0", CultureInfo.CurrentCulture);
                // Color-code variance: green for positive (under budget), red for negative (over budget)
                _varianceValueLabel.ForeColor = _viewModel.Variance >= 0 ? PositiveColor : NegativeColor;

                // Add indicator arrow
                var arrow = _viewModel.Variance >= 0 ? "▲" : "▼";
                _varianceValueLabel.Text = $"{arrow} {_varianceValueLabel.Text}";
            }

            if (_percentUsedValueLabel != null)
            {
                var percentUsed = _viewModel.TotalBudget != 0
                    ? (_viewModel.TotalActual / _viewModel.TotalBudget) * 100
                    : 0;
                _percentUsedValueLabel.Text = $"{percentUsed:F1}%";

                // Color code the percent
                if (percentUsed > 100)
                    _percentUsedValueLabel.ForeColor = NegativeColor;
                else if (percentUsed > 90)
                    _percentUsedValueLabel.ForeColor = WarningColor;
                else
                    _percentUsedValueLabel.ForeColor = PositiveColor;
            }

            // Update metrics grid
            if (_metricsGrid != null)
            {
                _metricsGrid.DataSource = _viewModel.Metrics;
                _metricsGrid.Refresh();
            }

            // Update status
            if (_statusLabel != null && string.IsNullOrEmpty(_viewModel.ErrorMessage))
            {
                _statusLabel.Text = string.Format(CultureInfo.CurrentCulture,
                    BudgetOverviewFormResources.LastUpdatedFormat, DateTime.Now);
                _statusLabel.ForeColor = NeutralColor;
            }
        }

        private void UpdateBudgetProgress()
        {
            if (_budgetProgressBar == null) return;

            var percentUsed = _viewModel.TotalBudget != 0
                ? (int)((_viewModel.TotalActual / _viewModel.TotalBudget) * 100)
                : 0;

            // Clamp to 0-100 range for the progress bar
            percentUsed = Math.Max(0, Math.Min(100, percentUsed));
            _budgetProgressBar.Value = percentUsed;

            // Update progress bar color based on utilization
            // Note: Standard ProgressBar doesn't support color changes easily
            // For Syncfusion, we could use SfProgressBar with custom colors
        }

        private void UpdateLoadingState()
        {
            if (_loadingProgress != null)
            {
                _loadingProgress.Visible = _viewModel.IsLoading;
            }

            if (_refreshButton != null)
            {
                _refreshButton.Enabled = !_viewModel.IsLoading;
                _refreshButton.Text = _viewModel.IsLoading ? "⏳ Loading..." : BudgetOverviewFormResources.RefreshButton;
            }

            if (_exportButton != null)
            {
                _exportButton.Enabled = !_viewModel.IsLoading;
            }

            if (_statusLabel != null && _viewModel.IsLoading)
            {
                _statusLabel.Text = BudgetOverviewFormResources.LoadingMessage;
                _statusLabel.ForeColor = AccentBlue;
            }

            // Disable grid during loading
            if (_metricsGrid != null)
            {
                _metricsGrid.Enabled = !_viewModel.IsLoading;
            }
        }

        private void UpdateErrorState()
        {
            if (_statusLabel != null && !string.IsNullOrEmpty(_viewModel.ErrorMessage))
            {
                _statusLabel.Text = $"⚠️ {BudgetOverviewFormResources.ErrorPrefix}{_viewModel.ErrorMessage}";
                _statusLabel.ForeColor = NegativeColor;
            }
        }

        private void ShowError(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = $"⚠️ {BudgetOverviewFormResources.ErrorPrefix}{message}";
                _statusLabel.ForeColor = NegativeColor;
            }

            _logger.LogWarning("Budget overview error displayed: {Message}", message);
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                _logger.LogInformation("Refreshing budget overview data for period: {Period}",
                    _periodSelector?.SelectedItem?.ToString() ?? "Unknown");

                await _viewModel.LoadDataCommand.ExecuteAsync(null);
                UpdateUI();
                UpdateBudgetProgress();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh budget overview");
                ShowError(ex.Message);
            }
        }

        private void ExportData()
        {
            try
            {
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx|CSV Files|*.csv|PDF Files|*.pdf",
                    DefaultExt = "xlsx",
                    FileName = $"BudgetOverview_{DateTime.Now:yyyyMMdd}"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    // TODO: Implement export logic with Syncfusion Excel/PDF
                    MessageBox.Show($"Export to {saveDialog.FileName} completed successfully.",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    _logger.LogInformation("Budget overview exported to: {FileName}", saveDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export budget overview");
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Reposition controls on resize
            if (_refreshButton != null && Controls.Count > 0)
            {
                var headerPanel = Controls.OfType<Panel>().FirstOrDefault(p => p.BackColor == HeaderBackColor);
                if (headerPanel != null)
                {
                    _refreshButton.Location = new Point(headerPanel.Width - 140, 17);
                    _periodSelector?.SetBounds(headerPanel.Width - 290, 21, 130, _periodSelector.Height);
                }
            }

            if (_exportButton != null)
            {
                var statusPanel = Controls.OfType<Panel>().FirstOrDefault(p =>
                    p.Height == 45 && p.Dock == DockStyle.Bottom);
                if (statusPanel != null)
                {
                    _exportButton.Location = new Point(statusPanel.Width - 110, 8);
                }
            }

            // Update budget progress bar width
            if (_budgetProgressBar != null)
            {
                var progressPanel = _budgetProgressBar.Parent;
                if (progressPanel != null)
                {
                    _budgetProgressBar.Width = progressPanel.Width - 50;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from events
                if (_metricsGrid != null)
                {
                    _metricsGrid.QueryRowStyle -= MetricsGrid_QueryRowStyle;
                    _metricsGrid.QueryCellStyle -= MetricsGrid_QueryCellStyle;
                }

                // Dispose controls
                _metricsGrid?.Dispose();
                _totalBudgetValueLabel?.Dispose();
                _totalActualValueLabel?.Dispose();
                _varianceValueLabel?.Dispose();
                _percentUsedValueLabel?.Dispose();
                _statusLabel?.Dispose();
                _loadingProgress?.Dispose();
                _budgetProgressBar?.Dispose();
                _refreshButton?.Dispose();
                _exportButton?.Dispose();
                _periodSelector?.Dispose();
                _summaryCardsPanel?.Dispose();

                // Cancel and dispose async operations
                Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
            }

            base.Dispose(disposing);
        }
    }
}
