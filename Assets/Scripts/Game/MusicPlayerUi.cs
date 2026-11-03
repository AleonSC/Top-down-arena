using UnityEngine;

namespace Arena
{
    /// The music player in Settings > Audio: pick any track (menu theme, wave battle, boss themes and the extra
    /// moods in MusicLibrary.cs) to play everywhere instead of the automatic music, with play / pause, previous /
    /// next, shuffle, repeat one / all and a bar for how far into the track you are. The choice is saved.
    public partial class ArenaApp
    {
        readonly Jukebox juke = new Jukebox();
        int audioTab;              // Settings > Audio: 0 volumes, 1 music player
        float jukeScroll;          // the track list's scroll offset
        float jukeLastTime;        // where the track was last frame (a jump back = it looped round)

        static Color MoodColor(string mood)
        {
            switch (mood)
            {
                case "Chill": return new Color(0.5f, 0.8f, 1f);
                case "Action": return new Color(1f, 0.72f, 0.3f);
                case "Dark": return new Color(0.72f, 0.52f, 1f);
                case "Epic": return new Color(1f, 0.5f, 0.45f);
                default: return new Color(1f, 0.42f, 0.62f);  // Boss
            }
        }

        void LoadJukebox()
        {
            juke.Shuffle = PlayerPrefs.GetInt("juke_shuffle", 0) == 1;
            juke.RepeatOne = PlayerPrefs.GetInt("juke_repeat1", 0) == 1;
            juke.BossThemes = PlayerPrefs.GetInt("juke_boss", 1) == 1;
            string id = PlayerPrefs.GetString("juke_track", "");
            juke.Select(id.Length == 0 ? null : id, false);
        }

        void SaveJukebox()
        {
            PlayerPrefs.SetInt("juke_shuffle", juke.Shuffle ? 1 : 0);
            PlayerPrefs.SetInt("juke_repeat1", juke.RepeatOne ? 1 : 0);
            PlayerPrefs.SetInt("juke_boss", juke.BossThemes ? 1 : 0);
            PlayerPrefs.SetString("juke_track", juke.Selected ?? "");
            PlayerPrefs.Save();
        }

        void PlayTrack(string id)
        {
            if (id == null)
                juke.Selected = null;
            else
            {
                juke.Select(id);
                sounds.RestartTrack(id);
            }
            sounds.MusicPaused = false;
            jukeLastTime = 0f;
            SaveJukebox();
        }

        /// Repeat all: when the chosen track comes round to its start again, go on to the next.
        void AdvanceJukebox()
        {
            if (juke.Selected == null || sounds.MusicPaused || sounds.PlayingId != juke.Selected)
            {
                jukeLastTime = 0f;
                return;
            }
            float t = sounds.PlayingTime;
            if (!juke.RepeatOne && jukeLastTime > 2f && t < jukeLastTime - 1f)
                PlayTrack(juke.PickNext());
            else
                jukeLastTime = t;
        }

        static string Clock(float s)
        {
            int m = (int)(s / 60f), r = (int)(s % 60f);
            return m + ":" + (r < 10 ? "0" : "") + r;
        }

        void DrawMusicPlayer(float t)
        {
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, y = DrawerPanel.y + 104f;
            var entry = juke.Selected == null ? null : MusicLibrary.Find(juke.Selected);
            string shown = sounds.PlayingId;
            var playing = shown == null ? null : MusicLibrary.Find(shown);
            Color accent = entry != null ? MoodColor(entry.Mood) : Ui.Accent;

            // Now playing.
            var card = new Rect(x, y, w, 134f);
            Ui.Pill(card, new Color(1f, 1f, 1f, 0.04f));
            string title = entry != null ? entry.Title : "Automatic";
            string line = entry != null ? entry.Mood + "  ·  " + entry.Blurb
                : "The game picks: menu theme, wave battle, and each boss's own theme" + (playing != null ? "  ·  now: " + playing.Title : "");
            Ui.Label(new Rect(card.x + 18f, card.y + 8f, w - 160f, 32f), title, 22, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(card.x + 18f, card.y + 38f, w - 36f, 18f), line, 13, Ui.Muted);
            bool loading = juke.Selected != null && !sounds.IsTrackReady(juke.Selected);
            string state = sounds.MusicPaused ? "PAUSED" : loading ? "RENDERING THE TRACK…" : sounds.Muted ? "MUTED" : "PLAYING";
            Ui.Label(new Rect(card.xMax - 230f, card.y + 10f, 212f, 20f), state, 11, sounds.MusicPaused || loading || sounds.Muted ? Ui.Warn : Ui.Good, TextAnchor.MiddleRight, true);
            float len = sounds.PlayingLength, pos = sounds.PlayingTime;
            Ui.Bar(new Rect(card.x + 18f, card.y + 66f, w - 36f, 8f), len > 0f ? pos / len : 0f, accent);
            Ui.Label(new Rect(card.x + 18f, card.y + 76f, 80f, 16f), Clock(pos), 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(card.xMax - 98f, card.y + 76f, 80f, 16f), Clock(len), 11, Ui.Muted, TextAnchor.MiddleRight, true);

            float bx = card.x + 18f, by = card.y + 96f;
            if (Ui.SmallButton(new Rect(bx, by, 70f, 30f), "Prev", Ui.Neutral, 13))
            {
                juke.Previous();
                PlayTrack(juke.Selected);
            }
            bx += 78f;
            if (Ui.SmallButton(new Rect(bx, by, 96f, 30f), sounds.MusicPaused ? "Play" : "Pause", Ui.Primary, 13))
                sounds.MusicPaused = !sounds.MusicPaused;
            bx += 104f;
            if (Ui.SmallButton(new Rect(bx, by, 70f, 30f), "Next", Ui.Neutral, 13))
            {
                juke.Select(juke.PickNext());
                PlayTrack(juke.Selected);
            }
            bx += 100f;
            if (Ui.SmallButton(new Rect(bx, by, 110f, 30f), juke.Shuffle ? "Shuffle: on" : "Shuffle: off", juke.Shuffle ? Ui.Primary : Ui.Neutral, 13))
            {
                juke.Shuffle = !juke.Shuffle;
                SaveJukebox();
            }
            bx += 118f;
            if (Ui.SmallButton(new Rect(bx, by, 130f, 30f), juke.RepeatOne ? "Repeat: this one" : "Repeat: all", Ui.Neutral, 13))
            {
                juke.RepeatOne = !juke.RepeatOne;
                SaveJukebox();
            }
            y += 144f;

            bool boss = SwitchRow(new Rect(x, y, w, 40f), juke.BossThemes, "Boss themes still play", "In a boss fight its own theme takes over, then your track comes back");
            if (boss != juke.BossThemes)
            {
                juke.BossThemes = boss;
                SaveJukebox();
            }
            y += 50f;

            // The track list (scrolls with the mouse wheel).
            var list = new Rect(x, y, w, DrawerPanel.yMax - 22f - y);
            Ui.Pill(list, new Color(0f, 0f, 0f, 0.3f));
            const float row = 40f;
            int n = MusicLibrary.All.Count + 1;   // "Automatic" first
            float maxScroll = Mathf.Max(0f, n * row - list.height + 8f);
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && list.Contains(ev.mousePosition))
            {
                jukeScroll = Mathf.Clamp(jukeScroll + ev.delta.y * 24f, 0f, maxScroll);
                ev.Use();
            }
            jukeScroll = Mathf.Clamp(jukeScroll, 0f, maxScroll);
            GUI.BeginGroup(list);
            for (int i = 0; i < n; i++)
            {
                float ry = 4f + i * row - jukeScroll;
                if (ry < -row || ry > list.height)
                    continue;
                var e = i == 0 ? null : MusicLibrary.All[i - 1];
                var r = new Rect(6f, ry, list.width - 18f, row - 4f);
                bool chosen = e == null ? juke.Selected == null : juke.Selected == e.Id;
                bool hover = r.Contains(Event.current.mousePosition);
                Color c = e == null ? Ui.Accent : MoodColor(e.Mood);
                Ui.Pill(r, chosen ? new Color(c.r, c.g, c.b, 0.3f) : new Color(1f, 1f, 1f, hover ? 0.1f : 0.04f));
                if (chosen)
                    for (int b = 0; b < 3; b++)  // a little equaliser on the chosen track
                    {
                        float hgt = sounds.MusicPaused ? 4f : 5f + 11f * Mathf.Abs(Mathf.Sin(t * (3.1f + b * 1.7f) + b));
                        Ui.Fill(new Rect(r.x + 14f + b * 6f, r.center.y + 8f - hgt, 4f, hgt), c);
                    }
                Ui.Label(new Rect(r.x + 44f, r.y, 210f, r.height), e == null ? "Automatic" : e.Title, 15, chosen ? Color.white : Ui.Text, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 260f, r.y, r.width - 360f, r.height), e == null ? "Let the game choose the music" : e.Blurb, 12, Ui.Muted);
                if (e != null)
                    Ui.Badge(new Rect(r.xMax - 86f, r.y + 8f, 76f, r.height - 16f), e.Mood, c);
                if (Ui.HitArea(r) && !chosen)
                    PlayTrack(e == null ? null : e.Id);
            }
            GUI.EndGroup();
            if (maxScroll > 0f)
            {
                float th = list.height * list.height / (n * row + 8f), ty = list.y + (list.height - th) * (jukeScroll / maxScroll);
                Ui.Fill(new Rect(list.xMax - 5f, ty, 3f, th), new Color(1f, 1f, 1f, 0.3f));
            }
        }
    }
}
