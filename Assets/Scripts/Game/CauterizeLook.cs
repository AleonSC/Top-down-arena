using UnityEngine;

namespace Arena
{
    /// Cauterize's look (FxKind.Cauterize: Pos = the enemy, Pos2 = (its id, the burst's damage), Radius = its size;
    /// Core/Cauterize.cs), drawing only. It follows the enemy while it lives:
    ///   - a white-hot flash on the wound and a searing ring snapping out, orange cooling to red;
    ///   - the blood boiling off: red drops flung out that brighten to embers as they go;
    ///   - a glowing seared brand across the body, cooling from white through orange to nothing;
    ///   - steam hissing up off it in soft grey puffs.
    /// It grows a little with the burst's damage. With Visual effects off: one white ring.
    public partial class ArenaApp
    {
        static readonly Rgba SearWhite = new Rgba(1f, 0.96f, 0.86f), SearOrange = new Rgba(1f, 0.58f, 0.18f),
            SearRed = new Rgba(0.8f, 0.14f, 0.1f), SearSteam = new Rgba(0.9f, 0.9f, 0.92f);

        /// White, then orange, then a dull red as it cools (k: 0 to 1).
        static Rgba SearHeat(float k) => k < 0.4f ? SearWhite.Mix(SearOrange, k / 0.4f) : SearOrange.Mix(SearRed, (k - 0.4f) / 0.6f);

        void DrawCauterizeFx(Effect fx)
        {
            V2 at = game.Enemies.TryGetValue((int)fx.Pos2.X, out Enemy e) ? e.Disp : fx.Pos;
            float t = fx.T, life = Mathf.Clamp01(t / fx.Dur), r = Mathf.Max(8f, fx.Radius);
            float size = Mathf.Clamp(0.75f + fx.Pos2.Y / 120f, 0.75f, 1.4f), seed = fx.Pos.X * 0.11f + fx.Pos.Y * 0.07f;
            if (!fxOn)
            {
                Arc(at, r + 6f + 14f * life, 2f, SearWhite.WithAlpha(1f - life), 20);
                return;
            }

            // The flash: white-hot on the wound.
            if (t < 0.14f)
            {
                float k = t / 0.14f;
                GlowDisc(at, (r + 26f) * size, SearWhite, 0.9f * (1f - k));
                Circle(at, r * (0.6f + 0.5f * k), SearWhite.WithAlpha(0.85f * (1f - k)), 20);
            }
            GlowDisc(at, (r + 14f) * size, SearOrange, 0.45f * (1f - life) * (1f - life));

            // The searing ring.
            float ring = Mathf.Clamp01(t / 0.32f);
            if (ring < 1f)
            {
                float k = EaseOut3(ring);
                Arc(at, r + 4f + 22f * size * k, 3.2f * (1f - ring) + 0.6f, SearHeat(ring).WithAlpha(1f - ring), 28);
            }

            // The brand: an X seared across it, cooling.
            float cool = Mathf.Clamp01(t / (fx.Dur * 0.9f));
            if (cool < 1f)
            {
                var col = SearHeat(cool).WithAlpha(1f - cool * cool);
                float s = r * 0.55f;
                Line(at + new V2(-s, -s), at + new V2(s, s), 3.4f * (1f - 0.5f * cool), col);
                Line(at + new V2(-s, s), at + new V2(s, -s), 3.4f * (1f - 0.5f * cool), col);
                if (cool < 0.35f)
                    Circle(at, 2.6f, SearWhite.WithAlpha(1f - cool / 0.35f), 8);
            }

            // The blood boiling off, turning to embers.
            float boil = Mathf.Clamp01(t / 0.5f);
            if (boil < 1f)
                for (int i = 0; i < 7; i++)
                {
                    float an = i / 7f * MathX.Tau + VenomHash(seed + i) * 0.7f;
                    float sp = (24f + 22f * VenomHash(seed + i * 2.3f)) * size;
                    V2 p = at + V2.FromAngle(an) * (r * 0.6f + sp * EaseOut3(boil)) + new V2(0f, -14f * boil);
                    Circle(p, 2.4f * (1f - 0.6f * boil), SearRed.Mix(SearOrange, boil).WithAlpha(1f - boil), 8);
                }

            // Steam hissing up.
            for (int i = 0; i < 5; i++)
            {
                float k = Mathf.Clamp01((t - i * 0.05f) / (fx.Dur * 0.85f));
                if (k <= 0f || k >= 1f)
                    continue;
                float sway = Mathf.Sin(k * 5f + i * 1.7f) * 5f;
                V2 p = at + new V2((VenomHash(seed + i * 4.1f) - 0.5f) * r * 1.4f + sway, -r * 0.3f - 34f * size * EaseOut3(k));
                GlowDisc(p, 7f + 12f * k, SearSteam, 0.5f * Mathf.Sin(Mathf.PI * k));
            }
        }
    }
}
