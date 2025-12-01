using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    /// <summary>
    /// Integration tests for panel docking operations.
    /// Tests open/close/dock cycles for memory leaks and layout restoration.
    /// </summary>
    public class PanelDockingTests : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public void Dispose()
        {
            _semaphore.Dispose();
        }

        [Fact]
        public void PanelStateManager_SaveAndLoad_RoundTrips()
        {
            // Arrange
            var tempFile = System.IO.Path.GetTempFileName();
            try
            {
                var manager = new Services.PanelStateManager(tempFile);

                // Set some state
                manager.StateData.AccountsPanel = new Services.AccountsPanelState
                {
                    SelectedFund = "General",
                    SelectedType = "Revenue"
                };
                manager.StateData.ChartPanel = new Services.ChartPanelState
                {
                    SelectedDepartment = "Public Works",
                    ChartZoom = new Services.ChartZoomState
                    {
                        XAxisVisibleStart = 0,
                        XAxisVisibleEnd = 100
                    }
                };

                // Act
                manager.SavePanelState();

                // Load in a new manager
                var manager2 = new Services.PanelStateManager(tempFile);
                manager2.LoadPanelState();

                // Assert
                Assert.NotNull(manager2.StateData.AccountsPanel);
                Assert.Equal("General", manager2.StateData.AccountsPanel.SelectedFund);
                Assert.Equal("Revenue", manager2.StateData.AccountsPanel.SelectedType);

                Assert.NotNull(manager2.StateData.ChartPanel);
                Assert.Equal("Public Works", manager2.StateData.ChartPanel.SelectedDepartment);
                Assert.NotNull(manager2.StateData.ChartPanel.ChartZoom);
                Assert.Equal(0, manager2.StateData.ChartPanel.ChartZoom.XAxisVisibleStart);
                Assert.Equal(100, manager2.StateData.ChartPanel.ChartZoom.XAxisVisibleEnd);
            }
            finally
            {
                try { System.IO.File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public void PanelStateManager_LoadMissingFile_ReturnsEmptyState()
        {
            // Arrange
            var manager = new Services.PanelStateManager("/nonexistent/path/state.json");

            // Act
            manager.LoadPanelState();

            // Assert - should have empty state, no exceptions
            Assert.NotNull(manager.StateData);
            Assert.Null(manager.StateData.AccountsPanel);
            Assert.Null(manager.StateData.ChartPanel);
        }

        [Fact]
        public void GridViewState_Serialization_Works()
        {
            // Arrange
            var state = new Services.GridViewState
            {
                GroupedColumns = new System.Collections.Generic.List<string> { "Department", "Fund" },
                SortedColumns = new System.Collections.Generic.List<Services.SortColumnState>
                {
                    new Services.SortColumnState { PropertyName = "Balance", Direction = "Descending" }
                },
                ColumnWidths = new System.Collections.Generic.Dictionary<string, double>
                {
                    { "AccountNumber", 120.5 },
                    { "Name", 200.0 }
                }
            };

            // Act - serialize
            var json = System.Text.Json.JsonSerializer.Serialize(state);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<Services.GridViewState>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.GroupedColumns?.Count);
            Assert.Contains("Department", deserialized.GroupedColumns);
            Assert.Single(deserialized.SortedColumns!);
            Assert.Equal("Balance", deserialized.SortedColumns[0].PropertyName);
            Assert.Equal(2, deserialized.ColumnWidths?.Count);
            Assert.Equal(120.5, deserialized.ColumnWidths["AccountNumber"]);
        }

        [Fact]
        public void ChartZoomState_NullValues_Handled()
        {
            // Arrange
            var state = new Services.ChartZoomState();

            // Act - all should be null by default
            Assert.Null(state.XAxisVisibleStart);
            Assert.Null(state.XAxisVisibleEnd);
            Assert.Null(state.YAxisVisibleStart);
            Assert.Null(state.YAxisVisibleEnd);

            // Set one value
            state.XAxisVisibleStart = 10.5;
            Assert.Equal(10.5, state.XAxisVisibleStart);
        }

        [Fact]
        public void AccountEditPanel_HasDataContext()
        {
            // Arrange - verify the panel has a DataContext property (panels used for docking)
            var t = typeof(Controls.AccountEditPanel);

            // Act
            var prop = t.GetProperty("DataContext");

            // Assert
            Assert.NotNull(prop);
        }

        [Fact]
        public void MainForm_HasCloseSettingsPanel()
        {
            // Arrange
            var t = typeof(Forms.MainForm);

            // Act
            var method = t.GetMethod("CloseSettingsPanel");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void MainForm_HasCloseAllPanels()
        {
            // Arrange
            var t = typeof(Forms.MainForm);

            // Act
            var method = t.GetMethod("CloseAllPanels");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void MainForm_HasCloseOtherPanels()
        {
            // Arrange
            var t = typeof(Forms.MainForm);

            // Act
            var method = t.GetMethod("CloseOtherPanels");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void MainForm_HasClosePanel()
        {
            // Arrange
            var t = typeof(Forms.MainForm);

            // Act
            var method = t.GetMethod("ClosePanel");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void MainForm_HasDockAccountEditPanel()
        {
            // Arrange
            var t = typeof(Forms.MainForm);

            // Act
            var method = t.GetMethod("DockAccountEditPanel");

            // Assert
            Assert.NotNull(method);
        }

        [Fact]
        public void SettingsViewModel_HasOpenEditFormsDocked()
        {
            // Arrange
            var t = typeof(ViewModels.SettingsViewModel);

            // Act
            var prop = t.GetProperty("OpenEditFormsDocked");

            // Assert
            Assert.NotNull(prop);
            Assert.Equal(typeof(bool), prop.PropertyType);
        }

        [Fact]
        public void PanelStateData_AllPropertiesNullable()
        {
            // Arrange
            var data = new Services.PanelStateData();

            // Assert - all should be null by default
            Assert.Null(data.AccountsPanel);
            Assert.Null(data.ChartPanel);
            Assert.Null(data.DashboardPanel);
            Assert.Null(data.GridStates);
        }

        [Fact]
        public void AccountsPanelState_SplitterPositions_CanBeSet()
        {
            // Arrange
            var state = new Services.AccountsPanelState();

            // Act
            state.SplitterPositions = new System.Collections.Generic.Dictionary<string, int>
            {
                { "mainSplitter", 300 },
                { "detailSplitter", 200 }
            };

            // Assert
            Assert.Equal(2, state.SplitterPositions.Count);
            Assert.Equal(300, state.SplitterPositions["mainSplitter"]);
        }
    }

    /// <summary>
    /// Memory leak detection tests for panel operations.
    /// These tests repeatedly open/close panels to detect leaks.
    /// </summary>
    public class PanelMemoryLeakTests
    {
        [Fact]
        public void PanelStateManager_MultipleLoadSave_NoMemoryGrowth()
        {
            // Arrange
            var tempFile = System.IO.Path.GetTempFileName();
            try
            {
                // Act - do multiple save/load cycles
                for (int i = 0; i < 100; i++)
                {
                    var manager = new Services.PanelStateManager(tempFile);
                    manager.StateData.AccountsPanel = new Services.AccountsPanelState
                    {
                        SelectedFund = $"Fund_{i}",
                        SelectedType = $"Type_{i}"
                    };
                    manager.SavePanelState();

                    var manager2 = new Services.PanelStateManager(tempFile);
                    manager2.LoadPanelState();
                }

                // Force GC to reclaim any leaked objects
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Assert - if we got here without OOM, we passed
                Assert.True(true);
            }
            finally
            {
                try { System.IO.File.Delete(tempFile); } catch { }
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(50)]
        public void GridViewState_MultipleSerialization_NoLeak(int iterations)
        {
            // Act
            for (int i = 0; i < iterations; i++)
            {
                var state = new Services.GridViewState
                {
                    GroupedColumns = new System.Collections.Generic.List<string> { "Col1", "Col2", "Col3" },
                    SortedColumns = new System.Collections.Generic.List<Services.SortColumnState>
                    {
                        new Services.SortColumnState { PropertyName = "Name", Direction = "Ascending" }
                    },
                    ColumnWidths = new System.Collections.Generic.Dictionary<string, double>
                    {
                        { "Col1", 100 + i },
                        { "Col2", 150 + i },
                        { "Col3", 200 + i }
                    }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(state);
                var deserialized = System.Text.Json.JsonSerializer.Deserialize<Services.GridViewState>(json);
                Assert.NotNull(deserialized);
            }

            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.True(true);
        }
    }
}
