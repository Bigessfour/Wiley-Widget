using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WileyWidget.WinForms.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Moq;

namespace WileyWidget.Tests.WinForms
{
    public class QuickBooksPanelTests
    {
        [Fact]
        public void QuickBooksPanel_ConstructorAndInitializeComponent_DoesNotThrow()
        {
            // Arrange & Act - This should not throw ArgumentOutOfRangeException
            var (panel, _) = CreatePanelWithMocks();

            // Force layout pass (simulates being added to form)
            panel.SuspendLayout();
            panel.Size = new Size(800, 600);
            panel.ResumeLayout(true);

            // Assert
            Assert.NotNull(panel);
            Assert.NotNull(GetPrivateField(panel, "_splitContainerMain"));
            Assert.NotNull(GetPrivateField(panel, "_syncHistoryGrid"));
            Assert.Equal(720, panel.MinimumSize.Width);   // from designer
            Assert.Equal(520, panel.MinimumSize.Height);
        }

        [Fact]
        public void OnResize_SmallSize_NoSplitterException()
        {
            // Arrange
            var (panel, _) = CreatePanelWithMocks();
            panel.Size = new Size(300, 200);  // below many min sizes

            // Act & Assert
            var exception = Record.Exception(() =>
            {
                CallOnResize(panel, EventArgs.Empty);
                panel.PerformLayout();           // important — many exceptions happen here
            });

            // Assert
            Assert.Null(exception);
        }

        [Theory]
        [InlineData(400, 300)]
        [InlineData(720, 520)]   // minimum from designer
        [InlineData(500, 400)]
        [InlineData(1200, 800)]
        public void OnResize_VariousSizes_NoException(int width, int height)
        {
            // Arrange
            var (panel, _) = CreatePanelWithMocks();
            panel.Size = new Size(width, height);

            // Act & Assert
            var exception = Record.Exception(() => CallOnResize(panel, EventArgs.Empty));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void OnResize_TriggerNestedSplitterConflict_HandlesGracefully()
        {
            // Arrange
            var (panel, _) = CreatePanelWithMocks();

            // Set panel to large size first to allow setting min sizes
            panel.Size = new Size(800, 1000);

            // Get the bottom splitter using reflection
            var splitContainerBottom = GetPrivateField(panel, "_splitContainerBottom");

            // Simulate aggressive min-size conflict (mimic what OnResize used to do)
            if (splitContainerBottom != null)
            {
                var panel1MinSizeProp = splitContainerBottom.GetType().GetProperty("Panel1MinSize");
                var panel2MinSizeProp = splitContainerBottom.GetType().GetProperty("Panel2MinSize");
                panel1MinSizeProp?.SetValue(splitContainerBottom, 400);
                panel2MinSizeProp?.SetValue(splitContainerBottom, 400);
            }
            panel.Size = new Size(800, 500);  // total height 500 < 400+400+splitter

            // Act & Assert
            var exception = Record.Exception(() =>
            {
                CallOnResize(panel, EventArgs.Empty);
                // If you have ClampSplitterSafely → it should prevent crash
            });

            // Assert
            Assert.Null(exception);

            // Optional: assert that SplitterDistance was clamped
            if (splitContainerBottom != null)
            {
                var splitterDistanceProp = splitContainerBottom.GetType().GetProperty("SplitterDistance");
                var panel2MinSizeProp = splitContainerBottom.GetType().GetProperty("Panel2MinSize");
                var splitterWidthProp = splitContainerBottom.GetType().GetProperty("SplitterWidth");

                var splitterDistance = (int?)splitterDistanceProp?.GetValue(splitContainerBottom);
                var panel2MinSize = (int?)panel2MinSizeProp?.GetValue(splitContainerBottom);
                var splitterWidth = (int?)splitterWidthProp?.GetValue(splitContainerBottom);

                if (splitterDistance.HasValue && panel2MinSize.HasValue && splitterWidth.HasValue)
                {
                    int expectedMax = 500 - panel2MinSize.Value - splitterWidth.Value;
                    Assert.True(splitterDistance.Value <= expectedMax);
                }
            }
        }

        [Fact]
        [STAThread]
        public void Panel_WhenAddedToFormAndResized_DoesNotThrow()
        {
            // Arrange
            var (panel, _) = CreatePanelWithMocks();

            using var form = new Form
            {
                Size = new Size(900, 700),
                StartPosition = FormStartPosition.Manual,
                Location = new Point(100, 100),
                ShowInTaskbar = false  // Prevent showing in taskbar during tests
            };

            // Act & Assert
            var exception = Record.Exception(() =>
            {
                form.Controls.Add(panel);
                panel.Dock = DockStyle.Fill;
                form.Show();               // triggers layout
                Application.DoEvents();    // force pending layout

                // Simulate user resize sequence
                form.Size = new Size(600, 400);
                Application.DoEvents();

                form.WindowState = FormWindowState.Minimized;
                Application.DoEvents();
                form.WindowState = FormWindowState.Normal;
                Application.DoEvents();

                form.Size = new Size(1200, 900);
                Application.DoEvents();
            });

            // Assert
            Assert.Null(exception);

            form.Close();
        }

        [Fact]
        public void OnResize_WhenSplitterConflict_ThrowsExpected_OrIsHandled()
        {
            // Arrange
            var (panel, _) = CreatePanelWithMocks();

            // Set panel to large size first to allow setting min sizes
            panel.Size = new Size(800, 2000);

            // Get the main splitter using reflection
            var splitContainerMain = GetPrivateField(panel, "_splitContainerMain");

            // Force bad state
            if (splitContainerMain != null)
            {
                var panel1MinSizeProp = splitContainerMain.GetType().GetProperty("Panel1MinSize");
                var panel2MinSizeProp = splitContainerMain.GetType().GetProperty("Panel2MinSize");
                panel1MinSizeProp?.SetValue(splitContainerMain, 1000);
                panel2MinSizeProp?.SetValue(splitContainerMain, 1000);
            }
            panel.Size = new Size(800, 600);

            // Act
            var ex = Record.Exception(() => CallOnResize(panel, EventArgs.Empty));

            // Assert - now that clamp exists → no throw
            Assert.Null(ex);
        }

        private (QuickBooksPanel panel, IServiceProvider provider) CreatePanelWithMocks()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));

            // Register QuickBooksViewModel dependencies
            var quickBooksServiceMock = new Mock<WileyWidget.Services.Abstractions.IQuickBooksService>();
            services.AddScoped(_ => quickBooksServiceMock.Object);

            var sp = services.BuildServiceProvider();

            // Create scope factory with the built provider
            var scopeFactory = new TestServiceScopeFactory(sp);
            var logger = sp.GetRequiredService<ILogger<QuickBooksPanel>>();

            var panel = new QuickBooksPanel(scopeFactory, logger);
            return (panel, sp);
        }

        private static object? GetPrivateField(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(obj);
        }

        private static void CallOnResize(QuickBooksPanel panel, EventArgs e)
        {
            var method = typeof(QuickBooksPanel).GetMethod("OnResize", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(panel, new object[] { e });
        }
    }

    // Test implementation of IServiceScopeFactory for testing
    internal class TestServiceScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _rootProvider;

        public TestServiceScopeFactory(IServiceProvider rootProvider)
        {
            _rootProvider = rootProvider;
        }

        public IServiceScope CreateScope()
        {
            return new TestServiceScope(_rootProvider.CreateScope());
        }
    }

    internal class TestServiceScope : IServiceScope
    {
        private readonly IServiceScope _innerScope;

        public TestServiceScope(IServiceScope innerScope)
        {
            _innerScope = innerScope;
        }

        public IServiceProvider ServiceProvider => _innerScope.ServiceProvider;
        public void Dispose() => _innerScope.Dispose();
    }
}
