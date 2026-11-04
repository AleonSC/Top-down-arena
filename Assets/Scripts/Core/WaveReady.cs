using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// What a player earned on the wave just cleared (shown as tags in the between-waves box).
    [Flags]
    public enum WaveAward : byte { None = 0, Untouched = 1, MostDamage = 2, MostKills = 4, MostHealing = 8, Reviver = 16 }

    public class WaveLine
    {
        public int Id, Kills;
        public float Damage, Taken, Healing;
        public WaveAward Awards;
    }

    /// How the wave just cleared went: the host works it out when the last enemy dies and sends it to everyone.
    public class WaveSummary
    {
        public int Wave, ScoreGain;
        public float ClearTime;
        public readonly List<WaveLine> Lines = new List<WaveLine>();
        public WaveLine For(int id) => Lines.Find(l => l.Id == id);
    }

    /// The between-waves "ready" system. Each player presses Ready (or turns on Auto-ready, which is remembered between runs).
    /// The break's countdown only runs once everyone is ready: if everyone is on auto-ready it is the usual countdown
    /// (so a solo player with auto-ready keeps today's pace), otherwise it is a short one (ReadyCountdown) after the last
    /// Ready. While anyone isn't ready the wave waits. The host keeps the flags (Player.Ready, Player.AutoReady) and sends
    /// them with Msg.WaveReady; the wave's numbers and awards go out once, with Msg.WaveSummary.
    public partial class Game
    {
        public const float ReadyCountdown = 3f;  // seconds between the last Ready and the next wave

        public WaveSummary WaveReport;  // the wave just cleared (this one's numbers; null before the first clear)

        readonly Dictionary<int, RunStats> waveBase = new Dictionary<int, RunStats>();  // host: each player's tallies when the wave began
        float waveStartTime;
        int waveStartScore;

        /// Everyone's ready (pressed it, or on auto-ready).
        public bool AllReady()
        {
            foreach (var p in Players.Values)
                if (!p.Ready && !p.AutoReady)
                    return false;
            return true;
        }

        public bool AllAutoReady()
        {
            foreach (var p in Players.Values)
                if (!p.AutoReady)
                    return false;
            return true;
        }

        /// The break's countdown runs when nothing else holds it and everyone is ready.
        bool TickReadyTimer(ref float timer, float dt)
        {
            if (Practice != null || WaveReport == null || WaveReport.Wave != Wave)
            {
                timer -= dt;  // the opening countdown, the Practice Range and a dev jump: no ready-up
                return true;
            }
            if (AllAutoReady())
            {
                timer -= dt;
                return true;
            }
            if (AllReady())
            {
                timer = Math.Min(timer, ReadyCountdown) - dt;
                return true;
            }
            return false;
        }

        // --- The wave's numbers and awards (host) ----------------------------------

        /// Called as a wave begins: remembers where everyone's tallies stood.
        void MarkWaveStart()
        {
            waveBase.Clear();
            foreach (var p in Players.Values)
            {
                var b = new RunStats();
                b.DamageDealt = p.Run.DamageDealt; b.DamageTaken = p.Run.DamageTaken; b.HealingDone = p.Run.HealingDone;
                b.Kills = p.Run.Kills; b.Revives = p.Run.Revives;
                waveBase[p.Id] = b;
            }
            waveStartTime = RunTime;
            waveStartScore = Score;
        }

        /// Called when a wave is cleared: works out the summary and awards, and sends it.
        void SummariseWave()
        {
            var s = new WaveSummary { Wave = Wave, ClearTime = Math.Max(0f, RunTime - waveStartTime), ScoreGain = Math.Max(0, Score - waveStartScore) };
            foreach (var p in Players.Values)
            {
                RunStats b;
                if (!waveBase.TryGetValue(p.Id, out b))
                    b = new RunStats();
                s.Lines.Add(new WaveLine
                {
                    Id = p.Id,
                    Kills = Math.Max(0, p.Run.Kills - b.Kills),
                    Damage = Math.Max(0f, p.Run.DamageDealt - b.DamageDealt),
                    Taken = Math.Max(0f, p.Run.DamageTaken - b.DamageTaken),
                    Healing = Math.Max(0f, p.Run.HealingDone - b.HealingDone),
                    Awards = p.Run.Revives > b.Revives ? WaveAward.Reviver : WaveAward.None,
                });
            }
            GiveAwards(s);
            WaveReport = s;
            var w = Proto.Begin(Msg.WaveSummary);
            w.Write(s.Wave);
            w.Write(s.ClearTime);
            w.Write(s.ScoreGain);
            w.Write((byte)s.Lines.Count);
            foreach (var l in s.Lines)
            {
                w.Write(l.Id);
                w.Write(l.Kills);
                w.Write(l.Damage);
                w.Write(l.Taken);
                w.Write((byte)l.Awards);
            }
            session.Broadcast(Proto.End(w));
        }

        /// Untouched (took no damage) and Reviver go to anyone who earned them; the Most-awards only count in a party,
        /// and only for a single leader with something to show.
        static void GiveAwards(WaveSummary s)
        {
            foreach (var l in s.Lines)
                if (l.Taken <= 0.5f)
                    l.Awards |= WaveAward.Untouched;
            if (s.Lines.Count < 2)
                return;
            Lead(s, l => l.Damage, WaveAward.MostDamage);
            Lead(s, l => l.Kills, WaveAward.MostKills);
            Lead(s, l => l.Healing, WaveAward.MostHealing);
        }

        static void Lead(WaveSummary s, Func<WaveLine, float> value, WaveAward award)
        {
            WaveLine best = null;
            bool tie = false;
            foreach (var l in s.Lines)
            {
                if (best == null || value(l) > value(best)) { best = l; tie = false; }
                else if (Math.Abs(value(l) - value(best)) < 0.01f) tie = true;
            }
            if (best != null && !tie && value(best) > 0.5f)
                best.Awards |= award;
        }

        void ReadWaveSummary(BinaryReader r)
        {
            var s = new WaveSummary { Wave = r.ReadInt32(), ClearTime = r.ReadSingle(), ScoreGain = r.ReadInt32() };
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
                s.Lines.Add(new WaveLine { Id = r.ReadInt32(), Kills = r.ReadInt32(), Damage = r.ReadSingle(), Taken = r.ReadSingle(), Awards = (WaveAward)r.ReadByte() });
            WaveReport = s;
        }

        // --- Ready and Auto-ready ------------------------------------------------

        /// This player's own button (kind 0: Ready on or off; kind 1: Auto-ready on or off).
        public void SetMyReady(bool ready) => SendReady(0, ready);
        public void SetMyAutoReady(bool auto) => SendReady(1, auto);

        void SendReady(byte kind, bool value)
        {
            if (Me == null)
                return;
            if (IsHost)
            {
                HostReady(MyId, kind, value);
                return;
            }
            var w = Proto.Begin(Msg.WaveReady);
            w.Write(kind);
            w.Write(value);
            session.SendToHost(Proto.End(w));
        }

        public void OnWaveReady(int peer, BinaryReader r)
        {
            byte kind = r.ReadByte();
            HostReady(peer, kind, r.ReadBoolean());
        }

        void HostReady(int id, byte kind, bool value)
        {
            Player p;
            if (!Players.TryGetValue(id, out p))
                return;
            if (kind == 1)
            {
                if (p.AutoReady == value)
                    return;
                p.AutoReady = value;
                if (value)
                    p.Ready = false;  // (the switch covers it)
            }
            else
            {
                // Ready only means something during the break, and only once this player's own picks are done.
                if (State != MatchState.Intermission || p.AutoReady || p.PendingPicks + p.PendingLegendary + p.PendingMods + p.PendingMilestones + p.PrizePicks > 0)
                    return;
                if (p.Ready == value)
                    return;
                p.Ready = value;
            }
            BroadcastReady();
        }

        /// Everyone's flags, to everyone (when anything changes, and as each break begins).
        void BroadcastReady()
        {
            if (!IsHost)
                return;
            var w = Proto.Begin(Msg.WaveReady);
            w.Write((byte)Players.Count);
            foreach (var p in Players.Values)
            {
                w.Write(p.Id);
                w.Write(p.Ready);
                w.Write(p.AutoReady);
            }
            session.Broadcast(Proto.End(w));
        }

        void ReadWaveReady(BinaryReader r)
        {
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                bool ready = r.ReadBoolean(), auto = r.ReadBoolean();
                Player p;
                if (Players.TryGetValue(id, out p))
                {
                    p.Ready = ready;
                    p.AutoReady = auto;
                }
            }
        }

        /// A break begins: nobody is ready yet.
        void ClearReady()
        {
            foreach (var p in Players.Values)
                p.Ready = false;
            BroadcastReady();
        }
    }
}
