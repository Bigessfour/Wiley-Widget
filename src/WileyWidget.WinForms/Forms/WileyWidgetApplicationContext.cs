using System;
using System.Windows.Forms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Minimal ApplicationContext implementation used by Program.Main to host the UI.
    /// Responsible for creating the MainForm and managing application lifetime.
    /// </summary>
    public sealed class WileyWidgetApplicationContext : ApplicationContext
    {
        private readonly WileyWidget.Services.IStartupTimelineService? _timelineService;

        public WileyWidgetApplicationContext(IServiceProvider services, WileyWidget.Services.IStartupTimelineService? timelineService, IHost? host)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Host = host;
            _timelineService = timelineService;

            // Resolve MainForm from DI if available, otherwise construct using minimal dependencies
            MainForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<MainForm>(services)
                ?? new MainForm(
                    services,
                    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(services) ?? new ConfigurationBuilder().Build(),
                    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<MainForm>>(services) ?? NullLogger<MainForm>.Instance,
                    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ReportViewerLaunchOptions>(services) ?? ReportViewerLaunchOptions.Disabled,
                    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IPanelNavigationService>(services));

            // When MainForm closes, exit the UI thread
            MainForm.FormClosed += (s, e) => ExitThread();
        }

        public IServiceProvider Services { get; }

        public IHost? Host { get; }
    }
}
