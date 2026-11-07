using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

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

        // --- Your own tracks (CustomMusic.cs) ---------------------------------------------------------------------------

        /// Clips you assigned in Unity: everything in Assets/Resources/Music becomes a track in the player, and a Music Pack
        /// asset (Resources/MusicPack, see MusicPack.cs) can also replace the menu, battle and boss music. Call before StartMusic.
        void LoadMusicPack()
        {
            var entries = new List<MusicLibrary.Entry>();
            var ids = new HashSet<string>();
            var used = new HashSet<AudioClip>();
            var pack = Resources.Load<MusicPack>("MusicPack");
            if (pack == null)  // saved under another name ("Music Pack", ...): take the first one in any Resources folder
            {
                var all = Resources.LoadAll<MusicPack>("");
                pack = all.Length > 0 ? all[0] : null;
                if (pack != null)
                    Debug.Log("Music Pack found as '" + pack.name + "' (name it MusicPack to be safe)");
            }
            if (pack != null)
            {
                foreach (var t in pack.Tracks)
                    if (t != null)
                        AddProjectClip(entries, ids, used, t.Title, t.Clip, t.Mood.ToString(), t.Volume);
                ReplaceTrack(MusicLibrary.Menu, pack.MainMenu);
                ReplaceTrack(MusicLibrary.Battle, pack.WaveBattle);
                foreach (var bs in pack.Bosses)
                    if (bs != null)
                        ReplaceTrack(SoundBank.BossTrackId(bs.Boss == EnemyType.TwinB ? EnemyType.TwinA : bs.Boss), bs.Music);
            }
            foreach (var clip in Resources.LoadAll<AudioClip>("Music"))
                AddProjectClip(entries, ids, used, null, clip, "Custom", 0.6f);
            MusicLibrary.SetProject(entries);
        }

        static void AddProjectClip(List<MusicLibrary.Entry> entries, HashSet<string> ids, HashSet<AudioClip> used, string title, AudioClip clip, string mood, float volume)
        {
            if (clip == null || !used.Add(clip))
                return;
            string id = MusicLibrary.ClipPrefix + clip.name;
            for (int n = 2; !ids.Add(id); n++)
                id = MusicLibrary.ClipPrefix + clip.name + "#" + n;
            entries.Add(new MusicLibrary.Entry
            {
                Id = id, Title = string.IsNullOrEmpty(title) || title.Trim().Length == 0 ? CustomMusic.TitleOf(clip.name) : title,
                Mood = mood, Gain = volume, Clip = clip, Blurb = "From your Unity project: " + clip.name,
            });
        }

        void ReplaceTrack(string id, MusicPack.Slot slot)
        {
            if (slot == null || slot.Clip == null)
                return;
            sounds.Overrides[id] = new KeyValuePair<AudioClip, float>(slot.Clip, slot.Volume);
            var e = MusicLibrary.Find(id);
            if (e != null)
                e.Blurb = "Your clip: " + slot.Clip.name;
        }

        static string MusicFolder => Path.Combine(Application.persistentDataPath, "music");
        int customCount;
        readonly Dictionary<string, UnityWebRequest> decodeJobs = new Dictionary<string, UnityWebRequest>();

        /// Look in the music folder and put what's there in the player's list. Cheap: nothing is decoded until a track is played.
        void ScanCustomMusic()
        {
            var files = CustomMusic.Scan(MusicFolder);
            customCount = files.Count;
            MusicLibrary.SetCustom(CustomMusic.Entries(files));
            CustomMusic.ClearFailed();
            if (juke.Selected != null && MusicLibrary.Find(juke.Selected) == null)
                juke.Selected = null;  // its file is gone
        }

        /// Ask for a track to be made ready. WAVs decode on a background thread (SoundBank); mp3 and ogg are decoded by
        /// Unity here first, then handed over (CustomMusic.Give).
        void RequestMusic(string id)
        {
            var e = id == null ? null : MusicLibrary.Find(id);
            if (e == null)
                return;
            if (!e.NeedsUnity || CustomMusic.HasDecoded(id) || sounds.IsTrackReady(id))
            {
                if (!CustomMusic.IsFailed(id))
                    sounds.RequestLibraryTrack(id);
                return;
            }
            if (decodeJobs.ContainsKey(id) || CustomMusic.IsFailed(id))
                return;
            var type = Path.GetExtension(e.Path).ToLowerInvariant() == ".mp3" ? AudioType.MPEG : AudioType.OGGVORBIS;
            var req = UnityWebRequestMultimedia.GetAudioClip(new System.Uri(e.Path).AbsoluteUri, type);
            req.SendWebRequest();
            decodeJobs[id] = req;
        }

        void TickDecodes()
        {
            if (decodeJobs.Count == 0)
                return;
            string done = null;
            foreach (var kv in decodeJobs)
                if (kv.Value.isDone)
                {
                    done = kv.Key;
                    break;
                }
            if (done == null)
                return;
            var req = decodeJobs[done];
            decodeJobs.Remove(done);
            AudioClip clip = null;
            try { clip = req.result == UnityWebRequest.Result.Success ? DownloadHandlerAudioClip.GetContent(req) : null; }
            catch (System.Exception) { }
            if (clip != null && clip.samples > 0)
            {
                var data = new float[clip.samples * clip.channels];
                if (clip.GetData(data, 0))
                    CustomMusic.Give(done, CustomMusic.Finish(data, clip.channels, clip.frequency));
                else
                    CustomMusic.MarkFailed(done);
            }
            else
                CustomMusic.MarkFailed(done);
            if (clip != null)
                Destroy(clip);
            req.Dispose();
        }

        static Color MoodColor(string mood)
        {
            switch (mood)
            {
                case "Chill": return new Color(0.5f, 0.8f, 1f);
                case "Action": return new Color(1f, 0.72f, 0.3f);
                case "Dark": return new Color(0.72f, 0.52f, 1f);
                case "Epic": return new Color(1f, 0.5f, 0.45f);
                case "Custom": return new Color(0.5f, 0.95f, 0.6f);
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
            if (juke.Selected != null && CustomMusic.IsFailed(juke.Selected) && !juke.RepeatOne)
            {
                PlayTrack(juke.PickNext());  // a file that can't be read: on to the next one
                return;
            }
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
            bool failed = juke.Selected != null && CustomMusic.IsFailed(juke.Selected);
            bool loading = juke.Selected != null && !failed && !sounds.IsTrackReady(juke.Selected);
            string state = failed ? "CAN'T PLAY THIS FILE" : sounds.MusicPaused ? "PAUSED" : loading ? (entry != null && entry.NeedsUnity ? "LOADING THE FILE…" : "RENDERING THE TRACK…") : sounds.Muted ? "MUTED" : "PLAYING";
            Ui.Label(new Rect(card.xMax - 230f, card.y + 10f, 212f, 20f), state, 11, failed || sounds.MusicPaused || loading || sounds.Muted ? Ui.Warn : Ui.Good, TextAnchor.MiddleRight, true);
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

            bool boss = SwitchRow(new Rect(x, y, w - 250f, 40f), juke.BossThemes, "Boss themes still play", "A boss fight plays its own theme, then yours returns");
            if (Ui.SmallButton(new Rect(x + w - 240f, y + 5f, 132f, 30f), "Open folder", Ui.Neutral, 13))
            {
                Directory.CreateDirectory(MusicFolder);
                Application.OpenURL("file:///" + MusicFolder.Replace('\\', '/'));
            }
            if (Ui.SmallButton(new Rect(x + w - 100f, y + 5f, 100f, 30f), "Rescan", Ui.Neutral, 13))
                ScanCustomMusic();
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
            int n = MusicLibrary.All.Count + 2;   // "Automatic" first, a tip for your own tracks last
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
                var r = new Rect(6f, ry, list.width - 18f, row - 4f);
                if (i == n - 1)
                {
                    Ui.Label(new Rect(r.x + 14f, r.y, r.width - 28f, r.height), "Your own tracks: click Open folder, drop .wav, .mp3 or .ogg files in, then Rescan.  "
                        + (customCount > 0 ? customCount + " found." : "None yet."), 12, Ui.Muted, TextAnchor.MiddleLeft, false, true);
                    continue;
                }
                var e = i == 0 ? null : MusicLibrary.All[i - 1];
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
                bool bad = e != null && CustomMusic.IsFailed(e.Id);
                Ui.Label(new Rect(r.x + 260f, r.y, r.width - 360f, r.height), e == null ? "Let the game choose the music" : bad ? "Couldn't read this file" : e.Blurb, 12, bad ? Ui.Warn : Ui.Muted);
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
