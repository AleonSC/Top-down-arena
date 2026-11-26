using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// Saved runs and records on screen (RunSaves.cs, SaveData.cs, Profile.cs): the main menu's Continue list and
    /// Records page, the lobby's seats when continuing a run, and "Unlocked" toasts in a run.
    public partial class ArenaApp
    {
        List<RunSave> savedRuns = new List<RunSave>();
        string deleteArmed;  // the save whose Delete was clicked once (a second click deletes it)


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

        // --- Play: the saved runs on the bay's screen, behind the case's shutter (MenuMachine.cs) ------------------

        float menuListScroll;   // the saved-runs list's scroll target, in pixels from the top
        float menuListShown;    // where it is drawn (eased toward the target)
        bool menuListDrag;      // the scroll bar's thumb is being dragged
        const float RunRowLatestH = 96f, RunRowH = 48f;  // the newest run's row is tall; every other row is compact

        /// Play was clicked: the shutter rolls up onto your saved runs (or Start a run).
        void OpenPlayPicker()
        {
            savedRuns = SaveStore.List();
            deleteArmed = null;
            menuJoinOpen = menuSetOpen = menuWikiOpen = false;
            menuListScroll = menuListShown = 0f;
            menuListDrag = false;
            menuPlayOpen = true;
        }

        /// Esc, or Play / Join game again: the shutter rolls back down.
        void PlayPickerBack()
        {
            deleteArmed = null;
            menuPlayOpen = false;
            menuJoinOpen = false;
            menuSetOpen = false;
            menuWikiOpen = false;
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
                MenuSound(Sfx.RunDelArm);
                return;
            }
            MenuSound(Sfx.RunDelete);
            if (!SaveStore.Trash(s.Id))
                SaveStore.Delete(s.Id);  // (couldn't move it: erase it, as the key always did)
            savedRuns = SaveStore.List();
            deleteArmed = null;
        }

        /// The bay's screen after Play (the screen is MenuCard): YOUR RUNS, every saved run on a scrolling list, the newest first and
        /// taller (its squad, Continue, a delete key: DEL, then REALLY?), and New run at the foot. With no saves it says so.
        void DrawPlayPicker(float t, bool live)
        {
            var scr = MenuCard;
            bool calm = fxReduced;
            bool any = savedRuns.Count > 0;
            RunSave toContinue = null, toDelete = null;
            var mouse = Event.current.mousePosition;
            CrtUi.Begin(scr);
            bool cursor = !calm && Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
            CrtUi.Text(new Rect(scr.x + 18f, scr.y + 12f, scr.width - 36f, 32f), (any ? "YOUR RUNS" : "START A RUN") + (cursor ? "_" : " "), 22, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
            Ui.Fill(new Rect(scr.x + 12f, scr.y + 50f, scr.width - 24f, 1f), CrtStyle.Ink.WithAlpha(0.25f));
            var list = new Rect(scr.x + 12f, scr.y + 52f, scr.width - 24f, scr.height - 52f - 62f);
            bool inside = live && list.Contains(mouse);
            if (!any)
            {
                CrtUi.Text(new Rect(list.x, list.y + 50f, list.width, 26f), "NO SAVED RUNS YET.", 18, CrtStyle.Ink, TextAnchor.MiddleCenter, true, calm);
                CrtUi.Text(new Rect(list.x + 20f, list.y + 84f, list.width - 40f, 44f), "Start a run: it's saved from its first wave on, until it's won or lost.", 13, CrtStyle.Dim, TextAnchor.UpperCenter, false, calm);
            }
            else
            {
                float total = RunRowLatestH + (savedRuns.Count - 1) * RunRowH, maxScroll = Mathf.Max(0f, total - list.height);
                var grab = new Rect(list.xMax - 14f, list.y, 14f, list.height);
                float thumbH = Mathf.Max(24f, (list.height - 8f) * list.height / total), trackH = list.height - 8f - thumbH;
                var type = Event.current.type;
                if (live && maxScroll > 0f)
                {
                    menuListScroll += WheelOver(list, live) * 24f;  // MachineLook.cs
                    if (type == EventType.MouseDown && grab.Contains(mouse))
                        menuListDrag = true;
                    if (menuListDrag && (type == EventType.MouseDown || type == EventType.MouseDrag))
                        menuListScroll = Mathf.Clamp01((mouse.y - list.y - 4f - thumbH / 2f) / Mathf.Max(1f, trackH)) * maxScroll;
                    if (type == EventType.MouseUp)
                        menuListDrag = false;
                }
                menuListScroll = Mathf.Clamp(menuListScroll, 0f, maxScroll);
                if (type == EventType.Repaint)
                    menuListShown = fxOn && !fxReduced ? Mathf.Lerp(menuListShown, menuListScroll, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime)) : menuListScroll;
                menuListShown = Mathf.Clamp(menuListShown, 0f, maxScroll);
                float rowW = list.width - (maxScroll > 0f ? 14f : 0f);
                GUI.BeginGroup(list);  // the rows in the list's own coordinates, clipped at its edges
                float y = -menuListShown;
                int below = 0;
                for (int i = 0; i < savedRuns.Count; i++)
                {
                    float h = i == 0 ? RunRowLatestH : RunRowH;
                    if (y + h > 0f && y < list.height)
                        DrawRunRow(new Rect(0f, y, rowW, h), savedRuns[i], i == 0, calm, live && inside, ref toContinue, ref toDelete);
                    if (y + h * 0.5f > list.height)
                        below++;
                    y += h;
                }
                GUI.EndGroup();
                if (maxScroll > 0f)
                {
                    var track = new Rect(list.xMax - 8f, list.y + 4f, 4f, list.height - 8f);
                    Ui.Fill(track, CrtStyle.Ink.WithAlpha(0.1f));
                    Ui.Fill(new Rect(track.x, track.y + trackH * (menuListShown / maxScroll), track.width, thumbH), menuListDrag ? CrtStyle.Ink : CrtStyle.Dim);
                    if (below > 0 && menuListShown < maxScroll - 1f)
                    {
                        for (int k = 0; k < 8; k++)
                            Ui.Fill(new Rect(list.x, list.yMax - 22f + k * 2.75f, rowW, 2.75f), CrtStyle.Bg.WithAlpha(0.95f * (k + 1) / 8f));
                        CrtUi.Text(new Rect(list.x, list.yMax - 19f, rowW, 16f), "\u25bc " + below + " MORE", 11, CrtStyle.Ink, TextAnchor.MiddleCenter, true, calm);
                    }
                }
            }
            if (DkKey(new Rect(scr.x + 12f, scr.yMax - 54f, scr.width - 24f, 42f), "NEW RUN", false, calm, icon: PixelArt.Get("ui_play"), size: 16, rawSize: true) && live)
            {
                MenuSound(Sfx.MenuPlay);
                StartNewRun();
            }
            CrtUi.End(scr, Time.unscaledTime, calm);
            if (toContinue != null)
            {
                MenuSound(Sfx.RunContinue);
                ContinueSave(toContinue);
            }
            else if (toDelete != null)
                DeleteRun(toDelete);
        }

        /// One saved run as a row (r in the list's own coordinates). The newest: LATEST RUN, its wave large, its squad, Continue and the
        /// delete key. The rest: the wave, when it was saved and how long it ran, Continue and the delete key.
        void DrawRunRow(Rect r, RunSave s, bool latest, bool calm, bool live, ref RunSave toContinue, ref RunSave toDelete)
        {
            bool armed = deleteArmed == s.Id;
            RunRowHover(r, s.Id);  // the phosphor tick as the mouse comes onto a run (MenuHolo.cs)
            if (armed)
                Ui.Fill(r, CrtStyle.Danger.WithAlpha(0.08f));
            else if (latest)
                Ui.Fill(r, CrtStyle.Ink.WithAlpha(0.04f));
            Ui.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), CrtStyle.Ink.WithAlpha(0.2f));
            string del = armed ? "REALLY?" : "DEL";
            if (latest)
            {
                CrtUi.Text(new Rect(r.x + 8f, r.y + 8f, 140f, 14f), "LATEST RUN", 10, CrtStyle.Dim, TextAnchor.MiddleLeft, false, calm);
                CrtUi.Text(new Rect(r.x + 8f, r.y + 20f, 220f, 34f), "Wave " + (s.ShownWave + 1) + (s.Endless ? " (endless)" : ""), 26, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
                for (int i = 0; i < s.Seats.Count && i < 2; i++)  // the squad, a line each (two at most: the third says "+1")
                {
                    var seat = s.Seats[i];
                    Ui.Fill(new Rect(r.x + 8f, r.y + 59f + i * 15f, 3f, 11f), (Defs.Get(seat.Role).Color).ToColor());
                    string more = i == 1 && s.Seats.Count > 2 ? "  +" + (s.Seats.Count - 2) : "";
                    CrtUi.Text(new Rect(r.x + 16f, r.y + 56f + i * 15f, r.width - 180f, 16f), ClipName(seat.Name, 10) + " · " + Defs.KitName(seat.Kit) + " " + Defs.Get(seat.Role).Name + more, 11, CrtStyle.Dim, TextAnchor.MiddleLeft, false, calm);
                }
                if (DkKey(new Rect(r.xMax - 150f, r.y + 12f, 142f, 40f), "CONTINUE", true, calm, icon: PixelArt.Get("ui_play"), size: 15, rawSize: true) && live)
                    toContinue = s;
                if (DkKey(new Rect(r.xMax - 150f, r.y + 58f, 142f, 26f), del, armed, calm, size: armed ? 11 : 13, danger: true, quiet: true, rawSize: true) && live)
                    toDelete = s;
                return;
            }
            CrtUi.Text(new Rect(r.x + 8f, r.y, 44f, r.height), (s.ShownWave + 1).ToString(), 18, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
            CrtUi.Text(new Rect(r.x + 44f, r.y, 150f, r.height), s.SavedAt.ToLocalTime().ToString("MMM d, HH:mm") + " · " + (int)(s.RunTime / 60f) + " min", 11, CrtStyle.Dim, TextAnchor.MiddleLeft, false, calm);
            if (DkKey(new Rect(r.xMax - 164f, r.y + 8f, 112f, 32f), "CONTINUE", false, calm, icon: PixelArt.Get("ui_play"), size: 11, rawSize: true) && live)
                toContinue = s;
            if (DkKey(new Rect(r.xMax - 52f, r.y + 8f, 44f, 32f), del, armed, calm, size: armed ? 9 : 12, danger: true, quiet: true, rawSize: true) && live)
                toDelete = s;
        }

        static string ClipName(string n, int max) => n == null ? "" : n.Length <= max ? n : n.Substring(0, max - 1) + "…";

        // --- What the profile's Records tab reads (ProfileUi.cs) ----------------------------------------

        static readonly EnemyType[] RecordBosses =
        {
            EnemyType.Warden, EnemyType.HiveQueen, EnemyType.ReaperOfDusk, EnemyType.Singularity, EnemyType.Colossus,
            EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse,
        };

        static string BossName(EnemyType t) => t == EnemyType.ReaperOfDusk ? "Twin Reapers" : t == EnemyType.Warden ? "The Warden" : Defs.Get(t).Name;

        // --- The lobby, continuing a run --------------------------------------------------------------

        /// Instead of the mode switch: which run this is.
        void DrawResumeBanner(Rect r, float t)
        {
            var s = session.Resume;
            Ui.Fill(r, CrtStyle.Ink.WithAlpha(0.95f));  // a lit key that does nothing: which run this is
            CrtKit.Frame(r, 2f, CrtStyle.Ink);
            Ui.Label(new Rect(r.x + 6f, r.y + 4f, r.width - 12f, 26f), "CONTINUING A SAVED RUN", 17, CrtStyle.Bg, TextAnchor.MiddleCenter, true);
            string line = "Starts at wave " + (s.ShownWave + 1) + (s.Endless ? " (endless)" : "") + "  ·  each player takes a seat";
            int ls = LobbyKit.FitSize(line, 12, 9, r.width - 12f, false);
            Ui.Label(new Rect(r.x + 6f, r.y + 29f, r.width - 12f, 18f), line, ls, CrtStyle.Bg.WithAlpha(0.85f), TextAnchor.MiddleCenter);
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
                var color = (Defs.Get(seat.Role).Color).ToColor();
                LobbyEntry sitter = null;
                foreach (var e in session.Lobby.Values)
                    if (e.Seat == i)
                        sitter = e;
                bool mine = sitter != null && sitter.Id == session.MyId;
                var r = new Rect(body.x, y, body.width, 104f);
                DkBox(r, color, mine ? 0.85f : 0.3f);
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
                Ui.Label(new Rect(tx, r.y + 62f, 460f, 20f), Defs.Weapon(seat.Role, seat.Kit, seat.Weapon).Name + "  ·  " + Upgrades.Ability(Upgrades.FirstAbility(seat.Role, seat.Kit, seat.Ability)).Name, 12, Ui.Muted);
                var who = new Rect(r.xMax - 210f, r.y + 16f, 196f, 24f);
                if (sitter != null)
                    DkTag(who, mine ? "YOU" : sitter.Name.ToUpperInvariant(), mine ? Ui.Accent : Ui.Neutral);
                else
                {
                    DkTag(who, "FREE SEAT", new Color(0.3f, 0.34f, 0.42f));
                    if (DkKey(new Rect(r.xMax - 210f, r.y + 54f, 196f, 36f), "TAKE THIS SEAT", false, fxReduced, size: 18, minSize: 12))
                        session.RequestSeat(i);
                }
                y += 112f;
            }
            if (session.Lobby.Count < s.Seats.Count)
                Ui.Label(new Rect(body.x, y + 4f, body.width, 36f), "Seats nobody takes are left out of the continued run.", 12, Ui.Warn, TextAnchor.UpperLeft, false, true);
        }

        // --- In a run ------------------------------------------------------------------------------------

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
                Color head = t.Warning ? new Color(1f, 0.35f, 0.3f, a) : CrtStyle.Gold.WithAlpha(a);
                Ui.Shadowed(new Rect(r.x, r.y + 2f, r.width, 22f), (t.Warning ? "" : "UNLOCKED: ") + t.Unlock.Name.ToUpperInvariant(), 16, head);
                Ui.Label(new Rect(r.x, r.y + 22f, r.width, 16f), t.Unlock.Desc, 11, new Color(1f, 1f, 1f, 0.8f * a), TextAnchor.MiddleCenter);
            }
        }
    }
}
