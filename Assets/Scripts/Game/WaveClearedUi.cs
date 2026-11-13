using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The "WAVE n CLEARED" box between waves, in the lobby's CRT (look A, "One tube", of Docs/Mockups/crt-wave-cleared.html): one CRT
    /// tube where the old card was, with the title and the wave's two numbers, a row per player (kills, damage, damage taken, award
    /// tags and a ready mark), a Squad row, the next wave's enemy types and the road to the next boss, and along the bottom the Ready
    /// key, the Auto-ready switch, the status and the countdown. Colours come from CrtStyle.cs (they follow the CRT colour setting);
    /// the frame from RoundWindows.cs. Core side: Core/WaveReady.cs.
    public partial class ArenaApp
    {
        const float WcW = 940f, WcH = 454f, WcY = 96f;
        int wcStep;                   // 0 Results, 1 Build, 2 Ready: the break runs in that order (the tabs along the tube's top)
        bool wcSawWindow;             // the build window was open last frame (closing it with Done moves on to Ready)
        int autoReadySent = -1;       // the Auto-ready choice the host was last told (-1: not yet this run)
        float wcBorn = -1f;           // unscaled time the box appeared (for its drop-in)
        int wcWave = -1;
        bool wcMin;                   // minimized to a strip at the top so you can walk to a shop or a chest (until the next break)
        Rect wcStripHit;              // the strip's rect as last drawn (a click on it isn't a shot)

        static bool AutoReadyPref
        {
            get => PlayerPrefs.GetInt("auto_ready", 0) == 1;
            set { PlayerPrefs.SetInt("auto_ready", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// Each frame: tells the host the saved Auto-ready choice, and turns the Ready key into a press.
        void TickWaveReady(Player me)
        {
            if (session.Screen != AppScreen.Game || me == null)
            {
                autoReadySent = -1;
                return;
            }
            TickWcSteps();
            int pref = AutoReadyPref && !WcHoldsAuto(me) ? 1 : 0;  // Auto-ready only counts once you reach the Ready step
            if (autoReadySent != pref)
            {
                autoReadySent = pref;
                game.SetMyAutoReady(pref == 1);
            }
            if (WaveBoxUp(me) && GUIUtility.keyboardControl == 0 && Keybinds.Down(KeyAction.ReadyUp))
            {
                if (game.GateUp && (wcMin || WcStepNow() == 2))
                {
                    wcStep = 2;
                    wcMin = true;  // before a boss the key folds the box away so you can walk to the boss gate (Core/BossGate.cs)
                }
                else if (wcMin)
                {
                    wcStep = 2;  // minimized: the key readies at once (you saw the results, the build window has its chip)
                    PressReady(me);
                }
                else if (WcStepNow() == 2)
                    PressReady(me);
                else
                    WcAdvance();  // the Ready key walks Results -> Build -> Ready first
            }
        }

        // --- The three steps: Results, then Build (when a building round is open), then Ready ----------------------

        bool WcBuildAvail => game.ExpandOpen && game.Me != null;

        /// The step shown: Build is skipped when there is no building round (Practice, hunts, the last wave, a full map, other arenas).
        int WcStepNow() => wcStep == 1 && !WcBuildAvail ? 2 : wcStep;

        /// Auto-ready is held back while you are still on Results or Build (the countdown starts when you reach Ready).
        bool WcHoldsAuto(Player me) => game.State == MatchState.Intermission && (WaveBoxUp(me) || ExpandWindowUp) && WcStepNow() < 2;

        void TickWcSteps()
        {
            if (game.State != MatchState.Intermission)
            {
                wcStep = 0;
                wcSawWindow = false;
                wcMin = false;
                return;
            }
            if (expOpen)
                wcSawWindow = true;
            else if (wcSawWindow)
            {
                wcSawWindow = false;
                if (wcStep == 1)
                    wcStep = 2;  // Done (or Decide later) in the build window leads to Ready
            }
        }

        /// Results -> Build (opens the build window) -> Ready.
        void WcAdvance()
        {
            if (WcStepNow() == 0 && WcBuildAvail)
            {
                wcStep = 1;
                expOpen = true;
            }
            else
                wcStep = 2;
        }
        void PressReady(Player me)
        {
            if (me.AutoReady)
                return;
            game.SetMyReady(!me.Ready);
        }

        /// The box shows between waves once this wave's numbers have arrived and nothing else (a path, the shop, a portal, your own picks) has the screen.
        bool WaveBoxUp(Player me) =>
            me != null && game.State == MatchState.Intermission && game.Wave > 0 && game.WaveReport != null && game.WaveReport.Wave == game.Wave
            && me.Offer == null && game.PathOffer == null && !game.HuntMapVoting && !ExpandWindowUp && !game.ShopBlocksWave && game.Room != RoomPhase.Portal && game.Practice == null && !BeamShowing;  // (the Practice Range has no ready-up; the boss beam-in: BossBeamLook.cs)

        /// The full box is on screen (not minimized): it takes the mouse, and the build chip waits behind it.
        bool WaveBoxShown(Player me) => WaveBoxUp(me) && !wcMin;

        /// The mouse is over the minimized strip (a click there is the strip's, not a shot).
        bool MouseOnWcStrip(Player me)
        {
            if (!wcMin || !WaveBoxUp(me) || wcStripHit.width <= 0f)
                return false;
            Vector3 m = Input.mousePosition;
            return wcStripHit.Contains(new Vector2((m.x - offX) / scale, (UnityEngine.Screen.height - m.y - offY) / scale));
        }

        /// Draws the box; false when it isn't up (the plain banner is drawn instead).
        bool DrawWaveCleared(Player me, string waiting)
        {
            if (!WaveBoxUp(me) || LaunchHoldsBanner)
            {
                wcBorn = -1f;
                return false;
            }
            float now = Time.unscaledTime;
            if (wcBorn < 0f || wcWave != game.Wave)
            {
                wcBorn = now;
                wcWave = game.Wave;
            }
            var rep = game.WaveReport;
            bool calm = CabCalm;
            if (wcMin)
            {
                DrawWcStrip(me, waiting, calm);
                return true;
            }
            // Drops in over a third of a second (at once with Reduced effects), over a light dark layer.
            float t = calm ? 1f : Mathf.Clamp01((now - wcBorn) / 0.35f);
            float ease = 1f - (1f - t) * (1f - t);
            RoundBackdrop(WaveClearedDark * ease);
            var r = new Rect(W / 2f - WcW / 2f, WcY - 26f * (1f - ease), WcW, WcH);
            RoundTube(r, ease, false, () => DrawWaveClearedBox(me, r, rep, waiting, calm));  // a CRT tube, fading in (RoundWindows.cs)
            return true;
        }

        // Inside the tube: the tabs along the top, then the step: the tally takes the left 596 px, a panel the right 278, the keys the bottom 72.
        const float WcPad = 22f, WcTallyW = 596f, WcRowH = 52f, WcRowStep = 58f, WcSquadH = 38f, WcTabH = 40f;

        /// The box itself, at `r`.
        void DrawWaveClearedBox(Player me, Rect r, WaveSummary rep, string waiting, bool calm)
        {
            int step = WcStepNow();
            DrawWcTabs(r, step, calm);
            float x = r.x, y = r.y + WcTabH + 4f;
            Color ink = CrtUi.Ink, dim = CrtUi.Dim;
            bool boss = game.BossNow;
            if (step == 0)
            {
                // Head: WAVE n CLEARED (gold after a boss) with the blinking cursor, and the wave's two numbers.
                string title = boss && game.Practice == null ? "BOSS CLEARED" : (game.Practice != null ? "PRACTICE WAVE " : "WAVE ") + game.ShownWave + " CLEARED";
                Color tc = boss ? CrtStyle.Gold : ink;
                DrawWcHead(x, y, title, tc, calm);
                if (boss && game.LivesMax > 0)  // the boss is down: the run's checkpoint is here now (Core/Checkpoints.cs)
                    HudCrt(new Rect(x + WcPad + 2f, y + 62f, 400f, 18f), "CHECKPOINT SAVED  ·  " + (game.LivesLeft == 1 ? "1 LIFE" : game.LivesLeft + " LIVES") + " LEFT", 16, CrtStyle.Gold);
                WcTile(new Rect(x + 646f, y + 16f, 130f, 50f), FormatClear(rep.ClearTime), "CLEAR TIME", calm);
                WcTile(new Rect(x + 786f, y + 16f, 132f, 50f), "+" + rep.ScoreGain, "SCORE", calm);
                DrawWcTally(x, y, rep, calm, false);
                if (WcBuildAvail)
                    DrawWcBuildPanel(me, new Rect(x + 640f, y + 84f, 278f, 236f), calm);
                else
                    DrawNextPanel(new Rect(x + 640f, y + 84f, 278f, 236f));
                var bar = new Rect(x + WcPad, y + 338f, WcW - 2f * WcPad, 58f);
                HudCrt(new Rect(bar.x, bar.y, 460f, 48f), WcBuildAvail ? "NEXT: BUILD YOUR ARENA" : "NEXT: GET READY", 22, dim);
                if (LobbyKit.Key(new Rect(bar.xMax - 330f, bar.y, 330f, 48f), WcBuildAvail ? "CONTINUE · BUILD" : "CONTINUE · READY", false, calm, size: 24, minSize: 14))
                    WcAdvance();
            }
            else if (step == 1)
            {
                DrawWcHead(x, y, "BUILD THE ARENA", CrtStyle.Gold, calm);
                DrawWcBuildPanel(me, new Rect(x + WcPad, y + 84f, WcW - 2f * WcPad, 236f), calm);
                var bar = new Rect(x + WcPad, y + 338f, WcW - 2f * WcPad, 58f);
                if (LobbyKit.Key(new Rect(bar.x, bar.y, 330f, 48f), "OPEN THE BUILD WINDOW", false, calm, size: 22, minSize: 14))
                    expOpen = true;
                if (LobbyKit.Key(new Rect(bar.xMax - 330f, bar.y, 330f, 48f), "DONE · READY", false, calm, size: 24, minSize: 14))
                    wcStep = 2;
            }
            else
            {
                DrawWcHead(x, y, boss || game.GateUp ? "NEXT: THE BOSS" : "NEXT: WAVE " + (game.Hunt != null ? game.Wave + 1 : game.ShownWave + 1), ink, calm);
                DrawWcTally(x, y, rep, calm, !game.GateUp);  // (no ready marks before a boss: the gate decides)
                DrawNextPanel(new Rect(x + 640f, y + 84f, 278f, 236f));
                DrawReadyBar(me, new Rect(x + WcPad, y + 338f, WcW - 2f * WcPad, 58f), waiting, calm);
            }
        }

        void DrawWcHead(float x, float y, string title, Color tc, bool calm)
        {
            CrtUi.Title(new Rect(x + WcPad, y + 12f, 600f, 56f), title, 52, tc, TextAnchor.MiddleLeft, calm);
            if (calm || Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0)
                Ui.Fill(new Rect(x + WcPad + CrtUi.TitleWidth(title, 52) + 6f, y + 27f, 14f, 28f), tc);
        }

        /// RESULTS / BUILD / READY along the top of the tube: the step you are on is lit, finished ones carry a tick. Build is left out when there is no round.
        void DrawWcTabs(Rect r, int step, bool calm)
        {
            string last = game.GateUp ? "GATE" : "READY";
            var names = WcBuildAvail ? new[] { "RESULTS", "BUILD", last } : new[] { "RESULTS", last };
            var ids = WcBuildAvail ? new[] { 0, 1, 2 } : new[] { 0, 2 };
            float tw = 200f, gap = 10f, x = r.x + WcPad;
            for (int i = 0; i < names.Length; i++)
            {
                int id = ids[i];
                string label = names[i];
                if (CrtUi.SoftKey(new Rect(x + i * (tw + gap), r.y + 10f, tw, 34f), label, null, 22, calm, lit: id == step) && id != step)
                    wcStep = id;
            }
            // Minimize (top right): the box folds into a strip at the top so you can walk to a shop or a chest.
            if (CrtUi.SoftKey(new Rect(r.xMax - WcPad - 160f, r.y + 10f, 160f, 34f), "MINIMIZE  _", null, 20, calm))
                wcMin = true;
            Ui.Fill(new Rect(r.x + WcPad, r.y + WcTabH + 6f, r.width - 2f * WcPad, 1f), new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.3f));
        }

        /// The minimized box: a slim tube at the top (under the build or door chip when one is up) with the next wave, the status, Ready and Open.
        void DrawWcStrip(Player me, string waiting, bool calm)
        {
            float y = ExpandChipUp ? 88f : DoorChipUp ? 72f : 34f;  // (under the build chip or the door chip)
            var r = new Rect(W / 2f - 330f, y, 660f, 60f);
            wcStripHit = r;
            RoundTube(r, 1f, false, () =>
            {
                bool boss = game.BossNow;
                string next = boss || game.GateUp ? "NEXT: THE BOSS" : "NEXT: WAVE " + (game.Hunt != null ? game.Wave + 1 : game.ShownWave + 1);
                HudCrt(new Rect(r.x + 16f, r.y + 6f, 250f, 26f), next, 24, CrtUi.Ink);
                string st;
                if (game.GateUp)
                {
                    int alive, inside;
                    game.GateCount(out alive, out inside);
                    st = inside + " / " + alive + " IN THE GATE";
                }
                else if (me.AutoReady || game.AllAutoReady() || game.AllReady())
                    st = "STARTS IN " + Mathf.CeilToInt(game.StateTimer);
                else if (!string.IsNullOrEmpty(waiting) && waiting.StartsWith("Waiting for"))
                    st = waiting.ToUpperInvariant().Replace("…", "...");
                else
                    st = me.Ready ? "WAITING FOR THE SQUAD" : "NO TIMER YET";
                bool was = Ui.CrtFont;
                Ui.CrtFont = true;
                st = LobbyKit.Truncate(st, 16, 250f, false);
                Ui.CrtFont = was;
                HudCrt(new Rect(r.x + 16f, r.y + 32f, 250f, 20f), st, 16, CrtUi.Dim);
                var rb = new Rect(r.x + 276f, r.y + 8f, 210f, 44f);
                string key = Keybinds.KeyName(Keybinds.Get(KeyAction.ReadyUp)).ToUpperInvariant();
                if (game.GateUp)
                    LobbyKit.Key(rb, "STAND IN THE GATE", false, calm, size: 20, minSize: 12, locked: true);
                else if (me.AutoReady)
                    LobbyKit.Key(rb, "AUTO-READY", false, calm, size: 20, locked: true);
                else if (LobbyKit.Key(rb, me.Ready ? "READY" : "READY  [" + key + "]", me.Ready, calm, me.Ready ? PixelArt.Get("ui_check") : null, size: 20, minSize: 12,
                             accent: me.Ready ? Ui.Good : (Color?)null))
                {
                    wcStep = 2;
                    PressReady(me);
                }
                if (LobbyKit.Key(new Rect(r.x + 496f, r.y + 8f, 148f, 44f), "OPEN", false, calm, size: 20, minSize: 12))
                    wcMin = false;
            });
        }

        /// The squad's tally (Results) or the same rows with the ready marks (Ready); a Squad total row for more than one player.
        void DrawWcTally(float x, float y, WaveSummary rep, bool calm, bool ready)
        {
            Color ink = CrtUi.Ink, dim = CrtUi.Dim;
            float rx = x + WcPad, ry = y + 84f;
            DrawTallyHeader(rx, ry);
            ry += 20f;
            var ids = new List<int>(game.Players.Keys);
            ids.Sort();
            int kills = 0;
            float dmg = 0f, taken = 0f;
            foreach (int id in ids)
            {
                var p = game.Players[id];
                var line = rep.For(id) ?? new WaveLine { Id = id };
                DrawTallyRow(new Rect(rx, ry, WcTallyW, WcRowH), p, line, calm, ready);
                kills += line.Kills; dmg += line.Damage; taken += line.Taken;
                ry += WcRowStep;
            }
            if (ids.Count > 1)
            {
                var sq = new Rect(rx, ry, WcTallyW, WcSquadH);
                CrtKit.Dashed(sq, 1f, new Color(ink.r, ink.g, ink.b, 0.4f));
                HudCrt(new Rect(sq.x + 10f + WcColName, sq.y, 100f, sq.height), "SQUAD", 26, dim);
                DrawNums(sq, kills, dmg, taken, ink, ink);
            }
        }

        /// Your vectors, the rooms built so far and the kits you hold, in a dashed frame (the Build step; a panel on Results).
        void DrawWcBuildPanel(Player me, Rect r, bool calm)
        {
            var ink = CrtUi.Ink;
            CrtKit.Dashed(r, 1f, new Color(ink.r, ink.g, ink.b, 0.45f));
            float x = r.x + 14f, y = r.y + 12f, w = r.width - 28f;
            HudCrt(new Rect(x, y, w, 18f), "BUILD THE ARENA", 18, CrtUi.Dim);
            y += 26f;
            int vec = game.TeamPurse ? game.SpendableVectors(me) : me.Vectors;
            CrtUi.Title(new Rect(x, y, 90f, 44f), vec.ToString(), 40, CrtStyle.Gold, TextAnchor.MiddleLeft, calm);
            HudCrt(new Rect(x + 96f, y + 10f, w - 96f, 22f), (game.TeamPurse ? "TEAM PURSE" : "VECTORS") + " / " + me.VectorCap, 20, CrtUi.Dim);
            y += 52f;
            float pw = Mathf.Min(w, 25f * 14f);
            int cap = Mathf.Max(1, me.VectorCap);
            for (int i = 0; i < cap; i++)
            {
                float sw = Mathf.Min(11f, (pw - (cap - 1) * 3f) / cap);
                Ui.Fill(new Rect(x + i * (sw + 3f), y, sw, 16f), i < vec ? CrtStyle.Gold : new Color(1f, 1f, 1f, 0.1f));
            }
            y += 30f;
            HudCrt(new Rect(x, y, w, 20f), "ROOMS BUILT  " + game.Expanded.Count, 20, Color.white);
            y += 24f;
            int kits = game.RoomTokens.Count;
            HudCrt(new Rect(x, y, w, 20f), "ROOM KITS  " + kits, 20, kits > 0 ? CrtStyle.Gold : CrtUi.Dim);
            y += 28f;
            string hint = game.FreeBuild ? "Built the moment you place it." : "Draw a room, the team votes.";
            Ui.Label(new Rect(x, y, w, 20f), hint, 12, Ui.Muted, TextAnchor.UpperLeft, true);
        }

        static string FormatClear(float s) => Mathf.FloorToInt(s / 60f) + ":" + Mathf.FloorToInt(s % 60f).ToString("00");

        /// A number tile: the value big in gold, its name small under it, in a faint lit box.
        void WcTile(Rect r, string value, string name, bool calm)
        {
            var ink = CrtUi.Ink;
            Ui.Fill(r, new Color(ink.r, ink.g, ink.b, 0.09f));
            CrtKit.Frame(r, 1f, new Color(ink.r, ink.g, ink.b, 0.45f));
            CrtUi.Title(new Rect(r.x, r.y + 2f, r.width, 30f), value, 28, CrtStyle.Gold, TextAnchor.MiddleCenter, calm);
            Ui.Label(new Rect(r.x, r.yMax - 17f, r.width, 13f), name, 10, CrtUi.Dim, TextAnchor.MiddleCenter, true);
        }

        // Columns inside a row (from 10 px in): the player's dot, name, kills, damage, taken, the award tags, the ready mark at the right.
        const float WcColName = 34f, WcColKills = 143f, WcColDamage = 209f, WcColTaken = 293f, WcColAwards = 363f, WcColAwardsEnd = 534f, WcColTick = 542f;

        void DrawTallyHeader(float x, float y)
        {
            var c = CrtUi.Dim;
            x += 10f;
            HudCrt(new Rect(x + WcColName, y, 80f, 16f), "PLAYER", 15, c);
            HudCrt(new Rect(x + WcColKills, y, 58f, 16f), "KILLS", 15, c, TextAnchor.MiddleRight);
            HudCrt(new Rect(x + WcColDamage, y, 76f, 16f), "DAMAGE", 15, c, TextAnchor.MiddleRight);
            HudCrt(new Rect(x + WcColTaken, y, 62f, 16f), "TAKEN", 15, c, TextAnchor.MiddleRight);
            HudCrt(new Rect(x + WcColAwards, y, 120f, 16f), "AWARDS", 15, c);
        }

        void DrawNums(Rect row, int kills, float damage, float taken, Color numColor, Color takenColor)
        {
            float x = row.x + 10f;
            HudCrt(new Rect(x + WcColKills, row.y, 58f, row.height), kills.ToString(), 26, numColor, TextAnchor.MiddleRight);
            HudCrt(new Rect(x + WcColDamage, row.y, 76f, row.height), Mathf.RoundToInt(damage).ToString("N0"), 26, numColor, TextAnchor.MiddleRight);
            HudCrt(new Rect(x + WcColTaken, row.y, 62f, row.height), Mathf.RoundToInt(taken).ToString("N0"), 26, takenColor, TextAnchor.MiddleRight);
        }

        void DrawTallyRow(Rect row, Player p, WaveLine line, bool calm, bool showReady)
        {
            bool mine = p.Id == game.MyId;
            CrtKit.SoftBox(row, CrtUi.Ink, mine, false, false);  // yours lit, like a selected key
            float x = row.x + 10f, cy = row.center.y;
            Ui.Dot(new Rect(x + 4f, cy - 9f, 18f, 18f), ToColor(p.Def.Color));  // the only colour on the row besides the awards
            float nw = WcColKills - WcColName - 8f;
            Ui.Label(new Rect(x + WcColName, row.y + 8f, nw, 18f), LobbyKit.Truncate(p.Name, 14, nw), 14, Color.white, TextAnchor.MiddleLeft, true);
            string cls = (Defs.KitName(p.Kit) + " " + p.Def.Name).ToUpperInvariant();
            Ui.Label(new Rect(x + WcColName, row.y + 27f, nw, 14f), LobbyKit.Truncate(cls, 9, nw), 9, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            DrawNums(row, line.Kills, line.Damage, line.Taken, Color.white, line.Taken <= 0.5f ? Ui.Good : Color.white);

            // Awards: inverse-video tags (gold, or the ink for the healers' ones), left to right, as many as fit.
            float ax = x + WcColAwards, limit = x + WcColAwardsEnd;
            foreach (var a in AwardList(line.Awards))
            {
                float w = CrtUi.TitleWidth(a.Item1, 15) + 10f;
                if (ax + w > limit)
                    break;
                var tag = new Rect(ax, cy - 9f, w, 18f);
                Ui.Fill(tag, a.Item2 ? CrtUi.Ink : CrtStyle.Gold);
                bool was = Ui.CrtFont;
                Ui.CrtFont = true;
                Ui.Label(tag, a.Item1, 15, a.Item2 ? CrtUi.Bg : new Color(0.11f, 0.07f, 0f), TextAnchor.MiddleCenter, false);
                Ui.CrtFont = was;
                ax += w + 4f;
            }

            // Ready mark (Ready step only): A for auto-ready, a green tick when ready, a dashed empty box while they are not.
            if (!showReady)
                return;
            var dot = new Rect(x + WcColTick + 8f, cy - 13f, 26f, 26f);
            if (p.AutoReady)
            {
                CrtKit.Frame(dot, 2f, CrtUi.Ink);
                HudCrt(dot, "A", 20, CrtUi.Ink, TextAnchor.MiddleCenter);
            }
            else if (p.Ready)
            {
                Ui.Fill(dot, new Color(Ui.Good.r, Ui.Good.g, Ui.Good.b, 0.12f));
                CrtKit.Frame(dot, 2f, Ui.Good);
                Ui.Label(dot, "✓", 15, Ui.Good, TextAnchor.MiddleCenter, true);
            }
            else
                CrtKit.Dashed(dot, 2f, CrtUi.Dim);
        }

        /// (text, blue?) for each award someone has, in the order they show.
        static List<System.Tuple<string, bool>> AwardList(WaveAward a)
        {
            var l = new List<System.Tuple<string, bool>>();
            if ((a & WaveAward.Untouched) != 0) l.Add(System.Tuple.Create("UNTOUCHED", false));
            if ((a & WaveAward.MostDamage) != 0) l.Add(System.Tuple.Create("MOST DAMAGE", false));
            if ((a & WaveAward.MostKills) != 0) l.Add(System.Tuple.Create("MOST KILLS", false));
            if ((a & WaveAward.MostHealing) != 0) l.Add(System.Tuple.Create("MOST HEALING", true));
            if ((a & WaveAward.Reviver) != 0) l.Add(System.Tuple.Create("REVIVER", true));
            return l;
        }

        /// The panel on the right, in a dashed frame: which enemy types the next wave draws from (a boss wave names its boss), and the road to the next boss.
        void DrawNextPanel(Rect r)
        {
            var ink = CrtUi.Ink;
            CrtKit.Dashed(r, 1f, new Color(ink.r, ink.g, ink.b, 0.45f));
            float x = r.x + 14f, y = r.y + 12f, w = r.width - 28f;
            int next = game.Hunt != null ? game.Wave + 1 : game.ShownWave + 1;  // (shown: a Campaign counts to 100)
            bool bossNext = game.BossNext;  // the boss after wave Wave comes before wave Wave + 1
            HudCrt(new Rect(x, y, w, 18f), bossNext ? "NEXT: BOSS" : "NEXT: WAVE " + next, 18, CrtUi.Dim);
            y += 26f;
            if (bossNext)
            {
                var d = Defs.Get(game.BossAt(game.Hunt != null ? next : game.Wave));
                Ui.Dot(new Rect(x, y + 6f, 12f, 12f), ToColor(d.Color));
                CrtUi.Title(new Rect(x + 22f, y, w - 22f, 26f), d.Name.ToUpperInvariant(), 26, CrtStyle.Gold, TextAnchor.MiddleLeft, CabCalm);
                y += 28f;
                HudCrt(new Rect(x, y, w, 20f), "A BOSS WAVE", 20, CrtUi.Dim);
            }
            else
            {
                var now = Game.WavePool(game.NextTrackWave);
                var before = Game.WavePool(game.Wave);
                int shown = 0;
                for (int i = now.Count - 1; i >= 0 && shown < 5; i--, shown++)  // the newest types first
                {
                    var d = Defs.Get(now[i]);
                    Ui.Dot(new Rect(x, y + 5f, 11f, 11f), ToColor(d.Color));
                    Ui.Label(new Rect(x + 20f, y, w - 70f, 21f), d.Name, 14, Color.white, TextAnchor.MiddleLeft, true);
                    if (!before.Contains(now[i]))
                    {
                        var chip = new Rect(r.xMax - 14f - 38f, y + 2f, 38f, 17f);
                        Ui.Fill(chip, CrtStyle.Gold);
                        bool was = Ui.CrtFont;
                        Ui.CrtFont = true;
                        Ui.Label(chip, "NEW", 15, new Color(0.11f, 0.07f, 0f), TextAnchor.MiddleCenter, false);
                        Ui.CrtFont = was;
                    }
                    y += 22f;
                }
            }
            DrawBossRoad(new Rect(x, r.yMax - 12f - 56f, w, 56f));
        }

        void DrawBossRoad(Rect r)
        {
            int next = game.NextBossWave(game.Wave, false);  // a run: the wave the boss follows; a hunt: its last wave
            int prev = game.Hunt != null ? 0 : game.Bosses.LastBossWave(next - 1);
            int steps = game.Hunt != null ? Mathf.Clamp(next - prev, 1, 10) : game.ShownEnd(next) - game.ShownEnd(prev) + 1;  // a run: the cycle's five (a Campaign's or a Quick Play's ten) waves and the boss
            int done = Mathf.Clamp(game.Hunt != null ? game.Wave - prev : game.ShownWave - game.ShownEnd(prev), 0, game.Hunt != null ? steps : steps - 1);
            string boss = Defs.Get(game.BossAt(next)).Name;
            HudCrt(new Rect(r.x, r.y, r.width, 18f), "ROAD TO " + boss.ToUpperInvariant(), 18, CrtUi.Dim);
            float gap = 4f, w = (r.width - gap * (steps - 1)) / steps;
            var ink = CrtUi.Ink;
            for (int i = 0; i < steps; i++)
            {
                var seg = new Rect(r.x + i * (w + gap), r.y + 26f, w, 8f);
                Ui.Fill(seg, i == steps - 1 ? CrtStyle.Gold : i < done ? ink : new Color(1f, 1f, 1f, 0.1f));
            }
            HudCrt(new Rect(r.x, r.y + 38f, r.width, 18f), game.BossNext && game.Hunt == null ? "BOSS NEXT" : "WAVE " + (game.Hunt != null ? game.Wave : game.ShownWave) + " OF " + (game.Hunt != null ? next : game.ShownEnd(next)), 19, CrtUi.Dim);
        }

        /// Before a boss (Core/BossGate.cs): no Ready, the boss gate instead. A key that folds the box away, who's in, and the fill.
        void DrawGateBar(Rect r, bool calm)
        {
            var ink = CrtUi.Ink;
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.ReadyUp)).ToUpperInvariant();
            if (LobbyKit.Key(new Rect(r.x, r.y, 330f, 48f), "GO TO THE GATE  [" + key + "]", false, calm, size: 24, minSize: 14, accent: CrtStyle.Gold))
                wcMin = true;
            int alive, inside;
            game.GateCount(out alive, out inside);
            string st = inside >= alive && alive > 0 ? "EVERYONE IS IN · THE BOSS IS COMING" : "STAND IN THE BOSS GATE · " + inside + " / " + alive + " IN";
            HudCrt(new Rect(r.x + 350f, r.y + 10f, r.width - 350f, 26f), st, 22, inside > 0 ? ink : CrtUi.Dim, TextAnchor.MiddleRight);
            var bar = new Rect(r.x, r.yMax - 4f, r.width, 10f);
            Ui.Fill(bar, new Color(1f, 1f, 1f, 0.08f));
            float fw = bar.width * Mathf.Clamp01(game.GateFill);
            for (float sx = 0f; sx < fw; sx += 8f)
                Ui.Fill(new Rect(bar.x + sx, bar.y, Mathf.Min(6f, fw - sx), bar.height), CrtStyle.Gold);
        }

        /// The Ready key, the Auto-ready switch, the status line, and the countdown bar along the bottom.
        void DrawReadyBar(Player me, Rect r, string waiting, bool calm)
        {
            if (game.GateUp)
            {
                DrawGateBar(r, calm);
                return;
            }
            var ink = CrtUi.Ink;
            // Ready: a lobby key (green and ticked once you are ready); with Auto-ready on it's a dim dashed key.
            var btn = new Rect(r.x, r.y, 220f, 48f);
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.ReadyUp)).ToUpperInvariant();
            if (me.AutoReady)
                LobbyKit.Key(btn, "AUTO-READY", false, calm, size: 24, locked: true);
            else if (LobbyKit.Key(btn, me.Ready ? "READY" : "READY  [" + key + "]", me.Ready, calm, me.Ready ? PixelArt.Get("ui_check") : null, size: 24, minSize: 14,
                         accent: me.Ready ? Ui.Good : (Color?)null))
                PressReady(me);

            // Auto-ready: the Settings switch (lamp, ON / OFF), remembered between runs; its label takes the click too.
            bool on = me.AutoReady;
            bool flip = WinKey(new Rect(r.x + 240f, r.y + 7f, 86f, 34f), on ? "ON" : "OFF", false, 18, on ? ink : CrtUi.Dim, true, on);
            var lab = new Rect(r.x + 338f, r.y + 4f, 170f, 40f);
            Ui.Label(new Rect(lab.x, r.y + 6f, lab.width, 18f), "Auto-ready", 14, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(lab.x, r.y + 24f, lab.width, 15f), "from now on, every wave", 11, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            Ui.NoteHover(lab);
            if (Ui.HitArea(lab))
                flip = true;
            if (flip)
            {
                bool v = !me.AutoReady;
                AutoReadyPref = v;
                autoReadySent = v ? 1 : 0;
                game.SetMyAutoReady(v);
            }

            // Status and the countdown.
            bool allAuto = game.AllAutoReady(), all = game.AllReady();
            string status;
            float frac = 0f;
            if (!string.IsNullOrEmpty(waiting) && waiting.StartsWith("Waiting for"))
                status = waiting;
            else if (allAuto)
            {
                status = "Wave " + (game.Wave + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer);
                frac = game.StateTimer / Game.IntermissionTime;
            }
            else if (all)
            {
                status = "Everyone is ready · wave " + (game.Wave + 1) + " in " + Mathf.CeilToInt(game.StateTimer);
                frac = game.StateTimer / Game.ReadyCountdown;
            }
            else
            {
                var names = new List<string>();
                foreach (var p in game.Players.Values)
                    if (!p.Ready && !p.AutoReady)
                        names.Add(p.Id == game.MyId ? "you" : p.Name);
                status = "Waiting for " + string.Join(" and ", names.ToArray()) + " · no timer yet";
            }
            var sr = new Rect(r.x + 520f, r.y + 10f, r.width - 520f, 26f);
            string st = status.ToUpperInvariant().Replace("…", "...");
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;  // (measured in the CRT font it's drawn in)
            st = LobbyKit.Truncate(st, 22, sr.width, false);
            Ui.CrtFont = was;
            HudCrt(sr, st, 22, frac > 0f ? ink : CrtUi.Dim, TextAnchor.MiddleRight);
            var bar = new Rect(r.x, r.yMax - 4f, r.width, 10f);
            Ui.Fill(bar, new Color(1f, 1f, 1f, 0.08f));
            float fw = bar.width * Mathf.Clamp01(frac);
            for (float sx = 0f; sx < fw; sx += 8f)  // segments, like the XP strip
                Ui.Fill(new Rect(bar.x + sx, bar.y, Mathf.Min(6f, fw - sx), bar.height), ink);
            if (frac <= 0f && !all && !calm)  // waiting: a slow light moving along the empty bar
            {
                float sweep = MathX.Fmod(Time.unscaledTime * 0.35f, 1f);
                Ui.Fill(new Rect(bar.x + sweep * (bar.width - 70f), bar.y, 70f, bar.height), new Color(ink.r, ink.g, ink.b, 0.35f));
            }
        }
    }
}
