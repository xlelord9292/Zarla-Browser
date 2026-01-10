using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace Zarla.Updater;

public partial class UpdateWindow : Window
{
    private readonly string? _installerPath;
    private readonly int? _browserPid;
    private string? _newVersion;

    public UpdateWindow()
    {
        InitializeComponent();
        _installerPath = App.InstallerPath;
        _browserPid = App.BrowserProcessId;
        
        // Read pending update info to get the new version
        LoadPendingUpdateInfo();

        Loaded += async (s, e) => await RunUpdateAsync();
    }

    private void LoadPendingUpdateInfo()
    {
        try
        {
            var userDataFolder = GetUserDataFolder();
            var pendingPath = Path.Combine(userDataFolder, "pending-update.json");
            
            if (File.Exists(pendingPath))
            {
                var json = File.ReadAllText(pendingPath);
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                
                if (data.TryGetProperty("NewVersion", out var versionElement))
                {
                    _newVersion = versionElement.GetString();
                }
            }
        }
        catch { }
    }

    private async Task RunUpdateAsync()
    {
        try
        {
            // Validate installer path
            if (string.IsNullOrEmpty(_installerPath) || !File.Exists(_installerPath))
            {
                ShowError("Update file not found. Please download the update manually.");
                return;
            }

            // Step 1: Wait for browser to close
            UpdateStatus("Waiting for Zarla Browser to close...", 10);

            if (_browserPid.HasValue)
            {
                await WaitForProcessToExitAsync(_browserPid.Value, TimeSpan.FromSeconds(30));
            }
            else
            {
                // Wait for any Zarla process
                await WaitForZarlaToCloseAsync(TimeSpan.FromSeconds(30));
            }

            UpdateStatus("Browser closed. Starting update...", 20);
            await Task.Delay(500);

            // Step 2: Backup user data (optional, data is preserved by NSIS installer)
            UpdateStatus("Preparing installation...", 30);
            await Task.Delay(300);

            // Step 3: Run the installer
            UpdateStatus("Installing update...", 40);
            SubStatusText.Text = Path.GetFileName(_installerPath);

            var installerProcess = Process.Start(new ProcessStartInfo
            {
                FileName = _installerPath,
                Arguments = "/S", // NSIS silent install
                UseShellExecute = true,
                Verb = "runas" // Run as admin if needed
            });

            if (installerProcess == null)
            {
                ShowError("Failed to start installer.");
                return;
            }

            // Monitor installer progress
            var progress = 40;
            while (!installerProcess.HasExited)
            {
                await Task.Delay(500);
                progress = Math.Min(progress + 5, 90);
                UpdateStatus("Installing update...", progress);
            }

            if (installerProcess.ExitCode != 0)
            {
                ShowError($"Installation failed with code {installerProcess.ExitCode}");
                return;
            }

            // Update completed successfully - update the config version
            if (!string.IsNullOrEmpty(_newVersion))
            {
                UpdateConfigVersion(_newVersion);
            }

            UpdateStatus("Update complete!", 100);
            ProgressBar.Foreground = (Brush)FindResource("SuccessBrush");
            SubStatusText.Text = "Starting Zarla Browser...";
            await Task.Delay(1000);

            // Step 4: Launch the updated browser
            var zarlaPath = GetZarlaPath();
            if (!string.IsNullOrEmpty(zarlaPath) && File.Exists(zarlaPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = zarlaPath,
                    UseShellExecute = true
                });
            }

            // Clean up the installer file
            try
            {
                if (File.Exists(_installerPath))
                    File.Delete(_installerPath);
            }
            catch { }

            await Task.Delay(500);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            ShowError($"Update failed: {ex.Message}");
        }
    }

    private void UpdateStatus(string status, int progress)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
            ProgressBar.Value = progress;
        });
    }

    private void ShowError(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = "Update Failed";
            StatusText.Foreground = (Brush)FindResource("ErrorBrush");
            SubStatusText.Text = message;
            ProgressBar.Foreground = (Brush)FindResource("ErrorBrush");
            ProgressBar.Value = 100;
        });

        // Keep window open to show error
        Task.Delay(5000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    message + "\n\nWould you like to open the downloads page to update manually?",
                    "Update Failed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/xlelord9292/Zarla-Browser/releases",
                        UseShellExecute = true
                    });
                }

                Application.Current.Shutdown();
            });
        });
    }

    private async Task WaitForProcessToExitAsync(int pid, TimeSpan timeout)
    {
        var endTime = DateTime.Now + timeout;

        while (DateTime.Now < endTime)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                if (process.HasExited)
                    return;

                await Task.Delay(500);
            }
            catch (ArgumentException)
            {
                // Process doesn't exist, it has exited
                return;
            }
        }
    }

    private async Task WaitForZarlaToCloseAsync(TimeSpan timeout)
    {
        var endTime = DateTime.Now + timeout;

        while (DateTime.Now < endTime)
        {
            var zarlaProcesses = Process.GetProcessesByName("Zarla");
            if (zarlaProcesses.Length == 0)
                return;

            await Task.Delay(500);
        }
    }

    private string? GetZarlaPath()
    {
        // Check common installation locations
        var paths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zarla", "Zarla.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zarla", "Zarla.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Zarla", "Zarla.exe"),
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string GetUserDataFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zarla");
    }

    private void UpdateConfigVersion(string newVersion)
    {
        try
        {
            var userDataFolder = GetUserDataFolder();
            
            // Save completed update marker
            var completedPath = Path.Combine(userDataFolder, "completed-update.json");
            var markerJson = JsonSerializer.Serialize(new
            {
                newVersion,
                completedAt = DateTime.Now
            });
            File.WriteAllText(completedPath, markerJson);

            // Directly update the config file with new version
            var configPath = Path.Combine(userDataFolder, "zarla-config.json");
            if (File.Exists(configPath))
            {
                var configJson = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson);
                
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

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(configPath, JsonSerializer.Serialize(updatedConfig, options));
                }
            }
            else
            {
                // Config doesn't exist in user data, create a minimal one
                Directory.CreateDirectory(userDataFolder);
                var newConfig = new Dictionary<string, object>
                {
                    ["browserName"] = "Zarla",
                    ["browserDisplayName"] = "Zarla Browser",
                    ["version"] = newVersion,
                    ["buildNumber"] = 1
                };
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(configPath, JsonSerializer.Serialize(newConfig, options));
            }

            // Clean up pending update file
            var pendingPath = Path.Combine(userDataFolder, "pending-update.json");
            if (File.Exists(pendingPath))
            {
                try { File.Delete(pendingPath); } catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating config version: {ex.Message}");
        }
    }
}
