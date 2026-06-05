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
    private const string ModelName = "gemini-3.5-flash";

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
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(8);

    public static async Task<string> Play(Board game, string command)
    {
        string apiKey = GetApiKey();
        string cacheName = await GetOrCreateCache(apiKey);

        string handStr = JsonSerializer.Serialize(
            game.e.hand.Select((x, index) => new
            {
                selected_index = index,
                name = x.name,
                id = x.id,
                cost = x is Unit u ? u.cost : 0
            })
        );

        string deckStr = JsonSerializer.Serialize(
            game.e.deck.Select(x => new
            {
                name = x.name,
                id = x.id
            })
        );

        string prompt = $@"
Current hand JSON:
{handStr}

Current deck JSON:
{deckStr}

Current energy:
{game.e.energy}

Objective:
{command}

Choose cards to play.

Rules:
- Only choose cards from the current hand.
- selected_index must match the hand index.
- selected_id must match the card id.
- mana_cost should match the card cost.
- Energy is only information, not a hard limit.
- You may choose a card even if its mana_cost is higher than current energy.
- Return only a JSON array.
- If you choose no cards, return [].
- Do not use markdown.
- Do not explain.

Required format:
[
  {{
    ""selected_index"": 0,
    ""selected_id"": 123,
    ""mana_cost"": 2
  }}
]
";

        object requestBody = new
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
                maxOutputTokens = 1024,
                responseMimeType = "application/json"
            }
        };

        // Retries only on Gemini temporary/server errors.
        string responseJson = await PostJsonUntilSuccess(apiKey, GenerateEndpoint, requestBody);

        using JsonDocument doc = JsonDocument.Parse(responseJson);
        JsonElement root = doc.RootElement;

        PrintUsage(root);

        string text = ExtractGeminiText(root)
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        Console.WriteLine("Raw Gemini JSON:");
        Console.WriteLine(text);

        // No gameplay validation here.
        // If Gemini chooses a too-expensive card, we return it anyway.
        return text;
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
                    parts = new[]
                    {
                        new
                        {
                            text = @"
You are a SKYTHRONE card-game AI.
Use the cached rulebook and cards as the source of truth.

When choosing cards:
- only choose cards from the current hand
- never invent cards
- energy is only information, not a hard limit
- you may choose cards even if their cost is higher than current energy
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

            string responseJson = await PostJsonUntilSuccess(apiKey, CacheEndpoint, cacheBody);

            using JsonDocument doc = JsonDocument.Parse(responseJson);
            JsonElement root = doc.RootElement;

            string? name = root.GetProperty("name").GetString();

            if (string.IsNullOrWhiteSpace(name))
                throw new Exception("Cache created, but Gemini did not return a cache name.");

            cachedContentName = name;
            cachedFilesHash = currentHash;

            // Refresh before real expiry to avoid using an expired cache.
            cacheExpiresAt = DateTimeOffset.UtcNow.Add(CacheTtl).AddMinutes(-5);

            Console.WriteLine($"Created Gemini cache: {cachedContentName}");

            return cachedContentName;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static async Task<string> PostJsonUntilSuccess(
        string apiKey,
        string endpoint,
        object body,
        CancellationToken cancellationToken = default)
    {
        int attempt = 1;

        while (true)
        {
            try
            {
                return await PostJsonOnce(apiKey, endpoint, body, cancellationToken);
            }
            catch (GeminiRetryableException ex)
            {
                Console.WriteLine($"Gemini temporary error on attempt {attempt}:");
                Console.WriteLine(ex.Message);
                Console.WriteLine($"Retrying in {RetryDelay.TotalSeconds} seconds...");

                attempt++;
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    private static async Task<string> PostJsonOnce(
        string apiKey,
        string endpoint,
        object body,
        CancellationToken cancellationToken = default)
    {
        string url = $"{endpoint}?key={apiKey}";
        string json = JsonSerializer.Serialize(body);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using HttpResponseMessage response =
            await Client.PostAsync(url, content, cancellationToken);

        string responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
            return responseText;

        int status = (int)response.StatusCode;

        bool retryable =
            status == 429 ||
            status == 500 ||
            status == 502 ||
            status == 503 ||
            status == 504 ||
            responseText.Contains("high demand", StringComparison.OrdinalIgnoreCase) ||
            responseText.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
            responseText.Contains("overloaded", StringComparison.OrdinalIgnoreCase);

        if (retryable)
        {
            throw new GeminiRetryableException(
                $"HTTP {status} {response.ReasonPhrase}\n{responseText}"
            );
        }

        throw new Exception(
            $"Gemini non-retryable error: HTTP {status} {response.ReasonPhrase}\n{responseText}"
        );
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

    private class GeminiRetryableException : Exception
    {
        public GeminiRetryableException(string message) : base(message) { }
    }
}