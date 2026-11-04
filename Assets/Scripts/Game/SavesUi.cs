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
        bool continueOpen, recordsOpen;
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

        bool menuPlayOlder;  // the picker is showing the older runs (every save but the newest)
        int menuOlderPage;   // which page of them (three to a page)

        /// Play was clicked: the box shows the picker (a new run, or your saved runs).
        void OpenPlayPicker()
        {
            savedRuns = SaveStore.List();
            deleteArmed = null;
            menuJoinOpen = false;
            menuPlayOlder = false;
            menuOlderPage = 0;
            menuPlayOpen = true;
        }

        /// Esc or Back: from the older runs to the latest one, or out of the picker to the menu's buttons.
        void PlayPickerBack()
        {
            deleteArmed = null;
            if (menuPlayOlder)
            {
                menuPlayOlder = false;
                menuOlderPage = 0;
            }
            else
                menuPlayOpen = false;
        }

        /// Starts a fresh run: what Play used to do.
        void StartNewRun() => HostGame();

        void ContinueSave(RunSave s)
        {
            if (session.HostResume(s, Port(), nameText))
                addressList = string.Join(", ", NetPeer.LocalAddresses().ToArray());
        }

        /// A delete button that needs a second click: the first one arms it.
        bool DeleteSaveButton(Rect r, string label, RunSave s, float t, bool live)
        {
            bool armed = deleteArmed == s.Id;
            if (!Ui.ActionButton(r, armed ? "Really delete?" : label, armed, Ui.Danger, PixelArt.Get("ui_trash"), armed ? (Color?)null : new Color(1f, 0.6f, 0.64f), 16) || !live)
                return false;
            if (!armed)
            {
                deleteArmed = s.Id;
                return false;
            }
            SaveStore.Delete(s.Id);
            savedRuns = SaveStore.List();
            deleteArmed = null;
            if (savedRuns.Count < 2)
            {
                menuPlayOlder = false;
                menuOlderPage = 0;
            }
            return true;
        }

        /// The box after Play: your latest run with a big Continue, and New run, Older runs and Delete under it. With no saves
        /// it says so, in a box, and offers New run.
        void DrawPlayPicker(float t, bool live)
        {
            var c = MenuCard;
            if (menuPlayOlder && savedRuns.Count > 1)
            {
                DrawOlderRuns(c, t, live);
                return;
            }
            bool any = savedRuns.Count > 0;
            Ui.Fancy(new Rect(c.x + 30f, c.y + 18f, 440f, 34f), any ? "CONTINUE YOUR LATEST RUN" : "START A RUN", 24, Color.white, new Color(0.72f, 1f, 0.82f), TextAnchor.MiddleLeft, SaveColor);
            if (Ui.ActionButton(new Rect(c.xMax - 140f, c.y + 18f, 110f, 34f), "Back", false, Ui.Primary, PixelArt.Get("ui_back"), null, 15) && live)
                PlayPickerBack();
            var r = new Rect(c.x + 30f, c.y + 62f, 620f, 118f);
            Ui.Card(r, SaveColor, 0.25f);
            float by = c.y + 196f;
            if (!any)
            {
                Ui.Label(new Rect(r.x + 20f, r.y + 20f, r.width - 40f, 26f), "No saved runs yet.", 20, Color.white, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(r.x + 40f, r.y + 52f, r.width - 80f, 44f), "Start a run: it's saved from its first wave on, until it's won or lost.", 14, Ui.Muted, TextAnchor.UpperCenter, false, true);
                if (Ui.ActionButton(new Rect(c.x + 30f, by, 620f, 50f), "New run", true, Ui.Primary, PixelArt.Get("ui_play"), null, 20) && live)
                    StartNewRun();
                return;
            }
            var s = savedRuns[0];
            Ui.Label(new Rect(r.x + 18f, r.y + 6f, 230f, 38f), "Wave " + (s.Wave + 1) + (s.Endless ? " (endless)" : ""), 28, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 18f, r.y + 46f, 230f, 16f), "Saved " + s.SavedAt.ToLocalTime().ToString("MMM d, HH:mm"), 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 18f, r.y + 63f, 230f, 16f), "Score " + s.Score + "  ·  " + (int)(s.RunTime / 60f) + " min played", 12, Ui.Muted);
            for (int i = 0; i < s.Seats.Count && i < 3; i++)
            {
                var seat = s.Seats[i];
                Ui.Fill(new Rect(r.x + 250f, r.y + 14f + i * 24f, 4f, 18f), ToColor(Defs.Get(seat.Role).Color));
                Ui.Label(new Rect(r.x + 260f, r.y + 12f + i * 24f, 180f, 22f), seat.Name + " — " + Defs.KitName(seat.Kit) + " " + Defs.Get(seat.Role).Name, 12, Ui.Text);
            }
            DrawSaveBadges(s, r.x + 18f, r.y + 90f, 112f, 6f, 3);
            if (Ui.ActionButton(new Rect(r.xMax - 170f, r.y + 28f, 152f, 62f), "Continue", true, Ui.Primary, PixelArt.Get("ui_play"), null, 22) && live)
                ContinueSave(s);
            const float bw = 200f;
            if (Ui.ActionButton(new Rect(c.x + 30f, by, bw, 50f), "New run", false, Ui.Primary, PixelArt.Get("ui_play"), null, 18) && live)
                StartNewRun();
            int older = savedRuns.Count - 1;
            if (Ui.ActionButton(new Rect(c.x + 30f + bw + 10f, by, bw, 50f), "Older runs (" + older + ")", false, Ui.Primary, PixelArt.Get("ui_book"), null, 17) && live && older > 0)
            {
                menuPlayOlder = true;
                menuOlderPage = 0;
                deleteArmed = null;
            }
            DeleteSaveButton(new Rect(c.x + 30f + 2f * (bw + 10f), by, bw, 50f), "Delete this run", s, t, live);
        }

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

        /// The picker's second view: every save but the newest, three to a page, each with Continue and a delete button.
        void DrawOlderRuns(Rect c, float t, bool live)
        {
            int older = savedRuns.Count - 1, pages = (older + 2) / 3;
            menuOlderPage = Mathf.Clamp(menuOlderPage, 0, pages - 1);
            Ui.Fancy(new Rect(c.x + 30f, c.y + 18f, 300f, 34f), "OLDER RUNS", 24, Color.white, new Color(0.72f, 1f, 0.82f), TextAnchor.MiddleLeft, SaveColor);
            if (pages > 1)
            {
                Ui.Label(new Rect(c.xMax - 330f, c.y + 18f, 80f, 34f), (menuOlderPage + 1) + " / " + pages, 14, Ui.Muted, TextAnchor.MiddleCenter);
                if (Ui.ActionButton(new Rect(c.xMax - 372f, c.y + 18f, 40f, 34f), "‹", false, Ui.Primary, null, null, 20) && live && menuOlderPage > 0)
                    menuOlderPage--;
                if (Ui.ActionButton(new Rect(c.xMax - 248f, c.y + 18f, 40f, 34f), "›", false, Ui.Primary, null, null, 20) && live && menuOlderPage < pages - 1)
                    menuOlderPage++;
            }
            if (Ui.ActionButton(new Rect(c.xMax - 140f, c.y + 18f, 110f, 34f), "Back", false, Ui.Primary, PixelArt.Get("ui_back"), null, 15) && live)
                PlayPickerBack();
            for (int i = 0; i < 3; i++)
            {
                int idx = 1 + menuOlderPage * 3 + i;
                if (idx >= savedRuns.Count)
                    break;
                var s = savedRuns[idx];
                var r = new Rect(c.x + 30f, c.y + 62f + i * 62f, 620f, 56f);
                Ui.Card(r, SaveColor, 0.25f);
                float x = r.x + 16f;
                Ui.Label(new Rect(x, r.y + 3f, 190f, 26f), "Wave " + (s.Wave + 1) + (s.Endless ? " (endless)" : ""), 19, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(x, r.y + 28f, 200f, 14f), "Saved " + s.SavedAt.ToLocalTime().ToString("MMM d, HH:mm"), 11, Ui.Muted);
                Ui.Label(new Rect(x, r.y + 41f, 200f, 14f), "Score " + s.Score + "  ·  " + (int)(s.RunTime / 60f) + " min played", 11, Ui.Muted);
                for (int k = 0; k < s.Seats.Count && k < 2; k++)
                {
                    Ui.Fill(new Rect(x + 216f, r.y + 9f + k * 20f, 4f, 15f), ToColor(Defs.Get(s.Seats[k].Role).Color));
                    Ui.Label(new Rect(x + 226f, r.y + 7f + k * 20f, 190f, 18f), s.Seats[k].Name + " — " + Defs.KitName(s.Seats[k].Kit) + " " + Defs.Get(s.Seats[k].Role).Name, 12, Ui.Text);
                }
                if (Ui.ActionButton(new Rect(r.xMax - 206f, r.y + 8f, 136f, 40f), "Continue", true, Ui.Primary, PixelArt.Get("ui_play"), null, 17) && live)
                    ContinueSave(s);
                bool armed = deleteArmed == s.Id;
                if (Ui.ActionButton(new Rect(r.xMax - 60f, r.y + 8f, 46f, 40f), "", armed, Ui.Danger, PixelArt.Get("ui_trash"), armed ? (Color?)null : new Color(1f, 0.6f, 0.64f)) && live)
                {
                    if (armed)
                    {
                        SaveStore.Delete(s.Id);
                        savedRuns = SaveStore.List();
                        deleteArmed = null;
                        if (savedRuns.Count < 2)
                            menuPlayOlder = false;
                        return;
                    }
                    deleteArmed = s.Id;
                }
            }
            Ui.Label(new Rect(c.x + 30f, c.yMax - 36f, 620f, 16f), deleteArmed != null ? "Click the red trash button again to delete that run." : "The trash button asks twice before it deletes.", 11, Ui.Muted, TextAnchor.MiddleCenter);
        }

        // --- Continue a run ------------------------------------------------------------------------

        void DrawContinue()
        {
            var panel = new Rect(W / 2f - 470f, 40f, 940f, 640f);
            Ui.Panel(panel, true);
            Ui.Fancy(new Rect(panel.x, panel.y + 14f, panel.width, 50f), "CONTINUE A RUN", 34, Color.white, new Color(0.72f, 1f, 0.82f), TextAnchor.MiddleCenter, SaveColor);
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

        // --- Records ---------------------------------------------------------------------------------

        static readonly EnemyType[] RecordBosses =
        {
            EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus,
            EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse,
        };

        static string BossName(EnemyType t) => t == EnemyType.TwinA ? "Twin Reapers" : t == EnemyType.Boss ? "The Warden" : Defs.Get(t).Name;

        /// Records, as the side drawer (MenuDrawer.cs).
        void DrawRecords() { if (!SkipWhileClosing) DrawRecordsWindow(); }

        // --- The lobby, continuing a run --------------------------------------------------------------

        /// Instead of the mode switch: which run this is.
        void DrawResumeBanner(Rect r, float t)
        {
            var s = session.Resume;
            Ui.Pill(r, new Color(0.04f, 0.05f, 0.08f, 0.9f));
            var inner = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f);
            Ui.Pill(inner, new Color(SaveColor.r * 0.45f, SaveColor.g * 0.45f, SaveColor.b * 0.45f, 0.9f));
            Ui.Fill(new Rect(inner.x + 10f, inner.yMax - 3f, inner.width - 20f, 2f), new Color(1f, 1f, 1f, 0.35f + 0.15f * Mathf.Sin(t * 3f)));
            Ui.Fancy(new Rect(inner.x, inner.y + 4f, inner.width, 24f), "CONTINUING A SAVED RUN", 17, Color.white, new Color(0.85f, 1f, 0.9f), TextAnchor.MiddleCenter);
            Ui.Label(new Rect(inner.x, inner.y + 27f, inner.width, 16f), "Starts at wave " + (s.Wave + 1) + (s.Endless ? " (endless)" : "") + "  ·  each player takes a seat", 11, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
        }

        /// Instead of your loadout: the run's seats, who's in each, and a button to move to a free one.
        void DrawResumeSeats(Rect body, float t)
        {
            var s = session.Resume;
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
                    if (Ui.ActionButton(new Rect(r.xMax - 210f, r.y + 54f, 196f, 36f), "Take this seat", true, Ui.Primary, null, null, 16))
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
