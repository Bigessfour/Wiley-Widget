using System;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    public class DockingIntegrationTests
    {
        [Fact]
        public void AccountsForm_HasPrepareForDocking()
        {
            var t = typeof(WileyWidget.WinForms.Forms.AccountsForm);
            var m = t.GetMethod("PrepareForDocking");
            Assert.NotNull(m);
        }

        [Fact]
        public void ChartForm_HasPrepareForDockingAndDataContext()
        {
            var t = typeof(WileyWidget.WinForms.Forms.ChartForm);
            var m = t.GetMethod("PrepareForDocking");
            var prop = t.GetProperty("DataContext");
            Assert.NotNull(m);
            Assert.NotNull(prop);
        }

        [Fact]
        public void SettingsForm_HasPrepareForDockingAndDataContext()
        {
            var t = typeof(WileyWidget.WinForms.Forms.SettingsForm);
            var m = t.GetMethod("PrepareForDocking");
            var prop = t.GetProperty("DataContext");
            Assert.NotNull(m);
            Assert.NotNull(prop);
        }
    }
}
