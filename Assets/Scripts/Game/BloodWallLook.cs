using UnityEngine;

namespace Arena
{
    /// The tank Hemomancer's Blood Wall (Core/BloodWall.cs), look B "Thornvein" of Docs/Mockups/blood-wall.html: two veins
    /// braid along the path with blood thorns jutting out both sides (every third node, alternating sides), a heartbeat
    /// pulse runs down the wall from the tank, faster as the drain climbs, and old wall darkens, its thorns drawing back,
    /// before it crumbles. Each `ZoneKind.BloodWall` zone draws the stretch from the node before it to itself; the
    /// stretch from the newest node to the tank is drawn with the tank (DrawWallTail). Round the tank: a ring heating
    /// through the drain's four stages (Seep, Flow, Gush, Hemorrhage), and the drain over their head on the HUD. A shot
    /// stopped by the wall bursts back as a spray of blood (FxKind.WallSplash).
    public partial class ArenaApp
    {
        static readonly Rgba VeinUnder = new Rgba(0.23f, 0.01f, 0.035f), VeinLow = new Rgba(0.54f, 0.05f, 0.125f), VeinHigh = new Rgba(0.7f, 0.08f, 0.17f);
        static readonly Rgba ThornRed = new Rgba(0.55f, 0.05f, 0.09f), PulsePink = new Rgba(1f, 0.43f, 0.49f), WallAsh = new Rgba(0.16f, 0.06f, 0.07f);
        static readonly Rgba[] WallTier = { new Rgba(1f, 0.54f, 0.59f), new Rgba(1f, 0.33f, 0.41f), new Rgba(0.91f, 0.13f, 0.23f), new Rgba(1f, 0.82f, 0.84f) };
        static readonly string[] WallTierName = { "Seep", "Flow", "Gush", "Hemorrhage" };
        const float BraidStep = 0.75f, BraidAmp = 4.5f, WallSubsteps = 3;

        /// The drain's stage: under 2%/s, under 4, under 8, then Hemorrhage.
        static int WallStage(float pctPerSec) => pctPerSec < 2f ? 0 : pctPerSec < 4f ? 1 : pctPerSec < 8f ? 2 : 3;

        static Rgba Mix(Rgba a, Rgba b, float u) => new Rgba(a.R + (b.R - a.R) * u, a.G + (b.G - a.G) * u, a.B + (b.B - a.B) * u, a.A + (b.A - a.A) * u);

        static float WallPct(Player p) => Game.WallRate(p.BloodWallClock, Game.WallDoubleOf(p)) * 100f;

        /// How fast the heartbeat runs down an owner's wall (beats a second): quicker with each stage.
        float WallBeat(int owner)
        {
            Player p;
            if (game.Players.TryGetValue(owner, out p) && p.BloodWallOn)
                return 1.2f + 0.9f * WallStage(WallPct(p));
            return 0.8f;
        }

        /// A point on one strand of the braid, `k` nodes along the wall (fractional between nodes).
        static V2 Braid(V2 at, V2 n, float k, float phase) => at + n * (Mathf.Sin(k * BraidStep + phase) * BraidAmp);

        /// One stretch of wall: thorns, the two veins, the pulse, the ageing.
        void DrawBloodWallNode(Zone z, float t)
        {
            Zone prev;
            V2 b = z.Pos, a = z.Aim >= 0f && game.Zones.TryGetValue((int)z.Aim, out prev) && prev.Kind == ZoneKind.BloodWall ? prev.Pos : b;
            float age = z.Duration - z.T, life = Mathf.Clamp01(age / Mathf.Max(0.1f, z.Duration)), k = z.GuardFrac;
            float grow = Mathf.Clamp01(age / 0.18f), crumble = Mathf.Clamp01(z.T / 0.6f);
            float dark = Mathf.Clamp01((life - 0.6f) / 0.4f);  // the last 40%: it dries and darkens
            V2 d = b - a;
            float len = d.Length;
            V2 dir = len > 0.01f ? d / len : new V2(1f, 0f), n = dir.Orthogonal();
            float alpha = crumble;
            DrawStrands(a, b, n, k - 1f, k, grow * crumble, dark, alpha);
            // a thorn every third node, alternating sides; it springs out as the node goes down and draws back before the crumble
            int ki = Mathf.RoundToInt(k);
            if (ki % 3 == 0)
            {
                float side = (ki / 3) % 2 == 0 ? -1f : 1f;
                float tl = (8f + 6f * SprayHash(z.Id * 0.37f)) * Mathf.Clamp01(age / 0.25f) * (dark > 0f ? 1f - 0.35f * dark : 1f) * crumble;
                V2 baseA = b - dir * 6f, baseB = b + dir * 6f, tip = b + n * (side * (tl + 3f)) - dir * 2f;
                SetColor(Mix(ThornRed, WallAsh, dark).WithAlpha(alpha));
                Tri(baseA, tip, baseB);
                Line(b + n * (side * 2f), tip, 1.2f, new Rgba(1f, 0.45f, 0.5f, 0.35f * (1f - dark) * alpha));  // its lit edge
            }
            // the heartbeat: a bright band riding down the wall from the tank
            float w = Mathf.Repeat(t * WallBeat(z.Owner) - age * 0.9f, 1f);
            if (w > 0.86f && dark < 1f)
            {
                float g = (w - 0.86f) / 0.14f;
                g = Mathf.Sin(g * Mathf.PI) * (1f - dark) * crumble;
                Circle(b, 5.5f, PulsePink.WithAlpha(0.45f * g), 10);
                Line(a, b, 4f, PulsePink.WithAlpha(0.35f * g));
            }
        }

        /// The two braided veins from a (node ka) to b (node kb): a dark under-vein, then each strand on top.
        void DrawStrands(V2 a, V2 b, V2 n, float ka, float kb, float width, float dark, float alpha)
        {
            if (width <= 0.01f || alpha <= 0.01f)
                return;
            for (int pass = 0; pass < 2; pass++)
                for (int s = 0; s < 2; s++)
                {
                    float phase = s == 0 ? 0f : Mathf.PI;
                    Rgba col = pass == 0 ? VeinUnder : (s == 0 ? VeinLow : VeinHigh);
                    col = Mix(col, WallAsh, dark * (pass == 0 ? 0.5f : 0.85f)).WithAlpha(alpha);
                    float wd = (pass == 0 ? 8f : 5f) * width;
                    V2 last = Braid(a, n, ka, phase);
                    for (int i = 1; i <= WallSubsteps; i++)
                    {
                        float u = i / (float)WallSubsteps;
                        V2 at = Braid(V2.Lerp(a, b, u), n, Mathf.Lerp(ka, kb, u), phase);
                        Line(last, at, wd, col);
                        Circle(at, wd * 0.5f, col, 8);
                        last = at;
                    }
                }
        }

        /// Under a tank laying the wall: the veins from the newest node to their feet, the drain ring and the drops.
        void DrawWallTail(Player p, float r, float t, float a)
        {
            if (!p.BloodWallOn || p.Downed)
                return;
            Zone newest = null;
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.BloodWall && z.Owner == p.Id && (newest == null || z.Id > newest.Id))
                    newest = z;
            if (newest != null && newest.Pos.DistanceTo(p.Disp) < 40f)
            {
                V2 d = p.Disp - newest.Pos;
                float len = d.Length;
                if (len > 0.5f)
                    DrawStrands(newest.Pos, p.Disp, (d / len).Orthogonal(), newest.GuardFrac, newest.GuardFrac + len / Game.WallSpacing, 1f, 0f, a);
            }
            float pct = WallPct(p);
            int stage = WallStage(pct);
            Rgba c = WallTier[stage];
            float beat = p.BloodWallClock * (1.2f + 0.9f * stage);
            float pulse = 0.5f + 0.5f * Mathf.Sin(beat * MathX.Tau);
            Arc(p.Disp, r + 7f + 2f * pulse, 2f + stage, c.WithAlpha((0.35f + 0.4f * pulse) * a), 32);
            for (int j = 0; j < 2 + stage; j++)  // drops falling from them into the line
            {
                float ph = Mathf.Repeat(t * 2.2f + j * 0.37f, 1f);
                V2 at = p.Disp + new V2((SprayHash(j * 3.3f + Mathf.Floor(t * 2.2f + j * 0.37f)) - 0.5f) * r * 1.2f, -r * 0.3f + ph * r * 1.1f);
                Circle(at, 1.8f * (1f - ph * 0.5f), BloodRed.WithAlpha((1f - ph) * 0.9f * a), 6);
            }
        }

        /// A shot stopped by the wall (FxKind.WallSplash): drops burst back the way it came, a flash at the vein.
        void DrawWallSplashFx(Effect fx, float f)
        {
            V2 back = fx.Pos2 - fx.Pos;
            float bl = back.Length;
            V2 dir = bl > 0.01f ? back / bl : new V2(0f, -1f);
            float e = 1f - (1f - f) * (1f - f);
            Circle(fx.Pos, (4f + fx.Radius) * (1f - f), PulsePink.WithAlpha(0.8f * (1f - f)), 10);
            for (int k = 0; k < 6; k++)
            {
                float ang = Mathf.Atan2(dir.Y, dir.X) + (SprayHash(fx.Pos.X * 0.13f + k * 1.7f) - 0.5f) * 1.8f;
                float dist = (10f + 16f * SprayHash(fx.Pos.Y * 0.11f + k * 2.3f)) * e;
                V2 at = fx.Pos + V2.FromAngle(ang) * dist + new V2(0f, 6f * f * f);
                Circle(at, (1.2f + 1.3f * SprayHash(k + fx.Pos.X)) * (1f - f * 0.6f), BloodRed.WithAlpha(1f - f), 6);
            }
        }

        /// On the HUD: the drain over each tank laying a wall, in its stage's colour ("-4.0%/s").
        void DrawBloodWallHud()
        {
            if (game == null)
                return;
            foreach (var p in game.Players.Values)
            {
                if (!p.BloodWallOn || p.Downed)
                    continue;
                float pct = WallPct(p);
                V2 at = ToUi(p.Disp + new V2(0f, -p.Def.BodyR - 44f));  // (above the name tag)
                string text = "-" + pct.ToString(pct < 10f ? "0.0" : "0") + "%/s";
                Ui.Shadowed(new Rect(at.X - 60f, at.Y - 10f, 120f, 20f), text, 13, ToColor(WallTier[WallStage(pct)]));
            }
        }
    }
}
