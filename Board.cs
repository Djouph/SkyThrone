using System.Net.NetworkInformation;

class Program
{
    static void Main()
    {
        Player p = new Player(new() { 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 });
        Player e = new Player(new() { 35, 35, 35, 35, 35, 35, 35, 35, 35, 35 });
        Board board = new Board(p, e);
        board.GameStart();

        while (board.p.health != 0 || board.e.health != 0)
        {
            Console.WriteLine("PLAYER PREP PHASE");
            board.PreparationPhase();
            Console.WriteLine("ENEMY PREP PHASE");
            board.PreparationPhase();
            Console.WriteLine("ATTAAAAAAAAAAACK");
            board.BattlePhase();
            Console.WriteLine("END:");
            board.EndPhase();

            if (board.p.health < 1)
            {
                Console.WriteLine("DEFEAT");
                break;
            }
            if (board.e.health < 1)
            {
                Console.WriteLine("VICTORY");
                break;
            }

        }
    }
}


class Board
{
    public Player p;
    public Player e;
    public Player current;
    public Player other;
    const int StartingHand = 3;
    public bool playerturn;
    public Board(Player p, Player e)
    {
        this.p = p;
        this.e = e;
        current = p;
        other = e;
    }

    public void Play(Unit unit)
    {
        if (current.energy < unit.cost)
        {
            Console.WriteLine("that costs too much energy");
        }
        else
        {
            if (current.board.Count < 7)
            {
                current.energy -= unit.cost;
                current.board.Add(unit);
                current.hand.Remove(unit);
                unit.OnDeploy(this);
            }
            else
            {
                Console.WriteLine("there is no room in the board");
            }
        }
    }

    public void Merge(Unit u, Unit u2)
    {

        u2.attack += u.attack;
        u2.Health += u.Health;
        kill(u);
        Console.WriteLine(u.name + " has merged with " + u2.name);
        u2.name = "MERGED " + u2.name;

    }

    public void kill(Unit u)
    {
        u.LastWords(this);
        e.board.Remove(u);
        p.board.Remove(u);
    }

    public void Remove(int place)
    {
        current.deck.RemoveAt(place);
    }
    public void Discard(int place)
    {
        current.hand.RemoveAt(place);
    }
    public void Draw()
    {


        if (current.hand.Count == current.MaxHandSize)
        {

            Console.WriteLine("maxerror");
            if (current.deck.Count != 0)
            {
                current.deck.RemoveAt(0);
            }

        }
        else
        {
            if (current.deck.Count == 0)
            {
                Console.WriteLine("You are out of cards");
                current.health--;
            }
            else if (current.deck[0] is InstaPlay)
            {
                InstaPlay ip = (InstaPlay)current.deck[0];
                Remove(0);
                ip.OnDraw(this);
                Draw();

            }
            else
            {
                current.hand.Add(current.deck[0]);
                current.deck.RemoveAt(0);
            }
        }
    }

    public void Build()
    {
        Random rnd = new Random();
        List<int> temp = new List<int>();


        for (int i = 0; i < p.build.Count; i++)
        {
            temp.Add(p.build[i]);
        }
        int random = 0;
        int len = temp.Count;
        for (int i = 0; i < len; i++)
        {
            random = rnd.Next(0, temp.Count);
            p.deck.Add(DataBase.CardFromId(temp[random]));
            temp.RemoveAt(random);
        }

        temp.Clear();

        for (int i = 0; i < e.build.Count; i++)
        {
            temp.Add(e.build[i]);
        }
        len = temp.Count;
        for (int i = 0; i < len; i++)
        {
            random = rnd.Next(0, temp.Count);
            e.deck.Add(DataBase.CardFromId(temp[random]));
            temp.RemoveAt(random);
        }
    }

    public void Infeltrait(int id)
    {
        Random rnd = new Random();

        int random = rnd.Next(0, other.deck.Count);
        other.deck.Insert(random, DataBase.CardFromId(id));

    }

    public void GameStart()
    {
        Build();
        p.health = 20;
        e.health = 20;
        for (int i = 0; i < StartingHand; i++)
        {
            Draw();
            current = e;
            other = p;
            Draw();
            current = p;
            other = e;
        }
        p.board = [];
        e.board = [];
    }


    public void PreparationPhase()
    {

        bool endPhase = false;
        if (current.maxenergy < current.maxEnergy)
        {
            current.maxenergy += 1;
        }
        current.energy = current.maxenergy;
        Draw();
        while (!endPhase)
        {
            Console.WriteLine("your energy:" + current.energy);
            Console.WriteLine("hand:");
            for (int i = 0; i < current.hand.Count; i++)
            {
                Console.Write(current.hand[i].name + " (" + ((Unit)current.hand[i]).cost + ")" + " ,");
            }
            Console.WriteLine();
            Console.WriteLine("your board:");
            for (int i = 0; i < current.board.Count; i++)
            {
                Console.Write(current.board[i].name + ",");
            }
            Console.WriteLine();
            Console.WriteLine("You have " + current.deck.Count + " cards in your deck");
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
                    Play((Unit)current.hand[cardplay]);
                }
                else if (char.Parse(Console.ReadLine()!) == 'v')
                {
                    Console.WriteLine(current.hand[cardplay].description);
                }
            }


            Console.WriteLine(" do you want to end your turn? yes/no");
            if (Console.ReadLine() == "yes")
            {
                endPhase = true;
            }
        }

        if (current == p)
        {
            current = e;
            other = p;
        }
        else
        {
            current = p;
            other = e;
        }
    }

    public void BattlePhase()
    {

        for (int i = 0; i < current.board.Count; i++)
        {
            Unit Unit = (Unit)current.board[i];
            Unit.Adrenaline(this);
        }
        if (current == p)
        {
            current = e;
            other = p;
        }
        else
        {
            current = p;
            other = e;
        }
        for (int i = 0; i < current.board.Count; i++)
        {
            Unit Unit = (Unit)current.board[i];
            Unit.Adrenaline(this);
        }
        bool ispdead = false;
        bool isedead = false;
        List<Card> longest = new List<Card>();
        List<Card> shortest = new List<Card>();
        if (p.board.Count > e.board.Count)
        {
            longest = p.board;
            shortest = e.board;
        }
        else
        {
            longest = e.board;
            shortest = p.board;
        }
        while (e.board.Count != 0 && p.board.Count != 0)
        {
            int j = 0;
            for (int i = 0; i < shortest.Count; i++)
            {
                ispdead = false;
                isedead = false;
                Console.WriteLine(longest[i].name + " is attacking " + shortest[j].name);
                int damage = 0;
                damage = ((Unit)longest[j]).attack;
                ispdead = ((Unit)longest[j]).TakeDamage(((Unit)shortest[i]).Attack(), this);
                isedead = ((Unit)shortest[i]).TakeDamage(damage, this);
                if (ispdead)
                {
                    if (p.board.Count > e.board.Count)
                    {
                        longest = p.board;
                        shortest = e.board;
                    }
                    else
                    {
                        longest = e.board;
                        shortest = p.board;
                    }
                    j--;
                }
                if (isedead)
                {
                    if (p.board.Count > e.board.Count)
                    {
                        longest = p.board;
                        shortest = e.board;
                    }
                    else
                    {
                        longest = e.board;
                        shortest = p.board;
                    }
                    i--;
                }
                j++;
            }
            if (!(e.board.Count != 0 && p.board.Count != 0))
            {
                return;
            }

            for (int i = shortest.Count; i < longest.Count; i++)
            {

                ispdead = false;
                isedead = false;
                Console.WriteLine(longest[i].name + " is attacking " + shortest[shortest.Count - 1].name);
                ispdead = ((Unit)longest[i]).TakeDamage(((Unit)shortest[shortest.Count - 1]).Attack(), this);
                isedead = ((Unit)shortest[shortest.Count - 1]).TakeDamage(((Unit)longest[i]).Attack(), this);
                if (ispdead)
                {
                    kill((Unit)longest[i]);
                    i--;
                }
                if (isedead)
                {
                    kill((Unit)shortest[shortest.Count - 1]);
                }
            }
        }
    }

    public void EndPhase()
    {
        if (p.board.Count != 0)
        {
            for (int i = 0; i < p.board.Count(); i++)
            {
                Console.WriteLine(p.board[i].name + " attacked the enemy for " + ((Unit)p.board[i]).Attack() + " damage");
                e.health -= ((Unit)p.board[i]).Attack();
            }
        }

        else if (e.board.Count != 0)
        {
            for (int i = 0; i < e.board.Count(); i++)
            {
                Console.WriteLine(e.board[i].name + " attacked the player for " + ((Unit)e.board[i]).Attack() + " damage");
                p.health -= ((Unit)e.board[i]).Attack();
            }
        }

        Console.WriteLine("player health: " + p.health);
        Console.WriteLine("enemy health: " + e.health);

    }

}