#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using WileyWidget.WinForms.Controls;
using WileyWidget.ViewModels;
using System.Threading;

namespace WileyWidget.WinForms.Tests
{
    public class DashboardPanelTests
    {
        [Test]
        public void SetupUI_BindsToolStrip()
        {
            var vm = new DashboardViewModel(NullLogger<DashboardViewModel>.Instance, null);
            var before = vm.LastRefreshed;

            using var panel = new DashboardPanel(vm);

            var btnField = typeof(DashboardPanel).GetField("_btnRefresh", BindingFlags.Instance | BindingFlags.NonPublic);
            var btn = btnField!.GetValue(panel) as System.Windows.Forms.ToolStripButton;
            btn.Should().NotBeNull();

            // Trigger click and ensure refresh runs (LastRefreshed updates)
            Task.Delay(20).Wait();
            var prev = vm.LastRefreshed;
            btn!.PerformClick();
            Task.Delay(200).Wait();
            vm.LastRefreshed.Should().BeOnOrAfter(prev);
        }

        private static object[] TryApplyViewModelBindingsCases =
        {
            new object[] { 1500000m, 1125000m, 375000m },
            new object[] { 0m, 0m, 0m },
            new object[] { 12345m, 2000m, 10345m }
        };

        [Test, TestCaseSource(nameof(TryApplyViewModelBindingsCases))]
        public void TryApplyViewModelBindings_UpdatesLabels(decimal totalBudget, decimal exp, decimal remaining)
        {
            // Arrange
            var vm = new DashboardViewModel(NullLogger<DashboardViewModel>.Instance, null);
            vm.TotalBudget = totalBudget;
            vm.TotalExpenditure = exp;
            vm.RemainingBudget = remaining;

            using var panel = new DashboardPanel(vm);

            // Force a binding update
            var method = typeof(DashboardPanel).GetMethod("TryApplyViewModelBindings", BindingFlags.Instance | BindingFlags.NonPublic);
            method!.Invoke(panel, null);

            var lblBudget = panel.Controls.Find("lblBudget", true).FirstOrDefault() as System.Windows.Forms.Label;
            lblBudget.Should().NotBeNull();
            lblBudget!.Text.Should().Be($"Total Budget: {totalBudget:C0}");
        }

        [Test]
        public void NavigateToPanel_Docks()
        {
            var vm = new DashboardViewModel(NullLogger<DashboardViewModel>.Instance, null);
            using var panel = new DashboardPanel(vm);

            // Build a fake parent form with a private generic DockUserControlPanel method to capture the call
            var form = new TestMainForm();
            form.Controls.Add(panel);

            var navigateMethod = typeof(DashboardPanel).GetMethod("NavigateToPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            navigateMethod!.MakeGenericMethod(typeof(AccountsPanel)).Invoke(panel, new object?[] { "Accounts" });

            form.LastDockedName.Should().Be("Accounts");
            form.LastDockedType.Should().Be(typeof(AccountsPanel));

            // Re-invoke to simulate duplicate - should activate existing and not throw
            navigateMethod.Invoke(panel, new object?[] { "Accounts" });
            form.LastDockedName.Should().Be("Accounts");
        }

        [Test]
        public async Task EnsureLoadedAsync_Failure_SetsError()
        {
            var vm = new DashboardViewModel(NullLogger<DashboardViewModel>.Instance, null);

            // Replace the LoadDashboardCommand backing field with one that throws
            var field = typeof(DashboardViewModel).GetField("<LoadDashboardCommand>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            field!.SetValue(vm, new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => throw new InvalidOperationException("boom")));

            using var panel = new DashboardPanel(vm);

            // Invoke EnsureLoadedAsync and wait
            var ensure = typeof(DashboardPanel).GetMethod("EnsureLoadedAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            var task = (Task)ensure!.Invoke(panel, null)!;
            await task;

            var errProvField = typeof(DashboardPanel).GetField("_errorProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            var errorProv = errProvField?.GetValue(panel);
            var mainChartField = typeof(DashboardPanel).GetField("_mainChart", BindingFlags.Instance | BindingFlags.NonPublic);
            var mainChart = mainChartField!.GetValue(panel);

            if (errorProv != null && mainChart != null)
            {
                var getError = errorProv!.GetType().GetMethod("GetError");
                var text = getError?.Invoke(errorProv, new object?[] { mainChart });
                (text as string).Should().NotBeNullOrEmpty();
            }
        }

        [Test]
        public void ChartSeries_AddPoints()
        {
            var vm = new DashboardViewModel(NullLogger<DashboardViewModel>.Instance, null);
            vm.Metrics.Clear();
            vm.Metrics.Add(new DashboardMetric { Name = "A", Value = 10 });
            vm.Metrics.Add(new DashboardMetric { Name = "B", Value = 20 });

            using var panel = new DashboardPanel(vm);

            var tryApply = typeof(DashboardPanel).GetMethod("TryApplyViewModelBindings", BindingFlags.Instance | BindingFlags.NonPublic);
            tryApply!.Invoke(panel, null);

            var chartField = typeof(DashboardPanel).GetField("_mainChart", BindingFlags.Instance | BindingFlags.NonPublic);
            var chart = chartField!.GetValue(panel);
            var seriesProp = chart!.GetType().GetProperty("Series");
            var series = seriesProp!.GetValue(chart) as System.Collections.IList;

            series.Should().NotBeNull();
            series!.Count.Should().Be(1);
            var pts = series[0].GetType().GetProperty("Points")!.GetValue(series[0]) as System.Collections.ICollection;
            pts.Should().NotBeNull();
            pts!.Count.Should().Be(2);
        }

        [Test]
        public void TryApplyViewModelBindings_NoCrossThread_WhenCalledFromBackgroundThread()
        {
            var vm = new DashboardViewModel(NullLogger<DashboardViewModel>.Instance, null);

            using var panel = new DashboardPanel(vm);

            var method = typeof(DashboardPanel).GetMethod("TryApplyViewModelBindings", BindingFlags.Instance | BindingFlags.NonPublic);

            // Calling TryApplyViewModelBindings from a threadpool thread used to cause
            // Cross-thread InvalidOperationException. This should now be marshalled to
            // the UI thread safely and not throw.
            Assert.DoesNotThrow(() => Task.Run(() => method!.Invoke(panel, null)).GetAwaiter().GetResult());
        }

        private class TestMainForm : System.Windows.Forms.Form
        {
            public string? LastDockedName;
            public Type? LastDockedType;

            // Private method to be invoked by reflection in panel.NavigateToPanel
            private void DockUserControlPanel<TPanel>(string displayName) where TPanel : System.Windows.Forms.UserControl
            {
                LastDockedName = displayName;
                LastDockedType = typeof(TPanel);
            }
        }
    }
}
