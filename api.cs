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
    private const string Model = "models/gemini-2.5-flash";

    private const string GenerateEndpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    private const string CacheEndpoint =
        "https://generativelanguage.googleapis.com/v1beta/cachedContents";

    private static readonly HttpClient Client = new HttpClient();
    private static readonly SemaphoreSlim CacheLock = new SemaphoreSlim(1, 1);

    private static string? cachedContentName = null;
    private static string? cachedFilesHash = null;
    private static DateTimeOffset cacheExpiresAt = DateTimeOffset.MinValue;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public static async Task<string> Play(Board game, string command)
    {
        string apiKey = GetApiKey();

        string cacheName = await GetOrCreateCache(apiKey);

        string handStr = "[" + string.Join(", ",
            game.e.hand.Select((x, index) =>
            {
                int cost = x is Unit u ? u.cost : 0;
                return $"{{ index: {index}, name: \"{x.name}\", id: {x.id}, cost: {cost} }}";
            })) + "]";

        string deckStr = "[" + string.Join(", ",
            game.e.deck.Select(x => $"{{ name: \"{x.name}\", id: {x.id} }}")) + "]";

        string prompt = $@"
You are choosing cards to play in SKYTHRONE.

Current hand:
{handStr}

Current deck:
{deckStr}

Current energy:
{game.e.energy}

Objective:
{command}

Rules:
- You may only play cards from the current hand.
- selected_index must match the card's position in the hand.
- selected_id must match the selected card id.
- Total mana_cost must never exceed current energy.
- After each played card, subtract its cost from remaining energy.
- Stop when there is no useful valid play or not enough energy.
- Return only a JSON array.
- Do not return markdown.
- Do not explain.

Required JSON format:
[
  {{
    ""selected_index"": 0,
    ""selected_id"": 123,
    ""mana_cost"": 2
  }}
]
";

        var requestBody = new
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
                temperature = 0.1,
                maxOutputTokens = 512,
                responseMimeType = "application/json"
            }
        };

        string responseJson;

        try
        {
            responseJson = await PostJson(apiKey, GenerateEndpoint, requestBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Gemini request failed:");
            Console.WriteLine(ex.Message);

            // Cache may have expired server-side. Recreate once and retry.
            cachedContentName = null;
            cacheName = await GetOrCreateCache(apiKey);

            requestBody = new
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
                    temperature = 0.1,
                    maxOutputTokens = 512,
                    responseMimeType = "application/json"
                }
            };

            responseJson = await PostJson(apiKey, GenerateEndpoint, requestBody);
        }

        using JsonDocument doc = JsonDocument.Parse(responseJson);
        JsonElement root = doc.RootElement;

        PrintUsage(root);

        string text = ExtractGeminiText(root);

        string cleaned = text
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        Console.WriteLine("Model response:");
        Console.WriteLine(cleaned);

        return cleaned;
    }

    private static async Task<string> GetOrCreateCache(string apiKey)
    {
        string rules = File.ReadAllText("rulebook.json");
        string cards = File.ReadAllText("cards.json");

        string currentHash = HashText(rules + "\n---CARDS---\n" + cards);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        bool cacheStillValid =
            cachedContentName != null &&
            cachedFilesHash == currentHash &&
            now < cacheExpiresAt;

        if (cacheStillValid)
            return cachedContentName!;

        await CacheLock.WaitAsync();

        try
        {
            now = DateTimeOffset.UtcNow;

            cacheStillValid =
                cachedContentName != null &&
                cachedFilesHash == currentHash &&
                now < cacheExpiresAt;

            if (cacheStillValid)
                return cachedContentName!;

            string cachedText = $@"
SKYTHRONE fixed game knowledge.

RULEBOOK JSON:
{rules}

CARDS JSON:
{cards}
";

            var cacheBody = new
            {
                model = Model,
                displayName = "skythrone-rules-and-cards",
                ttl = $"{(int)CacheTtl.TotalSeconds}s",
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = @"
You are a SKYTHRONE card-game AI.

Use the cached rulebook and card list as the source of truth.
When asked to play cards:
- obey the energy limit exactly
- only choose cards from the provided hand
- return only valid JSON
- do not include explanations
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
                throw new Exception("Gemini cache was created but no cache name was returned.");

            cachedContentName = name;
            cachedFilesHash = currentHash;

            // Recreate a bit before actual TTL to avoid using an expired cache.
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
            throw new Exception(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{responseText}"
            );
        }

        return responseText;
    }

    private static string ExtractGeminiText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out JsonElement candidates) ||
            candidates.GetArrayLength() == 0)
        {
            throw new Exception("Gemini returned no candidates.");
        }

        JsonElement first = candidates[0];

        if (!first.TryGetProperty("content", out JsonElement content) ||
            !content.TryGetProperty("parts", out JsonElement parts) ||
            parts.GetArrayLength() == 0 ||
            !parts[0].TryGetProperty("text", out JsonElement textElement))
        {
            throw new Exception("Gemini response did not contain text.");
        }

        return textElement.GetString() ?? "";
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
}