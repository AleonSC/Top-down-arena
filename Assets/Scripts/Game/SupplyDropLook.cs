using UnityEngine;

namespace Arena
{
    /// Supply Drop (Core/SupplyDrop.cs; mockup https://claude.ai/artifact/1ck4pSWwzcDfzphP6APDyi), all worked out from the zone's
    /// replicated timer: the call-in (a red ring that pulses over where it lands, red flare smoke, the crate's shadow growing as it
    /// falls under its chute), the landing (a gold shockwave and dust), the crate's four sides falling open, then the green zone
    /// (a soft wash, a bright rim, a slow dashed inner ring, a pulse rolling out, fire and cooldown chevrons round it and a time
    /// arc) and a green ring under every player it buffs. It folds away over the last `SupplyFold` s. The health packs are
    /// ordinary medkit orbs.
    public partial class ArenaApp
    {
        static readonly Rgba SupplyRed = new Rgba(1f, 0.35f, 0.4f), SupplyGreen = new Rgba(0.4f, 1f, 0.55f), SupplyGold = new Rgba(1f, 0.8f, 0.35f);
        static readonly Rgba SupplyOlive = new Rgba(0.42f, 0.48f, 0.23f), SupplyOliveDark = new Rgba(0.29f, 0.33f, 0.15f), SupplyStrap = new Rgba(0.17f, 0.2f, 0.09f);

        void DrawSupplyDrop(Zone z, float t)
        {
            float age = Game.SupplyAge(z);
            V2 c = z.Pos;
            if (age < Game.SupplyCallIn)
            {
                float f = age / Game.SupplyCallIn;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 14f);
                Circle(c, Game.SupplyBlast, SupplyRed.WithAlpha(0.08f + 0.05f * f), 40);
                Arc(c, Game.SupplyBlast, 2f, SupplyRed.WithAlpha(0.45f + 0.45f * pulse), 48);
                Arc(c, Game.SupplyBlast * (1f - f) + 6f, 1.5f, SupplyRed.WithAlpha(0.5f * f), 32);  // closing in on the spot
                if (!fxReduced)
                    for (int i = 0; i < 7; i++)  // flare smoke drifting up
                    {
                        float k = MathX.Fmod(t * 0.7f + i / 7f, 1f);
                        Circle(c + new V2(Mathf.Sin(t * 2f + i * 1.7f) * 8f * k, -8f - k * 60f), 5f + 9f * k, SupplyRed.WithAlpha(0.18f * (1f - k)), 14);
                    }
                Circle(c + new V2(3f, 3f), 4f, new Rgba(0f, 0f, 0f, 0.3f), 10);
                Line(c + new V2(0f, 4f), c + new V2(0f, -6f), 4f, SupplyRed);  // the flare
                Circle(c + new V2(0f, -7f), 2.5f + pulse, new Rgba(1f, 0.85f, 0.8f, 0.9f), 10);
                // the crate's shadow grows as it comes down, the crate and chute over it
                Circle(c, 6f + 24f * f, new Rgba(0f, 0f, 0f, 0.15f + 0.3f * f), 24);
                float fall = (1f - f) * (1f - f) * 520f;
                V2 at = c + new V2(0f, -fall);
                float s = 0.6f + 0.4f * f;
                Line(at + new V2(-18f, -14f) * s, at + new V2(0f, -64f) * s, 1f, new Rgba(0.75f, 0.85f, 1f, 0.5f));
                Line(at + new V2(18f, -14f) * s, at + new V2(0f, -64f) * s, 1f, new Rgba(0.75f, 0.85f, 1f, 0.5f));
                Arc(at + new V2(0f, -68f) * s, 30f * s, 12f * s, new Rgba(0.75f, 0.85f, 1f, 0.35f), 20, MathX.Pi, MathX.Pi);  // the chute's canopy
                DrawSupplyCrate(at, s, 0f, 1f);
                return;
            }

            float since = age - Game.SupplyCallIn;
            if (since < 0.5f)  // the landing: a gold shockwave and dust
            {
                float k = since / 0.5f;
                Arc(c, 30f + k * (Game.SupplyBlast + 20f), 2f + 6f * (1f - k), SupplyGold.WithAlpha(1f - k), 48);
                if (!fxReduced)
                    for (int i = 0; i < 16; i++)
                    {
                        float a = MathX.Tau * i / 16f + i * 0.37f;
                        Circle(c + V2.FromAngle(a) * (20f + k * (50f + 30f * Mathf.Sin(i * 2.1f))), 4f * (1f - k) + 1f, new Rgba(0.73f, 0.66f, 0.48f, 0.7f * (1f - k)), 8);
                    }
            }
            float open = MathX.Clamp01((since) / Game.SupplyOpen);
            open = 1f - (1f - open) * (1f - open);
            float fade = MathX.Clamp01(z.T / Game.SupplyFold);
            float R = z.Radius * open * (0.6f + 0.4f * fade);

            if (R > 4f)
            {
                float da = fade;
                Circle(c, R, SupplyGreen.WithAlpha(0.05f * da), 56);
                Circle(c, R * 0.55f, SupplyGreen.WithAlpha(0.05f * da), 40);
                Arc(c, R, 2.5f, SupplyGreen.WithAlpha(0.85f * da), 64);
                if (R > 14f)
                {
                    const int dashes = 24;  // a slow dashed inner ring
                    for (int i = 0; i < dashes; i++)
                        Arc(c, R - 10f, 1.2f, SupplyGreen.WithAlpha(0.45f * da), 3, t * 0.4f + MathX.Tau * i / dashes, MathX.Tau / dashes * 0.5f);
                }
                if (!fxReduced)
                {
                    float pr = MathX.Fmod(t * 0.8f, 1f) * R;  // a pulse rolling out
                    if (pr > 2f)
                        Arc(c, pr, 1.5f, SupplyGreen.WithAlpha(0.5f * (1f - pr / R) * da), 48);
                    for (int i = 0; i < 6; i++)  // chevrons: » fire, ^ cooldowns
                    {
                        float a = MathX.Tau * i / 6f + t * 0.25f;
                        V2 p = c + V2.FromAngle(a) * (R - 26f), dir = V2.FromAngle(a + MathX.Pi / 2f), side = dir.Orthogonal();
                        Rgba col = SupplyGreen.WithAlpha(0.55f * da);
                        for (int j = 0; j < 2; j++)
                        {
                            V2 tip = p + dir * (j * 6f + 4f);
                            Line(tip, tip - dir * 5f + side * 5f, 1.5f, col);
                            Line(tip, tip - dir * 5f - side * 5f, 1.5f, col);
                        }
                    }
                }
                // time left
                float total = Mathf.Max(0.01f, Game.SupplyZoneTime(z) + Game.SupplyOpen + Game.SupplyFold), left = MathX.Clamp01(z.T / total);
                Arc(c, R + 8f, 2f, new Rgba(1f, 1f, 1f, 0.1f * da), 48);
                Arc(c, R + 8f, 3f, SupplyGreen.WithAlpha(0.9f * da), 48, -MathX.Pi / 2f, MathX.Tau * left);
            }

            // a ring under everyone it buffs
            foreach (var p in game.Players.Values)
            {
                if (!Game.Supplied(p) || p.Pos.FartherThan(c, z.Radius + 30f))
                    continue;
                float r = p.Def.BodyR + 8f;
                Arc(p.Pos, r, 2f, SupplyGreen.WithAlpha(0.8f), 28);
                if (!fxReduced)
                    Arc(p.Pos, r + 3f, 1.5f, SupplyGreen.WithAlpha(0.5f), 6, t * 3f, 1.2f);
            }

            DrawSupplyCrate(c, 1f, open, fade);
        }

        /// The crate: olive with its straps crossed while shut; `open` (0-1) lets its four sides fall flat, showing the medkits.
        void DrawSupplyCrate(V2 c, float s, float open, float alpha)
        {
            float h = 18f * s, w = 22f * s;
            Quad(c + new V2(-w + 4f, -h + 4f), c + new V2(w + 4f, -h + 4f), c + new V2(w + 4f, h + 4f), c + new V2(-w + 4f, h + 4f), new Rgba(0f, 0f, 0f, 0.35f * alpha));
            if (open > 0f)
            {
                float o = open * 22f * s;
                Rgba side = SupplyOliveDark.WithAlpha(alpha);
                Quad(c + new V2(-w - o, -h), c + new V2(-w, -h), c + new V2(-w, h), c + new V2(-w - o, h), side);
                Quad(c + new V2(w, -h), c + new V2(w + o, -h), c + new V2(w + o, h), c + new V2(w, h), side);
                Quad(c + new V2(-w, -h - o * 0.8f), c + new V2(w, -h - o * 0.8f), c + new V2(w, -h), c + new V2(-w, -h), side);
                Quad(c + new V2(-w, h), c + new V2(w, h), c + new V2(w, h + o * 0.8f), c + new V2(-w, h + o * 0.8f), side);
            }
            Quad(c + new V2(-w, -h), c + new V2(w, -h), c + new V2(w, h), c + new V2(-w, h), SupplyOlive.WithAlpha(alpha));
            Rgba strap = SupplyStrap.WithAlpha(alpha);
            if (open < 1f)
            {
                Line(c + new V2(-w, -h), c + new V2(w, h), 2.5f * s, strap.WithAlpha(alpha * (1f - open)));
                Line(c + new V2(w, -h), c + new V2(-w, h), 2.5f * s, strap.WithAlpha(alpha * (1f - open)));
            }
            if (open > 0f)  // inside: two medkits
            {
                Quad(c + new V2(-w + 5f, -h + 5f), c + new V2(w - 5f, -h + 5f), c + new V2(w - 5f, h - 5f), c + new V2(-w + 5f, h - 5f), new Rgba(0.11f, 0.14f, 0.06f, alpha * open));
                for (int i = 0; i < 2; i++)
                {
                    V2 m = c + new V2(i == 0 ? -7f : 7f, i == 0 ? -3f : 3f) * s;
                    Quad(m + new V2(-6f, -5f), m + new V2(6f, -5f), m + new V2(6f, 5f), m + new V2(-6f, 5f), new Rgba(0.91f, 1f, 0.94f, alpha * open));
                    Line(m + new V2(-3.5f, 0f), m + new V2(3.5f, 0f), 2f, SupplyGreen.WithAlpha(alpha * open));
                    Line(m + new V2(0f, -3.5f), m + new V2(0f, 3.5f), 2f, SupplyGreen.WithAlpha(alpha * open));
                }
            }
            Line(c + new V2(-w, -h), c + new V2(w, -h), 1.5f, strap); Line(c + new V2(w, -h), c + new V2(w, h), 1.5f, strap);
            Line(c + new V2(w, h), c + new V2(-w, h), 1.5f, strap); Line(c + new V2(-w, h), c + new V2(-w, -h), 1.5f, strap);
            Line(c + new V2(-4f * s, -h + 2f * s), c + new V2(4f * s, -h + 2f * s), 2.5f * s, new Rgba(1f, 1f, 1f, 0.85f * alpha));  // white stripe
        }
    }
}
