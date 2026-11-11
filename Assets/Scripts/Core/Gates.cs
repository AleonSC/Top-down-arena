using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A shutter gate fitted to a door of the Expanding arena (Docs/RoomBuilding2.md step 3): the door's middle and which way its wall runs.
    public struct GateFit : IEquatable<GateFit>
    {
        public short X, Y;
        public bool Vertical;  // the wall runs up and down
        public V2 Pos => new V2(X, Y);
        public bool Equals(GateFit o) => X == o.X && Y == o.Y && Vertical == o.Vertical;
    }

    /// The rules of shutter gates: where one can go, what it costs, how long it holds.
    public static class Gates
    {
        public const int Price = 8, Max = 6;
        public const float CloseTime = 8f, Cooldown = 30f;  // shut this long; usable again this long after it was shut
        public const float UseRange = 150f;                  // stand this close to the door to work it
        public const float Grow = 0.4f;                      // the beams take this long to fill the doorway

        /// The plain door (not an opened wall) at this spot, or -1.
        public static int DoorAt(World w, V2 at)
        {
            for (int i = 0; i < w.Doors.Length; i++)
                if (w.Doors[i].Width <= 0f && w.Doors[i].Pos.DistanceTo(at) < 2f)
                    return i;
            return -1;
        }

        /// The nearest plain door within `reach` of a point, or -1 (the draw screen's click).
        public static int NearestDoor(World w, V2 at, float reach)
        {
            int best = -1;
            float bestD = reach;
            for (int i = 0; i < w.Doors.Length; i++)
            {
                if (w.Doors[i].Width > 0f)
                    continue;
                float d = w.Doors[i].Pos.DistanceTo(at);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// Can a gate go on the door at `at`? (A plain door between two connected rooms, without a gate, and fewer than Max on the map.)
        public static bool CanFit(World w, IList<GateFit> gates, V2 at, out string why)
        {
            why = null;
            if (gates.Count >= Max) { why = "The map holds " + Max + " gates at most"; return false; }
            int d = DoorAt(w, at);
            if (d < 0) { why = "Pick a door (not an opened wall)"; return false; }
            var door = w.Doors[d];
            if (w.Areas[door.A].Sealed || w.Areas[door.B].Sealed) { why = "A sealed room: build to it first"; return false; }
            foreach (var g in gates)
                if (g.Pos.DistanceTo(door.Pos) < 2f) { why = "That door has a gate already"; return false; }
            return true;
        }

        public static GateFit Of(RoomDoor d) => new GateFit { X = (short)Math.Round(d.Pos.X), Y = (short)Math.Round(d.Pos.Y), Vertical = d.Vertical };

        /// A proposal's ExpansionRoom for a gate: the door's middle, W = 1 for a vertical wall.
        public static ExpansionRoom Proposal(GateFit g) => new ExpansionRoom { Kind = RoomKind.Gate, X = g.X, Y = g.Y, W = g.Vertical ? 1 : 0, Price = Price };
        public static GateFit FromProposal(ExpansionRoom r) => new GateFit { X = (short)r.X, Y = (short)r.Y, Vertical = r.W != 0 };
    }

    public partial class Game
    {
        /// The gates the team fitted (synced after the portal pairs in Msg.ExpandState), and each one's clocks (Msg.GateState).
        public readonly List<GateFit> GateFits = new List<GateFit>();
        readonly List<float> gateShut = new List<float>(), gateCool = new List<float>();

        void SyncGateClocks()
        {
            while (gateShut.Count < GateFits.Count) { gateShut.Add(0f); gateCool.Add(0f); }
            if (gateShut.Count > GateFits.Count) { gateShut.RemoveRange(GateFits.Count, gateShut.Count - GateFits.Count); gateCool.RemoveRange(GateFits.Count, gateCool.Count - GateFits.Count); }
        }

        /// Seconds left shut (0: open) and seconds until it can be shut again.
        public float GateShut(int i) { SyncGateClocks(); return gateShut[i]; }
        public float GateCool(int i) { SyncGateClocks(); return gateCool[i]; }

        /// Is the gate on a door that exists on the map in play? (An opened wall can swallow a door; the gate then does nothing.)
        public bool GateLive(int i) => ExpandingOn && RoomsOn && Gates.DoorAt(CurrentWorld, GateFits[i].Pos) >= 0;

        /// The gate a player at p can work (closest within UseRange), or -1.
        public int GateNear(V2 p)
        {
            int best = -1;
            float bestD = Gates.UseRange;
            for (int i = 0; i < GateFits.Count; i++)
            {
                if (!GateLive(i))
                    continue;
                float d = GateFits[i].Pos.DistanceTo(p);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// This game's player shuts the gate they stand by (G). The host decides.
        public void UseGate()
        {
            if (Me == null || Me.Downed)
                return;
            int i = GateNear(Me.Pos);
            if (i < 0)
                return;
            if (IsHost)
            {
                HostUseGate(MyId, i);
                return;
            }
            var w = Proto.Begin(Msg.GateUse);
            w.Write((byte)i);
            session.SendToHost(Proto.End(w));
        }

        public void OnGateUse(int peer, BinaryReader r) => HostUseGate(peer, r.ReadByte());

        void HostUseGate(int id, int i)
        {
            Player p;
            if (!IsHost || i < 0 || i >= GateFits.Count || !Players.TryGetValue(id, out p) || p.Downed || !GateLive(i) || p.Pos.DistanceTo(GateFits[i].Pos) > Gates.UseRange + 40f)
                return;
            SyncGateClocks();
            if (gateCool[i] > 0f)
                return;
            gateShut[i] = Gates.CloseTime;
            gateCool[i] = Gates.Cooldown;
            Sounds.Add(Sfx.Pick);
            DoLog(p.Name + " shuts a gate");
            BroadcastGate(i);
        }

        void BroadcastGate(int i)
        {
            var w = Proto.Begin(Msg.GateState);
            w.Write((byte)i);
            w.Write(gateShut[i]);
            w.Write(gateCool[i]);
            session.Broadcast(Proto.End(w));
        }

        void ReadGateState(BinaryReader r)
        {
            int i = r.ReadByte();
            float shut = r.ReadSingle(), cool = r.ReadSingle();
            SyncGateClocks();
            if (i >= gateShut.Count)
                return;
            if (gateShut[i] <= 0f && shut > 0f)
                Sounds.Add(Sfx.Pick);
            gateShut[i] = shut;
            gateCool[i] = cool;
        }

        /// Every game, every frame: the gates' clocks run down.
        void TickGates(float dt)
        {
            SyncGateClocks();
            for (int i = 0; i < gateShut.Count; i++)
            {
                gateShut[i] = Math.Max(0f, gateShut[i] - dt);
                gateCool[i] = Math.Max(0f, gateCool[i] - dt);
            }
        }

        void ResetGates()
        {
            GateFits.Clear();
            gateShut.Clear();
            gateCool.Clear();
        }

        readonly Dictionary<long, LayoutDef> gatePlans = new Dictionary<long, LayoutDef>();
        LayoutDef gatePlanBase;

        /// The floor plan steering uses: the active one, plus a wall across every shut gate, so enemies (and bots) route round it
        /// through the other doors instead of pressing against it. One copy per set of shut gates, kept (its corner graph is built once).
        LayoutDef SteerLayout
        {
            get
            {
                var d = ActiveLayout;
                if (d == null || GateFits.Count == 0)
                    return d;
                long mask = 0;
                for (int i = 0; i < GateFits.Count && i < gateShut.Count; i++)
                    if (gateShut[i] > 0f && GateLive(i))
                        mask |= 1L << i;
                if (mask == 0)
                    return d;
                if (gatePlanBase != d)
                {
                    foreach (var old in gatePlans.Values)
                        wayGraphs.Remove(old);  // (their corner graphs go with them)
                    gatePlans.Clear();
                    gatePlanBase = d;
                }
                LayoutDef plan;
                if (gatePlans.TryGetValue(mask, out plan))
                    return plan;
                var extra = new List<Solid>();
                for (int i = 0; i < GateFits.Count; i++)
                    if ((mask & (1L << i)) != 0)
                    {
                        var g = GateFits[i];
                        float hw = g.Vertical ? Rooms.WallT / 2f : Rooms.DoorW / 2f, hh = g.Vertical ? Rooms.DoorW / 2f : Rooms.WallT / 2f;
                        extra.Add(new Solid { Pos = g.Pos, HW = hw, HH = hh });
                    }
                gatePlans[mask] = plan = d.WithSolids(extra.ToArray());
                return plan;
            }
        }

        /// A shut gate is a wall across its doorway for everyone (from PushOutOfSolids). Shots pass through.
        V2 PushOutOfGates(V2 pos, float r)
        {
            for (int i = 0; i < GateFits.Count; i++)
            {
                if (i >= gateShut.Count || gateShut[i] <= 0f || !GateLive(i))
                    continue;
                var g = GateFits[i];
                float hw = (g.Vertical ? Rooms.WallT / 2f : Rooms.DoorW / 2f) + r, hh = (g.Vertical ? Rooms.DoorW / 2f : Rooms.WallT / 2f) + r;
                V2 c = g.Pos;
                if (Math.Abs(pos.X - c.X) >= hw || Math.Abs(pos.Y - c.Y) >= hh)
                    continue;
                if (g.Vertical)
                    pos = new V2(pos.X < c.X ? c.X - hw : c.X + hw, pos.Y);  // out the side it was on
                else
                    pos = new V2(pos.X, pos.Y < c.Y ? c.Y - hh : c.Y + hh);
            }
            return pos;
        }
    }
}
