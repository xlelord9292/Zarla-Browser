using System.IO;
using System.Windows;
using Zarla.Browser.Services;
using Zarla.Core.Config;
using Zarla.Core.Data;
using Zarla.Core.Updates;

namespace Zarla.Browser;

public partial class App : Application
{
    public static Database Database { get; private set; } = null!;
    public static SettingsService Settings { get; private set; } = null!;
    public static string UserDataFolder { get; private set; } = null!;
    public static UpdateService? UpdateService { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize user data folder
        UserDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zarla");

        if (!Directory.Exists(UserDataFolder))
            Directory.CreateDirectory(UserDataFolder);

        // Initialize config system
        ZarlaConfig.Initialize(UserDataFolder);

        // Check if an update was just applied and update config version accordingly
        ZarlaConfig.CheckAndApplyPendingVersionUpdate(UserDataFolder);

        // Initialize services
        Database = new Database();
        Settings = new SettingsService();

        // Initialize update service
        UpdateService = new UpdateService(UserDataFolder);
        UpdateService.CleanupOldUpdates();

        // Don't auto-check updates on startup - let user manually check in Settings
        // This prevents background crashes from failed update checks

        // Apply theme
        ApplyTheme(Settings.CurrentSettings.Theme);
    }

    public static void ApplyTheme(string theme)
    {
        var themeFile = theme == "Light" ? "Themes/Light.xaml" : "Themes/Dark.xaml";
        var dict = new ResourceDictionary
        {
            Source = new Uri(themeFile, UriKind.Relative)
        };

        Current.Resources.MergedDictionaries.Clear();
        Current.Resources.MergedDictionaries.Add(dict);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Settings.CurrentSettings.ClearDataOnExit)
        {
            // Clear browsing data on exit
            var cacheManager = new Zarla.Core.Performance.CacheManager();
            cacheManager.ClearAllDataAsync().Wait();
        }

        Database?.Dispose();
        base.OnExit(e);
    }
}
