using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Records > Recent runs (Docs/RecentRuns.md, Docs/Mockups/recent-runs.html): a strip of your last runs, stat tiles
    /// and graphs of the open run (Overview: damage per second, health, accuracy per wave; Trends: wave and damage run
    /// by run). The curves come from RunTimeline.cs; a run without a file still shows its tiles and the Trends tab.
    public partial class ArenaApp
    {
        int recentSel;          // the open run (index into Profile.Recent, newest first)
        int recentTab;          // 0 Overview, 1 Trends
        int recentFirst;        // the first card shown in the strip
        int recentSelSeen = -1;
        string recentTlFile = "";
        RunTimeline recentTl;
        Rect? chartTipBounds;   // where ChartTip may draw (the drawer panel unless set; the results screen sets the whole canvas)

        const int RecentCards = 4;
        const int TrendRuns = 12;

        static readonly Rgba GGold = new Rgba(1f, 0.8f, 0.35f), GCyan = new Rgba(0.35f, 0.82f, 0.95f), GGreen = new Rgba(0.37f, 0.85f, 0.54f),
            GRed = new Rgba(1f, 0.36f, 0.42f), GPurple = new Rgba(0.69f, 0.55f, 1f), GBand = new Rgba(1f, 1f, 1f, 0.035f);
        static readonly Rgba[] GPlayers = { GGold, GCyan, GPurple, GGreen };
        static readonly Color GridLine = new Color(1f, 1f, 1f, 0.08f);

        static V2 P2(float x, float y) => new V2(x, y);

        static bool RunWon(RankLog l) => l.Result == 1 || l.Result == 3;
        static string RunTitle(RankLog l) => (l.Result == 2 || l.Result == 3) ? "Boss hunt" : l.Mode.Length > 0 ? l.Mode : "Run";

        static string RunClock(float s)
        {
            int n = Mathf.Max(0, Mathf.RoundToInt(s));
            return n >= 3600 ? n / 3600 + ":" + (n / 60 % 60).ToString("00") + ":" + (n % 60).ToString("00") : n / 60 + ":" + (n % 60).ToString("00");
        }

        /// The next round number at or above v (1, 2, 5 times a power of ten), so axes end on a tidy tick.
        static float NiceMax(float v)
        {
            if (v <= 0f)
                return 1f;
            float e = (float)Math.Pow(10.0, Math.Floor(Math.Log10(v))), m = v / e;
            return (m <= 1f ? 1f : m <= 2f ? 2f : m <= 5f ? 5f : 10f) * e;
        }

        RunTimeline TimelineFor(RankLog l)
        {
            if (l == null || l.File.Length == 0)
                return null;
            if (l.File != recentTlFile)
            {
                recentTlFile = l.File;
                recentTl = RunTimelineStore.Load(l.File);
            }
            return recentTl;
        }

        void DrawRecentRuns(Profile p, float t, Color col, float k)
        {
            var runs = p.Recent;
            OpenSectionPanel("RECENT RUNS", col, runs.Count == 0 ? null : "Pick a run to see how it went. Your last " + Profile.RecentKept + " are kept.", k);
            float x = DrawerPanel.x + 26f, w = DrawerPanel.width - 52f, top = DrawerPanel.y;
            if (runs.Count == 0)
            {
                Ui.Label(new Rect(x, top + 90f, w, 40f), "Finish a run and it shows up here, with graphs of how it went.", 14, Ui.Muted, TextAnchor.UpperLeft, false, true);
                return;
            }
            recentSel = Mathf.Clamp(recentSel, 0, runs.Count - 1);
            if (recentSel != recentSelSeen)  // a run picked elsewhere (a Trends click): scroll the strip to it (the arrows scroll freely otherwise)
            {
                recentSelSeen = recentSel;
                if (recentSel < recentFirst)
                    recentFirst = recentSel;
                else if (recentSel >= recentFirst + RecentCards)
                    recentFirst = recentSel - RecentCards + 1;
            }
            recentFirst = Mathf.Clamp(recentFirst, 0, Mathf.Max(0, runs.Count - RecentCards));
            var cur = runs[recentSel];

            // The strip of runs.
            float sy = top + 96f, arrow = 24f, cw = (w - arrow * 2f - 8f * (RecentCards + 1)) / RecentCards;
            if (runs.Count > RecentCards)
            {
                if (recentFirst > 0 && WinKey(new Rect(x, sy, arrow, 66f), "<", false, 22))
                    recentFirst--;
                if (recentFirst + RecentCards < runs.Count && WinKey(new Rect(x + w - arrow, sy, arrow, 66f), ">", false, 22))
                    recentFirst++;
            }
            for (int i = 0; i < RecentCards && recentFirst + i < runs.Count; i++)
            {
                int ri = recentFirst + i;
                var l = runs[ri];
                bool on = ri == recentSel, won = RunWon(l);
                var r = new Rect(x + arrow + 8f + i * (cw + 8f), sy, cw, 66f);
                Ui.Pill(r, on ? new Color(1f, 0.8f, 0.35f, 0.16f) : new Color(1f, 1f, 1f, r.Contains(Event.current.mousePosition) ? 0.07f : 0.04f));
                if (on)
                    Ui.Fill(new Rect(r.x, r.yMax - 3f, r.width, 3f), Ui.Gold);
                Ui.Label(new Rect(r.x + 10f, r.y + 4f, r.width - 20f, 20f), RunTitle(l), 14, on ? Ui.Gold : Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 10f, r.y + 24f, r.width - 20f, 16f), l.Class.Length > 0 ? l.Class : (ri == 0 ? "latest" : ""), 11, Ui.Muted);
                Ui.Label(new Rect(r.x + 10f, r.y + 42f, r.width * 0.55f, 16f), "Wave " + l.Wave, 11, Ui.Muted);
                Ui.Label(new Rect(r.x + r.width * 0.5f, r.y + 42f, r.width * 0.5f - 10f, 16f), won ? "WON" : l.Result == 4 ? "QUIT" : (l.Result >= 2 ? "LOST" : "DIED"), 11, won ? Ui.Good : new Color(1f, 0.42f, 0.46f), TextAnchor.MiddleRight, true);
                if (Ui.HitArea(r))
                    recentSel = ri;
            }

            // The open run's numbers.
            string[,] tiles =
            {
                { "RESULT", RunWon(cur) ? "Won" : cur.Result == 4 ? "Quit" : cur.Result >= 2 ? "Lost" : "Died" }, { "TIME", cur.Seconds > 0f ? RunClock(cur.Seconds) : "—" }, { "WAVE", cur.Wave.ToString() },
                { "DAMAGE", cur.Dealt > 0f ? ShortNumber(cur.Dealt) : "—" }, { "TAKEN", cur.Class.Length > 0 ? ShortNumber(cur.Taken) : "—" }, { "XP", "+" + cur.Xp.ToString("N0") } };
            float ty = sy + 76f, tw = (w - 5f * 8f) / 6f;
            for (int i = 0; i < 6; i++)
            {
                var tr = new Rect(x + i * (tw + 8f), ty, tw, 48f);
                Ui.Pill(tr, new Color(1f, 1f, 1f, 0.04f));
                Color vc = i == 0 ? (RunWon(cur) ? Ui.Good : new Color(1f, 0.42f, 0.46f)) : i == 5 ? Ui.Gold : Color.white;
                Ui.Label(new Rect(tr.x + 10f, tr.y + 4f, tr.width - 14f, 26f), tiles[i, 1], 18, vc, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(tr.x + 10f, tr.y + 30f, tr.width - 14f, 14f), tiles[i, 0], 9, Ui.Muted, TextAnchor.MiddleLeft, true);
            }

            // Tabs.
            float tby = ty + 58f;
            string[] tabs = { "Overview", "Trends" };
            for (int i = 0; i < tabs.Length; i++)
                if (WinKey(new Rect(x + i * 108f, tby, 100f, 30f), tabs[i].ToUpperInvariant(), i == recentTab, 15))
                    recentTab = i;
            string line = RunTitle(cur) + (cur.Class.Length > 0 ? "  ·  " + cur.Class : "") + (cur.Players > 1 ? "  ·  " + cur.Players + " players" : "") + (cur.Mods.Length > 0 ? "  ·  " + cur.Mods.Replace("|", ", ") : "");
            Ui.Label(new Rect(x + 200f, tby, w - 200f, 28f), line, 11, Ui.Muted, TextAnchor.MiddleRight);

            float cy = tby + 36f, ch = (DrawerPanel.yMax - 16f - cy - 8f) / 2f;
            if (recentTab == 0)
            {
                var tl = TimelineFor(cur);
                if (tl == null || tl.Samples.Count < 2)
                {
                    Ui.Pill(new Rect(x, cy, w, ch * 2f + 8f), new Color(1f, 1f, 1f, 0.03f));
                    Ui.Label(new Rect(x + 24f, cy + 20f, w - 48f, 60f), "No graph data for this run. Graphs are kept for runs played from now on; Boss Hunts and Practice don't have any. The Trends tab still covers every run.", 13, Ui.Muted, TextAnchor.UpperLeft, false, true);
                }
                else
                {
                    DrawDpsChart(tl, new Rect(x, cy, w, ch));
                    float hw = (w - 8f) / 2f;
                    DrawHealthChart(tl, new Rect(x, cy + ch + 8f, hw, ch));
                    DrawAccuracyChart(tl, new Rect(x + hw + 8f, cy + ch + 8f, hw, ch));
                }
            }
            else
            {
                DrawTrendChart(runs, new Rect(x, cy, w, ch), false);
                DrawTrendChart(runs, new Rect(x, cy + ch + 8f, w, ch), true);
            }
        }

        // --- Chart pieces ---------------------------------------------------------------------------------------

        /// The chart's box with its title; returns the plot area inside it.
        Rect ChartFrame(Rect box, string title, string meta)
        {
            Ui.Pill(box, new Color(1f, 1f, 1f, 0.04f));
            Ui.Label(new Rect(box.x + 12f, box.y + 4f, box.width * 0.6f, 18f), title, 10, new Color(0.79f, 0.83f, 0.92f), TextAnchor.MiddleLeft, true);
            if (!string.IsNullOrEmpty(meta))
                Ui.Label(new Rect(box.x + box.width * 0.4f, box.y + 4f, box.width * 0.6f - 12f, 18f), meta, 10, Ui.Muted, TextAnchor.MiddleRight);
            return new Rect(box.x + 46f, box.y + 28f, box.width - 46f - 14f, box.height - 28f - 20f);
        }

        void GridY(Rect plot, float ymax, string unit)
        {
            for (int g = 0; g <= 2; g++)
            {
                float y = plot.yMax - plot.height * g / 2f;
                Ui.Fill(new Rect(plot.x, Mathf.Round(y), plot.width, 1f), GridLine);
                Ui.Label(new Rect(plot.x - 44f, y - 8f, 38f, 16f), ShortNumber(ymax * g / 2f) + unit, 9, Ui.Muted, TextAnchor.MiddleRight);
            }
        }

        void AxisTime(Rect plot, float tmax)
        {
            float stepMin = Mathf.Max(1f, Mathf.Ceil(tmax / 60f / 6f));
            for (float m = 0f; m * 60f <= tmax + 0.1f; m += stepMin)
                Ui.Label(new Rect(plot.x + plot.width * m * 60f / tmax - 20f, plot.yMax + 3f, 40f, 14f), m + "m", 9, Ui.Muted, TextAnchor.MiddleCenter);
        }

        /// Alternating wave bands and a red tint while a boss wave was on.
        void WaveBands(Rect plot, RunTimeline tl, float tmax)
        {
            for (int i = 1; i < tl.Samples.Count; i++)
            {
                var a = tl.Samples[i - 1];
                var b = tl.Samples[i];
                float x0 = plot.x + plot.width * a.T / tmax, x1 = plot.x + plot.width * b.T / tmax;
                if (b.Wave % 2 == 0)
                    Ui.Fill(new Rect(x0, plot.y, x1 - x0, plot.height), new Color(1f, 1f, 1f, 0.035f));
                if (b.Boss)
                    Ui.Fill(new Rect(x0, plot.y, x1 - x0, plot.height), new Color(1f, 0.3f, 0.35f, 0.07f));
            }
        }

        /// The sample the mouse is over in a time chart, or -1.
        int HoverSample(Rect plot, RunTimeline tl, float tmax)
        {
            var m = Event.current.mousePosition;
            if (!plot.Contains(m))
                return -1;
            float tt = (m.x - plot.x) / plot.width * tmax;
            int best = 0;
            for (int i = 1; i < tl.Samples.Count; i++)
                if (Mathf.Abs(tl.Samples[i].T - tt) < Mathf.Abs(tl.Samples[best].T - tt))
                    best = i;
            return best;
        }

        void ChartTip(string head, List<KeyValuePair<string, Color>> rows)
        {
            var m = Event.current.mousePosition;
            var lim = chartTipBounds ?? DrawerPanel;
            float w = Mathf.Max(head.Length, 12) * 6.6f + 20f;
            foreach (var r in rows)
                w = Mathf.Max(w, r.Key.Length * 6.6f + 20f);
            float h = 22f + rows.Count * 16f;
            float x = m.x + 16f, y = m.y - h - 6f;
            if (x + w > lim.xMax - 6f)
                x = m.x - w - 12f;
            if (y < lim.y + 4f)
                y = m.y + 18f;
            var box = new Rect(x, y, w, h);
            Ui.Pill(box, new Color(0.03f, 0.04f, 0.08f, 0.96f));
            Ui.Label(new Rect(box.x + 10f, box.y + 3f, w - 14f, 16f), head, 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < rows.Count; i++)
                Ui.Label(new Rect(box.x + 10f, box.y + 20f + i * 16f, w - 14f, 16f), rows[i].Key, 11, rows[i].Value, TextAnchor.MiddleLeft);
        }

        static KeyValuePair<string, Color> Row(string text, Color c) => new KeyValuePair<string, Color>(text, c);

        /// Draws a series as an area under a line in the plot (GL; values are clamped to ymax).
        void GlSeries(Rect plot, RunTimeline tl, float[] v, float tmax, float ymax, Rgba c, bool fill, float width)
        {
            V2 prev = P2(0f, 0f);
            for (int i = 0; i < v.Length; i++)
            {
                var pt = P2(plot.x + plot.width * tl.Samples[i].T / tmax, plot.yMax - plot.height * Mathf.Clamp01(v[i] / ymax));
                if (i > 0)
                {
                    if (fill)
                    {
                        SetColor(c.WithAlpha(0.16f));
                        Quad(prev, pt, P2(pt.X, plot.yMax), P2(prev.X, plot.yMax));
                    }
                    Line(prev, pt, width, c);
                }
                prev = pt;
            }
            if (v.Length > 0 && v.Length <= 40 && width > 1.5f)  // (a dot per sample on a short run; a long run's line is enough)
                for (int i = 0; i < v.Length; i++)
                    Circle(P2(plot.x + plot.width * tl.Samples[i].T / tmax, plot.yMax - plot.height * Mathf.Clamp01(v[i] / ymax)), 2f, c, 8);
        }

        void DrawDpsChart(RunTimeline tl, Rect box)
        {
            int me = tl.Mine;
            float tmax = Mathf.Max(1f, tl.Samples[tl.Samples.Count - 1].T);
            var mine = tl.Rates(me, TL.Dealt);
            float[] team = null;
            if (tl.Players.Count > 1)
            {
                team = new float[mine.Length];
                for (int pl = 0; pl < tl.Players.Count; pl++)
                {
                    var r = tl.Rates(pl, TL.Dealt);
                    for (int i = 0; i < r.Length; i++)
                        team[i] += r[i];
                }
            }
            float top = 0f;
            foreach (var v in team ?? mine)
                top = Mathf.Max(top, v);
            float ymax = NiceMax(Mathf.Max(top, 10f));
            float avg = tl.Total(me, TL.Dealt) / tmax;
            var plot = ChartFrame(box, "DAMAGE PER SECOND", "you avg " + ShortNumber(avg) + (team != null ? "  ·  team line in blue" : ""));
            WaveBands(plot, tl, tmax);
            GridY(plot, ymax, "");
            AxisTime(plot, tmax);
            if (BeginUiGl())
            {
                if (team != null)
                    GlSeries(plot, tl, team, tmax, ymax, GCyan, false, 1.5f);
                GlSeries(plot, tl, mine, tmax, ymax, GGold, true, 2f);
                foreach (int i in tl.DownSamples(me))
                    Circle(P2(plot.x + plot.width * tl.Samples[i].T / tmax, plot.yMax - 3f), 4f, GRed, 10);
                EndUiGl();
            }
            int h = HoverSample(plot, tl, tmax);
            if (h >= 0)
            {
                float hx = plot.x + plot.width * tl.Samples[h].T / tmax;
                Ui.Fill(new Rect(Mathf.Round(hx), plot.y, 1f, plot.height), new Color(1f, 1f, 1f, 0.4f));
                var rows = new List<KeyValuePair<string, Color>> { Row("You  " + ShortNumber(mine[h]) + " dmg/s", ToColor(GGold)) };
                if (team != null)
                    rows.Add(Row("Team  " + ShortNumber(team[h]) + " dmg/s", ToColor(GCyan)));
                rows.Add(Row("Health  " + Mathf.RoundToInt(tl.Samples[h].Get(me, TL.Hp)) + "%", Ui.Muted));
                if (tl.Samples[h].Boss)
                    rows.Add(Row("Boss wave", new Color(1f, 0.42f, 0.46f)));
                ChartTip(RunClock(tl.Samples[h].T) + "  ·  wave " + tl.Samples[h].Wave, rows);
            }
        }

        void DrawHealthChart(RunTimeline tl, Rect box)
        {
            float tmax = Mathf.Max(1f, tl.Samples[tl.Samples.Count - 1].T);
            int me = tl.Mine, downs = tl.DownSamples(me).Count;
            var plot = ChartFrame(box, "HEALTH", downs == 0 ? "never went down" : "you went down " + downs + (downs == 1 ? " time" : " times"));
            WaveBands(plot, tl, tmax);
            GridY(plot, 100f, "%");
            AxisTime(plot, tmax);
            if (BeginUiGl())
            {
                for (int pl = 0; pl < tl.Players.Count; pl++)
                    if (pl != me)
                        GlSeries(plot, tl, tl.Series(pl, TL.Hp), tmax, 100f, GPlayers[pl % GPlayers.Length].WithAlpha(0.6f), false, 1.5f);
                GlSeries(plot, tl, tl.Series(me, TL.Hp), tmax, 100f, GPlayers[me % GPlayers.Length], false, 2f);
                foreach (int i in tl.DownSamples(me))
                    Circle(P2(plot.x + plot.width * tl.Samples[i].T / tmax, plot.yMax - plot.height * tl.Samples[i].Get(me, TL.Hp) / 100f), 4.5f, GRed, 10);
                EndUiGl();
            }
            float lx = box.x + 64f;
            for (int pl = 0; pl < tl.Players.Count && tl.Players.Count > 1; pl++)  // who is which line
            {
                string nm = tl.Players[pl].Name.Length > 8 ? tl.Players[pl].Name.Substring(0, 8) : tl.Players[pl].Name;
                float lw = nm.Length * 5.6f + 18f;
                Ui.Fill(new Rect(lx, box.y + 12f, 8f, 3f), ToColor(GPlayers[pl % GPlayers.Length]));
                Ui.Label(new Rect(lx + 11f, box.y + 4f, lw, 18f), nm, 9, Ui.Muted, TextAnchor.MiddleLeft);
                lx += lw;
            }
            int h = HoverSample(plot, tl, tmax);
            if (h >= 0)
            {
                Ui.Fill(new Rect(Mathf.Round(plot.x + plot.width * tl.Samples[h].T / tmax), plot.y, 1f, plot.height), new Color(1f, 1f, 1f, 0.4f));
                var rows = new List<KeyValuePair<string, Color>>();
                for (int pl = 0; pl < tl.Players.Count; pl++)
                    rows.Add(Row(tl.Players[pl].Name + "  " + Mathf.RoundToInt(tl.Samples[h].Get(pl, TL.Hp)) + "%", ToColor(GPlayers[pl % GPlayers.Length])));
                ChartTip(RunClock(tl.Samples[h].T) + "  ·  wave " + tl.Samples[h].Wave, rows);
            }
        }

        void DrawAccuracyChart(RunTimeline tl, Rect box)
        {
            var acc = tl.AccuracyPerWave(tl.Mine);
            float shots = tl.Total(tl.Mine, TL.Shots), hits = tl.Total(tl.Mine, TL.Hits);
            var plot = ChartFrame(box, "ACCURACY PER WAVE", shots > 0f ? "overall " + Mathf.RoundToInt(Mathf.Min(1f, hits / shots) * 100f) + "%" : "");
            GridY(plot, 100f, "%");
            if (acc.Length == 0 || shots <= 0f)
            {
                Ui.Label(new Rect(plot.x, plot.y, plot.width, plot.height), "No shots fired", 12, Ui.Muted, TextAnchor.MiddleCenter);
                return;
            }
            float bw = plot.width / acc.Length;
            int step = Mathf.Max(1, Mathf.CeilToInt(acc.Length / 10f));
            int hover = -1;
            if (plot.Contains(Event.current.mousePosition))
                hover = Mathf.Clamp((int)((Event.current.mousePosition.x - plot.x) / bw), 0, acc.Length - 1);
            for (int i = 0; i < acc.Length; i++)
            {
                if (acc[i] >= 0f)
                {
                    float bh = plot.height * acc[i] / 100f;
                    var c = ToColor(GGreen);
                    c.a = i == hover ? 1f : acc[i] < 45f ? 0.45f : 0.85f;
                    Ui.Fill(new Rect(plot.x + i * bw + 1.5f, plot.yMax - bh, Mathf.Max(1f, bw - 3f), bh), c);
                }
                if (i % step == 0)
                    Ui.Label(new Rect(plot.x + i * bw - 6f, plot.yMax + 3f, bw + 12f, 14f), (i + 1).ToString(), 9, Ui.Muted, TextAnchor.MiddleCenter);
            }
            if (hover >= 0)
                ChartTip("Wave " + (hover + 1), new List<KeyValuePair<string, Color>> { Row(acc[hover] >= 0f ? Mathf.RoundToInt(acc[hover]) + "% of shots hit" : "no shots", ToColor(GGreen)) });
        }

        /// The last runs side by side, oldest to newest: wave reached (line) or damage dealt (bars). Click one to open it.
        void DrawTrendChart(List<RankLog> runs, Rect box, bool damage)
        {
            int n = Mathf.Min(TrendRuns, runs.Count);
            float top = 0f;
            for (int i = 0; i < n; i++)
                top = Mathf.Max(top, damage ? runs[i].Dealt : runs[i].Wave);
            float ymax = NiceMax(Mathf.Max(top, damage ? 1000f : 5f));
            var plot = ChartFrame(box, damage ? "DAMAGE DEALT BY RUN" : "WAVE REACHED BY RUN", "last " + n + ", oldest to newest  ·  green won, red died");
            GridY(plot, ymax, "");
            float slot = plot.width / Mathf.Max(1, n);
            int hover = plot.Contains(Event.current.mousePosition) ? Mathf.Clamp((int)((Event.current.mousePosition.x - plot.x) / slot), 0, n - 1) : -1;
            Func<int, int> runAt = s => n - 1 - s;  // slot 0 is the oldest of the n
            for (int s = 0; s < n; s++)
            {
                var l = runs[runAt(s)];
                float v = damage ? l.Dealt : l.Wave, cx = plot.x + slot * (s + 0.5f);
                var c = RunWon(l) ? GGreen : GRed;
                bool sel = runAt(s) == recentSel;
                if (damage)
                {
                    float bh = plot.height * Mathf.Clamp01(v / ymax);
                    var bc = ToColor(c);
                    bc.a = sel || s == hover ? 1f : 0.6f;
                    Ui.Fill(new Rect(cx - slot * 0.32f, plot.yMax - bh, slot * 0.64f, bh), bc);
                }
                if (s % Mathf.Max(1, n / 6) == 0 || sel)
                    Ui.Label(new Rect(cx - 24f, plot.yMax + 3f, 48f, 14f), runAt(s) == 0 ? "latest" : "-" + runAt(s), 9, sel ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter);
            }
            if (!damage && BeginUiGl())
            {
                for (int s = 1; s < n; s++)
                    Line(P2(plot.x + slot * (s - 0.5f), plot.yMax - plot.height * Mathf.Clamp01(runs[runAt(s - 1)].Wave / ymax)), P2(plot.x + slot * (s + 0.5f), plot.yMax - plot.height * Mathf.Clamp01(runs[runAt(s)].Wave / ymax)), 2f, GPurple);
                for (int s = 0; s < n; s++)
                {
                    var l = runs[runAt(s)];
                    var pt = P2(plot.x + slot * (s + 0.5f), plot.yMax - plot.height * Mathf.Clamp01(l.Wave / ymax));
                    if (runAt(s) == recentSel)
                        Circle(pt, 8f, GGold, 14);
                    Circle(pt, 5f, RunWon(l) ? GGreen : GRed, 12);
                }
                EndUiGl();
            }
            if (hover >= 0)
            {
                var l = runs[runAt(hover)];
                ChartTip(RunTitle(l) + (l.Class.Length > 0 ? "  ·  " + l.Class : ""), new List<KeyValuePair<string, Color>> {
                    Row("Wave " + l.Wave + (RunWon(l) ? "  ·  won" : ""), RunWon(l) ? Ui.Good : Ui.Text), Row(l.Dealt > 0f ? ShortNumber(l.Dealt) + " damage" : "damage not kept", Ui.Muted), Row("+" + l.Xp + " XP", Ui.Gold) });
                if (Ui.HitArea(plot))
                    recentSel = runAt(hover);
            }
        }
    }
}
