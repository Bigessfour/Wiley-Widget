using System;

namespace WileyWidget.Services;

/// <summary>
/// Tracks the status of Syncfusion license registration so other services (health checks, diagnostics) can report accurate information.
/// </summary>
public sealed class SyncfusionLicenseState
{
    private readonly object _syncRoot = new();

    public bool Attempted { get; private set; }

    public bool Registered { get; private set; }

    public string Message { get; private set; } = "License validation has not run.";

    public DateTimeOffset? LastUpdatedUtc { get; private set; }

    public void MarkAttempt(bool registered, string message)
    {
        lock (_syncRoot)
        {
            Attempted = true;
            Registered = registered;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            LastUpdatedUtc = DateTimeOffset.UtcNow;
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            Attempted = false;
            Registered = false;
            Message = "License validation has not run.";
            LastUpdatedUtc = null;
        }
    }
}
