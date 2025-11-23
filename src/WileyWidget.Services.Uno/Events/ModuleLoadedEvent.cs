using System;

namespace WileyWidget.Services.Events
{
    /// <summary>
    /// Message published when a module completes loading.
    /// Used for lazy service initialization patterns.
    /// </summary>
    public class ModuleLoadedMessage
    {
        public string ModuleName { get; }
        public object? ModuleInstance { get; }
        public DateTime Timestamp { get; }
        
        public ModuleLoadedMessage(string moduleName, object? moduleInstance = null)
        {
            ModuleName = moduleName;
            ModuleInstance = moduleInstance;
            Timestamp = DateTime.UtcNow;
        }
    }
}
