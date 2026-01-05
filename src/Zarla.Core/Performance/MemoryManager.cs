using System.Diagnostics;

namespace Zarla.Core.Performance;

public class MemoryPressureEventArgs : EventArgs
{
    public long TotalMemoryMB { get; init; }
    public long AvailableMemoryMB { get; init; }
    public double UsagePercent { get; init; }
}

public class MemoryManager
{
    private System.Threading.Timer? _monitorTimer;
    private readonly int _maxMemoryPerTabMB;
    private readonly int _warningThresholdPercent;
    private bool _isEnabled = true;

    public event EventHandler<MemoryPressureEventArgs>? HighMemoryPressure;
    public event EventHandler? MemoryCleanupRequested;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public MemoryManager(int maxMemoryPerTabMB = 512, int warningThresholdPercent = 80)
    {
        _maxMemoryPerTabMB = maxMemoryPerTabMB;
        _warningThresholdPercent = warningThresholdPercent;
    }

    public void Start()
    {
        _monitorTimer = new System.Threading.Timer(
            MonitorMemory,
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    public void Stop()
    {
        _monitorTimer?.Dispose();
        _monitorTimer = null;
    }

    private void MonitorMemory(object? state)
    {
        if (!_isEnabled) return;

        try
        {
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / (1024 * 1024);

            // Get system memory info
            var memInfo = GetMemoryInfo();

            if (memInfo.UsagePercent > _warningThresholdPercent)
            {
                HighMemoryPressure?.Invoke(this, memInfo);
            }
        }
        catch
        {
            // Monitoring failed, ignore
        }
    }

    public MemoryPressureEventArgs GetMemoryInfo()
    {
        var process = Process.GetCurrentProcess();
        var workingSetMB = process.WorkingSet64 / (1024 * 1024);

        // Estimate total system memory (simplified)
        var gcMemInfo = GC.GetGCMemoryInfo();
        var totalMB = gcMemInfo.TotalAvailableMemoryBytes / (1024 * 1024);
        var availableMB = totalMB - workingSetMB;
        var usagePercent = (double)workingSetMB / totalMB * 100;

        return new MemoryPressureEventArgs
        {
            TotalMemoryMB = totalMB,
            AvailableMemoryMB = availableMB,
            UsagePercent = usagePercent
        };
    }

    public long GetCurrentMemoryUsageMB()
    {
        return Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
    }

    public void RequestCleanup()
    {
        // Force garbage collection
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect();

        MemoryCleanupRequested?.Invoke(this, EventArgs.Empty);
    }

    public void TrimMemory()
    {
        // Aggressive memory trimming
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();

        // Trim working set
        try
        {
            Process.GetCurrentProcess().MinWorkingSet = (IntPtr)(-1);
        }
        catch
        {
            // May fail without admin rights
        }
    }

    public int MaxMemoryPerTabMB => _maxMemoryPerTabMB;

    public Dictionary<string, long> GetPerTabMemoryEstimate(int tabCount)
    {
        var totalMemory = GetCurrentMemoryUsageMB();
        var baseMemory = 100; // Base browser memory in MB
        var perTabMemory = tabCount > 0 ? (totalMemory - baseMemory) / tabCount : 0;

        return new Dictionary<string, long>
        {
            { "Total", totalMemory },
            { "Base", baseMemory },
            { "PerTab", perTabMemory },
            { "TabCount", tabCount }
        };
    }
}
