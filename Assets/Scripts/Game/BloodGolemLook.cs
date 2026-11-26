using UnityEngine;

namespace Arena
{
    /// Blood Golem (Core/BloodGolem.cs; Docs/Mockups/blood-golem.html), drawn from the snapshot: Player.GolT (on), GolAge (the gather,
    /// then the golem), GolCoats (its coats) and GolPunch (the punch and its swing).
    ///   - Gather: a sigil under you whose twelve marks light as coats land, and each coat a lumpy ring of blood round your body.
    ///   - Golem: drawn instead of the model: layered strata of blood (oldest and darkest outside), two shoulders, two fists that punch
    ///     in turn and slam together on every third, the Hemomancer dim inside with a beating heart, and twelve marks round it for the coats.
    ///   - FxKind.GolemFx: a coat falling off as chunks, the collapse's burst, and Hurl Clot's clot in flight and its splash.
    public partial class ArenaApp
    {
        static readonly Rgba[] GolCoat =
        {
            new Rgba(0.29f, 0.04f, 0.07f), new Rgba(0.37f, 0.05f, 0.09f), new Rgba(0.43f, 0.06f, 0.11f), new Rgba(0.49f, 0.07f, 0.13f),
            new Rgba(0.56f, 0.08f, 0.15f), new Rgba(0.61f, 0.09f, 0.16f), new Rgba(0.66f, 0.1f, 0.18f), new Rgba(0.7f, 0.11f, 0.19f),
            new Rgba(0.74f, 0.13f, 0.21f), new Rgba(0.78f, 0.15f, 0.23f), new Rgba(0.81f, 0.18f, 0.26f), new Rgba(0.84f, 0.21f, 0.28f),
        };
        static readonly Rgba GolEdge = new Rgba(0.12f, 0.01f, 0.03f), GolPale = new Rgba(1f, 0.72f, 0.76f), GolLit = new Rgba(1f, 0.33f, 0.4f);

        static float GolH(float n) { float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }
        static float GolEase(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

        /// The golem's body radius for this many coats.
        public static float GolRadius(float coats) => 30f + 2.6f * Mathf.Clamp(coats, 1f, Game.GolCoatsMax);

        /// Up as the golem: drawn instead of the player's model.
        static bool GolemStanding(Player p) => Game.GolStanding(p);

        static float GolWob(float a, float seed, float t, float amp) =>
            1f + amp * (0.07f * Mathf.Sin(3f * a + seed) + 0.05f * Mathf.Sin(5f * a + seed * 2.3f + t * 2.2f) + 0.035f * Mathf.Sin(9f * a + seed * 4.1f - t * 1.3f));

        /// A lumpy disc of blood.
        void GolBlob(V2 c, float r, float seed, float t, float amp, int pts, Rgba fill)
        {
            SetColor(fill);
            V2 prev = c + new V2(r * GolWob(0f, seed, t, amp), 0f);
            for (int i = 1; i <= pts; i++)
            {
                float a = MathX.Tau * i / pts;
                V2 next = c + V2.FromAngle(a) * (r * GolWob(a, seed, t, amp));
                Tri(c, prev, next);
                prev = next;
            }
        }

        /// The rim of the same lumpy disc.
        void GolBlobEdge(V2 c, float r, float seed, float t, float amp, int pts, float width, Rgba col)
        {
            V2 prev = c + new V2(r * GolWob(0f, seed, t, amp), 0f);
            for (int i = 1; i <= pts; i++)
            {
                float a = MathX.Tau * i / pts;
                V2 next = c + V2.FromAngle(a) * (r * GolWob(a, seed, t, amp));
                Line(prev, next, width, col);
                prev = next;
            }
        }

        /// The gather: the sigil and the coats piling onto you (drawn over the model).
        void DrawGolemGather(Player p, float modelR, float t, float alpha)
        {
            if (!Game.GolOn(p) || p.GolAge >= Game.GolGather)
                return;
            V2 c = p.Disp;
            float age = p.GolAge, k = Mathf.Clamp01(age / 0.3f) * (1f - Mathf.Clamp01((age - Game.GolGather + 0.35f) / 0.35f));
            int coats = Mathf.Clamp((int)p.GolCoats, 0, Game.GolCoatsMax);
            Arc(c, 46f, 2.5f, GolLit.WithAlpha(0.7f * k * alpha), 40);
            Arc(c, 36f, 1.2f, GolLit.WithAlpha(0.45f * k * alpha), 36);
            for (int i = 0; i < Game.GolCoatsMax; i++)
            {
                float a = -MathX.Pi / 2f + MathX.Tau * i / Game.GolCoatsMax + age * 0.9f;
                V2 d = V2.FromAngle(a), n = d.Orthogonal();
                var col = (i < coats ? GolLit : GolLit.WithAlpha(0.3f)).WithAlpha((i < coats ? 1f : 0.3f) * k * alpha);
                SetColor(col);
                Tri(c + d * 37f + n * 3f, c + d * 45f, c + d * 37f - n * 3f);
            }
            if (!fxOn)
                return;
            float shake = coats > 8 ? Mathf.Sin(t * 60f) * 0.8f : 0f;
            for (int i = 0; i < coats; i++)
                GolBlobEdge(c + new V2(shake, 0f), modelR + 2f + i * 1.9f, i * 1.7f, t * 2f, 1.6f, 22, 3f, GolCoat[Game.GolCoatsMax - 1 - i].WithAlpha(0.9f * alpha));
        }

        /// The golem itself, in place of the model.
        void DrawBloodGolem(Player p, float t, float alpha)
        {
            V2 c = p.Disp;
            float coats = Mathf.Max(p.GolCoats, 0.5f), R = GolRadius(coats), ang = p.Aim;
            float since = p.GolAge - Game.GolGather;
            R *= 0.55f + 0.45f * GolEase(since / 0.45f);  // it swells up as it forms
            if (p.GolT < 1.2f)  // about to burst: it shakes
                c += new V2(Mathf.Sin(t * 70f), Mathf.Cos(t * 83f)) * (3f * (1f - p.GolT / 1.2f));
            V2 fw = V2.FromAngle(ang), sd = fw.Orthogonal();
            GlowDisc(c + new V2(5f, 8f), R * 1.35f, new Rgba(0f, 0f, 0f), 0.5f * alpha);
            if (!fxOn)  // the plain fallback: a disc, a rim and the coat marks
            {
                Circle(c, R, GolCoat[5].WithAlpha(alpha), 32);
                Arc(c, R, 2.5f, GolEdge.WithAlpha(alpha), 32);
                GolemCoatMarks(c, R, p.GolCoats, alpha);
                return;
            }
            // shoulders
            for (int s = -1; s <= 1; s += 2)
            {
                V2 at = c + sd * (s * R * 0.95f) - fw * (R * 0.12f);
                GolBlob(at, R * 0.5f, s * 3f + 1f, t, 1.3f, 20, GolCoat[1].WithAlpha(alpha));
                GolBlobEdge(at, R * 0.5f, s * 3f + 1f, t, 1.3f, 20, 2f, GolEdge.WithAlpha(alpha));
            }
            // the strata: the oldest coat outside and darkest, the freshest in the middle
            int n = Mathf.Max(1, Mathf.CeilToInt(coats - 0.001f));
            for (int i = n - 1; i >= 0; i--)
            {
                float r = R * (0.38f + 0.62f * (i + 1) / n), seed = i * 1.7f + 0.5f;
                var shade = GolCoat[Mathf.RoundToInt((n - 1 - i) / (float)Mathf.Max(n - 1, 1) * (Game.GolCoatsMax - 1))];
                GolBlob(c, r, seed, t * (1f + i * 0.05f), 1.1f, 30, shade.WithAlpha(alpha));
                GolBlobEdge(c, r, seed, t * (1f + i * 0.05f), 1.1f, 30, 1.4f, GolEdge.WithAlpha(0.75f * alpha));
            }
            // the Hemomancer inside, and the heart
            Circle(c, 10f, new Rgba(0.15f, 0.17f, 0.22f, 0.35f * alpha), 20);
            Arc(c, 10f, 2f, new Rgba(0.31f, 0.56f, 0.88f, 0.35f * alpha), 20);
            float beat = 1f + 0.18f * Mathf.Max(0f, Mathf.Sin(t * 9f));
            GlowDisc(c, R * 0.3f * beat, GolPale, 0.4f * alpha);
            Circle(c, R * 0.09f * beat, GolPale.WithAlpha(0.8f * alpha), 12);
            Arc(c, R * 0.82f, 2.5f, GolPale.WithAlpha(0.35f * alpha), 16, MathX.Pi * 1.05f, MathX.Pi * 0.4f);
            // the fists
            int no = Game.GolPunchNo(p);
            float swing = p.GolPunch - no;
            bool slam = Game.GolIsSlam(no) && swing > 0f;
            for (int s = -1; s <= 1; s += 2)
            {
                bool mine = slam || swing > 0f && (no % 2 == 1 ? -1 : 1) == s;
                float ext = !mine ? 0f : swing < 0.2f ? swing / 0.2f : 1f - (swing - 0.2f) / 0.8f, fr = R * 0.36f;
                if (slam)
                    fr *= 1f + 0.25f * (1f - ext);
                float side = slam ? 0.55f : 0.86f;
                V2 at = c + fw * (R * (0.62f + ext * (slam ? 0.55f : 0.95f))) + sd * (s * R * side);
                GolBlob(at, fr, s * 5f + 2f, t, 1.4f, 18, GolCoat[3].WithAlpha(alpha));
                GolBlobEdge(at, fr, s * 5f + 2f, t, 1.4f, 18, 2f, GolEdge.WithAlpha(alpha));
                for (int k = -1; k <= 1; k++)
                    Circle(at + fw * (fr * 0.55f) + sd * (k * fr * 0.45f), fr * 0.16f, GolPale.WithAlpha(0.35f * alpha), 8);
            }
            GolemCoatMarks(c, R, p.GolCoats, alpha);
            if (since < 0.5f)  // the flash as it forms
                GolBlob(c, R * 1.05f, 1f, t, 1f, 30, new Rgba(1f, 0.89f, 0.9f, (1f - since / 0.5f) * alpha));
        }

        /// Twelve marks round the golem, lit for each coat it still has.
        void GolemCoatMarks(V2 c, float R, float coats, float alpha)
        {
            int lit = Mathf.CeilToInt(coats - 0.001f);
            for (int i = 0; i < Game.GolCoatsMax; i++)
            {
                float a0 = -MathX.Pi / 2f + MathX.Tau * i / Game.GolCoatsMax + 0.04f;
                Arc(c, R + 12f, 3f, GolLit.WithAlpha((i < lit ? 0.9f : 0.15f) * alpha), 6, a0, MathX.Tau / Game.GolCoatsMax - 0.08f);
            }
        }

        /// FxKind.GolemFx: a coat falling off, the collapse, or a thrown clot (`f` 0 to 1 over its life).
        void DrawGolemFx(Effect fx, float f)
        {
            float v = fx.T;
            int kind = Mathf.RoundToInt(fx.Radius);
            if (kind == (int)Game.GolFxClot)
            {
                float fl = Game.GolClotFlight;
                if (v < fl)
                {
                    float u = v / fl;
                    V2 g = V2.Lerp(fx.Pos, fx.Pos2, u), up = g + new V2(0f, -Mathf.Sin(u * MathX.Pi) * 90f);
                    GlowDisc(g, 12f, new Rgba(0f, 0f, 0f), 0.35f);
                    float s = 13f * (1f + Mathf.Sin(u * MathX.Pi) * 0.4f);
                    GolBlob(up, s, 3f, v * 4f, 2f, 16, GolCoat[4]);
                    GolBlobEdge(up, s, 3f, v * 4f, 2f, 16, 2f, GolEdge);
                    return;
                }
                float k = (v - fl) / Game.GolClotTail;
                Arc(fx.Pos2, Mathf.Lerp(8f, Game.GolClotRadius, GolEase(k)), 6f * (1f - k) + 1f, GolLit.WithAlpha(1f - k), 32);
                GolChunks(fx.Pos2, 0f, MathX.Tau, 8, 70f, k, 4f, fx.Pos2.X * 0.37f);
                return;
            }
            if (kind == (int)Game.GolFxCollapse)
            {
                int left = Mathf.RoundToInt(fx.Pos2.X);
                float k = Mathf.Clamp01(v / 0.5f), fade = 1f - Mathf.Clamp01((f - 0.6f) / 0.4f);
                if (left > 0)
                {
                    Arc(fx.Pos, Mathf.Lerp(30f, Game.GolBurstRadius, GolEase(k)), 9f * (1f - k) + 1f, GolLit.WithAlpha(1f - k), 48);
                    GolBlob(fx.Pos, Game.GolBurstRadius * 0.55f * GolEase(v / 0.35f), fx.Pos.X * 0.1f, 0f, 2f, 28, new Rgba(0.37f, 0.04f, 0.09f, 0.6f * fade));
                }
                GolChunks(fx.Pos, 0f, MathX.Tau, 10 + left, 70f + 8f * left, Mathf.Clamp01(v / 0.7f), 5f, fx.Pos.Y * 0.21f, fade);
                return;
            }
            // a coat falling off
            V2 dir = fx.Pos2 - fx.Pos;
            float sa = dir.IsZero ? 0f : dir.Angle;
            GolChunks(fx.Pos + V2.FromAngle(sa) * 40f, sa - 0.7f, 1.4f, 4, 60f, Mathf.Clamp01(v / 0.55f), 5f, fx.Pos.X * 0.13f, 1f - Mathf.Clamp01((f - 0.7f) / 0.3f));
        }

        /// Chunks of crust flying out over `span` from `a0`, landing as splats (`k` 0 to 1 of the flight).
        void GolChunks(V2 from, float a0, float span, int n, float dist, float k, float size, float seed, float fade = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                float h = GolH(seed + i * 3.1f), h2 = GolH(seed + i * 7.7f + 1f);
                float a = a0 + span * (span >= MathX.Tau - 0.01f ? i / (float)n : (i + 0.5f) / n) + (h - 0.5f) * 0.4f;
                float d = dist * (0.6f + 0.6f * h2) * GolEase(k);
                V2 at = from + V2.FromAngle(a) * d + new V2(0f, -Mathf.Sin(k * MathX.Pi) * 14f);
                float s = size * (0.7f + 0.6f * h);
                if (k >= 1f)
                    GolBlob(at, s * 1.2f, h * 9f, 0f, 2.2f, 10, new Rgba(0.37f, 0.04f, 0.09f, 0.8f * fade));
                else
                {
                    GolBlob(at, s, h * 9f, k * 6f, 2f, 10, GolCoat[2].WithAlpha(fade));
                    GolBlobEdge(at, s, h * 9f, k * 6f, 2f, 10, 1f, GolEdge.WithAlpha(fade));
                }
            }
        }
    }
}
