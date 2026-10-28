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

            button = Style(Rounded(8, new Color(1f, 1f, 1f), new Color(0.82f, 0.84f, 0.88f), new Color(1f, 1f, 1f, 0.55f), 1.5f), 8);
            button.hover.background = Rounded(8, new Color(1f, 1f, 1f), new Color(0.95f, 0.96f, 1f), new Color(1f, 1f, 1f, 1f), 2f);
            button.active.background = Rounded(8, new Color(0.7f, 0.72f, 0.76f), new Color(0.78f, 0.8f, 0.84f), new Color(1f, 1f, 1f, 0.6f), 1.5f);
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
            GUI.Label(r, text, label);
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
            Label(new Rect(x, y, w, 20f), text.ToUpperInvariant(), 12, Muted, TextAnchor.MiddleLeft, true);
            Fill(new Rect(x, y + 21f, w, 1f), new Color(1f, 1f, 1f, 0.08f));
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static bool Button(Rect r, string text, Color tint)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool clicked = GUI.Button(r, text, button);
            GUI.backgroundColor = old;
            return clicked;
        }

        public static bool Button(Rect r, string text) => Button(r, text, Neutral);

        /// One option of a row of choices. The selected one is filled with the accent color.
        public static bool Choice(Rect r, string text, bool selected, Color accent)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = selected ? accent * 0.85f : new Color(0.17f, 0.19f, 0.26f);
            int size = button.fontSize;
            button.fontSize = 14;
            button.normal.textColor = button.hover.textColor = selected ? Color.white : Color.Lerp(accent, Color.white, 0.35f);
            bool clicked = GUI.Button(r, text, button);
            button.normal.textColor = button.hover.textColor = Color.white;
            button.fontSize = size;
            GUI.backgroundColor = old;
            return clicked;
        }

        public static string Field(Rect r, string value, int maxLength)
        {
            return GUI.TextField(r, value, maxLength, field);
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
            GUI.color = new Color(1f, 1f, 1f, alpha);
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
