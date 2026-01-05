namespace Zarla.Core.Performance;

public class TabSuspendedEventArgs : EventArgs
{
    public required string TabId { get; init; }
}

public class TabSuspender
{
    private readonly Dictionary<string, DateTime> _tabLastActivity = new();
    private readonly HashSet<string> _suspendedTabs = new();
    private readonly HashSet<string> _pinnedTabs = new();
    private readonly HashSet<string> _whitelistedDomains = new();
    private System.Threading.Timer? _checkTimer;
    private bool _isEnabled = true;
    private int _suspensionTimeoutMinutes = 5;

    public event EventHandler<TabSuspendedEventArgs>? TabSuspended;
    public event EventHandler<TabSuspendedEventArgs>? TabWoken;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!value) WakeAllTabs();
        }
    }

    public int SuspensionTimeoutMinutes
    {
        get => _suspensionTimeoutMinutes;
        set => _suspensionTimeoutMinutes = Math.Max(1, value);
    }

    public void Start()
    {
        _checkTimer = new System.Threading.Timer(CheckTabs, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public void Stop()
    {
        _checkTimer?.Dispose();
        _checkTimer = null;
    }

    public void RegisterTab(string tabId)
    {
        _tabLastActivity[tabId] = DateTime.UtcNow;
    }

    public void UnregisterTab(string tabId)
    {
        _tabLastActivity.Remove(tabId);
        _suspendedTabs.Remove(tabId);
        _pinnedTabs.Remove(tabId);
    }

    public void RecordActivity(string tabId)
    {
        _tabLastActivity[tabId] = DateTime.UtcNow;

        if (_suspendedTabs.Contains(tabId))
        {
            WakeTab(tabId);
        }
    }

    public void SetTabPinned(string tabId, bool pinned)
    {
        if (pinned)
        {
            _pinnedTabs.Add(tabId);
            if (_suspendedTabs.Contains(tabId))
                WakeTab(tabId);
        }
        else
        {
            _pinnedTabs.Remove(tabId);
        }
    }

    public bool IsTabSuspended(string tabId) => _suspendedTabs.Contains(tabId);

    public bool IsTabPinned(string tabId) => _pinnedTabs.Contains(tabId);

    private void CheckTabs(object? state)
    {
        if (!_isEnabled) return;

        var now = DateTime.UtcNow;
        var timeout = TimeSpan.FromMinutes(_suspensionTimeoutMinutes);

        foreach (var (tabId, lastActivity) in _tabLastActivity.ToList())
        {
            if (_pinnedTabs.Contains(tabId)) continue;
            if (_suspendedTabs.Contains(tabId)) continue;

            if (now - lastActivity > timeout)
            {
                SuspendTab(tabId);
            }
        }
    }

    private void SuspendTab(string tabId)
    {
        _suspendedTabs.Add(tabId);
        TabSuspended?.Invoke(this, new TabSuspendedEventArgs { TabId = tabId });
    }

    private void WakeTab(string tabId)
    {
        if (_suspendedTabs.Remove(tabId))
        {
            _tabLastActivity[tabId] = DateTime.UtcNow;
            TabWoken?.Invoke(this, new TabSuspendedEventArgs { TabId = tabId });
        }
    }

    public void WakeAllTabs()
    {
        foreach (var tabId in _suspendedTabs.ToList())
        {
            WakeTab(tabId);
        }
    }

    public void AddWhitelistedDomain(string domain)
    {
        _whitelistedDomains.Add(domain.ToLowerInvariant());
    }

    public void RemoveWhitelistedDomain(string domain)
    {
        _whitelistedDomains.Remove(domain.ToLowerInvariant());
    }

    public bool IsDomainWhitelisted(string url)
    {
        try
        {
            var host = new Uri(url).Host.ToLowerInvariant();
            return _whitelistedDomains.Any(d => host.EndsWith(d) || host == d);
        }
        catch
        {
            return false;
        }
    }

    public int SuspendedTabCount => _suspendedTabs.Count;

    public IReadOnlyCollection<string> GetSuspendedTabs() => _suspendedTabs.ToList();
}
