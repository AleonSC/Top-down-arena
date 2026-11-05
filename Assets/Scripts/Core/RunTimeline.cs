using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Arena
{
    /// What a timeline records for each player at each sample. The first nine are running totals (a rate is the
    /// difference between two samples); Hp is the health percentage at that moment.
    public enum TL { Dealt, Taken, Heal, Kills, Downs, Revives, Shots, Hits, Abilities, Hp }

    public class TimelinePlayer
    {
        public int Id;
        public string Name = "";
        public bool Mine;  // the player on this computer
    }

    public class TimelineSample
    {
        public float T;       // seconds of play (pauses excluded)
        public int Wave;
        public bool Boss;     // a boss wave was on
        public float[][] V = new float[0][];  // [player][(int)TL]; shorter than the player list when players joined later

        public float Get(int player, TL ch) => player < V.Length && V[player] != null ? V[player][(int)ch] : 0f;
    }

    /// One run as a curve: every RunTimeline.Every seconds each game saves everyone's running totals and health
    /// (Docs/RecentRuns.md). A "damage per second" chart is the difference between two samples. Kept as a text file
    /// beside the profile (RunTimelineStore) for the Records > Recent runs page. No Unity, no network.
    public class RunTimeline
    {
        public const int FormatVersion = 1;
        public const float Every = 10f;
        public const int Channels = 10;

        public string Id = "";                 // the file's name without ".run"
        public string Mode = "", Class = "", Mods = "", Diff = "";
        public bool Won;
        public int Wave;
        public float Seconds;
        public readonly List<TimelinePlayer> Players = new List<TimelinePlayer>();
        public readonly List<TimelineSample> Samples = new List<TimelineSample>();

        public int Mine
        {
            get
            {
                for (int i = 0; i < Players.Count; i++)
                    if (Players[i].Mine)
                        return i;
                return 0;
            }
        }

        /// The index of this player, added on first sight.
        public int PlayerIndex(int id, string name, bool mine)
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].Id == id)
                    return i;
            Players.Add(new TimelinePlayer { Id = id, Name = name ?? "", Mine = mine });
            return Players.Count - 1;
        }

        /// Starts a sample; fill it in with Set for each player.
        public TimelineSample Begin(float t, int wave, bool boss)
        {
            var s = new TimelineSample { T = t, Wave = wave, Boss = boss, V = new float[Players.Count][] };
            Samples.Add(s);
            return s;
        }

        public void Set(TimelineSample s, int player, RunStats r, float hpPct)
        {
            if (player >= s.V.Length)
                Array.Resize(ref s.V, player + 1);
            var v = new float[Channels];
            v[(int)TL.Dealt] = r.DamageDealt;
            v[(int)TL.Taken] = r.DamageTaken;
            v[(int)TL.Heal] = r.HealingDone;
            v[(int)TL.Kills] = r.Kills;
            v[(int)TL.Downs] = r.Downs;
            v[(int)TL.Revives] = r.Revives;
            v[(int)TL.Shots] = r.ShotsFired;
            v[(int)TL.Hits] = r.ShotsHit;
            v[(int)TL.Abilities] = r.AbilitiesUsed;
            v[(int)TL.Hp] = hpPct;
            s.V[player] = v;
        }

        /// A channel at every sample (running totals as they are; Hp as a percentage).
        public float[] Series(int player, TL ch)
        {
            var a = new float[Samples.Count];
            for (int i = 0; i < a.Length; i++)
                a[i] = Samples[i].Get(player, ch);
            return a;
        }

        /// A running-total channel as a rate per second at every sample (the first is 0).
        public float[] Rates(int player, TL ch)
        {
            var a = new float[Samples.Count];
            for (int i = 1; i < a.Length; i++)
            {
                float dt = Samples[i].T - Samples[i - 1].T;
                a[i] = dt > 0.01f ? Math.Max(0f, Samples[i].Get(player, ch) - Samples[i - 1].Get(player, ch)) / dt : 0f;
            }
            return a;
        }

        /// The last sample's running total of a channel (what that player did over the run).
        public float Total(int player, TL ch) => Samples.Count == 0 ? 0f : Samples[Samples.Count - 1].Get(player, ch);

        /// How many times this player went down: the rise in the Downs total.
        public List<int> DownSamples(int player)
        {
            var at = new List<int>();
            for (int i = 1; i < Samples.Count; i++)
                if (Samples[i].Get(player, TL.Downs) > Samples[i - 1].Get(player, TL.Downs))
                    at.Add(i);
            return at;
        }

        /// This player's accuracy (0..100) in each wave from 1 to the last one reached, -1 for a wave with no shots.
        /// Shots and hits between two samples count towards the later sample's wave.
        public float[] AccuracyPerWave(int player)
        {
            int waves = 0;
            foreach (var s in Samples)
                waves = Math.Max(waves, s.Wave);
            var shots = new float[waves];
            var hits = new float[waves];
            for (int i = 1; i < Samples.Count; i++)
            {
                int w = Samples[i].Wave - 1;
                if (w < 0 || w >= waves)
                    continue;
                shots[w] += Math.Max(0f, Samples[i].Get(player, TL.Shots) - Samples[i - 1].Get(player, TL.Shots));
                hits[w] += Math.Max(0f, Samples[i].Get(player, TL.Hits) - Samples[i - 1].Get(player, TL.Hits));
            }
            var acc = new float[waves];
            for (int w = 0; w < waves; w++)
                acc[w] = shots[w] > 0f ? Math.Min(100f, hits[w] / shots[w] * 100f) : -1f;
            return acc;
        }

        // --- Text ---------------------------------------------------------------------------------------

        static string Tok(string s) => string.IsNullOrEmpty(s) ? "-" : s.Replace(' ', '_');
        static string UnTok(string s) => s == "-" ? "" : s.Replace('_', ' ');

        public string ToText()
        {
            var sb = new StringBuilder();
            RunSave.Line(sb, "runlog", FormatVersion);
            RunSave.Line(sb, "meta", (Won ? 1 : 0) + " " + Wave + " " + RunSave.F(Seconds) + " " + Tok(Mode) + " " + Tok(Class) + " " + Tok(Diff) + " " + Tok(Mods));
            foreach (var p in Players)
                RunSave.Line(sb, "player", p.Id + " " + (p.Mine ? 1 : 0) + " " + Tok(p.Name));
            foreach (var s in Samples)
            {
                sb.Append("s ").Append(RunSave.F(s.T)).Append(' ').Append(s.Wave).Append(' ').Append(s.Boss ? 1 : 0).Append(' ').Append(s.V.Length);
                for (int p = 0; p < s.V.Length; p++)
                {
                    if (s.V[p] == null)
                    {
                        sb.Append(" ~");
                        continue;
                    }
                    sb.Append(' ');
                    for (int c = 0; c < Channels; c++)
                        sb.Append(c == 0 ? "" : ",").Append(RunSave.F(s.V[p][c]));
                }
                sb.Append('\n');
            }
            RunSave.Line(sb, "end", 1);
            return sb.ToString();
        }

        /// Reads a timeline back; null if it isn't one, is cut off (no "end" line) or is a newer format.
        public static RunTimeline Parse(string text)
        {
            var t = new RunTimeline();
            bool header = false, end = false;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                    continue;
                int sp = line.IndexOf(' ');
                string key = sp < 0 ? line : line.Substring(0, sp), val = sp < 0 ? "" : line.Substring(sp + 1);
                var f = val.Split(' ');
                try
                {
                    switch (key)
                    {
                        case "runlog":
                            if (RunSave.I(f[0]) > FormatVersion)
                                return null;
                            header = true;
                            break;
                        case "meta":
                            t.Won = RunSave.I(f[0]) != 0; t.Wave = RunSave.I(f[1]); t.Seconds = RunSave.Fl(f[2]);
                            t.Mode = UnTok(f[3]); t.Class = UnTok(f[4]); t.Diff = UnTok(f[5]); t.Mods = f.Length > 6 ? UnTok(f[6]) : "";
                            break;
                        case "player":
                            t.Players.Add(new TimelinePlayer { Id = RunSave.I(f[0]), Mine = RunSave.I(f[1]) != 0, Name = UnTok(f[2]) });
                            break;
                        case "s":
                        {
                            var s = new TimelineSample { T = RunSave.Fl(f[0]), Wave = RunSave.I(f[1]), Boss = RunSave.I(f[2]) != 0 };
                            int n = RunSave.I(f[3]);
                            s.V = new float[n][];
                            for (int p = 0; p < n; p++)
                            {
                                if (f[4 + p] == "~")
                                    continue;
                                var c = f[4 + p].Split(',');
                                var v = new float[Channels];
                                for (int i = 0; i < Channels && i < c.Length; i++)
                                    v[i] = RunSave.Fl(c[i]);
                                s.V[p] = v;
                            }
                            t.Samples.Add(s);
                            break;
                        }
                        case "end": end = true; break;
                    }
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is OverflowException)
                {
                    return null;
                }
            }
            return header && end ? t : null;
        }
    }

    /// The timelines of finished runs: Profile.Folder/runs/<id>.run, the newest Profile.RecentKept kept.
    public static class RunTimelineStore
    {
        static string Dir => Profile.Folder == null ? null : Path.Combine(Profile.Folder, "runs");

        /// Writes the timeline and trims the oldest files beyond `keep`. Returns the file's name ("" if nothing was written).
        public static string Save(RunTimeline t, int keep)
        {
            string dir = Dir;
            if (dir == null || t.Samples.Count < 2)
                return "";
            try
            {
                Directory.CreateDirectory(dir);
                if (t.Id.Length == 0)
                    t.Id = RunSave.NewId();
                string path = Path.Combine(dir, t.Id + ".run"), tmp = path + ".tmp";
                File.WriteAllText(tmp, t.ToText());
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
                var files = new List<string>(Directory.GetFiles(dir, "*.run"));
                files.Sort(StringComparer.Ordinal);  // ids start with the date, so oldest first
                for (int i = 0; i < files.Count - keep; i++)
                    File.Delete(files[i]);
                return t.Id;
            }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }

        /// The run's timeline, or null (no file, damaged, newer format).
        public static RunTimeline Load(string id)
        {
            string dir = Dir;
            if (dir == null || string.IsNullOrEmpty(id) || id.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                return null;
            try
            {
                string path = Path.Combine(dir, id + ".run");
                if (!File.Exists(path))
                    return null;
                var t = RunTimeline.Parse(File.ReadAllText(path));
                if (t != null)
                    t.Id = id;
                return t;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }

    /// Each game samples the run as it goes (every game, so a client's profile gets graphs too), and the run's end
    /// (RunSaves.cs, RecordKind.End) finishes and keeps it.
    public partial class Game
    {
        RunTimeline timeline;
        float timelineT, timelineNext;

        /// A match started: a fresh timeline (a resumed run's earlier part isn't kept, the curve starts here).
        void ResetTimeline()
        {
            timeline = null;
            timelineT = 0f;
            timelineNext = 0f;
        }

        bool TimelineOn => Running && RecordsRuns && Hunt == null && Me != null && !Replaying && !botTouched && !devTouched
            && (State == MatchState.Fighting || State == MatchState.Intermission);

        void TickRunTimeline(float dt)
        {
            if (!TimelineOn)
                return;
            timelineT += dt;
            if (timeline != null && timelineT < timelineNext)
                return;
            TakeTimelineSample();
            timelineNext = timelineT + RunTimeline.Every;
        }

        void TakeTimelineSample()
        {
            if (timeline == null)
                timeline = new RunTimeline();
            var s = timeline.Begin(timelineT, Wave, State == MatchState.Fighting && IsBossWave(Wave));
            foreach (var p in Players.Values)
            {
                int i = timeline.PlayerIndex(p.Id, p.Name, p.Id == MyId);
                timeline.Set(s, i, p.Run, p.MaxHp > 0f ? Math.Max(0f, Math.Min(100f, p.Hp / p.MaxHp * 100f)) : 0f);
            }
        }

        /// The run ended: closes the curve with a last sample and keeps it. Returns the file's name ("" if none was kept).
        string FinishTimeline(bool won, int wave, bool endless, float time, string cls, string mods)
        {
            if (timeline == null)
                return "";
            timelineT = Math.Max(timelineT, timeline.Samples.Count > 0 ? timeline.Samples[timeline.Samples.Count - 1].T + 0.01f : 0f);
            TakeTimelineSample();
            timeline.Won = won;
            timeline.Wave = wave;
            timeline.Seconds = Math.Max(time, timelineT);
            timeline.Mode = endless ? "Endless" : "Campaign";
            timeline.Class = cls;
            timeline.Diff = Diff.Level.ToString();
            timeline.Mods = mods ?? "";
            string id = RunTimelineStore.Save(timeline, Arena.Profile.RecentKept);
            timeline = null;
            return id;
        }
    }
}
