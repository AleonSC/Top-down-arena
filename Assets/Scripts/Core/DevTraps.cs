using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Dev Kit traps: spike plates and slow fields (and turrets) the host places by hand on the map (`RoomTrapList` adds them to the
    /// map's own). Every change goes to everyone as the whole list (Msg.DevTraps); they live until cleared or the game ends.
    public partial class Game
    {
        public const int DevTrapMax = 40;
        public static readonly float[] DevTrapSizes = { 25f, 50f, 75f };  // half a side, whole floor-grid squares: small, medium, big

        readonly List<RoomTrap> devTraps = new List<RoomTrap>();
        int devTrapVer;
        public int DevTrapCount => devTraps.Count;

        /// Host: put a trap at a point.
        public bool DevPlaceTrap(TrapKind kind, V2 at, float half)
        {
            if (!IsHost || devTraps.Count >= DevTrapMax)
                return false;
            if (kind != TrapKind.Turret)  // (a plate sits on the floor grid, RoomTraps.Tile)
                at = new V2(RoomTraps.SnapCentre(at.X, half), RoomTraps.SnapCentre(at.Y, half));
            devTraps.Add(new RoomTrap { Pos = at, Kind = kind, HalfX = kind == TrapKind.Turret ? 0f : half, HalfY = kind == TrapKind.Turret ? 0f : half });
            DevTrapsChanged();
            return true;
        }

        /// Host: take out the dev trap nearest a point (within its size), or all of them.
        public bool DevRemoveTrapAt(V2 at)
        {
            for (int i = devTraps.Count - 1; i >= 0; i--)
                if (RoomTraps.InRect(devTraps[i].Pos, at, Math.Max(24f, devTraps[i].HalfX), Math.Max(24f, devTraps[i].HalfY)))
                {
                    devTraps.RemoveAt(i);
                    DevTrapsChanged();
                    return true;
                }
            return false;
        }

        public void DevClearTraps()
        {
            if (devTraps.Count == 0)
                return;
            devTraps.Clear();
            DevTrapsChanged();
        }

        void DevTrapsChanged()
        {
            devTrapVer++;
            roomTrapKey = int.MinValue;
            if (!IsHost)
                return;
            var w = Proto.Begin(Msg.DevTraps);
            w.Write((byte)devTraps.Count);
            foreach (var t in devTraps)
            {
                w.Write((byte)t.Kind);
                w.Write(t.Pos.X); w.Write(t.Pos.Y);
                w.Write(t.HalfX); w.Write(t.HalfY);
            }
            session.Broadcast(Proto.End(w));
        }

        void ReadDevTraps(BinaryReader r)
        {
            int n = r.ReadByte();
            var list = new List<RoomTrap>();
            for (int i = 0; i < n; i++)
                list.Add(new RoomTrap { Kind = (TrapKind)r.ReadByte(), Pos = new V2(r.ReadSingle(), r.ReadSingle()), HalfX = r.ReadSingle(), HalfY = r.ReadSingle() });
            if (IsHost)
                return;
            devTraps.Clear();
            devTraps.AddRange(list);
            devTrapVer++;
            roomTrapKey = int.MinValue;
        }
    }
}
