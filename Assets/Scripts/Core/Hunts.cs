using System;

namespace Arena
{
    /// A Boss Hunt: a short run that ends with one boss (the lobby's third mode). Beat a boss in a full run to unlock
    /// its hunt. A hunt plays the boss's own stretch of five waves (the four before it, fully themed by its faction, then
    /// the boss at its usual wave), so every "boss every five waves" rule stays as it is, and winning ends the run.
    /// Hunts don't save, don't open challenge portals or offer paths, and keep their own records (Profile.HuntEnded).
    /// See Docs/BossHuntsAndPerks.md.
    public class HuntDef
    {
        public EnemyType Boss;
        public int BossWave;     // the wave it's fought at, as in a full run
        public string Name, UnlockId;

        /// The hunt's first wave: the start of the boss's stretch.
        public int FirstWave => Math.Max(1, BossWave - Hunts.Length + 1);
    }

    public static class Hunts
    {
        public const int Length = 5;  // waves in a hunt, the boss's included

        /// Only the Warden so far. Later bosses' stretches start mid-run, so their hunts will need the party handed a
        /// build that fits the wave (levels, legendaries and mods) before they can be added here.
        public static readonly HuntDef[] All =
        {
            new HuntDef { Boss = EnemyType.Boss, BossWave = 5, Name = "Warden Hunt", UnlockId = "hunt-warden" },
        };

        public static HuntDef Get(EnemyType boss) => Array.Find(All, h => h.Boss == boss);

        /// Has this profile unlocked the hunt? (The boss beaten counts even from before hunts existed.)
        public static bool Available(HuntDef h, Profile p) => h != null && p != null && (p.IsUnlocked(h.UnlockId) || p.Beaten(h.Boss) > 0);
    }

    public partial class Game
    {
        /// The Boss Hunt being played (host: from the lobby when the match starts; clients: told by the host), or null.
        public HuntDef Hunt;

        /// The hunt's wave number, 1 to Hunts.Length (0 before the first).
        public int HuntWave => Hunt == null ? 0 : Math.Max(0, Wave - Hunt.FirstWave + 1);
    }
}
