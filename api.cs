using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Api
{
    // Latest stable general text model right now
    private const string ModelName = "gemini-3.5-flash";

    // If you truly want Google's auto-updating alias instead, use:
    // private const string ModelName = "gemini-flash-latest";
    // But for a game, stable is safer.

    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private static readonly string GenerateEndpoint =
        $"{BaseUrl}/models/{ModelName}:generateContent";

    private const string CacheEndpoint =
        $"{BaseUrl}/cachedContents";

    private static readonly HttpClient Client = new HttpClient();
    private static readonly SemaphoreSlim CacheLock = new SemaphoreSlim(1, 1);

    private static string? cachedContentName;
    private static string? cachedFilesHash;
    private static DateTimeOffset cacheExpiresAt = DateTimeOffset.MinValue;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public static async Task<string> Play(Board game, string command)
    {
        string apiKey = GetApiKey();

        // Creates/reuses cache for rulebook.json + cards.json
        string cacheName = await GetOrCreateCache(apiKey);

        string handStr = "[" + string.Join(", ",
            game.e.hand.Select((x, index) =>
            {
                int cost = x is Unit u ? u.cost : 0;

                return $@"{{
                    ""selected_index"": {index},
                    ""name"": ""{Escape(x.name)}"",
                    ""id"": {x.id},
                    ""cost"": {cost}
                }}";
            })) + "]";

        string deckStr = "[" + string.Join(", ",
            game.e.deck.Select(x =>
                $@"{{ ""name"": ""{Escape(x.name)}"", ""id"": {x.id} }}"
            )) + "]";

        string prompt = $@"
Current SKYTHRONE game state:

Hand:
{handStr}

Deck:
{deckStr}

Current energy:
{game.e.energy}

Objective:
{command}

Choose which cards to play.

Rules:
- You may only choose cards from the hand above.
- selected_index must match the card position in the hand.
- selected_id must match that card's id.
- mana_cost must match that card's cost.
- Total mana_cost of all selected cards must be <= current energy.
- After playing a card, subtract its mana_cost from remaining energy.
- Stop when there is no valid useful card to play.
- Return ONLY valid JSON.
- No markdown.
- No explanation.

Required response format:
[
  {{
    ""selected_index"": 0,
    ""selected_id"": 123,
    ""mana_cost"": 2
  }}
]
";

        object requestBody = CreateGenerateBody(cacheName, prompt);

        string responseJson;

        try
        {
            responseJson = await PostJson(apiKey, GenerateEndpoint, requestBody);
        }
        catch
        {
            // Cache might have expired server-side.
            cachedContentName = null;
            cacheName = await GetOrCreateCache(apiKey);

            requestBody = CreateGenerateBody(cacheName, prompt);
            responseJson = await PostJson(apiKey, GenerateEndpoint, requestBody);
        }

        using JsonDocument doc = JsonDocument.Parse(responseJson);
        JsonElement root = doc.RootElement;

        PrintUsage(root);

        string text = ExtractGeminiText(root);

        return text
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();
    }

    private static object CreateGenerateBody(string cacheName, string prompt)
    {
        return new
        {
            cachedContent = cacheName,
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.0,
                maxOutputTokens = 512,
                responseMimeType = "application/json"
            }
        };
    }

    private static async Task<string> GetOrCreateCache(string apiKey)
    {
        string rules = File.ReadAllText("rulebook.json");
        string cards = File.ReadAllText("cards.json");

        string currentHash = HashText(rules + "\n---CARDS---\n" + cards);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (cachedContentName != null &&
            cachedFilesHash == currentHash &&
            now < cacheExpiresAt)
        {
            return cachedContentName;
        }

        await CacheLock.WaitAsync();

        try
        {
            now = DateTimeOffset.UtcNow;

            if (cachedContentName != null &&
                cachedFilesHash == currentHash &&
                now < cacheExpiresAt)
            {
                return cachedContentName;
            }

            string cachedText = $@"
SKYTHRONE RULEBOOK JSON:
{rules}

SKYTHRONE CARDS JSON:
{cards}
";

            object cacheBody = new
            {
                model = $"models/{ModelName}",
                displayName = "skythrone-rules-cards",
                ttl = $"{(int)CacheTtl.TotalSeconds}s",

                systemInstruction = new
                {
                    role = "system",
                    parts = new[]
                    {
                        new
                        {
                            text = @"
You are a SKYTHRONE card-game AI.
Use the cached rulebook and cards as the source of truth.

When choosing cards:
- obey energy exactly
- only choose cards from the current hand
- never invent cards
- never exceed available energy
- return only valid JSON
"
                        }
                    }
                },

                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = cachedText }
                        }
                    }
                }
            };

            string responseJson = await PostJson(apiKey, CacheEndpoint, cacheBody);

            using JsonDocument doc = JsonDocument.Parse(responseJson);
            JsonElement root = doc.RootElement;

            string? name = root.GetProperty("name").GetString();

            if (string.IsNullOrWhiteSpace(name))
                throw new Exception("Cache created, but Gemini did not return a cache name.");

            cachedContentName = name;
            cachedFilesHash = currentHash;

            // Refresh before real expiry to avoid using dead cache
            cacheExpiresAt = DateTimeOffset.UtcNow.Add(CacheTtl).AddMinutes(-5);

            Console.WriteLine($"Created Gemini cache: {cachedContentName}");

            return cachedContentName;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static async Task<string> PostJson(string apiKey, string endpoint, object body)
    {
        string url = $"{endpoint}?key={apiKey}";
        string json = JsonSerializer.Serialize(body);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await Client.PostAsync(url, content);

        string responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Gemini request failed:");
            Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}");
            Console.WriteLine(responseText);

            throw new Exception(responseText);
        }

        return responseText;
    }

    private static string ExtractGeminiText(JsonElement root)
    {
        return root
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";
    }

    private static void PrintUsage(JsonElement root)
    {
        if (root.TryGetProperty("usageMetadata", out JsonElement usage))
        {
            Console.WriteLine("Usage metadata:");
            Console.WriteLine(usage.ToString());
        }
    }

    private static string GetApiKey()
    {
        string? apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("Missing GEMINI_API_KEY environment variable.");

        return apiKey;
    }

    private static string HashText(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}