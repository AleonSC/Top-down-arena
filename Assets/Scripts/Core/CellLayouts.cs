using System;
using System.Collections.Generic;

namespace Arena
{
    /// One hand-made layout for a single 600 px lattice cell (Docs/Mockups/cell-layouts.html): its walls, the slots where sleeping
    /// groups can stand and the slots where room traps can be. Numbers are cell-local (0..600). The chest always stands in the
    /// cell's middle (`RoomChests.CellMiddles`), so no wall touches it; the four door lanes (the middle of each side) stay open.
    public sealed class CellLayout
    {
        public string Name;
        public int[][] Walls;     // each: x, y, w, h
        public int[][] Sleepers;  // each: x, y (a sleeping group's middle)
        public int[][] Traps;     // each: x, y, wall (1: leans on a wall, so a turret is likelier there)
        public int[][] Loot;      // each: x, y (a bonus loot stash: gold, now and then vectors or a health orb; BonusLoot.cs)
        public int[][] Pits = new int[0][];  // each: x, y, w, h (a box) or x, y, r (a round pit): voids in the floor (Pits.cs)
    }

    /// The layouts that replace the random obstacles in grid rooms (owner Nov 20): `Pick` seeds one per cell from its place on
    /// the lattice, so every machine builds the same room. Rooms off the lattice keep `Expansion.AddCover`.
    public static partial class CellLayouts
    {
        public const int Cell = 600;

        /// Does a wall (x, y, w, h, cell-local) reach into one of the cell's four doorways: the door's width (Rooms.DoorW, in the middle of a
        /// side) a square deep? A wall beside a door ends flush with its edge, so the door's frame and the wall line up.
        public static bool InDoorway(int[] w)
        {
            float d0 = (Cell - Rooms.DoorW) / 2f, d1 = (Cell + Rooms.DoorW) / 2f, deep = RoomTraps.Tile;
            int x0 = w[0], y0 = w[1], x1 = w[0] + w[2], y1 = w[1] + w[3];
            bool acrossX = x0 < d1 && x1 > d0, acrossY = y0 < d1 && y1 > d0;
            return (acrossX && (y0 < deep || y1 > Cell - deep)) || (acrossY && (x0 < deep || x1 > Cell - deep));
        }

        static int[] W(int x, int y, int w, int h) => new[] { x, y, w, h };
        static int[] P(int x, int y) => new[] { x, y };
        static int[] T(int x, int y, int wall) => new[] { x, y, wall };

        public static readonly CellLayout[] All =
        {
            new CellLayout { Name = "Open Plaza",
                Walls = new[] { W(50, 50, 50, 50), W(500, 500, 50, 50) },
                Sleepers = new[] { P(130, 130), P(470, 470) },
                Traps = new[] { T(300, 150, 0), T(300, 450, 0), T(160, 300, 0), T(440, 300, 0) },
                Loot = new[] { P(470, 130) } },
            new CellLayout { Name = "Four Pillars",
                Walls = new[] { W(150, 150, 50, 50), W(400, 150, 50, 50), W(150, 400, 50, 50), W(400, 400, 50, 50) },
                Sleepers = new[] { P(120, 120), P(480, 480) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(300, 200, 0), T(300, 400, 0) },
                Loot = new[] { P(480, 120) } },
            new CellLayout { Name = "Vault",
                Walls = new[] { W(200, 200, 200, 50), W(200, 350, 200, 50) },
                Sleepers = new[] { P(120, 120), P(480, 120) },
                Traps = new[] { T(100, 300, 1), T(500, 300, 1), T(300, 130, 0), T(300, 470, 0) },
                Loot = new[] { P(480, 490) } },
            new CellLayout { Name = "Side Alcoves",
                Walls = new[] { W(150, 150, 300, 50), W(150, 400, 300, 50) },
                Sleepers = new[] { P(110, 300), P(490, 300) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(200, 300, 0), T(400, 300, 0) },
                Loot = new[] { P(490, 490) } },
            new CellLayout { Name = "Corner Nooks",
                Walls = new[] { W(50, 50, 100, 50), W(50, 50, 50, 100), W(450, 50, 100, 50), W(500, 50, 50, 100), W(50, 500, 100, 50), W(50, 450, 50, 100), W(450, 500, 100, 50), W(500, 450, 50, 100) },
                Sleepers = new[] { P(145, 145), P(455, 455) },
                Traps = new[] { T(300, 150, 0), T(300, 450, 0), T(150, 300, 0), T(450, 300, 0), T(300, 60, 1) },
                Loot = new[] { P(475, 125), P(125, 475) } },
            new CellLayout { Name = "Crossfire",
                Walls = new[] { W(150, 150, 250, 50), W(200, 400, 250, 50) },
                Sleepers = new[] { P(120, 120), P(120, 480) },
                Traps = new[] { T(540, 100, 1), T(540, 500, 1), T(225, 275, 0), T(225, 325, 0) },
                Loot = new[] { P(500, 120) } },
            new CellLayout { Name = "Maze Lite",
                Walls = new[] { W(100, 100, 200, 50), W(300, 100, 50, 150), W(300, 350, 50, 150), W(300, 450, 200, 50), W(100, 250, 50, 100), W(450, 250, 50, 100) },
                Sleepers = new[] { P(110, 70), P(480, 535) },
                Traps = new[] { T(60, 300, 1), T(540, 300, 1), T(210, 300, 0), T(400, 300, 0) },
                Loot = new[] { P(520, 80) } },
            new CellLayout { Name = "Pillar Field",
                Walls = new[] { W(100, 100, 50, 50), W(250, 100, 50, 50), W(400, 100, 50, 50), W(100, 250, 50, 50), W(400, 250, 50, 50), W(100, 400, 50, 50), W(250, 400, 50, 50), W(400, 400, 50, 50) },
                Sleepers = new[] { P(200, 200), P(355, 355), P(200, 400) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(60, 300, 1), T(210, 330, 0), T(380, 330, 0) },
                Loot = new[] { P(400, 200) } },
            new CellLayout { Name = "Bunker",
                Walls = new[] { W(150, 150, 300, 50), W(150, 400, 300, 50), W(150, 200, 50, 50), W(150, 350, 50, 50), W(400, 200, 50, 50), W(400, 350, 50, 50) },
                Sleepers = new[] { P(120, 120), P(480, 120), P(120, 480) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(75, 300, 0), T(525, 300, 0) },
                Loot = new[] { P(520, 520) } },
            new CellLayout { Name = "Barricades",
                Walls = new[] { W(50, 100, 350, 50), W(200, 200, 350, 50), W(50, 400, 350, 50) },
                Sleepers = new[] { P(120, 330), P(480, 480) },
                Traps = new[] { T(60, 60, 1), T(540, 540, 1), T(430, 330, 0), T(150, 325, 0) },
                Loot = new[] { P(500, 310) } },
            new CellLayout { Name = "Diagonal Cross",
                Walls = new[] { W(150, 150, 50, 50), W(400, 150, 50, 50), W(150, 400, 50, 50), W(400, 400, 50, 50), W(200, 200, 50, 50), W(350, 200, 50, 50), W(200, 350, 50, 50), W(350, 350, 50, 50) },
                Sleepers = new[] { P(300, 120), P(300, 480), P(120, 300), P(480, 300) },
                Traps = new[] { T(125, 300, 0), T(475, 300, 0), T(300, 125, 0), T(300, 475, 0) },
                Loot = new[] { P(175, 230) } },
            new CellLayout { Name = "Fortress Corners",
                Walls = new[] { W(50, 50, 150, 50), W(50, 50, 50, 150), W(400, 50, 150, 50), W(500, 50, 50, 150), W(50, 500, 150, 50), W(50, 400, 50, 150), W(400, 500, 150, 50), W(500, 400, 50, 150), W(250, 200, 100, 50), W(250, 350, 100, 50) },
                Sleepers = new[] { P(140, 140), P(460, 460) },
                Traps = new[] { T(300, 130, 0), T(300, 470, 0), T(130, 300, 0), T(470, 300, 0) },
                Loot = new[] { P(480, 135), P(135, 465) } },
            new CellLayout { Name = "Corner Closets",
                Walls = new[] { W(150, 0, 50, 200), W(0, 150, 50, 50), W(400, 400, 50, 200), W(550, 400, 50, 50) },  // (each closet's wall flush with the door beside it, Rooms.DoorW)
                Sleepers = new[] { P(124, 124), P(476, 476) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(200, 325, 0), T(400, 275, 0) },
                Loot = new[] { P(500, 110), P(110, 500) } },
            new CellLayout { Name = "Vault Room",
                Walls = new[] { W(150, 150, 300, 50), W(150, 400, 300, 50), W(150, 200, 50, 50), W(150, 350, 50, 50), W(400, 200, 50, 50), W(400, 350, 50, 50) },
                Sleepers = new[] { P(100, 100), P(500, 500) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(90, 300, 0), T(510, 300, 0) },
                Loot = new[] { P(500, 100), P(100, 500) } },
            new CellLayout { Name = "Barracks",
                Walls = new[] { W(0, 150, 250, 50), W(350, 150, 250, 50) },  // (flush with the side doors, Rooms.DoorW)
                Sleepers = new[] { P(130, 100), P(470, 100) },
                Traps = new[] { T(60, 400, 1), T(540, 400, 1), T(300, 420, 0), T(200, 320, 0) },
                Loot = new[] { P(500, 500), P(100, 500) } },
            new CellLayout { Name = "Corner Vaults",
                Walls = new[] { W(150, 0, 50, 200), W(0, 150, 50, 50), W(400, 0, 50, 200), W(550, 150, 50, 50), W(150, 400, 50, 200), W(0, 400, 50, 50), W(400, 400, 50, 200), W(550, 400, 50, 50) },  // (each vault's wall flush with the door beside it, Rooms.DoorW)
                Sleepers = new[] { P(124, 124), P(476, 476) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(150, 300, 0), T(450, 300, 0) },
                Loot = new[] { P(476, 124), P(124, 476) } },
            new CellLayout { Name = "Hall & Alcoves",
                Walls = new[] { W(0, 150, 250, 50), W(350, 150, 250, 50), W(0, 400, 250, 50), W(350, 400, 250, 50) },
                Sleepers = new[] { P(130, 90), P(470, 90) },
                Traps = new[] { T(60, 300, 1), T(540, 300, 1), T(200, 300, 0), T(400, 300, 0) },
                Loot = new[] { P(130, 510), P(470, 510) } },
            new CellLayout { Name = "Safe Room",
                Walls = new[] { W(150, 150, 300, 50), W(150, 150, 50, 300), W(400, 150, 50, 300), W(150, 400, 100, 50), W(350, 400, 100, 50) },
                Sleepers = new[] { P(100, 100), P(500, 500) },
                Traps = new[] { T(300, 60, 1), T(60, 300, 1), T(300, 520, 0), T(540, 300, 1) },
                Loot = new[] { P(500, 100), P(100, 500) } },
        };

        // Indices of the room-like layouts (the order of `All`).
        const int CornerClosets = 12, VaultRoom = 13, Barracks = 14, CornerVaults = 15, HallAlcoves = 16, SafeRoom = 17;
        static readonly int[] Junction = { CornerVaults, 1, 7, 10 };  // (Four Pillars, Pillar Field, Diagonal Cross)

        /// The lattice cell index (column, row) holding this point.
        public static void CellOf(float x, float y, out int cx, out int cy)
        {
            cx = (int)Math.Floor((x - Expansion.GridX0) / (double)Cell);
            cy = (int)Math.Floor((y - Expansion.GridY0) / (double)Cell);
        }

        static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        public static long CellKey(int cx, int cy) => Key(cx, cy);

        /// Cells whose plan a layout test fixed (the Cell Editor's TEST LAYOUT: `Game.DevTestCells`); `Plans` puts them over the rolled ones. Dev only, one local game.
        static readonly Dictionary<long, CellPlan> forced = new Dictionary<long, CellPlan>();

        public static void ForcePlan(int cx, int cy, CellPlan plan)
        {
            lock (forced)
                forced[Key(cx, cy)] = plan;
        }

        public static void ClearForced()
        {
            lock (forced)
                forced.Clear();
        }

        /// The plan each of these cells (keys from `CellKey`) would get as one piece, the way `Plans` rolls them: for the Cell Editor's room view.
        public static Dictionary<long, CellPlan> PlansOfCells(HashSet<long> cells)
        {
            var plans = new Dictionary<long, CellPlan>();
            foreach (long k in cells)
                plans[k] = PlanOf(cells, (int)(k >> 32), (int)(uint)k);
            return plans;
        }

        /// A cell's layout and how many quarter turns (clockwise) it is turned.
        public struct CellPlan { public int Layout, Rot, Variant; }  // Variant 0: the plain layout; n: its n-th variant (CellVariants.cs)

        /// A layout turned `rot` quarter turns clockwise about the cell's middle: (x, y) goes to (600 - y, x).
        public sealed class Turned
        {
            public int[][] Walls, Sleepers, Loot, Traps, Pits;
        }

        static readonly Dictionary<int, Turned> turnedCache = new Dictionary<int, Turned>();

        /// Make the turned copies again (after a layout was edited: `CellLayoutEdits.Set`).
        public static void ForgetTurned()
        {
            lock (turnedCache)
                turnedCache.Clear();
        }

        public static Turned TurnedOf(CellPlan plan)
        {
            int key = (plan.Layout * 8 + plan.Variant) * 4 + (plan.Rot & 3);
            Turned t;
            lock (turnedCache)
            {
                if (turnedCache.TryGetValue(key, out t))
                    return t;
                var l = LayoutOf(plan);
                int k = plan.Rot & 3;
                t = new Turned { Walls = new int[l.Walls.Length][], Sleepers = new int[l.Sleepers.Length][], Loot = new int[l.Loot.Length][], Traps = new int[l.Traps.Length][], Pits = new int[l.Pits.Length][] };
                for (int i = 0; i < l.Walls.Length; i++) t.Walls[i] = TurnRect(l.Walls[i], k);
                for (int i = 0; i < l.Sleepers.Length; i++) t.Sleepers[i] = TurnPoint(l.Sleepers[i], k);
                for (int i = 0; i < l.Loot.Length; i++) t.Loot[i] = TurnPoint(l.Loot[i], k);
                for (int i = 0; i < l.Traps.Length; i++) t.Traps[i] = TurnPoint(l.Traps[i], k);
                for (int i = 0; i < l.Pits.Length; i++) t.Pits[i] = l.Pits[i].Length == 3 ? TurnPoint(l.Pits[i], k) : TurnRect(l.Pits[i], k);
                turnedCache[key] = t;
            }
            return t;
        }

        static int[] TurnRect(int[] w, int k)
        {
            int x = w[0], y = w[1], ww = w[2], hh = w[3];
            for (int i = 0; i < k; i++)
            {
                int nx = Cell - (y + hh), ny = x;
                x = nx; y = ny;
                int t = ww; ww = hh; hh = t;
            }
            return new[] { x, y, ww, hh };
        }

        static int[] TurnPoint(int[] p, int k)
        {
            int x = p[0], y = p[1];
            for (int i = 0; i < k; i++)
            {
                int nx = Cell - y, ny = x;
                x = nx; y = ny;
            }
            var r = (int[])p.Clone();
            r[0] = x; r[1] = y;
            return r;
        }

        /// A box that sits whole cells on the lattice (what grid pieces are).
        public static bool OnLattice(ExpansionRoom r)
            => r.W > 0 && r.H > 0 && r.W % Cell == 0 && r.H % Cell == 0 && Mod(r.X - Expansion.GridX0) == 0 && Mod(r.Y - Expansion.GridY0) == 0;

        static int Mod(int v) => ((v % Cell) + Cell) % Cell;

        /// Every cell origin (top-left corner) of this box.
        public static IEnumerable<V2> CellOrigins(ExpansionRoom r)
        {
            for (int y = r.Y; y + Cell <= r.Y1; y += Cell)
                for (int x = r.X; x + Cell <= r.X1; x += Cell)
                    yield return new V2(x, y);
        }

        /// The plan of every lattice cell in these rooms (the whole room list, so a piece's boxes are seen together). A cell's role
        /// comes from where the rest of its piece lies: a dead end gets a Safe Room, a Vault Room or a Barracks turned to face its
        /// way in, a corridor cell a Hall or Vault Room along the corridor, a corner cell closets or vaults, a junction or a lone
        /// cell one of the others; seeded from the cell's place, so every machine agrees.
        public static Dictionary<long, CellPlan> Plans(IList<ExpansionRoom> rooms)
        {
            var plans = new Dictionary<long, CellPlan>();
            var done = new HashSet<int>();
            for (int i = 0; i < rooms.Count; i++)
            {
                if (done.Contains(i) || !OnLattice(rooms[i]))
                    continue;
                var group = Expansion.GroupOf(rooms, i);
                var cells = new HashSet<long>();
                var list = new List<int[]>();
                foreach (int gi in group)
                {
                    done.Add(gi);
                    if (!OnLattice(rooms[gi]))
                        continue;
                    foreach (var o in CellOrigins(rooms[gi]))
                    {
                        CellOf(o.X + 1, o.Y + 1, out int cx, out int cy);
                        if (cells.Add(Key(cx, cy)))
                            list.Add(new[] { cx, cy });
                    }
                }
                bool vary = CanVary(rooms, group);
                foreach (var c in list)
                {
                    var plan = PlanOf(cells, c[0], c[1]);
                    if (vary)
                        plan.Variant = RollVariant(plan.Layout, c[0], c[1]);
                    plans[Key(c[0], c[1])] = plan;
                }
            }
            lock (forced)
                foreach (var kv in forced)
                    if (plans.ContainsKey(kv.Key))
                        plans[kv.Key] = kv.Value;
            return plans;
        }

        static CellPlan PlanOf(HashSet<long> cells, int cx, int cy)
        {
            bool[] nb = { cells.Contains(Key(cx, cy - 1)), cells.Contains(Key(cx + 1, cy)), cells.Contains(Key(cx, cy + 1)), cells.Contains(Key(cx - 1, cy)) };  // N, E, S, W
            int count = (nb[0] ? 1 : 0) + (nb[1] ? 1 : 0) + (nb[2] ? 1 : 0) + (nb[3] ? 1 : 0);
            var rng = new DetRandom(cx * 73856093 ^ cy * 19349663 ^ 0x5bd1e995);
            switch (count)
            {
                case 0:
                    if (ForcedBase() >= 0)
                        return new CellPlan { Layout = ForcedBase(), Rot = rng.Int(4) };  // (dev: the forced variant's layout, CellVariants.cs)
                    return new CellPlan { Layout = rng.Int(All.Length), Rot = rng.Int(4) };
                case 1:
                {
                    int d = nb[0] ? 0 : nb[1] ? 1 : nb[2] ? 2 : 3;  // the way in
                    int pick = rng.Int(3);
                    if (pick == 2)  // (doorways east and west: turned so they line up with the way in)
                        return new CellPlan { Layout = VaultRoom, Rot = d == 1 || d == 3 ? 0 : 1 };
                    return new CellPlan { Layout = pick == 0 ? SafeRoom : Barracks, Rot = (d + 2) & 3 };  // (their door or strip is on the far side from the way in)
                }
                case 2:
                    if (nb[0] == nb[2])  // a corridor: N-S or E-W
                    {
                        bool eastWest = nb[1];
                        return new CellPlan { Layout = rng.Int(10) < 6 ? HallAlcoves : VaultRoom, Rot = eastWest ? 0 : 1 };
                    }
                    return new CellPlan { Layout = rng.Int(2) == 0 ? CornerClosets : CornerVaults, Rot = rng.Int(4) };
                default:
                    return new CellPlan { Layout = Junction[rng.Int(Junction.Length)], Rot = rng.Int(4) };
            }
        }

        public static bool TryPlan(Dictionary<long, CellPlan> plans, int cx, int cy, out CellPlan plan) => plans.TryGetValue(Key(cx, cy), out plan);

        /// A room's walls from its cells' layouts.
        public static void AddWalls(List<Solid> to, ExpansionRoom r, Dictionary<long, CellPlan> plans)
        {
            foreach (var o in CellOrigins(r))
            {
                CellOf(o.X + 1, o.Y + 1, out int cx, out int cy);
                if (!plans.TryGetValue(Key(cx, cy), out var plan))
                    continue;
                foreach (var w in TurnedOf(plan).Walls)
                    to.Add(World.Box(o.X + w[0], o.Y + w[1], o.X + w[0] + w[2], o.Y + w[1] + w[3]));
            }
        }

        static List<V2> SlotsOf(IList<ExpansionRoom> boxes, Dictionary<long, CellPlan> plans, Func<Turned, int[][]> pick)
        {
            var list = new List<V2>();
            foreach (var b in boxes)
                if (OnLattice(b))
                    foreach (var o in CellOrigins(b))
                    {
                        CellOf(o.X + 1, o.Y + 1, out int cx, out int cy);
                        if (!plans.TryGetValue(Key(cx, cy), out var plan))
                            continue;
                        foreach (var s in pick(TurnedOf(plan)))
                            list.Add(new V2(o.X + s[0], o.Y + s[1]));
                    }
            return list;
        }

        /// The middles of every sleeper slot in these boxes (lattice boxes only); `all` is the whole room list the plans come from.
        public static List<V2> SleeperSlots(IList<ExpansionRoom> boxes, IList<ExpansionRoom> all) => SlotsOf(boxes, Plans(all), t => t.Sleepers);

        /// The middles of every bonus loot slot in these boxes (lattice boxes only).
        public static List<V2> LootSlots(IList<ExpansionRoom> boxes, IList<ExpansionRoom> all) => SlotsOf(boxes, Plans(all), t => t.Loot);

        /// Is this point (grown by pad) inside a wall of the layout of the cell it is in?
        public static bool Blocked(V2 p, float pad, Dictionary<long, CellPlan> plans)
        {
            CellOf(p.X, p.Y, out int cx, out int cy);
            if (!plans.TryGetValue(Key(cx, cy), out var plan))
                return false;
            float ox = Expansion.GridX0 + cx * Cell, oy = Expansion.GridY0 + cy * Cell;
            var t = TurnedOf(plan);
            foreach (var w in t.Walls)
                if (p.X > ox + w[0] - pad && p.X < ox + w[0] + w[2] + pad && p.Y > oy + w[1] - pad && p.Y < oy + w[1] + w[3] + pad)
                    return true;
            foreach (var w in t.Pits)  // (a pit stops standing as a wall does: sleepers and loot keep off it)
                if (InPit(w, p.X - ox, p.Y - oy, pad))
                    return true;
            return false;
        }

        /// Where a group's member stands: the wanted spot, or one nearer the slot's middle if a wall is there.
        public static V2 Clear(V2 want, V2 mid, Dictionary<long, CellPlan> plans)
        {
            for (int s = 0; s <= 4; s++)
            {
                V2 p = mid + (want - mid) * (1f - s * 0.25f);
                if (!Blocked(p, 22f, plans))
                    return p;
            }
            return mid;
        }
    }
}
