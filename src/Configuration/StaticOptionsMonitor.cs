using System;
using Microsoft.Extensions.Options;
using Serilog;

namespace WileyWidget.Models;

/// <summary>
/// Basic <see cref="IOptionsMonitor{TOptions}"/> implementation used to bridge
/// Microsoft.Extensions.Options patterns into the Prism/Unity container without
/// requiring the full generic host.
/// </summary>
/// <typeparam name="TOptions">Options type.</typeparam>
public sealed class StaticOptionsMonitor<TOptions> : IOptionsMonitor<TOptions> where TOptions : class
{
    private readonly object _syncRoot = new();
    private readonly ILogger _logger;
    private TOptions _currentValue;

    public StaticOptionsMonitor(TOptions currentValue, ILogger logger)
    {
        _currentValue = currentValue ?? throw new ArgumentNullException(nameof(currentValue));
        _logger = logger ?? Log.Logger;
    }

    public TOptions CurrentValue
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentValue;
            }
        }
    }

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<TOptions, string> listener)
    {
        // Static monitor does not support change notifications; log registration for diagnostics.
        if (listener != null)
        {
            _logger.Debug("StaticOptionsMonitor registered change listener for {OptionsType}, but change notifications are not supported in static mode.", typeof(TOptions).Name);
        }

        return NullDisposable.Instance;
    }

    /// <summary>
    /// Updates the current options snapshot.
    /// </summary>
    public void Update(TOptions newValue)
    {
        if (newValue == null)
        {
            throw new ArgumentNullException(nameof(newValue));
        }

        lock (_syncRoot)
        {
            _currentValue = newValue;
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}
