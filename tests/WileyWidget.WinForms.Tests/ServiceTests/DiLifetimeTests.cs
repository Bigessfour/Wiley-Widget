using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace WileyWidget.WinForms.Tests.ServiceTests
{
    public class DiLifetimeTests
    {
        [Fact]
        public void SingletonServices_AreSameInstance_TwiceResolve()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var sp = WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(config);

            var s1 = sp.GetService(typeof(WileyWidget.Services.ISettingsService));
            var s2 = sp.GetService(typeof(WileyWidget.Services.ISettingsService));

            Assert.Same(s1, s2);
        }

        [Fact]
        public void Transient_ViewModels_AreDistinctInstances()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var sp = WileyWidget.WinForms.Configuration.DependencyInjection.ConfigureServices(config);

            var v1 = sp.GetService(typeof(WileyWidget.WinForms.ViewModels.ChartViewModel));
            var v2 = sp.GetService(typeof(WileyWidget.WinForms.ViewModels.ChartViewModel));

            Assert.NotSame(v1, v2);
        }
    }
}
