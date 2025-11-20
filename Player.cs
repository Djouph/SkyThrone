using System.Security.Cryptography.X509Certificates;

class Enemy : Player
{
    public Enemy() : base(null)
    {
    }
}

class Player
{
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



    // public List<Artifact> artifacts;


    public Player(List<int> build)
    {
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

}