using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Zarla.Browser.Services;

public partial class BrowserTab : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = "New Tab";

    [ObservableProperty]
    private string _url = "zarla://newtab";

    [ObservableProperty]
    private string? _favicon;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isSuspended;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    private double _zoomLevel = 100;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isPlaying;

    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
}

public class TabManager : ObservableObject
{
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private BrowserTab? _activeTab;

    public ObservableCollection<BrowserTab> Tabs => _tabs;

    public BrowserTab? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (_activeTab != value)
            {
                if (_activeTab != null)
                    _activeTab.IsActive = false;

                _activeTab = value;

                if (_activeTab != null)
                {
                    _activeTab.IsActive = true;
                    _activeTab.LastActiveAt = DateTime.UtcNow;
                }

                OnPropertyChanged(nameof(ActiveTab));
                ActiveTabChanged?.Invoke(this, _activeTab);
            }
        }
    }

    public event EventHandler<BrowserTab?>? ActiveTabChanged;
    public event EventHandler<BrowserTab>? TabAdded;
    public event EventHandler<BrowserTab>? TabRemoved;
    public event EventHandler<BrowserTab>? TabUpdated;

    public BrowserTab CreateTab(string? url = null, bool activate = true)
    {
        var tab = new BrowserTab
        {
            Url = url ?? "zarla://newtab",
            Title = url == null ? "New Tab" : "Loading..."
        };

        _tabs.Add(tab);
        TabAdded?.Invoke(this, tab);

        if (activate || _tabs.Count == 1)
            ActiveTab = tab;

        return tab;
    }

    public void CloseTab(BrowserTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0) return;

        _tabs.Remove(tab);
        TabRemoved?.Invoke(this, tab);

        // If closing active tab, activate another
        if (tab == _activeTab && _tabs.Count > 0)
        {
            var newIndex = Math.Min(index, _tabs.Count - 1);
            ActiveTab = _tabs[newIndex];
        }
        else if (_tabs.Count == 0)
        {
            // Create new tab if all closed
            CreateTab();
        }
    }

    public void CloseAllTabs()
    {
        var tabs = _tabs.ToList();
        foreach (var tab in tabs)
        {
            if (!tab.IsPinned)
            {
                _tabs.Remove(tab);
                TabRemoved?.Invoke(this, tab);
            }
        }

        if (_tabs.Count == 0)
            CreateTab();
        else
            ActiveTab = _tabs[0];
    }

    public void CloseOtherTabs(BrowserTab keepTab)
    {
        var tabs = _tabs.Where(t => t != keepTab && !t.IsPinned).ToList();
        foreach (var tab in tabs)
        {
            _tabs.Remove(tab);
            TabRemoved?.Invoke(this, tab);
        }

        ActiveTab = keepTab;
    }

    public void MoveTab(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _tabs.Count) return;
        if (toIndex < 0 || toIndex >= _tabs.Count) return;

        _tabs.Move(fromIndex, toIndex);
    }

    public void DuplicateTab(BrowserTab tab)
    {
        var newTab = CreateTab(tab.Url, false);
        newTab.Title = tab.Title;
        newTab.Favicon = tab.Favicon;
    }

    public void ActivateNextTab()
    {
        if (_activeTab == null || _tabs.Count <= 1) return;

        var index = _tabs.IndexOf(_activeTab);
        var nextIndex = (index + 1) % _tabs.Count;
        ActiveTab = _tabs[nextIndex];
    }

    public void ActivatePreviousTab()
    {
        if (_activeTab == null || _tabs.Count <= 1) return;

        var index = _tabs.IndexOf(_activeTab);
        var prevIndex = (index - 1 + _tabs.Count) % _tabs.Count;
        ActiveTab = _tabs[prevIndex];
    }

    public void ActivateTab(int index)
    {
        if (index >= 0 && index < _tabs.Count)
            ActiveTab = _tabs[index];
    }

    public BrowserTab? FindTabById(string id) => _tabs.FirstOrDefault(t => t.Id == id);

    public void UpdateTab(BrowserTab tab)
    {
        TabUpdated?.Invoke(this, tab);
    }

    public void PinTab(BrowserTab tab)
    {
        tab.IsPinned = true;

        // Move pinned tabs to the beginning
        var index = _tabs.IndexOf(tab);
        var pinnedCount = _tabs.Count(t => t.IsPinned && t != tab);

        if (index > pinnedCount)
            _tabs.Move(index, pinnedCount);
    }

    public void UnpinTab(BrowserTab tab)
    {
        tab.IsPinned = false;

        // Move after other pinned tabs
        var index = _tabs.IndexOf(tab);
        var pinnedCount = _tabs.Count(t => t.IsPinned);

        if (index < pinnedCount)
            _tabs.Move(index, pinnedCount);
    }
}
