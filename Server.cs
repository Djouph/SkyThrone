using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;

public class HttpServer
{
    public static async Task ZipAllCardImages(IReadOnlyDictionary<int, Card> cards, string imagesDir, string outputZipPath, HttpContext context)
    {
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        if (string.IsNullOrWhiteSpace(imagesDir)) throw new ArgumentException("imagesDir is required.", nameof(imagesDir));

        Directory.CreateDirectory(Path.GetDirectoryName(outputZipPath) ?? ".");
        if (File.Exists(outputZipPath)) File.Delete(outputZipPath);

        using (var zip = new ZipArchive(
            context.Response.Body,
            ZipArchiveMode.Create,
            leaveOpen: true))
        {
            foreach (var (cardId, card) in cards)
            {
                // Prefer the dictionary key as the ID source (it’s the authoritative one)
                var fileName = GetFileNameFromUrlOrPath(card.imgUrl);
                var srcPath = Path.Combine(imagesDir, fileName);

                if (!File.Exists(srcPath))
                    throw new FileNotFoundException($"Image not found for card {cardId}: {srcPath}", srcPath);

                // ---- IMAGE ENTRY ----
                var imgEntry = zip.CreateEntry($"img_{cardId}.png");
                using (var entryStream = imgEntry.Open())
                using (var imgStream = File.OpenRead(srcPath))
                {
                    await imgStream.CopyToAsync(entryStream);
                }

                // ---- JSON ENTRY ----
                var jsonEntry = zip.CreateEntry($"json_{cardId}.json");
                using (var jsonStream = jsonEntry.Open())
                using (var writer = new StreamWriter(jsonStream))
                {
                    await writer.WriteAsync(card.ToString());
                }
            }
        }
    }

    private static string GetFileNameFromUrlOrPath(string imgUrl)
    {
        if (string.IsNullOrWhiteSpace(imgUrl))
            throw new ArgumentException("ImgUrl is empty.");

        // Works for: "/images/a.png", "images/a.png", "a.png", "http://x/y/a.png"
        if (Uri.TryCreate(imgUrl, UriKind.Absolute, out var uri))
            return Path.GetFileName(uri.LocalPath);

        return Path.GetFileName(imgUrl.Replace('\\', '/'));
    }

    static public async Task RunHttpDownloadServerAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.AllowSynchronousIO = true;
        });

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Project/app root (in dev = your .csproj folder)
        var root = builder.Environment.ContentRootPath;

        // Everything local to the project
        var imagesDir = Path.Combine(root, "images");               // ./images
        var zipPath = Path.Combine(root, "downloads", "images.zip"); // ./downloads/images.zip

        var app = builder.Build();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
            Path.Combine(app.Environment.ContentRootPath, "Images")),
            RequestPath = "/Images"
        });

        // GET /update -> streams the zip
        app.MapGet("/update", async (HttpContext context) =>
        {
            context.Response.ContentType = "application/zip";
            context.Response.Headers["Content-Disposition"] =
                "attachment; filename=cards.zip";

            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
            await ZipAllCardImages(DataBase.lookup, imagesDir, zipPath, context);
        });

        app.MapGet("/", () => "OK");

        await app.RunAsync();
    }
}

public class TcpServer
{
    public async void Run()
    {
        TcpListener server = null;
        try
        {
            IPAddress localAddr = IPAddress.Parse("127.0.0.1"); // Loopback address
            int port = 13000;

            server = new TcpListener(localAddr, port);

            // Start listening for client requests
            server.Start();
            Console.WriteLine($"Server started on {localAddr}:{port}");
            // Enter the listening loop
            while (true)
            {
                Console.WriteLine("Waiting for a connection...");

                // Accept incoming connection
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Client connected!");

                // Handle client communication in a new thread
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine($"SocketException: {e.Message}");
        }
        finally
        {
            server?.Stop();
        }
    }

    private static void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[2048];
        int bytesRead;

        JoinRequest? request = null;
        Player? p = null;
        Enemy? e = null;
        Board? game = null;

        try
        {
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                // Translate data bytes to a ASCII string
                string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                if (request == null)
                {
                    request = JsonSerializer.Deserialize<JoinRequest>(data);
                    if (request == null) break; // TODO : SEND ERROR RESPONSE

                    // TODO : CHECK IF THE PLAYER CAN PLAY THE SELECTED LEVEL

                    const int playerId = 20000;
                    const int enemeyId = 10000;

                    p = new Player(playerId, request.build);
                    e = new Enemy(enemeyId, request.build);
                    game = new Board(p, e);

                    // Echo back the data
                    OkJoin okJoin = new OkJoin()
                    {
                        e = e,
                        playerId = playerId, // temp const id for now (Change later)
                    };

                    var options = new JsonSerializerOptions
                    {
                        IncludeFields = true,  // This includes all fields (public and private)
                                               // DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never  // Never ignore anything
                    };

                    string json = JsonSerializer.Serialize(okJoin, options);

                    byte[] response = Encoding.ASCII.GetBytes(json);
                    stream.Write(response, 0, response.Length);
                }
                else
                {
                    // TODO : SIMULATE ENEMY PREPERATION (GEMINI API)

                    if (game != null)
                    {

                        game.BattlePhase(); // GET LIST OF ATTACKS (AND MAYBE ADDED CARDS) TO ANIAMTE AT THE USER SIDE.
                        game.EndPhase();

                        if (game.p.health < 1)
                        {
                            Console.WriteLine("DEFEAT");
                            break;
                        }
                        if (game.e.health < 1)
                        {
                            Console.WriteLine("VICTORY");
                            break;
                        }

                        // TODO : ADD RESPONSE
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine("Client disconnected.");
        }
    }
}


class JoinRequest
{
    public string token;
    public int level;
    public List<int> build;
}

class OkJoin
{
    public int playerId; // The id in the currnet on going game 

    public Enemy e;
}

class RFB // Ready for battle 
{
    public List<int> cardsPlayed;
}

/// <summary>
/// Src card index attack the dest index
/// </summary>
public class AttackData
{
    public int attackerplayerId;
    public int src;
    public int dest;
}

class OkRFB
{
    public List<AttackData> attacks;

    public int playerHealth;
    public List<int> playerHand;
    public List<int> playerDeck;

    public int enemyHealth;
    public List<int> enemyHand;
    public List<int> enemyDeck;
}