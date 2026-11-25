using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Frost Lance's look, ported from Docs/Mockups/frost-lance-rework.html (Core/FrostLance.cs has the rules):
    ///   - the lance itself (a frozen haft, a hex guard, a faceted crystal head) drawn in the world over the model's arms, the
    ///     hands placed on its shaft (FlHandPose, from CharacterModels.cs). Its pose comes from the newest FxKind.FrostStep of
    ///     its owner (FlStepPose, from the effect's age); whenever the source changes (a new step, a step ending, a parry
    ///     breaking it) the drawn pose blends from where the lance was (FlBlendIn / FlBlendOut), so the chain flows: each step
    ///     already starts about where the one before ended.
    ///   - each step (DrawFrostStepFx): the frost crescents (Shiver Crescent, Backhand Rime and its rime patches, the Waltz's
    ///     ring), the Crystal Fan's crystals, the Avalanche Slam's swing, crater, spikes and shock rings; flung shards are worked
    ///     out from the age (hash-seeded), so every screen draws the same.
    ///   - FxKind.FrostBurst (a freeze or a shatter on an enemy), FxKind.FrostBreak (a parry breaking the lance),
    ///     FxKind.AbsoluteZero (the rune, the gathering frost, the white wave, the cracks and the shattering ring).
    ///   - the chill pips under enemies (DrawFrostChill), the chain pips and the Rime Counter's glow under the player, and the
    ///     additive glow (DrawFrostLanceGlow, from Juice.cs DrawGlowPass).
    public partial class ArenaApp
    {
        static readonly Rgba FlDeep = new Rgba(0.1f, 0.35f, 0.55f), FlMid = new Rgba(0.37f, 0.78f, 0.95f), FlTip = new Rgba(0.94f, 0.98f, 1f),
            FlEdge = new Rgba(0.024f, 0.1f, 0.17f, 0.9f), FlIce = new Rgba(0.56f, 0.89f, 1f), FlRim = new Rgba(0.84f, 0.96f, 1f),
            FlHaft = new Rgba(0.31f, 0.52f, 0.66f), FlGold = new Rgba(1f, 0.81f, 0.48f);
        const float FlLen = 112f, FlSide = 4f, FlBlendIn = 0.08f, FlBlendOut = 0.24f;

        struct FlPose { public float A, Bx, Len, Ext, Glow; }

        class FlState
        {
            public Effect Src;
            public bool Idle = true;
            public FlPose From, Last;
            public float BlendAt = -9f, BlendDur = 0.1f;
            public int Frame = -1;
            public FlPose Now;
        }
        readonly Dictionary<int, FlState> flStates = new Dictionary<int, FlState>();

        static float FlSeg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        static float FlEo(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        static float FlEio(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; }
        static int FlOwner(Effect fx) => Mathf.RoundToInt(fx.Pos2.Y) / 8;
        static int FlStepOf(Effect fx) => Mathf.RoundToInt(fx.Pos2.Y) % 8;
        static float FlAge(Effect fx) => fx.T + (fx.Color.A < 0.75f ? Game.FlWind[FlStepOf(fx) % 5] : 0f);

        /// The lance's pose `c` into a step, its angle relative to the step's aim.
        static FlPose FlStepPose(int step, float c)
        {
            var m = new FlPose { Bx = -4f, Len = 1f, Glow = 0.6f };
            switch (step)
            {
                case 0:  // Shiver Crescent: sweep left to right, then hold out on the right (Backhand starts there)
                    m.A = Mathf.Lerp(-1.45f, 1.4f, FlEio(FlSeg(c, 0f, 0.18f)));
                    m.Ext = 52f * (1f - FlSeg(c, 0.18f, 0.32f)) * FlSeg(c, 0f, 0.04f);
                    break;
                case 1:  // Backhand Rime: back across, ending out on the left (the Waltz winds from there)
                    m.A = Mathf.Lerp(1.42f, -1.55f, FlEio(FlSeg(c, 0f, 0.17f)));
                    m.Ext = 48f * (1f - FlSeg(c, 0.17f, 0.3f));
                    break;
                case 2:  // Frost Waltz: one and a quarter turns, ending pointed down the aim (the Fan stabs from there)
                    m.A = -1.55f + FlEio(FlSeg(c, 0f, 0.34f)) * (1.55f + MathX.Tau);
                    m.Ext = 46f * (1f - FlSeg(c, 0.3f, 0.42f));
                    break;
                case 3:  // Crystal Fan: drive the point into the floor ahead, hold, draw it back
                {
                    float stab = FlEo(FlSeg(c, 0f, 0.08f)) * (1f - FlEio(FlSeg(c, 0.4f, 0.66f)));
                    m.A = 0f;
                    m.Bx = Mathf.Lerp(-6f, 16f, stab);
                    m.Len = 1f - 0.22f * stab;  // tipped down into the floor: a little shorter seen from above
                    m.Glow = 0.6f + 0.6f * stab;
                    break;
                }
                default:  // Avalanche Slam: swing back over the shoulder, over the top, down into the floor, lift it out
                {
                    if (c < 0.1f)
                    {
                        m.A = Mathf.Lerp(0f, -2.6f, FlEo(c / 0.1f));
                        m.Len = 1f - 0.15f * FlEo(c / 0.1f);
                    }
                    else if (c < 0.25f)
                    {
                        float k = FlEio(FlSeg(c, 0.1f, 0.25f));
                        m.A = Mathf.Lerp(-2.6f, 0f, k);
                        m.Len = 0.85f - 0.35f * Mathf.Sin(Mathf.PI * k) + 0.15f * k;  // raised high overhead: shortest at the top
                        m.Glow = 0.8f + k;
                    }
                    else
                    {
                        float lift = FlEio(FlSeg(c, 0.62f, 1.0f));
                        m.A = 0f;
                        m.Bx = Mathf.Lerp(20f, -10f, lift);
                        m.Len = Mathf.Lerp(0.92f, 1f, lift);
                        m.Glow = Mathf.Lerp(1.2f, 0.5f, lift);
                    }
                    break;
                }
            }
            return m;
        }

        FlPose FlIdlePose(Player p, float t) => new FlPose { A = p.Aim + 0.28f + 0.02f * Mathf.Sin(t * 2.4f), Bx = -18f, Len = 1f, Glow = 0.35f };

        static float FlAngleLerp(float a, float b, float k) => a + (Mathf.Repeat(b - a + MathX.Pi, MathX.Tau) - MathX.Pi) * k;

        /// The newest step or break effect of player `id` (null: none playing).
        Effect FlNewest(int id)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
            {
                bool mine = fx.Kind == FxKind.FrostStep ? FlOwner(fx) == id : fx.Kind == FxKind.FrostBreak && Mathf.RoundToInt(fx.Pos2.Y) == id;
                if (mine && fx.T < fx.Dur && (best == null || fx.T < best.T))
                    best = fx;
            }
            return best;
        }

        /// This frame's lance pose for `p` (world angle), with the blending between sources.
        FlPose FlPoseOf(Player p, float t)
        {
            FlState st;
            if (!flStates.TryGetValue(p.Id, out st))
                flStates[p.Id] = st = new FlState();
            if (st.Frame == Time.frameCount)
                return st.Now;
            st.Frame = Time.frameCount;
            var src = FlNewest(p.Id);
            FlPose target;
            bool idle = true;
            float zeroAge = Game.ZeroDur - p.FlZeroT;
            if (src != null && src.Kind == FxKind.FrostStep && FlAge(src) < Game.FlDur[FlStepOf(src) % 5])
            {
                target = FlStepPose(FlStepOf(src) % 5, FlAge(src));
                target.A += src.Pos2.X;
                idle = false;
            }
            else if (p.FlZeroT > 0f && zeroAge < 0.95f)
            {
                float up = FlEo(zeroAge / 0.25f) * (1f - FlEio(FlSeg(zeroAge, 0.7f, 0.95f)));
                target = new FlPose { A = p.Aim, Bx = Mathf.Lerp(-18f, -8f, up), Len = 1f - 0.45f * up, Glow = 0.5f + 1.2f * up };  // held up to the sky
                idle = false;
            }
            else
            {
                target = FlIdlePose(p, t);
                if (src != null && src.Kind == FxKind.FrostBreak)
                    target.Len = FlEo(FlSeg(src.T, 0.12f, 0.4f));  // a new lance grows back after the parry broke it
            }
            if (src != st.Src || idle != st.Idle)
            {
                st.From = st.BlendAt > -9f ? st.Last : target;
                st.BlendAt = t;
                st.BlendDur = idle ? FlBlendOut : FlBlendIn;
                if (src != null && src.Kind == FxKind.FrostBreak)
                    st.BlendDur = 0.01f;
                st.Src = src;
                st.Idle = idle;
            }
            float u = Mathf.Clamp01((t - st.BlendAt) / st.BlendDur);
            u = u * u * (3f - 2f * u);
            var m = new FlPose
            {
                A = FlAngleLerp(st.From.A, target.A, u), Bx = Mathf.Lerp(st.From.Bx, target.Bx, u), Len = Mathf.Lerp(st.From.Len, target.Len, u),
                Ext = Mathf.Lerp(st.From.Ext, target.Ext, u), Glow = Mathf.Lerp(st.From.Glow, target.Glow, u),
            };
            if (u >= 1f)
                m = target;
            st.Last = m;
            st.Now = m;
            return m;
        }

        /// The model's hands on the lance's shaft (model frame: x along the aim, y to the right).
        void FlHandPose(Player p, ref float rhx, ref float rhy, ref float lhx, ref float lhy)
        {
            var m = FlPoseOf(p, Time.time);
            float ra = m.A - p.Aim, c = Mathf.Cos(ra), s = Mathf.Sin(ra);
            float xr = m.Bx + 30f * m.Len, xl = m.Bx + 13f * m.Len;
            rhx = xr * c - FlSide * s; rhy = xr * s + FlSide * c;
            lhx = xl * c - FlSide * s; lhy = xl * s + FlSide * c;
        }

        /// From the player loop: the lance held (over the model's arms), the gloves on it, the chain pips.
        void DrawFrostLanceHeld(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.FrostLance || p.Downed)
                return;
            var m = FlPoseOf(p, t);
            V2 d = V2.FromAngle(m.A), n = d.Orthogonal(), o = p.Disp + n * FlSide;
            FlLance(o, d, m.Bx, FlLen * m.Len, m.Ext, 1f, t);
            foreach (float x in new[] { m.Bx + 13f * m.Len, m.Bx + 30f * m.Len })
            {
                V2 h = o + d * x;
                Circle(h, 5f, FlEdge, 12);
                Circle(h, 4.2f, new Rgba(0.62f, 0.72f, 0.8f), 12);
                Circle(h - new V2(1.1f, 1.4f), 1.3f, new Rgba(1f, 1f, 1f, 0.45f), 6);
            }
            DrawFlPips(p, t);
        }

        // --- pieces ---------------------------------------------------------------------------------------------

        /// A faceted ice crystal from x0 to x1 along `d` from `o`: deep blue at its root, white at its point, a lit upper half,
        /// a dark outline and a bright ridge.
        void FlCrystal(V2 o, V2 d, float x0, float x1, float w, float a)
        {
            float L = x1 - x0;
            if (L < 1f || a <= 0.01f)
                return;
            V2 n = d.Orthogonal();
            V2 p0 = o + d * x0, p3 = o + d * x1, m1 = o + d * (x0 + L * 0.16f), m2 = o + d * (x0 + L * 0.68f);
            V2 p1 = m1 + n * w, p2 = m2 + n * (w * 0.72f), p4 = m2 - n * (w * 0.72f), p5 = m1 - n * w;
            Rgba c0 = FlDeep.WithAlpha(a), c1 = FlDeep.Mix(FlMid, 0.4f).WithAlpha(a), c2 = FlMid.Mix(FlTip, 0.35f).WithAlpha(a), c3 = FlTip.WithAlpha(a);
            // outline first, a hair bigger
            V2 q0 = p0 - d * 1.2f, q3 = p3 + d * 1.6f, q1 = p1 + n * 1.2f, q2 = p2 + n * 1.2f, q4 = p4 - n * 1.2f, q5 = p5 - n * 1.2f;
            Rgba e = FlEdge.WithAlpha(FlEdge.A * a);
            VTri(q0, e, q1, e, q5, e); VTri(q1, e, q2, e, q4, e); VTri(q1, e, q4, e, q5, e); VTri(q2, e, q3, e, q4, e);
            VTri(p0, c0, p1, c1, p5, c1); VTri(p1, c1, p2, c2, p4, c2); VTri(p1, c1, p4, c2, p5, c1); VTri(p2, c2, p3, c3, p4, c2);
            Rgba hi = new Rgba(0.88f, 0.98f, 1f, 0.32f * a);
            SetColor(hi);
            Tri(p0, p1, m1); Tri(m1, p1, p2); Tri(m1, p2, m2); Tri(m2, p2, p3);
            Line(o + d * (x0 + L * 0.06f), p3 - d * 1.5f, 1f, new Rgba(1f, 1f, 1f, 0.8f * a));
        }

        /// The lance: haft from bx along `d` (len long in all), banded with frost, a crystal pommel, a hex guard, the crystal
        /// head, and `ext` px of frost sleeve shooting past its point.
        void FlLance(V2 o, V2 d, float bx, float len, float ext, float a, float t)
        {
            if (len < 4f || a <= 0.01f)
                return;
            V2 n = d.Orthogonal();
            float sh = len * 0.42f;
            V2 b = o + d * bx, g = o + d * (bx + sh);
            Line(b + n * 2f + new V2(2f, 3f), g + new V2(2f, 3f) + d * (len - sh), 4f, new Rgba(0f, 0.03f, 0.06f, 0.28f * a));  // shadow
            Line(b, g, 6.5f, FlEdge.WithAlpha(a));
            Line(b, g, 3.4f, FlHaft.WithAlpha(a));
            Line(b - n * 0.9f, g - n * 0.9f, 1f, new Rgba(0.8f, 0.95f, 1f, 0.65f * a));
            for (int i = 1; i < 4; i++)
            {
                V2 m = o + d * (bx + sh * i / 4f);
                Line(m - n * 2.4f, m + d * 3f + n * 2.4f, 1f, new Rgba(0.7f, 0.92f, 1f, 0.5f * a));
            }
            FlDiamond(b - d * 2f, d, 5f, 3.6f, new Rgba(0.75f, 0.94f, 1f, a), a);
            // the hex guard
            V2 g0 = g - d * 2f, g1 = g + d * 4f + n * 8.5f, g2 = g + d * 10f + n * 3f, g3 = g + d * 7f, g4 = g + d * 10f - n * 3f, g5 = g + d * 4f - n * 8.5f;
            SetColor(FlEdge.WithAlpha(a));
            Tri(g0 - d, g1 + n, g3); Tri(g1 + n, g2 + d, g3); Tri(g0 - d, g3, g5 - n); Tri(g5 - n, g3, g4 + d);
            SetColor(new Rgba(0.78f, 0.95f, 1f, a));
            Tri(g0, g1, g3); Tri(g1, g2, g3); Tri(g0, g3, g5); Tri(g5, g3, g4);
            FlCrystal(o, d, bx + sh + 5f, bx + len, 6.4f, a);
            if (ext > 2f)
                FlCrystal(o, d, bx + len - 12f, bx + len + ext, 5f, a * Mathf.Clamp01(ext / 20f) * 0.95f);
        }

        void FlDiamond(V2 c, V2 d, float l, float w, Rgba col, float a)
        {
            V2 n = d.Orthogonal();
            SetColor(FlEdge.WithAlpha(a));
            Tri(c - d * (l + 1f), c + n * (w + 1f), c + d * (l + 1f)); Tri(c - d * (l + 1f), c - n * (w + 1f), c + d * (l + 1f));
            SetColor(col);
            Tri(c - d * l, c + n * w, c + d * l); Tri(c - d * l, c - n * w, c + d * l);
        }

        /// A soft ellipse (shadows), along `ax`.
        void FlEllipse(V2 c, float rx, float ry, float ang, Rgba col)
        {
            V2.Axes(ang, out V2 ax, out V2 ay);
            SetColor(col);
            const int N = 14;
            for (int i = 0; i < N; i++)
            {
                float a0 = MathX.Tau * i / N, a1 = MathX.Tau * (i + 1) / N;
                Tri(c, c + ax * (Mathf.Cos(a0) * rx) + ay * (Mathf.Sin(a0) * ry), c + ax * (Mathf.Cos(a1) * rx) + ay * (Mathf.Sin(a1) * ry));
            }
        }

        /// A frost band between radii r0 and r1 round `c`, swept from a0 to a1: brightest and thickest at its leading edge, with
        /// a white rim and two thinner inner rims.
        void FlFrostArc(V2 c, float r0, float r1, float a0, float a1, float al)
        {
            if (al <= 0.01f || Mathf.Abs(a1 - a0) < 0.02f)
                return;
            const int N = 30;
            for (int i = 0; i < N; i++)
            {
                float u0 = i / (float)N, u1 = (i + 1) / (float)N, b0 = Mathf.Lerp(a0, a1, u0), b1 = Mathf.Lerp(a0, a1, u1);
                float ri0 = Mathf.Lerp(r1 - 5f, r0, u0 * u0), ri1 = Mathf.Lerp(r1 - 5f, r0, u1 * u1);
                V2 d0 = V2.FromAngle(b0), d1 = V2.FromAngle(b1);
                Rgba co0 = FlIce.WithAlpha(al * 0.42f * u0 * u0), co1 = FlIce.WithAlpha(al * 0.42f * u1 * u1);
                Rgba ci0 = FlIce.WithAlpha(al * 0.12f * u0 * u0), ci1 = FlIce.WithAlpha(al * 0.12f * u1 * u1);
                VQuad(c + d0 * r1, co0, c + d1 * r1, co1, c + d1 * ri1, ci1, c + d0 * ri0, ci0);
            }
            float span = a1 - a0, s30 = a0 + span * 0.3f, s55 = a0 + span * 0.55f, s75 = a0 + span * 0.75f;
            Arc(c, r1, 2.6f, FlRim.WithAlpha(al * 0.95f), 26, s30, a1 - s30);
            Arc(c, r1 - 8f, 1.3f, FlIce.WithAlpha(al * 0.6f), 20, s55, a1 - s55);
            Arc(c, Mathf.Lerp(r0, r1, 0.55f), 1f, FlIce.WithAlpha(al * 0.35f), 14, s75, a1 - s75);
        }

        /// A cluster of crystals bursting up from the floor, seen from above.
        void FlSpike(V2 at, float h, float ang, float a, int seed)
        {
            if (h <= 0.5f || a <= 0.01f)
                return;
            FlEllipse(at + new V2(3f, 4f), h * 0.6f, h * 0.32f, ang, new Rgba(0f, 0.03f, 0.06f, 0.35f * a));
            float s1 = 0.75f + (seed % 3) * 0.08f, s2 = -0.68f - (seed % 5) * 0.05f;
            FlCrystal(at, V2.FromAngle(ang + s1), -h * 0.11f, h * 0.6f, h * 0.144f + 1.5f, a);
            FlCrystal(at, V2.FromAngle(ang + s2), -h * 0.09f, h * 0.52f, h * 0.125f + 1.5f, a);
            FlCrystal(at, V2.FromAngle(ang), -h * 0.18f, h, h * 0.24f + 1.5f, a);
        }

        /// Rime on the floor: a pale patch with a few cracks.
        void FlRime(V2 c, float r, float a, float seed)
        {
            if (a <= 0.01f)
                return;
            FanGradient(c, r, new Rgba(0.8f, 0.95f, 1f, 0.26f * a), new Rgba(0.47f, 0.78f, 0.94f, 0f), 16);
            float cs = Mathf.Min(r, 50f) * 0.28f;
            for (int i = 0; i < 6; i++)
            {
                float an = seed + i * 1.05f;
                V2 p = c;
                for (int j = 0; j < 3; j++)
                {
                    float aa = an + Mathf.Sin(seed * 7f + i * 3f + j) * 0.5f;
                    V2 q = p + V2.FromAngle(aa) * cs;
                    Line(p, q, 1f, new Rgba(0.86f, 0.97f, 1f, 0.3f * a));
                    p = q;
                }
            }
        }

        /// Shards flung from `at`: n of them, each leaving at its own angle and speed and slowing, spinning, fading.
        void FlShards(V2 at, float age, int n, float speed, float seed, float size, float dir = 0f, float spread = MathX.Tau)
        {
            if (age < 0f || fxReduced && n > 6)
                n = fxReduced ? Mathf.Min(n, 6) : n;
            if (age < 0f)
                return;
            for (int k = 0; k < n; k++)
            {
                float life = 0.35f + 0.5f * WcHash(seed + k * 1.7f);
                if (age > life)
                    continue;
                float a = dir + (WcHash(seed + k * 2.9f) - 0.5f) * spread, v = speed * (0.35f + 0.65f * WcHash(seed + k * 4.1f));
                float dist = v / 3.2f * (1f - Mathf.Exp(-3.2f * age)), f = 1f - age / life, s = size * (0.5f + 0.75f * WcHash(seed + k * 5.3f));
                V2 p = at + V2.FromAngle(a) * dist;
                FlDiamond(p, V2.FromAngle(WcHash(seed + k) * MathX.Tau + age * 12f * (WcHash(seed + k * 6.1f) - 0.5f)), s, s * 0.42f,
                    (age / life < 0.3f ? new Rgba(1f, 1f, 1f) : new Rgba(0.75f, 0.94f, 1f)).WithAlpha(f), f * 0.6f);
            }
        }

        void FlRune(V2 c, float r, float a, float rot)
        {
            if (a <= 0.01f)
                return;
            Arc(c, r, 1.5f, FlIce.WithAlpha(a), 40);
            Arc(c, r * 0.8f, 1.5f, FlIce.WithAlpha(a), 36);
            for (int i = 0; i < 6; i++)
            {
                V2.Axes(rot + i * MathX.Tau / 6f, out V2 d, out V2 n);
                Line(c + d * (r * 0.15f), c + d * (r * 0.78f), 1.4f, FlIce.WithAlpha(a));
                V2 b1 = c + d * (r * 0.42f), b2 = c + d * (r * 0.6f);
                Line(b1, b1 + d * (r * 0.16f) + n * (r * 0.13f), 1.2f, FlIce.WithAlpha(a)); Line(b1, b1 + d * (r * 0.16f) - n * (r * 0.13f), 1.2f, FlIce.WithAlpha(a));
                Line(b2, b2 + d * (r * 0.1f) + n * (r * 0.08f), 1.2f, FlIce.WithAlpha(a)); Line(b2, b2 + d * (r * 0.1f) - n * (r * 0.08f), 1.2f, FlIce.WithAlpha(a));
                Circle(c + d * (r * 0.9f), 2.2f, FlRim.WithAlpha(a), 8);
            }
        }

        // --- the effects ----------------------------------------------------------------------------------------

        /// One chain step from its effect's age.
        void DrawFrostStepFx(Effect fx)
        {
            int id = FlOwner(fx), step = FlStepOf(fx) % 5;
            float c = FlAge(fx), aim = fx.Pos2.X, s = fx.Radius;
            Player owner;
            game.Players.TryGetValue(id, out owner);
            // A parry broke this step: its ground marks stay, its sweep stops.
            bool broken = false;
            foreach (var o in FxOf(FxKind.FrostBreak))
                if (o.Kind == FxKind.FrostBreak && Mathf.RoundToInt(o.Pos2.Y) == id && o.T < fx.T)
                    broken = true;
            V2 body = owner != null && c < 0.5f ? owner.Disp : fx.Pos, g = fx.Pos, dir = V2.FromAngle(aim);
            float seed = fx.Pos.X * 0.37f + fx.Pos.Y * 0.71f + step;
            switch (step)
            {
                case 0:
                {
                    if (broken && c > 0.12f)
                        break;
                    float a = Mathf.Lerp(-1.45f, 1.4f, FlEio(FlSeg(c, 0f, 0.18f)));
                    FlFrostArc(body, 26f * s, FlArcOuter(0, s), aim - 1.45f, aim + a, 1f - FlSeg(c, 0.2f, 0.52f));
                    FlSweepShards(body, aim, -1.45f, 1.4f, c, 0.18f, s, seed, 1f);
                    break;
                }
                case 1:
                {
                    float a = Mathf.Lerp(1.42f, -1.55f, FlEio(FlSeg(c, 0f, 0.17f)));
                    if (c > 0.1f)
                        for (int i = 0; i < 9; i++)
                        {
                            float b = -1.35f + i * 0.33f, rr = (110f + 40f * WcHash(seed + i)) * s;
                            FlRime(g + V2.FromAngle(aim + b) * rr, (18f + 10f * WcHash(seed + i * 3f)) * s, Mathf.Clamp01((c - 0.1f) / 0.08f) * (1f - FlSeg(c, 1.6f, 2.4f)), seed + i);
                        }
                    if (broken && c > 0.12f)
                        break;
                    float fa = 1f - FlSeg(c, 0.2f, 0.54f);
                    FlFrostArc(body, 30f * s, FlArcOuter(1, s), aim + 1.42f, aim + a, fa);
                    FlFrostArc(body, 26f * s, FlArcOuter(1, s) * 0.82f, aim + 1.42f, aim + Mathf.Lerp(1.42f, a, 0.92f), fa * 0.6f);
                    FlSweepShards(body, aim, 1.42f, -1.55f, c, 0.17f, s, seed, -1f);
                    break;
                }
                case 2:
                {
                    if (broken && c > 0.2f)
                        break;
                    float k = FlEio(FlSeg(c, 0f, 0.34f)), a = -1.55f + k * (1.55f + MathX.Tau), from = Mathf.Max(-1.55f, a - MathX.Tau * 0.96f);
                    FlFrostArc(body, 40f * s, Game.FlSpinR * s, aim + from, aim + a, 1f - FlSeg(c, 0.34f, 0.6f));
                    float w = FlSeg(c, 0.3f, 0.56f);
                    if (w > 0f && w < 1f)
                        Arc(body, (Game.FlSpinR + w * 40f) * s, 2f, FlRim.WithAlpha((1f - w) * 0.8f), 48);
                    for (int i = 0; i < 16; i++)
                        FlShards(body + V2.FromAngle(aim + i * MathX.Tau / 16f) * (160f * s), c - 0.32f, 2, 220f, seed + i * 9f, 4f, aim + i * MathX.Tau / 16f, 0.3f);
                    break;
                }
                case 3:
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float st = 0.06f + Mathf.Abs(i - 2) * 0.03f, L = FlEo(FlSeg(c, st, st + 0.07f)) * 155f * s, fa = 1f - FlSeg(c, 0.42f, 0.5f);
                        V2 rd = V2.FromAngle(aim + Game.FlRays[i]);
                        if (L > 2f && fa > 0f)
                        {
                            FlEllipse(g + rd * (40f * s + L * 0.55f) + new V2(2f, 5f), L * 0.5f, 6f, aim + Game.FlRays[i], new Rgba(0f, 0.03f, 0.06f, 0.3f * fa));
                            FlCrystal(g, rd, 40f * s, 40f * s + L, 9f, fa);
                        }
                        for (float r = 60f; r < 190f; r += 26f)
                            FlShards(g + rd * (r * s), c - 0.44f, 2, 90f, seed + i * 31f + r, 3f);
                    }
                    FlShards(g + dir * 36f, c - 0.08f, 8, 120f, seed + 77f, 3f);
                    break;
                }
                default:
                {
                    V2 at = g + dir * (Game.FlSlamAt * s);
                    if (c >= 0.25f)  // the crater, the rime round it, the cracks: they linger
                    {
                        float la = 1f - FlSeg(c, 1.8f, 2.8f);
                        FlRime(at, 120f * s, la, seed);
                        FanGradient(at, 44f * s, new Rgba(0.01f, 0.04f, 0.07f, 0.4f * la), new Rgba(0.01f, 0.04f, 0.07f, 0.1f * la), 18);
                        Arc(at, 44f * s, 1.5f, FlRim.WithAlpha(0.4f * la), 28);
                        for (int i = 0; i < 9; i++)
                        {
                            float an = seed + i * 0.7f;
                            V2 p0 = at + V2.FromAngle(an) * (40f * s), p1 = at + V2.FromAngle(an + 0.12f) * (62f * s), p2 = at + V2.FromAngle(an - 0.05f) * (78f * s);
                            Line(p0, p1, 1.3f, FlRim.WithAlpha(0.45f * la)); Line(p1, p2, 1.1f, FlRim.WithAlpha(0.35f * la));
                        }
                    }
                    if (broken && c < 0.25f)
                        break;
                    if (c < 0.26f && c > 0.1f)
                        FlFrostArc(body, 50f * s, 150f * s, aim - 2.6f, aim + Mathf.Lerp(-2.6f, 0f, FlEio(FlSeg(c, 0.1f, 0.25f))), 0.9f);
                    float sw = FlSeg(c, 0.24f, 0.5f);
                    if (sw > 0f && sw < 1f)
                    {
                        Arc(at, (20f + sw * 110f) * s, 4f, FlRim.WithAlpha(1f - sw), 48);
                        Arc(at, (10f + sw * 80f) * s, 2f, FlIce.WithAlpha((1f - sw) * 0.6f), 40);
                    }
                    float cr = 1f - FlEio(FlSeg(c, 0.66f, 0.8f));
                    for (int i = 0; i < 9; i++)
                    {
                        float b = i * MathX.Tau / 9f + 0.3f, h = FlEo(FlSeg(c, 0.25f + i * 0.006f, 0.31f + i * 0.006f)) * (26f + (i % 3) * 5f) * cr * s;
                        FlSpike(at + V2.FromAngle(b) * (52f * s), h, b, 1f, i);
                    }
                    FlShards(at, c - 0.25f, 26, 330f, seed + 5f, 5f);
                    for (int i = 0; i < 9; i++)
                        FlShards(at + V2.FromAngle(i * MathX.Tau / 9f + 0.3f) * (55f * s), c - 0.68f, 3, 110f, seed + i * 13f, 4f);
                    break;
                }
            }
        }

        static float FlArcOuter(int step, float s) => (step == 0 ? Game.FlArcR : Game.FlBackR) * s;

        /// Glittering shards shed tangentially off the lance's tip as it sweeps.
        void FlSweepShards(V2 c, float aim, float a0, float a1, float age, float sweepT, float s, float seed, float sense)
        {
            for (int k = 0; k < 16; k++)
            {
                float born = WcHash(seed + k * 1.3f) * sweepT, tau = age - born;
                if (tau < 0f || tau > 0.6f)
                    continue;
                float a = aim + Mathf.Lerp(a0, a1, FlEio(born / sweepT)), r = (100f + 75f * WcHash(seed + k * 2.2f)) * s;
                FlShards(c + V2.FromAngle(a) * r, tau, 1, 170f, seed + k * 7.7f, 3f, a + sense * MathX.Pi / 2f, 0.9f);
            }
        }

        /// A freeze (Radius 0) or a shatter (1) on an enemy.
        void DrawFrostBurstFx(Effect fx)
        {
            int kind = Mathf.RoundToInt(fx.Radius);
            float c = fx.T, seed = fx.Pos.X * 0.53f + fx.Pos.Y * 0.29f;
            if (kind == 0)
            {
                FlRime(fx.Pos, 32f, Mathf.Clamp01(c / 0.08f) * (1f - FlSeg(c, 1.6f, 2.4f)), seed);
                float k = FlSeg(c, 0f, 0.3f);
                if (k < 1f)
                    Arc(fx.Pos, 14f + k * 24f, 2f, FlRim.WithAlpha(1f - k), 28);
                FlShards(fx.Pos, c, 10, 140f, seed, 3f);
            }
            else if (kind == 1)
            {
                FlRime(fx.Pos, 40f, Mathf.Clamp01(c / 0.05f) * (1f - FlSeg(c, 0.5f, 0.9f)), seed);
                float k = FlSeg(c, 0f, 0.4f);
                if (k < 1f)
                {
                    Arc(fx.Pos, 14f + k * 60f, 3f, new Rgba(1f, 1f, 1f, 1f - k), 32);
                    Arc(fx.Pos, 10f + k * 40f, 1.5f, FlIce.WithAlpha((1f - k) * 0.7f), 28);
                }
                FlShards(fx.Pos, c, 26, 380f, seed, 6f);
            }
        }

        /// A parry breaking the lance: shards burst off along where it was.
        void DrawFrostBreakFx(Effect fx)
        {
            float aim = fx.Pos2.X, seed = fx.Pos.X * 0.41f + fx.Pos.Y * 0.67f;
            Player owner;
            V2 o = game.Players.TryGetValue(Mathf.RoundToInt(fx.Pos2.Y), out owner) ? owner.Disp : fx.Pos;
            for (int i = 0; i < 9; i++)
                FlShards(o + V2.FromAngle(aim) * (20f + i * 12f), fx.T, 2, 170f, seed + i * 5f, 3.5f);
            float k = FlSeg(fx.T, 0f, 0.3f);
            if (k < 1f)
                Arc(o + V2.FromAngle(aim) * 60f, 10f + k * 30f, 2f, FlRim.WithAlpha(1f - k), 24);
        }

        /// Absolute Zero from its effect's age: the rune drawing itself, frost gathering, the wave, the cracks, the shattering ring.
        void DrawAbsoluteZeroFx(Effect fx)
        {
            float c = fx.T, R = fx.Radius, seed = fx.Pos.X * 0.19f + fx.Pos.Y * 0.43f;
            V2 o = fx.Pos;
            float ch = FlEo(FlSeg(c, 0f, 0.66f)), runeA = ch * (1f - FlSeg(c, 0.8f, 1.2f));
            float gr = 1f - FlSeg(c, 1.4f, 1.95f);
            if (c > 0.78f)
                FanGradient(o, R, new Rgba(0.8f, 0.95f, 1f, 0.22f * gr), new Rgba(0.6f, 0.86f, 1f, 0.04f * gr), 40);
            FlRune(o, 30f + ch * 60f, runeA, c * 1.4f);
            FlRune(o, (30f + ch * 60f) * 0.55f, runeA * 0.6f, -c * 2f);
            if (c < 0.7f && !fxReduced)  // frost motes gathering in
                for (int k = 0; k < 36; k++)
                {
                    float born = WcHash(seed + k) * 0.5f, tau = (c - born) / 0.35f;
                    if (tau < 0f || tau > 1f)
                        continue;
                    V2 from = o + V2.FromAngle(WcHash(seed + k * 3.1f) * MathX.Tau) * (80f + 160f * WcHash(seed + k * 5.7f));
                    V2 at = from + (o - from) * (tau * tau);
                    Circle(at, 1.8f, FlRim.WithAlpha(0.9f * Mathf.Sin(Mathf.PI * tau)), 6);
                }
            float w = FlSeg(c, 0.7f, 1.08f);
            if (w > 0f && w < 1f)
            {
                float r = FlEo(w) * R;
                for (int i = 0; i < 56; i++)
                {
                    V2 d0 = V2.FromAngle(i * MathX.Tau / 56f), d1 = V2.FromAngle((i + 1) * MathX.Tau / 56f);
                    float r0 = Mathf.Max(0f, r - 40f);
                    VQuad(o + d0 * r0, FlIce.WithAlpha(0f), o + d1 * r0, FlIce.WithAlpha(0f), o + d1 * r, FlRim.WithAlpha(0.5f * (1f - w)), o + d0 * r, FlRim.WithAlpha(0.5f * (1f - w)));
                }
                Arc(o, r, 3f, new Rgba(1f, 1f, 1f, 1f - w * 0.6f), 64);
            }
            float f = 1f - FlSeg(c, 1.2f, 1.7f), grow = FlSeg(c, 0.78f, 1f);
            if (c > 0.78f && f > 0f)  // frost cracks racing out across the floor
                for (int i = 0; i < 14; i++)
                {
                    float a = i * MathX.Tau / 14f + 0.2f, lim = (R - 10f) * grow;
                    V2 prev = o + V2.FromAngle(a) * 40f;
                    for (float r = 64f; r < lim; r += 24f)
                    {
                        V2 q = o + V2.FromAngle(a + Mathf.Sin(r * 0.07f + i) * 0.12f) * r;
                        Line(prev, q, 1.2f, FlRim.WithAlpha(0.6f * f));
                        prev = q;
                    }
                }
            float sk = FlSeg(c, 1.5f, 1.85f);
            if (sk > 0f && sk < 1f)
            {
                Arc(o, 60f + sk * (R - 40f), 2.4f, new Rgba(1f, 1f, 1f, 1f - sk), 64);
                for (int i = 0; i < 18; i++)
                    FlShards(o + V2.FromAngle(i * MathX.Tau / 18f) * (R * 0.6f), c - 1.5f, 2, 200f, seed + i * 3f, 4f, i * MathX.Tau / 18f, 0.6f);
            }
        }

        /// Chill pips under an enemy (not when it's frozen: the ice block says that).
        void DrawFrostChill(Enemy e, V2 pos, float r)
        {
            if (e.FrostChill == 0 || e.Frozen)
                return;
            Circle(pos, r, FlIce.WithAlpha(0.14f * e.FrostChill), 18);
            for (int i = 0; i < Game.FlFreezeAt; i++)
                FlDiamond(pos + new V2(-10f + i * 10f, r + 9f), new V2(1f, 0f), 4f, 3f,
                    i < e.FrostChill ? FlIce : new Rgba(0.16f, 0.27f, 0.35f, 0.9f), 1f);
        }

        /// The chain's progress under the player (lit to the step just played while the chain is live), and the Rime Counter.
        void DrawFlPips(Player p, float t)
        {
            Effect last = null;
            foreach (var fx in FxOf(FxKind.FrostStep))
                if (fx.Kind == FxKind.FrostStep && FlOwner(fx) == p.Id && (last == null || fx.T < last.T))
                    last = fx;
            int lit = last != null && last.T < Game.FlGap[FlStepOf(last) % 5] + Game.FlComboReset ? FlStepOf(last) % 5 + 1 : 0;
            if (lit >= 5 && last.T > Game.FlDur[4])
                lit = 0;
            if (lit > 0 || p.FlRime)
                for (int i = 0; i < 5; i++)
                {
                    V2 at = p.Disp + new V2((i - 2f) * 11f, p.Def.BodyR + 16f);
                    Rgba col = i < lit ? (i == 4 ? FlGold : FlIce) : new Rgba(0.12f, 0.22f, 0.29f, 0.9f);
                    FlDiamond(at, new V2(1f, 0f), 4.4f, 3.4f, col, 1f);
                }
            if (p.FlRime)
                Arc(p.Disp, p.Def.BodyR + 6f + Mathf.Sin(t * 12f), 2f, FlGold.WithAlpha(0.6f + 0.3f * Mathf.Sin(t * 12f)), 32);
        }

        /// The additive glow: the lance's head, the sweeps' tips, the slam and fan flashes, freezes, shatters, Absolute Zero.
        void DrawFrostLanceGlow()
        {
            float t = Time.time;
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.FrostLance && !p.Downed && !LaunchHides(p))
                {
                    var m = FlPoseOf(p, t);
                    V2 d = V2.FromAngle(m.A), o = p.Disp + d.Orthogonal() * FlSide;
                    GlowDisc(o + d * (m.Bx + FlLen * m.Len - 9f + m.Ext), 26f + m.Ext * 0.3f, FlIce, 0.3f * m.Glow);
                    if (p.FlRime)
                        GlowDisc(p.Disp, 44f, FlGold, 0.35f + 0.15f * Mathf.Sin(t * 12f));
                    if (p.FlZeroT > 0f && Game.ZeroDur - p.FlZeroT < 0.78f)
                        GlowDisc(p.Disp, 34f + 40f * FlEo((Game.ZeroDur - p.FlZeroT) / 0.66f), FlRim, 0.6f);
                }
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.FrostStep)
                {
                    int step = FlStepOf(fx) % 5;
                    float c = FlAge(fx), s = fx.Radius;
                    V2 dir = V2.FromAngle(fx.Pos2.X);
                    if (step == 3 && c < 0.4f)
                        GlowDisc(fx.Pos + dir * 40f, 34f, FlRim, 0.5f * (1f - c / 0.4f));
                    else if (step == 4 && c > 0.24f && c < 0.6f)
                        GlowDisc(fx.Pos + dir * (Game.FlSlamAt * s), 90f * s, FlRim, 0.6f * (1f - FlSeg(c, 0.24f, 0.6f)));
                }
                else if (fx.Kind == FxKind.FrostBurst)
                {
                    int kind = Mathf.RoundToInt(fx.Radius);
                    if (kind == 1 && fx.T < 0.4f)
                        GlowDisc(fx.Pos, 48f, FlRim, 0.7f * (1f - fx.T / 0.4f));
                    else if (kind == 0 && fx.T < 0.4f)
                        GlowDisc(fx.Pos, 34f, FlIce, 0.6f * (1f - fx.T / 0.4f));
                    else if (kind == 2 && fx.T < 0.5f)
                        GlowDisc(fx.Pos, 240f, FlRim, 0.5f * (1f - fx.T / 0.5f));
                }
                else if (fx.Kind == FxKind.AbsoluteZero)
                {
                    float sk = FlSeg(fx.T, 1.5f, 1.85f);
                    if (sk > 0f && sk < 1f)
                        GlowDisc(fx.Pos, 200f, FlRim, 0.5f * (1f - sk));
                }
            }
        }
    }
}
