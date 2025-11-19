using System;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services;
using WileyWidget.ViewModels;

namespace WileyWidget
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;
        private Window? m_window;

        public App()
        {
            this.InitializeComponent();

            var services = new ServiceCollection();
            services.AddSingleton<QuickBooksService>();
            services.AddSingleton<AILoggingService>();
            services.AddTransient<QuickBooksDashboardViewModel>();
            Services = services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}
