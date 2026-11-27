using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Dragon Fang Kunai (Core/DragonFang.cs; mockup claude.ai artifact "Dragon Fang Kunai", look A Fire Serpent):
    ///   - DrawDragonFangShot (WorldShots.DrawNinjaShot): the kunai is the fang in a fire dragon's jaws; the dragon's body traces
    ///     the kunai's real path, so the chain it bit is drawn as the serpent. It grows with each bite (FxKind.DragonFangBite's
    ///     Pos2.Y, the bites so far): longer, wider, hotter; legs from the 4th bite, a fire mane from the 8th, white-hot at the 12th.
    ///   - DrawDragonFangFx (WorldEffects): a bite's flash and snapping jaws; Pos2.Y 0 is the dragon fading in embers.
    /// With Visual effects off: the kunai, a short orange trail and a plain ring a bite.
    public partial class ArenaApp
    {
        class FangTrail
        {
            public readonly List<V2> Path = new List<V2>();
            public int Bites;
            public float F, Snap, Seen;
        }

        readonly Dictionary<int, FangTrail> fangTrails = new Dictionary<int, FangTrail>();
        readonly List<int> fangOld = new List<int>();
        readonly List<V2> fangPts = new List<V2>();

        const float FangStep = 7f;
        const int FangPathMax = 160;

        /// How fierce a dragon with this many bites looks, 0 (just thrown) to 1 (the 12th).
        static float FangFerocity(int bites) => Mathf.Clamp01(bites / (float)Game.DragonFangHits);

        /// This screen's record of a Dragon Fang: its path so far and how many it has bitten (from FxKind.DragonFangBite). Kept up
        /// as it's drawn (Tools/Preview's scene calls it each step too).
        FangTrail TrackFang(Bullet b, float t)
        {
            FangTrail tr;
            if (!fangTrails.TryGetValue(b.Id, out tr))
            {
                if (fangTrails.Count > 6)
                    PruneFangTrails();
                fangTrails[b.Id] = tr = new FangTrail();
            }
            float dt = Mathf.Clamp(t - tr.Seen, 0f, 0.1f);
            tr.Seen = t;
            foreach (var fx in FxOf(FxKind.DragonFangBite))
                if ((int)fx.Pos2.X == b.Id && fx.Pos2.Y > tr.Bites)
                {
                    tr.Bites = (int)fx.Pos2.Y;
                    tr.Snap = 0.18f;
                }
            float goal = FangFerocity(tr.Bites);
            tr.F += (goal - tr.F) * Mathf.Min(1f, dt * 7f);
            tr.Snap = Mathf.Max(0f, tr.Snap - dt);
            var path = tr.Path;
            if (path.Count == 0 || (path[path.Count - 1] - b.Pos).LengthSq > 9f)
            {
                path.Add(b.Pos);
                if (path.Count > FangPathMax)
                    path.RemoveAt(0);
            }
            return tr;
        }

        void DrawDragonFangShot(Bullet b, float t)
        {
            var tr = TrackFang(b, t);
            var path = tr.Path;
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized();
            float f = tr.F;
            if (!fxOn)
            {
                for (int k = path.Count - 1; k > 0 && k > path.Count - 10; k--)
                    Line(path[k], path[k - 1], 3f, Game.DragonFire.WithAlpha(0.5f));
                DrawFangBlade(b.Pos - dir * 6f, dir, 16f, f);
                return;
            }
            FangBody(path, 100f + 380f * f, f, t);
            DrawFangDragon(fangPts, f, t, tr.Snap / 0.18f);
        }

        void PruneFangTrails()
        {
            fangOld.Clear();
            foreach (var id in fangTrails.Keys)
                if (!game.Bullets.ContainsKey(id))
                    fangOld.Add(id);
            foreach (var id in fangOld)
                fangTrails.Remove(id);
        }

        /// The body's points from the head back along the path, FangStep apart, `len` long, smoothed and swaying (into fangPts).
        void FangBody(List<V2> path, float len, float f, float t)
        {
            fangPts.Clear();
            int i = path.Count - 1;
            if (i < 0)
                return;
            V2 p = path[i];
            fangPts.Add(p);
            float acc = 0f, need = FangStep;
            for (i--; i >= 0 && acc < len; i--)
            {
                V2 q = path[i];
                float d = p.DistanceTo(q);
                while (d >= need && acc < len)
                {
                    p += (q - p) * (need / d);
                    d -= need;
                    acc += need;
                    fangPts.Add(p);
                    need = FangStep;
                }
                need -= d;
                acc += d;
                p = q;
            }
            int n = fangPts.Count;
            for (int pass = 0; pass < 3; pass++)
                for (int k = 1; k < n - 1; k++)
                    fangPts[k] = (fangPts[k - 1] + fangPts[k] * 2f + fangPts[k + 1]) * 0.25f;
            for (int k = 1; k < n - 1; k++)  // a little life along it, more toward the tail
            {
                V2 side = (fangPts[k - 1] - fangPts[k + 1]).Normalized().Orthogonal();
                fangPts[k] += side * (Mathf.Sin(k * 0.45f - t * 10f) * (2f + 5f * f) * Mathf.Min(1f, k / 8f));
            }
        }

        static float FangWidth(int i, int n, float w) => w * Mathf.Pow(1f - i / Mathf.Max(1f, n - 1f), 0.72f) * (i < 4 ? 0.78f + 0.055f * i : 1f) + 0.8f;

        /// The dragon along pts (pts[0] the head, at the kunai): glow, fire body, scales, flames, legs, tail, head.
        void DrawFangDragon(List<V2> pts, float f, float t, float snap)
        {
            int n = pts.Count;
            if (n < 2)
            {
                DrawFangHead(pts.Count > 0 ? pts[0] : V2.Zero, new V2(1f, 0f), f, t, snap);
                return;
            }
            float w = 10f + 18f * f;
            var glow = new Rgba(1f, 0.3f - 0.15f * f, 0.05f, 0.07f + 0.05f * f);
            var outer = new Rgba(1f, 0.36f - 0.16f * f, 0.08f, 0.55f);
            var mid = new Rgba(1f, 0.62f + 0.15f * f, 0.18f + 0.1f * f, 0.9f);
            var core = new Rgba(1f, 0.92f + 0.08f * f, 0.6f + 0.35f * f, 0.55f + 0.4f * f);
            for (int i = 0; i < n; i += 2)
                Circle(pts[i], FangWidth(i, n, w) * 1.35f, glow, 12);
            for (int i = 0; i < n - 1; i++)
                Line(pts[i], pts[i + 1], FangWidth(i, n, w) * 1.5f, outer);
            for (int i = 0; i < n - 1; i++)
            {
                float wi = FangWidth(i, n, w);
                Line(pts[i], pts[i + 1], wi, mid);
                Circle(pts[i], wi * 0.5f, mid, 10);
            }
            for (int i = 0; i < n - 1; i++)
                Line(pts[i], pts[i + 1], FangWidth(i, n, w) * 0.42f, core);
            for (int i = 3; i < n - 3; i += 2)  // scales: chevrons down the spine, pointing to the tail
            {
                V2 back = (pts[i + 1] - pts[i - 1]).Normalized(), side = back.Orthogonal();
                float s = FangWidth(i, n, w) * 0.55f, flick = 0.7f + 0.3f * Mathf.Sin(t * 14f + i);
                var sc = new Rgba(0.47f + 0.24f * f, 0.06f + 0.08f * f, 0.03f, 0.75f * flick);
                SetColor(sc);
                Tri(pts[i] - back * (s * 1.2f), pts[i] + side * s + back * (s * 0.2f), pts[i] - back * (s * 0.35f));
                Tri(pts[i] - back * (s * 1.2f), pts[i] - back * (s * 0.35f), pts[i] - side * s + back * (s * 0.2f));
            }
            int licks = 2 + (int)(4f * f);
            for (int i = 2; i < n - 2; i += 3)  // flames licking off both sides, longer as it grows
            {
                V2 back = (pts[i + 1] - pts[i - 1]).Normalized(), side = back.Orthogonal();
                float wi = FangWidth(i, n, w);
                for (int k = 0; k < licks && k < 2; k++)
                {
                    float sgn = k == 0 ? 1f : -1f, fl = 0.5f + 0.5f * Mathf.Sin(t * 17f + i * 1.7f + k * 2.1f);
                    float l = wi * (0.5f + 0.9f * f) * (0.5f + fl);
                    V2 root = pts[i] + side * (sgn * wi * 0.45f);
                    SetColor(new Rgba(1f, 0.5f + 0.35f * fl, 0.15f, 0.45f));
                    Tri(root - back * (wi * 0.3f), root + back * (wi * 0.3f), root + side * (sgn * l) + back * (l * 0.8f));
                }
            }
            int embers = 6 + (int)(26f * f);
            for (int k = 0; k < embers; k++)  // embers peeling off and rising
            {
                float u = Mathf.Repeat(k * 0.618f, 1f), age = Mathf.Repeat(t * (1.2f + 0.3f * (k % 3)) + k * 0.37f, 1f);
                int i = Mathf.Clamp((int)(u * u * (n - 1)), 0, n - 1);
                V2 side = (pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).Normalized().Orthogonal();
                float sgn = (k & 1) == 0 ? 1f : -1f;
                V2 at = pts[i] + side * (sgn * (FangWidth(i, n, w) * 0.6f + 26f * age)) + new V2(0f, -22f * age);
                Circle(at, (1.5f + 2.5f * f) * (1f - 0.6f * age), new Rgba(1f, 0.9f - 0.6f * age, 0.5f * (1f - age), 0.9f * (1f - age)), 6);
            }
            if (f >= 0.3f && n > 14)  // legs from Blaze on: two pairs, paddling
                for (int pair = 0; pair < 2; pair++)
                {
                    int i = (int)(n * (pair == 0 ? 0.22f : 0.55f));
                    if (i <= 0 || i >= n - 1)
                        continue;
                    V2 back = (pts[i + 1] - pts[i - 1]).Normalized(), fwd = back * -1f, side = back.Orthogonal();
                    float len = FangWidth(i, n, w) * (1.3f + 0.5f * f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float sw = Mathf.Sin(t * 12f + i + s) * 0.5f;
                        V2 knee = pts[i] + side * (s * len) + fwd * (len * (0.3f + 0.4f * sw));
                        V2 foot = knee + side * (s * len * 0.5f) + fwd * (len * 0.5f);
                        var lc = new Rgba(1f, 0.6f, 0.2f, 0.85f);
                        Line(pts[i], knee, 2f + 2f * f, lc);
                        Line(knee, foot, 2f + 1.5f * f, lc);
                        for (int c = -1; c <= 1; c++)
                            Line(foot, foot + (fwd + side * (s * 0.6f)) * 5f + side * (c * 3f), 1.4f, lc);
                    }
                }
            {
                V2 back = (pts[n - 1] - pts[n - 2]).Normalized(), side = back.Orthogonal();  // the tail's flame
                float len = 10f + 18f * f;
                for (int k = 0; k < 3; k++)
                {
                    float sway = Mathf.Sin(t * 16f + k * 2f) * 4f, r = 6f - 2f * k;
                    SetColor(new Rgba(1f, (120f + 40f * k) / 255f, 0.16f, 0.4f - 0.08f * k));
                    Tri(pts[n - 1] + side * r, pts[n - 1] - side * r, pts[n - 1] + back * (len * (1f - 0.2f * k)) + side * (sway * 2f));
                }
            }
            DrawFangHead(pts[0], (pts[0] - pts[Mathf.Min(2, n - 1)]).Normalized(), f, t, snap);
        }

        /// The kunai, the fang: steel at first, red-hot past half, white-hot at the end.
        void DrawFangBlade(V2 at, V2 fwd, float len, float f)
        {
            V2 side = fwd.Orthogonal();
            float u = len / 2.2f;
            var steel = new Rgba(0.79f, 0.82f, 0.86f);
            var hot = f > 0.9f ? new Rgba(1f, 0.97f, 0.85f) : f > 0.5f ? new Rgba(1f, 0.69f, 0.38f) : steel;
            SetColor(new Rgba(0.36f, 0.39f, 0.43f));
            Tri(at - fwd * (u * 0.5f) + side * (u * 0.12f), at - fwd * (u * 0.5f) - side * (u * 0.12f), at + fwd * (u * 0.4f) + side * (u * 0.3f));
            Tri(at - fwd * (u * 0.5f) - side * (u * 0.12f), at + fwd * (u * 0.4f) - side * (u * 0.3f), at + fwd * (u * 0.4f) + side * (u * 0.3f));
            SetColor(hot);
            Tri(at + fwd * len, at + fwd * (u * 0.4f) + side * (u * 0.3f), at + fwd * (u * 0.4f) - side * (u * 0.3f));
            Line(at - fwd * (u * 0.4f), at + fwd * len, 1f, Rgba.White.WithAlpha(0.55f));
            if (f > 0.5f)
                Circle(at + fwd * (u * 0.8f), u * 0.55f, new Rgba(1f, 0.47f, 0.16f, 0.25f * f), 12);
        }

        /// The head at the kunai: glow, fire mane, whiskers, the fang, horns, skull, jaws snapping on a bite (snap 1 -> 0), eyes.
        void DrawFangHead(V2 c, V2 fwd, float f, float t, float snap)
        {
            if (fwd.IsZero)
                fwd = new V2(1f, 0f);
            V2 side = fwd.Orthogonal();
            float u = (11f + 12f * f) * (1f + 0.18f * snap);
            System.Func<float, float, V2> P = (x, y) => c + fwd * x + side * y;
            Circle(P(-u * 1.1f, 0f), u * (3.2f + 1.5f * f), new Rgba(1f, 0.45f, 0.12f, 0.1f + 0.12f * f), 20);
            Circle(P(-u * 1.1f, 0f), u * (1.9f + 0.8f * f), new Rgba(1f, 0.55f, 0.16f, 0.12f + 0.12f * f), 18);
            if (f >= 0.5f)  // the fire mane
                for (int k = 0; k < 9; k++)
                {
                    float a = Mathf.PI * (0.62f + 0.76f * k / 8f), fl = u * (1.3f + 1.4f * f) * (0.7f + 0.3f * Mathf.Sin(t * 18f + k * 1.3f));
                    V2 bse = P(-u * 1.4f + Mathf.Cos(a) * u * 0.7f, Mathf.Sin(a) * u * 0.7f);
                    V2 o = fwd * Mathf.Cos(a) + side * Mathf.Sin(a), o2 = fwd * Mathf.Cos(a + 1.4f) + side * Mathf.Sin(a + 1.4f), o3 = fwd * Mathf.Cos(a - 1.4f) + side * Mathf.Sin(a - 1.4f);
                    float g = Mathf.Sin(k + t * 9f);
                    SetColor(new Rgba(1f, (110f + 80f * g * g) / 255f, 0.12f, 0.5f));
                    Tri(bse + o2 * (u * 0.25f), bse + o * fl, bse + o3 * (u * 0.25f));
                }
            for (int s = -1; s <= 1; s += 2)  // whiskers
            {
                float len = u * (3f + 3.5f * f);
                V2 prev = P(u * 0.25f, s * u * 0.3f);
                for (int k = 1; k <= 10; k++)
                {
                    float q = k / 10f;
                    V2 nx = P(u * 0.25f - len * q, s * (u * 0.3f + u * 1.1f * q) + Mathf.Sin(t * 7f + q * 6f + s) * u * 0.35f * q);
                    Line(prev, nx, 1.3f, new Rgba(1f, (190f + 50f * f) / 255f, 0.47f, 0.7f));
                    prev = nx;
                }
            }
            DrawFangBlade(P(-u * 0.5f, 0f), fwd, u * (2.5f + 0.4f * f), f);
            var horn = new Rgba(0.95f, 0.77f, 0.42f);
            for (int s = -1; s <= 1; s += 2)
            {
                float hl = u * (1.5f + 1.6f * f);
                V2 a = P(-u * 1.6f, s * u * 0.45f), m = P(-u * 2.2f, s * u * (0.9f + 0.3f * f)), e = P(-u * 1.6f - hl, s * u * (0.7f + 0.6f * f));
                Line(a, V2.Lerp(a, m, 0.6f), u * 0.22f, horn);
                Line(V2.Lerp(a, m, 0.6f), e, u * 0.16f, horn);
                if (f > 0.3f)
                    Line(P(-u * 2f, s * u * 0.75f), P(-u * 2.3f, s * u * (1.15f + 0.3f * f)), u * 0.13f, horn);
            }
            var skin = new Rgba((110f + 40f * f) / 255f, (16f + 10f * f) / 255f, 0.04f);
            var rim = new Rgba(1f, (190f + 50f * f) / 255f, 0.35f, 0.9f);
            V2 skull = P(-u * 1.35f, 0f);
            Circle(skull, u * 0.8f, rim, 18);
            Circle(skull, u * 0.72f, skin, 18);
            Circle(P(-u * 1.75f, 0f), u * 0.6f, skin, 14);
            float jaw = (0.12f + 0.32f * f) * (1f - 0.85f * snap) + 0.03f;
            for (int s = -1; s <= 1; s += 2)  // the two jaw halves, spread by `jaw`, teeth on the inner edge
            {
                V2 hinge = P(-u * 0.85f, 0f);
                V2 jf = fwd.Rotated(s * jaw * -1f), js = jf.Orthogonal();
                System.Func<float, float, V2> J = (x, y) => hinge + jf * x + js * y;
                SetColor(rim);
                Tri(J(0f, 0f), J(u * 0.35f, s * u * 0.6f), J(u * 1.25f, s * u * 0.44f));
                Tri(J(0f, 0f), J(u * 1.25f, s * u * 0.44f), J(u * 1.72f, s * u * 0.12f));
                SetColor(skin);
                Tri(J(u * 0.05f, 0f), J(u * 0.37f, s * u * 0.52f), J(u * 1.2f, s * u * 0.38f));
                Tri(J(u * 0.05f, 0f), J(u * 1.2f, s * u * 0.38f), J(u * 1.6f, s * u * 0.1f));
                SetColor(new Rgba(1f, 0.96f, 0.86f));
                for (int k = 0; k < 4; k++)
                {
                    float tx = u * (0.35f + k * 0.3f);
                    Tri(J(tx, s * u * 0.05f), J(tx + u * 0.09f, -s * u * 0.12f), J(tx + u * 0.18f, s * u * 0.05f));
                }
            }
            for (int s = -1; s <= 1; s += 2)  // eyes
            {
                V2 e = P(-u * 1.05f, s * u * 0.42f);
                Circle(e, u * (0.28f + 0.12f * f), new Rgba(1f, 0.78f, 0.24f, 0.35f + 0.35f * f), 10);
                Circle(e, u * 0.15f, f > 0.9f ? new Rgba(1f, 0.98f, 0.91f) : new Rgba(1f, 0.88f, 0.54f), 8);
                Line(e - fwd * (u * 0.08f), e + fwd * (u * 0.08f), u * 0.06f, new Rgba(0.09f, 0.02f, 0.02f));
            }
        }

        /// FxKind.DragonFangBite: a bite (Pos2.Y the bites so far): a flash, jaws snapping shut on it, sparks; or (Pos2.Y 0) the
        /// dragon fading where it stopped: a ring of embers and smoke rising.
        void DrawDragonFangFx(Effect fx, float f)
        {
            float k = 1f - f, ease = 1f - k * k;
            int bites = (int)fx.Pos2.Y;
            if (bites <= 0)
            {
                if (!fxOn)
                    return;
                Circle(fx.Pos + new V2(0f, -20f * ease), fx.Radius * (0.6f + 0.6f * ease), new Rgba(0.16f, 0.09f, 0.08f, 0.3f * k), 18);
                for (int i = 0; i < 14; i++)
                {
                    V2 d = V2.FromAngle(i * 2.4f + fx.Pos2.X);
                    V2 at = fx.Pos + d * (fx.Radius * ease) + new V2(0f, -30f * ease);
                    Circle(at, 3f * k + 0.8f, new Rgba(1f, 0.85f - 0.5f * f, 0.3f * k, 0.9f * k), 6);
                }
                return;
            }
            float r = fx.Radius;
            Arc(fx.Pos, 8f + r * 1.4f * ease, 1f + 4f * k, fx.Color.WithAlpha(0.9f * k), 28);
            if (!fxOn)
                return;
            float fe = FangFerocity(bites);
            Circle(fx.Pos, r * (0.9f + 0.4f * fe) * k, new Rgba(1f, 0.85f, 0.5f, 0.35f * k), 18);
            float shut = Mathf.Min(1f, f * 3f);  // jaws close in the first third
            for (int s = -1; s <= 1; s += 2)
            {
                float a0 = -Mathf.PI / 2f * s, open = 0.9f * (1f - shut);
                Arc(fx.Pos, r * 0.9f, 3f + 2f * fe, new Rgba(1f, 0.96f, 0.86f, 0.9f * k), 10, a0 - 0.6f - open * s, 1.2f);
            }
            int n = 8 + bites;
            for (int i = 0; i < n; i++)
            {
                V2 d = V2.FromAngle(i * MathX.Tau / n + bites);
                V2 a = fx.Pos + d * (r * 0.5f + r * 1.6f * ease), b = a + d * (6f + 6f * fe) * k;
                Line(a, b, 1.5f + fe, new Rgba(1f, 0.8f - 0.4f * f, 0.3f * k, k));
            }
        }
    }
}
