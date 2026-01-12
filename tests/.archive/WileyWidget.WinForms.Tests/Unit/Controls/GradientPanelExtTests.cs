using System;
using System.Drawing;
using System.Windows.Forms;
using Xunit;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;
using WileyWidget.WinForms.Forms;

#if FALSE // MCP Server not available as project reference; run MCP server standalone instead

namespace WileyWidget.WinForms.Tests.Unit.Controls
{
    /// <summary>
    /// Unit tests for GradientPanelExt control using WileyWidgetMcpServer helpers.
    /// Tests basic functionality, theme integration, and API surface.
    /// Uses ExecuteOnStaThread for proper WinForms message pump context.
    /// All UI instantiation is wrapped in ExecuteOnStaThread to prevent hangs.
    /// </summary>
    [Collection("Non-UI Panel Tests")]
    public class GradientPanelExtTests
    {
        [Fact]
        public void Constructor_CreatesInstance_Successfully()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange & Act
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Assert
                Assert.NotNull(panel);
                Assert.IsType<GradientPanelExt>(panel);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Constructor_InheritsFromSyncfusion_GradientPanelExt()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange & Act
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Assert
                Assert.IsAssignableFrom<Syncfusion.Windows.Forms.Tools.GradientPanelExt>(panel);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void BorderStyle_CanBeSet()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act
                panel.BorderStyle = BorderStyle.FixedSingle;

                // Assert
                Assert.Equal(BorderStyle.FixedSingle, panel.BorderStyle);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void BorderStyle_CanBeSetToNone()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act
                panel.BorderStyle = BorderStyle.None;

                // Assert
                Assert.Equal(BorderStyle.None, panel.BorderStyle);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Dock_CanBeSet()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act
                panel.Dock = DockStyle.Fill;

                // Assert
                Assert.Equal(DockStyle.Fill, panel.Dock);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Padding_CanBeSet()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();
                var padding = new Padding(10, 5, 10, 5);

                // Act
                panel.Padding = padding;

                // Assert
                Assert.Equal(padding, panel.Padding);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void BackgroundColor_CanBeConfigured()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();
                var brushInfo = new BrushInfo(GradientStyle.Vertical, Color.White, Color.LightGray);

                // Act
                panel.BackgroundColor = brushInfo;

                // Assert
                Assert.NotNull(panel.BackgroundColor);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void BackgroundColor_CanBeSetEmpty()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();
                var brushInfo = new BrushInfo(GradientStyle.Vertical, Color.Empty, Color.Empty);

                // Act
                panel.BackgroundColor = brushInfo;

                // Assert
                Assert.NotNull(panel.BackgroundColor);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void CanAddChildControls()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();
                var label = new Label { Text = "Test Label", Dock = DockStyle.Top };

                // Act
                panel.Controls.Add(label);

                // Assert
                Assert.Single(panel.Controls);
                Assert.Equal(label, panel.Controls[0]);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void CanAddMultipleChildControls()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();
                var label1 = new Label { Text = "Label 1", Dock = DockStyle.Top };
                var label2 = new Label { Text = "Label 2", Dock = DockStyle.Top };
                var button = new Button { Text = "Button", Dock = DockStyle.Bottom };

                // Act
                panel.Controls.Add(label1);
                panel.Controls.Add(label2);
                panel.Controls.Add(button);

                // Assert
                Assert.Equal(3, panel.Controls.Count);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void ChildControls_CanBeRemoved()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();
                var label = new Label { Text = "Test Label" };
                panel.Controls.Add(label);

                // Act
                panel.Controls.Remove(label);

                // Assert
                Assert.Empty(panel.Controls);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void AccessibleName_CanBeSet()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act
                panel.AccessibleName = "Test Panel";

                // Assert
                Assert.Equal("Test Panel", panel.AccessibleName);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Name_CanBeSet()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act
                panel.Name = "TestGradientPanel";

                // Assert
                Assert.Equal("TestGradientPanel", panel.Name);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void TabIndex_CanBeSet()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act
                panel.TabIndex = 5;

                // Assert
                Assert.Equal(5, panel.TabIndex);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Size_CanBeSet()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();
                var size = new Size(200, 150);

                // Act
                panel.Size = size;

                // Assert
                Assert.Equal(200, panel.Width);
                Assert.Equal(150, panel.Height);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Location_CanBeSet()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();
                var location = new Point(10, 20);

                // Act
                panel.Location = location;

                // Assert
                Assert.Equal(10, panel.Left);
                Assert.Equal(20, panel.Top);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Visible_CanBeToggled()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act
                panel.Visible = false;

                // Assert
                Assert.False(panel.Visible);

                // Act
                panel.Visible = true;

                // Assert
                Assert.True(panel.Visible);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Enabled_CanBeToggled()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act
                panel.Enabled = false;

                // Assert
                Assert.False(panel.Enabled);

                // Act
                panel.Enabled = true;

                // Assert
                Assert.True(panel.Enabled);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Dispose_ReleasesResources()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act & Assert
                panel.Dispose();
                // If we reach here without exception, disposal was successful

                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void MultipleInstances_AreIndependent()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange & Act
                var panel1 = new WileyWidget.WinForms.Controls.GradientPanelExt { Name = "Panel1" };
                var panel2 = new WileyWidget.WinForms.Controls.GradientPanelExt { Name = "Panel2" };

                // Assert
                Assert.NotEqual(panel1.Name, panel2.Name);

                // Cleanup
                panel1.Dispose();
                panel2.Dispose();

                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void GradientStyle_CanBeApplied()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();
                var brush = new BrushInfo(GradientStyle.Horizontal, Color.Blue, Color.Green);

                // Act
                panel.BackgroundColor = brush;

                // Assert
                Assert.NotNull(panel.BackgroundColor);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Theme_CanBeApplied()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act - apply theme via SfSkinManager
                Syncfusion.Windows.Forms.SkinManager.SetVisualStyle(panel, "Office2019Colorful");

                // Assert - theme should apply without error
                Assert.NotNull(panel);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }

        [Fact]
        public void Panel_HasNoManualColorViolations()
        {
            var result = FormInstantiationHelper.ExecuteOnStaThread(() =>
            {
                // Arrange
                var panel = new WileyWidget.WinForms.Controls.GradientPanelExt();

                // Act & Assert - GradientPanelExt should not have manual colors
                var violations = SyncfusionTestHelper.ValidateNoManualColors(panel);
                Assert.Empty(violations);

                panel.Dispose();
                return true;
            }, timeoutSeconds: 10);

            Assert.True(result);
        }
    }
}

#endif // MCP Server integration disabled
