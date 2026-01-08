using System.Text.Json;

namespace Zarla.Core.AI;

public class AIUsageTracker
{
    private readonly string _usageFilePath;
    private Dictionary<string, ModelUsage> _usage = new();
    private string? _bypassCode;
    private bool _bypassActive;

    // Hidden bypass code - stored in app config
    private const string DefaultBypassCode = "ZARLA-PRO-2024-UNLIMITED";

    public AIUsageTracker(string dataFolder)
    {
        _usageFilePath = Path.Combine(dataFolder, "ai_usage.json");
        LoadUsage();
    }

    public void SetBypassCode(string? code)
    {
        _bypassCode = code;
        _bypassActive = !string.IsNullOrEmpty(code) &&
                        (code == DefaultBypassCode || code == _bypassCode);
    }

    public bool ValidateBypassCode(string code)
    {
        return code == DefaultBypassCode || code == _bypassCode;
    }

    public bool IsBypassActive => _bypassActive;

    public UsageCheckResult CanUseModel(string modelId, int dailyLimit)
    {
        // Bypass active - unlimited usage
        if (_bypassActive)
        {
            return new UsageCheckResult
            {
                CanUse = true,
                RemainingUses = -1, // Unlimited
                ResetTime = null
            };
        }

        var key = GetUsageKey(modelId);

        if (!_usage.TryGetValue(key, out var usage))
        {
            return new UsageCheckResult
            {
                CanUse = true,
                RemainingUses = dailyLimit,
                ResetTime = DateTime.UtcNow.Date.AddDays(1)
            };
        }

        // Check if we need to reset (24 hours passed)
        if (DateTime.UtcNow >= usage.ResetAt)
        {
            usage.Count = 0;
            usage.ResetAt = DateTime.UtcNow.Date.AddDays(1);
            SaveUsage();
        }

        var remaining = dailyLimit - usage.Count;
        var canUse = remaining > 0;

        return new UsageCheckResult
        {
            CanUse = canUse,
            RemainingUses = Math.Max(0, remaining),
            ResetTime = usage.ResetAt,
            TimeUntilReset = canUse ? null : usage.ResetAt - DateTime.UtcNow
        };
    }

    public void RecordUsage(string modelId)
    {
        if (_bypassActive) return;

        var key = GetUsageKey(modelId);

        if (!_usage.TryGetValue(key, out var usage))
        {
            usage = new ModelUsage
            {
                ModelId = modelId,
                Count = 0,
                ResetAt = DateTime.UtcNow.Date.AddDays(1)
            };
            _usage[key] = usage;
        }

        // Check if we need to reset
        if (DateTime.UtcNow >= usage.ResetAt)
        {
            usage.Count = 0;
            usage.ResetAt = DateTime.UtcNow.Date.AddDays(1);
        }

        usage.Count++;
        usage.LastUsed = DateTime.UtcNow;
        SaveUsage();
    }

    public Dictionary<string, ModelUsage> GetAllUsage()
    {
        return new Dictionary<string, ModelUsage>(_usage);
    }

    private string GetUsageKey(string modelId)
    {
        return $"{modelId}_{DateTime.UtcNow:yyyy-MM-dd}";
    }

    private void LoadUsage()
    {
        try
        {
            if (File.Exists(_usageFilePath))
            {
                var json = File.ReadAllText(_usageFilePath);
                var data = JsonSerializer.Deserialize<UsageData>(json);
                if (data != null)
                {
                    _usage = data.Usage ?? new();
                    _bypassCode = data.BypassCode;
                    _bypassActive = !string.IsNullOrEmpty(_bypassCode) &&
                                    (_bypassCode == DefaultBypassCode);
                }
            }
        }
        catch
        {
            _usage = new();
        }
    }

    private void SaveUsage()
    {
        try
        {
            var directory = Path.GetDirectoryName(_usageFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = new UsageData
            {
                Usage = _usage,
                BypassCode = _bypassCode
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_usageFilePath, json);
        }
        catch
        {
            // Silently fail - usage tracking is non-critical
        }
    }
}

public class ModelUsage
{
    public string ModelId { get; set; } = "";
    public int Count { get; set; }
    public DateTime ResetAt { get; set; }
    public DateTime? LastUsed { get; set; }
}

public class UsageCheckResult
{
    public bool CanUse { get; set; }
    public int RemainingUses { get; set; }
    public DateTime? ResetTime { get; set; }
    public TimeSpan? TimeUntilReset { get; set; }
}

public class UsageData
{
    public Dictionary<string, ModelUsage>? Usage { get; set; }
    public string? BypassCode { get; set; }
}
