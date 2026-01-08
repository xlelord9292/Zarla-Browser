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
        // Always trusted domains (skip scanning)
        var trusted = new[]
        {
            "google.com", "youtube.com", "facebook.com", "amazon.com",
            "microsoft.com", "apple.com", "github.com", "twitter.com",
            "x.com", "reddit.com", "wikipedia.org", "linkedin.com",
            "netflix.com", "spotify.com", "twitch.tv", "discord.com",
            "instagram.com", "whatsapp.com", "telegram.org", "tiktok.com",
            "paypal.com", "ebay.com", "walmart.com", "target.com",
            "chase.com", "bankofamerica.com", "wellsfargo.com", "capitalone.com",
            "stackoverflow.com", "cloudflare.com", "mozilla.org"
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
        // Check for typosquatting/mirror sites
        foreach (var (brand, legitimate) in _legitimateDomains)
        {
            if (host.Contains(brand) && !host.EndsWith(legitimate) && !host.EndsWith("." + legitimate))
            {
                // Possible mirror/typosquatting
                var similarity = CalculateSimilarity(host, legitimate);
                if (similarity > 0.5 && similarity < 1.0)
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

        // Check for suspicious URL patterns
        var suspiciousPatterns = new[]
        {
            @"login.*secure.*verify",
            @"account.*confirm.*update",
            @"password.*reset.*urgent",
            @"bank.*verify.*account",
            @"paypal.*confirm.*identity",
            @"\d{5,}",  // Long number sequences
            @"[a-z]{20,}",  // Very long subdomains
            @"(verify|secure|login|account|update|confirm|password){2,}"  // Multiple suspicious keywords
        };

        foreach (var pattern in suspiciousPatterns)
        {
            if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
            {
                result.Issues.Add($"Suspicious URL pattern detected");
                result.RiskScore = Math.Max(result.RiskScore, 40);
            }
        }

        // Check for IP address URLs (often used in phishing)
        if (Regex.IsMatch(host, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
        {
            result.Issues.Add("IP address used instead of domain name");
            result.RiskScore = Math.Max(result.RiskScore, 50);
        }

        // Check for excessive subdomains
        if (host.Split('.').Length > 4)
        {
            result.Issues.Add("Excessive subdomains");
            result.RiskScore = Math.Max(result.RiskScore, 30);
        }

        // Check HTTP (not HTTPS)
        if (url.StartsWith("http://") && !host.StartsWith("localhost"))
        {
            result.Issues.Add("Insecure connection (HTTP)");
            result.RiskScore = Math.Max(result.RiskScore, 20);
        }

        // If risk score is high enough, flag as unsafe
        if (result.RiskScore >= 60)
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
        var suspiciousContent = new[]
        {
            "verify your account immediately",
            "your account has been suspended",
            "confirm your identity",
            "enter your password",
            "update your payment",
            "unusual activity detected",
            "verify your credit card",
            "click here to claim",
            "you have won",
            "act now before",
            "limited time offer",
            "your computer is infected"
        };

        var contentLower = content.ToLowerInvariant();
        foreach (var pattern in suspiciousContent)
        {
            if (contentLower.Contains(pattern))
            {
                result.Issues.Add($"Suspicious content: '{pattern}'");
                result.RiskScore = Math.Max(result.RiskScore, 30);
            }
        }

        // Check for login forms on suspicious pages
        if (Regex.IsMatch(content, @"<input[^>]*type=[""']password[""']", RegexOptions.IgnoreCase))
        {
            if (result.Issues.Count > 0)
            {
                result.Issues.Add("Password form on suspicious page");
                result.RiskScore = Math.Max(result.RiskScore, 60);
            }
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
