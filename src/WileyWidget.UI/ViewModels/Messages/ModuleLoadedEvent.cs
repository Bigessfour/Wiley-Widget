#nullable enable

using Prism.Events;
using Prism.Modularity;

namespace WileyWidget.ViewModels.Messages
{
    /// <summary>
    /// Event published when a Prism module has been loaded and initialized.
    /// Used by LazyQuickBooksService to detect when QuickBooksModule is ready.
    ///
    /// Pattern: Prism-Samples-Wpf EventAggregator cross-module communication
    /// Reference: https://github.com/PrismLibrary/Prism-Samples-Wpf
    /// </summary>
    public class ModuleLoadedEvent : PubSubEvent<ModuleLoadedEventPayload>
    {
    }

    /// <summary>
    /// Payload for ModuleLoadedEvent containing module information.
    /// </summary>
    public class ModuleLoadedEventPayload
    {
        /// <summary>
        /// Name of the module that was loaded (e.g., "QuickBooksModule").
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Reference to the module instance (for advanced scenarios).
        /// </summary>
        public IModule? ModuleInstance { get; set; }

        /// <summary>
        /// Timestamp when the module was loaded.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
