using System;
using System.Threading;
using System.Windows;
using Xunit;
using WileyWidget.Services;

namespace WileyWidget.Tests.Services
{
    /// <summary>
    /// Tests for DialogTrackingService to ensure proper dialog lifecycle management
    /// and shutdown behavior.
    /// </summary>
    public class DialogTrackingServiceTests
    {
        /// <summary>
        /// Mock dialog window for testing
        /// </summary>
        private class MockDialogWindow : Window
        {
            public MockDialogWindow()
            {
                // Don't show the window during tests
            }

            public void SimulateClose()
            {
                // Simulate closing without actually showing the window
                OnClosed(EventArgs.Empty);
            }
        }

        [StaFact]
        public void RegisterDialog_IncrementsCount()
        {
            // Arrange
            var service = new DialogTrackingService();
            var dialog = new MockDialogWindow();

            // Act
            service.RegisterDialog(dialog);

            // Assert
            Assert.Equal(1, service.OpenDialogCount);
        }

        [StaFact]
        public void RegisterDialog_NullDialog_DoesNotThrow()
        {
            // Arrange
            var service = new DialogTrackingService();

            // Act & Assert - should not throw
            service.RegisterDialog(null!);
            Assert.Equal(0, service.OpenDialogCount);
        }

        [StaFact]
        public void RegisterMultipleDialogs_TracksCorrectly()
        {
            // Arrange
            var service = new DialogTrackingService();
            var dialog1 = new MockDialogWindow();
            var dialog2 = new MockDialogWindow();
            var dialog3 = new MockDialogWindow();

            // Act
            service.RegisterDialog(dialog1);
            service.RegisterDialog(dialog2);
            service.RegisterDialog(dialog3);

            // Assert
            Assert.Equal(3, service.OpenDialogCount);
        }

        [StaFact]
        public void UnregisterDialog_DecrementsCount()
        {
            // Arrange
            var service = new DialogTrackingService();
            var dialog = new MockDialogWindow();
            service.RegisterDialog(dialog);

            // Act
            service.UnregisterDialog(dialog);

            // Assert
            Assert.Equal(0, service.OpenDialogCount);
        }

        [StaFact]
        public void DialogClosed_AutomaticallyUnregisters()
        {
            // Arrange
            var service = new DialogTrackingService();
            var dialog = new MockDialogWindow();
            service.RegisterDialog(dialog);

            // Act
            dialog.SimulateClose();

            // Give time for event to process
            Thread.Sleep(100);

            // Assert
            Assert.Equal(0, service.OpenDialogCount);
        }

        [StaFact]
        public void CloseAllDialogs_RemovesAllTracked()
        {
            // Arrange
            var service = new DialogTrackingService();
            var dialog1 = new MockDialogWindow();
            var dialog2 = new MockDialogWindow();
            service.RegisterDialog(dialog1);
            service.RegisterDialog(dialog2);

            // Act
            service.CloseAllDialogs();

            // Assert
            Assert.Equal(0, service.OpenDialogCount);
        }

        [StaFact]
        public void GetOpenDialogTypes_ReturnsCorrectTypes()
        {
            // Arrange
            var service = new DialogTrackingService();
            var dialog1 = new MockDialogWindow();
            var dialog2 = new MockDialogWindow();
            service.RegisterDialog(dialog1);
            service.RegisterDialog(dialog2);

            // Act
            var types = service.GetOpenDialogTypes();

            // Assert
            Assert.Equal(2, types.Count);
            Assert.All(types, t => Assert.Equal(nameof(MockDialogWindow), t));
        }

        [StaFact]
        public void CloseAllDialogs_WithNoDialogs_DoesNotThrow()
        {
            // Arrange
            var service = new DialogTrackingService();

            // Act & Assert - should not throw
            service.CloseAllDialogs();
            Assert.Equal(0, service.OpenDialogCount);
        }

        [StaFact]
        public void OpenDialogCount_AfterGarbageCollection_CleansUpDeadReferences()
        {
            // Arrange
            var service = new DialogTrackingService();

            // Create dialog in separate method to allow GC
            void CreateDialog()
            {
                var dialog = new MockDialogWindow();
                service.RegisterDialog(dialog);
                // Dialog goes out of scope here
            }

            CreateDialog();

            // Act
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var count = service.OpenDialogCount; // This triggers cleanup

            // Assert
            Assert.Equal(0, count);
        }

        [StaFact]
        public void GetOpenDialogTypes_EmptyWhenNoDialogs()
        {
            // Arrange
            var service = new DialogTrackingService();

            // Act
            var types = service.GetOpenDialogTypes();

            // Assert
            Assert.Empty(types);
        }
    }
}
