using System;
using System.Collections.Generic;

namespace Arena
{
    /// The pedestal shop (owner, Nov 26): a shop room has no stall and no cover; its wares stand on pedestals along two facing walls
    /// (your shelf's cards on one, the services on the other) and you buy one by standing at it and pressing Interact (ShopPedestalLook.cs).
    /// A shop cell on the grid lattice has doors on the other two walls only: the pair facing the room it was built from (its Parent).
    /// Nothing joins it through a pedestal wall: no door there (Build), no door spot off it (GridSpots), no parent link through it (FromTiles,
    /// ConnectedAreas). Suite `shoppedestals`.
    public static class ShopPedestals
    {
        public const float Reach = 90f;      // how close to a pedestal's middle you stand to buy from it
        public const int PerWall = 4;        // the most pedestals on a wall
        public const float WallGap = 68f;    // a pedestal's middle from its wall
        public const float Pitch = 110f;     // the least room a pedestal takes along its wall

        /// A pedestal shop's spots: wall 0 (west, or north) holds the shelf's cards, wall 1 (east, or south) the services; empty if this is no
        /// pedestal shop. Spread evenly along the wall, a little lower than the middle (the item floats over its plinth).
        public static V2[] Spots(RoomArea a, int wall)
        {
            if (a.PedestalWalls == 0)
                return new V2[0];
            bool ew = a.PedestalWalls == 1;  // (on the west and east walls: spread down them)
            float lo = ew ? a.Min.Y : a.Min.X, hi = ew ? a.Max.Y : a.Max.X;
            float from = lo + (ew ? 70f : 60f), len = hi - (ew ? 40f : 60f) - from;
            int n = Math.Max(1, Math.Min(PerWall, (int)(len / Pitch)));
            var res = new V2[n];
            for (int i = 0; i < n; i++)
            {
                float along = from + len * (i + 0.5f) / n;
                float across = ew ? (wall == 0 ? a.Min.X + WallGap : a.Max.X - WallGap) : (wall == 0 ? a.Min.Y + WallGap + 12f : a.Max.Y - WallGap - 30f);
                res[i] = ew ? new V2(across, along) : new V2(along, across);
            }
            return res;
        }

        /// The pedestal (wall * PerWall + index) within Reach of this point in this room, the nearest; -1 if none.
        public static int Near(RoomArea a, V2 p)
        {
            int best = -1;
            float bd = Reach * Reach;
            for (int w = 0; w < 2; w++)
            {
                var s = Spots(a, w);
                for (int i = 0; i < s.Length; i++)
                {
                    float d = (s[i] - p).LengthSq;
                    if (d < bd) { bd = d; best = w * PerWall + i; }
                }
            }
            return best;
        }
    }

    public static partial class Expansion
    {
        /// A shop cell on the lattice: the pedestal shop whose side walls take no doors.
        public static bool PedestalShop(IList<ExpansionRoom> rooms, int a)
            => a > 0 && a <= rooms.Count && rooms[a - 1].Kind == RoomKind.Shop && !rooms[a - 1].Floating && CellLayouts.OnLattice(rooms[a - 1]);

        /// Which walls a shop room's pedestals line: 1 west and east, 2 north and south. A lattice shop faces the room it was built from;
        /// any other shop room lines its long walls.
        public static byte ShopPedestalWalls(IList<ExpansionRoom> rooms, int a)
        {
            BoxOf(rooms, a, out int x0, out int y0, out int x1, out int y1);
            if (PedestalShop(rooms, a))
            {
                int p = rooms[a - 1].Parent;
                if (p >= 0 && p <= rooms.Count && p != a)
                {
                    BoxOf(rooms, p, out int px0, out int py0, out int px1, out int py1);
                    if ((x0 == px1 || x1 == px0) && SharedRun(x0, y0, x1, y1, px0, py0, px1, py1) > 0)
                        return 2;  // (built off its west or east wall: its doors are there, its pedestals north and south)
                }
                return 1;
            }
            return (byte)(x1 - x0 > y1 - y0 ? 2 : 1);
        }

        /// Does the box (bx0, by0)-(bx1, by1) meet pedestal shop `a` on one of its pedestal walls? (No door or link there.)
        public static bool OnShopPedestalWall(IList<ExpansionRoom> rooms, int a, int bx0, int by0, int bx1, int by1)
        {
            if (!PedestalShop(rooms, a))
                return false;
            BoxOf(rooms, a, out int x0, out int y0, out int x1, out int y1);
            if (SharedRun(x0, y0, x1, y1, bx0, by0, bx1, by1) <= 0)
                return false;
            bool sideWall = x0 == bx1 || x1 == bx0;  // (they meet on a's west or east wall)
            return ShopPedestalWalls(rooms, a) == 1 ? sideWall : !sideWall;
        }

        /// No door between rooms a and b: they meet on a pedestal shop's pedestal wall.
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
