using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Api
{
    // Gemini text endpoint (v1beta, 2.5-flash model)
    private const string Endpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    static public async Task<string> Play(Board game, string command)
    {
        const int maxRetries = 15;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

                using var client = new HttpClient();

                var url = $"{Endpoint}?key={apiKey}";

                string rules = File.ReadAllText("rulebook.json");
                string cards = File.ReadAllText("cards.json");

                var handstr = "[" + string.Join(", ", game.e.hand.Select(x =>
                    $"(name: {x.name}, id : {x.id}, cost: {((Unit)x).cost})")) + "]";

                var deckstr = "[" + string.Join(", ", game.e.deck.Select(x => x.name)) + "]";

                var requestBody = new
                {
                    contents = new[]
                    {
                    new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = @$"
                                    Rules: {rules},
                                    Cards: {cards}.
                                    Your hand cards: {handstr},
                                    Your deck ids: {deckstr},
                                    REALLY IMPORTANT - YOU HAVE *ONLY* {game.e.energy} ENERGY.
                                    your objective: {command}
                                "
                            }
                        }
                    }
                }
                };

                var json = JsonSerializer.Serialize(requestBody);

                using var content =
                    new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody =
                        await response.Content.ReadAsStringAsync();

                    Console.WriteLine(
                        $"Gemini attempt {attempt}/{maxRetries} failed: " +
                        $"{(int)response.StatusCode} {response.ReasonPhrase}");

                    Console.WriteLine(errorBody);

                    // Retry only for temporary failures
                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                        response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10));
                            continue;
                        }
                    }

                    return "ERROR";
                }

                var responseJson =
                    await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseJson);

                string? text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text?
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim()
                    ?? "ERROR";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(
                    $"Network error on attempt {attempt}/{maxRetries}: {ex.Message}");

                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    continue;
                }

                return "ERROR";
            }
        }

        return "ERROR";
    }
}