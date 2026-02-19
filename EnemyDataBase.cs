class EnemyDataBase
{

  public static Dictionary<int, Enemy> lookup = new Dictionary<int, Enemy>();


  // static Enemy - = new(
  //   playerId: -,
  //   build: -,
  // );

  static Enemy tutorialDaniel = new(
    playerId: 600,
    build: [600, 1, 10, 10, 1, 1, 2, 2, 3, 3, 4, 5, 5, 6, 6, 8, 8, 9, 9, 4]
  );






  // Enemy ids start from 600.

  static EnemyDataBase()
  {
    lookup.Add(tutorialDaniel.playerId, tutorialDaniel);
  }

  public static Enemy EnemyFromEnemyId(int id)
  {
    return lookup[id].Clone();
  }
}
