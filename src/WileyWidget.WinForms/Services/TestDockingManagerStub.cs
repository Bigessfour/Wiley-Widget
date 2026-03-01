using System;
using System.Drawing;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Lightweight no-op stub for DockingManager in TEST MODE (Syncfusion 32.2.3)
    /// Eliminates the [CRITICAL] null warnings you saw in flaui-final-run.txt
    /// </summary>
    public class TestDockingManagerStub : DockingManager
    {
        private readonly ILogger<TestDockingManagerStub> _logger;
        private readonly bool _isTestMode;

        public TestDockingManagerStub(ILogger<TestDockingManagerStub> logger, bool isTestMode = true)
        {
            _logger = logger;
            _isTestMode = isTestMode;
            _logger.LogDebug("TestDockingManagerStub created for TEST MODE");
        }

        // All critical methods are no-op in test mode
        public void FloatControl(Control control) { }
        public new void SetAsMDIChild(Control control, bool isMdiChild) { }

        public new void BeginInit() { }
        public new void EndInit() { }

        // Override RecalcHostFormLayout to prevent NRE in test mode
        public new void RecalcHostFormLayout() { }

        public override string ToString() => "TestDockingManagerStub (TEST MODE - no-op)";

        // Expose for any remaining code that checks .IsDisposed or similar
        public bool IsDisposed => false;
    }
}
