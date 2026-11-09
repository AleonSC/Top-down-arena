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
        public static string[] ThemeNames => CrtStyle.ThemeNames;
        public static int Theme
        {
            get => CrtStyle.Theme;
            set => CrtStyle.Theme = value;
        }
        public static Color Ink => CrtStyle.Ink;
        public static Color Dim => CrtStyle.Dim;
        public static Color Bg => CrtStyle.Bg;
        static Color Bezel => CrtStyle.Bezel;

        /// A colour drawn for the blue theme, moved to the chosen phosphor (CrtStyle.T).
        public static Color T(Color c) => CrtStyle.T(c);

        /// The dark blue screen, with its bezel and a thin phosphor rim.
        public static void Begin(Rect r)
        {
            Ui.Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), Bezel);
            Ui.Fill(r, Bg);
            CrtKit.Frame(r, 1f, new Color(Ink.r, Ink.g, Ink.b, 0.35f));
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

        /// An outlined, faintly lit square for a picture to sit in.
        public static void Tile(Rect r)
        {
            Ui.Fill(r, new Color(Ink.r, Ink.g, Ink.b, 0.07f));
            CrtKit.Frame(r, 2f, Ink);
        }

        /// A label that glows: four faint copies a pixel around it, then the text.
        public static void Text(Rect r, string s, int size, Color c, TextAnchor align, bool bold, bool calm, bool wrap = false)
        {
            if (!calm && Event.current.type == EventType.Repaint)
            {
                var g = new Color(c.r, c.g, c.b, c.a * CrtStyle.TextGlow);
                Ui.Label(new Rect(r.x - 1f, r.y, r.width, r.height), s, size, g, align, bold, wrap);
                Ui.Label(new Rect(r.x + 1f, r.y, r.width, r.height), s, size, g, align, bold, wrap);
                Ui.Label(new Rect(r.x, r.y - 1f, r.width, r.height), s, size, g, align, bold, wrap);
                Ui.Label(new Rect(r.x, r.y + 1f, r.width, r.height), s, size, g, align, bold, wrap);
            }
            Ui.Label(r, s, size, c, align, bold, wrap);
        }

        /// A soft key with an optional icon: the Play picker's Continue and DEL (look B of Docs/Mockups/crt-play-picker-keys.html).
        /// Drawn like the lobby's keys (CrtKit.SoftBox / SoftText), never a solid block. lit: always shown selected (the latest
        /// run's Continue, a delete waiting for its second click). danger: red. quiet: dashed and dim until the mouse is on it
        /// (unless lit). True when pressed.
        public static bool SoftKey(Rect r, string text, Texture2D icon, int size, bool calm, bool lit = false, bool danger = false, bool quiet = false)
        {
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            Ui.NoteHover(r);
            Color c = CrtStyle.KeyColor(null, danger), tc;
            bool hushed = quiet && !hover && !lit;
            if (hushed)
            {
                Ui.Fill(r, new Color(c.r, c.g, c.b, CrtStyle.KeyQuietTint));
                CrtKit.Dashed(r, CrtStyle.KeyQuietDashW, new Color(c.r, c.g, c.b, CrtStyle.KeyQuietDash));
                tc = new Color(c.r, c.g, c.b, CrtStyle.KeyQuietText);
            }
            else
            {
                CrtKit.SoftBox(r, c, lit, hover, false);
                tc = CrtKit.SoftText(c, lit || hover);
            }
            float isz = Mathf.Min(size * 1.0f, r.height * 0.5f);
            float tw = text.Length == 0 ? 0f : Ui.TextWidth(text, size, true) - 6f, gap = icon != null && text.Length > 0 ? (size >= 18 ? 10f : 6f) : 0f, ic = icon != null ? isz : 0f;
            float x = r.center.x - (ic + gap + tw) / 2f;
            if (icon != null)
                Ui.Icon(new Rect(x, r.center.y - isz / 2f, isz, isz), icon, tc);
            if (text.Length > 0)
            {
                var tr = new Rect(x + ic + gap, r.y, tw + 12f, r.height);
                if (hover || lit)
                    Ui.Label(tr, text, size, tc, TextAnchor.MiddleLeft, true);
                else
                    Text(tr, text, size, tc, TextAnchor.MiddleLeft, true, calm);
            }
            return Ui.HitArea(r);
        }

        /// Scanlines, vignette and (unless calm) the rolling bar and flicker, over everything drawn since Begin.
        public static void End(Rect r, float t, bool calm)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var line = new Color(0f, 0f, 0f, calm ? 0.16f : 0.26f);
            for (float y = r.y + 1f; y < r.yMax; y += 3f)
                Ui.Fill(new Rect(r.x, y, r.width, 1f), line);
            for (int i = 0; i < 8; i++)  // dark corners: strips a few pixels deep, fading inward
            {
                float k = 1f - i / 8f, d = i * 3f;
                var v = new Color(0f, 0f, 0f, 0.34f * k * k);
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
