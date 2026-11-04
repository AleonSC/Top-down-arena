using System;
using System.Collections.Generic;

namespace Arena
{
    /// What a ping says. The wheel lists them clockwise from the top in this order (Game/PingLooks.cs draws them).
    public enum PingKind : byte { Here, Focus, Danger, Help, Loot, Go }

    /// Pings (Docs/Mockups/ping-system.html): tap the Ping key (middle mouse by default) to mark a spot for the party, hold it
    /// for a six-slice wheel. Matches only. Everyone sees it in the pinger's colour for a few seconds. A Focus ping snaps to the
    /// nearest enemy and follows it until it dies. The host keeps the pause and the cap for everyone.
    public partial class Session
    {
        public const float PingPause = 0.4f;   // between one player's pings
        public const int PingMax = 3;          // live at once per player; a fourth replaces the oldest
        public const float PingSnapReach = 70f; // how far from an enemy's edge a Focus ping still grabs it

        public struct PingShow { public int Owner; public PingKind Kind; public V2 Pos; public double At; public int Target; }

        /// Every ping still showing (oldest first). Use PingNow to read one: it says when a ping has lapsed.
        public readonly List<PingShow> Pings = new List<PingShow>();
        readonly Dictionary<int, double> pingLast = new Dictionary<int, double>();
        double pingReadyAt;

        public static float PingLife(PingKind k) => k == PingKind.Danger ? 4f : k == PingKind.Focus ? 6f : 5f;

        /// Seconds until you can ping again (0 = ready).
        public float PingWait => (float)Math.Max(0.0, pingReadyAt - Clock);

        /// Where ping `i` is now and how old it is; false once it has lapsed (or its Focus target died).
        public bool PingNow(int i, out V2 pos, out float age)
        {
            var p = Pings[i];
            pos = p.Pos;
            age = (float)(Clock - p.At);
            if (age < 0f || age >= PingLife(p.Kind))
                return false;
            if (p.Target >= 0)
            {
                Enemy e;
                if (!Game.Enemies.TryGetValue(p.Target, out e) || e.Hp <= 0f)
                    return false;
                pos = e.Disp;
            }
            return true;
        }

        /// Ping `kind` at an arena position as this player. False: not in a match, or still pausing.
        public bool SendPing(PingKind kind, V2 at)
        {
            if (Net == null || Screen != AppScreen.Game || !Game.Running || (int)kind > (int)PingKind.Go || PingWait > 0f || !Finite(at))
                return false;
            pingReadyAt = Clock + PingPause;
            if (IsHost)
                HostPing(MyId, kind, at);
            else
            {
                var w = Proto.Begin(Msg.PingMark);
                w.Write((byte)kind);
                w.Write(at.X);
                w.Write(at.Y);
                SendToHost(Proto.End(w));
            }
            return true;
        }

        static bool Finite(V2 v) => !float.IsNaN(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.X) && !float.IsInfinity(v.Y);

        void HostPing(int id, PingKind kind, V2 at)
        {
            if (Screen != AppScreen.Game || !Game.Running || !Game.Players.ContainsKey(id) || (int)kind > (int)PingKind.Go || !Finite(at))
                return;
            double last;
            if (pingLast.TryGetValue(id, out last) && Clock - last < PingPause * 0.8)  // (a little slack for the trip)
                return;
            pingLast[id] = Clock;
            at = new V2(Math.Max(0f, Math.Min(Game.Width, at.X)), Math.Max(0f, Math.Min(Game.Height, at.Y)));
            int target = -1;
            if (kind == PingKind.Focus)
            {
                float best = PingSnapReach;
                foreach (var e in Game.Enemies.Values)
                {
                    float d = (e.Pos - at).Length - e.Radius;
                    if (e.Hp > 0f && d < best)
                    {
                        best = d;
                        target = e.Id;
                        at = e.Pos;
                    }
                }
            }
            AddPing(id, kind, at, target);
            var w = Proto.Begin(Msg.PingMark);
            w.Write(id);
            w.Write((byte)kind);
            w.Write(at.X);
            w.Write(at.Y);
            w.Write(target);
            Broadcast(Proto.End(w));
        }

        void AddPing(int owner, PingKind kind, V2 at, int target)
        {
            for (int i = Pings.Count - 1; i >= 0; i--)  // drop what has lapsed
                if (Clock - Pings[i].At >= PingLife(Pings[i].Kind))
                    Pings.RemoveAt(i);
            int mine = 0;
            for (int i = 0; i < Pings.Count; i++)
                if (Pings[i].Owner == owner)
                    mine++;
            for (int i = 0; i < Pings.Count && mine >= PingMax; i++)  // the oldest of theirs goes
                if (Pings[i].Owner == owner)
                {
                    Pings.RemoveAt(i--);
                    mine--;
                }
            Pings.Add(new PingShow { Owner = owner, Kind = kind, Pos = at, At = Clock, Target = target });
        }
    }
}
