using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Api
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    // Swap order if you want 2.5 first.
    // Gemini 3.5 Flash model id is gemini-3.5-flash.
    // Gemini 2.5 Flash model id is gemini-2.5-flash.
    private static readonly string[] Models =
    {
        "gemini-3.5-flash",
        "gemini-2.5-flash"
    };

    private const string CacheStateFile = "gemini_cache_state.json";
    private const int CacheTtlSeconds = 60 * 60; // 1 hour

    private static readonly HttpClient Client = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    private static readonly SemaphoreSlim CacheLock = new SemaphoreSlim(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private class CacheEntry
    {
        public string Name { get; set; } = "";
        public string Hash { get; set; } = "";
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }

    private class ApiCallResult
    {
        public bool Success { get; set; }
        public HttpStatusCode? StatusCode { get; set; }
        public string Text { get; set; } = "";
        public string ErrorBody { get; set; } = "";
    }

    public static async Task<string> Play(Board game, string command)
    {
        string? apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing GEMINI_API_KEY environment variable.");

        string rules = await File.ReadAllTextAsync("rulebook.json");
        string cards = await File.ReadAllTextAsync("cards.json");

        string staticHash = Sha256(rules + "\n---CARDS---\n" + cards);

        string handstr = "[" + string.Join(", ",
            game.e.hand.Select((x, i) =>
                $"(selected_index: {i}, name: {x.name}, id: {x.id}, cost: {((Unit)x).cost})"
            )) + "]";

        string deckstr = "[" + string.Join(", ",
            game.e.deck.Select(x => x.name)
        ) + "]";

        string dynamicPrompt = $@"
Your hand cards: {handstr}
Your deck cards: {deckstr}

You have ONLY {game.e.energy} ENERGY.

Objective:
{command}

Return ONLY valid JSON.

Return an array of objects:
[
  {{
    ""selected_index"": int,
    ""selected_id"": int,
    ""mana_cost"": int
  }}
]

Rules:
- selected_index is the card position in hand.
- selected_id must match the card id.
- mana_cost must match the card cost.
- You may play multiple cards.
- After every chosen card, subtract its mana_cost from the remaining energy.
- Do not choose a card if its cost is higher than the remaining energy.
- If no valid card should be played, return [].
";

        Console.WriteLine("Prompt dynamic part:");
        Console.WriteLine(dynamicPrompt);

        while (true)
        {
            bool sawRetryableFailure = false;
            int permanentFailures = 0;

            foreach (string model in Models)
            {
                try
                {
                    Console.WriteLine($"Trying model: {model}");

                    string cachedContentName = await GetOrCreateCacheAsync(
                        apiKey,
                        model,
                        rules,
                        cards,
                        staticHash
                    );

                    ApiCallResult result = await GenerateAsync(
                        apiKey,
                        model,
                        cachedContentName,
                        dynamicPrompt
                    );

                    if (result.Success)
                    {
                        Console.WriteLine($"Success using model: {model}");
                        Console.WriteLine("Model response:");
                        Console.WriteLine(result.Text);
                        return CleanJson(result.Text);
                    }

                    Console.WriteLine($"Model failed: {model}");
                    Console.WriteLine($"{(int?)result.StatusCode} {result.StatusCode}");
                    Console.WriteLine(result.ErrorBody);

                    if (IsAuthFailure(result.StatusCode))
                        return "ERROR";

                    if (IsRetryable(result.StatusCode))
                        sawRetryableFailure = true;
                    else
                        permanentFailures++;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    Console.WriteLine($"Network/timeout error with {model}: {ex.Message}");
                    sawRetryableFailure = true;
                }
                catch (ApiException ex)
                {
                    Console.WriteLine($"API error with {model}: {(int)ex.StatusCode} {ex.StatusCode}");
                    Console.WriteLine(ex.Body);

                    if (IsAuthFailure(ex.StatusCode))
                        return "ERROR";

                    if (IsRetryable(ex.StatusCode))
                        sawRetryableFailure = true;
                    else
                        permanentFailures++;
                }
            }

            if (!sawRetryableFailure && permanentFailures >= Models.Length)
            {
                Console.WriteLine("All models failed with permanent errors.");
                return "ERROR";
            }

            Console.WriteLine("All models unavailable/busy. Retrying in 3 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    private static async Task<string> GetOrCreateCacheAsync(
        string apiKey,
        string model,
        string rules,
        string cards,
        string staticHash)
    {
        await CacheLock.WaitAsync();

        try
        {
            Dictionary<string, CacheEntry> cacheState = LoadCacheState();

            if (cacheState.TryGetValue(model, out CacheEntry? entry) &&
                entry.Hash == staticHash &&
                entry.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                Console.WriteLine($"Using existing cache for {model}: {entry.Name}");
                return entry.Name;
            }

            Console.WriteLine($"Creating new cache for {model}...");

            string staticContext = $@"
Rules JSON:
{rules}

Cards JSON:
{cards}

Use these rules and card definitions for every future game decision.
";

            var requestBody = new
            {
                model = $"models/{model}",
                displayName = $"game-rules-cards-{model}",
                ttl = $"{CacheTtlSeconds}s",
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = staticContext }
                        }
                    }
                }
            };

            using HttpResponseMessage response = await PostJsonAsync(
                $"{BaseUrl}/cachedContents",
                apiKey,
                requestBody
            );

            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ApiException(response.StatusCode, body);

            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;

            string cacheName = root.GetProperty("name").GetString()
                ?? throw new Exception("Cache creation succeeded but no cache name was returned.");

            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(CacheTtlSeconds);

            if (root.TryGetProperty("expireTime", out JsonElement expireTimeElement))
            {
                string? expireTime = expireTimeElement.GetString();

                if (DateTimeOffset.TryParse(expireTime, out DateTimeOffset parsed))
                    expiresAt = parsed;
            }

            cacheState[model] = new CacheEntry
            {
                Name = cacheName,
                Hash = staticHash,
                ExpiresAtUtc = expiresAt
            };

            SaveCacheState(cacheState);

            Console.WriteLine($"Created cache for {model}: {cacheName}");
            return cacheName;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static async Task<ApiCallResult> GenerateAsync(
        string apiKey,
        string model,
        string cachedContentName,
        string dynamicPrompt)
    {
        var requestBody = new
        {
            cachedContent = cachedContentName,
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = dynamicPrompt }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        using HttpResponseMessage response = await PostJsonAsync(
            $"{BaseUrl}/models/{model}:generateContent",
            apiKey,
            requestBody
        );

        string body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new ApiCallResult
            {
                Success = false,
                StatusCode = response.StatusCode,
                ErrorBody = body
            };
        }

        string? text = ExtractModelText(body);

        if (string.IsNullOrWhiteSpace(text))
        {
            return new ApiCallResult
            {
                Success = false,
                StatusCode = response.StatusCode,
                ErrorBody = body
            };
        }

        return new ApiCallResult
        {
            Success = true,
            StatusCode = response.StatusCode,
            Text = text
        };
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(
        string url,
        string apiKey,
        object requestBody)
    {
        string json = JsonSerializer.Serialize(requestBody);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return await Client.SendAsync(request);
    }

    private static string? ExtractModelText(string responseJson)
    {
        using JsonDocument doc = JsonDocument.Parse(responseJson);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out JsonElement candidates))
            return null;

        if (candidates.GetArrayLength() == 0)
            return null;

        JsonElement firstCandidate = candidates[0];

        if (!firstCandidate.TryGetProperty("content", out JsonElement content))
            return null;

        if (!content.TryGetProperty("parts", out JsonElement parts))
            return null;

        if (parts.GetArrayLength() == 0)
            return null;

        if (!parts[0].TryGetProperty("text", out JsonElement text))
            return null;

        return text.GetString();
    }

    private static string CleanJson(string text)
    {
        return text
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();
    }

    private static bool IsRetryable(HttpStatusCode? statusCode)
    {
        if (statusCode == null)
            return true;

        return statusCode == HttpStatusCode.RequestTimeout ||        // 408
               statusCode == (HttpStatusCode)429 ||                 // Too Many Requests
               statusCode == HttpStatusCode.InternalServerError ||   // 500
               statusCode == HttpStatusCode.BadGateway ||            // 502
               statusCode == HttpStatusCode.ServiceUnavailable ||    // 503
               statusCode == HttpStatusCode.GatewayTimeout;          // 504
    }

    private static bool IsAuthFailure(HttpStatusCode? statusCode)
    {
        return statusCode == HttpStatusCode.Unauthorized ||
               statusCode == HttpStatusCode.Forbidden;
    }

    private static Dictionary<string, CacheEntry> LoadCacheState()
    {
        if (!File.Exists(CacheStateFile))
            return new Dictionary<string, CacheEntry>();

        try
        {
            string json = File.ReadAllText(CacheStateFile);

            return JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(json, JsonOptions)
                   ?? new Dictionary<string, CacheEntry>();
        }
        catch
        {
            return new Dictionary<string, CacheEntry>();
        }
    }

    private static void SaveCacheState(Dictionary<string, CacheEntry> cacheState)
    {
        string json = JsonSerializer.Serialize(cacheState, JsonOptions);
        File.WriteAllText(CacheStateFile, json);
    }

    private static string Sha256(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private class ApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string Body { get; }

        public ApiException(HttpStatusCode statusCode, string body)
            : base($"{(int)statusCode} {statusCode}")
        {
            StatusCode = statusCode;
            Body = body;
        }
    }
}