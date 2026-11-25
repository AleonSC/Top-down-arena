using System;
using System.Collections.Generic;

namespace Arena
{
    /// Flash Fire, the DPS Summoner's 4th choice (`AbilityId.FlashFire`, `AbilityDef.Fourth`: the lobby's 4th ability key),
    /// take C "Pyre" of Docs/Mockups/flash-fire.html. A 0.3 s kindle (rune arcs close round the edge, embers stream into
    /// you), then a ring of fire rolls out from where you stood to 160 px in 0.25 s. Every enemy it passes takes 40 and is
    /// set burning (6 a second for 4 s) and slowed 40% for 3 s (heavy enemies and bosses 20%). The ring then stands at the
    /// edge as a pyre for 3 s (mastered 5 s): enemies touching it are set burning and slowed again.
    ///
    /// The host keeps each fire (`flashFires`, ticked by `TickFlashFires` from TickZones). Every screen draws it from one
    /// effect (Game/FlashFireLook.cs): `FxKind.FlashFire` (Pos = the middle, Pos2 = (the caster's id, the pyre's life),
    /// Radius = the fire's radius).
    public partial class Game
    {
        public const float FfRadius = 160f, FfWindup = 0.3f, FfRoll = 0.25f;
        public const float FfPyre = 3f, FfPyreMastered = 5f, FfFade = 0.5f;
        public const float FfCooldown = 14f;
        public const float FfDamage = 40f, FfBurnDps = 6f, FfBurnTime = 4f;
        public const float FfSlowTime = 3f, FfSlow = 0.6f, FfSlowHeavy = 0.8f;  // 40% slower; heavy enemies and bosses 20%
        public const float FfBand = 14f;                                         // how near the pyre's line an enemy must be to catch

        class FlashFireBurst
        {
            public int Owner;
            public V2 Pos;
            public float R, T, Pyre, Power;
            public readonly HashSet<int> Hit = new HashSet<int>();
        }
        readonly List<FlashFireBurst> flashFires = new List<FlashFireBurst>();

        /// The fire's radius for this player (Backdraft: +25% a stack).
        public static float FfRadiusOf(Player p) => FfRadius * p.Stats.FfRadiusMult;

        /// How far the ring has rolled `since` the cast, in a fire of radius `r` (-1 during the kindle).
        public static float FfFrontAt(float since, float r) =>
            since < FfWindup ? -1f : r * EaseOut(Math.Min(1f, (since - FfWindup) / FfRoll));

        static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        /// When the pyre stands, counted from the cast.
        public static float FfPyreStart => FfWindup + FfRoll;

        /// Host: Flash Fire cast at the caster's feet.
        void CastFlashFire(Player p, float power, bool mastered)
        {
            var f = new FlashFireBurst { Owner = p.Id, Pos = p.Pos, R = FfRadiusOf(p), Power = power, Pyre = mastered ? FfPyreMastered : FfPyre };
            flashFires.Add(f);
            BroadcastFx(FxKind.FlashFire, f.Pos, new V2(p.Id, f.Pyre), new Rgba(1f, 0.43f, 0.1f), f.R,
                FfPyreStart + f.Pyre + FfFade, Sfx.SpellFire);
        }

        /// Host, every tick: the ring strikes what it passes, the pyre catches what touches it, fires end.
        void TickFlashFires(float dt)
        {
            for (int i = flashFires.Count - 1; i >= 0; i--)
            {
                var f = flashFires[i];
                f.T += dt;
                if (f.T > FfPyreStart + f.Pyre)
                {
                    flashFires.RemoveAt(i);
                    continue;
                }
                Player p;
                if (!Players.TryGetValue(f.Owner, out p))
                    continue;
                float front = FfFrontAt(f.T, f.R), burn = FfBurnDps * p.Stats.DmgMult * f.Power * p.Stats.FfBurnMult;
                if (front < 0f)
                    continue;
                bool rolling = f.T <= FfPyreStart + 0.05f;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e))
                        continue;
                    float d = e.Pos.DistanceTo(f.Pos);
                    if (rolling && !f.Hit.Contains(e.Id) && d - e.Radius <= front && d - e.Radius <= f.R)
                    {
                        f.Hit.Add(e.Id);
                        HurtEnemy(e, FfDamage * p.Stats.DmgMult * f.Power, p.Id);
                        FfIgnite(e, burn, FfSlowTime, p.Id);
                        castHits++;
                    }
                    else if (f.T >= FfPyreStart && Math.Abs(d - f.R) <= FfBand + e.Radius)  // touching the pyre
                        FfIgnite(e, burn, 1f, p.Id);
                }
            }
        }

        /// Set burning (FfBurnTime) and slowed for `slowFor` s (heavy enemies and bosses less).
        void FfIgnite(Enemy e, float burnDps, float slowFor, int by)
        {
            e.BurnT = Math.Max(e.BurnT, FfBurnTime);
            e.BurnDps = Math.Max(e.BurnDps, burnDps);
            e.BurnBy = by;
            Slow(e, slowFor, e.Def.Heavy || e.Def.IsBoss ? FfSlowHeavy : FfSlow);
        }
    }
}
