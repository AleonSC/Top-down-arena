using UnityEngine;

namespace Arena
{
    /// Settings → Keybinds: every changeable control as a name and a key cap. Click a cap, then press the new key (a mouse
    /// side or middle button works too); Esc cancels. A key another action uses swaps over. Saved at once (Keybinds.cs).
    public partial class ArenaApp
    {
        void DrawKeybindsSection(Color col, float t, float k)
        {
            OpenSectionPanel("KEYBINDS", col, "Click a key, then press the one you want. Esc cancels. A key that's already in use swaps over.", k);
            float x = DrawerPanel.x + 26f, w = DrawerPanel.width - 52f, top = DrawerPanel.y + 112f;
            float colW = (w - 28f) / 2f;
            Keybinds.Listening = Mathf.Clamp(Keybinds.Listening, -1, Keybinds.All.Length - 1);

            int hovered = -1;
            // Each group is a heading and its rows; the left column holds Movement and Combat, the right the rest.
            float yL = top, yR = top;
            for (int g = 0; g < Keybinds.GroupNames.Length; g++)
            {
                bool left = g < 2;
                float gx = left ? x : x + colW + 28f;
                float gy = left ? yL : yR;
                Ui.Label(new Rect(gx + 2f, gy, colW, 16f), Keybinds.GroupNames[g], 11, Ui.Muted, TextAnchor.MiddleLeft, true);
                Ui.Fill(new Rect(gx, gy + 18f, colW, 1f), new Color(1f, 1f, 1f, 0.08f));
                gy += 26f;
                int first = (int)Keybinds.GroupStart[g];
                int end = g + 1 < Keybinds.GroupNames.Length ? (int)Keybinds.GroupStart[g + 1] : Keybinds.All.Length;
                for (int i = first; i < end; i++)
                {
                    var row = new Rect(gx, gy, colW, 34f);
                    if (KeybindRow(row, (KeyAction)i, col, t))
                        Keybinds.Listening = Keybinds.Listening == i ? -1 : i;
                    if (row.Contains(Event.current.mousePosition))
                        hovered = i;
                    gy += 38f;
                }
                gy += 14f;
                if (left) yL = gy; else yR = gy;
            }

            // The right column's last: put everything back.
            var reset = new Rect(x + colW + 28f, yR + 6f, colW, 40f);
            if (Ui.SmallButton(reset, "Reset all keys to their defaults", Ui.Neutral, 13))
            {
                Keybinds.Listening = -1;
                Keybinds.ResetAll();
                Keybinds.Notice = "Every key is back to its default";
                Keybinds.NoticeAt = Time.unscaledTime;
            }

            // The line along the bottom: what a key does (hover), a message about the last change, or the fixed keys.
            string info;
            Color infoCol = Ui.Muted;
            if (Keybinds.Listening >= 0)
            {
                info = "Press a key for " + Keybinds.Title((KeyAction)Keybinds.Listening) + "  ·  Esc to cancel";
                infoCol = Ui.Gold;
            }
            else if (!string.IsNullOrEmpty(Keybinds.Notice) && Time.unscaledTime - Keybinds.NoticeAt < 4f)
            {
                info = Keybinds.Notice;
                infoCol = Ui.Gold;
            }
            else if (hovered >= 0)
                info = Keybinds.Title((KeyAction)hovered) + ": " + Keybinds.Line((KeyAction)hovered);
            else
                info = "Fixed: left click fires, right click parries, Esc pauses, Tab shows your stats, 1-3 pick a card, Enter confirms.";
            Ui.Label(new Rect(x, DrawerPanel.yMax - 54f, w, 36f), info, 13, infoCol, TextAnchor.UpperLeft, false, true);
        }

        /// One action: its name, and its key as a cap you click. Returns true when the cap was clicked.
        bool KeybindRow(Rect r, KeyAction a, Color col, float t)
        {
            bool listening = Keybinds.Listening == (int)a;
            bool hover = r.Contains(Event.current.mousePosition);
            if (listening)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 7f);
                Ui.Pill(r, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.1f + 0.08f * pulse));
                Ui.Fill(new Rect(r.x, r.y, 3f, r.height), Ui.Gold);
            }
            else if (hover)
                Ui.Pill(r, new Color(1f, 1f, 1f, 0.04f));
            Ui.Label(new Rect(r.x + 12f, r.y, r.width - 190f, r.height), Keybinds.Title(a), 15, listening ? Color.white : Ui.Text, TextAnchor.MiddleLeft, true);

            bool changed = !Keybinds.IsDefault(a);
            var cap = new Rect(r.xMax - 150f, r.y + 3f, 140f, r.height - 6f);
            if (changed && !listening)
            {
                var undo = new Rect(cap.x - 30f, cap.y + 2f, 26f, cap.height - 4f);
                Ui.Pill(undo, new Color(1f, 1f, 1f, undo.Contains(Event.current.mousePosition) ? 0.14f : 0.06f));
                Ui.Label(undo, "↺", 15, Ui.Muted, TextAnchor.MiddleCenter, true);
                if (Ui.HitArea(undo))
                    Keybinds.Reset(a);
            }
            Ui.Pill(new Rect(cap.x, cap.y + 2f, cap.width, cap.height), new Color(0.03f, 0.035f, 0.06f, 0.9f));  // its side
            Color face = listening ? Color.Lerp(new Color(0.3f, 0.26f, 0.1f), new Color(0.5f, 0.42f, 0.12f), 0.5f + 0.5f * Mathf.Sin(t * 7f))
                : changed ? new Color(0.3f, 0.27f, 0.14f) : hover ? new Color(0.3f, 0.34f, 0.48f) : new Color(0.24f, 0.27f, 0.38f);
            Ui.Pill(new Rect(cap.x, cap.y, cap.width, cap.height - 1f), face);
            string text = listening ? "Press a key…" : Keybinds.KeyName(Keybinds.Get(a));
            Ui.Label(new Rect(cap.x, cap.y, cap.width, cap.height - 1f), text, 13, listening ? Ui.Gold : changed ? Ui.Gold : Color.white, TextAnchor.MiddleCenter, true);
            return Ui.HitArea(cap);
        }
    }
}
