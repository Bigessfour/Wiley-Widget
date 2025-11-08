using System;
using Xunit;
using Prism.Dialogs;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Dialogs;

namespace WileyWidget.Tests.ViewModels
{
    /// <summary>
    /// Tests for DialogViewModelBase disposal and cleanup behavior.
    /// Ensures proper resource cleanup during shutdown to prevent memory leaks
    /// and NullReferenceException in Prism DialogService.
    /// </summary>
    public class DialogViewModelBaseTests
    {
        /// <summary>
        /// Test dialog ViewModel for disposal testing
        /// </summary>
        private class TestDialogViewModel : DialogViewModelBase
        {
            public bool IsDisposeCoreCalled { get; private set; }
            public bool IsDisposed { get; private set; }

            protected override void DisposeCore()
            {
                IsDisposeCoreCalled = true;
                base.DisposeCore();
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    IsDisposed = true;
                }
            }
        }

        [Fact]
        public void OnDialogClosed_CallsDispose()
        {
            // Arrange
            var viewModel = new TestDialogViewModel();

            // Act
            viewModel.OnDialogClosed();

            // Assert
            Assert.True(viewModel.IsDisposed, "ViewModel should be disposed when dialog closes");
            Assert.True(viewModel.IsDisposeCoreCalled, "DisposeCore should be called");
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var viewModel = new TestDialogViewModel();

            // Act
            viewModel.Dispose();
            viewModel.Dispose();
            viewModel.Dispose();

            // Assert - should not throw
            Assert.True(viewModel.IsDisposed, "ViewModel should be disposed");
        }

        [Fact]
        public void DisposedViewModel_CannotBeUsedAfterDisposal()
        {
            // Arrange
            var viewModel = new TestDialogViewModel();

            // Act
            viewModel.Dispose();

            // Assert - IsDisposed should be set
            Assert.True(viewModel.IsDisposed, "ViewModel should be marked as disposed");
        }

        [Fact]
        public void CanCloseDialog_ReturnsTrueByDefault()
        {
            // Arrange
            var viewModel = new TestDialogViewModel();

            // Act
            var canClose = viewModel.CanCloseDialog();

            // Assert
            Assert.True(canClose, "Dialog should be closeable by default");
        }

        [Fact]
        public void OnDialogOpened_SetsTitle()
        {
            // Arrange
            var viewModel = new TestDialogViewModel();
            var parameters = new DialogParameters
            {
                { "Title", "Test Dialog" }
            };

            // Act
            viewModel.OnDialogOpened(parameters);

            // Assert
            Assert.Equal("Test Dialog", viewModel.Title);
        }

        [Fact]
        public void OnDialogOpened_ThrowsOnNullParameters()
        {
            // Arrange
            var viewModel = new TestDialogViewModel();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => viewModel.OnDialogOpened(null!));
        }

        [Fact]
        public void ErrorDialogViewModel_DisposalDoesNotThrow()
        {
            // Arrange
            var viewModel = new ErrorDialogViewModel();
            var parameters = new DialogParameters
            {
                { "Message", "Test error" },
                { "ButtonText", "OK" }
            };
            viewModel.OnDialogOpened(parameters);

            // Act & Assert - should not throw
            viewModel.OnDialogClosed();
            viewModel.Dispose();
        }

        [Fact]
        public void ConfirmationDialogViewModel_DisposalDoesNotThrow()
        {
            // Arrange
            var viewModel = new ConfirmationDialogViewModel();
            var parameters = new DialogParameters
            {
                { "Message", "Confirm action?" },
                { "ConfirmButtonText", "Yes" },
                { "CancelButtonText", "No" }
            };
            viewModel.OnDialogOpened(parameters);

            // Act & Assert - should not throw
            viewModel.OnDialogClosed();
            viewModel.Dispose();
        }

        [Fact]
        public void NotificationDialogViewModel_DisposalDoesNotThrow()
        {
            // Arrange
            var viewModel = new NotificationDialogViewModel();
            var parameters = new DialogParameters
            {
                { "Message", "Notification message" }
            };
            viewModel.OnDialogOpened(parameters);

            // Act & Assert - should not throw
            viewModel.OnDialogClosed();
            viewModel.Dispose();
        }

        [Fact]
        public void WarningDialogViewModel_DisposalDoesNotThrow()
        {
            // Arrange
            var viewModel = new WarningDialogViewModel();
            var parameters = new DialogParameters
            {
                { "Message", "Warning message" }
            };
            viewModel.OnDialogOpened(parameters);

            // Act & Assert - should not throw
            viewModel.OnDialogClosed();
            viewModel.Dispose();
        }
    }
}
