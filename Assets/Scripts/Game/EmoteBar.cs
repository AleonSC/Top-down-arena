using UnityEngine;

namespace Arena
{
    /// The emote bar (Docs/Mockups/emote-bar.html): hold the Emote key (T) and a see-through bar slides up above the ability bar
    /// (in a match) or the foot of the Hangout room, with every emote your rank has side by side and its two ends fading away.
    /// The mouse wheel or the arrows at its sides move along it; click one in view (or press 1-9) to play it. While it's open
    /// a click picks instead of firing. Let go of the key and it closes. A short pause follows each (Session.EmotePause).
    public partial class ArenaApp
    {
        bool emoteBarOpen;
        float emoteBarK;     // 0 closed .. 1 open (eases)
        int emoteFirst;      // the first emote in view
        /// How many emotes the bar shows at once, and so how many number-key slots it has.
        static int EmoteView => Mathf.Max(1, Mathf.FloorToInt((EmoteBarW - 2f * (EmoteArrowW + 2f) - 2f * EmotePad + 8f) / EmoteStep));
        const float EmoteBarW = 520f, EmoteBarH = 78f, EmoteTile = 56f, EmoteTileH = 64f, EmoteStep = 64f, EmotePad = 34f, EmoteArrowW = 32f;

        /// Every emote number your rank has, in unlock order.
        static System.Collections.Generic.List<int> EmotesOwned()
        {
            var l = new System.Collections.Generic.List<int>();
            int rank = Profile.Current.RankNow;
            for (int i = 1; i <= Rank.EmoteCount; i++)
                if (Rank.Owns(rank, Rank.EmoteId(i)))
                    l.Add(i);
            return l;
        }

        /// Each frame: is the bar open? `allowed` is whether this screen can have it (a match with you up, or the Hangout).
        int emoteTickFrame = -1;

        void TickEmoteBar(bool allowed)
        {
            if (emoteTickFrame == Time.frameCount)
                return;  // (the Hangout calls this from OnGUI, once per frame is enough)
            emoteTickFrame = Time.frameCount;
            emoteBarOpen = allowed && Keybinds.Held(KeyAction.Emote);
            emoteBarK = Mathf.MoveTowards(emoteBarK, emoteBarOpen ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            Keybinds.SetEmoteNumbers(0);
            if (!emoteBarOpen)
                return;
            // The number keys are the slots of the bar (1 is the leftmost one in view): they always mean the same place, so which
            // emote a key plays depends on how far along the bar you've scrolled.
            var own = EmotesOwned();
            emoteFirst = Mathf.Clamp(emoteFirst, 0, Mathf.Max(0, own.Count - EmoteView));
            Keybinds.SetEmoteNumbers(Mathf.Min(EmoteView, own.Count - emoteFirst));  // its slot keys win over a bound number key
            for (int k = 0; k < EmoteView; k++)
                if (emoteFirst + k < own.Count && Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + k)))
                    session.SendEmote(own[emoteFirst + k]);
        }

        /// UI: the bar, with its centre-bottom at (cx, bottom).
        void DrawEmoteBar(float cx, float bottom)
        {
            if (emoteBarK <= 0.01f)
                return;
            var own = EmotesOwned();
            if (own.Count == 0)
                return;
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = oldAlpha * emoteBarK;
            var bar = new Rect(cx - EmoteBarW / 2f, bottom - EmoteBarH + (1f - emoteBarK) * 8f, EmoteBarW, EmoteBarH);
            var win = new Rect(bar.x + EmoteArrowW + 2f, bar.y, bar.width - 2f * (EmoteArrowW + 2f), bar.height);
            int view = EmoteView;
            int maxFirst = Mathf.Max(0, own.Count - view);
            emoteFirst = Mathf.Clamp(emoteFirst, 0, maxFirst);
            var ev = Event.current;
            if (emoteBarOpen && ev.type == EventType.ScrollWheel)
            {
                emoteFirst = Mathf.Clamp(emoteFirst + (ev.delta.y > 0f ? 1 : -1), 0, maxFirst);
                ev.Use();
            }
            float wait = session.EmoteWait;
            bool cooling = wait > 0f;

            // The arrows at the sides.
            var left = new Rect(bar.x, bar.y + 14f, EmoteArrowW, 50f);
            var right = new Rect(bar.xMax - EmoteArrowW, bar.y + 14f, EmoteArrowW, 50f);
            if (EmoteArrow(left, "‹", emoteFirst > 0))
                emoteFirst = Mathf.Max(0, emoteFirst - 3);
            if (EmoteArrow(right, "›", emoteFirst < maxFirst))
                emoteFirst = Mathf.Min(maxFirst, emoteFirst + 3);

            // The tiles, each faded by how near it is to an end of the window.
            float fadeW = win.width * 0.22f;
            string hoverName = null;
            Vector2 mp = ev.mousePosition;
            for (int i = 0; i < own.Count; i++)
            {
                var tile = new Rect(win.x + EmotePad + (i - emoteFirst) * EmoteStep, bar.y + 7f, EmoteTile, EmoteTileH);
                if (tile.xMax < win.x || tile.x > win.xMax)
                    continue;
                float edge = Mathf.Clamp01(Mathf.Min(tile.center.x - win.x, win.xMax - tile.center.x) / fadeW);
                if (edge <= 0.02f)
                    continue;
                float a = edge * edge * (3f - 2f * edge) * (cooling ? 0.45f : 1f);
                bool hot = !cooling && edge > 0.5f && tile.Contains(mp);
                float old2 = Ui.Alpha;
                Ui.Alpha = old2 * a;
                Ui.Fill(tile, hot ? new Color(0.47f, 0.63f, 1f, 0.28f) : new Color(0.03f, 0.05f, 0.08f, 0.32f));
                Ui.Fill(new Rect(tile.x, tile.y, tile.width, 1f), new Color(1f, 1f, 1f, hot ? 0.5f : 0.12f));
                Ui.Fill(new Rect(tile.x, tile.yMax - 1f, tile.width, 1f), new Color(1f, 1f, 1f, hot ? 0.5f : 0.12f));
                Ui.Fill(new Rect(tile.x, tile.y, 1f, tile.height), new Color(1f, 1f, 1f, hot ? 0.5f : 0.12f));
                Ui.Fill(new Rect(tile.xMax - 1f, tile.y, 1f, tile.height), new Color(1f, 1f, 1f, hot ? 0.5f : 0.12f));
                GUI.BeginGroup(tile);
                DrawEmote(own[i], new Vector2(tile.width / 2f, 46f), EmoteIconTime(own[i]), 0.62f);
                GUI.EndGroup();
                int slot = i - emoteFirst;  // 0.. : the number key that plays it (only the ones in view have one)
                if (slot >= 0 && slot < view)
                    Ui.Label(new Rect(tile.x, tile.yMax - 14f, tile.width, 12f), (slot + 1).ToString(), 9, new Color(0.9f, 0.93f, 0.96f, 0.85f), TextAnchor.MiddleCenter, true);
                if (hot)
                    hoverName = Rank.Cosmetic(Rank.EmoteId(own[i])).Name.Replace(" emote", "");
                Ui.NoteHover(tile);
                Ui.Alpha = old2;
                if (hot && ev.type == EventType.MouseDown && ev.button == 0)
                {
                    session.SendEmote(own[i]);
                    ev.Use();
                }
            }

            // The pause after one: a thin bar draining under the strip.
            if (cooling)
            {
                var track = new Rect(win.x + 6f, bar.yMax + 1f, win.width - 12f, 3f);
                Ui.Fill(track, new Color(1f, 1f, 1f, 0.1f));
                Ui.Fill(new Rect(track.x, track.y, track.width * Mathf.Clamp01(wait / Mathf.Max(0.01f, session.EmotePause)), track.height), new Color(0.5f, 0.7f, 1f, 0.9f));
            }
            Ui.Label(new Rect(bar.x, bar.y - 18f, bar.width, 14f), hoverName != null ? hoverName.ToUpperInvariant() : "SCROLL OR USE THE ARROWS  ·  CLICK OR PRESS 1-" + view, 9, new Color(0.9f, 0.93f, 0.96f, 0.7f), TextAnchor.MiddleCenter, true);
            Ui.Alpha = oldAlpha;
        }

        bool EmoteArrow(Rect r, string text, bool enabled)
        {
            bool hot = enabled && r.Contains(Event.current.mousePosition);
            Ui.Fill(r, hot ? new Color(0.47f, 0.63f, 1f, 0.28f) : new Color(0.03f, 0.05f, 0.08f, enabled ? 0.32f : 0.15f));
            Ui.Label(r, text, 22, new Color(0.9f, 0.93f, 0.96f, enabled ? 0.9f : 0.25f), TextAnchor.MiddleCenter, true);
            Ui.NoteHover(r);
            if (hot && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        /// Which moment of an emote makes the best little picture for its tile.
        static float EmoteIconTime(int index)
        {
            switch (Rank.EmoteId(index))
            {
                case "emote-spin": return 0.35f;
                case "emote-fireworks": return 0.75f;
                case "emote-sleep": return 0.8f;
                case "emote-cheer": return 0.7f;
                default: return 0.85f;
            }
        }

        // --- Bubbles over players ---------------------------------------------------------------------------------

        /// In a match: each player's emote over their head (the HUD pass; Hud.cs).
        void DrawMatchEmotes()
        {
            foreach (var p in game.Players.Values)
            {
                int index;
                float age;
                if (p.Downed || LaunchHides(p) || !session.EmoteOf(p.Id, out index, out age))
                    continue;
                V2 at = ToUi(p.Disp);
                DrawEmote(index, new Vector2(at.X, at.Y - p.Def.BodyR * UiWorldScale - 60f), age, 0.8f);
            }
        }
    }
}
