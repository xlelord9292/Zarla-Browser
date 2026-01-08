using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Zarla.Browser.Services;
using Zarla.Browser.Views;
using Zarla.Core.Data.Models;
using Zarla.Core.Privacy;
using Zarla.Core.Performance;
using Zarla.Core.AI;
using Zarla.Core.Security;
using Zarla.Core.Extensions;

namespace Zarla.Browser;

public partial class MainWindow : Window
{
    private readonly TabManager _tabManager = new();
    private readonly AdBlocker _adBlocker = new();
    private readonly TrackerBlocker _trackerBlocker = new();
    private readonly FingerprintProtection _fingerprintProtection = new();
    private readonly TabSuspender _tabSuspender = new();
    private readonly MemoryManager _memoryManager = new();
    private readonly SecurityScanner _securityScanner = new();
    private readonly ExtensionManager _extensionManager = new();
    private readonly ZarlaExtensionManager _zarlaExtensionManager;
    private readonly PasswordManager _passwordManager;
    private readonly PasswordAutofill _passwordAutofill;
    private readonly Dictionary<string, WebView2> _webViews = new();
    private readonly Dictionary<string, BitmapImage> _faviconCache = new();
    private DownloadService? _downloadService;

    // AI Services
    private AIService? _aiService;
    private AIUsageTracker? _aiUsageTracker;
    private static readonly string GroqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";

    // Tab drag-drop support
    private Border? _draggedTab;
    private Point _dragStartPoint;

    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Zarla/1.0 Chrome/120.0.0.0 Safari/537.36";

    public MainWindow()
    {
        // Initialize password manager and autofill
        _passwordManager = new PasswordManager(App.UserDataFolder);
        _passwordAutofill = new PasswordAutofill(_passwordManager);

        // Initialize Zarla Extensions manager
        _zarlaExtensionManager = new ZarlaExtensionManager(App.UserDataFolder);

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
        // Initialize security scanner first
        var settings = App.Settings.CurrentSettings;
        _securityScanner.Level = settings.SecurityLevel;
        
        // Initialize download service with security scanner
        _downloadService = new DownloadService(App.Database, App.Settings.CurrentSettings.DownloadPath, _securityScanner);
        _downloadService.ThreatDetected += OnDownloadThreatDetected;

        // Initialize AI services
        _aiService = new AIService(GroqApiKey);
        _aiUsageTracker = new AIUsageTracker(App.UserDataFolder);

        // Set bypass code if saved
        if (!string.IsNullOrEmpty(settings.AIBypassCode))
        {
            _aiUsageTracker.SetBypassCode(settings.AIBypassCode);
        }

        // Subscribe to settings changes for live updates
        App.Settings.SettingsChanged += OnSettingsChanged;

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

    /// <summary>
    /// Handles settings changes and applies them immediately without requiring restart
    /// </summary>
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var settings = App.Settings.CurrentSettings;
            
            // Apply privacy settings to all existing components
            _adBlocker.IsEnabled = settings.EnableAdBlocker;
            _trackerBlocker.IsEnabled = settings.EnableTrackerBlocker;
            _fingerprintProtection.IsEnabled = settings.EnableFingerprintProtection;
            
            // Apply tab suspension settings
            _tabSuspender.IsEnabled = settings.EnableTabSuspension;
            _tabSuspender.SuspensionTimeoutMinutes = settings.TabSuspensionTimeout;
            
            // Apply security level
            _securityScanner.Level = settings.SecurityLevel;
            
            // Apply bookmarks bar visibility
            BookmarksBar.Visibility = settings.ShowBookmarksBar ? Visibility.Visible : Visibility.Collapsed;
            
            // Apply home button visibility
            HomeButton.Visibility = settings.ShowHomeButton ? Visibility.Visible : Visibility.Collapsed;
            
            // Apply theme
            ApplyTheme(settings.Theme);
            
            // Update WebView settings for all tabs
            foreach (var webView in _webViews.Values)
            {
                if (webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Settings.AreDevToolsEnabled = settings.EnableDevTools;
                    
                    // Re-inject scripts for ad blocking if enabled
                    if (settings.EnableAdBlocker)
                    {
                        webView.CoreWebView2.ExecuteScriptAsync(_adBlocker.GetAdBlockScript());
                        webView.CoreWebView2.ExecuteScriptAsync(_adBlocker.GetCosmeticBlockScript());
                    }
                    
                    // Re-inject fingerprint protection
                    if (settings.EnableFingerprintProtection)
                    {
                        webView.CoreWebView2.ExecuteScriptAsync(_fingerprintProtection.GetProtectionScript());
                    }
                }
            }
            
            // Update status text
            StatusText.Text = "Settings applied";
        });
    }

    private void ApplyTheme(string themeName)
    {
        var themeUri = themeName.ToLower() switch
        {
            "dark" => new Uri("Themes/Dark.xaml", UriKind.Relative),
            "light" => new Uri("Themes/Light.xaml", UriKind.Relative),
            _ => new Uri("Themes/Dark.xaml", UriKind.Relative) // Default to dark
        };
        
        // Find and replace theme dictionary
        var mergedDicts = Application.Current.Resources.MergedDictionaries;
        ResourceDictionary? themeDict = null;
        
        foreach (var dict in mergedDicts)
        {
            if (dict.Source?.ToString().Contains("Themes/") == true)
            {
                themeDict = dict;
                break;
            }
        }
        
        if (themeDict != null)
        {
            mergedDicts.Remove(themeDict);
        }
        
        mergedDicts.Add(new ResourceDictionary { Source = themeUri });
    }

    private void SetupEventHandlers()
    {
        _tabManager.TabAdded += OnTabAdded;
        _tabManager.TabRemoved += OnTabRemoved;
        _tabManager.ActiveTabChanged += OnActiveTabChanged;

        _tabSuspender.TabSuspended += OnTabSuspended;
        _tabSuspender.TabWoken += OnTabWoken;

        // Password save popup events
        SavePasswordPopup.SaveRequested += OnSavePasswordRequested;
        SavePasswordPopup.NeverRequested += OnNeverSavePasswordRequested;
        SavePasswordPopup.CloseRequested += OnClosePasswordPopupRequested;
    }

    private void OnSavePasswordRequested(object? sender, Views.SavePasswordEventArgs e)
    {
        if (string.IsNullOrEmpty(_pendingPasswordUsername) || string.IsNullOrEmpty(_pendingPasswordPassword))
        {
            HideSavePasswordPopup();
            return;
        }

        // If password manager is locked, prompt to unlock first
        if (!_passwordManager.IsUnlocked)
        {
            // Open password manager to unlock
            NavigateToUrl("zarla://passwords");
            MessageBox.Show(
                "Please unlock your password manager first, then try logging in again.",
                "Password Manager Locked",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            HideSavePasswordPopup();
            return;
        }

        // Save the password
        _passwordManager.AddPassword(
            _pendingPasswordSite ?? "",
            _pendingPasswordUsername,
            _pendingPasswordPassword);

        StatusText.Text = $"Password saved for {_pendingPasswordSite}";
        HideSavePasswordPopup();
    }

    private void OnNeverSavePasswordRequested(object? sender, EventArgs e)
    {
        // TODO: Add site to "never save" list
        HideSavePasswordPopup();
    }

    private void OnClosePasswordPopupRequested(object? sender, EventArgs e)
    {
        HideSavePasswordPopup();
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

            // Initialize WebView2 with proxy settings and extension support
            var envOptions = new CoreWebView2EnvironmentOptions
            {
                AreBrowserExtensionsEnabled = true
            };
            
            var settings = App.Settings.CurrentSettings;
            var additionalArgs = "--enable-features=ExtensionsToolbarMenu";
            
            if (settings.ProxyMode == "Custom" && !string.IsNullOrWhiteSpace(settings.ProxyAddress))
            {
                // Build proxy server argument
                var proxyPort = settings.ProxyPort > 0 ? settings.ProxyPort : 8080;
                additionalArgs += $" --proxy-server={settings.ProxyAddress}:{proxyPort}";
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
                    additionalArgs += $" --enable-features=DnsOverHttps --doh-url={dohUrl}";
                }
            }
            
            envOptions.AdditionalBrowserArguments = additionalArgs;

            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: App.UserDataFolder,
                options: envOptions);

            await webView.EnsureCoreWebView2Async(env);

            // Configure WebView2 immediately
            ConfigureWebView(webView, tab);

            // Navigate to initial URL right away
            NavigateTab(tab, tab.Url);

            // Update tab strip
            UpdateTabStrip();

            // Register with tab suspender
            _tabSuspender.RegisterTab(tab.Id);

            // Initialize extensions in background (don't block tab creation)
            _ = Task.Run(async () =>
            {
                try
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await _extensionManager.InitializeAsync(webView.CoreWebView2);
                    });
                }
                catch { }
            });
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

        // Set custom user agent - use standard Chrome UA for compatibility
        settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

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

        // Add custom context menu for AI features
        webView.CoreWebView2.ContextMenuRequested += (s, e) => OnContextMenuRequested(tab, e);

        // Handle web messages (e.g., proceed anyway from security warning)
        webView.CoreWebView2.WebMessageReceived += (s, e) => OnWebMessageReceived(tab, e);

        // Inject fingerprint protection script (but not for YouTube to prevent sidebar issues)
        if (_fingerprintProtection.IsEnabled)
        {
            webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(_fingerprintProtection.GetProtectionScript());
        }

        // Inject ad blocking scripts
        if (_adBlocker.IsEnabled)
        {
            webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(_adBlocker.GetCosmeticBlockScript());
            webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(_adBlocker.GetAdBlockScript());
        }
    }

    private async void OnNavigationStarting(BrowserTab tab, CoreWebView2NavigationStartingEventArgs e)
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

        // Skip security check for about: URLs
        if (e.Uri.StartsWith("about:"))
        {
            return;
        }

        // Block ads/trackers
        if (_adBlocker.ShouldBlock(e.Uri) || _trackerBlocker.ShouldBlock(e.Uri))
        {
            e.Cancel = true;
            return;
        }

        // Security scanning
        var settings = App.Settings.CurrentSettings;
        if (settings.EnableSecurityScanning)
        {
            try
            {
                var scanResult = await _securityScanner.ScanUrlAsync(e.Uri);
                if (!scanResult.IsSafe && settings.BlockDangerousSites)
                {
                    e.Cancel = true;
                    Dispatcher.Invoke(() =>
                    {
                        ShowSecurityWarning(tab, e.Uri, scanResult);
                    });
                    return;
                }
            }
            catch
            {
                // If scanning fails, allow navigation
            }
        }

        // Record activity for tab suspension
        _tabSuspender.RecordActivity(tab.Id);
    }

    private void ShowSecurityWarning(BrowserTab tab, string url, SecurityScanResult result)
    {
        // Store blocked URL for "proceed anyway" option
        tab.Tag = url;
        
        // Navigate to security warning page
        if (_webViews.TryGetValue(tab.Id, out var webView))
        {
            var threatIcon = result.ThreatType switch
            {
                SecurityThreatType.MirrorSite => "🪞",
                SecurityThreatType.Phishing => "🎣",
                SecurityThreatType.Malware => "🦠",
                SecurityThreatType.Scam => "⚠️",
                _ => "🛡️"
            };
            
            var threatName = result.ThreatType switch
            {
                SecurityThreatType.MirrorSite => "Mirror/Fake Website",
                SecurityThreatType.Phishing => "Phishing Attempt",
                SecurityThreatType.Malware => "Malware Distribution",
                SecurityThreatType.Scam => "Potential Scam",
                _ => "Security Threat"
            };

            var warningHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Security Warning - Zarla</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: 'Segoe UI', sans-serif;
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
            color: #fff;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        .container {{
            max-width: 600px;
            text-align: center;
        }}
        .icon {{
            font-size: 80px;
            margin-bottom: 24px;
        }}
        h1 {{
            font-size: 28px;
            color: #ff6b6b;
            margin-bottom: 12px;
        }}
        .subtitle {{
            font-size: 18px;
            color: #ffd93d;
            margin-bottom: 24px;
        }}
        .message {{
            background: rgba(255,255,255,0.1);
            border-radius: 12px;
            padding: 24px;
            margin-bottom: 24px;
            text-align: left;
        }}
        .message p {{
            margin-bottom: 12px;
            line-height: 1.6;
        }}
        .code {{
            font-family: 'Consolas', monospace;
            background: rgba(0,0,0,0.3);
            padding: 4px 8px;
            border-radius: 4px;
            color: #6c5ce7;
        }}
        .url {{
            word-break: break-all;
            color: #a8a8a8;
            font-size: 14px;
        }}
        .issues {{
            text-align: left;
            margin-top: 16px;
        }}
        .issues li {{
            margin: 8px 0;
            color: #ffaa00;
        }}
        .buttons {{
            display: flex;
            gap: 16px;
            justify-content: center;
            flex-wrap: wrap;
        }}
        button {{
            padding: 14px 28px;
            border: none;
            border-radius: 8px;
            font-size: 16px;
            cursor: pointer;
            transition: all 0.2s;
        }}
        .back-btn {{
            background: #6c5ce7;
            color: white;
        }}
        .back-btn:hover {{
            background: #5b4cdb;
        }}
        .proceed-btn {{
            background: transparent;
            color: #888;
            border: 1px solid #444;
        }}
        .proceed-btn:hover {{
            background: rgba(255,255,255,0.1);
        }}
        .risk {{
            margin-top: 24px;
            font-size: 14px;
            color: #666;
        }}
        .risk-bar {{
            height: 8px;
            background: #333;
            border-radius: 4px;
            margin-top: 8px;
            overflow: hidden;
        }}
        .risk-fill {{
            height: 100%;
            background: linear-gradient(90deg, #ffd93d, #ff6b6b);
            width: {result.RiskScore}%;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>{threatIcon}</div>
        <h1>⚠️ This Website Has Been Flagged by Zarla</h1>
        <p class='subtitle'>{threatName} Detected</p>
        
        <div class='message'>
            <p><strong>Warning:</strong> {result.WarningMessage ?? "This website may be dangerous."}</p>
            <p>URL: <span class='url'>{System.Web.HttpUtility.HtmlEncode(url)}</span></p>
            <p>Error Code: <span class='code'>{result.WarningCode}</span></p>
            
            {(result.Issues.Count > 0 ? $@"
            <ul class='issues'>
                {string.Join("", result.Issues.Select(i => $"<li>{System.Web.HttpUtility.HtmlEncode(i)}</li>"))}
            </ul>" : "")}
        </div>
        
        <div class='risk'>
            Risk Score: {result.RiskScore}/100
            <div class='risk-bar'><div class='risk-fill'></div></div>
        </div>
        
        <div class='buttons' style='margin-top: 24px;'>
            <button class='back-btn' onclick='history.back()'>← Go Back to Safety</button>
            <button class='proceed-btn' onclick=""window.chrome.webview.postMessage('proceed:{url}')"">Proceed Anyway (Unsafe)</button>
        </div>
        
        <p style='margin-top: 24px; font-size: 12px; color: #666;'>
            Learn more at <a href='zarla://docs' style='color: #6c5ce7;'>zarla://docs</a>
        </p>
    </div>
</body>
</html>";

            webView.CoreWebView2.NavigateToString(warningHtml);
            tab.Title = "Security Warning";
            tab.Url = $"zarla://blocked?url={Uri.EscapeDataString(url)}";
            UpdateTabStrip();
        }
    }

    private async void OnWebMessageReceived(BrowserTab tab, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = e.TryGetWebMessageAsString();
            if (message != null)
            {
                if (message.StartsWith("proceed:"))
                {
                    var url = message.Substring(8);
                    // User chose to proceed to blocked site - navigate directly
                    if (_webViews.TryGetValue(tab.Id, out var webView))
                    {
                        webView.CoreWebView2.Navigate(url);
                    }
                }
                else if (message.Contains("installExtension"))
                {
                    // Handle Chrome Web Store extension install request
                    await HandleExtensionInstallRequest(message);
                }
                else if (message.Contains("autofill_selected"))
                {
                    // Handle autofill selection
                    await HandleAutofillSelection(tab, message);
                }
                else if (message.Contains("credentials_captured"))
                {
                    // Handle captured credentials for save prompt
                    HandleCapturedCredentials(tab, message);
                }
            }
        }
        catch
        {
            // Ignore message parsing errors
        }
    }

    private async System.Threading.Tasks.Task HandleAutofillSelection(BrowserTab tab, string message)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(message);
            if (json.RootElement.TryGetProperty("index", out var indexElement))
            {
                var index = indexElement.GetInt32();
                var entries = _passwordAutofill.GetMatchingEntries(tab.Url);

                if (index >= 0 && index < entries.Count)
                {
                    var entry = entries[index];
                    if (_webViews.TryGetValue(tab.Id, out var webView))
                    {
                        var fillScript = _passwordAutofill.GetAutofillScript(entry.Username, entry.Password);
                        await webView.CoreWebView2.ExecuteScriptAsync(fillScript);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Autofill selection error: {ex.Message}");
        }
    }

    private string? _pendingPasswordUsername;
    private string? _pendingPasswordPassword;
    private string? _pendingPasswordSite;
    private System.Windows.Threading.DispatcherTimer? _savePasswordTimer;

    private void HandleCapturedCredentials(BrowserTab tab, string message)
    {
        if (!App.Settings.CurrentSettings.OfferToSavePasswords) return;

        try
        {
            var json = System.Text.Json.JsonDocument.Parse(message);
            var username = json.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
            var password = json.RootElement.TryGetProperty("password", out var p) ? p.GetString() : null;
            var site = json.RootElement.TryGetProperty("site", out var s) ? s.GetString() : null;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return;

            // Extract site from URL if not provided
            if (string.IsNullOrEmpty(site))
            {
                try
                {
                    var uri = new Uri(tab.Url);
                    site = uri.Host;
                    if (site.StartsWith("www.")) site = site.Substring(4);
                }
                catch { site = tab.Url; }
            }

            // Check if already saved (only if password manager is unlocked)
            if (_passwordManager.IsUnlocked)
            {
                var existing = _passwordAutofill.GetMatchingEntries(tab.Url);
                var match = existing.FirstOrDefault(e => e.Username == username);
                if (match != null && match.Password == password) return; // Same password already saved
            }

            // Store pending credentials and show popup
            _pendingPasswordUsername = username;
            _pendingPasswordPassword = password;
            _pendingPasswordSite = site;

            // Show the Chrome-like popup
            SavePasswordPopup.SetCredentials(site!, username, password);
            SavePasswordPopupContainer.Visibility = Visibility.Visible;

            // Auto-hide after 30 seconds
            _savePasswordTimer?.Stop();
            _savePasswordTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _savePasswordTimer.Tick += (s, e) =>
            {
                _savePasswordTimer.Stop();
                HideSavePasswordPopup();
            };
            _savePasswordTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Credential capture error: {ex.Message}");
        }
    }

    private void HideSavePasswordPopup()
    {
        SavePasswordPopupContainer.Visibility = Visibility.Collapsed;
        _pendingPasswordUsername = null;
        _pendingPasswordPassword = null;
        _pendingPasswordSite = null;
    }
    
    private async System.Threading.Tasks.Task HandleExtensionInstallRequest(string message)
    {
        try
        {
            // Parse JSON message
            var json = System.Text.Json.JsonDocument.Parse(message);
            if (json.RootElement.TryGetProperty("extensionId", out var extIdElement))
            {
                var extensionId = extIdElement.GetString();
                if (!string.IsNullOrEmpty(extensionId))
                {
                    var webView = GetActiveWebView();
                    if (webView?.CoreWebView2 != null)
                    {
                        StatusText.Text = $"Installing extension {extensionId}...";

                        // Check if it's from Edge Add-ons
                        var isEdge = json.RootElement.TryGetProperty("source", out var sourceElement)
                            && sourceElement.GetString() == "edge";

                        Extension? extension;
                        if (isEdge)
                        {
                            extension = await _extensionManager.InstallFromEdgeStoreAsync(extensionId, webView.CoreWebView2);
                        }
                        else
                        {
                            extension = await _extensionManager.InstallFromWebStoreAsync(extensionId, webView.CoreWebView2);
                        }

                        if (extension != null)
                        {
                            StatusText.Text = $"Installed: {extension.Name}";

                            // Show notification
                            MessageBox.Show(
                                $"Successfully installed {extension.Name}!\n\nThe extension is now active in Zarla.",
                                "Extension Installed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            StatusText.Text = "Extension installation failed";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Install failed: {ex.Message}";
        }
    }

    private async void OnNavigationCompleted(BrowserTab tab, CoreWebView2NavigationCompletedEventArgs e)
    {
        tab.IsLoading = false;
        RefreshIcon.Text = "↻";

        if (_webViews.TryGetValue(tab.Id, out var webView))
        {
            tab.CanGoBack = webView.CoreWebView2.CanGoBack;
            tab.CanGoForward = webView.CoreWebView2.CanGoForward;

            UpdateNavigationButtons();
            
            // Inject extension store integration scripts
            if (tab.Url.Contains("chromewebstore.google.com"))
            {
                await InjectWebStoreScript(webView);
            }
            else if (tab.Url.Contains("microsoftedge.microsoft.com/addons"))
            {
                await InjectEdgeAddonsScript(webView);
            }
        }

        // Add to history
        if (e.IsSuccess && !tab.Url.StartsWith("zarla://"))
        {
            _ = App.Database.AddHistoryEntryAsync(tab.Url, tab.Title, tab.Favicon);
        }

        // Update privacy stats
        UpdatePrivacyStats();

        // Inject password autofill if enabled
        if (e.IsSuccess && App.Settings.CurrentSettings.EnablePasswordAutofill)
        {
            await InjectPasswordAutofill(tab);
        }

        // Inject Zarla Extensions scripts
        if (e.IsSuccess && !tab.Url.StartsWith("zarla://"))
        {
            await InjectZarlaExtensions(tab);
        }
    }

    private async Task InjectZarlaExtensions(BrowserTab tab)
    {
        if (!_webViews.TryGetValue(tab.Id, out var webView)) return;

        try
        {
            var script = _zarlaExtensionManager.GetScriptForUrl(tab.Url);
            if (!string.IsNullOrEmpty(script))
            {
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to inject Zarla extensions: {ex.Message}");
        }
    }

    private void OpenExtensionBuilder(ZarlaExtension? extension)
    {
        var tab = _tabManager.ActiveTab;
        if (tab == null) return;

        var builderPage = new ExtensionBuilderPage();
        builderPage.Initialize(_zarlaExtensionManager, extension, NavigateToUrl);
        ShowInternalPage(tab, builderPage);
        tab.Title = extension?.Name ?? "New Extension";
        tab.Url = "zarla://extension-builder";
    }

    private async System.Threading.Tasks.Task InjectPasswordAutofill(BrowserTab tab)
    {
        if (!_webViews.TryGetValue(tab.Id, out var webView)) return;
        if (tab.Url.StartsWith("zarla://")) return;

        try
        {
            // First, inject the credential capture script
            if (App.Settings.CurrentSettings.OfferToSavePasswords)
            {
                await webView.CoreWebView2.ExecuteScriptAsync(PasswordAutofill.GetCaptureCredentialsScript());
            }

            // Check if there are matching passwords for this site
            var matchingEntries = _passwordAutofill.GetMatchingEntries(tab.Url);
            if (matchingEntries.Count > 0 && _passwordManager.IsUnlocked)
            {
                // Inject autofill detection script
                var detectScript = PasswordAutofill.GetDetectLoginFormScript();
                var result = await webView.CoreWebView2.ExecuteScriptAsync(detectScript);

                // If login form detected, show autofill popup or auto-fill
                if (result != "null" && result.Contains("hasLoginForm"))
                {
                    if (matchingEntries.Count == 1 && App.Settings.CurrentSettings.AutoSignIn)
                    {
                        // Auto-fill the single matching entry
                        var entry = matchingEntries[0];
                        var fillScript = _passwordAutofill.GetAutofillScript(entry.Username, entry.Password);
                        await webView.CoreWebView2.ExecuteScriptAsync(fillScript);
                    }
                    else
                    {
                        // Show popup with matching entries
                        var popupScript = _passwordAutofill.GetShowAutofillPopupScript(matchingEntries);
                        await webView.CoreWebView2.ExecuteScriptAsync(popupScript);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Password autofill error: {ex.Message}");
        }
    }
    
    private async System.Threading.Tasks.Task InjectWebStoreScript(WebView2 webView)
    {
        // Script to intercept "Add to Chrome" button clicks
        var script = @"
(function() {
    'use strict';
    if (window.__zarlaWebStoreInjected) return;
    window.__zarlaWebStoreInjected = true;
    
    // Function to extract extension ID from URL
    function getExtensionId() {
        const match = window.location.pathname.match(/\/detail\/[^/]+\/([a-z]{32})/);
        return match ? match[1] : null;
    }
    
    // Override the install button behavior
    function hookInstallButtons() {
        // Find all potential install buttons
        const buttons = document.querySelectorAll('button');
        buttons.forEach(btn => {
            const text = btn.textContent.toLowerCase();
            if (text.includes('add to') || text.includes('install') || text.includes('get')) {
                if (btn.__zarlaHooked) return;
                btn.__zarlaHooked = true;
                
                btn.addEventListener('click', async function(e) {
                    const extId = getExtensionId();
                    if (extId) {
                        e.preventDefault();
                        e.stopPropagation();
                        
                        // Show installing state
                        const originalText = btn.textContent;
                        btn.textContent = 'Installing...';
                        btn.disabled = true;
                        
                        // Send message to Zarla to install
                        window.chrome.webview.postMessage({
                            type: 'installExtension',
                            extensionId: extId
                        });
                        
                        // Reset button after a delay
                        setTimeout(() => {
                            btn.textContent = 'Added to Zarla';
                            setTimeout(() => {
                                btn.textContent = originalText;
                                btn.disabled = false;
                            }, 2000);
                        }, 1500);
                    }
                }, true);
            }
        });
    }
    
    // Initial hook
    hookInstallButtons();
    
    // Observer for dynamically loaded content
    const observer = new MutationObserver(() => hookInstallButtons());
    observer.observe(document.body, { childList: true, subtree: true });
    
    console.log('[Zarla] Chrome Web Store integration active');
})();
";
        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch { }
    }

    private async System.Threading.Tasks.Task InjectEdgeAddonsScript(WebView2 webView)
    {
        // Script to fix Edge Add-ons compatibility and enable installation
        var script = @"
(function() {
    'use strict';
    if (window.__zarlaEdgeAddonsInjected) return;
    window.__zarlaEdgeAddonsInjected = true;

    // Hide incompatibility warnings
    function hideIncompatibilityWarnings() {
        const selectors = [
            '[class*=""incompatible""]',
            '[class*=""Incompatible""]',
            '[data-testid=""incompatible-banner""]',
            'div:has(> span:contains(""Incompatible""))'
        ];

        document.querySelectorAll('*').forEach(el => {
            const text = el.textContent || '';
            if (text.includes('Incompatible with your browser') ||
                text.includes('not compatible with your browser') ||
                text.includes('cannot be installed')) {
                // Hide the warning element
                if (el.closest('div[class*=""warning""], div[class*=""banner""], div[class*=""alert""]')) {
                    el.closest('div[class*=""warning""], div[class*=""banner""], div[class*=""alert""]').style.display = 'none';
                } else if (el.parentElement && el.parentElement.children.length <= 3) {
                    el.parentElement.style.display = 'none';
                }
            }
        });

        // Enable disabled Get buttons
        document.querySelectorAll('button[disabled], button.disabled').forEach(btn => {
            const text = (btn.textContent || '').toLowerCase();
            if (text.includes('get') || text === '') {
                btn.disabled = false;
                btn.classList.remove('disabled');
                btn.style.opacity = '1';
                btn.style.pointerEvents = 'auto';
            }
        });
    }

    // Function to extract extension ID from URL
    function getExtensionId() {
        // Edge Add-ons URL format: /addons/detail/{extension-name}/{extension-id}
        const match = window.location.pathname.match(/\/addons\/detail\/[^/]+\/([a-zA-Z0-9]+)/);
        return match ? match[1] : null;
    }

    // Create custom Get button if needed
    function ensureGetButton() {
        const extId = getExtensionId();
        if (!extId) return;

        // Find existing Get button area
        const existingBtn = document.querySelector('button:has-text(""Get""), [aria-label*=""Get""], [aria-label*=""Install""]');
        if (existingBtn && !existingBtn.disabled) return;

        // Look for the button container
        const containers = document.querySelectorAll('[class*=""action""], [class*=""install""], [class*=""button-container""]');
        containers.forEach(container => {
            if (container.querySelector('.__zarlaCustomBtn')) return;

            // Check if there's a disabled button here
            const disabledBtn = container.querySelector('button[disabled], button.disabled');
            if (disabledBtn) {
                // Replace with working button
                const btn = document.createElement('button');
                btn.className = '__zarlaCustomBtn';
                btn.textContent = 'Get';
                btn.style.cssText = 'background: #0078d4; color: white; border: none; padding: 8px 24px; border-radius: 4px; cursor: pointer; font-size: 14px; font-weight: 600;';
                btn.onclick = function(e) {
                    e.preventDefault();
                    e.stopPropagation();
                    btn.textContent = 'Installing...';
                    btn.disabled = true;
                    window.chrome.webview.postMessage({
                        type: 'installExtension',
                        extensionId: extId,
                        source: 'edge'
                    });
                    setTimeout(() => {
                        btn.textContent = 'Added to Zarla';
                    }, 1500);
                };
                disabledBtn.parentNode.insertBefore(btn, disabledBtn);
                disabledBtn.style.display = 'none';
            }
        });
    }

    // Override the install button behavior
    function hookInstallButtons() {
        // Find all potential install buttons
        const buttons = document.querySelectorAll('button, [role=""button""]');
        buttons.forEach(btn => {
            const text = (btn.textContent || btn.innerText || '').toLowerCase().trim();
            if ((text === 'get' || text.includes('install') || text.includes('add to')) && !btn.disabled) {
                if (btn.__zarlaHooked) return;
                btn.__zarlaHooked = true;

                btn.addEventListener('click', async function(e) {
                    const extId = getExtensionId();
                    if (extId) {
                        e.preventDefault();
                        e.stopPropagation();

                        // Show installing state
                        const originalText = btn.textContent || btn.innerText;
                        btn.textContent = 'Installing...';
                        btn.disabled = true;

                        // Send message to Zarla to install
                        window.chrome.webview.postMessage({
                            type: 'installExtension',
                            extensionId: extId,
                            source: 'edge'
                        });

                        // Reset button after a delay
                        setTimeout(() => {
                            btn.textContent = 'Added to Zarla';
                            setTimeout(() => {
                                btn.textContent = originalText;
                                btn.disabled = false;
                            }, 2000);
                        }, 1500);
                    }
                }, true);
            }
        });
    }

    // Initial setup
    hideIncompatibilityWarnings();
    hookInstallButtons();
    ensureGetButton();

    // Observer for dynamically loaded content
    const observer = new MutationObserver(() => {
        hideIncompatibilityWarnings();
        hookInstallButtons();
        ensureGetButton();
    });
    observer.observe(document.body, { childList: true, subtree: true });

    // Run periodically to catch lazy-loaded content
    setInterval(() => {
        hideIncompatibilityWarnings();
        ensureGetButton();
    }, 1000);

    console.log('[Zarla] Edge Add-ons integration active');
})();
";
        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch { }
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
        // Fast path - skip all checks if blockers disabled
        var settings = App.Settings.CurrentSettings;
        if (!settings.EnableAdBlocker && !settings.EnableTrackerBlocker && !settings.SendDoNotTrack)
            return;

        var url = e.Request.Uri;

        // Quick domain extraction for fast lookup
        if (settings.EnableAdBlocker && _adBlocker.ShouldBlock(url))
            return; // Just don't respond - faster than setting null

        if (settings.EnableTrackerBlocker && _trackerBlocker.ShouldBlock(url))
            return;

        // Add privacy headers only if enabled
        if (settings.SendDoNotTrack)
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
            case "extensions":
                var extensionsPage = new ExtensionsPage();
                extensionsPage.SetZarlaExtensionManager(_zarlaExtensionManager, NavigateToUrl, OpenExtensionBuilder);
                ShowInternalPage(tab, extensionsPage);
                break;
            case "extension-builder":
                var builderPage = new ExtensionBuilderPage();
                builderPage.Initialize(_zarlaExtensionManager, null, NavigateToUrl);
                ShowInternalPage(tab, builderPage);
                break;
            case "passwords":
                var passwordsPage = new PasswordsPage();
                passwordsPage.SetPasswordManager(_passwordManager);
                ShowInternalPage(tab, passwordsPage);
                break;
            case "about":
                ShowInternalPage(tab, new AboutPage());
                break;
            case "docs":
                ShowInternalPage(tab, new DocsPage());
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
        "extensions" => "Extensions",
        "passwords" => "Passwords",
        "about" => "About Zarla",
        "docs" => "Zarla Documentation",
        _ => "Zarla"
    };

    private WebView2? GetActiveWebView()
    {
        var tab = _tabManager.ActiveTab;
        if (tab == null) return null;
        return _webViews.TryGetValue(tab.Id, out var webView) ? webView : null;
    }

    /// <summary>
    /// Gets any initialized WebView2 CoreWebView2 for extension operations.
    /// Falls back to any available WebView if the active one isn't initialized.
    /// </summary>
    private Microsoft.Web.WebView2.Core.CoreWebView2? GetAnyInitializedWebView()
    {
        // First try the active tab's webview
        var activeWebView = GetActiveWebView();
        if (activeWebView?.CoreWebView2 != null)
            return activeWebView.CoreWebView2;

        // Fall back to any initialized webview
        foreach (var webView in _webViews.Values)
        {
            if (webView.CoreWebView2 != null)
                return webView.CoreWebView2;
        }

        return null;
    }

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

    private void Extensions_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        NavigateToUrl("zarla://extensions");
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

        _aiService?.Dispose();

        base.OnClosed(e);
    }

    // AI Functionality
    private string _selectedAIModel = "";
    private bool _aiPanelInitialized = false;

    private void AIButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleAIPanel();
    }

    private void AIAssistant_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        ToggleAIPanel();
    }

    private void ToggleAIPanel()
    {
        if (!App.Settings.CurrentSettings.AIEnabled)
        {
            MessageBox.Show("AI features are disabled. Enable them in Settings > AI.", "AI Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        bool willBeVisible = AIPanel.Visibility != Visibility.Visible;

        AIPanel.Visibility = willBeVisible
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Use column width to properly integrate AI panel side-by-side with browser content
        // This avoids WebView2 HWND overlay issues
        if (AIPanel.Parent is Grid parentGrid && parentGrid.ColumnDefinitions.Count > 1)
        {
            parentGrid.ColumnDefinitions[1].Width = willBeVisible
                ? new GridLength(420)  // AI panel width
                : new GridLength(0);
        }

        if (willBeVisible)
        {
            InitializeAIPanel();
        }
    }

    private void InitializeAIPanel()
    {
        if (!_aiPanelInitialized)
        {
            LoadAIPanelModels();
            _aiPanelInitialized = true;
        }
        UpdateAIUsageInfo();
    }

    private void LoadAIPanelModels()
    {
        AIPanelModelCombo.Items.Clear();
        var settings = App.Settings.CurrentSettings;

        // Add built-in models
        foreach (var model in BuiltInModels.Models)
        {
            AIPanelModelCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{model.Name} ({model.DailyLimit}/day)",
                Tag = model.Id
            });
        }

        // Add custom models
        foreach (var customModel in settings.CustomModels)
        {
            AIPanelModelCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{customModel.Name} (Custom)",
                Tag = $"custom:{customModel.Id}"
            });
        }

        // Select current model
        _selectedAIModel = settings.SelectedAIModel;
        for (int i = 0; i < AIPanelModelCombo.Items.Count; i++)
        {
            if (AIPanelModelCombo.Items[i] is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString() ?? "";
                if (tag == settings.SelectedAIModel || tag == $"custom:{settings.SelectedAIModel}")
                {
                    AIPanelModelCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        if (AIPanelModelCombo.SelectedIndex < 0 && AIPanelModelCombo.Items.Count > 0)
            AIPanelModelCombo.SelectedIndex = 0;
    }

    private void AIPanelModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AIPanelModelCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _selectedAIModel = tag.StartsWith("custom:") ? tag.Substring(7) : tag;
            App.Settings.Update(s => s.SelectedAIModel = _selectedAIModel);
            UpdateAIUsageInfo();
        }
    }

    private void CloseAIPanel_Click(object sender, RoutedEventArgs e)
    {
        AIPanel.Visibility = Visibility.Collapsed;
        // Find the parent grid and set column width to 0
        if (AIPanel.Parent is Grid parentGrid && parentGrid.ColumnDefinitions.Count > 1)
        {
            parentGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }
    }
    
    private void OnDownloadThreatDetected(object? sender, (DownloadItem Item, SecurityScanResult Result) e)
    {
        Dispatcher.Invoke(() =>
        {
            var result = MessageBox.Show(
                $"⚠️ THREAT DETECTED!\n\n" +
                $"File: {e.Item.FileName}\n" +
                $"Threat: {e.Result.VirusTotalResult?.ThreatLabel ?? e.Result.WarningTitle ?? "Unknown malware"}\n" +
                $"Risk Score: {e.Result.RiskScore}%\n\n" +
                $"Detection Details:\n{string.Join("\n", e.Result.Issues.Take(5))}\n\n" +
                $"Do you want to DELETE this file?",
                "Zarla Security - Malware Detected",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(e.Item.FilePath))
                    {
                        File.Delete(e.Item.FilePath);
                        e.Item.Status = DownloadStatus.Failed;
                        e.Item.ScanResult = "🗑️ Deleted (malware)";
                        MessageBox.Show("The malicious file has been deleted.", "File Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not delete file: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        });
    }

    private void UpdateAIUsageInfo()
    {
        if (_aiUsageTracker == null) return;

        var settings = App.Settings.CurrentSettings;
        var model = BuiltInModels.GetModel(_selectedAIModel);
        var dailyLimit = model?.DailyLimit ?? 50;
        var usage = _aiUsageTracker.CanUseModel(_selectedAIModel, dailyLimit);

        if (_aiUsageTracker.IsBypassActive)
        {
            AIUsageText.Text = "✨ Unlimited usage";
        }
        else
        {
            AIUsageText.Text = $"{usage.RemainingUses}/{dailyLimit} remaining today";
        }
    }

    private async Task<string?> GetPageContent()
    {
        if (_tabManager.ActiveTab == null) return null;
        if (_tabManager.ActiveTab.Url.StartsWith("zarla://")) return null;
        if (!_webViews.TryGetValue(_tabManager.ActiveTab.Id, out var webView)) return null;

        try
        {
            var script = @"
                (function() {
                    var text = document.body.innerText || document.body.textContent;
                    return text.substring(0, 15000);
                })();
            ";
            var result = await webView.CoreWebView2.ExecuteScriptAsync(script);
            if (result.StartsWith("\"") && result.EndsWith("\""))
            {
                result = result.Substring(1, result.Length - 2);
            }
            result = System.Text.RegularExpressions.Regex.Unescape(result);
            return result;
        }
        catch
        {
            return null;
        }
    }

    private async void SummarizePage_Click(object sender, RoutedEventArgs e)
    {
        var content = await GetPageContent();
        if (string.IsNullOrEmpty(content))
        {
            AddAIMessage("Please navigate to a web page first to use the summarize feature.", false);
            return;
        }
        await ProcessAIRequest("Summarize this page content concisely:", content, true);
    }

    private async void KeyPoints_Click(object sender, RoutedEventArgs e)
    {
        var content = await GetPageContent();
        if (string.IsNullOrEmpty(content))
        {
            AddAIMessage("Please navigate to a web page first to extract key points.", false);
            return;
        }
        await ProcessAIRequest("Extract the key points from this page as a bullet-point list:", content, true);
    }

    private async void ExplainPage_Click(object sender, RoutedEventArgs e)
    {
        var content = await GetPageContent();
        if (string.IsNullOrEmpty(content))
        {
            AddAIMessage("Please navigate to a web page first to explain the content.", false);
            return;
        }
        await ProcessAIRequest("Explain what this page is about in simple terms:", content, true);
    }

    private void AIInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(AIInput.Text))
        {
            SendAIMessage_Click(sender, e);
        }
    }

    private async void SendAIMessage_Click(object sender, RoutedEventArgs e)
    {
        var query = AIInput.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        AIInput.Text = "";

        // Add user message to chat
        AddUserMessage(query);

        // Check if we have page content to include
        var pageContent = await GetPageContent();

        await ProcessAIRequest(query, pageContent, false);
    }

    private async Task ProcessAIRequest(string prompt, string? pageContent, bool isPageAction)
    {
        if (_aiService == null || _aiUsageTracker == null) return;

        var settings = App.Settings.CurrentSettings;
        if (!settings.AIEnabled)
        {
            AddAIMessage("AI features are disabled. Enable them in Settings > AI.", false);
            return;
        }

        // Get current model info
        var builtInModel = BuiltInModels.GetModel(_selectedAIModel);
        var customModel = settings.CustomModels.FirstOrDefault(m => m.Id == _selectedAIModel || m.ModelId == _selectedAIModel);
        var dailyLimit = builtInModel?.DailyLimit ?? customModel?.DailyLimit ?? 50;

        // Check usage limits
        var usage = _aiUsageTracker.CanUseModel(_selectedAIModel, dailyLimit);
        if (!usage.CanUse)
        {
            var resetText = usage.TimeUntilReset.HasValue
                ? $"Resets in {usage.TimeUntilReset.Value.Hours}h {usage.TimeUntilReset.Value.Minutes}m"
                : "Try again later";
            AddAIMessage($"Daily limit reached for this model. {resetText}", false);
            return;
        }

        // Hide welcome panel, show loading
        AIWelcomePanel.Visibility = Visibility.Collapsed;
        AILoadingPanel.Visibility = Visibility.Visible;
        ScrollToBottom();

        try
        {
            AIResponse response;

            // Use custom API if it's a custom model with its own API key/endpoint
            if (customModel != null && !string.IsNullOrEmpty(customModel.ApiKey))
            {
                // Create service with custom base URL if provided (for OpenAI, etc.)
                var customService = new AIService(customModel.ApiKey, customModel.BaseUrl);
                string fullPrompt = !string.IsNullOrEmpty(pageContent)
                    ? $"{prompt}\n\nPage content:\n{pageContent}"
                    : prompt;
                response = await customService.SendMessageAsync(fullPrompt, customModel.ModelId);
                customService.Dispose();
            }
            else
            {
                // Use auto-search feature for queries that might need current info
                response = await _aiService.SendMessageWithAutoSearchAsync(prompt, _selectedAIModel, pageContent);
            }

            AILoadingPanel.Visibility = Visibility.Collapsed;

            if (response.Success)
            {
                _aiUsageTracker.RecordUsage(_selectedAIModel);

                // Show web search indicator if web search was used
                var displayContent = response.Content;
                if (response.UsedWebSearch)
                {
                    displayContent = "🔍 *Searched the web for this answer*\n\n" + displayContent;
                }

                AddAIMessage(displayContent, true);
            }
            else
            {
                AddAIMessage($"Error: {response.Error ?? "Unknown error"}", false);
            }
        }
        catch (Exception ex)
        {
            AILoadingPanel.Visibility = Visibility.Collapsed;
            AddAIMessage($"Error: {ex.Message}", false);
        }
        finally
        {
            UpdateAIUsageInfo();
            ScrollToBottom();
        }
    }

    private void AddUserMessage(string message)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("AccentBrush"),
            CornerRadius = new CornerRadius(12, 12, 4, 12),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(40, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Right,
            MaxWidth = 300
        };

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = Brushes.White
        };

        border.Child = text;
        AIChatPanel.Children.Add(border);
        ScrollToBottom();
    }

    private void AddAIMessage(string message, bool isSuccess)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceBrush"),
            CornerRadius = new CornerRadius(12, 12, 12, 4),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 40, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 350
        };

        // Convert markdown to HTML and display in WebBrowser
        var isDark = App.Settings.CurrentSettings.Theme == "Dark";
        var html = ConvertMarkdownToHtml(message, isDark, isSuccess);

        var webBrowser = new WebBrowser
        {
            MinHeight = 30,
            MaxHeight = 400
        };

        // Load HTML after browser is loaded
        webBrowser.Loaded += (s, e) =>
        {
            try
            {
                webBrowser.NavigateToString(html);
            }
            catch { }
        };

        // Auto-resize based on content
        webBrowser.LoadCompleted += (s, e) =>
        {
            try
            {
                dynamic doc = webBrowser.Document;
                if (doc?.body != null)
                {
                    var height = Math.Min(400, Math.Max(30, (int)doc.body.scrollHeight + 10));
                    webBrowser.Height = height;
                }
            }
            catch { webBrowser.Height = 100; }

            // Ensure scroll to bottom after content loads
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                ScrollToBottom();
            }));
        };

        border.Child = webBrowser;
        AIChatPanel.Children.Add(border);

        // Immediate scroll attempt
        ScrollToBottom();

        // Delayed scroll to ensure layout is complete
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            ScrollToBottom();
        }));
    }

    private void ClearAIChat_Click(object sender, RoutedEventArgs e)
    {
        // Keep only welcome panel and loading panel
        var toRemove = AIChatPanel.Children.OfType<Border>()
            .Where(b => b != AIWelcomePanel && b != AILoadingPanel)
            .ToList();

        foreach (var item in toRemove)
        {
            AIChatPanel.Children.Remove(item);
        }

        // Show welcome panel again
        AIWelcomePanel.Visibility = Visibility.Visible;

        // Clear AI memory
        _aiService?.ClearHistory();
    }
    
    private string ConvertMarkdownToHtml(string markdown, bool isDark, bool isSuccess)
    {
        // Use basic markdown pipeline
        var htmlBody = Markdig.Markdown.ToHtml(markdown);
        
        var bgColor = isDark ? "#2d2d2d" : "#f5f5f5";
        var textColor = isDark ? "#e0e0e0" : "#333333";
        var codeBackground = isDark ? "#1e1e1e" : "#e8e8e8";
        var linkColor = isDark ? "#6ea8fe" : "#0066cc";
        var errorColor = "#ff6b6b";
        
        if (!isSuccess) textColor = errorColor;
        
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='UTF-8'>
<meta http-equiv='X-UA-Compatible' content='IE=edge'>
<meta http-equiv='Content-Type' content='text/html; charset=utf-8'>
<style>
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    body {{ 
        font-family: 'Segoe UI', sans-serif; 
        font-size: 13px; 
        line-height: 1.5;
        color: {textColor}; 
        background: {bgColor};
        padding: 4px;
        overflow-x: hidden;
    }}
    p {{ margin: 0 0 8px 0; }}
    p:last-child {{ margin-bottom: 0; }}
    code {{ 
        background: {codeBackground}; 
        padding: 2px 6px; 
        border-radius: 4px; 
        font-family: 'Cascadia Code', 'Consolas', monospace;
        font-size: 12px;
    }}
    pre {{ 
        background: {codeBackground}; 
        padding: 10px; 
        border-radius: 6px; 
        overflow-x: auto;
        margin: 8px 0;
    }}
    pre code {{ 
        background: transparent; 
        padding: 0; 
    }}
    a {{ color: {linkColor}; text-decoration: none; }}
    a:hover {{ text-decoration: underline; }}
    ul, ol {{ margin: 8px 0; padding-left: 20px; }}
    li {{ margin: 4px 0; }}
    strong {{ font-weight: 600; }}
    h1, h2, h3, h4 {{ margin: 8px 0 4px 0; font-weight: 600; }}
    h1 {{ font-size: 16px; }}
    h2 {{ font-size: 15px; }}
    h3 {{ font-size: 14px; }}
    blockquote {{ 
        border-left: 3px solid {linkColor}; 
        padding-left: 10px; 
        margin: 8px 0;
        opacity: 0.9;
    }}
    table {{ border-collapse: collapse; margin: 8px 0; width: 100%; }}
    th, td {{ border: 1px solid {(isDark ? "#444" : "#ddd")}; padding: 6px 8px; text-align: left; }}
    th {{ background: {codeBackground}; }}
</style>
</head>
<body>{htmlBody}</body>
</html>";
    }

    private void ScrollToBottom()
    {
        AIChatScrollViewer.ScrollToEnd();
    }

    private void OnContextMenuRequested(BrowserTab tab, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        // Context menu customization - AI features accessible via AI button/panel
    }
}
