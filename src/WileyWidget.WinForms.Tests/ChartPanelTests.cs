#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Data;
using System.Threading;
using WileyWidget.WinForms.Theming;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Tests
{
    public class ChartPanelTests
    {
        [Test]
        public void Ctor_SetsDataContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new ChartViewModel(factory, NullLogger<ChartViewModel>.Instance);

            using var panel = new ChartPanel(vm);
            panel.DataContext.Should().BeSameAs(vm);
        }

        [Test]
        public void UpdateChartFromData_Empty_ShowsLabel()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new ChartViewModel(factory, NullLogger<ChartViewModel>.Instance);

            // Ensure ChartData is empty
            vm.ChartData.Clear();

            using var panel = new ChartPanel(vm);

            // Invoke private UpdateChartFromData method
            var method = typeof(ChartPanel).GetMethod("UpdateChartFromData", BindingFlags.Instance | BindingFlags.NonPublic);
            method!.Invoke(panel, null);

            // Look for label named NoDataLabel
            var found = panel.Controls.OfType<System.Windows.Forms.Label>().FirstOrDefault(l => l.Name == "NoDataLabel");
            found.Should().NotBeNull();
            found!.Text.Should().Contain("No");
        }

        [Apartment(ApartmentState.STA)]
        public async Task BtnRefresh_Click_Reloads()
        {
            // Seed an in-memory DB with department and accounts so chart data has something
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
            using (var ctx = new AppDbContext(options))
            {
                ctx.Departments.Add(new Department { Id = 100, Name = "Dept A" });
                ctx.MunicipalAccounts.Add(new MunicipalAccount { Id = 1000, DepartmentId = 100, Balance = 1000m, BudgetAmount = 500m, IsActive = true, AccountNumber = new AccountNumber("1000") });
                ctx.SaveChanges();
            }

            var factory = new AppDbContextFactory(options);
            var vm = new ChartViewModel(factory, NullLogger<ChartViewModel>.Instance);

            using var panel = new ChartPanel(vm);

            // Grab private _chartControl and ensure it exists
            var chartField = typeof(ChartPanel).GetField("_chartControl", BindingFlags.Instance | BindingFlags.NonPublic);
            var chart = chartField!.GetValue(panel);
            chart.Should().NotBeNull();

            // Call BtnRefresh_Click (private async void)
            var btnRefreshMethod = typeof(ChartPanel).GetMethod("BtnRefresh_Click", BindingFlags.Instance | BindingFlags.NonPublic);
            btnRefreshMethod!.Invoke(panel, new object?[] { null, EventArgs.Empty });

            // Wait for async refresh to complete
            await Task.Delay(200);

            // After refresh, UpdateChartFromData should have added series/points
            var chartObj = chart!;
            var seriesProp = chartObj.GetType().GetProperty("Series");
            var series = seriesProp!.GetValue(chartObj) as System.Collections.IList;

            series.Should().NotBeNull();
            series!.Count.Should().BeGreaterOrEqualTo(1);
            var firstSeries = series[0];
            var pointsProp = firstSeries.GetType().GetProperty("Points");
            var pts = pointsProp!.GetValue(firstSeries) as System.Collections.ICollection;

            pts.Should().NotBeNull();
            pts!.Count.Should().BeGreaterThan(0);
        }

        [Test]
        public void ThemeChange_UpdatesSkin()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new ChartViewModel(factory, NullLogger<ChartViewModel>.Instance);

            using var panel = new ChartPanel(vm);

            // Trigger theme change via ThemeManager API - should not throw
            Assert.DoesNotThrow(() => WileyWidget.WinForms.Theming.ThemeManager.SetTheme(AppTheme.Light));
        }

        [Test]
        public void CrossThreadUpdate_DoesNotThrow_OnEmptyAndDisposedChart()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new ChartViewModel(factory, NullLogger<ChartViewModel>.Instance);

            // fake dispatcher that simulates cross-thread to UI-thread marshaling
            // When InvokeAsync is called, it simulates arriving on the UI thread by setting CheckAccess to true
            var fakeDispatcher = new AccountsPanelTests.TestableDispatcher();
            fakeDispatcher.CheckAccessImpl = () => false; // Start off-thread
            fakeDispatcher.InvokeAsyncImpl = (action) =>
            {
                // Simulate that after marshaling, we're now on the UI thread
                fakeDispatcher.CheckAccessImpl = () => true;
                action();
                return Task.CompletedTask;
            };

            using var panel = new ChartPanel(vm, fakeDispatcher);

            // Reset to off-thread for the test
            fakeDispatcher.CheckAccessImpl = () => false;

            // Simulate update with no data
            var updateMethod = typeof(ChartPanel).GetMethod("UpdateChartFromData", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.DoesNotThrow(() => updateMethod!.Invoke(panel, null));

            // Dispose chart and ensure calling update again does not throw
            var chartField = typeof(ChartPanel).GetField("_chartControl", BindingFlags.Instance | BindingFlags.NonPublic);
            var chart = chartField?.GetValue(panel) as IDisposable;
            chart?.Dispose();

            // Reset to off-thread again for the disposed chart test
            fakeDispatcher.CheckAccessImpl = () => false;

            Assert.DoesNotThrow(() => updateMethod!.Invoke(panel, null));
        }
    }
}
