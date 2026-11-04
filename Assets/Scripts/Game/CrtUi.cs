using UnityEngine;

namespace Arena
{
    /// The green-phosphor CRT look for a run card (look A, "Phosphor", of Docs/Mockups/crt-run-card.html): a dark green screen,
    /// glowing text, scanlines, a vignette, a slow rolling bar and a rare flicker. Draw Begin, then the content (Text, Button, Ui.Fill),
    /// then End, which lays the scanlines and glass over it. calm (Reduced effects) drops the glow, the rolling bar and the flicker
    /// and keeps the colours with static scanlines and vignette.
    public static class CrtUi
    {
        public static readonly Color Ink = new Color(0.30f, 1f, 0.60f);
        public static readonly Color Dim = new Color(0.18f, 0.65f, 0.42f);
        public static readonly Color Bg = new Color(0.012f, 0.07f, 0.04f);
        static readonly Color Bezel = new Color(0.05f, 0.11f, 0.08f);

        /// The dark green screen, with its bezel and a thin phosphor rim.
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

        /// A label that glows: four faint copies a pixel around it, then the text.
        public static void Text(Rect r, string s, int size, Color c, TextAnchor align, bool bold, bool calm)
        {
            if (!calm && Event.current.type == EventType.Repaint)
            {
                var g = new Color(c.r, c.g, c.b, c.a * 0.16f);
                Ui.Label(new Rect(r.x - 1f, r.y, r.width, r.height), s, size, g, align, bold);
                Ui.Label(new Rect(r.x + 1f, r.y, r.width, r.height), s, size, g, align, bold);
                Ui.Label(new Rect(r.x, r.y - 1f, r.width, r.height), s, size, g, align, bold);
                Ui.Label(new Rect(r.x, r.y + 1f, r.width, r.height), s, size, g, align, bold);
            }
            Ui.Label(r, s, size, c, align, bold);
        }

        /// An outlined button that flips to inverse video (green block, dark text) under the mouse. True when clicked.
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
                    Ui.Fill(new Rect(r.x, y0, r.width, y1 - y0), new Color(0.7f, 1f, 0.8f, Mathf.Sin((i + 0.5f) / 8f * Mathf.PI) * 0.05f));
            }
            if (Mathf.FloorToInt(t * 12f) % 41 == 0)  // a quick dip in brightness now and then
                Ui.Fill(r, new Color(0f, 0f, 0f, 0.12f));
        }
    }
}
