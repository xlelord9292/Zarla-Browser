using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Zarla.Core.Performance;
using Zarla.Core.AI;
using Zarla.Core.Security;

namespace Zarla.Browser.Views;

public partial class SettingsPage : UserControl
{
    private bool _isLoading = true;
    private AIUsageTracker? _usageTracker;

    private readonly Dictionary<string, string> _searchEngines = new()
    {
        { "Google", "https://www.google.com/search?q=" },
        { "DuckDuckGo", "https://duckduckgo.com/?q=" },
        { "Bing", "https://www.bing.com/search?q=" },
        { "Yahoo", "https://search.yahoo.com/search?p=" },
        { "Brave", "https://search.brave.com/search?q=" },
        { "Ecosia", "https://www.ecosia.org/search?q=" },
         { "Zarla (Coming SOON)", "https://www.google.com/search?q=" }
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

        // Security
        switch (settings.SecurityLevel)
        {
            case SecurityLevel.Low:
                SecurityLowRadio.IsChecked = true;
                break;
            case SecurityLevel.High:
                SecurityHighRadio.IsChecked = true;
                break;
            default:
                SecurityMediumRadio.IsChecked = true;
                break;
        }
        SecurityWarningsCheckbox.IsChecked = settings.ShowSecurityWarnings;
        BlockDangerousSitesCheckbox.IsChecked = settings.BlockDangerousSites;

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

        // AI Settings
        LoadAISettings();

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
            SecuritySection.Visibility = Visibility.Collapsed;
            PasswordsSection.Visibility = Visibility.Collapsed;
            NetworkSection.Visibility = Visibility.Collapsed;
            PerformanceSection.Visibility = Visibility.Collapsed;
            DataSection.Visibility = Visibility.Collapsed;
            AISection.Visibility = Visibility.Collapsed;
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
                case "Security":
                    SecuritySection.Visibility = Visibility.Visible;
                    break;
                case "Passwords":
                    PasswordsSection.Visibility = Visibility.Visible;
                    LoadPasswordsSettings();
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
                case "AI":
                    AISection.Visibility = Visibility.Visible;
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

    // Security Settings
    private void SecurityLevel_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        SecurityLevel level = SecurityLevel.Medium;
        if (SecurityLowRadio.IsChecked == true) level = SecurityLevel.Low;
        else if (SecurityHighRadio.IsChecked == true) level = SecurityLevel.High;

        App.Settings.Update(s => s.SecurityLevel = level);
    }

    private void SecurityWarningsCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.ShowSecurityWarnings = SecurityWarningsCheckbox.IsChecked == true);
    }

    private void BlockDangerousSitesCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.BlockDangerousSites = BlockDangerousSitesCheckbox.IsChecked == true);
    }

    private void ViewDocsButton_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to docs page
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToUrl("zarla://docs");
        }
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

    // AI Settings
    private void LoadAISettings()
    {
        var settings = App.Settings.CurrentSettings;
        _usageTracker = new AIUsageTracker(App.UserDataFolder);

        // Set bypass code if saved
        if (!string.IsNullOrEmpty(settings.AIBypassCode))
        {
            _usageTracker.SetBypassCode(settings.AIBypassCode);
        }

        // AI Enable/Disable
        AIEnabledCheckbox.IsChecked = settings.AIEnabled;

        // Load models into combo
        AIModelCombo.Items.Clear();
        foreach (var model in BuiltInModels.Models)
        {
            AIModelCombo.Items.Add(new ComboBoxItem
            {
                Content = model.Name,
                Tag = model.Id
            });
        }

        // Add custom models
        foreach (var customModel in settings.CustomModels)
        {
            AIModelCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{customModel.Name} (Custom)",
                Tag = customModel.ModelId
            });
        }

        // Select current model
        for (int i = 0; i < AIModelCombo.Items.Count; i++)
        {
            if (AIModelCombo.Items[i] is ComboBoxItem item && item.Tag?.ToString() == settings.SelectedAIModel)
            {
                AIModelCombo.SelectedIndex = i;
                break;
            }
        }

        if (AIModelCombo.SelectedIndex < 0 && AIModelCombo.Items.Count > 0)
            AIModelCombo.SelectedIndex = 0;

        // Update usage stats
        UpdateUsageStats();

        // Load custom models list
        LoadCustomModelsList();
    }

    private void UpdateUsageStats()
    {
        if (_usageTracker == null) return;

        var settings = App.Settings.CurrentSettings;
        var model = BuiltInModels.GetModel(settings.SelectedAIModel);
        var dailyLimit = model?.DailyLimit ?? 25;

        var usage = _usageTracker.CanUseModel(settings.SelectedAIModel, dailyLimit);

        if (_usageTracker.IsBypassActive)
        {
            UsageStatsText.Text = "Unlimited usage active";
            UsageResetText.Text = "Bypass code applied";
            UnlimitedBadge.Visibility = Visibility.Visible;
        }
        else
        {
            UsageStatsText.Text = $"{usage.RemainingUses} / {dailyLimit} requests remaining today";
            if (usage.ResetTime.HasValue)
            {
                var resetIn = usage.ResetTime.Value - DateTime.UtcNow;
                UsageResetText.Text = $"Resets in {resetIn.Hours}h {resetIn.Minutes}m";
            }
            UnlimitedBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadCustomModelsList()
    {
        CustomModelsPanel.Children.Clear();
        var settings = App.Settings.CurrentSettings;

        if (settings.CustomModels.Count == 0)
        {
            NoCustomModelsText.Visibility = Visibility.Visible;
            return;
        }

        NoCustomModelsText.Visibility = Visibility.Collapsed;

        foreach (var model in settings.CustomModels)
        {
            var border = new Border
            {
                Background = (Brush)FindResource("BackgroundBrush"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = model.Name,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = (Brush)FindResource("TextBrush")
            });
            stack.Children.Add(new TextBlock
            {
                Text = model.ModelId,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 2, 0, 0)
            });

            Grid.SetColumn(stack, 0);
            grid.Children.Add(stack);

            var deleteBtn = new Button
            {
                Content = "Delete",
                Style = (Style)FindResource("DangerButton"),
                Padding = new Thickness(12, 6, 12, 6),
                Tag = model.Id
            };
            deleteBtn.Click += DeleteCustomModel_Click;
            Grid.SetColumn(deleteBtn, 1);
            grid.Children.Add(deleteBtn);

            border.Child = grid;
            CustomModelsPanel.Children.Add(border);
        }
    }

    private void AIEnabledCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.AIEnabled = AIEnabledCheckbox.IsChecked == true);
    }

    private void AIModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || AIModelCombo.SelectedItem == null) return;

        if (AIModelCombo.SelectedItem is ComboBoxItem item && item.Tag is string modelId)
        {
            App.Settings.Update(s => s.SelectedAIModel = modelId);
            UpdateUsageStats();
        }
    }

    private void ActivateBypassButton_Click(object sender, RoutedEventArgs e)
    {
        if (_usageTracker == null) return;

        var code = BypassCodeTextBox.Text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            MessageBox.Show("Please enter a bypass code.", "Invalid Code", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_usageTracker.ValidateBypassCode(code))
        {
            _usageTracker.SetBypassCode(code);
            App.Settings.Update(s => s.AIBypassCode = code);
            UpdateUsageStats();
            MessageBox.Show("Bypass code activated! You now have unlimited AI usage.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Invalid bypass code. Please check and try again.", "Invalid Code", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddCustomModelButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddModelDialog();
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() == true)
        {
            var customModel = new CustomModel
            {
                Name = dialog.ModelName,
                ModelId = dialog.ModelId,
                ApiKey = dialog.ApiKey,
                BaseUrl = dialog.BaseUrl,
                DailyLimit = dialog.DailyLimit
            };

            App.Settings.Update(s => s.CustomModels.Add(customModel));
            LoadAISettings();
        }
    }

    private void DeleteCustomModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete this custom model?",
                "Delete Model",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                App.Settings.Update(s =>
                {
                    var model = s.CustomModels.FirstOrDefault(m => m.Id == modelId);
                    if (model != null)
                        s.CustomModels.Remove(model);
                });
                LoadAISettings();
            }
        }
    }

    // Password Settings
    private Zarla.Core.Security.PasswordManager? _passwordManager;

    private void LoadPasswordsSettings()
    {
        var settings = App.Settings.CurrentSettings;

        PasswordAutofillCheckbox.IsChecked = settings.EnablePasswordAutofill;
        OfferSavePasswordsCheckbox.IsChecked = settings.OfferToSavePasswords;
        AutoSignInCheckbox.IsChecked = settings.AutoSignIn;

        // Update password count
        UpdatePasswordCount();
    }

    private void UpdatePasswordCount()
    {
        try
        {
            if (_passwordManager == null)
            {
                _passwordManager = new Zarla.Core.Security.PasswordManager(App.UserDataFolder);
            }

            // Just show if there are any passwords (can't count without unlocking)
            SavedPasswordsCount.Text = _passwordManager.HasMasterPassword ? "Encrypted" : "0";
        }
        catch
        {
            SavedPasswordsCount.Text = "0";
        }
    }

    public void SetPasswordManager(Zarla.Core.Security.PasswordManager manager)
    {
        _passwordManager = manager;
    }

    private void OpenPasswordManagerButton_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToUrl("zarla://passwords");
        }
    }

    private void PasswordAutofillCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.EnablePasswordAutofill = PasswordAutofillCheckbox.IsChecked == true);
    }

    private void OfferSavePasswordsCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.OfferToSavePasswords = OfferSavePasswordsCheckbox.IsChecked == true);
    }

    private void AutoSignInCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        App.Settings.Update(s => s.AutoSignIn = AutoSignInCheckbox.IsChecked == true);
    }

    private void ExportPasswordsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_passwordManager == null || !_passwordManager.IsUnlocked)
        {
            MessageBox.Show("Please unlock the password manager first by opening it from the button above.",
                "Password Manager Locked", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var data = _passwordManager.ExportPasswords();
        if (data != null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Zarla Passwords (*.zpw)|*.zpw",
                DefaultExt = "zpw",
                FileName = "passwords_backup"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.WriteAllText(dialog.FileName, data);
                    MessageBox.Show("Passwords exported successfully.", "Export Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export: {ex.Message}", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void ImportPasswordsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_passwordManager == null || !_passwordManager.IsUnlocked)
        {
            MessageBox.Show("Please unlock the password manager first by opening it from the button above.",
                "Password Manager Locked", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Zarla Passwords (*.zpw)|*.zpw|All Files (*.*)|*.*",
            DefaultExt = "zpw"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var data = System.IO.File.ReadAllText(dialog.FileName);
                var result = MessageBox.Show("Merge with existing passwords? Click 'No' to replace all.",
                    "Import Mode", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (_passwordManager.ImportPasswords(data, result == MessageBoxResult.Yes))
                {
                    MessageBox.Show("Passwords imported successfully.", "Import Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to import passwords. The file may be corrupted.",
                        "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import: {ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
