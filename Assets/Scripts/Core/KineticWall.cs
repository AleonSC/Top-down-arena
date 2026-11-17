using System;
using System.Collections.Generic;

namespace Arena
{
    /// Kinetic Wall (Elementalist DPS, the 4th Space ability, `AbilityId.KineticWall`; mockup Docs/Mockups/kinetic-wall.html, look B
    /// "Gravity Lens"). Space puts a curved lens in front of you (KwFront px ahead, KwHalfWidth px each side; it stays where it was
    /// cast). Enemy shots that reach it stop there and hang in it (red aimed shots too); each one is a charge (KwCap, mastered
    /// KwCapMastered). Space again, or the lens running out (KwTime), unleashes it: a cone from you toward your cursor (KwConeHalf
    /// each side, KwReach long) that hits each enemy in it once for KwDamage(charge) and shoves it back.
    /// Replicated on the Player: KwT (seconds left, 0 = no lens), KwPos/KwAim (where it stands and faces), KwWide (mastered: wider)
    /// and KwDots (each caught shot's place in the lens, two bytes; the count is the charge). FxKind.KwCatch is a shot being caught,
    /// FxKind.KwBlast the unleash (Game/KineticWallLook.cs).
    public partial class Game
    {
        public const float KwTime = 5f, KwFront = 70f, KwRadius = 150f, KwBand = 14f, KwHalfWidth = 80f, KwHalfWidthMastered = 110f;
        public const float KwBase = 40f, KwPerShot = 6f, KwReach = 420f, KwConeHalf = 35f * MathX.Pi / 180f, KwShove = 80f;
        public const float KwCatchT = 0.3f, KwBlastT = 0.9f, KwRadialSpread = 12f;
        public const int KwCap = 40, KwCapMastered = 60;
        public static readonly Rgba KwColor = new Rgba(0.72f, 0.64f, 1f);

        /// The centre of the lens's arc: behind where it was cast, so the arc bows out in front of the caster.
        public static V2 KwCentre(Player p) => p.KwPos - V2.FromAngle(p.KwAim) * (KwRadius - KwFront);

        /// Half the lens's angle round its centre.
        public static float KwSpan(Player p) => (float)Math.Asin((p.KwWide ? KwHalfWidthMastered : KwHalfWidth) / KwRadius);

        public static int KwCapOf(Player p) => p.KwWide ? KwCapMastered : KwCap;

        /// What the unleash hits each enemy for, before the caster's damage bonuses.
        public static float KwDamage(int charge, float perShot = KwPerShot) => KwBase + perShot * charge;

        /// A caught shot's place in the lens: `lat` -1..1 across it, `rad` -1..1 in and out of its band.
        public static V2 KwDotPos(Player p, float lat, float rad)
        {
            float a = p.KwAim + lat * KwSpan(p);
            return KwCentre(p) + V2.FromAngle(a) * (KwRadius + rad * KwRadialSpread);
        }

        /// Unpacks KwDots[i] into (lat, rad).
        public static void KwDot(Player p, int i, out float lat, out float rad)
        {
            int v = p.KwDots[i];
            lat = ((v & 255) / 255f) * 2f - 1f;
            rad = (((v >> 8) & 255) / 255f) * 2f - 1f;
        }

        /// True if this slot is a Kinetic Wall with its lens up: pressing it unleashes the lens instead of casting again.
        public bool KineticPress(Player p, int slot) => p.Slots[slot].Id == AbilityId.KineticWall && p.KwT > 0f;

        void CastKineticWall(Player p, bool cap)
        {
            p.KwT = KwTime;
            p.KwPos = p.Pos;
            p.KwAim = p.Aim;
            p.KwWide = cap;
            p.KwDots.Clear();
            p.KwPerShot = KwPerShot * p.Stats.KwChargeMult;
            BroadcastFx(FxKind.Ring, KwDotPos(p, 0f, 0f), KwColor, 60f, 0.35f, Sfx.Freeze);
        }

        /// Host, from the enemy-bullet loop in Combat.cs: does a Kinetic Wall catch this shot? Every shot stops in it (aimed ones
        /// too); each adds a charge up to the cap.
        bool KineticStops(Bullet b)
        {
            foreach (var p in Players.Values)
            {
                if (p.KwT <= 0f || p.Downed)
                    continue;
                V2 rel = b.Pos - KwCentre(p);
                float d = rel.Length, span = KwSpan(p);
                if (Math.Abs(d - KwRadius) > KwBand + b.Radius)
                    continue;
                float off = MathX.Fmod(rel.Angle - p.KwAim + MathX.Pi, MathX.Tau) - MathX.Pi;
                if (Math.Abs(off) > span)
                    continue;
                if (p.KwDots.Count < KwCapOf(p))
                {
                    float lat = off / span, rad = Math.Max(-1f, Math.Min(1f, (d - KwRadius) / KwRadialSpread));
                    int lb = (int)Math.Round((lat + 1f) * 0.5f * 255f), rb = (int)Math.Round((rad + 1f) * 0.5f * 255f);
                    p.KwDots.Add(lb | (rb << 8));
                }
                float velA = b.Vel.IsZero ? p.KwAim + MathX.Pi : b.Vel.Angle;
                BroadcastFx(FxKind.KwCatch, b.Pos, new V2(p.Id, velA), KwColor, b.Radius, KwCatchT, Sfx.None);
                return true;
            }
            return false;
        }

        /// Host: the lens lets go, a cone from the caster toward their cursor.
        void UnleashKineticWall(Player p)
        {
            if (p.KwT <= 0f)
                return;
            int charge = p.KwDots.Count;
            float face = p.Aim, D = KwDamage(charge, p.KwPerShot) * p.Stats.DmgMult;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || !InCone(p.Pos, face, KwConeHalf, KwReach, e.Pos, e.Radius))
                    continue;
                if (!e.Def.IsBoss)
                {
                    V2 away = e.Pos - p.Pos;
                    away = away.IsZero ? V2.FromAngle(face) : away.Normalized();
                    KnockEnemy(e, away * KwShove * (Heavy(e) ? 0.5f : 1f));
                }
                HurtEnemy(e, D, p.Id);
            }
            BroadcastFx(FxKind.KwBlast, p.Pos, p.Pos + V2.FromAngle(face) * KwReach, KwColor, charge, KwBlastT, Sfx.BigExplode);
            ClearKineticWall(p);
        }

        void ClearKineticWall(Player p)
        {
            p.KwT = 0f;
            p.KwDots.Clear();
        }

        /// Host, every tick: the lens runs down and unleashes itself at the end; a downed caster loses it.
        void TickKineticWalls(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.KwT <= 0f)
                    continue;
                if (p.Downed)
                {
                    ClearKineticWall(p);
                    continue;
                }
                p.KwT -= dt;
                if (p.KwT <= 0f)
                {
                    p.KwT = 0.001f;  // (so the unleash still sees the lens)
                    UnleashKineticWall(p);
                }
            }
        }
    }
}
