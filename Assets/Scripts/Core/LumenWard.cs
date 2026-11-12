using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Healer Elementalist's Lumen Ward (AbilityId.Sanctify, renamed; Docs/Mockups/sanctify-cone.html, look A "Cathedral").
    /// A cone of light along your aim, locked at the cast: LumenLength long, 2 * LumenHalf wide, for LumenTime seconds.
    ///   Inside: allies heal LumenHeal HP/s and enemy bullets burn away.
    ///   Outside: enemy bullets slow to a stop (anywhere on the map) and fly on again when the light ends.
    ///   Mastered: enemies outside the light freeze too (not bosses).
    /// The zone is a Zone of kind LumenWard: Pos is the apex, Radius the length, Aim its direction. It opens over LumenOpen
    /// and closes over the last LumenClose; its shape and the bullets' slowing are worked out from the replicated zone,
    /// so every peer agrees and nothing about a bullet is sent.
    public partial class Game
    {
        public const float LumenLength = 740f, LumenHalf = 0.47f, LumenTime = 6f, LumenOpen = 0.5f, LumenClose = 0.7f;
        public const float LumenWaveSpeed = 1100f;  // the stopping wave spreads out from the caster at this speed
        public const float LumenHold = 1.5f, LumenRelease = 2.2f, LumenBurnFrom = 0.25f;  // stasis builds this fast per second; it fades at (3x while in light) this; bullets burn once the cone is this open

        readonly List<Zone> lumens = new List<Zone>();

        void CastLumenWard(Player p, V2 target, bool mastered)
        {
            V2 d = target - p.Pos;
            var z = AddZone(ZoneKind.LumenWard, p, p.Pos, LumenLength, LumenTime + LumenClose);
            z.Aim = d.LengthSq > 1f ? d.Angle : p.Aim;
            z.Freezes = mastered;
            z.Follow = true;  // the apex moves with you (TickZones); the cone keeps the aim it was cast along
            Sounds.Add(Sfx.SpellHoly);
        }

        /// The cone's current length and half angle (it fans open, then folds shut), and whether it is still working.
        public static void LumenShape(Zone z, out float len, out float half, out bool on)
        {
            float age = z.Duration - z.T;
            float open = MathX.Clamp01(age / LumenOpen), close = MathX.Clamp01((LumenClose - z.T) / LumenClose);
            float eo = 1f - (1f - open) * (1f - open) * (1f - open);
            float k = eo * (1f - close * close);
            len = z.Radius * k;
            half = LumenHalf * (0.3f + 0.7f * eo) * (1f - close * 0.4f);
            on = z.T > LumenClose;
        }

        /// Is this point in the light?
        public static bool InLumen(Zone z, V2 p)
        {
            float len, half;
            bool on;
            LumenShape(z, out len, out half, out on);
            return InLumen(z, p, len, half);
        }

        static bool InLumen(Zone z, V2 p, float len, float half)
        {
            if (len < 1f)
                return false;
            V2 d = p - z.Pos;
            float dist = d.Length;
            if (dist > len)
                return false;
            if (dist < 30f)
                return true;
            float a = d.Angle - z.Aim;
            a = (float)Math.Atan2(Math.Sin(a), Math.Cos(a));
            return Math.Abs(a) < half;
        }

        /// The light has not yet stopped what lies this far from its caster: the stopping wave's radius.
        public static float LumenWave(Zone z) => (z.Duration - z.T) * LumenWaveSpeed;

        /// Host, every tick: heal whoever stands in the light, burn bullets in it, freeze what is outside it when mastered.
        void TickLumen(Zone z, float dt)
        {
            float len, half;
            bool on;
            LumenShape(z, out len, out half, out on);
            if (!on)
                return;
            var owner = PlayerOrNull(z.Owner);
            foreach (var q in Players.Values)
                if (!q.Downed && InLumen(z, q.Pos, len, half))
                    Heal(q, z.Def.HealRate * z.HealMult * dt, owner);
            if (len > z.Radius * LumenBurnFrom)
            {
                scratchIds.Clear();
                foreach (var b in Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && InLumen(z, b.Pos, len, half))
                        scratchIds.Add(b.Id);
                foreach (int id in scratchIds)
                {
                    Bullet b = Bullets[id];
                    AddFx(FxKind.Spark, b.Pos, Defs.BulletColor(Defs.HolyShot), b.Radius + 5f, 0.25f);
                    KillBullet(id);
                }
            }
            if (z.Freezes)
                foreach (var e in Enemies.Values)
                    if (e.SpawnT <= 0f && !e.Def.IsBoss && !InLumen(z, e.Pos, len, half))
                    {
                        e.FrozenT = Math.Max(e.FrozenT, 0.35f);
                        e.SlowT = Math.Max(e.SlowT, 0.35f);
                    }
        }

        /// MoveBullets, before it moves anything: the Lumen Wards that are up.
        void PrepareLumen()
        {
            lumens.Clear();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.LumenWard)
                    lumens.Add(z);
        }

        /// How much of its speed an enemy bullet keeps (every peer, from the replicated zones). Inside the light or with no Ward
        /// up its stasis fades; outside the light, once the stopping wave has reached it, stasis builds until it hangs still.
        float LumenSpeed(Bullet b, float dt)
        {
            if (lumens.Count == 0 && b.Stasis <= 0f || !Defs.IsEnemyShot(b.Kind))
                return 1f;
            bool hold = false, anyOn = false;
            foreach (var z in lumens)
            {
                float len, half;
                bool on;
                LumenShape(z, out len, out half, out on);
                if (!on)
                    continue;
                anyOn = true;
                if (!InLumen(z, b.Pos, len, half) && b.Pos.DistanceTo(z.Pos) < LumenWave(z))
                    hold = true;
            }
            if (hold)
                b.Stasis = Math.Min(1f, b.Stasis + LumenHold * dt);
            else
                b.Stasis = Math.Max(0f, b.Stasis - LumenRelease * dt * (anyOn ? 1.4f : 1f));
            float f = 1f - b.Stasis;
            return f * (float)Math.Sqrt(f);  // (1 - stasis) ^ 1.5: slows to a stop smoothly
        }
    }
}
