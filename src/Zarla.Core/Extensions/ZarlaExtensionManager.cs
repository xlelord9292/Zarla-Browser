using System.Text.Json;

namespace Zarla.Core.Extensions;

/// <summary>
/// Manages Zarla's custom extension system
/// </summary>
public class ZarlaExtensionManager
{
    private readonly string _extensionsPath;
    private readonly string _templatesPath;
    private readonly List<ZarlaExtension> _extensions = new();
    private readonly List<ZarlaExtension> _builtInTemplates = new();

    public event EventHandler<ZarlaExtension>? ExtensionCreated;
    public event EventHandler<ZarlaExtension>? ExtensionUpdated;
    public event EventHandler<string>? ExtensionDeleted;

    public IReadOnlyList<ZarlaExtension> Extensions => _extensions.AsReadOnly();
    public IReadOnlyList<ZarlaExtension> BuiltInTemplates => _builtInTemplates.AsReadOnly();

    public ZarlaExtensionManager(string userDataFolder)
    {
        _extensionsPath = Path.Combine(userDataFolder, "ZarlaExtensions");
        _templatesPath = Path.Combine(userDataFolder, "ZarlaTemplates");

        Directory.CreateDirectory(_extensionsPath);
        Directory.CreateDirectory(_templatesPath);

        LoadBuiltInTemplates();
        LoadExtensions();
    }

    /// <summary>
    /// Gets the JavaScript to inject for a specific URL
    /// </summary>
    public string GetScriptForUrl(string url)
    {
        var scripts = _extensions
            .Where(e => e.IsEnabled && e.ShouldRunOn(url))
            .Select(e => e.GenerateScript())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        return scripts.Count > 0 ? string.Join("\n", scripts) : "";
    }

    /// <summary>
    /// Creates a new extension
    /// </summary>
    public ZarlaExtension CreateExtension(string name, string description = "")
    {
        var extension = new ZarlaExtension
        {
            Name = name,
            Description = description
        };

        _extensions.Add(extension);
        SaveExtension(extension);
        ExtensionCreated?.Invoke(this, extension);

        return extension;
    }

    /// <summary>
    /// Creates an extension from a template
    /// </summary>
    public ZarlaExtension CreateFromTemplate(ZarlaExtension template)
    {
        var json = JsonSerializer.Serialize(template);
        var extension = JsonSerializer.Deserialize<ZarlaExtension>(json)!;

        extension.Id = Guid.NewGuid().ToString("N")[..8];
        extension.Name = template.Name + " (Copy)";
        extension.IsBuiltIn = false;
        extension.IsCommunity = false;
        extension.CreatedAt = DateTime.UtcNow;
        extension.UpdatedAt = DateTime.UtcNow;

        // Generate new IDs for blocks
        foreach (var block in extension.Blocks)
        {
            block.Id = Guid.NewGuid().ToString("N")[..8];
        }

        _extensions.Add(extension);
        SaveExtension(extension);
        ExtensionCreated?.Invoke(this, extension);

        return extension;
    }

    /// <summary>
    /// Updates an existing extension
    /// </summary>
    public void UpdateExtension(ZarlaExtension extension)
    {
        extension.UpdatedAt = DateTime.UtcNow;
        SaveExtension(extension);
        ExtensionUpdated?.Invoke(this, extension);
    }

    /// <summary>
    /// Deletes an extension
    /// </summary>
    public bool DeleteExtension(string extensionId)
    {
        var extension = _extensions.FirstOrDefault(e => e.Id == extensionId);
        if (extension == null || extension.IsBuiltIn) return false;

        _extensions.Remove(extension);

        var filePath = Path.Combine(_extensionsPath, $"{extensionId}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        ExtensionDeleted?.Invoke(this, extensionId);
        return true;
    }

    /// <summary>
    /// Gets an extension by ID
    /// </summary>
    public ZarlaExtension? GetExtension(string extensionId)
    {
        return _extensions.FirstOrDefault(e => e.Id == extensionId)
            ?? _builtInTemplates.FirstOrDefault(t => t.Id == extensionId);
    }

    /// <summary>
    /// Exports an extension as JSON
    /// </summary>
    public string ExportExtension(ZarlaExtension extension)
    {
        return JsonSerializer.Serialize(extension, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Imports an extension from JSON
    /// </summary>
    public ZarlaExtension? ImportExtension(string json)
    {
        try
        {
            var extension = JsonSerializer.Deserialize<ZarlaExtension>(json);
            if (extension == null) return null;

            // Generate new ID to avoid conflicts
            extension.Id = Guid.NewGuid().ToString("N")[..8];
            extension.IsBuiltIn = false;
            extension.IsCommunity = true;
            extension.CreatedAt = DateTime.UtcNow;
            extension.UpdatedAt = DateTime.UtcNow;

            _extensions.Add(extension);
            SaveExtension(extension);
            ExtensionCreated?.Invoke(this, extension);

            return extension;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Toggles an extension on/off
    /// </summary>
    public void SetExtensionEnabled(string extensionId, bool enabled)
    {
        var extension = _extensions.FirstOrDefault(e => e.Id == extensionId);
        if (extension != null)
        {
            extension.IsEnabled = enabled;
            SaveExtension(extension);
            ExtensionUpdated?.Invoke(this, extension);
        }
    }

    private void SaveExtension(ZarlaExtension extension)
    {
        var filePath = Path.Combine(_extensionsPath, $"{extension.Id}.json");
        var json = JsonSerializer.Serialize(extension, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    private void LoadExtensions()
    {
        _extensions.Clear();

        foreach (var file in Directory.GetFiles(_extensionsPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var extension = JsonSerializer.Deserialize<ZarlaExtension>(json);
                if (extension != null)
                {
                    _extensions.Add(extension);
                }
            }
            catch
            {
                // Skip invalid extensions
            }
        }
    }

    private void LoadBuiltInTemplates()
    {
        _builtInTemplates.Clear();

        // Cookie Consent Auto-Dismiss
        _builtInTemplates.Add(new ZarlaExtension
        {
            Id = "builtin-cookies",
            Name = "Cookie Consent Blocker",
            Description = "Automatically dismisses annoying cookie consent popups",
            Icon = "🍪",
            Author = "Zarla",
            IsBuiltIn = true,
            Blocks = new()
            {
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.CookieConsent,
                    Label = "Dismiss cookie banners"
                }
            }
        });

        // Dark Mode Everywhere
        _builtInTemplates.Add(new ZarlaExtension
        {
            Id = "builtin-darkmode",
            Name = "Dark Mode Everywhere",
            Description = "Forces dark mode on any website",
            Icon = "🌙",
            Author = "Zarla",
            IsBuiltIn = true,
            Blocks = new()
            {
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.DarkMode,
                    Label = "Apply dark mode",
                    Config = new()
                    {
                        { "brightness", "0.9" },
                        { "contrast", "1.1" }
                    }
                }
            }
        });

        // Reading Mode
        _builtInTemplates.Add(new ZarlaExtension
        {
            Id = "builtin-reading",
            Name = "Reading Mode",
            Description = "Clean, distraction-free reading experience",
            Icon = "📖",
            Author = "Zarla",
            IsBuiltIn = true,
            Blocks = new()
            {
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.ReadingMode,
                    Label = "Enable reading mode",
                    Config = new()
                    {
                        { "maxWidth", "750px" },
                        { "fontSize", "18px" },
                        { "lineHeight", "1.8" }
                    }
                }
            }
        });

        // YouTube Enhancer
        _builtInTemplates.Add(new ZarlaExtension
        {
            Id = "builtin-youtube",
            Name = "YouTube Enhancer",
            Description = "Cleaner YouTube experience - hides distractions",
            Icon = "📺",
            Author = "Zarla",
            IsBuiltIn = true,
            ActiveOnSites = new() { "*.youtube.com" },
            Blocks = new()
            {
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.HideElement,
                    Label = "Hide homepage shorts",
                    Config = new() { { "selector", "ytd-rich-shelf-renderer[is-shorts]" } }
                },
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.HideElement,
                    Label = "Hide sidebar shorts",
                    Config = new() { { "selector", "ytd-reel-shelf-renderer" } }
                },
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.HideElement,
                    Label = "Hide comments",
                    Config = new() { { "selector", "#comments" } },
                    IsEnabled = false // Disabled by default
                }
            }
        });

        // Twitter/X Cleaner
        _builtInTemplates.Add(new ZarlaExtension
        {
            Id = "builtin-twitter",
            Name = "Twitter/X Cleaner",
            Description = "Cleaner Twitter/X experience",
            Icon = "🐦",
            Author = "Zarla",
            IsBuiltIn = true,
            ActiveOnSites = new() { "*.twitter.com", "*.x.com" },
            Blocks = new()
            {
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.HideElement,
                    Label = "Hide trending sidebar",
                    Config = new() { { "selector", "[data-testid='sidebarColumn'] [data-testid='trend']" } }
                },
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.HideElement,
                    Label = "Hide who to follow",
                    Config = new() { { "selector", "[data-testid='UserCell']" } }
                }
            }
        });

        // Reddit Enhancer
        _builtInTemplates.Add(new ZarlaExtension
        {
            Id = "builtin-reddit",
            Name = "Reddit Enhancer",
            Description = "Better Reddit browsing",
            Icon = "👽",
            Author = "Zarla",
            IsBuiltIn = true,
            ActiveOnSites = new() { "*.reddit.com" },
            Blocks = new()
            {
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.HideElement,
                    Label = "Hide promoted posts",
                    Config = new() { { "selector", "[data-testid='promoted-post'], .promotedlink" } }
                },
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.AutoClick,
                    Label = "Auto-expand NSFW content warnings",
                    Config = new()
                    {
                        { "selector", "[data-testid='post-nsfw-blur-button']" },
                        { "delay", "500" }
                    },
                    IsEnabled = false
                }
            }
        });

        // Auto-Expand Images
        _builtInTemplates.Add(new ZarlaExtension
        {
            Id = "builtin-images",
            Name = "Auto-Expand Images",
            Description = "Automatically expand images to full size",
            Icon = "🖼️",
            Author = "Zarla",
            IsBuiltIn = true,
            Blocks = new()
            {
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.AddCSS,
                    Label = "Expand images on hover",
                    Config = new()
                    {
                        { "css", @"
img:hover {
    transform: scale(1.5);
    z-index: 9999;
    position: relative;
    transition: transform 0.3s ease;
    box-shadow: 0 10px 30px rgba(0,0,0,0.5);
}
" }
                    }
                }
            }
        });

        // Focus Mode
        _builtInTemplates.Add(new ZarlaExtension
        {
            Id = "builtin-focus",
            Name = "Focus Mode",
            Description = "Hide distracting elements for better focus",
            Icon = "🎯",
            Author = "Zarla",
            IsBuiltIn = true,
            Blocks = new()
            {
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.HideElement,
                    Label = "Hide chat widgets",
                    Config = new() { { "selector", "[class*='chat-widget'], [id*='chat-widget'], .intercom-launcher, #hubspot-messages-iframe-container" } }
                },
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.HideElement,
                    Label = "Hide notification badges",
                    Config = new() { { "selector", "[class*='notification-badge'], [class*='badge-count']" } }
                },
                new ExtensionBlock
                {
                    Type = ExtensionBlockType.HideElement,
                    Label = "Hide floating elements",
                    Config = new() { { "selector", "[class*='floating'], [class*='sticky-']:not(header):not(nav)" } }
                }
            }
        });
    }
}
