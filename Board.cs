using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;

class Program
{
    static void Main()
    {
        Player p = new Player(new() { 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, });
        Player e = new Player(new() { 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, });
        Board board = new Board(p, e);
        board.GameStart();

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string path = Path.Combine(desktop, "test.txt");

        using (StreamWriter writer = new StreamWriter(path, append: true))
        {
            for (int i = 1; i < 56; i++)
            {
                writer.WriteLine(DataBase.CardFromId(i).ToString() + ",");
            }
        }


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
                Console.WriteLine("the board is full");
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
        if (current.maxenergy < current.MaxEnergy)
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
                    current.LastFaction = ((Unit)current.hand[cardplay]).faction;
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
        Unit? FindTaunt(Player p)
        {
            for (int i = 0; i < p.board.Count; i++)
            {
                Unit u = (Unit)p.board[i];
                if (u.taunt) return u;
            }
            return null;
        }

        void ActivateAdrenaline(Player p)
        {
            if (current != p)
            {
                Player temp = current;
                current = p;
                other = temp;
            }

            for (int i = 0; i < p.board.Count; i++)
            {
                Unit u = (Unit)p.board[i];
                u.Adrenaline(this);
            }
        }

        void DoAttack(Player attacker, Player enemy, ref int attackerIndex, ref int enemyIndex)
        {
            if (attackerIndex >= attacker.board.Count) return;

            Unit? enemyTaunt = FindTaunt(enemy);
            Unit currentUnit = (Unit)attacker.board[attackerIndex];

            bool enemyDied = false;
            int deadIndex = -1;

            if (enemyTaunt != null)
            {
                Console.WriteLine($"{currentUnit.name} is attacking taunting {enemyTaunt.name}");
                deadIndex = enemy.board.IndexOf(enemyTaunt);
                enemyDied = enemyTaunt.TakeDamage(currentUnit.attack, this);
            }
            else
            {
                int position = attackerIndex;
                if (position >= enemy.board.Count) position = enemy.board.Count - 1;
                if (position < 0)
                {
                    System.Console.WriteLine("Enemy doesn't have any units");
                    attackerIndex++;
                    return;
                }

                Unit? enemyCard = (Unit)enemy.board[position];
                Console.WriteLine($"{currentUnit.name} is attacking {enemyCard.name}");
                enemyDied = enemyCard.TakeDamage(currentUnit.attack, this);
                deadIndex = position;
            }

            if (enemyDied && deadIndex < enemyIndex)
            {
                enemyIndex--;
            }

            attackerIndex++;
        }

        int shortIndex = 0;
        int longIndex = 0;

        ActivateAdrenaline(p);
        ActivateAdrenaline(e);

        Player shortest, longest;
        if (p.board.Count <= e.board.Count) (shortest, longest) = (p, e);
        else (shortest, longest) = (e, p);

        while (shortIndex < shortest.board.Count || longIndex < longest.board.Count)
        {
            DoAttack(shortest, longest, ref shortIndex, ref longIndex);
            DoAttack(longest, shortest, ref longIndex, ref shortIndex);
        }

        current = p;
        other = e;
    }



    public void EndPhase()
    {
        if (p.board.Count != 0 && e.board.Count==0)
        {
            for (int i = 0; i < p.board.Count(); i++)
            {
                Console.WriteLine(p.board[i].name + " attacked the enemy for " + ((Unit)p.board[i]).Attack() + " damage");
                e.health -= ((Unit)p.board[i]).Attack();
            }
        }

        else if (e.board.Count != 0 && p.board.Count==0)
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