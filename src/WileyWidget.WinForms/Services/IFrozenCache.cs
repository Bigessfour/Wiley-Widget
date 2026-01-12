using System;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Marker interface for cache implementations that support freezing during shutdown.
    /// When frozen, the cache rejects new writes to prevent data inconsistency during disposal.
    /// </summary>
    public interface IFrozenCache
    {
        /// <summary>
        /// Signals the cache to stop accepting new writes.
        /// Read operations continue, but Set/Remove operations should be no-ops after freezing.
        /// </summary>
        void FreezeCacheWrites();

        /// <summary>
        /// Gets whether the cache is currently frozen.
        /// </summary>
        bool IsFrozen { get; }
    }
}
