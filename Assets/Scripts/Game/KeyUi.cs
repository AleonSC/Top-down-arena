using UnityEngine;

namespace Arena
{
    /// The menu's mechanical buttons (style A, "Keycaps", of Docs/Mockups/mech-buttons.html): a keycap with a dished top sitting in a
    /// dark well, a lamp in its corner that lights under the mouse, and a few pixels of travel when pressed. Used for the main menu's
    /// Play, Join game, Records and Settings and the Play screen's New run and Back (the green CRT controls stay digital).
    public static class KeyUi
    {
        static readonly Color WellCol = new Color(0.03f, 0.04f, 0.09f);
        static readonly Color BaseCol = new Color(0.08f, 0.11f, 0.24f);
        static readonly Color CapCol = new Color(0.34f, 0.44f, 0.72f);
        static readonly Color DishCol = new Color(0.17f, 0.23f, 0.44f);
        static readonly Color LampOff = new Color(0.07f, 0.1f, 0.25f);
        static readonly Color LampOn = new Color(0.42f, 1f, 0.6f);
        static int pressedKey;  // the key the mouse went down on (until it comes up)

        /// A vertical gradient drawn as bands (the game's boxes are flat, so depth is built from strips).
        static void Band(Rect r, Color top, Color bottom, int steps)
        {
            steps = Mathf.Max(steps, Mathf.FloorToInt(r.height / 3f));  // thin strips, so no stripes show
            float h = r.height / steps;
            for (int i = 0; i < steps; i++)
                Ui.Fill(new Rect(r.x, r.y + i * h, r.width, h + 0.5f), Color.Lerp(top, bottom, (i + 0.5f) / steps));
        }

        /// The dark well around a key (a few pixels bigger than the key's rect), for rings and layout.
        public static Rect Well(Rect r) => new Rect(r.x - 7f, r.y - 6f, r.width + 14f, r.height + 13f);

        /// A keycap in rect r (its full height, travel included). lampOn keeps the lamp lit; otherwise it lights under the mouse.
        /// True when clicked.
        public static bool Key(Rect r, string text, Texture2D icon, int size, bool lampOn = false)
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
            float drop = pressed ? travel - 3f : 0f;
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
            float isz = Mathf.Min(size * 1.1f, cap.height * 0.5f);
            float tw = Ui.TextWidth(text, size, true) - 6f, gap = icon != null ? 12f : 0f, ic = icon != null ? isz : 0f;
            float x = cap.center.x - (ic + gap + tw) / 2f;
            if (icon != null)
                Ui.Icon(new Rect(x, cap.center.y - isz / 2f, isz, isz), icon, Color.white);
            var tr = new Rect(x + ic + gap, cap.y, tw + 12f, cap.height);
            Ui.Label(new Rect(tr.x, tr.y + 1f, tr.width, tr.height), text, size, new Color(0f, 0f, 0f, 0.5f), TextAnchor.MiddleLeft, true);
            Ui.Label(tr, text, size, new Color(0.93f, 0.95f, 1f), TextAnchor.MiddleLeft, true);
            // The lamp in the corner.
            float ls = Mathf.Clamp(cap.height / 9f, 6f, 11f);
            var lamp = new Rect(cap.xMax - 10f - ls, cap.y + 8f, ls, ls);
            if (lampOn || hover)
            {
                Ui.Fill(new Rect(lamp.x - 3f, lamp.y - 3f, ls + 6f, ls + 6f), new Color(LampOn.r, LampOn.g, LampOn.b, 0.22f));
                Ui.Pill(lamp, LampOn);
            }
            else
                Ui.Pill(lamp, LampOff);
            return Ui.HitArea(r);
        }
    }
}
