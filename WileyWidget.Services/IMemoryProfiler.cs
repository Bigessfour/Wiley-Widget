using System;

namespace WileyWidget.Services;

/// <summary>
/// Interface for memory profiling and monitoring
/// </summary>
public interface IMemoryProfiler
{
    /// <summary>
    /// Gets the current memory usage in MB
    /// </summary>
    double GetCurrentMemoryUsage();

    /// <summary>
    /// Gets detailed memory statistics
    /// </summary>
    MemoryStats GetMemoryStats();
}

/// <summary>
/// Memory statistics container
/// </summary>
public class MemoryStats
{
    public double TotalMemoryMB { get; set; }
    public double WorkingSetMB { get; set; }
    public double PrivateMemoryMB { get; set; }
    public double VirtualMemoryMB { get; set; }
    public double GCMemoryMB { get; set; }
    public int GCCollectionsGen0 { get; set; }
    public int GCCollectionsGen1 { get; set; }
    public int GCCollectionsGen2 { get; set; }
}
