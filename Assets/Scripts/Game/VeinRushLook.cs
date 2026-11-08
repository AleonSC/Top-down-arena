using UnityEngine;

namespace Arena
{
    /// The DPS Hemomancer's Vein Rush (Core/VeinRush.cs), look B "Needle" of Docs/Mockups/vein-rush.html: a straight
    /// crimson streak with a white core is drawn from where you started to where you are, leaving fading afterimages of you
    /// every 34 px; each enemy cut gets a sharp slash across it and a white flash. Its trail of blood (ZoneKind.VeinTrail)
    /// is drawn by DrawVeinTrail.
    public partial class ArenaApp
    {
        /// The streak (FxKind.VeinRush): it grows with the rush, then fades with its afterimages.
        void DrawVeinRushFx(Effect fx, float t)
        {
            float rush = Game.VeinRushTime, age = fx.T;
            V2 from = fx.Pos, to = fx.Pos2, path = to - from;
            float len = path.Length;
            if (len < 1f)
                return;
            V2 dir = path / len;
            float grow = Mathf.Clamp01(age / rush), fade = age <= rush ? 1f : 1f - Mathf.Clamp01((age - rush) / Game.VeinRushFxTime);
            V2 head = from + path * grow;
            Player p;
            float body = game.Players.TryGetValue((int)fx.Radius, out p) ? p.Def.BodyR : 12f;
            // Afterimages: your silhouette left behind every 34 px, the older ones fainter.
            for (float d = 0f; d < len * grow; d += 34f)
            {
                float k = d / Mathf.Max(1f, len);
                float alpha = fade * (0.15f + 0.35f * k);
                Circle(from + dir * d, body * 0.9f, BloodRed.WithAlpha(alpha * 0.6f), 16);
                Arc(from + dir * d, body * 0.9f, 1.5f, new Rgba(1f, 0.6f, 0.6f, alpha), 16);
            }
            // The streak: dark edge, crimson body, white core, thinning as it fades.
            float w = 7f * fade;
            Line(from, head, w + 4f, BloodDark.WithAlpha(0.6f * fade));
            Line(from, head, w, BloodRed.WithAlpha(fade));
            Line(from + dir * (len * grow * 0.15f), head, Mathf.Max(0.5f, w * 0.3f), new Rgba(1f, 0.95f, 0.95f, fade));
            if (age <= rush)  // the needle's point
            {
                V2 s = dir.Orthogonal();
                SetColor(new Rgba(1f, 0.9f, 0.9f));
                Tri(head + dir * 10f, head - dir * 4f + s * 4f, head - dir * 4f - s * 4f);
            }
        }

        /// One enemy cut (FxKind.VeinCut): a sharp slash across it along the rush and a white flash.
        void DrawVeinCutFx(Effect fx, float f)
        {
            V2 dir = fx.Pos2.IsZero ? new V2(1f, 0f) : fx.Pos2.Normalized(), s = dir.Orthogonal();
            V2 slant = (dir + s * 0.35f).Normalized();
            float r = fx.Radius + 8f, g = Mathf.Clamp01(f * 3f);
            Line(fx.Pos - slant * r, fx.Pos - slant * r + slant * (2f * r * g), 3f * (1f - f), new Rgba(1f, 0.95f, 0.95f, 1f - f));
            Line(fx.Pos - slant * r, fx.Pos - slant * r + slant * (2f * r * g), 6f * (1f - f), BloodRed.WithAlpha(0.6f * (1f - f)));
            if (f < 0.25f)
                Circle(fx.Pos, fx.Radius * (1f + f), new Rgba(1f, 1f, 1f, 0.5f * (1f - f * 4f)), 16);
        }

        /// A Vein Rush trail pool (ZoneKind.VeinTrail): a dark smear of blood, glossy at the middle, drying out at its end.
        void DrawVeinTrail(Zone z, float t)
        {
            float fade = Mathf.Clamp01(z.T / 0.6f), grow = Mathf.Clamp01((z.Duration - z.T) / 0.15f);
            float r = z.Radius * (0.6f + 0.4f * grow);
            Circle(z.Pos, r, BloodDark.WithAlpha(0.55f * fade), 14);
            Circle(z.Pos + new V2(Mathf.Sin(z.Id * 1.3f) * r * 0.3f, Mathf.Cos(z.Id * 2.1f) * r * 0.2f), r * 0.55f, BloodRed.WithAlpha(0.35f * fade), 10);
            Circle(z.Pos + new V2(-r * 0.25f, -r * 0.25f), r * 0.15f, new Rgba(1f, 0.7f, 0.7f, 0.35f * fade), 6);  // the gloss
        }
    }
}
