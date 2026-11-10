using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The F1 developer tools as a dock (Docs/Mockups option C): a slim bar over the live game with the things you reach for most pinned
    /// (God, Full heal, Skip wave, Kill all), four keys (WAVE, PARTY, VIEW, LABS) that each open a small drawer under them, the stat editor
    /// as a side drawer and the bigger tools (DPS and dummies, Replays, Balance test) as one sheet. In a match the game keeps running and
    /// only the mouse is blocked over the dock; in the lobby and menus it is modal. Esc and F1 close it (UiScreens.cs).
    public partial class ArenaApp
    {
        int devDrawer;            // 0 none, 1 wave, 2 party, 3 view, 4 labs
        bool devSide;             // the stat editor, as a side drawer
        int devSheet;             // 0 none, 1 DPS meter and dummies, 2 Replays, 3 Balance test
        readonly List<Rect> dockHit = new List<Rect>();  // the dock's rects as last drawn (the mouse over one doesn't shoot)

        static readonly Rect DockBar = new Rect(16f, 10f, W - 32f, 52f);

        /// Is the mouse over the dock or one of its drawers? (Then a click is the dock's, not a shot.)
        bool MouseOnDock()
        {
            if (!devMenuOpen || dockHit.Count == 0)
                return false;
            Vector3 m = Input.mousePosition;
            var p = new Vector2((m.x - offX) / scale, (UnityEngine.Screen.height - m.y - offY) / scale);
            foreach (var r in dockHit)
                if (r.Contains(p))
                    return true;
            return false;
        }

        /// A drawer's box: the screen colour, a thin rim, a small title. Returns the area inside.
        Rect DockBox(Rect r, string title)
        {
            dockHit.Add(r);
            var ink = CrtUi.Ink;
            Ui.Fill(r, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.97f));
            CrtKit.Frame(r, 1f, new Color(ink.r, ink.g, ink.b, 0.55f));
            Ui.Fill(new Rect(r.x, r.y + 10f, 3f, 20f), CrtStyle.Head);
            Ui.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 22f), title.ToUpperInvariant(), 12, CrtStyle.Head, TextAnchor.MiddleLeft, true);
            return new Rect(r.x + 14f, r.y + 38f, r.width - 28f, r.height - 52f);
        }

        /// A switch with its hotkey drawn as a small keycap on the right.
        bool DockSwitch(Rect r, bool value, string label, string hot = null)
        {
            if (string.IsNullOrEmpty(hot))
                return DkSwitch(r, value, label);
            bool now = DkSwitch(new Rect(r.x, r.y, r.width - 46f, r.height), value, label);
            var kc = new Rect(r.xMax - 40f, r.y + r.height / 2f - 10f, 40f, 20f);
            CrtKit.Frame(kc, 1f, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.5f));
            Ui.Label(kc, hot, 11, CrtStyle.Desc, TextAnchor.MiddleCenter, true);
            return now;
        }

        void CloseDockDrawers()
        {
            devDrawer = 0;
            devSheet = 0;
        }

        /// The screen: the dock bar and whatever is open under it.
        void DrawDevMenu()
        {
            if (Event.current.type == EventType.Repaint)
                dockHit.Clear();
            bool inMatch = session.Screen == AppScreen.Game;
            bool host = session.IsHost;
            if (!inMatch && (devDrawer == 1 || devDrawer == 2 || devSide || devSheet == 1 || devSheet == 3))
            {
                devDrawer = devDrawer == 1 || devDrawer == 2 ? 0 : devDrawer;
                devSide = false;
                devSheet = devSheet == 2 ? 2 : 0;
            }
            if (devSheet != 0)
                devDrawer = 0;
            dockHit.Add(DockBar);
            var ink = CrtUi.Ink;
            Ui.Fill(DockBar, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.96f));
            CrtKit.Frame(DockBar, 1f, new Color(ink.r, ink.g, ink.b, 0.6f));
            const float y = 18f, h = 36f;
            float x = DockBar.x + 12f;

            DkTitle(new Rect(x, y - 4f, 64f, h + 8f), "DEV", 26);
            x += 70f;
            var wr = new Rect(x, y, 124f, h);
            DkSub(wr);
            Ui.Label(wr, inMatch ? "WAVE " + Mathf.Max(1, game.Wave) + (game.Endless ? " ∞" : " / " + Game.MaxWave) : "NO MATCH", 13, Color.white, TextAnchor.MiddleCenter, true);
            x += 136f;

            bool was = GUI.enabled;
            if (inMatch)
            {
                GUI.enabled = was && host;
                bool allGod = game.Players.Count > 0;
                foreach (var p in game.Players.Values)
                    allGod &= p.DevGod;
                if (DkKey(new Rect(x, y, 78f, h), "GOD", CrtStyle.Gold, allGod, 13))
                    foreach (var p in game.Players.Values)
                        game.DevSetGod(p.Id, !allGod);
                x += 84f;
                if (DkKey(new Rect(x, y, 104f, h), "FULL HEAL", Ui.Neutral, false, 13))
                    foreach (var p in game.Players.Values)
                        game.DevHeal(p.Id);
                x += 110f;
                if (DkKey(new Rect(x, y, 104f, h), "SKIP WAVE", Ui.Neutral, false, 13))
                    game.DevJumpToWave(Mathf.Max(1, game.Wave) + 1);
                x += 110f;
                if (DkKey(new Rect(x, y, 90f, h), "KILL ALL", Ui.Danger, false, 13))
                    game.DevKillAll(game.MyId);
                x += 96f;
                GUI.enabled = was;
                Ui.Fill(new Rect(x + 2f, y + 4f, 1f, h - 8f), new Color(ink.r, ink.g, ink.b, 0.35f));
                x += 14f;
            }
            string[] names = { "WAVE", "PARTY", "VIEW", "LABS" };
            var keyRects = new Rect[4];
            for (int i = 0; i < 4; i++)
            {
                bool matchOnly = i < 2;
                if (matchOnly && !inMatch)
                    continue;
                keyRects[i] = new Rect(x, y, 86f, h);
                if (DkKey(keyRects[i], names[i], CrtStyle.Head, devDrawer == i + 1, 13))
                {
                    devDrawer = devDrawer == i + 1 ? 0 : i + 1;
                    devSheet = 0;
                }
                x += 92f;
            }

            float rx = DockBar.xMax - 12f;
            rx -= 44f;
            if (DkKey(new Rect(rx, y, 44f, h), "X", Ui.Danger, false, 14))
                devMenuOpen = false;
            rx -= 82f;
            bool rec = recordSessions;
            if (DkKey(new Rect(rx, y, 76f, h), "REC", Ui.Danger, rec, 13))
            {
                recordSessions = !recordSessions;
                PlayerPrefs.SetInt("record_sessions", recordSessions ? 1 : 0);
                PlayerPrefs.Save();
                ApplyRecording();
            }
            rx -= 82f;
            GUI.enabled = was && inMatch;
            if (DkKey(new Rect(rx, y, 76f, h), "BOT", CrtStyle.Gold, game.Bot, 13))
            {
                game.Bot = !game.Bot;
                game.BotLevel = game.Bot ? BotSkill.Good : BotSkill.Off;
            }
            GUI.enabled = was;

            // The open drawer, under its key (kept clear of the stat editor on the right).
            float edge = devSide && inMatch ? W - 16f - 470f - 8f : W - 16f;
            if (devDrawer >= 1 && devDrawer <= 4 && keyRects[devDrawer - 1].width > 0f)
            {
                var kr = keyRects[devDrawer - 1];
                switch (devDrawer)
                {
                    case 1: DrawDockWave(new Rect(Mathf.Min(kr.x, edge - 400f), 70f, 400f, 296f), host); break;
                    case 2: DrawDockParty(new Rect(Mathf.Min(kr.x, edge - 380f), 70f, 380f, 236f), host); break;
                    case 3: DrawDockView(new Rect(Mathf.Min(kr.x, edge - 400f), 70f, 400f, 270f), inMatch); break;
                    default: DrawDockLabs(new Rect(Mathf.Min(kr.x, edge - 340f), 70f, 340f, 300f), inMatch); break;
                }
            }
            if (devSide && inMatch)
                DrawDockStats(new Rect(W - 16f - 470f, 70f, 470f, 548f), host);
            if (devSheet != 0)
                DrawDockSheet(new Rect(W / 2f - 372f, 70f, 744f, 560f), inMatch);
        }

        void DockMessage(Rect inner, string text)
        {
            Ui.Label(new Rect(inner.x, inner.y, inner.width, 60f), text, 13, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
        }

        // --- WAVE ---------------------------------------------------------------------------------------------

        void DrawDockWave(Rect r, bool host)
        {
            var inner = DockBox(r, "Wave");
            var col = DkStack.Column(inner, 8f);
            bool was = GUI.enabled;
            GUI.enabled = was && host;
            int max = game.Endless ? 999 : Game.MaxWave;
            var row = DkStack.Cells(col.Next(34f), 7, 4f);
            int[] steps = { -10, -5, -1, 0, 1, 5, 10 };
            for (int i = 0; i < 7; i++)
            {
                if (steps[i] == 0)
                {
                    DkTitle(row[i], devWave.ToString(), 24, CrtStyle.Gold, TextAnchor.MiddleCenter);
                    continue;
                }
                if (DkKey(row[i], steps[i] > 0 ? "+" + steps[i] : steps[i].ToString(), Ui.Neutral, false, 12))
                    devWave = Mathf.Clamp(devWave + steps[i], 1, max);
            }
            if (DkKey(col.Next(38f), "GO TO WAVE " + devWave, Ui.GoldButton, false, 14))
                game.DevJumpToWave(devWave);
            int bossWave = devWave % 5 == 0 ? devWave : (devWave / 5 + 1) * 5;
            var boss = Game.BossForWave(bossWave);
            Ui.Label(col.Next(18f), (devWave % 5 == 0 ? "Boss on this wave: " : "Next boss (wave " + bossWave + "): ") + PracticePlan.NameOf(boss == EnemyType.TwinB ? EnemyType.TwinA : boss),
                12, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(col.Next(14f), "DATA GATES  (between waves)", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            bool canRoom = host && game.State == MatchState.Intermission && game.Room == RoomPhase.None;
            GUI.enabled = was && canRoom;
            var gates = DkStack.Cells(col.Next(32f), Challenges.All.Length, 4f);
            string[] gateNames = { "Crystal", "Gatekeeper", "Kill Rush", "Zones", "Mirror" };
            for (int i = 0; i < Challenges.All.Length; i++)
                if (DkKey(gates[i], gateNames[i], ToColor(Challenges.Accent(Challenges.All[i])), false, 10))
                    game.DevOpenPortal(Challenges.All[i], true);
            GUI.enabled = was && host;
            bool freeze = DkSwitch(col.Next(26f), game.DevFreeze, "Freeze enemies (still killable)");
            if (freeze != game.DevFreeze)
                game.DevSetFreeze(freeze);
            bool killAll = DockSwitch(col.Next(26f), devKillAllKey, "Kill-all key", "X");
            if (killAll != devKillAllKey)
            {
                devKillAllKey = killAll;
                PlayerPrefs.SetInt("dev_killall", killAll ? 1 : 0);
            }
            GUI.enabled = was;
            if (!host)
                DockMessage(new Rect(inner.x, r.yMax - 22f, inner.width, 16f), "Only the host changes the match.");
        }

        // --- PARTY --------------------------------------------------------------------------------------------

        void DrawDockParty(Rect r, bool host)
        {
            var inner = DockBox(r, "Party");
            var col = DkStack.Column(inner, 8f);
            bool was = GUI.enabled;
            GUI.enabled = was && host;
            var a = DkStack.Cells(col.Next(34f), 2, 6f);
            var b = DkStack.Cells(col.Next(34f), 2, 6f);
            if (DkKey(a[0], "LEVEL UP", Ui.Neutral, false, 13))
                foreach (var p in game.Players.Values) game.DevLevelUp(p.Id);
            if (DkKey(a[1], "MAX MASTERY", Ui.Neutral, false, 13))
                foreach (var p in game.Players.Values) game.DevMasterAll(p.Id);
            if (DkKey(b[0], "FULL HEAL", Ui.Neutral, false, 13))
                foreach (var p in game.Players.Values) game.DevHeal(p.Id);
            if (DkKey(b[1], "RESET STATS", Ui.Danger, false, 13))
                foreach (var p in game.Players.Values) game.DevResetStats(p.Id);
            GUI.enabled = was;
            if (DkKey(col.Next(36f), "EDIT STATS…", CrtStyle.Head, devSide, 14))
                devSide = !devSide;
            var prof = Profile.Current;
            Ui.Label(col.Next(16f), "ACCOUNT RANK  ·  " + prof.RankNow + "  ·  " + prof.Xp.ToString("N0") + " XP", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            var rk = DkStack.Cells(col.Next(30f), 5, 4f);
            string[] rn = { "+1", "+10", "30", "MAX", "RESET" };
            for (int i = 0; i < 5; i++)
                if (DkKey(rk[i], rn[i], i == 4 ? Ui.Danger : i == 3 ? Ui.GoldButton : Ui.Neutral, false, 11))
                    DevSetRank(i == 0 ? prof.RankNow + 1 : i == 1 ? prof.RankNow + 10 : i == 2 ? 30 : i == 3 ? Rank.MaxRank : 1);
        }

        // --- VIEW ---------------------------------------------------------------------------------------------

        void DrawDockView(Rect r, bool inMatch)
        {
            var inner = DockBox(r, "View and helpers");
            var col = DkStack.Column(inner, 6f);
            aimDebug = DockSwitch(col.Next(28f), aimDebug, "Aim debug overlay", "F3");
            dpsMeter = DockSwitch(col.Next(28f), dpsMeter, "DPS meter on screen", "F2");
            bool editText = DockSwitch(col.Next(28f), textEditMode, "Edit any text on screen", "F4");
            if (editText != textEditMode)
            {
                SetTextEditMode(editText);
                devMenuOpen = !editText;  // out of the way, so you can click the text you want
            }
            bool pauseKey = DockSwitch(col.Next(28f), devPauseKey, "Freeze-time key (no menu)", "P");
            if (pauseKey != devPauseKey)
            {
                devPauseKey = pauseKey;
                PlayerPrefs.SetInt("dev_pausekey", pauseKey ? 1 : 0);
            }
            bool was = GUI.enabled;
            GUI.enabled = was && inMatch;
            bool bot = DkSwitch(col.Next(28f), game.Bot, "Bot plays for me");
            if (bot != game.Bot)
            {
                game.Bot = bot;
                game.BotLevel = bot ? BotSkill.Good : BotSkill.Off;  // the Boss Lab's smart bot; the sim tests keep the simple one
            }
            col.Space(2f);
            if (DkKey(col.Next(34f), "DPS DETAILS AND TARGET DUMMIES…", CrtStyle.Head, false, 12))
            {
                devSheet = 1;
                devDrawer = 0;
            }
            GUI.enabled = was;
        }

        // --- LABS ---------------------------------------------------------------------------------------------

        void DrawDockLabs(Rect r, bool inMatch)
        {
            var inner = DockBox(r, "Labs");
            var col = DkStack.Column(inner, 6f);
            if (DkKey(col.Next(36f), "BOSS LAB", CrtStyle.Gold, false, 14))
            {
                devMenuOpen = false;
                labSetupOpen = true;
            }
            if (DkKey(col.Next(36f), "WAVE EDITOR", EdColor, false, 14))
                OpenWaveEditor();
            bool was = GUI.enabled;
            GUI.enabled = was && inMatch;
            if (DkKey(col.Next(36f), "BALANCE TEST…", CrtStyle.Head, false, 14))
            {
                devSheet = 3;
                devDrawer = 0;
            }
            GUI.enabled = was;
            if (DkKey(col.Next(36f), "REPLAYS…", CrtStyle.Head, false, 14))
            {
                devSheet = 2;
                devDrawer = 0;
            }
            col.Space(4f);
            bool on = DkSwitch(col.Next(26f), recordSessions, "Record sessions (when hosting)");
            if (on != recordSessions)
            {
                recordSessions = on;
                PlayerPrefs.SetInt("record_sessions", on ? 1 : 0);
                PlayerPrefs.Save();
                ApplyRecording();
            }
            var f = DkStack.Cells(col.Next(30f), 2, 6f);
            if (DkKey(f[0], "SESSIONS FOLDER", Ui.Neutral, false, 10))
                OpenFolder(SessionsFolder);
            if (DkKey(f[1], "PRACTICE FOLDER", Ui.Neutral, false, 10))
                OpenFolder(PracticeFolder);
        }

        // --- Stat editor and the big sheets ------------------------------------------------------------------------

        void DrawDockStats(Rect r, bool host)
        {
            var inner = DockBox(r, "Stat editor");
            if (DkKey(new Rect(r.xMax - 40f, r.y + 6f, 30f, 22f), "x", Ui.Neutral, false, 11))
                devSide = false;
            DrawDevPlayers(inner.x, inner.y, inner.width, inner.yMax);
        }

        void DrawDockSheet(Rect r, bool inMatch)
        {
            string title = devSheet == 1 ? "DPS meter and target dummies" : devSheet == 2 ? "Replays" : "Balance test";
            var inner = DockBox(r, title);
            if (DkKey(new Rect(r.xMax - 40f, r.y + 6f, 30f, 22f), "x", Ui.Neutral, false, 11))
                devSheet = 0;
            if (devSheet == 1)
            {
                if (inMatch) DrawDevDps(inner); else DevNotInMatch(inner, "use the DPS meter and dummies");
            }
            else if (devSheet == 2)
                DrawDevReplays(inner);  // ReplayUi.cs
            else if (inMatch)
                DrawDevBalance(inner);  // BalanceTestUi.cs
            else
                DevNotInMatch(inner, "run a balance test");
        }
    }
}
