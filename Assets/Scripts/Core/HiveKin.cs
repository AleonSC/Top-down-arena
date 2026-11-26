namespace Arena
{
    /// The Hive Queen's faction (Docs/HiveFaction.md): eight bees, each in the role of one of the Warden's enemies, so the
    /// Hive's cycle teaches in the Warden chart's order: Worker (a Rioter's rush), Soldier (a Guard), Comb Shield (a shield
    /// bearer), Honey Lobber (a lobber whose puddle slows), Stinger (a Taser Guard's bolt), Needler (a Security Guard's
    /// pistol), Bumble (a brute) and Hornet (a lunger). Each one's behaviour is its brain (Brain.cs and the *Brains.cs
    /// files, Docs/EnemyBehaviours.md); the looks (Game/HiveLook.cs) and Defs are their own.
    public static class HiveKin
    {
        /// Is it one of the Hive's eight bees?
        static readonly bool[] members = IdTable.Set(EnemyType.Worker, EnemyType.Soldier, EnemyType.CombShield, EnemyType.HoneyLobber, EnemyType.Stinger, EnemyType.Needler, EnemyType.Bumble, EnemyType.Hornet);  // (a list, not a range: ids are in name order)
        public static bool Is(EnemyType t) => (int)t < members.Length && members[(int)t];
    }
}
