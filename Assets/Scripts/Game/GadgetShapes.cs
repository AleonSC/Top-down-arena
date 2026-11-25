using UnityEngine;

namespace Arena
{
    /// The newer gadgets' looks (Core/Gadgets.cs): the Shield Generator, the Medkit, the Cloak, the Overclock Chip, the EMP and the
    /// throwables (the Grenade, the Smoke Bomb, the Gas Grenade, the Incendiary, the Land Mine) as world shapes (on the floor, thrown, held) and HUD glyphs (the wheel and the chip), plus
    /// the Shield Generator's dome. DrawGadgetShape picks any kind's shape (GadgetLook.cs has the
    /// Magnet, the Lodestone and the Energy Drink).
    public partial class ArenaApp
    {
        static readonly Rgba ShieldBlue = new Rgba(0.38f, 0.62f, 1f), ShieldDeep = new Rgba(0.06f, 0.14f, 0.34f);
        static readonly Rgba MedkitWhite = new Rgba(0.95f, 0.95f, 0.97f), MedkitRed = new Rgba(0.92f, 0.22f, 0.3f), MedkitDark = new Rgba(0.25f, 0.27f, 0.32f);
        static readonly Rgba NadeKhaki = new Rgba(0.6f, 0.64f, 0.36f), NadeDark = new Rgba(0.2f, 0.23f, 0.1f);
        static readonly Rgba SmokeTeal = new Rgba(0.37f, 0.83f, 0.74f), SmokeGrey = new Rgba(0.62f, 0.66f, 0.7f), CanDark = new Rgba(0.12f, 0.14f, 0.17f);
        static readonly Rgba GasPurple = new Rgba(0.72f, 0.42f, 1f);
        static readonly Rgba CloakIndigo = new Rgba(0.5f, 0.46f, 0.95f), CloakDeep = new Rgba(0.1f, 0.08f, 0.24f);
        static readonly Rgba ChipGold = new Rgba(1f, 0.8f, 0.25f), ChipBoard = new Rgba(0.1f, 0.24f, 0.16f);
        static readonly Rgba MineSalmon = new Rgba(0.95f, 0.55f, 0.38f), MineBody = new Rgba(0.32f, 0.35f, 0.3f);
        static readonly Rgba EmpIce = new Rgba(0.7f, 0.92f, 1f);
        static readonly Rgba BurnOrange = new Rgba(1f, 0.5f, 0.15f), BurnYellow = new Rgba(1f, 0.85f, 0.35f), BottleBrown = new Rgba(0.55f, 0.3f, 0.14f);

        /// A gadget's colour: its floor ring, held glow, wheel tile and chip.
        static Rgba GadgetTone(GadgetId g)
        {
            switch (g)
            {
                case GadgetId.Lodestone: return GadgetCyan;
                case GadgetId.EnergyDrink: return EnergyLime;
                case GadgetId.ShieldGenerator: return ShieldBlue;
                case GadgetId.Medkit: return new Rgba(1f, 0.72f, 0.76f);
                case GadgetId.Grenade: return NadeKhaki;
                case GadgetId.SmokeBomb: return SmokeTeal;
                case GadgetId.GasGrenade: return GasPurple;
                case GadgetId.Incendiary: return BurnOrange;
                case GadgetId.Cloak: return CloakIndigo;
                case GadgetId.OverclockChip: return ChipGold;
                case GadgetId.LandMine: return MineSalmon;
                case GadgetId.Emp: return EmpIce;
                default: return MagRed;
            }
        }

        /// Any gadget's world shape centred on c; s is about its radius, spin turns it (thrown, or bobbing on the floor), glow lights it.
        void DrawGadgetShape(GadgetId g, V2 c, float s, float spin, float t, bool glow)
        {
            switch (g)
            {
                case GadgetId.Lodestone: DrawLodestoneShape(c, s * 1.3f, 1f, spin); break;
                case GadgetId.EnergyDrink: DrawEnergyShape(c, s * 0.95f, 1f, Mathf.Sin(spin) * 0.35f, glow); break;
                case GadgetId.ShieldGenerator: DrawShieldGenShape(c, s, spin, glow); break;
                case GadgetId.Medkit: DrawMedkitCaseShape(c, s, Mathf.Sin(spin) * 0.3f, glow); break;
                case GadgetId.Grenade: DrawNadeShape(c, s, Mathf.Sin(spin) * 0.6f, glow); break;
                case GadgetId.SmokeBomb: DrawCanShape(c, s, Mathf.Sin(spin) * 0.5f, SmokeGrey, SmokeTeal, glow); break;
                case GadgetId.GasGrenade: DrawCanShape(c, s, Mathf.Sin(spin) * 0.5f, GasPurple, BurnYellow, glow); break;
                case GadgetId.Incendiary: DrawBottleShape(c, s, Mathf.Sin(spin) * 0.4f, t, glow); break;
                case GadgetId.Cloak: DrawCloakShape(c, s, Mathf.Sin(spin) * 0.25f, t, glow); break;
                case GadgetId.OverclockChip: DrawChipShape(c, s, spin * 0.3f, t, glow); break;
                case GadgetId.LandMine: DrawLandMineShape(c, s, t, glow); break;
                case GadgetId.Emp: DrawEmpShape(c, s, spin, t, glow); break;
                default: DrawMagnetShape(c, s, -Mathf.PI / 2f + Mathf.Sin(spin) * 0.5f, 1f, glow); break;
            }
        }

        /// The Shield Generator: a dark disc with a blue ring, a small dome on top and a lit core.
        void DrawShieldGenShape(V2 c, float s, float spin, bool glow)
        {
            if (glow)
                Circle(c, s * 2.4f, ShieldBlue.WithAlpha(0.14f), 16);
            Circle(c, s + 1.2f, ShieldDeep, 16);
            Circle(c, s, new Rgba(0.16f, 0.22f, 0.36f), 16);
            for (int i = 0; i < 6; i++)  // six emitters round the rim
                Circle(c + V2.FromAngle(spin + i * MathX.Tau / 6f) * s * 0.72f, Mathf.Max(1f, s * 0.14f), ShieldBlue, 6);
            Arc(c, s * 0.48f, Mathf.Max(1.2f, s * 0.18f), ShieldBlue, 14);
            Circle(c, s * 0.26f, new Rgba(0.85f, 0.93f, 1f), 10);
        }

        /// The Medkit: a white case with a red cross and a dark handle.
        void DrawMedkitCaseShape(V2 c, float s, float tilt, bool glow)
        {
            V2.Axes(tilt, out V2 x, out V2 y);
            if (glow)
                Circle(c, s * 2.4f, MedkitRed.WithAlpha(0.12f), 16);
            float w = s * 1.15f, h = s * 0.8f;
            Line(c - x * (w + 1f), c + x * (w + 1f), h * 2f + 2f, MedkitDark);  // outline
            Line(c - x * w, c + x * w, h * 2f, MedkitWhite);
            Line(c - x * w + y * (h * 0.7f), c + x * w + y * (h * 0.7f), h * 0.35f, new Rgba(0.8f, 0.82f, 0.86f));  // the lid's shadow
            float k = s * 0.5f, bar = Mathf.Max(1.4f, s * 0.26f);
            Line(c - x * k, c + x * k, bar, MedkitRed);
            Line(c - y * k, c + y * k, bar, MedkitRed);
            Line(c - x * (w * 0.5f) - y * (h + 0.5f), c + x * (w * 0.5f) - y * (h + 0.5f), Mathf.Max(1f, s * 0.2f), MedkitDark);  // the handle
        }

        /// The Grenade: a khaki segmented body, a steel lever and its pin ring.
        void DrawNadeShape(V2 c, float s, float tilt, bool glow)
        {
            V2.Axes(tilt - Mathf.PI / 2f, out V2 up, out V2 side);
            if (glow)
                Circle(c, s * 2.3f, NadeKhaki.WithAlpha(0.13f), 16);
            Circle(c, s + 1.2f, NadeDark, 16);
            Circle(c, s, NadeKhaki, 16);
            Line(c - side * s * 0.9f, c + side * s * 0.9f, Mathf.Max(1f, s * 0.12f), NadeDark.WithAlpha(0.7f));  // the grooves
            Line(c - up * s * 0.9f, c + up * s * 0.9f, Mathf.Max(1f, s * 0.12f), NadeDark.WithAlpha(0.7f));
            Circle(c - side * s * 0.35f + up * s * 0.35f, s * 0.22f, new Rgba(1f, 1f, 1f, 0.3f), 8);  // shine
            V2 top = c + up * (s * 1.05f);
            Line(top - side * s * 0.3f, top + side * s * 0.3f, s * 0.45f, MagSteel);  // the fuse head
            Line(top + side * s * 0.25f, c + side * s * 0.9f + up * s * 0.1f, Mathf.Max(1f, s * 0.2f), MagSteel);  // the lever
            Arc(top - side * s * 0.55f + up * s * 0.2f, s * 0.3f, Mathf.Max(0.8f, s * 0.1f), MagSteel, 10);  // the pin ring
        }

        /// A canister (the Smoke Bomb, the Gas Grenade): a body in `body`, a dark cap and base, a band in `band`.
        void DrawCanShape(V2 c, float s, float tilt, Rgba body, Rgba band, bool glow)
        {
            V2.Axes(tilt - Mathf.PI / 2f, out V2 up, out V2 side);
            if (glow)
                Circle(c, s * 2.3f, body.WithAlpha(0.13f), 16);
            float w = s * 1.15f;
            Line(c - up * (s + 0.8f), c + up * (s + 0.8f), w + 1.8f, CanDark);
            Line(c - up * s, c + up * s, w, body);
            Line(c + up * s * 0.62f, c + up * s, w, CanDark.Mix(body, 0.3f));  // the cap
            Line(c - up * s * 0.12f, c + up * s * 0.12f, w, band);  // the band
            Line(c + up * s * 0.98f, c + up * (s * 1.35f), w * 0.35f, MagSteel);  // the nozzle
            Line(c - side * (w * 0.3f) - up * s * 0.55f, c - side * (w * 0.3f) + up * s * 0.4f, Mathf.Max(1f, w * 0.14f), new Rgba(1f, 1f, 1f, 0.3f));
        }

        /// The Incendiary: a brown bottle with a rag in its neck, burning.
        void DrawBottleShape(V2 c, float s, float tilt, float t, bool glow)
        {
            V2.Axes(tilt - Mathf.PI / 2f, out V2 up, out V2 side);
            if (glow)
                Circle(c, s * 2.4f, BurnOrange.WithAlpha(0.15f), 16);
            V2 body = c - up * s * 0.25f;
            Circle(body, s * 0.85f + 1.2f, new Rgba(0.2f, 0.1f, 0.04f), 14);
            Circle(body, s * 0.85f, BottleBrown, 14);
            Line(body + up * s * 0.5f, c + up * s * 1.05f, s * 0.55f + 1.6f, new Rgba(0.2f, 0.1f, 0.04f));  // the neck
            Line(body + up * s * 0.5f, c + up * s * 1.05f, s * 0.55f, BottleBrown);
            Circle(body - side * s * 0.35f + up * s * 0.2f, s * 0.2f, new Rgba(1f, 0.85f, 0.7f, 0.35f), 8);  // shine
            V2 rag = c + up * s * 1.2f;
            Line(rag - up * s * 0.2f, rag + up * s * 0.25f, s * 0.3f, new Rgba(0.9f, 0.86f, 0.75f));
            float f = 0.8f + 0.2f * Mathf.Sin(t * 17f + c.X);  // the flame
            Circle(rag + up * s * 0.55f, s * 0.42f * f, BurnOrange.WithAlpha(0.85f), 10);
            Circle(rag + up * s * 0.5f, s * 0.24f * f, BurnYellow, 8);
        }

        /// The Cloak: a folded indigo cloak, its hood up, a faint shimmer running down it.
        void DrawCloakShape(V2 c, float s, float tilt, float t, bool glow)
        {
            V2.Axes(tilt - Mathf.PI / 2f, out V2 up, out V2 side);
            if (glow)
                Circle(c, s * 2.4f, CloakIndigo.WithAlpha(0.13f), 16);
            V2 hood = c + up * s * 0.55f, hem = c - up * s * 0.9f;
            Line(hood, hem - side * s * 0.75f, s * 0.55f + 1.6f, CloakDeep);  // the folds, outlined
            Line(hood, hem + side * s * 0.75f, s * 0.55f + 1.6f, CloakDeep);
            Line(hem - side * s * 0.85f, hem + side * s * 0.85f, s * 0.4f + 1.6f, CloakDeep);
            Circle(hood, s * 0.6f + 1.2f, CloakDeep, 12);
            Line(hood, hem - side * s * 0.75f, s * 0.55f, CloakIndigo);
            Line(hood, hem + side * s * 0.75f, s * 0.55f, CloakIndigo);
            Line(hem - side * s * 0.85f, hem + side * s * 0.85f, s * 0.4f, CloakIndigo);
            Line(hood - up * s * 0.2f, hem, s * 0.5f, CloakIndigo.Mix(CloakDeep, 0.3f));
            Circle(hood, s * 0.6f, CloakIndigo, 12);
            Circle(hood - up * s * 0.08f, s * 0.36f, CloakDeep, 10);  // the dark under the hood
            float f = Mathf.Repeat(t * 0.9f, 1f);  // the shimmer
            Line(hood - up * s * (0.2f + 1.2f * f) - side * s * 0.4f, hood - up * s * (0.2f + 1.2f * f) + side * s * 0.4f, Mathf.Max(1f, s * 0.14f), new Rgba(0.85f, 0.85f, 1f, 0.5f * (1f - f)));
        }

        /// The Overclock Chip: a green board with a gold die, pins down two sides, a pulse of light along its traces.
        void DrawChipShape(V2 c, float s, float tilt, float t, bool glow)
        {
            V2.Axes(tilt, out V2 x, out V2 y);
            if (glow)
                Circle(c, s * 2.4f, ChipGold.WithAlpha(0.14f), 16);
            for (int i = -2; i <= 2; i++)  // the pins
            {
                V2 o = x * (i * s * 0.36f);
                Line(c + o - y * s * 1.15f, c + o + y * s * 1.15f, Mathf.Max(1f, s * 0.16f), MagSteel);
            }
            Line(c - x * (s + 1f), c + x * (s + 1f), s * 1.7f + 2f, new Rgba(0.03f, 0.08f, 0.05f));
            Line(c - x * s, c + x * s, s * 1.7f, ChipBoard);
            Line(c - x * s * 0.45f, c + x * s * 0.45f, s * 0.9f, ChipGold);  // the die
            float f = Mathf.Repeat(t * 1.4f, 1f);
            Circle(c + x * s * (-0.9f + 1.8f * f) + y * s * 0.62f, Mathf.Max(1f, s * 0.13f), new Rgba(1f, 0.95f, 0.6f, 0.9f), 6);  // a pulse along a trace
            Line(c - x * s * 0.9f + y * s * 0.62f, c + x * s * 0.9f + y * s * 0.62f, Mathf.Max(0.8f, s * 0.07f), ChipGold.WithAlpha(0.5f));
        }

        /// The Land Mine: a squat olive disc with a pressure plate and a blinking salmon light.
        void DrawLandMineShape(V2 c, float s, float t, bool glow)
        {
            if (glow)
                Circle(c, s * 2.3f, MineSalmon.WithAlpha(0.12f), 16);
            Circle(c, s * 1.05f + 1.2f, new Rgba(0.08f, 0.09f, 0.08f), 16);
            Circle(c, s * 1.05f, MineBody, 16);
            Arc(c, s * 0.7f, Mathf.Max(1f, s * 0.14f), new Rgba(0.18f, 0.2f, 0.17f), 16);  // the plate's rim
            Circle(c, s * 0.45f, MineBody.Mix(MagSteel, 0.3f), 12);
            bool blink = Mathf.Repeat(t * 1.5f, 1f) < 0.3f;
            Circle(c, s * 0.2f, blink ? MineSalmon : MineSalmon.Mix(MineBody, 0.6f), 8);
            if (blink)
                Circle(c, s * 0.5f, MineSalmon.WithAlpha(0.3f), 10);
        }

        /// The EMP: a steel coil puck, an ice-blue core and a spark crawling round it.
        void DrawEmpShape(V2 c, float s, float spin, float t, bool glow)
        {
            if (glow)
                Circle(c, s * 2.4f, EmpIce.WithAlpha(0.14f), 16);
            Circle(c, s + 1.2f, new Rgba(0.08f, 0.12f, 0.18f), 16);
            Circle(c, s, new Rgba(0.3f, 0.36f, 0.45f), 16);
            for (int i = 0; i < 3; i++)  // the coil
                Arc(c, s * (0.45f + i * 0.17f), Mathf.Max(0.8f, s * 0.08f), new Rgba(0.85f, 0.6f, 0.3f), 14);
            Circle(c, s * 0.3f, EmpIce, 10);
            V2 sp = c + V2.FromAngle(spin * 3f + t * 5f) * s * 0.8f;
            Circle(sp, Mathf.Max(1f, s * 0.18f), Rgba.White, 6);
            Line(sp, c + V2.FromAngle(spin * 3f + t * 5f + 0.5f) * s * 0.55f, Mathf.Max(0.8f, s * 0.08f), EmpIce);
        }

        /// A player with the Cloak on: an indigo shimmer drifting round them (the body itself is drawn faded: Juice.cs, Player.VanishT).
        void DrawCloakAura(Player p, float t)
        {
            float r = p.Def.BodyR;
            if (fxOn)
                for (int i = 0; i < 6; i++)
                {
                    float a = t * 1.2f + i * MathX.Tau / 6f, f = Mathf.Repeat(t * 0.8f + i * 0.31f, 1f);
                    Arc(p.Disp, r + 5f + f * 8f, 1.4f, CloakIndigo.WithAlpha(0.35f * (1f - f)), 6, a, 0.6f);
                }
            Arc(p.Disp, r + 4f, 2.2f, CloakIndigo.WithAlpha(0.7f), 30, -Mathf.PI / 2f, MathX.Tau * Mathf.Clamp01(p.GadgetLeft / Game.CloakTime));
        }

        /// A player with the Overclock Chip on: gold sparks jumping off them, a ticking ring, the time left.
        void DrawOverclockAura(Player p, float t)
        {
            float r = p.Def.BodyR;
            if (fxOn)
                for (int i = 0; i < 5; i++)
                {
                    float f = Mathf.Repeat(t * 2.6f + i * 0.23f, 1f), a = i * 1.7f + Mathf.Floor(t * 2.6f + i * 0.23f) * 2.3f;
                    V2 d = V2.FromAngle(a), from = p.Disp + d * (r + 2f + f * 14f);
                    Line(from, from + d * 5f + d.Orthogonal() * 3f * Mathf.Sin(a * 3f), 1.6f, ChipGold.WithAlpha(0.8f * (1f - f)));
                }
            int ticks = 12;
            for (int i = 0; i < ticks; i++)  // a clock face ticking round fast
            {
                float a = i * MathX.Tau / ticks, lit = Mathf.Repeat(t * 3f - i / (float)ticks, 1f);
                Arc(p.Disp, r + 8f, 2f, ChipGold.WithAlpha(0.15f + 0.5f * (1f - lit)), 3, a, MathX.Tau / ticks * 0.5f);
            }
            Arc(p.Disp, r + 4f, 2.4f, ChipGold.WithAlpha(0.85f), 30, -Mathf.PI / 2f, MathX.Tau * Mathf.Clamp01(p.GadgetLeft / Game.OverclockTime));
        }

        /// A player's Shield Generator switched on: a blue dome round them, its emitters turning, the time left round the body.
        void DrawShieldDome(Player p, float t)
        {
            float R = Game.ShieldGenR, k = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            Circle(p.Disp, R, ShieldBlue.WithAlpha(fxOn ? 0.07f + 0.03f * k : 0.08f), 40);
            Arc(p.Disp, R, 2.2f, ShieldBlue.WithAlpha(0.65f), 48);
            if (fxOn)
            {
                for (int i = 0; i < 12; i++)  // hex plates shimmering round the rim
                {
                    float a = t * 0.6f + i * MathX.Tau / 12f, glint = Mathf.Repeat(t * 0.8f + i * 0.37f, 1f);
                    Arc(p.Disp, R - 4f, 2.6f, new Rgba(0.75f, 0.88f, 1f, 0.35f * (1f - glint)), 4, a, MathX.Tau / 12f * 0.7f);
                }
                Arc(p.Disp, R * 0.82f, 1.2f, ShieldBlue.WithAlpha(0.25f), 40, -t * 0.9f, MathX.Tau * 0.3f);
            }
            Arc(p.Disp, p.Def.BodyR + 4f, 2.4f, ShieldBlue.WithAlpha(0.85f), 30, -Mathf.PI / 2f, MathX.Tau * Mathf.Clamp01(p.GadgetLeft / Game.ShieldGenTime));
        }

        /// A gadget's HUD glyph for the newer kinds (DrawGadgetGlyph in GadgetWheel.cs handles the others). False when it isn't one of them.
        bool DrawNewGadgetGlyph(GadgetId g, Vector2 c, float r, Color col)
        {
            float t = Mathf.Max(2f, r * 0.16f), k = r * 0.55f;
            Color dark = new Color(0.06f, 0.07f, 0.1f), steel = new Color(0.86f, 0.9f, 0.96f);
            switch (g)
            {
                case GadgetId.ShieldGenerator:
                    Ui.Dot(new Rect(c.x - k, c.y - k, k * 2f, k * 2f), col);
                    Ui.Dot(new Rect(c.x - k * 0.62f, c.y - k * 0.62f, k * 1.24f, k * 1.24f), dark);
                    Ui.Dot(new Rect(c.x - k * 0.3f, c.y - k * 0.3f, k * 0.6f, k * 0.6f), Color.white);
                    Ui.Dot(new Rect(c.x - r * 0.85f, c.y - r * 0.85f, r * 1.7f, r * 1.7f), col, true);
                    return true;
                case GadgetId.Medkit:
                    Ui.Fill(new Rect(c.x - k, c.y - k * 0.75f, k * 2f, k * 1.5f), col);
                    Ui.Fill(new Rect(c.x - k * 0.5f, c.y - k * 0.75f - t, k, t), col);  // the handle
                    Ui.Fill(new Rect(c.x - k * 0.5f, c.y - t * 0.5f, k, t), MedkitRed.ToColor());
                    Ui.Fill(new Rect(c.x - t * 0.5f, c.y - k * 0.5f, t, k), MedkitRed.ToColor());
                    return true;
                case GadgetId.Grenade:
                    Ui.Dot(new Rect(c.x - k * 0.8f, c.y - k * 0.55f, k * 1.6f, k * 1.6f), col);
                    Ui.Fill(new Rect(c.x - k * 0.3f, c.y - k * 0.95f, k * 0.6f, k * 0.45f), steel);  // the fuse head
                    Ui.Fill(new Rect(c.x + k * 0.2f, c.y - k * 0.85f, k * 0.6f, t * 0.8f), steel);  // the lever
                    Ui.Fill(new Rect(c.x - t * 0.3f, c.y - k * 0.4f, t * 0.6f, k * 1.3f), dark.WithAlpha(0.5f));
                    return true;
                case GadgetId.SmokeBomb:
                case GadgetId.GasGrenade:
                {
                    var can = new Rect(c.x - k * 0.6f, c.y - k * 0.8f, k * 1.2f, k * 1.8f);
                    Ui.Fill(can, col);
                    Ui.Fill(new Rect(can.x, can.y, can.width, can.height * 0.22f), dark.WithAlpha(0.6f));  // the cap
                    Ui.Fill(new Rect(c.x - k * 0.2f, can.y - t, k * 0.4f, t), steel);  // the nozzle
                    Ui.Fill(new Rect(can.x, c.y + k * 0.05f, can.width, t), g == GadgetId.SmokeBomb ? Color.white : BurnYellow.ToColor());  // the band
                    return true;
                }
                case GadgetId.Cloak:
                    Ui.Dot(new Rect(c.x - k * 0.55f, c.y - k * 1f, k * 1.1f, k * 1.1f), col);  // the hood
                    Ui.Dot(new Rect(c.x - k * 0.3f, c.y - k * 0.75f, k * 0.6f, k * 0.6f), dark);
                    Ui.Fill(new Rect(c.x - k * 0.55f, c.y - k * 0.1f, k * 1.1f, k * 0.5f), col);  // the cloak, widening down
                    Ui.Fill(new Rect(c.x - k * 0.8f, c.y + k * 0.35f, k * 1.6f, k * 0.55f), col);
                    return true;
                case GadgetId.OverclockChip:
                    for (int i = -1; i <= 1; i++)  // the pins
                    {
                        Ui.Fill(new Rect(c.x + i * k * 0.5f - t * 0.3f, c.y - k * 1.05f, t * 0.6f, k * 2.1f), steel);
                        Ui.Fill(new Rect(c.x - k * 1.05f, c.y + i * k * 0.5f - t * 0.3f, k * 2.1f, t * 0.6f), steel);
                    }
                    Ui.Fill(new Rect(c.x - k * 0.75f, c.y - k * 0.75f, k * 1.5f, k * 1.5f), ChipBoard.ToColor());
                    Ui.Fill(new Rect(c.x - k * 0.42f, c.y - k * 0.42f, k * 0.84f, k * 0.84f), col);
                    return true;
                case GadgetId.LandMine:
                    Ui.Dot(new Rect(c.x - k, c.y - k * 0.6f, k * 2f, k * 1.4f), MineBody.ToColor());
                    Ui.Dot(new Rect(c.x - k, c.y - k * 0.6f, k * 2f, k * 1.4f), col, true);
                    Ui.Dot(new Rect(c.x - k * 0.25f, c.y - k * 0.15f, k * 0.5f, k * 0.5f), col);
                    return true;
                case GadgetId.Emp:
                    Ui.Dot(new Rect(c.x - r * 0.85f, c.y - r * 0.85f, r * 1.7f, r * 1.7f), col, true);
                    Ui.Fill(new Rect(c.x - t * 0.1f, c.y - k * 0.95f, t, k * 0.95f), col);  // a bolt: two offset bars
                    Ui.Fill(new Rect(c.x - t * 0.9f, c.y - t * 0.4f, t * 1.8f, t * 0.8f), col);
                    Ui.Fill(new Rect(c.x - t * 0.9f, c.y, t, k * 0.95f), col);
                    return true;
                case GadgetId.Incendiary:
                    Ui.Dot(new Rect(c.x - k * 0.7f, c.y - k * 0.2f, k * 1.4f, k * 1.2f), col);
                    Ui.Fill(new Rect(c.x - k * 0.22f, c.y - k * 0.75f, k * 0.44f, k * 0.7f), col);  // the neck
                    Ui.Dot(new Rect(c.x - k * 0.32f, c.y - k * 1.35f, k * 0.64f, k * 0.7f), BurnYellow.ToColor());  // the flame
                    return true;
            }
            return false;
        }
    }
}
