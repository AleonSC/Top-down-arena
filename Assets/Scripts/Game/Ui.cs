using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// A small IMGUI toolkit for the menus: rounded panels, tinted buttons, fields,
    /// sliders and checkboxes. Textures are generated in code, so there are no image
    /// files. Call Init() from OnGUI before using anything else.
    public static partial class Ui  // partial: tolerates a stale file from older versions (e.g. ButtonStyles.cs)
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

        static GUIStyle panel, panelAccent, button, field, bareField, label, track, thumb, meterThumb, check, hud, hudLit, hudRim, pill, rim;
        static GUIStyle card, cardTint, cardRim;  // choice cards
        static GUIStyle capsule, capsuleRim;      // fully rounded pills (the menu's tip chip)
        static Texture2D chip;
        static readonly Dictionary<int, GUIStyle> gradStyles = new Dictionary<int, GUIStyle>();  // GlowPanel's boxes, by colour
        static GUIStyle glowStyle;               // a soft glow that falls off over GlowSpread px (Glow)
        static Texture2D dotFill, dotRing;       // true circles (Dot): the 9-sliced capsule collapses when it's small
        public const float GlowSpread = 34f;
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
            hudRim = Style(Rounded(9, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), Color.white, 2.5f), 9);
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
            capsule = Style(Rounded(15, Color.white, Color.white, new Color(1f, 1f, 1f, 0f), 0f), 15);
            capsuleRim = Style(Rounded(15, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), Color.white, 1.2f), 15);
            glowStyle = new GUIStyle();
            glowStyle.normal.background = GlowTexture();
            glowStyle.border = new RectOffset(36, 36, 36, 36);
            dotFill = Circle(32, Color.white, Color.white);
            dotRing = RingTexture(48, 3.2f);
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
            // A field with no box of its own, for text typed straight onto a card (your name on the menu's name card).
            bareField = new GUIStyle();
            bareField.focused.background = Rounded(4, new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.08f), new Color(0.42f, 0.6f, 1f, 0.6f), 1f);
            bareField.border = new RectOffset(6, 6, 6, 6);
            bareField.fontSize = 19;
            bareField.fontStyle = FontStyle.Bold;
            bareField.alignment = TextAnchor.MiddleLeft;
            bareField.padding = new RectOffset(4, 4, 0, 0);
            bareField.normal.textColor = bareField.focused.textColor = bareField.hover.textColor = Color.white;

            label = new GUIStyle(GUI.skin.label) { richText = false };

            track = new GUIStyle();  // the track itself is drawn by Slider()
            thumb = new GUIStyle();
            thumb.normal.background = thumb.hover.background = thumb.active.background = Circle(18, Color.white, Accent);
            thumb.fixedWidth = thumb.fixedHeight = 18f;
            meterThumb = new GUIStyle { fixedWidth = 1f, fixedHeight = 1f };  // invisible: the meter draws the value

            checkOff = Rounded(5, new Color(0.06f, 0.07f, 0.1f), new Color(0.07f, 0.08f, 0.12f), new Color(0.4f, 0.45f, 0.6f), 1.5f, 22);
            checkOn = Rounded(5, Accent, new Color(0.25f, 0.45f, 0.85f), new Color(0.6f, 0.75f, 1f), 1.5f, 22);
            check = new GUIStyle();
        }

        // --- Drawing ------------------------------------------------------------

        /// The blue the big boxes (the menu card, the lobby's body) are lit with.
        public static readonly Color PanelBlue = new Color(0.42f, 0.6f, 1f);

        /// A box shaded top to bottom in an accent colour (rich at the top, dark at the bottom) with a soft glow of the
        /// accent round it and a rim: the menu card, the lobby's body, the profile cards. glow 0..1, rim 0..1.
        public static void GlowPanel(Rect r, Color accent, float glow = 0.3f, float rim = 0.8f)
        {
            Glow(r, accent, glow);
            int key = (Mathf.RoundToInt(accent.r * 255f) << 16) | (Mathf.RoundToInt(accent.g * 255f) << 8) | Mathf.RoundToInt(accent.b * 255f);
            GUIStyle st;
            if (!gradStyles.TryGetValue(key, out st))
            {
                st = new GUIStyle();
                st.normal.background = GradientBox(accent);
                st.border = new RectOffset(12, 12, 12, 12);
                gradStyles[key] = st;
            }
            GUI.Box(r, GUIContent.none, st);
            Rim(r, Color.Lerp(accent, Color.white, 0.15f), rim);
        }

        /// The lobby's big box (and the menu card it grows into): blue gradient, soft glow.
        public static void BodyPanel(Rect r) => GlowPanel(r, PanelBlue, 0.5f, 0.85f);

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
            NoteHover(r);
            bool clicked = GUI.Button(r, Shown(r, text), button);
            clickedThisEvent |= clicked;
            GUI.backgroundColor = old;
            return clicked;
        }

        public static bool Button(Rect r, string text) => Button(r, text, Neutral);

        // --- Hover and click sounds ---------------------------------------------
        // Every button, card and click area reports itself while the mouse is over it (on Repaint). The last
        // one drawn is the one on top; ArenaApp plays a soft tick when that changes from one frame to the next.

        static Rect hoverNow, hoverLast;
        static bool hoverFound, hoverHadLast;

        /// A small round close button: a dim ring and an X that lights up blue under the mouse (the drawers' "Esc" button).
        public static bool CloseChip(Rect r)
        {
            bool hot = r.Contains(Event.current.mousePosition);
            var old = GUI.color;
            GUI.color = Faded(hot ? new Color(0.55f, 0.72f, 1f, 1f) : new Color(0.17f, 0.22f, 0.35f, 1f));
            GUI.DrawTexture(r, chip);
            GUI.color = Faded(hot ? new Color(0.18f, 0.43f, 0.85f, 1f) : new Color(0.07f, 0.085f, 0.14f, 1f));
            GUI.DrawTexture(new Rect(r.x + 1.5f, r.y + 1.5f, r.width - 3f, r.height - 3f), chip);
            GUI.color = old;
            float isz = Mathf.Round(r.width * 0.5f);
            Icon(new Rect(r.center.x - isz / 2f, r.center.y - isz / 2f, isz, isz), PixelArt.Get("ui_close"), hot ? Color.white : new Color(0.62f, 0.68f, 0.8f, 1f));
            return HitArea(r);
        }

        /// An invisible click area over something drawn by hand (cards, tabs, swatches). Counts for the hover sound.
        public static bool HitArea(Rect r)
        {
            NoteHover(r);
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            clickedThisEvent |= clicked;
            return clicked;
        }

        static bool clickedThisEvent;

        /// True once after a button, toggle or click area was clicked (ArenaApp plays the click sound).
        public static bool TakeClick()
        {
            bool c = clickedThisEvent;
            clickedThisEvent = false;
            return c;
        }

        public static void NoteHover(Rect r)
        {
            if (Event.current.type != EventType.Repaint || !GUI.enabled || !r.Contains(Event.current.mousePosition))
                return;
            var p = GUIUtility.GUIToScreenPoint(new Vector2(r.x, r.y));  // screen space, so rects inside groups compare fairly
            hoverNow = new Rect(p.x, p.y, r.width, r.height);
            hoverFound = true;
        }

        public static void BeginHoverFrame()
        {
            if (Event.current.type == EventType.Repaint)
                hoverFound = false;
        }

        /// At the end of a Repaint: true when the mouse has just moved onto a different button.
        public static bool EndHoverFrame()
        {
            if (Event.current.type != EventType.Repaint)
                return false;
            bool entered = hoverFound && (!hoverHadLast || Mathf.Abs(hoverNow.x - hoverLast.x) > 1f || Mathf.Abs(hoverNow.y - hoverLast.y) > 1f
                || Mathf.Abs(hoverNow.width - hoverLast.width) > 1f || Mathf.Abs(hoverNow.height - hoverLast.height) > 1f);
            hoverHadLast = hoverFound;
            hoverLast = hoverNow;
            return entered;
        }

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

        /// The menus' showpiece button: an icon chip, game-style lettering (two-tone with an
        /// outline, like titles), and on hover a lighter face and a shine sweeping across. No outline around it. t = time in seconds.
        public static bool FancyButton(Rect r, string text, Color tint, Texture2D icon, float t, int size = 0)
        {
            bool hover = r.Contains(Event.current.mousePosition) && GUI.enabled;
            bool clicked = Button(r, "", hover ? Color.Lerp(tint, Color.white, 0.12f) : tint);
            Fill(new Rect(r.x + 12f, r.y + 2f, r.width - 24f, 1f), new Color(1f, 1f, 1f, hover ? 0.4f : 0.22f));  // top highlight
            if (hover)
            {
                // A shine that sweeps across, clipped inside the button's rounded edge.
                float f = t * 0.85f % 1f, sx = -30f + (r.width + 40f) * f;
                GUI.BeginGroup(new Rect(r.x + 4f, r.y + 3f, r.width - 8f, r.height - 6f));
                Fill(new Rect(sx, 0f, 12f, r.height), new Color(1f, 1f, 1f, 0.1f));
                Fill(new Rect(sx + 16f, 0f, 4f, r.height), new Color(1f, 1f, 1f, 0.08f));
                GUI.EndGroup();
            }
            int fs = size > 0 ? size : r.height >= 44f ? 19 : r.height >= 34f ? 16 : 13;
            float cs = icon != null ? Mathf.Min(26f, r.height - 12f) : 0f, gap = icon != null ? 9f : 0f;
            float tw = TextWidth(text, fs, true) - 6f;
            while (fs > 11 && cs + gap + tw > r.width - 24f)
                tw = TextWidth(text, --fs, true) - 6f;  // long labels shrink to fit
            float x = r.center.x - (cs + gap + tw) / 2f;
            if (icon != null)
            {
                var c = new Rect(x, r.center.y - cs / 2f, cs, cs);
                var oc = GUI.color;
                GUI.color = Faded(new Color(1f, 1f, 1f, hover ? 0.36f : 0.24f));
                GUI.DrawTexture(c, chip);
                GUI.color = oc;
                float isz = Mathf.Round(cs * 0.62f);
                Icon(new Rect(c.center.x - isz / 2f, c.center.y - isz / 2f, isz, isz), icon);
            }
            Fancy(new Rect(x + cs + gap, r.y, tw + 12f, r.height), text, fs, Color.white, Color.Lerp(tint, Color.white, 0.7f), TextAnchor.MiddleLeft,
                hover ? (Color?)new Color(tint.r, tint.g, tint.b, 0.9f) : null);
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

        /// A thin bar whose two ends fade out (the colour line along the bottom of a choice card): a solid middle and a
        /// smoothstep ramp at each end, 28 px long (or 30% of a short bar). It's ONE quad with a baked gradient. It used to
        /// be a solid piece plus 28 thin slices, and wherever the GUI snapped two neighbours to different pixels a one-pixel
        /// gap showed in the bar (on some buttons, always on the right); one quad has nothing to join.
        public static void FadedBar(Rect r, Color c)
        {
            if (r.width < 1f)
                return;
            float fade = Mathf.Min(28f, r.width * 0.3f);
            var old = GUI.color;
            GUI.color = Faded(c);
            GUI.DrawTexture(r, FadeTexture(Mathf.Clamp(Mathf.RoundToInt(fade / r.width * 100f), 1, 30)));
            GUI.color = old;
        }

        static readonly Texture2D[] fadeTextures = new Texture2D[31];  // by the ramp's share of the bar's width, in hundredths

        /// White with an alpha ramp up over the first `hundredths`% of its width, flat, then down over the last.
        static Texture2D FadeTexture(int hundredths)
        {
            var tex = fadeTextures[hundredths];
            if (tex != null)
                return tex;
            const int w = 128;
            var px = new Color32[w];
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float d = Mathf.Clamp01(Mathf.Min(u, 1f - u) / (hundredths / 100f));
                float a = d * d * (3f - 2f * d);
                px[x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex = new Texture2D(w, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return fadeTextures[hundredths] = tex;
        }

        /// One option of a row of choices, drawn as a card: dark with a colour bar along the bottom; the selected
        /// one is washed with its colour, rimmed in it and glows. With an icon (and an optional second line such
        /// as "276 HP") it becomes a small icon card: icon on top, name, then the second line.
        /// glow: false leaves out the soft halo around the selected one (the lobby loadout). iconLeft: the icon sits
        /// left of the name and second line, the group centred (the lobby's role cards).
        public static bool Choice(Rect r, string text, bool selected, Color accent, Texture2D icon = null, string sub = null, int textSize = 0, bool glow = true, bool iconLeft = false)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            var old = GUI.backgroundColor;
            if (glow && selected && r.height >= 30f)  // soft glow behind (left off small rows, where it looks blocky)
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
            FadedBar(new Rect(r.x + 5f, r.yMax - barH - 2f, r.width - 10f, barH), new Color(accent.r, accent.g, accent.b, selected ? 1f : hover ? 0.6f : 0.35f));

            Color name = selected ? Color.white : Color.Lerp(accent, Color.white, 0.2f);
            int size = textSize > 0 ? textSize : r.height < 30f ? 12 : 14;
            if (icon != null && iconLeft)
            {
                int ns = sub != null ? 15 : size;  // without a second line: the row's text size (shrunk to fit by the caller)
                float isz = sub != null ? Mathf.Min(28f, r.height * 0.5f) : Mathf.Min(16f, r.height * 0.42f);
                float tw = Mathf.Max(TextWidth(text, ns, true), sub != null ? TextWidth(sub, 11) : 0f);
                float igap = sub != null ? 10f : 5f;
                float gx = r.center.x - (isz + igap + tw) / 2f;
                Icon(new Rect(gx, r.center.y - isz / 2f - 1f, isz, isz), icon);
                float tx = gx + isz + igap;
                if (sub != null)
                {
                    Label(new Rect(tx, r.center.y - 19f, tw + 8f, 20f), text, 15, name, TextAnchor.MiddleLeft, true);
                    Label(new Rect(tx, r.center.y + 1f, tw + 8f, 16f), sub, 11, selected ? new Color(1f, 1f, 1f, 0.8f) : Muted, TextAnchor.MiddleLeft);
                }
                else
                    Label(new Rect(tx, r.y, tw + 8f, r.height - barH), text, ns, name, TextAnchor.MiddleLeft, true);
            }
            else if (icon != null)
            {
                float isz = Mathf.Min(24f, r.height * 0.36f);
                Icon(new Rect(r.center.x - isz / 2f, r.y + 6f, isz, isz), icon);
                Label(new Rect(r.x, r.y + 8f + isz, r.width, 18f), text, textSize > 0 ? textSize : 15, name, TextAnchor.MiddleCenter, true);
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
            return Ui.HitArea(r);
        }

        public static string Field(Rect r, string value, int maxLength)
        {
            return GUI.TextField(r, value, maxLength, field);
        }

        /// A text field with no box: the text sits on whatever is behind it (a faint box shows while typing).
        public static string BareField(Rect r, string value, int maxLength, int size)
        {
            bareField.fontSize = size;
            return GUI.TextField(r, value, maxLength, bareField);
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

        static readonly System.Collections.Generic.Dictionary<int, float> meterKick = new System.Collections.Generic.Dictionary<int, float>();

        /// Level-meter slider (Settings > Audio): 100 thin bars that light up to the value, amber then red at the top, in `tint`.
        /// Drag or click anywhere on it; r should be about 36 px tall. Lit bars ripple like a live meter (faster and
        /// livelier the higher the value), glow, and the bars at the value kick up when it changes, then settle.
        public static float MeterSlider(Rect r, float value, Color tint)
        {
            const int Segs = 100;
            float v = GUI.HorizontalSlider(r, value, 0f, 1f, track, meterThumb);
            float t = Time.unscaledTime;
            int id = Mathf.RoundToInt(r.y * 7f + r.x);
            float kick;
            meterKick.TryGetValue(id, out kick);
            if (v != value)
                kick = 1f;
            else
                kick = Mathf.Max(0f, kick - Time.unscaledDeltaTime * 2.2f);
            meterKick[id] = kick;
            float gap = 2f, bw = (r.width - gap * (Segs - 1)) / Segs;
            int peak = Mathf.RoundToInt(v * Segs) - 1;
            for (int i = 0; i < Segs; i++)
            {
                float h = r.height * (0.5f + 0.3f * Mathf.Abs(Mathf.Sin(i * 0.12f)));
                bool on = (i + 0.5f) / Segs <= v;
                Color c = !on ? new Color(0.17f, 0.2f, 0.28f) : i >= Segs * 0.85f ? new Color(1f, 0.33f, 0.44f) : i >= Segs * 0.7f ? new Color(1f, 0.71f, 0.28f) : tint;
                if (on)
                {
                    // only while moving: a wave runs back from the value and dies out as the kick fades
                    float near = Mathf.Clamp01(1f - Mathf.Abs(i - peak) / 20f);
                    float wave = 0.5f + 0.5f * Mathf.Sin(t * 18f - i * 0.3f);
                    h += r.height * 0.45f * kick * near * wave;
                }
                h = Mathf.Min(r.height, h);
                var bar = new Rect(r.x + i * (bw + gap), r.yMax - h, bw, h);
                if (on)
                    Fill(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 1f), new Color(c.r, c.g, c.b, 0.18f));
                Fill(bar, c);
            }
            return v;
        }

        public static bool Toggle(Rect r, bool value, string text)
        {
            var box = new Rect(r.x, r.y + (r.height - 22f) / 2f, 22f, 22f);
            GUI.DrawTexture(box, value ? checkOn : checkOff);
            if (value)
                Fill(new Rect(box.x + 6f, box.y + 6f, 10f, 10f), Color.white);
            Label(new Rect(r.x + 32f, r.y, r.width - 32f, r.height), text, 14, Text);
            NoteHover(r);
            bool now = GUI.Toggle(r, value, GUIContent.none, check);
            clickedThisEvent |= now != value;
            return now;
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

        /// Just a HUD panel's rim, in a colour (an ability tile flashing; HudTiles.cs).
        public static void HudRim(Rect r, Color c)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = c;
            GUI.Box(r, GUIContent.none, hudRim);
            GUI.backgroundColor = old;
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
        /// A fully rounded pill (ends are half circles, up to 30 px tall) in a colour, with an optional rim.
        public static void Capsule(Rect r, Color fill, Color? rimColor = null)
        {
            if (r.width <= 0f || r.height <= 0f)
                return;
            var old = GUI.backgroundColor;
            GUI.backgroundColor = fill;
            GUI.Box(r, GUIContent.none, capsule);
            if (rimColor.HasValue)
            {
                GUI.backgroundColor = rimColor.Value;
                GUI.Box(r, GUIContent.none, capsuleRim);
            }
            GUI.backgroundColor = old;
        }

        /// A soft glow round a rect: colour c, strength 0..1 at the rect's edge, falling off smoothly over GlowSpread px.
        /// Draw it before the thing it surrounds (the rect itself is solid, so that covers it).
        public static void Glow(Rect r, Color c, float strength)
        {
            if (strength <= 0.01f)
                return;
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(c.r, c.g, c.b, Mathf.Clamp01(strength) * Alpha);
            GUI.Box(new Rect(r.x - GlowSpread, r.y - GlowSpread, r.width + GlowSpread * 2f, r.height + GlowSpread * 2f), GUIContent.none, glowStyle);
            GUI.backgroundColor = old;
        }

        /// A filled circle (or just its ring, when ring is true), in a colour: for stack counters.
        public static void Dot(Rect r, Color c, bool ring = false)
        {
            var old = GUI.color;
            GUI.color = Faded(c);
            GUI.DrawTexture(r, ring ? dotRing : dotFill);
            GUI.color = old;
        }

        /// A rim only (no fill), in a colour: the outline of a card whose body is drawn some other way.
        public static void Rim(Rect r, Color c, float strength)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(c.r, c.g, c.b, Mathf.Clamp01(strength));
            GUI.Box(r, GUIContent.none, cardRim);
            GUI.backgroundColor = old;
        }

        /// A texture stretched over a rect (card bodies from CardArt).
        public static void Picture(Rect r, Texture2D tex)
        {
            var old = GUI.color;
            GUI.color = Faded(Color.white);
            GUI.DrawTexture(r, tex);
            GUI.color = old;
        }

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

        /// A tall 9-sliceable box: a vertical gradient from the accent's wash at the top to near-black at the bottom, rounded corners.
        static Texture2D GradientBox(Color accent)
        {
            const int w = 32, h = 128;
            const float radius = 10f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[w * h];
            var bottom = new Color(0.055f, 0.065f, 0.1f, 0.97f);
            var top = new Color(0.1f + accent.r * 0.22f, 0.11f + accent.g * 0.22f, 0.16f + accent.b * 0.22f, 0.97f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 1f - (y + 0.5f) / h;  // 0 at the top
                    float t = Mathf.Clamp01(v / 0.95f);
                    t = t * t * (3f - 2f * t);
                    var c = Color.Lerp(top, bottom, t);
                    float qx = Mathf.Abs(x + 0.5f - w / 2f) - (w / 2f - radius), qy = Mathf.Abs(y + 0.5f - h / 2f) - (h / 2f - radius);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    c.a *= Mathf.Clamp01(0.5f - d);
                    px[y * w + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// 80 px: a small solid box in the middle with alpha falling off smoothly to nothing at the edge (9-sliced by Glow).
        static Texture2D GlowTexture()
        {
            const int size = 80;
            var tex = NewTexture(size);
            var px = new Color[size * size];
            const float lo = 34f, hi = 46f, radius = 5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - size / 2f) - ((hi - lo) / 2f - radius);
                    float qy = Mathf.Abs(y + 0.5f - size / 2f) - ((hi - lo) / 2f - radius);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float f = Mathf.Clamp01(1f - Mathf.Max(d, 0f) / lo);
                    px[y * size + x] = new Color(1f, 1f, 1f, f * f * (3f - 2f * f));  // smoothstep: bright at the rim, soft far out
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// A ring of the given thickness (px at this size), white.
        static Texture2D RingTexture(int size, float thickness)
        {
            var tex = NewTexture(size);
            var px = new Color[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    float outer = Mathf.Clamp01(c - 1f - d + 0.5f), inner = Mathf.Clamp01(d - (c - 1f - thickness) + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, outer * inner);
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
