using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The menu windows as a side drawer (Docs/Mockups/menu-windows.html option C): the pause menu, Settings, Records
    /// and How to play. A tall drawer on the left holds the title and a vertical list (the pause menu's choices, or the
    /// window's sections) with your party or quick switches under it; the open section fills a panel on the right.
    /// The pause menu leaves the frozen arena in view, with a "this run" card over it. Opening a window slides the
    /// drawer in, its rows following one after another, and closing slides it out (WindowAnim.cs).
    public partial class ArenaApp
    {
        const float DrawerW = 420f;
        static readonly Rect DrawerPanel = new Rect(470f, 40f, 770f, 640f);  // the open section, right of the drawer

        int pauseSel;            // the pause menu's highlighted choice (↑ ↓, Enter)
        bool pauseSettings;      // the pause menu showing Settings
        bool pauseHowTo;         // the pause (or lobby) menu showing How to play
        Vector2 pauseMouse;      // where the mouse was last frame (hovering moves the highlight only when it moves)
        int settingsGroup = 1;   // 0 Audio, 1 Visual effects, 2 Gameplay
        int recordsSection;      // 0 Overview, 1 Bosses, 2 Classes, 3 Challenge rooms, 4 Unlocks
        int howToTopic;          // HowToTopics
        string drawerKey;        // which window the drawer last slid in for
        float drawerAt = -10f;

        /// A window is drawn: when it's a different one, it opens (its clock starts; the pause menu runs its own,
        /// WindowAnim.cs). Returns 1 (kept for the callers' panel fades, which WindowAnim now does).
        float DrawerIn(string key, bool opens)
        {
            if (key != drawerKey)
            {
                drawerKey = key;
                drawerAt = Time.unscaledTime;
                if (opens)
                    winOpenAt = drawerAt;
                listKey = panelKey = null;
            }
            return 1f;
        }

        /// The drawer's box and title; what follows is drawn in drawer coordinates until EndDrawer.
        void BeginDrawer(string title, Color accent, float k, string sub = null)
        {
            float dx = DrawerSlide;  // in from the left as the window opens, out as it closes (WindowAnim.cs)
            NoteList(title);
            rowIndex = 0;
            GUI.BeginGroup(new Rect(dx, 0f, DrawerW + 30f, H));
            for (int i = 0; i < 6; i++)  // its shadow on the arena
                Ui.Fill(new Rect(DrawerW + i * 4f, 0f, 4f, H), new Color(0f, 0f, 0f, 0.22f * (1f - i / 6f)));
            Ui.Fill(new Rect(0f, 0f, DrawerW, H), new Color(0.07f, 0.085f, 0.14f, 0.97f));
            Ui.Fill(new Rect(DrawerW - 1f, 0f, 1f, H), new Color(0.47f, 0.59f, 1f, 0.3f));
            float ti = TitleIn;  // a new list's title rises in
            var oc = GUI.color;
            GUI.color = new Color(oc.r, oc.g, oc.b, oc.a * ti);
            Ui.Alpha = ti;
            Ui.Fancy(new Rect(40f, 30f + 14f * (1f - ti), DrawerW - 60f, 60f), title, 44, Color.white, Color.Lerp(accent, Color.white, 0.35f), TextAnchor.MiddleLeft,
                new Color(accent.r, accent.g, accent.b, 0.5f));
            if (!string.IsNullOrEmpty(sub))
                Ui.Label(new Rect(42f, 98f, DrawerW - 60f, 18f), sub, 12, Ui.Muted);
            Ui.Alpha = 1f;
            GUI.color = oc;
            int segs = Mathf.RoundToInt(12f * UnderlineIn);  // the line under it, drawing across and fading out to the right
            for (int i = 0; i < segs; i++)
                Ui.Fill(new Rect(42f + i * 28f, 92f, 28f, 2f), new Color(accent.r, accent.g, accent.b, 0.9f * (1f - i / 12f)));
        }

        void EndDrawer() => GUI.EndGroup();

        /// A row in the drawer: an icon, the name (big and lit when it's the open one, with a line under it), and a key
        /// or a tag on the right. Returns true when clicked.
        bool DrawerRow(Rect r, Texture2D icon, string name, string line, string key, string tag, Color col, bool on, Color? tagColor = null)
        {
            var anim = NextRowAnim();  // one after another as the list arrives (WindowAnim.cs)
            if (anim.y <= 0.001f)
                return false;
            var oc = GUI.color;
            GUI.color = new Color(oc.r, oc.g, oc.b, oc.a * anim.y);
            Ui.Alpha = anim.y;
            r.x += anim.x;
            bool clicked = DrawRowBody(r, icon, name, line, key, tag, col, on, tagColor);
            Ui.Alpha = 1f;
            GUI.color = oc;
            return clicked;
        }

        bool DrawRowBody(Rect r, Texture2D icon, string name, string line, string key, string tag, Color col, bool on, Color? tagColor)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (on)
            {
                const int steps = 24;  // a fade to the right, in strips that meet exactly (no doubled seams)
                for (int i = 0; i < steps; i++)
                {
                    float x0 = Mathf.Round(r.x + r.width * i / steps), x1 = Mathf.Round(r.x + r.width * (i + 1) / steps);
                    Ui.Fill(new Rect(x0, r.y, x1 - x0, r.height), new Color(col.r, col.g, col.b, 0.26f * (1f - (float)i / steps)));
                }
                Ui.Fill(new Rect(r.x, r.y, 3f, r.height), col);
            }
            else if (hover)
                Ui.Pill(r, new Color(1f, 1f, 1f, 0.04f));
            if (icon != null)
                Ui.Icon(new Rect(r.x + 18f, r.center.y - 12f, 24f, 24f), icon);
            bool twoLines = on && !string.IsNullOrEmpty(line);
            if (on)
                Ui.Fancy(new Rect(r.x + 62f, twoLines ? r.y + 6f : r.y, r.width - 150f, twoLines ? 28f : r.height), name, 21, Color.white, Color.Lerp(col, Color.white, 0.3f), TextAnchor.MiddleLeft);
            else
                Ui.Label(new Rect(r.x + 62f, r.y, r.width - 150f, r.height), name, 18, new Color(0.79f, 0.83f, 0.92f), TextAnchor.MiddleLeft, true);
            if (twoLines)
                Ui.Label(new Rect(r.x + 62f, r.y + 34f, r.width - 80f, 16f), line, 12, Ui.Muted);
            if (!string.IsNullOrEmpty(key))
            {
                float kw = Mathf.Max(26f, 12f + key.Length * 7.5f);
                KeyCap(r.xMax - 14f - kw, r.center.y - 12f, key);
            }
            else if (!string.IsNullOrEmpty(tag))
                Ui.Label(new Rect(r.xMax - 120f, r.y, 106f, r.height), tag, 14, tagColor ?? Ui.Muted, TextAnchor.MiddleRight, true);
            return Ui.HitArea(r);
        }

        /// A setting with a switch on the right (its title, a key, a line under it). Returns the new value.
        bool SwitchRow(Rect r, bool value, string title, string desc = null, string key = null)
        {
            bool two = !string.IsNullOrEmpty(desc);
            Ui.Label(new Rect(r.x, two ? r.y + 3f : r.y, r.width - 60f, two ? 20f : r.height), title, 15, Ui.Text, TextAnchor.MiddleLeft, true);
            if (!string.IsNullOrEmpty(key))
                KeyCap(r.x + Ui.TextWidth(title, 15, true) + 10f, (two ? r.y + 3f : r.center.y - 10f) - 1f, key);
            if (two)
                Ui.Label(new Rect(r.x, r.y + 24f, r.width - 60f, 16f), desc, 12, Ui.Muted);
            var sw = new Rect(r.xMax - 46f, r.center.y - 12f, 46f, 24f);
            Ui.Pill(sw, value ? Ui.Primary : new Color(0.17f, 0.19f, 0.27f));
            Ui.Pill(new Rect(value ? sw.xMax - 21f : sw.x + 3f, sw.y + 3f, 18f, 18f), value ? Color.white : new Color(0.55f, 0.59f, 0.7f));
            return Ui.HitArea(r) ? !value : value;
        }

        /// The pause menu's players, in the run card: compact cards stacked up from its bottom (the Tab screen has more).
        void DrawPartyStack(Rect card)
        {
            var players = new List<Player>(game.Players.Values);
            var rise = PartyAnim;  // they rise in after the card (WindowAnim.cs)
            var pc = GUI.color;
            float oldAlpha = Ui.Alpha;
            GUI.color = new Color(pc.r, pc.g, pc.b, pc.a * rise.y);
            Ui.Alpha = oldAlpha * rise.y;
            const float h = 46f, gap = 6f;
            float x = card.x + 20f, w = card.width - 40f, y = card.yMax - 16f - h + rise.x;
            foreach (var p in players)  // the first at the bottom, each next one above it
            {
                PartyCompact(new Rect(x, y, w, h), p);
                y -= h + gap;
            }
            Ui.Label(new Rect(x + 2f, y + h - 16f, 200f, 14f), "PLAYERS", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Fill(new Rect(x, y + h - 26f, w, 1f), new Color(1f, 1f, 1f, 0.08f));
            Ui.Alpha = oldAlpha;
            GUI.color = pc;
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

        /// A hatched icon tile, tinted.
        void IconTile(Rect r, Texture2D icon, Color col, float iconSize)
        {
            Ui.Pill(r, new Color(col.r * 0.18f, col.g * 0.18f, col.b * 0.18f, 0.95f));
            Ui.Icon(r, Hatch(), new Color(col.r, col.g, col.b, 0.1f));
            if (icon != null)
                Ui.Icon(new Rect(r.center.x - iconSize / 2f, r.center.y - iconSize / 2f, iconSize, iconSize), icon);
        }

        void OpenSectionPanel(string title, Color col, string sub, float k)
        {
            Ui.Panel(DrawerPanel, false);
            Ui.Fancy(new Rect(DrawerPanel.x + 26f, DrawerPanel.y + 18f, DrawerPanel.width - 52f, 46f), title, 32, Color.white, Color.Lerp(col, Color.white, 0.25f), TextAnchor.MiddleLeft,
                new Color(col.r, col.g, col.b, 0.5f));
            if (!string.IsNullOrEmpty(sub))
                Ui.Label(new Rect(DrawerPanel.x + 28f, DrawerPanel.y + 64f, DrawerPanel.width - 56f, 34f), sub, 13, new Color(0.79f, 0.83f, 0.92f), TextAnchor.UpperLeft, false, true);
        }

        // --- The lobby's menu (Esc) --------------------------------------------------------------------------------------

        static readonly string[] LobbyChoices = { "Back to the lobby", "Settings", "Field guide", "How to play", "Leave to main menu", "Quit game" };
        bool lobbyMenuOpen;
        int lobbySel;

        /// Esc in the lobby: the menu slides in (its own clock starts here).
        void OpenLobbyMenu()
        {
            lobbyMenuOpen = true;
            lobbySel = 0;
            pauseSettings = pauseHowTo = false;
            drawerKey = null;
            winOpenAt = Time.unscaledTime;
            GUIUtility.keyboardControl = 0;
        }

        void CloseLobbyMenu() => CloseWindow(() => { lobbyMenuOpen = false; pauseSettings = pauseHowTo = false; });

        /// The lobby's menu: back to the lobby, Settings (in the drawer, like the pause menu's), the Field guide, leaving
        /// for the main menu, or quitting. The lobby stays behind it, dimmed.
        void DrawLobbyMenu()
        {
            if (SkipWhileClosing)
                return;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.6f * WindowVis));
            if (pauseSettings)
            {
                DrawSettingsWindow(true);  // its Back returns to this list
                return;
            }
            if (pauseHowTo)
            {
                DrawHowToContent(DrawerIn("pause-howto", false), Time.unscaledTime, "Back  [Esc]", () => pauseHowTo = false);
                return;
            }
            DrawerIn("pause", false);
            LobbyMenuKeys();
            BeginDrawer("MENU", Ui.Accent, 1f, session.IsHost && session.Lobby.Count > 1 ? "Leaving closes the lobby for everyone" : "In the lobby");
            Color[] cols = { Ui.Primary, new Color(0.6f, 0.66f, 0.82f), FieldGuideColor, HowToColor, new Color(1f, 0.6f, 0.45f), new Color(1f, 0.45f, 0.5f) };
            string[] icons = { "ui_play", "ui_gear", "ui_book", "ui_help", "ui_back", "ui_power" };
            string[] lines = { "Close this menu", "Audio, visual effects, aim line", "Every enemy, boss, class, weapon, ability and upgrade", "Controls and tips",
                session.IsHost ? "Close the lobby and go back to the title screen" : "Leave the lobby and go back to the title screen", "Close the game" };
            float y = 128f;
            for (int i = 0; i < LobbyChoices.Length; i++)
            {
                bool on = i == lobbySel;
                var r = new Rect(28f, y, DrawerW - 56f, on ? 62f : 52f);
                if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint && Event.current.mousePosition != pauseMouse)
                    lobbySel = i;
                if (DrawerRow(r, PixelArt.Get(icons[i]), LobbyChoices[i], lines[i], i == 0 ? "Esc" : null, null, cols[i], on))
                    LobbyChoose(i);
                y += r.height + 4f;
            }
            if (Event.current.type == EventType.Repaint)
                pauseMouse = Event.current.mousePosition;
            Ui.Label(new Rect(42f, H - 22f, DrawerW - 60f, 16f), "↑ ↓  Enter  ·  Esc to go back", 11, Ui.Muted);
            EndDrawer();
        }

        void LobbyMenuKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0)
                return;
            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W)
                lobbySel = (lobbySel + LobbyChoices.Length - 1) % LobbyChoices.Length;
            else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S)
                lobbySel = (lobbySel + 1) % LobbyChoices.Length;
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                LobbyChoose(lobbySel);
            else
                return;
            e.Use();
        }

        void LobbyChoose(int i)
        {
            switch (i)
            {
                case 0: CloseLobbyMenu(); break;
                case 1: pauseSettings = true; break;
                case 2: OpenFieldGuide(); break;
                case 3: pauseHowTo = true; break;
                case 4:
                    lobbyMenuOpen = false;
                    pauseSettings = pauseHowTo = false;
                    StartLobbyLeave();  // the way in from the menu, backwards (LobbyEnter.cs)
                    break;
                default: Application.Quit(); break;
            }
        }

        // --- The pause menu -------------------------------------------------------------------------------------------

        static readonly string[] PauseChoices = { "Resume", "Run stats", "Field guide", "How to play", "Settings", "Leave to lobby", "Leave game" };
        const int PauseToLobby = 5;  // the host only: everyone back to the lobby

        /// The pause menu's choices for this player (Leave to lobby is the host's: it takes everyone back).
        List<int> PauseRows()
        {
            var rows = new List<int>();
            for (int i = 0; i < PauseChoices.Length; i++)
                if (i != PauseToLobby || session.IsHost && !LabRunning)
                    rows.Add(i);
            return rows;
        }

        /// The pause menu (DrawPauseMenu applies its opening): the drawer of choices and your party, and the run's card.
        void DrawPauseWindow()
        {
            if (pauseSettings)
            {
                DrawSettingsWindow(true);
                return;
            }
            if (pauseHowTo)
            {
                DrawHowToContent(DrawerIn("pause-howto", false), Time.unscaledTime, "Back  [Esc]", () => pauseHowTo = false);
                return;
            }
            float t = Time.unscaledTime;
            if (pauseConfirmLobby)
            {
                DrawLobbyConfirm(t);
                return;
            }
            DrawerIn("pause", false);
            PauseKeys();
            Player by;
            string sub = "";
            if (game.Paused && game.PausedBy != game.MyId && game.Players.TryGetValue(game.PausedBy, out by))
                sub = "Paused by " + by.Name;
            else if (!game.Paused && game.State != MatchState.GameOver && game.State != MatchState.Victory)
                sub = "Pausing…";
            else if (game.Players.Count > 1)
                sub = "Nobody moves until someone resumes";
            BeginDrawer("PAUSED", Ui.Accent, 1f, sub);
            Color[] cols = { Ui.Primary, Ui.Gold, FieldGuideColor, HowToColor, new Color(0.6f, 0.66f, 0.82f), new Color(1f, 0.6f, 0.45f), new Color(1f, 0.45f, 0.5f) };
            string[] icons = { "ui_play", "ui_trophy", "ui_book", "ui_help", "ui_gear", "ui_back", "ui_power" };
            bool saved = session.IsHost && game.RunId != null;
            string[] lines = { "Back into wave " + Mathf.Max(1, game.Wave), "Your build, damage and kills so far", "Every enemy, weapon and upgrade", "Controls and tips",
                "Audio, visual effects, aim line", (game.Players.Count > 1 ? "Everyone back to the lobby" : "Back to the lobby") + (saved ? ": the run stays saved" : ""),
                saved ? "Saved as each wave starts: continue from the menu" : "Back to the main menu" };
            var rows = PauseRows();
            pauseSel = Mathf.Clamp(pauseSel, 0, rows.Count - 1);
            float y = 128f;
            for (int k = 0; k < rows.Count; k++)
            {
                int i = rows[k];
                bool on = k == pauseSel;
                var r = new Rect(28f, y, DrawerW - 56f, on ? 62f : 52f);
                if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint && Event.current.mousePosition != pauseMouse)
                    pauseSel = k;  // the mouse moved onto it (the keys move it otherwise)
                if (DrawerRow(r, PixelArt.Get(icons[i]), PauseChoices[i], lines[i], i == 0 ? "Esc" : null, null, cols[i], on))
                    PauseChoose(i);
                y += r.height + 4f;
            }
            if (Event.current.type == EventType.Repaint)
                pauseMouse = Event.current.mousePosition;
            EndDrawer();
            BeginPanelAnim("runcard", true);  // it drops in with a bounce (WindowAnim.cs)
            DrawRunCard(new Rect(W - 500f, 54f, 450f, H - 54f - 40f), t);  // down to near the bottom, the players in it
            EndPanelAnim();
        }

        static readonly Color LobbyConfirmColor = new Color(1f, 0.6f, 0.45f);

        /// The pause menu's Leave to lobby, asking first: back to the lobby (everyone beams out, Recall.cs) or keep
        /// playing. ↑ ↓ and Enter, or Esc for no. The run's card stays on the right.
        void DrawLobbyConfirm(float t)
        {
            DrawerIn("pause-confirm", false);
            var e = Event.current;
            if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == 0)
            {
                if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W || e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S)
                {
                    confirmSel = 1 - confirmSel;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    ConfirmLobby(confirmSel == 0);
                    e.Use();
                }
            }
            bool many = game.Players.Count > 1, saved = game.RunId != null && game.Practice == null;
            BeginDrawer("TO LOBBY?", LobbyConfirmColor, 1f, many ? "Ends the run for everyone" : "Ends the run");
            string[] names = { "Back to the lobby", "Keep playing" };
            string[] lines = { many ? "Everyone beams out to the lobby" : "Beam out to the lobby", "Back to the pause menu" };
            string[] icons = { "ui_back", "ui_play" };
            Color[] cols = { LobbyConfirmColor, Ui.Primary };
            float y = 128f;
            for (int i = 0; i < 2; i++)
            {
                bool on = i == confirmSel;
                var r = new Rect(28f, y, DrawerW - 56f, on ? 62f : 52f);
                if (r.Contains(e.mousePosition) && e.type == EventType.Repaint && e.mousePosition != pauseMouse)
                    confirmSel = i;
                if (DrawerRow(r, PixelArt.Get(icons[i]), names[i], lines[i], i == 1 ? "Esc" : "Enter", null, cols[i], on))
                    ConfirmLobby(i == 0);
                y += r.height + 4f;
            }
            if (e.type == EventType.Repaint)
                pauseMouse = e.mousePosition;
            y += 14f;
            string note = saved ? "The run is saved from the last break between waves: pick it up again with Continue a run on the main menu."
                : "This run isn't saved: it can't be continued.";
            float nh = Ui.TextHeight(note, 13, DrawerW - 84f);
            Ui.Fill(new Rect(28f, y, 3f, nh + 4f), new Color(LobbyConfirmColor.r, LobbyConfirmColor.g, LobbyConfirmColor.b, 0.7f));
            Ui.Label(new Rect(42f, y, DrawerW - 84f, nh + 4f), note, 13, new Color(0.79f, 0.83f, 0.92f), TextAnchor.UpperLeft, false, true);
            EndDrawer();
            BeginPanelAnim("runcard", true);
            DrawRunCard(new Rect(W - 500f, 54f, 450f, H - 54f - 40f), t);
            EndPanelAnim();
        }

        void ConfirmLobby(bool yes)
        {
            pauseConfirmLobby = false;
            if (yes)
                RecallToLobby();  // everyone beams out, then the host takes them back (Recall.cs)
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

        void PauseChoose(int i)
        {
            switch (i)
            {
                case 0: ResumeGame(); break;
                case 1: runStatsOpen = true; break;
                case 2: OpenFieldGuide(); break;
                case 3: pauseHowTo = true; break;
                case 4: pauseSettings = true; break;
                case PauseToLobby:
                    if (!session.IsHost)
                        break;
                    pauseConfirmLobby = true;  // asks first (DrawLobbyConfirm)
                    confirmSel = 1;            // on "keep playing", so a stray Enter doesn't end the run
                    break;
                default:
                    pauseMenuOpen = false;
                    session.Leave("");
                    break;
            }
        }

        /// The run's card over the arena: the wave, time and score, the path, modifiers and difficulty, the next boss
        /// (moving) and this stretch's challenge room.
        void DrawRunCard(Rect at, float t)
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
            Ui.Panel(r, false);
            DrawPartyStack(r);  // the players, stacked up from its bottom
            DrawCorners(r, Ui.Gold);
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
            int next = game.Wave % 5 == 0 && game.State == MatchState.Fighting ? game.Wave : (game.Wave / 5 + 1) * 5;
            var boss = Game.BossForWave(Mathf.Min(Game.MaxWave, Mathf.Max(5, next)));
            var tile = new Rect(x, y, 64f, 64f);
            IconTile(tile, null, Ui.Gold, 0f);
            bool fighting = game.Wave % 5 == 0 && game.State == MatchState.Fighting;
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
            Ui.Label(new Rect(x + 78f, y + 4f, 320f, 14f), "CHALLENGE ROOM · THIS STRETCH", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            int block = (Mathf.Max(1, game.Wave) - 1) / 5;
            string nextPortal = "Next portal after wave " + (block * 5 + 6) + ", " + (block * 5 + 7) + " or " + (block * 5 + 8);
            if (rc != ChallengeKind.None)
            {
                Ui.Fancy(new Rect(x + 78f, y + 18f, 320f, 28f), Challenges.Name(rc) + (lastRoomWon ? "  ✓" : "  ✗"), 19, Color.white, lastRoomWon ? ToColor(Challenges.Accent(rc)) : new Color(1f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
                Ui.Label(new Rect(x + 78f, y + 46f, 340f, 16f), (lastRoomWon ? "Won" : "Lost") + "  ·  " + nextPortal, 12, Ui.Muted);
            }
            else if (game.Room == RoomPhase.Portal || game.InRoom)
            {
                Ui.Fancy(new Rect(x + 78f, y + 18f, 320f, 28f), Challenges.Name(game.RoomKind), 19, Color.white, ToColor(Challenges.Accent(game.RoomKind)), TextAnchor.MiddleLeft);
                Ui.Label(new Rect(x + 78f, y + 46f, 340f, 16f), game.InRoom ? "You're in it now" : "Its portal is open", 12, Ui.Muted);
            }
            else
            {
                int k = Mathf.Max(1, game.Wave) % 5;
                Ui.Fancy(new Rect(x + 78f, y + 18f, 320f, 28f), "Not yet", 19, Color.white, new Color(0.75f, 0.68f, 1f), TextAnchor.MiddleLeft);
                Ui.Label(new Rect(x + 78f, y + 46f, 340f, 16f), k >= 1 && k <= 3 && game.Practice == null ? "A portal opens after one of the next few waves" : nextPortal, 12, Ui.Muted);
            }
            if (BeginUiGl())
            {
                UiClip(tile);
                DrawEnemyPreview(boss == EnemyType.TwinB ? EnemyType.TwinA : boss, bossAt, 52f, t, true);
                EndUiGl();
            }
        }

        static string RoomIcon(ChallengeKind k) =>
            k == ChallengeKind.Defend ? "shield" : k == ChallengeKind.MiniBoss ? "ui_skull" : k == ChallengeKind.KillRush ? "bolt"
            : k == ChallengeKind.HoldZones ? "pulse" : k == ChallengeKind.Mirror ? "mirror" : "blink";

        /// The lobby's corner brackets round a rect.
        static void DrawCorners(Rect r, Color c)
        {
            const float len = 20f, th = 3f, g = 6f;
            float x0 = r.x - g, y0 = r.y - g, x1 = r.xMax + g, y1 = r.yMax + g;
            Ui.Fill(new Rect(x0, y0, len, th), c); Ui.Fill(new Rect(x0, y0, th, len), c);
            Ui.Fill(new Rect(x1 - len, y0, len, th), c); Ui.Fill(new Rect(x1 - th, y0, th, len), c);
            Ui.Fill(new Rect(x0, y1 - th, len, th), c); Ui.Fill(new Rect(x0, y1 - len, th, len), c);
            Ui.Fill(new Rect(x1 - len, y1 - th, len, th), c); Ui.Fill(new Rect(x1 - th, y1 - len, th, len), c);
        }

        // --- Settings ---------------------------------------------------------------------------------------------------

        static readonly string[] SettingsGroups = { "Audio", "Visual effects", "Gameplay" };

        /// Settings (main menu, lobby, and the pause menu's Settings): the groups and quick switches in the drawer, the
        /// open group on the right. Visual effects shows a live preview of what they do.
        void DrawSettingsWindow(bool paused)
        {
            float t = Time.unscaledTime;
            float k = DrawerIn(paused ? "pause" : "settings", !paused);  // (in the pause or lobby menu: the same drawer, its list swapped)
            settingsGroup = Mathf.Clamp(settingsGroup, 0, SettingsGroups.Length - 1);
            BeginDrawer("SETTINGS", Ui.Accent, k, "Saved on this computer  ·  changes apply at once");
            string[] icons = { "pulse", "nova", "crosshair" };
            Color[] cols = { new Color(0.5f, 0.69f, 1f), Ui.Gold, new Color(0.55f, 1f, 0.55f) };
            string[] lines = { "Volume for each kind of sound", "What the fight looks like", "Aim line and the HUD" };
            float y = 128f;
            for (int i = 0; i < SettingsGroups.Length; i++)
            {
                bool on = i == settingsGroup;
                var r = new Rect(28f, y, DrawerW - 56f, on ? 62f : 52f);
                if (DrawerRow(r, PixelArt.Get(icons[i]), SettingsGroups[i], lines[i], null, null, cols[i], on))
                    settingsGroup = i;
                y += r.height + 4f;
            }
            y += 24f;
            Ui.Label(new Rect(42f, y, 200f, 16f), "QUICK", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            var quick = new Rect(40f, y + 22f, DrawerW - 80f, 104f);
            Ui.Pill(quick, new Color(1f, 1f, 1f, 0.03f));
            bool mute = SwitchRow(new Rect(quick.x + 14f, quick.y + 8f, quick.width - 28f, 40f), sounds.Muted, "Mute all sound", null, "M");
            if (mute != sounds.Muted)
            {
                sounds.Muted = mute;
                SaveAudioSettings();
            }
            bool line = SwitchRow(new Rect(quick.x + 14f, quick.y + 56f, quick.width - 28f, 40f), aimLine, "Aim line", null, "V");
            if (line != aimLine)
                SetAimLine(line);
            if (Ui.FancyButton(new Rect(40f, H - 100f, 220f, 48f), "Back  [Esc]", Ui.Primary, PixelArt.Get("ui_back"), t))
            {
                if (paused)
                    pauseSettings = false;
                else
                    CloseWindow(() => settingsOpen = false);  // it slides out first (WindowAnim.cs)
            }
            EndDrawer();

            var col = cols[settingsGroup];
            BeginPanelAnim("settings" + settingsGroup);
            switch (settingsGroup)
            {
                case 0:
                {
                    OpenSectionPanel("AUDIO", col, "Each kind of sound has its own volume, under the master.", k);
                    float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f;
                    y = DrawerPanel.y + 104f;
                    bool changed = false;
                    changed |= VolumeSlider(x, ref y, w, "Master", ref sounds.Master);
                    changed |= VolumeSlider(x, ref y, w, "Music", ref sounds.Music);
                    changed |= VolumeSlider(x, ref y, w, "Weapons  (your shots and spells)", ref sounds.Weapons);
                    changed |= VolumeSlider(x, ref y, w, "Enemies", ref sounds.Enemies);
                    changed |= VolumeSlider(x, ref y, w, "Effects  (explosions, abilities, the menus)", ref sounds.Effects);
                    bool m2 = SwitchRow(new Rect(x, y + 6f, w, 44f), sounds.Muted, "Mute all sound", "Everything off at once; the volumes are kept", "M");
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
                    DrawFxSection(col, t, k);
                    break;
                default:
                {
                    OpenSectionPanel("GAMEPLAY", col, null, k);
                    float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f;
                    y = DrawerPanel.y + 84f;
                    bool l2 = SwitchRow(new Rect(x, y, w, 48f), aimLine, "Aim line", "A line from you toward the cursor, as long as your weapon reaches", "V");
                    if (l2 != aimLine)
                        SetAimLine(l2);
                    y += 58f;
                    bool hud = SwitchRow(new Rect(x, y, w, 48f), hudShown, "Show the HUD", "Hide it for a clean screen; a slim boss bar stays", "H");
                    if (hud != hudShown)
                        hudShown = hud;
                    break;
                }
            }
            EndPanelAnim();
        }

        /// Visual effects: a live preview, then the switches and the screen shake choice.
        void DrawFxSection(Color col, float t, float k)
        {
            OpenSectionPanel("VISUAL EFFECTS", col, null, k);
            float x = DrawerPanel.x + 26f, w = DrawerPanel.width - 52f, y = DrawerPanel.y + 76f;
            var prev = new Rect(x, y, w, 170f);
            DrawFxPreview(prev, t);
            y += 184f;
            float half = (w - 30f) / 2f;
            bool changed = false;
            bool on = SwitchRow(new Rect(x, y, half, 46f), fxOn, "Visual effects", "Glow, sparks, shattering enemies, lighting");
            if (on != fxOn) { fxOn = on; changed = true; }
            bool nums = SwitchRow(new Rect(x + half + 30f, y, half, 46f), fxNumbers, "Damage numbers", "Small numbers rising off what's hit");
            if (nums != fxNumbers) { fxNumbers = nums; changed = true; }
            y += 58f;
            Ui.Label(new Rect(x, y, w, 20f), "Screen shake", 15, fxOn ? Ui.Text : Ui.Muted, TextAnchor.MiddleLeft, true);
            string[] shakes = { "Off", "Low", "Full" };
            float bw = (w - 8f) / 3f;
            for (int i = 0; i < 3; i++)
                if (Ui.Choice(new Rect(x + i * (bw + 4f), y + 24f, bw, 32f), shakes[i], fxShake == i, Ui.Accent, null, null, 13, glow: false) && fxShake != i)
                {
                    fxShake = i;
                    changed = true;
                }
            y += 72f;
            bool names = SwitchRow(new Rect(x, y, half, 46f), fxCallouts, "Ability names", "Each ability's name over whoever casts it");
            if (names != fxCallouts) { fxCallouts = names; changed = true; }
            bool calm = SwitchRow(new Rect(x + half + 30f, y, half, 46f), fxReduced, "Reduced effects", "No screen shake, hit-pause or white flashes");
            if (calm != fxReduced) { fxReduced = calm; changed = true; }
            if (changed)
                SaveFxSettings();
            Ui.Label(new Rect(x, DrawerPanel.yMax - 34f, w, 18f), "The preview shows what's on: shots and a hit, its number, an ability's name, a blast and its shake.", 12, Ui.Muted);
        }

        /// A tiny fight that follows the visual settings: shots into a target (it flashes and shows numbers), an
        /// ability's name now and then, a blast that shakes the box.
        void DrawFxPreview(Rect r, float t)
        {
            Ui.Pill(r, new Color(0.04f, 0.05f, 0.08f, 1f));
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
                    if (fxOn)
                        Circle(at, 9f, new Rgba(0.55f, 0.75f, 1f, 0.25f), 12);
                    Circle(at, 4.5f, new Rgba(0.55f, 0.75f, 1f), 10);
                }
                Circle(me, 16f, new Rgba(0.07f, 0.09f, 0.15f), 24);
                Arc(me, 16f, 3f, new Rgba(0.36f, 0.6f, 1f), 24);
                Polygon(foe, 20f, 3, -MathX.Pi / 2f, fxOn && hit ? Rgba.White : new Rgba(1f, 0.35f, 0.35f));
                if (fxOn && hit)
                    for (int s = 0; s < 6; s++)
                        Line(foe, foe + V2.FromAngle(s * 1.05f + t) * 22f, 2f, new Rgba(1f, 0.85f, 0.5f, 0.8f));
                if (boom > 0f)
                {
                    Arc(blast, 60f * (1.2f - boom), 4f, new Rgba(1f, 0.67f, 0.31f, boom), 32);
                    if (fxOn)
                        Circle(blast, 30f * boom, new Rgba(1f, 0.8f, 0.4f, 0.3f * boom), 20);
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

        static readonly string[] RecordSections = { "Overview", "Bosses", "Classes", "Challenge rooms", "Unlocks" };

        void DrawRecordsWindow()
        {
            var p = Profile.Current;
            float t = Time.unscaledTime;
            float k = DrawerIn("records", true);
            BeginDrawer("RECORDS", Ui.Gold, k, "Kept on this computer");
            int bossesBeaten = 0;
            foreach (var b in RecordBosses)
                if (p.Beaten(b) > 0)
                    bossesBeaten++;
            int classWins = 0, roomsWon = 0;
            foreach (var kv in p.WinsAs)
                if (kv.Value > 0)
                    classWins++;
            foreach (var c in Challenges.All)
                roomsWon += p.RoomWon(c);
            string[] icons = { "ui_star", "ui_skull", "class_knight", "blink", "ui_lock" };
            string[] tags = { p.RunsStarted + " runs", bossesBeaten + " / " + RecordBosses.Length, classWins + " won", roomsWon + " won", Unlocks.All.Count == 0 ? "soon" : null };
            Color[] cols = { Ui.Gold, Ui.Gold, new Color(1f, 0.6f, 0.3f), new Color(0.45f, 0.9f, 1f), Ui.Gold };
            float y = 128f;
            for (int i = 0; i < RecordSections.Length; i++)
            {
                bool on = i == recordsSection;
                var r = new Rect(28f, y, DrawerW - 56f, on ? 62f : 52f);
                if (DrawerRow(r, PixelArt.Get(icons[i]), RecordSections[i], i == 0 ? "Practice and dev-tool runs don't count" : null, null, tags[i], cols[i], on,
                    i == 1 && bossesBeaten > 0 ? Ui.Good : (Color?)null))
                    recordsSection = i;
                y += r.height + 4f;
            }
            if (Ui.FancyButton(new Rect(40f, H - 100f, 220f, 48f), "Back  [Esc]", Ui.Primary, PixelArt.Get("ui_back"), t))
                CloseWindow(() => recordsOpen = false);
            EndDrawer();
            BeginPanelAnim("records" + recordsSection);
            DrawRecordsSection(p, t, bossesBeaten, roomsWon, cols);
            EndPanelAnim();
        }

        void DrawRecordsSection(Profile p, float t, int bossesBeaten, int roomsWon, Color[] cols)
        {
            float k = 1f;

            var col = cols[recordsSection];
            float x = DrawerPanel.x + 26f, w = DrawerPanel.width - 52f;
            switch (recordsSection)
            {
                case 0:
                {
                    OpenSectionPanel("OVERVIEW", col, null, k);
                    var span = TimeSpan.FromSeconds(p.PlayTime);
                    string played = span.TotalHours >= 1 ? (int)span.TotalHours + " h " + span.Minutes + " m" : (int)(p.PlayTime / 60f) + " min";
                    string[,] tiles = {
                        { "Runs started", p.RunsStarted.ToString() }, { "Won", p.RunsWon.ToString() }, { "Lost", p.RunsLost.ToString() }, { "Time played", played },
                        { "Best wave", p.BestWave > 0 ? p.BestWave.ToString() : "—" }, { "Best endless", p.BestEndlessWave > 0 ? p.BestEndlessWave.ToString() : "—" },
                        { "Best score", p.BestScore > 0 ? p.BestScore.ToString("N0") : "—" }, { "Challenge rooms", roomsWon + " won" },
                        { "Enemies killed", p.Kills.ToString("N0") }, { "Damage dealt", ShortNumber(p.DamageDealt) }, { "Healing done", ShortNumber(p.HealingDone) }, { "Allies revived", p.Revives.ToString() } };
                    float tw = (w - 30f) / 4f, th = 78f;
                    for (int i = 0; i < tiles.GetLength(0); i++)
                    {
                        var tr = new Rect(x + (i % 4) * (tw + 10f), DrawerPanel.y + 84f + (i / 4) * (th + 12f), tw, th);
                        Ui.Pill(tr, new Color(1f, 1f, 1f, 0.035f));
                        Ui.Fancy(new Rect(tr.x + 14f, tr.y + 10f, tr.width - 20f, 34f), tiles[i, 1], 24, Color.white, new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleLeft);
                        Ui.Label(new Rect(tr.x + 14f, tr.y + 48f, tr.width - 20f, 16f), tiles[i, 0].ToUpperInvariant(), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
                    }
                    Ui.Label(new Rect(x, DrawerPanel.y + 84f + 3 * (th + 12f) + 8f, w, 18f), "Times downed: " + p.Downs + "  ·  boss kills: " + p.BossKills, 12, Ui.Muted);
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
                        Ui.Pill(rr, new Color(1f, 1f, 1f, n > 0 ? 0.045f : 0.02f));
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
                        Ui.Pill(rr, new Color(1f, 1f, 1f, 0.035f));
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
                    OpenSectionPanel("CHALLENGE ROOMS", col, played > 0 ? roomsWon + " of " + played + " won" : "A portal opens once every five waves. Win a room for a prize pick.", k);
                    for (int i = 0; i < Challenges.All.Length; i++)
                    {
                        var c = Challenges.All[i];
                        int won = p.RoomWon(c), pl = p.RoomPlayed(c);
                        var cc = ToColor(Challenges.Accent(c));
                        var rr = new Rect(x, DrawerPanel.y + 104f + i * 86f, w, 76f);
                        Ui.Pill(rr, new Color(1f, 1f, 1f, 0.035f));
                        IconTile(new Rect(rr.x + 12f, rr.y + 12f, 52f, 52f), PixelArt.Get(RoomIcon(c)), cc, 34f);
                        Ui.Fancy(new Rect(rr.x + 80f, rr.y + 8f, 340f, 30f), Challenges.Name(c), 20, Color.white, cc, TextAnchor.MiddleLeft);
                        Ui.Label(new Rect(rr.xMax - 140f, rr.y + 8f, 126f, 30f), pl > 0 ? won + " of " + pl : "not yet", 17, pl > 0 ? Ui.Text : Ui.Muted, TextAnchor.MiddleRight, true);
                        Ui.Bar(new Rect(rr.x + 80f, rr.y + 48f, rr.width - 96f, 8f), pl > 0 ? (float)won / pl : 0f, cc, new Color(0f, 0f, 0f, 0.5f));
                    }
                    break;
                }
                default:
                {
                    OpenSectionPanel("UNLOCKS", col, null, k);
                    float uy = DrawerPanel.y + 84f;
                    if (Unlocks.All.Count == 0)
                        Ui.Label(new Rect(x, uy, w, 60f), "Unlocks are on their way. Your records are already being kept, so anything you've done will count.", 14, Ui.Muted, TextAnchor.UpperLeft, false, true);
                    foreach (var u in Unlocks.All)
                    {
                        bool on = p.IsUnlocked(u.Id);
                        var rr = new Rect(x, uy, w, 56f);
                        Ui.Pill(rr, new Color(1f, 1f, 1f, on ? 0.06f : 0.025f));
                        IconTile(new Rect(rr.x + 6f, rr.y + 6f, 44f, 44f), PixelArt.Get(on ? "ui_star" : "ui_lock"), on ? Ui.Gold : Ui.Muted, 26f);
                        Ui.Label(new Rect(rr.x + 62f, rr.y + 6f, w - 180f, 20f), u.Name, 15, on ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
                        Ui.Label(new Rect(rr.x + 62f, rr.y + 28f, w - 80f, 18f), u.Desc, 12, Ui.Muted);
                        if (on)
                            Ui.Badge(new Rect(rr.xMax - 100f, rr.y + 8f, 86f, 18f), "UNLOCKED", Ui.GoldButton);
                        uy += 64f;
                    }
                    break;
                }
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
            new HowTo { Name = "The basics", Icon = "crosshair", Color = new Color(1f, 0.55f, 0.5f), Intro = "Survive 50 waves with a boss every fifth one.",
                Tips = new[,] { { "Hitbox", "Your hitbox is just the small white dot in the middle of your character.", "0" }, { "Wrap around", "Walk off an edge to come in on the other side. Enemies and bullets can't follow.", "1" },
                    { "Roles", "Enemies prefer to target the Tank. A Healer's aura heals allies nearby. DPS hits hardest.", "8" }, { "Health orbs", "Kills sometimes drop green orbs that heal you. They drift toward you when you're close.", "9" },
                    { "Practice Range", "Pick it in the lobby to play waves and bosses you set up yourself.", "13" } } },
            new HowTo { Name = "Fighting", Icon = "guard_reinforced", Color = new Color(0.55f, 0.8f, 1f), Intro = "Read the shots, parry what you can, and let your abilities grow.",
                Tips = new[,] { { "Perfect parry", "Raise your parry just as a shot arrives: blocked bullets fly back at enemies and cost nothing.", "7" }, { "Boss shots", "Dark-cored shots are a boss's: violet ones wear blockers down, red ones can't be blocked.", "5" },
                    { "Boss bodies", "Touching a boss hurts and shoves you back. Parry up close to get past its fire.", "12" }, { "Status icons", "Snowflake = frozen, arrows = slowed, flame = burning, drop = bleeding, crosshair = marked.", "2" },
                    { "Ability mastery", "Abilities level up from 1 to 5 as you use them in fights. Level 5 unlocks a capstone.", "10" } } },
            new HowTo { Name = "Between waves", Icon = "ui_star", Color = Ui.Gold, Intro = "Each break: pick upgrades, open what the wave left behind, then the next wave.",
                Tips = new[,] { { "Upgrades", "Levels give upgrade picks at the end of each wave; bosses give legendary ones.", "4" }, { "Treasure chests", "Now and then a chest appears: touch it once the wave is clear and everyone gets an item.", "icon:ui_coin" },
                    { "Rerolls", "Don't like the cards? Reroll them: you start with 2 and get 2 more for every boss.", "icon:ui_arrow" }, { "Downed", "At 0 HP you're downed: stand by a downed ally for 3 s to revive them. Clearing a wave brings everyone back.", "3" } } },
            new HowTo { Name = "Challenge rooms", Icon = "blink", Color = new Color(0.45f, 0.9f, 1f), Portal = true, Intro = "Once every five waves a portal opens in the break. Everyone alive stands in it to go through. Win the room for a prize pick; lose it and you're simply sent back.",
                Tips = new[,] { { "Defend the Crystal", "Keep it alive for 40 s. Most enemies go for it.", "icon:shield" }, { "The Gatekeeper", "A mini-boss: slip the ring's gap, step off its charge line.", "icon:ui_skull" },
                    { "Kill Rush", "40 kills in 40 s, weak enemies from every side.", "icon:bolt" }, { "Hold the Zones", "Stand in each zone until it fills. An enemy in it slows you.", "icon:pulse" },
                    { "Mirror Match", "A shadow of each of you, fighting your way. Beat your own.", "icon:mirror" }, { "The prize", "Touch the altar: a rare-or-better pick, sometimes a legendary.", "icon:ui_star" } } },
            new HowTo { Name = "Paths and modifiers", Icon = "snow", Color = new Color(0.65f, 0.92f, 1f), Intro = "Each stretch between bosses can play differently.",
                Tips = new[,] { { "Paths", "After each boss the host picks a path. More skulls: harder waves, better prize. The Bazaar has a shop.", "11" }, { "Modifiers", "Chosen in the lobby: rules for the whole run, like Glass Cannon or Hunted. Some make it easier.", "icon:ui_skull" },
                    { "Difficulty", "Easy to Nightmare, or Custom with every number on a slider. Set in the lobby.", "icon:ui_gear" } } },
            new HowTo { Name = "Playing together", Icon = "link", Color = new Color(0.55f, 1f, 0.6f), Intro = "Up to three players, one hosting.",
                Tips = new[,] { { "Co-op", "1 to 3 players: one hosts, the others join (Tailscale makes this easy).", "6" }, { "Reviving", "Stand by a downed ally to bring them back. Healers and some upgrades do it faster.", "3" },
                    { "Pausing", "Anyone can pause: it pauses the match for everyone until someone resumes.", "icon:ui_play" } } },
        };

        static readonly Color HowToColor = new Color(0.3f, 0.45f, 0.75f);  // the How to play button, as on the main menu

        void DrawHowToWindow()
        {
            float t = Time.unscaledTime;
            DrawHowToContent(DrawerIn("howto", true), t, "Back  [Esc]", () => CloseWindow(() => howToOpen = false));
        }

        /// How to play (from the main menu): its topics in the drawer, the open
        /// topic on the right. `back` is what its Back button does.
        void DrawHowToContent(float k, float t, string backLabel, System.Action back)
        {
            BeginDrawer("HOW TO PLAY", Ui.Accent, k);
            float y = 120f;
            for (int i = 0; i < HowToTopics.Length; i++)
            {
                var tp = HowToTopics[i];
                bool on = i == howToTopic;
                var r = new Rect(28f, y, DrawerW - 56f, 52f);
                if (DrawerRow(r, PixelArt.Get(tp.Icon), tp.Name, null, null, null, tp.Color, on))
                    howToTopic = i;
                y += 56f;
            }
            if (Ui.FancyButton(new Rect(40f, H - 100f, 260f, 48f), backLabel, Ui.Primary, PixelArt.Get("ui_back"), t))
                back();
            EndDrawer();

            var topic = HowToTopics[howToTopic];
            BeginPanelAnim("howto" + howToTopic);  // (WindowAnim.cs)
            DrawHowToTopic(topic, t, k);
            EndPanelAnim();
        }

        void DrawHowToTopic(HowTo topic, float t, float k)
        {
            OpenSectionPanel(topic.Name.ToUpperInvariant(), topic.Color, topic.Intro, k);
            float x = DrawerPanel.x + 26f, w = DrawerPanel.width - 52f, top = DrawerPanel.y + 110f;
            if (topic.Keys)
            {
                float colW = (w - 30f) / 2f, rowH = 52f;
                int rows = (HowToKeys.Length + 1) / 2;
                var keysW = new float[2];
                for (int i = 0; i < HowToKeys.Length; i++)
                {
                    float kw = 0f;
                    foreach (var key in HowToKeys[i])
                        kw += Mathf.Max(26f, 12f + key.Length * 7.5f) + 4f;
                    keysW[i / rows] = Mathf.Max(keysW[i / rows], kw);
                }
                for (int i = 0; i < HowToKeys.Length; i++)
                {
                    int c = i / rows;
                    float kx = x + c * (colW + 30f), ky = top + (i % rows) * rowH, cx = kx, lx = kx + keysW[c] + 10f;
                    foreach (var key in HowToKeys[i])
                        cx += KeyCap(cx, ky + 3f, key) + 4f;
                    string note = HowToActions[i, 1];
                    Ui.Label(new Rect(lx, ky, kx + colW - lx, note.Length > 0 ? 17f : 30f), HowToActions[i, 0], 14, Ui.Text, TextAnchor.MiddleLeft, true);
                    if (note.Length > 0)
                        Ui.Label(new Rect(lx, ky + 16f, kx + colW - lx, 30f), note, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
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
                Ui.Pill(r, new Color(1f, 1f, 1f, 0.035f));
                string g = topic.Tips[i, 2];
                var tile = new Rect(r.x + 10f, r.y + 10f, 44f, 44f);
                if (g.StartsWith("icon:"))
                    IconTile(tile, PixelArt.Get(g.Substring(5)), topic.Color, 28f);
                else
                {
                    IconTile(tile, null, topic.Color, 0f);
                    glyphs.Add(new KeyValuePair<int, V2>(int.Parse(g), new V2(tile.center.x, tile.center.y)));
                }
                Ui.Label(new Rect(r.x + 64f, r.y + 8f, r.width - 74f, 20f), topic.Tips[i, 0], 15, Ui.Text, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 64f, r.y + 30f, r.width - 74f, r.height - 36f), topic.Tips[i, 1], 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
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
            Ui.Pill(r, new Color(0.04f, 0.05f, 0.08f, 1f));
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
            Ui.Label(new Rect(r.x + 16f, r.y + 10f, 300f, 18f), fill >= 1f ? "Through you go!" : walk < 1f ? "A portal opens in the break" : "Everyone in: it fills", 13, Ui.Text, TextAnchor.MiddleLeft, true);
        }
    }
}
