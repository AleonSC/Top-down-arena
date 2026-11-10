using System;

namespace Arena
{
    /// A Boss Hunt: a short run that ends with one boss (the lobby's third mode). Beat a boss in a full run to unlock
    /// its hunt. A hunt is ten waves: nine built only from the boss's own enemies (its faction, Approaches.cs), tougher
    /// ones joining as they go and its set piece on waves 5 and 9, then the boss on wave 10. Beating it ends the run.
    /// Hunts don't save, don't open challenge portals or offer paths, and keep their own records (Profile.HuntEnded).
    /// "A boss every five waves" doesn't hold in a hunt: code that asks which waves have bosses uses IsBossWave / BossAt.
    /// See Docs/BossHuntsAndPerks.md.
    public class HuntDef
    {
        public EnemyType Boss;
        public int HomeWave;     // the wave it's met at in a full run (for the lobby's text)
        public string Name, UnlockId;
    }

    public static class Hunts
    {
        public const int Length = 10;  // waves in a hunt, the boss's (the last) included

        /// Only the Warden so far: a hunt needs the boss's faction for its build-up waves (only the Warden has one), and
        /// later bosses will need the party handed a build that fits their strength (levels, legendaries and mods).
        public static readonly HuntDef[] All =
        {
            new HuntDef { Boss = EnemyType.Boss, HomeWave = 5, Name = "Warden Hunt", UnlockId = "hunt-warden" },
        };

        /// How far into its faction a build-up wave reaches, 1 to 4 (as the four approach waves of a run): enemies and
        /// squads marked From 2, 3, 4 join as the hunt goes on.
        public static int Step(int wave) => Math.Max(1, Math.Min(4, (wave + 1) / 2));

        /// A hunt is the boss's faction turned up (Docs/Campaign.md): every member and squad from the first wave, more
        /// enemies than a run's wave (1.2x on wave 1 up to 1.8x on wave 9), and a faction squad in every wave, two from wave 6.
        public static float CountFactor(int wave) => 1.2f + 0.075f * (Math.Max(1, Math.Min(9, wave)) - 1);
        public static int SquadsPerWave(int wave) => wave >= 6 ? 2 : 1;
        public const int Reach = 99;

        /// The waves that bring the faction's set piece (the Warden's riot line).
        public static bool FinaleWave(int wave) => wave == 5 || wave == Length - 1;

        public static HuntDef Get(EnemyType boss) => Array.Find(All, h => h.Boss == boss);

        /// Has this profile unlocked the hunt? (The boss beaten counts even from before hunts existed.)
        public static bool Available(HuntDef h, Profile p) => h != null && p != null && (p.IsUnlocked(h.UnlockId) || p.Beaten(h.Boss) > 0);
    }

    public partial class Game
    {
        /// The Boss Hunt being played (host: from the lobby when the match starts; clients: told by the host), or null.
        public HuntDef Hunt;

        /// The hunt's wave number, 1 to Hunts.Length (0 before the first).
        public int HuntWave => Hunt == null ? 0 : Math.Max(0, Wave);

        /// The run's last wave: 50 (before endless), or a hunt's tenth.
        public int LastWave => Hunt != null ? Hunts.Length : MaxWave;

        /// Does a boss belong to this wave? In a run the boss comes after every fifth regular wave, as its own fight that
        /// doesn't use up a wave number (BossSlot); only the last wave of a hunt is a boss. For "is the boss being fought
        /// (or just beaten)" ask BossNow, for "is the boss next" BossNext.
        public bool IsBossWave(int wave) => Hunt != null ? wave >= Hunts.Length : wave > 0 && wave % 5 == 0;

        /// Run: the boss after regular wave `Wave` is being fought, or was beaten and the wave box is up. The wave counter
        /// doesn't move for it (the next regular wave is Wave + 1). Replicated; saved (RunSaves.cs).
        public bool BossSlot;

        /// The wave being fought now (or just cleared) is a boss fight.
        public bool BossNow => Hunt != null ? IsBossWave(Wave) : BossSlot;

        /// The next fight to start is a boss (the wave box between waves shows it).
        public bool BossNext => Hunt != null ? IsBossWave(Wave + 1) : !BossSlot && IsBossWave(Wave);

        /// Host: the fight that starts next is the boss after wave `Wave` (and not wave `Wave + 1`).
        bool BossStartsNext => Hunt == null && Practice == null && !BossSlot && IsBossWave(Wave);

        /// The boss of this wave, or of the next boss wave after it.
        public EnemyType BossAt(int wave) => Hunt != null ? Hunt.Boss : BossForWave(wave);

        /// The boss wave being fought now (fighting on a boss wave) or coming next.
        public int NextBossWave(int wave, bool fighting) =>
            Hunt != null ? Hunts.Length : BossSlot ? (fighting ? wave : (wave / 5 + 1) * 5) : Math.Max(5, (wave + 4) / 5 * 5);

        /// The approach's boss and its step (1-4; 0 on a boss wave), for a run or a hunt (Approaches.cs, ApproachUi.cs).
        public EnemyType ApproachBossAt(int wave) => Hunt != null ? Hunt.Boss : ApproachBoss(wave);
        public int ApproachStepAt(int wave) => Hunt != null ? (IsBossWave(wave) ? 0 : Hunts.Step(wave)) : ApproachStep(wave);
    }
}
