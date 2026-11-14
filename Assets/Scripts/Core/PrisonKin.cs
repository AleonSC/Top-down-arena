namespace Arena
{
    /// The Warden's prisoner reskins share their base enemy's behaviour (Docs/Approaches.md): a Thug moves, hits and sounds
    /// like a Brute, a Shiv like a Lunger; so do the Hive's Bumble and Hornet (HiveKin.cs). Anything that asks "is it a Brute /
    /// a Lunger" goes through these.
    public partial class Game
    {
        public static bool IsBrute(EnemyType t) => BrainHas(t, BrainRole.Brute) ?? false;  // Brute, Thug, Bumble, Gravedigger, Stomper, Gulper, Harlequin (ChaseBrains.cs)
        public static bool IsLunger(EnemyType t) => BrainHas(t, BrainRole.Lunger) ?? false;  // Lunger, Shiv, Hornet, Driller, Mummer, Fencer, Scythe Wraith (LungeBrains.cs)
    }
}
