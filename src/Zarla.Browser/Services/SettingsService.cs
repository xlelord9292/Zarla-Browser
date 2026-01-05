using System.IO;
using System.Text.Json;
using Zarla.Core.Data.Models;

namespace Zarla.Browser.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private BrowserSettings _settings;

    public BrowserSettings CurrentSettings => _settings;

    public event EventHandler? SettingsChanged;

    public SettingsService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Zarla", "settings.json");

        _settings = Load();
    }

    private BrowserSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<BrowserSettings>(json) ?? new BrowserSettings();
            }
        }
        catch
        {
            // Corrupted settings, use defaults
        }

        return new BrowserSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Failed to save
        }
    }

    public void Update(Action<BrowserSettings> updateAction)
    {
        updateAction(_settings);
        Save();
    }

    public void Reset()
    {
        _settings = new BrowserSettings();
        Save();
    }
}
