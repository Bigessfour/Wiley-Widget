// SyncfusionControlTracker.cs - Diagnostic tracker for Syncfusion control lifecycle
// Helps identify undisposed controls and memory leaks
#if DEBUG

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Diagnostics
{
    /// <summary>
    /// Tracks Syncfusion control creation and disposal to detect leaks
    /// </summary>
    public static class SyncfusionControlTracker
    {
        private static readonly ConcurrentDictionary<int, ControlInfo> _activeControls = new();
        private static ILogger? _logger;
        
        public static void Initialize(ILogger logger)
        {
            _logger = logger;
            _logger?.LogInformation("SyncfusionControlTracker initialized - tracking control lifecycles");
        }
        
        public static void TrackCreation(object control, string controlType)
        {
            if (control == null) return;
            
            var hash = control.GetHashCode();
            var stack = new StackTrace(1, true);
            
            var info = new ControlInfo
            {
                ControlType = controlType,
                HashCode = hash,
                CreatedAt = DateTime.UtcNow,
                CreationStack = stack.ToString()
            };
            
            _activeControls.TryAdd(hash, info);
            _logger?.LogDebug("SF Control Created: {Type} (Hash={Hash})", controlType, hash);
        }
        
        public static void TrackDisposal(object control, string controlType)
        {
            if (control == null) return;
            
            var hash = control.GetHashCode();
            
            if (_activeControls.TryRemove(hash, out var info))
            {
                var lifetime = DateTime.UtcNow - info.CreatedAt;
                _logger?.LogDebug("SF Control Disposed: {Type} (Hash={Hash}, Lifetime={Lifetime}ms)", 
                    controlType, hash, lifetime.TotalMilliseconds);
            }
            else
            {
                _logger?.LogWarning("SF Control disposal tracked but not in creation registry: {Type} (Hash={Hash})", 
                    controlType, hash);
            }
        }
        
        public static void LogActiveControls()
        {
            if (_activeControls.IsEmpty)
            {
                _logger?.LogInformation("No active Syncfusion controls tracked");
                return;
            }
            
            _logger?.LogWarning("Active Syncfusion controls (potential leaks): {Count}", _activeControls.Count);
            
            foreach (var kvp in _activeControls)
            {
                var info = kvp.Value;
                var age = DateTime.UtcNow - info.CreatedAt;
                _logger?.LogWarning("  - {Type} (Hash={Hash}, Age={Age}s)\n{Stack}", 
                    info.ControlType, info.HashCode, age.TotalSeconds, info.CreationStack);
            }
        }
        
        private class ControlInfo
        {
            public string ControlType { get; set; } = string.Empty;
            public int HashCode { get; set; }
            public DateTime CreatedAt { get; set; }
            public string CreationStack { get; set; } = string.Empty;
        }
    }
}

#endif
