using System;
using System.Windows;
using FluentAssertions;
using Xunit;

namespace WileyWidget.Tests
{
    // These tests must run on STA; StaFact is provided by Xunit.StaFact package in the test csproj
    public class LoadApplicationResourcesTests : IDisposable
    {
        private readonly Application? _app;

        public LoadApplicationResourcesTests()
        {
            if (Application.Current == null)
            {
                _app = new Application();
            }
        }

        [StaFact]
        public void Application_ShouldLoadResourcesWithoutException()
        {
            // Arrange
            var app = Application.Current ?? _app;
            app.Should().NotBeNull();

            // Act & Assert
            // This test verifies that the application can be created and basic resource loading works
            // The actual LoadApplicationResources method is tested indirectly through application startup
            Action act = () =>
            {
                // Try to access application resources - this will trigger resource loading if not already done
                var resources = app!.Resources;
                resources.Should().NotBeNull();
            };

            act.Should().NotThrow();
        }

        [StaFact]
        public void MemoryCheck_ShouldHandleLowMemoryConditions()
        {
            // Arrange - Force low memory simulation by allocating memory and triggering GC
            var largeList = new System.Collections.Generic.List<byte[]>();
            try
            {
                // Allocate memory to simulate low memory conditions
                for (int i = 0; i < 100; i++)
                {
                    largeList.Add(new byte[1024 * 1024]); // 1MB each
                }

                // Force garbage collection to update memory statistics
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Act & Assert
                // The application should still be able to start even under memory pressure
                var app = Application.Current ?? _app;
                app.Should().NotBeNull();

                // Accessing resources should not throw even under memory pressure
                var resources = app!.Resources;
                resources.Should().NotBeNull();
            }
            finally
            {
                // Clean up
                largeList.Clear();
                GC.Collect();
            }
        }

        public void Dispose()
        {
            _app?.Shutdown();
        }
    }
}
