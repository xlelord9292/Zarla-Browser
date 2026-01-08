using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zarla.Core.Extensions;

/// <summary>
/// Represents a Zarla Extension - a simple, block-based extension format
/// </summary>
public class ZarlaExtension
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "New Extension";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = "Anonymous";
    public string Icon { get; set; } = "🧩"; // Emoji icon
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsEnabled { get; set; } = true;
    public bool IsBuiltIn { get; set; } = false;
    public bool IsCommunity { get; set; } = false;

    /// <summary>
    /// The blocks that make up this extension
    /// </summary>
    public List<ExtensionBlock> Blocks { get; set; } = new();

    /// <summary>
    /// Sites where this extension is active (empty = all sites)
    /// </summary>
    public List<string> ActiveOnSites { get; set; } = new();

    /// <summary>
    /// Sites where this extension is disabled
    /// </summary>
    public List<string> DisabledOnSites { get; set; } = new();

    /// <summary>
    /// Generates the JavaScript code for this extension
    /// </summary>
    public string GenerateScript()
    {
        if (Blocks.Count == 0) return "";

        var scripts = new List<string>();

        foreach (var block in Blocks.Where(b => b.IsEnabled))
        {
            var script = block.GenerateCode();
            if (!string.IsNullOrEmpty(script))
            {
                scripts.Add(script);
            }
        }

        if (scripts.Count == 0) return "";

        // Wrap in IIFE with extension marker
        return $@"
(function() {{
    'use strict';
    if (window.__zarlaExt_{Id}) return;
    window.__zarlaExt_{Id} = true;

    // Extension: {Name}
    // Version: {Version}

    {string.Join("\n\n    ", scripts)}

    console.log('[Zarla Extension] {Name} loaded');
}})();
";
    }

    /// <summary>
    /// Checks if this extension should run on the given URL
    /// </summary>
    public bool ShouldRunOn(string url)
    {
        if (!IsEnabled) return false;

        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();

            // Check disabled sites first
            if (DisabledOnSites.Any(s => MatchesSite(host, s)))
                return false;

            // If no active sites specified, run on all sites
            if (ActiveOnSites.Count == 0)
                return true;

            // Check if host matches any active site
            return ActiveOnSites.Any(s => MatchesSite(host, s));
        }
        catch
        {
            return false;
        }
    }

    private bool MatchesSite(string host, string pattern)
    {
        pattern = pattern.ToLowerInvariant().Trim();

        if (pattern.StartsWith("*."))
        {
            // Wildcard subdomain match
            var domain = pattern[2..];
            return host.EndsWith(domain) || host == domain;
        }

        return host == pattern || host.EndsWith("." + pattern);
    }
}

/// <summary>
/// A single action block in a Zarla Extension
/// </summary>
public class ExtensionBlock
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public ExtensionBlockType Type { get; set; }
    public string Label { get; set; } = "";
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Block-specific configuration
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>
    /// Generates JavaScript code for this block
    /// </summary>
    public string GenerateCode()
    {
        return Type switch
        {
            ExtensionBlockType.HideElement => GenerateHideElement(),
            ExtensionBlockType.RemoveElement => GenerateRemoveElement(),
            ExtensionBlockType.ChangeText => GenerateChangeText(),
            ExtensionBlockType.ChangeStyle => GenerateChangeStyle(),
            ExtensionBlockType.AddCSS => GenerateAddCSS(),
            ExtensionBlockType.InjectScript => GenerateInjectScript(),
            ExtensionBlockType.BlockRequest => GenerateBlockRequest(),
            ExtensionBlockType.RedirectUrl => GenerateRedirectUrl(),
            ExtensionBlockType.AutoClick => GenerateAutoClick(),
            ExtensionBlockType.AutoFill => GenerateAutoFill(),
            ExtensionBlockType.KeyboardShortcut => GenerateKeyboardShortcut(),
            ExtensionBlockType.DarkMode => GenerateDarkMode(),
            ExtensionBlockType.ReadingMode => GenerateReadingMode(),
            ExtensionBlockType.CookieConsent => GenerateCookieConsent(),
            ExtensionBlockType.CustomCode => GenerateCustomCode(),
            _ => ""
        };
    }

    private string GetConfig(string key, string defaultValue = "")
        => Config.TryGetValue(key, out var value) ? value : defaultValue;

    private string GenerateHideElement()
    {
        var selector = GetConfig("selector");
        if (string.IsNullOrEmpty(selector)) return "";

        return $@"
    // Hide: {Label}
    (function() {{
        const style = document.createElement('style');
        style.textContent = `{EscapeSelector(selector)} {{ display: none !important; visibility: hidden !important; }}`;
        document.head.appendChild(style);
    }})();";
    }

    private string GenerateRemoveElement()
    {
        var selector = GetConfig("selector");
        if (string.IsNullOrEmpty(selector)) return "";

        return $@"
    // Remove: {Label}
    document.querySelectorAll('{EscapeSelector(selector)}').forEach(el => el.remove());
    new MutationObserver((mutations) => {{
        document.querySelectorAll('{EscapeSelector(selector)}').forEach(el => el.remove());
    }}).observe(document.body, {{ childList: true, subtree: true }});";
    }

    private string GenerateChangeText()
    {
        var selector = GetConfig("selector");
        var oldText = GetConfig("oldText");
        var newText = GetConfig("newText");

        if (string.IsNullOrEmpty(selector) && string.IsNullOrEmpty(oldText)) return "";

        if (!string.IsNullOrEmpty(selector))
        {
            return $@"
    // Change text: {Label}
    document.querySelectorAll('{EscapeSelector(selector)}').forEach(el => {{
        el.textContent = '{EscapeJs(newText)}';
    }});";
        }
        else
        {
            return $@"
    // Replace text: {Label}
    document.body.innerHTML = document.body.innerHTML.replace(/{EscapeRegex(oldText)}/gi, '{EscapeJs(newText)}');";
        }
    }

    private string GenerateChangeStyle()
    {
        var selector = GetConfig("selector");
        var property = GetConfig("property");
        var value = GetConfig("value");

        if (string.IsNullOrEmpty(selector) || string.IsNullOrEmpty(property)) return "";

        return $@"
    // Change style: {Label}
    document.querySelectorAll('{EscapeSelector(selector)}').forEach(el => {{
        el.style.setProperty('{EscapeJs(property)}', '{EscapeJs(value)}', 'important');
    }});";
    }

    private string GenerateAddCSS()
    {
        var css = GetConfig("css");
        if (string.IsNullOrEmpty(css)) return "";

        return $@"
    // Add CSS: {Label}
    (function() {{
        const style = document.createElement('style');
        style.textContent = `{css.Replace("`", "\\`")}`;
        document.head.appendChild(style);
    }})();";
    }

    private string GenerateInjectScript()
    {
        var script = GetConfig("script");
        if (string.IsNullOrEmpty(script)) return "";

        return $@"
    // Custom script: {Label}
    try {{
        {script}
    }} catch(e) {{
        console.error('[Zarla Extension] Script error:', e);
    }}";
    }

    private string GenerateBlockRequest()
    {
        // This is handled at the C# level, not JavaScript
        return "";
    }

    private string GenerateRedirectUrl()
    {
        var fromPattern = GetConfig("fromPattern");
        var toUrl = GetConfig("toUrl");

        if (string.IsNullOrEmpty(fromPattern) || string.IsNullOrEmpty(toUrl)) return "";

        return $@"
    // Redirect: {Label}
    if (window.location.href.match(/{EscapeRegex(fromPattern)}/i)) {{
        window.location.replace('{EscapeJs(toUrl)}');
    }}";
    }

    private string GenerateAutoClick()
    {
        var selector = GetConfig("selector");
        var delay = GetConfig("delay", "1000");

        if (string.IsNullOrEmpty(selector)) return "";

        return $@"
    // Auto-click: {Label}
    setTimeout(() => {{
        const el = document.querySelector('{EscapeSelector(selector)}');
        if (el) el.click();
    }}, {delay});";
    }

    private string GenerateAutoFill()
    {
        var selector = GetConfig("selector");
        var value = GetConfig("value");

        if (string.IsNullOrEmpty(selector)) return "";

        return $@"
    // Auto-fill: {Label}
    const el = document.querySelector('{EscapeSelector(selector)}');
    if (el) {{
        el.value = '{EscapeJs(value)}';
        el.dispatchEvent(new Event('input', {{ bubbles: true }}));
    }}";
    }

    private string GenerateKeyboardShortcut()
    {
        var key = GetConfig("key");
        var action = GetConfig("action");
        var ctrl = GetConfig("ctrl") == "true";
        var alt = GetConfig("alt") == "true";
        var shift = GetConfig("shift") == "true";

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(action)) return "";

        var conditions = new List<string> { $"e.key === '{EscapeJs(key)}'" };
        if (ctrl) conditions.Add("e.ctrlKey");
        if (alt) conditions.Add("e.altKey");
        if (shift) conditions.Add("e.shiftKey");

        return $@"
    // Keyboard shortcut: {Label}
    document.addEventListener('keydown', (e) => {{
        if ({string.Join(" && ", conditions)}) {{
            e.preventDefault();
            {action}
        }}
    }});";
    }

    private string GenerateDarkMode()
    {
        var brightness = GetConfig("brightness", "0.9");
        var contrast = GetConfig("contrast", "1.1");

        return $@"
    // Dark mode: {Label}
    (function() {{
        const style = document.createElement('style');
        style.textContent = `
            html {{
                filter: invert(1) hue-rotate(180deg);
            }}
            img, video, picture, canvas, iframe, svg {{
                filter: invert(1) hue-rotate(180deg);
            }}
            html {{
                filter: invert(1) hue-rotate(180deg) brightness({brightness}) contrast({contrast});
            }}
        `;
        document.head.appendChild(style);
    }})();";
    }

    private string GenerateReadingMode()
    {
        var maxWidth = GetConfig("maxWidth", "800px");
        var fontSize = GetConfig("fontSize", "18px");
        var lineHeight = GetConfig("lineHeight", "1.8");

        return $@"
    // Reading mode: {Label}
    (function() {{
        const style = document.createElement('style');
        style.textContent = `
            body {{
                max-width: {maxWidth} !important;
                margin: 0 auto !important;
                padding: 20px !important;
                font-size: {fontSize} !important;
                line-height: {lineHeight} !important;
            }}
            * {{
                max-width: 100% !important;
            }}
            [class*='sidebar'], [class*='ad'], [id*='sidebar'], [id*='ad'],
            aside, nav:not(:first-of-type), footer {{
                display: none !important;
            }}
        `;
        document.head.appendChild(style);
    }})();";
    }

    private string GenerateCookieConsent()
    {
        return @"
    // Auto-dismiss cookie consent: " + Label + @"
    (function() {
        const selectors = [
            '[class*=""cookie""] button[class*=""accept""]',
            '[class*=""cookie""] button[class*=""agree""]',
            '[class*=""consent""] button[class*=""accept""]',
            '[id*=""cookie""] button',
            '.cookie-banner button',
            '#cookie-notice button',
            '[aria-label*=""cookie""] button',
            'button[class*=""accept-cookies""]',
            'button[class*=""accept-all""]'
        ];

        function dismissCookies() {
            for (const selector of selectors) {
                const btn = document.querySelector(selector);
                if (btn && btn.offsetParent) {
                    btn.click();
                    return true;
                }
            }
            return false;
        }

        if (!dismissCookies()) {
            setTimeout(dismissCookies, 1000);
            setTimeout(dismissCookies, 3000);
        }
    })();";
    }

    private string GenerateCustomCode()
    {
        var code = GetConfig("code");
        return string.IsNullOrEmpty(code) ? "" : code;
    }

    private static string EscapeJs(string s)
        => s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");

    private static string EscapeSelector(string s)
        => s.Replace("'", "\\'");

    private static string EscapeRegex(string s)
        => System.Text.RegularExpressions.Regex.Escape(s);
}

/// <summary>
/// Types of blocks available in Zarla Extensions
/// </summary>
public enum ExtensionBlockType
{
    // Appearance
    HideElement,
    RemoveElement,
    ChangeText,
    ChangeStyle,
    AddCSS,
    DarkMode,
    ReadingMode,

    // Automation
    AutoClick,
    AutoFill,
    KeyboardShortcut,
    CookieConsent,

    // Advanced
    InjectScript,
    BlockRequest,
    RedirectUrl,
    CustomCode
}

/// <summary>
/// Metadata about a block type for the UI
/// </summary>
public class BlockTypeInfo
{
    public ExtensionBlockType Type { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Category { get; set; } = "";
    public List<BlockConfigField> ConfigFields { get; set; } = new();

    public static List<BlockTypeInfo> GetAllTypes() => new()
    {
        // Appearance
        new() {
            Type = ExtensionBlockType.HideElement,
            Name = "Hide Element",
            Description = "Hide elements on the page using CSS selector",
            Icon = "👁️",
            Category = "Appearance",
            ConfigFields = new() {
                new("selector", "CSS Selector", "e.g., .ad-banner, #popup", true)
            }
        },
        new() {
            Type = ExtensionBlockType.RemoveElement,
            Name = "Remove Element",
            Description = "Completely remove elements from the page",
            Icon = "🗑️",
            Category = "Appearance",
            ConfigFields = new() {
                new("selector", "CSS Selector", "e.g., .newsletter-popup", true)
            }
        },
        new() {
            Type = ExtensionBlockType.ChangeText,
            Name = "Change Text",
            Description = "Replace text on the page",
            Icon = "📝",
            Category = "Appearance",
            ConfigFields = new() {
                new("selector", "CSS Selector (optional)", "Target specific elements"),
                new("oldText", "Find Text", "Text to replace"),
                new("newText", "Replace With", "New text", true)
            }
        },
        new() {
            Type = ExtensionBlockType.ChangeStyle,
            Name = "Change Style",
            Description = "Modify CSS styles of elements",
            Icon = "🎨",
            Category = "Appearance",
            ConfigFields = new() {
                new("selector", "CSS Selector", "e.g., body", true),
                new("property", "CSS Property", "e.g., background-color", true),
                new("value", "Value", "e.g., #1a1a1a", true)
            }
        },
        new() {
            Type = ExtensionBlockType.AddCSS,
            Name = "Add Custom CSS",
            Description = "Inject custom CSS styles",
            Icon = "🎭",
            Category = "Appearance",
            ConfigFields = new() {
                new("css", "CSS Code", "Your custom CSS", true, true)
            }
        },
        new() {
            Type = ExtensionBlockType.DarkMode,
            Name = "Force Dark Mode",
            Description = "Apply dark mode to any website",
            Icon = "🌙",
            Category = "Appearance",
            ConfigFields = new() {
                new("brightness", "Brightness", "0.8 - 1.0"),
                new("contrast", "Contrast", "1.0 - 1.2")
            }
        },
        new() {
            Type = ExtensionBlockType.ReadingMode,
            Name = "Reading Mode",
            Description = "Clean up page for better reading",
            Icon = "📖",
            Category = "Appearance",
            ConfigFields = new() {
                new("maxWidth", "Max Width", "e.g., 800px"),
                new("fontSize", "Font Size", "e.g., 18px"),
                new("lineHeight", "Line Height", "e.g., 1.8")
            }
        },

        // Automation
        new() {
            Type = ExtensionBlockType.AutoClick,
            Name = "Auto Click",
            Description = "Automatically click an element",
            Icon = "👆",
            Category = "Automation",
            ConfigFields = new() {
                new("selector", "CSS Selector", "Element to click", true),
                new("delay", "Delay (ms)", "Wait before clicking")
            }
        },
        new() {
            Type = ExtensionBlockType.AutoFill,
            Name = "Auto Fill",
            Description = "Automatically fill form fields",
            Icon = "✍️",
            Category = "Automation",
            ConfigFields = new() {
                new("selector", "CSS Selector", "Input field to fill", true),
                new("value", "Value", "Text to fill", true)
            }
        },
        new() {
            Type = ExtensionBlockType.CookieConsent,
            Name = "Dismiss Cookie Popups",
            Description = "Automatically accept cookie consent dialogs",
            Icon = "🍪",
            Category = "Automation",
            ConfigFields = new()
        },
        new() {
            Type = ExtensionBlockType.KeyboardShortcut,
            Name = "Keyboard Shortcut",
            Description = "Add custom keyboard shortcuts",
            Icon = "⌨️",
            Category = "Automation",
            ConfigFields = new() {
                new("key", "Key", "e.g., s, Enter, Escape", true),
                new("ctrl", "Ctrl", "true/false"),
                new("alt", "Alt", "true/false"),
                new("shift", "Shift", "true/false"),
                new("action", "JavaScript Action", "Code to run", true, true)
            }
        },

        // Advanced
        new() {
            Type = ExtensionBlockType.InjectScript,
            Name = "Inject Script",
            Description = "Run custom JavaScript code",
            Icon = "⚡",
            Category = "Advanced",
            ConfigFields = new() {
                new("script", "JavaScript Code", "Your code here", true, true)
            }
        },
        new() {
            Type = ExtensionBlockType.RedirectUrl,
            Name = "Redirect URL",
            Description = "Redirect from one URL to another",
            Icon = "↪️",
            Category = "Advanced",
            ConfigFields = new() {
                new("fromPattern", "URL Pattern", "Regex pattern to match", true),
                new("toUrl", "Redirect To", "Target URL", true)
            }
        },
        new() {
            Type = ExtensionBlockType.CustomCode,
            Name = "Custom Code",
            Description = "Write raw JavaScript (advanced)",
            Icon = "💻",
            Category = "Advanced",
            ConfigFields = new() {
                new("code", "Code", "Raw JavaScript", true, true)
            }
        }
    };
}

/// <summary>
/// Configuration field definition for block UI
/// </summary>
public class BlockConfigField
{
    public string Key { get; set; }
    public string Label { get; set; }
    public string Placeholder { get; set; }
    public bool Required { get; set; }
    public bool Multiline { get; set; }

    public BlockConfigField(string key, string label, string placeholder, bool required = false, bool multiline = false)
    {
        Key = key;
        Label = label;
        Placeholder = placeholder;
        Required = required;
        Multiline = multiline;
    }
}
