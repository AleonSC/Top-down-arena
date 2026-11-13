namespace Arena
{
    /// The Warden's prisoner reskins share their base enemy's behaviour (Docs/Approaches.md): a Thug moves, hits and sounds
    /// like a Brute, a Shiv like a Lunger; so do the Hive's Bumble and Hornet (HiveKin.cs). Anything that asks "is it a Brute /
    /// a Lunger" goes through these.
    public partial class Game
    {
        public static bool IsBrute(EnemyType t) { t = HiveKin.Of(t); return t == EnemyType.Brute || t == EnemyType.Thug || t == EnemyType.Gravedigger; }  // (the Gravedigger: the Twin Reapers' cult, ReaperFaction.cs)
        public static bool IsLunger(EnemyType t) { t = HiveKin.Of(t); return t == EnemyType.Lunger || t == EnemyType.Shiv || t == EnemyType.ScytheWraith; }  // (the Scythe Wraith reaps along a longer, locked line: ReaperFaction.cs)
    }
}
