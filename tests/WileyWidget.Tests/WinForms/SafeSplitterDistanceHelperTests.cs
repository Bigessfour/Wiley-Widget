using System;
using System.Threading;
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
    ///
    /// NOTE ON TEST DESIGN: Following best practices for Syncfusion SplitContainerAdv:
    /// - Set MODEST min sizes in tests (50-100 px), never aggressive ones (250+)
    /// - Always show form BEFORE setting any constraints
    /// - Test helper methods for defensive bounds checking, not constraint violations
    /// </summary>
    public class SafeSplitterDistanceHelperTests : IDisposable
    {
        private Form? _testForm;

        public void Dispose()
        {
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

        [Fact]
        public void SetSplitterDistanceDeferred_WithNullSplitContainer_DoesNotThrow()
        {
            SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(null!, 100);
        }

        [Fact]
        public void SetSplitterDistanceDeferred_WithDisposedControl_DoesNotThrow()
        {
            var splitContainer = new SplitContainer();
            splitContainer.Dispose();
            SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(splitContainer, 100);
        }

        [Fact]
        public void SetSplitterDistanceDeferred_BeforeHandleCreated_WaitsForHandleAndSetsDistance()
        {
            var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50
            };

            Assert.False(splitContainer.IsHandleCreated, "Precondition: Handle should not be created yet");

            var handleCreated = false;
            var distanceSet = false;
            var expectedDistance = 200;

            splitContainer.HandleCreated += (s, e) => handleCreated = true;

            SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(splitContainer, expectedDistance);

            using var form = new Form { Width = 500, Height = 400 };
            form.Controls.Add(splitContainer);

            form.Load += (s, e) =>
            {
                Application.DoEvents();
                distanceSet = Math.Abs(splitContainer.SplitterDistance - expectedDistance) < 10;
            };

            form.Show();
            Application.DoEvents();
            Thread.Sleep(100);
            Application.DoEvents();

            Assert.True(handleCreated, "Handle should have been created");
            Assert.True(distanceSet, $"Splitter distance should be ~{expectedDistance}, was {splitContainer.SplitterDistance}");

            form.Close();
        }

        [Fact]
        public void SetSplitterDistanceDeferred_AfterHandleCreated_SetsDeferredDistance()
        {
            var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50
            };

            using var form = new Form { Width = 500, Height = 400 };
            form.Controls.Add(splitContainer);
            form.Show();
            Application.DoEvents();

            Assert.True(splitContainer.IsHandleCreated, "Precondition: Handle should be created");

            var expectedDistance = 200;

            SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(splitContainer, expectedDistance);

            Application.DoEvents();
            Thread.Sleep(50);
            Application.DoEvents();

            Assert.InRange(splitContainer.SplitterDistance, expectedDistance - 10, expectedDistance + 10);

            form.Close();
        }

        [Fact]
        public void TrySetSplitterDistance_WithOutOfBoundsValue_ReturnsFalse()
        {
            using var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50
            };

            var result = SafeSplitterDistanceHelper.TrySetSplitterDistance(splitContainer, 10000);

            Assert.False(result, "Should return false for out-of-bounds value");
        }

        [Fact]
        public void TrySetSplitterDistance_WithValidValue_ReturnsTrueAndSetsDistance()
        {
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

            var result = SafeSplitterDistanceHelper.TrySetSplitterDistance(splitContainer, expectedDistance);

            Assert.True(result, "Should return true for valid value");
            Assert.InRange(splitContainer.SplitterDistance, expectedDistance - 10, expectedDistance + 10);

            form.Close();
        }

        [Fact]
        public void CalculateSafeDistance_ReturnsProportionalDistance()
        {
            using var splitContainer = new SplitContainer
            {
                Width = 400,
                Height = 300,
                Panel1MinSize = 50,
                Panel2MinSize = 50,
                Orientation = Orientation.Vertical
            };

            var safeDistance = SafeSplitterDistanceHelper.CalculateSafeDistance(splitContainer, 0.5);

            Assert.InRange(safeDistance, 180, 220);
        }

        [Fact]
        public void SetupProportionalResizing_MaintainsProportionDuringResize()
        {
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

            splitContainer.SplitterDistance = 200;

            SafeSplitterDistanceHelper.SetupProportionalResizing(splitContainer, 0.5);

            form.Width = 700;
            Application.DoEvents();
            Thread.Sleep(50);
            Application.DoEvents();

            var expectedDistance = (splitContainer.Width - splitContainer.SplitterWidth) / 2;
            Assert.InRange(splitContainer.SplitterDistance, expectedDistance - 20, expectedDistance + 20);

            form.Close();
        }

        [Fact]
        public void SetSplitterDistanceDeferred_SimulateQuickBooksPanelScenario_DoesNotCrash()
        {
            var panelHandleCreated = false;
            var splitContainerHandleCreated = false;
            var exceptionThrown = false;

            using var parentPanel = new Panel { Width = 800, Height = 600 };

            parentPanel.HandleCreated += (s, e) =>
            {
                panelHandleCreated = true;

                var splitContainer = new SplitContainer
                {
                    Width = 700,
                    Height = 500,
                    Panel1MinSize = 100,
                    Panel2MinSize = 100,
                    Dock = DockStyle.Fill
                };

                parentPanel.Controls.Add(splitContainer);
                splitContainer.CreateControl();

                splitContainer.HandleCreated += (sender, args) => splitContainerHandleCreated = true;

                try
                {
                    SafeSplitterDistanceHelper.SetSplitterDistanceDeferred(splitContainer, 350);
                }
                catch (Exception ex)
                {
                    exceptionThrown = true;
                    System.Diagnostics.Debug.WriteLine($"Exception during SetSplitterDistanceDeferred: {ex}");
                }
            };

            using var form = new Form { Width = 900, Height = 700 };
            form.Controls.Add(parentPanel);
            form.Show();
            Application.DoEvents();
            Thread.Sleep(100);
            Application.DoEvents();

            Assert.True(panelHandleCreated, "Parent panel handle should be created");
            Assert.True(splitContainerHandleCreated, "SplitContainer handle should be created");
            Assert.False(exceptionThrown, "No exception should be thrown");

            form.Close();
        }

        [Fact]
        public void TrySetSplitterDistance_ReturnsFalse_WhenRequestedDistanceWayOutOfBounds()
        {
            using var form = new Form { Width = 600, Height = 400 };
            using var splitContainer = new SplitContainer
            {
                Orientation = Orientation.Vertical,
                Dock = DockStyle.Fill,
                Panel1MinSize = 100,
                Panel2MinSize = 100
            };

            form.Controls.Add(splitContainer);
            form.Show();
            Application.DoEvents();

            // Request distance way beyond valid range (5000 when max is ~400)
            var result = SafeSplitterDistanceHelper.TrySetSplitterDistance(splitContainer, 5000);

            Assert.False(result);
        }
    }
}
