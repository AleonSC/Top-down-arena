using System;
using System.Collections.Generic;

namespace Arena
{
    /// Flash Fire, the DPS Elementalist's third ultimate (`AbilityId.FlashFire`, key 2 of the lobby's Ultimate row), take C
    /// "Pyre" of Docs/Mockups/flash-fire.html. A 0.5 s kindle at your feet (rune arcs close round the edge, embers stream into
    /// you), then a ring of fire rolls out to 260 px in 0.3 s: 100 to every enemy it passes (bosses too). The fire then stays
    /// for 8 s (mastered 11 s): a wall of flame at the edge and fire on the ground inside, 25 a second to every enemy in it,
    /// slowing them 40% (heavy enemies and bosses 20%). When it goes out, every enemy still in it is left burning, 12 a second
    /// for 5 s. Damage is by your damage.
    ///
    /// The host keeps each fire (`flashFires`, ticked by `TickFlashFires` from TickZones). Every screen draws it from one
    /// effect (Game/FlashFireLook.cs): `FxKind.FlashFire` (Pos = the middle, Pos2 = (the caster's id, the fire's life),
    /// Radius = the fire's radius).
    public partial class Game
    {
        public const float FfRadius = 260f, FfWindup = 0.5f, FfRoll = 0.3f;
        public const float FfLife = 8f, FfLifeMastered = 11f, FfFade = 0.6f;
        public const float FfCooldown = 80f;
        public const float FfDamage = 100f, FfGroundDps = 25f;                   // the flash; the ground while it burns
        public const float FfBurnDps = 12f, FfBurnTime = 5f;                     // left burning when it goes out
        public const float FfSlow = 0.6f, FfSlowHeavy = 0.8f, FfSlowHold = 0.4f; // 40% slower in it (heavy enemies and bosses 20%)

        /// Flash Fire's entry: key 2 of the DPS Elementalist's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] FireUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.FlashFire, Kit = Kit.Elementalist, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Flash Fire", Cooldown = FfCooldown,
                Desc = "A ring of fire rolls out around you and the ground burns for 8 s, hurting and slowing enemies; when it goes out they're left burning.",
                Capstone = "The fire burns 11 s.",
                Bot = b => b.NearEnemies >= 4 && b.TargetD < FfRadius,
            },
        };

        class FlashFireBurst
        {
            public int Owner;
            public V2 Pos;
            public float R, T, Life, Power;
            public readonly HashSet<int> Hit = new HashSet<int>();
        }
        readonly List<FlashFireBurst> flashFires = new List<FlashFireBurst>();

        /// The fire's radius for this player (Backdraft: +20% a stack).
        public static float FfRadiusOf(Player p) => FfRadius * p.Stats.FfRadiusMult;

        /// How far the ring has rolled `since` the cast, in a fire of radius `r` (-1 during the kindle).
        public static float FfFrontAt(float since, float r) =>
            since < FfWindup ? -1f : r * EaseOut(Math.Min(1f, (since - FfWindup) / FfRoll));

        static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        /// When the ground catches, counted from the cast.
        public static float FfFireStart => FfWindup + FfRoll;

        /// Host: Flash Fire cast at the caster's feet.
        void CastFlashFire(Player p, bool mastered)
        {
            var f = new FlashFireBurst { Owner = p.Id, Pos = p.Pos, R = FfRadiusOf(p), Power = castPower, Life = mastered ? FfLifeMastered : FfLife };
            flashFires.Add(f);
            BroadcastFx(FxKind.FlashFire, f.Pos, new V2(p.Id, f.Life), new Rgba(1f, 0.43f, 0.1f), f.R,
                FfFireStart + f.Life + FfFade, Sfx.SpellFire);
        }

        /// Host, every tick: the ring strikes what it passes, the ground hurts and slows what's in it, and as a fire goes
        /// out it leaves everything in it burning.
        void TickFlashFires(float dt)
        {
            for (int i = flashFires.Count - 1; i >= 0; i--)
            {
                var f = flashFires[i];
                f.T += dt;
                Player p;
                Players.TryGetValue(f.Owner, out p);
                float mult = (p != null ? p.Stats.DmgMult : 1f) * f.Power;
                bool over = f.T > FfFireStart + f.Life;
                float front = FfFrontAt(f.T, f.R);
                foreach (var e in Enemies.Values)
                {
                    if (front < 0f || !Targetable(e))
                        continue;
                    float d = e.Pos.DistanceTo(f.Pos);
                    bool inside = d - e.Radius * 0.5f <= f.R;
                    if (over)  // going out: whatever is still in it burns on
                    {
                        if (!inside)
                            continue;
                        e.BurnT = Math.Max(e.BurnT, FfBurnTime);
                        e.BurnDps = Math.Max(e.BurnDps, FfBurnDps * mult * (p != null ? p.Stats.FfBurnMult : 1f));
                        e.BurnBy = f.Owner;
                        continue;
                    }
                    if (!f.Hit.Contains(e.Id) && f.T <= FfFireStart + 0.05f && d - e.Radius <= front && d - e.Radius <= f.R)  // the ring
                    {
                        f.Hit.Add(e.Id);
                        HurtEnemy(e, FfDamage * mult, f.Owner);
                        castHits++;
                    }
                    if (f.T >= FfWindup && inside && d - e.Radius <= front)  // the ground, once the ring has passed
                    {
                        if (f.T >= FfFireStart)
                            DotDamage(e, FfGroundDps * mult * dt, f.Owner);
                        Slow(e, FfSlowHold, e.Def.Heavy || e.Def.IsBoss ? FfSlowHeavy : FfSlow);
                    }
                }
                if (over)
                    flashFires.RemoveAt(i);
            }
        }
    }
}
