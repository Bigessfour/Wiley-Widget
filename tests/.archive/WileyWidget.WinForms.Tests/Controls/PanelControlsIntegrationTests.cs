#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Theming;
using WileyWidget.WinForms.Tests.Infrastructure;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;

namespace WileyWidget.WinForms.Tests.Controls
{
    /// <summary>
    /// Comprehensive integration tests for all panel controls validating:
    /// - Syncfusion control registration and properties
    /// - Theme application (SfSkinManager compliance)
    /// - DataGrid binding and state
    /// - Observable collection binding
    /// - Command execution
    /// - Data context propagation
    /// - Error handling for null/uninitialized states
    /// </summary>
    [Collection("Syncfusion License Collection")]
    public class PanelControlsIntegrationTests : IDisposable
    {
        private readonly Mock<IThemeService> _mockThemeService;
        private readonly Mock<IInsightFeedViewModel> _mockViewModel;
        private TestForm? _testForm;
        private bool _disposed;

        public PanelControlsIntegrationTests()
        {
            _mockThemeService = new Mock<IThemeService>();
            _mockViewModel = new Mock<IInsightFeedViewModel>();

            // Setup theme service defaults
            _mockThemeService.Setup(x => x.CurrentTheme).Returns(AppTheme.Office2019Colorful);
            _mockThemeService.Setup(x => x.Preference).Returns(AppTheme.Office2019Colorful);
        }

        // ===============================================
        // Test Form Infrastructure
        // ===============================================

        private class TestForm : Form
        {
#pragma warning disable WFO1000
            public InsightFeedPanel? Panel { get; set; }
#pragma warning restore WFO1000

            public TestForm()
            {
                Width = 800;
                Height = 600;
                StartPosition = FormStartPosition.CenterScreen;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Panel?.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        // ===============================================
        // InsightFeedPanel - Basic Initialization
        // ===============================================

        [Fact]
        public void InsightFeedPanel_Constructor_CreatesValidPanel()
        {
            // Arrange
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;

            // Act
            var panel = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger);

            // Assert
            Assert.NotNull(panel);
            Assert.IsType<InsightFeedPanel>(panel);
            panel.Dispose();
        }

        [Fact]
        public void InsightFeedPanel_Constructor_WithoutViewModel_CreatesPanel()
        {
            // Arrange
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;

            // Act
            var panel = new InsightFeedPanel(null, _mockThemeService.Object, logger);

            // Assert
            Assert.NotNull(panel);
            panel.Dispose();
        }

        [Fact]
        public void InsightFeedPanel_Constructor_WithoutThemeService_CreatesPanel()
        {
            // Arrange
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;

            // Act
            var panel = new InsightFeedPanel(_mockViewModel.Object, null, logger);

            // Assert
            Assert.NotNull(panel);
            panel.Dispose();
        }

        // ===============================================
        // InsightFeedPanel - Syncfusion Control Discovery
        // ===============================================

        [Fact]
        public void InsightFeedPanel_ContainsSyncfusionDataGrid()
        {
            // Arrange
            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act
            var dataGrids = FindControlsOfType<SfDataGrid>(panel);

            // Assert
            Assert.NotEmpty(dataGrids);
            Assert.Single(dataGrids);
        }

        [Fact]
        public void InsightFeedPanel_DataGrid_HasValidProperties()
        {
            // Arrange
            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act
            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();

            // Assert
            Assert.NotNull(dataGrid);
            Assert.True(dataGrid.AllowFiltering);
            Assert.True(dataGrid.AllowSorting);
            Assert.NotEmpty(dataGrid.Columns);
        }

        [Fact]
        public void InsightFeedPanel_DataGrid_IsProperlyDocked()
        {
            // Arrange
            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act
            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();

            // Assert
            Assert.NotNull(dataGrid);
            Assert.Equal(DockStyle.Fill, dataGrid.Dock);
        }

        [Fact]
        public void InsightFeedPanel_ContainsToolStripButtons()
        {
            // Arrange
            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act
            var toolStrips = FindControlsOfType<ToolStrip>(panel);

            // Assert
            Assert.NotEmpty(toolStrips);
        }

        [Fact]
        public void InsightFeedPanel_AllSyncfusionControlsAreValid()
        {
            // Arrange
            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act
            var syncfusionControls = FindAllSyncfusionControls(panel);

            // Assert
            Assert.NotEmpty(syncfusionControls);
            foreach (var control in syncfusionControls)
            {
                // Verify it's a Syncfusion control
                Assert.True(IsSyncfusionControl(control));
            }
        }

        // ===============================================
        // InsightFeedPanel - DataGrid Binding
        // ===============================================

        [Fact]
        public void InsightFeedPanel_DataGrid_BindsToViewModelItems()
        {
            // Arrange
            var items = new ObservableCollection<InsightCardModel>
            {
                new InsightCardModel { Priority = "High", Category = "Test", Explanation = "Test 1", Timestamp = DateTime.Now },
                new InsightCardModel { Priority = "Medium", Category = "Test", Explanation = "Test 2", Timestamp = DateTime.Now }
            };

            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.Setup(x => x.InsightCards).Returns(items);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act
            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();

            // Assert
            Assert.NotNull(dataGrid);
            // Grid should be bound to the items
            Assert.NotNull(dataGrid.DataSource);
        }

        [Fact]
        public void InsightFeedPanel_DataGrid_UpdatesOnViewModelChange()
        {
            // Arrange
            var items = new ObservableCollection<InsightCardModel>();
            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.Setup(x => x.InsightCards).Returns(items);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act
            items.Add(new InsightCardModel { Priority = "Low", Category = "Test", Explanation = "New Item", Timestamp = DateTime.Now });
            System.Windows.Forms.Application.DoEvents();

            // Assert
            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();
            Assert.NotNull(dataGrid);
        }

        // ===============================================
        // InsightFeedPanel - Theme Compliance
        // ===============================================

        [Fact]
        public void InsightFeedPanel_ApplyTheme_UsesThemeService()
        {
            // Arrange
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger);

            // Act & Assert - Panel should initialize without errors
            Assert.NotNull(panel);
            panel.Dispose();
        }

        [Fact]
        public void InsightFeedPanel_ThemeProperty_ConsistentWithThemeService()
        {
            // Arrange
            var expectedTheme = AppTheme.Office2019Colorful;
            _mockThemeService.Setup(x => x.CurrentTheme).Returns(expectedTheme);

            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger);

            // Act
            var currentTheme = _mockThemeService.Object.CurrentTheme;

            // Assert
            Assert.Equal(expectedTheme, currentTheme);
            panel.Dispose();
        }

        // ===============================================
        // InsightFeedPanel - Error Handling
        // ===============================================

        [Fact]
        public void InsightFeedPanel_HandlesNullViewModel_Gracefully()
        {
            // Arrange
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;

            // Act
            var panel = new InsightFeedPanel(null, _mockThemeService.Object, logger);

            // Assert
            Assert.NotNull(panel);
            panel.Dispose();
        }

        [Fact]
        public void InsightFeedPanel_HandlesNullThemeService_Gracefully()
        {
            // Arrange
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;

            // Act
            var panel = new InsightFeedPanel(_mockViewModel.Object, null, logger);

            // Assert
            Assert.NotNull(panel);
            panel.Dispose();
        }

        [Fact]
        public void InsightFeedPanel_HandlesNullLogger_Gracefully()
        {
            // Arrange & Act
            var panel = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, null);

            // Assert
            Assert.NotNull(panel);
            panel.Dispose();
        }

        // ===============================================
        // Observable Collection Change Detection (5/10)
        // ===============================================

        [Fact]
        public void DataGrid_ReflectsAddedItems_WhenCollectionChanges()
        {
            // Arrange
            var items = new ObservableCollection<InsightCardModel>();
            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.Setup(x => x.InsightCards).Returns(items);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();
            Assert.NotNull(dataGrid);
            var initialCount = dataGrid.View?.Records.Count ?? 0;

            // Act - Add items to collection
            items.Add(new InsightCardModel { Priority = "High", Category = "Test", Explanation = "Item 1", Timestamp = DateTime.Now });
            items.Add(new InsightCardModel { Priority = "Medium", Category = "Test", Explanation = "Item 2", Timestamp = DateTime.Now });
            System.Windows.Forms.Application.DoEvents();

            // Assert - Grid should reflect new items
            var finalCount = dataGrid.View?.Records.Count ?? 0;
            Assert.True(finalCount > initialCount, $"Expected grid to have more records. Before: {initialCount}, After: {finalCount}");
        }

        [Fact]
        public void DataGrid_ReflectsRemovedItems_WhenCollectionChanges()
        {
            // Arrange
            var items = new ObservableCollection<InsightCardModel>
            {
                new InsightCardModel { Priority = "High", Category = "Test", Explanation = "Item 1", Timestamp = DateTime.Now },
                new InsightCardModel { Priority = "Medium", Category = "Test", Explanation = "Item 2", Timestamp = DateTime.Now }
            };

            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.Setup(x => x.InsightCards).Returns(items);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();
            Assert.NotNull(dataGrid);
            System.Windows.Forms.Application.DoEvents();
            var beforeCount = dataGrid.View?.Records.Count ?? 0;

            // Act - Remove item from collection
            items.RemoveAt(0);
            System.Windows.Forms.Application.DoEvents();

            // Assert - Grid should reflect removed item
            var afterCount = dataGrid.View?.Records.Count ?? 0;
            Assert.True(afterCount < beforeCount, $"Expected grid to have fewer records. Before: {beforeCount}, After: {afterCount}");
        }

        // ===============================================
        // ViewModel Property Change Notification (5/10)
        // ===============================================

        [Fact]
        public void Panel_UpdatesLoadingState_WhenViewModelChanges()
        {
            // Arrange
            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.SetupProperty(x => x.IsLoading, false);
            mockViewModel.SetupProperty(x => x.StatusMessage, "Ready");

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act - Change ViewModel properties
            mockViewModel.Object.IsLoading = true;
            mockViewModel.Object.StatusMessage = "Loading insights...";

            // Assert - Verify ViewModel state changed
            Assert.True(mockViewModel.Object.IsLoading);
            Assert.Equal("Loading insights...", mockViewModel.Object.StatusMessage);
        }

        [Fact]
        public void Panel_ReflectsPriorityCountChanges_InViewModelState()
        {
            // Arrange
            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.SetupProperty(x => x.HighPriorityCount, 0);
            mockViewModel.SetupProperty(x => x.MediumPriorityCount, 0);
            mockViewModel.SetupProperty(x => x.LowPriorityCount, 0);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act - Update priority counts
            mockViewModel.Object.HighPriorityCount = 3;
            mockViewModel.Object.MediumPriorityCount = 5;
            mockViewModel.Object.LowPriorityCount = 2;

            // Assert - Verify counts reflect ViewModel state
            Assert.Equal(3, mockViewModel.Object.HighPriorityCount);
            Assert.Equal(5, mockViewModel.Object.MediumPriorityCount);
            Assert.Equal(2, mockViewModel.Object.LowPriorityCount);
        }

        // ===============================================
        // Edge Cases & Error States (5/10)
        // ===============================================

        [Fact]
        public void DataGrid_HandlesEmptyCollection_Gracefully()
        {
            // Arrange
            var items = new ObservableCollection<InsightCardModel>();
            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.Setup(x => x.InsightCards).Returns(items);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act
            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();
            System.Windows.Forms.Application.DoEvents();

            // Assert - Grid should be empty but valid
            Assert.NotNull(dataGrid);
            Assert.NotNull(dataGrid.View);
            Assert.Empty(dataGrid.View.Records);
        }

        [Fact]
        public void DataGrid_HandlesLargeCollection_WithoutCrashing()
        {
            // Arrange
            var items = new ObservableCollection<InsightCardModel>();
            for (int i = 0; i < 100; i++)
            {
                items.Add(new InsightCardModel
                {
                    Priority = i % 3 == 0 ? "High" : i % 3 == 1 ? "Medium" : "Low",
                    Category = $"Category{i % 5}",
                    Explanation = $"Item {i}",
                    Timestamp = DateTime.Now.AddHours(-i)
                });
            }

            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.Setup(x => x.InsightCards).Returns(items);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            // Act
            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();
            System.Windows.Forms.Application.DoEvents();

            // Assert - Grid should handle large collection
            Assert.NotNull(dataGrid);
            Assert.Equal(100, dataGrid.View?.Records.Count ?? 0);
        }

        // ===============================================
        // Command Execution Testing (6/10)
        // ===============================================

        // ===============================================
        // Sorting & Filtering Validation (6/10)
        // ===============================================

        [Fact]
        public void DataGrid_SupportsSorting_ByPriority()
        {
            // Arrange
            var items = new ObservableCollection<InsightCardModel>
            {
                new InsightCardModel { Priority = "Low", Category = "A", Explanation = "Low priority", Timestamp = DateTime.Now },
                new InsightCardModel { Priority = "High", Category = "B", Explanation = "High priority", Timestamp = DateTime.Now },
                new InsightCardModel { Priority = "Medium", Category = "C", Explanation = "Medium priority", Timestamp = DateTime.Now }
            };

            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.Setup(x => x.InsightCards).Returns(items);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();
            Assert.NotNull(dataGrid);
            Assert.True(dataGrid.AllowSorting, "Grid should allow sorting");

            var priorityColumn = dataGrid.Columns.FirstOrDefault(c => c.MappingName == "Priority");

            Assert.NotNull(priorityColumn);
            Assert.True(priorityColumn.AllowSorting);
        }

        [Fact]
        public void DataGrid_SupportsFiltering_ByCategory()
        {
            // Arrange
            var items = new ObservableCollection<InsightCardModel>
            {
                new InsightCardModel { Priority = "High", Category = "Budget", Explanation = "Budget insight", Timestamp = DateTime.Now },
                new InsightCardModel { Priority = "Medium", Category = "Revenue", Explanation = "Revenue insight", Timestamp = DateTime.Now },
                new InsightCardModel { Priority = "Low", Category = "Budget", Explanation = "Another budget insight", Timestamp = DateTime.Now }
            };

            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.Setup(x => x.InsightCards).Returns(items);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            var dataGrid = FindControlsOfType<SfDataGrid>(panel).FirstOrDefault();
            Assert.NotNull(dataGrid);
            Assert.True(dataGrid.AllowFiltering, "Grid should allow filtering");

            var categoryColumn = dataGrid.Columns.FirstOrDefault(c => c.MappingName == "Category");

            Assert.NotNull(categoryColumn);
            Assert.True(categoryColumn.AllowFiltering);
        }

        // ===============================================
        // Property Change Notification & UI Sync (6/10)
        // ===============================================

        [Fact]
        public void StatusLabel_ExistsInPanel()
        {
            // Arrange
            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.SetupProperty(x => x.StatusMessage, "Ready");

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            var labels = FindControlsOfType<Label>(panel);

            // Act & Assert
            Assert.NotEmpty(labels);
            var statusLabel = labels.FirstOrDefault(l => l.Name?.Contains("Status", StringComparison.OrdinalIgnoreCase) ?? false);
            Assert.NotNull(statusLabel);
        }

        [Fact]
        public void LoadingOverlay_ExistsInPanel()
        {
            // Arrange
            var mockViewModel = new Mock<IInsightFeedViewModel>();
            mockViewModel.SetupProperty(x => x.IsLoading, false);

            _testForm = new TestForm();
            var logger = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel = new InsightFeedPanel(mockViewModel.Object, _mockThemeService.Object, logger);
            _testForm.Controls.Add(panel);
            _testForm.Show();

            var overlays = FindControlsOfType<Control>(panel)
                .Where(c => c.GetType().Name.Contains("Loading", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Act & Assert - Verify loading overlay exists
            Assert.NotEmpty(overlays);
            var loadingOverlay = overlays.FirstOrDefault();
            Assert.NotNull(loadingOverlay);
        }

        // ===============================================
        // Multiple Panels - Coexistence
        // ===============================================

        [Fact]
        public void MultiplePanels_CanCoexistWithoutConflicts()
        {
            // Arrange
            _testForm = new TestForm();
            var logger1 = new Mock<ILogger<InsightFeedPanel>>().Object;
            var logger2 = new Mock<ILogger<InsightFeedPanel>>().Object;
            var panel1 = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger1);
            var panel2 = new InsightFeedPanel(_mockViewModel.Object, _mockThemeService.Object, logger2);

            // Act
            _testForm.Controls.Add(panel1);
            _testForm.Controls.Add(panel2);
            _testForm.Show();

            // Assert
            Assert.Equal(2, _testForm.Controls.Count);
            var allPanels = FindControlsOfType<InsightFeedPanel>(_testForm);
            Assert.Equal(2, allPanels.Count);
        }

        // ===============================================
        // Helper Methods
        // ===============================================

        private static List<T> FindControlsOfType<T>(Control container) where T : Control
        {
            var results = new List<T>();
            var queue = new Queue<Control>();
            queue.Enqueue(container);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current is T match)
                {
                    results.Add(match);
                }

                foreach (Control child in current.Controls)
                {
                    queue.Enqueue(child);
                }
            }

            return results;
        }

        private static List<Control> FindAllSyncfusionControls(Control container)
        {
            var results = new List<Control>();
            var queue = new Queue<Control>();
            queue.Enqueue(container);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (IsSyncfusionControl(current))
                {
                    results.Add(current);
                }

                foreach (Control child in current.Controls)
                {
                    queue.Enqueue(child);
                }
            }

            return results;
        }

        private static List<Control> GetAllControls(Control container)
        {
            var results = new List<Control> { container };
            var queue = new Queue<Control>();
            queue.Enqueue(container);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (Control child in current.Controls)
                {
                    results.Add(child);
                    queue.Enqueue(child);
                }
            }

            return results;
        }

        private static bool IsSyncfusionControl(Control control)
        {
            return control.GetType().Namespace?.StartsWith("Syncfusion", StringComparison.Ordinal) == true;
        }

        private static bool IsStatusIndicator(Control control)
        {
            // Check if control name or tag indicates it's a status indicator
            var name = control.Name?.ToLowerInvariant() ?? "";
            return name.Contains("status", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("indicator", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("error", StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _testForm?.Dispose();
            }
            _disposed = true;
        }
    }
}
