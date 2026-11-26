using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The "WAVE n CLEARED" box between waves, in the lobby's CRT (look A, "One tube", of Docs/Mockups/crt-wave-cleared.html): one CRT
    /// tube where the old card was, with the title and the wave's two numbers, a row per player (kills, damage, damage taken, award
    /// tags and a ready mark), a Squad row, the next wave's enemy types and the road to the next boss, and along the bottom the Ready
    /// key (Start wave n, on every tab), the Auto-ready switch, the status and the countdown. Colours come from CrtStyle.cs (they follow the CRT colour setting);
    /// the frame from RoundWindows.cs. Core side: Core/WaveReady.cs.
    public partial class ArenaApp
    {
        const float WcW = 940f, WcH = 454f, WcY = 70f;  // (high enough that the new-enemy chips under it clear the XP strip)
        int wcStep;                   // the tab: 0 Results, 2 Next wave, 3 Chests (Build isn't a tab of the box: it opens the build window); any order
        bool wcSawWindow;             // the build window was open last frame (closing it folds the box into the strip)
        bool wcSawBuild;              // the build window was opened this break (until then the Build tab pulses and Auto-ready waits)
        int autoReadySent = -1;       // the Auto-ready choice the host was last told (-1: not yet this run)
        float wcBorn = -1f;           // unscaled time the box appeared (for its drop-in)
        int wcWave = -1;
        const float WcStripAlpha = 0.45f;  // the minimized strip's background, scanlines and corners: see-through
        const float WcStripKeyBack = 0.88f;  // the strip's keys sit on near-solid backs
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
            if (game.State == MatchState.Intermission)  // the host shows your status chip from this (Core/WaveReady.cs)
                game.SetMyBreakUi(ExpandWindowUp ? Game.BreakUiBuild : ShopScreenUp(me) || game.ShopOpen && InPedestalShop(me) ? Game.BreakUiShop : wcMin ? Game.BreakUiOut : Game.BreakUiBox);
            int pref = (AutoReadyPref || game.Bot) && !WcHoldsAuto(me) ? 1 : 0;  // (a bot playing for you readies itself)  // Auto-ready only counts once you reach the Ready step
            if (autoReadySent != pref)
            {
                autoReadySent = pref;
                game.SetMyAutoReady(pref == 1);
            }
            if (WaveBoxUp(me) && GUIUtility.keyboardControl == 0 && Keybinds.Down(KeyAction.ReadyUp))
            {
                if (game.GateUp)
                    wcMin = true;  // before a boss the key folds the box away so you can walk to the boss gate (Core/BossGate.cs)
                else
                    PressReady(me);  // the start key works from every tab (Docs/RoundFlow.md)
            }
        }

        // --- The tabs: Results, Build (opens the build window, when a building round is open), Next wave ----------------

        bool WcBuildAvail => game.ExpandOpen && game.Me != null;

        /// The tab shown: without a building round (hunts, the last wave, a full map, other arenas) Results carries the
        /// next wave's panel, so there is no Next wave tab.
        int WcStepNow() => wcStep == 2 && WcBuildAvail ? 2 : wcStep == 3 && WcChestsAvail ? 3 : 0;

        /// Pieces and service cards in your hand (the Build tab's count and the "saved for later" line).
        int WcPieces(Player me) => me == null ? 0 : Game.HandCount(me);

        /// Auto-ready waits while the build window is open, and while you hold pieces in a building round and haven't opened the
        /// build window this break (so Auto-ready never skips building by accident; pressing Start skips it on purpose).
        bool WcHoldsAuto(Player me) => game.State == MatchState.Intermission
            && (ExpandWindowUp || WaveBoxUp(me) && WcBuildAvail && !wcSawBuild && WcPieces(me) > 0);

        void TickWcSteps()
        {
            if (game.State != MatchState.Intermission)
            {
                wcStep = 0;
                wcSawWindow = false;
                wcSawBuild = false;
                wcMin = false;
                return;
            }
            if (expOpen && game.ExpandOpen)
            {
                wcSawWindow = true;
                wcSawBuild = true;
            }
            else if (wcSawWindow)
            {
                wcSawWindow = false;
                wcStep = 0;  // closing the build window (Close, Done · ready up, Decide later, Esc) folds the box into the strip
                wcMin = true;  // at the top so you can walk the arena you built
            }
        }

        /// The Build tab and the build panel's key: the build window.
        void WcOpenBuild()
        {
            wcSawBuild = true;
            expOpen = true;
        }

        /// The start key: ready (or not); pressing it also ends Auto-ready's wait for the build window (you chose to skip building).
        void PressReady(Player me)
        {
            wcSawBuild = true;
            if (me.AutoReady)
                return;
            game.SetMyReady(!me.Ready);
        }

        /// What the start key starts: "WAVE 8", or "THE BOSS".
        string WcNextName() => game.GateUp || game.BossNext ? "THE BOSS" : "WAVE " + (game.Hunt != null ? game.Wave + 1 : game.ShownWave + 1);

        /// The box shows between waves once this wave's numbers have arrived and nothing else (a path, the shop, a portal, your own picks) has the screen.
        bool WaveBoxUp(Player me) =>
            me != null && game.State == MatchState.Intermission && game.Wave > 0 && game.WaveReport != null && game.WaveReport.Wave == game.Wave
            && me.Offer == null && game.PathOffer == null && !game.HuntMapVoting && !ExpandWindowUp && !game.ShopBlocksWave && game.ChallengePhase != RoomPhase.Portal && !BeamShowing;  // (the boss beam-in: BossBeamLook.cs)

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
            bool calm = FxCalm;
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

        /// The box itself, at `r`: the tabs, the tab's content, and the start key along the bottom (on every tab).
        void DrawWaveClearedBox(Player me, Rect r, WaveSummary rep, string waiting, bool calm)
        {
            RefreshWcChests();  // (the Chests tab's list, before the tabs count it: ChestsTabUi.cs)
            int step = WcStepNow();
            DrawWcTabs(me, r, step, calm);
            float x = r.x, y = r.y + WcTabH + 4f;
            Color ink = CrtStyle.Ink;
            bool boss = game.BossNow;
            bool marks = !game.GateUp;  // (no ready marks before a boss: the gate decides)
            if (step == 0)
            {
                // Head: WAVE n CLEARED (gold after a boss) with the blinking cursor, and the wave's two numbers.
                string title = boss ? "BOSS CLEARED" : "WAVE " + game.ShownWave + " CLEARED";
                Color tc = boss ? CrtStyle.Gold : ink;
                DrawWcHead(x, y, title, tc, calm);
                if (boss && game.LivesMax > 0)  // the boss is down: the run's checkpoint is here now (Core/Checkpoints.cs)
                    HudCrt(new Rect(x + WcPad + 2f, y + 62f, 400f, 18f), "CHECKPOINT SAVED  ·  " + (game.LivesLeft == 1 ? "1 LIFE" : game.LivesLeft + " LIVES") + " LEFT", 16, CrtStyle.Gold);
                WcTile(new Rect(x + 646f, y + 16f, 130f, 50f), FormatClear(rep.ClearTime), "CLEAR TIME", calm);
                WcTile(new Rect(x + 786f, y + 16f, 132f, 50f), "+" + rep.ScoreGain, "SCORE", calm);
                DrawWcTally(x, y, rep, calm, marks);
                if (WcBuildAvail)
                    DrawWcBuildPanel(me, new Rect(x + 640f, y + 84f, 278f, 222f), calm);  // (shorter than the next panel: the "saved for later" line sits under it)
                else
                    DrawNextPanel(new Rect(x + 640f, y + 84f, 278f, 236f));
            }
            else if (step == 3)
                DrawWcChests(me, x, y, calm);  // ChestsTabUi.cs
            else
            {
                DrawWcHead(x, y, "NEXT: " + WcNextName(), ink, calm);
                DrawWcTally(x, y, rep, calm, marks);
                DrawNextPanel(new Rect(x + 640f, y + 84f, 278f, 222f));  // (room for the "saved for later" line under it)
            }
            DrawReadyBar(me, new Rect(x + WcPad, y + 338f, WcW - 2f * WcPad, 58f), waiting, calm);
        }

        void DrawWcHead(float x, float y, string title, Color tc, bool calm)
        {
            CrtUi.Title(new Rect(x + WcPad, y + 12f, 600f, 56f), title, 52, tc, TextAnchor.MiddleLeft, calm);
            if (calm || Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0)
                Ui.Fill(new Rect(x + WcPad + CrtUi.TitleWidth(title, 52) + 6f, y + 27f, 14f, 28f), tc);
        }

        /// RESULTS / BUILD · n / NEXT WAVE / CHESTS · n along the top of the tube, in any order: the tab you are on is lit. Build opens the build window
        /// and carries your piece count; until you open it this break (with pieces in hand) it pulses gold. Without a building round only
        /// Results is shown (it carries the next wave's panel then). Chests shows while any chest is unopened (ChestsTabUi.cs).
        void DrawWcTabs(Player me, Rect r, int step, bool calm)
        {
            float tw = 160f, gap = 10f, x = r.x + WcPad, ty = r.y + 10f;
            int tabs = 1 + (WcBuildAvail ? 2 : 0);
            if (DkKey(new Rect(x, ty, tw, 34f), "RESULTS", step == 0, calm, size: 22, rawSize: true) && step != 0)
                wcStep = 0;
            if (WcBuildAvail)
            {
                int n = WcPieces(me);
                var br = new Rect(x + tw + gap, ty, tw, 34f);
                bool nudge = n > 0 && !wcSawBuild;
                if (nudge)  // a gold frame round the tab, breathing (steady with Reduced effects)
                {
                    float a = calm ? 0.9f : 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
                    var g = CrtStyle.Gold;
                    Ui.Fill(br, g.WithAlpha(0.12f * a));
                    CrtKit.Frame(new Rect(br.x - 2f, br.y - 2f, br.width + 4f, br.height + 4f), 2f, g.WithAlpha(a));
                }
                if (DkKey(br, n > 0 ? "BUILD · " + n : "BUILD", false, calm, size: 22, rawSize: true))
                    WcOpenBuild();
                if (DkKey(new Rect(x + 2f * (tw + gap), ty, tw, 34f), game.GateUp ? "THE BOSS" : "NEXT WAVE", step == 2, calm, size: 22, rawSize: true) && step != 2)
                    wcStep = 2;
            }
            if (WcChestsAvail && DkKey(new Rect(x + tabs * (tw + gap), ty, tw, 34f), "CHESTS · " + wcChests.Count, step == 3, calm, size: 22, rawSize: true) && step != 3)
                wcStep = 3;
            // Minimize (top right): the box folds into a strip at the top so you can walk to a shop or a chest.
            if (DkKey(new Rect(r.xMax - WcPad - 160f, ty, 160f, 34f), "MINIMIZE  _", false, calm, size: 20, rawSize: true))
                wcMin = true;
            Ui.Fill(new Rect(r.x + WcPad, r.y + WcTabH + 6f, r.width - 2f * WcPad, 1f), CrtStyle.Ink.WithAlpha(0.3f));
        }

        /// The minimized box: a slim, see-through tube at the top (under the door chip when one is up) with the next wave, the status, Ready,
        /// Build (while a building round is open: it stands in for the build chip) and Open. The keys sit on solid backs so they read over the arena.
        void DrawWcStrip(Player me, string waiting, bool calm)
        {
            bool build = ExpandChipUp;
            float y = 34f;
            const float sh = 44f, kh = 32f, kTop = 6f, rw = 160f, bw = 120f, ow = 92f, kGap = 8f, info = 206f;
            float sw = info + rw + kGap + (build ? bw + kGap : 0f) + ow + 12f;
            var r = new Rect(W / 2f - sw / 2f, y, sw, sh);
            wcStripHit = r;
            RoundPart(1f, Vector2.zero, () =>
            {
                CrtUi.Begin(r, WcStripAlpha);
                bool boss = game.BossNow;
                string next = boss || game.GateUp ? "NEXT: THE BOSS" : "NEXT: WAVE " + (game.Hunt != null ? game.Wave + 1 : game.ShownWave + 1);
                HudCrt(new Rect(r.x + 12f, r.y + 4f, info - 16f, 20f), next, 19, CrtStyle.Ink);
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
                st = LobbyKit.Truncate(st, 13, info - 16f, false);
                Ui.CrtFont = was;
                HudCrt(new Rect(r.x + 12f, r.y + 24f, info - 16f, 16f), st, 13, CrtStyle.Dim);
                var rb = new Rect(r.x + info, r.y + kTop, rw, kh);
                float ox = rb.xMax + kGap;
                var bb = new Rect(ox, r.y + kTop, bw, kh);
                if (build)
                    ox += bw + kGap;
                var ob = new Rect(ox, r.y + kTop, ow, kh);
                var back = new Color(0.01f, 0.03f, 0.07f, WcStripKeyBack);  // solid backs: the keys don't wash out over the arena
                Ui.Fill(rb, back);
                if (build)
                    Ui.Fill(bb, back);
                Ui.Fill(ob, back);
                string key = Keybinds.KeyName(Keybinds.Get(KeyAction.ReadyUp)).ToUpperInvariant();
                if (game.GateUp)
                    DkKey(rb, "TO THE GATE", false, calm, size: 16, minSize: 11, locked: true);
                else if (me.AutoReady)
                    DkKey(rb, "AUTO-READY", false, calm, size: 16, locked: true);
                else if (DkKey(rb, me.Ready ? "READY" : "START  [" + key + "]", me.Ready, calm, me.Ready ? PixelArt.Get("ui_check") : null, size: 16, minSize: 11,
                             accent: me.Ready ? Ui.Good : (Color?)null))
                    PressReady(me);
                if (build && DkKey(bb, "BUILD", false, calm, size: 16, minSize: 11, accent: WcPieces(me) > 0 && !wcSawBuild ? CrtStyle.Gold : (Color?)null))
                    WcOpenBuild();
                if (DkKey(ob, "OPEN", false, calm, size: 16, minSize: 11))
                    wcMin = false;
                CrtUi.End(r, Time.unscaledTime, fxReduced, WcStripAlpha);
            });
        }

        /// The squad's tally (Results) or the same rows with the ready marks (Ready); a Squad total row for more than one player.
        void DrawWcTally(float x, float y, WaveSummary rep, bool calm, bool ready)
        {
            Color ink = CrtStyle.Ink, dim = CrtStyle.Dim;
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
                CrtKit.Dashed(sq, 1f, ink.WithAlpha(0.4f));
                HudCrt(new Rect(sq.x + 10f + WcColName, sq.y, 100f, sq.height), "SQUAD", 26, dim);
                DrawNums(sq, kills, dmg, taken, ink, ink);
            }
        }

        /// Results' build panel, in a gold dashed frame: the pieces in your hand (by name), your vectors and the key to the build window.
        void DrawWcBuildPanel(Player me, Rect r, bool calm)
        {
            var g = CrtStyle.Gold;
            int n = WcPieces(me);
            CrtKit.Dashed(r, 2f, n > 0 ? g.WithAlpha(0.85f) : CrtStyle.Ink.WithAlpha(0.45f));
            float x = r.x + 14f, y = r.y + 12f, w = r.width - 28f;
            HudCrt(new Rect(x, y, w, 18f), "BUILD THE ARENA", 18, CrtStyle.Dim);
            y += 24f;
            CrtUi.Title(new Rect(x, y, 60f, 40f), n.ToString(), 38, n > 0 ? g : CrtStyle.Dim, TextAnchor.MiddleLeft, calm);
            HudCrt(new Rect(x + 50f, y + 9f, w - 50f, 22f), n == 1 ? "PIECE IN HAND" : "PIECES IN HAND", 20, n > 0 ? g : CrtStyle.Dim);
            y += 44f;
            int shown = 0;
            foreach (var c in me.Hand)  // the cards by name, three at most
            {
                if (c == (int)PieceCard.None)
                    continue;
                if (shown == 3)
                {
                    HudCrt(new Rect(x, y, w, 18f), "+" + (n - 3) + " MORE", 17, CrtStyle.Dim);
                    y += 20f;
                    break;
                }
                HudCrt(new Rect(x, y, w, 18f), "· " + Expansion.CardName((PieceCard)c).ToUpperInvariant(), 17, Color.white);
                y += 20f;
                shown++;
            }
            HudCrt(new Rect(x, r.yMax - 72f, w, 18f), "VECTORS  " + me.Vectors + " / " + me.VectorCap, 17, CrtStyle.Dim);
            if (DkKey(new Rect(x, r.yMax - 48f, w, 38f), "OPEN BUILD MAP", false, calm, size: 20, minSize: 13, accent: n > 0 ? g : (Color?)null))
                WcOpenBuild();
        }

        static string FormatClear(float s) => Mathf.FloorToInt(s / 60f) + ":" + Mathf.FloorToInt(s % 60f).ToString("00");

        /// A number tile: the value big in gold, its name small under it, in a faint lit box.
        void WcTile(Rect r, string value, string name, bool calm)
        {
            var ink = CrtStyle.Ink;
            Ui.Fill(r, ink.WithAlpha(0.09f));
            CrtKit.Frame(r, 1f, ink.WithAlpha(0.45f));
            CrtUi.Title(new Rect(r.x, r.y + 2f, r.width, 30f), value, 28, CrtStyle.Gold, TextAnchor.MiddleCenter, calm);
            Ui.Label(new Rect(r.x, r.yMax - 17f, r.width, 13f), name, 10, CrtStyle.Dim, TextAnchor.MiddleCenter, true);
        }

        // Columns inside a row (from 10 px in): the player's dot, name, kills, damage, taken, the award tags, the ready mark at the right.
        const float WcColName = 34f, WcColKills = 143f, WcColDamage = 209f, WcColTaken = 293f, WcColAwards = 363f, WcColAwardsEnd = 534f, WcColTick = 542f;

        void DrawTallyHeader(float x, float y)
        {
            var c = CrtStyle.Dim;
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
            CrtKit.SoftBox(row, CrtStyle.Ink, mine, false, false);  // yours lit, like a selected key
            float x = row.x + 10f, cy = row.center.y;
            Ui.Dot(new Rect(x + 4f, cy - 9f, 18f, 18f), p.Def.Color.ToColor());  // the only colour on the row besides the awards
            float nw = WcColKills - WcColName - 8f;
            Ui.Label(new Rect(x + WcColName, row.y + 8f, nw, 18f), LobbyKit.Truncate(p.Name, 14, nw), 14, Color.white, TextAnchor.MiddleLeft, true);
            string cls = (Defs.KitName(p.Kit) + " " + p.Def.Name).ToUpperInvariant();
            if (Ui.TextWidth(cls, 9, true) > nw)
                cls = Defs.KitName(p.Kit).ToUpperInvariant();  // (too long for the column: the class alone, not a cut-off "ELEMENTALIST TA…")
            Ui.Label(new Rect(x + WcColName, row.y + 27f, nw, 14f), LobbyKit.Truncate(cls, 9, nw), 9, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
            DrawNums(row, line.Kills, line.Damage, line.Taken, Color.white, line.Taken <= 0.5f ? Ui.Good : Color.white);

            // The status chip (Docs/RoundFlow.md step 2): what they're doing in the break, at the row's right end; the awards stop short of it.
            var st = game.State == MatchState.Intermission ? p.Break : BreakStatus.None;
            string chip = st == BreakStatus.Picking ? "PICKING" : st == BreakStatus.Building ? "BUILDING" : st == BreakStatus.Shopping ? "SHOPPING" : st == BreakStatus.Looting ? "LOOTING" : null;
            float limit = x + WcColAwardsEnd;
            if (chip != null)
            {
                Color cc = BreakChipColor(st);
                float cw = CrtUi.TitleWidth(chip, 13) + 8f;
                var cr = new Rect(row.xMax - 4f - cw, cy - 10f, cw, 20f);
                Ui.Fill(cr, cc.WithAlpha(0.16f));
                CrtKit.Frame(cr, 1f, cc);
                HudCrt(cr, chip, 13, cc, TextAnchor.MiddleCenter);  // (small enough that two awards still fit beside it)
                limit = Mathf.Min(limit, cr.x - 3f);
            }

            // Awards: inverse-video tags (gold, or the ink for the healers' ones), left to right, as many as fit.
            float ax = x + WcColAwards;
            foreach (var a in AwardList(line.Awards))
            {
                float w = CrtUi.TitleWidth(a.Item1, 15) + 8f;
                if (ax + w > limit)
                    break;
                var tag = new Rect(ax, cy - 9f, w, 18f);
                Ui.Fill(tag, a.Item2 ? CrtStyle.Ink : CrtStyle.Gold);
                bool was = Ui.CrtFont;
                Ui.CrtFont = true;
                Ui.Label(tag, a.Item1, 15, a.Item2 ? CrtStyle.Bg : new Color(0.11f, 0.07f, 0f), TextAnchor.MiddleCenter, false);
                Ui.CrtFont = was;
                ax += w + 3f;
            }

            // Ready mark (not before a boss): A for auto-ready, a green tick when ready, a dashed empty box while they are not (and have no chip).
            if (!showReady || chip != null)
                return;
            var dot = new Rect(x + WcColTick + 8f, cy - 13f, 26f, 26f);
            if (p.AutoReady)
            {
                CrtKit.Frame(dot, 2f, CrtStyle.Ink);
                HudCrt(dot, "A", 20, CrtStyle.Ink, TextAnchor.MiddleCenter);
            }
            else if (p.Ready)
            {
                Ui.Fill(dot, Ui.Good.WithAlpha(0.12f));
                CrtKit.Frame(dot, 2f, Ui.Good);
                Ui.Label(dot, "✓", 15, Ui.Good, TextAnchor.MiddleCenter, true);
            }
            else
                CrtKit.Dashed(dot, 2f, CrtStyle.Dim);
        }

        /// A status chip's colour: picking orange, building violet, shopping gold, out looting the ink.
        static Color BreakChipColor(BreakStatus st) => st == BreakStatus.Picking ? new Color(1f, 0.54f, 0.24f) : st == BreakStatus.Building ? new Color(0.76f, 0.55f, 1f)
            : st == BreakStatus.Shopping ? CrtStyle.Gold : CrtStyle.Ink;

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
            var ink = CrtStyle.Ink;
            CrtKit.Dashed(r, 1f, ink.WithAlpha(0.45f));
            float x = r.x + 14f, y = r.y + 12f, w = r.width - 28f;
            int next = game.Hunt != null ? game.Wave + 1 : game.ShownWave + 1;  // (shown: a Campaign counts to 100)
            bool bossNext = game.BossNext;  // the boss after wave Wave comes before wave Wave + 1
            HudCrt(new Rect(x, y, w, 18f), bossNext ? "NEXT: BOSS" : "NEXT: WAVE " + next, 18, CrtStyle.Dim);
            y += 26f;
            if (bossNext)
            {
                var d = Defs.Get(game.BossAt(game.Hunt != null ? next : game.Wave));
                Ui.Dot(new Rect(x, y + 6f, 12f, 12f), d.Color.ToColor());
                CrtUi.Title(new Rect(x + 22f, y, w - 22f, 26f), d.Name.ToUpperInvariant(), 26, CrtStyle.Gold, TextAnchor.MiddleLeft, FxCalm);
                y += 28f;
                HudCrt(new Rect(x, y, w, 20f), "A BOSS WAVE", 20, CrtStyle.Dim);
            }
            else
            {
                var now = Game.WavePool(game.NextTrackWave);
                var before = Game.WavePool(game.Wave);
                int shown = 0;
                int rows = Mathf.Min(5, now.Count);
                float step = rows <= 3 ? 34f : 26f;  // (a short list spreads out, each type with its job under the name)
                for (int i = now.Count - 1; i >= 0 && shown < 5; i--, shown++)  // the newest types first
                {
                    var d = Defs.Get(now[i]);
                    int job = (int)Game.JobOf(now[i]);
                    Ui.Dot(new Rect(x, y + 5f, 11f, 11f), d.Color.ToColor());
                    Ui.Label(new Rect(x + 20f, y, w - 70f, 21f), d.Name, 14, Color.white, TextAnchor.MiddleLeft, true);
                    if (rows <= 3)
                        HudCrt(new Rect(x + 20f, y + 18f, w - 70f, 13f), JobNames[job], 13, JobColors[job].ToColor());
                    else
                        HudCrt(new Rect(x + 20f, y, w - 76f, 21f), JobNames[job], 13, JobColors[job].ToColor(), TextAnchor.MiddleRight);
                    if (!before.Contains(now[i]))
                    {
                        var chip = new Rect(r.xMax - 14f - 38f, y + 2f, 38f, 17f);
                        Ui.Fill(chip, CrtStyle.Gold);
                        bool was = Ui.CrtFont;
                        Ui.CrtFont = true;
                        Ui.Label(chip, "NEW", 15, new Color(0.11f, 0.07f, 0f), TextAnchor.MiddleCenter, false);
                        Ui.CrtFont = was;
                    }
                    y += step;
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
            HudCrt(new Rect(r.x, r.y, r.width, 18f), "ROAD TO " + boss.ToUpperInvariant(), 18, CrtStyle.Dim);
            float gap = 4f, w = (r.width - gap * (steps - 1)) / steps;
            var ink = CrtStyle.Ink;
            for (int i = 0; i < steps; i++)
            {
                var seg = new Rect(r.x + i * (w + gap), r.y + 26f, w, 8f);
                Ui.Fill(seg, i == steps - 1 ? CrtStyle.Gold : i < done ? ink : new Color(1f, 1f, 1f, 0.1f));
            }
            HudCrt(new Rect(r.x, r.y + 38f, r.width, 18f), game.BossNext && game.Hunt == null ? "BOSS NEXT" : "WAVE " + (game.Hunt != null ? game.Wave : game.ShownWave) + " OF " + (game.Hunt != null ? next : game.ShownEnd(next)), 19, CrtStyle.Dim);
        }

        /// Before a boss (Core/BossGate.cs): no Ready, the boss gate instead. A key that folds the box away, who's in, and the fill.
        void DrawGateBar(Rect r, bool calm)
        {
            var ink = CrtStyle.Ink;
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.ReadyUp)).ToUpperInvariant();
            if (DkKey(new Rect(r.xMax - 330f, r.y, 330f, 48f), "GO TO THE GATE  [" + key + "]", false, calm, size: 24, minSize: 14, accent: CrtStyle.Gold))
                wcMin = true;
            int alive, inside;
            game.GateCount(out alive, out inside);
            string st = inside >= alive && alive > 0 ? "EVERYONE IS IN · THE BOSS IS COMING" : "STAND IN THE BOSS GATE · " + inside + " / " + alive + " IN";
            HudCrt(new Rect(r.x, r.y + 10f, r.width - 350f, 26f), st, 22, inside > 0 ? ink : CrtStyle.Dim, TextAnchor.MiddleLeft);
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
            var ink = CrtStyle.Ink;
            // Ready: a lobby key on the right, where Continue sits on Results (green and ticked once you are ready); with Auto-ready on it's a dim dashed key.
            var btn = new Rect(r.xMax - 330f, r.y, 330f, 48f);
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.ReadyUp)).ToUpperInvariant();
            string next = WcNextName();
            if (me.AutoReady)
                DkKey(btn, "AUTO-READY", false, calm, size: 24, locked: true);
            else if (DkKey(btn, me.Ready ? next + " · READY" : "START " + next + "  [" + key + "]", me.Ready, calm, me.Ready ? PixelArt.Get("ui_check") : null, size: 24, minSize: 14,
                         accent: me.Ready ? Ui.Good : (Color?)null))
                PressReady(me);
            // Skipping the build is fine (pieces keep for later), but never by accident: the key says what it leaves in your hand.
            int saved = WcPieces(me);
            if (WcBuildAvail && saved > 0 && !me.Ready && !me.AutoReady)
                HudCrt(new Rect(btn.x, r.y - 25f, btn.width, 16f), saved + (saved == 1 ? " PIECE" : " PIECES") + " SAVED FOR LATER", 16, CrtStyle.Gold, TextAnchor.MiddleRight);

            // Auto-ready: the Settings switch (lamp, ON / OFF), remembered between runs; its label takes the click too.
            bool on = me.AutoReady;
            bool flip = DkKey(new Rect(r.x, r.y + 7f, 86f, 34f), on ? "ON" : "OFF", false, FxCalm, size: 18, accent: on ? ink : CrtStyle.Dim, lamp: true, lampLit: on);
            var lab = new Rect(r.x + 98f, r.y + 4f, 170f, 40f);
            Ui.Label(new Rect(lab.x, r.y + 6f, lab.width, 18f), "Auto-ready", 14, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(lab.x, r.y + 24f, lab.width, 15f), "from now on, every wave", 11, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
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
                        names.Add(p.Id == game.MyId ? "you" : p.Name);  // (what they're doing is on their row's chip)
                status = "Waiting for " + string.Join(" and ", names.ToArray()) + " · no timer yet";
            }
            var sr = new Rect(r.x + 280f, r.y + 10f, r.width - 280f - 350f, 26f);  // between the switch and the Ready key
            string st = status.ToUpperInvariant().Replace("…", "...");
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;  // (measured in the CRT font it's drawn in)
            st = LobbyKit.Truncate(st, 18, sr.width, false);
            Ui.CrtFont = was;
            HudCrt(sr, st, 18, frac > 0f ? ink : CrtStyle.Dim, TextAnchor.MiddleRight);
            var bar = new Rect(r.x, r.yMax - 4f, r.width, 10f);
            Ui.Fill(bar, new Color(1f, 1f, 1f, 0.08f));
            float fw = bar.width * Mathf.Clamp01(frac);
            for (float sx = 0f; sx < fw; sx += 8f)  // segments, like the XP strip
                Ui.Fill(new Rect(bar.x + sx, bar.y, Mathf.Min(6f, fw - sx), bar.height), ink);
            if (frac <= 0f && !all && !calm)  // waiting: a slow light moving along the empty bar
            {
                float sweep = MathX.Fmod(Time.unscaledTime * 0.35f, 1f);
                Ui.Fill(new Rect(bar.x + sweep * (bar.width - 70f), bar.y, 70f, bar.height), ink.WithAlpha(0.35f));
            }
        }
    }
}
