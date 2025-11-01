using System;
using Prism.Events;
using Prism.Modularity;

namespace WileyWidget.Services.Events
{
    /// <summary>
    /// Event published when a Prism module completes loading.
    /// Used for lazy service initialization patterns.
    /// </summary>
    public class ModuleLoadedEvent : PubSubEvent<ModuleLoadedEventPayload>
    {
    }

    /// <summary>
    /// Payload for ModuleLoadedEvent containing module information.
    /// </summary>
    public class ModuleLoadedEventPayload
    {
        public string ModuleName { get; set; } = string.Empty;
        public IModule? ModuleInstance { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
