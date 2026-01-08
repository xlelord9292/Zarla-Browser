using Zarla.Core.AI;
using Zarla.Core.Security;

namespace Zarla.Core.Data.Models;

public class BrowserSettings
{
    // General
    public string Homepage { get; set; } = "zarla://newtab";
    public string SearchEngine { get; set; } = "https://www.google.com/search?q=";
    public string DownloadPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
    public bool AskBeforeDownload { get; set; } = false;

    // AI Settings
    public bool AIEnabled { get; set; } = true;
    public string SelectedAIModel { get; set; } = "meta-llama/llama-4-scout-17b-16e-instruct";
    public List<CustomModel> CustomModels { get; set; } = new();
    public string? AIBypassCode { get; set; }

    // Security Settings
    public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.Medium;
    public bool EnableSecurityScanning { get; set; } = true;
    public bool ShowSecurityWarnings { get; set; } = true;
    public bool BlockDangerousSites { get; set; } = true;

    // Appearance
    public string Theme { get; set; } = "Dark"; // Dark, Light, System
    public bool ShowBookmarksBar { get; set; } = true;
    public bool ShowHomeButton { get; set; } = true;
    public double ZoomLevel { get; set; } = 100;

    // Passwords
    public bool EnablePasswordAutofill { get; set; } = true;
    public bool OfferToSavePasswords { get; set; } = true;
    public bool AutoSignIn { get; set; } = false;

    // Privacy
    public bool EnableAdBlocker { get; set; } = true;
    public bool EnableTrackerBlocker { get; set; } = true;
    public bool EnableFingerprintProtection { get; set; } = true;
    public bool SendDoNotTrack { get; set; } = true;
    public bool ClearDataOnExit { get; set; } = false;
    public bool BlockThirdPartyCookies { get; set; } = true;

    // Network - DNS
    public string DnsProvider { get; set; } = "System"; // System, Cloudflare, Quad9, Google

    // Network - Proxy
    public string ProxyMode { get; set; } = "None"; // None, System, Custom
    public string ProxyAddress { get; set; } = "";
    public int ProxyPort { get; set; } = 8080;
    public bool ProxyRequiresAuth { get; set; } = false;
    public string ProxyUsername { get; set; } = "";
    public string ProxyPassword { get; set; } = "";

    // Performance
    public bool EnableTabSuspension { get; set; } = true;
    public int TabSuspensionTimeout { get; set; } = 5; // minutes
    public bool EnableHardwareAcceleration { get; set; } = true;
    public bool PreloadNextPage { get; set; } = false;
    public int MaxMemoryPerTab { get; set; } = 512; // MB

    // Developer
    public bool EnableDevTools { get; set; } = true;
    public bool ShowNetworkRequests { get; set; } = false;
    public bool EnableExtensions { get; set; } = true;

    // Startup
    public StartupBehavior StartupBehavior { get; set; } = StartupBehavior.NewTab;
    public List<string> StartupPages { get; set; } = new();
    public bool RestoreLastSession { get; set; } = false;
}

public enum StartupBehavior
{
    NewTab,
    Homepage,
    LastSession,
    SpecificPages
}
