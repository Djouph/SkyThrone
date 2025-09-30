using System.Data;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.Serialization;

class DataBase
{
    static Dictionary<int, Card> lookup = new Dictionary<int, Card>();

    // static Unit - = new(
    //     cost: -,
    //     picture: "-",
    //     name: "-",
    //     description: "-",
    //     attack: -,
    //     Health: -,
    //     id: -
    //     faction: Faction.-,
    //     );

    // static InstaPlay - = new(
    // picture: "-",
    // name: "-",
    // description: "-",
    // id: -,
    // onDraw: (b) =>
    // {

    // });


    ///small animals turorial (ids 3-12):
    static Unit b1 = new(
        cost: 3,
        picture: "pic",
        name: "Blast Charger",
        description: "OnDeploy: place an Instaplay card in the enemy's deck called doom charge",
        attack: 2,
        Health: 2,
        id: 1,
        faction: Faction.Beast,
        onDeploy: (b, card) =>
        {
            b.Infeltrait(2);
        });


    static InstaPlay i1 = new(
    picture: "pic",
    name: "doom charge",
    description: "when drawn destroy a card in your board, hand and deck",
    id: 2,
    onDraw: (b) =>
    {
        Console.WriteLine("doom charge explodes");
        int place = 0;
        Random rnd = new Random();
        if (b.current.board.Count != 0)
        {
            place = rnd.Next(0, b.current.board.Count);
            Console.WriteLine("The exposion kills " + b.current.board[place].name);
            b.kill((Unit)b.current.board[place]);
        }
        if (b.current.deck.Count != 0)
        {
            place = rnd.Next(0, b.current.deck.Count);
            Console.WriteLine("The exposion destorys card number " + place + " in your deck");
            b.current.deck.RemoveAt(place);
        }
        place = rnd.Next(0, b.current.hand.Count);
        Console.WriteLine("The exposion destroys card number " + place + " in your hand");
        b.current.hand.RemoveAt(place);
    });

    static Unit a1 = new(
    cost: 1,
    picture: "pic",
    name: "Carrier Hedgehog",
    description: "OnDeploy: draw a card",
    attack: 1,
    Health: 2,
    id: 3,
    faction: Faction.Small,
    onDeploy: (b, card) =>
    {
        b.Draw();
    });


    static Unit a2 = new(

       cost: 1,
       picture: "pic",
       name: "Evil Hedghog",
       description: "Adrenaline: get +1 attack",
       attack: 1,
       Health: 2,
       id: 4,
       faction: Faction.Small,
       adrenaline: (b, card) =>
       {
           card.attack += 1;
       }
       );

    static Unit a3 = new(

       cost: 1,
       picture: "pic",
       name: "Squirrel Scout",
       description: "no nut, no mercy!",
       attack: 1,
       Health: 1,
       id: 5,
       faction: Faction.Small
       );

    static Unit a4 = new(

       cost: 3,
       picture: "pic",
       name: "Owl Watcher",
       description: "opposing enemy has -1 attack",
       attack: 2,
       Health: 3,
       id: 6,
       faction: Faction.Small,
       adrenaline: (b, card) =>
        {
            Random rnd = new Random();

            int i = rnd.Next(0, b.current.board.Count);
            ((Unit)b.current.board[i]).attack -= 1;

        }
       );

    static Unit a5 = new(

       cost: 3,
       picture: "pic",
       name: "Ssssnake",
       description: "ssssss im a sssssnake",
       attack: 4,
       Health: 3,
       id: 7,
       faction: Faction.Small
       );


    static Unit a6 = new(

       cost: 6,
       picture: "pic",
       name: "Elephant",
       description: "Adrenaline: give another ally +1/+1",
       attack: 1,
       Health: 6,
       id: 8,
       faction: Faction.Small,
       adrenaline: (b, card) =>
    {

        for (int i = 0; i < b.p.board.Count; i++)
        {
            ((Unit)b.current.board[i]).attack += 1;
            ((Unit)b.current.board[i]).Health += 1;
        }

    }
 );


    static Unit a7 = new(

       cost: 3,
       picture: "pic",
       name: "Turtle Hatchling",
       description: "Adrenaline: +1 health",
       attack: 2,
       Health: 2,
       id: 9,
       faction: Faction.Small,
       adrenaline: (b, card) =>
       {
           card.Health += 1;
       }
       );


    static Unit a8 = new(

       cost: 5,
       picture: "pic",
       name: "LION",
       description: "OnDeploy: gain 1 attack for each other card you have on the board",
       attack: 5,
       Health: 5,
       id: 10,
       faction: Faction.Small,
       onDeploy: (b, card) =>
       {

           for (int i = 0; i < b.current.board.Count - 1; i++)
           {
               card.attack += 1;
           }

       }
       );

    static Unit a9 = new(
        cost: 2,
        picture: "pic",
        name: "macaque",
        description: "OnDeploy: give +1 max energy",
        attack: 1,
        Health: 1,
        id: 11,
        faction: Faction.Small,
        onDeploy: (b, card) =>
        {

            if (b.current.maxenergy < 11)
            {
                b.current.maxenergy++;
            }

        }
        );

    static Unit a10 = new(
        cost: 3,
        picture: "pic",
        name: "Capybara",
        description: "",
        attack: 3,
        Health: 3,
        id: 12,
        faction: Faction.Small,
        onDeploy: (b, card) =>
        {

            for (int i = 0; i < b.current.board.Count; i++)
            {
                ((Unit)b.current.board[i]).Health += 1;
            }


        }
        );

    /// robots (ids 13-22):
    static Unit r1 = new(
        cost: 2,
        picture: "pic",
        name: "ROBOT I",
        description: "OnDeploy: merge with another robot, if unsuccesful, kill this card",
        attack: 3,
        Health: 3,
        id: 13,
        faction: Faction.Robot,
        onDeploy: (b, card) =>
        {
            int Index = b.current.board.IndexOf(card);
            if (Index != 0)
            {
                if (((Unit)b.current.board[Index - 1]).faction == Faction.Robot)
                {
                    b.Merge(card, (Unit)b.current.board[Index - 1]);
                }
                else
                {
                    b.kill(card);
                }
            }
        }
        );


    static Unit r2 = new(
        cost: 1,
        picture: "pic",
        name: "ROBOT II",
        description: "just merge with me",
        attack: 1,
        Health: 2,
        id: 14,
        faction: Faction.Robot
        );

    static Unit r3 = new(
       cost: 3,
       picture: "pic",
       name: "The Builder",
       description: "OnDeploy: add ROBOT I and ROBOT II to your hand",
       attack: 1,
       Health: 2,
       id: 15,
       faction: Faction.Human,
       onDeploy: (b, Card) =>
       {
           b.current.hand.Add(CardFromId(13));
           b.current.hand.Add(CardFromId(14));
       }
       );

    static Unit r4 = new(
       cost: 2,
       picture: "pic",
       name: "UPgrader",
       description: "Adrenaline: give your first robot +1/+1",
       attack: 2,
       Health: 2,
       id: 16,
       faction: Faction.Robot,
        adrenaline: (b, Card) =>
       {
           for (int i = 0; i < b.current.board.Count; i++)
           {
               if (((Unit)b.current.board[i]).faction == Faction.Robot)
               {
                   ((Unit)b.current.board[i]).attack++;
                   ((Unit)b.current.board[i]).Health++;
                   break;
               }
           }
       }
    );


    static Unit r5 = new(
       cost: 6,
       picture: "pic",
       name: "OMEGA TITAN IV",
       description: "OnDeploy: merge all robots to this card, get +2/+2 for each card merged into this",
       attack: 5,
       Health: 6,
       id: 17,
       faction: Faction.Robot,
        onDeploy: (b, Card) =>
        {
            int Index = b.current.board.IndexOf(Card);
            for (int i = 0; i < b.current.board.Count; i++)
            {
                if (((Unit)b.current.board[i]).faction == Faction.Robot && i != Index)
                {
                    b.Merge(Card, ((Unit)b.current.board[i]));
                    Card.attack += 2;
                    Card.Health += 2;
                }
            }
        }
        );


    static Unit r6 = new(
       cost: 4,
       picture: "pic",
       name: "DOBLERBOT",
       description: "Adrenaline: double this card's power",
       attack: 1,
       Health: 1,
       id: 18,
       faction: Faction.Robot,
        adrenaline: (b, Card) =>
       {
           Card.attack = Card.attack * 2;
       }
    );

    static Unit r7 = new(
       cost: 2,
       picture: "pic",
       name: "ROB THE ROBOT",
       description: "Adrenaline: if you have another roobot, deal 1 damage to each enemy unit",
       attack: 2,
       Health: 2,
       id: 19,
       faction: Faction.Robot,
        adrenaline: (b, Card) =>
       {
           bool HaveRobot = false;
           for (int j = 0; j < b.current.board.Count; j++)
           {
               if ((((Unit)b.current.board[j]).faction) == Faction.Robot)
               {
                   for (int i = 0; i < b.other.board.Count; i++)
                   {
                       ((Unit)b.other.board[i]).TakeDamage(1, b);
                   }
               }
           }




       }
    );


    static Unit r8 = new(
       cost: 1,
       picture: "pic",
       name: "Magneticast",
       description: "OnDeploy: if you control a robot, draw a card from your deck that costs 5 or more",
       attack: 1,
       Health: 2,
       id: 20,
       faction: Faction.Robot,
        onDeploy: (b, Card) =>
       {

           for (int j = 0; j < b.current.board.Count; j++)
           {
               if (((Unit)b.current.board[j]) != Card)
               {
                   if (((Unit)b.current.board[j]).faction == Faction.Robot)
                   {
                       for (int i = 0; i < b.current.deck.Count; i++)
                       {
                           if (((Unit)b.current.deck[i]).cost >= 5)
                           {
                               b.current.hand.Add(b.current.deck[i]);
                               b.Remove(i);
                               break;
                           }
                       }
                   }
               }
           }

       }
    );

    static Unit r9 = new(
       cost: 2,
       picture: "pic",
       name: "D.N.A.E.C.O.A.H.C",
       description: "reduce the cost of robots in your hand by 1",
       attack: 1,
       Health: 2,
       id: 21,
       faction: Faction.Robot,
        onDeploy: (b, Card) =>
       {
           for (int i = 0; i < b.current.hand.Count; i++)
           {
               if (((Unit)b.current.hand[i]).faction == Faction.Robot)
               {
                   ((Unit)b.current.hand[i]).cost--;
               }
           }
       }
    );

    static Unit r10 = new(
       cost: 4,
       picture: "pic",
       name: "Repair Bot",
       description: "Adrenaline: Repairs it self to 4 health",
       attack: 3,
       Health: 4,
       id: 22,
       faction: Faction.Robot,
       adrenaline: (b, Card) =>
       {
           if (Card.Health < 4)
           {
               Card.Health = 4;
           }
       }
    );




    // undead ids => 23-34
    static Unit u1 = new(
        cost: 1,
        picture: "pic",
        name: "Ghoul Grunt",
        description: "Last Words: summon 1,1 Skeleton Grunt",
        attack: 1,
        Health: 1,
        id: 23,
        faction: Faction.Undead,
        lastWords: (b, Card) =>
        {
            if (b.current.board.Contains(Card))
            {
                int temp = b.current.board.IndexOf(Card);
                b.current.board[temp] = CardFromId(24);
            }
            else
            {
                int temp = b.other.board.IndexOf(Card);
                b.other.board[temp] = CardFromId(24);
            }
        }
        );

    static Unit u2 = new(
        cost: 1,
        picture: "pic",
        name: "Skeleton Grunt",
        description: "Grrrrr...",
        attack: 1,
        Health: 1,
        id: 24,
        faction: Faction.Undead
        );

    static Unit u3 = new(
        cost: 2,
        picture: "pic",
        name: "Ghoulling",
        description: "Last Words: give +1 ATK to all allies.",
        attack: 2,
        Health: 1,
        id: 25,
        faction: Faction.Undead,
        lastWords: (b, Card) =>
        {
            if (b.current.board.Contains(Card))
            {
                for (int i = 0; i < b.current.board.Count; i++)
                {
                    ((Unit)b.current.board[i]).attack++;
                }
            }
            else
            {
                for (int i = 0; i < b.other.board.Count; i++)
                {
                    ((Unit)b.other.board[i]).attack++;
                }
            }
        }
        );

    static Unit u4 = new(
        cost: 2,
        picture: "pic",
        name: "Bone Guard",
        description: "Last Words: give +1 health to all allies.",
        attack: 1,
        Health: 2,
        id: 26,
        faction: Faction.Undead,
        lastWords: (b, Card) =>
        {
            if (b.current.board.Contains(Card))
            {
                for (int i = 0; i < b.current.board.Count - 1; i++)
                {
                    ((Unit)b.current.board[i]).Health++;
                }
            }
            else
            {
                for (int i = 0; i < b.other.board.Count - 1; i++)
                {
                    ((Unit)b.other.board[i]).Health++;
                }
            }
        }
        );

    static Unit u5 = new(
        cost: 1,
        picture: "pic",
        name: "Plague Rat",
        description: "Adrenaline: Deal 1 damage to both players.",
        attack: 2,
        Health: 2,
        id: 27,
        faction: Faction.Undead,
        adrenaline: (b, Card) =>
        {
            b.p.health--;
            b.e.health--;
        }
        );

    static Unit u6 = new(
        cost: 3,
        picture: "pic",
        name: "Crypt Archer",
        description: "Adrenaline: Deal 2 damage to a random enemy.",
        attack: 3,
        Health: 1,
        id: 28,
        faction: Faction.Undead,
        adrenaline: (b, Card) =>
        {
            if (b.other.board.Count != 0)
            {
                Random rnd = new Random();
                int temp = rnd.Next(0, b.other.board.Count);
                ((Unit)b.other.board[temp]).TakeDamage(2, b);
            }
        }
        );

    static Unit u7 = new(
        cost: 3,
        picture: "pic",
        name: "Grave Knight",
        description: "Last Words, On Deploy: Draw a Card",
        attack: 2,
        Health: 2,
        id: 29,
        faction: Faction.Undead,
        onDeploy: (b, Card) =>
        {
            b.Draw();
        },
        lastWords: (b, Card) =>
        {
            b.Draw();
        }
        );

    static Unit u8 = new(
        cost: 3,
        picture: "pic",
        name: "Rotting Hulk",
        description: "Adrenaline: Gains +1, +1.",
        attack: 4,
        Health: 4,
        id: 30,
        faction: Faction.Undead,
        adrenaline: (b, Card) =>
        {
            Card.Health++;
            Card.attack++;
        }
        );

    static Unit u9 = new(
        cost: 5,
        picture: "pic",
        name: "Necrotic Horror",
        description: "Last Words: Summon two 1/1 Ghoul Grunt.",
        attack: 4,
        Health: 3,
        id: 31,
        faction: Faction.Undead,
        lastWords: (b, Card) =>
        {
            if (b.current.board.Contains(Card))
            {
                b.current.board.Add(CardFromId(23));
                b.current.board.Add(CardFromId(23));
            }
            else
            {
                b.other.board.Add(CardFromId(23));
                b.other.board.Add(CardFromId(23));
            }
        }
        );

    static Unit u10 = new(
        cost: 6,
        picture: "pic",
        name: "Lich King",
        description: "On Deploy: deal 3 damage to the player who played it, Last Words: Summon Wounded Lich King and deal deal 1 damage to all enemies",
        attack: 6,
        Health: 6,
        id: 32,
        faction: Faction.Undead,
        onDeploy: (b, Card) =>
        {
            b.current.health -= 3;
        },
        lastWords: (b, Card) =>
        {
            if (b.current.board.Contains(Card))
            {
                for (int i = 0; i < b.other.board.Count; i++)
                {
                    ((Unit)b.other.board[i]).TakeDamage(1, b);
                }
                int temp = b.current.board.IndexOf(Card);
                b.current.board[temp] = CardFromId(33);
            }
            else
            {
                for (int i = 0; i < b.current.board.Count; i++)
                {
                    ((Unit)b.current.board[i]).TakeDamage(1, b);
                }
                int temp = b.other.board.IndexOf(Card);
                b.other.board[temp] = CardFromId(33);
            }
        }
        );

    static Unit u11 = new(
    cost: 6,
    picture: "pic",
    name: "Wounded Lich King",
    description: "Last Words: Summon Dieing Lich King and deal deal 1 damage to all enemies",
    attack: 5,
    Health: 5,
    id: 33,
    faction: Faction.Undead,
    lastWords: (b, Card) =>
    {
        if (b.current.board.Contains(Card))
        {
            for (int i = 0; i < b.other.board.Count; i++)
            {
                ((Unit)b.other.board[i]).TakeDamage(1, b);
            }
            int temp = b.current.board.IndexOf(Card);
            b.current.board[temp] = CardFromId(34);
        }
        else
        {
            for (int i = 0; i < b.current.board.Count; i++)
            {
                ((Unit)b.current.board[i]).TakeDamage(1, b);
            }
            int temp = b.other.board.IndexOf(Card);
            b.other.board[temp] = CardFromId(34);
        }
    }
    );

    static Unit u12 = new(
    cost: 6,
    picture: "pic",
    name: "Dying Lich King",
    description: "Last Words: Summon Dieing Lich King and deal deal 1 damage to all enemies",
    attack: 5,
    Health: 4,
    id: 34,
    faction: Faction.Undead,
    lastWords: (b, Card) =>
    {
        if (b.current.board.Contains(Card))
        {
            for (int i = 0; i < b.other.board.Count; i++)
            {
                ((Unit)b.other.board[i]).TakeDamage(1, b);
            }
        }
        else
        {
            for (int i = 0; i < b.current.board.Count; i++)
            {
                ((Unit)b.current.board[i]).TakeDamage(1, b);
            }
        }
    }
    );

















    static DataBase() // c= unit, i= instaplay, r = robot, u = undead
    {
        //INSTAPLAYS:
        lookup.Add(i1.id, i1);
        //BEASTS:
        lookup.Add(b1.id, b1);
        //SMALL ANIMALS:
        lookup.Add(a1.id, a1);
        lookup.Add(a2.id, a2);
        lookup.Add(a3.id, a3);
        lookup.Add(a4.id, a4);
        lookup.Add(a5.id, a5);
        lookup.Add(a6.id, a6);
        lookup.Add(a7.id, a7);
        lookup.Add(a8.id, a8);
        lookup.Add(a9.id, a9);
        lookup.Add(a10.id, a10);
        //ROBOTS:
        lookup.Add(r1.id, r1);
        lookup.Add(r2.id, r2);
        lookup.Add(r3.id, r3);
        lookup.Add(r4.id, r4);
        lookup.Add(r5.id, r5);
        lookup.Add(r6.id, r6);
        lookup.Add(r7.id, r7);
        lookup.Add(r8.id, r8);
        lookup.Add(r9.id, r9);
        lookup.Add(r10.id, r10);
        //UNDEAD:
        lookup.Add(u1.id, u1);
        lookup.Add(u2.id, u2);
        lookup.Add(u3.id, u3);
        lookup.Add(u4.id, u4);
        lookup.Add(u5.id, u5);
        lookup.Add(u6.id, u6);
        lookup.Add(u7.id, u7);
        lookup.Add(u8.id, u8);
        lookup.Add(u9.id, u9);
        lookup.Add(u10.id, u10);
        lookup.Add(u11.id, u11);
        lookup.Add(u12.id, u12);











    }



    public static Card CardFromId(int id)
    {
        return lookup[id].Clone();
    }


}