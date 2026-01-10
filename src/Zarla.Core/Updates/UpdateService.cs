using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Zarla.Core.Config;

namespace Zarla.Core.Updates;

/// <summary>
/// Service for checking and downloading updates from GitHub releases
/// </summary>
public class UpdateService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _userDataFolder;

    public event EventHandler<UpdateInfo>? UpdateAvailable;
    public event EventHandler<DownloadProgressEventArgs>? DownloadProgress;
    public event EventHandler<string>? UpdateReady;
    public event EventHandler<string>? UpdateError;

    public UpdateService(string userDataFolder)
    {
        _userDataFolder = userDataFolder;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Zarla-Browser-UpdateChecker");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    /// <summary>
    /// Checks for updates from GitHub releases
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var config = ZarlaConfig.Instance;
            if (!config.EnableAutoUpdate)
                return null;

            var response = await _httpClient.GetAsync(config.UpdateCheckUrl);
            if (!response.IsSuccessStatusCode)
                return null;

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
            if (release == null)
                return null;

            // Parse version from tag name (e.g., "v1.0.2" or "1.0.2")
            var tagVersion = release.TagName?.TrimStart('v', 'V') ?? "0.0.0";
            if (!Version.TryParse(tagVersion, out var remoteVersion))
                return null;

            var currentVersion = config.GetVersionObject();

            if (remoteVersion > currentVersion)
            {
                // Find the installer asset
                var installerAsset = release.Assets?.FirstOrDefault(a =>
                    a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true &&
                    (a.Name.Contains("Setup") || a.Name.Contains("Installer")));

                var updateInfo = new UpdateInfo
                {
                    CurrentVersion = config.Version,
                    NewVersion = tagVersion,
                    ReleaseNotes = release.Body ?? "No release notes available.",
                    ReleaseDate = release.PublishedAt ?? DateTime.Now,
                    DownloadUrl = installerAsset?.BrowserDownloadUrl ?? "",
                    FileName = installerAsset?.Name ?? $"ZarlaSetup-{tagVersion}.exe",
                    FileSize = installerAsset?.Size ?? 0,
                    ReleasePageUrl = release.HtmlUrl ?? config.ReleasesPageUrl,
                    IsPreRelease = release.Prerelease
                };

                UpdateAvailable?.Invoke(this, updateInfo);
                return updateInfo;
            }

            return null;
        }
        catch (Exception ex)
        {
            UpdateError?.Invoke(this, $"Failed to check for updates: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads the update installer to the updates folder
    /// </summary>
    public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
        {
            UpdateError?.Invoke(this, "No download URL available");
            return null;
        }

        try
        {
            var updatesFolder = Path.Combine(_userDataFolder, "Updates");
            Directory.CreateDirectory(updatesFolder);

            var filePath = Path.Combine(updatesFolder, updateInfo.FileName);

            // Delete old file if exists
            if (File.Exists(filePath))
                File.Delete(filePath);

            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSize;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;
            var lastProgressUpdate = DateTime.Now;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                // Report progress every 100ms
                if ((DateTime.Now - lastProgressUpdate).TotalMilliseconds > 100)
                {
                    var progress = totalBytes > 0 ? (double)totalBytesRead / totalBytes * 100 : 0;
                    DownloadProgress?.Invoke(this, new DownloadProgressEventArgs
                    {
                        BytesDownloaded = totalBytesRead,
                        TotalBytes = totalBytes,
                        ProgressPercent = progress
                    });
                    lastProgressUpdate = DateTime.Now;
                }
            }

            // Final progress update
            DownloadProgress?.Invoke(this, new DownloadProgressEventArgs
            {
                BytesDownloaded = totalBytesRead,
                TotalBytes = totalBytes,
                ProgressPercent = 100
            });

            UpdateReady?.Invoke(this, filePath);
            return filePath;
        }
        catch (OperationCanceledException)
        {
            UpdateError?.Invoke(this, "Download cancelled");
            return null;
        }
        catch (Exception ex)
        {
            UpdateError?.Invoke(this, $"Download failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Launches the updater to install the update
    /// </summary>
    public void LaunchUpdater(string installerPath)
    {
        try
        {
            var updaterPath = Path.Combine(AppContext.BaseDirectory, "ZarlaUpdater.exe");

            // If updater doesn't exist, just run the installer directly
            if (!File.Exists(updaterPath))
            {
                // Run installer directly with silent flag
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/S", // NSIS silent install
                    UseShellExecute = true
                });
            }
            else
            {
                // Launch the updater with installer path and current process ID
                var currentProcessId = Process.GetCurrentProcess().Id;
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{installerPath}\" {currentProcessId}",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            UpdateError?.Invoke(this, $"Failed to launch updater: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves update info to disk for the updater to use
    /// </summary>
    public void SaveUpdateInfo(UpdateInfo info, string installerPath)
    {
        try
        {
            var infoPath = Path.Combine(_userDataFolder, "pending-update.json");
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                info.CurrentVersion,
                info.NewVersion,
                InstallerPath = installerPath,
                Timestamp = DateTime.Now
            });
            File.WriteAllText(infoPath, json);
        }
        catch { }
    }

    /// <summary>
    /// Marks update as completed and updates the config version
    /// Called after installer finishes successfully
    /// </summary>
    public static void MarkUpdateComplete(string userDataFolder, string newVersion)
    {
        try
        {
            // Save completed update marker
            var completedPath = Path.Combine(userDataFolder, "completed-update.json");
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                newVersion,
                completedAt = DateTime.Now
            });
            File.WriteAllText(completedPath, json);

            // Also directly update the config file
            var configPath = Path.Combine(userDataFolder, "zarla-config.json");
            if (File.Exists(configPath))
            {
                var configJson = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(configJson);
                
                if (config != null)
                {
                    // Create updated config with new version
                    var updatedConfig = new Dictionary<string, object>();
                    foreach (var kvp in config)
                    {
                        if (kvp.Key == "version")
                            updatedConfig[kvp.Key] = newVersion;
                        else
                            updatedConfig[kvp.Key] = kvp.Value;
                    }

                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    File.WriteAllText(configPath, System.Text.Json.JsonSerializer.Serialize(updatedConfig, options));
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error marking update complete: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the path to the user data folder
    /// </summary>
    public static string GetUserDataFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zarla");
    }

    /// <summary>
    /// Cleans up old update files
    /// </summary>
    public void CleanupOldUpdates()
    {
        try
        {
            var updatesFolder = Path.Combine(_userDataFolder, "Updates");
            if (Directory.Exists(updatesFolder))
            {
                foreach (var file in Directory.GetFiles(updatesFolder, "*.exe"))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < DateTime.Now.AddDays(-7))
                        {
                            File.Delete(file);
                        }
                    }
                    catch { }
                }
            }

            // Remove pending update info
            var pendingUpdatePath = Path.Combine(_userDataFolder, "pending-update.json");
            if (File.Exists(pendingUpdatePath))
            {
                File.Delete(pendingUpdatePath);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public class UpdateInfo
{
    public string CurrentVersion { get; set; } = "";
    public string NewVersion { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public DateTime ReleaseDate { get; set; }
    public string DownloadUrl { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string ReleasePageUrl { get; set; } = "";
    public bool IsPreRelease { get; set; }
}

public class DownloadProgressEventArgs : EventArgs
{
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double ProgressPercent { get; set; }
}

// GitHub API response models
public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
