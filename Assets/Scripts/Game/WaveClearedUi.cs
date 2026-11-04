using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The "WAVE n CLEARED" box between waves (option 1 of Docs/Mockups/wave-cleared-ready.html, the Squad tally with Awards):
    /// a card shaded like the upgrade cards with a soft glow, a row per player (kills, damage, damage taken, awards and a
    /// ready tick), a Squad row, the next wave's enemy types and the road to the next boss, and along the bottom the Ready
    /// button, the Auto-ready switch and the status or countdown. Core side: Core/WaveReady.cs.
    public partial class ArenaApp
    {
        const float WcW = 940f, WcH = 410f, WcY = 110f;
        int autoReadySent = -1;       // the Auto-ready choice the host was last told (-1: not yet this run)
        float wcBorn = -1f;           // unscaled time the box appeared (for its drop-in)
        int wcWave = -1;

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
            int pref = AutoReadyPref ? 1 : 0;
            if (autoReadySent != pref)
            {
                autoReadySent = pref;
                game.SetMyAutoReady(pref == 1);
            }
            if (WaveBoxUp(me) && GUIUtility.keyboardControl == 0 && Keybinds.Down(KeyAction.ReadyUp))
                PressReady(me);
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
            && me.Offer == null && game.PathOffer == null && !game.ShopOpen && game.Room != RoomPhase.Portal && game.Practice == null;  // (the Practice Range has no ready-up)

        static readonly Color WcGold = new Color(1f, 0.8f, 0.35f);
        static readonly Color WcBlue = new Color(0.61f, 0.75f, 1f);

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
            bool boss = game.IsBossWave(game.Wave);
            Color accent = boss ? Ui.Gold : Ui.Accent;
            // Drops in over a third of a second.
            float t = Mathf.Clamp01((now - wcBorn) / 0.35f);
            float ease = 1f - (1f - t) * (1f - t);
            var r = new Rect(W / 2f - WcW / 2f, WcY - 26f * (1f - ease), WcW, WcH);
            var oldCol = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, ease);

            Ui.GlowPanel(r, accent, 0.22f, 0.9f);  // the upgrade cards' shaded body, a slight glow and a rim
            float x = r.x, y = r.y;

            // Head: check, title, and the wave's two numbers.
            var chk = new Rect(x + 20f, y + 14f, 30f, 30f);
            Ui.Dot(chk, accent, true);
            Ui.Label(chk, "✓", 16, accent, TextAnchor.MiddleCenter, true);
            Ui.Title(new Rect(x + 58f, y + 8f, 420f, 40f), (game.Practice != null ? "PRACTICE WAVE " : "WAVE ") + game.Wave + " CLEARED", 30, Color.white, TextAnchor.MiddleLeft);
            Chip(new Rect(x + WcW - 20f - 100f, y + 17f, 100f, 24f), "+" + rep.ScoreGain + " score", accent);
            Chip(new Rect(x + WcW - 20f - 100f - 8f - 140f, y + 17f, 140f, 24f), FormatClear(rep.ClearTime) + " clear time", accent);

            // The tally.
            float rx = x + 20f, ry = y + 62f;
            DrawTallyHeader(rx, ry);
            ry += 22f;
            var ids = new List<int>(game.Players.Keys);
            ids.Sort();
            int kills = 0;
            float dmg = 0f, taken = 0f;
            foreach (int id in ids)
            {
                var p = game.Players[id];
                var line = rep.For(id) ?? new WaveLine { Id = id };
                DrawTallyRow(new Rect(rx, ry, 600f, 60f), p, line);
                kills += line.Kills; dmg += line.Damage; taken += line.Taken;
                ry += 66f;
            }
            if (ids.Count > 1)
            {
                var sq = new Rect(rx, ry, 600f, 36f);
                Ui.Pill(sq, new Color(accent.r, accent.g, accent.b, 0.12f));
                Ui.Rim(sq, accent, 0.45f);
                Ui.Label(new Rect(sq.x + 12f, sq.y, 90f, sq.height), "Squad", 14, Ui.Muted, TextAnchor.MiddleLeft, true);
                DrawNums(sq, kills, dmg, taken, null, false);
            }

            DrawNextPanel(new Rect(x + 640f, y + 62f, 280f, WcH - 62f - 84f), accent);

            // The bottom bar: Ready, Auto-ready, the status.
            DrawReadyBar(me, new Rect(x + 20f, y + WcH - 76f, WcW - 40f, 62f), accent, waiting);
            GUI.color = oldCol;
            return true;
        }

        static string FormatClear(float s) => Mathf.FloorToInt(s / 60f) + ":" + Mathf.FloorToInt(s % 60f).ToString("00");

        void Chip(Rect r, string text, Color accent)
        {
            Ui.Pill(r, new Color(0f, 0f, 0f, 0.45f));
            Ui.Rim(r, accent, 0.5f);
            Ui.Label(r, text, 12, new Color(0.86f, 0.9f, 0.97f), TextAnchor.MiddleCenter, true);
        }

        // Columns inside a 600-wide row: avatar 10, name 38, kills 118, damage 166, taken 224, awards 284, ready tick at the right.
        void DrawTallyHeader(float x, float y)
        {
            var c = Ui.Muted;
            Ui.Label(new Rect(x + 38f, y, 80f, 18f), "PLAYER", 10, c, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 118f, y, 46f, 18f), "KILLS", 10, c, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(x + 166f, y, 56f, 18f), "DAMAGE", 10, c, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(x + 224f, y, 46f, 18f), "TAKEN", 10, c, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(x + 284f, y, 80f, 18f), "AWARDS", 10, c, TextAnchor.MiddleLeft, true);
        }

        void DrawNums(Rect row, int kills, float damage, float taken, Color? takenColor, bool bright)
        {
            var white = Color.white;
            Ui.Label(new Rect(row.x + 118f, row.y, 46f, row.height), kills.ToString(), 14, white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(row.x + 166f, row.y, 56f, row.height), Mathf.RoundToInt(damage).ToString("N0"), 14, white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(row.x + 224f, row.y, 46f, row.height), Mathf.RoundToInt(taken).ToString("N0"), 14, takenColor ?? white, TextAnchor.MiddleRight, true);
        }

        void DrawTallyRow(Rect row, Player p, WaveLine line)
        {
            bool mine = p.Id == game.MyId;
            var color = ToColor(p.Def.Color);
            Ui.Pill(row, mine ? new Color(0.14f, 0.2f, 0.36f, 0.9f) : new Color(0.05f, 0.07f, 0.14f, 0.9f));
            Ui.Rim(row, mine ? Ui.Accent : new Color(0.3f, 0.34f, 0.5f), mine ? 0.7f : 0.5f);
            Ui.Dot(new Rect(row.x + 10f, row.y + (row.height - 22f) / 2f, 22f, 22f), color);
            Ui.Label(new Rect(row.x + 38f, row.y, 82f, row.height), p.Name, 14, Color.white, TextAnchor.MiddleLeft, true);
            Color takenColor = line.Taken <= 0.5f ? Ui.Good : Color.white;
            DrawNums(row, line.Kills, line.Damage, line.Taken, takenColor, true);

            // Awards: tags left to right, as many as fit.
            // Each tag is a square, the label split onto two lines at its space (MOST / DAMAGE).
            float ax = row.x + 284f, limit = row.x + 562f, side = 52f;
            foreach (var a in AwardList(line.Awards))
            {
                float w = side;
                if (ax + w > limit)
                    break;
                var tag = new Rect(ax, row.y + (row.height - side) / 2f, side, side);
                Color tc = a.Item2 ? WcBlue : WcGold;
                Ui.Pill(tag, new Color(tc.r * 0.16f, tc.g * 0.16f, tc.b * 0.16f, 0.95f));
                Ui.Rim(tag, tc, 0.7f);
                Ui.Label(tag, a.Item1.Replace(' ', '\n'), 8, tc, TextAnchor.MiddleCenter, true);
                ax += w + 3f;
            }

            // Ready tick: a ring for auto-ready, a green tick when ready, an empty ring while they are not.
            var dot = new Rect(row.xMax - 34f, row.y + (row.height - 24f) / 2f, 24f, 24f);
            if (p.AutoReady)
            {
                Ui.Dot(dot, Ui.Accent, true);
                Ui.Label(dot, "A", 11, Ui.Accent, TextAnchor.MiddleCenter, true);
            }
            else if (p.Ready)
            {
                Ui.Dot(dot, Ui.Good, true);
                Ui.Label(dot, "✓", 13, Ui.Good, TextAnchor.MiddleCenter, true);
            }
            else
                Ui.Dot(dot, new Color(0.3f, 0.35f, 0.55f), true);
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

        /// The panel on the right: which enemy types the next wave draws from (a boss wave names its boss), and the road to the next boss.
        void DrawNextPanel(Rect r, Color accent)
        {
            Ui.Pill(r, new Color(0.05f, 0.07f, 0.14f, 0.9f));
            Ui.Rim(r, new Color(0.3f, 0.34f, 0.5f), 0.5f);
            float x = r.x + 14f, y = r.y + 10f;
            int next = game.Wave + 1;
            Ui.Label(new Rect(x, y, r.width - 28f, 16f), "NEXT: WAVE " + next, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            y += 22f;
            if (game.IsBossWave(next))
            {
                var d = Defs.Get(game.BossAt(next));
                Ui.Dot(new Rect(x, y + 3f, 14f, 14f), ToColor(d.Color));
                Ui.Label(new Rect(x + 22f, y, r.width - 50f, 22f), d.Name, 16, Ui.Gold, TextAnchor.MiddleLeft, true);
                y += 26f;
                Ui.Label(new Rect(x, y, r.width - 28f, 20f), "A boss wave", 12, Ui.Muted, TextAnchor.MiddleLeft);
                y += 24f;
            }
            else
            {
                var now = Game.WavePool(next);
                var before = Game.WavePool(game.Wave);
                int shown = 0;
                for (int i = now.Count - 1; i >= 0 && shown < 5; i--, shown++)  // the newest types first
                {
                    var d = Defs.Get(now[i]);
                    bool isNew = !before.Contains(now[i]);
                    Ui.Dot(new Rect(x, y + 4f, 12f, 12f), ToColor(d.Color));
                    Ui.Label(new Rect(x + 20f, y, r.width - 90f, 20f), d.Name, 13, Color.white, TextAnchor.MiddleLeft, true);
                    if (isNew)
                    {
                        var chip = new Rect(r.xMax - 14f - 40f, y + 2f, 40f, 16f);
                        Ui.Pill(chip, new Color(0.16f, 0.13f, 0.03f, 0.95f));
                        Ui.Rim(chip, WcGold, 0.7f);
                        Ui.Label(chip, "NEW", 9, WcGold, TextAnchor.MiddleCenter, true);
                    }
                    y += 22f;
                }
                y += 2f;
            }
            DrawBossRoad(new Rect(x, r.yMax - 52f, r.width - 28f, 44f), accent);
        }

        void DrawBossRoad(Rect r, Color accent)
        {
            int next = game.Wave + 1;
            while (next < game.Wave + 60 && !game.IsBossWave(next))
                next++;
            int prev = game.Wave;
            while (prev > 0 && !game.IsBossWave(prev))
                prev--;
            if (game.Hunt != null)
                prev = 0;
            int steps = Mathf.Clamp(next - prev, 1, 10), done = Mathf.Clamp(game.Wave - prev, 0, steps);
            string boss = Defs.Get(game.BossAt(next)).Name;
            Ui.Label(new Rect(r.x, r.y, r.width, 16f), "ROAD TO " + boss.ToUpperInvariant(), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            float gap = 4f, w = (r.width - gap * (steps - 1)) / steps;
            for (int i = 0; i < steps; i++)
            {
                var seg = new Rect(r.x + i * (w + gap), r.y + 22f, w, 6f);
                Ui.Pill(seg, i == steps - 1 ? Ui.Gold : i < done ? accent : new Color(1f, 1f, 1f, 0.12f));
            }
            Ui.Label(new Rect(r.x, r.y + 28f, r.width, 16f), "Wave " + game.Wave + " of " + next, 11, Ui.Muted, TextAnchor.MiddleLeft);
        }

        /// Ready button, the Auto-ready switch, the status line and the bar under it all.
        void DrawReadyBar(Player me, Rect r, Color accent, string waiting)
        {
            // Ready.
            var btn = new Rect(r.x, r.y + 4f, 200f, 40f);
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.ReadyUp)).ToUpperInvariant();
            if (me.AutoReady)
            {
                GUI.enabled = false;
                Ui.ActionButton(btn, "AUTO-READY", false, Ui.Primary, null, null, 15);
                GUI.enabled = true;
            }
            else if (Ui.ActionButton(btn, me.Ready ? "✓  READY   (undo)" : "READY   [" + key + "]", me.Ready, Ui.Primary, null, null, 15))
                PressReady(me);

            // Auto-ready: a switch, remembered between runs.
            var sw = new Rect(r.x + 222f, r.y + 10f, 42f, 24f);
            bool on = me.AutoReady;
            Ui.Pill(sw, on ? Ui.Good : new Color(0.17f, 0.2f, 0.28f));
            Ui.Rim(sw, on ? Ui.Good : new Color(0.3f, 0.34f, 0.5f), 0.9f);
            Ui.Dot(new Rect(sw.x + (on ? 22f : 4f), sw.y + 4f, 16f, 16f), on ? new Color(0.03f, 0.13f, 0.06f) : new Color(0.6f, 0.65f, 0.76f));
            Ui.Label(new Rect(sw.xMax + 10f, r.y + 6f, 190f, 20f), "Auto-ready", 14, new Color(0.86f, 0.9f, 0.97f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(sw.xMax + 10f, r.y + 24f, 190f, 16f), "from now on, every wave", 11, Ui.Muted, TextAnchor.MiddleLeft);
            var hit = new Rect(sw.x, r.y + 4f, 260f, 40f);
            if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
            {
                bool v = !me.AutoReady;
                AutoReadyPref = v;
                autoReadySent = v ? 1 : 0;
                game.SetMyAutoReady(v);
            }

            // Status and bar.
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
                status = "Everyone is ready. Wave " + (game.Wave + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer);
                frac = game.StateTimer / Game.ReadyCountdown;
            }
            else
            {
                var names = new List<string>();
                foreach (var p in game.Players.Values)
                    if (!p.Ready && !p.AutoReady)
                        names.Add(p.Id == game.MyId ? "you" : p.Name);
                status = "Waiting for " + string.Join(" and ", names.ToArray()) + (game.Players.Count > 1 ? "  ·  no timer until everyone is ready" : "  ·  no timer until you are ready");
            }
            Ui.Label(new Rect(r.x + 420f, r.y + 8f, r.width - 420f, 22f), status, 14, Ui.Muted, TextAnchor.MiddleRight);
            var bar = new Rect(r.x, r.yMax - 6f, r.width, 5f);
            Ui.Bar(bar, frac, accent, new Color(1f, 1f, 1f, 0.11f));
            if (frac <= 0f && !all)  // waiting: a slow light moving along the empty bar
            {
                float sweep = MathX.Fmod(Time.unscaledTime * 0.35f, 1f);
                Ui.Fill(new Rect(bar.x + sweep * (bar.width - 60f), bar.y, 60f, bar.height), new Color(1f, 1f, 1f, 0.16f));
            }
        }
    }
}
