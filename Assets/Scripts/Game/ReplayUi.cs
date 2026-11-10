using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// Run replays (Core/Replay.cs): the F1 menu's Replays tab (record on/off, the folder, the list with Open / Rename / Delete) and
    /// the timeline over the game window while one plays: wave lanes, a scrub bar, pause, frame steps and speeds from 0.1x to 10x
    /// either way. While a replay plays, `session` and `game` are the replay's own private copies, so all the usual drawing just works.
    public partial class ArenaApp
    {
        ReplayPlayer replay;                 // non-null while watching a replay
        Session realSession;                 // the app's own session and game, put back when the replay closes
        Game realGame;
        bool recordReplays;                  // dev: save a replay of each match you host (saved setting)
        List<ReplayFile> replayList;
        float replayListAt = -99f;
        float replayScroll;
        string replayRenamePath = "", replayRenameText = "", replayDeletePath = "", replayNote = "";
        bool replayScrub;                    // dragging the scrub bar

        static string ReplayFolder => Path.Combine(Application.persistentDataPath, "replays");

        /// Recording happens on the host, so this only matters when you host. (Not while a replay has swapped the game.)
        void ApplyReplayRecording()
        {
            if (replay == null)
                game.ReplayFolder = recordReplays ? ReplayFolder : null;
        }

        /// Replays open from the main menu: nothing else is running that would need its session.
        bool CanOpenReplay => replay == null && session.Screen == AppScreen.Menu && session.Net == null && !whoOpen && !profTr;

        void RefreshReplays()
        {
            replayList = ReplayFile.List(ReplayFolder);
            replayListAt = Time.unscaledTime;
        }

        void OpenReplay(ReplayFile head)
        {
            var file = ReplayFile.Load(head.Path);
            if (file == null || file.Frames == 0)
            {
                replayNote = "Couldn't read that replay.";
                return;
            }
            var rp = new ReplayPlayer();
            rp.Load(file);
            realSession = session;
            realGame = game;
            session = rp.Session;
            game = rp.Game;
            replay = rp;
            devMenuOpen = false;
            pauseMenuOpen = false;
            devFrozen = false;
            endT = -1f;
            replayScrub = false;
            replayNote = "";
        }

        void CloseReplay()
        {
            if (replay == null)
                return;
            replay.Close();
            replay = null;
            session = realSession;
            game = realGame;
            realSession = null;
            realGame = null;
            devMenuOpen = false;
            replayScrub = false;
            endT = -1f;
            Time.timeScale = 1f;
        }

        // --- Each frame ---------------------------------------------------------------------------------------------------

        void ReplayFixedStep(float dt) => replay.Step(dt);

        /// Update while a replay plays (instead of the usual one: nobody is at the controls).
        void ReplayUpdate()
        {
            Keybinds.Tick();
            bool moving = !replay.Paused && replay.Rate != 0f;
            game.Update(moving ? Mathf.Min(0.1f, Time.deltaTime * Mathf.Abs(replay.Rate)) : 0f);
            bool hear = moving && replay.Rate > 0f && replay.Rate <= 2f;
            foreach (var sfx in game.Sounds)
                if (hear)
                    sounds.Play(sfx.Sfx, sfx.Volume, Time.unscaledTime);
            game.Sounds.Clear();
            game.MasteryToasts.Clear();
            UpdateMusic();
            UpdateLayout();
            UpdateCameraLean(Time.unscaledDeltaTime);
            ReplayKeys();
        }

        static readonly float[] ReplaySteps = { -10f, -4f, -2f, -1f, -0.5f, -0.25f, -0.1f, 0.1f, 0.25f, 0.5f, 1f, 2f, 4f, 10f };

        void ReplaySpeedNudge(int dir)
        {
            int at = 10;  // 1x
            float best = 99f;
            for (int i = 0; i < ReplaySteps.Length; i++)
                if (Mathf.Abs(ReplaySteps[i] - replay.Rate) < best)
                {
                    best = Mathf.Abs(ReplaySteps[i] - replay.Rate);
                    at = i;
                }
            replay.Play(ReplaySteps[Mathf.Clamp(at + dir, 0, ReplaySteps.Length - 1)]);
        }

        void ReplayKeys()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                devMenuOpen = !devMenuOpen;
                GUIUtility.keyboardControl = 0;
            }
            if (devMenuOpen)
            {
                if (EscPressed && GUIUtility.keyboardControl == 0)
                    devMenuOpen = false;
                return;
            }
            if (GUIUtility.keyboardControl != 0)
                return;
            if (EscPressed)
            {
                CloseReplay();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space)) replay.TogglePause();
            else if (Input.GetKeyDown(KeyCode.Comma)) replay.StepFrames(-1);
            else if (Input.GetKeyDown(KeyCode.Period)) replay.StepFrames(1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) replay.SeekTime(replay.Time - 5f);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) replay.SeekTime(replay.Time + 5f);
            else if (Input.GetKeyDown(KeyCode.Home)) replay.Seek(0);
            else if (Input.GetKeyDown(KeyCode.End)) replay.Seek(replay.Frames);
            else if (Input.GetKeyDown(KeyCode.LeftBracket)) ReplaySpeedNudge(-1);
            else if (Input.GetKeyDown(KeyCode.RightBracket)) ReplaySpeedNudge(1);
            else if (Input.GetKeyDown(KeyCode.V)) replay.CycleView();
        }

        // --- The timeline ---------------------------------------------------------------------------------------------------

        static string ReplayClock(float s)
        {
            int m = (int)(s / 60f);
            return m + ":" + (s - m * 60f).ToString("00.0");
        }

        static readonly Color ReplayWaveCol = new Color(0.35f, 0.55f, 1f), ReplayBossCol = new Color(1f, 0.35f, 0.35f), ReplayDownCol = new Color(1f, 0.72f, 0.3f);

        void DrawReplayBar()
        {
            var f = replay.File;
            int frames = Math.Max(1, replay.Frames);
            var bar = new Rect(16f, H - 158f, W - 32f, 146f);
            DkSub(bar, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.94f));
            float lx = bar.x + 78f, lw = bar.width - 78f - 18f;
            var ev = Event.current;

            // Badge.
            var name = f.Name;
            DkSub(new Rect(18f, 14f, 270f, 28f), new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.9f));
            Ui.Label(new Rect(30f, 14f, 160f, 28f), "REPLAY" + (replay.Paused ? "  PAUSED" : ""), 13, replay.Paused ? Ui.Gold : new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(110f, 14f, 170f, 28f), name, 11, CrtStyle.Desc, TextAnchor.MiddleRight);

            // Wave lane: one block per wave (red for a boss); click one to jump to where it starts.
            Ui.Label(new Rect(bar.x + 14f, bar.y + 10f, 62f, 20f), "WAVES", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            var lane = new Rect(lx, bar.y + 10f, lw, 20f);
            Ui.Fill(lane, new Color(1f, 1f, 1f, 0.05f));
            var starts = new List<ReplayMark>();
            foreach (var m in f.Marks)
                if (m.Kind != ReplayMark.Down)
                    starts.Add(m);
            for (int i = 0; i < starts.Count; i++)
            {
                int from = starts[i].Frame, to = i + 1 < starts.Count ? starts[i + 1].Frame : frames;
                var seg = new Rect(lane.x + lane.width * from / frames, lane.y, Mathf.Max(2f, lane.width * (to - from) / frames - 1f), lane.height);
                bool boss = starts[i].Kind == ReplayMark.Boss, hover = seg.Contains(ev.mousePosition);
                var col = boss ? ReplayBossCol : ReplayWaveCol;
                Ui.Fill(seg, new Color(col.r, col.g, col.b, hover ? 0.55f : boss ? 0.38f : (i % 2 == 0 ? 0.28f : 0.2f)));
                if (seg.width > 34f)
                    Ui.Label(seg, (boss ? "BOSS " : "W") + starts[i].Value, 10, boss ? new Color(1f, 0.7f, 0.7f) : CrtStyle.Body, TextAnchor.MiddleCenter, true);
                if (Ui.HitArea(seg))
                    replay.Seek(from);
            }

            // Time bar: drag anywhere on it. Blue ticks are waves, red a boss, amber a player going down.
            Ui.Label(new Rect(bar.x + 14f, bar.y + 38f, 62f, 24f), "TIME", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            var track = new Rect(lx, bar.y + 38f, lw, 24f);
            Ui.Fill(new Rect(track.x, track.y + 9f, track.width, 6f), new Color(0.18f, 0.2f, 0.28f));
            float head = track.x + track.width * replay.Frame / frames;
            Ui.Fill(new Rect(track.x, track.y + 9f, head - track.x, 6f), Ui.Accent);
            foreach (var m in f.Marks)
            {
                float mx = track.x + track.width * m.Frame / frames;
                if (m.Kind == ReplayMark.Down)
                    Ui.Fill(new Rect(mx - 1f, track.y + 7f, 2f, 10f), ReplayDownCol);
                else
                    Ui.Fill(new Rect(mx - 1f, track.y + (m.Kind == ReplayMark.Boss ? 2f : 5f), 2f, m.Kind == ReplayMark.Boss ? 20f : 14f), m.Kind == ReplayMark.Boss ? ReplayBossCol : ReplayWaveCol);
            }
            Ui.Fill(new Rect(head - 2f, track.y + 1f, 4f, 22f), Color.white);
            if (ev.type == EventType.MouseDown && ev.button == 0 && track.Contains(ev.mousePosition))
                replayScrub = true;
            if (ev.type == EventType.MouseUp)
                replayScrub = false;
            if (replayScrub && (ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag))
            {
                int to = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01((ev.mousePosition.x - track.x) / track.width) * frames), 0, frames);
                if (to != replay.Frame)
                    replay.Seek(to);
                ev.Use();
            }

            // Buttons: transport on the left, the speed strip on the right.
            float by = bar.y + 72f, x = bar.x + 14f;
            Func<float, string, Color, bool> btn = (w, text, tint) =>
            {
                bool hit = DkKey(new Rect(x, by, w, 30f), text, tint);
                x += w + 6f;
                return hit;
            };
            if (btn(84f, "|< Wave", Ui.Neutral)) replay.Seek(replay.PrevWaveFrame());
            if (btn(52f, "-1 f", Ui.Neutral)) replay.StepFrames(-1);
            if (btn(92f, replay.Paused ? "Play" : "Pause", replay.Paused ? Ui.Primary : Ui.Neutral)) replay.TogglePause();
            if (btn(52f, "+1 f", Ui.Neutral)) replay.StepFrames(1);
            if (btn(84f, "Wave >|", Ui.Neutral)) replay.Seek(replay.NextWaveFrame());
            x += 18f;
            float[] speeds = { -10f, -4f, -1f, -0.5f, -0.1f, 0f, 0.1f, 0.5f, 1f, 2f, 4f, 10f };
            foreach (float s in speeds)
            {
                if (s == 0f)
                {
                    x += 10f;  // a gap between backward and forward
                    continue;
                }
                bool on = !replay.Paused && Mathf.Abs(replay.Rate - s) < 0.001f;
                string label = (s < 0f ? "<< " : "") + Mathf.Abs(s).ToString("0.##") + "x";
                if (btn(s == 0.1f || s == -0.1f || s == -0.5f || s == 0.5f ? 66f : 58f, label, on ? Ui.Primary : Ui.Neutral))
                    replay.Play(s);
            }

            // Bottom line: where, who, keys, close.
            float ly = bar.y + 112f;
            int wave = Math.Max(0, game.Wave);
            Ui.Label(new Rect(bar.x + 14f, ly, 330f, 22f), ReplayClock(replay.Time) + " / " + ReplayClock(replay.Length) + "   ·   " + (wave > 0 ? "wave " + wave : "start") + "   ·   frame " + replay.Frame, 12, CrtStyle.Body, TextAnchor.MiddleLeft, true);
            string seeing = game.Me != null ? game.Me.Name : "-";
            if (game.Players.Count > 1 && DkKey(new Rect(bar.x + 350f, ly - 2f, 160f, 26f), "Following: " + seeing, Ui.Neutral))
                replay.CycleView();
            Ui.Label(new Rect(bar.x + 520f, ly, bar.width - 520f - 140f, 22f), "Space pause   , .  step   [ ]  speed   arrows  ±5 s   V  next player   Esc  close", 11, CrtStyle.Desc, TextAnchor.MiddleLeft);
            if (DkKey(new Rect(bar.xMax - 128f, ly - 2f, 112f, 26f), "Close replay", Ui.Danger))
                CloseReplay();
        }

        // --- The F1 menu's Replays tab --------------------------------------------------------------------------------------------

        void DrawDevReplays(Rect body)
        {
            if (replayList == null || Time.unscaledTime - replayListAt > 2f)
                RefreshReplays();

            // Recording.
            var card = new Rect(body.x, body.y, body.width, 112f);
            float y = DevCard(card, "Recording", "Saves each run you host so you can watch it back: pause, rewind, fast forward, jump to any wave.", new Color(1f, 0.45f, 0.45f));
            bool on = DkSwitch(new Rect(card.x + 16f, y, 330f, 26f), recordReplays, "Record replays of runs I host");
            if (on != recordReplays)
            {
                recordReplays = on;
                PlayerPrefs.SetInt("record_replays", on ? 1 : 0);
                PlayerPrefs.Save();
                ApplyReplayRecording();
            }
            var rec = replay == null ? game.Replay : null;
            string status = rec != null ? "Recording now: " + ReplayClock(rec.Seconds) + (rec.Full ? " (size limit reached, the rest isn't kept)" : "")
                : recordReplays ? "On: the next run you host is saved when it ends." : "Off.";
            Ui.Label(new Rect(card.x + 360f, y, card.width - 360f - 150f, 26f), status, 12, rec != null ? new Color(1f, 0.55f, 0.55f) : CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            if (rec != null && DkKey(new Rect(card.xMax - 146f, y - 2f, 130f, 28f), "Stop and save", Ui.Danger))
            {
                game.Replay.Finish();
                game.Replay = null;
                RefreshReplays();
            }

            // Folder.
            float fy = card.yMax + 10f;
            if (DkKey(new Rect(body.x, fy, 150f, 30f), "Open folder"))
                OpenFolder(ReplayFolder);
            if (DkKey(new Rect(body.x + 158f, fy, 90f, 30f), "Refresh", Ui.Neutral))
                RefreshReplays();
            string note = replayNote.Length > 0 ? replayNote : !CanOpenReplay && replay == null ? "To watch one, go to the main menu (leave the lobby first)." : replay != null ? "Watching one now." : "";
            Ui.Label(new Rect(body.x + 262f, fy, body.width - 262f, 30f), note, 12, replayNote.Length > 0 ? Ui.Warn : CrtStyle.Desc, TextAnchor.MiddleLeft);

            // The list.
            var list = new Rect(body.x, fy + 40f, body.width, body.yMax - fy - 40f);
            DkSub(list);
            if (replayList.Count == 0)
            {
                Ui.Label(new Rect(list.x, list.y + 30f, list.width, 24f), "No replays yet", 16, Color.white, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(list.x, list.y + 58f, list.width, 40f), "Turn on recording above and play a run. It shows up here when the run ends.", 12, CrtStyle.Desc, TextAnchor.UpperCenter);
                return;
            }
            const float row = 66f;
            float maxScroll = Mathf.Max(0f, replayList.Count * row - list.height + 8f);
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && list.Contains(e.mousePosition))
            {
                replayScroll = Mathf.Clamp(replayScroll + e.delta.y * 24f, 0f, maxScroll);
                e.Use();
            }
            replayScroll = Mathf.Clamp(replayScroll, 0f, maxScroll);
            ReplayFile open = null, rename = null, delete = null;
            string commit = null;
            GUI.BeginGroup(list);
            for (int i = 0; i < replayList.Count; i++)
            {
                float ry = 4f + i * row - replayScroll;
                if (ry < -row || ry > list.height)
                    continue;
                var r = replayList[i];
                var rr = new Rect(6f, ry, list.width - 12f - (maxScroll > 0f ? 6f : 0f), row - 6f);
                DkSub(rr, new Color(1f, 1f, 1f, 0.03f));
                bool renaming = replayRenamePath == r.Path, deleting = replayDeletePath == r.Path;
                if (renaming)
                {
                    replayRenameText = DkField(new Rect(rr.x + 12f, rr.y + 8f, rr.width - 280f, 28f), replayRenameText, 60);
                    if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
                    {
                        commit = r.Path;
                        e.Use();
                    }
                }
                else
                    Ui.Label(new Rect(rr.x + 14f, rr.y + 6f, rr.width - 290f, 26f), r.Name, 15, Color.white, TextAnchor.MiddleLeft, true);
                string meta = r.Created.ToLocalTime().ToString("MMM d, HH:mm") + "  ·  " + r.Mode + "  ·  wave " + r.MaxWave + "  ·  " + ReplayClock(r.Seconds) + "  ·  " + (r.Bytes / 1024f / 1024f).ToString("0.0") + " MB  ·  " + r.Party;
                Ui.Label(new Rect(rr.x + 14f, rr.y + 34f, rr.width - 28f, 20f), meta, 11, CrtStyle.Desc);
                float bx = rr.xMax - 12f;
                if (renaming)
                {
                    bx -= 80f;
                    if (DkKey(new Rect(bx, rr.y + 8f, 80f, 28f), "Cancel", Ui.Neutral)) replayRenamePath = "";
                    bx -= 86f;
                    if (DkKey(new Rect(bx, rr.y + 8f, 80f, 28f), "Save", Ui.Primary)) commit = r.Path;
                }
                else if (deleting)
                {
                    bx -= 80f;
                    if (DkKey(new Rect(bx, rr.y + 8f, 80f, 28f), "Keep", Ui.Neutral)) replayDeletePath = "";
                    bx -= 86f;
                    if (DkKey(new Rect(bx, rr.y + 8f, 80f, 28f), "Delete", Ui.Danger)) delete = r;
                    Ui.Label(new Rect(bx - 150f, rr.y + 8f, 144f, 28f), "Delete for good?", 12, Ui.Warn, TextAnchor.MiddleRight);
                }
                else
                {
                    bx -= 76f;
                    if (DkKey(new Rect(bx, rr.y + 8f, 76f, 28f), "Delete", Ui.Danger)) { replayDeletePath = r.Path; replayRenamePath = ""; }
                    bx -= 84f;
                    if (DkKey(new Rect(bx, rr.y + 8f, 78f, 28f), "Rename", Ui.Neutral)) { replayRenamePath = r.Path; replayRenameText = r.Name; replayDeletePath = ""; }
                    bx -= 80f;
                    bool can = CanOpenReplay;
                    var was = GUI.enabled;
                    GUI.enabled = can;
                    if (DkKey(new Rect(bx, rr.y + 8f, 74f, 28f), "Open", Ui.Primary)) open = r;
                    GUI.enabled = was;
                }
            }
            GUI.EndGroup();
            if (maxScroll > 0f)
            {
                float frac = list.height / (replayList.Count * row + 8f);
                float th = list.height * frac, ty = list.y + (list.height - th) * (replayScroll / maxScroll);
                Ui.Fill(new Rect(list.xMax - 4f, ty, 3f, th), new Color(1f, 1f, 1f, 0.3f));
            }

            // Do what was clicked after the loop, so the list isn't changed while it's drawn.
            if (commit != null)
            {
                var r = replayList.Find(x => x.Path == commit);
                replayRenamePath = "";
                if (r != null && replayRenameText.Trim().Length > 0 && !r.Rename(replayRenameText))
                    replayNote = "Couldn't rename it.";
                else
                    replayNote = "";
                RefreshReplays();
            }
            if (delete != null)
            {
                replayDeletePath = "";
                replayNote = delete.Delete() ? "" : "Couldn't delete it.";
                RefreshReplays();
            }
            if (open != null)
                OpenReplay(open);
        }
    }
}
