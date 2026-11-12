using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Arena
{
    /// One message the host sent, and the frame it was sent in.
    public struct ReplayMsg
    {
        public int Frame;
        public byte[] Data;
        public Msg Type => (Msg)Data[0];
    }

    /// A spot on the timeline: a wave starting, a boss wave starting, or a player going down.
    public struct ReplayMark
    {
        public const byte Wave = 0, Boss = 1, Down = 2;
        public int Frame;
        public byte Kind;
        public int Value;  // the wave number, or the player's id
    }

    /// A recorded run: every message the host sent to its clients (so a replay is a client watching the match), the
    /// length of each frame, and a few marks for the timeline. Saved as a gzipped .replay file; the small header comes
    /// first so the list can show a replay without reading all of it. Nothing about it touches the network protocol.
    public class ReplayFile
    {
        public const string Ext = ".replay";
        public const int FormatVersion = 6;  // 2: the match-start message carries each player's perks and snapshots carry Riot Gear's timer; 3: snapshots carry a Bone Phalanx's walk and skeletons; 4: Soul Link's player fields and the Soul Ghost minion kind are gone; 5: ZoneKind.Lantern is gone, so later zone kinds renumbered; 6: ZoneKind.Haven too (old replays no longer read)
        static readonly byte[] Magic = { (byte)'A', (byte)'R', (byte)'N', (byte)'R' };

        // Header (what the list shows).
        public string Path;                // where it lives (not stored in the file)
        public DateTime Created;           // UTC
        public string Mode = "Standard";   // Standard, Endless, Practice, Boss Hunt
        public string Party = "";          // "Mia (Knight), Bo (Healer)"
        public int MaxWave;
        public float Seconds;
        public int Players;

        // How the match was set up (the replay's session needs it to read the Start message the way the host did).
        public bool Practice;
        public string Plan = "";
        public bool Hunt;
        public EnemyType HuntBoss;
        public RunMod Mods;
        public Difficulty Diff = new Difficulty();

        // Body.
        public readonly List<ReplayMark> Marks = new List<ReplayMark>();
        public readonly List<float> Dts = new List<float>();         // Dts[i] is frame i+1's length in seconds
        public readonly List<ReplayMsg> Msgs = new List<ReplayMsg>();

        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path ?? "");
        public int Frames => Dts.Count;
        public long Bytes;  // file size, for the list

        float[] times;
        /// Seconds from the start to the end of frame f (0 for f = 0).
        public float TimeAt(int f)
        {
            if (times == null || times.Length != Dts.Count + 1)
            {
                times = new float[Dts.Count + 1];
                double t = 0;
                for (int i = 0; i < Dts.Count; i++)
                    times[i + 1] = (float)(t += Dts[i]);
            }
            return times[Math.Max(0, Math.Min(Dts.Count, f))];
        }

        /// The last frame that ends at or before this time.
        public int FrameAt(float seconds)
        {
            TimeAt(0);
            int lo = 0, hi = Dts.Count;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (times[mid] <= seconds) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        // --- Saving and loading ----------------------------------------------------------------------------------------

        public void Save(string path)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create))
            {
                fs.Write(Magic, 0, Magic.Length);
                using (var gz = new GZipStream(fs, CompressionMode.Compress))
                using (var w = new BinaryWriter(gz))
                {
                    w.Write(FormatVersion);
                    w.Write(Created.Ticks);
                    w.Write(Mode);
                    w.Write(Party);
                    w.Write(MaxWave);
                    w.Write(Seconds);
                    w.Write(Players);
                    w.Write(Practice);
                    w.Write(Plan ?? "");
                    w.Write(Hunt);
                    w.Write((byte)HuntBoss);
                    w.Write((int)Mods);
                    Diff.Write(w);
                    w.Write(Marks.Count);
                    foreach (var m in Marks)
                    {
                        w.Write(m.Frame);
                        w.Write(m.Kind);
                        w.Write(m.Value);
                    }
                    w.Write(Dts.Count);
                    foreach (float d in Dts)
                        w.Write(d);
                    w.Write(Msgs.Count);
                    foreach (var m in Msgs)
                    {
                        w.Write(m.Frame);
                        w.Write(m.Data.Length);
                        w.Write(m.Data);
                    }
                }
            }
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
            Path = path;
        }

        /// Just the header (cheap); null if the file isn't a replay this version can read.
        public static ReplayFile ReadHeader(string path) => Read(path, false);

        public static ReplayFile Load(string path) => Read(path, true);

        static ReplayFile Read(string path, bool body)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var magic = new byte[4];
                    if (fs.Read(magic, 0, 4) != 4 || !magic.SequenceEqual(Magic))
                        return null;
                    using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                    using (var r = new BinaryReader(gz))
                    {
                        if (r.ReadInt32() != FormatVersion)
                            return null;
                        var f = new ReplayFile { Path = path, Bytes = new FileInfo(path).Length };
                        f.Created = new DateTime(r.ReadInt64(), DateTimeKind.Utc);
                        f.Mode = r.ReadString();
                        f.Party = r.ReadString();
                        f.MaxWave = r.ReadInt32();
                        f.Seconds = r.ReadSingle();
                        f.Players = r.ReadInt32();
                        f.Practice = r.ReadBoolean();
                        f.Plan = r.ReadString();
                        f.Hunt = r.ReadBoolean();
                        f.HuntBoss = (EnemyType)r.ReadByte();
                        f.Mods = (RunMod)r.ReadInt32();
                        f.Diff = Difficulty.Read(r);
                        if (!body)
                            return f;
                        int n = r.ReadInt32();
                        for (int i = 0; i < n; i++)
                            f.Marks.Add(new ReplayMark { Frame = r.ReadInt32(), Kind = r.ReadByte(), Value = r.ReadInt32() });
                        n = r.ReadInt32();
                        for (int i = 0; i < n; i++)
                            f.Dts.Add(r.ReadSingle());
                        n = r.ReadInt32();
                        for (int i = 0; i < n; i++)
                        {
                            int frame = r.ReadInt32();
                            f.Msgs.Add(new ReplayMsg { Frame = frame, Data = r.ReadBytes(r.ReadInt32()) });
                        }
                        return f;
                    }
                }
            }
            catch (Exception)
            {
                return null;  // damaged, cut short, or not ours
            }
        }

        // --- The folder ------------------------------------------------------------------------------------------------

        /// Every readable replay in the folder, newest first.
        public static List<ReplayFile> List(string folder)
        {
            var list = new List<ReplayFile>();
            if (!Directory.Exists(folder))
                return list;
            foreach (var p in Directory.GetFiles(folder, "*" + Ext))
            {
                var h = ReadHeader(p);
                if (h != null)
                    list.Add(h);
            }
            list.Sort((a, b) => b.Created.CompareTo(a.Created));
            return list;
        }

        /// A name safe for a file: no slashes or other characters files can't have; empty gives "Replay".
        public static string CleanName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in (name ?? "").Trim())
                if (Array.IndexOf(System.IO.Path.GetInvalidFileNameChars(), c) < 0 && "\\/:*?\"<>|.".IndexOf(c) < 0 && !char.IsControl(c))
                    sb.Append(c);
            string s = sb.ToString().Trim();
            if (s.Length > 60)
                s = s.Substring(0, 60).Trim();
            return s.Length > 0 ? s : "Replay";
        }

        /// A path in the folder for this name that isn't taken (adds " (2)", " (3)" ...), unless `own` is that very file.
        public static string FreePath(string folder, string name, string own = null)
        {
            name = CleanName(name);
            string path = System.IO.Path.Combine(folder, name + Ext);
            for (int i = 2; File.Exists(path) && !SamePath(path, own); i++)
                path = System.IO.Path.Combine(folder, name + " (" + i + ")" + Ext);
            return path;
        }

        static bool SamePath(string a, string b) => b != null && string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

        /// Renames the file (and this object). Returns false if it can't.
        public bool Rename(string newName)
        {
            try
            {
                string to = FreePath(System.IO.Path.GetDirectoryName(Path), newName, Path);
                if (!SamePath(to, Path))
                    File.Move(Path, to);
                Path = to;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Delete()
        {
            try
            {
                File.Delete(Path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// Records the host's match as it is played: attached to a Game by HostStart (when a replay folder is set), fed every
    /// message the host broadcasts (Session.Broadcast) and every frame (FixedStep), and saved when the match stops.
    public class ReplayRecorder
    {
        public const long MaxBytes = 400L * 1024 * 1024;  // a very long run stops growing here (the rest of it isn't saved)

        /// Only these go in the file: the ones that change what a spectating client draws. Lobby chatter, Records (they'd
        /// credit a profile) and messages meant for one player's own game are left out.
        static readonly HashSet<Msg> Kept = new HashSet<Msg>
        {
            Msg.Start, Msg.Spawn, Msg.Remove, Msg.ClearEnemyBullets, Msg.Fx, Msg.Snapshot, Msg.Upgrades, Msg.BulletSync, Msg.RunStats,
            Msg.RunStatsLive, Msg.DevStats, Msg.Sound, Msg.PracticeWave, Msg.PathOffer, Msg.PathChosen, Msg.PathPatch, Msg.ExpandState, Msg.GateState, Msg.ShopGift,
            Msg.ReverseEnemyBullets, Msg.WaveReady, Msg.WaveSummary, Msg.Steer,
        };

        readonly Game game;
        readonly string folder;
        readonly ReplayFile file = new ReplayFile();
        int frame, lastWave;
        bool lastBoss;
        long bytes;
        bool full;
        readonly Dictionary<int, bool> wasDown = new Dictionary<int, bool>();

        public ReplayRecorder(Game game, Session session, string folder)
        {
            this.game = game;
            this.folder = folder;
            file.Created = DateTime.UtcNow;
            file.Practice = session.PracticeMode;
            file.Plan = session.PracticeMode ? session.Plan.ToText() : "";
            file.Hunt = session.HuntMode && !session.PracticeMode && session.Resume == null;
            file.HuntBoss = session.HuntBoss;
            file.Mods = session.Modifiers;
            file.Diff = session.Diff.Copy();
            file.Mode = session.PracticeMode ? "Practice" : file.Hunt ? "Boss Hunt" : "Standard";
        }

        public int Frame => frame;
        public float Seconds => file.TimeAt(frame);
        public bool Full => full;

        public void Capture(byte[] msg)
        {
            if (full || msg == null || msg.Length == 0 || !Kept.Contains((Msg)msg[0]))
                return;
            bytes += msg.Length + 8;
            if (bytes > MaxBytes)
            {
                full = true;
                return;
            }
            file.Msgs.Add(new ReplayMsg { Frame = frame, Data = msg });
        }

        /// Once per unpaused host frame.
        public void Tick(float dt)
        {
            if (full)
                return;
            frame++;
            file.Dts.Add(dt);
            if ((game.Wave != lastWave || game.BossNow != lastBoss) && game.Wave > 0)
            {
                lastWave = game.Wave;
                lastBoss = game.BossNow;
                file.Marks.Add(new ReplayMark { Frame = frame, Kind = game.BossNow ? ReplayMark.Boss : ReplayMark.Wave, Value = game.Wave });
                if (game.Wave > file.MaxWave)
                    file.MaxWave = game.Wave;
            }
            foreach (var p in game.Players.Values)
            {
                bool before;
                wasDown.TryGetValue(p.Id, out before);
                if (p.Downed && !before)
                    file.Marks.Add(new ReplayMark { Frame = frame, Kind = ReplayMark.Down, Value = p.Id });
                wasDown[p.Id] = p.Downed;
            }
        }

        /// Called right after the Start message is read: who is playing.
        public void SetParty()
        {
            var parts = new List<string>();
            foreach (var p in game.Players.Values)
                parts.Add(p.Name + " (" + (p.Kit != Kit.Soldier ? p.Kit.ToString() + " " : "") + p.Role + ")");
            file.Players = parts.Count;
            file.Party = string.Join(", ", parts.ToArray());
            file.Mode = game.Practice != null ? "Practice" : game.Hunt != null ? "Boss Hunt" : game.Quick ? "Quick Play" : "Standard";
        }

        /// Writes the file; returns its path, or null if there was nothing worth keeping (under a second) or it failed.
        public string Finish()
        {
            if (frame < 60)
                return null;
            file.Seconds = file.TimeAt(frame);
            if (game.Endless)
                file.Mode = "Endless";
            try
            {
                string name = "Run " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                string path = ReplayFile.FreePath(folder, name);
                file.Save(path);
                return path;
            }
            catch (Exception e)
            {
                game.Log?.Invoke("replay not saved: " + e.Message);
                return null;
            }
        }
    }

    /// Plays a ReplayFile: a private offline Session that reads the recorded messages the way a client reads the host's,
    /// stepping its Game frame by frame. Any frame can be reached (Seek) by starting over and fast-forwarding, so you can
    /// pause, step, run at any speed in either direction and drag to any moment.
    public class ReplayPlayer
    {
        /// Playback speeds, slowest to fastest; the sign is the direction.
        public static readonly float[] Speeds = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f, 10f };

        /// Seeking steps only this many of the last frames for real; before that, one-off effects and shots are skipped
        /// (they're over by then), so a jump to anywhere takes about the same short time.
        public const int Window = 360;

        public readonly Session Session = new Session();
        public Game Game => Session.Game;
        public ReplayFile File { get; private set; }
        public int Frame { get; private set; }
        public bool Paused;
        public float Rate = 1f;  // signed speed; negative runs backward

        int msgIdx;
        float acc;

        public int Frames => File.Frames;
        public float Time => File.TimeAt(Frame);
        public float Length => File.Seconds;

        public void Load(ReplayFile f)
        {
            File = f;
            Game.Replaying = true;
            Game.Unrecorded = true;
            Game.Log = null;
            Seek(0);
            Paused = false;
            Rate = 1f;
        }

        public void Close()
        {
            Session.Leave("");
            Game.Replaying = false;
            Game.Unrecorded = false;
        }

        void Reset()
        {
            var s = Session;
            s.Leave("");
            Game.Replaying = true;
            Game.Unrecorded = true;
            s.MyId = 0;  // nobody: every player is seen as the host would show them to someone else
            s.PracticeMode = File.Practice;
            if (File.Practice)
            {
                List<string> problems;
                s.Plan = PracticePlan.Parse(File.Plan, out problems);
            }
            s.HuntMode = File.Hunt;
            s.HuntBoss = File.HuntBoss;
            s.Modifiers = File.Mods;
            s.Diff = File.Diff.Copy();
            msgIdx = 0;
            Frame = 0;
            acc = 0f;
        }

        void Apply(ReplayMsg m)
        {
            Session.ReplayApply(m.Data);
            if (m.Type == Msg.Start)
            {
                int first = 0;
                foreach (var id in Game.Players.Keys.OrderBy(k => k))
                {
                    first = id;
                    break;
                }
                Game.ViewId = first;
                foreach (var p in Game.Players.Values)
                    Session.Lobby[p.Id] = new LobbyEntry { Id = p.Id, Name = p.Name, Role = p.Role, Kit = p.Kit };
            }
        }

        /// Transient messages older than the seek window are skipped; Snapshot-like ones only matter if they're the latest.
        static bool Skippable(Msg t) => t == Msg.Fx || t == Msg.Sound || t == Msg.Spawn || t == Msg.Remove || t == Msg.ClearEnemyBullets
            || t == Msg.ReverseEnemyBullets || t == Msg.Snapshot || t == Msg.RunStatsLive;

        /// Go to this frame (0 = the start, Frames = the end). Always starts over and replays, so going back is as good as going forward.
        public void Seek(int target)
        {
            target = Math.Max(0, Math.Min(Frames, target));
            Reset();
            int from = Math.Max(0, target - Window);
            // Everything up to `from`: apply what lasting state needs, and only the newest of the repeating full-state messages.
            int lastSnapIdx = -1, lastStatsIdx = -1, end = 0;
            for (int i = 0; i < File.Msgs.Count && File.Msgs[i].Frame <= from; i++)
            {
                end = i + 1;
                if (File.Msgs[i].Type == Msg.Snapshot) lastSnapIdx = i;
                else if (File.Msgs[i].Type == Msg.RunStatsLive) lastStatsIdx = i;
            }
            for (int i = 0; i < end; i++)
            {
                var m = File.Msgs[i];
                if (!Skippable(m.Type) || i == lastSnapIdx || i == lastStatsIdx)
                    Apply(m);
            }
            msgIdx = end;
            Frame = from;
            while (Frame < target)
                StepOne();
            Settle();
        }

        /// Run one frame forward: that frame's messages arrive, then the game steps.
        void StepOne()
        {
            Frame++;
            while (msgIdx < File.Msgs.Count && File.Msgs[msgIdx].Frame <= Frame)
                Apply(File.Msgs[msgIdx++]);
            Settle();
            Game.FixedStep(File.Dts[Frame - 1], new PlayerInput());
        }

        /// What the host's screen state would have been but a spectator mustn't act on.
        void Settle()
        {
            Game.Paused = false;  // the host pausing stopped the clock; the recording doesn't count those frames
            foreach (var p in Game.Players.Values)
                p.Offer = null;   // no upgrade cards over the replay (nobody here can pick)
        }

        /// Advance by `dt` real seconds at the current speed. Forward runs frames; backward jumps to the frame it lands on.
        public void Step(float dt)
        {
            if (File == null || Paused || Rate == 0f)
                return;
            acc += dt * Rate;
            if (acc > 0f)
            {
                int guard = 0;
                while (Frame < Frames && acc >= File.Dts[Frame] && guard++ < 1200)
                {
                    acc -= File.Dts[Frame];
                    StepOne();
                }
                if (Frame >= Frames)
                {
                    acc = 0f;
                    Paused = true;  // the end: wait there
                }
            }
            else if (acc < 0f)
            {
                int target = Frame;
                while (target > 0 && -acc >= File.Dts[target - 1])
                {
                    acc += File.Dts[target - 1];
                    target--;
                }
                if (target != Frame)
                    Seek(target);
                if (target == 0)
                {
                    acc = 0f;
                    Paused = true;
                }
            }
        }

        /// One frame at a time (pauses first).
        public void StepFrames(int n)
        {
            Paused = true;
            acc = 0f;
            if (n > 0 && Frame < Frames)
            {
                for (int i = 0; i < n && Frame < Frames; i++)
                    StepOne();
            }
            else if (n < 0)
                Seek(Frame + n);
        }

        public void SeekTime(float seconds) => Seek(File.FrameAt(Math.Max(0f, seconds)));

        public void Play(float rate)
        {
            Rate = rate;
            Paused = false;
            if (Frame >= Frames && rate > 0f)
                Seek(0);
        }

        public void TogglePause()
        {
            if (Paused && Frame >= Frames && Rate > 0f)
                Seek(0);
            Paused = !Paused;
        }

        /// The wave or boss mark at or before this frame, to jump back to / the next after it.
        public int PrevWaveFrame()
        {
            int best = 0;
            foreach (var m in File.Marks)
                if (m.Kind != ReplayMark.Down && m.Frame < Frame - 90)
                    best = m.Frame;
            return best;
        }

        public int NextWaveFrame()
        {
            foreach (var m in File.Marks)
                if (m.Kind != ReplayMark.Down && m.Frame > Frame)
                    return m.Frame;
            return Frames;
        }

        /// Follow the next player's screen.
        public void CycleView()
        {
            var ids = Game.Players.Keys.OrderBy(k => k).ToList();
            if (ids.Count == 0)
                return;
            int i = ids.IndexOf(Game.ViewId);
            Game.ViewId = ids[(i + 1) % ids.Count];
        }
    }
}
