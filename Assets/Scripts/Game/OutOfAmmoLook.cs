using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Out of Ammo (Core/OutOfAmmo.cs): the Twin Rifle's second variant, drawn with two sets of arms. The rifle arms are the Twin Rifle's held out
    /// at your sides (its guns, brass and heat), turned and thrust by the bashes, the clap and the swats; your own arms come out in front and hold the
    /// knife. Moves are keyframed in the Gun and Knife mockup's units (a model unit each) and played from a clock this look starts when a Seq rises.
    /// With Visual effects on: sparks, a flash and a ring where a hit lands, a slash mark, camera kicks, the clap's cracks and shockwave, afterimages
    /// on the lunge, and ribbons behind the blade and the rifle tips (DrawAmmoSmears).
    public partial class ArenaApp
    {
        class AmmoSeen
        {
            public byte Seq, KSeq, SwL, SwR;
            public int Move, KMove;
            public float T0 = -99f, KT0 = -99f, SwT0L = -99f, SwT0R = -99f;
            public bool Fired, KFired;
            public float ClapT = -99f, ClapAim;  // when the last clap landed, and the way you faced (the burst and the cone are drawn from here)
            public V2 ClapAt;
        }

        class AmmoPose
        {
            public float Face, Lean;
            public float GX0, GY0, GH0, GX1, GY1, GH1;       // each rifle's grip and heading (0 left, 1 right)
            public float KRX, KRY, KAk, KLX, KLY;            // the knife hand (and its blade's angle) and the guard hand
            public bool Glow, Hot0, Hot1;
        }

        readonly Dictionary<int, AmmoSeen> ammoSeen = new Dictionary<int, AmmoSeen>();
        readonly AmmoPose ammoPose = new AmmoPose();

        // the knife's keyframes per move, seconds into the move: [t, x, y, blade angle] for the knife hand, [t, x, y] for the guard hand, [t, v] for the body
        static readonly float[][][] KnifeR = {
            new[] { new[] { 0f, 26, 12, -.15f }, new[] { .10f, 14, 34, 1.9f }, new[] { .16f, 38, 8, .15f }, new[] { .22f, 28, -30, -1.5f }, new[] { .38f, 26, 12, -.15f } },
            new[] { new[] { 0f, 26, 12, -.15f }, new[] { .10f, 14, -26, -1.9f }, new[] { .16f, 38, -4, -.15f }, new[] { .22f, 28, 30, 1.5f }, new[] { .38f, 26, 12, -.15f } },
            new[] { new[] { 0f, 26, 12, -.15f }, new[] { .14f, 8, 16, .05f }, new[] { .26f, 62, 6, 0f }, new[] { .34f, 62, 6, 0f }, new[] { .55f, 26, 12, -.15f } } };
        static readonly float[][][] KnifeL = {
            new[] { new[] { 0f, 24, -14 }, new[] { .10f, 20, -26 }, new[] { .22f, 18, -8 }, new[] { .38f, 24, -14 } },
            new[] { new[] { 0f, 24, -14 }, new[] { .10f, 20, -4 }, new[] { .22f, 26, -26 }, new[] { .38f, 24, -14 } },
            new[] { new[] { 0f, 24, -14 }, new[] { .14f, 14, -22 }, new[] { .26f, 36, -12 }, new[] { .55f, 24, -14 } } };
        static readonly float[][][] KnifeFace = {
            new[] { new[] { 0f, 0f }, new[] { .10f, .38f }, new[] { .22f, -.4f }, new[] { .38f, 0f } },
            new[] { new[] { 0f, 0f }, new[] { .10f, -.38f }, new[] { .22f, .4f }, new[] { .38f, 0f } },
            new[] { new[] { 0f, 0f }, new[] { .14f, .12f }, new[] { .26f, 0f }, new[] { .55f, 0f } } };
        static readonly float[][][] KnifeLean = {
            new[] { new[] { 0f, 0f }, new[] { .10f, -2f }, new[] { .22f, 4f }, new[] { .38f, 0f } },
            new[] { new[] { 0f, 0f }, new[] { .10f, -2f }, new[] { .22f, 4f }, new[] { .38f, 0f } },
            new[] { new[] { 0f, 0f }, new[] { .14f, -6f }, new[] { .26f, 18f }, new[] { .34f, 18f }, new[] { .55f, 0f } } };

        const float AmmoUpper = 22f, AmmoFore = 22f, AmmoKShoulder = 7f, AmmoSwatTime = 0.22f, AmmoSpinRate = 14f, AmmoGunLen = 28f;

        static float AmmoPh(float t, float a, float b)
        {
            float p = Mathf.Clamp01((t - a) / Mathf.Max(0.0001f, b - a));
            return p * p * (3f - 2f * p);
        }

        static float[] AmmoTrack(float[][] k, float t)
        {
            if (t <= k[0][0])
                return k[0];
            for (int i = 1; i < k.Length; i++)
                if (t <= k[i][0])
                {
                    float p = AmmoPh(t, k[i - 1][0], k[i][0]);
                    var r = new float[k[i].Length];
                    for (int j = 1; j < r.Length; j++)
                        r[j] = Mathf.Lerp(k[i - 1][j], k[i][j], p);
                    return r;
                }
            return k[k.Length - 1];
        }

        AmmoSeen AmmoOf(Player p)
        {
            AmmoSeen s;
            if (!ammoSeen.TryGetValue(p.Id, out s))
                ammoSeen[p.Id] = s = new AmmoSeen { Seq = p.AmmoSeq, KSeq = p.KnifeSeq, SwL = p.SwatSeqL, SwR = p.SwatSeqR };
            float now = Time.unscaledTime;
            if (p.AmmoSeq != s.Seq) { s.Seq = p.AmmoSeq; s.Move = p.AmmoMove; s.T0 = now; s.Fired = false; }
            if (p.KnifeSeq != s.KSeq) { s.KSeq = p.KnifeSeq; s.KMove = p.KnifeMove; s.KT0 = now; s.KFired = false; }
            if (p.SwatSeqL != s.SwL) { s.SwL = p.SwatSeqL; s.SwT0L = now; }
            if (p.SwatSeqR != s.SwR) { s.SwR = p.SwatSeqR; s.SwT0R = now; }
            return s;
        }

        /// The pose `back` seconds ago, into ammoPose (model units: x forward, y right of the body).
        AmmoPose AmmoCompute(Player p, AmmoSeen s, float back)
        {
            var o = ammoPose;
            float u = p.Def.BodyR / 18f, gx = Game.AutoForward / u, gy = Game.AutoShoulder / u, at = Time.unscaledTime - back;
            o.Face = 0f; o.Lean = 0f; o.Glow = o.Hot0 = o.Hot1 = false;
            o.GX0 = gx; o.GY0 = -gy; o.GH0 = -Game.AutoRestOut; o.GX1 = gx; o.GY1 = gy; o.GH1 = Game.AutoRestOut;
            o.KRX = 26f; o.KRY = 12f; o.KAk = -.15f; o.KLX = 24f; o.KLY = -14f;
            if (p.AmmoSpin > 0f && back <= 0f)
            {
                float el = Game.AmmoSpinTime - p.AmmoSpin, ease = Mathf.Clamp01(el / 0.15f);
                o.Face = el * AmmoSpinRate;
                o.GH0 = Mathf.Lerp(-Game.AutoRestOut, -1.05f, ease); o.GH1 = Mathf.Lerp(Game.AutoRestOut, 1.05f, ease);
                o.GX0 = gx + 14f * ease; o.GX1 = gx + 14f * ease;
                o.KRX = Mathf.Lerp(26f, 32f, ease); o.KRY = Mathf.Lerp(12f, 40f, ease); o.KAk = Mathf.Lerp(-.15f, .7f, ease);
                o.KLX = Mathf.Lerp(24f, 30f, ease); o.KLY = Mathf.Lerp(-14f, -38f, ease);
                o.Glow = o.Hot0 = o.Hot1 = true;
                return o;
            }
            // the rifle arms' bash: the arm cocks back and out, thrusts in, and comes home (the clap is both at once)
            float tau = at - s.T0;
            float lean = 0f;
            if (s.Move > 0 && tau >= 0f && tau < AmmoMotion.Total[s.Move - 1])
            {
                int m = s.Move - 1;
                float w = AmmoMotion.Wind[m], st = AmmoMotion.Strike[m], tot = AmmoMotion.Total[m];
                bool clap = m == 3;
                float cock = AmmoPh(tau, 0f, w), thrust = AmmoPh(tau, w, st), home = AmmoPh(tau, clap ? .78f : st + .02f, tot);
                float pull = cock * (1f - thrust), hit = thrust * (1f - home);
                for (int g = 0; g < 2; g++)
                {
                    bool mine = clap || (m == 1 ? g == 1 : g == 0);
                    if (!mine)
                        continue;
                    float side = g == 0 ? -1f : 1f;
                    float ext = -9f * pull + (clap ? 52f : 44f) * hit;
                    float lat = side * (9f * pull * (clap ? 1f : 0f)) - side * (clap ? 38f : 22f) * hit;
                    float head = side * (Game.AutoRestOut + (clap ? .7f : .45f) * pull) * (1f - hit) + -side * (clap ? .16f : .1f) * hit;
                    if (g == 0) { o.GX0 = gx + ext; o.GY0 = -gy + lat; o.GH0 = head; o.Hot0 = hit > .3f; }
                    else { o.GX1 = gx + ext; o.GY1 = gy + lat; o.GH1 = head; o.Hot1 = hit > .3f; }
                }
                lean = (clap ? 14f : 3f) * hit - (clap ? 8f : 0f) * pull;
            }
            // a swat swings that arm out to the bullet and back
            for (int g = 0; g < 2; g++)
            {
                float st = at - (g == 0 ? s.SwT0L : s.SwT0R);
                if (st < 0f || st > AmmoSwatTime)
                    continue;
                float ph = st / AmmoSwatTime, sw = Mathf.Sin(ph * Mathf.PI), world = g == 0 ? p.SwatAngL : p.SwatAngR;
                float a = MathX.Fmod(world - (p.Aim + o.Face) + MathX.Pi, MathX.Tau) - MathX.Pi + sw * .9f * (g == 0 ? -1f : 1f);
                if (g == 0) { o.GH0 = Mathf.Lerp(o.GH0, a, sw); o.GX0 += 14f * sw; o.Hot0 = ph > .3f && ph < .7f; }
                else { o.GH1 = Mathf.Lerp(o.GH1, a, sw); o.GX1 += 14f * sw; o.Hot1 = ph > .3f && ph < .7f; }
            }
            // the knife arms
            float kt = at - s.KT0;
            if (s.KMove > 0 && kt >= 0f && kt < AmmoMotion.KTotal[s.KMove - 1])
            {
                int k = s.KMove - 1;
                var r = AmmoTrack(KnifeR[k], kt); var l = AmmoTrack(KnifeL[k], kt);
                o.KRX = r[1]; o.KRY = r[2]; o.KAk = r[3]; o.KLX = l[1]; o.KLY = l[2];
                o.Face = AmmoTrack(KnifeFace[k], kt)[1];
                lean += AmmoTrack(KnifeLean[k], kt)[1];
                o.Glow = kt > AmmoMotion.KWind[k] - .02f && kt < AmmoMotion.KStrike[k] + .06f;
            }
            o.Lean = lean;
            return o;
        }

        /// Model-frame elbow for a two-bone arm from the shoulder (0, sy) to the hand target; side -1 left, +1 right.
        static void AmmoArm(float sx, float sy, float tx, float ty, float side, out float ex, out float ey, out float hx, out float hy)
        {
            float dx = tx - sx, dy = ty - sy, d = Mathf.Sqrt(dx * dx + dy * dy), dm = AmmoUpper + AmmoFore - .5f;
            if (d > dm) { dx *= dm / d; dy *= dm / d; d = dm; }
            d = Mathf.Max(d, 6f);
            float a = Mathf.Atan2(dy, dx), c = Mathf.Clamp((AmmoUpper * AmmoUpper + d * d - AmmoFore * AmmoFore) / (2f * AmmoUpper * d), -1f, 1f), sa = Mathf.Acos(c) * side;
            ex = sx + Mathf.Cos(a + sa) * AmmoUpper; ey = sy + Mathf.Sin(a + sa) * AmmoUpper;
            hx = sx + dx; hy = sy + dy;
        }

        /// The body's turn and lean for the pose (called by the model before it sets its frame).
        void AmmoBody(Player p)
        {
            var o = AmmoCompute(p, AmmoOf(p), 0f);
            mAim = p.Aim + o.Face;
            mDir = V2.FromAngle(mAim);
            mSide = new V2(-mDir.Y, mDir.X);
            mO += mDir * (o.Lean * mU);
        }

        /// The rifle arms' grips and headings, for the model's two Twin Rifles (the same hands the Twin Rifle Auto uses).
        void AmmoGunPose(Player p, ref float rhx, ref float rhy, ref float lhx, ref float lhy, out float headR, out float headL)
        {
            var o = AmmoCompute(p, AmmoOf(p), 0f);
            lhx = o.GX0 - mTwinKick; lhy = o.GY0; rhx = o.GX1 - mTwinKick; rhy = o.GY1;
            headR = o.GH1 + mTwinClimb;
            headL = o.GH0 - mTwinClimb;
        }

        /// Your own arms out in front, the knife in the right hand and the left hand guarding (drawn after the rifle arms).
        void DrawAmmoKnifeArms(Player p, ModelPal K)
        {
            var s = AmmoOf(p);
            var o = AmmoCompute(p, s, 0f);
            AmmoFx(p, s);
            for (int g = 0; g < 2; g++)
            {
                float side = g == 0 ? -1f : 1f, sy = side * AmmoKShoulder, ex, ey, hx, hy;
                AmmoArm(3f, sy, g == 0 ? o.KLX : o.KRX, g == 0 ? o.KLY : o.KRY, side, out ex, out ey, out hx, out hy);
                MLine(3f, sy, ex, ey, 6.2f, K.Dark); MLine(ex, ey, hx, hy, 6.2f, K.Dark);
                MLine(3f, sy, ex, ey, 4.6f, K.Sleeve); MLine(ex, ey, hx, hy, 4.6f, K.Sleeve);
                MCirc(ex, ey, 2.6f, K.Sleeve, 8);
                if (g == 1)
                {
                    float ca = Mathf.Cos(o.KAk), sa = Mathf.Sin(o.KAk), nx = -sa, ny = ca;
                    MLine(hx - ca * 4f, hy - sa * 4f, hx, hy, 3.6f, Hx(0x5b4a30));
                    MLine(hx + nx * 3.4f, hy + ny * 3.4f, hx - nx * 3.4f, hy - ny * 3.4f, 1.9f, Hx(0x9aa7b3));
                    var blade = o.Glow ? MGold : MSteel;
                    MPoly(blade, hx + nx * 2f, hy + ny * 2f, hx + ca * 16f + nx * 1.8f, hy + sa * 16f + ny * 1.8f, hx + ca * 24f, hy + sa * 24f, hx + ca * 16f - nx * 1f, hy + sa * 16f - ny * 1f, hx - nx * 2f, hy - ny * 2f);
                    MLine(hx + ca * 4f, hy + sa * 4f, hx + ca * 19f, hy + sa * 19f, 0.9f, o.Glow ? MWhite : Hx(0xeef3f6));
                }
                MCirc(hx, hy, 3.6f, K.Glove);
            }
        }

        /// Sparks, a flash and a ring where a move lands (once, with Visual effects on), a slash mark, and a kick of the camera for yours.
        void AmmoFx(Player p, AmmoSeen s)
        {
            if (!Juicy)
                return;
            float now = Time.unscaledTime;
            V2 dir = V2.FromAngle(p.Aim);
            if (s.Move > 0 && !s.Fired && now - s.T0 >= AmmoMotion.Hit[s.Move - 1])
            {
                s.Fired = true;
                int m = s.Move - 1;
                bool clap = m == 3;
                V2 at = p.Disp + dir * (p.Def.BodyR + 46f) + dir.Orthogonal() * (m == 0 || m == 2 ? 6f : m == 1 ? -6f : 0f);
                var col = clap ? new Rgba(1f, 0.76f, 0.24f) : new Rgba(0.25f, 0.88f, 0.91f);
                Sparks(at, col, clap ? 26 : 12, clap ? 320f : 220f, 0.3f, 1.8f);
                if (!clap)
                    Ring(at, 26f, 0.26f, col);
                else
                {
                    s.ClapT = now; s.ClapAim = p.Aim; s.ClapAt = p.Disp + dir * (p.Def.BodyR + 40f);  // (the ring burst and the cone, DrawAmmoClap)
                }
                AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.09f, Max = 0.09f, Size = clap ? 15f : 14f, Col = Rgba.White });
                if (clap && p.Id == game.MyId)
                    Shake(0.3f);
            }
            if (s.KMove > 0 && !s.KFired && now - s.KT0 >= AmmoMotion.KHit[s.KMove - 1])
            {
                s.KFired = true;
                int k = s.KMove - 1;
                V2 at = p.Disp + dir * (p.Def.BodyR + (k == 2 ? 56f : 44f));
                var col = new Rgba(1f, 0.76f, 0.24f);
                Sparks(at, col, k == 2 ? 20 : 12, k == 2 ? 300f : 230f, 0.28f, 1.7f);
                Ring(at, k == 2 ? 44f : 24f, 0.24f, col);
                AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.08f, Max = 0.08f, Size = k == 2 ? 20f : 12f, Col = Rgba.White });
                if (p.Id == game.MyId)
                    Shake(k == 2 ? 0.14f : 0.07f);
            }
        }

        /// A world point from a model-frame point of pose `o` (the body turned and leaned as the model is).
        V2 AmmoWorld(Player p, AmmoPose o, float x, float y)
        {
            float u = p.Def.BodyR / 18f;
            V2 dir = V2.FromAngle(p.Aim + o.Face), side = new V2(-dir.Y, dir.X);
            return p.Disp + dir * (o.Lean * u) + (dir * x + side * y) * u;
        }

        void AmmoRibbon(V2[] tips, V2[] bases, int n, Rgba col, float al)
        {
            for (int i = 0; i < n - 1; i++)
            {
                float f = (1f - i / (float)n);
                SetColor(col.WithAlpha(al * f * f));
                Tri(tips[i], tips[i + 1], bases[i]);
                Tri(tips[i + 1], bases[i + 1], bases[i]);
            }
        }

        /// The clap's aftermath, drawn from where and when it landed (about a second): a white flash, a dome of force and two thick glowing rings
        /// (the ring burst), dust boiling outward, and a forward cone of chevron waves with speed streaks and a spark spray.
        void DrawAmmoClap(AmmoSeen s, float now)
        {
            float tt = now - s.ClapT;
            if (tt < 0f || tt > 1.3f)
                return;
            V2 c = s.ClapAt, dir = V2.FromAngle(s.ClapAim);
            // the floor scorch
            float sc = Mathf.Clamp01(tt / 0.12f), sf = 1f - Mathf.Clamp01((tt - 0.4f) / 0.9f);
            Circle(c + dir * 14f, 58f * sc, new Rgba(0.1f, 0.06f, 0.03f, 0.45f * sf), 26);
            // dust
            for (int k = 0; k < 14; k++)
            {
                float d = Mathf.Clamp01(tt / 0.9f), a = k * 0.449f + (k * 37 % 10) * 0.03f, dist = Mathf.Lerp(8f, 78f + (k * 53 % 10) * 4f, d * (2f - d)), r = Mathf.Lerp(5f, 20f, d) * (1f + (k * 29 % 10) * 0.07f);
                Circle(c + V2.FromAngle(a) * dist, r, new Rgba(0.68f, 0.64f, 0.6f, 0.3f * (1f - d)), 10);
            }
            // the dome
            float dd = Mathf.Clamp01(tt / 0.4f);
            if (dd < 1f)
            {
                float r = Mathf.Lerp(12f, 90f, dd * (2f - dd));
                for (int i = 0; i < 4; i++)
                    Circle(c, r * (1f - i * 0.2f), new Rgba(1f, 0.85f - i * 0.1f, 0.4f, (0.05f + 0.04f * i) * (1f - dd)), 28);
            }
            // two thick rings with a thin white core
            for (int i = 0; i < 2; i++)
            {
                float d = Mathf.Clamp01((tt - i * 0.1f) / 0.55f);
                if (d <= 0f || d >= 1f)
                    continue;
                float r = Mathf.Lerp(8f, 96f + i * 24f, d * (2f - d)), a = 1f - d;
                Arc(c, r, Mathf.Lerp(13f, 2f, d), (i == 0 ? new Rgba(1f, 0.76f, 0.24f) : new Rgba(0.25f, 0.88f, 0.91f)).WithAlpha(0.75f * a), 40);
                Arc(c, r, Mathf.Lerp(3f, 0.8f, d), new Rgba(1f, 1f, 1f, 0.9f * a), 40);
            }
            // the cone: five chevron waves, each a filled wedge with a bright front edge
            V2 o = c - dir * 12f;
            for (int i = 0; i < 5; i++)
            {
                float d = Mathf.Clamp01((tt - i * 0.05f) / 0.5f);
                if (d <= 0f || d >= 1f)
                    continue;
                float r = Mathf.Lerp(18f, 200f, d * (2f - d)), half = 0.6f, a = 1f - d;
                var col = i % 2 == 1 ? new Rgba(0.25f, 0.88f, 0.91f) : new Rgba(1f, 0.76f, 0.24f);
                SetColor(col.WithAlpha(0.22f * a));
                const int seg = 10;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = s.ClapAim - half + 2f * half * k / seg, a1 = s.ClapAim - half + 2f * half * (k + 1) / seg;
                    Tri(o, o + V2.FromAngle(a0) * r, o + V2.FromAngle(a1) * r);
                }
                Arc(o, r, Mathf.Lerp(5f, 1.2f, d), new Rgba(1f, 1f, 1f, 0.85f * a), 18, s.ClapAim - half, 2f * half);
                V2 e0 = o + V2.FromAngle(s.ClapAim - half) * r, e1 = o + V2.FromAngle(s.ClapAim + half) * r, tip = o + dir * (r + 9f);
                Line(e0, tip, Mathf.Lerp(3.5f, 1f, d), col.WithAlpha(0.9f * a));
                Line(tip, e1, Mathf.Lerp(3.5f, 1f, d), col.WithAlpha(0.9f * a));
            }
            // speed streaks and a spray of sparks
            for (int k = 0; k < 12; k++)
            {
                float ang = s.ClapAim + ((k * 17 % 10) / 10f - 0.5f) * 1.0f, d = Mathf.Clamp01((tt - (k * 13 % 10) * 0.01f) / 0.45f);
                if (d <= 0f || d >= 1f)
                    continue;
                float r0 = Mathf.Lerp(18f, 200f, d * (2f - d)), len = 34f + (k * 29 % 10) * 4f;
                Line(o + V2.FromAngle(ang) * Mathf.Max(0f, r0 - len), o + V2.FromAngle(ang) * r0, 1.4f, new Rgba(1f, 1f, 1f, 0.75f * (1f - d)));
            }
            for (int k = 0; k < 22; k++)
            {
                float ang = s.ClapAim + ((k * 31 % 10) / 10f - 0.5f) * 1.5f, v = 110f + (k * 47 % 10) * 26f, life = 0.3f + (k * 19 % 10) * 0.04f;
                if (tt > life)
                    continue;
                Circle(o + V2.FromAngle(ang) * (v * tt), 1.6f, new Rgba(1f, 0.82f + (k % 3) * 0.06f, 0.4f, 1f - tt / life), 5);
            }
        }

        /// World pass: ribbons behind the blade and the rifle tips while they swing, afterimages on the lunge, the clap's cracks, the spin's ring.
        void DrawAmmoSmears(Player p, float t)
        {
            if (!fxOn || p.Downed || p.Weapon.Id != WeaponId.OutOfAmmo)
                return;
            float now = Time.unscaledTime;
            V2 dirF = V2.FromAngle(p.Aim);
            if (p.AmmoSpin > 0f)
            {
                float el = Game.AmmoSpinTime - p.AmmoSpin, a = el * AmmoSpinRate;
                Arc(p.Disp, 64f, 4f, new Rgba(1f, 0.76f, 0.24f, 0.6f), 22, a - 1.5f, 1.5f);
                Arc(p.Disp, 60f, 3.4f, new Rgba(0.25f, 0.88f, 0.91f, 0.55f), 22, a + MathX.Pi - 1.3f, 1.3f);
                Arc(p.Disp, Game.AmmoSpinRadius, 1.2f, new Rgba(1f, 0.76f, 0.24f, 0.2f), 32);
                return;
            }
            var s = AmmoOf(p);
            DrawAmmoClap(s, now);  // the ring burst and the cone
            bool active = now - s.T0 < 1.3f || now - s.KT0 < 0.7f || now - s.SwT0L < 0.4f || now - s.SwT0R < 0.4f;
            if (!active)
                return;
            const int steps = 12;
            var tipK = new V2[steps + 1]; var baseK = new V2[steps + 1];
            var tipL = new V2[steps + 1]; var baseL = new V2[steps + 1];
            var tipR = new V2[steps + 1]; var baseR = new V2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                var o = AmmoCompute(p, s, i * 0.01f);
                float ca = Mathf.Cos(o.KAk), sa = Mathf.Sin(o.KAk);
                float sy = AmmoKShoulder, ex, ey, hx, hy;
                AmmoArm(3f, sy, o.KRX, o.KRY, 1f, out ex, out ey, out hx, out hy);
                tipK[i] = AmmoWorld(p, o, hx + ca * 24f, hy + sa * 24f); baseK[i] = AmmoWorld(p, o, hx + ca * 4f, hy + sa * 4f);
                tipL[i] = AmmoWorld(p, o, o.GX0 + Mathf.Cos(o.GH0) * AmmoGunLen, o.GY0 + Mathf.Sin(o.GH0) * AmmoGunLen); baseL[i] = AmmoWorld(p, o, o.GX0 + Mathf.Cos(o.GH0) * 10f, o.GY0 + Mathf.Sin(o.GH0) * 10f);
                tipR[i] = AmmoWorld(p, o, o.GX1 + Mathf.Cos(o.GH1) * AmmoGunLen, o.GY1 + Mathf.Sin(o.GH1) * AmmoGunLen); baseR[i] = AmmoWorld(p, o, o.GX1 + Mathf.Cos(o.GH1) * 10f, o.GY1 + Mathf.Sin(o.GH1) * 10f);
            }
            float sk = tipK[0].DistanceTo(tipK[3]), sl = tipL[0].DistanceTo(tipL[3]), sr = tipR[0].DistanceTo(tipR[3]);
            if (sk > 4f) AmmoRibbon(tipK, baseK, steps, new Rgba(1f, 0.78f, 0.28f), Mathf.Clamp01((sk - 4f) / 14f) * 0.9f);
            if (sl > 4f) AmmoRibbon(tipL, baseL, steps, new Rgba(0.35f, 0.9f, 1f), Mathf.Clamp01((sl - 4f) / 16f) * 0.7f);
            if (sr > 4f) AmmoRibbon(tipR, baseR, steps, new Rgba(0.35f, 0.9f, 1f), Mathf.Clamp01((sr - 4f) / 16f) * 0.7f);
            // afterimages behind the lunging stab
            float kt = now - s.KT0;
            if (s.KMove == 3 && kt > 0.1f && kt < 0.5f)
            {
                float a = 1f - Mathf.Clamp01((kt - 0.2f) / 0.3f);
                for (int g = 1; g <= 3; g++)
                    Circle(p.Disp - dirF * (g * 6f), p.Def.BodyR * (1f - g * 0.08f), new Rgba(0.55f, 0.5f, 1f, 0.22f * a / g), 14);
            }
            // a slash mark across the target as a cut lands
            if (s.KMove > 0 && s.KMove < 3)
            {
                float st = kt - AmmoMotion.KHit[s.KMove - 1];
                if (st >= 0f && st < 0.14f)
                {
                    V2 at = p.Disp + dirF * (p.Def.BodyR + 44f), n = dirF.Orthogonal();
                    float tilt = s.KMove == 1 ? 0.6f : -0.6f, f = 1f - st / 0.14f;
                    V2 d = (n * Mathf.Sin(tilt) + dirF * Mathf.Cos(tilt)) * 16f;
                    Line(at - d, at + d, 3f * f + 1f, new Rgba(1f, 1f, 1f, 0.9f * f));
                }
            }
        }
    }
}
