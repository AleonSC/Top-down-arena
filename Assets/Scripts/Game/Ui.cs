using System;
using UnityEngine;

namespace Arena
{
    /// A small IMGUI toolkit for the menus: rounded panels, tinted buttons, fields,
    /// sliders and checkboxes. Textures are generated in code, so there are no image
    /// files. Call Init() from OnGUI before using anything else.
    public static class Ui
    {
        public static readonly Color Text = new Color(0.93f, 0.95f, 1f);
        public static readonly Color Muted = new Color(0.62f, 0.67f, 0.78f);
        public static readonly Color Accent = new Color(0.36f, 0.6f, 1f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.35f);
        public static readonly Color Good = new Color(0.45f, 1f, 0.55f);
        public static readonly Color Warn = new Color(1f, 0.72f, 0.4f);

        // Button tints (the button texture is near-white, so these set its color).
        public static readonly Color Neutral = new Color(0.26f, 0.29f, 0.4f);
        public static readonly Color Primary = new Color(0.3f, 0.55f, 1f);
        public static readonly Color Danger = new Color(0.78f, 0.3f, 0.34f);
        public static readonly Color GoldButton = new Color(0.85f, 0.62f, 0.18f);

        static GUIStyle panel, panelAccent, button, field, label, track, thumb, check, hud, hudLit, pill, rim;
        static GUIStyle card, cardTint, cardRim;  // choice cards
        static Texture2D chip;
        static Texture2D checkOn, checkOff;

        public static void Init()
        {
            if (panel != null)
                return;
            panel = Style(Rounded(10, new Color(0.11f, 0.12f, 0.18f, 0.96f), new Color(0.08f, 0.09f, 0.13f, 0.96f), new Color(0.27f, 0.32f, 0.46f, 1f), 1.5f), 10);
            panelAccent = Style(Rounded(10, new Color(0.12f, 0.14f, 0.21f, 0.97f), new Color(0.09f, 0.1f, 0.15f, 0.97f), new Color(0.42f, 0.6f, 1f, 1f), 2f), 10);

            // In-match HUD: translucent so the arena shows through, with a lighter rim.
            hud = Style(Rounded(9, new Color(0.08f, 0.09f, 0.14f, 0.88f), new Color(0.04f, 0.05f, 0.08f, 0.88f), new Color(1f, 1f, 1f, 0.14f), 1.5f), 9);
            hudLit = Style(Rounded(9, new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.85f, 0.9f, 0.9f), new Color(1f, 1f, 1f, 1f), 2f), 9);
            pill = Style(Rounded(4, Color.white, new Color(0.86f, 0.86f, 0.9f), new Color(1f, 1f, 1f, 0f), 0f), 4);
            rim = Style(Rounded(12, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), Color.white, 2.5f), 12);

            // Buttons: a strong top-to-bottom shade (tinted by the button's colour) under a soft light rim.
            button = Style(Rounded(10, new Color(1f, 1f, 1f), new Color(0.7f, 0.72f, 0.78f), new Color(1f, 1f, 1f, 0.38f), 1.5f), 10);
            button.hover.background = Rounded(10, new Color(1f, 1f, 1f), new Color(0.84f, 0.86f, 0.92f), new Color(1f, 1f, 1f, 0.85f), 2f);
            button.active.background = Rounded(10, new Color(0.66f, 0.68f, 0.74f), new Color(0.78f, 0.8f, 0.86f), new Color(1f, 1f, 1f, 0.5f), 1.5f);
            // Choice cards: a dark card with a slate rim; the selected one is washed with its colour from the top.
            card = Style(Rounded(10, new Color(0.137f, 0.157f, 0.22f, 1f), new Color(0.106f, 0.122f, 0.172f, 1f), new Color(0.2f, 0.235f, 0.337f, 1f), 1.5f), 10);
            cardTint = Style(Rounded(10, new Color(1f, 1f, 1f, 0.42f), new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), 0f), 10);
            cardRim = Style(Rounded(10, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), Color.white, 1.5f), 10);
            chip = Circle(40, Color.white, Color.white);
            button.alignment = TextAnchor.MiddleCenter;
            button.fontSize = 16;
            button.fontStyle = FontStyle.Bold;
            button.normal.textColor = button.hover.textColor = button.active.textColor = Color.white;
            button.padding = new RectOffset(10, 10, 4, 4);

            field = Style(Rounded(6, new Color(0.05f, 0.06f, 0.09f, 1f), new Color(0.06f, 0.07f, 0.1f, 1f), new Color(0.3f, 0.35f, 0.5f, 1f), 1.5f), 6);
            field.focused.background = Rounded(6, new Color(0.06f, 0.07f, 0.11f, 1f), new Color(0.07f, 0.08f, 0.12f, 1f), Accent, 2f);
            field.hover.background = field.normal.background;
            field.fontSize = 16;
            field.alignment = TextAnchor.MiddleLeft;
            field.padding = new RectOffset(10, 10, 4, 4);
            field.normal.textColor = field.focused.textColor = field.hover.textColor = Text;

            label = new GUIStyle(GUI.skin.label) { richText = false };

            track = new GUIStyle();  // the track itself is drawn by Slider()
            thumb = new GUIStyle();
            thumb.normal.background = thumb.hover.background = thumb.active.background = Circle(18, Color.white, Accent);
            thumb.fixedWidth = thumb.fixedHeight = 18f;

            checkOff = Rounded(5, new Color(0.06f, 0.07f, 0.1f), new Color(0.07f, 0.08f, 0.12f), new Color(0.4f, 0.45f, 0.6f), 1.5f, 22);
            checkOn = Rounded(5, Accent, new Color(0.25f, 0.45f, 0.85f), new Color(0.6f, 0.75f, 1f), 1.5f, 22);
            check = new GUIStyle();
        }

        // --- Drawing ------------------------------------------------------------

        public static void Panel(Rect r, bool accent = false)
        {
            GUI.Box(r, GUIContent.none, accent ? panelAccent : panel);
        }

        public static void Label(Rect r, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false, bool wrap = false)
        {
            label.fontSize = size;
            label.alignment = align;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.wordWrap = wrap;
            // Every state gets the same color: the default skin's hover color would otherwise light up
            // the dark shadow copy under titles when the mouse is over them (it looked like doubled text).
            label.normal.textColor = label.hover.textColor = label.active.textColor = label.focused.textColor = color;
            GUI.Label(r, Shown(r, text), label);
        }

        /// How wide a label of this text would be.
        public static float TextWidth(string text, int size, bool bold = false)
        {
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.wordWrap = false;
            return label.CalcSize(new GUIContent(text)).x;
        }

        /// How tall a word-wrapped label of this text would be at this width.
        public static float TextHeight(string text, int size, float width, bool bold = false)
        {
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.wordWrap = true;
            return label.CalcHeight(new GUIContent(text), width);
        }

        /// Big text with a soft drop shadow.
        public static void Title(Rect r, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            Label(new Rect(r.x + 3f, r.y + 4f, r.width, r.height), text, size, new Color(0f, 0f, 0f, 0.55f), align, true);
            Label(r, text, size, color, align, true);
        }

        /// Small uppercase section heading with a line under it.
        public static void Heading(float x, float y, float w, string text)
        {
            Label(new Rect(x, y, w, 20f), Shown(new Rect(x, y, w, 20f), text).ToUpperInvariant(), 12, Muted, TextAnchor.MiddleLeft, true);
            Fill(new Rect(x, y + 21f, w, 1f), new Color(1f, 1f, 1f, 0.08f));
        }

        /// Multiplies the alpha of everything drawn (the HUD fading in and out). Drawing that sets GUI.color itself
        /// goes through Faded, so it fades too; everything else is tinted by GUI.color, which the HUD sets as well.
        public static float Alpha = 1f;

        public static Color Faded(Color c) => new Color(c.r, c.g, c.b, c.a * Alpha);

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = Faded(c);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static bool Button(Rect r, string text, Color tint)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool clicked = GUI.Button(r, Shown(r, text), button);
            GUI.backgroundColor = old;
            return clicked;
        }

        public static bool Button(Rect r, string text) => Button(r, text, Neutral);

        /// A button with a small round icon chip before its label (e.g. a check on Ready, a play arrow on Start).
        public static bool Button(Rect r, string text, Color tint, Texture2D icon)
        {
            if (icon == null)
                return Button(r, text, tint);
            bool clicked = Button(r, "", tint);
            float tw = button.CalcSize(new GUIContent(text)).x - button.padding.horizontal;
            float cs = Mathf.Min(22f, r.height - 16f), total = cs + 9f + tw;
            float x = r.center.x - total / 2f;
            var c = new Rect(x, r.center.y - cs / 2f, cs, cs);
            var old = GUI.color;
            GUI.color = Faded(new Color(1f, 1f, 1f, 0.28f));
            GUI.DrawTexture(c, chip);
            GUI.color = old;
            float isz = Mathf.Round(cs * 0.6f);
            Icon(new Rect(c.center.x - isz / 2f, c.center.y - isz / 2f, isz, isz), icon);
            Label(new Rect(c.xMax + 9f, r.y, tw + 8f, r.height), text, button.fontSize, Color.white, TextAnchor.MiddleLeft, true);
            return clicked;
        }

        /// A ring that pulses outward around a button (e.g. Ready once you're ready). t = time in seconds.
        public static void PulseRing(Rect r, Color c, float t)
        {
            float f = (t / 1.4f) % 1f, grow = 2f + 6f * f;
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(c.r, c.g, c.b, 0.9f * (1f - f));
            GUI.Box(new Rect(r.x - grow, r.y - grow, r.width + 2f * grow, r.height + 2f * grow), GUIContent.none, cardRim);
            GUI.backgroundColor = old;
        }

        /// A button with smaller text, for tight rows.
        public static bool SmallButton(Rect r, string text, Color tint, int size = 12)
        {
            int old = button.fontSize;
            button.fontSize = size;
            bool clicked = Button(r, text, tint);
            button.fontSize = old;
            return clicked;
        }

        public static bool SmallButton(Rect r, string text, int size = 12) => SmallButton(r, text, Neutral, size);

        /// One option of a row of choices, drawn as a card: dark with a colour bar along the bottom; the selected
        /// one is washed with its colour, rimmed in it and glows. With an icon (and an optional second line such
        /// as "276 HP") it becomes a small icon card: icon on top, name, then the second line.
        public static bool Choice(Rect r, string text, bool selected, Color accent, Texture2D icon = null, string sub = null, int textSize = 0)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            var old = GUI.backgroundColor;
            if (selected && r.height >= 30f)  // soft glow behind (left off small rows, where it looks blocky)
            {
                GUI.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.22f);
                GUI.Box(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), GUIContent.none, pill);
            }
            GUI.backgroundColor = Color.white;
            GUI.Box(r, GUIContent.none, card);
            if (selected)
            {
                GUI.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.75f);
                GUI.Box(r, GUIContent.none, cardTint);
            }
            if (selected || hover)
            {
                Color hc = Color.Lerp(accent, Color.white, 0.3f);
                GUI.backgroundColor = selected ? accent : new Color(hc.r, hc.g, hc.b, 0.7f);
                GUI.Box(r, GUIContent.none, cardRim);
            }
            GUI.backgroundColor = old;
            float barH = r.height >= 34f ? 3f : 2f;
            Fill(new Rect(r.x + 5f, r.yMax - barH - 2f, r.width - 10f, barH), new Color(accent.r, accent.g, accent.b, selected ? 1f : hover ? 0.6f : 0.35f));

            Color name = selected ? Color.white : Color.Lerp(accent, Color.white, 0.2f);
            int size = textSize > 0 ? textSize : r.height < 30f ? 12 : 14;
            if (icon != null)
            {
                float isz = Mathf.Min(24f, r.height * 0.36f);
                Icon(new Rect(r.center.x - isz / 2f, r.y + 6f, isz, isz), icon);
                Label(new Rect(r.x, r.y + 8f + isz, r.width, 18f), text, 15, name, TextAnchor.MiddleCenter, true);
                if (sub != null)
                    Label(new Rect(r.x, r.y + 25f + isz, r.width, 14f), sub, 11, selected ? new Color(1f, 1f, 1f, 0.8f) : Muted, TextAnchor.MiddleCenter);
            }
            else if (sub != null)
            {
                Label(new Rect(r.x, r.y + r.height / 2f - 18f, r.width, 20f), text, 15, name, TextAnchor.MiddleCenter, true);
                Label(new Rect(r.x, r.y + r.height / 2f + 1f, r.width, 16f), sub, 11, selected ? new Color(1f, 1f, 1f, 0.8f) : Muted, TextAnchor.MiddleCenter);
            }
            else
            {
                while (size > 10 && TextWidth(text, size, true) > r.width - 14f)
                    size--;  // long names in narrow cards (six class buttons) shrink to fit
                Label(new Rect(r.x + 4f, r.y, r.width - 8f, r.height - barH), text, size, name, TextAnchor.MiddleCenter, true);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static string Field(Rect r, string value, int maxLength)
        {
            return GUI.TextField(r, value, maxLength, field);
        }

        static GUIStyle area;

        /// A multi-line text box (the dev text editor).
        public static string Area(Rect r, string value)
        {
            if (area == null)
                area = new GUIStyle(field) { wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8), fontSize = 15 };
            return GUI.TextArea(r, value, area);
        }

        // --- Dev text editing (DevTextEdit.cs) -----------------------------------------
        // Every label and button goes through Shown: it swaps in edited text (TextOverrides), and in text-edit
        // mode notes the smallest text under the mouse and outlines the one picked last frame.

        public static bool PickText;   // text-edit mode is on
        public static bool NoPick;     // drawing the editor itself: not pickable
        public static string Hovered;  // the text under the mouse, as the code draws it (before any edit)
        static Rect hoveredRect, pickRect;
        static string pickText;
        static float pickArea;
        static bool outlined;

        /// Call at the start of each OnGUI pass.
        public static void BeginPick()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            pickText = null;
            pickArea = float.MaxValue;
            outlined = false;
        }

        /// Call once everything pickable is drawn.
        public static void EndPick()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Hovered = PickText ? pickText : null;
            hoveredRect = pickRect;
        }

        public static string Shown(Rect r, string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            if (PickText && !NoPick && Event.current.type == EventType.Repaint)
            {
                if (text == Hovered && !outlined && r.x == hoveredRect.x && r.y == hoveredRect.y && r.width == hoveredRect.width && r.height == hoveredRect.height)
                {
                    outlined = true;
                    var old = Alpha;
                    Alpha = 1f;
                    Fill(new Rect(r.x - 2f, r.y - 1f, r.width + 4f, r.height + 2f), new Color(1f, 0.8f, 0.3f, 0.16f));
                    Fill(new Rect(r.x - 2f, r.y - 1f, r.width + 4f, 1.5f), Gold);
                    Fill(new Rect(r.x - 2f, r.yMax - 0.5f, r.width + 4f, 1.5f), Gold);
                    Fill(new Rect(r.x - 2f, r.y - 1f, 1.5f, r.height + 2f), Gold);
                    Fill(new Rect(r.xMax + 0.5f, r.y - 1f, 1.5f, r.height + 2f), Gold);
                    Alpha = old;
                }
                float a = r.width * r.height;
                if (a < pickArea && r.Contains(Event.current.mousePosition) && HasLetter(text))
                {
                    pickArea = a;
                    pickText = text;
                    pickRect = r;
                }
            }
            return TextOverrides.Apply(text);
        }

        static bool HasLetter(string s)
        {
            foreach (char c in s)
                if (char.IsLetter(c))
                    return true;
            return false;
        }

        /// r should be 18 px tall (the thumb's size); the track is drawn through its middle.
        public static float Slider(Rect r, float value)
        {
            var bar = new Rect(r.x + 4f, r.y + r.height / 2f - 3f, r.width - 8f, 6f);
            Fill(bar, new Color(0.18f, 0.2f, 0.28f));
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(value), bar.height), Accent);
            return GUI.HorizontalSlider(r, value, 0f, 1f, track, thumb);
        }

        public static bool Toggle(Rect r, bool value, string text)
        {
            var box = new Rect(r.x, r.y + (r.height - 22f) / 2f, 22f, 22f);
            GUI.DrawTexture(box, value ? checkOn : checkOff);
            if (value)
                Fill(new Rect(box.x + 6f, box.y + 6f, 10f, 10f), Color.white);
            Label(new Rect(r.x + 32f, r.y, r.width - 32f, r.height), text, 14, Text);
            return GUI.Toggle(r, value, GUIContent.none, check);
        }

        public static void Badge(Rect r, string text, Color color)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = color * 0.7f;
            GUI.Box(r, GUIContent.none, button);
            GUI.backgroundColor = old;
            Label(r, text, 11, Color.white, TextAnchor.MiddleCenter, true);
        }

        /// A thin horizontal meter, e.g. for comparing two players' numbers.
        public static void Meter(Rect r, float frac, Color color)
        {
            Fill(r, new Color(1f, 1f, 1f, 0.06f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), color);
        }

        // --- In-match HUD pieces ------------------------------------------------

        /// A translucent HUD panel. With an accent, the rim glows in that color.
        public static void HudPanel(Rect r, Color? accent = null)
        {
            GUI.Box(r, GUIContent.none, hud);
            if (accent.HasValue)
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(accent.Value.r, accent.Value.g, accent.Value.b, 0.22f * accent.Value.a);
                GUI.Box(r, GUIContent.none, hudLit);
                GUI.backgroundColor = old;
            }
        }

        /// A solid card with a colored rim; glow (0..1) sets how strongly the rim shows.
        public static void Card(Rect r, Color rimColor, float glow)
        {
            GUI.Box(r, GUIContent.none, panel);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(rimColor.r, rimColor.g, rimColor.b, Mathf.Clamp01(glow));
            GUI.Box(r, GUIContent.none, rim);
            GUI.backgroundColor = old;
        }

        /// A pixel-art icon, scaled up with crisp pixels.
        public static void Icon(Rect r, Texture2D tex, float alpha = 1f)
        {
            if (tex == null)
                return;
            var old = GUI.color;
            GUI.color = Faded(new Color(1f, 1f, 1f, alpha));
            GUI.DrawTexture(r, tex);
            GUI.color = old;
        }

        /// A pixel-art icon tinted a colour (white icons take the colour exactly).
        public static void Icon(Rect r, Texture2D tex, Color tint)
        {
            if (tex == null)
                return;
            var old = GUI.color;
            GUI.color = Faded(tint);
            GUI.DrawTexture(r, tex);
            GUI.color = old;
        }

        /// A rounded rectangle in one color (tints a near-white texture, so the top is a bit lighter).
        public static void Pill(Rect r, Color c)
        {
            if (r.width <= 0f || r.height <= 0f)
                return;
            var old = GUI.backgroundColor;
            GUI.backgroundColor = c;
            GUI.Box(r, GUIContent.none, pill);
            GUI.backgroundColor = old;
        }

        /// A rounded meter: dark track, the filled part clipped from a full-width pill so both
        /// ends stay rounded, and a faint highlight along the top.
        public static void Bar(Rect r, float frac, Color fill, Color? trackColor = null)
        {
            Pill(r, trackColor ?? new Color(0f, 0f, 0f, 0.55f));
            frac = Mathf.Clamp01(frac);
            if (frac > 0f)
            {
                GUI.BeginGroup(new Rect(r.x, r.y, r.width * frac, r.height));
                Pill(new Rect(0f, 0f, r.width, r.height), fill);
                Fill(new Rect(2f, 1f, r.width - 4f, Mathf.Max(1f, r.height * 0.3f)), new Color(1f, 1f, 1f, 0.18f));
                GUI.EndGroup();
            }
        }

        /// Text with a 1 px dark outline-ish shadow, for labels over the arena.
        public static void Shadowed(Rect r, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true)
        {
            Label(new Rect(r.x + 1f, r.y + 1.5f, r.width, r.height), text, size, new Color(0f, 0f, 0f, 0.75f * color.a), align, bold);
            Label(r, text, size, color, align, bold);
        }

        /// Game-style text: a soft glow, a dark outline, and a two-tone fill (the top half lighter than
        /// the bottom, like a metallic sheen). glow = null for none.
        public static void Fancy(Rect r, string text, int size, Color top, Color bottom, TextAnchor align = TextAnchor.MiddleLeft, Color? glow = null)
        {
            if (glow.HasValue)
            {
                var g = glow.Value;
                var gc = new Color(g.r, g.g, g.b, 0.3f * g.a);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    Label(new Rect(r.x + Mathf.Cos(a) * 2.5f, r.y + Mathf.Sin(a) * 2.5f, r.width, r.height), text, size, gc, align, true);
                }
            }
            var outline = new Color(0f, 0f, 0f, 0.85f * bottom.a);
            Label(new Rect(r.x - 1f, r.y, r.width, r.height), text, size, outline, align, true);
            Label(new Rect(r.x + 1f, r.y, r.width, r.height), text, size, outline, align, true);
            Label(new Rect(r.x, r.y - 1f, r.width, r.height), text, size, outline, align, true);
            Label(new Rect(r.x, r.y + 1.5f, r.width, r.height), text, size, outline, align, true);
            Label(r, text, size, bottom, align, true);
            // The top half again in the lighter color, clipped to the upper part of the glyphs.
            GUI.BeginGroup(new Rect(r.x, r.y, r.width, r.height * 0.5f + size * 0.02f));
            Label(new Rect(0f, 0f, r.width, r.height), text, size, top, align, true);
            GUI.EndGroup();
        }

        // --- Texture generation -------------------------------------------------

        static GUIStyle Style(Texture2D bg, int radius)
        {
            var s = new GUIStyle();
            s.normal.background = bg;
            s.border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2);
            return s;
        }

        /// A rounded rectangle with a vertical gradient and a border, meant to be 9-sliced.
        static Texture2D Rounded(int radius, Color top, Color bottom, Color border, float borderWidth, int size = 0)
        {
            if (size == 0)
                size = radius * 2 + 6;
            var tex = NewTexture(size);
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to the rounded box (negative inside).
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    // Unity textures start at the bottom row, so y = 0 is the bottom of the gradient.
                    Color fill = Color.Lerp(bottom, top, (float)y / (size - 1));
                    float edge = Mathf.Clamp01(d + borderWidth + 0.5f);
                    Color c = Color.Lerp(fill, border, edge * border.a);
                    c.a = Mathf.Max(fill.a, edge * border.a) * alpha;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Texture2D Circle(int size, Color fill, Color border)
        {
            var tex = NewTexture(size);
            var px = new Color[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) - (c - 1f);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    Color col = Color.Lerp(fill, border, Mathf.Clamp01(d + 3.5f));
                    col.a = alpha;
                    px[y * size + x] = col;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
    }
}
