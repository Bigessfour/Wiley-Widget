using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Windows.Forms;
using WileyWidget.Business.Interfaces;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Factory for creating and configuring dashboard card panels.
/// Extracts dashboard creation logic from MainForm for better testability and maintainability.
/// </summary>
public static class DashboardFactory
{
    private const string SegoeUiFontName = "Segoe UI";

    /// <summary>
    /// Create dashboard panel with navigation cards.
    /// </summary>
    /// <param name="panelNavigator">Panel navigation service (nullable - cards are inert until navigator is initialized)</param>
    /// <param name="mainForm">MainForm instance for resource access</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Configured dashboard panel</returns>
    public static Panel CreateDashboardPanel(IPanelNavigationService? panelNavigator, MainForm mainForm, ILogger logger)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            logger?.LogInformation("Dashboard panel creation started");

            var dashboardPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(12, 12, 12, 12),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };

            for (int i = 0; i < 5; i++)
            {
                dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            }

            // Create dashboard cards
            var accountsCard = CreateDashboardCard("Accounts", MainFormResources.LoadingText).Panel;
            SetupCardClickHandler(accountsCard, () =>
            {
                if (panelNavigator != null)
                    panelNavigator.ShowPanel<AccountsPanel>("Municipal Accounts", Syncfusion.Windows.Forms.Tools.DockingStyle.Left, allowFloating: true);
            });

            var chartsCard = CreateDashboardCard("Charts", "Analytics Ready").Panel;
            SetupCardClickHandler(chartsCard, () =>
            {
                if (panelNavigator != null)
                    panelNavigator.ShowPanel<ChartPanel>("Budget Analytics", Syncfusion.Windows.Forms.Tools.DockingStyle.Right, allowFloating: true);
            });

            var settingsCard = CreateDashboardCard("Settings", "System Config").Panel;
            SetupCardClickHandler(settingsCard, () =>
            {
                if (panelNavigator != null)
                    panelNavigator.ShowPanel<SettingsPanel>("Settings", Syncfusion.Windows.Forms.Tools.DockingStyle.Right, allowFloating: true);
            });

            var reportsCard = CreateDashboardCard("Reports", "Generate Now").Panel;
            SetupCardClickHandler(reportsCard, () =>
            {
                if (panelNavigator != null)
                    panelNavigator.ShowPanel<ReportsPanel>("Reports", Syncfusion.Windows.Forms.Tools.DockingStyle.Right, allowFloating: true);
            });

            var budgetStatusCard = CreateDashboardCard("Budget Status", MainFormResources.LoadingText).Panel;

            logger?.LogDebug("Dashboard cards created: Accounts, Charts, Settings, Reports, Budget Status");

            // Add cards to panel
            dashboardPanel.Controls.Add(accountsCard, 0, 0);
            dashboardPanel.Controls.Add(chartsCard, 0, 1);
            dashboardPanel.Controls.Add(settingsCard, 0, 2);
            dashboardPanel.Controls.Add(reportsCard, 0, 3);
            dashboardPanel.Controls.Add(budgetStatusCard, 0, 4);

            stopwatch.Stop();
            logger?.LogInformation("Dashboard panel created successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return dashboardPanel;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger?.LogError(ex, "Dashboard panel creation failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Create a dashboard card with title and description.
    /// </summary>
    /// <param name="title">Card title</param>
    /// <param name="description">Card description</param>
    /// <returns>Tuple of (Panel, DescriptionLabel)</returns>
    private static (Panel Panel, Label DescriptionLabel) CreateDashboardCard(string title, string description)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(4, 4, 4, 8),
            BorderStyle = BorderStyle.FixedSingle
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28
        };

        var descriptionLabel = new Label
        {
            Text = description,
            Dock = DockStyle.Fill
        };

        panel.Controls.Add(descriptionLabel);
        panel.Controls.Add(titleLabel);

        return (panel, descriptionLabel);
    }

    /// <summary>
    /// Setup click handler for a card control and all its children.
    /// </summary>
    /// <param name="card">Card control</param>
    /// <param name="onClick">Click action</param>
    private static void SetupCardClickHandler(Control card, System.Action onClick)
    {
        void Wire(Control control)
        {
            control.Cursor = Cursors.Hand;
            control.Click += (_, _) => onClick();
            foreach (Control child in control.Controls)
            {
                Wire(child);
            }
        }

        Wire(card);
    }
}
