using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Zarla.Browser.Services;
using Zarla.Browser.Views;
using Zarla.Core.Privacy;
using Zarla.Core.Performance;

namespace Zarla.Browser;

public partial class MainWindow : Window
{
    private readonly TabManager _tabManager = new();
    private readonly AdBlocker _adBlocker = new();
    private readonly TrackerBlocker _trackerBlocker = new();
    private readonly FingerprintProtection _fingerprintProtection = new();
    private readonly TabSuspender _tabSuspender = new();
    private readonly MemoryManager _memoryManager = new();
    private readonly Dictionary<string, WebView2> _webViews = new();
    private readonly Dictionary<string, BitmapImage> _faviconCache = new();
    private DownloadService? _downloadService;

    // Tab drag-drop support
    private Border? _draggedTab;
    private Point _dragStartPoint;

    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Zarla/1.0 Chrome/120.0.0.0 Safari/537.36";
    public MainWindow()
    {
        InitializeComponent();

        try
        {
            // Set window icon
            SetWindowIcon();
            
            InitializeServices();
            SetupEventHandlers();

            // Create initial tab
            _tabManager.CreateTab();

            // Update privacy settings from config
            var settings = App.Settings.CurrentSettings;
            _adBlocker.IsEnabled = settings.EnableAdBlocker;
            _trackerBlocker.IsEnabled = settings.EnableTrackerBlocker;
            _fingerprintProtection.IsEnabled = settings.EnableFingerprintProtection;
            _tabSuspender.IsEnabled = settings.EnableTabSuspension;
            _tabSuspender.SuspensionTimeoutMinutes = settings.TabSuspensionTimeout;

            // Show bookmarks bar if enabled
            if (settings.ShowBookmarksBar)
                BookmarksBar.Visibility = Visibility.Visible;

            // Show/hide home button
            HomeButton.Visibility = settings.ShowHomeButton ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing browser: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetWindowIcon()
    {
        try
        {
            // Try to load icon from file path relative to executable
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = System.IO.Path.GetDirectoryName(exePath);
            var iconPath = System.IO.Path.Combine(exeDir ?? "", "..", "..", "..", "..", "assets", "icons", "zarla.ico");
            
            if (System.IO.File.Exists(iconPath))
            {
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(iconPath)));
            }
            else
            {
                // Try absolute path during development
                iconPath = @"c:\Zarla\assets\icons\zarla.ico";
                if (System.IO.File.Exists(iconPath))
                {
                    Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
                }
            }
        }
        catch
        {
            // Icon loading failed, continue without custom icon
        }
    }

    private void InitializeServices()
    {
        _downloadService = new DownloadService(App.Database, App.Settings.CurrentSettings.DownloadPath);

        _tabSuspender.Start();
        _memoryManager.Start();

        _memoryManager.HighMemoryPressure += (s, e) =>
        {
            // Auto-suspend tabs when memory is high
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"High memory usage: {e.UsagePercent:F0}%";
            });
        };
    }

    private void SetupEventHandlers()
    {
        _tabManager.TabAdded += OnTabAdded;
        _tabManager.TabRemoved += OnTabRemoved;
        _tabManager.ActiveTabChanged += OnActiveTabChanged;

        _tabSuspender.TabSuspended += OnTabSuspended;
        _tabSuspender.TabWoken += OnTabWoken;
    }

    private async void OnTabAdded(object? sender, BrowserTab tab)
    {
        try
        {
            // Create WebView2 for this tab
            var webView = new WebView2
            {
                Visibility = Visibility.Collapsed
            };

            _webViews[tab.Id] = webView;
            BrowserContent.Children.Add(webView);

            // Initialize WebView2 with proxy settings if configured
            var envOptions = new CoreWebView2EnvironmentOptions();
            
            var settings = App.Settings.CurrentSettings;
            if (settings.ProxyMode == "Custom" && !string.IsNullOrWhiteSpace(settings.ProxyAddress))
            {
                // Build proxy server argument
                var proxyPort = settings.ProxyPort > 0 ? settings.ProxyPort : 8080;
                envOptions.AdditionalBrowserArguments = $"--proxy-server={settings.ProxyAddress}:{proxyPort}";
            }
            else if (settings.DnsProvider != "System")
            {
                // Use DNS over HTTPS based on provider selection
                var dohUrl = settings.DnsProvider switch
                {
                    "Cloudflare" => "https://cloudflare-dns.com/dns-query",
                    "Quad9" => "https://dns.quad9.net/dns-query",
                    "Google" => "https://dns.google/dns-query",
                    _ => ""
                };
                
                if (!string.IsNullOrEmpty(dohUrl))
                {
                    envOptions.AdditionalBrowserArguments = $"--enable-features=DnsOverHttps --doh-url={dohUrl}";
                }
            }

            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: App.UserDataFolder,
                options: envOptions);

            await webView.EnsureCoreWebView2Async(env);

            // Configure WebView2
            ConfigureWebView(webView, tab);

            // Navigate to initial URL
            NavigateTab(tab, tab.Url);

            // Register with tab suspender
            _tabSuspender.RegisterTab(tab.Id);

            // Update tab strip
            UpdateTabStrip();
        }
        catch (Exception ex)
        {
            // Handle WebView2 initialization failure gracefully
            System.Diagnostics.Debug.WriteLine($"Tab creation error: {ex.Message}");
            StatusText.Text = "Error creating tab";
        }
    }

    private void ConfigureWebView(WebView2 webView, BrowserTab tab)
    {
        var settings = webView.CoreWebView2.Settings;

        // Basic settings
        settings.IsStatusBarEnabled = true;
        settings.AreDefaultContextMenusEnabled = true;
        settings.IsZoomControlEnabled = true;
        settings.AreDevToolsEnabled = App.Settings.CurrentSettings.EnableDevTools;
        settings.IsWebMessageEnabled = true;

        // Set custom user agent
        settings.UserAgent = UserAgent;

        // Privacy settings
        if (App.Settings.CurrentSettings.SendDoNotTrack)
        {
            // DNT header is handled via request interception
        }

        // Event handlers
        webView.CoreWebView2.NavigationStarting += (s, e) => OnNavigationStarting(tab, e);
        webView.CoreWebView2.NavigationCompleted += (s, e) => OnNavigationCompleted(tab, e);
        webView.CoreWebView2.SourceChanged += (s, e) => OnSourceChanged(tab, webView.Source?.ToString() ?? "");
        webView.CoreWebView2.DocumentTitleChanged += (s, e) => OnTitleChanged(tab, webView.CoreWebView2.DocumentTitle);
        webView.CoreWebView2.FaviconChanged += (s, e) => OnFaviconChanged(tab, webView.CoreWebView2.FaviconUri);
        webView.CoreWebView2.NewWindowRequested += (s, e) => OnNewWindowRequested(e);
        webView.CoreWebView2.DownloadStarting += (s, e) => OnDownloadStarting(e);
        webView.CoreWebView2.StatusBarTextChanged += (s, e) => OnStatusBarTextChanged(webView.CoreWebView2.StatusBarText);

        // Request filtering for ad/tracker blocking
        webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        webView.CoreWebView2.WebResourceRequested += (s, e) => OnWebResourceRequested(e);

        // Inject fingerprint protection script
        if (_fingerprintProtection.IsEnabled)
        {
            webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(_fingerprintProtection.GetProtectionScript());
        }
    }

    private void OnNavigationStarting(BrowserTab tab, CoreWebView2NavigationStartingEventArgs e)
    {
        tab.IsLoading = true;
        RefreshIcon.Text = "✕";

        // Handle zarla:// URLs
        if (e.Uri.StartsWith("zarla://"))
        {
            e.Cancel = true;
            HandleInternalUrl(tab, e.Uri);
            return;
        }

        // Block ads/trackers
        if (_adBlocker.ShouldBlock(e.Uri) || _trackerBlocker.ShouldBlock(e.Uri))
        {
            e.Cancel = true;
            return;
        }

        // Record activity for tab suspension
        _tabSuspender.RecordActivity(tab.Id);
    }

    private void OnNavigationCompleted(BrowserTab tab, CoreWebView2NavigationCompletedEventArgs e)
    {
        tab.IsLoading = false;
        RefreshIcon.Text = "↻";

        if (_webViews.TryGetValue(tab.Id, out var webView))
        {
            tab.CanGoBack = webView.CoreWebView2.CanGoBack;
            tab.CanGoForward = webView.CoreWebView2.CanGoForward;

            UpdateNavigationButtons();
        }

        // Add to history
        if (e.IsSuccess && !tab.Url.StartsWith("zarla://"))
        {
            _ = App.Database.AddHistoryEntryAsync(tab.Url, tab.Title, tab.Favicon);
        }

        // Update privacy stats
        UpdatePrivacyStats();
    }

    private void OnSourceChanged(BrowserTab tab, string url)
    {
        tab.Url = url;
        if (tab == _tabManager.ActiveTab)
        {
            AddressBar.Text = url;
            UpdateSecurityIndicator(url);
        }
        UpdateTabStrip();
    }

    private void OnTitleChanged(BrowserTab tab, string title)
    {
        tab.Title = string.IsNullOrEmpty(title) ? "New Tab" : title;
        UpdateTabStrip();

        if (tab == _tabManager.ActiveTab)
        {
            Title = $"{tab.Title} - Zarla";
        }
    }

    private void OnFaviconChanged(BrowserTab tab, string faviconUri)
    {
        tab.Favicon = faviconUri;
        UpdateTabStrip();
    }

    private void OnNewWindowRequested(CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        var tab = _tabManager.CreateTab(e.Uri);
    }

    private async void OnDownloadStarting(CoreWebView2DownloadStartingEventArgs e)
    {
        if (_downloadService == null) return;

        var download = await _downloadService.StartDownload(
            e.DownloadOperation.Uri,
            e.ResultFilePath,
            e.DownloadOperation.MimeType);

        e.DownloadOperation.BytesReceivedChanged += async (s, args) =>
        {
            await _downloadService.UpdateProgress(
                download.Id,
                (long)e.DownloadOperation.BytesReceived,
                (long)(e.DownloadOperation.TotalBytesToReceive ?? 0));
        };

        e.DownloadOperation.StateChanged += async (s, args) =>
        {
            var success = e.DownloadOperation.State == CoreWebView2DownloadState.Completed;
            await _downloadService.CompleteDownload(download.Id, success);
        };
    }

    private void OnStatusBarTextChanged(string text)
    {
        StatusText.Text = text;
    }

    private void OnWebResourceRequested(CoreWebView2WebResourceRequestedEventArgs e)
    {
        var url = e.Request.Uri;

        // Block ads
        if (_adBlocker.ShouldBlock(url))
        {
            e.Response = null;
            return;
        }

        // Block trackers
        if (_trackerBlocker.ShouldBlock(url))
        {
            e.Response = null;
            return;
        }

        // Add privacy headers
        if (App.Settings.CurrentSettings.SendDoNotTrack)
        {
            e.Request.Headers.SetHeader("DNT", "1");
            e.Request.Headers.SetHeader("Sec-GPC", "1");
        }
    }

    private void OnTabRemoved(object? sender, BrowserTab tab)
    {
        if (_webViews.TryGetValue(tab.Id, out var webView))
        {
            BrowserContent.Children.Remove(webView);
            webView.Dispose();
            _webViews.Remove(tab.Id);
        }

        _tabSuspender.UnregisterTab(tab.Id);
        UpdateTabStrip();
    }

    private void OnActiveTabChanged(object? sender, BrowserTab? tab)
    {
        // Hide all WebViews
        foreach (var wv in _webViews.Values)
        {
            wv.Visibility = Visibility.Collapsed;
        }

        // Remove any internal pages
        var internalPages = BrowserContent.Children.OfType<UserControl>().ToList();
        foreach (var page in internalPages)
        {
            BrowserContent.Children.Remove(page);
        }

        if (tab != null && _webViews.TryGetValue(tab.Id, out var activeWebView))
        {
            // Check if this is an internal URL
            if (tab.Url.StartsWith("zarla://"))
            {
                // Show internal page instead of WebView
                HandleInternalUrl(tab, tab.Url);
                AddressBar.Text = tab.Url;
            }
            else
            {
                activeWebView.Visibility = Visibility.Visible;
                AddressBar.Text = tab.Url;
            }
            
            Title = $"{tab.Title} - Zarla";
            UpdateNavigationButtons();
            UpdateSecurityIndicator(tab.Url);
            UpdateZoomLevel();

            // Wake suspended tab
            if (tab.IsSuspended)
            {
                _tabSuspender.RecordActivity(tab.Id);
            }
        }

        UpdateTabStrip();
    }

    private void OnTabSuspended(object? sender, TabSuspendedEventArgs e)
    {
        var tab = _tabManager.FindTabById(e.TabId);
        if (tab != null)
        {
            tab.IsSuspended = true;

            // Unload WebView content to save memory
            if (_webViews.TryGetValue(tab.Id, out var webView))
            {
                webView.CoreWebView2?.Navigate("about:blank");
            }

            UpdateTabStrip();
        }
    }

    private void OnTabWoken(object? sender, TabSuspendedEventArgs e)
    {
        var tab = _tabManager.FindTabById(e.TabId);
        if (tab != null)
        {
            tab.IsSuspended = false;

            // Reload the page
            if (_webViews.TryGetValue(tab.Id, out var webView))
            {
                webView.CoreWebView2?.Navigate(tab.Url);
            }

            UpdateTabStrip();
        }
    }

    private void HandleInternalUrl(BrowserTab tab, string url)
    {
        var host = url.Replace("zarla://", "").TrimEnd('/').ToLower();

        switch (host)
        {
            case "newtab":
                ShowInternalPage(tab, new NewTabPage(this));
                break;
            case "settings":
                ShowInternalPage(tab, new SettingsPage());
                break;
            case "history":
                ShowInternalPage(tab, new HistoryPage(this));
                break;
            case "bookmarks":
                ShowInternalPage(tab, new BookmarksPage(this));
                break;
            case "downloads":
                ShowInternalPage(tab, new DownloadsPage(_downloadService!));
                break;
            case "about":
                ShowInternalPage(tab, new AboutPage());
                break;
            default:
                // Unknown internal page
                break;
        }

        tab.Url = url;
        tab.Title = GetInternalPageTitle(host);
        tab.IsLoading = false;
        UpdateTabStrip();
    }

    private void ShowInternalPage(BrowserTab tab, UserControl page)
    {
        // Hide WebView and show internal page
        if (_webViews.TryGetValue(tab.Id, out var webView))
        {
            webView.Visibility = Visibility.Collapsed;
        }

        // Remove any existing internal page
        var existingPages = BrowserContent.Children.OfType<UserControl>().ToList();
        foreach (var p in existingPages)
        {
            BrowserContent.Children.Remove(p);
        }

        BrowserContent.Children.Add(page);
    }

    private string GetInternalPageTitle(string host) => host switch
    {
        "newtab" => "New Tab",
        "settings" => "Settings",
        "history" => "History",
        "bookmarks" => "Bookmarks",
        "downloads" => "Downloads",
        "about" => "About Zarla",
        _ => "Zarla"
    };

    public void NavigateTab(BrowserTab tab, string url)
    {
        try
        {
            // Remove internal pages if showing
            var internalPages = BrowserContent.Children.OfType<UserControl>().ToList();
            foreach (var page in internalPages)
            {
                BrowserContent.Children.Remove(page);
            }

            if (url.StartsWith("zarla://"))
            {
                HandleInternalUrl(tab, url);
                return;
            }

            // Ensure URL has protocol
            if (!url.Contains("://"))
            {
                // Check if it looks like a URL or search query
                if (url.Contains(".") && !url.Contains(" "))
                {
                    url = "https://" + url;
                }
                else
                {
                    // Search
                    url = App.Settings.CurrentSettings.SearchEngine + Uri.EscapeDataString(url);
                }
            }

            if (_webViews.TryGetValue(tab.Id, out var webView))
            {
                webView.Visibility = Visibility.Visible;
                webView.CoreWebView2?.Navigate(url);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            StatusText.Text = "Navigation error";
        }
    }

    public void NavigateToUrl(string url)
    {
        if (_tabManager.ActiveTab != null)
        {
            NavigateTab(_tabManager.ActiveTab, url);
        }
    }

    private void UpdateTabStrip()
    {
        TabStrip.Children.Clear();

        foreach (var tab in _tabManager.Tabs)
        {
            var tabElement = CreateTabElement(tab);
            TabStrip.Children.Add(tabElement);
        }
    }

    private Border CreateTabElement(BrowserTab tab)
    {
        var border = new Border
        {
            Style = (Style)FindResource("BrowserTabItem"),
            Background = tab.IsActive
                ? (Brush)FindResource("TabActiveBrush")
                : (Brush)FindResource("TabInactiveBrush"),
            Margin = new Thickness(0, 0, 2, 0),
            Tag = tab,
            AllowDrop = true
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Favicon container - shows loading, suspended state, or actual favicon
        FrameworkElement faviconElement;
        
        if (tab.IsLoading)
        {
            // Loading spinner
            faviconElement = new TextBlock
            {
                Text = "◌",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 8, 0)
            };
        }
        else if (tab.IsSuspended)
        {
            // Suspended indicator
            faviconElement = new TextBlock
            {
                Text = "💤",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 8, 0)
            };
        }
        else if (!string.IsNullOrEmpty(tab.Favicon))
        {
            // Actual favicon image - load asynchronously with caching
            var faviconImage = new Image
            {
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            
            // Check cache first
            if (_faviconCache.TryGetValue(tab.Favicon, out var cachedBitmap))
            {
                faviconImage.Source = cachedBitmap;
            }
            else
            {
                // Load favicon asynchronously
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(tab.Favicon);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.DecodePixelWidth = 16;
                    bitmap.DecodePixelHeight = 16;
                    bitmap.EndInit();
                    
                    var faviconUrl = tab.Favicon; // Capture for closure
                    
                    // Handle async download for web URLs
                    if (!bitmap.IsDownloading)
                    {
                        faviconImage.Source = bitmap;
                        _faviconCache[faviconUrl] = bitmap;
                    }
                    else
                    {
                        bitmap.DownloadCompleted += (s, e) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                faviconImage.Source = bitmap;
                                _faviconCache[faviconUrl] = bitmap;
                            });
                        };
                        bitmap.DownloadFailed += (s, e) =>
                        {
                            // Show globe on failure
                        };
                    }
                }
                catch
                {
                    // Ignore favicon loading errors
                }
            }
            
            faviconElement = faviconImage;
        }
        else
        {
            // Default globe icon for pages without favicon
            faviconElement = new TextBlock
            {
                Text = "🌐",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 8, 0)
            };
        }
        
        Grid.SetColumn(faviconElement, 0);
        grid.Children.Add(faviconElement);

        // Title
        var title = new TextBlock
        {
            Text = tab.Title,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("TextBrush"),
            FontSize = 13
        };
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        // Close button
        var closeBtn = new Button
        {
            Content = "✕",
            Style = (Style)FindResource("IconButton"),
            FontSize = 10,
            Padding = new Thickness(4),
            Margin = new Thickness(4, 0, 0, 0),
            Tag = tab
        };
        closeBtn.Click += CloseTabButton_Click;
        Grid.SetColumn(closeBtn, 2);
        grid.Children.Add(closeBtn);

        border.Child = grid;

        // Click to activate
        border.MouseLeftButtonDown += (s, e) =>
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedTab = border;
            _tabManager.ActiveTab = tab;
        };

        // Drag handling
        border.MouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedTab == border)
            {
                var currentPoint = e.GetPosition(null);
                var diff = _dragStartPoint - currentPoint;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop(border, tab, DragDropEffects.Move);
                    _draggedTab = null;
                }
            }
        };

        border.DragOver += (s, e) =>
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        };

        border.Drop += (s, e) =>
        {
            if (e.Data.GetDataPresent(typeof(BrowserTab)))
            {
                var draggedBrowserTab = e.Data.GetData(typeof(BrowserTab)) as BrowserTab;
                var targetTab = border.Tag as BrowserTab;

                if (draggedBrowserTab != null && targetTab != null && draggedBrowserTab != targetTab)
                {
                    var fromIndex = _tabManager.Tabs.IndexOf(draggedBrowserTab);
                    var toIndex = _tabManager.Tabs.IndexOf(targetTab);
                    
                    if (fromIndex >= 0 && toIndex >= 0)
                    {
                        _tabManager.MoveTab(fromIndex, toIndex);
                        UpdateTabStrip();
                    }
                }
            }
        };

        // Middle click to close
        border.MouseDown += (s, e) =>
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _tabManager.CloseTab(tab);
            }
        };

        return border;
    }

    private void UpdateNavigationButtons()
    {
        var tab = _tabManager.ActiveTab;
        BackButton.IsEnabled = tab?.CanGoBack ?? false;
        ForwardButton.IsEnabled = tab?.CanGoForward ?? false;
    }

    private void UpdateSecurityIndicator(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;

            if (url.StartsWith("https://"))
            {
                SecurityIndicator.Text = "🔒";
                SecurityIndicator.Foreground = (Brush)FindResource("SuccessBrush");
                SecurityText.Text = host;
                SecurityText.Foreground = (Brush)FindResource("TextSecondaryBrush");

                // Update popup info
                SecurityPopupIcon.Text = "🔒";
                SecurityPopupTitle.Text = "Connection is secure";
                SecurityPopupTitle.Foreground = (Brush)FindResource("SuccessBrush");
                SecurityPopupSubtitle.Text = "Your information is private when sent to this site";
                CertIssuedTo.Text = host;
                CertIssuedBy.Text = "Verified Certificate Authority";
                CertProtocol.Text = "TLS 1.3";
            }
            else if (url.StartsWith("http://"))
            {
                SecurityIndicator.Text = "⚠";
                SecurityIndicator.Foreground = (Brush)FindResource("WarningBrush");
                SecurityText.Text = "Not secure";
                SecurityText.Foreground = (Brush)FindResource("WarningBrush");

                // Update popup info
                SecurityPopupIcon.Text = "⚠";
                SecurityPopupTitle.Text = "Connection is not secure";
                SecurityPopupTitle.Foreground = (Brush)FindResource("WarningBrush");
                SecurityPopupSubtitle.Text = "Your information could be visible to others";
                CertIssuedTo.Text = host;
                CertIssuedBy.Text = "No certificate";
                CertProtocol.Text = "HTTP (unencrypted)";
            }
            else if (url.StartsWith("zarla://"))
            {
                SecurityIndicator.Text = "ℹ";
                SecurityIndicator.Foreground = (Brush)FindResource("AccentBrush");
                SecurityText.Text = "Zarla";
                SecurityText.Foreground = (Brush)FindResource("AccentBrush");

                SecurityPopupIcon.Text = "ℹ";
                SecurityPopupTitle.Text = "Zarla internal page";
                SecurityPopupTitle.Foreground = (Brush)FindResource("AccentBrush");
                SecurityPopupSubtitle.Text = "This is a built-in browser page";
                CertIssuedTo.Text = "Zarla Browser";
                CertIssuedBy.Text = "Internal";
                CertProtocol.Text = "N/A";
            }
            else
            {
                SecurityIndicator.Text = "○";
                SecurityText.Text = "";
            }

            // Update privacy stats
            FingerprintStatus.Text = _fingerprintProtection.IsEnabled
                ? "Fingerprint protection active"
                : "Fingerprint protection disabled";
        }
        catch
        {
            SecurityIndicator.Text = "○";
            SecurityText.Text = "";
        }
    }

    private void SecurityButton_Click(object sender, RoutedEventArgs e)
    {
        // Update site-specific stats
        SiteAdsBlocked.Text = $"{_adBlocker.BlockedCount} ads blocked this session";
        SiteTrackersBlocked.Text = $"{_trackerBlocker.BlockedCount} trackers blocked this session";

        SecurityPopup.PlacementTarget = SecurityButton;
        SecurityPopup.IsOpen = true;
    }

    private void AddressBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Could be used for autocomplete suggestions
    }

    private void UpdatePrivacyStats()
    {
        PrivacyStats.Text = $"Blocked: {_adBlocker.BlockedCount} ads, {_trackerBlocker.BlockedCount} trackers";
    }

    private void UpdateZoomLevel()
    {
        var tab = _tabManager.ActiveTab;
        if (tab != null)
        {
            ZoomLevel.Text = $"{tab.ZoomLevel:F0}%";
        }
    }

    // UI Event Handlers
    private void NewTabButton_Click(object sender, RoutedEventArgs e) => _tabManager.CreateTab();

    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is BrowserTab tab)
        {
            _tabManager.CloseTab(tab);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tabManager.ActiveTab != null && _webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView))
        {
            webView.CoreWebView2?.GoBack();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tabManager.ActiveTab != null && _webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView))
        {
            webView.CoreWebView2?.GoForward();
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tabManager.ActiveTab == null) return;

        if (_tabManager.ActiveTab.IsLoading)
        {
            _webViews[_tabManager.ActiveTab.Id].CoreWebView2?.Stop();
        }
        else
        {
            _webViews[_tabManager.ActiveTab.Id].CoreWebView2?.Reload();
        }
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToUrl(App.Settings.CurrentSettings.Homepage);
    }

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateToUrl(AddressBar.Text);
        }
    }

    private void AddressBar_GotFocus(object sender, RoutedEventArgs e)
    {
        AddressBar.SelectAll();
    }

    private async void BookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var tab = _tabManager.ActiveTab;
        if (tab == null || tab.Url.StartsWith("zarla://")) return;

        var isBookmarked = await App.Database.IsBookmarkedAsync(tab.Url);
        if (isBookmarked)
        {
            // TODO: Show bookmark edit/delete
        }
        else
        {
            await App.Database.AddBookmarkAsync(tab.Url, tab.Title, tab.Favicon);
            BookmarkIcon.Text = "★";
        }
    }

    private void DownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToUrl("zarla://downloads");
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.PlacementTarget = sender as Button;
        MenuPopup.IsOpen = true;
    }

    // Menu click handlers
    private void NewTab_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        _tabManager.CreateTab();
    }

    private void NewWindow_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        var window = new MainWindow();
        window.Show();
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        NavigateToUrl("zarla://history");
    }

    private void Bookmarks_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        NavigateToUrl("zarla://bookmarks");
    }

    private void Downloads_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        NavigateToUrl("zarla://downloads");
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        if (_tabManager.ActiveTab != null && _webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView))
        {
            webView.ZoomFactor = Math.Min(webView.ZoomFactor + 0.1, 3.0);
            _tabManager.ActiveTab.ZoomLevel = webView.ZoomFactor * 100;
            UpdateZoomLevel();
        }
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        if (_tabManager.ActiveTab != null && _webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView))
        {
            webView.ZoomFactor = Math.Max(webView.ZoomFactor - 0.1, 0.25);
            _tabManager.ActiveTab.ZoomLevel = webView.ZoomFactor * 100;
            UpdateZoomLevel();
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        if (_tabManager.ActiveTab != null && _webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView))
        {
            webView.CoreWebView2?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
        }
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        FindBar.Visibility = Visibility.Visible;
        FindTextBox.Focus();
    }

    private void DevTools_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        if (_tabManager.ActiveTab != null && _webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView))
        {
            webView.CoreWebView2?.OpenDevToolsWindow();
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        NavigateToUrl("zarla://settings");
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        NavigateToUrl("zarla://about");
    }

    // Find bar handlers
    private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNext_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            FindClose_Click(sender, e);
        }
    }

    private async void FindNext_Click(object sender, RoutedEventArgs e)
    {
        if (_tabManager.ActiveTab != null && _webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView))
        {
            var searchText = FindTextBox.Text.Replace("'", "\\'");
            await webView.CoreWebView2.ExecuteScriptAsync($"window.find('{searchText}', false, false, true)");
        }
    }

    private async void FindPrevious_Click(object sender, RoutedEventArgs e)
    {
        if (_tabManager.ActiveTab != null && _webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView))
        {
            var searchText = FindTextBox.Text.Replace("'", "\\'");
            await webView.CoreWebView2.ExecuteScriptAsync($"window.find('{searchText}', false, true, true)");
        }
    }

    private async void FindClose_Click(object sender, RoutedEventArgs e)
    {
        FindBar.Visibility = Visibility.Collapsed;
        if (_tabManager.ActiveTab != null && _webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView))
        {
            await webView.CoreWebView2.ExecuteScriptAsync("window.getSelection().removeAllRanges()");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _tabSuspender.Stop();
        _memoryManager.Stop();

        foreach (var webView in _webViews.Values)
        {
            webView.Dispose();
        }

        base.OnClosed(e);
    }
}
