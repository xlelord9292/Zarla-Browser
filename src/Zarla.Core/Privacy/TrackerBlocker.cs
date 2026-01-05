using System.Text.RegularExpressions;

namespace Zarla.Core.Privacy;

public class TrackerBlocker
{
    private readonly HashSet<string> _trackerDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Regex> _trackerPatterns = new();
    private readonly HashSet<string> _whitelistedDomains = new(StringComparer.OrdinalIgnoreCase);
    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public int BlockedCount { get; private set; }

    public TrackerBlocker()
    {
        LoadDefaultTrackers();
    }

    private void LoadDefaultTrackers()
    {
        // EasyPrivacy inspired tracker list
        var trackerDomains = new[]
        {
            // Analytics
            "google-analytics.com",
            "analytics.google.com",
            "googletagmanager.com",
            "hotjar.com",
            "fullstory.com",
            "mixpanel.com",
            "segment.com",
            "segment.io",
            "amplitude.com",
            "heap.io",
            "heapanalytics.com",
            "mouseflow.com",
            "crazyegg.com",
            "clicktale.net",
            "luckyorange.com",
            "inspectlet.com",
            "logrocket.com",
            "smartlook.com",
            "clarity.ms",
            "newrelic.com",
            "nr-data.net",
            "appdynamics.com",
            "datadoghq.com",
            "sumologic.com",
            "splunk.com",
            "elastic-cloud.com",

            // Social trackers
            "connect.facebook.net",
            "pixel.facebook.com",
            "facebook.com/tr",
            "platform.twitter.com",
            "analytics.twitter.com",
            "syndication.twitter.com",
            "platform.linkedin.com",
            "snap.licdn.com",
            "ads.linkedin.com",
            "platform.instagram.com",
            "tiktok.com/i18n",
            "analytics.tiktok.com",
            "pinterest.com/ct.html",
            "ct.pinterest.com",

            // Marketing/Attribution
            "branch.io",
            "app.link",
            "appsflyer.com",
            "adjust.com",
            "kochava.com",
            "singular.net",
            "mparticle.com",
            "braze.com",
            "leanplum.com",
            "onesignal.com",
            "pushwoosh.com",
            "airship.com",
            "iterable.com",
            "customer.io",
            "intercom.io",
            "drift.com",
            "hubspot.com",
            "hs-analytics.net",
            "hsforms.net",
            "marketo.net",
            "marketo.com",
            "pardot.com",
            "eloqua.com",
            "omtrdc.net",
            "demdex.net",
            "everesttech.net",

            // Fingerprinting services
            "fingerprintjs.com",
            "fpjs.io",
            "datadome.co",
            "perimeterx.net",
            "kasada.io",

            // Session replay
            "rrweb.io",
            "quantummetric.com",
            "glassbox.com",
            "contentsquare.com",
            "medallia.com",
            "usabilla.com",
            "qualtrics.com",

            // Error tracking (when used for tracking)
            "sentry.io",
            "bugsnag.com",
            "rollbar.com",
            "raygun.io",
            "trackjs.com",
            "errorception.com"
        };

        foreach (var domain in trackerDomains)
        {
            _trackerDomains.Add(domain);
        }

        // Tracking URL patterns
        var patterns = new[]
        {
            @"[?&]utm_",
            @"[?&]fbclid=",
            @"[?&]gclid=",
            @"[?&]msclkid=",
            @"[?&]dclid=",
            @"[?&]_ga=",
            @"[?&]_gl=",
            @"[?&]mc_[ce]id=",
            @"[?&]oly_",
            @"[?&]__hs",
            @"[?&]mkt_tok=",
            @"[?&]trk=",
            @"[?&]clickid=",
            @"[?&]affiliate",
            @"[?&]ref_?=",
            @"/collect\?",
            @"/pageview",
            @"/track[ing]?",
            @"/event[s]?\?",
            @"/log[ging]?\?",
            @"/telemetry",
            @"/metrics",
            @"/beacon",
            @"/__imp",
            @"/pixel",
            @"\.gif\?.*=",
            @"/1x1\.",
            @"/tr\?",
            @"/v1/track"
        };

        foreach (var pattern in patterns)
        {
            _trackerPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }
    }

    public bool ShouldBlock(string url)
    {
        if (!_isEnabled) return false;

        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();

            // Check whitelist
            if (IsWhitelisted(host)) return false;

            // Check tracker domains
            foreach (var trackerDomain in _trackerDomains)
            {
                if (host.EndsWith(trackerDomain) || host == trackerDomain)
                {
                    BlockedCount++;
                    return true;
                }
            }

            // Check URL patterns
            foreach (var pattern in _trackerPatterns)
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
            // Invalid URL
        }

        return false;
    }

    public string CleanUrl(string url)
    {
        // Remove tracking parameters from URL
        try
        {
            var uri = new Uri(url);
            if (string.IsNullOrEmpty(uri.Query)) return url;

            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var trackingParams = new[]
            {
                "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
                "fbclid", "gclid", "msclkid", "dclid",
                "_ga", "_gl", "_hsenc", "_hsmi",
                "mc_cid", "mc_eid",
                "oly_anon_id", "oly_enc_id",
                "mkt_tok", "trk",
                "ref", "ref_",
                "clickid", "affiliate_id",
                "__s", "__hstc", "__hsfp"
            };

            foreach (var param in trackingParams)
            {
                query.Remove(param);
            }

            var cleanQuery = query.ToString();
            var builder = new UriBuilder(uri)
            {
                Query = cleanQuery
            };

            return builder.ToString();
        }
        catch
        {
            return url;
        }
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

    public void AddToWhitelist(string domain) => _whitelistedDomains.Add(domain.ToLowerInvariant());
    public void RemoveFromWhitelist(string domain) => _whitelistedDomains.Remove(domain.ToLowerInvariant());
    public IReadOnlyCollection<string> GetWhitelist() => _whitelistedDomains;
    public void ResetBlockCount() => BlockedCount = 0;
}
