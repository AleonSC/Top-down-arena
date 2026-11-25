using System;

namespace Arena
{
    /// Land Shark, the DPS Summoner's 4th choice (`AbilityId.LandShark`, `AbilityDef.Fourth`: the lobby's 4th choice), from the
    /// Land Shark mockup (look B, Leap). A puddle opens at your cursor (up to LandSharkRange px away), a fin circles in it, then a
    /// shark leaps across it along your aim and bites at the top of the jump:
    ///   the puddle: spreads from nothing to LandSharkRadius over LandSharkGrow s; until the bite, every enemy wading in it is
    ///     slowed LandSharkSlow (not Heavy enemies or bosses), so the bite is hard to walk out of;
    ///   the bite:   at LandSharkBiteAt s, every enemy in the puddle takes LandSharkDamage (bosses and Heavy enemies too) and is
    ///     stunned LandSharkStun s;
    ///   the dive:   the shark dives back in and the puddle drains away by LandSharkLife s, doing nothing more.
    /// Mastered: LandSharkRadiusMastered wide and LandSharkDamageMastered. Its Rare card Great White adds Stats.LandSharkDmgMult.
    /// It is one `ZoneKind.LandShark` zone: Pos the puddle's middle, Radius its full size, Aim the leap's heading; replicated as an
    /// ordinary zone, so every screen draws the same puddle, fin and leap from its age (Game/LandSharkLook.cs).
    public partial class Game
    {
        public const float LandSharkCooldown = 14f, LandSharkRange = 420f;
        public const float LandSharkRadius = 100f, LandSharkRadiusMastered = 130f;
        public const float LandSharkDamage = 120f, LandSharkDamageMastered = 160f, LandSharkStun = 0.5f, LandSharkSlow = 0.3f;
        public const float LandSharkGrow = 0.45f;     // the puddle spreading
        public const float LandSharkFinFrom = 0.3f;   // the fin breaks the surface and circles in
        public const float LandSharkLeapFrom = 0.85f; // the shark leaves the water...
        public const float LandSharkBiteAt = 1.1f;    // ...bites at the top of its jump...
        public const float LandSharkLeapTo = 1.35f;   // ...and dives back in
        public const float LandSharkDrainFrom = 1.6f, LandSharkLife = 2.2f;  // the puddle drains away

        /// Seconds since this puddle opened.
        public static float LandSharkAge(Zone z) => z.Duration - z.T;

        /// Is this puddle the mastered one? Read from its size.
        public static bool LandSharkMastered(Zone z) => z.Radius > (LandSharkRadius + LandSharkRadiusMastered) * 0.5f;

        /// How far the water reaches at this age: out over LandSharkGrow s (easing out), all of it, then back in as it drains.
        public static float LandSharkReach(Zone z)
        {
            float age = LandSharkAge(z);
            if (age < LandSharkGrow)
            {
                float x = Math.Max(0f, age / LandSharkGrow);
                return z.Radius * (1f - (1f - x) * (1f - x) * (1f - x));
            }
            if (age < LandSharkDrainFrom)
                return z.Radius;
            float k = Math.Min(1f, (age - LandSharkDrainFrom) / (LandSharkLife - LandSharkDrainFrom));
            return z.Radius * (1f - k * k);
        }

        /// How far through its jump the shark is: 0 leaving the water, 0.5 the bite, 1 back in; -1 when it isn't out.
        public static float LandSharkLeap(Zone z)
        {
            float age = LandSharkAge(z);
            if (age < LandSharkLeapFrom || age > LandSharkLeapTo)
                return -1f;
            return (age - LandSharkLeapFrom) / (LandSharkLeapTo - LandSharkLeapFrom);
        }

        /// Is something of this radius at pos in the puddle (where the bite lands)?
        public static bool InLandShark(Zone z, V2 pos, float radius) => pos.WithinReach(z.Pos, LandSharkReach(z) + radius * 0.5f);

        void CastLandShark(Player p, V2 target, bool cap)
        {
            V2 off = target - p.Pos;
            float aim = off.LengthSq > 1f ? off.Angle : p.Aim;
            if (off.Length > LandSharkRange)
                target = p.Pos + V2.FromAngle(aim) * LandSharkRange;
            float radius = cap ? LandSharkRadiusMastered : LandSharkRadius;
            var z = AddZone(ZoneKind.LandShark, p, ClampToArena(target, 20f), radius, LandSharkLife);
            z.Aim = aim;
            z.Leg = -1;  // (0 once it has bitten)
            BroadcastFx(FxKind.Ring, z.Pos, Kits.Get(ZoneKind.LandShark).Color, radius * 0.5f, 0.35f, Sfx.Summon);
        }

        /// Host, every tick, for each Land Shark (TickZones): slow what wades in, then bite everything in the puddle once.
        void TickLandShark(Zone z)
        {
            Player owner;
            if (!Battle || !Players.TryGetValue(z.Owner, out owner))
            {
                z.T = Math.Min(z.T, 0.001f);  // the wave is over
                return;
            }
            float age = LandSharkAge(z);
            if (age < LandSharkBiteAt)
            {
                foreach (var e in AllFoes(by: z.Owner))
                    if (!Heavy(e) && InLandShark(z, e.Pos, e.Radius))
                        Slow(e, 0.2f, 1f - LandSharkSlow);
                return;
            }
            if (z.Leg >= 0)
                return;  // already bitten: the dive and the drain are only the look
            z.Leg = 0;
            float dmg = (LandSharkMastered(z) ? LandSharkDamageMastered : LandSharkDamage) * z.DmgMult * owner.Stats.LandSharkDmgMult;
            var color = Kits.Get(ZoneKind.LandShark).Color;
            foreach (var e in AllFoes(by: z.Owner))
            {
                if (!InLandShark(z, e.Pos, e.Radius))
                    continue;
                HurtEnemy(e, dmg, z.Owner);
                Stun(e, LandSharkStun);
                BroadcastFx(FxKind.Spark, e.Pos, e.Pos, new Rgba(1f, 1f, 1f), e.Radius + 4f, 0.3f, Sfx.Hit);
            }
            BroadcastFx(FxKind.Ring, z.Pos, color, z.Radius, 0.4f, Sfx.BossChomp);
        }
    }
}
