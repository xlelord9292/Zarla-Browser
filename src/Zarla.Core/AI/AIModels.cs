namespace Zarla.Core.AI;

public class AIModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public int DailyLimit { get; set; } = 25; // Default daily limit
    public string? Description { get; set; }
}

public static class BuiltInModels
{
    public static readonly List<AIModel> Models = new()
    {
        new AIModel
        {
            Id = "openai/gpt-oss-120b",
            Name = "GPT-OSS 120B",
            Provider = "OpenAI",
            IsBuiltIn = true,
            DailyLimit = 10,
            Description = "Powerful 120B parameter model for complex tasks"
        },
        new AIModel
        {
            Id = "openai/gpt-oss-20b",
            Name = "GPT-OSS 20B",
            Provider = "OpenAI",
            IsBuiltIn = true,
            DailyLimit = 25,
            Description = "Balanced 20B model for general use"
        },
        new AIModel
        {
            Id = "meta-llama/llama-4-scout-17b-16e-instruct",
            Name = "Llama 4 Scout 17B",
            Provider = "Meta",
            IsBuiltIn = true,
            DailyLimit = 30,
            Description = "Fast and efficient instruction-following model"
        }
    };

    public static AIModel? GetModel(string id)
    {
        return Models.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}

public class CustomModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ModelId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public int DailyLimit { get; set; } = 50;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
