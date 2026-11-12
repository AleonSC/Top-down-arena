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
        int devSheet;             // 0 none, 1 DPS meter and dummies, 2 Replays, 3 Balance test, 4 Spawn an upgrade, 5 Spawn an enemy
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
            if (!inMatch && (devDrawer == 1 || devDrawer == 2 || devSide || devSheet == 1 || devSheet == 3 || devSheet == 4 || devSheet == 5))
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
            Ui.Label(wr, inMatch ? "WAVE " + Mathf.Max(1, game.ShownWave) + (game.Endless ? " ∞" : " / " + game.ShownLastWave) : "NO MATCH", 13, Color.white, TextAnchor.MiddleCenter, true);
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
                {
                    if (game.BossNext && game.Wave > 0)
                        game.DevJumpToWave(game.Wave, true);  // the boss comes after every fifth wave
                    else
                        game.DevJumpToWave(Mathf.Max(1, game.Wave) + 1);
                }
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
                    case 1: DrawDockWave(new Rect(Mathf.Min(kr.x, edge - 400f), 70f, 400f, 340f), host); break;
                    case 2: DrawDockParty(new Rect(Mathf.Min(kr.x, edge - 380f), 70f, 380f, 282f), host); break;
                    case 3: DrawDockView(new Rect(Mathf.Min(kr.x, edge - 400f), 70f, 400f, 270f), inMatch); break;
                    default: DrawDockLabs(new Rect(Mathf.Min(kr.x, edge - 340f), 70f, 340f, 342f), inMatch); break;
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
            int bossWave = game.Bosses.NextBossWaveFrom(devWave);
            var boss = game.BossAt(bossWave);
            Ui.Label(col.Next(18f), "Next boss (after wave " + bossWave + "): " + PracticePlan.NameOf(boss == EnemyType.TwinB ? EnemyType.TwinA : boss),
                12, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            if (DkKey(col.Next(30f), "GO TO THE BOSS AFTER WAVE " + bossWave, Ui.Neutral, false, 12))
                game.DevJumpToWave(Mathf.Min(bossWave, max), true);
            if (DkKey(col.Next(34f), "SPAWN AN ENEMY…", CrtStyle.Head, false, 14))
            {
                devSheet = 5;
                devDrawer = 0;
            }
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
            if (DkKey(col.Next(36f), "SPAWN AN UPGRADE…", CrtStyle.Head, false, 14))
            {
                devSheet = 4;
                devDrawer = 0;
            }
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
            bool wasArena = GUI.enabled;
            GUI.enabled = wasArena && (session.IsHost || session.Screen == AppScreen.Menu);
            if (DkKey(col.Next(36f), game.DevArena ? "LEAVE DEV ARENA" : "DEV ARENA (PLAY AS ENEMIES)", CrtStyle.Gold, game.DevArena, 13))
                ToggleDevArena();  // DevArenaUi.cs
            GUI.enabled = wasArena;
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
            if (!inMatch && session.IsHost)  // the Rooms arena prototype (Rooms.cs): kept for building generated layouts later
            {
                bool rooms = session.Diff[DiffKnob.ArenaRooms] >= 0.5f;
                if (DkSwitch(col.Next(26f), rooms, "Rooms arena prototype (next runs)") != rooms)
                    session.SetDifficultyKnob(DiffKnob.ArenaRooms, rooms ? 0f : 1f);
            }
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
            string title = devSheet == 1 ? "DPS meter and target dummies" : devSheet == 2 ? "Replays" : devSheet == 4 ? "Spawn an upgrade" : devSheet == 5 ? (game.DevArena ? "Dev Arena: spawn or play as an enemy" : "Spawn an enemy") : "Balance test";
            var inner = DockBox(r, title);
            if (DkKey(new Rect(r.xMax - 40f, r.y + 6f, 30f, 22f), "x", Ui.Neutral, false, 11))
                devSheet = 0;
            if (devSheet == 1)
            {
                if (inMatch) DrawDevDps(inner); else DevNotInMatch(inner, "use the DPS meter and dummies");
            }
            else if (devSheet == 2)
                DrawDevReplays(inner);  // ReplayUi.cs
            else if (devSheet == 4)
            {
                if (inMatch) DrawDockUpgrades(inner); else DevNotInMatch(inner, "spawn an upgrade");
            }
            else if (devSheet == 5)
            {
                if (inMatch) DrawDockEnemies(inner); else DevNotInMatch(inner, "spawn an enemy");
            }
            else if (inMatch)
                DrawDevBalance(inner);  // BalanceTestUi.cs
            else
                DevNotInMatch(inner, "run a balance test");
        }

        // --- Spawn an upgrade ---------------------------------------------------------------------------------

        int devUpRarity = -1;     // -1 every rarity
        string devUpFilter = "";
        float devUpScroll;
        int devUpPlayer;          // whose build the list is for (player id)
        string devUpNote = "";

        /// Every upgrade, filtered, as the chest's item for the chosen player: click one and it drops in front of them. Greyed ones
        /// can't be offered to that build (another class, a missing ability or weapon, taken the most times).
        void DrawDockUpgrades(Rect r)
        {
            bool host = session.IsHost;
            var players = new List<Player>(game.Players.Values);
            players.Sort((a, b) => a.Id.CompareTo(b.Id));
            Player who = players.Find(q => q.Id == devUpPlayer) ?? game.Me ?? (players.Count > 0 ? players[0] : null);
            if (who == null)
                return;
            devUpPlayer = who.Id;
            var col = DkStack.Column(r, 8f);
            var top = col.Next(30f);
            devUpFilter = DkField(new Rect(top.x, top.y, 240f, 30f), devUpFilter, 24);
            if (devUpFilter.Length == 0)
                Ui.Label(new Rect(top.x + 10f, top.y, 220f, 30f), "Filter by name or text", 13, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
            float px = top.x + 256f;
            if (players.Count > 1)
                for (int i = 0; i < players.Count; i++)
                {
                    var q = players[i];
                    float pw = Mathf.Min(150f, (top.xMax - px - 6f * (players.Count - 1)) / players.Count);
                    if (DkKey(new Rect(px, top.y, pw, 30f), q.Name + (q.Id == game.MyId ? " (you)" : ""), ToColor(q.Def.Color), q.Id == who.Id, 12))
                        devUpPlayer = q.Id;
                    px += pw + 6f;
                }
            else
                Ui.Label(new Rect(px, top.y, top.xMax - px, 30f), "for " + who.Name + "  ·  " + who.Def.Name, 13, CrtStyle.Desc, TextAnchor.MiddleLeft, true);

            var chips = DkStack.Cells(col.Next(28f), 8, 4f);
            string[] names = { "ALL", "COMMON", "UNCOMMON", "RARE", "VERY RARE", "LEGENDARY", "MOD", "MILESTONE" };
            Rarity[] rars = { Rarity.Common, Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.VeryRare, Rarity.Legendary, Rarity.Mod, Rarity.Milestone };
            for (int i = 0; i < 8; i++)
                if (DkKey(chips[i], names[i], i == 0 ? Ui.Neutral : ToColor(Upgrades.RarityColor(rars[i])), devUpRarity == i - 1, 10))
                {
                    devUpRarity = i - 1;
                    devUpScroll = 0f;
                }

            var ids = new List<int>();
            string f = devUpFilter.Trim();
            for (int i = 0; i < Upgrades.All.Length; i++)
            {
                var u = Upgrades.All[i];
                if (u.Unoffered || devUpRarity >= 0 && (int)u.Rarity != devUpRarity)
                    continue;
                if (f.Length > 0 && (u.Name + " " + u.Desc).IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                ids.Add(i);
            }
            ids.Sort((a, b) =>
            {
                int c = Upgrades.All[a].Rarity.CompareTo(Upgrades.All[b].Rarity);
                return c != 0 ? c : string.Compare(Upgrades.All[a].Name, Upgrades.All[b].Name, StringComparison.Ordinal);
            });
            var list = col.Rest();
            list.height -= 26f;
            DkSub(list);
            const float row = 34f;
            float maxScroll = Mathf.Max(0f, ids.Count * row - list.height + 6f);
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && list.Contains(ev.mousePosition))
            {
                devUpScroll = Mathf.Clamp(devUpScroll + ev.delta.y * 24f, 0f, maxScroll);
                ev.Use();
            }
            devUpScroll = Mathf.Clamp(devUpScroll, 0f, maxScroll);
            GUI.BeginGroup(list);
            bool was = GUI.enabled;
            for (int k = 0; k < ids.Count; k++)
            {
                float ry = 3f + k * row - devUpScroll;
                if (ry < -row || ry > list.height)
                    continue;
                int id = ids[k];
                var u = Upgrades.All[id];
                bool ok = host && Upgrades.Available(id, who.Role, who.RoleAbility, who.Taken, who.Weapon.Id);
                Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
                var rr = new Rect(4f, ry, list.width - 12f, row - 2f);
                bool hover = ok && rr.Contains(Event.current.mousePosition);
                if (hover)
                    Ui.Fill(rr, new Color(rc.r, rc.g, rc.b, 0.16f));
                Ui.Fill(new Rect(rr.x, rr.y + 4f, 3f, rr.height - 8f), new Color(rc.r, rc.g, rc.b, ok ? 1f : 0.35f));
                float a = ok ? 1f : 0.45f;
                Ui.Label(new Rect(rr.x + 12f, rr.y, 190f, rr.height), u.Name, 14, new Color(rc.r, rc.g, rc.b, a), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(rr.x + 206f, rr.y, rr.width - 310f, rr.height), u.Desc, 11, new Color(CrtStyle.Desc.r, CrtStyle.Desc.g, CrtStyle.Desc.b, a), TextAnchor.MiddleLeft);
                string tag = !ok ? (host ? "not for this build" : "host only") : Upgrades.RarityName(u.Rarity);
                Ui.Label(new Rect(rr.xMax - 96f, rr.y, 92f, rr.height), tag, 10, new Color(rc.r, rc.g, rc.b, a), TextAnchor.MiddleRight, true);
                if (ok && Ui.HitArea(rr) && game.DevSpawnLoot(who.Id, id))
                    devUpNote = "Dropped " + u.Name + " in front of " + (who.Id == game.MyId ? "you" : who.Name) + ": walk onto it, G takes it, X throws it away.";
            }
            GUI.enabled = was;
            GUI.EndGroup();
            if (ids.Count == 0)
                Ui.Label(new Rect(list.x + 12f, list.y + 10f, list.width - 24f, 24f), "Nothing matches.", 13, CrtStyle.Desc);
            Ui.Label(new Rect(r.x, r.yMax - 20f, r.width, 18f), devUpNote.Length > 0 ? devUpNote : ids.Count + " upgrades  ·  click one to drop it like a chest's item (host only; marks the run as a dev run)",
                11, devUpNote.Length > 0 ? CrtStyle.Gold : CrtStyle.Desc);
        }

        // --- Spawn an enemy -----------------------------------------------------------------------------------

        int devEnemyTab;          // 0 enemies, 1 bosses
        int devEnemyCount = 1;
        bool devEnemyElite;
        float devEnemyScroll;
        string devEnemyFilter = "";
        string devEnemyNote = "";

        /// Every enemy (or boss) with its live model: click one to put it in front of you (several with the count keys, tougher with
        /// Elite). Needs a wave under way, and the host.
        void DrawDockEnemies(Rect r)
        {
            bool host = session.IsHost, fighting = game.State == MatchState.Fighting;
            var col = DkStack.Column(r, 8f);
            var top = col.Next(30f);
            string[] tabs = { "ENEMIES", "BOSSES" };
            for (int i = 0; i < 2; i++)
                if (DkChoice(new Rect(top.x + i * 92f, top.y, 88f, 30f), tabs[i], devEnemyTab == i, CrtStyle.Head))
                {
                    devEnemyTab = i;
                    devEnemyScroll = 0f;
                }
            devEnemyFilter = DkField(new Rect(top.x + 196f, top.y, 190f, 30f), devEnemyFilter, 20);
            if (devEnemyFilter.Length == 0)
                Ui.Label(new Rect(top.x + 206f, top.y, 170f, 30f), "Filter by name", 13, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
            float cx = top.x + 400f;
            Ui.Label(new Rect(cx, top.y, 50f, 30f), "COUNT", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            int[] counts = { 1, 3, 5, 10 };
            for (int i = 0; i < counts.Length; i++)
                if (DkChoice(new Rect(cx + 50f + i * 38f, top.y, 34f, 30f), counts[i].ToString(), devEnemyCount == counts[i], CrtStyle.Gold))
                    devEnemyCount = counts[i];
            devEnemyElite = DkSwitch(new Rect(cx + 212f, top.y, top.xMax - cx - 212f, 30f), devEnemyElite, "Elite");

            var all = EdPaletteEnemies(devEnemyTab == 1);
            var list = new List<EnemyType>();
            string f = devEnemyFilter.Trim();
            foreach (var t in all)
                if (f.Length == 0 || PracticePlan.NameOf(t).IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                    list.Add(t);
            if (game.DevArena)
            {
                DrawDevArenaRow(col.Next(32f));  // Spawn / Play as, Let go, Clear, Leave (DevArenaUi.cs)
                DrawDevArenaDummyRow(col.Next(30f));  // hero dummies, Can't die
            }
            var area = col.Rest();
            area.height -= 26f;
            DkSub(area);
            const int cols = 9;
            float gap = 4f, cw = (area.width - 12f - gap * (cols - 1)) / cols, chh = 78f;
            int rowsAll = (list.Count + cols - 1) / cols;
            int rowsVisible = Mathf.Max(1, Mathf.FloorToInt((area.height - 8f + gap) / (chh + gap)));
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && area.Contains(ev.mousePosition))
            {
                devEnemyScroll += ev.delta.y > 0 ? 1 : -1;
                ev.Use();
            }
            devEnemyScroll = Mathf.Clamp(devEnemyScroll, 0, Mathf.Max(0, rowsAll - rowsVisible));
            int first = (int)devEnemyScroll * cols;
            var cells = new List<KeyValuePair<EnemyType, Rect>>();
            bool canSpawn = host && fighting;
            for (int i = first; i < list.Count && i < first + rowsVisible * cols; i++)
            {
                int k = i - first;
                var cell = new Rect(area.x + 6f + (k % cols) * (cw + gap), area.y + 4f + (k / cols) * (chh + gap), cw, chh);
                bool hover = canSpawn && cell.Contains(ev.mousePosition);
                bool boss = Defs.Get(list[i]).IsBoss;
                Color tint = boss ? CrtStyle.Danger : Approaches.Of(list[i]) != null ? CrtStyle.Gold : CrtUi.Ink;
                Ui.Fill(cell, new Color(tint.r, tint.g, tint.b, hover ? 0.16f : 0.05f));
                CrtKit.Frame(cell, 1f, new Color(tint.r, tint.g, tint.b, hover ? 0.8f : 0.25f));
                Ui.Label(new Rect(cell.x, cell.yMax - 15f, cell.width, 13f), PracticePlan.NameOf(list[i]), 9, canSpawn ? CrtStyle.Body : CrtStyle.Desc, TextAnchor.MiddleCenter);
                cells.Add(new KeyValuePair<EnemyType, Rect>(list[i], cell));
                bool hitCell = canSpawn && Ui.HitArea(cell);
                if (hitCell && game.DevArena && devPlayAs)
                {
                    if (game.DevPossess(game.MyId, list[i]))
                        devMenuOpen = false;  // out of the way: you're it now (DevArenaUi.cs)
                    else
                        devEnemyNote = "Couldn't play as it.";
                }
                else if (hitCell)
                {
                    int n = game.DevSpawnEnemy(game.MyId, list[i], boss ? 1 : devEnemyCount, devEnemyElite && !boss);
                    devEnemyNote = n > 0 ? "Spawned " + (n > 1 ? n + " " : "") + PracticeEnemyName(list[i]) + " in front of you." : "Couldn't spawn it.";
                }
            }
            if (BeginUiGl())
            {
                foreach (var kv in cells)
                {
                    UiClip(kv.Value);
                    DrawEnemyPreview(kv.Key, new V2(kv.Value.center.x, kv.Value.y + 30f), 40f, Time.unscaledTime, kv.Value.Contains(ev.mousePosition));
                }
                EndUiGl();
            }
            if (rowsAll > rowsVisible)
                Ui.Label(new Rect(area.xMax - 110f, area.yMax + 6f, 104f, 16f), "scroll " + ((int)devEnemyScroll + 1) + "/" + (rowsAll - rowsVisible + 1), 10, CrtStyle.Desc, TextAnchor.MiddleRight);
            string idle = !host ? "Only the host can spawn enemies." : !fighting ? "Start a wave first: enemies can only be spawned while one is under way." : list.Count + " " + (devEnemyTab == 1 ? "bosses" : "enemies") + (game.DevArena && devPlayAs ? "  ·  click one to play as it" : "  ·  click one to put it in front of you (marks the run as a dev run)");
            Ui.Label(new Rect(r.x, r.yMax - 20f, r.width - 120f, 18f), devEnemyNote.Length > 0 && canSpawn ? devEnemyNote : idle, 11, devEnemyNote.Length > 0 && canSpawn ? CrtStyle.Gold : CrtStyle.Desc);
        }

        static string PracticeEnemyName(EnemyType t) => PracticePlan.NameOf(t);
    }
}