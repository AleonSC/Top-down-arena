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

        /// The dark well around a key (a few pixels bigger than the key's rect), for rings and layout.
        public static Rect Well(Rect r) => new Rect(r.x - 6f, r.y - 5f, r.width + 12f, r.height + 11f);

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
            float travel = Mathf.Clamp(r.height * 0.1f, 5f, 8f);
            Ui.Pill(Well(r), WellCol);
            Ui.Pill(new Rect(r.x, r.y + travel, r.width, r.height - travel), BaseCol);  // the key's side, in shade
            var cap = new Rect(r.x, r.y + (pressed ? travel - 2f : 0f), r.width, r.height - travel);
            Ui.Pill(cap, hover ? Color.Lerp(CapCol, Color.white, 0.12f) : CapCol);
            Ui.Pill(new Rect(cap.x + 5f, cap.y + 5f, cap.width - 10f, cap.height - 10f), DishCol);  // the dished top
            Ui.Fill(new Rect(cap.x + 7f, cap.y + 1f, cap.width - 14f, 1f), new Color(1f, 1f, 1f, 0.4f));
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
