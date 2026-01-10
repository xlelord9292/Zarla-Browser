using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Zarla.Core.Config;
using Zarla.Core.Updates;

namespace Zarla.Browser.Views;

public partial class AboutPage : UserControl
{
    private UpdateService? _updateService;
    private UpdateInfo? _currentUpdateInfo;
    private CancellationTokenSource? _downloadCts;

    public AboutPage()
    {
        InitializeComponent();
        Loaded += AboutPage_Loaded;
    }

    private void AboutPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadVersionInfo();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading AboutPage: {ex.Message}");
        }
    }

    private void LoadVersionInfo()
    {
        try
        {
            var config = ZarlaConfig.Instance;
            BrowserNameText.Text = config.BrowserDisplayName;
            VersionText.Text = $"Version {config.Version}";
            GitHubLink.Text = config.WebsiteUrl.Replace("https://", "");
        }
        catch (Exception ex)
        {
            BrowserNameText.Text = "Zarla Browser";
            VersionText.Text = "Version Unknown";
            GitHubLink.Text = "github.com/xlelord9292/Zarla-Browser";
            System.Diagnostics.Debug.WriteLine($"Error loading version info: {ex.Message}");
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = "Checking...";
        UpdateStatusText.Text = "Checking for updates...";
        UpdateSubText.Text = "Please wait...";

        try
        {
            // Initialize update service if needed
            if (_updateService == null)
            {
                var userDataFolder = App.UserDataFolder ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zarla");
                _updateService = new UpdateService(userDataFolder);
            }

            var updateInfo = await _updateService.CheckForUpdatesAsync();

            if (updateInfo != null)
            {
                _currentUpdateInfo = updateInfo;

                // Show update available panel
                UpdateAvailablePanel.Visibility = Visibility.Visible;
                NewVersionText.Text = $"Version {updateInfo.NewVersion} is available!";
                ReleaseNotesText.Text = TruncateReleaseNotes(updateInfo.ReleaseNotes, 200);

                UpdateStatusText.Text = "Update available!";
                UpdateSubText.Text = $"Current: {updateInfo.CurrentVersion} → New: {updateInfo.NewVersion}";
                CheckUpdateButton.Content = "Check Again";
            }
            else
            {
                UpdateStatusText.Text = "You're up to date!";
                UpdateSubText.Text = $"Zarla {ZarlaConfig.Instance.Version} is the latest version";
                CheckUpdateButton.Content = "Check Again";
                UpdateAvailablePanel.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Update check failed";
            UpdateSubText.Text = ex.Message;
            CheckUpdateButton.Content = "Retry";
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_currentUpdateInfo == null || _updateService == null)
            return;

        try
        {
            // Show download progress panel
            DownloadProgressPanel.Visibility = Visibility.Visible;
            UpdateAvailablePanel.Visibility = Visibility.Collapsed;

            _downloadCts = new CancellationTokenSource();

            // Subscribe to progress updates
            _updateService.DownloadProgress += OnDownloadProgress;

            var installerPath = await _updateService.DownloadUpdateAsync(_currentUpdateInfo, _downloadCts.Token);

            _updateService.DownloadProgress -= OnDownloadProgress;

            if (!string.IsNullOrEmpty(installerPath))
            {
                DownloadStatusText.Text = "Download complete!";
                DownloadProgressText.Text = "Ready to install";

                // Ask user if they want to install now
                var result = MessageBox.Show(
                    "Update downloaded successfully!\n\n" +
                    "The browser will close and restart to install the update.\n" +
                    "Your data (bookmarks, history, passwords) will be preserved.\n\n" +
                    "Install now?",
                    "Install Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Save update info and launch updater
                    _updateService.SaveUpdateInfo(_currentUpdateInfo, installerPath);
                    _updateService.LaunchUpdater(installerPath);

                    // Close the browser
                    Application.Current.Shutdown();
                }
            }
            else
            {
                DownloadStatusText.Text = "Download failed";
                DownloadProgressPanel.Visibility = Visibility.Collapsed;
                UpdateAvailablePanel.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            DownloadStatusText.Text = $"Error: {ex.Message}";
            DownloadProgressPanel.Visibility = Visibility.Collapsed;
            UpdateAvailablePanel.Visibility = Visibility.Visible;
        }
    }

    private void OnDownloadProgress(object? sender, DownloadProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            DownloadProgressBar.Value = e.ProgressPercent;
            DownloadProgressText.Text = $"{e.ProgressPercent:F0}% ({FormatBytes(e.BytesDownloaded)} / {FormatBytes(e.TotalBytes)})";
        });
    }

    private void ViewRelease_Click(object sender, RoutedEventArgs e)
    {
        var url = _currentUpdateInfo?.ReleasePageUrl ?? ZarlaConfig.Instance.ReleasesPageUrl;
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void GitHubLink_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = ZarlaConfig.Instance.WebsiteUrl,
            UseShellExecute = true
        });
    }

    private static string TruncateReleaseNotes(string notes, int maxLength)
    {
        if (string.IsNullOrEmpty(notes))
            return "";

        // Remove markdown formatting for cleaner display
        notes = System.Text.RegularExpressions.Regex.Replace(notes, @"[#*_`]", "");
        notes = System.Text.RegularExpressions.Regex.Replace(notes, @"\r?\n", " ");
        notes = System.Text.RegularExpressions.Regex.Replace(notes, @"\s+", " ");

        if (notes.Length <= maxLength)
            return notes.Trim();

        return notes.Substring(0, maxLength).Trim() + "...";
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int i = 0;
        double size = bytes;

        while (size >= 1024 && i < suffixes.Length - 1)
        {
            size /= 1024;
            i++;
        }

        return $"{size:F1} {suffixes[i]}";
    }
}
