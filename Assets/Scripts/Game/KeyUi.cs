using UnityEngine;

namespace Arena
{
    /// The menu's mechanical buttons (style A, "Keycaps", of Docs/Mockups/mech-buttons.html): a keycap with a dished top sitting in a
    /// dark well, a lamp in its corner that lights under the mouse, and a few pixels of travel when pressed. Used for the main menu's
    /// Play, Join game and Settings and the Play screen's New run and Back (the green CRT controls stay digital).
    public static class KeyUi
    {
        static readonly Color WellCol = new Color(0.03f, 0.04f, 0.09f);
        static readonly Color LampOff = new Color(0.07f, 0.1f, 0.25f);
        static readonly Color LampOn = new Color(0.42f, 1f, 0.6f);
        static int pressedKey;  // the key the mouse went down on (until it comes up)

        static void Legend(Rect cap, string text, Texture2D icon, int size, float a, System.Action<Rect, float> drawIcon = null, float iconW = 0f)
        {
            float isz = Mathf.Min(size * 1.1f, cap.height * 0.5f);
            bool hasIcon = icon != null || drawIcon != null;
            float tw = Ui.TextWidth(text, size, true) - 6f, gap = hasIcon ? 12f : 0f, ic = drawIcon != null && iconW > 0f ? iconW : hasIcon ? isz : 0f;
            float x = cap.center.x - (ic + gap + tw) / 2f;
            if (drawIcon != null)
                drawIcon(new Rect(x, cap.center.y - isz / 2f, ic, isz), a);  // a drawn icon (the main menu's Play chevrons, MenuHolo.cs)
            else if (icon != null)
                Ui.Icon(new Rect(x, cap.center.y - isz / 2f, isz, isz), icon, new Color(1f, 1f, 1f, a));
            var tr = new Rect(x + ic + gap, cap.y, tw + 12f, cap.height);
            Ui.Label(new Rect(tr.x, tr.y + 1f, tr.width, tr.height), text, size, new Color(0f, 0f, 0f, 0.5f * a), TextAnchor.MiddleLeft, true);
            Ui.Label(tr, text, size, new Color(0.93f, 0.95f, 1f, a), TextAnchor.MiddleLeft, true);
        }

        /// A screw head: a dark ring, a metal dome with a highlight, and a slot.
        public static void Bolt(Vector2 c, float d)
        {
            Ui.Dot(new Rect(c.x - d / 2f - 1f, c.y - d / 2f - 1f, d + 2f, d + 2f), new Color(0.03f, 0.04f, 0.1f));
            Ui.Dot(new Rect(c.x - d / 2f, c.y - d / 2f, d, d), new Color(0.3f, 0.39f, 0.68f));
            Ui.Dot(new Rect(c.x - d * 0.3f, c.y - d * 0.35f, d * 0.4f, d * 0.4f), new Color(0.6f, 0.7f, 0.95f));
            Ui.Fill(new Rect(c.x - d * 0.3f, c.y, d * 0.6f, 1f), new Color(0.03f, 0.04f, 0.1f, 0.8f));
        }

        /// The riveted metal frame around the runs screen (Docs/Mockups/menu-bay-doors.html): drawn under the screen, 7 px out on every side.
        public static void Bezel(Rect screen)
        {
            var r = new Rect(screen.x - 7f, screen.y - 7f, screen.width + 14f, screen.height + 14f);
            Ui.Pill(new Rect(r.x, r.y + 4f, r.width, r.height), new Color(0.03f, 0.04f, 0.09f, 0.55f));   // drop shadow
            Ui.Pill(r, new Color(0.42f, 0.52f, 0.81f));                                                     // lit rim
            var f = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f);
            Ui.Pill(f, new Color(0.14f, 0.18f, 0.37f));
            Band(new Rect(f.x + 1f, f.y + 1f, f.width - 2f, f.height - 2f), new Color(0.23f, 0.29f, 0.51f), new Color(0.1f, 0.13f, 0.27f), 8);
            Ui.Fill(new Rect(f.x + 5f, f.y + 1f, f.width - 10f, 1f), new Color(1f, 1f, 1f, 0.25f));        // the lit top edge
            if (r.width > 40f && r.height > 40f)
                foreach (var c in new[] { new Vector2(r.x + 6f, r.y + 6f), new Vector2(r.xMax - 6f, r.y + 6f), new Vector2(r.x + 6f, r.yMax - 6f), new Vector2(r.xMax - 6f, r.yMax - 6f) })
                    Bolt(c, 5f);
        }

        /// A vertical gradient drawn as bands (the game's boxes are flat, so depth is built from strips).
        static void Band(Rect r, Color top, Color bottom, int steps)
        {
            steps = Mathf.Max(steps, Mathf.FloorToInt(r.height / 3f));  // thin strips, so no stripes show
            float h = r.height / steps;
            for (int i = 0; i < steps; i++)
                Ui.Fill(new Rect(r.x, r.y + i * h, r.width, h + 0.5f), Color.Lerp(top, bottom, (i + 0.5f) / steps));
        }

        /// The Who's playing? screen's Manage profiles key style (a mockup, picked in previews with WHOKEY=): 0 the blue keycap, 1 Graphite, 2 Machined.
        public static int WhoStyle;

        /// A smooth vertical gradient: one strip per pixel row, so no bands show.
        static void Smooth(Rect r, Color top, Color bottom)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(r.height));
            float h = r.height / n;
            for (int i = 0; i < n; i++)
                Ui.Fill(new Rect(r.x, r.y + i * h, r.width, h + 0.5f), Color.Lerp(top, bottom, (i + 0.5f) / n));
        }

        /// A clean graphite keycap (style 1 Graphite, style 2 Machined): a recessed well, a short skirt the cap travels down over,
        /// a smooth satin face with one lit edge, an LED in a slot at the left (lit under the mouse or when lampOn), and the legend
        /// in light grey. Machined adds knurled grip ridges at both ends and a flush screw in each. True when clicked.
        public static bool Graphite(Rect r, string text, Texture2D icon, int size, bool lampOn = false, bool machined = false)
        {
            var e = Event.current;
            int id = Mathf.RoundToInt(r.y * 17f + r.width) + Mathf.FloorToInt(r.x / 8f) * 7919 + 3;
            bool hover = GUI.enabled && r.Contains(e.mousePosition);
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && hover)
                pressedKey = id;
            else if (e.type == EventType.MouseUp)
                pressedKey = 0;
            bool pressed = hover && pressedKey == id;
            float lit = hover ? 1f : 0f;
            float travel = 8f;
            // The well: a dark recess with a soft inner shadow on top and a faint lit lip at the bottom.
            var well = new Rect(r.x - 6f, r.y - 5f, r.width + 12f, r.height + 11f);
            Ui.Pill(new Rect(well.x - 1f, well.y - 1f, well.width + 2f, well.height + 2f), new Color(0.2f, 0.21f, 0.23f));
            Ui.Pill(well, new Color(0.035f, 0.037f, 0.042f));
            Ui.Fill(new Rect(well.x + 6f, well.y + 2f, well.width - 12f, 2f), new Color(0f, 0f, 0f, 0.6f));
            Ui.Fill(new Rect(well.x + 8f, well.yMax - 2f, well.width - 16f, 1f), new Color(1f, 1f, 1f, 0.1f));
            // The skirt under the cap (seen below it), then the cap, pushed down when pressed.
            var skirt = new Rect(r.x, r.y + travel, r.width, r.height - travel);
            Ui.Pill(skirt, new Color(0.06f, 0.063f, 0.07f));
            Smooth(new Rect(skirt.x + 3f, skirt.yMax - travel - 3f, skirt.width - 6f, travel), new Color(0.2f, 0.21f, 0.23f), new Color(0.09f, 0.095f, 0.1f));
            float drop = pressed ? travel - 2f : 0f;
            var cap = new Rect(r.x, r.y + drop, r.width, r.height - travel);
            Ui.Pill(cap, Color.Lerp(new Color(0.3f, 0.31f, 0.34f), new Color(0.46f, 0.48f, 0.52f), lit * 0.6f));
            var face = new Rect(cap.x + 2f, cap.y + 2f, cap.width - 4f, cap.height - 4f);
            Ui.Pill(face, new Color(0.17f, 0.18f, 0.2f));
            Smooth(new Rect(face.x + 3f, face.y + 1f, face.width - 6f, face.height - 2f),
                Color.Lerp(new Color(0.27f, 0.28f, 0.31f), new Color(0.33f, 0.345f, 0.38f), lit),
                Color.Lerp(new Color(0.12f, 0.125f, 0.14f), new Color(0.16f, 0.17f, 0.19f), lit));
            Ui.Fill(new Rect(face.x + 8f, face.y + 1f, face.width - 16f, 1f), new Color(1f, 1f, 1f, 0.38f));     // the lit top edge
            Ui.Fill(new Rect(face.x + 14f, face.y + 2f, face.width - 28f, 1f), new Color(1f, 1f, 1f, 0.1f));
            Ui.Fill(new Rect(face.x + 8f, face.yMax - 2f, face.width - 16f, 1f), new Color(0f, 0f, 0f, 0.45f));   // the shaded bottom edge
            float inset = 12f;
            if (machined)
            {
                // Knurled grips at both ends: fine raised ridges, each a lit line beside a dark one, and a flush screw past them.
                float gw = 20f;
                foreach (float gx in new[] { face.x + 8f, face.xMax - 8f - gw })
                {
                    for (float x = gx; x < gx + gw - 1f; x += 4f)
                    {
                        Ui.Fill(new Rect(x, face.y + 8f, 1f, face.height - 16f), new Color(1f, 1f, 1f, 0.13f));
                        Ui.Fill(new Rect(x + 1f, face.y + 8f, 2f, face.height - 16f), new Color(0f, 0f, 0f, 0.32f));
                    }
                }
                inset = 8f + gw + 10f;
            }
            // The LED: a dark slot with a lens that glows green when lit.
            var slot = new Rect(face.x + inset, face.center.y - 10f, 8f, 20f);
            Ui.Pill(new Rect(slot.x - 1f, slot.y - 1f, slot.width + 2f, slot.height + 2f), new Color(1f, 1f, 1f, 0.08f));
            Ui.Pill(slot, new Color(0.02f, 0.022f, 0.025f));
            var lens = new Rect(slot.x + 2f, slot.y + 2f, slot.width - 4f, slot.height - 4f);
            if (lampOn || hover)
            {
                Ui.Pill(new Rect(lens.x - 4f, lens.y - 4f, lens.width + 8f, lens.height + 8f), LampOn.WithAlpha(0.18f));
                Ui.Pill(lens, LampOn);
                Ui.Fill(new Rect(lens.x + 1f, lens.y + 2f, 1f, lens.height - 6f), new Color(1f, 1f, 1f, 0.55f));
            }
            else
                Ui.Pill(lens, new Color(0.11f, 0.17f, 0.13f));
            // The legend, engraved, centred in the space right of the LED.
            float isz = Mathf.Min(size * 1.05f, face.height * 0.5f);
            float tw = Ui.TextWidth(text, size, true) - 6f, gap = icon != null ? 12f : 0f, ic = icon != null ? isz : 0f;
            float left = slot.xMax + 6f, right = face.xMax - (machined ? inset : 12f);
            float x0 = Mathf.Max(left, (left + right) / 2f - (ic + gap + tw) / 2f);
            var ink = Color.Lerp(new Color(0.82f, 0.84f, 0.87f), new Color(0.97f, 0.98f, 1f), lit);
            if (icon != null)
            {
                Ui.Icon(new Rect(x0, face.center.y - isz / 2f + 1f, isz, isz), icon, new Color(0f, 0f, 0f, 0.5f));
                Ui.Icon(new Rect(x0, face.center.y - isz / 2f, isz, isz), icon, ink);
            }
            var tr = new Rect(x0 + ic + gap, face.y, tw + 12f, face.height);
            Ui.Label(new Rect(tr.x, tr.y + 1f, tr.width, tr.height), text, size, new Color(0f, 0f, 0f, 0.55f), TextAnchor.MiddleLeft, true);
            Ui.Label(tr, text, size, ink, TextAnchor.MiddleLeft, true);
            return Ui.HitArea(r);
        }

        /// The dark well around a key (a few pixels bigger than the key's rect), for rings and layout.
        public static Rect Well(Rect r) => new Rect(r.x - 7f, r.y - 6f, r.width + 14f, r.height + 13f);

        /// A keycap in rect r (its full height, travel included). lampOn keeps the lamp lit; otherwise it lights under the mouse.
        /// True when clicked. text2 / icon2 / mix swap the legend to a second label (mix 0 = the first, 1 = the second; the menu's
        /// Quit becoming Back, MenuServo.cs), and dip pushes the cap down that many extra pixels.
        public static bool Key(Rect r, string text, Texture2D icon, int size, bool lampOn = false, string text2 = null, Texture2D icon2 = null, float mix = 0f, float dip = 0f,
            System.Action<Rect, float> drawIcon = null, float iconW = 0f)
        {
            var e = Event.current;
            int id = Mathf.RoundToInt(r.y * 17f + r.width) + Mathf.FloorToInt(r.x / 8f) * 7919;
            bool hover = GUI.enabled && r.Contains(e.mousePosition);
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && hover)
                pressedKey = id;
            else if (e.type == EventType.MouseUp)
                pressedKey = 0;
            bool pressed = hover && pressedKey == id;
            float travel = Mathf.Clamp(r.height * 0.14f, 7f, 14f);
            var well = Well(r);
            Ui.Pill(well, WellCol);
            Ui.Fill(new Rect(well.x + 3f, well.y + 2f, well.width - 6f, 3f), new Color(0f, 0f, 0f, 0.55f));        // the well's inner shadow at the top
            Ui.Fill(new Rect(well.x + 4f, well.yMax - 2f, well.width - 8f, 1f), new Color(1f, 1f, 1f, 0.07f));      // and its lit lip at the bottom
            float drop = (pressed ? travel - 3f : 0f) + dip;
            var cap = new Rect(r.x, r.y + drop, r.width, r.height - travel);
            // A soft shadow under the key, then its side (a gradient from lit to dark), which the cap travels down over.
            Ui.Pill(new Rect(r.x + 2f, r.y + travel + 3f, r.width - 4f, r.height - travel), new Color(0f, 0f, 0f, 0.5f));
            var side = new Rect(r.x, r.y + travel, r.width, r.height - travel);
            Ui.Pill(side, new Color(0.07f, 0.1f, 0.24f));
            Band(new Rect(side.x + 2f, side.y, side.width - 4f, side.height - 2f), new Color(0.24f, 0.33f, 0.62f), new Color(0.09f, 0.13f, 0.3f), 6);
            // The cap: a rim, a face that is lit at the top and darker at the bottom, bevel lines, then the dished top.
            float lit = hover ? 0.1f : 0f;
            Ui.Pill(cap, Color.Lerp(new Color(0.22f, 0.3f, 0.56f), Color.white, lit));
            Band(new Rect(cap.x + 2f, cap.y + 2f, cap.width - 4f, cap.height - 4f), Color.Lerp(new Color(0.5f, 0.64f, 0.98f), Color.white, lit), Color.Lerp(new Color(0.27f, 0.36f, 0.68f), Color.white, lit), 10);
            Ui.Fill(new Rect(cap.x + 5f, cap.y + 1f, cap.width - 10f, 2f), new Color(1f, 1f, 1f, 0.6f));            // the lit top edge
            Ui.Fill(new Rect(cap.x + 5f, cap.yMax - 3f, cap.width - 10f, 2f), new Color(0f, 0f, 0f, 0.3f));         // the shaded bottom edge
            var dish = new Rect(cap.x + 8f, cap.y + 8f, cap.width - 16f, cap.height - 16f);
            Ui.Pill(dish, new Color(0.1f, 0.14f, 0.32f));
            Band(new Rect(dish.x + 1f, dish.y + 2f, dish.width - 2f, dish.height - 3f), new Color(0.14f, 0.19f, 0.4f), Color.Lerp(new Color(0.27f, 0.36f, 0.66f), Color.white, lit * 0.6f), 8);  // concave: dark at the top, lighter at the bottom
            Ui.Fill(new Rect(dish.x + 3f, dish.y + 1f, dish.width - 6f, 2f), new Color(0f, 0f, 0f, 0.4f));          // shadow under the dish's top lip
            Ui.Fill(new Rect(dish.x + 3f, dish.yMax - 1f, dish.width - 6f, 1f), new Color(1f, 1f, 1f, 0.22f));     // light on its bottom lip
            // The label, engraved: a dark copy under it, then the text; the icon sits before it, the pair centred.
            if (text2 == null)
                Legend(cap, text, icon, size, 1f, drawIcon, iconW);
            else
            {
                if (mix < 1f)
                    Legend(cap, text, icon, size, 1f - mix);
                if (mix > 0f)
                    Legend(cap, text2, icon2, size, mix);
            }
            // The lamp in the corner.
            float ls = Mathf.Clamp(cap.height / 9f, 6f, 11f);
            var lamp = new Rect(cap.xMax - 10f - ls, cap.y + 8f, ls, ls);
            if (lampOn || hover)
            {
                Ui.Fill(new Rect(lamp.x - 3f, lamp.y - 3f, ls + 6f, ls + 6f), LampOn.WithAlpha(0.22f));
                Ui.Pill(lamp, LampOn);
            }
            else
                Ui.Pill(lamp, LampOff);
            return Ui.HitArea(r);
        }
    }
}
