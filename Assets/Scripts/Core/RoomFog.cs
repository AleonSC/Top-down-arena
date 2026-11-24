using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Room fog (Docs/FogOfWar.md): a room the map gets after the core starts unseen (`ExpansionRoom.Unseen`): dark on the
    /// maps and dim in the world. Players see through a line-of-sight cone (the way they aim, stopped by walls) plus a small
    /// circle round them. Everything the cone sweeps over in an unseen room is explored for good (a grid of cells, shared by the
    /// team); an enemy, coin or pickup in an unseen room shows only while somebody is looking at it. Once most of a room (every
    /// box of its piece) is explored, the whole room is seen. Enemies still spawn in unseen rooms; bots scout them first.
    public static class RoomFog
    {
        public const float ConeReach = 900f;    // how far the cone sees
        public const float ConeHalf = 0.7854f;  // half its angle: 45 degrees, a 90 degree cone
        public const float CloseReach = 170f;   // the circle round a player: seen in every direction, walls still block it
        public const float Cell = 25f;          // an unseen room's explored grid: a quarter of a tile a cell
        public const float DoneShare = 0.9f;    // the share of a room's cells explored that counts as seen (a pillar's shadow isn't a hunt)
        public const float WakeReach = 600f;    // a sleeping group wakes when somebody sees it from this close
        public const float WallInset = 16f;     // a cell or foe inside a wall's thickness is looked at up to its near face
        public const float RevealTime = 0.6f;   // the gold sweep across a room as it's seen (the look)

        /// Whether a room the map gets starts unseen: everything but the core and the vendors' rooms (Shop, Black Market, Arms
        /// Dealer, Medic, Builder Shop), which are never fogged (a kept room is settled by the map reset).
        public static bool StartsUnseen(ExpansionRoom r) => !Expansion.IsGridCore(r) && !IsVendorRoom(r.Kind) && r.Kind != RoomKind.Hospital;

        /// A vendor's room: never under fog.
        public static bool IsVendorRoom(RoomKind k) =>
            k == RoomKind.Shop || k == RoomKind.BlackMarket || k == RoomKind.Arms || k == RoomKind.Medic || k == RoomKind.Builder;

        /// How many rooms (pieces, not boxes) are still unseen.
        public static int UnseenCount(IList<ExpansionRoom> rooms)
        {
            int n = 0;
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Unseen && Expansion.HeadOf(rooms, i) == i)
                    n++;
            return n;
        }

        /// Whether `p` is lit for someone standing at `from` aiming along `aim`: within the close circle or the cone, with no wall
        /// between them.
        public static bool Sees(World wd, V2 from, float aim, V2 p, float reach = ConeReach)
        {
            float dx = p.X - from.X, dy = p.Y - from.Y;
            float d2 = dx * dx + dy * dy;
            if (d2 > reach * reach)
                return false;
            if (d2 > CloseReach * CloseReach && Math.Abs(MathX.WrapPi((float)Math.Atan2(dy, dx) - aim)) > ConeHalf)
                return false;
            if (d2 < 30f * 30f)
                return true;
            float d = (float)Math.Sqrt(d2), k = (d - WallInset) / d;
            return !Blocked(wd, from, new V2(from.X + dx * k, from.Y + dy * k));
        }

        /// Whether a wall stands between two points.
        public static bool Blocked(World wd, V2 a, V2 b)
        {
            var walls = wd.Walls();
            float dx = b.X - a.X, dy = b.Y - a.Y;
            for (int i = 0; i < walls.Length; i++)
            {
                var s = walls[i];
                if (s.Round)
                    continue;
                float t0 = 0f, t1 = 1f;
                if (Slab(a.X, dx, s.Pos.X - s.HW, s.Pos.X + s.HW, ref t0, ref t1) && Slab(a.Y, dy, s.Pos.Y - s.HH, s.Pos.Y + s.HH, ref t0, ref t1))
                    return true;
            }
            return false;
        }

        static bool Slab(float o, float d, float lo, float hi, ref float t0, ref float t1)
        {
            if (Math.Abs(d) < 1e-6f)
                return o >= lo && o <= hi;
            float inv = 1f / d, ta = (lo - o) * inv, tb = (hi - o) * inv;
            if (ta > tb) { float t = ta; ta = tb; tb = t; }
            if (ta > t0) t0 = ta;
            if (tb < t1) t1 = tb;
            return t0 <= t1;
        }

        /// The explored grid of a box: its cells across and down.
        public static void Grid(RoomArea a, out int cols, out int rows)
        {
            cols = Math.Max(1, (int)Math.Ceiling(a.Size.X / Cell));
            rows = Math.Max(1, (int)Math.Ceiling(a.Size.Y / Cell));
        }

        /// The middle of a cell.
        public static V2 CellMid(RoomArea a, int c, int r) =>
            new V2(Math.Min(a.Max.X, a.Min.X + (c + 0.5f) * Cell), Math.Min(a.Max.Y, a.Min.Y + (r + 0.5f) * Cell));
    }

    public partial class Game
    {
        /// Host: rooms from this index on are new since the last look (-1: settle quietly, the core or a loaded map).
        int roomFogMark = -1;

        /// Every game: each box's explored cells, by its index in Expanded (one bit a cell, row by row; null: none yet).
        byte[][] fogCells = new byte[0][];
        bool fogDirty;       // host: cells changed since the last send
        float fogSendT, fogScanT;

        /// Whether world area `area` (0 is the hub, area i is Expanded[i - 1]) is an unseen room.
        public bool AreaUnseen(int area) => ExpandingOn && area >= 1 && area <= Expanded.Count && Expanded[area - 1].Unseen;

        /// Whether the point is inside an unseen room.
        public bool UnseenAt(V2 p) => ExpandingOn && RoomsOn && AreaUnseen(CurrentWorld.AreaAt(p));

        /// Whether a standing player is looking at `p` (their cone or close circle, no wall between).
        public bool FogSees(V2 p, float reach = RoomFog.ConeReach)
        {
            var wd = CurrentWorld;
            foreach (var q in Players.Values)
                if (!q.Downed && RoomFog.Sees(wd, q.Pos, q.Aim, p, reach))
                    return true;
            return false;
        }

        /// Whether something at `p` is hidden by room fog: inside an unseen room and nobody is looking at it.
        public bool FogHides(V2 p) => UnseenAt(p) && !FogSees(p);

        /// Whether something that never moves (a trap, a coin, a sleeper) is hidden by room fog: in an unseen room, on ground nobody has
        /// explored yet, and nobody looking at it now. Once its ground is explored it stays shown, even if the room isn't fully clear (owner Nov 20).
        public bool FogHidesStill(V2 p) => UnseenAt(p) && !FogRevealedAt(p) && !FogSees(p);

        /// Whether the ground at `p` has been explored (a room not unseen counts).
        public bool FogRevealedAt(V2 p)
        {
            if (!UnseenAt(p))
                return true;
            var wd = CurrentWorld;
            int ai = wd.AreaAt(p);
            if (ai < 1)
                return true;
            var a = wd.Areas[ai];
            RoomFog.Grid(a, out int cols, out int rows);
            int c = Math.Max(0, Math.Min(cols - 1, (int)Math.Floor((p.X - a.Min.X) / RoomFog.Cell)));
            int r = Math.Max(0, Math.Min(rows - 1, (int)Math.Floor((p.Y - a.Min.Y) / RoomFog.Cell)));
            return FogExplored(ai - 1, c, r);
        }

        /// Whether the cell (c, r) of Expanded[room] is explored.
        public bool FogExplored(int room, int c, int r)
        {
            if (room < 0 || room >= fogCells.Length || fogCells[room] == null)
                return false;
            int cols, rows;
            var wd = CurrentWorld;
            if (room + 1 >= wd.Areas.Length)
                return false;
            RoomFog.Grid(wd.Areas[room + 1], out cols, out rows);
            int bit = r * cols + c;
            var cells = fogCells[room];
            return (bit >> 3) < cells.Length && (cells[bit >> 3] & (1 << (bit & 7))) != 0;
        }

        /// The share (0..1) of a box's cells explored.
        public float FogShare(int room)
        {
            if (room < 0 || room >= Expanded.Count || !Expanded[room].Unseen)
                return room >= 0 && room < Expanded.Count ? 1f : 0f;
            if (room >= fogCells.Length || fogCells[room] == null)
                return 0f;
            int cols, rows;
            var wd = CurrentWorld;
            if (room + 1 >= wd.Areas.Length)
                return 0f;
            RoomFog.Grid(wd.Areas[room + 1], out cols, out rows);
            int n = 0, total = cols * rows;
            for (int b = 0; b < total; b++)
                if ((b >> 3) < fogCells[room].Length && (fogCells[room][b >> 3] & (1 << (b & 7))) != 0)
                    n++;
            return n / (float)total;
        }

        /// The share of the whole room (every box of its piece) explored.
        public float FogRoomShare(int room)
        {
            if (room < 0 || room >= Expanded.Count)
                return 0f;
            float have = 0f, all = 0f;
            foreach (int i in Expansion.GroupOf(Expanded, room))
            {
                int cols, rows;
                var wd = CurrentWorld;
                if (i + 1 >= wd.Areas.Length)
                    continue;
                RoomFog.Grid(wd.Areas[i + 1], out cols, out rows);
                all += cols * rows;
                have += FogShare(i) * cols * rows;
            }
            return all > 0f ? have / all : 0f;
        }

        void FogSetCell(int room, int bit, int bytes)
        {
            if (fogCells.Length < Expanded.Count)
                Array.Resize(ref fogCells, Expanded.Count);
            if (fogCells[room] == null)
                fogCells[room] = new byte[bytes];
            fogCells[room][bit >> 3] |= (byte)(1 << (bit & 7));
        }

        /// Host: the map was replaced wholesale (a new core, a map reset, a loaded save): what's there now isn't new.
        void SettleRoomFog()
        {
            roomFogMark = -1;
            fogCells = new byte[0][];
            fogDirty = false;
        }

        /// Host: rooms added since the last look start unseen. Run before every room list goes out, so nobody sees a new room lit.
        bool FogNewRooms()
        {
            if (!IsHost || !ExpandingOn)
                return false;
            if (roomFogMark < 0 || roomFogMark > Expanded.Count)
            {
                roomFogMark = Expanded.Count;
                return false;
            }
            bool changed = false;
            for (int i = roomFogMark; i < Expanded.Count; i++)
            {
                var r = Expanded[i];
                if (r.Unseen || !RoomFog.StartsUnseen(r))
                    continue;
                r.Unseen = true;
                Expanded[i] = r;
                changed = true;
            }
            roomFogMark = Expanded.Count;
            return changed;
        }

        /// Host, every frame: new rooms go dark; the players' cones explore the unseen rooms they sweep over; a room mostly explored
        /// is seen by everyone.
        void TickRoomFog(float dt)
        {
            if (!IsHost || !ExpandingOn || Expanded.Count == 0)
                return;
            bool changed = FogNewRooms();
            if (RoomsOn)
            {
                TickDormant();  // a dormant group somebody sees wakes (DormantGroups.cs)
                fogScanT -= dt;
                if (fogScanT <= 0f)
                {
                    fogScanT = 0.1f;
                    changed |= ScanFog();
                }
                fogSendT -= dt;
                if (fogDirty && fogSendT <= 0f)
                {
                    fogSendT = 0.25f;
                    fogDirty = false;
                    BroadcastFog();
                }
            }
            if (changed)
                BroadcastExpansion();
        }

        /// Host: every standing player marks the cells of unseen rooms in their cone or circle explored. True when a room became seen.
        bool ScanFog()
        {
            var wd = CurrentWorld;
            HashSet<int> touched = null;
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    continue;
                for (int ai = 1; ai < wd.Areas.Length && ai <= Expanded.Count; ai++)
                {
                    int room = ai - 1;
                    if (!Expanded[room].Unseen)
                        continue;
                    var a = wd.Areas[ai];
                    if (a.Sealed)
                        continue;
                    float cx = Math.Max(a.Min.X, Math.Min(p.Pos.X, a.Max.X)), cy = Math.Max(a.Min.Y, Math.Min(p.Pos.Y, a.Max.Y));
                    if (!p.Pos.CloserThan(new V2(cx, cy), RoomFog.ConeReach))
                        continue;
                    int cols, rows;
                    RoomFog.Grid(a, out cols, out rows);
                    int bytes = (cols * rows + 7) / 8;
                    int c0 = Math.Max(0, (int)((p.Pos.X - RoomFog.ConeReach - a.Min.X) / RoomFog.Cell)), c1 = Math.Min(cols - 1, (int)((p.Pos.X + RoomFog.ConeReach - a.Min.X) / RoomFog.Cell));
                    int r0 = Math.Max(0, (int)((p.Pos.Y - RoomFog.ConeReach - a.Min.Y) / RoomFog.Cell)), r1 = Math.Min(rows - 1, (int)((p.Pos.Y + RoomFog.ConeReach - a.Min.Y) / RoomFog.Cell));
                    for (int r = r0; r <= r1; r++)
                        for (int c = c0; c <= c1; c++)
                        {
                            if (FogExplored(room, c, r) || !RoomFog.Sees(wd, p.Pos, p.Aim, RoomFog.CellMid(a, c, r)))
                                continue;
                            FogSetCell(room, r * cols + c, bytes);
                            fogDirty = true;
                            (touched ?? (touched = new HashSet<int>())).Add(room);
                        }
                }
            }
            if (touched == null)
                return false;
            bool seen = false;
            foreach (int room in touched)
            {
                if (!Expanded[room].Unseen || FogRoomShare(room) < RoomFog.DoneShare)
                    continue;
                foreach (int i in Expansion.GroupOf(Expanded, room))
                {
                    var r = Expanded[i];
                    r.Unseen = false;
                    Expanded[i] = r;
                    if (i < fogCells.Length)
                        fogCells[i] = null;
                }
                DoLog("room seen at " + Expanded[room].X + "," + Expanded[room].Y);
                seen = true;
            }
            return seen;
        }

        /// Bots: the middle of the nearest unexplored cell of an unseen room (null: none left), to scout it.
        public V2? NearestUnseen(V2 from)
        {
            if (!ExpandingOn || !RoomsOn)
                return null;
            var wd = CurrentWorld;
            V2? best = null;
            float bd = float.MaxValue;
            for (int ai = 1; ai < wd.Areas.Length; ai++)
            {
                if (!AreaUnseen(ai) || wd.Areas[ai].Sealed)
                    continue;  // (a sealed room can't be walked into)
                var a = wd.Areas[ai];
                var ways = new List<V2>();  // its doors: a cell is as far as the walk through the nearest one (a cell just behind a wall is not near)
                if (wd.AreaAt(from) != ai)
                    foreach (var dr in wd.Doors)
                        if (dr.Joins(ai))
                            ways.Add(dr.Pos);
                int cols, rows;
                RoomFog.Grid(a, out cols, out rows);
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        if (FogExplored(ai - 1, c, r))
                            continue;
                        V2 m = RoomFog.CellMid(a, c, r);
                        float d = float.MaxValue;
                        foreach (var w in ways)
                            d = Math.Min(d, from.DistanceTo(w) + w.DistanceTo(m));
                        d = ways.Count == 0 ? m.DistanceTo(from) : d;
                        if (d < bd)
                        {
                            bd = d;
                            best = m;
                        }
                    }
            }
            return best;
        }

        // --- Net ---------------------------------------------------------------------------------------------------

        /// The explored cells of every unseen box (index, bytes), after Msg.ExpandState's rooms and in Msg.FogCells.
        void WriteFogCells(BinaryWriter w)
        {
            var list = new List<int>();
            for (int i = 0; i < Expanded.Count && i < fogCells.Length; i++)
                if (Expanded[i].Unseen && fogCells[i] != null)
                    list.Add(i);
            w.Write((ushort)list.Count);
            foreach (int i in list)
            {
                w.Write((ushort)i);
                w.Write((ushort)fogCells[i].Length);
                w.Write(fogCells[i]);
            }
        }

        void ReadFogCells(BinaryReader r)
        {
            int n = r.ReadUInt16();
            var cells = new byte[Expanded.Count][];
            for (int k = 0; k < n; k++)
            {
                int i = r.ReadUInt16(), len = r.ReadUInt16();
                var bytes = r.ReadBytes(len);
                if (i < cells.Length)
                    cells[i] = bytes;
            }
            fogCells = cells;
        }

        /// Host -> all: the explored cells so far (a few times a second while someone is exploring).
        void BroadcastFog()
        {
            var w = Proto.Begin(Msg.FogCells);
            WriteFogCells(w);
            session.Broadcast(Proto.End(w));
        }

        /// A saved map's explored cells (SaveData `fog` lines: box index, hex bytes), after its rooms are loaded.
        public static string FogHex(byte[] cells) => BitConverter.ToString(cells).Replace("-", "");

        static byte[] FogUnhex(string s)
        {
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }

        void LoadFogCells(IDictionary<int, string> saved)
        {
            fogCells = new byte[Expanded.Count][];
            foreach (var kv in saved)
                if (kv.Key >= 0 && kv.Key < Expanded.Count && Expanded[kv.Key].Unseen && kv.Value.Length % 2 == 0)
                    fogCells[kv.Key] = FogUnhex(kv.Value);
        }
    }
}
