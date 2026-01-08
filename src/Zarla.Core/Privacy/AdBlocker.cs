using System.Text.RegularExpressions;

namespace Zarla.Core.Privacy;

public class AdBlocker
{
    private readonly HashSet<string> _blockedDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _blockedSubstrings = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _whitelistedDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _youtubeAdPatterns = new(StringComparer.OrdinalIgnoreCase);
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
        LoadYouTubeAdBlocklist();
    }

    private void LoadDefaultBlocklist()
    {
        // Common ad domains - EasyList inspired
        var adDomains = new[]
        {
            // Google Ads
            "doubleclick.net",
            "googlesyndication.com",
            "googleadservices.com",
            "google-analytics.com",
            "googletagmanager.com",
            "googletagservices.com",
            "adservice.google.com",
            "pagead2.googlesyndication.com",
            "tpc.googlesyndication.com",
            "www.googleadservices.com",
            "securepubads.g.doubleclick.net",
            "pubads.g.doubleclick.net",
            "ad.doubleclick.net",
            "static.doubleclick.net",
            "m.doubleclick.net",
            "mediavisor.doubleclick.net",
            
            // Yahoo/AOL
            "ads.yahoo.com",
            "advertising.com",
            "advertising.yahoo.com",
            
            // Other ad networks
            "adnxs.com",
            "adsrvr.org",
            "adtechus.com",
            "amazon-adsystem.com",
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
            "digitalturbine.com",
            
            // Facebook/Meta
            "facebook.com/tr",
            "facebook.net/signals",
            "pixel.facebook.com",
            "an.facebook.com",
            "connect.facebook.net/en_US/fbevents.js",
            
            // Twitter
            "analytics.twitter.com",
            "ads.twitter.com",
            "ads-api.twitter.com",
            
            // Tracking
            "hotjar.com",
            "mixpanel.com",
            "segment.com",
            "amplitude.com",
            "fullstory.com",
            "mouseflow.com",
            "crazyegg.com",
            "clicktale.net",
            "optimizely.com",
            "branch.io",
            "adjust.com",
            "appsflyer.com",
            "kochava.com",
            "singular.net",
            
            // Additional ad servers
            "serving-sys.com",
            "adtech.de",
            "turn.com",
            "adnxs.net",
            "contextweb.com",
            "yieldmanager.com",
            "yieldlab.net",
            "smartadserver.com",
            "innovid.com",
            "eyeviewdigital.com",
            "tubemogul.com",
            "videologygroup.com",
            "flashtalking.com",
            "sizmek.com",
            "celtra.com",
            "jivox.com",
            "adap.tv"
        };

        foreach (var domain in adDomains)
        {
            _blockedDomains.Add(domain);
        }

        // Fast substring patterns to block (no regex - much faster)
        var substrings = new[]
        {
            "/pagead/",
            "/ads/",
            "/ad/",
            "doubleclick",
            "googlesyndication",
            "googleadservices",
            "adserver",
            "/gtag/",
            "/gtm.js"
        };

        foreach (var s in substrings)
        {
            _blockedSubstrings.Add(s);
        }
    }

    private void LoadYouTubeAdBlocklist()
    {
        // YouTube-specific ad patterns
        var ytAdPatterns = new[]
        {
            // YouTube ad serving domains
            "youtubei.googleapis.com/youtubei/v1/player/ad_break",
            "www.youtube.com/api/stats/ads",
            "www.youtube.com/pagead/",
            "www.youtube.com/ptracking",
            "www.youtube.com/get_midroll_",
            "www.youtube.com/api/stats/watchtime",
            "www.youtube.com/csi_204",
            
            // Ad-related paths
            "/pagead/",
            "/ptracking",
            "/api/stats/ads",
            "/get_midroll_info",
            "ad_break",
            "/youtubei/v1/log_event",
            
            // Sponsored content
            "sponsor.ytimg.com",
            "adsapi.youtube.com",
            
            // Video ads
            "googleads.g.doubleclick.net",
            "ad.youtube.com",
            "ads.youtube.com"
        };
        
        foreach (var pattern in ytAdPatterns)
        {
            _youtubeAdPatterns.Add(pattern);
        }
        
        // Additional YouTube ad domains
        var ytAdDomains = new[]
        {
            "adsapi.youtube.com",
            "ad.youtube.com",
            "ads.youtube.com",
            "youtube.cleverads.vn",
            "youtube.moatads.com"
        };
        
        foreach (var domain in ytAdDomains)
        {
            _blockedDomains.Add(domain);
        }
    }

    public bool ShouldBlock(string url)
    {
        if (!_isEnabled) return false;

        try
        {
            // Fast host extraction without full Uri parsing
            var hostStart = url.IndexOf("://");
            if (hostStart < 0) return false;
            hostStart += 3;

            var hostEnd = url.IndexOf('/', hostStart);
            if (hostEnd < 0) hostEnd = url.Length;

            var host = url.Substring(hostStart, hostEnd - hostStart).ToLowerInvariant();

            // Remove port if present
            var portIdx = host.IndexOf(':');
            if (portIdx > 0) host = host.Substring(0, portIdx);

            // Check whitelist first
            if (IsWhitelisted(host)) return false;

            // Fast domain check using HashSet
            foreach (var blockedDomain in _blockedDomains)
            {
                if (host.EndsWith(blockedDomain) || host == blockedDomain)
                {
                    BlockedCount++;
                    return true;
                }
            }

            // Fast substring check (no regex)
            var lowerUrl = url.ToLowerInvariant();
            foreach (var substring in _blockedSubstrings)
            {
                if (lowerUrl.Contains(substring))
                {
                    BlockedCount++;
                    return true;
                }
            }

            // Check YouTube-specific patterns only for YouTube domains
            if (host.Contains("youtube") || host.Contains("googlevideo"))
            {
                foreach (var pattern in _youtubeAdPatterns)
                {
                    if (lowerUrl.Contains(pattern))
                    {
                        BlockedCount++;
                        return true;
                    }
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

    /// <summary>
    /// Gets JavaScript to inject for in-page ad blocking (YouTube ads, etc.)
    /// </summary>
    public string GetAdBlockScript()
    {
        return @"
(function() {
    'use strict';
    
    // Skip if not YouTube
    if (!window.location.hostname.includes('youtube.com')) return;
    
    console.log('[Zarla] YouTube ad blocker v2 initializing...');
    
    let lastAdState = false;
    let videoPlaybackRate = 1;
    let wasMuted = false;
    
    // Ad detection and skipping - runs very frequently
    const handleAds = () => {
        const video = document.querySelector('video.html5-main-video');
        const player = document.querySelector('.html5-video-player');
        
        if (!video || !player) return;
        
        const isAdPlaying = player.classList.contains('ad-showing') || 
                           player.classList.contains('ad-interrupting') ||
                           !!document.querySelector('.ytp-ad-player-overlay-instream-info') ||
                           !!document.querySelector('.ytp-ad-simple-ad-badge') ||
                           !!document.querySelector('.ytp-ad-preview-container');
        
        if (isAdPlaying) {
            if (!lastAdState) {
                console.log('[Zarla] Ad started - skipping...');
                wasMuted = video.muted;
                videoPlaybackRate = video.playbackRate;
            }
            lastAdState = true;
            
            // Mute and speed through ad
            video.muted = true;
            video.playbackRate = 16;
            
            // Try to skip to end
            if (video.duration && video.duration > 0 && isFinite(video.duration)) {
                video.currentTime = video.duration - 0.1;
            }
            
            // Click ALL possible skip buttons
            const skipSelectors = [
                '.ytp-ad-skip-button',
                '.ytp-ad-skip-button-modern', 
                '.ytp-skip-ad-button',
                '.ytp-ad-skip-button-container button',
                '.ytp-ad-skip-button-slot button',
                'button.ytp-ad-skip-button-modern',
                '.videoAdUiSkipButton',
                '[id^=""skip-button""]',
                '.ytp-ad-overlay-close-button',
                '.ytp-ad-skip-button-icon-container'
            ];
            
            skipSelectors.forEach(sel => {
                document.querySelectorAll(sel).forEach(btn => {
                    try { 
                        btn.click(); 
                        console.log('[Zarla] Clicked:', sel);
                    } catch(e) {}
                });
            });
            
        } else if (lastAdState) {
            // Ad just ended
            console.log('[Zarla] Ad ended - restoring playback');
            lastAdState = false;
            video.playbackRate = videoPlaybackRate || 1;
            video.muted = wasMuted;
        }
    };
    
    // Remove ad elements from page
    const removeAdElements = () => {
        const adSelectors = [
            // Video player ads
            '.ytp-ad-module',
            '.ytp-ad-overlay-container', 
            '.ytp-ad-text-overlay',
            '.ytp-ad-image-overlay',
            '.ytp-ad-overlay-slot',
            'div.video-ads',
            '#player-ads',
            
            // Page ads
            'ytd-ad-slot-renderer',
            'ytd-banner-promo-renderer', 
            'ytd-video-masthead-ad-v3-renderer',
            'ytd-promoted-sparkles-web-renderer',
            'ytd-promoted-video-renderer',
            'ytd-compact-promoted-video-renderer',
            'ytd-in-feed-ad-layout-renderer',
            'ytd-display-ad-renderer',
            'ytd-statement-banner-renderer',
            '#masthead-ad',
            'ytd-merch-shelf-renderer',
            'ytd-action-companion-ad-renderer',
            'ytd-engagement-panel-section-list-renderer[target-id=""engagement-panel-ads""]',
            '#related ytd-promoted-sparkles-text-search-renderer',
            'ytd-primetime-promo-renderer',
            'ytd-search-pyv-renderer',
            
            // Popup/modal ads  
            'tp-yt-paper-dialog:has(ytd-enforcement-message-view-model)',
            'ytd-popup-container'
        ];
        
        adSelectors.forEach(sel => {
            document.querySelectorAll(sel).forEach(el => {
                el.style.display = 'none';
                el.remove();
            });
        });
        
        // Close overlay ads
        document.querySelectorAll('.ytp-ad-overlay-close-button').forEach(btn => {
            try { btn.click(); } catch(e) {}
        });
    };
    
    // Intercept and block ad requests
    const setupRequestBlocking = () => {
        const adUrlPatterns = [
            'googlesyndication.com',
            'doubleclick.net',
            '/pagead/',
            '/ptracking',
            '/api/stats/ads',
            'youtube.com/api/stats/qoe',
            '/youtubei/v1/player/ad_break',
            'googleads.g.doubleclick.net',
            '/get_midroll_',
            'adsapi.youtube.com',
            '/ad_break',
            'ad.youtube.com',
            'ads.youtube.com'
        ];
        
        const shouldBlock = (url) => {
            if (!url) return false;
            return adUrlPatterns.some(p => url.includes(p));
        };
        
        // Block fetch requests
        const origFetch = window.fetch;
        window.fetch = async function(input, init) {
            const url = typeof input === 'string' ? input : input?.url || '';
            if (shouldBlock(url)) {
                console.log('[Zarla] Blocked fetch:', url.substring(0, 50));
                return new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } });
            }
            return origFetch.apply(this, arguments);
        };
        
        // Block XHR requests
        const origXhrOpen = XMLHttpRequest.prototype.open;
        XMLHttpRequest.prototype.open = function(method, url) {
            this._zarlaBlockedUrl = shouldBlock(url) ? url : null;
            if (this._zarlaBlockedUrl) {
                console.log('[Zarla] Will block XHR:', url.substring(0, 50));
            }
            return origXhrOpen.apply(this, arguments);
        };
        
        const origXhrSend = XMLHttpRequest.prototype.send;
        XMLHttpRequest.prototype.send = function() {
            if (this._zarlaBlockedUrl) {
                // Simulate successful empty response
                Object.defineProperty(this, 'readyState', { value: 4 });
                Object.defineProperty(this, 'status', { value: 200 });
                Object.defineProperty(this, 'responseText', { value: '{}' });
                this.dispatchEvent(new Event('load'));
                return;
            }
            return origXhrSend.apply(this, arguments);
        };
    };
    
    // Hook into YouTube's player API to prevent ads
    const hookPlayerApi = () => {
        // Remove ad config from player response
        const processPlayerResponse = (data) => {
            if (!data) return data;
            try {
                if (data.adPlacements) data.adPlacements = [];
                if (data.playerAds) data.playerAds = [];
                if (data.adSlots) data.adSlots = [];
                if (data.adBreakParams) data.adBreakParams = null;
            } catch(e) {}
            return data;
        };
        
        // Override JSON.parse to filter ad data
        const origParse = JSON.parse;
        JSON.parse = function(text) {
            const result = origParse.apply(this, arguments);
            return processPlayerResponse(result);
        };
    };
    
    // Initialize
    setupRequestBlocking();
    hookPlayerApi();
    
    // Run immediately and continuously
    handleAds();
    removeAdElements();
    
    // High frequency ad detection (50ms)
    setInterval(handleAds, 50);
    
    // Element removal (500ms)  
    setInterval(removeAdElements, 500);
    
    // Watch for DOM changes
    const observer = new MutationObserver((mutations) => {
        handleAds();
        // Check if any ad elements were added
        mutations.forEach(m => {
            if (m.addedNodes.length) {
                removeAdElements();
            }
        });
    });
    
    observer.observe(document.body, { 
        childList: true, 
        subtree: true,
        attributes: true,
        attributeFilter: ['class', 'src']
    });
    
    // Also hook video element events
    document.addEventListener('play', handleAds, true);
    document.addEventListener('loadeddata', handleAds, true);
    
    console.log('[Zarla] YouTube ad blocker v2 active!');
})();
";
    }

    /// <summary>
    /// Gets a cosmetic blocking script for hiding ad elements
    /// </summary>
    public string GetCosmeticBlockScript()
    {
        return @"
(function() {
    'use strict';
    
    const style = document.createElement('style');
    style.textContent = `
        /* Hide common ad containers */
        [id*='google_ads'], [id*='GoogleAds'], [class*='google-ad'],
        [id*='ad-'], [id*='ad_'], [class*='ad-container'], [class*='ad_container'],
        [class*='advert'], [class*='advertisement'],
        [data-ad], [data-ad-slot], [data-ad-client],
        ins.adsbygoogle, .adsbygoogle,
        iframe[src*='doubleclick'], iframe[src*='googlesyndication'],
        .sponsored-content, .sponsored-ad,
        [class*='sidebar-ad'], [class*='banner-ad'],
        
        /* YouTube specific */
        ytd-ad-slot-renderer, ytd-banner-promo-renderer,
        ytd-video-masthead-ad-v3-renderer, ytd-promoted-sparkles-web-renderer,
        ytd-in-feed-ad-layout-renderer, .ytp-ad-module,
        .ytp-ad-overlay-container, .ytp-ad-text-overlay,
        #masthead-ad, #player-ads, .video-ads,
        ytd-merch-shelf-renderer, ytd-promoted-video-renderer,
        ytd-compact-promoted-video-renderer,
        ytd-display-ad-renderer,
        ytd-statement-banner-renderer,
        #related ytd-promoted-sparkles-text-search-renderer,
        tp-yt-paper-dialog:has(ytd-enforcement-message-view-model)
        {
            display: none !important;
            visibility: hidden !important;
            height: 0 !important;
            width: 0 !important;
            opacity: 0 !important;
            pointer-events: none !important;
        }
    `;
    document.head.appendChild(style);
})();
";
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
