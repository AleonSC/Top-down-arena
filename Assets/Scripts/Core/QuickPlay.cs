using System;
using System.Collections.Generic;

namespace Arena
{
    /// Quick Play (Docs/QuickPlay.md): the lobby's first mode and a new player's first run. Ten waves, then a random boss as
    /// its own fight after wave 10 (the Campaign's boss slot); win, then end the run or keep going: a random boss after
    /// every tenth wave, for ever. The waves before a boss are its own faction's (as a Boss Hunt's), or the usual mix for a
    /// boss without one yet. A profile's first Quick Play always meets the Warden. Bosses play exactly as anywhere else;
    /// only their health is set for a fight of about two minutes at whatever wave they come (QuickBossHp).
    /// No paths, Data Gates or saving; its own records (Profile.QuickEnded).
    public static class QuickPlay
    {
        public const int Length = 10;          // regular waves before the first boss (and between bosses in endless)
        public const int OrderLength = 100;    // bosses drawn at the start (wave 1000 before the order repeats)
        public const float FightSeconds = 120f;  // the boss fight it aims for with a typical build
        public const float HomeFightSeconds = 90f;
        public static EnemyType[] OrderOverride;  // the Boss Lab and tests: the boss order the next Quick Play plays (null: drawn)  // what a boss's fight lasts at its own Campaign wave (Balance.cs: 60-120 s)

        /// The run's bosses, in order: shuffled bags of all ten (none twice until every one has come up, never the same boss
        /// twice in a row), the Warden first on a profile's first Quick Play.
        public static EnemyType[] MakeOrder(Random rng, bool wardenFirst)
        {
            var order = new List<EnemyType>();
            var all = Game.BossCycle;
            while (order.Count < OrderLength)
            {
                var bag = new List<EnemyType>(all);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var t = bag[i]; bag[i] = bag[j]; bag[j] = t;
                }
                if (order.Count == 0 && wardenFirst)
                {
                    bag.Remove(EnemyType.Boss);
                    bag.Insert(0, EnemyType.Boss);
                }
                if (order.Count > 0 && bag[0] == order[order.Count - 1])
                {
                    var t = bag[0]; bag[0] = bag[1]; bag[1] = t;  // (no repeat across the bags' seam)
                }
                order.AddRange(bag);
            }
            return order.ToArray();
        }

        /// The wave a boss is met at in a Campaign's 50-wave track (its fight is tuned there: Balance.BossHpMult).
        public static int HomeWave(EnemyType boss)
        {
            if (boss == EnemyType.TwinB || boss == EnemyType.DualReaper)
                boss = EnemyType.TwinA;
            int i = Array.IndexOf(Game.BossCycle, boss);
            return i < 0 ? 5 : (i + 1) * 5;
        }
    }

    /// Quick Play's boss schedule: a boss after every tenth wave, from the run's order.
    public class QuickBosses : BossSchedule
    {
        public readonly EnemyType[] Order;
        public QuickBosses(EnemyType[] order) { Order = order; }
        const int Every = QuickPlay.Length;
        public override bool Slotted => true;
        public override bool IsBossWave(int wave) => wave > 0 && wave % Every == 0;
        public override EnemyType BossAt(int wave) => Order[(Math.Max(Every, wave) / Every - 1) % Order.Length];
        public override int NextBossWaveFrom(int wave) => Math.Max(Every, (wave + Every - 1) / Every * Every);
        public override int LastBossWave(int wave) => Math.Max(0, wave - wave % Every);

        /// Through the coming boss's faction as a Boss Hunt's build-up goes: steps 1, 1, 2, 2, 3, 3, 4, 4, 4, 4.
        public override int ApproachStep(int wave) => wave <= 0 ? 0 : Hunts.Step((wave - 1) % Every + 1);

        /// The set piece on the fifth and tenth wave of each ten.
        public override bool FinaleWave(int wave) => wave > 0 && ((wave - 1) % Every == 4 || (wave - 1) % Every == Every - 1);
    }

    public partial class Game
    {
        /// This run is a Quick Play (set by the start message; QuickPlay.cs).
        public bool Quick;

        /// Quick Play's boss order (the start message carries it, so every machine agrees).
        public EnemyType[] QuickOrder;
        QuickBosses quickBosses;

        BossSchedule QuickSchedule()
        {
            if (quickBosses == null || quickBosses.Order != QuickOrder)
                quickBosses = new QuickBosses(QuickOrder);
            return quickBosses;
        }

        /// Host: Quick Play bosses beaten this run (for the records).
        public int QuickBossesBeaten;

        /// A Quick Play boss's health: its Campaign health at its own wave (a fight of about 90 s for a typical build there),
        /// grown with how much harder a typical build hits by this wave than by that one, and stretched to about two minutes.
        /// The same rule after wave 10, 20 or 300; the strong-build boost (BossPowerScale) still comes on top. Nothing per boss.
        public float QuickBossHp(EnemyType type, int wave)
        {
            int home = QuickPlay.HomeWave(type);
            float ratio = 1f;
            if (Players.Count > 0)
            {
                ratio = 0f;
                foreach (var p in Players.Values)
                    ratio += TypicalPower(p.Role, p.RoleAbility, Math.Max(1, wave)) / Math.Max(0.01f, TypicalPower(p.Role, p.RoleAbility, home));
                ratio /= Players.Count;
            }
            return SpawnHp(type, home) * ratio * (QuickPlay.FightSeconds / QuickPlay.HomeFightSeconds);
        }
    }
}
