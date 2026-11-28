using System;
using System.Collections.Generic;

namespace Arena
{
    /// Blood Garden: the Healer Hemomancer's fourth ultimate (key 3 of the lobby's Ultimate row; Sanguine Rain stays the
    /// default, Heartbeat is key 1, Heart of the Swarm key 2), from the mockup (claude.ai artifact "Blood Garden Ult").
    ///   - Costs BgCost of your current health (AbilityDef.HealthCost, CostsCurrent). Flings BgSeeds (BgSeedsMastered mastered) blood
    ///     seeds round you, BgMin..BgMax px out, evenly spread with a jitter (FxKind.BloodSeed draws each flight).
    ///   - Each seed lands after its flight (Zone.Aim, replicated: the age it lands at) and sprouts a `ZoneKind.BloodSapling`,
    ///     BgAura px across, for BgLife s.
    ///   - Enemies inside are slowed BgSlow (Heavy enemies and bosses wade instead) and siphoned BgSiphon HP a second, as a hit
    ///     from you every BgTick s (your damage multiplier scales it).
    ///   - The health siphoned fills the sapling's bulb (Zone.Soaked; GuardFrac replicated, its fraction of BgBulb). Full, it bursts
    ///     and drops a blood orb anyone can drink (Owner 0) that heals BgOrbHeal, and the sapling is gone. Unfilled, it withers.
    /// The saplings stay when you go down: they're planted.
    public partial class Game
    {
        public const int BgSeeds = 12, BgSeedsMastered = 16;
        public const float BgCost = 0.3f, BgMin = 60f, BgMax = 260f, BgFlyMin = 0.5f, BgFlyMax = 0.85f;
        public const float BgAura = 90f, BgLife = 12f, BgSlow = 0.4f, BgSiphon = 12f, BgTick = 0.5f, BgBulb = 60f;
        public const float BgOrbHeal = 25f, BgOrbLife = 12f, BgSprout = 0.4f;
        public static readonly Rgba BgRed = new Rgba(0.85f, 0.16f, 0.25f);

        internal static readonly AbilityDef[] GardenUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.BloodGarden, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Blood Garden", Cooldown = 80f, HealthCost = BgCost, CostsCurrent = true,
                Desc = "Pay 30% of your health to fling 12 blood seeds around you. Their saplings slow and siphon nearby enemies, then burst into healing orbs.",
                Capstone = "Fling 16 seeds.",
                Bot = b => b.NearEnemies >= 4 && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
        };

        /// Seconds since the seed was flung.
        public static float BgAge(Zone z) => z.Duration - z.T;
        /// The sapling has sprouted (its seed has landed): it slows, siphons and fills.
        public static bool BgRooted(Zone z) => BgAge(z) >= z.Aim;

        void CastGarden(Player p, bool cap)
        {
            int n = cap ? BgSeedsMastered : BgSeeds;
            float turn = RandAngle();
            for (int i = 0; i < n; i++)
            {
                float a = turn + i * MathX.Tau / n + Rand(-0.2f, 0.2f), r = Rand(BgMin, BgMax), fly = Rand(BgFlyMin, BgFlyMax) + i * 0.02f;
                V2 at = ClampToArena(p.Pos + V2.FromAngle(a) * r, 30f);
                var z = AddZone(ZoneKind.BloodSapling, p, at, BgAura, fly + BgLife);
                z.Aim = fly;
                BroadcastFx(FxKind.BloodSeed, p.Pos, at, BgRed, 6f, fly, Sfx.None);
            }
            BroadcastFx(FxKind.Ring, p.Pos, BgRed, 70f, 0.4f, Sfx.BossSplat);
        }

        /// Host, every tick, for each sapling (TickZones): the slow, the siphon and the bulb.
        void TickSapling(Zone z, float dt)
        {
            if (!BgRooted(z))
                return;
            if (!z.Landed)
            {
                z.Landed = true;
                BroadcastFx(FxKind.Ring, z.Pos, BgRed, 22f, 0.3f, Sfx.None);
            }
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || !e.Pos.WithinReach(z.Pos, z.Radius + e.Radius * 0.5f))
                    continue;
                if (Heavy(e))  // the usual slow skips Heavy enemies and bosses, so they wade (as in a Bloodbath)
                {
                    e.WadeT = Math.Max(e.WadeT, 0.2f);
                    e.WadeMult = Math.Min(e.WadeMult, 1f - BgSlow);
                }
                else
                    Slow(e, 0.2f, 1f - BgSlow);
            }
            z.TickT += dt;
            while (z.TickT >= BgTick)
            {
                z.TickT -= BgTick;
                SaplingSiphon(z);
                if (z.T <= 0f)
                    break;
            }
            z.GuardFrac = MathX.Clamp01(z.Soaked / BgBulb);
        }

        readonly List<Enemy> gardenScratch = new List<Enemy>();

        void SaplingSiphon(Zone z)
        {
            gardenScratch.Clear();
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.WithinReach(z.Pos, z.Radius + e.Radius * 0.5f))
                    gardenScratch.Add(e);
            float dmg = BgSiphon * BgTick * z.DmgMult;
            foreach (var e in gardenScratch)
            {
                float before = e.Hp;
                HurtEnemy(e, dmg, z.Owner);
                z.Soaked += Math.Max(0f, before - Math.Max(0f, e.Hp));
            }
            gardenScratch.Clear();
            if (z.Soaked >= BgBulb)
                SaplingBurst(z);
        }

        /// The bulb is full: it bursts, and a blood orb anyone can drink drops where it stood.
        void SaplingBurst(Zone z)
        {
            z.GuardFrac = 1f;
            z.T = 0f;
            Player p;
            if (Players.TryGetValue(z.Owner, out p))
                p.BgBursts++;
            var o = SpawnOrb(z.Pos + new V2(0f, -10f), false);
            o.Blood = true;
            o.Boost = BgOrbHeal / BloodOrbHeal;
            o.Life = BgOrbLife;
            BroadcastFx(FxKind.Ring, z.Pos, BgRed, z.Radius * 0.7f, 0.4f, Sfx.PickupBig);
            BroadcastFx(FxKind.Ring, z.Pos, new Rgba(1f, 0.55f, 0.6f), 30f, 0.3f, Sfx.None);
        }
    }
}
