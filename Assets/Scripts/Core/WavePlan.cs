using System.Collections.Generic;

namespace Arena
{
    /// The wave being built (Docs/WaveRefactor.md step 2): everything the stages of `BuildWave` share, in one object that is
    /// made fresh for each wave. The finished wave is in `Game.spawnQueue` (singles) and `Game.squadQueue` (squads);
    /// the plan only holds the working state: the wave's size, the types it can draw from, and what the boss's faction has taken.
    public sealed class WavePlan
    {
        public readonly int N;                                   // the wave number
        public int Count;                                        // picks left to fill (the size after layout, map, party, difficulty and mode)
        public List<EnemyType> Pool;                             // the types a regular wave can draw from (Game.WavePool)
        public List<EnemyType> Melee;                            // the melee rushers the early waves draw from

        // The distinct types the wave has so far (the boss factions' included); once KindCap is reached only these can be drawn
        // again (WaveRecipe.cs). The factions, picked first, stop at ApproachKindCap so the headliners always have room.
        public readonly HashSet<EnemyType> Kinds = new HashSet<EnemyType>();
        public int KindCap = 99, ApproachKindCap = 99;

        // The boss faction's share (Approaches.cs), mixed into the wave once the rest is built.
        public readonly List<EnemyType> ApproachSingles = new List<EnemyType>();
        public readonly List<SquadDef> ApproachSquads = new List<SquadDef>();
        public readonly List<EnemyType> ApproachTrickle = new List<EnemyType>();  // a lesson's star: spread over the wave's second half

        public WavePlan(int n) { N = n; }
    }
}
