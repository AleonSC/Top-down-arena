using System;
using System.Collections.Generic;

namespace Arena
{
    /// Bosses that keep up with strong builds. A build's power is a rough estimate of how many times harder it hits a
    /// boss than the same weapon with no upgrades (damage x fire rate x projectiles x crits x boss legendaries and
    /// companions). A typical build's power at each wave comes from builds picked at random from the same offers the
    /// game makes (an unoptimised player); a boss gets more health in step with how far the party is above that
    /// (damped by BossPowerCurve, at most x3). Weak and ordinary builds meet the usual boss.
    public partial class Game
    {
        public const float BossPowerCap = 3f;       // a boss gets at most this much more health
        public const float TypicalPercentile = 0.75f;  // "typical": this far up the random builds (a player who picks sensibly)
        public const float BossPowerCurve = 0.6f;     // boss health grows as (how far above typical) to this power: a strong build still kills faster

        /// How much harder this build hits a boss than its bare weapon (rough).
        public static float BuildPower(Stats s)
        {
            float dmg = s.DmgMult * (1f - HomingDamageCost * s.Homing);
            float rate = s.FireRateMult;
            float projectiles = (1f + 0.4f * s.ExtraShots) * (1f + 0.3f * s.Fan);  // measured: a Multishot pick ~+40%, a Barrage pick ~+20-40%
            float crits = 1f + Math.Min(1f, s.CritChance);
            float bossCards = (1f + 0.2f * s.ExecuteBonus) * (1f + 0.2f * s.ReapBossBonus) * (1f + s.SlowedBonus) * (1f + 0.5f * s.BurnFrac);
            float helpers = 1f + 0.2f * s.Guards + 0.15f * s.HiveDrones + 0.2f * s.TwinScythes + 0.1f * s.Bombers + (s.Searchlight > 0 ? 0.25f : 0f);
            return dmg * rate * projectiles * crits * bossCards * helpers;
        }

        /// The level a player typically has reached by a wave (from recorded runs: ~11 at wave 10, ~34 at wave 25).
        public static int TypicalLevel(int wave) => Math.Max(1, (int)Math.Round(1.35f * wave));

        static readonly Dictionary<long, float> typicalPower = new Dictionary<long, float>();

        /// The power of a typical build of this role and Space ability by this wave: builds picked at random from the
        /// game's own offers (level-ups, and a legendary and a mod per boss beaten), TypicalPercentile of the way up.
        public static float TypicalPower(Role role, AbilityId roleAbility, int wave)
        {
            long key = ((long)role << 40) | ((long)roleAbility << 20) | (uint)wave;
            float cached;
            lock (typicalPower)
                if (typicalPower.TryGetValue(key, out cached))
                    return cached;
            var rng = new Random(12345 + wave);  // the same answer every time
            var powers = new List<float>();
            for (int run = 0; run < 200; run++)
            {
                var taken = new List<int>();
                int levels = TypicalLevel(wave) - 1;
                for (int l = 0; l < levels; l++)
                {
                    var offer = Upgrades.RollOffer(rng, role, roleAbility, taken, l + 2);
                    if (offer != null && offer.Length > 0)
                        taken.Add(offer[rng.Next(offer.Length)]);
                }
                for (int b = 5; b < wave; b += 5)  // the bosses before this one: a legendary and a mod each
                {
                    var leg = Upgrades.LegendaryOffer(rng, BossOfWave(b), role, roleAbility, taken);
                    taken.Add(leg[rng.Next(leg.Length)]);
                    var mod = Upgrades.ModOffer(role, roleAbility, taken);
                    if (mod != null && mod.Length > 0)
                        taken.Add(mod[rng.Next(mod.Length)]);
                }
                powers.Add(BuildPower(Upgrades.Compute(taken)));
            }
            powers.Sort();
            float p = powers[Math.Min(powers.Count - 1, (int)(TypicalPercentile * powers.Count))];
            lock (typicalPower)
                typicalPower[key] = p;
            return p;
        }

        static EnemyType BossOfWave(int wave)
        {
            switch (wave)
            {
                case 5: return EnemyType.Warden;
                case 10: return EnemyType.HiveQueen;
                case 15: return EnemyType.TwinA;
                case 20: return EnemyType.Singularity;
                case 25: return EnemyType.Colossus;
                default: return EnemyType.Warden;  // later bosses: any legendaries (the offer tops up from others)
            }
        }

        /// How much more health a boss gets for this party: how far its average player is above a typical build at this
        /// wave, to the BossPowerCurve power, from x1 (typical or weaker) to BossPowerCap.
        public bool ScaleBosses = true;  // off: bosses keep their usual health whatever the build (the Boss Lab; tests that need a quick kill)

        public float BossPowerScale()
        {
            if (!ScaleBosses || Practice != null || Players.Count == 0)
                return 1f;
            float ratio = 0f;
            foreach (var p in Players.Values)
                ratio += BuildPower(p.Stats) / Math.Max(0.01f, TypicalPower(p.Role, p.RoleAbility, Wave));
            ratio /= Players.Count;
            return BossScaleFor(ratio);
        }

        /// Boss health for a party this many times stronger than typical.
        public static float BossScaleFor(float ratio) => Math.Max(1f, Math.Min(BossPowerCap, (float)Math.Pow(Math.Max(0f, ratio), BossPowerCurve)));
    }
}
