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
        public static readonly Color HeadCol = new Color(0.35f, 0.85f, 0.91f);  // section headings (the mockup's teal)

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
            size = CrtKit.Sz(size);
            minSize = Mathf.Min(size, CrtKit.Sz(minSize));
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
            CrtKit.SoftBox(r, c, on, hover, false);  // a faint tint and a thin outline; the open key gets an underline glow, not a solid fill
            Color tc = CrtKit.SoftText(c, hover || on);
            Draw(r, text, icon, stack, colorIcon, size, minSize, tc, false, calm, sub, false);
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
            Ui.Fill(r, new Color(c.r, c.g, c.b, 0.02f));
            CrtKit.Dashed(r, 1f, new Color(c.r, c.g, c.b, hover ? 0.6f : 0.32f));  // faint, like the mockup's locked keys
            var ic = PixelArt.Get("ui_lock");
            var tint = new Color(1f, 1f, 1f, hover ? 0.55f : 0.35f);
            string label = "LOCKED";
            size = Mathf.Min(size, 10);  // small and dim
            if (stack)
            {
                float isz = Mathf.Min(r.height * 0.42f, 18f);
                Ui.Icon(new Rect(r.center.x - isz / 2f, r.y + 4f, isz, isz), ic, tint);
                Ui.Label(new Rect(r.x, r.y + 4f + isz, r.width, r.yMax - (r.y + 4f + isz) - 1f), label, size, new Color(c.r, c.g, c.b, 0.75f), TextAnchor.MiddleCenter, true);
            }
            else
            {
                float isz = Mathf.Min(r.height - 12f, 14f), tw = Ui.TextWidth(label, size, true) - 6f;
                float x = r.center.x - (isz + 8f + tw) / 2f;
                Ui.Icon(new Rect(x, r.center.y - isz / 2f, isz, isz), ic, tint);
                Ui.Label(new Rect(x + isz + 8f - 2f, r.y, tw + 12f, r.height), label, size, new Color(c.r, c.g, c.b, 0.75f), TextAnchor.MiddleLeft, true);
            }
            return hover;
        }

        /// A section heading: the name on the left, a short caption on the right (shrunk to fit), a thin rule under both.
        public static void Heading(float x, float y, float w, string title, string caption, bool calm, int size = 18)
        {
            string t = title.ToUpperInvariant();
            size = CrtKit.Sz(size);  // small caps, like the mockup's 12 px headings
            float sp = size * 0.14f;  // .14em, as in the mockup
            float tw = SpacedWidth(t, size, sp);
            Spaced(new Rect(x, y + 8f, tw + 12f, 24f), t, size, HeadCol, TextAnchor.MiddleLeft, sp);  // sits just above the rule, flush with its left end
            if (!string.IsNullOrEmpty(caption))
            {
                float left = tw + 16f;
                var cr = new Rect(x + left, y + 9f, w - left, 24f);
                string cap = caption.ToUpperInvariant();
                int s = FitSize(cap, 10, 9, cr.width);
                var dimCap = new Color(HeadCol.r * 0.55f, HeadCol.g * 0.55f, HeadCol.b * 0.6f, 0.65f);  // very dim, like the mockup's captions
                if (Ui.TextWidth(cap, s, true) > cr.width)
                {
                    // Too long for one line: two small lines, cut at a word with "…" if even that is too much.
                    var words = cap.Split(' ');
                    for (int n = words.Length; n > 0 && Ui.TextHeight(cap, 10, cr.width, true) > 30f; n--)
                        cap = string.Join(" ", words, 0, n - 1).TrimEnd(',', '.', ';', ':') + "…";
                    Ui.Label(new Rect(cr.x, y + 8f, cr.width, 26f), cap, 9, dimCap, TextAnchor.MiddleRight, true, true);
                }
                else
                {
                    s = FitSize(cap, 10, 9, cr.width);
                    Ui.Label(cr, Truncate(cap, s, cr.width), s, dimCap, TextAnchor.MiddleRight, true);
                }
            }
            Ui.Fill(new Rect(x, y + 27f, w, 1f), new Color(HeadCol.r, HeadCol.g, HeadCol.b, 0.4f));
        }

        /// A small label in the dim ink (the mockup's .lab).
        public static void Lab(Rect r, string text, int size, bool calm, bool bright = false)
        {
            string t = text.ToUpperInvariant();
            Ui.Label(r, Truncate(t, size, r.width), size, bright ? CrtUi.Ink : CrtUi.Dim, TextAnchor.MiddleLeft, true);
        }

        /// The blue corner brackets of the lobby's boxes (as on the original's ability box): an L on every corner, 3 px thick,
        /// bright blue with a soft glow behind, hugging the box from just outside.
        public static void Corners(Rect r, float arm = 26f, float th = 3f)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var c = new Color(0.36f, 0.6f, 1f);
            float o = 4f, x0 = r.x - o, y0 = r.y - o, x1 = r.xMax + o, y1 = r.yMax + o;
            // A faint halo along each arm only (a halo round the whole corner square read as a bright spot).
            const float hs = 0.05f;
            Ui.Glow(new Rect(x0, y0, arm, th), c, hs); Ui.Glow(new Rect(x0, y0, th, arm), c, hs);                       // top left
            Ui.Glow(new Rect(x1 - arm, y0, arm, th), c, hs); Ui.Glow(new Rect(x1 - th, y0, th, arm), c, hs);            // top right
            Ui.Glow(new Rect(x0, y1 - th, arm, th), c, hs); Ui.Glow(new Rect(x0, y1 - arm, th, arm), c, hs);            // bottom left
            Ui.Glow(new Rect(x1 - arm, y1 - th, arm, th), c, hs); Ui.Glow(new Rect(x1 - th, y1 - arm, th, arm), c, hs);  // bottom right
            Ui.Fill(new Rect(x0, y0, arm, th), c);                      // top left
            Ui.Fill(new Rect(x0, y0, th, arm), c);
            Ui.Fill(new Rect(x1 - arm, y0, arm, th), c);                // top right
            Ui.Fill(new Rect(x1 - th, y0, th, arm), c);
            Ui.Fill(new Rect(x0, y1 - th, arm, th), c);                 // bottom left
            Ui.Fill(new Rect(x0, y1 - arm, th, arm), c);
            Ui.Fill(new Rect(x1 - arm, y1 - th, arm, th), c);           // bottom right
            Ui.Fill(new Rect(x1 - th, y1 - arm, th, arm), c);
        }

        // --- The role wash: the big boxes' gradient in the picked role's colour -------------------------------------------------

        static Color washCol = new Color(0.36f, 0.64f, 1f);
        static float washAt = -10f;
        static int washFrame = -1;

        /// The role colour the boxes are washed with right now (it eases to a new role's colour).
        public static Color WashColor => washCol;

        /// Once a frame (the lobby draws several times a frame while it slides): ease the wash colour to the shown role's colour over
        /// about 0.7 s. `snap` (Reduced effects) and a lobby that was away for a moment jump straight to it.
        public static void WashTo(Color target, bool snap)
        {
            float now = Time.unscaledTime;
            bool away = now - washAt > 0.5f;
            washAt = now;
            if (washFrame == Time.frameCount)
                return;
            washFrame = Time.frameCount;
            if (snap || away)
            {
                washCol = target;
                return;
            }
            float k = 1f - Mathf.Exp(-Mathf.Min(0.1f, Time.unscaledDeltaTime) * 4.5f);
            washCol = Color.Lerp(washCol, target, k);
        }

        /// The mockup's `.grad #main` (Docs/Mockups/crt-lobby-softer.html), in strips that meet exactly: a dark diagonal wash running from about 20% of the
        /// role colour to near black (horizontal strips, with the slight lean to the right as a faint dark ramp), and over it a glow from the top left
        /// corner that is 32% of the role colour and fades out by 64% of the way (one soft radial texture).
        public static void Wash(Rect r)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var rc = washCol;
            Color d0 = new Color(0.031f, 0.063f, 0.157f), d1 = new Color(0.0196f, 0.039f, 0.110f), d2 = new Color(0.0157f, 0.031f, 0.082f);
            Color c0 = Mix(rc, 0.11f, d0), c1 = Mix(rc, 0.03f, d1), c2 = d2;
            // 165 degrees: t runs down and a little to the right. Strips take t along the left edge; the ramp below darkens toward the right.
            const float sin = 0.2588f, cos = 0.9659f;
            float len = r.width * sin + r.height * cos;
            const int rows = 14;
            for (int i = 0; i < rows; i++)
            {
                float y0 = Mathf.Round(r.y + r.height * i / rows), y1 = Mathf.Round(r.y + r.height * (i + 1) / rows);
                float yc = (y0 + y1) / 2f - r.center.y;
                float t = Mathf.Clamp01(((-r.width / 2f) * sin + yc * cos) / len + 0.5f);
                Color c = t < 0.58f ? Color.Lerp(c0, c1, t / 0.58f) : Color.Lerp(c1, c2, (t - 0.58f) / 0.42f);
                Ui.Fill(new Rect(r.x, y0, r.width, y1 - y0), new Color(c.r, c.g, c.b, 1f));
            }
            const int cols = 6;
            for (int i = 1; i < cols; i++)
            {
                float x0 = Mathf.Round(r.x + r.width * i / cols), x1 = Mathf.Round(r.x + r.width * (i + 1) / cols);
                Ui.Fill(new Rect(x0, r.y, x1 - x0, r.height), new Color(0f, 0f, 0f, 0.07f * i / (cols - 1f)));
            }
            // The glow: an ellipse from the top left corner, 32% of the role colour in the middle fading to nothing at 64% of 120% x 80% of the box.
            // One soft radial texture, clipped to the box.
            float rx = r.width * 1.2f * 0.64f, ry = r.height * 0.8f * 0.64f;
            GUI.BeginGroup(r);
            Ui.RadialGlow(new Rect(-rx, -ry, rx * 2f, ry * 2f), new Color(rc.r, rc.g, rc.b, 0.3f));
            GUI.EndGroup();
        }

        /// share of colour `c` mixed with `rest` (CSS color-mix(in srgb, c share, rest)).
        static Color Mix(Color c, float share, Color rest) => new Color(c.r * share + rest.r * (1f - share), c.g * share + rest.g * (1f - share), c.b * share + rest.b * (1f - share), 1f);

        /// A screen panel: the role wash (or, without it, a faint blue one), a thin rim that leans toward the role colour and bracket corners
        /// that stick out at the top left and bottom right.
        public static void Panel(Rect r, bool brackets = true, bool wash = true)
        {
            if (wash)
            {
                Wash(r);
                CrtKit.Frame(r, 1f, new Color(washCol.r, washCol.g, washCol.b, 0.55f));
            }
            else
            {
                Ui.Fill(r, Ink(0.04f));
                CrtKit.Frame(r, 1f, Ink(0.5f));
            }
            if (!brackets)
                return;
            Corners(r);
        }

        /// Text with letter-spacing (IMGUI has none): each letter placed by hand, the row aligned left, centre or right in `r`.
        static readonly System.Collections.Generic.Dictionary<string, float> advCache = new System.Collections.Generic.Dictionary<string, float>();

        /// One letter's advance at this size, measured between two bars so the label style's padding cancels out.
        static float Adv(char ch, int size, bool bold)
        {
            string key = ch + "|" + size + (bold ? "b" : "n") + (Ui.MenuFont ? "m" : "d");
            float v;
            if (advCache.TryGetValue(key, out v))
                return v;
            string mid = ch == ' ' ? "n" : ch.ToString();
            v = Mathf.Max(1f, Ui.TextWidth("|" + mid + "|", size, bold) - Ui.TextWidth("||", size, bold));
            advCache[key] = v;
            return v;
        }

        public static void Spaced(Rect r, string text, int size, Color c, TextAnchor align, float spacing, bool bold = true)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var adv = new float[text.Length];
            float total = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                adv[i] = Adv(text[i], size, bold);
                total += adv[i] + (i < text.Length - 1 ? spacing : 0f);
            }
            float x = align == TextAnchor.MiddleCenter || align == TextAnchor.UpperCenter || align == TextAnchor.LowerCenter ? r.center.x - total / 2f
                : align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight || align == TextAnchor.LowerRight ? r.xMax - total : r.x;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ')
                    Ui.Label(new Rect(x - Ui.LabelPadLeft - 1f, r.y, adv[i] + 12f, r.height), text[i].ToString(), size, c, TextAnchor.MiddleLeft, bold);  // (the first letter sits flush at r.x)
                x += adv[i] + spacing;
            }
        }

        /// The width Spaced gives this text.
        public static float SpacedWidth(string text, int size, float spacing, bool bold = true)
        {
            float total = 0f;
            for (int i = 0; i < text.Length; i++)
                total += Adv(text[i], size, bold) + (i < text.Length - 1 ? spacing : 0f);
            return total;
        }

        /// A quiet tab (the mockup's `.quiet [data-tab]`): no box, 12 px bold letter-spaced caps at 48% of the ink, 85% (a touch of white) under the
        /// mouse; the open one is a small lit pill (30% ink, a 1 px outline at 75%, white text). True on a click. Text shrinks to fit.
        public static bool QuietTab(Rect r, string text, bool on, bool enabled = true)
        {
            bool hover = enabled && GUI.enabled && r.Contains(Event.current.mousePosition);
            var ink = CrtUi.Ink;
            if (on)
            {
                var fill = new Color(ink.r, ink.g, ink.b, 0.3f);
                Ui.Fill(new Rect(r.x + 1f, r.y, r.width - 2f, r.height), fill);  // (corners cut by a pixel: the 3 px radius)
                Ui.Fill(new Rect(r.x, r.y + 1f, 1f, r.height - 2f), fill);
                Ui.Fill(new Rect(r.xMax - 1f, r.y + 1f, 1f, r.height - 2f), fill);
                var line = new Color(ink.r, ink.g, ink.b, 0.75f);
                Ui.Fill(new Rect(r.x + 1f, r.y, r.width - 2f, 1f), line);
                Ui.Fill(new Rect(r.x + 1f, r.yMax - 1f, r.width - 2f, 1f), line);
                Ui.Fill(new Rect(r.x, r.y + 1f, 1f, r.height - 2f), line);
                Ui.Fill(new Rect(r.xMax - 1f, r.y + 1f, 1f, r.height - 2f), line);
            }
            Color tc = on ? Color.white : hover ? Color.Lerp(ink, Color.white, 0.15f) : new Color(ink.r, ink.g, ink.b, 0.48f);
            const float sp = 1.44f;  // .12em at 12 px
            int s = 12;
            while (s > 9 && SpacedWidth(text, s, sp) > r.width - 8f)
                s--;
            string shown = text;
            while (shown.Length > 3 && SpacedWidth(shown, s, sp) > r.width - 8f)
                shown = shown.Substring(0, shown.Length - 2).TrimEnd() + "…";
            Spaced(r, shown, s, tc, TextAnchor.MiddleCenter, sp);
            return enabled && Ui.HitArea(r);
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
        static readonly System.Collections.Generic.Dictionary<string, float> barShown = new System.Collections.Generic.Dictionary<string, float>();
        static readonly System.Collections.Generic.Dictionary<string, float> barAt = new System.Collections.Generic.Dictionary<string, float>();
        static readonly System.Collections.Generic.Dictionary<string, int> barFrame = new System.Collections.Generic.Dictionary<string, int>();

        /// The fill a stat bar shows: it eases up or down to the new value (about a third of a second), once per frame however many times the
        /// lobby draws. A bar that was away for a moment, and Reduced effects, jump straight there.
        static float BarShown(string name, float target, bool calm)
        {
            float now = Time.unscaledTime, cur;
            float last;
            bool seen = barShown.TryGetValue(name, out cur) && barAt.TryGetValue(name, out last) && now - last < 0.5f;
            barAt[name] = now;
            if (!seen || calm)
            {
                barShown[name] = target;
                return target;
            }
            int fr;
            if (barFrame.TryGetValue(name, out fr) && fr == Time.frameCount)
                return cur;
            barFrame[name] = Time.frameCount;
            cur = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-Mathf.Min(0.1f, Time.unscaledDeltaTime) * 9f));
            if (Mathf.Abs(cur - target) < 0.002f)
                cur = target;
            barShown[name] = cur;
            return cur;
        }

        public static void StatBar(Rect r, string name, string value, float frac, Color c, bool calm)
        {
            const float nameW = 70f, valW = 64f;
            Ui.Label(new Rect(r.x, r.y, nameW, r.height), name.ToUpperInvariant(), 12, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            var tr = new Rect(r.x + nameW + 8f, r.center.y - 4f, r.width - nameW - valW - 16f, 9f);
            Ui.Fill(new Rect(tr.x - 1f, tr.y - 1f, tr.width + 2f, tr.height + 2f), new Color(1f, 1f, 1f, 0.06f));
            Ui.Fill(tr, new Color(0.02f, 0.04f, 0.08f, 0.9f));
            var fr = new Rect(tr.x, tr.y, Mathf.Max(2f, tr.width * Mathf.Clamp01(BarShown(name, frac, calm))), tr.height);
            if (calm)
            {
                Ui.Fill(fr, c);
            }
            else
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
                float k = 0.85f + 0.3f * pulse;  // a slow brightness pulse
                Ui.Glow(fr, c, 0.06f * k);  // a faint halo
                Ui.Fill(fr, Color.Lerp(c, new Color(0f, 0f, 0f), 0.3f));                                              // deeper lower half
                Ui.Fill(new Rect(fr.x, fr.y, fr.width, fr.height * 0.6f), Color.Lerp(c, Color.white, 0.12f * k));  // main body
                Ui.Fill(new Rect(fr.x, fr.y + 1f, fr.width, 2f), Color.Lerp(c, Color.white, 0.55f));         // the lit top edge
                var cap = new Rect(fr.xMax - 2f, fr.y - 3f, 3f, fr.height + 6f);                              // the bright end cap
                Ui.Glow(cap, c, 0.1f * k);
                Ui.Fill(cap, Color.white);
            }
            var vr = new Rect(r.xMax - valW, r.y, valW, r.height);
            int s = FitSize(value, 13, 10, vr.width);
            Ui.Label(vr, value, s, new Color(0.9f, 0.93f, 1f), TextAnchor.MiddleRight, true);
        }
    }
}
