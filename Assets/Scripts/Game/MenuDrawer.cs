using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The menu windows as a CRT cabinet (Docs/Mockups/crt-pause-menu.html; the cabinet and window machinery is CrtWindows.cs):
    /// the pause menu, the lobby's Esc menu, Settings, How to play and the Records pages. A tall cabinet on the left holds a
    /// header screen and one screen of digital keys (the pause menu's choices, or Settings' four sections); what a key opens
    /// is a CRT window to its right, which slides out of the cabinet the first time and only retypes after that. The pause
    /// menu leaves the frozen arena in view, with a "this run" screen over it when no window is out.
    public partial class ArenaApp
    {
        // The floating cabinet: 24 px in from the left, top and bottom; CabW x CabH is the inside of its 1 px outline.
        const float CabX = 24f, CabY = 24f, CabW = 378f, CabH = 670f;
        // The drawer stops short of whatever sits to its right by the margin that box has to the screen's other edge: the lobby's loadout box
        // is LobbyBodyRect (440 to 1244 of 1280), so a 36 px gap, and the window the drawer opens takes the same place (DrawerPanel). This is the
        // standard placement for every drawer menu; the drawer's contents are CabInW wide (rows CabRowW) inside it.
        const float CabInW = CabW - 50f, CabRowW = CabW - 66f;
        const float DrawerSlideW = CabX + CabW + 2f + 40f;  // how far it slides to be out of sight, its glow included
        static readonly Rect DrawerPanel = new Rect(440f, 40f, 804f, 640f);  // the open section, right of the drawer: where the lobby's loadout box is

        int pauseSel;            // the pause menu's highlighted choice (↑ ↓, Enter)
        bool pauseSettings;      // the pause menu showing Settings
        bool pauseHowTo;         // the pause (or lobby) menu showing How to play
        bool pauseRecords;       // the lobby or pause menu showing the profile (ProfileUi.cs, without the Look tab)
        Vector2 pauseMouse;      // where the mouse was last frame (hovering moves the highlight only when it moves)
        int howToShown;
        int settingsShown = 1;   // the section on screen (follows settingsGroup once the old one has erased)
        int settingsGroup = 1;   // 0 Audio, 1 Visual effects, 2 Gameplay, 3 Keybinds
        int recordsSection;      // the Records tab's open row: 0 Overview, 1 Bosses, 2 Classes, 3 Challenge rooms, 4 Recent runs (ProfileUi.cs)
        int howToTopic;          // HowToTopics
        string drawerKey;        // which window the drawer last slid in for
        float drawerAt = -10f;

        /// A window is drawn: when it's a different one, it opens (its clock starts; the pause menu runs its own,
        /// WindowAnim.cs). Returns 1 (kept for the callers).
        float DrawerIn(string key, bool opens)
        {
            if (key != drawerKey)
            {
                drawerKey = key;
                drawerAt = Time.unscaledTime;
                if (opens)
                    winOpenAt = drawerAt;
                ResetCabWindow();
                CrtKit.Forget("cab.");
            }
            return 1f;
        }

        /// The pause menu's players, in the run card: compact cards stacked up from its bottom (the Tab screen has more).
        void DrawPartyStack(Rect card)
        {
            var players = new List<Player>(game.Players.Values);
            const float h = 46f, gap = 6f;
            float x = card.x + 20f, w = card.width - 40f, y = card.yMax - 16f - h;
            foreach (var p in players)  // the first at the bottom, each next one above it
            {
                PartyCompact(new Rect(x, y, w, h), p);
                y -= h + gap;
            }
            Ui.Label(new Rect(x + 2f, y + h - 16f, 200f, 14f), "PLAYERS", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Fill(new Rect(x, y + h - 26f, w, 1f), new Color(1f, 1f, 1f, 0.08f));
        }

        /// A one-line player card: class icon, name, class and weapon, level, and a thin health bar along the bottom.
        void PartyCompact(Rect r, Player p)
        {
            var col = ToColor(p.Def.Color);
            Ui.Card(r, col, p.Id == game.MyId ? 0.4f : 0.15f);
            var art = new Rect(r.x + 2f, r.y + 2f, r.height - 4f, r.height - 4f);
            Ui.Pill(art, new Color(col.r * 0.15f, col.g * 0.15f, col.b * 0.15f, 0.95f));
            Ui.Icon(new Rect(art.center.x - 15f, art.center.y - 15f, 30f, 30f), PixelArt.Get("class_" + Defs.KitName(p.Kit).ToLowerInvariant()));
            float x = art.xMax + 10f;
            string name = p.Name + (p.Id == game.MyId ? "  (you)" : "");
            Ui.Label(new Rect(x, r.y + 4f, 200f, 22f), name, 15, Color.white, TextAnchor.MiddleLeft, true);
            float cx = x + Ui.TextWidth(name, 15, true) + 10f, room = r.xMax - 64f - cx;
            string cls = Defs.KitName(p.Kit) + " " + p.Def.Name, full = cls + "  ·  " + p.Weapon.Name;
            Ui.Label(new Rect(cx, r.y + 4f, room, 22f), Ui.TextWidth(full, 12, true) <= room ? full : cls, 12, col, TextAnchor.MiddleLeft, true);  // the weapon only when it fits
            var lvl = new Rect(r.xMax - 54f, r.y + 7f, 44f, 17f);
            Ui.Pill(lvl, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.9f));
            Ui.Label(lvl, "LV " + p.Level, 10, new Color(0.08f, 0.06f, 0.02f), TextAnchor.MiddleCenter, true);
            Ui.Bar(new Rect(x, r.yMax - 12f, r.xMax - 10f - x, 5f), p.Downed ? 0f : p.Hp / Mathf.Max(1f, p.MaxHp), p.Downed ? Ui.Danger : new Color(0.31f, 0.82f, 0.4f), new Color(0f, 0f, 0f, 0.5f));
        }

        /// A coloured chip with a label (path, modifiers, difficulty).
        float Chip(float x, float y, string text, Color col)
        {
            float w = Ui.TextWidth(text, 11, true) + 18f;
            Ui.Pill(new Rect(x, y, w, 22f), new Color(col.r, col.g, col.b, 0.16f));
            Ui.Label(new Rect(x, y, w, 22f), text, 11, col, TextAnchor.MiddleCenter, true);
            return w;
        }

        /// An icon tile on a screen: a faint tint of its colour in a frame of that colour.
        void IconTile(Rect r, Texture2D icon, Color col, float iconSize)
        {
            Ui.Fill(r, new Color(col.r * 0.18f, col.g * 0.18f, col.b * 0.18f, 0.95f));
            CrtKit.Frame(r, 2f, new Color(col.r, col.g, col.b, 0.75f));
            if (icon != null)
                Ui.Icon(new Rect(r.center.x - iconSize / 2f, r.center.y - iconSize / 2f, iconSize, iconSize), icon);
        }

        // --- The lobby's menu (Esc) --------------------------------------------------------------------------------------

        static readonly string[] LobbyChoices = { "LOBBY", "SETTINGS", "FIELD GUIDE", "PROFILE", "HOW TO PLAY", "MAIN MENU", "QUIT GAME" };
        const int FieldGuideRow = 2;  // hidden from both menus for now (the code in FieldGuide.cs stays)
        bool lobbyMenuOpen;
        int lobbySel;

        static List<int> LobbyRows()
        {
            var rows = new List<int>();
            for (int i = 0; i < LobbyChoices.Length; i++)
                if (i != FieldGuideRow && i != 3 && i != 5)  // no Profile (main menu or your lobby card) and no Main menu (the lobby's back arrow does that)
                    rows.Add(i);
            return rows;
        }

        /// Esc in the lobby: the menu slides in (its own clock starts here).
        void OpenLobbyMenu()
        {
            lobbyMenuOpen = true;
            profileFromCard = false;
            lobbySel = 0;
            pauseSettings = pauseHowTo = pauseRecords = false;
            drawerKey = null;
            ResetCabWindow();
            winOpenAt = Time.unscaledTime;
            GUIUtility.keyboardControl = 0;
        }

        void CloseLobbyMenu() => CloseWindow(() => { lobbyMenuOpen = false; pauseSettings = pauseHowTo = pauseRecords = false; });

        /// The lobby's menu: back to the lobby, Settings (the cabinet's list switches to its sections), the Field guide, leaving
        /// for the main menu, or quitting. The lobby stays behind it, dimmed.
        void DrawLobbyMenu()
        {
            if (SkipWhileClosing)
                return;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.6f * WindowVis));
            if (pauseRecords)
            {
                DrawProfileWindow(true);  // its Back returns to this list
                return;
            }
            DrawerIn("pause", false);
            if (bgBig && pauseSettings && settingsGroup == 1 && cabWin)
            {
                DrawBackgroundBig();  // MenuBackgrounds.cs: only the background and its controls
                return;
            }
            bgBig = false;
            DrawCabWindow();
            if (pauseSettings)
            {
                DrawSettingsCabinet(true);
                return;
            }
            LobbyMenuKeys();
            cabBootLog = "> LINK OK   > SAVE OK   > LOBBY";
            BeginDrawer("MENU", Ui.Accent, 1f, "Esc closes this menu");
            string[] lines = { "Close this menu", "Audio, visual effects, aim line", "Every enemy, weapon and upgrade", "Your rank, records and unlocks", "Controls and tips",
                session.IsHost ? "Close the lobby, back to the title screen" : "Leave the lobby, back to the title screen", "Close the game" };
            Color[] lcols = { Ui.Primary, new Color(0.6f, 0.66f, 0.82f), FieldGuideColor, Ui.Gold, HowToColor, new Color(1f, 0.6f, 0.45f), new Color(1f, 0.45f, 0.5f) };
            string[] licons = { "ui_play", "ui_gear", "ui_book", "ui_trophy", "ui_help", "ui_back", "ui_power" };
            float y = cabScreen.y + 10f;
            var lrows = LobbyRows();
            for (int k = 0; k < lrows.Count; k++)
            {
                int i = lrows[k];
                var r = new Rect(34f, y, CabRowW, 58f);
                if (cabScreenOn && r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint && Event.current.mousePosition != pauseMouse)
                    lobbySel = k;
                if (DrawerRow(r, PixelArt.Get(licons[i]), LobbyChoices[i], lines[i], i == 0 ? "Esc" : null, null, lcols[i], k == lobbySel, null, i == LobbyChoices.Length - 1))
                    LobbyChoose(i);
                y += 62f;
            }
            if (Event.current.type == EventType.Repaint)
                pauseMouse = Event.current.mousePosition;
            EndDrawerScreen();
            CabFoot("ESC CLOSE / ARROWS SELECT / ENTER CHOOSE");
            EndDrawer();
        }

        void LobbyMenuKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0)
                return;
            var rows = LobbyRows();
            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W)
                lobbySel = (lobbySel + rows.Count - 1) % rows.Count;
            else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S)
                lobbySel = (lobbySel + 1) % rows.Count;
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                LobbyChoose(rows[Mathf.Clamp(lobbySel, 0, rows.Count - 1)]);
            else
                return;
            e.Use();
        }

        void LobbyChoose(int i)
        {
            switch (i)
            {
                case 0: CloseLobbyMenu(); break;
                case 1: pauseSettings = true; CloseCabWindow(); break;
                case 2: OpenFieldGuide(); break;
                case 3: pauseRecords = true; break;
                case 4: pauseHowTo = true; OpenCabWindow(2); break;  // a window beside the list
                case 5:
                    lobbyMenuOpen = false;
                    pauseSettings = pauseHowTo = false;
                    StartLobbyLeave();  // the way in from the menu, backwards (LobbyEnter.cs)
                    break;
                default: Application.Quit(); break;
            }
        }

        // --- The pause menu -------------------------------------------------------------------------------------------

        static readonly string[] PauseChoices = { "RESUME", "RUN STATS", "FIELD GUIDE", "PROFILE", "HOW TO PLAY", "SETTINGS", "QUIT" };
        const int PauseQuit = 6;  // asks "to lobby or desktop?" (DrawLobbyConfirm); the lobby is the host's: it takes everyone back

        /// The pause menu's choices for this player.
        List<int> PauseRows()
        {
            // Resume, Settings, How to play, Quit. Run stats is the small key beside the cabinet (DrawRunStatsKey); the Field guide is
            // hidden for now; the profile opens from the main menu or your lobby card only.
            return new List<int> { 0, 5, 4, PauseQuit };
        }

        bool CanQuitToLobby => session.IsHost && !LabRunning;

        /// The pause menu (DrawPauseMenu applies its opening): the cabinet of choices, a window to its right for what you pick,
        /// and the run's screen over the arena while no window is out.
        void DrawPauseWindow()
        {
            if (pauseRecords)
            {
                DrawProfileWindow(true);  // its Back returns to the choices
                return;
            }
            float t = Time.unscaledTime;
            if (pauseConfirmLobby)
            {
                DrawLobbyConfirm(t);
                return;
            }
            DrawerIn("pause", false);
            if (bgBig && pauseSettings && settingsGroup == 1 && cabWin)
            {
                DrawBackgroundBig();  // MenuBackgrounds.cs: only the background and its controls
                return;
            }
            bgBig = false;
            DrawCabWindow();  // (first: it comes out from behind the cabinet)
            if (pauseSettings)
            {
                DrawSettingsCabinet(true);
                return;
            }
            PauseKeys();
            Player by;
            string sub = "";
            if (game.Paused && game.PausedBy != game.MyId && game.Players.TryGetValue(game.PausedBy, out by))
                sub = "Paused by " + by.Name;
            else if (!game.Paused && game.State != MatchState.GameOver && game.State != MatchState.Victory)
                sub = "Pausing…";
            else if (game.Players.Count > 1)
                sub = "Nobody moves until someone resumes";
            else
                sub = "The arena is waiting";
            cabBootLog = "> LINK OK   > SAVE OK   > WAVE " + Mathf.Max(1, game.Wave);
            BeginDrawer("PAUSED", Ui.Accent, 1f, sub);
            string[] lines = { "Back into wave " + Mathf.Max(1, game.Wave), "Your build, damage and kills so far", "Every enemy, weapon and upgrade", "Your rank, records and unlocks", "Controls and tips",
                "Audio, visual effects, aim line", CanQuitToLobby ? "Back to the lobby, or out to the desktop" : "Out to the desktop" };
            Color[] pcols = { Ui.Primary, Ui.Gold, FieldGuideColor, Ui.Gold, HowToColor, new Color(0.6f, 0.66f, 0.82f), new Color(1f, 0.6f, 0.45f) };
            string[] picons = { "ui_play", "ui_trophy", "ui_book", "ui_star", "ui_help", "ui_gear", "ui_power" };
            var rows = PauseRows();
            pauseSel = Mathf.Clamp(pauseSel, 0, rows.Count - 1);
            float y = cabScreen.y + 10f;
            for (int k = 0; k < rows.Count; k++)
            {
                int i = rows[k];
                var r = new Rect(34f, y, CabRowW, 58f);
                if (cabScreenOn && r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint && Event.current.mousePosition != pauseMouse)
                    pauseSel = k;  // the mouse moved onto it (the keys move it otherwise)
                if (DrawerRow(r, PixelArt.Get(picons[i]), PauseChoices[i], lines[i], i == 0 ? "Esc" : null, null, pcols[i], k == pauseSel, null, i == PauseQuit))
                    PauseChoose(i);
                y += 62f;
            }
            if (Event.current.type == EventType.Repaint)
                pauseMouse = Event.current.mousePosition;
            EndDrawerScreen();
            DrawRunStatsKey();
            CabFoot("ESC RESUME / ARROWS SELECT / ENTER CHOOSE", "HOLD TAB RUN STATS & BUILD   H HIDE HUD");
            EndDrawer();
            DrawTip(true);
            DrawRunScreen(t);
        }

        /// The small square key in the pause box's header, lined up with the right edge of the rows: three statistic bars, "Current run
        /// stats" on hover. Opens the Run stats screen. Call inside the drawer (cabinet coordinates).
        void DrawRunStatsKey()
        {
            TipClear();
            if (!cabScreenOn)
                return;
            var r = new Rect(34f + CabRowW - 36f, 52f, 36f, 36f);
            bool hover = r.Contains(Event.current.mousePosition);
            Ui.NoteHover(r);
            Color c = CrtStyle.KeyColor(CrtStyle.Gold);
            CrtKit.SoftBox(r, c, false, hover, false);
            Color tc = CrtKit.SoftText(c, hover);
            float[] hs = { 0.45f, 0.8f, 0.6f };
            for (int i = 0; i < 3; i++)
            {
                float bh = 20f * hs[i];
                Ui.Fill(new Rect(r.x + 8f + i * 8f, r.yMax - 8f - bh, 6f, bh), tc);
            }
            if (hover)
                TipStart(new Rect(CabX + 1f + DrawerSlide + r.x, CabY + 1f + r.y, r.width, r.height), "Current run stats", Ui.Gold);
            if (Ui.HitArea(r))
                runStatsOpen = true;
        }

        /// The run's screen on the right, fading as a window comes out over it.
        void DrawRunScreen(float t)
        {
            float a = 1f - CabWinSlideK();
            if (a <= 0.01f)
                return;
            if (a < 0.99f)
                PushShift(0f, 0f, a);
            DrawRunCard(DrawerPanel, t, a >= 0.99f);  // in the window's place, right of the box
            PopShift();
        }

        static readonly Color LobbyConfirmColor = new Color(1f, 0.6f, 0.45f);

        /// The pause menu's Quit, asking first: to the lobby (the host's: everyone beams out, Recall.cs), to the desktop, or
        /// keep playing. ↑ ↓ and Enter, or Esc for no. The run's screen stays on the right.
        void DrawLobbyConfirm(float t)
        {
            DrawerIn("pause", false);
            var e = Event.current;
            bool lobby = CanQuitToLobby;
            var names = new List<string>(); var lines = new List<string>(); var ids = new List<int>();
            bool many = game.Players.Count > 1, saved = game.RunId != null && game.Practice == null;
            names.Add("KEEP PLAYING"); lines.Add("Back to the pause menu"); ids.Add(2);
            if (lobby)
            {
                names.Add("TO LOBBY"); lines.Add(many ? "Everyone beams out to the lobby" : "Beam out to the lobby"); ids.Add(0);
            }
            names.Add("TO DESKTOP"); lines.Add(many && session.IsHost ? "Close the game (the party leaves)" : "Close the game"); ids.Add(1);
            int n = names.Count;
            confirmSel = Mathf.Clamp(confirmSel, 0, n - 1);
            if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == 0)
            {
                if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W)
                {
                    confirmSel = (confirmSel + n - 1) % n;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S)
                {
                    confirmSel = (confirmSel + 1) % n;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    ConfirmQuit(ids[confirmSel]);
                    e.Use();
                }
            }
            cabBootLog = "> LINK OK   > SAVE OK   > WAVE " + Mathf.Max(1, game.Wave);
            BeginDrawer("QUIT?", LobbyConfirmColor, 1f, lobby ? "To the lobby, or out to the desktop" : "Out to the desktop");
            float y = cabScreen.y + 10f;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(34f, y, CabRowW, 58f);
                if (cabScreenOn && r.Contains(e.mousePosition) && e.type == EventType.Repaint && e.mousePosition != pauseMouse)
                    confirmSel = i;
                if (DrawerRow(r, null, names[i], lines[i], ids[i] == 2 ? "Esc" : null, null, Ui.Accent, i == confirmSel, null, ids[i] == 1))
                    ConfirmQuit(ids[i]);
                y += 62f;
            }
            if (e.type == EventType.Repaint)
                pauseMouse = e.mousePosition;
            y += 10f;
            string note = lobby ? (saved ? "To lobby: the run is saved from the last break between waves; pick it up again with Continue a run on the main menu."
                : "To lobby: this run isn't saved, so it can't be continued.") : "This run can't be continued once you quit.";
            if (lobby && saved)
                note += " Quitting to the desktop saves it the same way.";
            if (cabScreenOn)
            {
                float nw = cabScreen.width - 52f, nh = Ui.TextHeight(note, 12, nw);
                Ui.Fill(new Rect(34f, y, 2f, nh + 4f), new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.7f));
                Ui.Label(new Rect(46f, y, nw, nh + 4f), note, 12, CabSoft, TextAnchor.UpperLeft, false, true);
            }
            EndDrawerScreen();
            CabFoot("ESC KEEP PLAYING / ARROWS SELECT / ENTER CHOOSE");
            EndDrawer();
            DrawRunScreen(t);
        }

        void ConfirmQuit(int choice)
        {
            pauseConfirmLobby = false;
            if (choice == 0)
                RecallToLobby();  // everyone beams out, then the host takes them back (Recall.cs)
            else if (choice == 1)
            {
                pauseMenuOpen = false;
                session.Leave("");
                Application.Quit();
            }
        }

        /// ↑ ↓ to move through the pause menu, Enter to choose.
        void PauseKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0)
                return;
            int n = PauseRows().Count;
            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W)
                pauseSel = (pauseSel + n - 1) % n;
            else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S)
                pauseSel = (pauseSel + 1) % n;
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                PauseChoose(PauseRows()[Mathf.Clamp(pauseSel, 0, n - 1)]);
            else
                return;
            e.Use();
        }

        /// Esc in the pause or lobby menu's Settings, How to play or profile: an open window slides back in first, then the
        /// cabinet returns to its list.
        void MenuBack()
        {
            if (pauseRecords)
            {
                if (CabWindowOpen)
                    CloseCabWindow();  // the profile's window first
                else
                {
                    pauseRecords = false;
                    if (profileFromCard)
                        CloseLobbyMenu();  // opened from your party card: straight back to the lobby
                    profileFromCard = false;
                }
                return;
            }
            if (CabWindowOpen)
            {
                CloseCabWindow();
                return;
            }
            pauseSettings = pauseHowTo = false;
        }

        void PauseChoose(int i)
        {
            switch (i)
            {
                case 0: ResumeGame(); break;
                case 1: runStatsOpen = true; break;
                case 2: OpenFieldGuide(); break;
                case 3: pauseRecords = true; break;
                case 4: pauseHowTo = true; OpenCabWindow(2); break;  // a window beside the list
                case 5: pauseSettings = true; CloseCabWindow(); break;  // the list switches to the sections
                default:  // PauseQuit
                    pauseConfirmLobby = true;  // asks first (DrawLobbyConfirm)
                    confirmSel = 0;            // "keep playing" is first, so a stray Enter doesn't end the run
                    break;
            }
        }

        /// The run's card over the arena: the wave, time and score, the path, modifiers and difficulty, the next boss
        /// (moving) and this stretch's challenge room.
        void DrawRunCard(Rect at, float t, bool gl = true)
        {
            // The chips: the path, the modifiers, the difficulty.
            var chips = new List<KeyValuePair<string, Color>>();
            if (game.CurrentPath != PathId.None)
            {
                var pd = Paths.Get(game.CurrentPath);
                int left = game.PathWavesLeft;
                chips.Add(new KeyValuePair<string, Color>(pd.Name.ToUpperInvariant() + (left > 0 ? "  ·  " + left + (left == 1 ? " WAVE LEFT" : " WAVES LEFT") : ""), ToColor(pd.Accent)));
            }
            foreach (var m in Modifiers.All)
                if (game.Has(m.Mod))
                    chips.Add(new KeyValuePair<string, Color>(m.Name.ToUpperInvariant(), m.Lean == ModLean.Easier ? Ui.Good : new Color(1f, 0.6f, 0.6f)));
            chips.Add(new KeyValuePair<string, Color>(game.Diff.Name.ToUpperInvariant(), new Color(0.79f, 0.83f, 0.92f)));
            var r = at;
            float powerAge = CabAge - 1.3f;  // the box fades in after the cabinet's opening
            if (!CabCalm && powerAge < 0f)
                return;
            CrtKit.LoadoutBox(r);
            DrawPartyStack(r);  // the players, stacked up from its bottom
            float x = r.x + 20f, y = r.y + 16f;
            Ui.Label(new Rect(x, y, 200f, 14f), game.Practice != null ? "PRACTICE RANGE" : "THIS RUN", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            y += 18f;
            string wave = game.Practice != null ? "WAVE " + Mathf.Max(1, game.PracticeIndex + 1) : "WAVE " + Mathf.Max(1, game.Wave);
            Ui.Fancy(new Rect(x, y, 260f, 48f), wave, 38, Color.white, Ui.Gold, TextAnchor.MiddleLeft, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f));
            float ww = Ui.TextWidth(wave, 38, true) + 16f;
            int secs = Mathf.RoundToInt(game.RunTime);
            Ui.Label(new Rect(x + ww, y + 6f, 140f, 18f), secs / 60 + ":" + (secs % 60).ToString("00"), 13, Ui.Text);
            Ui.Label(new Rect(x + ww, y + 24f, 160f, 18f), "Score " + game.Score.ToString("N0"), 13, Ui.Text);
            y += 56f;
            // Chips: the path, the modifiers, the difficulty.
            float cx = x, maxX = r.xMax - 20f;
            foreach (var c in chips)
            {
                float w = Ui.TextWidth(c.Key, 11, true) + 18f;
                if (cx + w > maxX && cx > x)
                {
                    cx = x;
                    y += 26f;
                }
                cx += Chip(cx, y, c.Key, c.Value) + 6f;
            }
            y += 34f;
            Ui.Fill(new Rect(x, y, r.width - 40f, 1f), new Color(1f, 1f, 1f, 0.08f));
            y += 14f;
            // The next boss, moving in its tile.
            int next = game.NextBossWave(game.Wave, game.State == MatchState.Fighting);
            var boss = game.BossAt(Mathf.Min(Game.MaxWave, Mathf.Max(5, next)));
            var tile = new Rect(x, y, 64f, 64f);
            IconTile(tile, null, Ui.Gold, 0f);
            bool fighting = game.IsBossWave(game.Wave) && game.State == MatchState.Fighting;
            Ui.Label(new Rect(x + 78f, y + 4f, 300f, 14f), (fighting ? "BOSS · NOW" : "NEXT BOSS · WAVE " + next), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Fancy(new Rect(x + 78f, y + 18f, 300f, 28f), PracticePlan.NameOf(boss == EnemyType.TwinB ? EnemyType.TwinA : boss), 20, Color.white, new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleLeft);
            if (!fighting && game.Practice == null)
            {
                int to = next - game.Wave - 1;
                Ui.Label(new Rect(x + 78f, y + 46f, 320f, 16f), to <= 0 ? "Next up" : to + (to == 1 ? " wave" : " waves") + " before it", 12, Ui.Muted);
            }
            var bossAt = new V2(tile.center.x, tile.center.y);
            y += 78f;
            // This stretch's challenge room.
            var room = new Rect(x, y, 64f, 64f);
            var rc = lastRoomKind != ChallengeKind.None && (lastRoomWave - 1) / 5 == (Mathf.Max(1, game.Wave) - 1) / 5 ? lastRoomKind : ChallengeKind.None;
            IconTile(room, PixelArt.Get(RoomIcon(rc)), rc != ChallengeKind.None ? ToColor(Challenges.Accent(rc)) : new Color(0.7f, 0.6f, 1f), 36f);
            Ui.Label(new Rect(x + 78f, y + 4f, 320f, 14f), "DATA GATE · THIS STRETCH", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            int block = (Mathf.Max(1, game.Wave) - 1) / 5;
            string nextPortal = "Next Data Gate after wave " + (block * 5 + 6) + ", " + (block * 5 + 7) + " or " + (block * 5 + 8);
            if (rc != ChallengeKind.None)
            {
                Ui.Fancy(new Rect(x + 78f, y + 18f, 320f, 28f), Challenges.Name(rc) + (lastRoomWon ? "  ✓" : "  ✗"), 19, Color.white, lastRoomWon ? ToColor(Challenges.Accent(rc)) : new Color(1f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
                Ui.Label(new Rect(x + 78f, y + 46f, 340f, 16f), (lastRoomWon ? "Won" : "Lost") + "  ·  " + nextPortal, 12, Ui.Muted);
            }
            else if (game.Room == RoomPhase.Portal || game.InRoom)
            {
                Ui.Fancy(new Rect(x + 78f, y + 18f, 320f, 28f), Challenges.Name(game.RoomKind), 19, Color.white, ToColor(Challenges.Accent(game.RoomKind)), TextAnchor.MiddleLeft);
                Ui.Label(new Rect(x + 78f, y + 46f, 340f, 16f), game.InRoom ? "You're in it now" : "Its Data Gate is open", 12, Ui.Muted);
            }
            else
            {
                int k = game.Hunt != null ? 0 : Mathf.Max(1, game.Wave) % 5;  // (no Data Gates in a Boss Hunt)
                Ui.Fancy(new Rect(x + 78f, y + 18f, 320f, 28f), "Not yet", 19, Color.white, new Color(0.75f, 0.68f, 1f), TextAnchor.MiddleLeft);
                Ui.Label(new Rect(x + 78f, y + 46f, 340f, 16f), game.Hunt != null ? "No Data Gates on a Boss Hunt" : k >= 1 && k <= 3 && game.Practice == null ? "A Data Gate opens after one of the next few waves" : nextPortal, 12, Ui.Muted);
            }
            if (gl && BeginUiGl())
            {
                UiClip(tile);
                DrawEnemyPreview(boss == EnemyType.TwinB ? EnemyType.TwinA : boss, bossAt, 52f, t, true);
                EndUiGl();
            }
        }

        static string RoomIcon(ChallengeKind k) =>
            k == ChallengeKind.Defend ? "shield" : k == ChallengeKind.MiniBoss ? "ui_skull" : k == ChallengeKind.KillRush ? "bolt"
            : k == ChallengeKind.HoldZones ? "pulse" : k == ChallengeKind.Mirror ? "mirror" : "blink";

        // --- Settings ---------------------------------------------------------------------------------------------------

        static readonly string[] SettingsGroups = { "Audio", "Visual effects", "Gameplay", "Keybinds" };

        /// Settings from the main menu or the lobby's button: the cabinet with its four sections, each opening a window.
        /// (The pause and lobby menus switch their own cabinet to the same list, DrawSettingsCabinet.)
        void DrawSettingsWindow()
        {
            if (bgBig && settingsGroup == 1 && cabWin)
            {
                DrawBackgroundBig();  // MenuBackgrounds.cs: only the background and its controls
                return;
            }
            bgBig = false;
            DrawerIn("settings", true);
            settingsGroup = Mathf.Clamp(settingsGroup, 0, SettingsGroups.Length - 1);
            DrawCabWindow();  // (first: it comes out from behind the cabinet)
            DrawSettingsCabinet(false);
        }

        /// The cabinet's list as Audio / Visual effects / Gameplay / Keybinds with a physical Back key. `paused`: in the pause or
        /// lobby menu, where Back returns to that menu's list (the text retypes); else it slides the whole cabinet out.
        void DrawSettingsCabinet(bool paused)
        {
            cabBootLog = paused ? "> LINK OK   > SAVE OK   > SETTINGS" : "> LINK OK   > SAVE OK";
            BeginDrawer("SETTINGS", Ui.Accent, 1f, "Pick one to open it");
            string[] lines = { "Volume for each kind of sound", "What the fight looks like", "Aim line, the HUD, the network port", "Change your controls" };
            string[] gicons = { "pulse", "nova", "crosshair", "ui_pencil" };
            Color[] gcols = { new Color(0.5f, 0.69f, 1f), Ui.Gold, new Color(0.55f, 1f, 0.55f), new Color(1f, 0.62f, 0.5f) };
            float y = cabScreen.y + 10f;
            for (int i = 0; i < SettingsGroups.Length; i++)
            {
                bool on = cabWin && cabKind == 1 && cabWinOutAt < 0f && settingsGroup == i;
                if (DrawerRow(new Rect(34f, y, CabRowW, 76f), PixelArt.Get(gicons[i]), SettingsGroups[i], lines[i], null, null, gcols[i], on))
                {
                    settingsGroup = i;
                    OpenCabWindow(1);  // (the first pick slides it out; later ones only retype it)
                }
                y += 80f;
            }
            EndDrawerScreen();
            if (CabBackKey(y + 6f))
            {
                Keybinds.Listening = -1;
                if (paused)
                {
                    pauseSettings = false;  // the list retypes to the menu's
                    CloseCabWindow();
                }
                else
                    CloseWindow(() => settingsOpen = false);  // it slides out first (WindowAnim.cs)
            }
            EndDrawer();
        }

        /// The open Settings section, in the window.
        void DrawSettingsSection(float t)
        {
            settingsGroup = Mathf.Clamp(settingsGroup, 0, SettingsGroups.Length - 1);
            int group = PanelShown(ref settingsShown, settingsGroup);  // (the old section erases first, CrtWindows.cs)
            if (group != 3)
                Keybinds.Listening = -1;  // (leaving the page ends a rebind)
            var col = CrtUi.Ink;
            BeginPanelAnim("settings" + group);
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, y = DrawerPanel.y + 102f;
            switch (group)
            {
                case 0:
                {
                    OpenSectionPanel("AUDIO", col, audioTab == 0 ? "Each kind of sound has its own volume, under the master." : "Pick what plays, in the menus and in a match.", 1f);
                    string[] audioTabs = { "VOLUME", "MUSIC PLAYER" };
                    float tx = DrawerPanel.xMax - 28f - 46f - 14f - 290f;
                    for (int ti = 0; ti < 2; ti++)
                        if (WinKey(new Rect(tx + ti * 136f, DrawerPanel.y + 18f, ti == 0 ? 130f : 154f, 34f), audioTabs[ti], audioTab == ti, 16))
                        {
                            audioTab = ti;
                            jukeScroll = 0f;
                            if (ti == 1)
                                ScanCustomMusic();  // picks up files added since last time
                        }
                    if (audioTab == 1)
                    {
                        DrawMusicPlayer(t);
                        break;
                    }
                    bool changed = false;
                    changed |= VolumeSlider(x, ref y, w, "Master", ref sounds.Master, col);
                    changed |= VolumeSlider(x, ref y, w, "Music", ref sounds.Music, col);
                    changed |= VolumeSlider(x, ref y, w, "Weapons  (your shots and spells)", ref sounds.Weapons, col);
                    changed |= VolumeSlider(x, ref y, w, "Enemies", ref sounds.Enemies, col);
                    changed |= VolumeSlider(x, ref y, w, "Effects  (explosions, abilities, the menus)", ref sounds.Effects, col);
                    bool m2 = SwitchRow(new Rect(x, y + 4f, w, 54f), sounds.Muted, "Mute all sound", "Everything off at once; the volumes are kept", Keybinds.KeyName(Keybinds.Get(KeyAction.Mute)));
                    if (m2 != sounds.Muted)
                    {
                        sounds.Muted = m2;
                        changed = true;
                    }
                    if (changed)
                        SaveAudioSettings();
                    break;
                }
                case 1:
                    DrawFxSection(col, t);
                    break;
                case 3:
                    DrawKeybindsSection(col, t, 1f);
                    break;
                default:
                {
                    OpenSectionPanel("GAMEPLAY", col, "Aim, the HUD, the view and the network", 1f);
                    bool l2 = SwitchRow(new Rect(x, y, w, 54f), aimLine, "Aim line", "A line from you toward the cursor, as long as your weapon reaches", Keybinds.KeyName(Keybinds.Get(KeyAction.AimLine)));
                    if (l2 != aimLine)
                        SetAimLine(l2);
                    y += 66f;
                    WinLabel(new Rect(x, y, w, 24f), "HUD", "H cycles it; a slim boss bar stays when it's hidden");
                    int hudPick = WinChoices(new Rect(x, y + 46f, w, 34f), HudModeNames, hudMode);  // (under the line, not over it)
                    if (hudPick != hudMode)
                        SetHudMode(hudPick);
                    DashLine(x, y + 88f, w);
                    y += 96f;
                    float halfW = (w - 36f) / 2f;  // (camera lean and zoom share a row, to leave room for the lag chart's)
                    bool lean2 = SwitchRow(new Rect(x, y, halfW, 54f), camLean, "Camera lean", "The view eases toward where you aim and move");
                    if (lean2 != camLean)
                        SetCamLean(lean2);
                    bool zoom2 = SwitchRow(new Rect(x + halfW + 36f, y, halfW, 54f), scrollZoom, "Scroll-wheel zoom", "Scroll up to zoom in on yourself (up to 2x)");
                    if (zoom2 != scrollZoom)
                        SetCamZoom(zoom2);
                    y += 66f;
                    bool hover2 = SwitchRow(new Rect(x, y, w, 54f), hoverNames, "Enemy names on hover", "Point at an enemy to see its name and what it does (the first sightings show it anyway)");
                    if (hover2 != hoverNames)
                        SetHoverNames(hover2);
                    y += 66f;
                    y += DrawLagSettingsRow(x, y, w);  // LagChartUi.cs
                    WinLabel(new Rect(x, y + 6f, w - 396f, 48f), "Teammate shots", "How solid other players' bullets look (25% at least)");
                    float mate = Mathf.Max(0.25f, WinSlider(new Rect(x + w - 376f, y + 6f, 376f, 48f), mateShotAlpha));
                    if (Mathf.Abs(mate - mateShotAlpha) > 0.0001f)
                        SetMateShotAlpha(mate);
                    DashLine(x, y + 60f, w);
                    y += 64f;
                    // The network port (it used to sit on the main menu): hosting and joining both use it.
                    WinLabel(new Rect(x, y, w - 150f, 54f), "Network port", "Hosting and joining use it (" + Session.DefaultPort + " unless changed). Everyone in a game needs the same one.");
                    var pr = new Rect(x + w - 120f, y + 9f, 120f, 36f);
                    Ui.Fill(pr, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.08f));
                    CrtKit.Frame(pr, 2f, CrtUi.Ink);
                    string port = Ui.BareField(new Rect(pr.x + 8f, pr.y, pr.width - 16f, pr.height), portText, 5, 22);
                    if (port != portText)
                    {
                        portText = port;
                        SaveMenuFields();
                    }
                    DashLine(x, y + 54f, w);
                    break;
                }
            }
            EndPanelAnim();
        }

        /// Visual effects: a live preview, then the switches and the screen shake choice.
        void DrawFxSection(Color col, float t)
        {
            OpenSectionPanel("VISUAL EFFECTS", col, "What the fight looks like", 1f);
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, y = DrawerPanel.y + 102f;
            DrawFxPreview(new Rect(x, y, w, 128f), t);
            y += 142f;
            float half = (w - 36f) / 2f;
            bool changed = false;
            bool on = SwitchRow(new Rect(x, y, half, 54f), fxOn, "Visual effects", "Glow, sparks, shattering enemies, lighting");
            if (on != fxOn) { fxOn = on; changed = true; }
            bool nums = SwitchRow(new Rect(x + half + 36f, y, half, 54f), fxNumbers, "Damage numbers", "Small numbers rising off what's hit");
            if (nums != fxNumbers) { fxNumbers = nums; changed = true; }
            y += 64f;
            WinLabel(new Rect(x, y, w, 24f), "Screen shake", null, !fxOn);
            int shake = WinChoices(new Rect(x, y + 28f, w, 34f), new[] { "Off", "Low", "Full" }, fxShake, !fxOn);
            if (shake != fxShake) { fxShake = shake; changed = true; }
            y += 74f;
            bool names = SwitchRow(new Rect(x, y, half, 54f), fxCallouts, "Ability names", "Each ability's name over whoever casts it");
            if (names != fxCallouts) { fxCallouts = names; changed = true; }
            bool calm = SwitchRow(new Rect(x + half + 36f, y, half, 54f), fxReduced, "Reduced effects", "No screen shake, hit-pause or white flashes");
            if (calm != fxReduced) { fxReduced = calm; changed = true; }
            DrawBackgroundRow(new Rect(x, y + 70f, w, 92f));  // MenuBackgrounds.cs
            float cy = y + 172f;
            WinLabel(new Rect(x, cy, w, 24f), "CRT colour", null, false);
            int theme = WinChoices(new Rect(x, cy + 26f, w, 34f), CrtUi.ThemeNames, CrtUi.Theme);
            if (theme != CrtUi.Theme) { CrtUi.Theme = theme; changed = true; }
            if (changed)
                SaveFxSettings();
        }

        /// A tiny fight that follows the visual settings: shots into a target (it flashes and shows numbers), an
        /// ability's name now and then, a blast that shakes the box.
        void DrawFxPreview(Rect r, float t)
        {
            Ui.Fill(r, CrtUi.T(new Color(0.01f, 0.02f, 0.05f, 1f)));
            CrtKit.Frame(r, 1f, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.35f));
            float cyc = MathX.Fmod(t, 3f), boom = Mathf.Max(0f, 1f - Mathf.Abs(cyc - 2.2f) * 4f);
            bool shake = fxOn && !fxReduced && fxShake > 0;
            float amp = shake ? (fxShake == 2 ? 6f : 3f) * boom : 0f;
            var off = new V2(Mathf.Sin(t * 80f) * amp, Mathf.Cos(t * 70f) * amp);
            V2 me = new V2(r.x + 130f, r.y + 100f) + off, foe = new V2(r.x + 500f, r.y + 80f) + off, blast = new V2(r.x + 610f, r.y + 120f) + off;
            float shotK = MathX.Fmod(t * 2.2f, 1f);
            bool hit = shotK < 0.12f;
            if (BeginUiGl())
            {
                UiClip(r);
                UiModelAt(V2.Zero, 1f);
                for (float gx = r.x; gx < r.xMax; gx += 30f)
                    Line(new V2(gx, r.y), new V2(gx, r.yMax), 1f, new Rgba(0.43f, 0.55f, 0.86f, 0.1f));
                for (float gy = r.y; gy < r.yMax; gy += 30f)
                    Line(new V2(r.x, gy), new V2(r.xMax, gy), 1f, new Rgba(0.43f, 0.55f, 0.86f, 0.1f));
                for (int i = 0; i < 4; i++)
                {
                    float f = MathX.Fmod(t * 2.2f + i / 4f, 1f);
                    V2 at = me + (foe - me) * f;
                    Circle(at, 4.5f, new Rgba(0.55f, 0.75f, 1f), 10);
                }
                Circle(me, 16f, new Rgba(0.07f, 0.09f, 0.15f), 24);
                Arc(me, 16f, 3f, new Rgba(0.36f, 0.6f, 1f), 24);
                Polygon(foe, 20f, 3, -MathX.Pi / 2f, fxOn && hit ? Rgba.White : new Rgba(1f, 0.35f, 0.35f));
                if (fxOn && hit)
                    for (int s = 0; s < 6; s++)
                        Line(foe, foe + V2.FromAngle(s * 1.05f + t) * 22f, 2f, new Rgba(1f, 0.85f, 0.5f, 0.8f));
                if (boom > 0f)
                    Arc(blast, 60f * (1.2f - boom), 4f, new Rgba(1f, 0.67f, 0.31f, boom), 32);
                if (fxOn)
                {
                    // The real glow, as in the arena (Juice.DrawGlowPass): soft discs of added light, not flat circles.
                    SwitchToGlow(true);
                    for (int i = 0; i < 4; i++)
                        GlowDisc(me + (foe - me) * MathX.Fmod(t * 2.2f + i / 4f, 1f), 14f, new Rgba(0.55f, 0.75f, 1f), 0.47f);
                    GlowDisc(me, 16f * 2.6f, new Rgba(0.36f, 0.6f, 1f), 0.18f);
                    if (hit)
                        GlowDisc(foe, 40f, Rgba.White, 0.5f);
                    if (boom > 0f)
                        GlowDisc(blast, 40f, new Rgba(1f, 0.8f, 0.4f), 0.55f * boom);
                    SwitchToGlow(false);
                }
                EndUiGl();
            }
            if (fxNumbers)
                for (int i = 0; i < 3; i++)
                {
                    float f = (shotK + i) / 3f;
                    Ui.Shadowed(new Rect(foe.X + 6f + i * 8f, foe.Y - 34f - f * 40f, 40f, 18f), (12 + i * 3).ToString(), 15, new Color(1f, 0.88f, 0.54f, 1f - f));
                }
            if (fxCallouts && MathX.Fmod(t, 4f) < 1.4f)
                Ui.Shadowed(new Rect(me.X - 70f, me.Y - 48f, 140f, 18f), "Shockwave", 13, new Color(0.6f, 0.85f, 1f));
            Ui.Label(new Rect(r.x + 14f, r.yMax - 26f, r.width - 28f, 18f), (fxOn ? "Effects on" : "Effects off") + "  ·  shake " + (shake ? (fxShake == 2 ? "full" : "low") : "off")
                + (fxNumbers ? "  ·  numbers" : "") + (fxCallouts ? "  ·  ability names" : ""), 11, Ui.Muted, TextAnchor.MiddleLeft, true);
        }

        // --- Records -----------------------------------------------------------------------------------------------------

        // The Records pages are the Records tab of the profile (ProfileUi.cs): `sec` 0 Overview, 1 Bosses, 2 Classes, 3 Challenge rooms, 6 Recent runs.

        void DrawRecordsSection(int sec, Profile p, float t, int bossesBeaten, int roomsWon, Color[] cols)
        {
            float k = 1f;

            var col = cols[sec];
            float x = DrawerPanel.x + 26f, w = DrawerPanel.width - 52f;
            switch (sec)
            {
                case 0:
                {
                    OpenSectionPanel("OVERVIEW", col, "Practice and dev-tool runs do not count.", k);
                    var span = TimeSpan.FromSeconds(p.PlayTime);
                    string played = span.TotalHours >= 1 ? (int)span.TotalHours + " h " + span.Minutes + " m" : (int)(p.PlayTime / 60f) + " min";
                    string[,] tiles = {
                        { "Runs started", p.RunsStarted.ToString() }, { "Won", p.RunsWon.ToString() }, { "Lost", p.RunsLost.ToString() }, { "Time played", played },
                        { "Best wave", p.BestWave > 0 ? p.BestWave.ToString() : "—" }, { "Best endless", p.BestEndlessWave > 0 ? p.BestEndlessWave.ToString() : "—" },
                        { "Best score", p.BestScore > 0 ? p.BestScore.ToString("N0") : "—" }, { "Data Gates", roomsWon + " won" },
                        { "Enemies killed", p.Kills.ToString("N0") }, { "Damage dealt", ShortNumber(p.DamageDealt) }, { "Healing done", ShortNumber(p.HealingDone) }, { "Allies revived", p.Revives.ToString() } };
                    float tw = (w - 30f) / 4f, th = 78f;
                    for (int i = 0; i < tiles.GetLength(0); i++)
                    {
                        var tr = new Rect(x + (i % 4) * (tw + 10f), DrawerPanel.y + 104f + (i / 4) * (th + 12f), tw, th);
                        CabCard(tr, 0.035f);
                        Ui.Fancy(new Rect(tr.x + 14f, tr.y + 10f, tr.width - 20f, 34f), tiles[i, 1], 24, Color.white, new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleLeft);
                        Ui.Label(new Rect(tr.x + 14f, tr.y + 48f, tr.width - 20f, 16f), tiles[i, 0].ToUpperInvariant(), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
                    }
                    Ui.Label(new Rect(x, DrawerPanel.y + 104f + 3 * (th + 12f) + 8f, w, 18f), "Times downed: " + p.Downs + "  ·  boss kills: " + p.BossKills, 12, Ui.Muted);
                    break;
                }
                case 1:
                {
                    OpenSectionPanel("BOSSES", col, bossesBeaten + " of " + RecordBosses.Length + " beaten at least once", k);
                    float cw = (w - 16f) / 2f, rh = 98f;
                    var spots = new List<KeyValuePair<EnemyType, V2>>();
                    for (int i = 0; i < RecordBosses.Length; i++)
                    {
                        var b = RecordBosses[i];
                        int n = p.Beaten(b);
                        var rr = new Rect(x + (i % 2) * (cw + 16f), DrawerPanel.y + 104f + (i / 2) * (rh * 0.53f + 4f), cw, rh * 0.53f);
                        CabCard(rr, n > 0 ? 0.045f : 0.02f);
                        Ui.Label(new Rect(rr.x + 58f, rr.y, rr.width - 120f, rr.height), BossName(b), 15, n > 0 ? Ui.Text : Ui.Muted, TextAnchor.MiddleLeft, true);
                        Ui.Label(new Rect(rr.xMax - 90f, rr.y, 76f, rr.height), n > 0 ? "× " + n : "—", 15, n > 0 ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight, true);
                        spots.Add(new KeyValuePair<EnemyType, V2>(b, new V2(rr.x + 28f, rr.center.y)));
                    }
                    if (BeginUiGl())
                    {
                        foreach (var s in spots)
                            DrawEnemyPreview(s.Key, s.Value, 34f, t, false);
                        EndUiGl();
                    }
                    break;
                }
                case 2:
                {
                    OpenSectionPanel("CLASSES", col, "Wins out of runs finished, for each class you've played", k);
                    var classes = new List<KeyValuePair<string, int>>(p.RunsAs);
                    classes.Sort((a, b) => b.Value.CompareTo(a.Value));
                    if (classes.Count == 0)
                        Ui.Label(new Rect(x, DrawerPanel.y + 110f, w, 20f), "Finish a run to see your classes here.", 14, Ui.Muted);
                    for (int i = 0; i < classes.Count && i < 8; i++)
                    {
                        int wins;
                        p.WinsAs.TryGetValue(classes[i].Key, out wins);
                        var rr = new Rect(x, DrawerPanel.y + 104f + i * 62f, w, 54f);
                        CabCard(rr, 0.035f);
                        string kit = classes[i].Key.Split(' ')[0].ToLowerInvariant();
                        IconTile(new Rect(rr.x + 6f, rr.y + 6f, 42f, 42f), PixelArt.Get("class_" + kit), col, 30f);
                        Ui.Label(new Rect(rr.x + 62f, rr.y + 6f, 300f, 22f), classes[i].Key, 15, Ui.Text, TextAnchor.MiddleLeft, true);
                        Ui.Bar(new Rect(rr.x + 62f, rr.y + 34f, rr.width - 170f, 7f), classes[i].Value > 0 ? (float)wins / classes[i].Value : 0f, wins > 0 ? Ui.Gold : col, new Color(0f, 0f, 0f, 0.5f));
                        Ui.Label(new Rect(rr.xMax - 100f, rr.y, 86f, rr.height), wins + " of " + classes[i].Value, 15, wins > 0 ? Ui.Gold : Ui.Text, TextAnchor.MiddleRight, true);
                    }
                    break;
                }
                case 3:
                {
                    int played = 0;
                    foreach (var c in Challenges.All)
                        played += p.RoomPlayed(c);
                    OpenSectionPanel("DATA GATES", col, played > 0 ? roomsWon + " of " + played + " won" : "A Data Gate opens once every five waves. Win its challenge for a prize pick.", k);
                    for (int i = 0; i < Challenges.All.Length; i++)
                    {
                        var c = Challenges.All[i];
                        int won = p.RoomWon(c), pl = p.RoomPlayed(c);
                        var cc = ToColor(Challenges.Accent(c));
                        var rr = new Rect(x, DrawerPanel.y + 104f + i * 86f, w, 76f);
                        CabCard(rr, 0.035f);
                        IconTile(new Rect(rr.x + 12f, rr.y + 12f, 52f, 52f), PixelArt.Get(RoomIcon(c)), cc, 34f);
                        Ui.Fancy(new Rect(rr.x + 80f, rr.y + 8f, 340f, 30f), Challenges.Name(c), 20, Color.white, cc, TextAnchor.MiddleLeft);
                        Ui.Label(new Rect(rr.xMax - 140f, rr.y + 8f, 126f, 30f), pl > 0 ? won + " of " + pl : "not yet", 17, pl > 0 ? Ui.Text : Ui.Muted, TextAnchor.MiddleRight, true);
                        Ui.Bar(new Rect(rr.x + 80f, rr.y + 48f, rr.width - 96f, 8f), pl > 0 ? (float)won / pl : 0f, cc, new Color(0f, 0f, 0f, 0.5f));
                    }
                    break;
                }
                default:
                    DrawRecentRuns(p, t, col, k);  // RunGraphs.cs (page 6)
                    break;
            }
        }

        static string ShortNumber(float v) => v >= 1e6f ? (v / 1e6f).ToString("0.0") + " M" : v >= 1e4f ? (v / 1e3f).ToString("0") + " k" : ((long)v).ToString("N0");

        // --- How to play --------------------------------------------------------------------------------------------------

        /// A How to play topic: its tips, each the old drawn glyph (HowToTips index, DrawTipGlyph) or a pixel icon.
        class HowTo
        {
            public string Name, Icon, Intro;
            public Color Color;
            public bool Keys;  // the controls list instead of tips
            public bool Portal;  // the challenge rooms' diagram
            public string[,] Tips;  // title, text, glyph index or "icon:name"
        }

        static readonly HowTo[] HowToTopics =
        {
            new HowTo { Name = "Controls", Icon = "ui_help", Color = new Color(0.6f, 0.75f, 1f), Keys = true, Intro = "Keyboard and mouse. Ability keys show on the bar at the bottom." },
            new HowTo { Name = "The basics", Icon = "crosshair", Color = new Color(1f, 0.55f, 0.5f), Intro = "Survive 50 waves with a boss every fifth one, or take a Boss Hunt: ten waves ending in one boss.",
                Tips = new[,] { { "Hitbox", "Your hitbox is the small ring in the middle of your character, with a coloured dot at its centre. Only the ring's inside can be hit, not the whole body.", "0" }, { "Wrap around", "Walk off an edge to come in on the other side. Enemies and bullets can't follow.", "1" },
                    { "Roles", "Enemies prefer to target the Tank. A Healer's aura heals allies nearby. DPS hits hardest.", "8" }, { "Health orbs", "Kills sometimes drop green orbs that heal you. They drift toward you when you're close.", "9" },
                    { "Dodge", "A tiny dash with a moment of invulnerability. Double tap a movement key or press Ctrl.", "icon:blink" },
                    { "Practice Range", "Pick it in the lobby to play waves and bosses you set up yourself.", "13" } } },
            new HowTo { Name = "Fighting", Icon = "guard_reinforced", Color = new Color(0.55f, 0.8f, 1f), Intro = "Read the shots, parry what you can, and let your abilities grow.",
                Tips = new[,] { { "Perfect parry", "Raise your parry just as a shot arrives: blocked bullets fly back at enemies and cost nothing.", "7" }, { "Boss shots", "Dark-cored shots are a boss's: violet ones wear blockers down, red ones can't be blocked.", "5" },
                    { "Boss bodies", "Touching a boss hurts and shoves you back. Parry up close to get past its fire.", "12" }, { "Status icons", "Snowflake = frozen, arrows = slowed, flame = burning, drop = bleeding, crosshair = marked.", "2" },
                    { "Ability mastery", "Abilities level up from 1 to 5 as you use them in fights. Level 5 unlocks a capstone.", "10" } } },
            new HowTo { Name = "Between waves", Icon = "ui_star", Color = Ui.Gold, Intro = "Each break: pick upgrades, open what the wave left behind, then the next wave.",
                Tips = new[,] { { "Upgrades", "Levels give upgrade picks at the end of each wave; bosses give legendary ones.", "4" }, { "Treasure chests", "Now and then a chest appears: touch it and everyone gets their own item. Stand on it to take (G) or throw away (X) yours.", "icon:ui_coin" },
                    { "Rerolls", "Don't like the cards? Reroll them: you start with 2 and get 2 more for every boss.", "icon:ui_arrow" }, { "Downed", "At 0 HP you're downed: stand by a downed ally for a few seconds (4 s on Normal) to revive them. Clearing a wave brings everyone back.", "3" } } },
            new HowTo { Name = "Data Gates", Icon = "blink", Color = new Color(0.45f, 0.9f, 1f), Portal = true, Intro = "Once every five waves a Data Gate opens in the break. Everyone alive stands in it to go through. Win the challenge for a prize pick; lose it and you're simply sent back.",
                Tips = new[,] { { "Defend the Crystal", "Keep it alive for 40 s. Most enemies go for it.", "icon:shield" }, { "The Gatekeeper", "A mini-boss: slip the ring's gap, step off its charge line.", "icon:ui_skull" },
                    { "Kill Rush", "40 kills in 40 s, weak enemies from every side.", "icon:bolt" }, { "Hold the Zones", "Stand in each zone until it fills. An enemy in it slows you.", "icon:pulse" },
                    { "Mirror Match", "A shadow of each of you, fighting your way. Beat your own.", "icon:mirror" }, { "The prize", "Touch the altar: a rare-or-better pick, sometimes a legendary.", "icon:ui_star" } } },
            new HowTo { Name = "Paths and modifiers", Icon = "snow", Color = new Color(0.65f, 0.92f, 1f), Intro = "Each stretch between bosses can play differently.",
                Tips = new[,] { { "Paths", "After each boss the host picks a path. More skulls: harder waves, better prize. The Bazaar has a shop.", "11" }, { "Modifiers", "Chosen in the lobby: rules for the whole run, like Glass Cannon or Hunted. Some make it easier.", "icon:ui_skull" },
                    { "Difficulty", "Easy to Nightmare, or Custom with every number on a slider. Set in the lobby.", "icon:ui_gear" } } },
            new HowTo { Name = "Playing together", Icon = "link", Color = new Color(0.55f, 1f, 0.6f), Intro = "Up to three players, one hosting.",
                Tips = new[,] { { "Co-op", "1 to 3 players: one hosts, the others join (Tailscale makes this easy).", "6" }, { "Reviving", "Stand by a downed ally to bring them back. Healers and some upgrades do it faster.", "3" },
                    { "Pings and emotes", "Tap the ping key to mark your cursor, hold it for the wheel. Hold the emote key to cheer on your team.", "icon:ui_arrow" },
                    { "Hangout", "Walk around the lobby with your party while you wait.", "icon:ui_star" },
                    { "Pausing", "Anyone can pause: it pauses the match for everyone until someone resumes.", "icon:ui_play" } } },
        };

        static readonly Color HowToColor = new Color(0.3f, 0.45f, 0.75f);  // the How to play button, as on the main menu

        /// The open How to play topic, in the window. From the pause and lobby menus (where the cabinet's list stays) the topics
        /// are keys along the top of the window.
        void DrawHowToBody(float t)
        {
            howToTopic = Mathf.Clamp(howToTopic, 0, HowToTopics.Length - 1);
            int shownTopic = PanelShown(ref howToShown, howToTopic);
            BeginPanelAnim("howto" + shownTopic);
            var topic = HowToTopics[shownTopic];
            OpenSectionPanel(topic.Name.ToUpperInvariant(), topic.Color, topic.Intro, 1f);
            // The topics as keys along the top of the window (the cabinet's list stays on the pause or lobby menu).
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, cx = x, ty = DrawerPanel.y + 98f;
            for (int i = 0; i < HowToTopics.Length; i++)
            {
                string n = HowToTopics[i].Name.ToUpperInvariant();
                float bw = Ui.TextWidth(n, 15, true) + 24f;
                if (cx + bw > x + w && cx > x)
                {
                    cx = x;
                    ty += 38f;
                }
                if (WinKey(new Rect(cx, ty, bw, 32f), n, i == howToTopic, 15))
                    howToTopic = i;
                cx += bw + 6f;
            }
            DrawHowToTopic(HowToTopics[shownTopic], t, ty + 48f);
            EndPanelAnim();
        }

        void DrawHowToTopic(HowTo topic, float t, float top)
        {
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f;
            if (topic.Keys)
            {
                int rows = (HowToKeys.Length + 1) / 2;
                float colW = (w - 36f) / 2f, rowH = Mathf.Min(62f, (DrawerPanel.yMax - 24f - top) / rows);
                var keysW = new float[2];
                for (int i = 0; i < HowToKeys.Length; i++)
                {
                    float kw = 0f;
                    foreach (var key in HowToKeys[i])
                        kw += Mathf.Max(36f, 18f + key.Length * 9f) + 4f;
                    keysW[i / rows] = Mathf.Max(keysW[i / rows], kw);
                }
                for (int i = 0; i < HowToKeys.Length; i++)
                {
                    int c = i / rows;
                    float kx = x + c * (colW + 36f), ky = top + (i % rows) * rowH, cx = kx, lx = kx + keysW[c] + 12f;
                    foreach (var key in HowToKeys[i])
                    {
                        float kw = Mathf.Max(36f, 18f + key.Length * 9f);
                        WinKey(new Rect(cx, ky + 4f, kw, 30f), key.ToUpperInvariant(), false, 14, null, false, true, true);
                        cx += kw + 4f;
                    }
                    string note = HowToActions[i, 1];
                    CrtUi.Text(new Rect(lx, ky, kx + colW - lx, note.Length > 0 ? 20f : 38f), WinText("win.k" + i, HowToActions[i, 0]), 15, CrtUi.Ink, TextAnchor.MiddleLeft, true, CabCalm);
                    if (note.Length > 0)
                        Ui.Label(new Rect(lx, ky + 19f, kx + colW - lx, 30f), WinText("win.n" + i, note), 11, CabSoft, TextAnchor.UpperLeft, false, true);
                    DashLine(kx, ky + rowH - 4f, colW);
                }
                return;
            }
            if (topic.Portal)
            {
                var d = new Rect(x, top, w, 150f);
                DrawPortalDiagram(d, t);
                top += 162f;
            }
            int n = topic.Tips.GetLength(0);
            bool small = topic.Portal;
            int cols = small ? 3 : 2;
            float tw = (w - (cols - 1) * 12f) / cols, th = small ? 108f : 92f;
            var glyphs = new List<KeyValuePair<int, V2>>();
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x + (i % cols) * (tw + 12f), top + (i / cols) * (th + 10f), tw, th);
                CabCard(r, 0.03f);
                string g = topic.Tips[i, 2];
                var tile = new Rect(r.x + 10f, r.y + 10f, 44f, 44f);
                if (g.StartsWith("icon:"))
                    IconTile(tile, PixelArt.Get(g.Substring(5)), topic.Color, 28f);
                else
                {
                    IconTile(tile, null, topic.Color, 0f);
                    glyphs.Add(new KeyValuePair<int, V2>(int.Parse(g), new V2(tile.center.x, tile.center.y)));
                }
                CrtUi.Text(new Rect(r.x + 64f, r.y + 8f, r.width - 74f, 20f), WinText("win.t" + i, topic.Tips[i, 0]), 15, CrtUi.Ink, TextAnchor.MiddleLeft, true, CabCalm);
                Ui.Label(new Rect(r.x + 64f, r.y + 30f, r.width - 74f, r.height - 36f), WinText("win.d" + i, topic.Tips[i, 1]), 12, CabSoft, TextAnchor.UpperLeft, false, true);
            }
            if (glyphs.Count > 0 && BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                foreach (var gl in glyphs)
                    DrawTipGlyph(gl.Key, gl.Value, t);
                EndUiGl();
            }
        }

        /// How to play's challenge rooms: two players walk into a portal and it fills.
        void DrawPortalDiagram(Rect r, float t)
        {
            Ui.Fill(r, CrtUi.T(new Color(0.01f, 0.02f, 0.05f, 1f)));
            CrtKit.Frame(r, 1f, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.35f));
            float k = MathX.Fmod(t, 5f) / 5f, walk = Mathf.Clamp01(k / 0.35f), fill = Mathf.Clamp01((k - 0.35f) / 0.45f);
            V2 portal = new V2(r.x + r.width * 0.62f, r.center.y);
            var green = new Rgba(0.55f, 1f, 0.55f);
            if (BeginUiGl())
            {
                UiClip(r);
                UiModelAt(V2.Zero, 1f);
                for (float gx = r.x; gx < r.xMax; gx += 30f)
                    Line(new V2(gx, r.y), new V2(gx, r.yMax), 1f, new Rgba(0.43f, 0.55f, 0.86f, 0.1f));
                for (float gy = r.y; gy < r.yMax; gy += 30f)
                    Line(new V2(r.x, gy), new V2(r.xMax, gy), 1f, new Rgba(0.43f, 0.55f, 0.86f, 0.1f));
                Circle(portal, 40f, new Rgba(0.03f, 0.02f, 0.07f, 0.9f), 32);
                for (int i = 0; i < 6; i++)
                    Arc(portal, 10f + i * 6f, 3f - 0.3f * i, green.WithAlpha(0.85f - i * 0.1f), 20, t * (2.4f - 0.25f * i) + i, 2.2f);
                Arc(portal, 56f, 3f, new Rgba(1f, 1f, 1f, 0.12f), 40);
                if (fill > 0f)
                    Arc(portal, 56f, 5f, green, 40, -MathX.Pi / 2f, MathX.Tau * fill);
                V2 a0 = new V2(r.x + 140f, r.y + 40f), a1 = portal + new V2(-16f, -8f), b0 = new V2(r.x + 170f, r.yMax - 40f), b1 = portal + new V2(16f, 10f);
                var pa = a0 + (a1 - a0) * walk;
                var pb = b0 + (b1 - b0) * walk;
                Circle(pa, 11f, new Rgba(0.07f, 0.09f, 0.15f), 20);
                Arc(pa, 11f, 3f, Defs.Roles[0].Color, 20);
                Circle(pb, 11f, new Rgba(0.07f, 0.09f, 0.15f), 20);
                Arc(pb, 11f, 3f, Defs.Roles[2].Color, 20);
                EndUiGl();
            }
            Ui.Label(new Rect(r.x + 16f, r.y + 10f, 300f, 18f), fill >= 1f ? "Through you go!" : walk < 1f ? "A Data Gate opens in the break" : "Everyone in: it fills", 13, Ui.Text, TextAnchor.MiddleLeft, true);
        }
    }
}
