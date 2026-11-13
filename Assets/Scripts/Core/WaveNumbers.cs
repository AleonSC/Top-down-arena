using System;

namespace Arena
{
    /// The wave-size numbers in one place (Docs/WaveRefactor.md step 1): how many enemies a regular wave holds and how fast the legacy spawner drips them in. `BuildWave` and `TickWaves` read them from here.
    /// The alive cap and spawn window of timed waves are in `TimedWaves`, a burst's size in `SpawnPortals`.
    public static class WaveNumbers
    {
        public const int BasePicks = 4, PicksPerWave = 2;     // a regular wave: 4 + 2 per wave...
        public const int PicksCapEarly = 32;                  // ...up to 32 before wave 20
        public const int LateFrom = 20, PicksCapLate = 40, LateGrowthWaves = 2;  // from wave 20 the cap grows 1 per 2 waves, to 40
        public const int OpeningFrom = 5;                     // waves 1-5 get a few extra picks (below)
        public const int FirstWaveExtra = 6;                  // wave 1 has the most (it was too quiet)
        public const float ThirdPlayerShare = 1.25f;          // three players: a quarter more enemies
        public const float OpeningDelay = 0.5f;               // seconds from the wave starting to its first spawn
        public const float SquadPause = 3f;                   // the spawn timer after a squad comes out is this many times longer

        /// A regular wave's number of picks, before the layout, map, party and difficulty change it.
        public static int Picks(int n)
        {
            int cap = n < LateFrom ? PicksCapEarly : Math.Min(PicksCapLate, PicksCapEarly + (n - LateFrom) / LateGrowthWaves);
            int count = Math.Min(BasePicks + n * PicksPerWave, cap);
            if (n <= OpeningFrom)
                count += n == 1 ? FirstWaveExtra : (7 - n) / 2;  // 12, 10, 12, 13, 14 picks on waves 1-5
            return count;
        }

        /// The legacy spawner's gap between singles (waves that aren't timed: designed, practice, Zombie Infection).
        public static float SpawnInterval(int wave) => Math.Max(0.3f, (wave <= 1 ? 0.7f : 1.1f) - wave * 0.05f);
    }

    /// What differs between the game modes about a wave's size. Quick Play and the Campaign build waves the same way
    /// (`Standard`); a Boss Hunt has its own rules, with more enemies per wave.
    public sealed class WaveRules
    {
        public float CountMult = 1f;  // times the wave's picks, after everything else

        public static readonly WaveRules Standard = new WaveRules();
        public static readonly WaveRules Hunt = new WaveRules { CountMult = 1f };  // the owner wants more enemies here; the number isn't picked yet
    }

    public partial class Game
    {
        /// The wave-size rules for this run's mode.
        public WaveRules Rules => Hunt != null ? WaveRules.Hunt : WaveRules.Standard;
    }
}
