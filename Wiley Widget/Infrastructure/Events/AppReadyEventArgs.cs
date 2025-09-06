using System;

namespace WileyWidget.Infrastructure.Events;

/// <summary>
/// Event arguments for AppReady event with memory and thread information
/// </summary>
public class AppReadyEventArgs : EventArgs
{
    public long MemoryUsageMB { get; }
    public int ThreadCount { get; }
    public TimeSpan StartupTime { get; }
    public DateTime ReadyTimestamp { get; }

    public AppReadyEventArgs(long memoryUsageMB, int threadCount, TimeSpan startupTime)
    {
        MemoryUsageMB = memoryUsageMB;
        ThreadCount = threadCount;
        StartupTime = startupTime;
        ReadyTimestamp = DateTime.UtcNow;
    }
}
