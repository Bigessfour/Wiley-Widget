using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Services;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Forms;

public class DashboardPanel : UserControl
{
    private readonly IPanelNavigationService _panelNavigator;
    private readonly MainForm _mainForm;
    private readonly ILogger _logger;

    public DashboardPanel(IPanelNavigationService panelNavigator, MainForm mainForm, ILogger<DashboardPanel> logger)
    {
        _panelNavigator = panelNavigator;
        _mainForm = mainForm;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        // Create and add the dashboard content from factory
        var dashboardContent = DashboardFactory.CreateDashboardPanel(_panelNavigator, _mainForm, _logger);
        dashboardContent.Dock = DockStyle.Fill;
        this.Controls.Add(dashboardContent);
        this.ResumeLayout(false);
    }
}
