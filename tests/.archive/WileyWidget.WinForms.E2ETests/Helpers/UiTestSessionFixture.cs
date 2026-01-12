using System;

namespace WileyWidget.WinForms.E2ETests.Helpers
{
    /// <summary>
    /// xUnit fixture that ensures the shared UI test session is cleaned up after the collection completes.
    /// </summary>
    public sealed class UiTestSessionFixture : IDisposable
    {
        public void Dispose()
        {
            UiTestSession.Reset();
        }
    }
}
