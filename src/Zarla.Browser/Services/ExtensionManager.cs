using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace Zarla.Browser.Services;

/// <summary>
/// Manages Chrome extension support for Zarla browser
/// WebView2 supports extensions in Edge/Chrome extension format (.crx)
/// </summary>
public class ExtensionManager
{
    private readonly string _extensionsPath;
    private readonly List<Extension> _installedExtensions = new();
    private bool _isInitialized;
    
    public event EventHandler<Extension>? ExtensionInstalled;
    public event EventHandler<string>? ExtensionRemoved;
    public event EventHandler<string>? ExtensionError;
    
    public IReadOnlyList<Extension> InstalledExtensions => _installedExtensions.AsReadOnly();
    
    public ExtensionManager()
    {
        _extensionsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zarla", "Extensions");
        
        Directory.CreateDirectory(_extensionsPath);
    }
    
    /// <summary>
    /// Initializes extension support for a WebView2 environment
    /// Must be called during CoreWebView2Environment creation
    /// </summary>
    public Task<CoreWebView2EnvironmentOptions> GetEnvironmentOptionsAsync()
    {
        var options = new CoreWebView2EnvironmentOptions
        {
            AreBrowserExtensionsEnabled = true,
            AdditionalBrowserArguments = "--enable-features=ExtensionsToolbarMenu"
        };
        
        return Task.FromResult(options);
    }
    
    /// <summary>
    /// Loads installed extensions after WebView2 is initialized
    /// </summary>
    public async Task InitializeAsync(CoreWebView2 webView)
    {
        if (_isInitialized) return;

        try
        {
            // Load extension metadata from disk
            LoadExtensionMetadata();

            // Get already loaded extensions from the profile
            var loadedExtensions = await webView.Profile.GetBrowserExtensionsAsync();
            var loadedIds = loadedExtensions.Select(e => e.Id).ToHashSet();

            // Add each extension to the browser profile if not already loaded
            foreach (var ext in _installedExtensions.ToList())
            {
                try
                {
                    // Skip if path doesn't exist
                    if (!Directory.Exists(ext.Path))
                    {
                        ext.IsEnabled = false;
                        ext.Error = "Extension folder not found";
                        continue;
                    }

                    // Check if manifest.json exists
                    var manifestPath = Path.Combine(ext.Path, "manifest.json");
                    if (!File.Exists(manifestPath))
                    {
                        ext.IsEnabled = false;
                        ext.Error = "manifest.json not found";
                        continue;
                    }

                    // Add to browser profile
                    await webView.Profile.AddBrowserExtensionAsync(ext.Path);
                    ext.IsEnabled = true;
                    ext.Error = null;

                    System.Diagnostics.Debug.WriteLine($"Loaded extension: {ext.Name} from {ext.Path}");
                }
                catch (Exception ex)
                {
                    ext.IsEnabled = false;
                    ext.Error = ex.Message;
                    System.Diagnostics.Debug.WriteLine($"Failed to load extension {ext.Name}: {ex.Message}");
                }
            }

            // Save updated metadata
            SaveExtensionMetadata();

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            ExtensionError?.Invoke(this, $"Failed to initialize extensions: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Extension initialization error: {ex}");
        }
    }
    
    /// <summary>
    /// Installs an extension from a .crx file or unpacked folder
    /// </summary>
    public async Task<Extension?> InstallExtensionAsync(string sourcePath, CoreWebView2 webView)
    {
        try
        {
            Extension? extension = null;
            
            if (sourcePath.EndsWith(".crx", StringComparison.OrdinalIgnoreCase))
            {
                extension = await InstallFromCrxAsync(sourcePath, webView);
            }
            else if (Directory.Exists(sourcePath))
            {
                extension = await InstallFromFolderAsync(sourcePath, webView);
            }
            else if (sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                extension = await InstallFromZipAsync(sourcePath, webView);
            }
            
            if (extension != null)
            {
                _installedExtensions.Add(extension);
                SaveExtensionMetadata();
                ExtensionInstalled?.Invoke(this, extension);
            }
            
            return extension;
        }
        catch (Exception ex)
        {
            ExtensionError?.Invoke(this, $"Failed to install extension: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Installs an extension from Chrome Web Store ID
    /// </summary>
    public async Task<Extension?> InstallFromWebStoreAsync(string extensionId, CoreWebView2 webView)
    {
        try
        {
            // Try multiple download URLs as Chrome Web Store has restrictions
            var downloadUrls = new[]
            {
                // Edge extension format (works better with WebView2)
                $"https://edge.microsoft.com/extensionwebstorebase/v1/crx?response=redirect&x=id%3D{extensionId}%26installsource%3Dondemand%26uc",
                // Chrome Web Store format
                $"https://clients2.google.com/service/update2/crx?response=redirect&prodversion=120.0.0.0&acceptformat=crx2,crx3&x=id%3D{extensionId}%26uc",
                // Alternative Chrome format
                $"https://clients2.google.com/service/update2/crx?response=redirect&os=win&arch=x64&os_arch=x86_64&nacl_arch=x86-64&prod=chromecrx&prodchannel=&prodversion=120.0.0.0&lang=en&acceptformat=crx3&x=id%3D{extensionId}%26installsource%3Dondemand%26uc"
            };

            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/x-chrome-extension,*/*");
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            byte[]? crxBytes = null;
            string? successUrl = null;

            foreach (var downloadUrl in downloadUrls)
            {
                try
                {
                    var response = await httpClient.GetAsync(downloadUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        crxBytes = await response.Content.ReadAsByteArrayAsync();
                        // Verify it's actually a CRX file (starts with "Cr24" magic bytes)
                        if (crxBytes.Length > 4 && crxBytes[0] == 'C' && crxBytes[1] == 'r' && crxBytes[2] == '2' && crxBytes[3] == '4')
                        {
                            successUrl = downloadUrl;
                            break;
                        }
                        // Or check for ZIP signature (some downloads skip CRX header)
                        if (crxBytes.Length > 4 && crxBytes[0] == 0x50 && crxBytes[1] == 0x4B)
                        {
                            successUrl = downloadUrl;
                            break;
                        }
                        crxBytes = null; // Reset if not valid
                    }
                }
                catch
                {
                    // Try next URL
                    continue;
                }
            }

            if (crxBytes == null || crxBytes.Length < 100)
            {
                ExtensionError?.Invoke(this, $"Could not download extension. Try using 'Load unpacked' with a downloaded extension folder instead.");
                return null;
            }

            // Save to temp file and install
            var tempPath = Path.Combine(Path.GetTempPath(), $"{extensionId}.crx");
            await File.WriteAllBytesAsync(tempPath, crxBytes);

            Extension? extension = null;

            // Check if it's a ZIP file (no CRX header)
            if (crxBytes[0] == 0x50 && crxBytes[1] == 0x4B)
            {
                extension = await InstallFromZipAsync(tempPath, webView);
            }
            else
            {
                extension = await InstallFromCrxAsync(tempPath, webView);
            }

            // Clean up temp file
            try { File.Delete(tempPath); } catch { }

            if (extension != null)
            {
                // Update the ID to match the store ID
                extension.Id = extensionId;
                SaveExtensionMetadata();
            }

            return extension;
        }
        catch (Exception ex)
        {
            ExtensionError?.Invoke(this, $"Failed to download extension: {ex.Message}\n\nTip: Download the extension manually and use 'Load unpacked' to install it.");
            return null;
        }
    }

    /// <summary>
    /// Installs an extension from Edge Add-ons store ID
    /// </summary>
    public async Task<Extension?> InstallFromEdgeStoreAsync(string extensionId, CoreWebView2 webView)
    {
        try
        {
            // Edge Add-ons CRX download URL
            var downloadUrl = $"https://edge.microsoft.com/extensionwebstorebase/v1/crx?response=redirect&x=id%3D{extensionId}%26installsource%3Dondemand%26uc";

            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/x-chrome-extension,*/*");
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var response = await httpClient.GetAsync(downloadUrl);
            if (!response.IsSuccessStatusCode)
            {
                ExtensionError?.Invoke(this, $"Failed to download extension from Edge Add-ons (HTTP {response.StatusCode})");
                return null;
            }

            var crxBytes = await response.Content.ReadAsByteArrayAsync();

            if (crxBytes.Length < 100)
            {
                ExtensionError?.Invoke(this, "Downloaded file is too small - extension may not be available");
                return null;
            }

            // Save to temp file and install
            var tempPath = Path.Combine(Path.GetTempPath(), $"{extensionId}.crx");
            await File.WriteAllBytesAsync(tempPath, crxBytes);

            Extension? extension;

            // Check if it's a ZIP file (no CRX header)
            if (crxBytes[0] == 0x50 && crxBytes[1] == 0x4B)
            {
                extension = await InstallFromZipAsync(tempPath, webView);
            }
            else
            {
                extension = await InstallFromCrxAsync(tempPath, webView);
            }

            // Clean up temp file
            try { File.Delete(tempPath); } catch { }

            if (extension != null)
            {
                extension.Id = extensionId;
                SaveExtensionMetadata();
            }

            return extension;
        }
        catch (Exception ex)
        {
            ExtensionError?.Invoke(this, $"Failed to install from Edge Add-ons: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Removes an installed extension
    /// </summary>
    public async Task<bool> RemoveExtensionAsync(string extensionId, CoreWebView2 webView)
    {
        try
        {
            var extension = _installedExtensions.FirstOrDefault(e => e.Id == extensionId);
            if (extension == null) return false;
            
            // Remove from WebView2
            var browserExtensions = await webView.Profile.GetBrowserExtensionsAsync();
            var browserExt = browserExtensions.FirstOrDefault(e => e.Id == extensionId);
            if (browserExt != null)
            {
                await browserExt.RemoveAsync();
            }
            
            // Remove from disk
            if (Directory.Exists(extension.Path))
            {
                Directory.Delete(extension.Path, true);
            }
            
            _installedExtensions.Remove(extension);
            SaveExtensionMetadata();
            ExtensionRemoved?.Invoke(this, extensionId);
            
            return true;
        }
        catch (Exception ex)
        {
            ExtensionError?.Invoke(this, $"Failed to remove extension: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Enables or disables an extension
    /// </summary>
    public async Task<bool> SetExtensionEnabledAsync(string extensionId, bool enabled, CoreWebView2 webView)
    {
        try
        {
            var browserExtensions = await webView.Profile.GetBrowserExtensionsAsync();
            var browserExt = browserExtensions.FirstOrDefault(e => e.Id == extensionId);
            
            if (browserExt != null)
            {
                await browserExt.EnableAsync(enabled);
                
                var extension = _installedExtensions.FirstOrDefault(e => e.Id == extensionId);
                if (extension != null)
                {
                    extension.IsEnabled = enabled;
                    SaveExtensionMetadata();
                }
                
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            ExtensionError?.Invoke(this, $"Failed to update extension state: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Gets a list of currently loaded browser extensions
    /// </summary>
    public async Task<List<Extension>> GetLoadedExtensionsAsync(CoreWebView2 webView)
    {
        var result = new List<Extension>();

        try
        {
            var browserExtensions = await webView.Profile.GetBrowserExtensionsAsync();

            foreach (var ext in browserExtensions)
            {
                // Find metadata from installed extensions
                var installed = _installedExtensions.FirstOrDefault(e => e.Id == ext.Id);

                result.Add(new Extension
                {
                    Id = ext.Id,
                    Name = installed?.Name ?? ext.Name,
                    Version = installed?.Version ?? "1.0",
                    Description = installed?.Description ?? "",
                    Path = installed?.Path ?? "",
                    IsEnabled = ext.IsEnabled,
                    IconPath = installed?.IconPath
                });
            }

            // Also add any installed extensions that might not be loaded yet
            foreach (var installed in _installedExtensions)
            {
                if (!result.Any(e => e.Id == installed.Id))
                {
                    result.Add(new Extension
                    {
                        Id = installed.Id,
                        Name = installed.Name,
                        Version = installed.Version,
                        Description = installed.Description,
                        Path = installed.Path,
                        IsEnabled = false,
                        Error = "Not loaded - restart browser to load"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            ExtensionError?.Invoke(this, $"Failed to get loaded extensions: {ex.Message}");

            // Return installed extensions even if browser API fails
            result.AddRange(_installedExtensions);
        }

        return result;
    }

    /// <summary>
    /// Reloads extension metadata from disk
    /// </summary>
    public void ReloadMetadata()
    {
        LoadExtensionMetadata();
    }
    
    private async Task<Extension?> InstallFromCrxAsync(string crxPath, CoreWebView2 webView)
    {
        // CRX files need to be unpacked first
        var bytes = await File.ReadAllBytesAsync(crxPath);
        
        // Parse CRX header to find ZIP start
        // CRX3 format: "Cr24" magic, version (4 bytes), header length (4 bytes), header, zip
        var zipStart = FindZipStart(bytes);
        if (zipStart < 0)
        {
            throw new InvalidOperationException("Invalid CRX file format");
        }
        
        // Extract extension ID from the file
        var extensionId = Path.GetFileNameWithoutExtension(crxPath);
        var extractPath = Path.Combine(_extensionsPath, extensionId);
        
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }
        
        // Extract the ZIP portion
        using var zipStream = new MemoryStream(bytes, zipStart, bytes.Length - zipStart);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        archive.ExtractToDirectory(extractPath);
        
        // Parse manifest.json
        var manifestPath = Path.Combine(extractPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Extension manifest not found");
        }
        
        var manifest = JsonSerializer.Deserialize<ExtensionManifest>(
            await File.ReadAllTextAsync(manifestPath));
        
        // Add to WebView2
        await webView.Profile.AddBrowserExtensionAsync(extractPath);
        
        // Get the actual extension ID from WebView2
        var browserExtensions = await webView.Profile.GetBrowserExtensionsAsync();
        var browserExt = browserExtensions.LastOrDefault();
        
        return new Extension
        {
            Id = browserExt?.Id ?? extensionId,
            Name = manifest?.Name ?? extensionId,
            Version = manifest?.Version ?? "1.0",
            Description = manifest?.Description ?? "",
            Path = extractPath,
            IsEnabled = true
        };
    }
    
    private async Task<Extension?> InstallFromZipAsync(string zipPath, CoreWebView2 webView)
    {
        var extensionId = Path.GetFileNameWithoutExtension(zipPath);
        var extractPath = Path.Combine(_extensionsPath, extensionId);
        
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }
        
        ZipFile.ExtractToDirectory(zipPath, extractPath);
        
        return await InstallFromFolderAsync(extractPath, webView);
    }
    
    private async Task<Extension?> InstallFromFolderAsync(string folderPath, CoreWebView2 webView)
    {
        var manifestPath = Path.Combine(folderPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Extension manifest not found");
        }
        
        var manifest = JsonSerializer.Deserialize<ExtensionManifest>(
            await File.ReadAllTextAsync(manifestPath));
        
        // Copy to extensions folder if not already there
        var extensionId = Path.GetFileName(folderPath);
        var targetPath = Path.Combine(_extensionsPath, extensionId);
        
        if (folderPath != targetPath)
        {
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }
            CopyDirectory(folderPath, targetPath);
        }
        else
        {
            targetPath = folderPath;
        }
        
        // Add to WebView2
        await webView.Profile.AddBrowserExtensionAsync(targetPath);
        
        // Get the actual extension ID from WebView2
        var browserExtensions = await webView.Profile.GetBrowserExtensionsAsync();
        var browserExt = browserExtensions.LastOrDefault();
        
        return new Extension
        {
            Id = browserExt?.Id ?? extensionId,
            Name = manifest?.Name ?? extensionId,
            Version = manifest?.Version ?? "1.0",
            Description = manifest?.Description ?? "",
            Path = targetPath,
            IsEnabled = true
        };
    }
    
    private int FindZipStart(byte[] data)
    {
        // Look for PK signature (ZIP start)
        for (int i = 0; i < Math.Min(data.Length - 4, 10000); i++)
        {
            if (data[i] == 0x50 && data[i + 1] == 0x4B && 
                data[i + 2] == 0x03 && data[i + 3] == 0x04)
            {
                return i;
            }
        }
        return -1;
    }
    
    private void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        }
        
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }
    
    private void LoadExtensionMetadata()
    {
        _installedExtensions.Clear();
        
        var metadataPath = Path.Combine(_extensionsPath, "extensions.json");
        if (File.Exists(metadataPath))
        {
            try
            {
                var json = File.ReadAllText(metadataPath);
                var extensions = JsonSerializer.Deserialize<List<Extension>>(json);
                if (extensions != null)
                {
                    foreach (var ext in extensions)
                    {
                        if (Directory.Exists(ext.Path))
                        {
                            _installedExtensions.Add(ext);
                        }
                    }
                }
            }
            catch { }
        }
        else
        {
            // Scan extensions folder for existing extensions
            foreach (var dir in Directory.GetDirectories(_extensionsPath))
            {
                var manifestPath = Path.Combine(dir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var manifest = JsonSerializer.Deserialize<ExtensionManifest>(
                            File.ReadAllText(manifestPath));
                        
                        _installedExtensions.Add(new Extension
                        {
                            Id = Path.GetFileName(dir),
                            Name = manifest?.Name ?? Path.GetFileName(dir),
                            Version = manifest?.Version ?? "1.0",
                            Description = manifest?.Description ?? "",
                            Path = dir,
                            IsEnabled = true
                        });
                    }
                    catch { }
                }
            }
            
            if (_installedExtensions.Count > 0)
            {
                SaveExtensionMetadata();
            }
        }
    }
    
    private void SaveExtensionMetadata()
    {
        var metadataPath = Path.Combine(_extensionsPath, "extensions.json");
        var json = JsonSerializer.Serialize(_installedExtensions, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        File.WriteAllText(metadataPath, json);
    }
}

/// <summary>
/// Represents an installed browser extension
/// </summary>
public class Extension
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public string Description { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string? Error { get; set; }
    public string? IconPath { get; set; }
}

/// <summary>
/// Chrome extension manifest structure
/// </summary>
public class ExtensionManifest
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public int ManifestVersion { get; set; }
    public Dictionary<string, string>? Icons { get; set; }
}
