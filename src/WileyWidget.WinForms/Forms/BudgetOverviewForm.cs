using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Events;
using Syncfusion.WinForms.DataGrid.Styles;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.Models;
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
        public const string RefreshButton = "≡ƒöä Refresh";
        public const string ExportButton = "≡ƒôè Export";
        public const string PrintButton = "≡ƒû¿∩╕Å Print";
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
        private static readonly Color PositiveColor = ThemeColors.Success;   // Green - under budget
        private static readonly Color NegativeColor = ThemeColors.Error;   // Red - over budget
        private static readonly Color WarningColor = ThemeColors.Warning;    // Yellow - approaching limit
        private static readonly Color NeutralColor = ThemeManager.Colors.TextPrimary;  // Gray/text fallback
        private static readonly Color AccentBlue = ThemeColors.PrimaryAccent;     // Blue
        private static readonly Color AccentPurple = ThemeColors.PrimaryAccent;   // Purple/fallback
        private static readonly Color BackgroundColor = ThemeColors.Background;
        private static readonly Color CardColor = ThemeColors.Background;
        private static readonly Color HeaderBackColor = ThemeColors.HeaderBackground;  // Header background
        private static readonly Color AlternateRowColor = ThemeColors.AlternatingRowBackground;
        private static readonly Color GridBorderColor = ThemeManager.Colors.TextPrimary;

        public BudgetOverviewForm(BudgetOverviewViewModel viewModel, ILogger<BudgetOverviewForm> logger, MainForm mainForm)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            MdiParent = mainForm ?? throw new ArgumentNullException(nameof(mainForm));

            ThemeColors.ApplyTheme(this);

            try
            {
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
            Name = "BudgetOverviewForm";
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
                Text = $"≡ƒÆ░ {BudgetOverviewFormResources.FormTitle}",
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
                ForeColor = ThemeManager.Colors.TextPrimary,
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

            // Action buttons
            var addCategoryBtn = new Button
            {
                Text = "Γ₧ò Add Category",
                Font = new Font("Segoe UI", 9),
                BackColor = PositiveColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(120, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand,
                Location = new Point(headerPanel.Width - 400, 17)
            };
            addCategoryBtn.FlatAppearance.BorderSize = 0;
            addCategoryBtn.Click += (s, e) =>
            {
                _logger.LogInformation("Add budget category button clicked");
                MessageBox.Show("Add Budget Category dialog will be implemented here.\n\nThis will allow you to create new budget categories with budgeted amounts.",
                    "Add Category", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            headerPanel.Controls.Add(addCategoryBtn);

            var editCategoryBtn = new Button
            {
                Text = "Γ£Å∩╕Å Edit",
                Font = new Font("Segoe UI", 9),
                BackColor = AccentPurple,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand,
                Location = new Point(headerPanel.Width - 270, 17)
            };
            editCategoryBtn.FlatAppearance.BorderSize = 0;
            editCategoryBtn.Click += (s, e) =>
            {
                _logger.LogInformation("Edit budget category button clicked");
                if (_metricsGrid?.SelectedItem != null)
                {
                    MessageBox.Show("Edit Budget Category dialog will be implemented here.\n\nThis will allow you to modify the selected budget category.",
                        "Edit Category", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Please select a budget category to edit.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            headerPanel.Controls.Add(editCategoryBtn);

            var deleteCategoryBtn = new Button
            {
                Text = "≡ƒùæ∩╕Å Delete",
                Font = new Font("Segoe UI", 9),
                BackColor = NegativeColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand,
                Location = new Point(headerPanel.Width - 180, 17)
            };
            deleteCategoryBtn.FlatAppearance.BorderSize = 0;
            deleteCategoryBtn.Click += (s, e) =>
            {
                _logger.LogInformation("Delete budget category button clicked");
                if (_metricsGrid?.SelectedItem != null)
                {
                    var result = MessageBox.Show("Are you sure you want to delete the selected budget category?\n\nThis action cannot be undone.",
                        "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result == DialogResult.Yes)
                    {
                        MessageBox.Show("Budget category deletion will be implemented here.", "Delete Category", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Please select a budget category to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            headerPanel.Controls.Add(deleteCategoryBtn);

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
                Dock = DockStyle.Fill,
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

            var budgetCard = CreateSummaryCard(BudgetOverviewFormResources.TotalBudgetLabel, "$0.00", AccentBlue, "≡ƒôè", out _totalBudgetValueLabel);
            var actualCard = CreateSummaryCard(BudgetOverviewFormResources.TotalActualLabel, "$0.00", AccentPurple, "≡ƒÆ╡", out _totalActualValueLabel);
            var varianceCard = CreateSummaryCard(BudgetOverviewFormResources.VarianceLabel, "$0.00", NeutralColor, "≡ƒôê", out _varianceValueLabel);
            var percentCard = CreateSummaryCard(BudgetOverviewFormResources.PercentUsedLabel, "0%", NeutralColor, "ΓÅ▒∩╕Å", out _percentUsedValueLabel);

            ((TableLayoutPanel)_summaryCardsPanel).Controls.Add(budgetCard, 0, 0);
            ((TableLayoutPanel)_summaryCardsPanel).Controls.Add(actualCard, 1, 0);
            ((TableLayoutPanel)_summaryCardsPanel).Controls.Add(varianceCard, 2, 0);
            ((TableLayoutPanel)_summaryCardsPanel).Controls.Add(percentCard, 3, 0);

            var summaryHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                Padding = new Padding(0),
                BackColor = BackgroundColor
            };
            summaryHost.Controls.Add(_summaryCardsPanel);

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
                ForeColor = ThemeManager.Colors.TextPrimary,
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
                ForeColor = ThemeManager.Colors.TextPrimary,
                Padding = new Padding(15),
                Margin = new Padding(15, 10, 15, 10),
                BackColor = CardColor
            };

            _metricsGrid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                Name = "BudgetOverview_MetricsGrid",
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
                RowHeaderWidth = 35,
                // Additional essential properties from Syncfusion best practices
                NavigationMode = NavigationMode.Cell,
                AllowDraggingColumns = true,
                AllowTriStateSorting = true
            };

            // Apply theme to the metrics grid
            ThemeColors.ApplySfDataGridTheme(_metricsGrid);
            SfSkinManager.SetVisualStyle(_metricsGrid, "Office2019Colorful");

            // Configure columns with proper formatting
            ConfigureGridColumns();

            metricsGroup.Controls.Add(_metricsGrid);

            // === Status Bar Panel ===
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                BackColor = ThemeColors.Background,
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
                BackColor = ThemeManager.Colors.TextPrimary,
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
            Controls.Add(summaryHost);       // Top (below header)
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

            // Apply theme to the data grid
            ThemeColors.ApplySfDataGridTheme(_metricsGrid);

            // === Header Style ===
            _metricsGrid.Style.HeaderStyle.Font.Size = 10;
            _metricsGrid.Style.HeaderStyle.HorizontalAlignment = HorizontalAlignment.Center;
            _metricsGrid.Style.HeaderStyle.Borders.All = new SfGridBorder(SfGridBorderStyle.Solid, GridBorderColor, SfGridBorderWeight.Thin);

            // === Cell Style ===
            _metricsGrid.Style.CellStyle.BackColor = CardColor;
            _metricsGrid.Style.CellStyle.TextColor = ThemeManager.Colors.TextPrimary;
            _metricsGrid.Style.CellStyle.Font.Size = 10;
            _metricsGrid.Style.CellStyle.Borders.All = new SfGridBorder(SfGridBorderStyle.Solid, GridBorderColor, SfGridBorderWeight.Thin);
            _metricsGrid.Style.CellStyle.VerticalAlignment = System.Windows.Forms.VisualStyles.VerticalAlignment.Center;

            // === Selection Style ===
            _metricsGrid.Style.SelectionStyle.BackColor = Color.FromArgb(48, ThemeColors.PrimaryAccent);
            _metricsGrid.Style.SelectionStyle.TextColor = ThemeManager.Colors.TextPrimary;

            // === Row Header Style ===
            _metricsGrid.Style.RowHeaderStyle.BackColor = ThemeColors.Background;
            _metricsGrid.Style.RowHeaderStyle.TextColor = ThemeManager.Colors.TextPrimary;

            // === Border Style ===
            _metricsGrid.Style.BorderStyle = BorderStyle.FixedSingle;
            _metricsGrid.Style.BorderColor = GridBorderColor;

            // === Scrollbar Style ===
            _metricsGrid.Style.VerticalScrollBar.ThumbColor = ThemeManager.Colors.TextPrimary;
            _metricsGrid.Style.VerticalScrollBar.ThumbHoverColor = ThemeManager.Colors.TextPrimary;
            _metricsGrid.Style.VerticalScrollBar.ScrollBarBackColor = ThemeColors.Background;
            _metricsGrid.Style.HorizontalScrollBar.ThumbColor = ThemeManager.Colors.TextPrimary;
            _metricsGrid.Style.HorizontalScrollBar.ThumbHoverColor = ThemeManager.Colors.TextPrimary;
            _metricsGrid.Style.HorizontalScrollBar.ScrollBarBackColor = ThemeColors.Background;

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
            if (e.DataRow?.RowData is BudgetCategoryDto category)
            {
                // Style Variance column based on value
                if (e.Column?.MappingName == "Variance")
                {
                    if (category.Variance >= 0)
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
                    var status = category.Status;
                    if (status.Contains("Under", StringComparison.Ordinal))
                    {
                        e.Style.TextColor = PositiveColor;
                        e.Style.Font.Bold = true;
                    }
                    else if (status.Contains("Over", StringComparison.Ordinal))
                    {
                        e.Style.TextColor = NegativeColor;
                        e.Style.Font.Bold = true;
                    }
                    else if (status.Contains("Approaching", StringComparison.Ordinal))
                    {
                        e.Style.TextColor = WarningColor;
                        e.Style.Font.Bold = true;
                    }
                }

                // Style PercentUsed column
                if (e.Column?.MappingName == "PercentUsed")
                {
                    var percentUsed = category.PercentUsed;
                    if (percentUsed > 1.0m) // Over 100%
                    {
                        e.Style.BackColor = Color.FromArgb(40, ThemeColors.Error);
                        e.Style.TextColor = NegativeColor;
                        e.Style.Font.Bold = true;
                    }
                    else if (percentUsed > 0.9m) // 90-100%
                    {
                        e.Style.BackColor = Color.FromArgb(40, ThemeColors.Warning);
                        e.Style.TextColor = WarningColor;
                    }
                    else if (percentUsed > 0.75m) // 75-90%
                    {
                        e.Style.BackColor = Color.FromArgb(40, ThemeColors.Warning);
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
                using var borderPen = new Pen(ThemeManager.Colors.TextPrimary, 1);
                g.DrawRectangle(borderPen, 0, 0, card.Width - 1, card.Height - 1);

                // Draw bottom shadow line
                using var shadowPen = new Pen(Color.FromArgb(15, ThemeManager.Colors.TextPrimary), 2);
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
                ForeColor = ThemeManager.Colors.TextPrimary,
                AutoSize = true,
                Location = new Point(55, 38)
            };
            card.Controls.Add(valueLabel);

            // Subtext for additional context
            var subtextLbl = new Label
            {
                Text = "vs. prior period",
                Font = new Font("Segoe UI", 8),
                ForeColor = ThemeManager.Colors.TextPrimary,
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
                case nameof(BudgetOverviewViewModel.Categories):
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
                var arrow = _viewModel.Variance >= 0 ? "Γû▓" : "Γû╝";
                _varianceValueLabel.Text = $"{arrow} {_varianceValueLabel.Text}";
            }

            if (_percentUsedValueLabel != null)
            {
                var percentUsed = _viewModel.TotalBudget != 0
                    ? ((_viewModel.TotalActual + _viewModel.TotalEncumbrance) / _viewModel.TotalBudget) * 100
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

            // Update metrics grid with actual categories
            if (_metricsGrid != null)
            {
                _metricsGrid.DataSource = _viewModel.Categories;
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
                _refreshButton.Text = _viewModel.IsLoading ? "ΓÅ│ Loading..." : BudgetOverviewFormResources.RefreshButton;
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
                _statusLabel.Text = $"ΓÜá∩╕Å {BudgetOverviewFormResources.ErrorPrefix}{_viewModel.ErrorMessage}";
                _statusLabel.ForeColor = NegativeColor;
            }
        }

        private void ShowError(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = $"ΓÜá∩╕Å {BudgetOverviewFormResources.ErrorPrefix}{message}";
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
                    Filter = "CSV Files|*.csv|Excel Files|*.xlsx|PDF Files|*.pdf",
                    DefaultExt = "csv",
                    FileName = $"BudgetOverview_{DateTime.Now:yyyyMMdd}"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    var extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower(CultureInfo.InvariantCulture);

                    if (extension == ".csv")
                    {
                        ExportToCsv(saveDialog.FileName);
                    }
                    else if (extension == ".xlsx" || extension == ".pdf")
                    {
                        MessageBox.Show($"Export to {extension.ToUpper(CultureInfo.InvariantCulture)} requires Syncfusion.DataGridExport.WinForms package.\n\nPlease run 'dotnet restore' and rebuild to enable Excel/PDF export.\n\nCSV export is available now.",
                            "Export Feature", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    else
                    {
                        throw new NotSupportedException($"Export format {extension} is not supported");
                    }

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

        private void ExportToCsv(string fileName)
        {
            if (_metricsGrid == null || _viewModel.Categories == null)
                throw new InvalidOperationException("Metrics grid or data is not initialized");

            using var writer = new System.IO.StreamWriter(fileName);

            // Write header
            writer.WriteLine("Category,Account Number,Budgeted Amount,Actual Amount,Encumbrance,Variance,Percent Used,Status,Trend");

            // Write data
            foreach (var category in _viewModel.Categories)
            {
                writer.WriteLine($"\"{category.Category}\",\"{category.AccountNumber}\",{category.BudgetedAmount:F2},{category.ActualAmount:F2},{category.EncumbranceAmount:F2},{category.Variance:F2},{category.PercentUsed:P2},\"{category.Status}\",\"{category.Trend}\"");
            }

            _logger.LogInformation("CSV export completed: {FileName}", fileName);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (MdiParent is MainForm mainForm)
            {
                mainForm.RegisterAsDockingMDIChild(this, true);
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
