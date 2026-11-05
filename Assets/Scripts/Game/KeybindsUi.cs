using UnityEngine;

namespace Arena
{
    /// Settings → Keybinds: every changeable control as a name and a key cap. Click a cap, then press the new key (a mouse
    /// side or middle button works too); Esc cancels. A key another action uses swaps over. Saved at once (Keybinds.cs).
    public partial class ArenaApp
    {
        float keybindScroll;  // the page's scroll offset

        void DrawKeybindsSection(Color col, float t, float k)
        {
            OpenSectionPanel("KEYBINDS", col, null, k);
            float px = DrawerPanel.x + 26f, pw = DrawerPanel.width - 52f;
            Keybinds.Listening = Mathf.Clamp(Keybinds.Listening, -1, Keybinds.All.Length - 1);

            // The groups scroll with the mouse wheel inside this view; positions below are local to it.
            var view = new Rect(DrawerPanel.x + 20f, DrawerPanel.y + 76f, DrawerPanel.width - 40f, DrawerPanel.yMax - 66f - (DrawerPanel.y + 76f));
            float x = 6f, w = view.width - 24f;
            float colW = (w - 28f) / 2f;

            int hovered = -1;
            // Each group is a heading and its rows; the left column holds Movement and Abilities, the right the rest.
            float yL = 8f, yR = 8f;
            var ev = Event.current;
            bool inView = view.Contains(ev.mousePosition);
            GUI.BeginGroup(view);
            for (int g = 0; g < Keybinds.GroupNames.Length; g++)
            {
                bool left = g < 2;  // Movement and Abilities on the left; Weapon, Items and Toggles on the right
                float gx = left ? x : x + colW + 28f;
                float gy = (left ? yL : yR) - keybindScroll;
                Ui.Label(new Rect(gx + 2f, gy, colW, 16f), Keybinds.GroupNames[g], 11, Ui.Muted, TextAnchor.MiddleLeft, true);
                Ui.Fill(new Rect(gx, gy + 18f, colW, 1f), new Color(1f, 1f, 1f, 0.08f));
                gy += 24f;
                int first = (int)Keybinds.GroupStart[g];
                int end = g + 1 < Keybinds.GroupNames.Length ? (int)Keybinds.GroupStart[g + 1] : Keybinds.All.Length;
                for (int i = first; i < end; i++)
                {
                    var row = new Rect(gx, gy, colW, 31f);
                    if (KeybindRow(row, (KeyAction)i, col, t))
                        Keybinds.Listening = Keybinds.Listening == i ? -1 : i;
                    if (inView && row.Contains(ev.mousePosition))
                        hovered = i;
                    gy += 34f;
                }
                gy += 12f;
                if (left) yL = gy + keybindScroll; else yR = gy + keybindScroll;
            }

            // The right column's last: put everything back.
            var reset = new Rect(x + colW + 28f, yR + 4f - keybindScroll, colW, 38f);
            if (Ui.SmallButton(reset, "Reset all keys to their defaults", Ui.Neutral, 13))
            {
                Keybinds.Listening = -1;
                Keybinds.ResetAll();
                Keybinds.Notice = "Every key is back to its default";
                Keybinds.NoticeAt = Time.unscaledTime;
            }
            GUI.EndGroup();

            float contentH = Mathf.Max(yL, yR + 4f + 38f) + 8f;
            float maxScroll = Mathf.Max(0f, contentH - view.height);
            if (ev.type == EventType.ScrollWheel && view.Contains(ev.mousePosition))
            {
                keybindScroll = Mathf.Clamp(keybindScroll + ev.delta.y * 24f, 0f, maxScroll);
                ev.Use();
            }
            keybindScroll = Mathf.Clamp(keybindScroll, 0f, maxScroll);
            if (maxScroll > 0f)
            {
                float th = view.height * view.height / contentH, ty = view.y + (view.height - th) * (keybindScroll / maxScroll);
                Ui.Fill(new Rect(view.xMax - 6f, ty, 3f, th), new Color(1f, 1f, 1f, 0.3f));
            }

            // Fire and Parry can also take a click, which can't be pressed while the page is the one being clicked: two buttons.
            var la = (KeyAction)Keybinds.Listening;
            if (Keybinds.Listening >= 0 && (la == KeyAction.Fire || la == KeyAction.Parry))
            {
                Ui.Label(new Rect(px + pw - 340f, DrawerPanel.yMax - 58f, 60f, 32f), "Or use", 13, Ui.Muted, TextAnchor.MiddleLeft, true);
                if (Ui.SmallButton(new Rect(px + pw - 280f, DrawerPanel.yMax - 58f, 130f, 32f), "Left click", Ui.Neutral, 13))
                    FinishMouseBind(la, KeyCode.Mouse0);
                if (Ui.SmallButton(new Rect(px + pw - 142f, DrawerPanel.yMax - 58f, 130f, 32f), "Right click", Ui.Neutral, 13))
                    FinishMouseBind(la, KeyCode.Mouse1);
            }

            // The line along the bottom: what a key does (hover) or a message about the last change.
            string info = null;
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
            if (info != null)
                Ui.Label(new Rect(px, DrawerPanel.yMax - 54f, pw - 350f, 36f), info, 13, infoCol, TextAnchor.UpperLeft, false, true);
        }

        void FinishMouseBind(KeyAction a, KeyCode mouse)
        {
            Keybinds.Listening = -1;
            Keybinds.Assign(a, mouse);
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
