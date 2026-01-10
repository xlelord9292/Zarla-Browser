using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zarla.Core.Config;

/// <summary>
/// Central configuration for Zarla Browser - edit zarla-config.json to customize
/// </summary>
public class ZarlaConfig
{
    private static ZarlaConfig? _instance;
    private static readonly object _lock = new();
    private static string? _configPath;

    // Branding
    [JsonPropertyName("browserName")]
    public string BrowserName { get; set; } = "Zarla";

    [JsonPropertyName("browserDisplayName")]
    public string BrowserDisplayName { get; set; } = "Zarla Browser";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.1";

    [JsonPropertyName("buildNumber")]
    public int BuildNumber { get; set; } = 1;

    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = "Zarla";

    [JsonPropertyName("websiteUrl")]
    public string WebsiteUrl { get; set; } = "https://github.com/xlelord9292/Zarla-Browser";

    [JsonPropertyName("supportUrl")]
    public string SupportUrl { get; set; } = "https://github.com/xlelord9292/Zarla-Browser/issues";

    // Update Settings
    [JsonPropertyName("enableAutoUpdate")]
    public bool EnableAutoUpdate { get; set; } = true;

    [JsonPropertyName("updateCheckUrl")]
    public string UpdateCheckUrl { get; set; } = "https://api.github.com/repos/xlelord9292/Zarla-Browser/releases/latest";

    [JsonPropertyName("releasesPageUrl")]
    public string ReleasesPageUrl { get; set; } = "https://github.com/xlelord9292/Zarla-Browser/releases";

    [JsonPropertyName("updateChannel")]
    public string UpdateChannel { get; set; } = "stable"; // stable, beta, dev

    // AI Configuration
    [JsonPropertyName("aiEnabled")]
    public bool AIEnabled { get; set; } = true;

    [JsonPropertyName("aiProviderName")]
    public string AIProviderName { get; set; } = "Zarla AI";

    [JsonPropertyName("aiApiEndpoint")]
    public string AIApiEndpoint { get; set; } = "https://api.groq.com/openai/v1/chat/completions";

    [JsonPropertyName("aiApiKey")]
    public string? AIApiKey { get; set; }

    [JsonPropertyName("encryptedAIApiKey")]
    public string? EncryptedAIApiKey { get; set; }

    [JsonPropertyName("aiBypassCode")]
    public string AIBypassCode { get; set; } = "XLEPOG1962";

    // Default Settings
    [JsonPropertyName("defaultHomepage")]
    public string DefaultHomepage { get; set; } = "zarla://newtab";

    [JsonPropertyName("defaultSearchEngine")]
    public string DefaultSearchEngine { get; set; } = "https://www.google.com/search?q=";

    [JsonPropertyName("defaultTheme")]
    public string DefaultTheme { get; set; } = "Dark";

    // Feature Flags
    [JsonPropertyName("enableExtensions")]
    public bool EnableExtensions { get; set; } = true;

    [JsonPropertyName("enablePasswordManager")]
    public bool EnablePasswordManager { get; set; } = true;

    [JsonPropertyName("enableAdBlocker")]
    public bool EnableAdBlocker { get; set; } = true;

    [JsonPropertyName("enableTrackerBlocker")]
    public bool EnableTrackerBlocker { get; set; } = true;

    [JsonPropertyName("enableFingerprintProtection")]
    public bool EnableFingerprintProtection { get; set; } = true;

    // User Agent
    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    /// <summary>
    /// Gets the singleton instance of the configuration
    /// </summary>
    public static ZarlaConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Load();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Initialize the config with a specific path
    /// </summary>
    public static void Initialize(string userDataFolder)
    {
        _configPath = Path.Combine(userDataFolder, "zarla-config.json");
        _instance = null; // Force reload
    }

    /// <summary>
    /// Loads configuration from zarla-config.json
    /// </summary>
    private static ZarlaConfig Load()
    {
        try
        {
            // Get exe location - try multiple methods for reliability
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Environment.ProcessPath;
            }
            var exeDir = !string.IsNullOrEmpty(exePath) ? Path.GetDirectoryName(exePath) : null;
            
            // Also check current directory for development
            var currentDir = Environment.CurrentDirectory;
            
            // Try to find project root from either location
            var projectRootFromExe = FindProjectRoot(exeDir);
            var projectRootFromCwd = FindProjectRoot(currentDir);
            var projectRoot = projectRootFromExe ?? projectRootFromCwd;
            
            var isDevelopment = !string.IsNullOrEmpty(projectRoot) && 
                               (exeDir?.Contains("bin\\Debug") == true || 
                                exeDir?.Contains("bin\\Release") == true ||
                                currentDir.Contains("Zarla"));

            // In development mode, prioritize project root config
            if (isDevelopment && !string.IsNullOrEmpty(projectRoot))
            {
                var projectConfigPath = Path.Combine(projectRoot, "zarla-config.json");
                if (File.Exists(projectConfigPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Loading config from project root: {projectConfigPath}");
                    var json = File.ReadAllText(projectConfigPath);
                    var config = JsonSerializer.Deserialize<ZarlaConfig>(json);
                    if (config != null)
                        return config;
                }
            }

            // Then try user data folder
            if (!string.IsNullOrEmpty(_configPath) && File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<ZarlaConfig>(json);
                if (config != null)
                    return config;
            }

            // Then try application directory (for installed version)
            var appConfigPath = Path.Combine(exeDir ?? "", "zarla-config.json");
            if (File.Exists(appConfigPath))
            {
                var json = File.ReadAllText(appConfigPath);
                var config = JsonSerializer.Deserialize<ZarlaConfig>(json);
                if (config != null)
                    return config;
            }

            // Try current working directory
            var cwdConfigPath = Path.Combine(Environment.CurrentDirectory, "zarla-config.json");
            if (File.Exists(cwdConfigPath))
            {
                var json = File.ReadAllText(cwdConfigPath);
                var config = JsonSerializer.Deserialize<ZarlaConfig>(json);
                if (config != null)
                    return config;
            }

            // Return defaults
            var defaultConfig = new ZarlaConfig();

            // Try to save defaults to user data folder
            if (!string.IsNullOrEmpty(_configPath))
            {
                defaultConfig.Save();
            }

            return defaultConfig;
        }
        catch
        {
            return new ZarlaConfig();
        }
    }

    /// <summary>
    /// Finds the project root by looking for Zarla.sln
    /// </summary>
    private static string? FindProjectRoot(string? startDir)
    {
        if (string.IsNullOrEmpty(startDir))
            return null;

        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Zarla.sln")) ||
                File.Exists(Path.Combine(dir.FullName, "zarla-config.json")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Saves the configuration to zarla-config.json
    /// </summary>
    public void Save()
    {
        try
        {
            if (string.IsNullOrEmpty(_configPath))
                return;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    /// <summary>
    /// Reloads configuration from disk
    /// </summary>
    public static void Reload()
    {
        lock (_lock)
        {
            _instance = Load();
        }
    }

    /// <summary>
    /// Gets the full version string (e.g., "1.0.1 Build 1")
    /// </summary>
    public string GetFullVersion() => $"{Version} Build {BuildNumber}";

    /// <summary>
    /// Gets version for comparison (e.g., "1.0.1")
    /// </summary>
    public Version GetVersionObject()
    {
        if (System.Version.TryParse(Version, out var version))
            return version;
        return new Version(1, 0, 0);
    }

    /// <summary>
    /// Updates the version after a successful update
    /// </summary>
    public void UpdateVersion(string newVersion, int? buildNumber = null)
    {
        Version = newVersion;
        if (buildNumber.HasValue)
            BuildNumber = buildNumber.Value;
        Save();
    }

    /// <summary>
    /// Checks if an update was just applied and updates the config version
    /// </summary>
    public static void CheckAndApplyPendingVersionUpdate(string userDataFolder)
    {
        try
        {
            var pendingUpdatePath = Path.Combine(userDataFolder, "pending-update.json");
            var completedUpdatePath = Path.Combine(userDataFolder, "completed-update.json");

            // Check if we have a completed update marker from installer
            if (File.Exists(completedUpdatePath))
            {
                var json = File.ReadAllText(completedUpdatePath);
                var updateData = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (updateData.TryGetProperty("newVersion", out var versionElement))
                {
                    var newVersion = versionElement.GetString();
                    if (!string.IsNullOrEmpty(newVersion))
                    {
                        Instance.UpdateVersion(newVersion);
                    }
                }

                // Clean up the marker files
                File.Delete(completedUpdatePath);
            }

            // Also check pending update and mark as complete
            if (File.Exists(pendingUpdatePath))
            {
                var json = File.ReadAllText(pendingUpdatePath);
                var updateData = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (updateData.TryGetProperty("NewVersion", out var versionElement))
                {
                    var newVersion = versionElement.GetString();
                    var currentConfigVersion = Instance.GetVersionObject();
                    
                    if (!string.IsNullOrEmpty(newVersion) && 
                        System.Version.TryParse(newVersion, out var pendingVersion))
                    {
                        // Check if the files have been updated (compare exe version vs config)
                        var exeVersion = GetAssemblyVersion();
                        if (exeVersion != null && exeVersion >= pendingVersion)
                        {
                            // Exe is newer than config, update was successful
                            Instance.UpdateVersion(newVersion);
                        }
                    }
                }

                // Clean up pending file
                try { File.Delete(pendingUpdatePath); } catch { }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking pending update: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the assembly version of the currently running executable
    /// </summary>
    private static Version? GetAssemblyVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                var version = assembly.GetName().Version;
                return version;
            }
        }
        catch { }
        return null;
    }
}
