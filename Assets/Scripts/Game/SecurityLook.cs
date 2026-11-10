using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Security Guard (Core/SecurityGuard.cs) and its tracer round (Defs.TracerRound), drawn as in
    /// `Docs/Mockups/security-guard.html` (look B, the white tracer), in world pixels so the game matches it:
    ///   Under it: the locked shot's red lane and shrinking reticle (Enemy.Winding), and the cream reload ring.
    ///   The body: a slate uniform with a hi-vis vest, a belt pouch behind, a blinking shoulder radio, epaulettes, a peaked cap,
    ///   and a two-handed pistol whose slide kicks back. Reloading it lowers the pistol, drops the magazine, seats a new one
    ///   and racks the slide. A brass casing flies out of each shot. Above it: the rounds left, as little brass rounds.
    /// The ammo is Enemy.Phase (0: reloading); the recoil, the reload and the casing are timed from the age of the Phase this
    /// peer sees (Enemy.SeenT). Every colour is the mockup's.
    public partial class ArenaApp
    {
        // Frame: a point in the guard's own space (x forward, y to its right), scaled with its size.
        struct SecFrame
        {
            public V2 O, F, S;
            public float K;
            public V2 P(float x, float y) => O + F * (x * K) + S * (y * K);
        }

        static Rgba Hex(int rgb, float a = 1f) => new Rgba(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        static float SecEase(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
        static float SecLerp(float a, float b, float u) => a + (b - a) * u;

        static readonly Rgba SecLaneRed = new Rgba(1f, 0.43f, 0.31f), SecCream = new Rgba(0.914f, 0.886f, 0.784f);

        readonly Dictionary<int, float> secWindSince = new Dictionary<int, float>();

        /// Keyframes along the reload (x, y per key), eased between them like the mockup's kf().
        static V2 SecKf(float k, float[][] pts)
        {
            if (k <= pts[0][0])
                return new V2(pts[0][1], pts[0][2]);
            for (int i = 1; i < pts.Length; i++)
                if (k <= pts[i][0])
                {
                    float u = SecEase((k - pts[i - 1][0]) / (pts[i][0] - pts[i - 1][0]));
                    return new V2(SecLerp(pts[i - 1][1], pts[i][1], u), SecLerp(pts[i - 1][2], pts[i][2], u));
                }
            var l = pts[pts.Length - 1];
            return new V2(l[1], l[2]);
        }

        static readonly float[][] SecHand = {
            new[] { 0f, 15f, -1.8f }, new[] { 0.14f, 12f, 6f }, new[] { 0.32f, 12f, 6f }, new[] { 0.5f, -3f, -11f },
            new[] { 0.74f, 11f, 6f }, new[] { 0.88f, 11f, 6f }, new[] { 1f, 15f, -1.8f } };

        /// A line with round ends.
        void SecRound(V2 a, V2 b, float w, Rgba c)
        {
            Line(a, b, w, c);
            Circle(a, w / 2f, c, 8);
            Circle(b, w / 2f, c, 8);
        }

        /// The player the locked shot is meant for: the one nearest its line (a front end guess at the lead point).
        float SecLaneLength(Enemy e, V2 muzzle, V2 dir)
        {
            float best = 1e9f, along = 300f;
            foreach (var p in game.Players.Values)
            {
                if (p.Downed)
                    continue;
                V2 rel = p.Disp - muzzle;
                float a = rel.Dot(dir), off = Mathf.Abs(rel.Dot(new V2(-dir.Y, dir.X)));
                if (a > 0f && off < best)
                {
                    best = off;
                    along = a;
                }
            }
            return Mathf.Clamp(along, 60f, 480f);
        }

        /// On the floor: the locked shot, and the reload ring.
        void DrawSecurityUnder(Enemy e, V2 pos, float r, float t)
        {
            if (e.Winding && e.Phase > 0)
            {
                if (!secWindSince.TryGetValue(e.Id, out float since))
                {
                    if (secWindSince.Count > 128)
                        secWindSince.Clear();
                    since = secWindSince[e.Id] = t;
                }
                float k = SecEase((t - since) / Game.SecurityWindUp);
                V2 u = V2.FromAngle(e.Facing), n = new V2(-u.Y, u.X), m = pos + u * (30f * r / 15f);
                float len = SecLaneLength(e, m, u), reach = len + 24f;
                Quad(m + n * 3.5f, m + u * reach + n * 3.5f, m + u * reach - n * 3.5f, m - n * 3.5f, new Rgba(1f, 0.69f, 0.18f, 0.14f));
                const int steps = 10;
                float fill = reach * k;
                for (int i = 0; i < steps; i++)  // the red filling toward the lead point, stronger at the far end
                {
                    float a0 = (float)i / steps, a1 = (float)(i + 1) / steps;
                    if (fill * a0 >= fill)
                        break;
                    Quad(m + u * (fill * a0) + n * 3.5f, m + u * (fill * a1) + n * 3.5f, m + u * (fill * a1) - n * 3.5f, m + u * (fill * a0) - n * 3.5f,
                        new Rgba(1f, 0.27f, 0.2f, 0.1f + 0.6f * a1));
                }
                Rgba edge = new Rgba(1f, 0.43f, 0.31f, 0.95f);
                Line(m + n * 3.5f, m + u * reach + n * 3.5f, 5f, edge.WithAlpha(0.22f));  // the glow
                Line(m - n * 3.5f, m + u * reach - n * 3.5f, 5f, edge.WithAlpha(0.22f));
                Line(m + n * 3.5f, m + u * reach + n * 3.5f, 1.8f, edge);
                Line(m - n * 3.5f, m + u * reach - n * 3.5f, 1.8f, edge);
                Line(m + u * reach + n * 3.5f, m + u * reach - n * 3.5f, 1.8f, edge);
                V2 lead = m + u * len;  // the reticle, closing to the size of a round
                float rr = SecLerp(26f, 11f, k);
                Rgba rc = SecLaneRed.WithAlpha(0.5f + 0.45f * k);
                Arc(lead, rr, 2.2f, rc, 28);
                for (int i = 0; i < 4; i++)
                {
                    V2 d = V2.FromAngle(i * MathX.Pi / 2f + MathX.Pi / 4f);
                    Line(lead + d * (rr - 3f), lead + d * (rr + 4f), 2.2f, rc);
                }
            }
            else
                secWindSince.Remove(e.Id);
            if (e.Phase == 0)
            {
                float rk = Mathf.Clamp01(e.SeenT / Game.SecurityReload);
                for (int i = 0; i < 24; i++)  // the dashed ring, then the cream sweep
                    Arc(pos, 38f * r / 15f, 1.5f, SecCream.WithAlpha(0.3f), 3, i * MathX.Tau / 24f, MathX.Tau / 48f);
                Circle(pos, 38f * r / 15f, SecCream.WithAlpha(0.04f + 0.06f * (1f - rk)), 32);
                Arc(pos, 38f * r / 15f, 3f, SecCream.WithAlpha(0.9f), Mathf.Max(3, (int)(40 * rk)), -MathX.Pi / 2f, MathX.Tau * rk);
            }
        }

        /// The body, in place of the usual polygon (the uniform is drawn in the mockup's own colours; a hit flashes it white).
        void DrawSecurityBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 fwd = V2.FromAngle(e.Facing);
            var F = new SecFrame { O = pos, F = fwd, S = new V2(-fwd.Y, fwd.X), K = r / 15f };
            float s = F.K;
            bool reloading = e.Phase == 0;
            float rk = reloading ? Mathf.Clamp01(e.SeenT / Game.SecurityReload) : 0f;
            float recoil = !reloading && e.Phase < Game.SecurityMag ? Mathf.Clamp01(1f - e.SeenT * 7f) : 0f;
            float aim = e.Winding ? 1f : 0.35f;

            // the belt pouch trailing behind
            SetColor(Hex(0x14232b));
            V2 c0 = F.P(-12f, 0f);
            V2[] pouch = { F.P(-10f, -11f), F.P(-18f, -9.5f), F.P(-22f, -4f), F.P(-22f, 4f), F.P(-18f, 9.5f), F.P(-10f, 11f) };
            for (int i = 0; i < pouch.Length - 1; i++)
                Tri(c0, pouch[i], pouch[i + 1]);
            Tri(c0, pouch[pouch.Length - 1], pouch[0]);
            for (int i = 0; i < pouch.Length - 1; i++)
                Line(pouch[i], pouch[i + 1], 1.5f * s, Hex(0x0a141a));
            Quad(F.P(-20f, -6f), F.P(-14f, -6f), F.P(-14f, -1f), F.P(-20f, -1f), Hex(0x223641));
            Quad(F.P(-20f, 2f), F.P(-14f, 2f), F.P(-14f, 7f), F.P(-20f, 7f), Hex(0x223641));

            // the body: a dark rim and a shaded disc (the mockup's radial gradient, as stacked discs)
            Circle(pos, r, Hex(0x0f2029), 28);
            Circle(pos, r - 1.25f * s, Hex(0x2a4856), 28);
            float[] rad = { 12.5f, 10f, 7.5f, 5f };
            int[] shade = { 0x35596b, 0x436c81, 0x5584a0, 0x6f9cb2 };
            for (int i = 0; i < rad.Length; i++)
                Circle(F.P(-4f * (i + 1) / 4f, -4f * (i + 1) / 4f), rad[i] * s, Hex(shade[i]), 24);

            // the hi-vis vest: two bands across the chest, clipped to the body
            float inner = r - 1.25f * s;
            System.Func<float, float> H = x => Mathf.Sqrt(Mathf.Max(0f, inner * inner - x * x * s * s)) / s;
            System.Action<float, float, int, int, float, float> Band = (x0, x1, edgeCol, midCol, hi0, hi1) =>
            {
                float h0 = H(x0), h1 = H(x1);
                Quad(F.P(x0, -h0), F.P(x1, -h1), F.P(x1, h1), F.P(x0, h0), Hex(edgeCol));
                Quad(F.P(x0, -h0 * 0.55f), F.P(x1, -h1 * 0.55f), F.P(x1, h1 * 0.55f), F.P(x0, h0 * 0.55f), Hex(midCol));
                Quad(F.P(hi0, -H(hi0)), F.P(hi1, -H(hi1)), F.P(hi1, H(hi1)), F.P(hi0, H(hi0)), new Rgba(1f, 1f, 1f, 0.7f));
            };
            Band(-9f, -5.4f, 0xc9d33a, 0xeef55c, -7.9f, -6.9f);
            Band(-1.5f, 1.1f, 0xc9d33a, 0xeef55c, -0.9f, -0.1f);
            Arc(pos, r - 1.25f * s, 2.5f * s, Hex(0x0f2029), 28);

            // the shoulder radio, with a blinking light
            Quad(F.P(-3f, -19f), F.P(5f, -19f), F.P(5f, -14f), F.P(-3f, -14f), Hex(0x4d6370));
            Quad(F.P(-2.2f, -18.2f), F.P(4.2f, -18.2f), F.P(4.2f, -14.8f), F.P(-2.2f, -14.8f), Hex(0x10181d));
            Circle(F.P(3f, -16.5f), 1.2f * s, Mathf.FloorToInt(t * 1.5f) % 2 != 0 ? Hex(0x5dff9a) : Hex(0x1a5b37), 8);

            // arms and the pistol
            float gx, gy, gr, slideBack = recoil * 4.2f;
            V2 lh;
            bool magInHand = false;
            if (!reloading)
            {
                gx = SecLerp(15f, 20f, aim) - recoil * 3.5f;
                gy = 0f;
                gr = 0f;
                lh = new V2(gx - 1f, -1.8f);
            }
            else
            {
                float w = SecEase(Mathf.Min(rk / 0.14f, 1f)) * (1f - SecEase((rk - 0.9f) / 0.1f));
                gx = SecLerp(15f, 11f, w);
                gy = SecLerp(0f, 9f, w);
                gr = SecLerp(0f, 0.5f, w);
                lh = SecKf(rk, SecHand);
                magInHand = rk > 0.5f && rk < 0.76f;
                slideBack = rk > 0.9f ? Mathf.Sin((rk - 0.9f) / 0.1f * MathX.Pi) * 5f : 0f;
            }
            Rgba armCol = Hex(0x2b4a59), armEdge = Hex(0x0f2029);
            V2 rh = new V2(gx + 1.5f, gy + 2f);
            SecRound(F.P(0f, 12f), F.P(rh.X, rh.Y), 8f * s, armEdge);
            SecRound(F.P(0f, -12f), F.P(lh.X, lh.Y), 8f * s, armEdge);
            SecRound(F.P(0f, 12f), F.P(rh.X, rh.Y), 5.5f * s, armCol);
            SecRound(F.P(0f, -12f), F.P(lh.X, lh.Y), 5.5f * s, armCol);

            float cg = Mathf.Cos(gr), sg = Mathf.Sin(gr);
            System.Func<float, float, V2> G = (x, y) => F.P(gx + x * cg - y * sg, gy + x * sg + y * cg);
            Quad(G(-2f, -2.6f), G(14f, -2.6f), G(14f, 2.6f), G(-2f, 2.6f), Hex(0x262c31));       // the frame under the slide
            float sx0 = -1f - slideBack, sx1 = 12f - slideBack;
            Quad(G(sx0 - 0.5f, -2.9f), G(sx1 + 0.5f, -2.9f), G(sx1 + 0.5f, 2.9f), G(sx0 - 0.5f, 2.9f), Hex(0x3c474f));  // the slide's outline
            Quad(G(sx0, -2.4f), G(sx1, -2.4f), G(sx1, -0.8f), G(sx0, -0.8f), Hex(0xe3e9ed));      // and its shading
            Quad(G(sx0, -0.8f), G(sx1, -0.8f), G(sx1, 0.8f), G(sx0, 0.8f), Hex(0xb4bfc7));
            Quad(G(sx0, 0.8f), G(sx1, 0.8f), G(sx1, 2.4f), G(sx0, 2.4f), Hex(0x7b8892));
            Quad(G(12f, -1.1f), G(15.2f, -1.1f), G(15.2f, 1.1f), G(12f, 1.1f), Hex(0x394249));    // the barrel
            for (int i = 0; i < 3; i++)
                Line(G(1.5f - slideBack + i * 1.6f, -2.2f), G(1.5f - slideBack + i * 1.6f, 2.2f), 0.8f * s, Hex(0x28343c, 0.6f));
            if (!reloading && aim > 0.5f)
                Circle(G(11.4f, 0f), 1f * s, Hex(0xffb02e), 8);  // the front sight
            if (recoil > 0.55f)  // the muzzle flash
            {
                float fk = (recoil - 0.55f) / 0.45f;
                TaserHalo(G(15f, 0f), 22f * s, Hex(0xffc866), 0.55f * fk);
                SetColor(new Rgba(1f, 0.957f, 0.784f, fk));
                V2 a = G(15f, 0f), b = G(22f, -4.5f * fk - 1f), c = G(31f * fk + 8f, 0f), d = G(22f, 4.5f * fk + 1f);
                Tri(a, b, c);
                Tri(a, c, d);
                Circle(G(15f, 0f), 3.5f * s * fk, new Rgba(1f, 1f, 1f, fk), 10);
            }

            // the magazine: in the off hand while it is seated, and the old one falling away
            Rgba magCol = Hex(0x8c969e);
            if (magInHand)
            {
                Quad(F.P(lh.X - 2.9f, lh.Y - 4.8f), F.P(lh.X + 2.9f, lh.Y - 4.8f), F.P(lh.X + 2.9f, lh.Y + 4.8f), F.P(lh.X - 2.9f, lh.Y + 4.8f), Hex(0x2c363c));
                Quad(F.P(lh.X - 2.2f, lh.Y - 4f), F.P(lh.X + 2.2f, lh.Y - 4f), F.P(lh.X + 2.2f, lh.Y + 4f), F.P(lh.X - 2.2f, lh.Y + 4f), magCol);
                Quad(F.P(lh.X - 1.4f, lh.Y - 4.6f), F.P(lh.X + 1.4f, lh.Y - 4.6f), F.P(lh.X + 1.4f, lh.Y - 2.8f), F.P(lh.X - 1.4f, lh.Y - 2.8f), Hex(0xd2a84c));
            }
            if (reloading && rk > 0.15f && rk < 0.42f)
            {
                float df = (rk - 0.15f) / 0.27f, ca = Mathf.Cos(df * 1.6f), sa = Mathf.Sin(df * 1.6f);
                V2 o = F.P(gx + 1f, gy + 3f + df * 17f);
                System.Func<float, float, V2> Q = (x, y) => o + F.F * ((x * ca - y * sa) * s) + F.S * ((x * sa + y * ca) * s);
                float al = 1f - df * 0.8f;
                Quad(Q(-2.9f, -4.8f), Q(2.9f, -4.8f), Q(2.9f, 4.8f), Q(-2.9f, 4.8f), Hex(0x2c363c, al));
                Quad(Q(-2.2f, -4f), Q(2.2f, -4f), Q(2.2f, 4f), Q(-2.2f, 4f), magCol.WithAlpha(al));
            }

            // gloves
            Circle(F.P(rh.X, rh.Y), 3.3f * s, Hex(0x050a0d), 12);
            Circle(F.P(rh.X, rh.Y), 2.6f * s, Hex(0x161f25), 12);
            Circle(F.P(lh.X, lh.Y), 3.3f * s, Hex(0x050a0d), 12);
            Circle(F.P(lh.X, lh.Y), 2.6f * s, Hex(0x161f25), 12);

            // epaulettes
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                V2 pc = F.P(0f, 13f * sgn);
                Circle(pc, 7.4f * s, Hex(0x0a161d), 16);
                Circle(pc, 6.6f * s, Hex(0x1d3340), 16);
                Circle(F.P(-1f, 13f * sgn - 1f), 5f * s, Hex(0x37566a), 14);
                Circle(F.P(-2f, 13f * sgn - 2f), 2.8f * s, Hex(0x5b7e91), 12);
                Circle(F.P(1f, 13f * sgn), 1.7f * s, Hex(0xd2a84c), 8);
            }

            // the peaked cap: crown, band, glossy brim, gold badge
            Circle(pos, 10.6f * s, Hex(0x070e13), 22);
            Circle(pos, 9.6f * s, Hex(0x14212c), 22);
            Circle(F.P(-1f, -1f), 7.4f * s, Hex(0x273d50), 20);
            Circle(F.P(-2.2f, -2.2f), 4.6f * s, Hex(0x46627a), 16);
            Arc(pos, 6.6f * s, 1.3f * s, new Rgba(0.706f, 0.804f, 0.863f, 0.35f), 20);
            V2[] brim = { F.P(7f, -6.8f), F.P(11f, -6.2f), F.P(14.5f, -4.5f), F.P(16.5f, 0f), F.P(14.5f, 4.5f), F.P(11f, 6.2f), F.P(7f, 6.8f) };
            V2 bc = F.P(9f, 0f);
            SetColor(Hex(0x1a2833));
            for (int i = 0; i < brim.Length - 1; i++)
                Tri(bc, brim[i], brim[i + 1]);
            Tri(bc, brim[brim.Length - 1], brim[0]);
            SetColor(Hex(0x080f14));  // the far half of the brim is darker (the mockup's gradient)
            V2 bf = F.P(14f, 0f);
            Tri(bf, brim[2], brim[3]);
            Tri(bf, brim[3], brim[4]);
            Tri(bf, brim[4], brim[5]);
            Tri(bf, brim[1], brim[2]);
            for (int i = 0; i < brim.Length - 1; i++)
                Line(brim[i], brim[i + 1], 1f * s, Hex(0x02060a));
            Line(brim[brim.Length - 1], brim[0], 1f * s, Hex(0x02060a));
            Line(F.P(9f, -4.2f), F.P(12.5f, -3.6f), 1.2f * s, new Rgba(1f, 1f, 1f, 0.35f));
            Line(F.P(12.5f, -3.6f), F.P(15f, -1f), 1.2f * s, new Rgba(1f, 1f, 1f, 0.35f));
            Circle(F.P(0.5f, 0f), 3f * s, Hex(0x7a5d12), 12);
            Circle(F.P(0.5f, 0f), 2.4f * s, Hex(0xe6c25e), 12);

            // a hit flashes the whole uniform white, softer under steady fire (Juice.cs)
            if (e.Flash > 0f || JuiceFlash(e))
                Circle(pos, r, Rgba.White.WithAlpha(0.8f * HitFlashMix(e)), 24);

            DrawSecurityCasing(e, F, t);
        }

        readonly Dictionary<int, SecSeen> secSeen = new Dictionary<int, SecSeen>();
        struct SecSeen { public byte Phase; public float T0, Spin; public V2 From, Dir; }

        /// A brass casing out of each shot: it kicks out to the guard's right and slows, spinning, over 0.7 s.
        void DrawSecurityCasing(Enemy e, SecFrame F, float t)
        {
            SecSeen sn;
            if (!secSeen.TryGetValue(e.Id, out sn))
            {
                if (secSeen.Count > 128)
                    secSeen.Clear();
                sn = new SecSeen { Phase = e.Phase, T0 = -9f };
            }
            if (e.Phase != sn.Phase)
            {
                if (e.Phase < sn.Phase)  // a shot: eject
                {
                    sn.T0 = t;
                    sn.From = F.P(12f, 14f);
                    sn.Dir = F.S * 70f + F.F * 10f;
                    sn.Spin = (e.Id * 37 % 100) / 100f * MathX.Tau;
                }
                sn.Phase = e.Phase;
            }
            secSeen[e.Id] = sn;
            float a = t - sn.T0;
            if (a < 0f || a > 0.7f)
                return;
            float run = (1f - Mathf.Exp(-3.7f * a)) / 3.7f;
            V2 at = sn.From + sn.Dir * run;
            float rot = sn.Spin + 3.2f * (1f - Mathf.Exp(-3.7f * a)), al = Mathf.Clamp01(1f - a / 0.7f);
            V2 u = V2.FromAngle(rot), n = new V2(-u.Y, u.X);
            Quad(at - u * 3f + n * 1.3f, at + u * 3f + n * 1.3f, at + u * 3f - n * 1.3f, at - u * 3f - n * 1.3f, Hex(0xe6c25e, al));
            Quad(at - u * 3f + n * 1.3f, at - u * 1.4f + n * 1.3f, at - u * 1.4f - n * 1.3f, at - u * 3f - n * 1.3f, Hex(0x8f6a1c, al));
        }

        /// Above its head: the rounds left, as little brass rounds on a dark plate.
        void DrawSecurityPips(Enemy e, V2 pos, float r)
        {
            int n = e.Phase;
            const float w = 8f, tot = Game.SecurityMag * w + 10f;
            V2 c = pos + new V2(0f, -r - 22f);
            float x0 = c.X - tot / 2f;
            Rgba plate = new Rgba(0f, 0f, 0f, 0.5f);
            FillRect(x0 + 5f, c.Y - 8f, tot - 10f, 16f, plate);
            FillRect(x0, c.Y - 3f, tot, 6f, plate);
            Circle(new V2(x0 + 5f, c.Y - 3f), 5f, plate, 8);
            Circle(new V2(x0 + tot - 5f, c.Y - 3f), 5f, plate, 8);
            Circle(new V2(x0 + 5f, c.Y + 3f), 5f, plate, 8);
            Circle(new V2(x0 + tot - 5f, c.Y + 3f), 5f, plate, 8);
            for (int i = 0; i < Game.SecurityMag; i++)
            {
                float px = x0 + 9f + i * w;
                if (i < n)
                {
                    FillRect(px - 2f, c.Y - 2f, 4f, 6f, Hex(0xe8b950));
                    Circle(new V2(px, c.Y - 2f), 2f, Hex(0xe8b950), 8);
                    FillRect(px - 2f, c.Y + 1.5f, 4f, 2.5f, Hex(0xb8782a));
                }
                else
                {
                    Rgba dim = new Rgba(1f, 1f, 1f, 0.22f);
                    Line(new V2(px - 2f, c.Y - 2f), new V2(px - 2f, c.Y + 4f), 1f, dim);
                    Line(new V2(px + 2f, c.Y - 2f), new V2(px + 2f, c.Y + 4f), 1f, dim);
                    Line(new V2(px - 2f, c.Y + 4f), new V2(px + 2f, c.Y + 4f), 1f, dim);
                    Circle(new V2(px, c.Y - 2f), 2f, dim.WithAlpha(0.15f), 8);
                }
            }
        }

        /// The tracer in flight (WorldShots.cs): a 48 px tail fading in toward a bright white round, in a pale blue glow.
        void DrawTracerRound(Bullet b, float t)
        {
            V2 u = b.Vel.Length > 0.01f ? b.Vel.Normalized() : V2.FromAngle(0f);
            const int N = 8;
            for (int i = 0; i < N; i++)  // the tail: transparent at the far end, 95% white at the round
            {
                float a0 = (float)i / N, a1 = (float)(i + 1) / N;
                Line(b.Pos - u * (48f * (1f - a0)), b.Pos - u * (48f * (1f - a1)), 2.4f, new Rgba(0.84f + 0.12f * a1, 0.92f + 0.05f * a1, 1f, 0.95f * a1));
            }
            TaserHalo(b.Pos, 16f, new Rgba(0.737f, 0.863f, 1f), 0.5f);
            Line(b.Pos - u * 3.8f, b.Pos + u * 3.8f, 4.4f, Rgba.White);
            Circle(b.Pos - u * 3.8f, 2.2f, Rgba.White, 8);
            Circle(b.Pos + u * 3.8f, 2.2f, Rgba.White, 8);
        }
    }
}
