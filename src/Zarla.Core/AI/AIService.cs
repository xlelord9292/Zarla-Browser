using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Zarla.Core.AI;

public class AIService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private const string DefaultBaseUrl = "https://api.groq.com/openai/v1/chat/completions";

    // Conversation memory for context
    private readonly List<ChatMessage> _conversationHistory = new();
    private const int MaxHistoryMessages = 20;

    public AIService(string apiKey, string? customBaseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = !string.IsNullOrWhiteSpace(customBaseUrl) ? customBaseUrl : DefaultBaseUrl;

        // Ensure the URL ends with chat/completions for OpenAI-compatible APIs
        if (!_baseUrl.EndsWith("/chat/completions") && !_baseUrl.EndsWith("/chat/completions/"))
        {
            _baseUrl = _baseUrl.TrimEnd('/') + "/chat/completions";
        }

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Zarla/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Searches the web and returns relevant content for the AI to use
    /// </summary>
    public async Task<string?> SearchWebAsync(string query)
    {
        try
        {
            var results = new List<SearchResult>();

            // Try DuckDuckGo HTML search first
            try
            {
                var searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                request.Headers.Add("Accept", "text/html");
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    results = ParseDuckDuckGoResults(html);
                }
            }
            catch { }

            // Fallback: Try Brave Search API (free tier available)
            if (results.Count == 0)
            {
                try
                {
                    var braveUrl = $"https://search.brave.com/api/suggest?q={Uri.EscapeDataString(query)}";
                    var braveResponse = await _httpClient.GetStringAsync(braveUrl);
                    // Parse suggestions as backup
                }
                catch { }
            }

            // Fallback: Use simple web scraping from Google (limited)
            if (results.Count == 0)
            {
                try
                {
                    var googleUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}&hl=en";
                    using var request = new HttpRequestMessage(HttpMethod.Get, googleUrl);
                    request.Headers.Add("Accept", "text/html");

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var html = await response.Content.ReadAsStringAsync();
                        results = ParseGoogleResults(html);
                    }
                }
                catch { }
            }

            if (results.Count == 0)
                return null;

            // Build search results content
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("=== Web Search Results ===\n");

            int fetchedCount = 0;
            foreach (var result in results.Take(5))
            {
                contentBuilder.AppendLine($"**{result.Title}**");
                if (!string.IsNullOrEmpty(result.Url))
                    contentBuilder.AppendLine($"URL: {result.Url}");
                if (!string.IsNullOrEmpty(result.Snippet))
                    contentBuilder.AppendLine($"Snippet: {result.Snippet}");

                // Try to fetch full page content for top 2 results
                if (fetchedCount < 2 && !string.IsNullOrEmpty(result.Url))
                {
                    try
                    {
                        var pageContent = await FetchPageContentAsync(result.Url);
                        if (!string.IsNullOrEmpty(pageContent) && pageContent.Length > 100)
                        {
                            contentBuilder.AppendLine($"Content: {pageContent}");
                            fetchedCount++;
                        }
                    }
                    catch { }
                }
                contentBuilder.AppendLine();
            }

            var resultText = contentBuilder.ToString();
            return resultText.Length > 100 ? resultText : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Web search failed: {ex.Message}");
            return null;
        }
    }

    private List<SearchResult> ParseGoogleResults(string html)
    {
        var results = new List<SearchResult>();

        try
        {
            // Find result blocks
            var pattern = new Regex(
                @"<a[^>]+href=""(/url\?q=|)(https?://[^""&]+)[^""]*""[^>]*>([^<]+)</a>",
                RegexOptions.IgnoreCase);

            var matches = pattern.Matches(html);
            foreach (Match match in matches.Take(10))
            {
                var url = match.Groups[2].Value;
                var title = System.Net.WebUtility.HtmlDecode(match.Groups[3].Value.Trim());

                // Filter out Google's own URLs and empty titles
                if (!url.Contains("google.com") && !string.IsNullOrWhiteSpace(title) && title.Length > 3)
                {
                    results.Add(new SearchResult
                    {
                        Url = url,
                        Title = title,
                        Snippet = ""
                    });
                }
            }
        }
        catch { }

        return results.Take(5).ToList();
    }

    /// <summary>
    /// Fetches and extracts text content from a URL
    /// </summary>
    public async Task<string?> FetchPageContentAsync(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetStringAsync(url, cts.Token);

            // Extract text content from HTML
            var text = ExtractTextFromHtml(response);

            // Limit content length
            if (text.Length > 3000)
                text = text.Substring(0, 3000) + "...";

            return text;
        }
        catch
        {
            return null;
        }
    }

    private List<SearchResult> ParseDuckDuckGoResults(string html)
    {
        var results = new List<SearchResult>();

        try
        {
            // Match result blocks - DuckDuckGo HTML format
            var resultPattern = new Regex(
                @"<a[^>]+class=""result__a""[^>]+href=""([^""]+)""[^>]*>([^<]+)</a>",
                RegexOptions.IgnoreCase);

            var snippetPattern = new Regex(
                @"<a[^>]+class=""result__snippet""[^>]*>([^<]+)</a>",
                RegexOptions.IgnoreCase);

            var resultMatches = resultPattern.Matches(html);
            var snippetMatches = snippetPattern.Matches(html);

            for (int i = 0; i < resultMatches.Count && i < 10; i++)
            {
                var match = resultMatches[i];
                var url = match.Groups[1].Value;

                // DuckDuckGo uses redirect URLs, extract actual URL
                if (url.Contains("uddg="))
                {
                    var uddgMatch = Regex.Match(url, @"uddg=([^&]+)");
                    if (uddgMatch.Success)
                    {
                        url = Uri.UnescapeDataString(uddgMatch.Groups[1].Value);
                    }
                }

                var snippet = i < snippetMatches.Count
                    ? System.Net.WebUtility.HtmlDecode(snippetMatches[i].Groups[1].Value.Trim())
                    : "";

                results.Add(new SearchResult
                {
                    Url = url,
                    Title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim()),
                    Snippet = snippet
                });
            }

            // Fallback: simpler pattern for links
            if (results.Count == 0)
            {
                var simplePattern = new Regex(
                    @"<a[^>]+href=""(https?://[^""]+)""[^>]*>([^<]+)</a>",
                    RegexOptions.IgnoreCase);

                var simpleMatches = simplePattern.Matches(html);
                foreach (Match match in simpleMatches.Cast<Match>().Take(10))
                {
                    var url = match.Groups[1].Value;
                    if (!url.Contains("duckduckgo.com") && !url.Contains("duck.co"))
                    {
                        results.Add(new SearchResult
                        {
                            Url = url,
                            Title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim()),
                            Snippet = ""
                        });
                    }
                }
            }
        }
        catch { }

        return results.Take(5).ToList();
    }

    private string ExtractTextFromHtml(string html)
    {
        // Remove script and style tags
        html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<head[^>]*>[\s\S]*?</head>", "", RegexOptions.IgnoreCase);

        // Remove HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ");

        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Clean up whitespace
        html = Regex.Replace(html, @"\s+", " ").Trim();

        return html;
    }

    /// <summary>
    /// Clears conversation history
    /// </summary>
    public void ClearHistory()
    {
        _conversationHistory.Clear();
    }

    /// <summary>
    /// Adds context to the AI's memory
    /// </summary>
    public void AddToMemory(string role, string content)
    {
        _conversationHistory.Add(new ChatMessage { Role = role, Content = content });

        // Keep history bounded
        while (_conversationHistory.Count > MaxHistoryMessages)
        {
            _conversationHistory.RemoveAt(0);
        }
    }

    public async Task<AIResponse> SendMessageAsync(string message, string model, string? systemPrompt = null, bool useHistory = true, bool searchWeb = false)
    {
        try
        {
            var messages = new List<ChatMessage>();

            // Add system prompt
            var defaultSystemPrompt = @"You are Zarla AI, a helpful assistant built into the Zarla web browser.
You have access to web search results when needed. Today's date is " + DateTime.Now.ToString("MMMM dd, yyyy") + @".
Be helpful, accurate, and concise. If you're asked about current events or recent information, use the web search results provided.";

            messages.Add(new ChatMessage
            {
                Role = "system",
                Content = systemPrompt ?? defaultSystemPrompt
            });

            // Add conversation history for context
            if (useHistory)
            {
                messages.AddRange(_conversationHistory);
            }

            // If web search is requested, perform search first
            string? webSearchContext = null;
            if (searchWeb)
            {
                webSearchContext = await SearchWebAsync(message);
                if (!string.IsNullOrEmpty(webSearchContext))
                {
                    message = $"Based on the following web search results, please answer my question.\n\n{webSearchContext}\n\nQuestion: {message}";
                }
            }

            messages.Add(new ChatMessage { Role = "user", Content = message });

            var request = new ChatRequest
            {
                Model = model,
                Messages = messages,
                MaxTokens = 4096,
                Temperature = 0.7
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_baseUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBytes = await response.Content.ReadAsByteArrayAsync();
                var errorContent = Encoding.UTF8.GetString(errorBytes);
                return new AIResponse
                {
                    Success = false,
                    Error = $"API Error: {response.StatusCode} - {errorContent}"
                };
            }

            // Read as bytes and decode as UTF-8 to properly handle emojis
            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            var responseJson = Encoding.UTF8.GetString(responseBytes);
            var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            if (chatResponse?.Choices?.Count > 0)
            {
                var responseContent = chatResponse.Choices[0].Message?.Content ?? "";

                // Add to conversation history
                if (useHistory)
                {
                    AddToMemory("user", message);
                    AddToMemory("assistant", responseContent);
                }

                return new AIResponse
                {
                    Success = true,
                    Content = responseContent,
                    TokensUsed = chatResponse.Usage?.TotalTokens ?? 0,
                    UsedWebSearch = searchWeb && !string.IsNullOrEmpty(webSearchContext)
                };
            }

            return new AIResponse
            {
                Success = false,
                Error = "No response from AI model"
            };
        }
        catch (TaskCanceledException)
        {
            return new AIResponse
            {
                Success = false,
                Error = "Request timed out"
            };
        }
        catch (Exception ex)
        {
            return new AIResponse
            {
                Success = false,
                Error = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Sends a message with automatic web search when the query seems to need current information
    /// </summary>
    public async Task<AIResponse> SendMessageWithAutoSearchAsync(string message, string model, string? pageContent = null)
    {
        // Detect if the query needs web search (current events, recent info, etc.)
        bool needsSearch = ShouldSearchWeb(message);

        string fullPrompt;
        if (!string.IsNullOrEmpty(pageContent))
        {
            fullPrompt = $"{message}\n\nCurrent page content:\n{pageContent}";
        }
        else
        {
            fullPrompt = message;
        }

        return await SendMessageAsync(fullPrompt, model, searchWeb: needsSearch);
    }

    private bool ShouldSearchWeb(string query)
    {
        var lowerQuery = query.ToLower();

        // Keywords that suggest need for current/recent information
        var searchTriggers = new[]
        {
            "current", "latest", "recent", "today", "now", "2024", "2025", "2026",
            "who is the", "what is the", "when did", "news", "price",
            "president", "weather", "score", "result", "update",
            "how much", "where can i", "best", "top", "new"
        };

        return searchTriggers.Any(trigger => lowerQuery.Contains(trigger));
    }

    public async Task<AIResponse> SummarizeContentAsync(string content, string model)
    {
        var systemPrompt = @"You are a helpful assistant that summarizes web content.
Provide a clear, concise summary of the content provided.
Focus on the main points and key information.
Use bullet points when appropriate.
Keep the summary under 500 words unless the content is very complex.";

        var message = $"Please summarize the following web page content:\n\n{content}";

        return await SendMessageAsync(message, model, systemPrompt);
    }

    public async Task<AIResponse> AnalyzePageAsync(string content, string query, string model)
    {
        var systemPrompt = @"You are a helpful assistant that analyzes web content.
Answer the user's question based on the provided web page content.
Be accurate and cite specific parts of the content when relevant.
If the answer cannot be found in the content, say so clearly.";

        var message = $"Web page content:\n{content}\n\nUser question: {query}";

        return await SendMessageAsync(message, model, systemPrompt);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public class AIResponse
{
    public bool Success { get; set; }
    public string Content { get; set; } = "";
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    public bool UsedWebSearch { get; set; }
}

public class SearchResult
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Snippet { get; set; } = "";
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}

public class ChatResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public TokenUsage? Usage { get; set; }
}

public class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }
}

public class TokenUsage
{
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
