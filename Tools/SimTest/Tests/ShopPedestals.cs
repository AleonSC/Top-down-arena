using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The pedestal shop (ShopPedestals.cs): a shop cell on the lattice has doors on two facing walls only, its pedestals line the other two,
// nothing is built or linked through those, and the room has no cover. Suite `shoppedestals`.
static partial class Program
{
    static void ShopPedestalsTest()
    {
        var core = Expansion.GridCore();
        var spots = Expansion.PlacementsFor(core, PieceShape.Mono);
        Func<Placement, List<ExpansionRoom>> withShop = pl =>
        {
            var all = new List<ExpansionRoom>(core);
            var r = pl.Rooms[0];
            r.Kind = RoomKind.Shop;
            all.Add(r);
            return all;
        };

        // Off the core's bottom wall: doors north and south, pedestals west and east.
        var below = spots.First(o => o.Side == 1);
        var map = withShop(below);
        int shop = map.Count;  // (its area index: the hub is 0)
        var w = Expansion.Build(map, Expansion.GridCoreCuts());
        var area = w.Areas[shop];
        Check(Expansion.PedestalShop(map, shop) && area.PedestalWalls == (ShopPedestals.West | ShopPedestals.East), "a shop cell built off the core's bottom wall lines its west and east walls (" + area.PedestalWalls + ")");
        Check(w.Doors.Count(d => d.A == shop || d.B == shop) == 1 && w.Doors.Where(d => d.A == shop || d.B == shop).All(d => !d.Vertical), "its one door is on its north wall, to the core");
        var shopBox = map[shop - 1];
        Check(!w.Cover.Any(c => c.Pos.X > shopBox.X && c.Pos.X < shopBox.X1 && c.Pos.Y > shopBox.Y && c.Pos.Y < shopBox.Y1), "no cover stands in the shop (its wares line the walls)");

        // Off the core's right wall: doors west and east, pedestals north and south.
        var right = spots.First(o => o.Side == 3);
        var rmap = withShop(right);
        var rw = Expansion.Build(rmap, Expansion.GridCoreCuts());
        Check(rw.Areas[rmap.Count].PedestalWalls == (ShopPedestals.North | ShopPedestals.South), "a shop cell built off the core's right wall lines its north and south walls");

        // Nothing is built off a pedestal wall: no door spot on the shop's west or east side, and every spot left opens with a door.
        var next = Expansion.PlacementsFor(map, PieceShape.Mono);
        bool offSide = next.Any(o => o.Rooms[0].Y == shopBox.Y && (o.Rooms[0].X1 == shopBox.X || o.Rooms[0].X == shopBox.X1) && !SharesWallWithOther(map, shop, o.Rooms[0]));
        Check(!offSide, "no room can be built that meets only the shop's pedestal wall");
        var south = next.FirstOrDefault(o => o.Rooms[0].X == shopBox.X && o.Rooms[0].Y == shopBox.Y1);
        Check(south.Rooms != null, "a room can be built off the shop's south wall (its other door)");
        if (south.Rooms != null)
        {
            var smap = new List<ExpansionRoom>(map);
            smap.AddRange(south.Rooms);
            var sw = Expansion.Build(smap, Expansion.GridCoreCuts());
            Check(sw.Doors.Count(d => d.A == shop || d.B == shop) == 2, "built off its south wall, the shop has its two doors");
        }

        // A room already beside the shop (built before it): no door through the pedestal wall, and everything stays connected.
        var beside = spots.FirstOrDefault(o => o.Side == 1 && o.Rooms[0].Y == below.Rooms[0].Y && o.Rooms[0].X == below.Rooms[0].X1);
        if (beside.Rooms != null)
        {
            var bmap = new List<ExpansionRoom>(core);
            bmap.AddRange(beside.Rooms);
            var r = below.Rooms[0];
            r.Kind = RoomKind.Shop;
            bmap.Add(r);
            int s2 = bmap.Count;
            var bw = Expansion.Build(bmap, Expansion.GridCoreCuts());
            Check(!bw.Doors.Any(d => (d.A == s2 || d.B == s2) && d.Vertical), "a room already beside the shop gets no door through its pedestal wall");
            Check(Expansion.ConnectedAreas(bmap).All(x => x), "and the map stays connected round it");
        }

        // The spots: four a wall in a cell, inside the room, a pedestal's room apart, and Near finds the one you stand at.
        var west = ShopPedestals.Spots(area, ShopPedestals.West);
        var east = ShopPedestals.Spots(area, ShopPedestals.East);
        Check(west.Length == ShopPedestals.PerWall && east.Length == ShopPedestals.PerWall, "four pedestals on each wall of a cell (" + west.Length + ", " + east.Length + ")");
        Check(west.Concat(east).All(p => area.Contains(p, -40f)), "every pedestal stands well inside the room");
        Check(west.All(p => Math.Abs(p.X - (area.Min.X + ShopPedestals.WallGap)) < 1f) && east.All(p => Math.Abs(p.X - (area.Max.X - ShopPedestals.WallGap)) < 1f), "the cards line the west wall, the services the east wall");
        float gap = float.MaxValue;
        for (int i = 1; i < west.Length; i++)
            gap = Math.Min(gap, west[i].Y - west[i - 1].Y);
        Check(gap >= ShopPedestals.Pitch, "pedestals at least a pitch apart (" + gap + ")");
        Check(ShopPedestals.Near(area, west[2] + new V2(30f, 20f)) == 2 && ShopPedestals.Near(area, east[0]) == ShopPedestals.PerWall && ShopPedestals.Near(area, area.Mid) < 0, "Near: the pedestal you stand at, none from the runner");
        Check(ShopPedestals.AllSpots(w.Areas[1]).Count == 0, "a room that isn't a vendor has no pedestals");

        // The other vendors, each built off the core's bottom wall (its door on its north wall).
        Func<RoomKind, List<ExpansionRoom>> withKind = k =>
        {
            var all = new List<ExpansionRoom>(core);
            var r = below.Rooms[0];
            r.Kind = k;
            all.Add(r);
            return all;
        };
        foreach (var k in new[] { RoomKind.BlackMarket, RoomKind.Arms })
        {
            var km = withKind(k);
            int ka = km.Count;
            var kw = Expansion.Build(km, Expansion.GridCoreCuts());
            Check(kw.Areas[ka].PedestalWalls == (ShopPedestals.West | ShopPedestals.East | ShopPedestals.South), k + ": pedestals on three walls, the one door to the core");
            Check(kw.Doors.Count(d => d.A == ka || d.B == ka) == 1, k + ": one door");
            var kb = km[ka - 1];
            Check(!Expansion.PlacementsFor(km, PieceShape.Mono).Any(o => !SharesWallWithOther(km, ka, o.Rooms[0])), k + ": nothing can be built off it");
            Check(!kw.Cover.Any(c => c.Pos.X > kb.X && c.Pos.X < kb.X1 && c.Pos.Y > kb.Y && c.Pos.Y < kb.Y1), k + ": no cover in it");
            int need = k == RoomKind.Arms ? Game.ModIds.Length : Market.Count;
            Check(ShopPedestals.AllSpots(kw.Areas[ka]).Count >= need, k + ": a pedestal for each of its " + need + " wares (" + ShopPedestals.AllSpots(kw.Areas[ka]).Count + " pedestals)");
        }
        var mm = withKind(RoomKind.Medic);
        int ma = mm.Count;
        var mw = Expansion.Build(mm, Expansion.GridCoreCuts());
        Check(mw.Areas[ma].PedestalWalls == ShopPedestals.West, "Medic: pedestals on its west wall alone (" + mw.Areas[ma].PedestalWalls + ")");
        var mbox = mm[ma - 1];
        var mnext = Expansion.PlacementsFor(mm, PieceShape.Mono);
        Check(mnext.Any(o => o.Rooms[0].X == mbox.X1 && o.Rooms[0].Y == mbox.Y) && mnext.Any(o => o.Rooms[0].X == mbox.X && o.Rooms[0].Y == mbox.Y1), "Medic: rooms can be built off its east and south walls (three doors)");
        Check(!mnext.Any(o => o.Rooms[0].X1 == mbox.X && o.Rooms[0].Y == mbox.Y && !SharesWallWithOther(mm, ma, o.Rooms[0])), "Medic: nothing off its west wall");
    }

    /// Does this new box share a door's run of wall with any room but `except` (an area index)?
    static bool SharesWallWithOther(List<ExpansionRoom> map, int except, ExpansionRoom box)
    {
        for (int b = 1; b <= map.Count; b++)
        {
            if (b == except)
                continue;
            var o = map[b - 1];
            int run = 0;
            if (box.X1 == o.X || o.X1 == box.X)
                run = Math.Min(box.Y1, o.Y1) - Math.Max(box.Y, o.Y);
            else if (box.Y1 == o.Y || o.Y1 == box.Y)
                run = Math.Min(box.X1, o.X1) - Math.Max(box.X, o.X);
            if (run >= Expansion.MinDoorRun)
                return true;
        }
        return false;
    }
}
