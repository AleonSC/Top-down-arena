using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Cell layouts (Core/CellLayouts.cs): a grid room's cells use hand-made, room-like layouts, not random obstacles.
static partial class Program
{
    static void CellLayoutsTest()
    {
        int bad = 0;
        for (int li = 0; li < CellLayouts.All.Length; li++)
            for (int rot = 0; rot < 4; rot++)
            {
                var l = CellLayouts.All[li];
                var t = CellLayouts.TurnedOf(new CellLayouts.CellPlan { Layout = li, Rot = rot });
                string tag = "  " + l.Name + " (turn " + rot + "): ";
                // the middle and the four door lanes are open; every wall is inside the cell
                Func<float, float, float, bool> hit = (x, y, pad) => t.Walls.Any(w => x > w[0] - pad && x < w[0] + w[2] + pad && y > w[1] - pad && y < w[1] + w[3] + pad);
                if (hit(300, 300, 40)) { bad++; Console.WriteLine(tag + "the chest's middle is walled"); }
                foreach (var d in new[] { new[] { 300, 0 }, new[] { 300, 600 }, new[] { 0, 300 }, new[] { 600, 300 } })
                    if (hit(d[0], d[1], 50)) { bad++; Console.WriteLine(tag + "a door lane is walled"); }
                if (t.Walls.Any(w => w[0] < 0 || w[1] < 0 || w[0] + w[2] > 600 || w[1] + w[3] > 600)) { bad++; Console.WriteLine(tag + "a wall leaves its cell"); }
                foreach (var lo in t.Loot)
                    if (hit(lo[0], lo[1], 20) || Math.Abs(lo[0] - 300) + Math.Abs(lo[1] - 300) < 120) { bad++; Console.WriteLine(tag + "a loot slot is in a wall or on the chest"); }
                if (t.Loot.Length == 0 || t.Sleepers.Length == 0 || t.Traps.Length == 0) { bad++; Console.WriteLine(tag + "no slots"); }
                foreach (var s in t.Sleepers)
                    if (hit(s[0], s[1], 24)) { bad++; Console.WriteLine(tag + "a sleeper slot is in a wall"); }
                foreach (var tr in t.Traps)
                    if (hit(tr[0], tr[1], 20) && tr[2] == 0) { bad++; Console.WriteLine(tag + "a trap slot is in a wall"); }
            }
        Check(CellLayouts.All.Length == 18 && bad == 0, "eighteen layouts, each turned four ways: the middle and door lanes open, walls and slots inside the cell (" + bad + " bad)");

        // the Cell Editor's file: an edit round-trips, shows in the turned copies, and reverts
        {
            var saved = CellLayoutEdits.Original(3);
            var edit = CellLayoutEdits.Clone(saved);
            edit.Walls = edit.Walls.Concat(new[] { new[] { 20, 20, 60, 20 } }).ToArray();
            CellLayoutEdits.Set(3, edit);
            string file = CellLayoutEdits.Serialize();
            bool edited = CellLayoutEdits.IsEdited(3) && file.Contains(saved.Name + "\t") && CellLayouts.TurnedOf(new CellLayouts.CellPlan { Layout = 3, Rot = 0 }).Walls.Length == saved.Walls.Length + 1;
            CellLayoutEdits.Revert(3);
            bool back = !CellLayoutEdits.IsEdited(3) && CellLayoutEdits.Serialize().IndexOf('\t') < 0;
            int n = CellLayoutEdits.Parse(file);
            bool again = n == 1 && CellLayouts.All[3].Walls.Length == saved.Walls.Length + 1;
            CellLayoutEdits.Revert(3);
            Check(edited && back && again && CellLayoutEdits.Problems(CellLayouts.All[3]).Count == 0 && CellLayoutEdits.Problems(edit).Count == 0,
                "an edited layout is saved as one line, read back, used by the turned copies and reverts");
        }

        var rooms = new List<ExpansionRoom>(Expansion.GridCore());
        var spot = Expansion.PlacementsFor(rooms, PieceShape.TetO).First();
        rooms.AddRange(spot.Rooms);
        var w1 = Expansion.Build(rooms, Expansion.GridCoreCuts());
        var w2 = Expansion.Build(rooms, Expansion.GridCoreCuts());
        Check(CellLayouts.OnLattice(spot.Rooms[0]) && w1.Cover.Length > 0 && w1.Cover.Length == w2.Cover.Length && w1.Cover.Where((c, i) => c.Pos.DistanceTo(w2.Cover[i].Pos) > 0.01f).Count() == 0,
            "a grid piece's cover is its cells' layouts, the same on every build (" + w1.Cover.Length + " walls)");

        // Roles: a piece's cells take their layout from where the rest of the piece lies.
        var layoutsSeen = new HashSet<int>();
        int badRole = 0, pieces = 0;
        foreach (PieceShape ps in Enum.GetValues(typeof(PieceShape)))
            foreach (var pl in Expansion.PlacementsFor(Expansion.GridCore(), ps).Take(8))
            {
                var all = new List<ExpansionRoom>(Expansion.GridCore());
                all.AddRange(pl.Rooms);
                var plans = CellLayouts.Plans(all);
                var mine = new HashSet<string>(); var cellList = new List<int[]>();
                foreach (var r in pl.Rooms) foreach (var o in CellLayouts.CellOrigins(r)) { CellLayouts.CellOf(o.X + 1, o.Y + 1, out int cx, out int cy); mine.Add(cx + "," + cy); cellList.Add(new[] { cx, cy }); }
                pieces++;
                Func<int, int, bool> has = (a, b) => mine.Contains(a + "," + b);
                foreach (var cc in cellList)
                {
                    int cx = cc[0], cy = cc[1];
                    CellLayouts.TryPlan(plans, cx, cy, out var plan);
                    layoutsSeen.Add(plan.Layout);
                    int nbs = (has(cx, cy - 1) ? 1 : 0) + (has(cx + 1, cy) ? 1 : 0) + (has(cx, cy + 1) ? 1 : 0) + (has(cx - 1, cy) ? 1 : 0);
                    string name = CellLayouts.All[plan.Layout].Name;
                    if (nbs == 1 && !(name == "Safe Room" || name == "Barracks" || name == "Vault Room")) badRole++;
                    if (nbs == 2 && !(name == "Hall & Alcoves" || name == "Vault Room" || name == "Corner Closets" || name == "Corner Vaults")) badRole++;
                    if (nbs == 1 && name == "Safe Room")  // its doorway faces the way in: the point 140 px that way is open, the far side walled
                    {
                        int d = has(cx, cy - 1) ? 0 : has(cx + 1, cy) ? 1 : has(cx, cy + 1) ? 2 : 3;
                        float dx = d == 1 ? 1 : d == 3 ? -1 : 0, dy = d == 2 ? 1 : d == 0 ? -1 : 0;
                        float ox = Expansion.GridX0 + cx * 600 + 300, oy = Expansion.GridY0 + cy * 600 + 300;
                        if (CellLayouts.Blocked(new V2(ox + dx * 140, oy + dy * 140), 6f, plans) || !CellLayouts.Blocked(new V2(ox - dx * 140, oy - dy * 140), 6f, plans)) badRole++;
                    }
                }
            }
        Check(badRole == 0 && pieces > 40, "dead ends get a safe room, barracks or vault, corridors a hall, vault or closets, and a safe room's doorway faces the way in (" + pieces + " pieces, " + badRole + " bad)");
        Check(layoutsSeen.Count >= 6, "pieces use the room-like layouts (" + layoutsSeen.Count + " kinds)");

        var seen = new HashSet<int>();
        var one = new List<ExpansionRoom>(Expansion.GridCore());
        for (int cx = -5; cx < 15; cx++)
            for (int cy = -5; cy < 15; cy++)
            {
                var single = new List<ExpansionRoom> { new ExpansionRoom { X = Expansion.GridX0 + cx * 600, Y = Expansion.GridY0 + cy * 600, W = 600, H = 600 } };
                CellLayouts.TryPlan(CellLayouts.Plans(single), cx, cy, out var pl1);
                seen.Add(pl1.Layout);
            }
        Check(seen.Count == CellLayouts.All.Length, "a lone cell can use any of the eighteen layouts");

        var allRooms = new List<ExpansionRoom>(Expansion.GridCore());
        allRooms.AddRange(spot.Rooms);
        var slots = CellLayouts.SleeperSlots(spot.Rooms, allRooms);
        Check(slots.Count >= 4 && slots.All(p => spot.Rooms.Any(r => p.X > r.X && p.X < r.X1 && p.Y > r.Y && p.Y < r.Y1)), "a four-cell piece has sleeper slots in its cells (" + slots.Count + ")");
        var plansAll = CellLayouts.Plans(allRooms);
        bool clear = true;
        foreach (var p in slots)
            for (int k = 0; k < 6; k++)
                clear &= !CellLayouts.Blocked(CellLayouts.Clear(DormantGroups.Spot(p, k, 6), p, plansAll), 20f, plansAll);
        Check(clear, "a group of six at any slot stands clear of the walls");

        // Room traps: derived from the rooms, the same on every build; only Combat grid rooms get them.
        var t1 = RoomTraps.For(allRooms); var t2 = RoomTraps.For(allRooms);
        Check(t1.Count > 0 && t1.Count == t2.Count && t1.Select((t, i) => t.Pos.DistanceTo(t2[i].Pos) < 0.01f && t.Kind == t2[i].Kind).All(b => b), "a Combat piece rolls traps, the same every time (" + t1.Count + ")");
        Check(t1.All(t => spot.Rooms.Any(r => t.Pos.X > r.X && t.Pos.X < r.X1 && t.Pos.Y > r.Y && t.Pos.Y < r.Y1)), "they stand inside the piece, none in the core");
        var shop = new List<ExpansionRoom>(Expansion.GridCore());
        var sp = Expansion.PlacementsFor(shop, PieceShape.Mono).First().Rooms[0]; sp.Kind = RoomKind.Shop; shop.Add(sp);
        Check(RoomTraps.For(shop).Count == 0, "a shop gets none");
        var plates = t1.Where(t => t.Kind != TrapKind.Turret).ToList();
        Check(plates.Count > 0 && plates.All(t => t.HalfX >= 24f && t.HalfY >= 24f) && plates.Select(t => t.HalfX + t.HalfY * 1000f).Distinct().Count() > 1, "spike plates and slow fields come in different sizes (" + string.Join("/", plates.Select(t => t.HalfX + "x" + t.HalfY).Distinct()) + ")");
        Func<float, bool> onLine = v => Math.Abs(v / RoomTraps.Tile - Math.Round(v / RoomTraps.Tile)) < 0.001f;
        Check(plates.All(t => onLine(t.Pos.X - t.HalfX) && onLine(t.Pos.X + t.HalfX) && onLine(t.Pos.Y - t.HalfY) && onLine(t.Pos.Y + t.HalfY)), "every plate's edges lie on the floor grid");
        bool fits = true;
        foreach (var l in CellLayouts.All)
            foreach (var s in l.Traps)
            {
                float fit = RoomTraps.Fit(l.Walls, s[0], s[1]);
                var rr = new DetRandom(s[0] * 31 + s[1]);
                fits &= RoomTraps.PickHalf(RoomTraps.SlowSizes, fit, rr) <= Math.Max(fit, RoomTraps.SlowSizes[0]) && RoomTraps.PickHalf(RoomTraps.SpikeSizes, fit, rr) <= Math.Max(fit, RoomTraps.SpikeSizes[0]);
            }
        Check(fits, "a plate never outgrows the room around its slot");
        bool stripsOk = true; int strips = 0;
        foreach (var l in CellLayouts.All)
            foreach (var s in l.Traps)
                for (int sd = 0; sd < 40; sd++)
                {
                    RoomTraps.PickSize(l.Walls, s[0], s[1], RoomTraps.SlowSizes, RoomTraps.SlowLongs, new DetRandom(sd * 7 + s[0]), out float hx, out float hy);
                    if (hx != hy) { strips++; stripsOk &= RoomTraps.RectFree(l.Walls, s[0], s[1], hx, hy); }
                }
        Check(strips > 0 && stripsOk, "pinched slots grow strips along the open way, clear of every wall (" + strips + " strips)");
        var kinds = new HashSet<TrapKind>(); int empty = 0, total = 0;
        for (int i = 0; i < 400; i++) { var k = RoomTraps.Roll(new DetRandom(i), i % 2 == 0); total++; if (k.HasValue) kinds.Add(k.Value); else empty++; }
        Check(kinds.Count == 3 && empty > 100 && empty < 180, "a slot rolls a turret, a spike plate, a slow field or nothing (" + empty + " of " + total + " empty)");
    }
}
