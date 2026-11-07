using UnityEngine;

namespace Arena
{
    /// The Titan (Core/Titan.cs; Docs/Mockups/titan-colossus.html, look C, Runestone): blue-grey carved stone with cyan runes.
    /// Everything is drawn from what the snapshot carries: the minion's age (rising), T (crumbling), Aim (the way it faces),
    /// Wind (its slam, 0..1) and how far it walked since the last frame (its feet). The slam, its standing-up thud and the
    /// rubble it leaves are FxKind.TitanSlam (Pos2.X = 0 / 1 / 2), drawn from the effect's age.
    public partial class ArenaApp
    {
        static readonly Rgba TitanBase = new Rgba(0.44f, 0.5f, 0.55f), TitanLite = new Rgba(0.66f, 0.74f, 0.79f), TitanDark = new Rgba(0.21f, 0.26f, 0.31f),
            TitanGlow = new Rgba(0.43f, 0.89f, 1f), TitanDust = new Rgba(0.6f, 0.62f, 0.6f);
        const float TitanUnit = 2.36f;  // game pixels per drawing unit at Scale 1 (the body is about 175 px across with its fists)

        static float TH(float n) { float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return x - (float)System.Math.Floor(x); }
        static float TEase(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

        /// One lump of carved stone: a shaded, lumpy polygon with a dark rim and, if lit, a glowing crack.
        void TitanStone(V2 c, float r, float seed, int n, float glow, float alpha)
        {
            float rot = TH(seed) * MathX.Tau;
            var pts = new V2[n];
            for (int i = 0; i < n; i++)
                pts[i] = c + V2.FromAngle(rot + MathX.Tau * i / n) * (r * (0.86f + TH(seed + i * 3.1f) * 0.28f));
            SetColor(TitanDark.WithAlpha(alpha));
            for (int i = 0; i < n; i++)
                Tri(c, pts[i], pts[(i + 1) % n]);
            V2 hi = c + new V2(-r * 0.22f, -r * 0.26f);
            SetColor(TitanBase.WithAlpha(alpha));
            for (int i = 0; i < n; i++)
                Tri(hi, V2.Lerp(hi, pts[i], 0.82f), V2.Lerp(hi, pts[(i + 1) % n], 0.82f));
            SetColor(TitanLite.WithAlpha(alpha * 0.8f));
            for (int i = 0; i < n; i++)
                Tri(hi + new V2(-r * 0.06f, -r * 0.06f), V2.Lerp(hi, pts[i], 0.4f), V2.Lerp(hi, pts[(i + 1) % n], 0.4f));
            for (int i = 0; i < n; i++)
                Line(pts[i], pts[(i + 1) % n], Mathf.Max(1.5f, r * 0.09f), TitanDark.WithAlpha(alpha));
            if (glow > 0.02f)
            {
                V2 a = c + new V2(-r * 0.6f, -r * 0.1f), b = c + new V2(-r * 0.15f, r * 0.12f), d = c + new V2(r * 0.1f, -r * 0.3f), e = c + new V2(r * 0.55f, -r * 0.05f);
                float w = Mathf.Max(2f, r * 0.08f);
                Line(a, b, w * 2.4f, TitanGlow.WithAlpha(0.18f * glow * alpha));
                Line(b, d, w * 2.4f, TitanGlow.WithAlpha(0.18f * glow * alpha));
                Line(d, e, w * 2.4f, TitanGlow.WithAlpha(0.18f * glow * alpha));
                Line(a, b, w, TitanGlow.WithAlpha(0.9f * glow * alpha));
                Line(b, d, w, TitanGlow.WithAlpha(0.9f * glow * alpha));
                Line(d, e, w, TitanGlow.WithAlpha(0.9f * glow * alpha));
            }
        }

        /// A rune: one of five strokes, centred on c, r tall, turned by rot.
        void TitanRune(V2 c, float r, float rot, int v, float w, Rgba col)
        {
            V2 X = V2.FromAngle(rot), Y = X.Orthogonal();
            System.Func<float, float, V2> P = (x, y) => c + X * (x * r) + Y * (y * r);
            switch (((v % 5) + 5) % 5)
            {
                case 0: Line(P(0f, -.5f), P(0f, .5f), w, col); Line(P(-.35f, -.2f), P(.35f, -.2f), w, col); break;
                case 1: Line(P(-.35f, -.5f), P(.35f, 0f), w, col); Line(P(.35f, 0f), P(-.35f, .5f), w, col); break;
                case 2: Line(P(-.35f, .5f), P(0f, -.5f), w, col); Line(P(0f, -.5f), P(.35f, .5f), w, col); Line(P(-.2f, .1f), P(.2f, .1f), w, col); break;
                case 3: Arc(c, r * 0.35f, w, col, 10); Line(P(0f, -.55f), P(0f, .55f), w, col); break;
                default: Line(P(-.35f, -.4f), P(.35f, -.4f), w, col); Line(P(0f, -.4f), P(0f, .5f), w, col); Line(P(-.3f, .5f), P(.3f, .5f), w, col); break;
            }
        }

        /// The titan's body, drawn in its own units (facing +X, the footprint about 74 units) scaled by s.
        void TitanBody(V2 pos, float s, float ang, float step, float slam, float glow, float alpha)
        {
            V2 fx = V2.FromAngle(ang), fy = fx.Orthogonal();
            System.Func<float, float, V2> L = (x, y) => pos + (fx * x + fy * y) * s;
            float wk = Mathf.Sin(step);
            float reach, lift;
            if (slam < 0.5f) { reach = lift = TEase(slam / 0.5f); }
            else { reach = 1f - (slam - 0.5f) / 0.5f * 0.6f; lift = Mathf.Max(0f, 1f - (slam - 0.5f) / 0.12f); }
            Circle(pos + new V2(4f, 8f) * s, 62f * s, new Rgba(0f, 0f, 0f, 0.32f * alpha), 28);
            TitanStone(L(-14f + wk * 10f, -24f), 16f * s, 11f, 5, 0f, alpha);
            TitanStone(L(-14f - wk * 10f, 24f), 16f * s, 12f, 5, 0f, alpha);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                V2 sh = L(-8f, sd * 44f);
                V2 rest = new V2(40f + wk * sd * 5f, sd * 68f), up = new V2(76f, sd * 26f);
                V2 fp = L(Mathf.Lerp(rest.X, up.X, reach), Mathf.Lerp(rest.Y, up.Y, reach));
                float fs = 1f + 0.4f * lift;
                Line(sh, fp, 19f * s, TitanDark.WithAlpha(alpha));
                Line(sh, fp, 13f * s, TitanBase.WithAlpha(alpha));
                Line(sh + fy * (-sd * 3f * s), fp + fy * (-sd * 3f * s), 3f * s, TitanLite.WithAlpha(0.5f * alpha));
                if (lift > 0.05f)
                    Circle(fp + new V2(12f, 12f) * (lift * s), 19f * fs * s, new Rgba(0f, 0f, 0f, 0.3f * lift * alpha), 18);
                TitanStone(fp, 18f * fs * s, sd > 0 ? 21f : 22f, 6, glow * (0.35f + 0.65f * lift), alpha);
            }
            TitanStone(L(-8f, 0f), 40f * s, 31f, 9, glow, alpha);
            TitanStone(L(-8f, -44f), 22f * s, 32f, 7, glow * 0.4f, alpha);
            TitanStone(L(-8f, 44f), 22f * s, 33f, 7, glow * 0.4f, alpha);
            TitanRune(L(-12f, 0f), 24f * s, ang, 3, Mathf.Max(2f, 2.6f * s), TitanGlow.WithAlpha((0.25f + 0.6f * glow) * alpha));
            Circle(L(16f, 0f), 17f * s, new Rgba(0f, 0f, 0f, 0.3f * alpha), 14);
            TitanStone(L(24f, 0f), 17f * s, 41f, 6, 0f, alpha);
            Rgba eye = TitanGlow.Mix(Rgba.White, 0.5f);
            float ey = 0.55f + 0.45f * glow;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Circle(L(36f, sd * 7f), 9f * s, TitanGlow.WithAlpha(0.35f * ey * alpha), 12);
                Circle(L(36f, sd * 7f), 3f * s, eye.WithAlpha(ey * alpha), 8);
            }
        }

        /// The ground sigil under a rising or standing titan.
        void TitanSigil(V2 c, float r, float t, float al)
        {
            Circle(c, r, TitanGlow.WithAlpha(0.07f * al), 36);
            for (int i = 0; i < 28; i++)  // dashed ring, turning
            {
                float a0 = MathX.Tau * i / 28f - t * 0.5f;
                Arc(c, r, 3f, TitanGlow.WithAlpha(0.8f * al), 3, a0, MathX.Tau / 56f * 1.5f);
            }
            for (int i = 0; i < 6; i++)
            {
                float a0 = i / 6f * MathX.Tau + t * 0.1f, a1 = (i + 1) / 6f * MathX.Tau + t * 0.1f;
                Line(c + V2.FromAngle(a0) * r * 0.62f, c + V2.FromAngle(a1) * r * 0.62f, 2f, TitanGlow.WithAlpha(0.5f * al));
            }
            for (int i = 0; i < 12; i++)
            {
                float ga = MathX.Tau * i / 12f - t * 0.14f;
                TitanRune(c + V2.FromAngle(ga) * r * 0.9f, r * 0.1f, ga + MathX.Pi / 2f, i, 2f, TitanGlow.WithAlpha(0.75f * al));
            }
        }

        void DrawTitan(Minion m, float t)
        {
            float age = m.Duration > 0f ? m.Duration - m.T : 99f;
            V2 p = m.Disp;
            float sc = m.Scale * TitanUnit;
            float reach = m.Def.Range * m.Scale, ringR = 120f * 2f * m.Scale;
            // feet: the distance it walked since the last frame turns the stride
            titanStep.TryGetValue(m.Id, out V2 last);
            float walked = last.IsZero ? 0f : p.DistanceTo(last);
            float stepNow = (titanStepPhase.TryGetValue(m.Id, out float ph) ? ph : 0f) + walked * 0.055f;
            titanStep[m.Id] = p;
            titanStepPhase[m.Id] = stepNow;

            if (age < Game.TitanRise)  // rubble gathers into a titan
            {
                float k = Mathf.Clamp01(age / Game.TitanRise);
                TitanSigil(p, ringR, t, Mathf.Clamp01(age / 0.25f));
                Circle(p, 120f * m.Scale * k, TitanGlow.WithAlpha(0.12f * k), 24);
                for (int i = 0; i < 12; i++)
                {
                    float dly = TH(i * 5.3f) * 0.4f, ck = Mathf.Clamp01((age - dly) / (Game.TitanRise - dly)), ek = TEase(ck);
                    if (ck <= 0f)
                        continue;
                    float a0 = i / 12f * MathX.Tau + TH(i) * 0.4f, d0 = (300f + TH(i + 9f) * 80f) * m.Scale;
                    float sw = (TH(i + 21f) < 0.5f ? -1f : 1f) * (1.2f + TH(i + 7f));
                    float an = a0 + sw * (1f - ek), dd = Mathf.Lerp(d0, 44f * m.Scale, ek);
                    V2 cp = p + V2.FromAngle(an) * dd + new V2(0f, -Mathf.Sin(ck * MathX.Pi) * 50f);
                    TitanStone(cp, (12f + TH(i + 3f) * 12f) * m.Scale, i * 7.1f, 6, 0f, Mathf.Clamp01(ck * 3f));
                }
                float al = Mathf.Clamp01(k * 1.8f - 0.3f);
                if (al > 0f)
                    TitanBody(p, sc * (0.55f + 0.45f * TEase(k)), m.Aim, 0f, 0f, k * 0.6f, al);
                return;
            }

            float crum = m.T < Game.TitanCrumble ? 1f - Mathf.Clamp01(m.T / Game.TitanCrumble) : 0f;
            if (crum <= 0f)
                TitanSigil(p, ringR, t, 0.55f);
            else
                TitanSigil(p, ringR, t, 0.55f * (1f - crum));
            V2 jit = crum > 0f ? new V2(Mathf.Sin(t * 70f), Mathf.Cos(t * 83f)) * (4f * crum) : V2.Zero;
            float glow = crum > 0f ? (0.7f + 0.3f * Mathf.Sin(t * 40f)) * (1f - crum) : 1f;
            float body = crum > 0f ? 1f - crum * crum : 1f;
            TitanBody(p + jit, sc * (1f - 0.12f * crum), m.Aim, stepNow, m.Wind, glow, body);
            if (crum > 0f)  // chunks start to fly off
                for (int i = 0; i < 14; i++)
                {
                    float a = TH(i * 3.3f) * MathX.Tau, dd = crum * crum * (80f + TH(i + 4f) * 150f) * m.Scale;
                    TitanStone(p + V2.FromAngle(a) * dd, (8f + TH(i + 8f) * 10f) * m.Scale * (1f - crum * 0.5f), i * 4.1f, 5, 0f, 1f - crum);
                }

            if (!Game.TitanBlocking(m))
                return;
            // the soak ring and bar
            float g = Mathf.Clamp01(m.GuardFrac);
            Arc(p, m.Def.BlockRadius * m.Scale, 2f + 2f * g, TitanLite.Mix(TitanGlow, 0.4f).WithAlpha(0.25f + 0.35f * g), 48);
            float bw = 150f * m.Scale;
            Bar(new V2(p.X - bw / 2f, p.Y - m.Def.BlockRadius * m.Scale - 26f), bw, 8f, g, g < 0.25f ? new Rgba(1f, 0.4f, 0.35f) : TitanLite.Mix(TitanGlow, 0.3f));
            // slam telegraph: where the fists will land
            if (m.Wind > 0f && m.Wind < 0.62f)
            {
                float wk = Mathf.Clamp01(m.Wind / 0.615f);
                V2 at = p + V2.FromAngle(m.Aim) * Game.TitanAhead * m.Scale;
                Arc(at, reach, 2f + 3f * wk, TitanGlow.WithAlpha(0.25f + 0.5f * wk), 48);
                Arc(at, reach * (1f - wk), 3f, new Rgba(0.9f, 1f, 1f, 0.5f * wk), 48);
            }
        }

        readonly System.Collections.Generic.Dictionary<int, V2> titanStep = new System.Collections.Generic.Dictionary<int, V2>();
        readonly System.Collections.Generic.Dictionary<int, float> titanStepPhase = new System.Collections.Generic.Dictionary<int, float>();

        /// The Titan's floor marks (cracks, rubble), drawn under everything on the floor (DrawArenaContents).
        void DrawTitanFloor()
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.TitanSlam)
                    DrawTitanFx(fx, true);
        }

        /// FxKind.TitanSlam: Pos2.X 0 = a slam (ring, cracks, chunks), 1 = standing up (bigger), 2 = the rubble it leaves.
        /// floor true: the cracks and rubble (under everything); false: the ring, flying chunks and dust (over everything).
        void DrawTitanFx(Effect fx, bool floor)
        {
            float age = fx.T;
            int kind = (int)(fx.Pos2.X + 0.5f);
            float seed = fx.Pos.X * 0.013f + fx.Pos.Y * 0.029f + kind * 7f;
            if (kind == 2)
            {
                if (floor)
                {
                    float fade = 1f - Mathf.Clamp01((age - 1f) / (fx.Dur - 1f));
                    Circle(fx.Pos, fx.Radius * 1.05f, new Rgba(0f, 0f, 0f, 0.3f * fade), 28);
                    for (int i = 0; i < 26; i++)
                    {
                        float a = TH(seed + i) * MathX.Tau, d = Mathf.Sqrt(TH(seed + i + 31f)) * fx.Radius * 0.95f;
                        float burst = TEase(age / 0.5f);
                        TitanStone(fx.Pos + V2.FromAngle(a) * d * burst, (6f + TH(seed + i + 7f) * 14f), seed + i * 2.9f, 6, 0f, fade);
                    }
                    return;
                }
                if (age < 0.8f)
                    for (int i = 0; i < 10; i++)
                    {
                        float a = TH(seed + i * 1.7f) * MathX.Tau;
                        Circle(fx.Pos + V2.FromAngle(a) * (age * (140f + TH(seed + i) * 120f)), 26f + age * 40f, TitanDust.WithAlpha(0.25f * (1f - age / 0.8f)), 14);
                    }
                return;
            }
            float big = kind == 1 ? 1f : 0f, ra = age / 0.75f;
            if (!floor && ra < 1f)
            {
                float rr = TEase(ra) * fx.Radius;
                Arc(fx.Pos, rr, 14f * (1f - ra) + 2f, new Rgba(1f, 1f, 0.97f, (1f - ra) * (0.85f + 0.15f * big)), 56);
                Arc(fx.Pos, rr * 0.93f, 4f, TitanGlow.WithAlpha((1f - ra) * 0.5f), 56);
                Circle(fx.Pos, rr, TitanDust.WithAlpha(0.12f * (1f - ra)), 40);
                int chunks = kind == 1 ? 16 : 14;
                for (int i = 0; i < chunks; i++)
                {
                    float a = TH(seed + i * 2.3f) * MathX.Tau, d = TEase(age / 0.9f) * (0.3f + TH(seed + i) * 0.7f) * fx.Radius * 1.1f;
                    TitanStone(fx.Pos + V2.FromAngle(a) * d, 6f + TH(seed + i + 5f) * 10f, seed + i * 3.7f, 5, 0f, 1f - age / 0.9f);
                }
            }
            // cracks stay on the floor, hot at first
            if (!floor)
                return;
            int n = kind == 1 ? 12 : 9;
            float fade2 = Mathf.Clamp01(1f - (age - fx.Dur * 0.45f) / (fx.Dur * 0.55f)), grow = Mathf.Clamp01(age / 0.18f), hot = Mathf.Clamp01(1f - age / 1.2f);
            for (int i = 0; i < n; i++)
            {
                float a = seed + i * MathX.Tau / n + (TH(seed + i * 11f) - 0.5f) * 0.4f, L = fx.Radius * (0.55f + TH(seed + i * 5f) * 0.5f);
                V2 prev = fx.Pos;
                int segs = Mathf.Max(1, Mathf.CeilToInt(4 * grow));
                for (int j = 1; j <= segs; j++)
                {
                    a += (TH(seed + i * 7f + j) - 0.5f) * 0.55f;
                    V2 next = prev + V2.FromAngle(a) * (L / 4f);
                    Line(prev, next, 9f, new Rgba(0f, 0f, 0f, 0.7f * fade2));
                    Line(prev, next, 3.5f, TitanDark.Mix(TitanGlow, hot).WithAlpha((0.35f + 0.6f * hot) * fade2));
                    prev = next;
                }
            }
        }
    }
}
