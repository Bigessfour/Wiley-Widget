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
            // Try to get ServiceProvider from MainForm via a method or property
            IServiceProvider? serviceProvider = mainForm as IServiceProvider;
            if (serviceProvider == null)
            {
                var prop = mainForm.GetType().GetProperty("ServiceProvider");
                serviceProvider = prop?.GetValue(mainForm) as IServiceProvider;
            }
            if (serviceProvider != null)
            {
                viewModel = ServiceProviderExtensions.GetService<DashboardViewModel>(serviceProvider);
            }
        }
        catch { /* Suppress; optional data for cards */ }

        try
        {
            logger?.LogInformation("Dashboard panel creation started");

            // Use FlowLayoutPanel for centered, responsive card layout
            var dashboardPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(12, 12, 12, 12),
                BackColor = Color.Transparent,
                // Center items horizontally
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            // Create cards with fixed width for centering
            const int cardWidth = 280;
            const int cardHeight = 80;

            // Card 1: Accounts
            var accountsCard = CreateDashboardCard("Accounts", viewModel?.AccountsSummary ?? "Loading...", cardWidth, cardHeight).Panel;
            SetupCardClickHandler(accountsCard, () =>
            {
                panelNavigator?.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left);
            });

            // Card 2: Charts
            var chartsCard = CreateDashboardCard("Charts", "Analytics Ready", cardWidth, cardHeight).Panel;
            SetupCardClickHandler(chartsCard, () =>
            {
                panelNavigator?.ShowPanel<BudgetAnalyticsPanel>("Budget Analytics", DockingStyle.Right);
            });

            // Card 3: Settings
            var settingsCard = CreateDashboardCard("Settings", "System Config", cardWidth, cardHeight).Panel;
            SetupCardClickHandler(settingsCard, () =>
            {
                panelNavigator?.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right);
            });

            // Card 4: Reports
            var reportsCard = CreateDashboardCard("Reports", "Generate Now", cardWidth, cardHeight).Panel;
            SetupCardClickHandler(reportsCard, () =>
            {
                panelNavigator?.ShowPanel<ReportsPanel>("Reports", DockingStyle.Right);
            });

            // Card 5: Budget Status (Static/Status Display)
            var infoCard = CreateDashboardCard("Budget Status", viewModel?.BudgetStatus ?? "Loading...", cardWidth, cardHeight).Panel;
            SetupCardClickHandler(infoCard, () =>
            {
                panelNavigator?.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom);
            });

            // Add cards to flow layout
            dashboardPanel.Controls.Add(accountsCard);
            dashboardPanel.Controls.Add(chartsCard);
            dashboardPanel.Controls.Add(settingsCard);
            dashboardPanel.Controls.Add(reportsCard);
            dashboardPanel.Controls.Add(infoCard);

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
    private static (GradientPanelExt Panel, Label DescriptionLabel) CreateDashboardCard(string title, string description, int width = 280, int height = 80)
    {
        var panel = new GradientPanelExt
        {
            Dock = DockStyle.None,
            Width = width,
            Height = height,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(4, 4, 4, 8),
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty),
            AutoSize = false
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
