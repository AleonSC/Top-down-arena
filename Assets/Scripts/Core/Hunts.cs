using System;

namespace Arena
{
    /// A Boss Hunt: a short run that ends with one boss (the lobby's third mode). Beat a boss in a full run to unlock
    /// its hunt. A hunt is ten waves: nine built only from the boss's own enemies (its faction, Approaches.cs), tougher
    /// ones joining as they go and its set piece on waves 5 and 9, then the boss on wave 10. Beating it ends the run.
    /// Hunts don't save, don't open challenge portals or offer paths, and keep their own records (Profile.HuntEnded).
    /// "A boss every five waves" doesn't hold in a hunt: code that asks which waves have bosses uses the run's BossSchedule (Game.Bosses).
    /// See Docs/BossHuntsAndPerks.md.
    public class HuntDef
    {
        public EnemyType Boss;
        public int HomeWave;     // the wave it's met at in a full run (for the lobby's text)
        public string Name, UnlockId;
    }

    public static class Hunts
    {
        public const int Length = 10;      // waves in the short hunt, the boss's (the last) included
        public const int LongLength = 20;  // the long hunt (Docs/LongHunt.md); a run's own length is Game.HuntLength

        /// Only the Warden so far: a hunt needs the boss's faction for its build-up waves (only the Warden has one), and
        /// later bosses will need the party handed a build that fits their strength (levels, legendaries and mods).
        public static readonly HuntDef[] All =
        {
            new HuntDef { Boss = EnemyType.Warden, HomeWave = 5, Name = "Warden Hunt", UnlockId = "hunt-warden" },
        };

        /// How far into its faction a build-up wave reaches, 1 to 4 (as the four approach waves of a run): enemies and
        /// squads marked From 2, 3, 4 join as the hunt goes on.
        public static int Step(int wave, int length = Length) => Math.Max(1, Math.Min(4, (wave * Length / length + 1) / 2));

        /// The waves that bring the faction's set piece (the Warden's riot line).
        public static bool FinaleWave(int wave, int length = Length) => wave == length / 2 || wave == length - 1;

        public static HuntDef Get(EnemyType boss) => Array.Find(All, h => h.Boss == boss);

        /// Has this profile unlocked the hunt? (The boss beaten counts even from before hunts existed.)
        public static bool Available(HuntDef h, Profile p) => h != null && p != null && (p.IsUnlocked(h.UnlockId) || p.Beaten(h.Boss) > 0);
    }

    public partial class Game
    {
        /// The Boss Hunt being played (host: from the lobby when the run starts; clients: told by the host), or null.
        public HuntDef Hunt;

        /// The hunt's wave number, 1 to HuntLength (0 before the first).
        /// This hunt's waves (Hunts.Length, or Hunts.LongLength for a long hunt), the boss's included: set when the run starts (Session).
        public int HuntLength = Hunts.Length;

        /// The wave that enemy health and damage scale by: a long hunt's twenty steps are as tough as the short one's ten (each step
        /// counts half a wave), so it is twice as long, not twice as hard (Docs/LongHunt.md).
        public int StrengthWave(int wave) => Hunt != null && HuntLength > Hunts.Length && wave > 0 ? (wave * Hunts.Length + HuntLength - 1) / HuntLength : wave;

        public int HuntWave => Hunt == null ? 0 : Math.Max(0, Wave);

        /// The run's last wave: 50 (before endless), or a hunt's tenth.
        public int LastWave => Hunt != null ? HuntLength : Quick ? QuickPlay.Length : MaxWave;

        /// The Campaign's 100 waves (Docs/RunLength.md step 2): each wave of the 50-wave track is fought twice, as two
        /// "legs", so the player sees waves 1-100 with a boss after every tenth, while everything that scales or unlocks by
        /// wave (enemy health, damage, fire rate, elites, new types, squads, approaches...) reads Wave, the track's wave:
        /// wave 37 plays as wave 19 does on the track (effective wave = shown wave / 2, rounded up). Per-wave rewards (XP,
        /// kill gold, vectors, chests) are halved (Pace) so a build reaches each boss as strong as before. Off for Boss Hunts,
        /// the Boss Lab, the lobby preview and runs saved before it (LongRun false: the old 50 waves).
        public bool LongRun;                          // this run is a 100-wave Campaign (set by the start message; saved)
        public static bool LongRunDefault = true;     // a new run from the lobby is (the sim tests turn it off for the older suites)
        public int Leg;                               // 0: the first of the wave's two legs, 1: the second (replicated; saved)
        public bool LongLegs => LongRun && ModeRules.Legs && !ShowcaseMode && !LabRun;  // (a run with lobby bots is still the Campaign: Expanding arena, building)
        bool LegDone => !LongLegs || Leg == 1;
        bool FirstLeg => !LongLegs || Leg == 0;       // once-a-wave things happen on a Campaign wave's first leg only        // the track's wave is done with this fight (the boss or the next wave is next)

        /// The wave number the player sees (1-100 in a Campaign).
        public int ShownWave => LongLegs && Wave > 0 ? Wave * 2 - 1 + Leg : Wave;

        /// The shown numbers of a track wave's first and last legs (ShownEnd: the wave a boss follows, in a Campaign).
        public int ShownStart(int wave) => LongLegs ? wave * 2 - 1 : wave;
        public int ShownEnd(int wave) => LongLegs ? wave * 2 : wave;

        /// The track wave of the next regular fight (a Campaign's second leg is the same track wave again).
        public int NextTrackWave => LongLegs && Wave > 0 && Leg == 0 && !BossSlot ? Wave : Wave + 1;

        /// The run's last wave as the player sees it (100 in a Campaign).
        public int ShownLastWave => LongLegs ? MaxWave * 2 : LastWave;

        /// The wave as records keep it (best wave, Recent runs, the run's curve): the shown wave in a Campaign (1-100), the
        /// wave itself otherwise (a 50-wave run saved before the Campaign; a Boss Hunt keeps its own, HuntWave).
        public int RecordWave => LongLegs ? ShownWave : Wave;

        /// Per-wave rewards: half in a Campaign, which has twice the waves.
        public float Pace => LongLegs ? 0.5f : 1f;

        /// Does a boss belong to this wave? Asks the run's boss schedule (BossSchedule.cs): in a run the boss comes after
        /// every fifth regular wave, as its own fight that doesn't use up a wave number (BossSlot); in a hunt the last wave
        /// is the boss. For "is the boss being fought (or just beaten)" ask BossNow, for "is the boss next" BossNext.
        public bool IsBossWave(int wave) => Bosses.IsBossWave(wave);

        /// Run: the boss after regular wave `Wave` is being fought, or was beaten and the wave box is up. The wave counter
        /// doesn't move for it (the next regular wave is Wave + 1). Replicated; saved (RunSaves.cs).
        public bool BossSlot;

        /// The wave being fought now (or just cleared) is a boss fight.
        public bool BossNow => Bosses.Slotted ? BossSlot : IsBossWave(Wave);

        /// The next fight to start is a boss (the wave box between waves shows it).
        public bool BossNext => Bosses.Slotted ? !BossSlot && IsBossWave(Wave) && LegDone : IsBossWave(Wave + 1);

        /// Host: the fight that starts next is the boss after wave `Wave` (and not wave `Wave + 1`).
        bool BossStartsNext => Bosses.Slotted && !BossSlot && IsBossWave(Wave) && LegDone;

        /// The boss of this wave, or of the next boss wave after it.
        public EnemyType BossAt(int wave) => Bosses.BossAt(wave);

        /// The boss wave being fought now (fighting on a boss wave) or coming next.
        public int NextBossWave(int wave, bool fighting) =>
            !Bosses.Slotted ? Bosses.NextBossWaveFrom(wave) : BossSlot ? (fighting ? wave : Bosses.NextBossWaveFrom(wave + 1)) : Bosses.NextBossWaveFrom(wave);

        /// The approach's boss and its step (1-4; 0 on a boss wave), for a run or a hunt (Approaches.cs, ApproachUi.cs).
        public EnemyType ApproachBossAt(int wave) => Bosses.BossAt(Bosses.NextBossWaveFrom(wave));
        public int ApproachStepAt(int wave) => Bosses.ApproachStep(wave);
    }
}
