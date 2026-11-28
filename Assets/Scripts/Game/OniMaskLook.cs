using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Oni Mask's look (Core/OniMask.cs; from its mockup, the artifact "Oni Mask"):
    ///   - DrawOniUnder (under the model, WorldRenderer.cs): a crimson flame aura licking round the body in three layers, a dark
    ///     red wash on the floor and red afterimages where this screen saw you last; in the last 2 s it sputters.
    ///   - DrawOniMask (over the model, WorldPlayers.cs): the hannya mask on the head, upright and leaning toward the aim (red face, cream horns, gold
    ///     eyes, fangs), dropping on from above over the cast; cracked in the last 2 s. A red ring round you drains with the time
    ///     left, and flashes a gold arc where a kill put time back on.
    ///   - DrawOniMaskFx (FxKind.OniMask): the cast's red shockwave and sparks (OmFxCast), a kill's gold streak from the enemy to
    ///     you (OmFxKill: Pos2 the enemy), the mask's shards flying off at the end (OmFxEnd).
    /// With Visual effects off: no aura, wash, afterimages or glow; the mask and ring still show.
    public partial class ArenaApp
    {
        static readonly Rgba OmHorn = new Rgba(0.95f, 0.9f, 0.77f), OmFace = new Rgba(0.77f, 0.11f, 0.16f), OmShade = new Rgba(0.1f, 0.01f, 0.02f),
            OmFlame = new Rgba(1f, 0.55f, 0.35f);
        const int OmTrailMax = 6;

        class OmView
        {
            public float Len, FlashT = -1f, FlashAt, TrailT;
            public readonly List<V2> Trail = new List<V2>();
        }

        readonly Dictionary<int, OmView> omViews = new Dictionary<int, OmView>();

        OmView OmViewOf(Player p)
        {
            OmView v;
            if (!omViews.TryGetValue(p.Id, out v))
                omViews[p.Id] = v = new OmView { Len = p.OmLen };
            if (p.OmLen > v.Len + 0.01f && v.Len > 0f)
            {
                v.FlashT = 0f;  // a kill put time back on
                v.FlashAt = p.OmT / Mathf.Max(0.01f, p.OmLen);
            }
            v.Len = p.OmLen;
            if (v.FlashT >= 0f)
                v.FlashT += Time.deltaTime;
            return v;
        }

        /// The mask is sputtering out (its last 2 s): 1 lit, lower when it flickers.
        static float OmSputter(Player p, float t) => p.OmT < 2f && Game.OmAge(p) > Game.OmCastT ? (Mathf.Sin(t * 31f) > 0.2f ? 1f : 0.45f) : 1f;

        /// Under the model: the flame aura, the floor wash and the afterimages.
        void DrawOniUnder(Player p, float t)
        {
            if (!Game.OmOn(p))
            {
                omViews.Remove(p.Id);
                return;
            }
            var v = OmViewOf(p);
            if (!fxOn)
                return;
            V2 c = p.Disp;
            float r = p.Def.BodyR, k = Mathf.Clamp01(Game.OmAge(p) / Game.OmCastT), sp = OmSputter(p, t);
            GlowDisc(c, r * 4.2f, Game.OmDeep, 0.35f * k);
            // afterimages: where this screen saw you over the last moments
            v.TrailT += Time.deltaTime;
            if (v.TrailT >= 0.04f)
            {
                v.TrailT = 0f;
                v.Trail.Add(c);
                if (v.Trail.Count > OmTrailMax)
                    v.Trail.RemoveAt(0);
            }
            for (int i = 0; i < v.Trail.Count - 1; i++)
            {
                if (v.Trail[i].CloserThan(c, r * 0.6f))
                    continue;
                float a = 0.28f * (i + 1) / v.Trail.Count * k;
                Circle(v.Trail[i], r * 0.95f, Game.OmRed.WithAlpha(a), 20);
            }
            // the flames: three wobbling rings of tongues, dark red under, orange on top
            for (int layer = 0; layer < 3; layer++)
            {
                Rgba col = (layer == 0 ? Game.OmDeep : layer == 1 ? Game.OmRed : OmFlame).WithAlpha((layer == 0 ? 0.42f : layer == 1 ? 0.3f : 0.16f) * k * sp);
                const int n = 26;
                V2 prev = default(V2);
                for (int i = 0; i <= n; i++)
                {
                    float a = MathX.Tau * i / n;
                    float w = 0.5f * Mathf.Sin(a * 5f + t * (6f + layer * 2f)) + 0.5f * Mathf.Sin(a * 9f - t * 11f);
                    float rr = r + 5f + layer * 4.5f + w * (6f - layer * 1.5f) + (i % 2 == 0 ? 4f : 0f);
                    V2 at = c + V2.FromAngle(a) * rr;
                    if (i > 0)
                        VTri(c, col, prev, col, at, col);
                    prev = at;
                }
            }
        }

        /// Over the model: the mask on the head and the timer ring.
        void DrawOniMask(Player p, float t, float alpha)
        {
            if (!Game.OmOn(p))
                return;
            var v = OmViewOf(p);
            V2 c = p.Disp;
            float r = p.Def.BodyR, age = Game.OmAge(p), k = Mathf.Clamp01(age / Game.OmCastT), e = 1f - (1f - k) * (1f - k);
            // the timer ring
            float left = Mathf.Clamp01(p.OmT / Mathf.Max(0.01f, p.OmLen));
            float ringR = r + 13f;
            Arc(c, ringR, 2.2f, Game.OmRed.WithAlpha(0.18f * alpha), 40);
            Arc(c, ringR, 2.6f, Game.OmRed.WithAlpha(0.9f * alpha * OmSputter(p, t)), 40, -Mathf.PI / 2f, MathX.Tau * left);
            if (v.FlashT >= 0f && v.FlashT < 0.5f)
            {
                float f = 1f - v.FlashT / 0.5f;
                Arc(c, ringR, 4.5f, Game.OmGold.WithAlpha(f * alpha), 12, -Mathf.PI / 2f + MathX.Tau * left - 0.55f, 0.55f);
                if (fxOn)
                    GlowDisc(c + V2.FromAngle(-Mathf.PI / 2f + MathX.Tau * left) * ringR, 9f, Game.OmGold, 0.6f * f);
            }
            // the mask: drops from above and shrinks onto the face over the cast
            float s = r / 17f * (1f + 0.6f * (1f - e));
            V2 at = c + new V2(0f, -55f * (1f - e));
            float lean = 0.3f * Mathf.Cos(p.Aim);  // (upright, so it reads as a face; leaning a little toward the aim)
            DrawOniFace(at, -Mathf.PI / 2f + lean, s, alpha * Mathf.Clamp01(k * 1.6f), t, p.OmT < 2f && age > Game.OmCastT);
        }

        /// The hannya mask, its brow toward `aim` (screen up: -pi/2), `s` its scale (1: 40 px from horn tip to chin), `cracked` near the end.
        void DrawOniFace(V2 at, float aim, float s, float alpha, float t, bool cracked)
        {
            if (alpha <= 0.01f)
                return;
            V2 fw = V2.FromAngle(aim) * s, sd = fw.Orthogonal();
            System.Func<float, float, V2> P = (x, y) => at + sd * x + fw * y;  // x across the face, y up it (toward the aim)
            Rgba horn = OmHorn.WithAlpha(alpha), face = OmFace.WithAlpha(alpha), shade = OmShade.WithAlpha(alpha);
            // horns
            for (int d = -1; d <= 1; d += 2)
            {
                VTri(P(d * 9f, 13f), horn, P(d * 4f, 16f), horn, P(d * 22f, 38f), horn);
                VTri(P(d * 9f, 13f), horn, P(d * 22f, 38f), horn, P(d * 20f, 22f), horn);
                Line(P(d * 9f, 13f), P(d * 22f, 38f), 1f, shade.WithAlpha(0.6f * alpha));
            }
            // the face: a rounded shield, wide at the brow, narrow at the chin
            var outline = new[] { P(0f, 19f), P(10f, 18f), P(17f, 11f), P(19f, 2f), P(17f, -8f), P(11f, -17f), P(0f, -21f), P(-11f, -17f), P(-17f, -8f), P(-19f, 2f), P(-17f, 11f), P(-10f, 18f) };
            V2 mid = P(0f, 0f);
            for (int i = 0; i < outline.Length; i++)
                VTri(mid, face, outline[i], face, outline[(i + 1) % outline.Length], face);
            for (int i = 0; i < outline.Length; i++)
                Line(outline[i], outline[(i + 1) % outline.Length], 1.4f, shade.WithAlpha(0.8f * alpha));
            // brows, scowling down to the nose
            Line(P(-14f, 8f), P(-4f, 3f), 2.6f, shade);
            Line(P(14f, 8f), P(4f, 3f), 2.6f, shade);
            // eyes: gold, glowing
            float glow = 0.75f + 0.25f * Mathf.Sin(t * 8f);
            Rgba eye = Game.OmGold.WithAlpha(alpha * glow);
            if (fxOn)
            {
                GlowDisc(P(-8f, 0f), 7f * s, Game.OmGold, 0.45f * alpha * glow);
                GlowDisc(P(8f, 0f), 7f * s, Game.OmGold, 0.45f * alpha * glow);
            }
            VTri(P(-13f, 2f), eye, P(-4f, 1f), eye, P(-8f, -2.5f), eye);
            VTri(P(13f, 2f), eye, P(4f, 1f), eye, P(8f, -2.5f), eye);
            // the mouth, wide open, and two fangs
            VTri(P(-11f, -9f), shade, P(11f, -9f), shade, P(0f, -15f), shade);
            VTri(P(-8f, -9.5f), horn, P(-4.5f, -9.5f), horn, P(-6.5f, -15f), horn);
            VTri(P(8f, -9.5f), horn, P(4.5f, -9.5f), horn, P(6.5f, -15f), horn);
            if (cracked)
            {
                Line(P(3f, 19f), P(-1f, 8f), 1.3f, shade);
                Line(P(-1f, 8f), P(4f, 0f), 1.3f, shade);
                Line(P(4f, 0f), P(0f, -9f), 1.3f, shade);
            }
        }

        /// FxKind.OniMask: the cast, a kill putting time on, the end.
        void DrawOniMaskFx(Effect fx, float t)
        {
            float k = fx.Dur > 0f ? Mathf.Clamp01(fx.T / fx.Dur) : 1f, fade = 1f - k;
            V2 c = fx.Pos;
            int seed = Mathf.RoundToInt(c.X * 7f + c.Y * 13f);
            switch (Mathf.RoundToInt(fx.Radius))
            {
                case Game.OmFxCast:  // a red shockwave and sparks thrown out
                    Arc(c, 24f + 130f * (1f - fade * fade), 3f * fade + 0.5f, Game.OmRed.WithAlpha(0.85f * fade), 48);
                    Arc(c, 18f + 80f * (1f - fade * fade), 1.5f, Game.OmGold.WithAlpha(0.6f * fade), 40);
                    if (fxOn)
                        GlowDisc(c, 70f * fade + 20f, Game.OmRed, 0.4f * fade);
                    for (int i = 0; i < 16; i++)
                    {
                        float a = MathX.Tau * i / 16f + Hash01(seed + i) * 0.4f, d = (40f + 90f * Hash01(seed * 3 + i)) * (1f - fade * fade);
                        Circle(c + V2.FromAngle(a) * d, 2.2f * fade + 0.4f, (i % 3 == 0 ? Game.OmGold : Game.OmRed).WithAlpha(fade), 6);
                    }
                    break;
                case Game.OmFxKill:  // a gold streak from the enemy back to you
                {
                    V2 from = fx.Pos2, d = c - from;
                    float head = Mathf.Clamp01(k * 2f), tail = Mathf.Clamp01(k * 2f - 0.6f);
                    Line(from + d * tail, from + d * head, 2.4f * fade + 0.6f, fx.Color.WithAlpha(0.9f * fade));
                    if (fxOn)
                        GlowDisc(from + d * head, 10f, fx.Color, 0.6f * fade);
                    Arc(from, 6f + 24f * k, 1.6f, Game.OmRed.WithAlpha(0.7f * fade), 20);
                    break;
                }
                case Game.OmFxEnd:  // the mask's shards flying off
                    for (int i = 0; i < 9; i++)
                    {
                        float a = MathX.Tau * Hash01(seed + i * 5), d = 10f + 70f * (1f - fade * fade);
                        V2 at = c + V2.FromAngle(a) * d, f = V2.FromAngle(a + k * 6f) * 5f;
                        Rgba col = (i % 3 == 0 ? OmHorn : OmFace).WithAlpha(fade);
                        VTri(at + f, col, at - f, col, at + f.Orthogonal() * 0.8f, col);
                    }
                    Arc(c, 20f + 40f * k, 1.5f, Game.OmRed.WithAlpha(0.6f * fade), 32);
                    break;
            }
        }
    }
}
