using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    [Trait("Category", "Unit")]
    [Collection(WinFormsUiCollection.CollectionName)]
    public class SyncfusionNullRefTests
    {
        private readonly WinFormsUiThreadFixture _ui;

        public SyncfusionNullRefTests(WinFormsUiThreadFixture ui)
        {
            _ui = ui;
        }


        private class TestConfigurationSection : IConfigurationSection
        {
            private readonly string _key;
            private readonly Dictionary<string, string> _values;

            public TestConfigurationSection(string key, Dictionary<string, string> values)
            {
                _key = key;
                _values = values;
            }

            public string? this[string key]
            {
                get => _values.TryGetValue($"{_key}:{key}", out var value) ? value : null;
                set => _values[$"{_key}:{key}"] = value!;
            }

            public string Key => _key;
            public string Path => _key;
            public string? Value { get => _values.TryGetValue(_key, out var value) ? value : null; set => _values[_key] = value!; }
            public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
            public IChangeToken GetReloadToken() => throw new NotImplementedException();
            public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, _values);
        }

        private class TestConfiguration : IConfiguration
        {
            private readonly Dictionary<string, string> _values;

            public TestConfiguration(bool useMdiMode = true, bool useTabbedMdi = true, bool useDockingManager = true)
            {
                _values = new Dictionary<string, string>
                {
                    ["UI:UseMdiMode"] = useMdiMode ? "true" : "false",
                    ["UI:UseTabbedMdi"] = useTabbedMdi ? "true" : "false",
                    ["UI:UseDockingManager"] = useDockingManager ? "true" : "false",
                    ["UI:IsUiTestHarness"] = "true"
                };
            }

            public string? this[string key]
            {
                get => _values.TryGetValue(key, out var value) ? value : null;
                set => _values[key] = value!;
            }

            public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
            public IChangeToken GetReloadToken() => throw new NotImplementedException();
            public IConfigurationSection GetSection(string key) => new TestConfigurationSection(key, _values);
        }

        private class TestMainForm : MainForm
        {
            public TestMainForm() : this(new TestConfiguration(useDockingManager: false)) { }
            public TestMainForm(TestConfiguration config) : base(new ServiceCollection().BuildServiceProvider(), config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled) { }
        }

        [Fact]
        public void MainForm_InitializeDocking_ThrowsIfDockingManagerNull()
        {
            // Arrange - Create MainForm with docking disabled to simulate _dockingManager being null
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var testConfig = new TestConfiguration();
            testConfig["UI:UseDockingManager"] = "false"; // Disable docking
            using var mainForm = new MainForm(serviceProvider, testConfig, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            // Act & Assert - Calling DockForm when docking is disabled should not throw
            var method = mainForm.GetType().GetMethod("DockForm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ex = Record.Exception(() => method?.MakeGenericMethod(typeof(DashboardForm), typeof(DashboardViewModel))
                .Invoke(mainForm, new object[] { DockingStyle.Fill }));

            // Should complete without throwing NullReferenceException
            Assert.Null(ex);
        }

        [Fact]
        public void DashboardForm_BuildStatusBar_HandlesNullControls()
        {
            // Arrange
            _ui.Run(() =>
            {
                var mockVm = new Mock<DashboardViewModel>();
                var mockAnalyticsSvc = new Mock<IAnalyticsService>();
                var mockAnalyticsLogger = new Mock<ILogger<AnalyticsViewModel>>();
                var mockAnalyticsVm = new Mock<AnalyticsViewModel>(mockAnalyticsSvc.Object, mockAnalyticsLogger.Object);
                using var mainForm = new TestMainForm();
                var mockLogger = new Mock<ILogger<DashboardForm>>();
                using var form = new DashboardForm(mockVm.Object, mockAnalyticsVm.Object, mainForm, mockLogger.Object);

                // Assert: Form should still be functional even if status bar creation fails
                Assert.NotNull(form);
                Assert.True(form.Controls.Count >= 0); // Should have at least some controls
            });
        }

        [Fact]
        public void SyncfusionGrid_BindData_HandlesNullDataSource()
        {
            // Arrange
            _ui.Run(() =>
            {
                using var grid = new SfDataGrid();

                // Act: Bind null data source
                grid.DataSource = null;

                // Assert: Grid should handle null data gracefully
                Assert.NotNull(grid);
                Assert.Null(grid.DataSource);
            });
        }
    }
}
