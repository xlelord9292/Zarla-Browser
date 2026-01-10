using Zarla.Core.AI;
using Zarla.Core.Security;

namespace Zarla.Core.Data.Models;

public class BrowserSettings
{
    // ============================================
    // GENERAL
    // ============================================
    public string Homepage { get; set; } = "zarla://newtab";
    public string SearchEngine { get; set; } = "https://www.google.com/search?q=";
    public string DownloadPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
    public bool AskBeforeDownload { get; set; } = false;
    public string DefaultBrowser { get; set; } = "Zarla"; // For protocol handling
    public string Language { get; set; } = "en-US";
    public bool CheckSpelling { get; set; } = true;
    public string SpellCheckLanguage { get; set; } = "en-US";

    // ============================================
    // AI SETTINGS
    // ============================================
    public bool AIEnabled { get; set; } = true;
    public string SelectedAIModel { get; set; } = "meta-llama/llama-4-scout-17b-16e-instruct";
    public List<CustomModel> CustomModels { get; set; } = new();
    public string? AIBypassCode { get; set; }
    public bool AIWebSearch { get; set; } = false;
    public int AIMaxTokens { get; set; } = 2048;
    public double AITemperature { get; set; } = 0.7;
    public bool AIRememberConversation { get; set; } = true;
    public int AIMaxConversationHistory { get; set; } = 20;

    // ============================================
    // SECURITY SETTINGS
    // ============================================
    public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.Medium;
    public bool EnableSecurityScanning { get; set; } = true;
    public bool ShowSecurityWarnings { get; set; } = true;
    public bool BlockDangerousSites { get; set; } = true;
    public bool BlockMixedContent { get; set; } = true;
    public bool WarnOnUnsafeDownloads { get; set; } = true;
    public bool ScanDownloadsWithVirusTotal { get; set; } = false;
    public bool BlockPopups { get; set; } = true;
    public bool AllowPopupsForTrustedSites { get; set; } = true;

    // ============================================
    // APPEARANCE
    // ============================================
    public string Theme { get; set; } = "Dark"; // Dark, Light, System
    public bool ShowBookmarksBar { get; set; } = true;
    public bool ShowHomeButton { get; set; } = true;
    public double ZoomLevel { get; set; } = 100;
    public string FontFamily { get; set; } = "Segoe UI";
    public int DefaultFontSize { get; set; } = 16;
    public int MinimumFontSize { get; set; } = 10;
    public bool UseSystemTitleBar { get; set; } = false;
    public bool ShowStatusBar { get; set; } = true;
    public bool CompactMode { get; set; } = false;
    public string AccentColor { get; set; } = "#6c5ce7"; // Default purple
    public bool AnimationsEnabled { get; set; } = true;
    public bool SmoothScrolling { get; set; } = true;
    public double TabWidth { get; set; } = 200;
    public bool ShowTabPreviews { get; set; } = true;
    public bool ShowFaviconsInTabs { get; set; } = true;

    // ============================================
    // TABS
    // ============================================
    public bool OpenLinksInNewTab { get; set; } = false;
    public bool SwitchToNewTab { get; set; } = true;
    public bool CloseWindowWithLastTab { get; set; } = true;
    public bool ShowTabCloseButton { get; set; } = true;
    public bool ConfirmBeforeClosingMultipleTabs { get; set; } = true;
    public bool DuplicateTabOnMiddleClick { get; set; } = false;
    public NewTabPosition NewTabPosition { get; set; } = NewTabPosition.End;
    public bool GroupTabsByDomain { get; set; } = false;
    public bool ShowTabAudioIndicator { get; set; } = true;
    public bool MuteTabsOnBackground { get; set; } = false;

    // ============================================
    // PASSWORDS
    // ============================================
    public bool EnablePasswordAutofill { get; set; } = true;
    public bool OfferToSavePasswords { get; set; } = true;
    public bool AutoSignIn { get; set; } = false;
    public bool RequireMasterPassword { get; set; } = false;
    public bool ShowPasswordStrengthIndicator { get; set; } = true;
    public bool GenerateStrongPasswords { get; set; } = true;
    public int GeneratedPasswordLength { get; set; } = 16;
    public bool CheckForBreachedPasswords { get; set; } = false;
    public bool BiometricUnlock { get; set; } = false;

    // ============================================
    // PRIVACY
    // ============================================
    public bool EnableAdBlocker { get; set; } = true;
    public bool EnableTrackerBlocker { get; set; } = true;
    public bool EnableFingerprintProtection { get; set; } = true;
    public bool SendDoNotTrack { get; set; } = true;
    public bool ClearDataOnExit { get; set; } = false;
    public bool BlockThirdPartyCookies { get; set; } = true;
    public bool BlockCookieBanners { get; set; } = false;
    public bool EnablePrivateBrowsing { get; set; } = false;
    public bool BlockWebRTC { get; set; } = false;
    public bool BlockCanvasFingerprinting { get; set; } = true;
    public bool BlockAudioFingerprinting { get; set; } = false;
    public bool SpoofTimezone { get; set; } = false;
    public bool SpoofLanguage { get; set; } = false;
    public bool RemoveTrackingParameters { get; set; } = true;
    public bool BlockSocialTrackers { get; set; } = true;
    public bool EnableHTTPSOnly { get; set; } = false;
    public CookiePolicy CookiePolicy { get; set; } = CookiePolicy.AllowAll;

    // Data to clear on exit
    public bool ClearHistoryOnExit { get; set; } = false;
    public bool ClearCookiesOnExit { get; set; } = false;
    public bool ClearCacheOnExit { get; set; } = false;
    public bool ClearDownloadsOnExit { get; set; } = false;
    public bool ClearFormDataOnExit { get; set; } = false;
    public bool ClearPasswordsOnExit { get; set; } = false;

    // ============================================
    // NETWORK - DNS
    // ============================================
    public string DnsProvider { get; set; } = "System"; // System, Cloudflare, Quad9, Google, Custom
    public string CustomDnsServer { get; set; } = "";
    public bool EnableDnsOverHttps { get; set; } = false;
    public string DohProvider { get; set; } = "Cloudflare"; // Cloudflare, Google, Quad9

    // ============================================
    // NETWORK - PROXY
    // ============================================
    public string ProxyMode { get; set; } = "None"; // None, System, Custom, PAC
    public string ProxyAddress { get; set; } = "";
    public int ProxyPort { get; set; } = 8080;
    public bool ProxyRequiresAuth { get; set; } = false;
    public string ProxyUsername { get; set; } = "";
    public string ProxyPassword { get; set; } = "";
    public string ProxyBypassList { get; set; } = "localhost;127.0.0.1;*.local";
    public string PacScriptUrl { get; set; } = "";

    // ============================================
    // PERFORMANCE
    // ============================================
    public bool EnableTabSuspension { get; set; } = true;
    public int TabSuspensionTimeout { get; set; } = 5; // minutes
    public bool EnableHardwareAcceleration { get; set; } = true;
    public bool PreloadNextPage { get; set; } = false;
    public int MaxMemoryPerTab { get; set; } = 512; // MB
    public bool LazyLoadImages { get; set; } = false;
    public bool BlockAutoplayMedia { get; set; } = false;
    public bool ReduceMotion { get; set; } = false;
    public bool LiteMode { get; set; } = false;
    public bool EnableBackForwardCache { get; set; } = true;
    public bool PrefetchDNS { get; set; } = true;
    public bool PreconnectToSites { get; set; } = true;
    public int MaxConcurrentDownloads { get; set; } = 3;
    public int ConnectionTimeout { get; set; } = 30; // seconds

    // ============================================
    // DEVELOPER
    // ============================================
    public bool EnableDevTools { get; set; } = true;
    public bool ShowNetworkRequests { get; set; } = false;
    public bool EnableExtensions { get; set; } = true;
    public bool AllowExtensionsInIncognito { get; set; } = false;
    public bool EnableExperimentalFeatures { get; set; } = false;
    public bool EnableRemoteDebugging { get; set; } = false;
    public int RemoteDebuggingPort { get; set; } = 9222;
    public bool LogConsoleToFile { get; set; } = false;
    public bool ShowElementInspector { get; set; } = true;

    // ============================================
    // STARTUP
    // ============================================
    public StartupBehavior StartupBehavior { get; set; } = StartupBehavior.NewTab;
    public List<string> StartupPages { get; set; } = new();
    public bool RestoreLastSession { get; set; } = false;
    public bool LaunchOnSystemStartup { get; set; } = false;
    public bool LaunchMinimized { get; set; } = false;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public bool ShowWelcomePageOnFirstRun { get; set; } = true;

    // ============================================
    // DOWNLOADS
    // ============================================
    public bool AutoOpenDownloadedFiles { get; set; } = false;
    public bool ShowDownloadNotifications { get; set; } = true;
    public bool ResumeInterruptedDownloads { get; set; } = true;
    public List<string> AlwaysOpenFileTypes { get; set; } = new();
    public List<string> NeverOpenFileTypes { get; set; } = new() { ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs" };

    // ============================================
    // ACCESSIBILITY
    // ============================================
    public bool HighContrastMode { get; set; } = false;
    public bool LargeText { get; set; } = false;
    public bool ScreenReaderSupport { get; set; } = true;
    public bool KeyboardNavigation { get; set; } = true;
    public bool FocusHighlight { get; set; } = true;
    public bool ReduceTransparency { get; set; } = false;
    public bool CaretBrowsing { get; set; } = false;

    // ============================================
    // MEDIA
    // ============================================
    public bool AutoplayVideos { get; set; } = true;
    public bool MuteAutoplayVideos { get; set; } = true;
    public bool EnablePictureInPicture { get; set; } = true;
    public bool EnableMediaKeys { get; set; } = true;
    public double DefaultVolume { get; set; } = 100;
    public bool RememberVolumePerSite { get; set; } = true;

    // ============================================
    // NOTIFICATIONS
    // ============================================
    public bool AllowNotifications { get; set; } = true;
    public bool NotificationSound { get; set; } = true;
    public NotificationPosition NotificationPosition { get; set; } = NotificationPosition.BottomRight;
    public List<string> BlockedNotificationSites { get; set; } = new();

    // ============================================
    // SYNC (Future)
    // ============================================
    public bool EnableSync { get; set; } = false;
    public bool SyncBookmarks { get; set; } = true;
    public bool SyncHistory { get; set; } = true;
    public bool SyncPasswords { get; set; } = false;
    public bool SyncSettings { get; set; } = true;
    public bool SyncExtensions { get; set; } = true;

    // ============================================
    // SEARCH
    // ============================================
    public bool SearchSuggestionsEnabled { get; set; } = true;
    public bool SearchFromAddressBar { get; set; } = true;
    public bool ShowSearchEngineInAddressBar { get; set; } = false;
    public bool HighlightSearchTerms { get; set; } = true;
    public List<SearchEngineEntry> CustomSearchEngines { get; set; } = new();

    // ============================================
    // READING
    // ============================================
    public bool EnableReadingMode { get; set; } = true;
    public string ReadingModeFont { get; set; } = "Georgia";
    public int ReadingModeFontSize { get; set; } = 18;
    public string ReadingModeBackground { get; set; } = "Auto"; // Auto, Light, Dark, Sepia
    public int ReadingModeLineSpacing { get; set; } = 150; // percent
    public int ReadingModeMaxWidth { get; set; } = 700; // pixels
}

public enum StartupBehavior
{
    NewTab,
    Homepage,
    LastSession,
    SpecificPages
}

public enum NewTabPosition
{
    End,
    AfterCurrent,
    Start
}

public enum CookiePolicy
{
    AllowAll,
    BlockThirdParty,
    BlockAll
}

public enum NotificationPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public class SearchEngineEntry
{
    public string Name { get; set; } = "";
    public string Keyword { get; set; } = "";
    public string Url { get; set; } = "";
    public bool IsDefault { get; set; } = false;
}
