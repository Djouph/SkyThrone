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
        var apiKey = "AIzaSyAaoFqvDwzhZU-6BvpHpiPsD063pGnf9FU";

        // 2. Create HTTP client and request body
        using var client = new HttpClient();

        var url = $"{Endpoint}?key={apiKey}";

        string rules = File.ReadAllText("rulebook.json");
        string cards = File.ReadAllText("cards.json");

        // Decide what to play until you dont have enough mana and return a result in an array of
        // {{
        // selected_index: int, //the position in hand
        // selected_id: int,
        // mana_cost: int,
        // }}


        var handstr = "[" + string.Join(", ", game.e.hand.Select(x => $"(name: {x.name}, id : {x.id}, cost: {((Unit)x).cost})").ToArray()) + "]";
        var deckstr = "[" + string.Join(", ", game.e.deck.Select(x => x.name).ToArray()) + "]";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = @$"
                            Rules: {rules},
                            Cards: {cards}.
                            Your hand cards: {handstr},
                            Your deck ids: {deckstr},
                            REALLY IMPORTANT - YOU HAVE *ONLY* {game.e.energy} ENERGY, AFTER PLAYING A CARD CALCULATE THE NEW ENERGY LEFT,
                            your objective: {command}
                           
                            THINK ABOUT THE RESPONSE AND MAKE SURE THE MANA COST IS VALID.
                        " }
                    }
                }
            }
        };

        System.Console.WriteLine("Prompt sent: ");
        System.Console.WriteLine(@$"
                            Your hand: {handstr},
                            Your deck: {deckstr},
                            You have {game.e.energy} mana and each card has a mana cost noted as ""cost"",
                            your objective: {command}
                            You have {game.e.energy} mana and each card has a mana cost noted as ""cost"",
                        ");

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 3. Send POST request
        var response = await client.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Request failed:");
            Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}");
            var errorBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(errorBody);
            return "ERROR";
        }

        // 4. Parse response JSON and print the model text
        var responseJson = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        // Typical path: candidates[0].content.parts[0].text
        var text = root
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        Console.WriteLine("Model response:");
        string cleaned = text
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        Console.WriteLine(cleaned);
        return cleaned;
    }
}