using System;
using System.Collections.Generic;

namespace Arena
{
    /// Gale Wall (Elementalist DPS, the 4th choice, `AbilityId.GaleWall`; mockup Docs/Mockups/kinetic-wall.html, look B
    /// "Gravity Lens"). The 1st ability puts a curved lens in front of you (GaleFront px ahead, GaleHalfWidth px each side; it stays where it was
    /// cast). Enemy shots that reach it stop there and hang in it (red aimed shots too); each one is a charge (GaleCap, mastered
    /// GaleCapMastered). Pressing it again, or the lens running out (GaleTime), unleashes it: a cone from you toward your cursor (GaleConeHalf
    /// each side, GaleReach long) that hits each enemy in it once for GaleDamage(charge) and shoves it back.
    /// Replicated on the Player: GaleT (seconds left, 0 = no lens), GalePos/GaleAim (where it stands and faces), GaleWide (mastered: wider)
    /// and GaleDots (each caught shot's place in the lens, two bytes; the count is the charge). FxKind.GaleCatch is a shot being caught,
    /// FxKind.GaleBlast the unleash (Game/GaleWallLook.cs).
    public partial class Game
    {
        public const float GaleTime = 5f, GaleFront = 70f, GaleRadius = 150f, GaleBand = 14f, GaleHalfWidth = 80f, GaleHalfWidthMastered = 110f;
        public const float GaleBase = 40f, GalePerShot = 6f, GaleReach = 420f, GaleConeHalf = 35f * MathX.Pi / 180f, GaleShove = 80f;
        public const float GaleCatchT = 0.3f, GaleBlastT = 0.9f, GaleRadialSpread = 12f;
        public const int GaleCap = 40, GaleCapMastered = 60;
        public static readonly Rgba GaleColor = new Rgba(0.42f, 0.84f, 0.86f);

        /// The centre of the lens's arc: behind where it was cast, so the arc bows out in front of the caster.
        public static V2 GaleCentre(Player p) => p.GalePos - V2.FromAngle(p.GaleAim) * (GaleRadius - GaleFront);

        /// Half the lens's angle round its centre.
        public static float GaleSpan(Player p) => (float)Math.Asin((p.GaleWide ? GaleHalfWidthMastered : GaleHalfWidth) / GaleRadius);

        public static int GaleCapOf(Player p) => p.GaleWide ? GaleCapMastered : GaleCap;

        /// What the unleash hits each enemy for, before the caster's damage bonuses.
        public static float GaleDamage(int charge, float perShot = GalePerShot) => GaleBase + perShot * charge;

        /// A caught shot's place in the lens: `lat` -1..1 across it, `rad` -1..1 in and out of its band.
        public static V2 GaleDotPos(Player p, float lat, float rad)
        {
            float a = p.GaleAim + lat * GaleSpan(p);
            return GaleCentre(p) + V2.FromAngle(a) * (GaleRadius + rad * GaleRadialSpread);
        }

        /// Unpacks GaleDots[i] into (lat, rad).
        public static void GaleDot(Player p, int i, out float lat, out float rad)
        {
            int v = p.GaleDots[i];
            lat = ((v & 255) / 255f) * 2f - 1f;
            rad = (((v >> 8) & 255) / 255f) * 2f - 1f;
        }

        /// True if this slot is a Gale Wall with its lens up: pressing it unleashes the lens instead of casting again.
        public bool GalePress(Player p, int slot) => p.Slots[slot].Id == AbilityId.GaleWall && p.GaleT > 0f;

        void CastGaleWall(Player p, bool cap)
        {
            p.GaleT = GaleTime;
            p.GalePos = p.Pos;
            p.GaleAim = p.Aim;
            p.GaleWide = cap;
            p.GaleDots.Clear();
            p.GalePerShot = GalePerShot * p.Stats.GaleChargeMult;
            BroadcastFx(FxKind.Ring, GaleDotPos(p, 0f, 0f), GaleColor, 60f, 0.35f, Sfx.None);  // (its sounds are played by each screen: GaleWallSoundBank.cs)
        }

        /// Host, from the enemy-bullet loop in Combat.cs: does a Gale Wall catch this shot? Every shot stops in it (aimed ones
        /// too); each adds a charge up to the cap.
        bool GaleStops(Bullet b)
        {
            foreach (var p in Players.Values)
            {
                if (p.GaleT <= 0f || p.Downed)
                    continue;
                V2 rel = b.Pos - GaleCentre(p);
                float d = rel.Length, span = GaleSpan(p);
                if (Math.Abs(d - GaleRadius) > GaleBand + b.Radius)
                    continue;
                float off = MathX.WrapPi(rel.Angle - p.GaleAim);
                if (Math.Abs(off) > span)
                    continue;
                if (p.GaleDots.Count < GaleCapOf(p))
                {
                    float lat = off / span, rad = Math.Max(-1f, Math.Min(1f, (d - GaleRadius) / GaleRadialSpread));
                    int lb = (int)Math.Round((lat + 1f) * 0.5f * 255f), rb = (int)Math.Round((rad + 1f) * 0.5f * 255f);
                    p.GaleDots.Add(lb | (rb << 8));
                }
                float velA = b.Vel.IsZero ? p.GaleAim + MathX.Pi : b.Vel.Angle;
                BroadcastFx(FxKind.GaleCatch, b.Pos, new V2(p.Id, velA), GaleColor, b.Radius, GaleCatchT, Sfx.None);
                return true;
            }
            return false;
        }

        /// Host: the lens lets go, a cone from the caster toward their cursor.
        void UnleashGaleWall(Player p)
        {
            if (p.GaleT <= 0f)
                return;
            int charge = p.GaleDots.Count;
            float face = p.Aim, D = GaleDamage(charge, p.GalePerShot) * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (!InCone(p.Pos, face, GaleConeHalf, GaleReach, e.Pos, e.Radius))
                    continue;
                if (!e.Def.IsBoss)
                {
                    V2 away = e.Pos - p.Pos;
                    away = away.IsZero ? V2.FromAngle(face) : away.Normalized();
                    KnockEnemy(e, away * GaleShove * (Heavy(e) ? 0.5f : 1f));
                }
                HurtEnemy(e, D, p.Id);
                WindHit(e, e.Pos - p.Pos);  // Wind.cs
            }
            BroadcastFx(FxKind.GaleBlast, p.Pos, p.Pos + V2.FromAngle(face) * GaleReach, GaleColor, charge, GaleBlastT, Sfx.None);
            ClearGaleWall(p);
        }

        void ClearGaleWall(Player p)
        {
            p.GaleT = 0f;
            p.GaleDots.Clear();
        }

        /// Host, every tick: the lens runs down and unleashes itself at the end; a downed caster loses it.
        void TickGaleWalls(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.GaleT <= 0f)
                    continue;
                if (p.Downed)
                {
                    ClearGaleWall(p);
                    continue;
                }
                p.GaleT -= dt;
                if (p.GaleT <= 0f)
                {
                    p.GaleT = 0.001f;  // (so the unleash still sees the lens)
                    UnleashGaleWall(p);
                }
            }
        }

        // --- Network: the player's Gale Wall fields (SyncPlayer) ---

        void SyncGaleWall(NetSync s, Player p)
        {
            s.Float(ref p.GaleT);  // Gale Wall
            if (p.GaleT > 0f)
            {
                s.Vec(ref p.GalePos);
                s.Float(ref p.GaleAim);
                s.Bool(ref p.GaleWide);
                int dots = Math.Min(255, p.GaleDots.Count);
                s.Byte(ref dots);
                if (s.Reading)
                    p.GaleDots.Clear();
                for (int k = 0; k < dots; k++)
                {
                    int v = s.Reading ? 0 : p.GaleDots[k], lat = v & 255, rad = (v >> 8) & 255;
                    s.Byte(ref lat);
                    s.Byte(ref rad);
                    if (s.Reading)
                        p.GaleDots.Add(lat | (rad << 8));
                }
            }
            else if (s.Reading)
                p.GaleDots.Clear();
        }
    }
}
