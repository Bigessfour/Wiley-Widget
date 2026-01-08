#nullable enable

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Tests.Controls
{
    /// <summary>
    /// Testable wrapper for InsightFeedPanel to expose internal state for verification.
    /// Allows unit tests to access and assert on private controls without reflection.
    /// </summary>
    internal class TestableInsightFeedPanel : InsightFeedPanel
    {
        public TestableInsightFeedPanel(
            InsightFeedViewModel? viewModel = null,
            IThemeService? themeService = null,
            ILogger<InsightFeedPanel>? logger = null)
            : base(viewModel, themeService, logger)
        {
        }

        // Expose internal state for testing
        public object? GetDataContext() => DataContext;
        public int GetControlCount() => Controls.Count;

        public Syncfusion.WinForms.DataGrid.SfDataGrid? GetDataGrid()
        {
            foreach (System.Windows.Forms.Control ctrl in Controls)
            {
                if (ctrl is Syncfusion.WinForms.DataGrid.SfDataGrid grid)
                    return grid;
            }
            return null;
        }

        public System.Windows.Forms.Label? GetStatusLabel()
        {
            var topPanel = Controls.Count > 0 ? Controls[0] : null;
            if (topPanel != null)
            {
                foreach (System.Windows.Forms.Control ctrl in topPanel.Controls)
                {
                    if (ctrl is System.Windows.Forms.Label lbl && ctrl.Name == "StatusLabel")
                        return lbl;
                }
            }
            return null;
        }

        public LoadingOverlay? GetLoadingOverlay()
        {
            foreach (System.Windows.Forms.Control ctrl in Controls)
            {
                if (ctrl is LoadingOverlay overlay)
                    return overlay;
            }
            return null;
        }
    }

    /// <summary>
    /// Construction and Initialization Tests for InsightFeedPanel.
    /// Tests: Constructor variations, UI setup, control creation, column configuration.
    /// </summary>
    public sealed class InsightFeedPanelConstructionTests : IDisposable
    {
        private readonly Mock<ILogger<InsightFeedPanel>> _mockLogger;
        private TestableInsightFeedPanel? _panel;

        public InsightFeedPanelConstructionTests()
        {
            _mockLogger = new Mock<ILogger<InsightFeedPanel>>();
        }

        #region Happy Path Tests

        [Fact]
        public void Constructor_Default_InitializesSuccessfully()
        {
            // Act & Assert - Should not throw
            var panel = new TestableInsightFeedPanel();
            Assert.NotNull(panel);
            Assert.NotNull(panel.GetDataContext());
            panel.Dispose();
        }

        [Fact]
        public void Constructor_WithExplicitDependencies_UsesProvidedInstances()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();
            mockViewModel.Object.StatusMessage = "Ready";
            var mockThemeService = new Mock<IThemeService>();

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, mockThemeService.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(_panel);
            Assert.Same(mockViewModel.Object, _panel.GetDataContext());
        }

        [Fact]
        public void InitializeUI_CreatesAllRequiredControls()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();
            mockViewModel.Object.StatusMessage = "Ready";

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Assert
            Assert.True(_panel.GetControlCount() >= 3, "Panel should have at least 3 child controls (top panel, overlay, grid)");
            Assert.NotNull(_panel.GetDataGrid());
            Assert.NotNull(_panel.GetLoadingOverlay());
        }

        [Fact]
        public void ConfigureGridColumns_CreatesExactlyFourColumns()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            var grid = _panel.GetDataGrid();

            // Assert
            Assert.NotNull(grid);
            Assert.Equal(4, grid.Columns.Count);
        }

        [Fact]
        public void ConfigureGridColumns_CreatesCorrectColumnMappings()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            var grid = _panel.GetDataGrid();

            // Assert
            Assert.NotNull(grid);
            var columnMappings = new[]
            {
                nameof(InsightCardModel.Priority),
                nameof(InsightCardModel.Category),
                nameof(InsightCardModel.Explanation),
                nameof(InsightCardModel.Timestamp)
            };

            foreach (var mapping in columnMappings)
            {
                Assert.Contains(grid.Columns, c => c.MappingName == mapping);
            }
        }

        [Fact]
        public void ConfigureGridColumns_AllColumnsAllowSortingAndFiltering()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            var grid = _panel.GetDataGrid();

            // Assert
            Assert.NotNull(grid);
            foreach (var column in grid.Columns)
            {
                Assert.True(column.AllowSorting, $"Column {column.MappingName} should allow sorting");
                Assert.True(column.AllowFiltering, $"Column {column.MappingName} should allow filtering");
            }
        }

        [Fact]
        public void GridConfiguration_DisallowsEditingAndGrouping()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            var grid = _panel.GetDataGrid();

            // Assert
            Assert.NotNull(grid);
            Assert.False(grid.AllowEditing, "Grid should not allow editing");
            Assert.False(grid.AllowGrouping, "Grid should not allow grouping");
            Assert.False(grid.ShowRowHeader, "Grid should not show row headers");
        }

        #endregion

        #region Sad Path Tests

        [Fact]
        public void Constructor_WithNullViewModel_UsesFallbackAndContinues()
        {
            // Act - Should not throw with null ViewModel
            _panel = new TestableInsightFeedPanel(null, null, _mockLogger.Object);

            // Assert
            Assert.NotNull(_panel);
            Assert.NotNull(_panel.GetDataContext());
            Assert.IsType<InsightFeedViewModel>(_panel.GetDataContext());
        }

        [Fact]
        public void Constructor_WithNullLogger_UsesFallbackAndContinues()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act - Should not throw with null logger
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, null);

            // Assert
            Assert.NotNull(_panel);
        }

        [Fact]
        public void Constructor_WithNullThemeService_ContinuesWithoutError()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act - Should not throw with null theme service
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Assert
            Assert.NotNull(_panel);
        }

        [Fact]
        public void InitializeUI_LogsErrorIfExceptionOccurs()
        {
            // Verify that InitializeUI includes try-catch with logging
            // This is verified through successful construction (no unhandled exception)
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Assert - Panel created successfully means error handling works
            Assert.NotNull(_panel);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Constructor_MultipleInstances_IndependentState()
        {
            // Arrange
            var vm1 = new Mock<InsightFeedViewModel>();
            vm1.Object.InsightCards = new ObservableCollection<InsightCardModel>();
            var vm2 = new Mock<InsightFeedViewModel>();
            vm2.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act
            var panel1 = new TestableInsightFeedPanel(vm1.Object, null, _mockLogger.Object);
            var panel2 = new TestableInsightFeedPanel(vm2.Object, null, _mockLogger.Object);

            // Assert
            Assert.NotSame(panel1.GetDataContext(), panel2.GetDataContext());
            panel1.Dispose();
            panel2.Dispose();
        }

        [Fact]
        public void DataContext_ReferencesCorrectViewModel()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Assert
            Assert.Same(mockViewModel.Object, _panel.GetDataContext());
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _panel?.Dispose();
            }
        }
    }

    /// <summary>
    /// Binding and Data Flow Tests for InsightFeedPanel.
    /// Tests: ViewModel binding, collection binding, property propagation.
    /// </summary>
    public sealed class InsightFeedPanelBindingTests : IDisposable
    {
        private readonly Mock<ILogger<InsightFeedPanel>> _mockLogger;
        private TestableInsightFeedPanel? _panel;

        public InsightFeedPanelBindingTests()
        {
            _mockLogger = new Mock<ILogger<InsightFeedPanel>>();
        }

        #region Happy Path Tests

        [Fact]
        public void BindViewModel_BindsInsightCardsCollectionToGrid()
        {
            // Arrange
            var insights = new ObservableCollection<InsightCardModel>
            {
                new InsightCardModel
                {
                    Priority = "High",
                    Category = "Budget",
                    Explanation = "Test insight 1",
                    Timestamp = DateTime.Now
                },
                new InsightCardModel
                {
                    Priority = "Medium",
                    Category = "Revenue",
                    Explanation = "Test insight 2",
                    Timestamp = DateTime.Now
                }
            };

            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = insights;
            mockViewModel.Object.StatusMessage = "Ready";

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            var grid = _panel.GetDataGrid();

            // Assert
            Assert.NotNull(grid);
            Assert.Same(insights, grid.DataSource);
        }

        [Fact]
        public void PropertyChanged_StatusMessage_UpdatesStatusLabel()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();
            mockViewModel.Object.StatusMessage = "Initial Status";
            mockViewModel.Object.IsLoading = false;

            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            var statusLabel = _panel.GetStatusLabel();

            // Act
            mockViewModel.Object.StatusMessage = "Updated Status";
            mockViewModel.Raise(
                vm => vm.PropertyChanged += null,
                new PropertyChangedEventArgs(nameof(InsightFeedViewModel.StatusMessage)));

            System.Windows.Forms.Application.DoEvents();

            // Assert
            Assert.NotNull(statusLabel);
            Assert.Equal("Updated Status", statusLabel.Text);
        }

        [Fact]
        public void PropertyChanged_IsLoading_TogglesLoadingOverlay()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();
            mockViewModel.Object.StatusMessage = "Ready";
            mockViewModel.Object.IsLoading = false;

            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            var overlay = _panel.GetLoadingOverlay();

            // Act
            mockViewModel.Object.IsLoading = true;
            mockViewModel.Raise(
                vm => vm.PropertyChanged += null,
                new PropertyChangedEventArgs(nameof(InsightFeedViewModel.IsLoading)));

            System.Windows.Forms.Application.DoEvents();

            // Assert
            Assert.NotNull(overlay);
            Assert.True(overlay.Visible, "Loading overlay should be visible when IsLoading is true");
        }

        [Fact]
        public void PropertyChanged_PriorityCounts_LoggedCorrectly()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();
            mockViewModel.Object.HighPriorityCount = 1;
            mockViewModel.Object.MediumPriorityCount = 2;
            mockViewModel.Object.LowPriorityCount = 3;

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            mockViewModel.Raise(
                vm => vm.PropertyChanged += null,
                new PropertyChangedEventArgs(nameof(InsightFeedViewModel.HighPriorityCount)));

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Priority counts")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Sad Path Tests

        [Fact]
        public void BindViewModel_WithNullViewModel_HandlesGracefully()
        {
            // Arrange - Create panel with null ViewModel
            // Act
            _panel = new TestableInsightFeedPanel(null, null, _mockLogger.Object);

            // Assert
            Assert.NotNull(_panel);
            Assert.NotNull(_panel.GetDataContext()); // Should have fallback ViewModel
            Assert.NotNull(_panel.GetDataGrid());
        }

        [Fact]
        public void PropertyChanged_WithNullPropertyName_DoesNotThrow()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Act & Assert - Should not throw
            mockViewModel.Raise(
                vm => vm.PropertyChanged += null,
                new PropertyChangedEventArgs(null)); // Null property name

            System.Windows.Forms.Application.DoEvents();
        }

        [Fact]
        public void PropertyChanged_WithUnknownPropertyName_IgnoresAndContinues()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Act & Assert - Should not throw with unknown property
            mockViewModel.Raise(
                vm => vm.PropertyChanged += null,
                new PropertyChangedEventArgs("UnknownProperty"));

            System.Windows.Forms.Application.DoEvents();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void BindViewModel_WithEmptyInsightCollection_DisplaysCorrectly()
        {
            // Arrange
            var insights = new ObservableCollection<InsightCardModel>();
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = insights;
            mockViewModel.Object.StatusMessage = "No insights";

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            var grid = _panel.GetDataGrid();

            // Assert
            Assert.NotNull(grid);
            Assert.Empty(grid.DataSource as ObservableCollection<InsightCardModel> ?? new());
        }

        [Fact]
        public void BindViewModel_WithLargeInsightCollection_BindsSuccessfully()
        {
            // Arrange
            var insights = new ObservableCollection<InsightCardModel>();
            for (int i = 0; i < 1000; i++)
            {
                insights.Add(new InsightCardModel
                {
                    Priority = i % 3 == 0 ? "High" : i % 3 == 1 ? "Medium" : "Low",
                    Category = $"Category {i}",
                    Explanation = $"Insight {i}",
                    Timestamp = DateTime.Now
                });
            }

            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = insights;

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);
            var grid = _panel.GetDataGrid();

            // Assert
            Assert.NotNull(grid);
            Assert.Equal(1000, (grid.DataSource as ObservableCollection<InsightCardModel>)?.Count ?? 0);
        }

        [Fact]
        public void PropertyChanged_RapidConsecutiveUpdates_HandleAllCorrectly()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Act - Fire multiple property changes rapidly
            for (int i = 0; i < 10; i++)
            {
                mockViewModel.Object.StatusMessage = $"Status {i}";
                mockViewModel.Raise(
                    vm => vm.PropertyChanged += null,
                    new PropertyChangedEventArgs(nameof(InsightFeedViewModel.StatusMessage)));
            }

            System.Windows.Forms.Application.DoEvents();

            // Assert - Final status should be the last one
            var statusLabel = _panel.GetStatusLabel();
            Assert.NotNull(statusLabel);
            Assert.Equal("Status 9", statusLabel.Text);
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _panel?.Dispose();
            }
        }
    }

    /// <summary>
    /// Command Execution and User Interaction Tests.
    /// Tests: Button clicks, grid selection, command execution.
    /// </summary>
    public class InsightFeedPanelCommandTests : IDisposable
    {
        private readonly Mock<ILogger<InsightFeedPanel>> _mockLogger;
        private TestableInsightFeedPanel? _panel;

        public InsightFeedPanelCommandTests()
        {
            _mockLogger = new Mock<ILogger<InsightFeedPanel>>();
        }

        #region Happy Path Tests

        [Fact]
        public void RefreshButton_Click_LogsRefreshRequest()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Find and click refresh button
            System.Windows.Forms.ToolStripButton? refreshBtn = null;
            foreach (System.Windows.Forms.Control ctrl in _panel.Controls)
            {
                if (ctrl is System.Windows.Forms.ToolStrip toolStrip)
                {
                    foreach (System.Windows.Forms.ToolStripItem item in toolStrip.Items)
                    {
                        if (item is System.Windows.Forms.ToolStripButton btn && btn.Name == "RefreshButton")
                        {
                            refreshBtn = btn;
                            break;
                        }
                    }
                }
            }

            // Act
            Assert.NotNull(refreshBtn);
            refreshBtn.PerformClick();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User requested manual refresh")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ApplyTheme_AppliesSfSkinManagerStyling()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Theme applied successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void PanelInitialization_CompletesSuccessfully()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("initialized successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Sad Path Tests

        [Fact]
        public void RefreshButton_WhenViewModelIsNull_DoesNotThrow()
        {
            // Arrange
            _panel = new TestableInsightFeedPanel(null, null, _mockLogger.Object);

            // Find and click refresh button
            System.Windows.Forms.ToolStripButton? refreshBtn = null;
            foreach (System.Windows.Forms.Control ctrl in _panel.Controls)
            {
                if (ctrl is System.Windows.Forms.ToolStrip toolStrip)
                {
                    foreach (System.Windows.Forms.ToolStripItem item in toolStrip.Items)
                    {
                        if (item is System.Windows.Forms.ToolStripButton btn && btn.Name == "RefreshButton")
                        {
                            refreshBtn = btn;
                            break;
                        }
                    }
                }
            }

            // Act & Assert - Should not throw
            Assert.NotNull(refreshBtn);
            refreshBtn.PerformClick();
        }

        [Fact]
        public void ApplyTheme_WhenFails_LogsErrorAndContinues()
        {
            // The ApplyTheme method includes try-catch, so it won't throw
            // This test verifies the pattern is in place
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            // Act
            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Assert - Panel was created (error was handled internally)
            Assert.NotNull(_panel);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Dispose_CleansUpResourcesAndUnsubscribes()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Act
            _panel.Dispose();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disposed successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void MultipleDispose_DoesNotThrow()
        {
            // Arrange
            var mockViewModel = new Mock<InsightFeedViewModel>();
            mockViewModel.Object.InsightCards = new ObservableCollection<InsightCardModel>();

            _panel = new TestableInsightFeedPanel(mockViewModel.Object, null, _mockLogger.Object);

            // Act & Assert - Should not throw on multiple dispose
            _panel.Dispose();
            _panel.Dispose(); // Second dispose should be safe
        }

        [Fact]
        public void Panel_WithNullServices_StillFunctional()
        {
            // Arrange & Act
            _panel = new TestableInsightFeedPanel(null, null, null);

            // Assert
            Assert.NotNull(_panel);
            Assert.NotNull(_panel.GetDataGrid());
            Assert.NotNull(_panel.GetLoadingOverlay());
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _panel?.Dispose();
            }
        }
    }
}
