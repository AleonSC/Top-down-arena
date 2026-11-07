using UnityEngine;

namespace Arena
{
    /// The chat box (Docs/Mockups/chat-box.html, look C "Bubbles"): in a match, Enter opens a text line at the bottom right, above the
    /// player cards. Enter sends, Esc cancels. Each line is a small pill with an edge in its player's colour; lines fade one by one
    /// and the whole box is gone after a few quiet seconds. A new line from a teammate shows it again for a moment (the box never
    /// takes the keyboard by itself). While you type the keyboard is the chat's (Keybinds.ChatOpen): no moving, casting or pausing.
    public partial class ArenaApp
    {
        const float ChatW = 300f, ChatEntryH = 30f, ChatLineLife = 6f, ChatFade = 0.6f, ChatTypeIdle = 12f;
        const int ChatShown = 7;
        const string ChatField = "ChatField";

        bool chatOpen;
        string chatText = "";
        float chatK;            // 0 hidden .. 1 the entry line is showing (eases)
        float chatLastKey;      // when you last typed (the line closes itself after ChatTypeIdle)
        int chatOpenedFrame = -1;
        bool chatFocused;

        /// Each frame (Update): Enter opens the line when `allowed`; it closes by itself when you leave the screen or it sits idle.
        void TickChat(bool allowed)
        {
            if (chatOpen && !allowed || chatOpen && Time.unscaledTime - chatLastKey > ChatTypeIdle)
                CloseChat();
            else if (!chatOpen && allowed && GUIUtility.keyboardControl == 0 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                chatOpen = true;
                chatText = "";
                chatFocused = false;
                chatLastKey = Time.unscaledTime;
                chatOpenedFrame = Time.frameCount;
                Keybinds.ChatOpen = true;
            }
            chatK = Mathf.MoveTowards(chatK, chatOpen ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        }

        void CloseChat()
        {
            chatOpen = false;
            chatText = "";
            Keybinds.ChatOpen = false;
            if (GUI.GetNameOfFocusedControl() == ChatField)
                GUI.FocusControl(null);
        }

        /// UI: the lines and the entry line, right-aligned above your card and any teammates' rows.
        void DrawChat()
        {
            if (session.Screen != AppScreen.Game)
                return;
            var lines = session.Chat;
            double now = Session.Clock;
            bool riot = game.Me != null && Game.HasPerk(game.Me, PerkId.RiotGear);
            float bottom = HudStackTop(Mathf.Max(0, game.Players.Count - 1), riot) - (riot ? 0f : 4f);  // on the teammate rows and Riot Gear chip over your card (HudLayout.cs)
            float x = HudMyCard.xMax - ChatW;  // right-aligned with your card

            if (chatOpen || chatK > 0.01f)
            {
                float a = chatK;
                var entry = new Rect(x, bottom - ChatEntryH + (1f - a) * 6f, ChatW, ChatEntryH);
                float old = Ui.Alpha;
                Ui.Alpha = old * a;
                Ui.Panel(entry, true);
                Ui.Label(new Rect(entry.x + 12f, entry.y, 24f, entry.height), "ALL", 9, Ui.Good, TextAnchor.MiddleLeft, true);
                if (chatOpen)
                {
                    var f = new Rect(entry.x + 40f, entry.y + 4f, entry.width - 84f, entry.height - 8f);
                    var e = Event.current;
                    bool fresh = Time.frameCount == chatOpenedFrame;  // (the Enter that opened it is still in this frame's events)
                    if (!fresh && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                    {
                        session.SendChat(chatText);
                        e.Use();
                        CloseChat();
                    }
                    else if (!fresh && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                    {
                        e.Use();
                        CloseChat();
                    }
                    else
                    {
                        GUI.SetNextControlName(ChatField);
                        string next = Ui.BareField(f, chatText, Session.ChatMaxLength, 12);
                        if (next != chatText)
                        {
                            chatText = next;
                            chatLastKey = Time.unscaledTime;
                        }
                        if (!chatFocused)
                        {
                            GUI.FocusControl(ChatField);
                            chatFocused = true;
                        }
                        if (chatText.Length == 0)
                            Ui.Label(f, "Say something...", 12, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
                    }
                    Ui.Label(new Rect(entry.xMax - 40f, entry.y, 32f, entry.height), (Session.ChatMaxLength - chatText.Length).ToString(), 9, Ui.Muted, TextAnchor.MiddleRight);
                }
                Ui.Alpha = old;
                bottom = entry.y - 5f;
            }

            // newest at the bottom; while the line is open every recent line shows, else each one fades after ChatLineLife
            int shown = 0;
            for (int i = lines.Count - 1; i >= 0 && shown < ChatShown; i--)
            {
                float age = (float)(now - lines[i].At);
                float a = chatOpen ? 1f : Mathf.Clamp01((ChatLineLife - age) / ChatFade);
                if (a <= 0.01f)
                    break;  // (older ones are older still)
                DrawChatLine(lines[i], x, ref bottom, a);
                shown++;
            }
        }

        void DrawChatLine(Session.ChatLine l, float x, ref float bottom, float a)
        {
            Player p;
            Color edge = game.Players.TryGetValue(l.Id, out p) ? ToColor(p.Def.Color) : Color.white;
            const int size = 12;
            float nameW = Ui.TextWidth(l.Name, size, true) + 7f;
            float inner = ChatW - 22f;
            float textW = Ui.TextWidth(l.Text, size);
            bool two = nameW + textW > inner;  // a long line wraps under its name
            float h = two ? 38f : 22f;
            float w = two ? ChatW : Mathf.Min(ChatW, nameW + textW + 22f);
            var r = new Rect(x + ChatW - w, bottom - h, w, h);
            float old = Ui.Alpha;
            Ui.Alpha = old * a;
            Ui.HudPanel(r);
            Ui.Pill(new Rect(r.x + 4f, r.y + 4f, 3f, r.height - 8f), edge);
            Ui.Label(new Rect(r.x + 12f, r.y + 2f, nameW, 18f), l.Name, size, edge, TextAnchor.MiddleLeft, true);
            if (two)
                Ui.Label(new Rect(r.x + 12f, r.y + 19f, inner, 16f), l.Text, size, Color.white, TextAnchor.MiddleLeft);
            else
                Ui.Label(new Rect(r.x + 12f + nameW, r.y + 2f, textW + 4f, 18f), l.Text, size, Color.white, TextAnchor.MiddleLeft);
            Ui.Alpha = old;
            bottom = r.y - 4f;
        }
    }
}
