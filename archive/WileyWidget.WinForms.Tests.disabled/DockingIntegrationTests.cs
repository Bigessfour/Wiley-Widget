using System;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    public class DockingIntegrationTests
    {
        [Fact]
        public void AccountsPanel_HasDataContextProperty()
        {
            var t = typeof(WileyWidget.WinForms.Controls.AccountsPanel);
            var prop = t.GetProperty("DataContext");
            Assert.NotNull(prop);
        }

        [Fact]
        public void ChartPanel_HasDataContextProperty()
        {
            var t = typeof(WileyWidget.WinForms.Controls.ChartPanel);
            var prop = t.GetProperty("DataContext");
            Assert.NotNull(prop);
        }

        [Fact]
        public void SettingsPanel_HasDataContextProperty()
        {
            var t = typeof(WileyWidget.WinForms.Controls.SettingsPanel);
            var prop = t.GetProperty("DataContext");
            Assert.NotNull(prop);
        }
    }
}
