using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ServiceProviderExtensions = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.Drawing;
using WileyWidget.ViewModels;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Forms;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;
using Action = System.Action;  // Disambiguate from Syncfusion.Windows.Forms.Tools.Action

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
    /// <param name="panelNavigator">Panel navigation service for orchestration.</param>
    /// <param name="mainForm">MainForm instance for service resolution.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Configured dashboard panel.</returns>
    public static Panel CreateDashboardPanel(IPanelNavigationService? panelNavigator, MainForm mainForm, ILogger logger)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // ViewModel integration - assume DashboardViewModel is available via DI in MainForm
        DashboardViewModel? viewModel = null;
        try
        {
            if (mainForm?.ServiceProvider != null)
            {
                viewModel = ServiceProviderExtensions.GetService<DashboardViewModel>(mainForm.ServiceProvider);
            }
        }
        catch { /* Suppress; optional data for cards */ }

        try
        {
            logger?.LogInformation("Dashboard panel creation started");

            var dashboardPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(12, 12, 12, 12),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                BackColor = Color.Transparent
            };

            for (int i = 0; i < 5; i++)
            {
                dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            }

            // Card 1: Accounts
            var accountsCard = CreateDashboardCard("Accounts", viewModel?.AccountsSummary ?? MainFormResources.LoadingText).Panel;
            SetupCardClickHandler(accountsCard, () => {
                panelNavigator?.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left);
            });

            // Card 2: Charts
            var chartsCard = CreateDashboardCard("Charts", "Analytics Ready").Panel;
            SetupCardClickHandler(chartsCard, () => {
                panelNavigator?.ShowPanel<ChartPanel>("Budget Analytics", DockingStyle.Right);
            });

            // Card 3: Settings
            var settingsCard = CreateDashboardCard("Settings", "System Config").Panel;
            SetupCardClickHandler(settingsCard, () => {
                panelNavigator?.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right);
            });

            // Card 4: Reports
            var reportsCard = CreateDashboardCard("Reports", "Generate Now").Panel;
            SetupCardClickHandler(reportsCard, () => {
                panelNavigator?.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right);
            });

            // Card 5: Budget Status (Static/Status Display)
            var infoCard = CreateDashboardCard("Budget Status", viewModel?.BudgetStatus ?? MainFormResources.LoadingText).Panel;
            SetupCardClickHandler(infoCard, () => {
                panelNavigator?.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom);
            });

            dashboardPanel.Controls.Add(accountsCard, 0, 0);
            dashboardPanel.Controls.Add(chartsCard, 0, 1);
            dashboardPanel.Controls.Add(settingsCard, 0, 2);
            dashboardPanel.Controls.Add(reportsCard, 0, 3);
            dashboardPanel.Controls.Add(infoCard, 0, 4);

            stopwatch.Stop();
            logger?.LogInformation("Dashboard panel created successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return dashboardPanel;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger?.LogError(ex, "Dashboard panel creation failed");
            throw;
        }
    }

    /// <summary>
    /// Create a dashboard card with title and description.
    /// </summary>
    private static (GradientPanelExt Panel, Label DescriptionLabel) CreateDashboardCard(string title, string description)
    {
        var panel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(4, 4, 4, 8),
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };

        // Accessibility: give controls deterministic names for UI automation
        string safeKey = string.Concat(title.Where(c => !char.IsWhiteSpace(c)));
        panel.Name = $"DashboardCard_{safeKey}";
        panel.AccessibleName = panel.Name;

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28,
            Name = panel.Name + "_Title",
            AccessibleName = panel.Name + "_Title"
        };

        var descriptionLabel = new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            Name = panel.Name + "_Desc",
            AccessibleName = panel.Name + "_Desc"
        };

        panel.Controls.Add(descriptionLabel);
        panel.Controls.Add(titleLabel);

        return (panel, descriptionLabel);
    }

    /// <summary>
    /// </summary>
    private static void SetupCardClickHandler(Control card, System.Action onClick)
    {
        void Wire(Control control)
        {
            control.Cursor = Cursors.Hand;
            control.Click += (s, e) => onClick();
            foreach (Control child in control.Controls)
            {
                Wire(child);
            }
        }
        Wire(card);
    }
}
