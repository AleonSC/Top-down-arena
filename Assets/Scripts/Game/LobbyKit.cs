using UnityEngine;

namespace Arena
{
    /// Lobby-only pieces of the digital CRT lobby (Docs/Mockups/crt-lobby-digital.html): keys that fit their text (a stacked
    /// icon over the name, long names shrinking or wrapping), the locked slot, section headings, number tiles, the stat bar,
    /// and the screen panel with its corner brackets. Everything uses CrtUi's blue phosphor; colour only comes from icons,
    /// stat bars and the tiles' gold. CrtKit has the shared key; this adds the variants the loadout needs.
    public static class LobbyKit
    {
        public static readonly Color Gold = new Color(1f, 0.83f, 0.42f);
        public static readonly Color Soft = new Color(0.76f, 0.80f, 0.91f);  // body text on the screen

        public static Color Ink(float a) => new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, a);

        /// The text cut short with "…" so it fits `width` at this size.
        public static string Truncate(string text, int size, float width, bool bold = true)
        {
            if (Ui.TextWidth(text, size, bold) <= width)
                return text;
            int n = text.Length;
            while (n > 1 && Ui.TextWidth(text.Substring(0, n).TrimEnd() + "…", size, bold) > width)
                n--;
            return text.Substring(0, n).TrimEnd() + "…";
        }

        /// The biggest size (down to min) at which the text is one line of `width`.
        public static int FitSize(string text, int size, int min, float width, bool bold = true)
        {
            while (size > min && Ui.TextWidth(text, size, bold) > width)
                size--;
            return size;
        }

        /// One line of text that always fits: smaller first (down to min), then cut short.
        public static void Fit(Rect r, string text, int size, int min, Color c, TextAnchor align, bool calm, bool glow = true)
        {
            int s = FitSize(text, size, min, r.width);
            text = Truncate(text, s, r.width);
            if (glow)
                CrtUi.Text(r, text, s, c, align, true, calm);
            else
                Ui.Label(r, text, s, c, align, true);
        }

        /// A digital key that fits its text: inverse video under the mouse or when `on`. An icon sits beside the name
        /// (centred as a group) or, with `stack`, above it. Long names shrink to `minSize`, then wrap onto a second line when
        /// the key is tall enough, then are cut. enabled = false: shown (lit when `on`) but never hot and never clicked
        /// (guests looking at the host's choices). Returns true on a click.
        public static bool Key(Rect r, string text, bool on, bool calm, Texture2D icon = null, bool stack = false, bool colorIcon = false,
            int size = 18, int minSize = 11, bool danger = false, bool enabled = true, Color? accent = null, string sub = null, bool locked = false)
        {
            if (locked)
            {
                // A key that can't be used yet (Start before everyone is ready): dim and dashed, with its text.
                var d = CrtUi.Dim;
                Ui.Fill(r, new Color(d.r, d.g, d.b, 0.03f));
                CrtKit.Dashed(r, 2f, new Color(d.r, d.g, d.b, 0.7f));
                Draw(r, text, icon, stack, colorIcon, size, minSize, d, false, calm, sub, true);
                return false;
            }
            bool hover = enabled && GUI.enabled && r.Contains(Event.current.mousePosition);
            Color c = danger ? new Color(1f, 0.42f, 0.48f) : accent ?? CrtUi.Ink;
            Color dark = danger ? new Color(0.1f, 0.02f, 0.03f) : CrtUi.Bg;
            bool fill = hover || on;
            Ui.Fill(r, fill ? new Color(c.r, c.g, c.b, 0.95f) : new Color(c.r, c.g, c.b, 0.08f));
            CrtKit.Frame(r, 2f, c);
            Color tc = fill ? dark : c;
            Draw(r, text, icon, stack, colorIcon, size, minSize, tc, fill, calm, sub, false);
            if (!enabled)
                return false;
            return Ui.HitArea(r);
        }

        static void Draw(Rect r, string text, Texture2D icon, bool stack, bool colorIcon, int size, int minSize, Color tc, bool fill, bool calm, string sub, bool dim)
        {
            const float pad = 8f;
            if (!string.IsNullOrEmpty(sub))  // two lines, centred: the name, then a small line under it
            {
                float nh = size + 6f, y0 = r.center.y - (nh + 14f) / 2f;
                var nr = new Rect(r.x + 6f, y0, r.width - 12f, nh);
                int s2 = FitSize(text, size, minSize, nr.width);
                Line(nr, Truncate(text, s2, nr.width), s2, tc, TextAnchor.MiddleCenter, fill, calm);
                int ss = FitSize(sub, 12, 9, nr.width, false);
                Ui.Label(new Rect(nr.x, y0 + nh - 1f, nr.width, 15f), Truncate(sub, ss, nr.width, false), ss, new Color(tc.r, tc.g, tc.b, fill ? 0.85f : 0.8f), TextAnchor.MiddleCenter, false);
                return;
            }
            if (icon != null && stack)
            {
                float isz = Mathf.Min(r.height * 0.44f, 22f);
                Ui.Icon(new Rect(r.center.x - isz / 2f, r.y + 4f, isz, isz), icon, colorIcon ? Color.white : tc);
                var tr = new Rect(r.x + 3f, r.y + 4f + isz, r.width - 6f, r.yMax - (r.y + 4f + isz) - 1f);
                int s = FitSize(text, size, minSize, tr.width);
                text = Truncate(text, s, tr.width);
                Line(tr, text, s, tc, TextAnchor.MiddleCenter, fill, calm);
                return;
            }
            float ic = 0f, gap = text.Length > 0 ? 6f : 0f;
            if (icon != null)
                ic = Mathf.Min(r.height - 12f, size + 4f);
            float avail = r.width - 2f * pad - (ic > 0f ? ic + gap : 0f);
            // Shrink a little first; if it still isn't one line, wrap onto a second line (a key tall enough) before shrinking further.
            int fs = FitSize(text, size, Mathf.Max(minSize, size - 4), avail);
            bool wrap = ic == 0f && Ui.TextWidth(text, fs, true) > avail && Ui.TextHeight(text, fs, avail, true) <= r.height - 2f;
            if (!wrap)
                fs = FitSize(text, size, minSize, avail);
            if (wrap)
            {
                var wr = new Rect(r.x + pad, r.y, r.width - 2f * pad, r.height);
                if (fill)
                    Ui.Label(wr, text, fs, tc, TextAnchor.MiddleCenter, true, true);
                else
                    CrtUi.Text(wr, text, fs, tc, TextAnchor.MiddleCenter, true, calm, true);
                return;
            }
            text = Truncate(text, fs, avail);
            float tw = text.Length == 0 ? 0f : Mathf.Min(avail, Ui.TextWidth(text, fs, true) - 6f);
            float x = r.center.x - (ic + (ic > 0f ? gap : 0f) + tw) / 2f;
            if (icon != null)
            {
                Ui.Icon(new Rect(x, r.center.y - ic / 2f, ic, ic), icon, colorIcon ? new Color(1f, 1f, 1f, dim ? 0.5f : 1f) : tc);
                x += ic + gap;
            }
            Line(new Rect(x - 2f, r.y, tw + 12f, r.height), text, fs, tc, TextAnchor.MiddleLeft, fill, calm);
        }

        static void Line(Rect r, string text, int size, Color tc, TextAnchor a, bool fill, bool calm)
        {
            if (fill)
                Ui.Label(r, text, size, tc, a, true);
            else
                CrtUi.Text(r, text, size, tc, a, true, calm);
        }

        /// A locked slot: dim, dashed, the real padlock and "LOCKED" (stacked above it in `stack`). True while the mouse is on it
        /// (the caller shows the tip).
        public static bool Locked(Rect r, bool calm, bool stack, int size = 14)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Ui.NoteHover(r);
            var c = CrtUi.Dim;
            Ui.Fill(r, new Color(c.r, c.g, c.b, 0.03f));
            CrtKit.Dashed(r, 2f, new Color(c.r, c.g, c.b, hover ? 0.95f : 0.7f));
            var ic = PixelArt.Get("ui_lock");
            var tint = new Color(1f, 1f, 1f, hover ? 0.7f : 0.5f);
            string label = "LOCKED";
            if (stack)
            {
                float isz = Mathf.Min(r.height * 0.42f, 18f);
                Ui.Icon(new Rect(r.center.x - isz / 2f, r.y + 4f, isz, isz), ic, tint);
                Ui.Label(new Rect(r.x, r.y + 4f + isz, r.width, r.yMax - (r.y + 4f + isz) - 1f), label, Mathf.Min(size, 13), c, TextAnchor.MiddleCenter, true);
            }
            else
            {
                float isz = Mathf.Min(r.height - 12f, size + 6f), tw = Ui.TextWidth(label, size, true) - 6f;
                float x = r.center.x - (isz + 8f + tw) / 2f;
                Ui.Icon(new Rect(x, r.center.y - isz / 2f, isz, isz), ic, tint);
                Ui.Label(new Rect(x + isz + 8f - 2f, r.y, tw + 12f, r.height), label, size, c, TextAnchor.MiddleLeft, true);
            }
            return hover;
        }

        /// A section heading: the name on the left, a short caption on the right (shrunk to fit), a thin rule under both.
        public static void Heading(float x, float y, float w, string title, string caption, bool calm, int size = 18)
        {
            string t = title.ToUpperInvariant();
            float tw = Ui.TextWidth(t, size, true) - 4f;
            CrtUi.Text(new Rect(x, y, tw + 12f, 26f), t, size, CrtUi.Ink, TextAnchor.MiddleLeft, true, calm);
            if (!string.IsNullOrEmpty(caption))
            {
                float left = tw + 16f;
                var cr = new Rect(x + left, y + 2f, w - left, 24f);
                string cap = caption.ToUpperInvariant();
                int s = FitSize(cap, 12, 10, cr.width);
                if (Ui.TextWidth(cap, s, true) > cr.width)
                {
                    // Too long for one line: two small lines, cut at a word with "…" if even that is too much.
                    var words = cap.Split(' ');
                    for (int n = words.Length; n > 0 && Ui.TextHeight(cap, 10, cr.width, true) > 30f; n--)
                        cap = string.Join(" ", words, 0, n - 1).TrimEnd(',', '.', ';', ':') + "…";
                    Ui.Label(new Rect(cr.x, y + 1f, cr.width, 26f), cap, 10, CrtUi.Dim, TextAnchor.MiddleRight, true, true);
                }
                else
                {
                    s = FitSize(cap, 12, 9, cr.width);
                    Ui.Label(cr, Truncate(cap, s, cr.width), s, CrtUi.Dim, TextAnchor.MiddleRight, true);
                }
            }
            Ui.Fill(new Rect(x, y + 27f, w, 1f), Ink(0.4f));
        }

        /// A small label in the dim ink (the mockup's .lab).
        public static void Lab(Rect r, string text, int size, bool calm, bool bright = false)
        {
            string t = text.ToUpperInvariant();
            Ui.Label(r, Truncate(t, size, r.width), size, bright ? CrtUi.Ink : CrtUi.Dim, TextAnchor.MiddleLeft, true);
        }

        /// A screen panel: a faint wash, a thin rim and bracket corners that stick out at the top left and bottom right.
        public static void Panel(Rect r, bool brackets = true)
        {
            Ui.Fill(r, Ink(0.04f));
            CrtKit.Frame(r, 1f, Ink(0.5f));
            if (!brackets)
                return;
            var c = CrtUi.Ink;
            Ui.Fill(new Rect(r.x - 6f, r.y - 6f, 22f, 2f), c);
            Ui.Fill(new Rect(r.x - 6f, r.y - 6f, 2f, 22f), c);
            Ui.Fill(new Rect(r.xMax - 16f, r.yMax + 4f, 22f, 2f), c);
            Ui.Fill(new Rect(r.xMax + 4f, r.yMax - 16f, 2f, 22f), c);
        }

        /// A number tile: a gold value over a small dim caption, the value shrinking to fit the tile.
        public static void Tile(Rect r, string value, string caption, bool calm)
        {
            Ui.Fill(r, Ink(0.10f));
            CrtKit.Frame(r, 1f, Ink(0.4f));
            var vr = new Rect(r.x + 8f, r.y + 2f, r.width - 12f, r.height * 0.58f);
            Fit(vr, value, 20, 11, Gold, TextAnchor.MiddleLeft, calm, false);
            var cr = new Rect(r.x + 8f, r.y + r.height * 0.58f, r.width - 10f, r.height * 0.4f);
            int s = FitSize(caption, 10, 8, cr.width);
            Ui.Label(cr, Truncate(caption, s, cr.width), s, CrtUi.Dim, TextAnchor.UpperLeft, true);
        }

        /// A stat row: dim name, a lit bar in the stat's own colour (a bright top, a deeper bottom, a soft halo, a bright end cap and a slow
        /// pulse), the value on the right. Reduced effects: the flat bar, no halo or pulse.
        public static void StatBar(Rect r, string name, string value, float frac, Color c, bool calm)
        {
            const float nameW = 70f, valW = 64f;
            Ui.Label(new Rect(r.x, r.y, nameW, r.height), name.ToUpperInvariant(), 12, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            var tr = new Rect(r.x + nameW + 8f, r.center.y - 4f, r.width - nameW - valW - 16f, 9f);
            Ui.Fill(new Rect(tr.x - 1f, tr.y - 1f, tr.width + 2f, tr.height + 2f), new Color(1f, 1f, 1f, 0.06f));
            Ui.Fill(tr, new Color(0.02f, 0.04f, 0.08f, 0.9f));
            var fr = new Rect(tr.x, tr.y, Mathf.Max(2f, tr.width * Mathf.Clamp01(frac)), tr.height);
            if (calm)
            {
                Ui.Fill(fr, c);
            }
            else
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
                float k = 0.85f + 0.3f * pulse;  // a slow brightness pulse
                for (int i = 4; i >= 1; i--)  // the halo: wider and fainter each step
                    Ui.Fill(new Rect(fr.x - i, fr.y - i * 1.4f, fr.width + i * 1.5f, fr.height + i * 2.8f), new Color(c.r, c.g, c.b, 0.07f * k));
                Ui.Fill(fr, Color.Lerp(c, new Color(0f, 0f, 0f), 0.3f));                                              // deeper lower half
                Ui.Fill(new Rect(fr.x, fr.y, fr.width, fr.height * 0.6f), Color.Lerp(c, Color.white, 0.12f * k));  // main body
                Ui.Fill(new Rect(fr.x, fr.y + 1f, fr.width, 2f), Color.Lerp(c, Color.white, 0.55f));         // the lit top edge
                var cap = new Rect(fr.xMax - 2f, fr.y - 3f, 3f, fr.height + 6f);                              // the bright end cap
                Ui.Fill(new Rect(cap.x - 2f, cap.y - 2f, cap.width + 4f, cap.height + 4f), new Color(c.r, c.g, c.b, 0.3f * k));
                Ui.Fill(cap, Color.white);
            }
            var vr = new Rect(r.xMax - valW, r.y, valW, r.height);
            int s = FitSize(value, 13, 10, vr.width);
            Ui.Label(vr, value, s, new Color(0.9f, 0.93f, 1f), TextAnchor.MiddleRight, true);
        }
    }
}
