using System;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services;
using WileyWidget.ViewModels;

namespace WileyWidget
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; }

        public App()
        {
            Bootstrap.Initialize(0x00010000);

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<AILoggingService>();
                    services.AddSingleton<QuickBooksService>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            Services = host.Services;

            InitializeComponent();

            var window = Services.GetRequiredService<MainWindow>();
            window.Activate();
        }
    }
}