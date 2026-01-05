namespace Zarla.Core.Performance;

public class CacheManager
{
    private readonly string _cachePath;
    private readonly string _userDataPath;
    private readonly long _maxCacheSizeBytes;
    private bool _isEnabled = true;

    public CacheManager(long maxCacheSizeMB = 500)
    {
        _userDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zarla");
        _cachePath = Path.Combine(_userDataPath, "Cache");
        _maxCacheSizeBytes = maxCacheSizeMB * 1024 * 1024;

        EnsureCacheDirectory();
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public string CachePath => _cachePath;
    public string UserDataPath => _userDataPath;

    private void EnsureCacheDirectory()
    {
        try
        {
            if (!Directory.Exists(_cachePath))
                Directory.CreateDirectory(_cachePath);
        }
        catch
        {
            // Ignore directory creation errors
        }
    }

    public long GetCacheSizeBytes()
    {
        try
        {
            long totalSize = 0;
            
            // Check the EBWebView folder (WebView2 cache)
            var webViewCachePath = Path.Combine(_userDataPath, "EBWebView");
            if (Directory.Exists(webViewCachePath))
            {
                totalSize += GetDirectorySizeRecursive(webViewCachePath);
            }

            // Check our custom cache folder
            if (Directory.Exists(_cachePath))
            {
                totalSize += GetDirectorySizeRecursive(_cachePath);
            }

            return totalSize;
        }
        catch
        {
            return 0;
        }
    }

    private long GetDirectorySizeRecursive(string path)
    {
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0; }
                });
        }
        catch
        {
            return 0;
        }
    }

    public string GetCacheSizeFormatted()
    {
        var bytes = GetCacheSizeBytes();
        return FormatBytes(bytes);
    }

    public async Task ClearCacheAsync()
    {
        await Task.Run(() =>
        {
            // Clear custom cache
            if (Directory.Exists(_cachePath))
            {
                ClearDirectoryContents(_cachePath);
            }

            // Clear WebView2 cache (be careful not to delete essential files)
            var webViewCachePath = Path.Combine(_userDataPath, "EBWebView", "Default", "Cache");
            if (Directory.Exists(webViewCachePath))
            {
                ClearDirectoryContents(webViewCachePath);
            }

            var webViewCodeCachePath = Path.Combine(_userDataPath, "EBWebView", "Default", "Code Cache");
            if (Directory.Exists(webViewCodeCachePath))
            {
                ClearDirectoryContents(webViewCodeCachePath);
            }
        });
    }

    private void ClearDirectoryContents(string path)
    {
        try
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // File in use, skip
                }
            }

            // Remove empty directories
            foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Reverse())
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch
                {
                    // Directory in use
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    public async Task TrimCacheAsync()
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(_cachePath)) return;

            var currentSize = GetCacheSizeBytes();
            if (currentSize <= _maxCacheSizeBytes) return;

            // Delete oldest files first
            var files = Directory.GetFiles(_cachePath, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastAccessTime)
                .ToList();

            var targetSize = _maxCacheSizeBytes * 0.8; // Trim to 80%
            var deletedSize = 0L;
            var toDelete = currentSize - (long)targetSize;

            foreach (var file in files)
            {
                if (deletedSize >= toDelete) break;

                try
                {
                    var size = file.Length;
                    file.Delete();
                    deletedSize += size;
                }
                catch
                {
                    // File in use
                }
            }
        });
    }

    public async Task ClearCookiesAsync()
    {
        var cookiePaths = new[]
        {
            Path.Combine(_userDataPath, "Cookies"),
            Path.Combine(_userDataPath, "EBWebView", "Default", "Cookies"),
            Path.Combine(_userDataPath, "EBWebView", "Default", "Cookies-journal")
        };

        await Task.Run(() =>
        {
            foreach (var path in cookiePaths)
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    else if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                catch
                {
                    // In use
                }
            }
        });
    }

    public async Task ClearLocalStorageAsync()
    {
        var storagePaths = new[]
        {
            Path.Combine(_userDataPath, "Local Storage"),
            Path.Combine(_userDataPath, "EBWebView", "Default", "Local Storage"),
            Path.Combine(_userDataPath, "EBWebView", "Default", "Session Storage")
        };

        await Task.Run(() =>
        {
            foreach (var path in storagePaths)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
                catch
                {
                    // In use
                }
            }
        });
    }

    public async Task ClearAllDataAsync()
    {
        await ClearCacheAsync();
        await ClearCookiesAsync();
        await ClearLocalStorageAsync();
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
