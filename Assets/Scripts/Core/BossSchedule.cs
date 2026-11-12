using System;

namespace Arena
{
    /// Which waves of a run have a boss, and which boss (Docs/QuickPlay.md step 0). Every mode plugs in its own: the
    /// Campaign's rotation (a boss after every fifth wave, BossCycle), a Boss Hunt's one boss on its last wave, and later
    /// Quick Play's. Code that asks about bosses and waves goes through the run's schedule (Game.Bosses); code that means
    /// the Campaign's order itself (the Boss Lab, the Boss Hunt list, the Campaign's faction mix) uses BossSchedule.Campaign.
    /// The bosses themselves don't know about waves: what they do depends only on their type.
    public abstract class BossSchedule
    {
        /// The Campaign's: a different boss after every fifth wave, cycling through all ten.
        public static readonly BossSchedule Campaign = new CampaignBosses();

        /// Does a boss belong to this wave? Slotted: the boss fight after it (Game.BossSlot); otherwise the wave is the boss.
        public abstract bool IsBossWave(int wave);

        /// The boss of this boss wave (or, between them, of the last boss wave before it; the first one before any).
        public abstract EnemyType BossAt(int wave);

        /// The first boss wave at or after this one.
        public abstract int NextBossWaveFrom(int wave);

        /// The last boss wave at or before this one (0 before the first).
        public abstract int LastBossWave(int wave);

        /// The boss is its own fight after its wave (a run: Game.BossSlot, it doesn't use up a wave number), not the wave itself (a hunt).
        public abstract bool Slotted { get; }

        /// How far into the coming boss's faction this regular wave reaches, 1 to 4 (0 on a boss wave or before wave 1).
        public abstract int ApproachStep(int wave);

        /// The wave brings the coming boss's set piece (the Warden's riot line).
        public abstract bool FinaleWave(int wave);

        /// The first boss wave.
        public int FirstBossWave => NextBossWaveFrom(1);
    }

    /// The Campaign's (and every run's before Quick Play): a boss after every fifth wave, in BossCycle's order, then round again.
    public class CampaignBosses : BossSchedule
    {
        public const int Every = 5;
        public override bool Slotted => true;
        public override bool IsBossWave(int wave) => wave > 0 && wave % Every == 0;
        public override EnemyType BossAt(int wave) => Game.BossCycle[(Math.Max(Every, wave) / Every - 1) % Game.BossCycle.Length];
        public override int NextBossWaveFrom(int wave) => Math.Max(Every, (wave + Every - 1) / Every * Every);
        public override int LastBossWave(int wave) => Math.Max(0, wave - wave % Every);
        public override int ApproachStep(int wave) => wave <= 0 ? 0 : Math.Min(4, (wave - 1) % Every + 1);
        public override bool FinaleWave(int wave) => (wave - 1) % Every == Every - 1;
    }

    /// A Boss Hunt's (Hunts.cs): its one boss on the last wave, the build-up waves before it.
    public class HuntBosses : BossSchedule
    {
        public readonly HuntDef Hunt;
        public HuntBosses(HuntDef hunt) { Hunt = hunt; }
        public override bool Slotted => false;
        public override bool IsBossWave(int wave) => wave >= Hunts.Length;
        public override EnemyType BossAt(int wave) => Hunt.Boss;
        public override int NextBossWaveFrom(int wave) => Hunts.Length;
        public override int LastBossWave(int wave) => wave >= Hunts.Length ? Hunts.Length : 0;
        public override int ApproachStep(int wave) => IsBossWave(wave) ? 0 : Hunts.Step(wave);
        public override bool FinaleWave(int wave) => Hunts.FinaleWave(wave);
    }

    public partial class Game
    {
        HuntBosses huntBosses;

        /// This run's boss schedule: the hunt's in a Boss Hunt, the Campaign's otherwise.
        public BossSchedule Bosses
        {
            get
            {
                if (Hunt == null)
                    return BossSchedule.Campaign;
                if (huntBosses == null || huntBosses.Hunt != Hunt)
                    huntBosses = new HuntBosses(Hunt);
                return huntBosses;
            }
        }
    }
}
