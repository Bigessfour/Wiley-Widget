using System;

namespace WileyWidget.Services.Abstractions
{
    /// <summary>
    /// Synchronous initialization hook for services that must perform startup work.
    /// </summary>
    public interface IInitializable
    {
        /// <summary>
        /// Perform synchronous initialization work during application startup.
        /// </summary>
        void Initialize();
    }
}