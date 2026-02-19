using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;

abstract class PlayableUser
{
    public int playerId;
    public List<int> build;
    public List<Card> hand;
    public List<Card> deck;
    public List<Card> board;
    public int MaxEnergy;
    public int MaxHandSize;
    public int MaxBoardSize;
    public int StartingHand;
    public int energy;
    public int maxenergy;
    public int health;
    public Faction? LastFaction;

    public PlayableUser(int playerId, List<int> build)
    {
        this.playerId = playerId;
        this.build = build;
        hand = [];
        deck = [];
        board = [];
        MaxBoardSize = 6;
        MaxHandSize = 11;
        StartingHand = 3;
        energy = 0;
        maxenergy = 0;
        health = 20;
        MaxEnergy = 10;
        LastFaction = null;
    }

    public abstract Task Prepere(Board b);
}

class Enemy : PlayableUser
{
    class PrepereAction
    {
        public int cardIndex { get; set; }
        public int cardId { get; set; }
    }

    public Enemy(int playerId, List<int> build) : base(playerId, build)
    {
    }

    public async override Task Prepere(Board b)
    {
        if (maxenergy < MaxEnergy)
        {
            maxenergy += 1;
        }
        energy = maxenergy;
        b.Draw(this);

        string result = await Api.Play(b, @"Prepere for battle based on the given rules, return the result STRICTLY in the json format of:
            {{
                cardIndex : int, // cand index in hand
                cardId : int, // the id of the card
            }}[]

            note: indexes start from 0.
        ");

        PrepereAction[] actions = JsonSerializer.Deserialize<PrepereAction[]>(result)!;
        for (int i = 0; i < actions.Length; i++)
        {
            int cardplay = actions[i].cardIndex;
            Console.WriteLine("The Enemy has played " + hand[cardplay].name + $"(id = {actions[i].cardId})");
            b.Play(this, (Unit)hand[cardplay]);
            for (int j = 0; j < actions.Length; j++)
            {
                if (actions[j].cardIndex > cardplay)
                {
                    actions[j].cardIndex--;
                }
            }
        }
    }

    public Enemy Clone()
    {
        return new Enemy(playerId, [.. build]);
    }
}

class Admin : PlayableUser
{
    // public List<Artifact> artifacts;

    public Admin(int playerId, List<int> build) : base(playerId, build)
    {

    }

    public async override Task Prepere(Board b)
    {
        bool endPhase = false;
        if (maxenergy < MaxEnergy)
        {
            maxenergy += 1;
        }
        energy = maxenergy;
        b.Draw(this);
        while (!endPhase)
        {
            Console.WriteLine("your energy:" + energy);
            Console.WriteLine("hand:");
            for (int i = 0; i < hand.Count; i++)
            {
                Console.Write(hand[i].name + " (" + ((Unit)hand[i]).cost + ")" + " ,");
            }
            Console.WriteLine();
            Console.WriteLine("your board:");
            for (int i = 0; i < board.Count; i++)
            {
                Console.Write(board[i].name + ",");
            }
            Console.WriteLine();
            Console.WriteLine("You have " + deck.Count + " cards in your deck");
            Console.WriteLine();

            Console.WriteLine("what card do you want to play? (0 = Dont play anything)");
            int cardplay = int.Parse(Console.ReadLine()!) - 1;
            if (cardplay == -1)
            {
                break;
            }
            else
            {
                Console.WriteLine("do you want to play this card or view it? p/v");
                if (char.Parse(Console.ReadLine()!) == 'p')
                {
                    LastFaction = ((Unit)hand[cardplay]).faction;
                    b.Play(this, (Unit)hand[cardplay]);

                }
                else if (char.Parse(Console.ReadLine()!) == 'v')
                {
                    Console.WriteLine(hand[cardplay].description);
                }
            }


            Console.WriteLine(" do you want to end your turn? yes/no");
            if (Console.ReadLine() == "yes")
            {
                endPhase = true;
            }
        }
    }
}

class Player : PlayableUser
{
    // public List<Artifact> artifacts;

    public Player(int playerId, List<int> build) : base(playerId, build)
    {

    }




    /// <summary>
    /// this function requires a RFB (ready for battle) and a board,
    /// when the player plays a card or ends their turn, 
    /// the game will send the server the index of the card or -1 if they ended their turn, 
    /// then it will call upon this function which will play the card or do the starting turn functions, 
    /// the function will return a json which will have the new inforation of what happned in the following order:
    /// energy, maxEnergy, hand, board, deck, player health.
    /// </summary>
    /// <param name="rfb"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public Response[] PlayCard(RFB rfb, Board b)
    {
        List<Response> response = new();

        if (rfb.cardPlayed == -1)
        {
            b.Draw(this);
            if (maxenergy < MaxEnergy)
            {
                maxenergy++;
            }
            energy = maxenergy;
        }
        else
        {
            b.Play(this, (Unit)hand[rfb.cardPlayed]);
        }
        response.Add(new Response()
        {
            name = "energy",
            payload = energy,
        });
        response.Add(new Response()
        {
            name = "maxenergy",
            payload = maxenergy,
        });
        response.Add(new Response()
        {
            name = "hand",
            payload = hand.Select((c) => c.id).ToList(),
        });
        response.Add(new Response()
        {
            name = "board",
            payload = board.Select((c) => c.id).ToList(),
        });
        response.Add(new Response()
        {
            name = "deck",
            payload = deck.Select((c) => c.id).ToList(),
        });
        response.Add(new Response()
        {
            name = "playerhealth",
            payload = health,
        });
        return response.ToArray();
    }

    public async override Task Prepere(Board b)
    {

    }
}

class Response
{
    public string name;
    public object payload;
}