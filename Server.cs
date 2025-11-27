using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
public class TcpServer
{
    public void Run()
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