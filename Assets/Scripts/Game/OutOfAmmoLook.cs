using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Out of Ammo (Core/OutOfAmmo.cs): the Twin Rifle's second variant drawn as a fighter. The rifles are lashed to the forearms (two-bone arms, the
    /// gun along the forearm), the knife is in the right hand. Moves are keyframed in the Gun and Knife mockup's units (a model unit each) and
    /// played from a clock this look starts when Player.AmmoSeq rises: left bash, cross cut, right bash, reverse cut, rifle clap; a swat swings one
    /// arm to the bullet's angle (Player.SwatSeqL/R, SwatAngL/R); Alt's spin turns the whole body. Hit sparks and the clap's ring come with Visual
    /// effects on; the smears behind the blade and rifles are drawn in the world pass (DrawAmmoSmears).
    public partial class ArenaApp
    {
        class AmmoSeen { public byte Seq, SwL, SwR; public int Move; public float T0 = -99f, SwT0L = -99f, SwT0R = -99f; public bool Fired; }
        class AmmoPose { public float Face, Lean, Ak, LX, LY, RX, RY; public bool Glow, LHot, RHot; }

        readonly Dictionary<int, AmmoSeen> ammoSeen = new Dictionary<int, AmmoSeen>();
        readonly AmmoPose ammoPose = new AmmoPose();

        static readonly float[] AmmoStart = { 0f, .45f, .95f, 1.42f, 2.0f };
        // keyframes in the mockup's time: [t, x, y(, knife angle)], x forward and y to the right of the body
        static readonly float[][] AmmoLH = { new[] { 0f, 22, -26 }, new[] { .12f, 4, -33 }, new[] { .2f, 50, -8 }, new[] { .42f, 22, -26 }, new[] { 2.0f, 22, -26 }, new[] { 2.4f, -4, -44 }, new[] { 2.5f, 56, -7 }, new[] { 2.76f, 56, -7 }, new[] { 3.2f, 22, -26 } };
        static readonly float[][] AmmoRH = {
            new[] { 0f, 22, 26, .5f }, new[] { .45f, 22, 26, .5f }, new[] { .57f, 12, 42, 1.9f }, new[] { .62f, 38, 8, .15f }, new[] { .68f, 28, -34, -1.5f }, new[] { .9f, 22, 26, .5f },
            new[] { .95f, 22, 26, .5f }, new[] { 1.07f, 4, 34, 2.5f }, new[] { 1.12f, 50, 8, 2.5f }, new[] { 1.37f, 22, 26, .5f },
            new[] { 1.42f, 22, 26, .5f }, new[] { 1.54f, 14, -38, -1.9f }, new[] { 1.6f, 38, -8, -.15f }, new[] { 1.66f, 28, 34, 1.5f }, new[] { 1.85f, 22, 26, .5f },
            new[] { 2.0f, 22, 26, .5f }, new[] { 2.4f, -4, 44, .8f }, new[] { 2.5f, 56, 7, 0f }, new[] { 2.76f, 56, 7, 0f }, new[] { 3.2f, 22, 26, .5f } };
        static readonly float[][] AmmoFace = { new[] { 0f, 0f }, new[] { .12f, -.22f }, new[] { .2f, .1f }, new[] { .42f, 0f }, new[] { .45f, 0f }, new[] { .57f, .38f }, new[] { .68f, -.4f }, new[] { .9f, 0f },
            new[] { .95f, 0f }, new[] { 1.07f, .22f }, new[] { 1.12f, -.1f }, new[] { 1.37f, 0f }, new[] { 1.42f, 0f }, new[] { 1.54f, -.36f }, new[] { 1.66f, .4f }, new[] { 1.85f, 0f }, new[] { 2.4f, 0f }, new[] { 3.2f, 0f } };
        static readonly float[][] AmmoLean = { new[] { 0f, 0f }, new[] { .12f, -3f }, new[] { .2f, 5f }, new[] { .42f, 0f }, new[] { .57f, -2f }, new[] { .68f, 4f }, new[] { .9f, 0f }, new[] { 1.07f, -3f }, new[] { 1.12f, 5f },
            new[] { 1.37f, 0f }, new[] { 1.54f, -2f }, new[] { 1.66f, 4f }, new[] { 1.85f, 0f }, new[] { 2.0f, 0f }, new[] { 2.4f, -11f }, new[] { 2.5f, 18f }, new[] { 2.76f, 18f }, new[] { 3.2f, 0f } };

        const float AmmoUpper = 27f, AmmoFore = 27f, AmmoShoulder = 15f, AmmoSwatTime = 0.22f, AmmoSpinRate = 14f;

        static float[] AmmoTrack(float[][] k, float t)
        {
            if (t <= k[0][0])
                return k[0];
            for (int i = 1; i < k.Length; i++)
                if (t <= k[i][0])
                {
                    float p = Mathf.Clamp01((t - k[i - 1][0]) / Mathf.Max(0.0001f, k[i][0] - k[i - 1][0]));
                    p = p * p * (3f - 2f * p);
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
                ammoSeen[p.Id] = s = new AmmoSeen { Seq = p.AmmoSeq, SwL = p.SwatSeqL, SwR = p.SwatSeqR };
            float now = Time.unscaledTime;
            if (p.AmmoSeq != s.Seq)
            {
                s.Seq = p.AmmoSeq; s.Move = p.AmmoMove; s.T0 = now; s.Fired = false;
            }
            if (p.SwatSeqL != s.SwL) { s.SwL = p.SwatSeqL; s.SwT0L = now; }
            if (p.SwatSeqR != s.SwR) { s.SwR = p.SwatSeqR; s.SwT0R = now; }
            return s;
        }

        /// The pose `back` seconds ago, into ammoPose (model units: x forward, y right of the body).
        AmmoPose AmmoCompute(Player p, AmmoSeen s, float back)
        {
            var o = ammoPose;
            float at = Time.unscaledTime - back;
            o.Face = 0f; o.Lean = 0f; o.Glow = false; o.LHot = o.RHot = false;
            o.LX = 22f; o.LY = -26f; o.RX = 22f; o.RY = 26f; o.Ak = .5f;
            if (p.AmmoSpin > 0f && back <= 0f)
            {
                float el = Game.AmmoSpinTime - p.AmmoSpin, ease = Mathf.Clamp01(el / 0.15f);
                o.Face = el * AmmoSpinRate;
                o.LX = Mathf.Lerp(22f, 30f, ease); o.LY = Mathf.Lerp(-26f, -46f, ease);
                o.RX = Mathf.Lerp(22f, 30f, ease); o.RY = Mathf.Lerp(26f, 46f, ease); o.Ak = Mathf.Lerp(.5f, .7f, ease);
                o.Glow = o.LHot = o.RHot = true;
                return o;
            }
            float tau = at - s.T0;
            if (s.Move > 0 && tau >= 0f && tau < AmmoMotion.Total[s.Move - 1])
            {
                float tg = AmmoStart[s.Move - 1] + tau;
                var l = AmmoTrack(AmmoLH, tg); var r = AmmoTrack(AmmoRH, tg);
                o.LX = l[1]; o.LY = l[2]; o.RX = r[1]; o.RY = r[2]; o.Ak = r[3];
                o.Face = AmmoTrack(AmmoFace, tg)[1]; o.Lean = AmmoTrack(AmmoLean, tg)[1];
                int m = s.Move - 1;
                o.Glow = (m == 1 || m == 3) && tau > AmmoMotion.Wind[m] && tau < AmmoMotion.Strike[m] + .05f || m == 4 && tau > AmmoMotion.Wind[m] - .1f && tau < AmmoMotion.Total[m] - .45f;
                o.LHot = m == 0 && tau > .1f && tau < .3f || m == 4 && tau > AmmoMotion.Wind[m] && tau < .75f;
                o.RHot = m == 2 && tau > .1f && tau < .3f || m == 4 && tau > AmmoMotion.Wind[m] && tau < .75f;
            }
            for (int g = 0; g < 2; g++)  // a swat swings that arm out to the bullet and back
            {
                float st = at - (g == 0 ? s.SwT0L : s.SwT0R);
                if (st < 0f || st > AmmoSwatTime)
                    continue;
                float ph = st / AmmoSwatTime, sw = Mathf.Sin(ph * Mathf.PI) * .9f * (g == 0 ? -1f : 1f);
                float world = g == 0 ? p.SwatAngL : p.SwatAngR, a = MathX.Fmod(world - (p.Aim + o.Face) + MathX.Pi, MathX.Tau) - MathX.Pi + sw;
                float sy = g == 0 ? -AmmoShoulder : AmmoShoulder, reach = 20f + 22f * Mathf.Sin(ph * Mathf.PI);
                if (g == 0) { o.LX = Mathf.Cos(a) * reach; o.LY = sy + Mathf.Sin(a) * reach; o.LHot = ph > .3f && ph < .7f; }
                else { o.RX = Mathf.Cos(a) * reach; o.RY = sy + Mathf.Sin(a) * reach; o.Ak = 2.5f; o.RHot = ph > .3f && ph < .7f; }
            }
            return o;
        }

        /// Model-frame elbow for a two-bone arm from the shoulder (0, sy) to the hand target; side -1 left, +1 right. Returns the hand, clamped to reach.
        static void AmmoArm(float sy, float tx, float ty, float side, out float ex, out float ey, out float hx, out float hy)
        {
            float dx = tx, dy = ty - sy, d = Mathf.Sqrt(dx * dx + dy * dy), dm = AmmoUpper + AmmoFore - .5f;
            if (d > dm) { dx *= dm / d; dy *= dm / d; d = dm; }
            d = Mathf.Max(d, 6f);
            float a = Mathf.Atan2(dy, dx), c = Mathf.Clamp((AmmoUpper * AmmoUpper + d * d - AmmoFore * AmmoFore) / (2f * AmmoUpper * d), -1f, 1f), s = Mathf.Acos(c) * side;
            ex = Mathf.Cos(a + s) * AmmoUpper; ey = sy + Mathf.Sin(a + s) * AmmoUpper;
            hx = dx; hy = sy + dy;
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

        /// The arms, the rifles lashed to the forearms and the knife (called in place of the usual arms and held weapon).
        void DrawAmmoModel(Player p, ModelPal K)
        {
            var s = AmmoOf(p);
            var o = AmmoCompute(p, s, 0f);
            AmmoFx(p, s);
            for (int g = 0; g < 2; g++)
            {
                float side = g == 0 ? -1f : 1f, sy = side * AmmoShoulder, ex, ey, hx, hy;
                AmmoArm(sy, g == 0 ? o.LX : o.RX, g == 0 ? o.LY : o.RY, side, out ex, out ey, out hx, out hy);
                MLine(0f, sy, ex, ey, 6f, K.Dark); MLine(ex, ey, hx, hy, 6f, K.Dark);
                MLine(0f, sy, ex, ey, 4.4f, K.Sleeve); MLine(ex, ey, hx, hy, 4.4f, K.Sleeve);
                MCirc(ex, ey, 2.4f, K.Sleeve, 8);
                // the rifle: lashed along the forearm, its muzzle past the hand
                float fl = Mathf.Max(8f, Mathf.Sqrt((hx - ex) * (hx - ex) + (hy - ey) * (hy - ey))), ux = (hx - ex) / fl, uy = (hy - ey) / fl;
                float bx = ex + ux * fl * .25f, by = ey + uy * fl * .25f, mx = hx + ux * 17f, my = hy + uy * 17f;
                bool hot = g == 0 ? o.LHot : o.RHot;
                MLine(bx, by, mx, my, 6.4f, MDark);
                MLine(bx, by, mx, my, 4f, hot ? MWhite : MSteel.Mix(MDark, .25f));
                MLine(hx + ux * 8f, hy + uy * 8f, mx, my, 2.4f, Hx(0xcfd8e6));
                MLine(mx - uy * 2.2f, my + ux * 2.2f, mx + uy * 2.2f, my - ux * 2.2f, 2f, Hx(0x11151b));  // the muzzle brake
                MLine(ex + ux * fl * .5f - uy * 2f, ey + uy * fl * .5f + ux * 2f, ex + ux * fl * .5f + uy * 2f, ey + uy * fl * .5f - ux * 2f, 2f, Hx(0x5b4a30));  // a strap
                if (g == 1)
                {
                    // the knife: in the right hand
                    float ca = Mathf.Cos(o.Ak), sa = Mathf.Sin(o.Ak), nx = -sa, ny = ca;
                    MLine(hx - ca * 4f, hy - sa * 4f, hx, hy, 3.4f, Hx(0x5b4a30));
                    MLine(hx + nx * 3f, hy + ny * 3f, hx - nx * 3f, hy - ny * 3f, 1.8f, Hx(0x9aa7b3));
                    var blade = o.Glow ? MGold : MSteel;
                    MPoly(blade, hx + nx * 1.8f, hy + ny * 1.8f, hx + ca * 15f + nx * 1.6f, hy + sa * 15f + ny * 1.6f, hx + ca * 21f, hy + sa * 21f, hx + ca * 15f - nx * .9f, hy + sa * 15f - ny * .9f, hx - nx * 1.8f, hy - ny * 1.8f);
                }
                MCirc(hx, hy, 3.2f, K.Glove);
            }
        }

        /// Sparks where a move lands (once, with Visual effects on).
        void AmmoFx(Player p, AmmoSeen s)
        {
            if (!Juicy || s.Move <= 0 || s.Fired)
                return;
            int m = s.Move - 1;
            if (Time.unscaledTime - s.T0 < AmmoMotion.Hit[m])
                return;
            s.Fired = true;
            V2 at = p.Disp + V2.FromAngle(p.Aim) * (p.Def.BodyR + 46f);
            var col = m == 4 ? new Rgba(1f, 0.76f, 0.24f) : m == 1 || m == 3 ? new Rgba(0.8f, 0.85f, 0.9f) : new Rgba(0.25f, 0.88f, 0.91f);
            Sparks(at, col, m == 4 ? 22 : 9, m == 4 ? 300f : 190f, 0.25f, 1.6f);
            Ring(at, m == 4 ? 70f : 22f, m == 4 ? 0.45f : 0.25f, col);
        }

        /// World pass: smears behind the blade and the rifle tips while they swing, and the spin's ring.
        void DrawAmmoSmears(Player p, float t)
        {
            if (!fxOn || p.Downed || p.Weapon.Id != WeaponId.OutOfAmmo)
                return;
            float u = p.Def.BodyR / 18f;
            if (p.AmmoSpin > 0f)
            {
                float el = Game.AmmoSpinTime - p.AmmoSpin, a = el * AmmoSpinRate;
                Arc(p.Disp, 64f, 3.4f, new Rgba(1f, 0.76f, 0.24f, 0.55f), 20, a - 1.3f, 1.3f);
                Arc(p.Disp, 60f, 3f, new Rgba(0.25f, 0.88f, 0.91f, 0.5f), 20, a + MathX.Pi - 1.1f, 1.1f);
                Arc(p.Disp, Game.AmmoSpinRadius, 1.2f, new Rgba(1f, 0.76f, 0.24f, 0.18f), 32);
                return;
            }
            var s = AmmoOf(p);
            if (Time.unscaledTime - s.T0 > 1.4f && Time.unscaledTime - s.SwT0L > .4f && Time.unscaledTime - s.SwT0R > .4f)
                return;
            const int steps = 8;
            var tips = new V2[3, steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                var o = AmmoCompute(p, s, i * 0.012f);
                float ang = p.Aim + o.Face;
                V2 dir = V2.FromAngle(ang), side = new V2(-dir.Y, dir.X), org = p.Disp + dir * (o.Lean * u);
                for (int g = 0; g < 2; g++)
                {
                    float sd = g == 0 ? -1f : 1f, sy = sd * AmmoShoulder, ex, ey, hx, hy;
                    AmmoArm(sy, g == 0 ? o.LX : o.RX, g == 0 ? o.LY : o.RY, sd, out ex, out ey, out hx, out hy);
                    float fl = Mathf.Max(8f, Mathf.Sqrt((hx - ex) * (hx - ex) + (hy - ey) * (hy - ey))), mx = hx + (hx - ex) / fl * 17f, my = hy + (hy - ey) / fl * 17f;
                    tips[g, i] = org + (dir * mx + side * my) * u;
                    if (g == 1)
                    {
                        float kx = hx + Mathf.Cos(o.Ak) * 21f, ky = hy + Mathf.Sin(o.Ak) * 21f;
                        tips[2, i] = org + (dir * kx + side * ky) * u;
                    }
                }
            }
            for (int w = 0; w < 3; w++)
            {
                float sp = tips[w, 0].DistanceTo(tips[w, 3]);
                float al = Mathf.Clamp01((sp - 6f) / 22f);
                if (al <= 0.02f)
                    continue;
                var col = w == 2 ? new Rgba(1f, 0.76f, 0.24f) : new Rgba(0.25f, 0.88f, 0.91f);
                for (int i = 0; i < steps; i++)
                    Line(tips[w, i], tips[w, i + 1], 3.2f * (1f - i / (float)steps) + .8f, col.WithAlpha(al * 0.65f * (1f - i / (float)steps)));
            }
        }
    }
}
