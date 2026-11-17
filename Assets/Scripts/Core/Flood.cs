using System;

namespace Arena
{
    /// Flood, the Tank Elementalist's fourth Space ability (`AbilityId.Flood`, `AbilityDef.Fourth`: the lobby's 4th ability key),
    /// from Docs/Mockups/flood.html (look B, Breaker). You stamp and the ground floods round you:
    ///   the stamp: enemies within FloodSurgeReach of you take FloodSurgeDamage and are shoved FloodSurgePush out (not heavies or bosses);
    ///   the pool: a `ZoneKind.Flood` zone that stays where it was cast, spreading from nothing to FloodRadius over FloodGrow s
    ///     (FloodReach), lasting FloodTime s, then draining over FloodDrain s (inert meanwhile);
    ///   wading: every enemy in it is slowed FloodSlow (Heavy enemies and bosses FloodSlowHeavy, through `Enemy.WadeMult`, as the usual
    ///     slow skips them) and kept **wet** (Tsunami.cs: slowed 30%, lightning arcs to it) for as long as it's in and FloodWetAfter s after.
    /// Mastered (Deluge): FloodRadiusMastered wide and FloodTimeMastered s. Its Rare card High Tide adds Stats.FloodRadiusMult and FloodBonusTime.
    /// Replicated as an ordinary zone (Pos, Radius, T, Duration), so every screen spreads, churns and drains it the same (Game/FloodLook.cs).
    public partial class Game
    {
        public const float FloodCooldown = 16f;
        public const float FloodRadius = 190f, FloodRadiusMastered = 247f, FloodGrow = 0.5f, FloodTime = 6f, FloodTimeMastered = 9f, FloodDrain = 0.8f;
        public const float FloodSlow = 0.4f, FloodSlowHeavy = 0.2f, FloodWetAfter = 3f;
        public const float FloodSurgeReach = 90f, FloodSurgePush = 60f, FloodSurgeDamage = 10f;

        /// Seconds since this pool was cast.
        public static float FloodAge(Zone z) => z.Duration - z.T;

        /// How long this pool floods before it starts draining (its Duration less the drain).
        public static float FloodLife(Zone z) => Math.Max(0f, z.Duration - FloodDrain);

        /// Is the pool still working (not draining)?
        public static bool FloodOn(Zone z) => z.Kind == ZoneKind.Flood && z.T > 0f && FloodAge(z) < FloodLife(z);

        /// How far the water reaches: out from nothing over FloodGrow s (easing out), all of it while it lasts, then back in as it drains.
        public static float FloodReach(Zone z)
        {
            float age = FloodAge(z), life = FloodLife(z);
            if (age >= life)
            {
                float k = Math.Min(1f, (age - life) / FloodDrain);
                return z.Radius * (1f - k * k);
            }
            float x = Math.Max(0f, Math.Min(1f, age / FloodGrow));
            return z.Radius * (1f - (1f - x) * (1f - x) * (1f - x));
        }

        /// Is something of this radius at pos wading in the pool? (Only while it floods: a draining pool does nothing.)
        public static bool InFlood(Zone z, V2 pos, float radius) => FloodOn(z) && pos.WithinReach(z.Pos, FloodReach(z) + radius);

        /// How much an enemy in the pool is slowed: Heavy enemies and bosses less.
        public static float FloodSlowFor(Enemy e) => e.Def.IsBoss || e.Def.Heavy ? FloodSlowHeavy : FloodSlow;

        void CastFlood(Player p, bool cap, float pw)
        {
            var s = p.Stats;
            float radius = (cap ? FloodRadiusMastered : FloodRadius) * s.FloodRadiusMult;
            float time = (cap ? FloodTimeMastered : FloodTime) * pw + s.FloodBonusTime;
            AddZone(ZoneKind.Flood, p, p.Pos, radius, time + FloodDrain);
            foreach (var e in Enemies.Values)  // the stamp: a splash that throws back what's touching you
            {
                if (!Targetable(e) || e.Pos.FartherThan(p.Pos, FloodSurgeReach + e.Radius))
                    continue;
                HurtEnemy(e, FloodSurgeDamage * s.DmgMult * pw, p.Id);
                castHits++;
                if (Heavy(e))
                    continue;
                V2 away = (e.Pos - p.Pos).Normalized();
                KnockEnemy(e, (away.IsZero ? V2.FromAngle(Rand(0f, MathX.Tau)) : away) * FloodSurgePush);  // slides (Knock.cs)
            }
            BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Flood).Color, FloodSurgeReach, 0.4f, Sfx.None);  // (its sounds play from the pool: FloodLook.cs UpdateFloodSound)
        }

        /// Host, every tick, for each Flood (TickZones): slow and soak everything wading in it.
        void TickFlood(Zone z)
        {
            if (!FloodOn(z))
                return;  // draining: only the look is left
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || !InFlood(z, e.Pos, e.Radius))
                    continue;
                e.WetT = Math.Max(e.WetT, FloodWetAfter);  // wet while in, and FloodWetAfter s after (TickWet wears it off)
                if (Heavy(e))  // (LateGame.cs: Heavy enemies and bosses) the usual slow skips them, so they wade instead
                {
                    e.WadeT = Math.Max(e.WadeT, 0.2f);
                    e.WadeMult = Math.Min(e.WadeMult, 1f - FloodSlowFor(e));
                }
                else
                {
                    Slow(e, 0.2f, 1f - FloodSlowFor(e));
                }
            }
        }
    }
}
