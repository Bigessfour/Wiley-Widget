using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Controls.Styles;
using Syncfusion.Drawing;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Controls;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;
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
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                BackColor = Color.Transparent
            };

            for (int i = 0; i < 5; i++)
            {
                dashboardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            }

            // Create dashboard cards (keep description label references for later updates)
            var (accountsCard, accountsDesc) = CreateDashboardCard("Accounts", MainFormResources.LoadingText);
            SetupCardClickHandler(accountsCard, () =>
            {
                if (panelNavigator != null)
                    panelNavigator.ShowPanel<AccountsPanel>("Municipal Accounts", Syncfusion.Windows.Forms.Tools.DockingStyle.Left, allowFloating: true);
            });

            var (chartsCard, chartsDesc) = CreateDashboardCard("Charts", "Analytics Ready");
            SetupCardClickHandler(chartsCard, () =>
            {
                if (panelNavigator != null)
                    panelNavigator.ShowPanel<ChartPanel>("Budget Analytics", Syncfusion.Windows.Forms.Tools.DockingStyle.Right, allowFloating: true);
            });

            var (settingsCard, settingsDesc) = CreateDashboardCard("Settings", "System Config");
            SetupCardClickHandler(settingsCard, () =>
            {
                if (panelNavigator != null)
                    panelNavigator.ShowPanel<SettingsPanel>("Settings", Syncfusion.Windows.Forms.Tools.DockingStyle.Right, allowFloating: true);
            });

            var (reportsCard, reportsDesc) = CreateDashboardCard("Reports", "Generate Now");
            SetupCardClickHandler(reportsCard, () =>
            {
                if (panelNavigator != null)
                    panelNavigator.ShowPanel<ReportsPanel>("Reports", Syncfusion.Windows.Forms.Tools.DockingStyle.Right, allowFloating: true);
            });

            var (budgetStatusCard, budgetStatusDesc) = CreateDashboardCard("Budget Status", MainFormResources.LoadingText);

            logger?.LogDebug("Dashboard cards created: Accounts, Charts, Settings, Reports, Budget Status");

            // Add cards to panel
            dashboardPanel.Controls.Add(accountsCard, 0, 0);
            dashboardPanel.Controls.Add(chartsCard, 0, 1);
            dashboardPanel.Controls.Add(settingsCard, 0, 2);
            dashboardPanel.Controls.Add(reportsCard, 0, 3);
            dashboardPanel.Controls.Add(budgetStatusCard, 0, 4);

            // Asynchronously refresh small card summaries using IDashboardService (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    if (mainForm?.ServiceProvider == null)
                    {
                        logger?.LogDebug("DashboardFactory: ServiceProvider unavailable - skipping card data refresh");
                        return;
                    }

                    using var scope = mainForm.ServiceProvider.CreateScope();
                    // Resolve dashboard service as required for dashboard summaries. If missing, outer catch will handle.
                    var dashSvc = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IDashboardService>(scope.ServiceProvider);

                    var items = (await dashSvc.GetDashboardItemsAsync().ConfigureAwait(false))?.ToList() ?? new System.Collections.Generic.List<DashboardItem>();

                    var accountsItem = items.FirstOrDefault(i => string.Equals(i.Category, "Accounts", System.StringComparison.OrdinalIgnoreCase) || (i.Title != null && i.Title.IndexOf("Account", System.StringComparison.OrdinalIgnoreCase) >= 0));
                    var budgetItem = items.FirstOrDefault(i => i.Title != null && i.Title.IndexOf("Total Budget", System.StringComparison.OrdinalIgnoreCase) >= 0) ?? items.FirstOrDefault(i => string.Equals(i.Category, "Budget", System.StringComparison.OrdinalIgnoreCase));
                    var varianceItem = items.FirstOrDefault(i => i.Title != null && i.Title.IndexOf("Variance", System.StringComparison.OrdinalIgnoreCase) >= 0);
                    var activityCount = items.Count(i => string.Equals(i.Category, "Activity", System.StringComparison.OrdinalIgnoreCase));

                    void UpdateUi()
                    {
                        try
                        {
                            if (!dashboardPanel.IsDisposed)
                            {
                                if (accountsDesc != null)
                                    accountsDesc.Text = accountsItem != null ? $"{accountsItem.Value} — {accountsItem.Description}" : MainFormResources.LoadingText;

                                if (chartsDesc != null)
                                    chartsDesc.Text = budgetItem != null ? $"Updated: {System.DateTime.Now:g}" : "No chart data";

                                if (settingsDesc != null)
                                    settingsDesc.Text = "Configured";

                                if (reportsDesc != null)
                                    reportsDesc.Text = activityCount > 0 ? $"{activityCount} recent activities" : "No recent activity";

                                if (budgetStatusDesc != null)
                                    budgetStatusDesc.Text = varianceItem != null ? $"{varianceItem.Value} — {varianceItem.Description}" : (budgetItem != null ? $"{budgetItem.Value} — {budgetItem.Description}" : MainFormResources.LoadingText);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            logger?.LogDebug(ex, "DashboardFactory: Failed to update card UI");
                        }
                    }

                    if (dashboardPanel.IsHandleCreated && dashboardPanel.InvokeRequired)
                        dashboardPanel.BeginInvoke((System.Action)UpdateUi);
                    else
                        UpdateUi();
                }
                catch (System.Exception ex)
                {
                    logger?.LogDebug(ex, "DashboardFactory: Failed to refresh dashboard cards asynchronously");

                    // Ensure UI doesn't remain stuck on "Loading..." - set friendly fallback text
                    void SetUnavailable()
                    {
                        try
                        {
                            if (!dashboardPanel.IsDisposed)
                            {
                                accountsDesc?.Text = "Unavailable";
                                chartsDesc?.Text = "Unavailable";
                                settingsDesc?.Text = "Unavailable";
                                reportsDesc?.Text = "Unavailable";
                                budgetStatusDesc?.Text = "Unavailable";
                            }
                        }
                        catch (System.Exception inner)
                        {
                            logger?.LogDebug(inner, "DashboardFactory: Failed to set unavailable fallback text");
                        }
                    }

                    if (dashboardPanel.IsHandleCreated && dashboardPanel.InvokeRequired)
                        dashboardPanel.BeginInvoke((System.Action)SetUnavailable);
                    else
                        SetUnavailable();
                }
            });

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
    private static (GradientPanelExt Panel, Label DescriptionLabel) CreateDashboardCard(string title, string description)
    {
        GradientPanelExt panel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(4, 4, 4, 8),
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty)
        };

        // Accessibility: give controls deterministic names for UI automation
        string safeKey = string.Concat(title.Where(c => !char.IsWhiteSpace(c)))
            .Replace("\"", string.Empty, System.StringComparison.Ordinal)
            .Replace("'", string.Empty, System.StringComparison.Ordinal);
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
