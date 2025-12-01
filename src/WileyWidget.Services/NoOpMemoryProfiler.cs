using System;

namespace WileyWidget.Services
{
    /// <summary>
    /// Minimal no-op memory profiler used for DI fallback in dev environments.
    /// </summary>
    public class NoOpMemoryProfiler : IMemoryProfiler
    {
        public void Snapshot()
        {
            // Intentionally no-op — used to satisfy DI when a real profiler isn't present in dev
        }
    }
}
