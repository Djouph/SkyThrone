using System.Security.Cryptography.X509Certificates;

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

    public abstract void Prepere();
}

class Enemy : PlayableUser
{
    public Enemy(int playerId, List<int> build) : base(playerId, build)
    {

    }

    public override void Prepere()
    {
        throw new NotImplementedException();
    }
}

class Player : PlayableUser
{
    // public List<Artifact> artifacts;

    public Player(int playerId, List<int> build) : base(playerId, build)
    {

    }

    public override void Prepere()
    {
        throw new NotImplementedException();
    }
}