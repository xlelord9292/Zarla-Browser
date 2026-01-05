using System.Text.RegularExpressions;

namespace Zarla.Core.Privacy;

public class AdBlocker
{
    private readonly HashSet<string> _blockedDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Regex> _blockedPatterns = new();
    private readonly HashSet<string> _whitelistedDomains = new(StringComparer.OrdinalIgnoreCase);
    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public int BlockedCount { get; private set; }

    public AdBlocker()
    {
        LoadDefaultBlocklist();
    }

    private void LoadDefaultBlocklist()
    {
        // Common ad domains - EasyList inspired
        var adDomains = new[]
        {
            "doubleclick.net",
            "googlesyndication.com",
            "googleadservices.com",
            "google-analytics.com",
            "googletagmanager.com",
            "googletagservices.com",
            "adservice.google.com",
            "pagead2.googlesyndication.com",
            "ads.yahoo.com",
            "advertising.com",
            "adnxs.com",
            "adsrvr.org",
            "adtechus.com",
            "amazon-adsystem.com",
            "facebook.com/tr",
            "facebook.net/signals",
            "analytics.twitter.com",
            "ads.twitter.com",
            "adsymptotic.com",
            "adform.net",
            "advertising.microsoft.com",
            "ads.microsoft.com",
            "bat.bing.com",
            "criteo.com",
            "criteo.net",
            "outbrain.com",
            "taboola.com",
            "revcontent.com",
            "mgid.com",
            "zergnet.com",
            "moatads.com",
            "scorecardresearch.com",
            "quantserve.com",
            "adsafeprotected.com",
            "rubiconproject.com",
            "pubmatic.com",
            "openx.net",
            "casalemedia.com",
            "advertising.yahoo.com",
            "adroll.com",
            "mediavine.com",
            "sharethrough.com",
            "spotxchange.com",
            "bidswitch.net",
            "lijit.com",
            "sovrn.com",
            "gumgum.com",
            "triplelift.com",
            "smaato.net",
            "adcolony.com",
            "unity3d.com/ads",
            "applovin.com",
            "mopub.com",
            "inmobi.com",
            "vungle.com",
            "chartboost.com",
            "fyber.com",
            "ironsrc.com",
            "digitalturbine.com"
        };

        foreach (var domain in adDomains)
        {
            _blockedDomains.Add(domain);
        }

        // URL patterns to block
        var patterns = new[]
        {
            @"/ads?/",
            @"/ad\.",
            @"[?&]ad[s]?=",
            @"/banner[s]?/",
            @"/sponsor",
            @"/popup",
            @"tracking\.js",
            @"analytics\.js",
            @"/pixel\.",
            @"/beacon",
            @"\.gif\?.*track",
            @"/impression",
            @"/click\?",
            @"affiliate",
            @"/promo/"
        };

        foreach (var pattern in patterns)
        {
            _blockedPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }
    }

    public bool ShouldBlock(string url)
    {
        if (!_isEnabled) return false;

        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();

            // Check whitelist first
            if (IsWhitelisted(host)) return false;

            // Check blocked domains
            foreach (var blockedDomain in _blockedDomains)
            {
                if (host.EndsWith(blockedDomain) || host == blockedDomain)
                {
                    BlockedCount++;
                    return true;
                }
            }

            // Check URL patterns
            foreach (var pattern in _blockedPatterns)
            {
                if (pattern.IsMatch(url))
                {
                    BlockedCount++;
                    return true;
                }
            }
        }
        catch
        {
            // Invalid URL, don't block
        }

        return false;
    }

    public bool ShouldBlockResourceType(string resourceType)
    {
        if (!_isEnabled) return false;

        // Block common ad resource types
        return resourceType.ToLowerInvariant() switch
        {
            "ping" => true,  // Tracking pings
            _ => false
        };
    }

    private bool IsWhitelisted(string host)
    {
        foreach (var whitelist in _whitelistedDomains)
        {
            if (host.EndsWith(whitelist) || host == whitelist)
                return true;
        }
        return false;
    }

    public void AddToWhitelist(string domain)
    {
        _whitelistedDomains.Add(domain.ToLowerInvariant());
    }

    public void RemoveFromWhitelist(string domain)
    {
        _whitelistedDomains.Remove(domain.ToLowerInvariant());
    }

    public IReadOnlyCollection<string> GetWhitelist() => _whitelistedDomains;

    public void AddBlockedDomain(string domain)
    {
        _blockedDomains.Add(domain.ToLowerInvariant());
    }

    public void LoadBlocklistFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('!'))
                continue;

            // Simple domain blocking format
            if (trimmed.StartsWith("||") && trimmed.EndsWith("^"))
            {
                var domain = trimmed[2..^1];
                _blockedDomains.Add(domain);
            }
            else if (!trimmed.Contains('/') && !trimmed.Contains('*'))
            {
                _blockedDomains.Add(trimmed);
            }
        }
    }

    public void ResetBlockCount() => BlockedCount = 0;
}
