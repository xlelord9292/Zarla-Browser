using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Zarla.Browser.Services;
using Zarla.Core.Data.Models;

namespace Zarla.Browser.Views;

public class DownloadViewModel
{
    public DownloadItem Item { get; }

    public DownloadViewModel(DownloadItem item)
    {
        Item = item;
    }

    public string FileName => Item.FileName;
    public string FormattedSize => Item.FormattedSize;
    public string SpeedText => Item.SpeedText;
    public double Progress => Item.Progress;

    public string FileIcon => GetFileIcon();
    public string StatusText => GetStatusText();
    public Brush StatusColor => GetStatusColor();

    public Visibility ProgressVisibility =>
        Item.Status == DownloadStatus.InProgress ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SpeedVisibility =>
        Item.Status == DownloadStatus.InProgress ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OpenVisibility =>
        Item.Status == DownloadStatus.Completed ? Visibility.Visible : Visibility.Collapsed;

    private string GetFileIcon()
    {
        var ext = Path.GetExtension(Item.FileName).ToLower();
        return ext switch
        {
            ".pdf" => "📄",
            ".doc" or ".docx" => "📝",
            ".xls" or ".xlsx" => "📊",
            ".ppt" or ".pptx" => "📽",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => "🖼",
            ".mp3" or ".wav" or ".flac" or ".aac" => "🎵",
            ".mp4" or ".mkv" or ".avi" or ".mov" => "🎬",
            ".zip" or ".rar" or ".7z" or ".tar" => "📦",
            ".exe" or ".msi" => "⚙",
            ".html" or ".htm" => "🌐",
            ".js" or ".ts" or ".py" or ".cs" => "💻",
            _ => "📁"
        };
    }

    private string GetStatusText()
    {
        return Item.Status switch
        {
            DownloadStatus.Pending => "Waiting...",
            DownloadStatus.InProgress => $"{Item.Progress:F0}% - {Item.SpeedText}",
            DownloadStatus.Completed => "Completed",
            DownloadStatus.Failed => "Failed",
            DownloadStatus.Cancelled => "Cancelled",
            DownloadStatus.Paused => "Paused",
            _ => ""
        };
    }

    private Brush GetStatusColor()
    {
        return Item.Status switch
        {
            DownloadStatus.Completed => new SolidColorBrush(Color.FromRgb(85, 255, 85)),
            DownloadStatus.Failed => new SolidColorBrush(Color.FromRgb(255, 85, 85)),
            DownloadStatus.Cancelled => new SolidColorBrush(Color.FromRgb(255, 170, 85)),
            _ => new SolidColorBrush(Color.FromRgb(160, 160, 160))
        };
    }
}

public partial class DownloadsPage : UserControl
{
    private readonly DownloadService _downloadService;

    public DownloadsPage(DownloadService downloadService)
    {
        InitializeComponent();
        _downloadService = downloadService;
        Loaded += DownloadsPage_Loaded;
    }

    private void DownloadsPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateList();

        // Subscribe to updates
        _downloadService.DownloadStarted += (s, d) => Dispatcher.Invoke(UpdateList);
        _downloadService.DownloadCompleted += (s, d) => Dispatcher.Invoke(UpdateList);
        _downloadService.DownloadFailed += (s, d) => Dispatcher.Invoke(UpdateList);
    }

    private void UpdateList()
    {
        var viewModels = _downloadService.Downloads
            .Select(d => new DownloadViewModel(d))
            .ToList();

        DownloadsList.ItemsSource = viewModels;
        EmptyState.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DownloadViewModel vm)
        {
            _downloadService.OpenFile(vm.Item);
        }
    }

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DownloadViewModel vm)
        {
            _downloadService.OpenFolder(vm.Item);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = App.Settings.CurrentSettings.DownloadPath;
        if (Directory.Exists(path))
        {
            Process.Start("explorer.exe", path);
        }
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        _downloadService.ClearCompleted();
        UpdateList();
    }
}
