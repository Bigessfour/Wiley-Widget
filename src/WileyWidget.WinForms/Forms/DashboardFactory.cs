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
using WileyWidget.ViewModels;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Controls.Panels;

using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Forms;
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

            // POLISH: Calculate responsive card widths based on DPI and available space
            var dpi = GetDpiScaling();
            var responsiveCardWidth = CalculateResponsiveCardWidth(dashboardPanel, dpi);
            const int cardHeight = 80;

            // Card 1: Accounts
            var accountsTuple = CreateDashboardCard("Accounts", viewModel != null ? viewModel.AccountsSummary : "Error: Dashboard ViewModel not available", responsiveCardWidth, cardHeight);
            var accountsCard = accountsTuple.Panel;
            var accountsDesc = accountsTuple.DescriptionLabel;
            SetupCardClickHandler(accountsCard, () =>
            {
                panelNavigator?.ShowPanel<AccountsPanel>("Municipal Accounts", DockingStyle.Left);
            });
            if (viewModel != null)
            {
                accountsDesc.DataBindings.Add("Text", viewModel, "AccountsSummary");
                viewModel.LoadCommand.Execute(null);
            }

            // Card 2: Charts
            var chartsCard = CreateDashboardCard("Charts", "Analytics Ready", responsiveCardWidth, cardHeight).Panel;
            SetupCardClickHandler(chartsCard, () =>
            {
                panelNavigator?.ShowPanel<WileyWidget.WinForms.Controls.Panels.AnalyticsHubPanel>("Budget Analytics", DockingStyle.Right);
            });

            // Card 3: Settings
            var settingsCard = CreateDashboardCard("Settings", "System Config", responsiveCardWidth, cardHeight).Panel;
            SetupCardClickHandler(settingsCard, () =>
            {
                panelNavigator?.ShowPanel<SettingsPanel>("Settings", DockingStyle.Right);
            });

            // Card 4: Analytics Hub
            var reportsCard = CreateDashboardCard("Analytics Hub", "Open Hub", responsiveCardWidth, cardHeight).Panel;
            SetupCardClickHandler(reportsCard, () =>
            {
                panelNavigator?.ShowPanel<WileyWidget.WinForms.Controls.Panels.AnalyticsHubPanel>("Analytics Hub", DockingStyle.Right);
            });

            // Card 5: Budget Status (Static/Status Display)
            var infoTuple = CreateDashboardCard("Budget Status", viewModel != null ? viewModel.BudgetStatus : "Error: Dashboard ViewModel not available", responsiveCardWidth, cardHeight);
            var infoCard = infoTuple.Panel;
            var infoDesc = infoTuple.DescriptionLabel;
            SetupCardClickHandler(infoCard, () =>
            {
                panelNavigator?.ShowPanel<BudgetOverviewPanel>("Budget Overview", DockingStyle.Bottom);
            });
            if (viewModel != null)
            {
                infoDesc.DataBindings.Add("Text", viewModel, "BudgetStatus");
            }

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
    /// Create a dashboard as a Form (`BudgetDashboardForm`) to be hosted in a FormHostPanel.
    /// Uses DI to construct the form so constructor dependencies are resolved.
    /// </summary>
    public static BudgetDashboardForm CreateDashboardForm(IPanelNavigationService? panelNavigator, MainForm mainForm, ILogger logger)
    {
        try
        {
            IServiceProvider? serviceProvider = mainForm as IServiceProvider;
            if (serviceProvider == null)
            {
                var prop = mainForm.GetType().GetProperty("ServiceProvider");
                serviceProvider = prop?.GetValue(mainForm) as IServiceProvider;
            }

            if (serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider not available from MainForm - cannot create BudgetDashboardForm via DI");
            }

            var form = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<BudgetDashboardForm>(serviceProvider);
            logger?.LogDebug("Created BudgetDashboardForm via DI");
            return form;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "CreateDashboardForm failed");
            throw;
        }
    }

    /// <summary>
    /// Create a dashboard card with title and description.
    /// Includes full accessibility support for screen readers (WCAG 2.1 Level A).
    /// </summary>
    private static (Panel Panel, Label DescriptionLabel) CreateDashboardCard(string title, string description, int width = 280, int height = 80)
    {
        var panel = new Panel
        {
            Dock = DockStyle.None,
            Width = width,
            Height = height,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(4, 4, 4, 8),
            BorderStyle = BorderStyle.FixedSingle,
            AutoSize = false
        };

        // Accessibility: give controls deterministic names for UI automation
        string safeKey = string.Concat(title.Where(c => !char.IsWhiteSpace(c)));
        panel.Name = $"DashboardCard_{safeKey}";
        panel.AccessibleName = $"Dashboard Card: {title}";  // Descriptive title
        panel.AccessibleRole = AccessibleRole.Grouping;  // Card is a container/group
        panel.AccessibleDescription = $"Click to navigate to {title} panel. {description}";  // Full description

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28,
            Name = panel.Name + "_Title",
            AccessibleName = $"{title} Card Title",  // Descriptive title
            AccessibleRole = AccessibleRole.StaticText,  // Static text role
            Font = new Font(SegoeUiFontName, 12F, FontStyle.Bold)
        };

        var descriptionLabel = new Label
        {
            Text = description,
            Dock = DockStyle.Fill,
            Name = panel.Name + "_Desc",
            AccessibleName = $"{title} Card Description",  // Descriptive label
            AccessibleRole = AccessibleRole.StaticText,  // Static text role
            AccessibleDescription = description  // Full text
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

    /// <summary>
    /// POLISH: Calculate responsive card width based on parent container and DPI scaling.
    /// Cards will be ~30% of parent width for responsive layout, with minimum width constraint.
    /// Adjusts padding for DPI > 100% for consistent spacing across displays.
    /// </summary>
    private static int CalculateResponsiveCardWidth(FlowLayoutPanel dashboardPanel, float dpiScale)
    {
        const int minWidth = 200;  // Minimum width for card readability
        const int maxWidth = 400;  // Maximum width to prevent excessive stretching
        const float responsivePercent = 0.30f;  // Cards occupy ~30% of parent width

        // Calculate responsive width based on parent container width
        int containerWidth = dashboardPanel.ClientSize.Width;
        if (containerWidth <= 0)
        {
            containerWidth = 800;  // Fallback to reasonable default
        }

        int responsiveWidth = (int)(containerWidth * responsivePercent);
        responsiveWidth = Math.Clamp(responsiveWidth, minWidth, maxWidth);

        // Adjust for DPI scaling (add padding for DPI > 100%)
        if (dpiScale > 1.0f)
        {
            responsiveWidth = (int)(responsiveWidth * (1 + (dpiScale - 1.0f) * 0.1f));  // 10% padding per 25% DPI increase
        }

        return responsiveWidth;
    }

    /// <summary>
    /// POLISH: Get DPI scaling factor for responsive layout adjustments.
    /// Used to adjust padding and spacing on high-DPI displays (125%, 150%, 200%).
    /// </summary>
    private static float GetDpiScaling()
    {
        // Detect DPI using Graphics object
        // DpiAwareImageService handles scaling internally; we detect DPI for layout sizing
        try
        {
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                return g.DpiX / 96f;  // 96 DPI = 100% scaling (125% = 1.25, 150% = 1.5, etc.)
            }
        }
        catch
        {
            return 1.0f;  // Default to 100% scaling if detection fails
        }
    }
}
