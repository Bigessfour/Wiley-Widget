using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.WinForms.Utils;
using Xunit;

namespace WileyWidget.Tests.WinForms
{
    /// <summary>
    /// Tests for SafeSplitterDistanceHelper thread-safe splitter distance assignment.
    ///
    /// CRITICAL: These tests verify the fix for the handle creation crash where
    /// BeginInvoke was called on controls without window handles, causing
    /// InvalidOperationException during panel initialization.
    /// </summary>
    public class SafeSplitterDistanceHelperTests : IDisposable
    {
        private Form? _testForm;

        public void Dispose()
        {
            // Cleanup on UI thread
            if (_testForm != null && !_testForm.IsDisposed)
            {
                if (_testForm.InvokeRequired)
                {
                    _testForm.Invoke(new Action(() =>
                    {
                        _testForm.Dispose();
                        _testForm = null;
                    }));
                }
                else
                {
                    _testForm.Dispose();
                    _testForm = null;
                }
            }


        }

        /// <summary>
        /// Verifies that SetSplitterDistanceDeferred handles null gracefully
        /// </summary>
        [Fact]
        public void SetSplitterDistanceDeferred_WithNullSplitContainer_DoesNotThrow()
        {
            // Act & Assert - should not throw
            SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(null!, 100);
        }

        /// <summary>
        /// Verifies that SetSplitterDistanceDeferred handles disposed controls gracefully
        /// </summary>
        [Fact]
        public void SetSplitterDistanceDeferred_WithDisposedControl_DoesNotThrow()
        {
            // Arrange
            var splitContainer = new SplitContainer();
            splitContainer.Dispose();

            // Act & Assert - should not throw
            SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(splitContainer, 100);
        }

        /// <summary>
        /// CRITICAL TEST: Verifies that SetSplitterDistanceDeferred works correctly
        /// when called BEFORE the control's window handle is created.
        ///
        /// This is the crash scenario that was fixed - BeginInvoke was called on
        /// controls without handles during InitializeControls() in OnHandleCreated.
        /// </summary>
        [Fact]
        public void SetSplitterDistanceDeferred_BeforeHandleCreated_WaitsForHandleAndSetsDistance()
        {
            // Arrange - Create control WITHOUT creating handle
            var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50
            };

            // Verify handle is NOT created yet
            Assert.False(splitContainer.IsHandleCreated, "Precondition: Handle should not be created yet");

            var handleCreated = false;
            var distanceSet = false;
            var expectedDistance = 200;

            // Monitor when handle gets created
            splitContainer.HandleCreated += (s, e) => handleCreated = true;

            // Act - Call SetSplitterDistanceDeferred BEFORE handle exists
            SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(splitContainer, expectedDistance);

            // Create a form to host the control and force handle creation
            using var form = new Form { Width = 500, Height = 400 };
            form.Controls.Add(splitContainer);

            // Force handle creation by showing form
            form.Load += (s, e) =>
            {
                // Give BeginInvoke time to execute on UI thread
                Application.DoEvents();

                // Verify the distance was set correctly
                distanceSet = Math.Abs(splitContainer.SplitterDistance - expectedDistance) < 10; // Allow small tolerance
            };

            form.Show();
            Application.DoEvents(); // Process messages
            Thread.Sleep(100); // Allow async operations to complete
            Application.DoEvents(); // Process any pending invokes

            // Assert
            Assert.True(handleCreated, "Handle should have been created");
            Assert.True(distanceSet, $"Splitter distance should be ~{expectedDistance}, was {splitContainer.SplitterDistance}");

            form.Close();
        }

        /// <summary>
        /// Verifies that SetSplitterDistanceDeferred works correctly when called
        /// AFTER the control's window handle already exists (normal scenario).
        /// </summary>
        [Fact]
        public void SetSplitterDistanceDeferred_AfterHandleCreated_SetsDeferredDistance()
        {
            // Arrange - Create control WITH handle
            var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50
            };

            using var form = new Form { Width = 500, Height = 400 };
            form.Controls.Add(splitContainer);
            form.Show(); // Force handle creation
            Application.DoEvents();

            // Verify handle IS created
            Assert.True(splitContainer.IsHandleCreated, "Precondition: Handle should be created");

            var expectedDistance = 200;

            // Act - Call SetSplitterDistanceDeferred AFTER handle exists
            SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(splitContainer, expectedDistance);

            // Allow BeginInvoke to execute
            Application.DoEvents();
            Thread.Sleep(50);
            Application.DoEvents();

            // Assert - distance should be set
            Assert.InRange(splitContainer.SplitterDistance, expectedDistance - 10, expectedDistance + 10);

            form.Close();
        }

        /// <summary>
        /// Verifies that TrySetSplitterDistance returns false for out-of-bounds values
        /// </summary>
        [Fact]
        public void TrySetSplitterDistance_WithOutOfBoundsValue_ReturnsFalse()
        {
            // Arrange
            using var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50
            };

            // Act - Try to set distance beyond valid range
            var result = SafeSplitterDistanceHelper.TrySetSplitterDistance(splitContainer, 10000);

            // Assert
            Assert.False(result, "Should return false for out-of-bounds value");
        }

        /// <summary>
        /// Verifies that TrySetSplitterDistance returns true for valid values
        /// </summary>
        [Fact]
        public void TrySetSplitterDistance_WithValidValue_ReturnsTrueAndSetsDistance()
        {
            // Arrange
            using var form = new Form { Width = 500, Height = 400 };
            using var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50
            };
            form.Controls.Add(splitContainer);
            form.Show();
            Application.DoEvents();

            var expectedDistance = 200;

            // Act
            var result = SafeSplitterDistanceHelper.TrySetSplitterDistance(splitContainer, expectedDistance);

            // Assert
            Assert.True(result, "Should return true for valid value");
            Assert.InRange(splitContainer.SplitterDistance, expectedDistance - 10, expectedDistance + 10);

            form.Close();
        }

        /// <summary>
        /// Verifies that CalculateSafeDistance returns proportional values
        /// </summary>
        [Fact]
        public void CalculateSafeDistance_ReturnsProportionalDistance()
        {
            // Arrange
            using var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50,
                Orientation = Orientation.Vertical
            };

            // Act - Calculate 50% split
            var safeDistance = SafeSplitterDistanceHelper.CalculateSafeDistance(splitContainer, 0.5);

            // Assert - should be approximately half the width
            Assert.InRange(safeDistance, 180, 220); // ~200 with tolerance
        }

        /// <summary>
        /// Verifies that SetupProportionalResizing maintains proportional split during resize
        /// </summary>
        [Fact]
        public void SetupProportionalResizing_MaintainsProportionDuringResize()
        {
            // Arrange
            using var form = new Form { Width = 500, Height = 400 };
            using var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50,
                Orientation = Orientation.Vertical,
                Dock = DockStyle.Fill
            };
            form.Controls.Add(splitContainer);
            form.Show();
            Application.DoEvents();

            // Set initial 50% split
            splitContainer.SplitterDistance = 200;

            // Act - Setup proportional resizing
            SafeSplitterDistanceHelper.SetupProportionalResizing(splitContainer, 0.5);

            // Simulate resize
            form.Width = 700;
            Application.DoEvents();
            Thread.Sleep(50);
            Application.DoEvents();

            // Assert - splitter should maintain ~50% (allowing tolerance for borders/padding)
            var expectedDistance = (splitContainer.Width - splitContainer.SplitterWidth) / 2;
            Assert.InRange(splitContainer.SplitterDistance, expectedDistance - 20, expectedDistance + 20);

            form.Close();
        }

        /// <summary>
        /// Regression test: Verifies the fix for the crash reported in QuickBooksPanel
        /// where SetSplitterDistanceDeferred was called during InitializeControls() in
        /// OnHandleCreated, before child controls had their handles created.
        /// </summary>
        [Fact]
        public void SetSplitterDistanceDeferred_SimulateQuickBooksPanelScenario_DoesNotCrash()
        {
            // Arrange - Simulate the crash scenario:
            // 1. Parent panel OnHandleCreated fires
            // 2. InitializeControls() creates new SplitContainer
            // 3. SetSplitterDistanceDeferred called immediately
            // 4. Child SplitContainer doesn't have handle yet

            var panelHandleCreated = false;
            var splitContainerHandleCreated = false;
            var exceptionThrown = false;

            using var parentPanel = new Panel { Width = 800, Height = 600 };

            // Simulate InitializeControls() being called in OnHandleCreated
            parentPanel.HandleCreated += (s, e) =>
            {
                panelHandleCreated = true;

                // Create SplitContainer (doesn't have handle yet)
                var splitContainer = new SplitContainer
                {
                    Width = 700,
                    Height = 500,
                    Panel1MinSize = 100,
                    Panel2MinSize = 100,
                    Dock = DockStyle.Fill
                };

                // Add to parent (may or may not create handle depending on layout state)
                parentPanel.Controls.Add(splitContainer);

                // Monitor when child handle gets created
                splitContainer.HandleCreated += (sender, args) => splitContainerHandleCreated = true;

                try
                {
                    // This was the crash point - calling BeginInvoke before handle exists
                    SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(splitContainer, 350);
                }
                catch (Exception ex)
                {
                    exceptionThrown = true;
                    System.Diagnostics.Debug.WriteLine($"Exception during SetSplitterDistanceDeferred: {ex}");
                }
            };

            // Act - Create form and show (triggers handle creation cascade)
            using var form = new Form { Width = 900, Height = 700 };
            form.Controls.Add(parentPanel);
            form.Show();
            Application.DoEvents();
            Thread.Sleep(100);
            Application.DoEvents();

            // Assert
            Assert.True(panelHandleCreated, "Parent panel handle should be created");
            Assert.True(splitContainerHandleCreated, "SplitContainer handle should be created");
            Assert.False(exceptionThrown, "No exception should be thrown");

            form.Close();
        }

        /// <summary>
        /// Ensures TrySetSplitterDistance returns false (and does not throw) when the available size
        /// is smaller than Panel1MinSize + Panel2MinSize + SplitterWidth, matching SplitContainer rules.
        /// </summary>
        [Fact]
        public void TrySetSplitterDistance_ReturnsFalse_WhenAvailableSpaceTooSmall()
        {
            using var form = new Form { Width = 480, Height = 300 };
            using var splitContainer = new SplitContainer
            {
                Orientation = Orientation.Vertical,
                Dock = DockStyle.Fill,
                Panel1MinSize = 250,
                Panel2MinSize = 250,
                SplitterWidth = 8
            };

            form.Controls.Add(splitContainer);
            form.Show();
            Application.DoEvents();

            var result = SafeSplitterDistanceHelper.TrySetSplitterDistance(splitContainer, 300);

            Assert.False(result);
        }

        /// <summary>
        /// Verifies we clamp using the documented upper bound: Width - SplitterWidth - Panel2MinSize.
        /// </summary>
        [Fact]
        public void TrySetSplitterDistance_ClampsWithSplitterWidth()
        {
            using var form = new Form { Width = 600, Height = 400 };
            using var splitContainer = new SplitContainer
            {
                Orientation = Orientation.Vertical,
                Dock = DockStyle.Fill,
                Panel1MinSize = 250,
                Panel2MinSize = 250,
                SplitterWidth = 12
            };

            form.Controls.Add(splitContainer);
            form.Show();
            Application.DoEvents();

            var result = SafeSplitterDistanceHelper.TrySetSplitterDistance(splitContainer, 400);

            Assert.True(result);

            var expectedMax = splitContainer.Width - splitContainer.SplitterWidth - splitContainer.Panel2MinSize;
            Assert.Equal(expectedMax, splitContainer.SplitterDistance);
        }
    }
}
