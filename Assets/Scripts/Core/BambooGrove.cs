using System;
using System.Collections.Generic;

namespace Arena
{
    /// Bamboo Grove, the Tank Ninja's 4th choice (`AbilityId.BambooGrove`, `AbilityDef.Fourth`: the lobby's 4th ability key),
    /// from Docs/Mockups/bamboo-grove.html (take B, Ripple). Palm to the ground: a 0.35 s wind-up (a ring closing in and the
    /// ground cracking out from your feet), then six rings of bamboo burst up one after another, racing out to 140 px. Every
    /// enemy the wave reaches takes 40 and is stunned (1.2 s; heavy enemies 0.6 s, bosses 0.3 s); when the stun ends it
    /// bleeds, 8/s for 4 s. The outer ring stays as a fence for 3.2 s (mastered 6 s): enemies can't walk through it
    /// (heavy ones and bosses push past) and enemy shots stop on it.
    ///
    /// The host keeps each grove (`groves`, ticked by `TickBambooGroves` from TickNinjas): the wave's front, who it has hit,
    /// the bleeds waiting for their stun to end, the fence. Every screen draws it from one effect (Game/BambooGroveLook.cs):
    /// `FxKind.BambooGrove` (Pos = the middle, Pos2 = (the caster's id, the fence's life), Radius = the grove's radius), its
    /// stalks laid out from a hash of the middle so every screen grows the same grove.
    public partial class Game
    {
        public const float BgRadius = 140f, BgWindup = 0.35f;
        public const int BgRings = 6;
        public const float BgRingGap = 0.07f;                      // each ring bursts this long after the one inside it
        public const float BgFrontStart = 18f, BgFrontStep = 24f;  // the wave's front: 18 px at the burst, +24 px a ring gap (at 140 px)
        public const float BgFence = 3.2f, BgFenceMastered = 6f;   // the outer ring's life
        public const float BgRetract = 0.35f;                     // stalks sinking back
        public const float BgCooldown = 14f;
        const float BgDamage = 40f, BgBleedDps = 8f, BgBleedTime = 4f;
        const float BgStun = 1.2f, BgStunHeavy = 0.6f, BgStunBoss = 0.3f;
        const float BgFenceBand = 6f;                             // how near the fence's line a shot stops

        /// The rings' radii as a share of the grove's (the outer one is the fence).
        public static readonly float[] BgRingAt = { 0.2f, 0.371f, 0.543f, 0.714f, 0.871f, 1f };

        class Grove
        {
            public int Owner;
            public V2 Pos;
            public float R, T, Fence, Power;
            public readonly HashSet<int> Hit = new HashSet<int>();
        }
        struct GroveBleed { public int Owner, Target; public float T, Dps; }
        readonly List<Grove> groves = new List<Grove>();
        readonly List<GroveBleed> groveBleeds = new List<GroveBleed>();

        /// The grove's radius for this player (Rhizome: +30% a stack).
        public static float BgRadiusOf(Player p) => BgRadius * p.Stats.BgRadiusMult;

        /// When the fence (the outer ring) is up, counted from the cast.
        public static float BgFenceStart => BgWindup + (BgRings - 1) * BgRingGap;

        /// How far the wave's front has run `since` the cast, in a grove of radius `r`.
        public static float BgFrontAt(float since, float r) =>
            since < BgWindup ? -1f : (BgFrontStart + (since - BgWindup) / BgRingGap * BgFrontStep) * r / BgRadius;

        /// Host: Bamboo Grove cast.
        void CastBambooGrove(Player p, float power, bool mastered)
        {
            var g = new Grove { Owner = p.Id, Pos = p.Pos, R = BgRadiusOf(p), Power = power, Fence = mastered ? BgFenceMastered : BgFence };
            groves.Add(g);
            BroadcastFx(FxKind.BambooGrove, g.Pos, new V2(p.Id, g.Fence), new Rgba(0.5f, 0.69f, 0.29f), g.R,
                BgFenceStart + g.Fence + BgRetract, Sfx.StoneSlam);
        }

        /// Host, every tick: the wave strikes what it reaches, bleeds start as stuns end, groves end.
        void TickBambooGroves(float dt)
        {
            for (int i = groves.Count - 1; i >= 0; i--)
            {
                var g = groves[i];
                g.T += dt;
                if (g.T > BgFenceStart + g.Fence)
                {
                    groves.RemoveAt(i);
                    continue;
                }
                float front = BgFrontAt(g.T, g.R);
                if (front < 0f || front > g.R + BgFrontStep * 2f)
                    continue;
                Player p;
                if (!Players.TryGetValue(g.Owner, out p))
                    continue;
                foreach (var e in FoesInCircle(g.Pos, g.R, by: p.Id))  // (the ring's own test below: half the body counts)
                {
                    if (g.Hit.Contains(e.Id))
                        continue;
                    float d = e.Pos.DistanceTo(g.Pos);
                    if (d > g.R + e.Radius * 0.5f || d - e.Radius * 0.5f > front)
                        continue;
                    g.Hit.Add(e.Id);
                    HurtEnemy(e, BgDamage * p.Stats.DmgMult * g.Power, p.Id);
                    float stun = e.Def.IsBoss ? BgStunBoss : e.Def.Heavy ? BgStunHeavy : BgStun;
                    Stun(e, stun);  // Daze.cs: held, with the daze stars over its head
                    groveBleeds.Add(new GroveBleed { Owner = p.Id, Target = e.Id, T = -stun, Dps = BgBleedDps * p.Stats.DmgMult * g.Power });
                    castHits++;
                }
            }
            for (int i = groveBleeds.Count - 1; i >= 0; i--)
            {
                var b = groveBleeds[i];
                if ((b.T += dt) < 0f)
                {
                    groveBleeds[i] = b;
                    continue;
                }
                groveBleeds.RemoveAt(i);
                Enemy e;
                if (!TryFoe(b.Target, out e) || !Targetable(e))
                    continue;
                ApplyBleed(e, b.Dps, b.Owner);
                e.BleedT = Math.Max(e.BleedT, BgBleedTime * BgBleedMult(b.Owner));
            }
        }

        float BgBleedMult(int owner)
        {
            Player p;
            return Players.TryGetValue(owner, out p) ? p.Stats.BgBleedMult : 1f;
        }

        bool BgFenceUp(Grove g) => g.T >= BgFenceStart && g.T <= BgFenceStart + g.Fence;

        /// Host, every tick: a fence holds the enemies outside it (heavy ones and bosses push past).
        void KeepOffGroveFences()
        {
            foreach (var g in groves)
            {
                if (!BgFenceUp(g))
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || e.Def.Heavy || e.Def.IsBoss)
                        continue;
                    V2 off = e.Pos - g.Pos;
                    float d = off.Length, outer = g.R + 12f + e.Radius;
                    if (d <= g.R - 4f || d >= outer)
                        continue;
                    V2 dir = d > 0.01f ? off * (1f / d) : new V2(1f, 0f);
                    e.Pos = ClampToArena(g.Pos + dir * outer, e.Radius);
                }
            }
        }

        /// Host: does a fence stop this enemy shot?
        bool GroveStops(Bullet b)
        {
            foreach (var g in groves)
            {
                if (!BgFenceUp(g))
                    continue;
                float d = b.Pos.DistanceTo(g.Pos);
                if (Math.Abs(d - g.R) > BgFenceBand + b.Radius)
                    continue;
                BroadcastFx(FxKind.Spark, b.Pos, new Rgba(0.75f, 0.85f, 0.5f), 6f, 0.2f, Sfx.None);
                return true;
            }
            return false;
        }
    }
}
