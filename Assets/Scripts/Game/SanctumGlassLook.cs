using UnityEngine;

namespace Arena
{
    /// Sanctum Glass (Core/SanctumGlass.cs), from the mock-up "Sanctum Glass" (flat pane). While the pane is up:
    ///   boot      it grows out from its middle in GlassBoot s;
    ///   light     each pane throws a soft wedge of its colour on the floor behind it (toward the caster);
    ///   panes     seven coloured panes, each with a pale diamond, in a dark lead came with gold knobs at the ends; a sheen sweeps across;
    ///   flash     a pane flares when a shot turns to light in it (FxKind.GlassMote, from its age);
    ///   cracks    past a third of its cap, cracks spread across the panes;
    ///   bars      time left (gold) and healing left (green) as two lines along its back.
    /// FxKind.GlassMote is a mote curving from where the shot was turned to the player it heals; FxKind.GlassShatter the pane
    /// breaking into spinning shards.
    public partial class ArenaApp
    {
        const float GlassBoot = 0.3f, GlassFlash = 0.35f;

        static float GlassHash(int i) => Mathf.Repeat(Mathf.Sin(i * 78.233f + 1.7f) * 43758.5453f, 1f);

        /// A point on the pane: `v` px across it (from its middle, caster's left to right), `u` px out from its middle line.
        static V2 GlassAt(Player p, float v, float u) => p.GlassPos + V2.FromAngle(p.GlassAim) * u + Game.GlassAcross(p) * v;

        readonly float[] glassFlash = new float[Game.GlassPanels];

        /// Called for each player with a pane up.
        void DrawSanctumGlass(Player p, float t)
        {
            if (p.GlassT <= 0f)
                return;
            for (int i = 0; i < Game.GlassPanels; i++)
                glassFlash[i] = 0f;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.GlassMote && (int)fx.Pos2.Y == p.Id && fx.T < GlassFlash)
                {
                    int k = Game.GlassPanelAt(p, fx.Pos);
                    glassFlash[k] = Mathf.Max(glassFlash[k], 1f - fx.T / GlassFlash);
                }

            float age = Game.GlassTime - p.GlassT, b = Mathf.Clamp01(age / GlassBoot), boot = 1f - (1f - b) * (1f - b);
            float half = Game.GlassHalf(p) * boot, band = Game.GlassBand, seg = 2f * half / Game.GlassPanels;
            float crack = p.GlassCap > 0f ? Mathf.Clamp01(p.GlassHealed / p.GlassCap) : 0f;
            if (half < 1f)
                return;

            for (int i = 0; i < Game.GlassPanels; i++)  // the light each pane throws on the floor behind it
            {
                float v0 = -half + i * seg, v1 = v0 + seg;
                var c = Game.GlassColors[i].WithAlpha(0.10f + 0.22f * glassFlash[i]);
                Quad(GlassAt(p, v0, -band), GlassAt(p, v1, -band), GlassAt(p, v1 * 1.25f, -band - 70f), GlassAt(p, v0 * 1.25f, -band - 70f), c);
            }
            GlowDisc(p.GlassPos, half * 0.9f, Game.GlassGold, 0.10f);
            for (int i = 0; i < Game.GlassPanels; i++)  // the panes
            {
                float v0 = -half + i * seg, v1 = v0 + seg, vm = (v0 + v1) * 0.5f, fl = glassFlash[i];
                Quad(GlassAt(p, v0, -band), GlassAt(p, v1, -band), GlassAt(p, v1, band), GlassAt(p, v0, band), Game.GlassColors[i].Mix(Rgba.White, 0.35f * fl).WithAlpha(0.74f + 0.26f * fl));
                var dia = Rgba.White.WithAlpha(0.2f + 0.55f * fl);
                Quad(GlassAt(p, vm, -band + 2.5f), GlassAt(p, v1 - 3f, 0f), GlassAt(p, vm, band - 2.5f), GlassAt(p, v0 + 3f, 0f), dia);
                if (fl > 0f)
                    GlowDisc(GlassAt(p, vm, 0f), 22f, Game.GlassColors[i].Mix(Rgba.White, 0.5f), 0.55f * fl);
                if (crack > 0.33f)  // cracks: one per pane at first, two near the cap
                {
                    var cc = Rgba.White.WithAlpha(0.35f + 0.4f * crack);
                    int n = crack > 0.75f ? 2 : 1;
                    for (int k = 0; k < n; k++)
                    {
                        float h = GlassHash(i * 7 + k * 3 + p.Id), h2 = GlassHash(i * 11 + k + 5);
                        V2 a = GlassAt(p, v0 + seg * (0.15f + 0.7f * h), -band), m = GlassAt(p, v0 + seg * (0.2f + 0.6f * h2), (h - 0.5f) * band);
                        V2 e = GlassAt(p, v0 + seg * (0.15f + 0.7f * (1f - h)), band);
                        Line(a, m, 1f, cc);
                        Line(m, e, 1f, cc);
                    }
                }
            }
            float sheen = Mathf.Repeat(t * 0.45f, 1.6f) - 0.3f;  // a sheen sweeping across the glass
            if (sheen > 0f && sheen < 1f)
            {
                float v = -half + 2f * half * sheen;
                Quad(GlassAt(p, v - 6f, -band), GlassAt(p, v + 2f, -band), GlassAt(p, v + 10f, band), GlassAt(p, v + 2f, band), Rgba.White.WithAlpha(0.18f));
            }
            var lead = new Rgba(0.1f, 0.08f, 0.06f, 0.95f);  // the lead came
            Line(GlassAt(p, -half, -band), GlassAt(p, half, -band), 3f, lead);
            Line(GlassAt(p, -half, band), GlassAt(p, half, band), 3f, lead);
            for (int i = 0; i <= Game.GlassPanels; i++)
                Line(GlassAt(p, -half + i * seg, -band), GlassAt(p, -half + i * seg, band), i == 0 || i == Game.GlassPanels ? 3.5f : 2.5f, lead);
            for (int s = -1; s <= 1; s += 2)  // gold knobs at the ends
            {
                GlowDisc(GlassAt(p, s * half, 0f), 12f, Game.GlassGold, 0.5f);
                Circle(GlassAt(p, s * half, 0f), 4.5f, Game.GlassGold, 12);
            }
            float left = Mathf.Clamp01(p.GlassT / Game.GlassTime), heal = 1f - crack;  // time left and healing left, from the middle out
            Line(GlassAt(p, -half * left, -band - 5f), GlassAt(p, half * left, -band - 5f), 1.8f, Game.GlassGold.WithAlpha(0.8f));
            Line(GlassAt(p, -half * heal, -band - 9f), GlassAt(p, half * heal, -band - 9f), 1.8f, Game.GlassHealColor.WithAlpha(0.8f));
        }

        /// FxKind.GlassMote: a healing mote curving from the glass to the player it heals (Pos2.X), a trail of the pane's colour
        /// behind a green core with a white cross; a small ring where it lands.
        void DrawGlassMoteFx(Effect fx)
        {
            Player to;
            if (!game.Players.TryGetValue((int)fx.Pos2.X, out to))
                return;
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
            V2 a = fx.Pos, c = to.Disp, mid = (a + c) * 0.5f, side = (c - a).Orthogonal().Normalized();
            float bow = (GlassHash((int)(a.X * 3f + a.Y)) - 0.5f) * 0.7f * (c - a).Length;
            V2 ctrl = mid + side * bow;
            System.Func<float, V2> at = s => a * ((1f - s) * (1f - s)) + ctrl * (2f * s * (1f - s)) + c * (s * s);
            float e = f * f * (3f - 2f * f);
            for (int k = 5; k >= 1; k--)  // the trail, in the pane's colour
            {
                float s0 = Mathf.Max(0f, e - k * 0.05f);
                Line(at(s0), at(Mathf.Max(0f, e - (k - 1) * 0.05f)), 5f - k * 0.7f, fx.Color.Mix(Rgba.White, 0.25f).WithAlpha(0.8f - k * 0.13f));
            }
            V2 pt = at(e);
            GlowDisc(pt, 18f, Game.GlassHealColor, 0.85f);
            Circle(pt, 5f, Game.GlassHealColor, 12);
            Line(pt + new V2(0f, -4f), pt + new V2(0f, 4f), 1.8f, Rgba.White);
            Line(pt + new V2(-4f, 0f), pt + new V2(4f, 0f), 1.8f, Rgba.White);
            if (f > 0.85f)
                Arc(c, 10f + (f - 0.85f) * 160f, 2f, Game.GlassHealColor.WithAlpha((1f - f) * 5f), 16);
        }

        /// FxKind.GlassShatter: the pane breaking (Pos = its middle, Pos2.X = its aim, Radius = its half width): 28 shards in the
        /// panes' colours spin out both ways and fall, over a gold flash.
        void DrawGlassShatterFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
            V2 n = V2.FromAngle(fx.Pos2.X), across = V2.FromAngle(fx.Pos2.X + MathX.Pi * 0.5f);
            if (f < 0.25f)
                GlowDisc(fx.Pos, fx.Radius * (0.8f + f), Game.GlassGold, 0.5f * (1f - f / 0.25f));
            for (int i = 0; i < 28; i++)
            {
                float h = GlassHash(i), h2 = GlassHash(i + 40), h3 = GlassHash(i + 80);
                float v = (h * 2f - 1f) * fx.Radius, out_ = (h2 - 0.5f) * 2f * (40f + 60f * h3);
                V2 start = fx.Pos + across * v, p = start + n * (out_ * f) + across * ((h3 - 0.5f) * 40f * f);
                float ang = fx.Pos2.X + f * (6f + h * 10f) * (i % 2 == 0 ? 1f : -1f), sz = 3f + 4f * h3;
                V2 d1 = V2.FromAngle(ang) * sz, d2 = V2.FromAngle(ang + 1.9f) * sz * 0.6f;
                var col = Game.GlassColors[Mathf.Min(Game.GlassPanels - 1, (int)((v / fx.Radius + 1f) * 0.5f * Game.GlassPanels))];
                Quad(p - d1, p - d2, p + d1, p + d2, col.Mix(Rgba.White, 0.2f).WithAlpha(1f - f));
            }
        }
    }
}
