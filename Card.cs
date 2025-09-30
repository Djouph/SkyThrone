abstract class Card
{
    public string picture;
    public string name;
    public string description;
    public readonly int id;

    public Card(string picture, string name, string description, int id)
    {
        this.picture = picture;
        this.name = name;
        this.description = description;
        this.id = id;
    }

    public abstract Card Clone();


}

class Unit : Card
{
    public int cost;
    public int attack;
    public int Health;
    public Faction faction;

    private Action<int, Board> takeDamage;
    private Action<Board, Unit> adrenaline;
    private Action<Board, Unit> onDeploy;
    private Action<Board, Unit> lastWords;
    public Unit(int cost, string picture, string name, string description, int attack, int Health, Faction faction, int id, Action<Board, Unit> lastWords = null!, Action<int, Board> takeDamage = null!, Action<Board, Unit> adrenaline = null!, Action<Board, Unit> onDeploy = null!) : base(picture, picture, description, id)
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
    }

    public virtual bool TakeDamage(int damage, Board b)
    {
        if (takeDamage == null)
        {
            Health -= damage;
            if (Health <= 0)
            {
                b.kill(this);
                return true;
            }
        }
        else
        {
            takeDamage.Invoke(damage, b);
        }
        return false;
    }

    public virtual void LastWords(Board board)
    {
        //empty so card without lastwords wont do anything

        if (lastWords != null)
        {
            lastWords.Invoke(board, this);
        }
    }

    public virtual int Attack()
    {
        return attack;
        //need to add that it sends the attack to the cards
    }

    public virtual void Adrenaline(Board board)
    {
        if (adrenaline != null)
        {
            adrenaline.Invoke(board, this);
        }
        //empty so card without adrenaline wont do anything, and access to the  board if it need it
    }

    public virtual void OnDeploy(Board board)
    {
        if (onDeploy != null)
        {
            onDeploy.Invoke(board, this);
        }
        //empty so card without ondep wont do anything
    }

    public override Card Clone()
    {
        return new Unit(cost, picture, name, description, attack, Health, faction, id, lastWords, takeDamage, adrenaline, onDeploy);
    }
}

class InstaPlay : Card
{
    private Action<Board> onDraw;
    public InstaPlay(string picture, string name, string description, int id, Action<Board> onDraw) : base(picture, name, description, id)
    {
        this.onDraw = onDraw;
    }

    public override Card Clone()
    {
        return new InstaPlay(picture, name, description, id, onDraw);
    }

    public virtual void OnDraw(Board board)
    {
        onDraw.Invoke(board);
        //send what the card does then destroy it
    }
}

class Artifact
{

}