using UnityEngine;

namespace Arena
{
    /// The Expanding arena's breaches (Core/Breaches.cs, Docs/RoomBuilding2.md step 1): the wall cracks open and a red
    /// holographic barrier stands in the gap, so it reads as "enemies come in here, you can't go out". It flickers up over
    /// the telegraph (scanning out from the middle), then hums: a translucent field with bright edges, scrolling scanlines,
    /// emitter posts at both ends, a glitch now and then, warning chevrons on the floor, and a flare when you walk up to it.
    public partial class ArenaApp
    {
        static readonly Rgba BreachRed = new Rgba(1f, 0.22f, 0.26f), BreachHot = new Rgba(1f, 0.55f, 0.5f), BreachDark = new Rgba(0.07f, 0.01f, 0.02f, 0.95f);

        /// Every breach of this wave, over the walls (after DrawLayout).
        void DrawBreaches()
        {
            var bs = game.ActiveBreaches;
            for (int i = 0; i < bs.Length; i++)
                DrawBreach(bs[i], i * 1.37f);
        }

        void DrawBreach(Breach b, float seed)
        {
            float t = game.AnimT, age = game.BreachAge, tele = Breaches.Telegraph;
            float open = Mathf.Clamp01(age / tele);
            float ease = 1f - (1f - open) * (1f - open);
            // While it comes up it flickers like a bad tube, settling as it opens.
            float flick = open < 1f ? (Mathf.Repeat(t * 23f + seed * 7f, 1f) < 0.35f + 0.6f * open ? 1f : 0.25f) : 1f;
            V2 al = b.Along, inw = b.In;
            float half = Breaches.Len / 2f, wt = Rooms.WallT / 2f;
            float near = float.MaxValue;
            foreach (var p in game.Players.Values)
                if (!p.Downed)
                    near = Mathf.Min(near, p.Disp.DistanceTo(b.Pos));
            float flare = Mathf.Clamp01(1f - (near - 60f) / 140f);  // brighter as you walk up to it
            float hum = 0.85f + 0.15f * Mathf.Sin(t * 5f + seed);
            float a = flick * hum;

            // The crack: the wall torn open (dark), its broken ends jagged and glowing.
            V2 c = b.Pos;
            SetColor(BreachDark);
            Quad(c - al * half - inw * wt, c + al * half - inw * wt, c + al * half + inw * wt, c - al * half + inw * wt);
            for (int side = -1; side <= 1; side += 2)
            {
                V2 end = c + al * (half * side);
                for (int k = 0; k < 3; k++)
                {
                    float off = (k - 1) * wt * 0.7f, jag = (k % 2 == 0 ? 7f : -5f) * side;
                    Line(end + inw * off, end + inw * off - al * jag, 2f, BreachRed.WithAlpha(0.7f * ease));
                }
            }

            // The field: scans out from the middle as it opens.
            float h = half * ease;
            float depth = 10f + 4f * flare;
            V2 a0 = c - al * h - inw * depth, a1 = c + al * h - inw * depth, a2 = c + al * h + inw * depth, a3 = c - al * h + inw * depth;
            float glow = depth * 2.6f;  // a soft halo round the field
            SetColor(BreachRed.WithAlpha((0.05f + 0.06f * flare) * a));
            Quad(c - al * (h + 10f) - inw * glow, c + al * (h + 10f) - inw * glow, c + al * (h + 10f) + inw * glow, c - al * (h + 10f) + inw * glow);
            SetColor(BreachRed.WithAlpha((0.16f + 0.14f * flare) * a));
            Quad(a0, a1, a2, a3);
            SetColor(BreachRed.WithAlpha((0.1f + 0.1f * flare) * a));
            Quad(c - al * h - inw * (depth * 0.4f), c + al * h - inw * (depth * 0.4f), c + al * h + inw * (depth * 0.4f), c - al * h + inw * (depth * 0.4f));
            // Scanlines running along it.
            const float gap = 11f;
            float scroll = Mathf.Repeat(t * 40f + seed * 13f, gap);
            for (float u = -h + scroll; u < h; u += gap)
                Line(c + al * u - inw * depth, c + al * u + inw * depth, 1.2f, BreachHot.WithAlpha(0.22f * a));
            // A glitch: now and then a slice of the field jumps sideways for a moment.
            float g = Mathf.Repeat(t * 0.7f + seed, 1f);
            if (open >= 1f && g < 0.06f)
            {
                float gu = Mathf.Lerp(-h * 0.6f, h * 0.6f, Mathf.Repeat(seed * 3.1f + Mathf.Floor(t * 0.7f + seed) * 0.37f, 1f));
                V2 gc = c + al * (gu + 6f);
                Line(gc - inw * depth, gc + inw * depth, 9f, BreachHot.WithAlpha(0.35f));
            }
            // Bright edges, both faces of the field.
            Line(a0, a1, 2.2f, BreachRed.WithAlpha((0.75f + 0.25f * flare) * a));
            Line(a3, a2, 2.2f, BreachRed.WithAlpha((0.75f + 0.25f * flare) * a));
            Line(c - al * h, c + al * h, 1f, BreachHot.WithAlpha(0.5f * a));
            // The emitter posts at both ends.
            for (int side = -1; side <= 1; side += 2)
            {
                V2 post = c + al * ((half + 4f) * side);
                Circle(post, 10f + 3f * flare, BreachRed.WithAlpha(0.18f * ease), 14);
                FillRect(post.X - 4f, post.Y - 4f, 8f, 8f, BreachHot.WithAlpha(0.95f * ease));
            }
            // A ripple across it when someone walks up.
            if (flare > 0f)
            {
                float rp = Mathf.Repeat(t * 1.6f + seed, 1f);
                Line(c + al * (-h + 2f * h * rp) - inw * depth, c + al * (-h + 2f * h * rp) + inw * depth, 4f, BreachHot.WithAlpha(0.6f * flare * (1f - rp) * ease));
            }

            // On the floor inside: chevrons pointing in (this is where they come from), pulsing.
            for (int k = 0; k < 3; k++)
            {
                float pulse = Mathf.Repeat(t * 1.2f - k * 0.18f + seed, 1f);
                V2 at = c + inw * (34f + k * 16f);
                var col = BreachRed.WithAlpha((0.55f - 0.15f * k) * (0.5f + 0.5f * (1f - pulse)) * ease);
                Line(at - inw * 7f - al * 12f, at, 2.5f, col);
                Line(at - inw * 7f + al * 12f, at, 2.5f, col);
            }
        }
    }
}
