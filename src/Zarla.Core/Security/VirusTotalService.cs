using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zarla.Core.Security;

/// <summary>
/// VirusTotal API integration for scanning files and URLs
/// Free tier: 4 lookups/min, 500/day, 15.5K/month
/// </summary>
public class VirusTotalService
{
    private const string ApiBaseUrl = "https://www.virustotal.com/api/v3";
    private const int MaxLookupsPerMinute = 4;
    private const int MaxLookupsPerDay = 500;
    private const int MaxLookupsPerMonth = 15500;
    
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly Queue<DateTime> _minuteRequests = new();
    private readonly Queue<DateTime> _dayRequests = new();
    private int _monthlyCount;
    private DateTime _monthStart;
    
    public bool IsEnabled => !string.IsNullOrEmpty(_apiKey);
    public int RemainingMinute => MaxLookupsPerMinute - GetRecentCount(_minuteRequests, TimeSpan.FromMinutes(1));
    public int RemainingDay => MaxLookupsPerDay - GetRecentCount(_dayRequests, TimeSpan.FromDays(1));
    public int RemainingMonth => MaxLookupsPerMonth - _monthlyCount;

    public VirusTotalService(string? apiKey = null)
    {
        _apiKey = apiKey ?? "1cee49111af9aa7f785d026916038a03eb0796e0a7552069f8b4074472164b51";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("x-apikey", _apiKey);
        _monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        LoadUsageStats();
    }

    private void LoadUsageStats()
    {
        try
        {
            var statsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Zarla", "vt_stats.json");
            
            if (File.Exists(statsPath))
            {
                var json = File.ReadAllText(statsPath);
                var stats = JsonSerializer.Deserialize<VTUsageStats>(json);
                if (stats != null && stats.MonthStart.Month == DateTime.UtcNow.Month)
                {
                    _monthlyCount = stats.MonthlyCount;
                    _monthStart = stats.MonthStart;
                }
            }
        }
        catch { /* Ignore stats errors */ }
    }

    private void SaveUsageStats()
    {
        try
        {
            var statsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Zarla", "vt_stats.json");
            
            Directory.CreateDirectory(Path.GetDirectoryName(statsPath)!);
            var stats = new VTUsageStats { MonthStart = _monthStart, MonthlyCount = _monthlyCount };
            File.WriteAllText(statsPath, JsonSerializer.Serialize(stats));
        }
        catch { /* Ignore stats errors */ }
    }

    private int GetRecentCount(Queue<DateTime> queue, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        while (queue.Count > 0 && queue.Peek() < cutoff)
            queue.Dequeue();
        return queue.Count;
    }

    private async Task<bool> WaitForRateLimit()
    {
        // Check monthly limit
        if (DateTime.UtcNow.Month != _monthStart.Month)
        {
            _monthlyCount = 0;
            _monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        }
        
        if (_monthlyCount >= MaxLookupsPerMonth)
            return false;
        
        // Check daily limit
        if (GetRecentCount(_dayRequests, TimeSpan.FromDays(1)) >= MaxLookupsPerDay)
            return false;
        
        // Wait for minute limit if needed
        var minuteCount = GetRecentCount(_minuteRequests, TimeSpan.FromMinutes(1));
        if (minuteCount >= MaxLookupsPerMinute)
        {
            var oldestRequest = _minuteRequests.Peek();
            var waitTime = oldestRequest.AddMinutes(1) - DateTime.UtcNow;
            if (waitTime > TimeSpan.Zero && waitTime < TimeSpan.FromMinutes(1))
            {
                await Task.Delay(waitTime);
            }
        }
        
        return true;
    }

    private void RecordRequest()
    {
        var now = DateTime.UtcNow;
        _minuteRequests.Enqueue(now);
        _dayRequests.Enqueue(now);
        _monthlyCount++;
        SaveUsageStats();
    }

    /// <summary>
    /// Scan a URL for threats
    /// </summary>
    public async Task<VTScanResult> ScanUrlAsync(string url)
    {
        if (!IsEnabled)
            return new VTScanResult { Success = false, Error = "VirusTotal API not configured" };

        if (!await WaitForRateLimit())
            return new VTScanResult { Success = false, Error = "Rate limit exceeded" };

        try
        {
            // First, get URL ID (base64 encoded URL without padding)
            var urlId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(url))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            // Try to get existing analysis
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/urls/{urlId}");
            RecordRequest();

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<VTUrlResponse>();
                if (result?.Data?.Attributes != null)
                {
                    return ParseUrlResult(result.Data.Attributes, url);
                }
            }

            // If not found, submit for analysis
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                if (!await WaitForRateLimit())
                    return new VTScanResult { Success = false, Error = "Rate limit exceeded" };

                var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("url", url) });
                var submitResponse = await _httpClient.PostAsync($"{ApiBaseUrl}/urls", content);
                RecordRequest();

                if (submitResponse.IsSuccessStatusCode)
                {
                    // URL submitted, return pending status
                    return new VTScanResult 
                    { 
                        Success = true, 
                        IsSafe = true, // Assume safe while pending
                        Message = "URL submitted for analysis",
                        IsPending = true
                    };
                }
            }

            return new VTScanResult { Success = false, Error = $"API error: {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new VTScanResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Scan a file by its SHA256 hash
    /// </summary>
    public async Task<VTScanResult> ScanFileHashAsync(string sha256Hash)
    {
        if (!IsEnabled)
            return new VTScanResult { Success = false, Error = "VirusTotal API not configured" };

        if (!await WaitForRateLimit())
            return new VTScanResult { Success = false, Error = "Rate limit exceeded" };

        try
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/files/{sha256Hash}");
            RecordRequest();

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<VTFileResponse>();
                if (result?.Data?.Attributes != null)
                {
                    return ParseFileResult(result.Data.Attributes);
                }
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new VTScanResult 
                { 
                    Success = true, 
                    IsSafe = true, 
                    Message = "File not found in VirusTotal database (likely safe)",
                    IsUnknown = true
                };
            }

            return new VTScanResult { Success = false, Error = $"API error: {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new VTScanResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Scan a file by computing its hash
    /// </summary>
    public async Task<VTScanResult> ScanFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return new VTScanResult { Success = false, Error = "File not found" };

        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = await Task.Run(() => sha256.ComputeHash(stream));
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            
            return await ScanFileHashAsync(hash);
        }
        catch (Exception ex)
        {
            return new VTScanResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Get the SHA256 hash of a file
    /// </summary>
    public static string GetFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private VTScanResult ParseUrlResult(VTUrlAttributes attrs, string url)
    {
        var malicious = attrs.LastAnalysisStats?.Malicious ?? 0;
        var suspicious = attrs.LastAnalysisStats?.Suspicious ?? 0;
        var harmless = attrs.LastAnalysisStats?.Harmless ?? 0;
        var undetected = attrs.LastAnalysisStats?.Undetected ?? 0;
        var total = malicious + suspicious + harmless + undetected;

        var threatScore = total > 0 ? (malicious * 100 + suspicious * 50) / total : 0;
        var isSafe = malicious == 0 && suspicious <= 1;

        var result = new VTScanResult
        {
            Success = true,
            IsSafe = isSafe,
            ThreatScore = threatScore,
            MaliciousCount = malicious,
            SuspiciousCount = suspicious,
            HarmlessCount = harmless,
            TotalEngines = total,
            Categories = attrs.Categories?.Values.ToList() ?? new List<string>(),
            Message = isSafe 
                ? $"URL appears safe ({harmless}/{total} engines)" 
                : $"⚠️ Threat detected: {malicious} malicious, {suspicious} suspicious"
        };

        // Add threat details
        if (!isSafe && attrs.LastAnalysisResults != null)
        {
            result.ThreatDetails = attrs.LastAnalysisResults
                .Where(r => r.Value.Category == "malicious" || r.Value.Category == "suspicious")
                .Select(r => $"{r.Key}: {r.Value.Result}")
                .Take(5)
                .ToList();
        }

        return result;
    }

    private VTScanResult ParseFileResult(VTFileAttributes attrs)
    {
        var malicious = attrs.LastAnalysisStats?.Malicious ?? 0;
        var suspicious = attrs.LastAnalysisStats?.Suspicious ?? 0;
        var harmless = attrs.LastAnalysisStats?.Harmless ?? 0;
        var undetected = attrs.LastAnalysisStats?.Undetected ?? 0;
        var total = malicious + suspicious + harmless + undetected;

        var threatScore = total > 0 ? (malicious * 100 + suspicious * 50) / total : 0;
        var isSafe = malicious == 0 && suspicious <= 2;

        var result = new VTScanResult
        {
            Success = true,
            IsSafe = isSafe,
            ThreatScore = threatScore,
            MaliciousCount = malicious,
            SuspiciousCount = suspicious,
            HarmlessCount = harmless,
            TotalEngines = total,
            FileName = attrs.MeaningfulName ?? attrs.Names?.FirstOrDefault(),
            FileType = attrs.TypeDescription,
            FileSize = attrs.Size,
            Message = isSafe 
                ? $"File appears safe ({harmless}/{total} engines)" 
                : $"⚠️ DANGER: {malicious} malicious, {suspicious} suspicious detections"
        };

        // Add threat names
        if (!isSafe && attrs.LastAnalysisResults != null)
        {
            result.ThreatDetails = attrs.LastAnalysisResults
                .Where(r => r.Value.Category == "malicious" || r.Value.Category == "suspicious")
                .Select(r => $"{r.Key}: {r.Value.Result}")
                .Take(10)
                .ToList();
        }

        // Popular threat label
        if (!string.IsNullOrEmpty(attrs.PopularThreatClassification?.SuggestedThreatLabel))
        {
            result.ThreatLabel = attrs.PopularThreatClassification.SuggestedThreatLabel;
        }

        return result;
    }
}

public class VTScanResult
{
    public bool Success { get; set; }
    public bool IsSafe { get; set; }
    public bool IsPending { get; set; }
    public bool IsUnknown { get; set; }
    public int ThreatScore { get; set; }
    public int MaliciousCount { get; set; }
    public int SuspiciousCount { get; set; }
    public int HarmlessCount { get; set; }
    public int TotalEngines { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public long FileSize { get; set; }
    public string? ThreatLabel { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<string> ThreatDetails { get; set; } = new();
}

internal class VTUsageStats
{
    public DateTime MonthStart { get; set; }
    public int MonthlyCount { get; set; }
}

// API Response Models
internal class VTUrlResponse
{
    [JsonPropertyName("data")]
    public VTUrlData? Data { get; set; }
}

internal class VTUrlData
{
    [JsonPropertyName("attributes")]
    public VTUrlAttributes? Attributes { get; set; }
}

internal class VTUrlAttributes
{
    [JsonPropertyName("last_analysis_stats")]
    public VTAnalysisStats? LastAnalysisStats { get; set; }
    
    [JsonPropertyName("last_analysis_results")]
    public Dictionary<string, VTAnalysisResult>? LastAnalysisResults { get; set; }
    
    [JsonPropertyName("categories")]
    public Dictionary<string, string>? Categories { get; set; }
}

internal class VTFileResponse
{
    [JsonPropertyName("data")]
    public VTFileData? Data { get; set; }
}

internal class VTFileData
{
    [JsonPropertyName("attributes")]
    public VTFileAttributes? Attributes { get; set; }
}

internal class VTFileAttributes
{
    [JsonPropertyName("last_analysis_stats")]
    public VTAnalysisStats? LastAnalysisStats { get; set; }
    
    [JsonPropertyName("last_analysis_results")]
    public Dictionary<string, VTAnalysisResult>? LastAnalysisResults { get; set; }
    
    [JsonPropertyName("meaningful_name")]
    public string? MeaningfulName { get; set; }
    
    [JsonPropertyName("names")]
    public List<string>? Names { get; set; }
    
    [JsonPropertyName("type_description")]
    public string? TypeDescription { get; set; }
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
    
    [JsonPropertyName("popular_threat_classification")]
    public VTThreatClassification? PopularThreatClassification { get; set; }
}

internal class VTAnalysisStats
{
    [JsonPropertyName("malicious")]
    public int Malicious { get; set; }
    
    [JsonPropertyName("suspicious")]
    public int Suspicious { get; set; }
    
    [JsonPropertyName("harmless")]
    public int Harmless { get; set; }
    
    [JsonPropertyName("undetected")]
    public int Undetected { get; set; }
}

internal class VTAnalysisResult
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }
    
    [JsonPropertyName("result")]
    public string? Result { get; set; }
}

internal class VTThreatClassification
{
    [JsonPropertyName("suggested_threat_label")]
    public string? SuggestedThreatLabel { get; set; }
}
