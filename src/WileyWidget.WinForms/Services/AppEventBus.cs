using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Lightweight thread-safe event bus for in-process notifications.
    /// </summary>
    public class AppEventBus : IAppEventBus
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
        private readonly ILogger<AppEventBus>? _logger;

        public AppEventBus(ILogger<AppEventBus>? logger = null)
        {
            _logger = logger;
        }

        public void Publish<TEvent>(TEvent evt)
        {
            if (evt == null) return;

            if (_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                // Snapshot to avoid locking during invocation
                var snapshot = list.ToArray();
                foreach (var d in snapshot)
                {
                    try
                    {
                        ((Action<TEvent>)d)(evt);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "AppEventBus: handler threw for event {EventType}", typeof(TEvent));
                    }
                }
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) return;
            var list = _handlers.GetOrAdd(typeof(TEvent), _ => new List<Delegate>());
            lock (list)
            {
                list.Add(handler);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) return;
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                lock (list)
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                    {
                        _handlers.TryRemove(typeof(TEvent), out _);
                    }
                }
            }
        }
    }
}
