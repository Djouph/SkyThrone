using System.ComponentModel.DataAnnotations.Schema;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        bool isRender = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KAMATERA"));

        if (isRender)
        {
            await HttpServer.RunHttpDownloadServerAsync([]);
        }
        else
        {
            HttpServer.RunHttpDownloadServerAsync([]);

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string path = Path.Combine(desktop, "test.txt");

            using (StreamWriter writer = new StreamWriter(path, append: true))
            {
                foreach (var kvp in DataBase.lookup)
                {
                    writer.WriteLine(kvp.Value.ToString() + ",");
                }
            }

            PlayableUser p = new Player(1, new() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 201, 202, 203, 204, 205, 206, 207, 208, 2, 10, });
            PlayableUser e = new Enemy(2, new() { 300, 301, 302, 303, 304, 305, 306, 307, 308, 300, 301, 302, 303, 304, 305, 306, 307, 308, 10, 10, });
            Enemy ToturialEnemy = new Enemy(3, new() { });
            Board board = new Board(p, e);
            board.GameStart();




            while (board.p.health != 0 || board.e.health != 0)
            {
                Console.WriteLine("PREP PHASE:");
                await board.PreparationPhase();
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
}

class Board
{
    public PlayableUser p;
    public PlayableUser e;
    // public PlayableUser current;
    // public PlayableUser other;
    const int StartingHand = 3;
    public bool playerturn;
    public Board(PlayableUser p, PlayableUser e)
    {
        this.p = p;
        this.e = e;
    }

/// <summary>
/// FindOther is needed for many cards in the CardDataBase, 
/// they use it to know the PlayableUser they need for their abilities
/// </summary>
/// <param name="sender"></param>
/// <returns></returns>
    public PlayableUser FindOther(PlayableUser sender)
    {
        PlayableUser other;
        if (sender == p)
        {
            other = e;
        }
        else
        {
            other = p;
        }
        return other;
    }

/// <summary>
    /// Play is a core function that is used to put cards on the battlefield,
    /// it requires to know the PlayableUser that uses it and the Unit it tries to play,
    /// the PlayableUser is needed to check if they have enough energy to play the Unit, 
    /// and if the Unit is played, the function will call its OnDeploy to activate.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="unit"></param>
    public void Play(PlayableUser sender, Unit unit)
    {
        if (sender.energy < unit.cost)
        {
            Console.WriteLine("that costs too much energy");
        }
        else
        {
            if (sender.board.Count < 7)
            {
                sender.energy -= unit.cost;
                sender.board.Add(unit);
                sender.hand.Remove(unit);

                var owner = GetUserFromUnit(unit);
                unit.OnDeploy(owner, this);
            }
            else
            {
                Console.WriteLine("the board is full");
            }
        }
    }

/// <summary>
/// Merge is a function that is mainly used by the Robot faction, 
/// Merge takes 2 Units, one on the left and one on the right, 
/// the Unit on the left takes the stats of the Unit on the right and then destroys it.
/// </summary>
/// <param name="u"></param>
/// <param name="u2"></param>
    public void Merge(Unit u, Unit u2)
    {

        u2.attack += u.attack;
        u2.Health += u.Health;
        kill(u);
        Console.WriteLine(u.name + " has merged with " + u2.name);
        u2.name = "MERGED " + u2.name;

    }

/// <summary>
/// Kill is a core game function that require a Unit, 
/// Kill removes the Unit from the battlefield and activates their LastWords,
/// Kill won't remove a Unit that isn't connected since every Unit is a special clone with their hidden clone id. 
/// </summary>
/// <param name="u"></param>
    public void kill(Unit u)
    {
        var owner = GetUserFromUnit(u);
        u.LastWords(owner, this);
        e.board.Remove(u);
        p.board.Remove(u);
    }

/// <summary>
/// Remove is a core game function that removes cards from the deck of a PlayableUser, 
/// Remove requires a PlayableUser and the int of the place they are removing.
/// </summary>
/// <param name="sender"></param>
/// <param name="place"></param>
    public void Remove(PlayableUser sender, int place)
    {
        sender.deck.RemoveAt(place);
    }

/// <summary>
/// Discard is a core game function that removes cards from the hand of a PlayableUser, 
/// Remove requires a PlayableUser and the int of the card they are removing.
/// </summary>
/// <param name="sender"></param>
/// <param name="place"></param>
    public void Discard(PlayableUser sender, int place)
    {
        sender.hand.RemoveAt(place);
    }

/// <summary>
/// Draw is a core game function that tries to draw a card and requires a PlayableUser, 
/// when Draw gets a PlayableUser it will try to draw a card and will do the following effects:
/// if the PlayableUser's hand is full it will Remove the card on the top of the PlayableUser's deck, 
/// if the PlayableUser has an empty deck it will lower the PlayableUser's health by one, 
/// if the top card of the PlayableUser's deck is an InstaPlay card then it will activate the OnDraw effect of the InstaPlay card and Removes it, after which it will recall the Draw function, 
/// if non of the above happens then it will simply draw a card and add it to the PlayableUsers hand.
/// </summary>
/// <param name="user"></param>
    public void Draw(PlayableUser user)
    {
        if (user.hand.Count == user.MaxHandSize)
        {
            Console.WriteLine("maxerror");
            if (user.deck.Count != 0)
            {
                user.deck.RemoveAt(0);
            }

        }
        else
        {
            if (user.deck.Count == 0)
            {
                Console.WriteLine("You are out of cards");
                user.health--;
            }
            else if (user.deck[0] is InstaPlay)
            {
                InstaPlay ip = (InstaPlay)user.deck[0];
                Remove(user, 0);
                ip.OnDraw(user, this);
                Draw(user);

            }
            else
            {
                user.hand.Add(user.deck[0]);
                user.deck.RemoveAt(0);
            }
        }
    }

/// <summary>
/// This function is used by the game to get the PlayableUser of a Unit, 
/// GetUserFromUnit requires a Unit, 
/// GetUserFromUnit will then check the battlefield to see which PlayableUser owns the Unit and returns the PlayableUser.
/// </summary>
/// <param name="u"></param>
/// <returns></returns>
    public PlayableUser GetUserFromUnit(Unit u)
    {
        if (e.board.Contains(u) || e.hand.Contains(u)) return e;
        return p;
    }

/// <summary>
/// Build is a core game function that activates every battle, 
/// Build uses the builds of the PlayableUsers and shuffles the cards into the decks.
/// </summary>
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

/// <summary>
/// Infeltrait is a function that is used mainly by the Pirate faction, 
/// Infeltrait requires a PlayableUser and the id of a card, 
/// Infeltrait goes to the PlayableUser that isnt the PlayableUser it was given, 
/// and then inserts a clone of the card who's id the Infeltrait function was given in a random place in the PlayableUser's deck.
/// </summary>
/// <param name="sender"></param>
/// <param name="id"></param>
    public void Infeltrait(PlayableUser sender, int id)
    {
        Random rnd = new Random();
        PlayableUser other = FindOther(sender);
        int random = rnd.Next(0, other.deck.Count);
        other.deck.Insert(random, DataBase.CardFromId(id));

    }

/// <summary>
/// GameStart is a core game function that starts each battle, 
/// when called it will call on the Build function, 
/// reset the health of the PlayableUsers back to 20, 
/// Draws cards until both the PlayableUsers have their starting hands, 
/// and resets the battlefields.
/// </summary>
    public void GameStart()
    {
        Build();
        p.health = 20;
        e.health = 20;
        for (int i = 0; i < StartingHand; i++)
        {
            Draw(p);
            Draw(e);
        }
        p.board = [];
        e.board = [];
    }

/// <summary>
/// The PreparationPhase functions is a core game function that calls the PlayableUsers prepere.
/// </summary>
/// <returns></returns>
    public async Task PreparationPhase()
    {
        await p.Prepere(this);
        await e.Prepere(this);
    }

/// <summary>
/// The BattlePhase is a core game function that is a big part of the main game (the combat) and uses multiple nested functions, 
/// First it will call on a nested function called ActivateAdrenaline, this nested function will call all the adrenaline of the cards in the battlefields (starting with the Player), 
/// then it will start having all the Units attack each other using a nested function called DoAttack.
/// </summary>
/// <returns></returns>
    public List<AttackData> BattlePhase()
    {
        List<AttackData> attackdata = new();

        Unit? FindTaunt(PlayableUser p)
        {
            for (int i = 0; i < p.board.Count; i++)
            {
                Unit u = (Unit)p.board[i];
                if (u.taunt) return u;
            }
            return null;
        }

        void ActivateAdrenaline(PlayableUser p)
        {
            for (int i = 0; i < p.board.Count; i++)
            {
                Unit u = (Unit)p.board[i];
                var owner = GetUserFromUnit(u);
                u.Adrenaline(owner, this);
            }
        }

        void DoAttack(PlayableUser attacker, PlayableUser enemy, ref int attackerIndex, ref int enemyIndex)
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
                AttackData temp = new AttackData();
                temp.src = attackerIndex;
                temp.dest = deadIndex;
                temp.attackerplayerId = attacker.playerId;
                attackdata.Add(temp);
                enemyDied = enemyTaunt.TakeDamage(enemy, currentUnit.attack, this);
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
                AttackData temp = new AttackData();
                temp.src = attackerIndex;
                temp.dest = position;
                temp.attackerplayerId = attacker.playerId;
                attackdata.Add(temp);
                enemyDied = enemyCard.TakeDamage(enemy, currentUnit.attack, this);
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

        PlayableUser shortest, longest;
        if (p.board.Count <= e.board.Count) (shortest, longest) = (p, e);
        else (shortest, longest) = (e, p);

        while (shortIndex < shortest.board.Count || longIndex < longest.board.Count)
        {
            DoAttack(shortest, longest, ref shortIndex, ref longIndex);
            DoAttack(longest, shortest, ref longIndex, ref shortIndex);
        }

        return attackdata;
    }

/// <summary>
/// EndPhase function is a core game function that checks the health win condition, 
/// when the BattlePhase function ends with one side victories, that side's remaining Units will attack the other PlayableUser's health, 
/// if their health is lowered to or below 0, then that PlayableUser is defeated and the other PlayableUser has won, 
/// during the function, a List of AttackData will be made to send to the frontend so the player will see what happened.
/// </summary>
/// <returns></returns>
    public List<AttackData> EndPhase()
    {
        List<AttackData> attackData = new();
        if (p.board.Count != 0 && e.board.Count == 0)
        {
            for (int i = 0; i < p.board.Count(); i++)
            {
                Console.WriteLine(p.board[i].name + " attacked the enemy for " + ((Unit)p.board[i]).Attack() + " damage");
                e.health -= ((Unit)p.board[i]).Attack();
                AttackData temp = new();
                temp.src = p.board[i].id;
                temp.dest = 10000;
                temp.attackerplayerId = p.playerId;
                attackData.Add(temp);
            }
        }

        else if (e.board.Count != 0 && p.board.Count == 0)
        {
            for (int i = 0; i < e.board.Count(); i++)
            {
                Console.WriteLine(e.board[i].name + " attacked the player for " + ((Unit)e.board[i]).Attack() + " damage");
                p.health -= ((Unit)e.board[i]).Attack();
                AttackData temp = new();
                temp.src = e.board[i].id;
                temp.dest = 20000;
                temp.attackerplayerId = e.playerId;
                attackData.Add(temp);
            }
        }

        Console.WriteLine("player health: " + p.health);
        Console.WriteLine("enemy health: " + e.health);
        return attackData;
    }

}