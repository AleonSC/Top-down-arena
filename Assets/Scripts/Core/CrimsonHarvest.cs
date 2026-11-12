using System;
using System.Collections.Generic;

namespace Arena
{
    /// Crimson Harvest (DPS Hemomancer ultimate, replaces Crimson Apotheosis; `AbilityId.CrimsonApotheosis` kept). Three steps:
    ///   Rip   (HarvestRip s): blood is torn out of every enemy in the arena. Each takes HarvestRipDmg over four ticks, and its blood
    ///         (HarvestWeight: Grunt 1, Heavy 3, boss 8) is added to a ball floating over you, one quarter per tick.
    ///   Hold  (HarvestHoldTime s, mastered HarvestHoldMastered): the ball floats; you move at HarvestSlow of your speed. Pressing the
    ///         ability again throws it at your cursor (up to HarvestRange px); if the time runs out it throws itself.
    ///   Throw (HarvestThrowT s of flight): it lands for HarvestDamage(blood) in HarvestRadius(blood) px, half damage at the edge, enemies
    ///         flung and slowed.
    /// Mastered: a longer hold and HarvestMasteredBlood times the blood. The ball is replicated as three Player fields (HarvestClock, HarvestBlood,
    /// HarvestHold); the tendrils, droplets and aim ring are drawn from those and the enemies' own positions (Game/CrimsonHarvestLook.cs). The throw
    /// is one FxKind.HarvestThrow (Pos = where you stood, Pos2 = where it lands, Radius = the blood) drawn from its age alone. Mockup:
    /// Docs/Mockups/hemomancer-ult-rework.html (tendrils of look B, ball of look A).
    public partial class Game
    {
        public const float HarvestRip = 2.2f, HarvestHoldTime = 5f, HarvestHoldMastered = 8f, HarvestThrowT = 0.65f, HarvestTail = 4.6f;
        public const float HarvestRipDmg = 20f, HarvestSlow = 0.6f, HarvestRange = 900f, HarvestLift = 110f, HarvestMasteredBlood = 1.25f;
        public const float HarvestKnock = 180f, HarvestSlowMult = 0.65f, HarvestSlowTime = 4.5f;
        public const int HarvestTicks = 4, HarvestDrops = 10;

        /// Blood an enemy gives: a boss 8, a Heavy 3, anything else 1.
        public static float HarvestWeight(Enemy e) => e.Def.IsBoss ? 8f : e.Def.Heavy ? 3f : 1f;

        /// How far the ball swells: its radius in px for a given haul of blood (area follows the blood).
        public static float HarvestBallR(float blood) => 25f + 15f * (float)Math.Sqrt(Math.Max(0f, blood));

        /// The blast: radius in px and damage at its core (the inner half; it falls to half at the edge).
        public static float HarvestRadius(float blood) => 150f + 50f * (float)Math.Sqrt(Math.Max(0f, blood));
        public static float HarvestDamage(float blood) => 60f + 12f * blood;

        /// When the tendril to an enemy `dist` px from the ball starts reaching (seconds after the cast): farther ones go later, and a
        /// little by id so a crowd doesn't move as one. Its four damage ticks follow at 0.1 s + k * 0.2 s and its droplets at 0.3 s + i * 0.08 s.
        public static float HarvestStart(float dist, int id) => 0.3f + Math.Min(1f, dist / 2400f) * 0.45f + (id * 37 % 100) * 0.001f;
        public static float HarvestTickAt(float start, int k) => start + 0.1f + k * 0.2f;

        class HarvestTarget { public int Owner, Enemy, Ticks; public float Start, Weight; }
        class HarvestShot { public int Owner; public V2 To; public float T, Blood, Mult; }

        readonly List<HarvestTarget> harvestTargets = new List<HarvestTarget>();
        readonly List<HarvestShot> harvests = new List<HarvestShot>();

        /// The ball is up (rip or hold) for this player.
        public static bool HarvestActive(Player p) => p.HarvestHold > 0f;

        /// The ball is ready to throw (the rip is over).
        public static bool HarvestHolding(Player p) => p.HarvestHold > 0f && p.HarvestClock >= HarvestRip;

        /// Seconds of hold left (0 while still ripping).
        public static float HarvestLeft(Player p) => Math.Max(0f, HarvestRip + p.HarvestHold - p.HarvestClock);

        /// Where the ball would land if thrown now: toward `aim`, up to HarvestRange px from the player.
        public V2 HarvestAimPoint(Player p, V2 aim) => ClampToArena(p.Pos + (aim - p.Pos).LimitLength(HarvestRange), 20f);

        /// True if this slot is a Crimson Harvest with a ball up: pressing it throws (or does nothing yet) instead of casting again.
        public bool HarvestPress(Player p, int slot) => p.Slots[slot].Id == AbilityId.CrimsonApotheosis && p.HarvestHold > 0f;

        void CastHarvest(Player p, bool cap)
        {
            p.HarvestClock = 0f;
            p.HarvestBlood = 0f;
            p.HarvestHold = cap ? HarvestHoldMastered : HarvestHoldTime;
            p.HarvestMastered = cap;
            harvestTargets.RemoveAll(t => t.Owner == p.Id);
            V2 ball = p.Pos + new V2(0f, -HarvestLift);  // (the arena's y runs down)
            foreach (var e in Enemies.Values)
                if (Targetable(e))
                    harvestTargets.Add(new HarvestTarget { Owner = p.Id, Enemy = e.Id, Start = HarvestStart(e.Pos.DistanceTo(ball), e.Id), Weight = HarvestWeight(e) });
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), 120f, 0.5f, Sfx.Roar);
        }

        void ClearHarvest(Player p)
        {
            p.HarvestHold = p.HarvestClock = p.HarvestBlood = 0f;
            p.HarvestMastered = false;
            harvestTargets.RemoveAll(t => t.Owner == p.Id);
        }

        /// Host: throw the ball at the player's cursor.
        void ThrowHarvest(Player p)
        {
            if (!HarvestActive(p))
                return;
            V2 to = HarvestAimPoint(p, p.AimAt);
            harvests.Add(new HarvestShot { Owner = p.Id, To = to, Blood = p.HarvestBlood, Mult = p.Stats.DmgMult });
            BroadcastFx(FxKind.HarvestThrow, p.Pos, to, new Rgba(0.85f, 0.12f, 0.2f), p.HarvestBlood, HarvestThrowT + HarvestTail, Sfx.Throw);
            ClearHarvest(p);
        }

        /// Host, every tick: tear blood from enemies, run the hold down, and land thrown balls.
        void TickHarvest(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.HarvestHold <= 0f)
                    continue;
                if (p.Downed)
                {
                    ClearHarvest(p);
                    continue;
                }
                p.HarvestClock += dt;
                float give = p.HarvestMastered ? HarvestMasteredBlood : 1f;
                for (int i = harvestTargets.Count - 1; i >= 0; i--)
                {
                    var t = harvestTargets[i];
                    if (t.Owner != p.Id)
                        continue;
                    while (t.Ticks < HarvestTicks && p.HarvestClock >= HarvestTickAt(t.Start, t.Ticks))
                    {
                        t.Ticks++;
                        Enemy e;
                        if (Enemies.TryGetValue(t.Enemy, out e) && e.Hp > 0f)
                            HurtEnemy(e, HarvestRipDmg / HarvestTicks * p.Stats.DmgMult, p.Id);
                        p.HarvestBlood += t.Weight * give / HarvestTicks;  // an enemy that died mid-rip still gives its blood
                    }
                    if (t.Ticks >= HarvestTicks)
                        harvestTargets.RemoveAt(i);
                }
                if (p.HarvestClock >= HarvestRip + p.HarvestHold)
                    ThrowHarvest(p);
            }
            for (int i = harvests.Count - 1; i >= 0; i--)
            {
                var h = harvests[i];
                h.T += dt;
                if (h.T < HarvestThrowT)
                    continue;
                harvests.RemoveAt(i);
                LandHarvest(h);
            }
        }

        void LandHarvest(HarvestShot h)
        {
            float R = HarvestRadius(h.Blood), D = HarvestDamage(h.Blood) * h.Mult;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                float d = e.Pos.DistanceTo(h.To);
                if (d > R + e.Radius)
                    continue;
                float f = d <= R * 0.5f ? 1f : Math.Max(0.5f, 1f - 0.5f * (d - R * 0.5f) / (R * 0.5f));
                bool heavy = Heavy(e);
                Slow(e, HarvestSlowTime, HarvestSlowMult);
                if (!e.Def.IsBoss)
                {
                    V2 away = e.Pos - h.To;
                    away = away.IsZero ? V2.FromAngle(e.Id) : away.Normalized();
                    KnockEnemy(e, away * HarvestKnock * f * (heavy ? 0.5f : 1f));
                }
                HurtEnemy(e, D * f, h.Owner);
            }
            Sounds.Add(Sfx.BigExplode);
        }
    }
}
