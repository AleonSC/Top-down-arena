using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// Saved runs and records on screen (RunSaves.cs, SaveData.cs, Profile.cs): the main menu's Continue list and
    /// Records page, the lobby's seats when continuing a run, and "Unlocked" toasts in a match.
    public partial class ArenaApp
    {
        bool continueOpen;
        List<RunSave> savedRuns = new List<RunSave>();
        string deleteArmed;  // the save whose Delete was clicked once (a second click deletes it)

        static readonly Color SaveColor = new Color(0.42f, 0.86f, 0.62f);

        /// Where saves and the profile live (called once at start): the profile last used, under profiles/ (ProfileStore.cs).
        /// A pre-profiles install is copied into a profile named after the old player name.
        static void InitSaveFolders()
        {
            ProfileStore.Init(Application.persistentDataPath, PlayerPrefs.GetString("player_name", ""));
        }

        float savesCountT;
        int savesCount;

        /// How many runs are saved (for the menu's button), looked up every few seconds rather than every frame.
        int savedRunsCount
        {
            get
            {
                if (Time.unscaledTime >= savesCountT)
                {
                    savesCountT = Time.unscaledTime + 3f;
                    savesCount = SaveStore.List().Count;
                }
                return savesCount;
            }
        }

        void OpenContinue()
        {
            savedRuns = SaveStore.List();
            deleteArmed = null;
            continueOpen = true;
        }

        // --- Play: the saved-runs picker inside the menu's box -------------------------------------

        float menuListScroll;   // the saved-runs list's scroll target, in pixels from the top
        float menuListShown;    // where it is drawn (eased toward the target)
        bool menuListDrag;      // the scroll bar's thumb is being dragged
        string menuHdrShown;    // the header screen's text as typed so far (null = not shown yet, so it appears at once)
        float menuHdrT;         // time until the next character is typed or erased
        const float MenuHdrCharTime = 0.025f;
        const float RunRowLatestH = 126f, RunRowH = 52f;  // the newest run's row is tall; every other row is compact

        /// Where the saved runs scroll (a CRT screen under the header), and the New run button under it.
        static Rect RunsScreenRect(Rect c) => new Rect(c.x + 30f, c.y + 62f, 620f, 190f);
        static Rect NewRunRect(Rect c) => new Rect(c.x + 30f, c.y + 262f, 620f, 46f);

        /// Play was clicked: the box shows the picker (a new run, or your saved runs).
        void OpenPlayPicker()
        {
            savedRuns = SaveStore.List();
            deleteArmed = null;
            menuJoinOpen = false;
            menuListScroll = menuListShown = 0f;
            menuListDrag = false;
            menuHdrShown = null;
            menuPlayOpen = true;
        }

        /// Esc or Back: out of the picker to the menu's buttons.
        void PlayPickerBack()
        {
            deleteArmed = null;
            menuPlayOpen = false;
        }

        /// Starts a fresh run: what Play used to do.
        void StartNewRun() => StartNewRunCrt();  // LobbyCrt.cs: hosts, and opens the lobby with the CRT boot

        void ContinueSave(RunSave s)
        {
            if (session.HostResume(s, Port(), nameText))
                addressList = string.Join(", ", NetPeer.LocalAddresses().ToArray());
        }

        /// A delete that needs a second click: the first one arms it.
        void DeleteRun(RunSave s)
        {
            if (deleteArmed != s.Id)
            {
                deleteArmed = s.Id;
                return;
            }
            SaveStore.Delete(s.Id);
            savedRuns = SaveStore.List();
            deleteArmed = null;
        }

        /// The box after Play: every saved run on one scrolling CRT screen, the newest first and larger, each with Continue and a
        /// delete key (DEL, then REALLY?), and New run under it. With no saves it says so, on the screen, and offers New run.
        bool servoPicker;  // drawn inside the Play move (MenuServo.cs): the header, Quit/Back key and New run are drawn there

        void DrawPlayPicker(float t, bool live)
        {
            var c = MenuCard;
            if (!servoPicker)
                DrawPickerHeader(c, live);
            bool calm = fxReduced;
            var scr = RunsScreenRect(c);
            var newRun = NewRunRect(c);
            bool any = savedRuns.Count > 0;
            if (!servoPicker)
                KeyUi.Bezel(scr);   // the Play move draws its own, following the opening screen (MenuServo.cs)
            RunSave toContinue = null, toDelete = null;
            var mouse = Event.current.mousePosition;
            bool inside = live && scr.Contains(mouse);  // clicks on rows count only while the mouse is really over the screen
            CrtUi.Begin(scr);
            if (!any)
            {
                CrtUi.Text(new Rect(scr.x + 20f, scr.y + 44f, scr.width - 40f, 26f), "NO SAVED RUNS YET.", 20, CrtUi.Ink, TextAnchor.MiddleCenter, true, calm);
                CrtUi.Text(new Rect(scr.x + 40f, scr.y + 76f, scr.width - 80f, 44f), "Start a run: it's saved from its first wave on, until it's won or lost.", 14, CrtUi.Dim, TextAnchor.UpperCenter, false, calm);
            }
            else
            {
                float total = RunRowLatestH + (savedRuns.Count - 1) * RunRowH, maxScroll = Mathf.Max(0f, total - scr.height);
                var grab = new Rect(scr.xMax - 18f, scr.y, 18f, scr.height);  // the scroll bar, with a wide grip
                float thumbH = Mathf.Max(24f, (scr.height - 8f) * scr.height / total);
                float trackH = scr.height - 8f - thumbH;
                var type = Event.current.type;
                if (live && maxScroll > 0f)
                {
                    if (inside && type == EventType.ScrollWheel)
                    {
                        menuListScroll += Event.current.delta.y * 24f;
                        Event.current.Use();
                    }
                    if (type == EventType.MouseDown && grab.Contains(mouse))
                        menuListDrag = true;
                    if (menuListDrag && (type == EventType.MouseDown || type == EventType.MouseDrag))
                        menuListScroll = Mathf.Clamp01((mouse.y - scr.y - 4f - thumbH / 2f) / Mathf.Max(1f, trackH)) * maxScroll;
                    if (type == EventType.MouseUp)
                        menuListDrag = false;
                }
                menuListScroll = Mathf.Clamp(menuListScroll, 0f, maxScroll);
                if (type == EventType.Repaint)
                    menuListShown = fxOn && !fxReduced ? Mathf.Lerp(menuListShown, menuListScroll, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime)) : menuListScroll;
                menuListShown = Mathf.Clamp(menuListShown, 0f, maxScroll);
                float sc = menuListShown, rowW = scr.width - 14f;
                GUI.BeginGroup(scr);  // the rows are drawn in the screen's own coordinates, so anything past its edge is clipped
                float y = -sc;
                int below = 0;
                for (int i = 0; i < savedRuns.Count; i++)
                {
                    float h = i == 0 ? RunRowLatestH : RunRowH;
                    if (y + h > 0f && y < scr.height)
                        DrawRunRow(new Rect(0f, y, rowW, h), savedRuns[i], i == 0, calm, ref toContinue, ref toDelete);
                    if (y + h * 0.5f > scr.height)
                        below++;
                    y += h;
                }
                GUI.EndGroup();
                if (maxScroll > 0f)
                {
                    var track = new Rect(scr.xMax - 9f, scr.y + 4f, 5f, scr.height - 8f);
                    Ui.Fill(track, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.1f));
                    Ui.Fill(new Rect(track.x, track.y + trackH * (sc / maxScroll), track.width, thumbH), menuListDrag ? CrtUi.Ink : CrtUi.Dim);
                    if (below > 0 && sc < maxScroll - 1f)  // "more below": a fade at the foot of the screen with a count
                    {
                        for (int k = 0; k < 8; k++)
                            Ui.Fill(new Rect(scr.x, scr.yMax - 22f + k * 2.75f, rowW, 2.75f), new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.95f * (k + 1) / 8f));
                        CrtUi.Text(new Rect(scr.x, scr.yMax - 19f, rowW, 16f), "\u25bc " + below + " MORE", 12, CrtUi.Ink, TextAnchor.MiddleCenter, true, calm);
                    }
                }
            }
            CrtUi.End(scr, Time.unscaledTime, calm);
            if (servoPicker)
                return;
            if (MenuVhs.Draw(newRun, "TRK 03", live, fxReduced, () => KeyUi.Key(newRun, "New run", PixelArt.Get("ui_play"), 20)) && live)
                StartNewRun();
            if (!inside)
                return;
            if (toContinue != null)
                ContinueSave(toContinue);
            else if (toDelete != null)
                DeleteRun(toDelete);
        }

        /// One saved run as a row of the list (r is in the screen's own coordinates). The newest run is tall, with its squad and tags;
        /// the rest are one line. Both have Continue and a delete key.
        void DrawRunRow(Rect r, RunSave s, bool latest, bool calm, ref RunSave toContinue, ref RunSave toDelete)
        {
            bool armed = deleteArmed == s.Id;
            var rule = new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.25f);
            if (armed)
                Ui.Fill(r, new Color(CrtStyle.Danger.r, CrtStyle.Danger.g, CrtStyle.Danger.b, 0.08f));
            else if (latest)
                Ui.Fill(r, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.04f));
            Ui.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), rule);
            string delLabel = armed ? "REALLY?" : "DEL";
            if (latest)
            {
                // Run column: label, wave, then Saved / Score / Played as label-value pairs.
                float x = r.x + 18f;
                CrtUi.Text(new Rect(x, r.y + 8f, 140f, 14f), "LATEST RUN", 10, CrtUi.Dim, TextAnchor.MiddleLeft, false, calm);
                CrtUi.Text(new Rect(x, r.y + 20f, 160f, 34f), "Wave " + (s.Wave + 1) + (s.Endless ? " (endless)" : ""), 26, CrtUi.Ink, TextAnchor.MiddleLeft, true, calm);
                string[] labs = { "SAVED", "SCORE", "PLAYED" };
                string[] vals = { s.SavedAt.ToLocalTime().ToString("MMM d, HH:mm"), s.Score.ToString(), (int)(s.RunTime / 60f) + " min" };
                for (int i = 0; i < 3; i++)
                {
                    CrtUi.Text(new Rect(x, r.y + 56f + i * 15f, 50f, 14f), labs[i], 10, CrtUi.Dim, TextAnchor.MiddleLeft, false, calm);
                    CrtUi.Text(new Rect(x + 52f, r.y + 55f + i * 15f, 110f, 15f), vals[i], 12, CrtUi.Ink, TextAnchor.MiddleLeft, false, calm);
                }
                Ui.Fill(new Rect(r.x + 184f, r.y + 12f, 1f, 86f), rule);
                // Squad column: one line per player, colour bar, name, then class.
                float sx = r.x + 202f;
                CrtUi.Text(new Rect(sx, r.y + 8f, 100f, 14f), "SQUAD", 10, CrtUi.Dim, TextAnchor.MiddleLeft, false, calm);
                for (int i = 0; i < s.Seats.Count && i < 3; i++)
                {
                    var seat = s.Seats[i];
                    Ui.Fill(new Rect(sx, r.y + 28f + i * 23f, 4f, 16f), ToColor(Defs.Get(seat.Role).Color));
                    CrtUi.Text(new Rect(sx + 12f, r.y + 26f + i * 23f, 100f, 20f), ClipName(seat.Name, 12), 13, CrtUi.Ink, TextAnchor.MiddleLeft, true, calm);
                    CrtUi.Text(new Rect(sx + 114f, r.y + 26f + i * 23f, 110f, 20f), Defs.KitName(seat.Kit) + " " + Defs.Get(seat.Role).Name, 12, CrtUi.Dim, TextAnchor.MiddleLeft, false, calm);
                }
                Ui.Fill(new Rect(r.xMax - 188f, r.y + 12f, 1f, 86f), rule);
                DrawSaveBadges(s, r.x + 18f, r.y + 100f, 112f, 6f, 4);
                bool blink = calm || Mathf.FloorToInt(Time.unscaledTime * 1.5f) % 2 == 0;
                CrtUi.Text(new Rect(r.xMax - 170f, r.y + 6f, 152f, 14f), (blink ? "\u25cf " : "  ") + "READY", 10, CrtUi.Dim, TextAnchor.MiddleCenter, false, calm);
                if (CrtUi.Button(new Rect(r.xMax - 170f, r.y + 24f, 152f, 50f), "CONTINUE", PixelArt.Get("ui_play"), 20, calm))
                    toContinue = s;
                if (CrtUi.Small(new Rect(r.xMax - 170f, r.y + 82f, 152f, 28f), delLabel, null, armed ? 12 : 15, calm, true, armed))
                    toDelete = s;
                return;
            }
            // A compact row: wave, saved, score, played, players, then Continue and the delete key.
            float[] colX = { 0f, 38f, 126f, 170f, 218f };
            float cx = r.x + 14f;
            CrtUi.Text(new Rect(cx + colX[0], r.y + 4f, 46f, s.Endless ? 30f : r.height - 8f), (s.Wave + 1).ToString(), 24, CrtUi.Ink, TextAnchor.MiddleLeft, true, calm);
            if (s.Endless)
                CrtUi.Text(new Rect(cx + colX[0], r.y + 32f, 46f, 14f), "endless", 10, CrtUi.Dim, TextAnchor.MiddleLeft, false, calm);
            CrtUi.Text(new Rect(cx + colX[1], r.y + 4f, 86f, r.height - 8f), s.SavedAt.ToLocalTime().ToString("MMM d, HH:mm"), 12, CrtUi.Ink, TextAnchor.MiddleLeft, false, calm);
            CrtUi.Text(new Rect(cx + colX[2], r.y + 4f, 46f, r.height - 8f), s.Score.ToString(), 13, CrtUi.Ink, TextAnchor.MiddleLeft, false, calm);
            CrtUi.Text(new Rect(cx + colX[3], r.y + 4f, 56f, r.height - 8f), (int)(s.RunTime / 60f) + " min", 12, CrtUi.Ink, TextAnchor.MiddleLeft, false, calm);
            int n = Mathf.Min(s.Seats.Count, 3);
            float top = r.y + (r.height - n * 15f) / 2f;
            for (int k = 0; k < n; k++)
            {
                var seat = s.Seats[k];
                Ui.Fill(new Rect(cx + colX[4], top + k * 15f + 2f, 3f, 11f), ToColor(Defs.Get(seat.Role).Color));
                CrtUi.Text(new Rect(cx + colX[4] + 9f, top + k * 15f - 1f, 80f, 16f), ClipName(seat.Name, 10), 11, CrtUi.Ink, TextAnchor.MiddleLeft, true, calm);
                CrtUi.Text(new Rect(cx + colX[4] + 78f, top + k * 15f - 1f, 84f, 16f), Defs.KitName(seat.Kit) + " " + Defs.Get(seat.Role).Name, 11, CrtUi.Dim, TextAnchor.MiddleLeft, false, calm);
            }
            if (CrtUi.Small(new Rect(r.xMax - 202f, r.y + 6f, 132f, 36f), "CONTINUE", PixelArt.Get("ui_play"), 13, calm))
                toContinue = s;
            if (CrtUi.Small(new Rect(r.xMax - 66f, r.y + 6f, 66f, 36f), delLabel, null, armed ? 12 : 15, calm, true, armed))
                toDelete = s;
        }

        /// The picker's header: a small CRT screen of its own with the title, and a physical Back key beside it (a slab with a lamp).
        void DrawPickerHeader(Rect c, bool live)
        {
            bool calm = fxReduced;
            var hs = new Rect(c.x + 30f, c.y + 10f, 488f, 44f);
            CrtUi.Begin(hs);
            string title = savedRuns.Count > 0 ? "YOUR RUNS" : "START A RUN";
            // The title is typed: the old words are erased from the end, then the new ones typed in (when the last run is deleted).
            bool typing = false;
            if (menuHdrShown == null || !(fxOn && !fxReduced))
                menuHdrShown = title;
            else if (menuHdrShown != title)
            {
                typing = true;
                if (Event.current.type == EventType.Repaint)
                {
                    menuHdrT -= Time.unscaledDeltaTime;
                    while (menuHdrT <= 0f && menuHdrShown != title)
                    {
                        menuHdrT += MenuHdrCharTime;
                        menuHdrShown = title.StartsWith(menuHdrShown) ? title.Substring(0, menuHdrShown.Length + 1) : menuHdrShown.Substring(0, menuHdrShown.Length - 1);
                    }
                }
            }
            bool cursor = typing || calm == false && Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
            CrtUi.Text(new Rect(hs.x + 16f, hs.y + 4f, 340f, 36f), menuHdrShown + (cursor ? "_" : " "), 24, CrtUi.Ink, TextAnchor.MiddleLeft, true, calm);
            CrtUi.End(hs, Time.unscaledTime, calm);
            var br = new Rect(c.xMax - 30f - 120f, c.y + 10f, 120f, 44f);
            bool back = MenuVhs.Draw(br, "TRK 02", live, fxReduced, () =>
                KeyUi.Key(br, "Back", PixelArt.Get("ui_back"), 15, true));  // a keycap with its lamp always lit
            if (live && back)
                PlayPickerBack();
        }

        static string ClipName(string n, int max) => n == null ? "" : n.Length <= max ? n : n.Substring(0, max - 1) + "…";

        /// The path, difficulty, modifier and dev-tools tags of a save, in a row (at most max).
        void DrawSaveBadges(RunSave s, float x, float y, float w, float gap, int max)
        {
            int n = 0;
            if (s.CurrentPath != PathId.None && s.PathTo > s.Wave && n < max)
            {
                Ui.Badge(new Rect(x + n * (w + gap), y, w, 20f), Game.PathName(s.CurrentPath).ToUpperInvariant(), new Color(0.55f, 0.4f, 0.85f));
                n++;
            }
            if (s.Diff.Level != DifficultyLevel.Normal && n < max)
            {
                Ui.Badge(new Rect(x + n * (w + gap), y, w, 20f), s.Diff.Name.ToUpperInvariant(), DiffBadgeColor(s.Diff.Level));
                n++;
            }
            int mods = Modifiers.Count(s.Mods);
            if (mods > 0 && n < max)
            {
                Ui.Badge(new Rect(x + n * (w + gap), y, w, 20f), mods + (mods == 1 ? " MODIFIER" : " MODIFIERS"), ModColor);
                n++;
            }
            if (s.DevUsed && n < max)
                Ui.Badge(new Rect(x + n * (w + gap), y, w, 20f), "DEV TOOLS USED", Ui.Neutral);
        }

        // --- Continue a run ------------------------------------------------------------------------

        void DrawContinue()
        {
            var panel = new Rect(W / 2f - 470f, 40f, 940f, 640f);
            Ui.Panel(panel, true);
            Ui.Glitch(new Rect(panel.x, panel.y + 14f, panel.width, 50f), "CONTINUE A RUN", 34, Color.white, TextAnchor.MiddleCenter, SaveColor);
            Ui.Label(new Rect(panel.x, panel.y + 60f, panel.width, 18f), "Runs save themselves as each wave starts, and when you leave between waves. You host the run; friends join and take their seats.",
                12, Ui.Muted, TextAnchor.MiddleCenter);
            float x = panel.x + 30f, w = panel.width - 60f, y = panel.y + 96f;
            if (savedRuns.Count == 0)
                Ui.Label(new Rect(x, y + 120f, w, 60f), "No saved runs yet.\nStart a run: it's saved from its first wave on, until it's won or lost.", 16, Ui.Muted, TextAnchor.MiddleCenter, false, true);
            for (int i = 0; i < savedRuns.Count && i < 5; i++)
            {
                DrawSaveRow(new Rect(x, y, w, 92f), savedRuns[i]);
                y += 100f;
            }
            if (savedRuns.Count > 5)
                Ui.Label(new Rect(x, y - 4f, w, 16f), "and " + (savedRuns.Count - 5) + " older (delete some to see them)", 11, Ui.Muted, TextAnchor.MiddleCenter);
            if (session.Status.Length > 0)
                Ui.Label(new Rect(x, panel.yMax - 100f, w, 30f), session.Status, 13, Ui.Warn, TextAnchor.MiddleCenter, false, true);
            if (Ui.ActionButton(new Rect(panel.x + panel.width / 2f - 100f, panel.yMax - 62f, 200f, 44f), "Back", false, Ui.Primary, PixelArt.Get("ui_back"), null, 16))
                continueOpen = false;
        }

        void DrawSaveRow(Rect r, RunSave s)
        {
            Ui.Card(r, SaveColor, 0.25f);
            float x = r.x + 18f;
            Ui.Label(new Rect(x, r.y + 8f, 260f, 30f), "Wave " + (s.Wave + 1) + (s.Endless ? "  (endless)" : ""), 22, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x, r.y + 38f, 260f, 18f), "Saved " + s.SavedAt.ToLocalTime().ToString("MMM d, HH:mm"), 12, Ui.Muted);
            Ui.Label(new Rect(x, r.y + 56f, 260f, 18f), "Score " + s.Score + "  ·  " + (int)(s.RunTime / 60f) + " min played", 12, Ui.Muted);
            // The seats, as they'll be taken back.
            float sx = x + 250f;
            for (int i = 0; i < s.Seats.Count; i++)
            {
                var seat = s.Seats[i];
                var c = ToColor(Defs.Get(seat.Role).Color);
                Ui.Fill(new Rect(sx, r.y + 12f + i * 24f, 4f, 18f), c);
                Ui.Label(new Rect(sx + 10f, r.y + 10f + i * 24f, 290f, 22f), seat.Name + "  —  " + Defs.KitName(seat.Kit) + " " + Defs.Get(seat.Role).Name + ", level " + seat.Level, 13, Ui.Text);
            }
            // What's special about it.
            float bx = r.xMax - 324f, by = r.y + 12f;
            if (s.CurrentPath != PathId.None && s.PathTo > s.Wave)
            {
                Ui.Badge(new Rect(bx, by, 150f, 20f), Game.PathName(s.CurrentPath).ToUpperInvariant(), new Color(0.55f, 0.4f, 0.85f));
                by += 26f;
            }
            if (s.Diff.Level != DifficultyLevel.Normal)
            {
                Ui.Badge(new Rect(bx, by, 150f, 20f), s.Diff.Name.ToUpperInvariant(), DiffBadgeColor(s.Diff.Level));
                by += 26f;
            }
            int mods = Modifiers.Count(s.Mods);
            if (mods > 0)
            {
                Ui.Badge(new Rect(bx, by, 150f, 20f), mods + (mods == 1 ? " MODIFIER" : " MODIFIERS"), ModColor);
                by += 26f;
            }
            if (s.DevUsed)
                Ui.Badge(new Rect(bx, by, 150f, 20f), "DEV TOOLS USED", Ui.Neutral);
            // Continue, or delete (click twice).
            bool armed = deleteArmed == s.Id;
            if (Ui.ActionButton(new Rect(r.xMax - 150f, r.y + 10f, 136f, 40f), "Continue", true, Ui.Primary, PixelArt.Get("ui_play"), null, 16))
            {
                if (session.HostResume(s, Port(), nameText))
                {
                    addressList = string.Join(", ", NetPeer.LocalAddresses().ToArray());
                    continueOpen = false;
                }
            }
            if (Ui.ActionButton(new Rect(r.xMax - 150f, r.y + 56f, 136f, 28f), armed ? "Really delete?" : "Delete", armed, Ui.Danger, PixelArt.Get("ui_trash"), armed ? (Color?)null : new Color(1f, 0.6f, 0.64f), 13))
            {
                if (armed)
                {
                    SaveStore.Delete(s.Id);
                    savedRuns = SaveStore.List();
                    deleteArmed = null;
                }
                else
                    deleteArmed = s.Id;
            }
        }

        // --- What the profile's Records tab reads (ProfileUi.cs) ----------------------------------------

        static readonly EnemyType[] RecordBosses =
        {
            EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus,
            EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse,
        };

        static string BossName(EnemyType t) => t == EnemyType.TwinA ? "Twin Reapers" : t == EnemyType.Boss ? "The Warden" : Defs.Get(t).Name;

        // --- The lobby, continuing a run --------------------------------------------------------------

        /// Instead of the mode switch: which run this is.
        void DrawResumeBanner(Rect r, float t)
        {
            var s = session.Resume;
            Ui.Fill(r, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.95f));  // a lit key that does nothing: which run this is
            CrtKit.Frame(r, 2f, CrtUi.Ink);
            Ui.Label(new Rect(r.x + 6f, r.y + 4f, r.width - 12f, 26f), "CONTINUING A SAVED RUN", 17, CrtUi.Bg, TextAnchor.MiddleCenter, true);
            string line = "Starts at wave " + (s.Wave + 1) + (s.Endless ? " (endless)" : "") + "  ·  each player takes a seat";
            int ls = LobbyKit.FitSize(line, 12, 9, r.width - 12f, false);
            Ui.Label(new Rect(r.x + 6f, r.y + 29f, r.width - 12f, 18f), line, ls, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.85f), TextAnchor.MiddleCenter);
        }

        /// Instead of your loadout: the run's seats, who's in each, and a button to move to a free one.
        void DrawResumeSeats(Rect panel, float t)
        {
            var s = session.Resume;
            LobbyKit.Panel(panel);  // the lobby screen with its role wash (LobbyKit.cs); the seats sit inside it
            var body = new Rect(panel.x + 16f, panel.y + 14f, panel.width - 32f, panel.height - 28f);
            Ui.Label(new Rect(body.x, body.y, body.width, 20f), "Everyone plays the character they had. Players take back the seat with their name; move to a free seat if yours isn't picked.", 12, Ui.Muted, TextAnchor.MiddleLeft, false, true);
            float y = body.y + 34f;
            for (int i = 0; i < s.Seats.Count; i++)
            {
                var seat = s.Seats[i];
                var color = ToColor(Defs.Get(seat.Role).Color);
                LobbyEntry sitter = null;
                foreach (var e in session.Lobby.Values)
                    if (e.Seat == i)
                        sitter = e;
                bool mine = sitter != null && sitter.Id == session.MyId;
                var r = new Rect(body.x, y, body.width, 104f);
                Ui.Card(r, color, mine ? 0.85f : 0.3f);
                var face = new Rect(r.x + 12f, r.y + 12f, 80f, 80f);
                Ui.Pill(face, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.9f));
                if (BeginUiGl())
                {
                    UiModelAt(new V2(face.center.x, face.center.y), 1.05f);
                    DrawPreviewPlayer(seat.Role, seat.Kit, seat.Weapon, t + i * 1.7f);
                    EndUiGl();
                }
                float tx = face.xMax + 16f;
                Ui.Label(new Rect(tx, r.y + 10f, 360f, 26f), seat.Name, 20, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(tx, r.y + 38f, 420f, 20f), Defs.KitName(seat.Kit) + " " + Defs.Get(seat.Role).Name + "  ·  level " + seat.Level, 14, Color.Lerp(color, Color.white, 0.15f), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(tx, r.y + 62f, 460f, 20f), Defs.Weapon(seat.Role, seat.Kit, seat.Weapon).Name + "  ·  " + Upgrades.Ability(Upgrades.RoleAbility(seat.Role, seat.Kit, seat.Ability)).Name, 12, Ui.Muted);
                var who = new Rect(r.xMax - 210f, r.y + 16f, 196f, 24f);
                if (sitter != null)
                    Ui.Badge(who, mine ? "YOU" : sitter.Name.ToUpperInvariant(), mine ? Ui.Accent : Ui.Neutral);
                else
                {
                    Ui.Badge(who, "FREE SEAT", new Color(0.3f, 0.34f, 0.42f));
                    if (LobbyKit.Key(new Rect(r.xMax - 210f, r.y + 54f, 196f, 36f), "TAKE THIS SEAT", false, fxReduced, size: 18, minSize: 12))
                        session.RequestSeat(i);
                }
                y += 112f;
            }
            if (session.Lobby.Count < s.Seats.Count)
                Ui.Label(new Rect(body.x, y + 4f, body.width, 36f), "Seats nobody takes are left out of the continued run.", 12, Ui.Warn, TextAnchor.UpperLeft, false, true);
        }

        // --- In a match ------------------------------------------------------------------------------------

        /// "Unlocked: ..." when this player's records earn something (RunSaves.cs).
        void DrawUnlockToasts(float cx, float y)
        {
            var toasts = game.UnlockToasts;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                toasts[i].T -= Time.unscaledDeltaTime;
                if (toasts[i].T <= 0f)
                    toasts.RemoveAt(i);
            }
            for (int i = 0; i < toasts.Count && i < 3; i++)
            {
                var t = toasts[toasts.Count - 1 - i];
                float a = Mathf.Clamp01(t.T / 0.5f);
                var r = new Rect(cx - 260f, y + i * 48f, 520f, 42f);
                Ui.Pill(r, new Color(0.12f, 0.09f, 0.02f, 0.85f * a));
                Color head = t.Warning ? new Color(1f, 0.35f, 0.3f, a) : new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, a);
                Ui.Shadowed(new Rect(r.x, r.y + 2f, r.width, 22f), (t.Warning ? "" : "UNLOCKED: ") + t.Unlock.Name.ToUpperInvariant(), 16, head);
                Ui.Label(new Rect(r.x, r.y + 22f, r.width, 16f), t.Unlock.Desc, 11, new Color(1f, 1f, 1f, 0.8f * a), TextAnchor.MiddleCenter);
            }
        }
    }
}
