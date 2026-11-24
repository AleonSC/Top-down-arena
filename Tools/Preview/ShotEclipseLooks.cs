using UnityEngine;

namespace Arena
{
    /// Rework concepts for the Eclipse's body and corona, drawn with the game's own primitives (ECLIPSEMOCK=1-3 in
    /// Shot.cs). 1 Totality: a bright sun the moon slides across, one phase at a time, with long streamers and
    /// prominences. 2 Orrery: neon rings, ticks and orbit rails in the arena's vector look, lit ring by ring.
    /// 3 Black Sun: a crown of flame tongues round a black disc with an eye that watches the nearest player.
    public partial class ArenaApp
    {
        static readonly Rgba[] MockStageCol =
        {
            new Rgba(1f, 0.82f, 0.4f),    // Corona: gold
            new Rgba(0.82f, 0.86f, 0.95f),// Penumbra: silver
            new Rgba(0.61f, 0.48f, 1f),   // Umbra: violet
            new Rgba(0.45f, 0.95f, 0.85f),// Echoes: teal
            new Rgba(1f, 0.54f, 0.24f),   // Last Light: orange
            new Rgba(1f, 0.25f, 0.25f),   // Totality: crimson
        };

        static float MockHash(int k) => Mathf.Abs(Mathf.Sin(k * 12.9898f + 4.1414f) * 43758.547f) % 1f;

        partial void DrawEclipseMock(Enemy e, V2 pos, float r, float t)
        {
            var s = e.Eclipse;
            int stage = s == null ? 0 : (int)s.Stage;
            float swell = s != null && s.FlashT > 0f && s.FlashLen > 0f ? 1f - s.FlashT / s.FlashLen : 0f;
            bool trans = s != null && s.TransT > 0f;
            if (EclipseMock == 1) MockTotality(e, pos, r, t, stage, swell, trans);
            else if (EclipseMock == 2) MockOrrery(e, pos, r, t, stage, swell, trans);
            else MockBlackSun(e, pos, r, t, stage, swell, trans);
        }

        void MockTri(V2 a, V2 b, V2 c, Rgba col)
        {
            SetColor(col);
            Tri(a, b, c);
        }

        // ---- 1 Totality -------------------------------------------------------------------------------------------
        void MockTotality(Enemy e, V2 pos, float r, float t, int stage, float swell, bool trans)
        {
            var sc = MockStageCol[stage];
            float cover = stage / 5f;  // how far the moon has crossed the sun
            GlowDisc(pos, r * (3.2f + swell * 1.5f), stage >= 5 ? sc : SunGold, 0.35f + 0.4f * swell);
            // Streamers: long tapered petals of light, uneven like a real corona, slowly turning.
            int n = 10;
            for (int pass = 0; pass < 2; pass++)
                for (int k = 0; k < n; k++)
                {
                    float a = MathX.Tau * k / n + t * 0.06f + MockHash(k) * 0.5f;
                    float len = r * (0.7f + 1.3f * MockHash(k + 7)) * (1f + 0.12f * Mathf.Sin(t * 1.4f + k * 2.1f)) * (1f + 0.6f * swell) * (0.8f + 0.12f * stage);
                    float w = r * (pass == 0 ? 0.42f : 0.2f);
                    V2 d = V2.FromAngle(a), nn = d.Orthogonal();
                    var c = (stage == 2 ? UmbraViolet : stage >= 5 ? sc : SunWhite).WithAlpha(pass == 0 ? 0.14f : 0.3f);
                    MockTri(pos + d * (r * 0.8f) + nn * w, pos + d * (r * 0.8f) - nn * w, pos + d * (r + len * (pass == 0 ? 1f : 0.75f)), c);
                }
            // The sun: a bright disc.
            Circle(pos, r + 4f, SunGold.WithAlpha(0.5f), 40);
            Circle(pos, r, stage >= 5 ? new Rgba(1f, 0.55f, 0.45f) : SunGold, 40);
            Circle(pos, r * 0.86f, SunWhite, 40);
            // Prominences: small red loops at the rim.
            for (int k = 0; k < 3 + stage; k++)
            {
                float a = MathX.Tau * MockHash(k + 30) + t * 0.1f;
                V2 at = pos + V2.FromAngle(a) * (r + 2f);
                float lr = 6f + 6f * MockHash(k + 40) + 2f * Mathf.Sin(t * 3f + k);
                Arc(at, lr, 3.5f, new Rgba(1f, 0.3f, 0.25f, 0.9f), 10, a - MathX.Pi / 2f, MathX.Pi);
            }
            // The moon, crossing a step further each phase; at Totality it covers the sun exactly.
            V2 dir = V2.FromAngle(-2.3f);
            V2 m = pos + dir * (r * 2.05f * (1f - cover) * (1f - cover * 0.15f));
            Circle(m, r * 0.99f, new Rgba(0.03f, 0.03f, 0.05f), 40);
            Arc(m, r * 0.99f, 1.5f, MoonSilver.WithAlpha(0.25f), 40);
            if (cover > 0.5f)
            {
                // The chromosphere: the thin burning ring left round the moon, and the diamond on the far edge.
                Arc(pos, r, 3f, (stage >= 5 ? sc : SunWhite).WithAlpha(0.5f + 0.5f * cover), 48);
                V2 bead = pos - dir * r;
                float bl = (stage >= 5 ? 26f : 18f) * (1f + 0.25f * Mathf.Sin(t * 7f)) + swell * 30f;
                GlowDisc(bead, 34f, SunWhite, 0.8f);
                Line(bead - new V2(bl, 0f), bead + new V2(bl, 0f), 2.5f, SunWhite);
                Line(bead - new V2(0f, bl), bead + new V2(0f, bl), 2.5f, SunWhite);
                Circle(bead, 6f, Rgba.White, 12);
            }
            if (trans)
                Arc(pos, r + 18f, 4f, Rgba.White.WithAlpha(0.5f + 0.4f * Mathf.Sin(t * 16f)), 40);
        }

        // ---- 2 Orrery ---------------------------------------------------------------------------------------------
        void NeonArc(V2 c, float rr, Rgba col, float start = 0f, float sweep = MathX.Tau)
        {
            Arc(c, rr, 8f, col.WithAlpha(0.14f), 64, start, sweep);
            Arc(c, rr, 2.5f, col, 64, start, sweep);
            Arc(c, rr, 1f, Rgba.White.WithAlpha(0.55f), 64, start, sweep);
        }

        void NeonLine(V2 a, V2 b, Rgba col)
        {
            Line(a, b, 7f, col.WithAlpha(0.14f));
            Line(a, b, 2.5f, col);
            Line(a, b, 1f, Rgba.White.WithAlpha(0.5f));
        }

        void MockOrrery(Enemy e, V2 pos, float r, float t, int stage, float swell, bool trans)
        {
            var sc = MockStageCol[stage];
            GlowDisc(pos, r * 2.6f + swell * 80f, sc, 0.3f + 0.4f * swell);
            // Ring 3 (outermost): the orbit rail, dashed, a bead for each phase reached.
            float r3 = r + 66f;
            for (int k = 0; k < 36; k += 2)
                Arc(pos, r3, 1.5f, sc.WithAlpha(0.45f), 4, MathX.Tau * k / 36f - t * 0.1f, MathX.Tau / 36f);
            for (int k = 0; k <= stage; k++)
            {
                V2 b = pos + V2.FromAngle(t * 0.5f + MathX.Tau * k / 6f) * r3;
                Circle(b, 6f, MockStageCol[k], 12);
                Arc(b, 9f, 1.5f, MockStageCol[k].WithAlpha(0.6f), 12);
            }
            // Ring 2: three arcs with gaps, turning against ring 1.
            float r2 = r + 40f;
            for (int k = 0; k < 3; k++)
                NeonArc(pos, r2, sc, -t * 0.4f + MathX.Tau * k / 3f, MathX.Tau / 3f - 0.5f);
            // Ring 1: a dial of ticks, the long ones every sixth.
            float r1 = r + 16f;
            NeonArc(pos, r1, sc);
            for (int k = 0; k < 24; k++)
            {
                V2 d = V2.FromAngle(t * 0.25f + MathX.Tau * k / 24f);
                bool big = k % 6 == 0;
                Line(pos + d * (r1 + 3f), pos + d * (r1 + (big ? 16f : 8f)), big ? 3f : 2f, (big ? Rgba.White : sc).WithAlpha(0.85f));
            }
            // Flares out of the dial while the sun swells.
            if (swell > 0f)
                for (int k = 0; k < 8; k++)
                {
                    V2 d = V2.FromAngle(MathX.Tau * k / 8f);
                    NeonLine(pos + d * (r2 + 8f), pos + d * (r2 + 8f + 70f * swell), SunWhite);
                }
            // The core: a hatched sun with the moon's disc crossing it, the crescent thinning each phase.
            Circle(pos, r, new Rgba(0.08f, 0.06f, 0.03f), 40);
            for (int k = -5; k <= 5; k++)
            {
                float o = k * r / 5.5f, h = Mathf.Sqrt(Mathf.Max(0f, r * r - o * o)) * 0.95f;
                V2 u = V2.FromAngle(MathX.Pi / 4f), n = u.Orthogonal();
                Line(pos + n * o - u * h, pos + n * o + u * h, 1.2f, SunGold.WithAlpha(0.35f));
            }
            NeonArc(pos, r, SunGold);
            float cover = stage / 5f;
            V2 m = pos + V2.FromAngle(-2.3f) * (r * 1.9f * (1f - cover));
            Circle(m, r * 0.94f, new Rgba(0.02f, 0.02f, 0.04f), 40);
            NeonArc(m, r * 0.94f, MoonSilver.WithAlpha(0.8f));
            if (stage >= 5)
                NeonArc(pos, r + 4f, sc);
            if (trans)
                NeonArc(pos, r + 90f, Rgba.White.WithAlpha(0.5f + 0.4f * Mathf.Sin(t * 16f)));
        }

        // ---- 3 Black Sun ------------------------------------------------------------------------------------------
        void MockBlackSun(Enemy e, V2 pos, float r, float t, int stage, float swell, bool trans)
        {
            var sc = stage == 0 || stage == 1 ? SunGold : MockStageCol[stage];
            GlowDisc(pos, r * 3f + swell * 90f, sc, 0.35f + 0.4f * swell);
            // The crown: flame tongues, long and short in turn, licking; three layers from deep orange to white.
            int n = 16 + 2 * stage;
            for (int layer = 0; layer < 3; layer++)
            {
                Rgba lc = layer == 0 ? (stage >= 5 ? new Rgba(0.7f, 0.05f, 0.1f) : stage == 2 ? new Rgba(0.35f, 0.2f, 0.8f) : FlareOrange).WithAlpha(0.55f)
                        : layer == 1 ? sc : SunWhite;
                float scale = layer == 0 ? 1f : layer == 1 ? 0.62f : 0.22f;
                for (int k = 0; k < n; k++)
                {
                    float a = MathX.Tau * (k + 0.5f * (k % 2)) / n + t * (0.15f + 0.05f * stage);
                    float flick = 0.75f + 0.25f * Mathf.Sin(t * 9f + k * 1.7f) + 0.15f * Mathf.Sin(t * 23f + k * 3.1f);
                    float len = r * (k % 2 == 0 ? 0.75f : 0.42f) * flick * scale * (1f + 0.12f * stage) * (1f + 0.8f * swell);
                    float w = MathX.Tau / n * r * (layer == 0 ? 0.62f : layer == 1 ? 0.42f : 0.22f);
                    V2 d = V2.FromAngle(a), nn = d.Orthogonal();
                    V2 tip = pos + V2.FromAngle(a + 0.18f * Mathf.Sin(t * 5f + k)) * (r + len);
                    MockTri(pos + d * (r * 0.9f) + nn * w, pos + d * (r * 0.9f) - nn * w, tip, lc);
                }
            }
            // Embers circling the crown.
            for (int k = 0; k < 6; k++)
            {
                float a = t * 0.8f + MathX.Tau * k / 6f;
                V2 p = pos + V2.FromAngle(a) * (r + 46f + 6f * Mathf.Sin(t * 3f + k));
                Circle(p, 3.5f, sc, 8);
            }
            // The black disc and its white-hot rim.
            Circle(pos, r * 0.94f, new Rgba(0.02f, 0.01f, 0.03f), 40);
            Arc(pos, r * 0.94f, 3.5f, SunWhite, 48);
            Arc(pos, r * 0.94f - 5f, 1.5f, sc.WithAlpha(0.5f), 48);
            // The eye: watches the nearest player; shut while it changes phase, wide and white as it swells.
            Player near = null;
            float best = 1e9f;
            foreach (var p in game.Players.Values)
                if (p.Disp.DistanceTo(pos) < best) { best = p.Disp.DistanceTo(pos); near = p; }
            V2 look = near != null ? (near.Disp - pos).Normalized() : new V2(0f, 1f);
            V2 c = pos + look * r * 0.22f;
            float open = trans ? 0.08f : 1f;
            float ew = r * 0.5f, eh = r * 0.26f * open * (1f + 0.5f * swell);
            V2 ex = new V2(1f, 0f), ey = new V2(0f, 1f);
            const int es = 16;
            for (int i = 0; i < es; i++)
            {
                float a0 = MathX.Tau * i / es, a1 = MathX.Tau * (i + 1) / es;
                V2 p0 = c + ex * (Mathf.Cos(a0) * ew) + ey * (Mathf.Sin(a0) * eh);
                V2 p1 = c + ex * (Mathf.Cos(a1) * ew) + ey * (Mathf.Sin(a1) * eh);
                MockTri(c, p0, p1, (swell > 0.5f ? SunWhite : sc).WithAlpha(0.95f));
            }
            // The pupil: a slit, red at Totality.
            float ph = eh * 0.9f, pw = r * 0.07f * (1f + swell);
            SetColor(stage >= 5 ? new Rgba(0.6f, 0f, 0.05f) : new Rgba(0.05f, 0.02f, 0.02f));
            Quad(c + new V2(0f, -ph), c + new V2(pw, 0f), c + new V2(0f, ph), c + new V2(-pw, 0f));
            if (trans)
                Arc(pos, r + 18f, 4f, Rgba.White.WithAlpha(0.5f + 0.4f * Mathf.Sin(t * 16f)), 40);
        }
    }
}
