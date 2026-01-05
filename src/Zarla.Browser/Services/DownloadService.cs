using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Zarla.Core.Data;
using Zarla.Core.Data.Models;

namespace Zarla.Browser.Services;

public partial class DownloadItem : ObservableObject
{
    [ObservableProperty]
    private long _id;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private long _receivedBytes;

    [ObservableProperty]
    private DownloadStatus _status = DownloadStatus.Pending;

    [ObservableProperty]
    private DateTime _startedAt = DateTime.UtcNow;

    [ObservableProperty]
    private string? _mimeType;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _speedText = string.Empty;

    private DateTime _lastProgressUpdate;
    private long _lastReceivedBytes;

    public void UpdateProgress(long received, long total)
    {
        ReceivedBytes = received;
        TotalBytes = total;
        Progress = total > 0 ? (double)received / total * 100 : 0;

        // Calculate speed
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastProgressUpdate).TotalSeconds;
        if (elapsed >= 1)
        {
            var bytesDelta = received - _lastReceivedBytes;
            var speedBps = bytesDelta / elapsed;
            SpeedText = FormatSpeed(speedBps);
            _lastProgressUpdate = now;
            _lastReceivedBytes = received;
        }
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond:F0} B/s";
        if (bytesPerSecond < 1024 * 1024)
            return $"{bytesPerSecond / 1024:F1} KB/s";
        return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
    }

    public string FormattedSize => FormatBytes(TotalBytes);
    public string FormattedReceived => FormatBytes(ReceivedBytes);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

public class DownloadService
{
    private readonly Database _database;
    private readonly ObservableCollection<DownloadItem> _downloads = new();
    private readonly string _defaultDownloadPath;

    public ObservableCollection<DownloadItem> Downloads => _downloads;

    public event EventHandler<DownloadItem>? DownloadStarted;
    public event EventHandler<DownloadItem>? DownloadCompleted;
    public event EventHandler<DownloadItem>? DownloadFailed;

    public DownloadService(Database database, string defaultDownloadPath)
    {
        _database = database;
        _defaultDownloadPath = defaultDownloadPath;

        if (!Directory.Exists(_defaultDownloadPath))
            Directory.CreateDirectory(_defaultDownloadPath);

        LoadRecentDownloads();
    }

    private async void LoadRecentDownloads()
    {
        var downloads = await _database.GetDownloadsAsync(50);
        foreach (var download in downloads)
        {
            _downloads.Add(new DownloadItem
            {
                Id = download.Id,
                Url = download.Url,
                FileName = download.FileName,
                FilePath = download.FilePath,
                TotalBytes = download.TotalBytes,
                ReceivedBytes = download.ReceivedBytes,
                Status = download.Status,
                StartedAt = download.StartedAt,
                MimeType = download.MimeType,
                Progress = download.TotalBytes > 0 ? (double)download.ReceivedBytes / download.TotalBytes * 100 : 0
            });
        }
    }

    public async Task<DownloadItem> StartDownload(string url, string suggestedFileName, string? mimeType = null)
    {
        var fileName = GetUniqueFileName(suggestedFileName);
        var filePath = Path.Combine(_defaultDownloadPath, fileName);

        var download = new Download
        {
            Url = url,
            FileName = fileName,
            FilePath = filePath,
            Status = DownloadStatus.InProgress,
            MimeType = mimeType
        };

        var id = await _database.AddDownloadAsync(download);

        var item = new DownloadItem
        {
            Id = id,
            Url = url,
            FileName = fileName,
            FilePath = filePath,
            Status = DownloadStatus.InProgress,
            MimeType = mimeType
        };

        _downloads.Insert(0, item);
        DownloadStarted?.Invoke(this, item);

        return item;
    }

    public async Task UpdateProgress(long downloadId, long receivedBytes, long totalBytes)
    {
        var item = _downloads.FirstOrDefault(d => d.Id == downloadId);
        if (item == null) return;

        item.UpdateProgress(receivedBytes, totalBytes);
        await _database.UpdateDownloadProgressAsync(downloadId, receivedBytes, DownloadStatus.InProgress);
    }

    public async Task CompleteDownload(long downloadId, bool success)
    {
        var item = _downloads.FirstOrDefault(d => d.Id == downloadId);
        if (item == null) return;

        var status = success ? DownloadStatus.Completed : DownloadStatus.Failed;
        item.Status = status;

        await _database.CompleteDownloadAsync(downloadId, status);

        if (success)
            DownloadCompleted?.Invoke(this, item);
        else
            DownloadFailed?.Invoke(this, item);
    }

    public void OpenFile(DownloadItem item)
    {
        if (File.Exists(item.FilePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.FilePath,
                UseShellExecute = true
            });
        }
    }

    public void OpenFolder(DownloadItem item)
    {
        if (File.Exists(item.FilePath))
        {
            Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
        }
        else if (Directory.Exists(_defaultDownloadPath))
        {
            Process.Start("explorer.exe", _defaultDownloadPath);
        }
    }

    public void ClearCompleted()
    {
        var completed = _downloads.Where(d => d.Status == DownloadStatus.Completed || d.Status == DownloadStatus.Failed).ToList();
        foreach (var item in completed)
        {
            _downloads.Remove(item);
        }
    }

    private string GetUniqueFileName(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 1;
        var result = fileName;

        while (File.Exists(Path.Combine(_defaultDownloadPath, result)))
        {
            result = $"{baseName} ({counter}){extension}";
            counter++;
        }

        return result;
    }
}
