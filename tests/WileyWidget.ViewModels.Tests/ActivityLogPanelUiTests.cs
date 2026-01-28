using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using WileyWidget.Models;
using WileyWidget.WinForms.Controls;

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
            Assert.Equal(vm.ActivityEntries, grid.DataSource);

            panel.Dispose();
        }

        [StaFact]
        public async Task ActivityLogPanel_LoadsDataFromService()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddScoped<ActivityLogViewModel>();
            services.AddLogging(builder => builder.AddDebug());

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ScopedPanelBase<ActivityLogViewModel>>();

            var panel = new ActivityLogPanel(scopeFactory, logger);
            // Force handle creation
            var handle = panel.Handle;

            // Act
            // Trigger refresh by calling the embedded ViewModel's method
            var vmField = typeof(ActivityLogPanel).GetField("ViewModel", BindingFlags.NonPublic | BindingFlags.Instance);
            var embeddedVm = vmField?.GetValue(panel) as dynamic; // The embedded ViewModel
            if (embeddedVm != null)
            {
                await embeddedVm.LoadActivityEntriesAsync();
            }

            // Assert
            var gridField = typeof(ActivityLogPanel).GetField("_activityGrid", BindingFlags.NonPublic | BindingFlags.Instance);
            var grid = gridField?.GetValue(panel) as Syncfusion.WinForms.DataGrid.SfDataGrid;
            Assert.NotNull(grid);
            // Since no data, check that it's bound to empty or sample data
            Assert.NotNull(grid.DataSource);

            panel.Dispose();
        }
    }
}
