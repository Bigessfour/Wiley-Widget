using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using WileyWidget.WinForms.Services;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.Integration.Tests.Services
{
    public class PanelNavigationServiceTests
    {
        [Fact]
        public void ShowPanel_CachesPanel_WhenDockingManagerUnavailable()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddDebug());
            services.AddSingleton<IPanelNavigationService, PanelNavigationService>();

            var sp = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            var nav = sp.GetRequiredService<IPanelNavigationService>();

            var fakeMain = new FakeMainForm();
            nav.Initialize(fakeMain);

            // Should not throw when docking manager is not initialized
            nav.ShowPanel<TestPanel>("Test Panel Name", DockingStyle.Right, allowFloating: true);

            // Hiding should succeed since panel should be cached
            Assert.True(nav.HidePanel("Test Panel Name"));
        }

        private class FakeMainForm : IMainFormDockingProvider
        {
            public DockingManager GetDockingManager() => throw new InvalidOperationException("DockingManager not initialized (test)");
            public Control GetCentralDocumentPanel() => new Panel();
        }

        private class TestPanel : UserControl { }
    }
}
