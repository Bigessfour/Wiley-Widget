using System;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.ViewModels.Tests
{
    public class ActivityLogPanelUiTests
    {
        [StaFact]
        public void ActivityLogPanel_ResolvesViewModel_And_BindsGrid()
        {
            var services = new ServiceCollection();
            services.AddScoped<ActivityLogViewModel>();
            services.AddLogging(builder => builder.AddDebug());

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ScopedPanelBase<ActivityLogViewModel>>();

            var panel = new ActivityLogPanel(scopeFactory, logger);
            // Force handle creation which triggers ScopedPanelBase.OnHandleCreated
            var handle = panel.Handle;

            Assert.NotNull(panel.ViewModel);
            var vm = panel.ViewModel!;
            Assert.IsType<ActivityLogViewModel>(vm);

            var gridField = typeof(ActivityLogPanel).GetField("_activityGrid", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(gridField);
            var grid = gridField.GetValue(panel) as Syncfusion.WinForms.DataGrid.SfDataGrid;
            Assert.NotNull(grid);
            Assert.Same(vm.ActivityEntries, grid.DataSource);

            panel.Dispose();
        }
    }
}
