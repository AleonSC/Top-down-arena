using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Arena
{
    /// How the connection feels right now, for the lag chart's coloured pill.
    public enum LagQuality { Good, Ok, Poor }

    /// A thing that happened in the match, ticked on the lag chart so a spike can be lined up with it.
    public enum LagMarkKind : byte { Wave, Boss, Storm }

    public struct LagMark
    {
        public double T;
        public LagMarkKind Kind;
        public string Label;
    }

    /// The numbers behind the lag chart (Game/LagChartUi.cs, F5): round trips to the host, gaps between snapshots
    /// (stalls), your frame rate, bandwidth in and out, each player's ping and match events, kept as a 30 s strip of
    /// quarter-second columns plus totals since the run started (for the copied summary). Plain C#; the clock is
    /// whatever `now` (seconds) the caller passes, so tests can drive it. Docs/LagChart.md.
    public class LagStats
    {
        public const float ColSec = 0.25f;     // one column of the chart
        public const int Cols = 120;           // 30 s of them
        public const float StallMs = 150f;     // a snapshot gap longer than this is a stall
        public const float NormalGapMs = 50f;  // the host sends 20 snapshots a second
        public const float StormBullets = 450f;
        const int MaxPingSamples = 20000;      // for the summary's p95 (an hour and a half at 4 a second)

        // The strip: column i of `Cols`, oldest first through Column(i). NaN = no ping that column (yet).
        readonly float[] ping = new float[Cols], gap = new float[Cols], frameMs = new float[Cols], fps = new float[Cols], kbIn = new float[Cols], kbOut = new float[Cols];
        int head, filled;  // next column to write; columns written so far (up to Cols)

        // The column being gathered.
        double colStart = double.NaN;
        float curPing = float.NaN, curGap, curFrameMax;
        int curFrames;
        long colBytesIn, colBytesOut;

        public readonly List<LagMark> Marks = new List<LagMark>();

        // Totals since Reset.
        readonly List<float> pings = new List<float>();
        readonly int[] frameHist = new int[251];  // frame times in whole ms (250 = 250 or more)
        int frameCount;
        double frameSum;
        float pingSum, jitterSum;
        int jitterN;
        public int Stalls { get; private set; }
        public float WorstGapMs { get; private set; }
        public double Started { get; private set; } = double.NaN;
        long totalIn, totalOut;
        int snapCount;
        long snapBytes;

        // Live values.
        public float LastPing { get; private set; } = float.NaN;
        public float LastSnapBytes { get; private set; }
        double lastSnap = double.NaN, lastStallAt = double.NegativeInfinity;
        float lastStallMs;
        bool countStalls = true;

        /// Each player's round trip to the host as the host last shared it (Msg.NetPings), by player id; the host's own is 0.
        public readonly Dictionary<int, float> PlayerPing = new Dictionary<int, float>();

        /// While true the strip stops moving (Shift+F5): the totals keep counting, the picture holds still.
        public bool Frozen;

        /// A file to append a CSV row to every quarter second, or null (Settings > Gameplay > "Log lag to a file").
        public string LogPath;
        readonly StringBuilder pendingCsv = new StringBuilder();
        double lastFlush;
        public const string CsvHeader = "time_s,ping_ms,snapshot_gap_ms,frame_ms_max,fps,kb_in_s,kb_out_s,event";

        public void Reset(double now)
        {
            Array.Clear(ping, 0, Cols);
            Array.Clear(gap, 0, Cols);
            Array.Clear(frameMs, 0, Cols);
            Array.Clear(fps, 0, Cols);
            Array.Clear(kbIn, 0, Cols);
            Array.Clear(kbOut, 0, Cols);
            for (int i = 0; i < Cols; i++)
                ping[i] = float.NaN;
            head = filled = 0;
            colStart = stripEnd = now;
            curPing = float.NaN;
            curGap = curFrameMax = 0f;
            curFrames = 0;
            colBytesIn = colBytesOut = 0;
            Marks.Clear();
            pings.Clear();
            Array.Clear(frameHist, 0, frameHist.Length);
            frameCount = 0;
            frameSum = 0;
            pingSum = jitterSum = 0f;
            jitterN = 0;
            Stalls = 0;
            WorstGapMs = 0f;
            Started = now;
            totalIn = totalOut = 0;
            snapCount = 0;
            snapBytes = 0;
            LastPing = float.NaN;
            LastSnapBytes = 0f;
            lastSnap = double.NaN;
            lastStallAt = double.NegativeInfinity;
            lastStallMs = 0f;
            PlayerPing.Clear();
            Frozen = false;
            pendingCsv.Length = 0;
            lastFlush = now;
            if (LogPath != null)
                StartLog();
        }

        /// A round trip came back (ms).
        public void AddPing(double now, float ms)
        {
            Advance(now);
            if (!float.IsNaN(LastPing))
            {
                jitterSum += Math.Abs(ms - LastPing);
                jitterN++;
            }
            LastPing = ms;
            curPing = float.IsNaN(curPing) ? ms : Math.Max(curPing, ms);
            pingSum += ms;
            if (pings.Count < MaxPingSamples)
                pings.Add(ms);
        }

        /// A snapshot arrived (clients). `bytes`: its size. `counts`: false while a gap is expected (paused, between runs), so it isn't called a stall.
        public void NoteSnapshot(double now, int bytes, bool counts = true)
        {
            Advance(now);
            if (!double.IsNaN(lastSnap))
            {
                float g = (float)((now - lastSnap) * 1000.0);
                curGap = Math.Max(curGap, g);
                if (counts && countStalls && g > StallMs)
                {
                    Stalls++;
                    lastStallAt = now;
                    lastStallMs = g;
                }
                if (counts && countStalls)
                    WorstGapMs = Math.Max(WorstGapMs, g);
            }
            lastSnap = now;
            countStalls = counts;
            snapCount++;
            snapBytes += bytes;
            LastSnapBytes = LastSnapBytes <= 0f ? bytes : LastSnapBytes * 0.9f + bytes * 0.1f;
        }

        /// One rendered frame of `dt` seconds.
        public void AddFrame(double now, float dt)
        {
            Advance(now);
            float ms = dt * 1000f;
            curFrameMax = Math.Max(curFrameMax, ms);
            curFrames++;
            frameHist[Math.Min(250, Math.Max(0, (int)ms))]++;
            frameCount++;
            frameSum += dt;
        }

        /// Bytes that went in and out since the last call (NetPeer's counters, read each frame).
        public void AddBytes(double now, long inBytes, long outBytes)
        {
            Advance(now);
            colBytesIn += inBytes;
            colBytesOut += outBytes;
            totalIn += inBytes;
            totalOut += outBytes;
        }

        public void Mark(double now, LagMarkKind kind, string label)
        {
            Advance(now);
            Marks.Add(new LagMark { T = now, Kind = kind, Label = label });
            if (LogPath != null)
                pendingCsv.Append(Fmt(now - Started)).Append(",,,,,,,").Append(label).Append('\n');
        }

        /// Close every column that has ended by `now`.
        public void Advance(double now)
        {
            if (double.IsNaN(colStart))
                Reset(now);
            // A gap still open counts toward the column it's in (a stall shows while it happens, not only once it ends).
            while (now >= colStart + ColSec)
            {
                float open = double.IsNaN(lastSnap) ? 0f : (float)((colStart + ColSec - lastSnap) * 1000.0);
                float g = Math.Max(curGap, open > NormalGapMs * 1.5f ? open : 0f);
                Push(curPing, g, curFrameMax, curFrames / ColSec, colBytesIn / ColSec / 1024f, colBytesOut / ColSec / 1024f);
                colStart += ColSec;
                curPing = float.NaN;
                curGap = curFrameMax = 0f;
                curFrames = 0;
                colBytesIn = colBytesOut = 0;
                if (now - colStart > Cols * ColSec)
                    colStart = now - (now - colStart) % ColSec;  // a long gap (a breakpoint, a suspended laptop): skip the empty columns
            }
            while (Marks.Count > 0 && Marks[0].T < now - Cols * ColSec - 1.0)
                Marks.RemoveAt(0);
            if (LogPath != null && now - lastFlush >= 5.0)
                Flush(now);
        }

        void Push(float p, float g, float f, float rate, float inK, float outK)
        {
            if (LogPath != null)
                pendingCsv.Append(Fmt(colStart + ColSec - Started)).Append(',').Append(float.IsNaN(p) ? "" : Fmt(p)).Append(',').Append(Fmt(g)).Append(',')
                    .Append(Fmt(f)).Append(',').Append(Fmt(rate)).Append(',').Append(Fmt(inK)).Append(',').Append(Fmt(outK)).Append(",\n");
            if (Frozen)
                return;
            ping[head] = p;
            gap[head] = g;
            frameMs[head] = f;
            fps[head] = rate;
            kbIn[head] = inK;
            kbOut[head] = outK;
            head = (head + 1) % Cols;
            stripEnd = colStart + ColSec;
            filled = Math.Min(Cols, filled + 1);
        }

        static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        // --- Reading the strip ------------------------------------------------------------

        /// Columns written so far (up to Cols).
        public int Filled => filled;

        /// The end time of the newest column on the strip, for placing marks.
        public double StripEnd => stripEnd;
        double stripEnd = double.NaN;

        int At(int i) => (head - filled + i + Cols * 2) % Cols;

        /// Column i of Filled, oldest first.
        public float PingAt(int i) => ping[At(i)];
        public float GapAt(int i) => gap[At(i)];
        public float FrameMsAt(int i) => frameMs[At(i)];
        public float FpsAt(int i) => fps[At(i)];
        public float KbInAt(int i) => kbIn[At(i)];
        public float KbOutAt(int i) => kbOut[At(i)];

        /// The average of the last `n` columns of a series (NaN columns skipped); NaN if there are none.
        float Recent(Func<int, float> get, int n)
        {
            float sum = 0f;
            int k = 0;
            for (int i = Math.Max(0, filled - n); i < filled; i++)
            {
                float v = get(i);
                if (!float.IsNaN(v))
                {
                    sum += v;
                    k++;
                }
            }
            return k > 0 ? sum / k : float.NaN;
        }

        public float RecentPing => Recent(PingAt, 12);     // the last 3 s
        public float RecentFps => Recent(FpsAt, 4);        // the last second
        public float RecentKbIn => Recent(KbInAt, 4);
        public float RecentKbOut => Recent(KbOutAt, 4);

        /// The worst ping on the strip (30 s), NaN if none.
        public float StripMaxPing
        {
            get
            {
                float m = float.NaN;
                for (int i = 0; i < filled; i++)
                    if (!float.IsNaN(PingAt(i)) && (float.IsNaN(m) || PingAt(i) > m))
                        m = PingAt(i);
                return m;
            }
        }

        // --- Totals -----------------------------------------------------------------------

        public int PingCount => pings.Count;
        public float AvgPing => pings.Count > 0 ? pingSum / pings.Count : float.NaN;
        public float Jitter => jitterN > 0 ? jitterSum / jitterN : 0f;
        public float MinPing => pings.Count > 0 ? Min(pings) : float.NaN;
        public float MaxPing => pings.Count > 0 ? Max(pings) : float.NaN;
        public float P95Ping => Percentile(pings, 0.95f);
        public float AvgFps => frameSum > 0 ? (float)(frameCount / frameSum) : 0f;
        public float AvgSnapBytes => snapCount > 0 ? (float)snapBytes / snapCount : 0f;
        public long TotalIn => totalIn;
        public long TotalOut => totalOut;

        /// The "1% low": the frame rate of the slowest 1% of frames.
        public float FpsLow1
        {
            get
            {
                if (frameCount == 0)
                    return 0f;
                int want = Math.Max(1, frameCount / 100), seen = 0;
                for (int ms = 250; ms >= 0; ms--)
                {
                    seen += frameHist[ms];
                    if (seen >= want)
                        return 1000f / Math.Max(1, ms + 0.5f);
                }
                return 0f;
            }
        }

        static float Min(List<float> v) { float m = v[0]; foreach (float x in v) m = Math.Min(m, x); return m; }
        static float Max(List<float> v) { float m = v[0]; foreach (float x in v) m = Math.Max(m, x); return m; }

        public static float Percentile(List<float> v, float q)
        {
            if (v.Count == 0)
                return float.NaN;
            var s = new List<float>(v);
            s.Sort();
            return s[Math.Min(s.Count - 1, (int)Math.Ceiling(q * s.Count) - 1)];
        }

        /// Green, yellow or red. `host`: you're the host (no ping or snapshots of your own; the worst player's ping counts instead).
        public LagQuality Quality(double now, bool host)
        {
            var q = LagQuality.Good;
            float p = RecentPing;  // (a host's ping line is the worst guest's)
            if (!float.IsNaN(p))
                q = p >= 150f ? LagQuality.Poor : p >= 80f ? LagQuality.Ok : LagQuality.Good;
            if (!host && now - lastStallAt < 5.0)
                q = Worse(q, lastStallMs >= 300f ? LagQuality.Poor : LagQuality.Ok);
            float f = RecentFps;
            if (!float.IsNaN(f) && f > 0f)
                q = Worse(q, f < 20f ? LagQuality.Poor : f < 40f ? LagQuality.Ok : LagQuality.Good);
            return q;
        }

        static LagQuality Worse(LagQuality a, LagQuality b) => a > b ? a : b;

        public static string QualityName(LagQuality q) => q == LagQuality.Good ? "Good" : q == LagQuality.Ok ? "OK" : "Poor";

        /// The text Ctrl+F5 copies: what to paste to whoever's hosting when it felt laggy.
        public string Summary(double now, bool host, Func<int, string> nameOf)
        {
            var sb = new StringBuilder();
            double secs = double.IsNaN(Started) ? 0 : now - Started;
            sb.Append("Lag summary (").Append(host ? "host" : "guest").Append(", ").Append(((int)secs / 60) + "m " + ((int)secs % 60).ToString("00") + "s").Append(", version ")
              .Append(Session.ProtocolVersion).Append(")\n");
            sb.Append("Quality now: ").Append(QualityName(Quality(now, host))).Append('\n');
            if (PingCount > 0)
                sb.Append(host ? "Worst guest ping: min " : "Ping: min ").Append(R(MinPing)).Append(" / avg ").Append(R(AvgPing)).Append(" / p95 ").Append(R(P95Ping)).Append(" / max ").Append(R(MaxPing))
                  .Append(" ms, jitter ").Append(R(Jitter)).Append(" ms\n");
            if (!host)
                sb.Append("Stalls (snapshot gap over ").Append((int)StallMs).Append(" ms): ").Append(Stalls).Append(", worst gap ").Append(R(WorstGapMs)).Append(" ms\n");
            sb.Append("FPS: avg ").Append(R(AvgFps)).Append(", 1% low ").Append(R(FpsLow1)).Append('\n');
            if (secs > 0)
                sb.Append("Bandwidth: in ").Append((totalIn / secs / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)).Append(" KB/s, out ")
                  .Append((totalOut / secs / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)).Append(" KB/s");
            if (!host && snapCount > 0)
                sb.Append(", snapshot ").Append((AvgSnapBytes / 1024f).ToString("0.0", CultureInfo.InvariantCulture)).Append(" KB");
            sb.Append('\n');
            if (PlayerPing.Count > 0)
            {
                sb.Append("Players:");
                bool first = true;
                foreach (var kv in PlayerPing)
                {
                    sb.Append(first ? " " : ", ").Append(nameOf != null ? nameOf(kv.Key) : "#" + kv.Key).Append(' ').Append(R(kv.Value)).Append(" ms");
                    first = false;
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static string R(float v) => float.IsNaN(v) ? "-" : ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);

        // --- The CSV log ----------------------------------------------------------------------

        void StartLog()
        {
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                if (!File.Exists(LogPath))
                    File.WriteAllText(LogPath, CsvHeader + "\n");
            }
            catch (Exception)
            {
                LogPath = null;  // can't write there: stop trying
            }
        }

        /// Append what's gathered to the log file (every 5 s, and when the run ends).
        public void Flush(double now)
        {
            lastFlush = now;
            if (LogPath == null || pendingCsv.Length == 0)
                return;
            try
            {
                File.AppendAllText(LogPath, pendingCsv.ToString());
            }
            catch (Exception)
            {
                LogPath = null;
            }
            pendingCsv.Length = 0;
        }
    }
}
