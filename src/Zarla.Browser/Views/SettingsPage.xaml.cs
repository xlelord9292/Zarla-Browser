using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Zarla.Core.Performance;

namespace Zarla.Browser.Views;

public partial class SettingsPage : UserControl
{
    private bool _isLoading = true;

    private readonly Dictionary<string, string> _searchEngines = new()
    {
        { "Google", "https://www.google.com/search?q=" },
        { "DuckDuckGo", "https://duckduckgo.com/?q=" },
        { "Bing", "https://www.bing.com/search?q=" },
        { "Yahoo", "https://search.yahoo.com/search?p=" },
        { "Brave", "https://search.brave.com/search?q=" },
        { "Ecosia", "https://www.ecosia.org/search?q=" }
    };

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoading = true;
        var settings = App.Settings.CurrentSettings;

        // General
        HomepageTextBox.Text = settings.Homepage;
        DownloadPathText.Text = settings.DownloadPath;

        SearchEngineCombo.Items.Clear();
        foreach (var engine in _searchEngines.Keys)
        {
            SearchEngineCombo.Items.Add(engine);
        }
        var currentEngine = _searchEngines.FirstOrDefault(x => x.Value == settings.SearchEngine).Key ?? "Google";
        SearchEngineCombo.SelectedItem = currentEngine;

        // Appearance
        DarkThemeRadio.IsChecked = settings.Theme == "Dark";
        LightThemeRadio.IsChecked = settings.Theme == "Light";
        BookmarksBarCheckbox.IsChecked = settings.ShowBookmarksBar;
        HomeButtonCheckbox.IsChecked = settings.ShowHomeButton;

        // Privacy
        AdBlockerCheckbox.IsChecked = settings.EnableAdBlocker;
        TrackerBlockerCheckbox.IsChecked = settings.EnableTrackerBlocker;
        FingerprintCheckbox.IsChecked = settings.EnableFingerprintProtection;
        DoNotTrackCheckbox.IsChecked = settings.SendDoNotTrack;
        ClearOnExitCheckbox.IsChecked = settings.ClearDataOnExit;

        // Network - DNS
        switch (settings.DnsProvider)
        {
            case "Cloudflare":
                DnsCloudflareRadio.IsChecked = true;
                break;
            case "Quad9":
                DnsQuad9Radio.IsChecked = true;
                break;
            case "Google":
                DnsGoogleRadio.IsChecked = true;
                break;
            default:
                DnsSystemRadio.IsChecked = true;
                break;
        }

        // Network - Proxy
        switch (settings.ProxyMode)
        {
            case "System":
                ProxySystemRadio.IsChecked = true;
                break;
            case "Custom":
                ProxyCustomRadio.IsChecked = true;
                CustomProxyPanel.Visibility = Visibility.Visible;
                ProxyAddressTextBox.Text = settings.ProxyAddress;
                ProxyPortTextBox.Text = settings.ProxyPort.ToString();
                ProxyAuthCheckbox.IsChecked = settings.ProxyRequiresAuth;
                if (settings.ProxyRequiresAuth)
                {
                    ProxyAuthPanel.Visibility = Visibility.Visible;
                    ProxyUsernameTextBox.Text = settings.ProxyUsername;
                }
                break;
            default:
                ProxyNoneRadio.IsChecked = true;
                break;
        }

        // Performance
        TabSuspensionCheckbox.IsChecked = settings.EnableTabSuspension;
        HardwareAccelCheckbox.IsChecked = settings.EnableHardwareAcceleration;

        SuspensionTimeoutCombo.Items.Clear();
        foreach (var timeout in new[] { 1, 2, 5, 10, 15, 30 })
        {
            SuspensionTimeoutCombo.Items.Add($"{timeout} minutes");
        }
        SuspensionTimeoutCombo.SelectedIndex = Array.IndexOf(new[] { 1, 2, 5, 10, 15, 30 }, settings.TabSuspensionTimeout);
        if (SuspensionTimeoutCombo.SelectedIndex < 0) SuspensionTimeoutCombo.SelectedIndex = 2; // Default 5 min

        // Cache size
        await UpdateCacheSize();

        _isLoading = false;
    }

    private async Task UpdateCacheSize()
    {
        try
        {
            await Task.Run(() =>
            {
                var cacheManager = new CacheManager();
                var formattedSize = cacheManager.GetCacheSizeFormatted();
                Dispatcher.Invoke(() => CacheSizeText.Text = $"Cache size: {formattedSize}");
            });
        }
        catch
        {
            CacheSizeText.Text = "Cache size: Unknown";
        }
    }

    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton && radioButton.Tag is string section)
        {
            // Hide all sections
            GeneralSection.Visibility = Visibility.Collapsed;
            AppearanceSection.Visibility = Visibility.Collapsed;
            PrivacySection.Visibility = Visibility.Collapsed;
            NetworkSection.Visibility = Visibility.Collapsed;
            PerformanceSection.Visibility = Visibility.Collapsed;
            DataSection.Visibility = Visibility.Collapsed;
            AboutSection.Visibility = Visibility.Collapsed;

            // Show selected section
            switch (section)
            {
                case "General":
                    GeneralSection.Visibility = Visibility.Visible;
                    break;
                case "Appearance":
                    AppearanceSection.Visibility = Visibility.Visible;
                    break;
                case "Privacy":
                    PrivacySection.Visibility = Visibility.Visible;
                    break;
                case "Network":
                    NetworkSection.Visibility = Visibility.Visible;
                    break;
                case "Performance":
                    PerformanceSection.Visibility = Visibility.Visible;
                    break;
                case "Data":
                    DataSection.Visibility = Visibility.Visible;
                    break;
                case "About":
                    AboutSection.Visibility = Visibility.Visible;
                    break;
            }
        }
    }

    private void HomepageTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.Homepage = HomepageTextBox.Text);
    }

    private void SearchEngineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || SearchEngineCombo.SelectedItem == null) return;
        var engine = SearchEngineCombo.SelectedItem.ToString()!;
        if (_searchEngines.TryGetValue(engine, out var url))
        {
            App.Settings.Update(s => s.SearchEngine = url);
        }
    }

    private void DownloadPathButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Download Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            App.Settings.Update(s => s.DownloadPath = dialog.FolderName);
            DownloadPathText.Text = dialog.FolderName;
        }
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        var theme = DarkThemeRadio.IsChecked == true ? "Dark" : "Light";
        App.Settings.Update(s => s.Theme = theme);
        App.ApplyTheme(theme);
    }

    private void BookmarksBarCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.ShowBookmarksBar = BookmarksBarCheckbox.IsChecked == true);
    }

    private void HomeButtonCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.ShowHomeButton = HomeButtonCheckbox.IsChecked == true);
    }

    private void AdBlockerCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.EnableAdBlocker = AdBlockerCheckbox.IsChecked == true);
    }

    private void TrackerBlockerCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.EnableTrackerBlocker = TrackerBlockerCheckbox.IsChecked == true);
    }

    private void FingerprintCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.EnableFingerprintProtection = FingerprintCheckbox.IsChecked == true);
    }

    private void DoNotTrackCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.SendDoNotTrack = DoNotTrackCheckbox.IsChecked == true);
    }

    private void ClearOnExitCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.ClearDataOnExit = ClearOnExitCheckbox.IsChecked == true);
    }

    // DNS Settings
    private void DnsRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        string provider = "System";
        if (DnsCloudflareRadio.IsChecked == true) provider = "Cloudflare";
        else if (DnsQuad9Radio.IsChecked == true) provider = "Quad9";
        else if (DnsGoogleRadio.IsChecked == true) provider = "Google";

        App.Settings.Update(s => s.DnsProvider = provider);
    }

    // Proxy Settings
    private void ProxyRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        string mode = "None";
        if (ProxySystemRadio.IsChecked == true) mode = "System";
        else if (ProxyCustomRadio.IsChecked == true) mode = "Custom";

        CustomProxyPanel.Visibility = mode == "Custom" ? Visibility.Visible : Visibility.Collapsed;

        App.Settings.Update(s => s.ProxyMode = mode);
    }

    private void ProxySettings_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;

        App.Settings.Update(s =>
        {
            s.ProxyAddress = ProxyAddressTextBox.Text;
            if (int.TryParse(ProxyPortTextBox.Text, out var port))
                s.ProxyPort = port;
            s.ProxyUsername = ProxyUsernameTextBox.Text;
        });
    }

    private void ProxyPassword_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.ProxyPassword = ProxyPasswordBox.Password);
    }

    private void ProxyAuthCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        var requiresAuth = ProxyAuthCheckbox.IsChecked == true;
        ProxyAuthPanel.Visibility = requiresAuth ? Visibility.Visible : Visibility.Collapsed;
        App.Settings.Update(s => s.ProxyRequiresAuth = requiresAuth);
    }

    private void TabSuspensionCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.EnableTabSuspension = TabSuspensionCheckbox.IsChecked == true);
    }

    private void SuspensionTimeoutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || SuspensionTimeoutCombo.SelectedIndex < 0) return;
        var timeouts = new[] { 1, 2, 5, 10, 15, 30 };
        App.Settings.Update(s => s.TabSuspensionTimeout = timeouts[SuspensionTimeoutCombo.SelectedIndex]);
    }

    private void HardwareAccelCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.EnableHardwareAcceleration = HardwareAccelCheckbox.IsChecked == true);
    }

    private async void ClearDataButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will clear your browsing history, cache, and cookies. Continue?",
            "Clear Browsing Data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var cacheManager = new CacheManager();
                await cacheManager.ClearAllDataAsync();
                await App.Database.ClearHistoryAsync();

                await UpdateCacheSize();
                MessageBox.Show("Browsing data cleared successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
