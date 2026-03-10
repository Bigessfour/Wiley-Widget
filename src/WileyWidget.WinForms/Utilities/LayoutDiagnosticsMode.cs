using System;
using System.Linq;
using System.Threading;

namespace WileyWidget.WinForms.Utilities
{
    internal static class LayoutDiagnosticsMode
    {
        private static readonly AsyncLocal<int> ScopeDepth = new();

        public static bool IsActive =>
            ScopeDepth.Value > 0 ||
            Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--layout-diagnostics-all-panels", StringComparison.OrdinalIgnoreCase));

        public static IDisposable EnterScope()
        {
            ScopeDepth.Value = ScopeDepth.Value + 1;
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                ScopeDepth.Value = Math.Max(0, ScopeDepth.Value - 1);
            }
        }
    }
}