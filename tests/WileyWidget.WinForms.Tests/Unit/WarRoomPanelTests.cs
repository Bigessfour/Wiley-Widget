using System;
using System.Windows.Forms;
using FluentAssertions;
using Syncfusion.Windows.Forms.Chart;
using Syncfusion.WinForms.DataGrid;
using Xunit;
using Xunit.Sdk;

namespace WileyWidget.WinForms.Tests.Unit
{
    public class WarRoomPanelTests
    {
        [StaFact]
        public void CollectionChange_Should_RenderChartsAndShowResults()
        {
            // Arrange
            var vm = new WileyWidget.WinForms.ViewModels.WarRoomViewModel();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ScopedPanelBase<WarRoomViewModel>>.Instance;
            var scopeFactory = new DummyScopeFactory();

            using var form = new Form();
            using var panel = new WileyWidget.WinForms.Controls.WarRoomPanel(scopeFactory, logger);

            // Simulate adding panel to a visible form so control handles are created
            form.Controls.Add(panel);
            form.CreateControl();
            Application.DoEvents();

            // Manually assign ViewModel and invoke protected OnViewModelResolved via reflection
            var viewModelProp = panel.GetType().GetProperty("ViewModel");
            viewModelProp!.SetValue(panel, vm);
            var onViewModelResolved = panel.GetType().GetMethod("OnViewModelResolved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            onViewModelResolved.Invoke(panel, new object[] { vm });

            // Act - add projection so collection changed handlers fire
            vm.Projections.Add(new WileyWidget.WinForms.ViewModels.ScenarioProjection
            {
                Year = DateTime.Now.Year,
                ProjectedRate = 50m,
                ProjectedRevenue = 10000m,
                ProjectedExpenses = 4000m,
                ProjectedBalance = 6000m,
                ReserveLevel = 18000m
            });

            // Allow UI message pump to process events
            Application.DoEvents();

            // Assert: charts, grid, and results panel visible / populated
            var revenueChart = panel.GetType().GetField("_revenueChart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(panel) as ChartControl;
            revenueChart.Should().NotBeNull();
            revenueChart!.Series.Count.Should().BeGreaterOrEqualTo(1, "Revenue chart should have at least one series after projections added");

            var resultsPanel = panel.GetType().GetField("_resultsPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(panel) as Panel;
            resultsPanel!.Visible.Should().BeTrue("Results panel should be visible when projections exist");

            var projectionsGrid = panel.GetType().GetField("_projectionsGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(panel) as SfDataGrid;
            projectionsGrid!.Visible.Should().BeTrue("Projections grid should be visible when projections exist");

            form.Dispose();
        }

        private class DummyScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope() => new DummyScope();

            private class DummyScope : IServiceScope
            {
                public IServiceProvider ServiceProvider { get; } = new DummyServiceProvider();
                public void Dispose() { }
            }

            private class DummyServiceProvider : IServiceProvider
            {
                public object? GetService(Type serviceType)
                {
                    if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.Logging.ILogger<>))
                    {
                        var loggerType = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(serviceType.GetGenericArguments());
                        var instanceProp = loggerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        return instanceProp?.GetValue(null);
                    }

                    return null;
                }
            }
        }
    }
}
