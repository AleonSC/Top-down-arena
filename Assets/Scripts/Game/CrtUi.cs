using UnityEngine;

namespace Arena
{
    /// The blue-phosphor CRT look for a run card (look A, "Phosphor", of Docs/Mockups/crt-run-card.html): a dark blue screen,
    /// glowing text, scanlines, a vignette, a slow rolling bar and a rare flicker. Draw Begin, then the content (Text, Button, Ui.Fill),
    /// then End, which lays the scanlines and glass over it. calm (Reduced effects) drops the glow, the rolling bar and the flicker
    /// and keeps the colours with static scanlines and vignette.
    public static class CrtUi
    {
        // The colours and the CRT colour setting (Settings > Visual effects) live in CrtStyle.cs; these names are kept for the many
        // screens that use them.
        public static float Hue
        {
            get => CrtStyle.Hue;
            set => CrtStyle.Hue = value;
        }
        static Color Bezel => CrtStyle.Bezel;

        /// A colour drawn for the blue theme, moved to the chosen phosphor (CrtStyle.T).
        public static Color T(Color c) => CrtStyle.T(c);

        /// The dark blue screen, with its bezel and a thin phosphor rim.
        public static void Begin(Rect r) => Begin(r, 1f);

        /// The same screen with its bezel and background at `alpha` (a see-through tube, e.g. the minimized Ready strip).
        /// The see-through screen with `holes` left bare (keys in one row, left to right, sharing a top and bottom): the arena shows through them.
        public static void Begin(Rect r, float alpha, Rect[] holes)
        {
            if (holes == null || holes.Length == 0)
            {
                Begin(r, alpha);
                return;
            }
            var bz = Bezel.ScaleAlpha(alpha);
            var bg = CrtStyle.Bg.ScaleAlpha(alpha);
            System.Action<Rect> both = q => { if (q.width > 0f && q.height > 0f) { Ui.Fill(q, bz); Ui.Fill(q, bg); } };
            Ui.Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, 3f), bz);  // the bezel ring
            Ui.Fill(new Rect(r.x - 3f, r.yMax, r.width + 6f, 3f), bz);
            Ui.Fill(new Rect(r.x - 3f, r.y, 3f, r.height), bz);
            Ui.Fill(new Rect(r.xMax, r.y, 3f, r.height), bz);
            float top = holes[0].y, bot = holes[0].yMax, x = r.x;
            both(new Rect(r.x, r.y, r.width, top - r.y));
            both(new Rect(r.x, bot, r.width, r.yMax - bot));
            foreach (var h in holes)
            {
                both(new Rect(x, top, h.x - x, bot - top));
                x = h.xMax;
            }
            both(new Rect(x, top, r.xMax - x, bot - top));
            CrtKit.Frame(r, 1f, CrtStyle.Ink.WithAlpha(0.35f));
        }

        public static void Begin(Rect r, float alpha)
        {
            Ui.Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), Bezel.ScaleAlpha(alpha));
            Ui.Fill(r, CrtStyle.Bg.ScaleAlpha(alpha));
            CrtKit.Frame(r, 1f, CrtStyle.Ink.WithAlpha(0.35f));
        }

        /// A big screen title (the cabinet's header, a window's heading) in the CRT font, glowing. `size` is in pixels, as in the mockups.
        public static void Title(Rect r, string s, int size, Color c, TextAnchor align, bool calm)
        {
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            Text(r, s, size, c, align, false, calm);
            Ui.CrtFont = was;
        }

        /// How wide Title draws this text.
        public static float TitleWidth(string s, int size)
        {
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            float w = Ui.TextWidth(s, size, false);
            Ui.CrtFont = was;
            return w;
        }

        /// A label that glows: four faint copies a pixel around it, then the text.
        public static void Text(Rect r, string s, int size, Color c, TextAnchor align, bool bold, bool calm, bool wrap = false)
        {
            if (!calm && Event.current.type == EventType.Repaint)
            {
                var g = c.ScaleAlpha(CrtStyle.TextGlow);
                Ui.Label(new Rect(r.x - 1f, r.y, r.width, r.height), s, size, g, align, bold, wrap);
                Ui.Label(new Rect(r.x + 1f, r.y, r.width, r.height), s, size, g, align, bold, wrap);
                Ui.Label(new Rect(r.x, r.y - 1f, r.width, r.height), s, size, g, align, bold, wrap);
                Ui.Label(new Rect(r.x, r.y + 1f, r.width, r.height), s, size, g, align, bold, wrap);
            }
            Ui.Label(r, s, size, c, align, bold, wrap);
        }

        /// Scanlines, vignette and (unless calm) the rolling bar and flicker, over everything drawn since Begin.
        public static void End(Rect r, float t, bool calm) => End(r, t, calm, 1f);

        /// The same glass with its scanlines and dark corners at `alpha` (a see-through tube, e.g. the minimized Ready strip).
        public static void End(Rect r, float t, bool calm, float alpha)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var line = new Color(0f, 0f, 0f, (calm ? 0.16f : 0.26f) * alpha);
            for (float y = r.y + 1f; y < r.yMax; y += 3f)
                Ui.Fill(new Rect(r.x, y, r.width, 1f), line);
            for (int i = 0; i < 8; i++)  // dark corners: strips a few pixels deep, fading inward
            {
                float k = 1f - i / 8f, d = i * 3f;
                var v = new Color(0f, 0f, 0f, 0.34f * k * k * alpha);
                Ui.Fill(new Rect(r.x, r.y + d, r.width, 3f), v);
                Ui.Fill(new Rect(r.x, r.yMax - d - 3f, r.width, 3f), v);
                Ui.Fill(new Rect(r.x + d, r.y, 3f, r.height), v);
                Ui.Fill(new Rect(r.xMax - d - 3f, r.y, 3f, r.height), v);
            }
            if (calm)
                return;
            float p = (t / 7f) % 1f;  // the bar: a third of the screen tall, falls from above to below every 7 s
            float bh = r.height * 0.34f, by = Mathf.Lerp(r.y - bh, r.yMax, p);
            for (int i = 0; i < 8; i++)
            {
                float y0 = Mathf.Max(by + bh * i / 8f, r.y), y1 = Mathf.Min(by + bh * (i + 1) / 8f + 1f, r.yMax);
                if (y1 > y0)
                    Ui.Fill(new Rect(r.x, y0, r.width, y1 - y0), new Color(0.75f, 0.85f, 1f, Mathf.Sin((i + 0.5f) / 8f * Mathf.PI) * 0.05f));
            }
            if (Mathf.FloorToInt(t * 12f) % 41 == 0)  // a quick dip in brightness now and then
                Ui.Fill(r, new Color(0f, 0f, 0f, 0.12f));
        }
    }
}
