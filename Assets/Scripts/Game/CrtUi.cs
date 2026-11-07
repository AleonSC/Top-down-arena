using UnityEngine;

namespace Arena
{
    /// The blue-phosphor CRT look for a run card (look A, "Phosphor", of Docs/Mockups/crt-run-card.html): a dark blue screen,
    /// glowing text, scanlines, a vignette, a slow rolling bar and a rare flicker. Draw Begin, then the content (Text, Button, Ui.Fill),
    /// then End, which lays the scanlines and glass over it. calm (Reduced effects) drops the glow, the rolling bar and the flicker
    /// and keeps the colours with static scanlines and vignette.
    public static class CrtUi
    {
        /// The phosphor colour (Settings > Visual effects > CRT colour). 0 is the original blue.
        public static readonly string[] ThemeNames = { "Blue", "Green", "Amber", "Red", "Violet", "White" };
        static readonly Color[] ThemeInk = { new Color(0.40f, 0.64f, 1f), new Color(0.35f, 0.95f, 0.5f), new Color(1f, 0.72f, 0.25f), new Color(1f, 0.42f, 0.42f), new Color(0.72f, 0.5f, 1f), new Color(0.88f, 0.92f, 0.96f) };
        static int theme;

        public static int Theme
        {
            get => theme;
            set => theme = Mathf.Clamp(value, 0, ThemeInk.Length - 1);
        }

        public static Color Ink => ThemeInk[theme];
        public static Color Dim => Color.Lerp(Ink, new Color(0.05f, 0.08f, 0.14f), 0.38f);
        public static Color Bg => theme == 0 ? new Color(0.012f, 0.03f, 0.085f) : Tinted(0.045f, 0.12f);
        static Color Bezel => theme == 0 ? new Color(0.05f, 0.07f, 0.14f) : Tinted(0.1f, 0.2f);

        /// A very dark screen with a breath of the phosphor colour in it.
        static Color Tinted(float darkness, float tint)
        {
            var i = Ink;
            return new Color(darkness * 0.3f + i.r * tint * 0.25f, darkness * 0.3f + i.g * tint * 0.25f, darkness * 0.3f + i.b * tint * 0.25f);
        }

        /// The dark blue screen, with its bezel and a thin phosphor rim.
        public static void Begin(Rect r)
        {
            Ui.Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), Bezel);
            Ui.Fill(r, Bg);
            Edge(r, 1f, new Color(Ink.r, Ink.g, Ink.b, 0.35f));
        }

        static void Edge(Rect r, float w, Color c)
        {
            Ui.Fill(new Rect(r.x, r.y, r.width, w), c);
            Ui.Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Ui.Fill(new Rect(r.x, r.y, w, r.height), c);
            Ui.Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
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
            Edge(r, 2f, Ink);
        }

        /// A label that glows: four faint copies a pixel around it, then the text.
        public static void Text(Rect r, string s, int size, Color c, TextAnchor align, bool bold, bool calm, bool wrap = false)
        {
            if (!calm && Event.current.type == EventType.Repaint)
            {
                var g = new Color(c.r, c.g, c.b, c.a * 0.16f);
                Ui.Label(new Rect(r.x - 1f, r.y, r.width, r.height), s, size, g, align, bold, wrap);
                Ui.Label(new Rect(r.x + 1f, r.y, r.width, r.height), s, size, g, align, bold, wrap);
                Ui.Label(new Rect(r.x, r.y - 1f, r.width, r.height), s, size, g, align, bold, wrap);
                Ui.Label(new Rect(r.x, r.y + 1f, r.width, r.height), s, size, g, align, bold, wrap);
            }
            Ui.Label(r, s, size, c, align, bold, wrap);
        }

        /// An outlined button that flips to inverse video (blue block, dark text) under the mouse. True when clicked.
        public static bool Button(Rect r, string text, Texture2D icon, int size, bool calm)
        {
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            Ui.NoteHover(r);
            Ui.Fill(r, hover ? new Color(Ink.r, Ink.g, Ink.b, 0.95f) : new Color(Ink.r, Ink.g, Ink.b, 0.07f));
            Edge(r, 2f, Ink);
            Color tc = hover ? Bg : Ink;
            float isz = Mathf.Min(size * 1.0f, r.height * 0.45f);
            float tw = Ui.TextWidth(text, size, true) - 6f, gap = icon != null ? 10f : 0f, ic = icon != null ? isz : 0f;
            float x = r.center.x - (ic + gap + tw) / 2f;
            if (icon != null)
                Ui.Icon(new Rect(x, r.center.y - isz / 2f, isz, isz), icon, tc);
            var tr = new Rect(x + ic + gap, r.y, tw + 12f, r.height);
            if (hover)
                Ui.Label(tr, text, size, tc, TextAnchor.MiddleLeft, true);
            else
                Text(tr, text, size, tc, TextAnchor.MiddleLeft, true, calm);
            return Ui.HitArea(r);
        }

        static readonly Color Danger = new Color(1f, 0.42f, 0.48f);

        /// A small outlined button (pager arrows, Back, row buttons). danger: red, for delete; lit: already filled (a delete waiting for its second click).
        public static bool Small(Rect r, string text, Texture2D icon, int size, bool calm, bool danger = false, bool lit = false)
        {
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            Ui.NoteHover(r);
            Color col = danger ? Danger : Ink, dark = danger ? new Color(0.1f, 0.02f, 0.03f) : Bg;
            bool fill = hover || lit;
            Ui.Fill(r, fill ? new Color(col.r, col.g, col.b, 0.95f) : new Color(col.r, col.g, col.b, 0.07f));
            Edge(r, 2f, col);
            Color tc = fill ? dark : col;
            float isz = Mathf.Min(size * 1.0f, r.height * 0.5f);
            float tw = text.Length == 0 ? 0f : Ui.TextWidth(text, size, true) - 6f, gap = icon != null && text.Length > 0 ? 6f : 0f, ic = icon != null ? isz : 0f;
            float x = r.center.x - (ic + gap + tw) / 2f;
            if (icon != null)
                Ui.Icon(new Rect(x, r.center.y - isz / 2f, isz, isz), icon, tc);
            if (text.Length > 0)
            {
                var tr = new Rect(x + ic + gap, r.y, tw + 12f, r.height);
                if (fill)
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
