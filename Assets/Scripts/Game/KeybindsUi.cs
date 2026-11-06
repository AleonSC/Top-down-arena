using UnityEngine;

namespace Arena
{
    /// Settings → Keybinds: every changeable control as a name and a digital key. Click a key, then press the new one (a mouse
    /// side or middle button works too; the key reads "PRESS KEY_" meanwhile); Esc cancels. A key another action uses swaps
    /// over. Saved at once (Keybinds.cs). Drawn in the Settings window (CrtWindows.cs).
    public partial class ArenaApp
    {
        float keybindScroll;  // the page's scroll offset

        void DrawKeybindsSection(Color col, float t, float k)
        {
            OpenSectionPanel("KEYBINDS", col, "Click a key, then press the new one", k);
            float px = DrawerPanel.x + 28f, pw = DrawerPanel.width - 56f;
            Keybinds.Listening = Mathf.Clamp(Keybinds.Listening, -1, Keybinds.All.Length - 1);

            // The groups scroll with the mouse wheel inside this view; positions below are local to it.
            var view = new Rect(DrawerPanel.x + 20f, DrawerPanel.y + 98f, DrawerPanel.width - 40f, DrawerPanel.yMax - 70f - (DrawerPanel.y + 98f));
            float x = 8f, w = view.width - 24f;
            float colW = (w - 32f) / 2f;

            int hovered = -1;
            // Each group is a heading and its rows; the left column holds Movement and Abilities, the right the rest.
            float yL = 4f, yR = 4f;
            var ev = Event.current;
            bool inView = view.Contains(ev.mousePosition);
            GUI.BeginGroup(view);
            for (int g = 0; g < Keybinds.GroupNames.Length; g++)
            {
                bool left = g < 2;  // Movement and Abilities on the left; Weapon, Items and Toggles on the right
                float gx = left ? x : x + colW + 32f;
                float gy = (left ? yL : yR) - keybindScroll;
                Ui.Label(new Rect(gx + 2f, gy, colW, 16f), Keybinds.GroupNames[g].ToUpperInvariant(), 11, CabSoft, TextAnchor.MiddleLeft, true);
                DashLine(gx, gy + 19f, colW);
                gy += 26f;
                int first = (int)Keybinds.GroupStart[g];
                int end = g + 1 < Keybinds.GroupNames.Length ? (int)Keybinds.GroupStart[g + 1] : Keybinds.All.Length;
                for (int i = first; i < end; i++)
                {
                    var row = new Rect(gx, gy, colW, 34f);
                    if (KeybindRow(row, (KeyAction)i, col, t))
                        Keybinds.Listening = Keybinds.Listening == i ? -1 : i;
                    if (inView && row.Contains(ev.mousePosition))
                        hovered = i;
                    gy += 38f;
                }
                gy += 12f;
                if (left) yL = gy + keybindScroll; else yR = gy + keybindScroll;
            }

            // The right column's last: put everything back.
            var reset = new Rect(x + colW + 32f, yR + 4f - keybindScroll, colW, 38f);
            if (WinKey(reset, "RESET ALL KEYS", false, 15))
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
                Ui.Fill(new Rect(view.xMax - 6f, ty, 4f, th), new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.5f));
            }

            // Fire and Parry can also take a click, which can't be pressed while the page is the one being clicked: two keys.
            var la = (KeyAction)Keybinds.Listening;
            if (Keybinds.Listening >= 0 && (la == KeyAction.Fire || la == KeyAction.Parry))
            {
                Ui.Label(new Rect(px + pw - 350f, DrawerPanel.yMax - 58f, 60f, 34f), "OR USE", 12, CabSoft, TextAnchor.MiddleLeft, true);
                if (WinKey(new Rect(px + pw - 286f, DrawerPanel.yMax - 58f, 134f, 34f), "LEFT CLICK", false, 15))
                    FinishMouseBind(la, KeyCode.Mouse0);
                if (WinKey(new Rect(px + pw - 146f, DrawerPanel.yMax - 58f, 146f, 34f), "RIGHT CLICK", false, 15))
                    FinishMouseBind(la, KeyCode.Mouse1);
            }

            // The line along the bottom: what a key does (hover) or a message about the last change.
            string info = null;
            Color infoCol = CabSoft;
            if (Keybinds.Listening >= 0)
            {
                info = "Press a key for " + Keybinds.Title((KeyAction)Keybinds.Listening) + "  ·  Esc to cancel";
                infoCol = CabWarm;
            }
            else if (!string.IsNullOrEmpty(Keybinds.Notice) && Time.unscaledTime - Keybinds.NoticeAt < 4f)
            {
                info = Keybinds.Notice;
                infoCol = CabWarm;
            }
            else if (hovered >= 0)
                info = Keybinds.Title((KeyAction)hovered) + ": " + Keybinds.Line((KeyAction)hovered);
            if (info != null)
                Ui.Label(new Rect(px, DrawerPanel.yMax - 58f, pw - 360f, 40f), info, 13, infoCol, TextAnchor.UpperLeft, false, true);
        }

        void FinishMouseBind(KeyAction a, KeyCode mouse)
        {
            Keybinds.Listening = -1;
            Keybinds.Assign(a, mouse);
        }

        /// One action: its name, and its key as a digital key you click ("PRESS KEY_" while it listens). Returns true when the
        /// key was clicked.
        bool KeybindRow(Rect r, KeyAction a, Color col, float t)
        {
            bool listening = Keybinds.Listening == (int)a;
            bool changed = !Keybinds.IsDefault(a);
            Ui.Label(new Rect(r.x + 4f, r.y, r.width - 190f, r.height), Keybinds.Title(a), 15, listening ? CrtUi.Ink : CabSoft, TextAnchor.MiddleLeft, true);
            var cap = new Rect(r.xMax - 154f, r.y + 2f, 150f, r.height - 4f);
            if (changed && !listening)
            {
                var undo = new Rect(cap.x - 34f, cap.y, 30f, cap.height);
                if (WinKey(undo, "↺", false, 15, CrtUi.Dim))
                    Keybinds.Reset(a);
            }
            string text = listening ? "PRESS KEY" + (CabCalm || Mathf.FloorToInt(t * 2f) % 2 == 0 ? "_" : " ") : Keybinds.KeyName(Keybinds.Get(a)).ToUpperInvariant();
            return WinKey(cap, text, listening, 15, changed && !listening ? CabWarm : (Color?)null);
        }
    }
}
