using UnityEngine;

namespace Arena
{
    /// Hologram looks for the lobby's hover tooltips (candidates; `holoStyle` 0 = off, the game's current soft glass).
    /// The tip floats above the hovered button (below if there's no room, else beside it) and is projected from it:
    /// a bright emitter line on the button's edge and a beam up to the slab.
    /// 1 Projection: a see-through cyan slab, scanlines, a slow bright band, bracket corners, a filled beam.
    /// 2 Glitch: like 1, but the title has cyan and magenta ghosts, a strip of the slab slips sideways, and it flickers now and then.
    /// 3 Wireframe: barely any fill; a grid, a glowing top rail, ruler ticks and an outlined beam.
    public partial class ArenaApp
    {
        int holoStyle;            // 0 off; 1..3 see above
        float holoClock = -1f;    // preview override for the animation clock (seconds); < 0 = real time
        bool holoUp = true;       // the slab sits above its button (the beam runs down to it); false: below it
        bool holoSide;            // no room above or below: beside the button, no beam

        static readonly Color HoloCyan = new Color(0.18f, 0.86f, 1f);

        float HoloT => holoClock >= 0f ? holoClock : Time.unscaledTime;

        /// Where the hologram slab goes: centred over the button with a gap for the beam.
        Rect HoloPlace(Rect anchor, float pw, float h)
        {
            const float gap = 26f;
            holoSide = false;
            float x = Mathf.Clamp(anchor.center.x - pw / 2f, 8f, W - 8f - pw);
            if (anchor.y - gap - h >= 8f)
            {
                holoUp = true;
                return new Rect(x, anchor.y - gap - h, pw, h);
            }
            if (anchor.yMax + gap + h <= H - 8f)
            {
                holoUp = false;
                return new Rect(x, anchor.yMax + gap, pw, h);
            }
            holoSide = true;
            return new Rect(anchor.xMax + 10f + pw > W - 8f ? anchor.x - 10f - pw : anchor.xMax + 10f, Mathf.Clamp(anchor.y - 6f, 8f, H - 8f - h), pw, h);
        }

        /// The tint: the hologram's cyan with a little of the tip's own colour (so DPS still leans orange, Healer green).
        static Color HoloTint(Color accent) => Color.Lerp(HoloCyan, accent, 0.08f);

        /// Draws the chrome behind the tip text (DrawTip draws the title and lines after this).
        void DrawHoloBack(Rect pop, Rect anchor, Color accent)
        {
            float t = HoloT;
            var c = HoloTint(accent);
            float flick = holoStyle == 2 && Mathf.Repeat(t, 2.3f) > 2.2f ? 0.5f : 1f;
            float k = (0.93f + 0.07f * Mathf.Sin(t * 5.3f)) * flick;

            if (!holoSide)
            {
                // the emitter: a bright line along the button's edge that faces the slab, with a faint halo
                float ey = holoUp ? anchor.y - 1f : anchor.yMax - 1f;
                Ui.Fill(new Rect(anchor.x, ey, anchor.width, 2f), new Color(c.r, c.g, c.b, 0.95f * k));
                for (int i = 1; i <= 4; i++)
                    Ui.Fill(new Rect(anchor.x, holoUp ? ey - i * 2f : ey + 2f + (i - 1) * 2f, anchor.width, 2f), new Color(c.r, c.g, c.b, 0.22f * k * (1f - i / 5f)));

                // the beam: from the button's edge out to the slab, widening, drawn as 2 px rows
                float yA = holoUp ? anchor.y : anchor.yMax, yB = holoUp ? pop.yMax : pop.y;
                float lo = Mathf.Min(yA, yB), hi = Mathf.Max(yA, yB);
                for (float y = lo; y < hi; y += 2f)
                {
                    float f = (y - lo) / Mathf.Max(1f, hi - lo);
                    if (holoUp) f = 1f - f;  // 0 at the button, 1 at the slab
                    float cx = Mathf.Lerp(anchor.center.x, pop.center.x, f);
                    float w = Mathf.Lerp(anchor.width * 0.9f, pop.width * 0.92f, f);
                    var bar = new Rect(cx - w / 2f, y, w, 2f);
                    if (holoStyle == 3)  // wireframe: just the two edges of the cone
                    {
                        Ui.Fill(new Rect(bar.x, y, 1f, 2f), new Color(c.r, c.g, c.b, 0.55f * k));
                        Ui.Fill(new Rect(bar.xMax - 1f, y, 1f, 2f), new Color(c.r, c.g, c.b, 0.55f * k));
                    }
                    else
                    {
                        float a = 0.34f * (1f - f * 0.65f) * k * (0.8f + 0.2f * Mathf.Sin(y * 0.9f - t * 9f));  // a slow upward ripple
                        Ui.Fill(bar, new Color(c.r, c.g, c.b, a));
                    }
                }
            }

            // glow: stacked frames that fade out (Ui.Glow's texture isn't in the previews, and this is the same in Unity)
            for (int i = 1; i <= 6; i++)
                CrtKit.Frame(new Rect(pop.x - i * 2f, pop.y - i * 2f, pop.width + i * 4f, pop.height + i * 4f), 2f, new Color(c.r, c.g, c.b, 0.30f * k * (1f - i / 7f)));

            if (holoStyle == 3)
            {
                Ui.Fill(pop, new Color(0.01f, 0.05f, 0.09f, 0.82f));
                for (float gx = pop.x + 20f; gx < pop.xMax; gx += 20f)
                    Ui.Fill(new Rect(gx, pop.y, 1f, pop.height), new Color(c.r, c.g, c.b, 0.08f));
                for (float gy = pop.y + 20f; gy < pop.yMax; gy += 20f)
                    Ui.Fill(new Rect(pop.x, gy, pop.width, 1f), new Color(c.r, c.g, c.b, 0.08f));
                CrtKit.Frame(pop, 1f, new Color(c.r, c.g, c.b, 0.5f * k));
                Ui.Fill(new Rect(pop.x, pop.y, pop.width, 3f), new Color(c.r, c.g, c.b, 0.95f * k));
                for (float tx = pop.x; tx <= pop.xMax; tx += 10f)  // ruler ticks along the bottom edge
                    Ui.Fill(new Rect(tx, pop.yMax - (Mathf.Repeat(tx - pop.x, 50f) < 1f ? 7f : 4f), 1f, Mathf.Repeat(tx - pop.x, 50f) < 1f ? 7f : 4f), new Color(c.r, c.g, c.b, 0.7f * k));
            }
            else
            {
                Ui.Fill(pop, new Color(0.01f, 0.06f, 0.11f, 0.95f));
                // a vertical wash, stronger at the edge facing the button (that's where the light comes in)
                for (int i = 0; i < 10; i++)
                {
                    float f = i / 10f, y = holoUp ? pop.yMax - (i + 1f) * pop.height / 10f : pop.y + i * pop.height / 10f;
                    Ui.Fill(new Rect(pop.x, y, pop.width, pop.height / 10f + 1f), new Color(c.r, c.g, c.b, 0.30f * (1f - f) * k));
                }
                float crawl = Mathf.Repeat(t * 14f, 3f);
                for (float y = pop.y + crawl; y < pop.yMax; y += 3f)  // scanlines, crawling
                    Ui.Fill(new Rect(pop.x, y, pop.width, 1f), new Color(0f, 0.05f, 0.09f, 0.4f));
                float band = Mathf.Repeat(t * (holoStyle == 2 ? 0.5f : 0.25f), 1f);  // a bright band sweeping down
                float by = pop.y + band * (pop.height - 10f);
                Ui.Fill(new Rect(pop.x, by, pop.width, 10f), new Color(c.r, c.g, c.b, holoStyle == 2 ? 0.2f : 0.09f));
                Ui.Fill(new Rect(pop.x, by + 4f, pop.width, 1f), new Color(1f, 1f, 1f, holoStyle == 2 ? 0.3f : 0.14f));
                if (holoStyle == 2)  // a strip that slips sideways and back, now and then
                {
                    float ph = Mathf.Repeat(t * 1.7f, 4f);
                    if (ph < 0.25f)
                    {
                        float sy = pop.y + Mathf.Repeat(t * 37f, pop.height - 12f), off = (ph < 0.12f ? 6f : -4f);
                        Ui.Fill(new Rect(pop.x + off, sy, pop.width, 8f), new Color(c.r, c.g, c.b, 0.22f));
                        Ui.Fill(new Rect(pop.x + off, sy, pop.width, 1f), new Color(1f, 1f, 1f, 0.4f));
                    }
                }
                CrtKit.Frame(pop, 1f, new Color(c.r, c.g, c.b, 0.95f * k));
                Ui.Fill(new Rect(pop.x, holoUp ? pop.yMax - 2f : pop.y, pop.width, 2f), new Color(c.r, c.g, c.b, 0.95f * k));  // the bright rim on the beam side
            }
            HoloCorners(pop, c, k);
        }

        static void HoloCorners(Rect r, Color c, float k)
        {
            var col = new Color(c.r, c.g, c.b, 1f * k);
            const float L = 14f, w = 2f;
            Ui.Fill(new Rect(r.x - 3f, r.y - 3f, L, w), col); Ui.Fill(new Rect(r.x - 3f, r.y - 3f, w, L), col);
            Ui.Fill(new Rect(r.xMax - L + 3f, r.y - 3f, L, w), col); Ui.Fill(new Rect(r.xMax + 1f, r.y - 3f, w, L), col);
            Ui.Fill(new Rect(r.x - 3f, r.yMax + 1f, L, w), col); Ui.Fill(new Rect(r.x - 3f, r.yMax - L + 3f, w, L), col);
            Ui.Fill(new Rect(r.xMax - L + 3f, r.yMax + 1f, L, w), col); Ui.Fill(new Rect(r.xMax + 1f, r.yMax - L + 3f, w, L), col);
        }

        /// The title in the hologram look: near-white cyan (the glitch style adds offset cyan and magenta ghosts behind it).
        void DrawHoloTitle(Rect r, string title, Color accent)
        {
            var c = HoloTint(accent);
            string s = title.ToUpperInvariant();
            if (holoStyle == 2)
            {
                float j = Mathf.Repeat(HoloT, 2.3f) > 2.2f ? 3.5f : 1.5f;
                Ui.Label(new Rect(r.x - j, r.y, r.width, r.height), s, 16, new Color(1f, 0.2f, 0.7f, 0.6f), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + j, r.y, r.width, r.height), s, 16, new Color(0.2f, 0.95f, 1f, 0.6f), TextAnchor.MiddleLeft, true);
            }
            Ui.Label(r, s, 16, Color.Lerp(c, Color.white, 0.65f), TextAnchor.MiddleLeft, true);
            Ui.Fill(new Rect(r.x - 8f, r.y + 4f, 3f, r.height - 8f), accent);  // the tip's own colour, as a marker
        }
    }
}
