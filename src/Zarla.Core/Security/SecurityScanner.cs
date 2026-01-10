using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Zarla.Core.Security;

public enum SecurityLevel
{
    Low,     // Known threats only
    Medium,  // + URL pattern analysis
    High     // + AI scanning + VirusTotal
}

public class SecurityScanResult
{
    public bool IsSafe { get; set; } = true;
    public string? WarningCode { get; set; }
    public string? WarningTitle { get; set; }
    public string? WarningMessage { get; set; }
    public List<string> Issues { get; set; } = new();
    public double RiskScore { get; set; } = 0; // 0-100
    public SecurityThreatType ThreatType { get; set; } = SecurityThreatType.None;
    public VTScanResult? VirusTotalResult { get; set; }
}

public enum SecurityThreatType
{
    None,
    MirrorSite,
    Phishing,
    Malware,
    SuspiciousContent,
    UntrustedCertificate,
    DataHarvesting,
    Scam,
    VirusTotalDetection
}

public class SecurityScanner : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly VirusTotalService _virusTotal;
    private readonly HashSet<string> _knownMaliciousDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _trustedDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _legitimateDomains = new(StringComparer.OrdinalIgnoreCase);
    private SecurityLevel _securityLevel = SecurityLevel.Medium;
    
    // Groq API for AI scanning (free, no limits for security)
    private static readonly string GroqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";
    private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";

    public SecurityLevel Level
    {
        get => _securityLevel;
        set => _securityLevel = value;
    }
    
    public VirusTotalService VirusTotal => _virusTotal;

    public SecurityScanner()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GroqApiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        
        _virusTotal = new VirusTotalService();
        
        LoadKnownThreats();
        LoadLegitimateDomains();
        LoadTrustedDomains();
    }

    private void LoadKnownThreats()
    {
        // Known malicious/phishing domains
        var threats = new[]
        {
            "malware-site.com", "phishing-example.com", "fake-bank.com",
            "scam-alert.net", "virus-download.org"
        };
        
        foreach (var domain in threats)
            _knownMaliciousDomains.Add(domain);
    }

    private void LoadLegitimateDomains()
    {
        // Map of legitimate domains for mirror detection
        _legitimateDomains["google"] = "google.com";
        _legitimateDomains["youtube"] = "youtube.com";
        _legitimateDomains["facebook"] = "facebook.com";
        _legitimateDomains["instagram"] = "instagram.com";
        _legitimateDomains["twitter"] = "twitter.com";
        _legitimateDomains["x"] = "x.com";
        _legitimateDomains["amazon"] = "amazon.com";
        _legitimateDomains["paypal"] = "paypal.com";
        _legitimateDomains["microsoft"] = "microsoft.com";
        _legitimateDomains["apple"] = "apple.com";
        _legitimateDomains["netflix"] = "netflix.com";
        _legitimateDomains["spotify"] = "spotify.com";
        _legitimateDomains["github"] = "github.com";
        _legitimateDomains["reddit"] = "reddit.com";
        _legitimateDomains["linkedin"] = "linkedin.com";
        _legitimateDomains["twitch"] = "twitch.tv";
        _legitimateDomains["discord"] = "discord.com";
        _legitimateDomains["whatsapp"] = "whatsapp.com";
        _legitimateDomains["telegram"] = "telegram.org";
        _legitimateDomains["tiktok"] = "tiktok.com";
        _legitimateDomains["ebay"] = "ebay.com";
        _legitimateDomains["walmart"] = "walmart.com";
        _legitimateDomains["target"] = "target.com";
        _legitimateDomains["bestbuy"] = "bestbuy.com";
        _legitimateDomains["chase"] = "chase.com";
        _legitimateDomains["bankofamerica"] = "bankofamerica.com";
        _legitimateDomains["wellsfargo"] = "wellsfargo.com";
        _legitimateDomains["capitalone"] = "capitalone.com";
    }

    private void LoadTrustedDomains()
    {
        // Always trusted domains (skip scanning) - comprehensive list
        var trusted = new[]
        {
            // Major tech companies
            "google.com", "youtube.com", "gmail.com", "googleapis.com", "gstatic.com",
            "googlevideo.com", "googleusercontent.com", "google.co.uk", "google.ca",
            "microsoft.com", "live.com", "outlook.com", "office.com", "azure.com",
            "windows.com", "xbox.com", "bing.com", "msn.com", "skype.com",
            "apple.com", "icloud.com", "itunes.com",
            "amazon.com", "amazonaws.com", "aws.amazon.com", "amazon.co.uk",
            "meta.com", "facebook.com", "fb.com", "instagram.com", "whatsapp.com",
            "twitter.com", "x.com", "twimg.com",

            // Social media & entertainment
            "reddit.com", "redd.it", "redditstatic.com",
            "linkedin.com", "licdn.com",
            "pinterest.com", "pinimg.com",
            "tumblr.com",
            "snapchat.com", "snap.com",
            "tiktok.com", "tiktokcdn.com",
            "discord.com", "discordapp.com", "discord.gg",
            "twitch.tv", "twitchcdn.net",
            "youtube.com", "youtu.be", "ytimg.com",
            "netflix.com", "nflxvideo.net",
            "spotify.com", "scdn.co",
            "hulu.com", "disneyplus.com",
            "hbomax.com", "max.com",
            "primevideo.com",
            "crunchyroll.com", "funimation.com",
            "soundcloud.com", "bandcamp.com",
            "vimeo.com", "dailymotion.com",

            // News & media
            "cnn.com", "bbc.com", "bbc.co.uk", "nytimes.com", "washingtonpost.com",
            "theguardian.com", "reuters.com", "apnews.com", "npr.org",
            "foxnews.com", "nbcnews.com", "cbsnews.com", "abcnews.go.com",
            "usatoday.com", "wsj.com", "bloomberg.com", "forbes.com",
            "huffpost.com", "buzzfeed.com", "vice.com", "vox.com",
            "techcrunch.com", "theverge.com", "wired.com", "arstechnica.com",
            "engadget.com", "gizmodo.com", "mashable.com", "cnet.com",
            "ign.com", "gamespot.com", "kotaku.com", "polygon.com",

            // Shopping & commerce
            "ebay.com", "etsy.com", "shopify.com",
            "walmart.com", "target.com", "bestbuy.com", "costco.com",
            "homedepot.com", "lowes.com", "ikea.com",
            "newegg.com", "bhphotovideo.com",
            "aliexpress.com", "alibaba.com",
            "wish.com", "shein.com", "temu.com",
            "wayfair.com", "overstock.com",
            "zappos.com", "nordstrom.com", "macys.com",
            "nike.com", "adidas.com", "underarmour.com",

            // Banking & finance
            "paypal.com", "venmo.com", "cashapp.com",
            "chase.com", "bankofamerica.com", "wellsfargo.com",
            "capitalone.com", "citibank.com", "usbank.com",
            "americanexpress.com", "discover.com",
            "fidelity.com", "vanguard.com", "schwab.com",
            "robinhood.com", "coinbase.com", "binance.com",
            "stripe.com", "square.com", "plaid.com",

            // Developer & tech
            "github.com", "gitlab.com", "bitbucket.org",
            "stackoverflow.com", "stackexchange.com",
            "npmjs.com", "pypi.org", "rubygems.org", "nuget.org",
            "docker.com", "hub.docker.com",
            "heroku.com", "vercel.com", "netlify.com", "railway.app",
            "digitalocean.com", "linode.com", "vultr.com",
            "cloudflare.com", "fastly.com", "akamai.com",
            "godaddy.com", "namecheap.com", "domains.google",
            "mozilla.org", "firefox.com",
            "opera.com", "brave.com", "vivaldi.com",
            "jetbrains.com", "visualstudio.com", "code.visualstudio.com",
            "atlassian.com", "jira.com", "trello.com", "confluence.com",
            "slack.com", "zoom.us", "teams.microsoft.com",
            "notion.so", "airtable.com", "asana.com", "monday.com",
            "figma.com", "canva.com", "adobe.com", "behance.net", "dribbble.com",

            // Education & reference
            "wikipedia.org", "wikimedia.org", "wiktionary.org",
            "archive.org", "web.archive.org",
            "quora.com", "medium.com", "substack.com",
            "coursera.org", "udemy.com", "edx.org", "khanacademy.org",
            "duolingo.com", "skillshare.com", "masterclass.com",
            "mit.edu", "stanford.edu", "harvard.edu", "berkeley.edu",
            "docs.google.com", "drive.google.com", "sheets.google.com",

            // Gaming
            "steampowered.com", "store.steampowered.com", "steamcommunity.com",
            "epicgames.com", "gog.com", "origin.com", "ea.com",
            "blizzard.com", "battle.net", "activision.com",
            "riotgames.com", "leagueoflegends.com", "valorant.com",
            "ubisoft.com", "rockstargames.com", "bethesda.net",
            "playstation.com", "nintendo.com", "xbox.com",
            "roblox.com", "minecraft.net", "fortnite.com",
            "curseforge.com", "modrinth.com", "nexusmods.com",

            // Utilities & services
            "dropbox.com", "box.com", "onedrive.com",
            "evernote.com", "onenote.com",
            "grammarly.com", "deepl.com", "translate.google.com",
            "openai.com", "chat.openai.com", "anthropic.com", "claude.ai",
            "wolframalpha.com", "mathway.com",
            "speedtest.net", "fast.com",
            "weather.com", "accuweather.com",
            "maps.google.com", "waze.com",
            "uber.com", "lyft.com", "doordash.com", "grubhub.com", "ubereats.com",
            "airbnb.com", "booking.com", "expedia.com", "tripadvisor.com",
            "yelp.com", "opentable.com",

            // CDNs and infrastructure (commonly used by all sites)
            "cloudfront.net", "akamaized.net", "fastly.net",
            "jsdelivr.net", "unpkg.com", "cdnjs.cloudflare.com",
            "bootstrapcdn.com", "fontawesome.com",
            "fonts.googleapis.com", "fonts.gstatic.com",
            "gravatar.com", "wp.com", "wordpress.com", "wordpress.org",
            "typekit.net", "use.typekit.net",
            "recaptcha.net", "gstatic.com",
            "hcaptcha.com", "cloudflare.com",

            // File sharing
            "mediafire.com", "mega.nz", "mega.io",
            "wetransfer.com", "sendspace.com",
            "imgur.com", "gyazo.com", "prnt.sc", "lightshot.com",
            "pastebin.com", "hastebin.com", "ghostbin.com",

            // Forums & communities
            "fandom.com", "wikia.com",
            "4chan.org", "4channel.org",
            "resetera.com", "neogaf.com",
            "somethingawful.com", "kiwifarms.net"
        };

        foreach (var domain in trusted)
            _trustedDomains.Add(domain);
    }

    public async Task<SecurityScanResult> ScanUrlAsync(string url)
    {
        var result = new SecurityScanResult();
        
        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();
            
            // Skip scanning for internal URLs
            if (url.StartsWith("zarla://") || url.StartsWith("about:"))
            {
                result.IsSafe = true;
                return result;
            }

            // Check if trusted domain
            if (IsTrustedDomain(host))
            {
                result.IsSafe = true;
                return result;
            }

            // Low security - only check known threats
            if (_securityLevel == SecurityLevel.Low)
            {
                return CheckKnownThreats(host, result);
            }

            // Medium security - check known threats + URL analysis
            if (_securityLevel == SecurityLevel.Medium)
            {
                CheckKnownThreats(host, result);
                if (!result.IsSafe) return result;
                
                CheckUrlPatterns(url, host, result);
                return result;
            }

            // High security - full scan with VirusTotal + AI
            CheckKnownThreats(host, result);
            if (!result.IsSafe) return result;
            
            CheckUrlPatterns(url, host, result);
            if (!result.IsSafe) return result;

            // VirusTotal URL scan (respects rate limits)
            var vtResult = await _virusTotal.ScanUrlAsync(url);
            result.VirusTotalResult = vtResult;
            
            if (vtResult.Success && !vtResult.IsSafe)
            {
                result.IsSafe = false;
                result.ThreatType = SecurityThreatType.VirusTotalDetection;
                result.WarningCode = "ZARLA-SEC-VT1";
                result.WarningTitle = "VirusTotal Detection";
                result.WarningMessage = vtResult.Message;
                result.RiskScore = vtResult.ThreatScore;
                result.Issues.AddRange(vtResult.ThreatDetails);
                return result;
            }

            // AI-powered deep scan for High security (only if VT didn't flag it)
            await PerformAIScanAsync(url, host, result);
            
            return result;
        }
        catch (Exception)
        {
            // If scan fails, return safe to not block legitimate sites
            return result;
        }
    }

    public async Task<SecurityScanResult> ScanPageContentAsync(string url, string content, string title)
    {
        var result = new SecurityScanResult();
        
        if (_securityLevel != SecurityLevel.High)
            return result;

        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();
            
            if (IsTrustedDomain(host))
                return result;

            // Check for suspicious content patterns
            CheckContentPatterns(content, title, result);
            
            if (result.Issues.Count > 2)
            {
                // Multiple suspicious patterns detected, use AI for final verdict
                await PerformAIContentScanAsync(url, content, title, result);
            }

            return result;
        }
        catch
        {
            return result;
        }
    }

    private bool IsTrustedDomain(string host)
    {
        foreach (var trusted in _trustedDomains)
        {
            if (host == trusted || host.EndsWith("." + trusted))
                return true;
        }
        return false;
    }

    private SecurityScanResult CheckKnownThreats(string host, SecurityScanResult result)
    {
        foreach (var malicious in _knownMaliciousDomains)
        {
            if (host == malicious || host.EndsWith("." + malicious))
            {
                result.IsSafe = false;
                result.WarningCode = "ZARLA-SEC-001";
                result.WarningTitle = "Dangerous Website Blocked";
                result.WarningMessage = "This website is on Zarla's blocklist for known malicious activity.";
                result.ThreatType = SecurityThreatType.Malware;
                result.RiskScore = 100;
                result.Issues.Add("Known malicious domain");
                return result;
            }
        }
        return result;
    }

    private void CheckUrlPatterns(string url, string host, SecurityScanResult result)
    {
        // Skip checks for safe URL schemes
        if (url.StartsWith("data:") || url.StartsWith("blob:") || url.StartsWith("javascript:") ||
            url.StartsWith("about:") || url.StartsWith("file:") || url.StartsWith("chrome:") ||
            url.StartsWith("edge:") || url.StartsWith("zarla://"))
        {
            return;
        }

        // Skip if it's a well-known TLD with a legitimate-looking domain
        var wellKnownTlds = new[] { ".com", ".org", ".net", ".edu", ".gov", ".io", ".co", ".app", ".dev", ".me", ".tv", ".gg" };
        var hasWellKnownTld = wellKnownTlds.Any(tld => host.EndsWith(tld));

        // Check for typosquatting/mirror sites - only for very obvious cases
        foreach (var (brand, legitimate) in _legitimateDomains)
        {
            // Only check if the host STARTS with the brand name (more precise)
            // e.g., "google-login.com" but not "mygoogleapp.com"
            if (host.StartsWith(brand) && !host.EndsWith(legitimate) && !host.EndsWith("." + legitimate))
            {
                // Must be very similar to be flagged (higher threshold)
                var similarity = CalculateSimilarity(host, legitimate);
                if (similarity > 0.75 && similarity < 1.0)
                {
                    // Additional check: the host should look like it's trying to impersonate
                    var suspiciousImpersonation = host.Contains("-login") || host.Contains("-secure") ||
                                                   host.Contains("-verify") || host.Contains("-account") ||
                                                   host.Contains(".login.") || host.Contains(".secure.");

                    if (suspiciousImpersonation)
                    {
                        result.IsSafe = false;
                        result.WarningCode = "ZARLA-SEC-002";
                        result.WarningTitle = "Possible Mirror Website Detected";
                        result.WarningMessage = $"This website may be impersonating {legitimate}. The URL looks similar but is not the official site.";
                        result.ThreatType = SecurityThreatType.MirrorSite;
                        result.RiskScore = 85;
                        result.Issues.Add($"Possible typosquatting of {legitimate}");
                        return;
                    }
                }
            }
        }

        // Only check for very obvious phishing patterns - NOT general patterns
        // These must be VERY specific to avoid false positives
        var obviousPhishingPatterns = new[]
        {
            @"^[a-z]+-login-secure-verify\.",  // google-login-secure-verify.com
            @"^secure-[a-z]+-verify\.",        // secure-paypal-verify.com
            @"\.ru/.*login.*password",         // Russian domains with login/password paths
            @"\.tk/.*account.*verify",         // Free TLD with account verification
            @"^[a-z]{3,}-[a-z]{3,}-[a-z]{3,}-[a-z]{3,}\."  // Multiple hyphenated words: verify-your-account-now.com
        };

        foreach (var pattern in obviousPhishingPatterns)
        {
            if (Regex.IsMatch(host + url.Substring(url.IndexOf(host) + host.Length), pattern, RegexOptions.IgnoreCase))
            {
                result.Issues.Add("Highly suspicious URL pattern");
                result.RiskScore = Math.Max(result.RiskScore, 70);
            }
        }

        // Check for IP address URLs (often used in phishing) - but only flag, don't block
        if (Regex.IsMatch(host, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
        {
            // Local IPs are fine
            if (!host.StartsWith("192.168.") && !host.StartsWith("10.") &&
                !host.StartsWith("127.") && !host.StartsWith("172."))
            {
                result.Issues.Add("IP address used instead of domain name");
                result.RiskScore = Math.Max(result.RiskScore, 30); // Lower score - just a warning
            }
        }

        // Only flag excessive subdomains if combined with other issues
        if (host.Split('.').Length > 5) // Raised from 4 to 5
        {
            result.Issues.Add("Many subdomains detected");
            result.RiskScore = Math.Max(result.RiskScore, 15); // Very low score
        }

        // HTTP is common, don't penalize too much
        if (url.StartsWith("http://") && !host.StartsWith("localhost") && !host.StartsWith("127."))
        {
            result.Issues.Add("Unencrypted connection (HTTP)");
            result.RiskScore = Math.Max(result.RiskScore, 10); // Very low score
        }

        // Much higher threshold - only flag if VERY suspicious (score >= 85)
        // This means multiple strong indicators must be present
        if (result.RiskScore >= 85)
        {
            result.IsSafe = false;
            result.WarningCode = "ZARLA-SEC-003";
            result.WarningTitle = "Suspicious Website";
            result.WarningMessage = "This website has multiple suspicious characteristics. Proceed with caution.";
            result.ThreatType = SecurityThreatType.Phishing;
        }
    }

    private void CheckContentPatterns(string content, string title, SecurityScanResult result)
    {
        // Only check for VERY specific scam/phishing phrases that normal sites wouldn't use
        // These must be exact phrases that are clear indicators of scams
        var obviousScamPhrases = new[]
        {
            "your account will be closed in 24 hours",
            "verify your account immediately or it will be suspended",
            "you have won $1,000,000",
            "click here to claim your prize",
            "your computer has been infected with",
            "call this number immediately",
            "microsoft has detected a virus",
            "your ip address has been flagged",
            "enter your social security number",
            "send bitcoin to"
        };

        var contentLower = content.ToLowerInvariant();
        var matchCount = 0;

        foreach (var phrase in obviousScamPhrases)
        {
            if (contentLower.Contains(phrase))
            {
                result.Issues.Add($"Suspicious phrase detected");
                matchCount++;
            }
        }

        // Only flag if multiple scam phrases are found (not just one)
        if (matchCount >= 2)
        {
            result.RiskScore = Math.Max(result.RiskScore, 50);
        }

        // Password forms are normal - only flag if the page has OTHER suspicious elements
        // AND the domain is untrusted AND multiple scam phrases were found
        if (matchCount >= 2 && Regex.IsMatch(content, @"<input[^>]*type=[""']password[""']", RegexOptions.IgnoreCase))
        {
            result.Issues.Add("Password form with suspicious content");
            result.RiskScore = Math.Max(result.RiskScore, 70);
        }
    }

    private async Task PerformAIScanAsync(string url, string host, SecurityScanResult result)
    {
        try
        {
            var prompt = $@"Analyze this URL for security threats. Be concise.

URL: {url}
Host: {host}

Check for:
1. Typosquatting (looks like a famous brand but slightly different spelling)
2. Phishing indicators (suspicious keywords, unusual TLD)
3. Scam patterns

Respond in this exact JSON format only:
{{""safe"": true/false, ""threat"": ""none/mirror/phishing/scam"", ""reason"": ""brief explanation""}}";

            var response = await CallGroqApiAsync(prompt);
            if (response != null)
            {
                ParseAISecurityResponse(response, result);
            }
        }
        catch
        {
            // AI scan failed, keep current result
        }
    }

    private async Task PerformAIContentScanAsync(string url, string content, string title, SecurityScanResult result)
    {
        try
        {
            // Truncate content for API
            var truncatedContent = content.Length > 2000 ? content.Substring(0, 2000) : content;
            
            var prompt = $@"Analyze this webpage for security threats. Be very concise.

URL: {url}
Title: {title}
Content preview: {truncatedContent}

Is this page trying to:
1. Steal credentials (phishing)?
2. Impersonate another website?
3. Distribute malware?
4. Scam users?

Respond in this exact JSON format only:
{{""safe"": true/false, ""threat"": ""none/phishing/impersonation/malware/scam"", ""reason"": ""brief explanation""}}";

            var response = await CallGroqApiAsync(prompt);
            if (response != null)
            {
                ParseAISecurityResponse(response, result);
            }
        }
        catch
        {
            // AI scan failed, keep current result
        }
    }

    private async Task<string?> CallGroqApiAsync(string prompt)
    {
        var request = new
        {
            model = "meta-llama/llama-4-scout-17b-16e-instruct",
            messages = new[]
            {
                new { role = "system", content = "You are a security analyzer. Respond only in valid JSON format." },
                new { role = "user", content = prompt }
            },
            max_tokens = 200,
            temperature = 0.1
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(GroqApiUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            if (chatResponse.TryGetProperty("choices", out var choices) && 
                choices.GetArrayLength() > 0)
            {
                return choices[0].GetProperty("message").GetProperty("content").GetString();
            }
        }
        
        return null;
    }

    private void ParseAISecurityResponse(string response, SecurityScanResult result)
    {
        try
        {
            // Extract JSON from response
            var jsonMatch = Regex.Match(response, @"\{[^}]+\}");
            if (!jsonMatch.Success) return;

            var json = JsonSerializer.Deserialize<JsonElement>(jsonMatch.Value);
            
            if (json.TryGetProperty("safe", out var safeElement) && !safeElement.GetBoolean())
            {
                result.IsSafe = false;
                result.RiskScore = 90;

                var threat = json.TryGetProperty("threat", out var threatElement) 
                    ? threatElement.GetString() ?? "suspicious" 
                    : "suspicious";
                    
                var reason = json.TryGetProperty("reason", out var reasonElement)
                    ? reasonElement.GetString() ?? "Suspicious activity detected"
                    : "Suspicious activity detected";

                result.ThreatType = threat.ToLower() switch
                {
                    "mirror" => SecurityThreatType.MirrorSite,
                    "phishing" => SecurityThreatType.Phishing,
                    "scam" => SecurityThreatType.Scam,
                    "malware" => SecurityThreatType.Malware,
                    "impersonation" => SecurityThreatType.MirrorSite,
                    _ => SecurityThreatType.SuspiciousContent
                };

                result.WarningCode = "ZARLA-SEC-AI1";
                result.WarningTitle = "⚠️ This Website Has Been Flagged by Zarla";
                result.WarningMessage = reason;
                result.Issues.Add($"AI detected: {reason}");
            }
        }
        catch
        {
            // JSON parsing failed, ignore
        }
    }

    private double CalculateSimilarity(string s1, string s2)
    {
        // Levenshtein distance-based similarity
        var distance = LevenshteinDistance(s1.ToLower(), s2.ToLower());
        var maxLen = Math.Max(s1.Length, s2.Length);
        return 1.0 - (double)distance / maxLen;
    }

    private int LevenshteinDistance(string s1, string s2)
    {
        var n = s1.Length;
        var m = s2.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    public void AddToBlocklist(string domain)
    {
        _knownMaliciousDomains.Add(domain.ToLowerInvariant());
    }

    public void AddToTrustedList(string domain)
    {
        _trustedDomains.Add(domain.ToLowerInvariant());
    }

    /// <summary>
    /// Scan a file for threats using VirusTotal (High security only)
    /// </summary>
    public async Task<SecurityScanResult> ScanFileAsync(string filePath)
    {
        var result = new SecurityScanResult();
        
        if (_securityLevel != SecurityLevel.High)
        {
            result.IsSafe = true;
            return result;
        }
        
        try
        {
            var vtResult = await _virusTotal.ScanFileAsync(filePath);
            result.VirusTotalResult = vtResult;
            
            if (vtResult.Success && !vtResult.IsSafe)
            {
                result.IsSafe = false;
                result.ThreatType = SecurityThreatType.Malware;
                result.WarningCode = "ZARLA-SEC-VT2";
                result.WarningTitle = "Malicious File Detected";
                result.WarningMessage = vtResult.ThreatLabel ?? vtResult.Message;
                result.RiskScore = vtResult.ThreatScore;
                result.Issues.AddRange(vtResult.ThreatDetails);
            }
            else if (vtResult.IsUnknown)
            {
                // File not in VT database - treat with caution
                result.IsSafe = true;
                result.Issues.Add("File not found in VirusTotal database");
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Scan error: {ex.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Scan a file by its hash using VirusTotal
    /// </summary>
    public async Task<SecurityScanResult> ScanFileHashAsync(string sha256Hash)
    {
        var result = new SecurityScanResult();
        
        if (_securityLevel != SecurityLevel.High)
        {
            result.IsSafe = true;
            return result;
        }
        
        try
        {
            var vtResult = await _virusTotal.ScanFileHashAsync(sha256Hash);
            result.VirusTotalResult = vtResult;
            
            if (vtResult.Success && !vtResult.IsSafe)
            {
                result.IsSafe = false;
                result.ThreatType = SecurityThreatType.Malware;
                result.WarningCode = "ZARLA-SEC-VT2";
                result.WarningTitle = "Malicious File Detected";
                result.WarningMessage = vtResult.ThreatLabel ?? vtResult.Message;
                result.RiskScore = vtResult.ThreatScore;
                result.Issues.AddRange(vtResult.ThreatDetails);
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Scan error: {ex.Message}");
        }
        
        return result;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public static class SecurityCodes
{
    public static readonly Dictionary<string, (string Title, string Description)> Codes = new()
    {
        ["ZARLA-SEC-001"] = ("Known Malicious Site", "This website is on Zarla's blocklist for distributing malware, phishing, or other malicious activities."),
        ["ZARLA-SEC-002"] = ("Mirror/Typosquatting Site", "This website appears to be impersonating a legitimate website. The URL is similar but not the official domain."),
        ["ZARLA-SEC-003"] = ("Suspicious Website", "This website has multiple suspicious characteristics that may indicate phishing or scam activity."),
        ["ZARLA-SEC-AI1"] = ("AI Flagged Threat", "Zarla's AI security scanner has detected potential threats on this page."),
        ["ZARLA-SEC-VT1"] = ("VirusTotal URL Detection", "This URL was flagged as malicious by multiple security vendors on VirusTotal."),
        ["ZARLA-SEC-VT2"] = ("VirusTotal File Detection", "This file was detected as malware by multiple security vendors on VirusTotal."),
        ["ZARLA-NET-001"] = ("DNS Resolution Failed", "Unable to resolve the domain name. Check your internet connection or the website may not exist."),
        ["ZARLA-NET-002"] = ("Connection Timeout", "The connection to the server timed out. The server may be down or your network may be slow."),
        ["ZARLA-NET-003"] = ("SSL Certificate Error", "The website's security certificate is invalid, expired, or not trusted."),
        ["ZARLA-NET-004"] = ("Proxy Error", "Unable to connect through the configured proxy server. Check your proxy settings."),
        ["ZARLA-BLK-001"] = ("Blocked by Ad Blocker", "This content was blocked by Zarla's ad blocker."),
        ["ZARLA-BLK-002"] = ("Blocked by Tracker Blocker", "This tracking request was blocked to protect your privacy."),
        ["ZARLA-ERR-001"] = ("Page Not Found (404)", "The requested page does not exist on this server."),
        ["ZARLA-ERR-002"] = ("Server Error (500)", "The website's server encountered an error."),
        ["ZARLA-ERR-003"] = ("Access Denied (403)", "You don't have permission to access this resource."),
    };
}
