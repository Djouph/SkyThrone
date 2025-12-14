using System.Text.Json;

public abstract class Card
{
    public string name;
    public string description;
    public readonly int id;
    public string imgUrl;

    public Card(string name, string description, int id, string imgUrl = "")
    {
        this.name = name;
        this.description = description;
        this.id = id;
        this.imgUrl = imgUrl;
    }

    public abstract Card Clone();
}

class Unit : Card
{
    public int cost;
    public int attack;
    public int Health;
    public Faction faction;
    public bool HolyGuard;
    public bool taunt;
    private Action<PlayableUser, int, Board, Unit> takeDamage;
    private Action<PlayableUser, Board, Unit> adrenaline;
    private Action<PlayableUser, Board, Unit> onDeploy;
    private Action<PlayableUser, Board, Unit> lastWords;
    public Unit(int cost, string name, string description, int attack, int Health, Faction faction, int id, string imgUrl = "null.png", bool taunt = false, bool HolyGuard = false,
        Action<PlayableUser, Board, Unit> lastWords = null!,
        Action<PlayableUser, int, Board, Unit> takeDamage = null!,
        Action<PlayableUser, Board, Unit> adrenaline = null!,
        Action<PlayableUser, Board, Unit> onDeploy = null!) : base(name, description, id, imgUrl)
    {
        this.faction = faction;
        this.cost = cost;
        this.name = name;
        this.attack = attack;
        this.Health = Health;
        this.lastWords = lastWords;
        this.takeDamage = takeDamage;
        this.adrenaline = adrenaline;
        this.onDeploy = onDeploy;
        this.HolyGuard = HolyGuard;
        this.taunt = taunt;
    }

    public virtual bool TakeDamage(PlayableUser owner, int damage, Board b)
    {
        if (takeDamage == null)
        {
            if (HolyGuard)
            {
                HolyGuard = false;
            }
            else
            {
                Health -= damage;
                if (Health <= 0)
                {
                    b.kill(this);
                    return true;
                }
            }
        }
        else
        {
            takeDamage.Invoke(owner, damage, b, this);
        }
        return false;
    }

    public virtual void LastWords(PlayableUser owner, Board board)
    {
        //empty so card without lastwords wont do anything

        if (lastWords != null)
        {
            lastWords.Invoke(owner, board, this);
        }
    }

    public virtual int Attack()
    {
        return attack;
        //need to add that it sends the attack to the cards
    }

    public virtual void Adrenaline(PlayableUser owner, Board board)
    {
        if (adrenaline != null)
        {
            adrenaline.Invoke(owner, board, this);
        }
        //empty so card without adrenaline wont do anything, and access to the  board if it need it
    }

    public virtual void OnDeploy(PlayableUser owner, Board board)
    {
        if (onDeploy != null)
        {
            onDeploy.Invoke(owner, board, this);
        }
        //empty so card without ondep wont do anything
    }

    public override Card Clone()
    {
        return new Unit(cost, name, description, attack, Health, faction, id, imgUrl, taunt, HolyGuard, lastWords, takeDamage, adrenaline, onDeploy);
    }

    public override string ToString()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,      // pretty JSON
            IncludeFields = true       // include public fields like attack, cost, etc.
        };

        return JsonSerializer.Serialize(this, options);
    }
}

class InstaPlay : Card
{
    private Action<PlayableUser, Board> onDraw;
    public InstaPlay(string name, string description, int id, Action<PlayableUser, Board> onDraw, string imgUrl = "null.png") : base(name, description, id, imgUrl)
    {
        this.onDraw = onDraw;
    }

    public override Card Clone()
    {
        return new InstaPlay(name, description, id, onDraw, imgUrl);
    }

    public virtual void OnDraw(PlayableUser sender, Board board)
    {
        onDraw.Invoke(sender, board);
        //send what the card does then destroy it
    }

    public override string ToString()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,      // pretty JSON
            IncludeFields = true       // include public fields like attack, cost, etc.
        };

        return JsonSerializer.Serialize(this, options);
    }
}

class Artifact
{

}