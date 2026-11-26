using System;

namespace Arena
{
    /// The Healer Elementalist's Lumen Ward (AbilityId.LumenWard, renamed; Docs/Mockups/sanctify-cone.html, look A "Cathedral").
    /// A cone of light from you toward your cursor: LumenLength long, 2 * LumenHalf wide, for LumenTime seconds. It moves
    /// with you and turns with your aim the whole time.
    ///   Inside: allies heal LumenHeal HP/s and enemy bullets inside it or touching its edge burn away.
    ///   Mastered: enemies outside the light freeze (not bosses).
    /// The zone is a Zone of kind LumenWard: Pos is the apex, Radius the length, Aim its direction (host sets both every
    /// tick from the owner; both replicated). It opens over LumenOpen and closes over the last LumenClose.
    public partial class Game
    {
        public const float LumenLength = 740f, LumenHalf = 0.47f, LumenTime = 6f, LumenOpen = 0.5f, LumenClose = 0.7f;
        public const float LumenBurnFrom = 0.25f;  // bullets burn once the cone is this open
        public const float LumenDazzleHold = 1f;   // enemies in the light are dazzled, and stay so this long after (Dazzled.cs)

        void CastLumenWard(Player p, V2 target, bool mastered)
        {
            V2 d = target - p.Pos;
            var z = AddZone(ZoneKind.LumenWard, p, p.Pos, LumenLength, LumenTime + LumenClose + p.Stats.LumenBonusTime);  // Dawnlight: longer, heals more
            z.HealMult *= p.Stats.LumenHealMult;
            z.Aim = d.LengthSq > 1f ? d.Angle : p.Aim;
            z.Freezes = mastered;
            z.Follow = true;  // the apex moves with you and the cone turns with your aim (TickLumen)
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

        /// Is a circle of radius r at p in the light or touching it (r = 0: the point itself)?
        static bool InLumen(Zone z, V2 p, float len, float half, float r = 0f)
        {
            if (len < 1f)
                return false;
            V2 d = p - z.Pos;
            float dist = d.Length;
            if (dist > len + r)
                return false;
            if (dist < 30f + r)
                return true;
            float a = d.Angle - z.Aim;
            a = Math.Abs((float)Math.Atan2(Math.Sin(a), Math.Cos(a)));
            if (a < half)
                return true;
            return a < half + MathX.Pi / 2f && dist * (float)Math.Sin(a - half) < r;  // just outside an edge, within r of it
        }

        /// Host, every tick: heal whoever stands in the light, burn bullets in it, freeze what is outside it when mastered.
        void TickLumen(Zone z, float dt)
        {
            float len, half;
            bool on;
            var owner = PlayerOrNull(z.Owner);
            if (owner != null && !owner.Downed)
                z.Aim = owner.Aim;  // turns with your cursor (TickZones moves the apex with you)
            LumenShape(z, out len, out half, out on);
            if (!on)
                return;
            foreach (var q in Players.Values)
                if (!q.Downed && InLumen(z, q.Pos, len, half))
                    Heal(q, z.Def.HealRate * z.HealMult * dt, owner);
            if (len > z.Radius * LumenBurnFrom)
            {
                scratchIds.Clear();
                foreach (var b in Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && InLumen(z, b.Pos, len, half, b.Radius))
                        scratchIds.Add(b.Id);
                foreach (int id in scratchIds)
                {
                    Bullet b = Bullets[id];
                    AddFx(FxKind.Spark, b.Pos, Defs.BulletColor(Defs.HolyShot), b.Radius + 5f, 0.25f);
                    KillBullet(id);
                }
            }
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && InLumen(z, e.Pos, len, half, e.Radius))
                    Dazzle(e, LumenDazzleHold);  // in the light: dazzled, and for a moment after it leaves (Dazzled.cs)
            if (z.Freezes)
                foreach (var e in Enemies.Values)
                    if (e.SpawnT <= 0f && !e.Def.IsBoss && !InLumen(z, e.Pos, len, half))
                    {
                        Hold(e, 0.35f);
                    }
        }
    }
}
