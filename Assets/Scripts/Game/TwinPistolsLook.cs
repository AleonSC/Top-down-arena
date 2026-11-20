using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Healer Soldier's Twin Pistols (Core/TwinPistols.cs). Drawing only:
    ///   - the guns: a pistol in each hand held out in front, the one that fired kicking back (TpGunKick), turned in to
    ///     meet at your cursor while Laser Sights is on, a red emitter under each barrel (TpHandPose, DrawHeldTwinPistols);
    ///   - the shots: a short gold tracer, red and fatter down the beams (DrawTpShot), a flash and a casing per round (OnTwinPistolShot);
    ///   - Laser Sights: two thin red beams from the muzzles to where they meet, a dot there, flickering a little (DrawTwinLasers);
    ///   - the key: over your head, a red bar draining while the lasers are on, then an arc filling while it cools down (DrawTwinLaserReady).
    public partial class ArenaApp
    {
        class TpSeen { public float L = -99f, R = -99f; }
        readonly Dictionary<int, TpSeen> tpSeen = new Dictionary<int, TpSeen>();

        static readonly Rgba TpGold = new Rgba(1f, 0.86f, 0.5f);
        const float TpGunLen = 17f;  // grip to muzzle in the model's units

        TpSeen TpOf(int id)
        {
            TpSeen s;
            if (!tpSeen.TryGetValue(id, out s))
                tpSeen[id] = s = new TpSeen();
            return s;
        }

        /// A Twin Pistols round has just appeared at a muzzle (Juice's new-shots pass): which gun it left, its flash and casing.
        void OnTwinPistolShot(Player owner, Bullet b)
        {
            V2.Axes(owner.Aim, out V2 fwd, out V2 left);
            bool right = (b.Pos - owner.Pos).Dot(left) < 0f;
            var s = TpOf(owner.Id);
            if (right) s.R = juiceClock; else s.L = juiceClock;
            bool laser = owner.TpLaserT > 0f;
            V2 dir = b.Vel.Normalized();
            AddPart(new Part { Kind = PMuzzle, Pos = b.Pos, Vel = V2.Zero, Rot = b.Vel.Angle, Life = 0.07f, Max = 0.07f, Size = laser ? 18f : 14f, Col = laser ? new Rgba(1f, 0.55f, 0.5f) : new Rgba(1f, 0.9f, 0.6f) });
            V2 outward = right ? -left : left;  // casings fly out of each gun's outer side
            AddPart(new Part { Kind = PCasing, Pos = b.Pos - dir * 14f, Vel = outward * Rnd(55f, 90f) - dir * Rnd(0f, 25f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-22f, 22f), Life = 0.5f, Max = 0.5f, Size = 2.4f, Col = BrassCol });
            if (laser)
                Sparks(b.Pos, Game.TpLaserColor, 2, 160f, 0.12f, 1.6f);
        }

        /// How far gun `gun` (1 right, 0 left) is kicked back, 0..1.
        float TpGunKick(Player p, int gun)
        {
            TpSeen s;
            if (!tpSeen.TryGetValue(p.Id, out s))
                return 0f;
            float age = juiceClock - (gun == 1 ? s.R : s.L);
            return age > 0.4f ? 0f : Mathf.Exp(-GunKickRate * age);
        }

        /// Where the beams meet for `p`, along its aim: your own cursor; the host knows everyone's; otherwise a fair guess.
        float TpMeetOf(Player p)
        {
            if (p.Id == game.MyId || session != null && session.IsHost)
                return game.TpMeet(p);
            return 260f;
        }

        /// The model's hands: a pistol in each, held out so their muzzles are where the rounds start, turned in to meet at
        /// the beams' crossing while Laser Sights is on; each kicks back on its own shot.
        void TpHandPose(Player p, float wk, ref float rhx, ref float rhy, ref float lhx, ref float lhy, out float twist)
        {
            float mx = p.Def.BodyR + Game.TpMuzzleOut, half = Game.TpGunGap;
            twist = p.TpLaserT > 0f ? Mathf.Atan2(half, Mathf.Max(20f, TpMeetOf(p) - mx)) : 0f;
            float len = TpGunLen * wk, c = Mathf.Cos(twist), sn = Mathf.Sin(twist);
            float tx = mx / mU, ty = half / mU;
            float kR = 4f * TpGunKick(p, 1), kL = 4f * TpGunKick(p, 0);
            rhx = tx - (len + kR) * c; rhy = ty + (len + kR) * sn;
            lhx = tx - (len + kL) * c; lhy = -ty - (len + kL) * sn;
        }

        /// One pistol in the hand's frame (mHeldTube: 1 the right hand, 0 the left; the left is drawn mirrored).
        void DrawHeldTwinPistols(Player p, float t, int hold)
        {
            float k = Juicy ? TpGunKick(p, mHeldTube) : 0f;
            if (k > 0f)
                MFrame(mOx, mOy, mAng + 0.18f * k * (mMir ? -1f : 1f), mWs, mMir);  // the muzzle flips outward a little
            MRect(-4f, -2f, 6f, 7f, MDark);                          // grip
            MRect(-3f, -2.8f, 18f, 5.2f, Hx(0x4a5664));              // slide
            MLine(1f, -2.8f, 15f, -2.8f, 1f, MSteel);                // its top edge
            MRect(15f, -1.4f, 2.2f, 2.8f, Hx(0x1b2026));             // the muzzle
            bool laser = p.TpLaserT > 0f;
            MRect(6f, 2.4f, 6f, 2.4f, laser ? Game.TpLaserColor : Hx(0x2a3038));  // the laser under the barrel
            if (laser)
                MCirc(12.5f, 3.6f, 1.3f, Rgba.White, 6);
        }

        /// Laser Sights: two thin red beams from the muzzles to where they meet, and a dot there.
        void DrawTwinLasers(Player p, float t)
        {
            if (p.TpLaserT <= 0f || p.Downed)
                return;
            float aim = p.Aim;
            V2 meet = p.Disp + V2.FromAngle(aim) * TpMeetOf(p);
            float fade = Mathf.Clamp01(p.TpLaserT / 0.3f);  // eases out as it runs dry
            float flick = 0.85f + 0.15f * Mathf.Sin(t * 47f + p.Id);
            var col = Game.TpLaserColor;
            for (int gun = 0; gun < 2; gun++)
            {
                V2 from = Game.TpMuzzle(p, aim, gun) + (p.Disp - p.Pos);
                if (fxOn)
                    Line(from, meet, 4f, col.WithAlpha(0.18f * fade * flick));
                Line(from, meet, 1.2f, col.WithAlpha(0.85f * fade * flick));
            }
            Circle(meet, 3f, col.WithAlpha(fade), 10);
            Arc(meet, 7f, 1.2f, col.WithAlpha(0.5f * fade), 16);
        }

        /// Over your head: while the lasers are on, a red bar draining; while the key cools down, a faint arc filling.
        void DrawTwinLaserReady(Player p)
        {
            float r = p.Def.BodyR + 13f, span = 1.1f, a0 = -MathX.Pi / 2f - span / 2f;
            if (p.TpLaserT > 0f)
            {
                float f = Mathf.Clamp01(p.TpLaserT / game.TpLaserTime(p));
                Arc(p.Disp, r, 3f, new Rgba(1f, 1f, 1f, 0.15f), 16, a0, span);
                Arc(p.Disp, r, 3f, Game.TpLaserColor.WithAlpha(0.9f), 16, a0, span * f);
            }
            else if (p.TpCd > 0f)
            {
                float f = 1f - Mathf.Clamp01(p.TpCd / Mathf.Max(1f, Game.TpLaserCd - game.TpLaserTime(p)));
                Arc(p.Disp, r, 2.5f, new Rgba(1f, 1f, 1f, 0.15f), 16, a0, span);
                Arc(p.Disp, r, 2.5f, Game.TpLaserColor.WithAlpha(0.45f), 16, a0, span * f);
            }
        }

        /// A Twin Pistols round: a short gold tracer with a white head; down the beams, red and fatter.
        void DrawTpShot(Bullet b, Rgba c)
        {
            Player owner;
            bool laser = game.Players.TryGetValue(b.Owner, out owner) && b.Radius / Mathf.Max(0.1f, owner.Stats.ShotSizeMult) >= 4.5f;
            TaperTrail(b, 6, laser ? 3f : 2.2f, laser ? Game.TpLaserColor : TpGold, 0.85f);
            Circle(b.Pos, laser ? 2.8f : 2.2f, Rgba.White, 8);
        }
    }
}
