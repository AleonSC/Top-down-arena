using System;
using System.Collections.Generic;

namespace Arena
{
    /// Pedestal rooms (owner, Nov 26): the vendor rooms (shop, Black Market, Arms Dealer, Medic) have no stall, no screen and no cover; their
    /// wares stand on pedestals along the walls that have no door, and you buy one by standing at it and pressing Interact (ShopPedestalLook.cs).
    /// A vendor cell on the grid lattice has doors on set walls only, counted from the wall facing the room it was built from (its Parent):
    /// the shop that wall and the one opposite, the Black Market and Arms Dealer that wall alone, the Medic every wall but one side.
    /// Nothing joins it through a pedestal wall: no door there (Build), no door spot off it (GridSpots), no parent link through it (FromTiles,
    /// ConnectedAreas). Suite `shoppedestals`.
    public static class ShopPedestals
    {
        public const float Reach = 90f;      // how close to a pedestal's middle you stand to buy from it
        public const int PerWall = 4;        // the most pedestals on a wall
        public const float WallGap = 68f;    // a pedestal's middle from its wall
        public const float Pitch = 110f;     // the least room a pedestal takes along its wall
        public const byte West = 1, East = 2, North = 4, South = 8;  // RoomArea.PedestalWalls bits

        /// The pedestal walls in the order their wares fill them (west, east, north, south).
        public static List<byte> Walls(RoomArea a)
        {
            var res = new List<byte>();
            foreach (byte w in new[] { West, East, North, South })
                if ((a.PedestalWalls & w) != 0)
                    res.Add(w);
            return res;
        }

        /// The pedestals on one wall (a West/East/North/South bit), spread evenly along it, clear of a pedestal wall round the corner;
        /// empty if that wall has none.
        public static V2[] Spots(RoomArea a, byte wall)
        {
            if ((a.PedestalWalls & wall) == 0)
                return new V2[0];
            bool ew = wall == West || wall == East;  // (down a west or east wall; along a north or south one)
            float lo = ew ? a.Min.Y : a.Min.X, hi = ew ? a.Max.Y : a.Max.X;
            bool loTaken = ew ? (a.PedestalWalls & North) != 0 : (a.PedestalWalls & West) != 0;
            bool hiTaken = ew ? (a.PedestalWalls & South) != 0 : (a.PedestalWalls & East) != 0;
            float from = lo + (loTaken ? (ew ? 170f : 140f) : ew ? 70f : 60f), len = hi - (hiTaken ? (ew ? 150f : 140f) : ew ? 40f : 60f) - from;
            int n = Math.Max(1, Math.Min(PerWall, (int)(len / Pitch)));
            var res = new V2[n];
            for (int i = 0; i < n; i++)
            {
                float along = from + len * (i + 0.5f) / n;
                float across = wall == West ? a.Min.X + WallGap : wall == East ? a.Max.X - WallGap : wall == North ? a.Min.Y + WallGap + 12f : a.Max.Y - WallGap - 30f;
                res[i] = ew ? new V2(across, along) : new V2(along, across);
            }
            return res;
        }

        /// Every pedestal in this room, wall by wall in Walls order.
        public static List<V2> AllSpots(RoomArea a)
        {
            var res = new List<V2>();
            foreach (var w in Walls(a))
                res.AddRange(Spots(a, w));
            return res;
        }

        /// The pedestal (an index into AllSpots) within Reach of this point, the nearest; -1 if none.
        public static int Near(RoomArea a, V2 p)
        {
            var s = AllSpots(a);
            int best = -1;
            float bd = Reach * Reach;
            for (int i = 0; i < s.Count; i++)
            {
                float d = (s[i] - p).LengthSq;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }

    public static partial class Expansion
    {
        static bool IsVendor(RoomKind k) => k == RoomKind.Shop || k == RoomKind.BlackMarket || k == RoomKind.Arms || k == RoomKind.Medic;

        /// A vendor cell on the lattice: the pedestal room whose doors go on set walls only.
        public static bool PedestalShop(IList<ExpansionRoom> rooms, int a)
            => a > 0 && a <= rooms.Count && IsVendor(rooms[a - 1].Kind) && !rooms[a - 1].Floating && CellLayouts.OnLattice(rooms[a - 1]);

        /// The wall of room a that the box meets (a ShopPedestals West/East/North/South bit), 0 if they don't share wall.
        static byte WallMet(IList<ExpansionRoom> rooms, int a, int bx0, int by0, int bx1, int by1)
        {
            BoxOf(rooms, a, out int x0, out int y0, out int x1, out int y1);
            if (SharedRun(x0, y0, x1, y1, bx0, by0, bx1, by1) <= 0)
                return 0;
            return bx1 == x0 ? ShopPedestals.West : bx0 == x1 ? ShopPedestals.East : by1 == y0 ? ShopPedestals.North : ShopPedestals.South;
        }

        static byte Opposite(byte w) => w == ShopPedestals.West ? ShopPedestals.East : w == ShopPedestals.East ? ShopPedestals.West : w == ShopPedestals.North ? ShopPedestals.South : ShopPedestals.North;

        /// The walls a vendor room's pedestals line (ShopPedestals bits); 0 for any other room. A lattice cell counts from the wall facing
        /// its Parent: the shop leaves that wall and the opposite one open, the Black Market and Arms Dealer that wall alone, the Medic every
        /// wall but one side (west, or north when its door faces west or east). Any other vendor room lines its two long walls.
        public static byte ShopPedestalWalls(IList<ExpansionRoom> rooms, int a)
        {
            if (a <= 0 || a > rooms.Count || !IsVendor(rooms[a - 1].Kind))
                return 0;
            BoxOf(rooms, a, out int x0, out int y0, out int x1, out int y1);
            if (!PedestalShop(rooms, a))
                return x1 - x0 > y1 - y0 ? (byte)(ShopPedestals.North | ShopPedestals.South) : (byte)(ShopPedestals.West | ShopPedestals.East);
            byte door = ShopPedestals.South;
            int p = rooms[a - 1].Parent;
            if (p >= 0 && p <= rooms.Count && p != a)
            {
                BoxOf(rooms, p, out int px0, out int py0, out int px1, out int py1);
                byte met = WallMet(rooms, a, px0, py0, px1, py1);
                if (met != 0)
                    door = met;
            }
            const byte all = 15;
            switch (rooms[a - 1].Kind)
            {
                case RoomKind.Shop: return (byte)(all & ~door & ~Opposite(door));
                case RoomKind.Medic: return door == ShopPedestals.West || door == ShopPedestals.East ? ShopPedestals.North : ShopPedestals.West;
                default: return (byte)(all & ~door);
            }
        }

        /// Does the box (bx0, by0)-(bx1, by1) meet pedestal room `a` on one of its pedestal walls? (No door or link there.)
        public static bool OnShopPedestalWall(IList<ExpansionRoom> rooms, int a, int bx0, int by0, int bx1, int by1)
        {
            if (!PedestalShop(rooms, a))
                return false;
            byte met = WallMet(rooms, a, bx0, by0, bx1, by1);
            return met != 0 && (ShopPedestalWalls(rooms, a) & met) != 0;
        }

        /// No door between rooms a and b: they meet on a pedestal room's pedestal wall.
        public static bool ShopWallBetween(IList<ExpansionRoom> rooms, int a, int b)
        {
            BoxOf(rooms, b, out int bx0, out int by0, out int bx1, out int by1);
            if (OnShopPedestalWall(rooms, a, bx0, by0, bx1, by1))
                return true;
            BoxOf(rooms, a, out int ax0, out int ay0, out int ax1, out int ay1);
            return OnShopPedestalWall(rooms, b, ax0, ay0, ax1, ay1);
        }
    }
}
