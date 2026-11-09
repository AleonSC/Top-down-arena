using System;
using UnityEngine;

namespace Arena
{
    /// The lag chart (F5 cycles off > pill > chart; Shift+F5 freezes the chart, Ctrl+F5 copies a summary; Settings >
    /// Gameplay also sets it and turns on the CSV log). Top right in a match: a 30 s strip of your ping, the gaps between
    /// snapshots (red past 150 ms: a stall) and your frame time, with wave, boss and bullet-storm ticks; under it the
    /// numbers, bandwidth and every player's ping. The numbers come from Core/LagStats.cs (fed by Core/LagNet.cs).
    public partial class ArenaApp
    {
        public const int LagOff = 0, LagPill = 1, LagFull = 2;
        static readonly string[] LagModeNames = { "Off", "Pill", "Chart" };

        int lagMode;      // PlayerPrefs "lag_chart"
        bool lagLog;      // PlayerPrefs "lag_log": write a CSV each run to <profile>/lag/
        float lagCopiedAt = -99f;
        bool lagPreviewGuest;  // Tools/Preview only (ShotLag.cs): draw as a guest though the preview's session hosts

        static readonly Color LagAccent = new Color(0.35f, 0.85f, 1f);
        static readonly Color LagFrameCol = new Color(1f, 0.85f, 0.3f);
        static readonly Color LagGapCol = new Color(0.55f, 0.6f, 0.85f);
        static readonly Color LagStallCol = new Color(1f, 0.38f, 0.38f);
        static readonly Color LagPoorCol = new Color(1f, 0.45f, 0.45f);

        void LoadLagChart()
        {
            lagMode = Mathf.Clamp(PlayerPrefs.GetInt("lag_chart", 0), 0, 2);
            lagLog = PlayerPrefs.GetInt("lag_log", 0) == 1;
        }

        void SetLagMode(int mode)
        {
            lagMode = ((mode % 3) + 3) % 3;
            PlayerPrefs.SetInt("lag_chart", lagMode);
            PlayerPrefs.Save();
        }

        void SetLagLog(bool on)
        {
            lagLog = on;
            PlayerPrefs.SetInt("lag_log", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// The folder the CSV logs go in (shown in Settings).
        static string LagLogDir => System.IO.Path.Combine(Profile.Folder ?? Application.persistentDataPath, "lag");

        /// Every frame: feed the frame time, keep the log folder current, and the F5 keys.
        void LagChartUpdate()
        {
            game.LagLogFolder = lagLog ? LagLogDir : null;  // (taken at the start of each run)
            game.LagFrame(Time.unscaledDeltaTime);
            if (!Input.GetKeyDown(KeyCode.F5) || GUIUtility.keyboardControl != 0)
                return;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl)
                CopyLagSummary();
            else if (shift)
            {
                if (lagMode == LagOff)
                    SetLagMode(LagFull);
                game.Lag.Frozen = !game.Lag.Frozen;
            }
            else
                SetLagMode(lagMode + 1);
        }

        void CopyLagSummary()
        {
            GUIUtility.systemCopyBuffer = game.Lag.Summary(game.LagNow, session.IsHost, LagName);
            lagCopiedAt = Time.unscaledTime;
        }

        string LagName(int id)
        {
            Player p;
            return game.Players.TryGetValue(id, out p) ? p.Name : "#" + id;
        }

        static Color LagQualityColor(LagQuality q) => q == LagQuality.Good ? Ui.Good : q == LagQuality.Ok ? Ui.Warn : LagPoorCol;

        static string Ms(float v) => float.IsNaN(v) ? "-" : Mathf.RoundToInt(v).ToString();

        /// The number shown in the pill: your ping (a guest), the worst player's ping (a host with company), or your frame rate.
        string LagHeadline(bool host)
        {
            var lag = game.Lag;
            if (!float.IsNaN(lag.RecentPing))
                return Ms(lag.RecentPing) + " ms";  // (a host's: the worst guest's)
            float f = lag.RecentFps;
            return float.IsNaN(f) ? "-" : Mathf.RoundToInt(f) + " fps";
        }

        /// The HUD: the pill, or the whole chart.
        void DrawLagChart()
        {
            if (lagMode == LagOff || session.Screen != AppScreen.Game)
                return;
            bool host = session.IsHost && !lagPreviewGuest;
            var lag = game.Lag;
            double now = lag.Filled > 0 && lagPreviewGuest ? lag.StripEnd : game.LagNow;
            var q = lag.Quality(now, host);
            Color qc = LagQualityColor(q);
            if (lagMode == LagPill)
            {
                var pr = new Rect(W - 14f - 132f, 30f, 132f, 26f);
                Ui.HudPanel(pr, qc);
                Ui.Dot(new Rect(pr.x + 10f, pr.y + 9f, 8f, 8f), qc);
                Ui.Label(new Rect(pr.x + 24f, pr.y, 60f, pr.height), LagStats.QualityName(q), 11, qc, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(pr.x + 60f, pr.y, pr.width - 70f, pr.height), LagHeadline(host), 11, Color.white, TextAnchor.MiddleRight, true);
                return;
            }

            int rows = game.Players.Count > 1 ? 4 : 3;
            var r = new Rect(W - 14f - 340f, 30f, 340f, 150f + rows * 16f);
            Ui.HudPanel(r, LagAccent);
            // Header: LAG, the quality, and the keys (or "Copied").
            Ui.Label(new Rect(r.x + 12f, r.y + 6f, 40f, 16f), "LAG", 11, LagAccent, TextAnchor.MiddleLeft, true);
            Ui.Dot(new Rect(r.x + 44f, r.y + 10f, 8f, 8f), qc);
            Ui.Label(new Rect(r.x + 56f, r.y + 6f, 120f, 16f), LagStats.QualityName(q) + "  " + LagHeadline(host), 11, qc, TextAnchor.MiddleLeft, true);
            bool copied = Time.unscaledTime - lagCopiedAt < 2f;
            Ui.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 16f), copied ? "Summary copied" : "F5  ·  Shift+F5 freeze  ·  Ctrl+F5 copy", 9,
                copied ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight, copied);

            var plot = new Rect(r.x + 34f, r.y + 28f, r.width - 46f, 92f);
            DrawLagPlot(plot, host);

            // Legend.
            float ly = plot.yMax + 5f, lx = plot.x;
            LagLegend(ref lx, ly, LagAccent, host ? "worst guest ping" : "ping");
            LagLegend(ref lx, ly, LagFrameCol, "frame time");
            if (!host)
            {
                LagLegend(ref lx, ly, LagGapCol, "snapshot gap");
                LagLegend(ref lx, ly, LagStallCol, "stall");
            }

            // The numbers.
            float y = plot.yMax + 24f, x = r.x + 12f, w = r.width - 24f;
            if (host && lag.PingCount == 0)
                LagRow(x, y, w, "Ping", "you're the host" + (game.Players.Count > 1 ? " (waiting for the guests' pings)" : ""), null);
            else
                LagRow(x, y, w, host ? "Guests" : "Ping", (host ? "worst " : "now ") + Ms(lag.RecentPing) + "  ·  avg " + Ms(lag.AvgPing) + "  ·  p95 " + Ms(lag.P95Ping) + "  ·  max " + Ms(lag.MaxPing)
                    + "  ·  jitter " + Ms(lag.Jitter), lag.RecentPing >= 150f ? LagPoorCol : (Color?)null);
            y += 16f;
            string stalls = host ? "" : lag.Stalls + (lag.Stalls > 0 ? " (worst " + Ms(lag.WorstGapMs) + " ms)" : "") + "  ·  ";
            float fpsNow = lag.RecentFps;
            LagRow(x, y, w, host ? "Frames" : "Stalls", stalls + "FPS " + (float.IsNaN(fpsNow) ? "-" : Mathf.RoundToInt(fpsNow).ToString())
                + "  ·  1% low " + Mathf.RoundToInt(lag.FpsLow1), !host && lag.Stalls > 0 ? Ui.Warn : (Color?)null);
            y += 16f;
            string snap = host ? "" : "  ·  snapshot " + (lag.LastSnapBytes / 1024f).ToString("0.0") + " KB";
            LagRow(x, y, w, "Data", "in " + Kb(lag.RecentKbIn) + "  ·  out " + Kb(lag.RecentKbOut) + snap, null);
            y += 16f;
            if (rows == 4)
            {
                Ui.Label(new Rect(x, y, 52f, 16f), "Players", 10, Ui.Muted);
                float px = x + 52f;
                foreach (var p in game.Players.Values)
                {
                    float ms;
                    bool known = lag.PlayerPing.TryGetValue(p.Id, out ms);
                    string text = p.Name + " " + (p.Id == 1 ? "host" : known ? Ms(ms) + " ms" : "-");  // (the host is always player 1)
                    float tw = Mathf.Min(w - (px - x), 7f * text.Length);
                    if (tw <= 20f)
                        break;
                    Ui.Label(new Rect(px, y, tw, 16f), text, 10, known && ms >= 150f ? LagPoorCol : ToColor(p.Def.Color), TextAnchor.MiddleLeft, p.Id == game.MyId);
                    px += tw + 6f;
                }
            }
        }

        static string Kb(float v) => float.IsNaN(v) ? "-" : v.ToString("0.0") + " KB/s";

        static void LagRow(float x, float y, float w, string head, string text, Color? c)
        {
            Ui.Label(new Rect(x, y, 52f, 16f), head, 10, Ui.Muted);
            Ui.Label(new Rect(x + 52f, y, w - 52f, 16f), text, 10, c ?? Ui.Text);
        }

        static void LagLegend(ref float x, float y, Color c, string text)
        {
            Ui.Fill(new Rect(x, y + 6f, 10f, 3f), c);
            float tw = 6f * text.Length + 8f;
            Ui.Label(new Rect(x + 13f, y, tw, 14f), text, 9, Ui.Muted);
            x += 13f + tw + 4f;
        }

        /// The 30 s strip: gap bars, the frame-time and ping lines, the 150 ms stall line and the event ticks.
        void DrawLagPlot(Rect plot, bool host)
        {
            var lag = game.Lag;
            int n = lag.Filled;
            Ui.Fill(plot, new Color(0f, 0f, 0f, 0.35f));
            // The scale: at least 200 ms, more when the ping or a frame went past it (to the next 100, up to 1 s). Snapshot gaps
            // don't stretch it: a stall taller than the plot is cut off at the top with its length written there.
            float top = 0f;
            for (int i = 0; i < n; i++)
            {
                float p = lag.PingAt(i);
                if (!float.IsNaN(p))
                    top = Mathf.Max(top, p);
                top = Mathf.Max(top, Mathf.Min(lag.FrameMsAt(i), 250f));
            }
            float ymax = Mathf.Clamp(Mathf.Ceil(top * 1.15f / 100f) * 100f, 200f, 1000f);
            Func<float, float> Y = v => plot.yMax - plot.height * Mathf.Clamp01(v / ymax);
            float colW = plot.width / LagStats.Cols;
            Func<int, float> X = i => plot.xMax - (n - i) * colW;  // column i's left edge (the newest column ends at the right)

            // Grid: 50, 100, 150 (the stall line, red), then every 100.
            for (float g = 50f; g < ymax; g += g < 150f ? 50f : 100f)
            {
                bool stall = Mathf.Abs(g - LagStats.StallMs) < 0.5f && !host;
                Ui.Fill(new Rect(plot.x, Mathf.Round(Y(g)), plot.width, 1f), stall ? new Color(1f, 0.38f, 0.38f, 0.35f) : new Color(1f, 1f, 1f, 0.07f));
                if (g <= 150f || g % 200f == 0f)
                    Ui.Label(new Rect(plot.x - 28f, Y(g) - 7f, 25f, 14f), g.ToString("0"), 8, stall ? LagStallCol : Ui.Muted, TextAnchor.MiddleRight);
            }
            Ui.Label(new Rect(plot.x - 28f, plot.y - 2f, 25f, 12f), "ms", 8, Ui.Muted, TextAnchor.MiddleRight);

            // Snapshot gaps as bars (guests): faint when normal, red when a stall.
            if (!host)
                for (int i = 0; i < n; i++)
                {
                    float g = lag.GapAt(i);
                    if (g <= 0f)
                        continue;
                    bool stall = g > LagStats.StallMs;
                    float y0 = Y(g);
                    Ui.Fill(new Rect(X(i), y0, Mathf.Max(1f, colW - 0.5f), plot.yMax - y0), stall ? new Color(1f, 0.38f, 0.38f, 0.8f) : new Color(LagGapCol.r, LagGapCol.g, LagGapCol.b, 0.16f));
                    if (g > ymax)
                        Ui.Label(new Rect(X(i) - 30f, plot.y + 13f, 28f, 12f), Mathf.RoundToInt(g).ToString(), 8, LagStallCol, TextAnchor.MiddleRight, true);
                }

            // Event ticks.
            double span = LagStats.Cols * LagStats.ColSec;
            foreach (var m in lag.Marks)
            {
                double age = lag.StripEnd - m.T;
                if (age < 0 || age > span)
                    continue;
                float mx = Mathf.Round(plot.xMax - (float)(age / span) * plot.width);
                Color mc = m.Kind == LagMarkKind.Boss ? new Color(0.85f, 0.5f, 1f) : m.Kind == LagMarkKind.Storm ? new Color(1f, 0.6f, 0.25f) : Color.white;
                Ui.Fill(new Rect(mx, plot.y, 1f, plot.height), new Color(mc.r, mc.g, mc.b, 0.45f));
                Ui.Label(new Rect(mx + 2f, plot.y + 1f, 70f, 12f), m.Label, 8, mc, TextAnchor.MiddleLeft, true);
            }

            // The lines (GL).
            if (n > 1 && BeginUiGl())
            {
                UiClip(plot);
                Rgba fc = new Rgba(LagFrameCol.r, LagFrameCol.g, LagFrameCol.b, 0.85f), pc = new Rgba(LagAccent.r, LagAccent.g, LagAccent.b, 1f);
                for (int i = 1; i < n; i++)
                    Line(new V2(X(i - 1) + colW * 0.5f, Y(lag.FrameMsAt(i - 1))), new V2(X(i) + colW * 0.5f, Y(lag.FrameMsAt(i))), 1.2f, fc);
                {
                    float last = float.NaN;
                    int lastI = -1;
                    for (int i = 0; i < n; i++)
                    {
                        float p = lag.PingAt(i);
                        if (float.IsNaN(p))
                            continue;
                        if (lastI >= 0)
                            Line(new V2(X(lastI) + colW * 0.5f, Y(last)), new V2(X(i) + colW * 0.5f, Y(p)), 2f, pc);
                        last = p;
                        lastI = i;
                    }
                }
                EndUiGl();
            }

            if (lag.Frozen)
            {
                var fr = new Rect(plot.xMax - 62f, plot.y + 3f, 58f, 14f);
                Ui.Fill(fr, new Color(0f, 0f, 0f, 0.6f));
                Ui.Label(fr, "FROZEN", 9, LagAccent, TextAnchor.MiddleCenter, true);
            }
            if (n == 0)
                Ui.Label(plot, "waiting for the run to start", 10, Ui.Muted, TextAnchor.MiddleCenter);
        }

        // --- Settings > Gameplay -----------------------------------------------------------

        /// The lag chart row: Off / Pill / Chart, and the CSV log switch. Returns the height used.
        float DrawLagSettingsRow(float x, float y, float w)
        {
            float half = (w - 36f) / 2f;
            WinLabel(new Rect(x, y, half, 24f), "Lag chart", "F5 cycles it in a match; Ctrl+F5 copies a summary");
            int pick = WinChoices(new Rect(x, y + 46f, half, 34f), LagModeNames, lagMode);  // (under the line, as the HUD row's)
            if (pick != lagMode)
                SetLagMode(pick);
            bool log = SwitchRow(new Rect(x + half + 36f, y, half, 54f), lagLog, "Log lag to a file", "A CSV each run in your profile's lag folder");
            if (log != lagLog)
                SetLagLog(log);
            DashLine(x, y + 88f, w);
            return 96f;
        }
    }
}
