using System;
using System.IO;

namespace Arena
{
    /// The lag chart's side of the game (LagStats.cs holds the numbers, Game/LagChartUi.cs draws them): pings four times a
    /// second (the client's ping carries its last round trip so the host knows everyone's), Msg.NetPings once a second
    /// (host -> all: every player's round trip), the match events marked on the chart, and the bytes counted by NetPeer.
    public partial class Game
    {
        public const float PingEvery = 0.25f;  // seconds between pings (Game.FixedStep)
        const float NetPingsEvery = 1f;

        public readonly LagStats Lag = new LagStats();

        /// A folder for the lag CSV (Settings > Gameplay > "Log lag to a file"), or null for none. A new file each run.
        public string LagLogFolder;

        public double LagNow => clock.Elapsed.TotalSeconds;

        bool lagWasRunning, lagBoss, lagStorm;
        int lagWave = -1, lagCheck;
        float netPingsT;
        long lagIn, lagOut;

        /// Every fixed step, running or not (before FixedStep's early returns).
        void TickLag(float dt)
        {
            double now = LagNow;
            if (Running && !lagWasRunning)
            {
                Lag.LogPath = LagLogFolder != null && !Replaying
                    ? Path.Combine(LagLogFolder, "lag-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv") : null;
                Lag.Reset(now);
                lagWave = -1;
                lagBoss = lagStorm = false;
            }
            else if (!Running && lagWasRunning)
                Lag.Flush(now);
            lagWasRunning = Running;
            var net = session.Net;
            if (net != null)
            {
                Lag.AddBytes(now, net.BytesIn - lagIn, net.BytesOut - lagOut);
                lagIn = net.BytesIn;
                lagOut = net.BytesOut;
            }
            if (!Running)
                return;
            Lag.Advance(now);
            if (++lagCheck % 6 == 0)
                MarkLagEvents(now);
            if (IsHost && !Replaying && (netPingsT -= dt) <= 0f)
            {
                netPingsT = NetPingsEvery;
                SendNetPings();
            }
        }

        /// Wave starts, a boss arriving, and bullet storms (enemy bullets passing LagStats.StormBullets; it ends below two thirds of that).
        void MarkLagEvents(double now)
        {
            if (Wave != lagWave && State == MatchState.Fighting)
            {
                if (lagWave >= 0 || Wave > 0)
                    Lag.Mark(now, LagMarkKind.Wave, "W" + Wave);
                lagWave = Wave;
            }
            bool boss = false;
            foreach (var e in Enemies.Values)
                if (e.Def.IsBoss && e.Hp > 0f)
                {
                    boss = true;
                    break;
                }
            if (boss && !lagBoss)
                Lag.Mark(now, LagMarkKind.Boss, "Boss");
            lagBoss = boss;
            int shots = 0;
            foreach (var b in Bullets.Values)
                if (b.Owner == 0)
                    shots++;
            if (!lagStorm && shots >= LagStats.StormBullets)
            {
                lagStorm = true;
                Lag.Mark(now, LagMarkKind.Storm, shots + " shots");
            }
            else if (lagStorm && shots < LagStats.StormBullets * 2f / 3f)
                lagStorm = false;
        }

        /// The front end, once a rendered frame (for the frame-rate line).
        public void LagFrame(float dt)
        {
            if (Running)
                Lag.AddFrame(LagNow, dt);
        }

        /// Client: a Pong came back.
        void NotePong(float ms)
        {
            PingMs = ms;
            Lag.AddPing(LagNow, ms);
        }

        /// Client: a snapshot arrived (`bytes` long).
        void NoteSnapshotLag(int bytes) => Lag.NoteSnapshot(LagNow, bytes, Running && !Paused && State == MatchState.Fighting);

        /// Host: every player's round trip to everyone (Msg.NetPings: count, then (id, ms as a ushort) each).
        void SendNetPings()
        {
            Lag.PlayerPing.Clear();
            var w = Proto.Begin(Msg.NetPings);
            w.Write((byte)Players.Count);
            float worst = -1f;
            foreach (var p in Players.Values)
            {
                float ms = p.Id == MyId ? 0f : session.PeerPingMs(p.Id);
                Lag.PlayerPing[p.Id] = ms;
                if (p.Id != MyId)
                    worst = Math.Max(worst, ms);
                w.Write(p.Id);
                w.Write((ushort)Math.Min(65535, Math.Max(0, (int)Math.Round(ms))));
            }
            if (worst >= 0f)
                Lag.AddPing(LagNow, worst);  // the host's ping line is the worst guest's (it has no round trip of its own)
            session.Net?.Broadcast(Proto.End(w));  // (straight to the network: live numbers, not kept in a replay)
        }

        void ReadNetPings(BinaryReader r)
        {
            Lag.PlayerPing.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                Lag.PlayerPing[id] = r.ReadUInt16();
            }
        }
    }
}
