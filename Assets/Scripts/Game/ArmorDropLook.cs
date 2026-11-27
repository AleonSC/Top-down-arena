using UnityEngine;

namespace Arena
{
    /// Armor Drop (Core/ArmorDrop.cs; mockup https://claude.ai/artifact/4CUNVxkwG8EgAe3tNViHpC), all worked out from the zone's
    /// replicated timer: the call-in (a red ring that pulses over where it lands, red flare smoke, the steel crate's shadow growing
    /// as it falls under its chute), the landing (a gold shockwave and dust), then the crate's four riveted sides falling open. It
    /// folds away over the last `ArmorFold` s. The vests are `Orb.Vest` (a plate carrier with a steel ring, bobbing); a player
    /// wearing armor gets a steel half-shell and a five-segment steel bar over their health bar.
    public partial class ArenaApp
    {
        static readonly Rgba VestRed = new Rgba(1f, 0.35f, 0.4f), VestGold = Rgba.Gold, VestSteel = new Rgba(0.62f, 0.85f, 1f);  // VestSteel: CrtStyle.Steel
        static readonly Rgba VestMetal = new Rgba(0.29f, 0.32f, 0.36f), VestMetalDark = new Rgba(0.19f, 0.21f, 0.24f), VestStrap = new Rgba(0.11f, 0.13f, 0.15f);
        static readonly Rgba VestCloth = new Rgba(0.17f, 0.21f, 0.25f), VestPlate = new Rgba(0.27f, 0.33f, 0.37f);
        const int VestSegs = 5;  // the armor bar's segments (10 armor each)

        void DrawArmorDrop(Zone z, float t)
        {
            float age = Game.ArmorAge(z);
            V2 c = z.Pos;
            if (age < Game.ArmorCallIn)
            {
                float f = age / Game.ArmorCallIn;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 14f);
                Circle(c, Game.ArmorBlast, VestRed.WithAlpha(0.08f + 0.05f * f), 40);
                Arc(c, Game.ArmorBlast, 2f, VestRed.WithAlpha(0.45f + 0.45f * pulse), 48);
                Arc(c, Game.ArmorBlast * (1f - f) + 6f, 1.5f, VestRed.WithAlpha(0.5f * f), 32);  // closing in on the spot
                if (!fxReduced)
                    for (int i = 0; i < 7; i++)  // flare smoke drifting up
                    {
                        float k = MathX.Fmod(t * 0.7f + i / 7f, 1f);
                        Circle(c + new V2(Mathf.Sin(t * 2f + i * 1.7f) * 8f * k, -8f - k * 60f), 5f + 9f * k, VestRed.WithAlpha(0.18f * (1f - k)), 14);
                    }
                Circle(c + new V2(3f, 3f), 4f, new Rgba(0f, 0f, 0f, 0.3f), 10);
                Line(c + new V2(0f, 4f), c + new V2(0f, -6f), 4f, VestRed);  // the flare
                Circle(c + new V2(0f, -7f), 2.5f + pulse, new Rgba(1f, 0.85f, 0.8f, 0.9f), 10);
                Circle(c, 6f + 26f * f, new Rgba(0f, 0f, 0f, 0.15f + 0.3f * f), 24);  // the shadow grows as it comes down
                float fall = (1f - f) * (1f - f) * 520f;
                V2 at = c + new V2(0f, -fall);
                float s = 0.6f + 0.4f * f;
                Line(at + new V2(-20f, -16f) * s, at + new V2(0f, -70f) * s, 1f, new Rgba(0.75f, 0.85f, 1f, 0.5f));
                Line(at + new V2(20f, -16f) * s, at + new V2(0f, -70f) * s, 1f, new Rgba(0.75f, 0.85f, 1f, 0.5f));
                Arc(at + new V2(0f, -74f) * s, 32f * s, 12f * s, VestSteel.WithAlpha(0.35f), 20, MathX.Pi, MathX.Pi);  // the chute's canopy
                DrawArmorCrate(at, s, 0f, 1f);
                return;
            }

            float since = age - Game.ArmorCallIn;
            if (since < 0.5f)  // the landing: a gold shockwave and dust
            {
                float k = since / 0.5f;
                Arc(c, 30f + k * (Game.ArmorBlast + 20f), 2f + 6f * (1f - k), VestGold.WithAlpha(1f - k), 48);
                if (!fxReduced)
                    for (int i = 0; i < 16; i++)
                    {
                        float a = MathX.Tau * i / 16f + i * 0.37f;
                        Circle(c + V2.FromAngle(a) * (20f + k * (50f + 30f * Mathf.Sin(i * 2.1f))), 4f * (1f - k) + 1f, new Rgba(0.73f, 0.66f, 0.48f, 0.7f * (1f - k)), 8);
                    }
            }
            float open = MathX.Clamp01(since / Game.ArmorOpen);
            open = 1f - (1f - open) * (1f - open);
            DrawArmorCrate(c, 1f, open, MathX.Clamp01(z.T / Game.ArmorFold));
        }

        /// The crate: gunmetal with crossed straps and a steel chevron-shield stencil while shut; `open` (0-1) lets its four riveted
        /// sides fall flat, showing the empty inside.
        void DrawArmorCrate(V2 c, float s, float open, float alpha)
        {
            float h = 22f * s, w = 26f * s;
            Quad(c + new V2(-w + 5f, -h + 5f), c + new V2(w + 5f, -h + 5f), c + new V2(w + 5f, h + 5f), c + new V2(-w + 5f, h + 5f), new Rgba(0f, 0f, 0f, 0.35f * alpha));
            if (open > 0f)
            {
                float o = open * 26f * s;
                Rgba side = VestMetalDark.WithAlpha(alpha);
                Quad(c + new V2(-w - o, -h), c + new V2(-w, -h), c + new V2(-w, h), c + new V2(-w - o, h), side);
                Quad(c + new V2(w, -h), c + new V2(w + o, -h), c + new V2(w + o, h), c + new V2(w, h), side);
                Quad(c + new V2(-w, -h - o * 0.8f), c + new V2(w, -h - o * 0.8f), c + new V2(w, -h), c + new V2(-w, -h), side);
                Quad(c + new V2(-w, h), c + new V2(w, h), c + new V2(w, h + o * 0.8f), c + new V2(-w, h + o * 0.8f), side);
                Rgba rivet = VestSteel.WithAlpha(0.6f * open * alpha);
                Circle(c + new V2(-w - o * 0.5f, 0f), 2f, rivet, 8);
                Circle(c + new V2(w + o * 0.5f, 0f), 2f, rivet, 8);
                Circle(c + new V2(0f, -h - o * 0.4f), 2f, rivet, 8);
                Circle(c + new V2(0f, h + o * 0.4f), 2f, rivet, 8);
            }
            Quad(c + new V2(-w, -h), c + new V2(w, -h), c + new V2(w, h), c + new V2(-w, h), VestMetal.WithAlpha(alpha));
            if (open < 1f)
            {
                float a = alpha * (1f - open);
                Line(c + new V2(-w, -h), c + new V2(w, h), 3f * s, VestStrap.WithAlpha(a));
                Line(c + new V2(w, -h), c + new V2(-w, h), 3f * s, VestStrap.WithAlpha(a));
                // the stencil: a shield with a chevron
                V2 tl = c + new V2(-9f, -8f) * s, tr = c + new V2(9f, -8f) * s, mr = c + new V2(9f, 1f) * s, ml = c + new V2(-9f, 1f) * s, bot = c + new V2(0f, 9f) * s;
                Quad(tl, tr, mr, ml, VestMetalDark.WithAlpha(a));
                Quad(ml, mr, bot, bot, VestMetalDark.WithAlpha(a));
                Rgba rim = VestSteel.WithAlpha(a);
                Line(tl, tr, 1.5f * s, rim); Line(tr, mr, 1.5f * s, rim); Line(mr, bot, 1.5f * s, rim); Line(bot, ml, 1.5f * s, rim); Line(ml, tl, 1.5f * s, rim);
                Line(c + new V2(-5f, -2f) * s, c + new V2(0f, 2f) * s, 1.8f * s, rim);
                Line(c + new V2(0f, 2f) * s, c + new V2(5f, -2f) * s, 1.8f * s, rim);
            }
            if (open > 0f)
                Quad(c + new V2(-w + 5f, -h + 5f), c + new V2(w - 5f, -h + 5f), c + new V2(w - 5f, h - 5f), c + new V2(-w + 5f, h - 5f), new Rgba(0.08f, 0.1f, 0.12f, alpha * open));
            Rgba strap = VestStrap.WithAlpha(alpha);
            Line(c + new V2(-w, -h), c + new V2(w, -h), 2f, strap); Line(c + new V2(w, -h), c + new V2(w, h), 2f, strap);
            Line(c + new V2(w, h), c + new V2(-w, h), 2f, strap); Line(c + new V2(-w, h), c + new V2(-w, -h), 2f, strap);
            Line(c + new V2(-6f * s, -h + 3f * s), c + new V2(6f * s, -h + 3f * s), 3f * s, VestSteel.WithAlpha(0.9f * alpha));  // steel stripe
        }

        /// A ballistic vest on the floor: a plate carrier with steel trim, a steel ground ring and a slow bob; it blinks its last 3 s.
        void DrawVest(Orb o, float t)
        {
            if (o.Life < 3f && MathX.Fmod(t, 0.25f) < 0.1f)
                return;
            V2 c = o.Disp;
            float bob = fxReduced ? 0f : Mathf.Sin(t * 3f + o.Id) * 2f;
            Circle(c + new V2(3f, 4f), 12f, new Rgba(0f, 0f, 0f, 0.35f), 16);
            Arc(c, 19f, 1.5f, VestSteel.WithAlpha(0.55f), 32);
            if (!fxReduced)
                Arc(c, 23f, 1.2f, VestSteel.WithAlpha(0.35f), 10, t * 2f + o.Id, 1.1f);
            c += new V2(0f, bob);
            // the carrier: two shoulder straps over a body, a front plate
            V2 ls = c + new V2(-5f, -13f), rs = c + new V2(5f, -13f);
            Quad(c + new V2(-11f, -10f), c + new V2(11f, -10f), c + new V2(12f, 12f), c + new V2(-12f, 12f), VestCloth);
            Quad(c + new V2(-11f, -10f), ls, c + new V2(-3f, -7f), c + new V2(-9f, -6f), VestCloth);
            Quad(rs, c + new V2(11f, -10f), c + new V2(9f, -6f), c + new V2(3f, -7f), VestCloth);
            Quad(c + new V2(-7f, -3f), c + new V2(7f, -3f), c + new V2(7f, 8f), c + new V2(-7f, 8f), VestPlate);
            Line(c + new V2(-9f, 3f), c + new V2(9f, 3f), 1.2f, VestSteel.WithAlpha(0.5f));
            Rgba trim = VestSteel.WithAlpha(0.95f);
            Line(c + new V2(-11f, -10f), ls, 1.5f, trim); Line(ls, c + new V2(-3f, -7f), 1.5f, trim);
            Line(c + new V2(-3f, -7f), c + new V2(3f, -7f), 1.5f, trim); Line(c + new V2(3f, -7f), rs, 1.5f, trim);
            Line(rs, c + new V2(11f, -10f), 1.5f, trim); Line(c + new V2(11f, -10f), c + new V2(12f, 12f), 1.5f, trim);
            Line(c + new V2(12f, 12f), c + new V2(-12f, 12f), 1.5f, trim); Line(c + new V2(-12f, 12f), c + new V2(-11f, -10f), 1.5f, trim);
        }

        /// A player wearing ballistic armor: a steel half-shell round the body (thicker the more is left).
        void DrawArmorShell(Player p, float t)
        {
            float frac = Mathf.Clamp01(p.Armor / Game.ArmorCap), r = p.Def.BodyR + 3f;
            float w = 2.5f + 1.5f * frac;
            Arc(p.Disp, r, w, VestSteel.WithAlpha(0.45f + 0.45f * frac), 20, MathX.Pi * 0.9f, MathX.Pi * 1.2f);
            Arc(p.Disp, r, w, VestSteel.WithAlpha(0.25f + 0.25f * frac), 14, MathX.Pi * 0.15f, MathX.Pi * 0.7f);
        }

        /// The armor bar over the health bar: five steel segments, 10 armor each. Over the Juggernaut Suit's bar when both show.
        void DrawArmorBar(Player p)
        {
            if (!Game.Armored(p))
                return;
            float frac = Mathf.Clamp01(p.Armor / Game.ArmorCap);
            float w = 40f, h = 4f, gap = 2f, sw = (w - gap * (VestSegs - 1)) / VestSegs;
            V2 at = p.Disp + new V2(-w / 2f, OverheadRow(h));  // (OverheadStack.cs)
            FillRect(at.X - 1f, at.Y - 1f, w + 2f, h + 2f, new Rgba(0f, 0f, 0f, 0.65f));
            for (int i = 0; i < VestSegs; i++)
            {
                float x = at.X + i * (sw + gap), fill = Mathf.Clamp01(frac * VestSegs - i);
                FillRect(x, at.Y, sw, h, new Rgba(0.36f, 0.5f, 0.6f, 0.35f));
                if (fill > 0f)
                {
                    FillRect(x, at.Y, sw * fill, h, VestSteel);
                    FillRect(x, at.Y, sw * fill, 1f, new Rgba(1f, 1f, 1f, 0.5f));  // lit top
                }
            }
        }
    }
}
