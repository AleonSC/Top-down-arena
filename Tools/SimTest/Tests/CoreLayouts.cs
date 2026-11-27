using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Core layouts (Core/CoreLayouts.cs): ten hand-made floor plans for a Grid run's core, one a run, a new one each Campaign map.
static partial class Program
{
    static void CoreLayoutsTest(float dt)
    {
        const int W = CoreLayouts.W, H = CoreLayouts.H;
        var doors = new List<int[]>();  // each door spot's lane: x0, y0, x1, y1 in the core's frame
        foreach (int x in new[] { 300, 900, 1500, 2100 })
        {
            doors.Add(new[] { x - CoreLayouts.LaneHalf, 0, x + CoreLayouts.LaneHalf, CoreLayouts.LaneDeep });
            doors.Add(new[] { x - CoreLayouts.LaneHalf, H - CoreLayouts.LaneDeep, x + CoreLayouts.LaneHalf, H });
        }
        foreach (int y in new[] { 300, 900, 1500 })
        {
            doors.Add(new[] { 0, y - CoreLayouts.LaneHalf, CoreLayouts.LaneDeep, y + CoreLayouts.LaneHalf });
            doors.Add(new[] { W - CoreLayouts.LaneDeep, y - CoreLayouts.LaneHalf, W, y + CoreLayouts.LaneHalf });
        }
        Func<int[], int[]> boxOf = p => p[0] == 0 ? new[] { p[1], p[2], p[1] + p[3], p[2] + p[4] }
            : new[] { p[1] - Rad(p), p[2] - Rad(p), p[1] + Rad(p), p[2] + Rad(p) };

        // Every layout: inside the core, off the door lanes and the gate's floor, and every open spot reachable from the middle.
        int bad = 0;
        var names = new HashSet<string>();
        foreach (var l in CoreLayouts.All)
        {
            names.Add(l.Name);
            var pieces = CoreLayouts.Expanded(l.Id);
            foreach (var p in pieces)
            {
                var b = boxOf(p);
                string why = null;
                if (b[0] < 14 || b[1] < 14 || b[2] > W - 14 || b[3] > H - 14) why = "leaves the core";
                else if (doors.Any(d => b[0] < d[2] && b[2] > d[0] && b[1] < d[3] && b[3] > d[1])) why = "is in a door lane";
                else
                {
                    float nx = Math.Max(b[0], Math.Min(W / 2, b[2])), ny = Math.Max(b[1], Math.Min(H / 2, b[3]));
                    if (Math.Sqrt((nx - W / 2) * (nx - W / 2) + (ny - H / 2) * (ny - H / 2)) < CoreLayouts.GateClear) why = "is on the gate's floor";
                }
                if (why != null) { bad++; Console.WriteLine("  " + l.Name + ": a piece at " + p[1] + "," + p[2] + " " + why); }
            }
            var solid = pieces.Where(p => p[0] < 2).ToList();  // two walls or pillars either touch or leave room to walk between (a narrow slot snags bots and enemies)
            for (int i = 0; i < solid.Count; i++)
                for (int j = i + 1; j < solid.Count; j++)
                {
                    float gap = Gap(solid[i], solid[j], boxOf);
                    if (gap > 0.5f && gap < 50f) { bad++; Console.WriteLine("  " + l.Name + ": a " + (int)gap + " px slot between pieces at " + solid[i][1] + "," + solid[i][2] + " and " + solid[j][1] + "," + solid[j][2]); }
                }
            int open, reached;
            Flood(pieces, boxOf, out open, out reached);
            if (reached < open) { bad++; Console.WriteLine("  " + l.Name + ": " + (open - reached) + " of " + open + " open spots are walled off"); }
            if (pieces.Count < 40) { bad++; Console.WriteLine("  " + l.Name + ": only " + pieces.Count + " pieces"); }
        }
        Check(CoreLayouts.All.Length == 10 && names.Count == 10 && CoreLayouts.All.Select((l, i) => (int)l.Id == i).All(x => x) && bad == 0,
            "ten core layouts of 40+ pieces each: inside the core, the 14 door lanes and the gate's floor clear, no slot too narrow to walk, no walled-off pocket (" + bad + " bad)");

        // On the 50 px floor grid (the core's corner is on it): wall edges on grid lines, crates one to a square on its middle.
        {
            const int T = (int)RoomTraps.Tile;
            int off = 0, twice = 0;
            foreach (var l in CoreLayouts.All)
            {
                var seen = new HashSet<string>();
                foreach (var p in CoreLayouts.Expanded(l.Id))
                {
                    bool on = p[0] == 0 ? p[1] % T == 0 && p[2] % T == 0 && p[3] % T == 0 && p[4] % T == 0
                        : p[0] == 1 ? (p[3] * 2) % T == 0 && ((p[3] * 2 / T) % 2 == 1 ? (p[1] - T / 2) % T == 0 && (p[2] - T / 2) % T == 0 : p[1] % T == 0 && p[2] % T == 0)
                        : (p[1] - T / 2) % T == 0 && (p[2] - T / 2) % T == 0;
                    if (!on) { off++; Console.WriteLine("  " + l.Name + ": off the grid at " + p[1] + "," + p[2]); }
                    if (p[0] >= 2 && !seen.Add(p[1] + "," + p[2])) twice++;
                }
            }
            Check(Expansion.GridX0 % T == 0 && Expansion.GridY0 % T == 0 && off == 0 && twice == 0,
                "every core plan is on the floor grid: wall edges on its lines, crates on square middles, one a square (" + off + " off, " + twice + " doubled)");
        }

        // The world: a layout's walls and pillars stand in the core in place of its rooms' random cover, the same on every build.
        var core = Expansion.GridCore();
        var plain = Expansion.Build(core, Expansion.GridCoreCuts());
        var w1 = Expansion.Build(core, Expansion.GridCoreCuts(), null, null, CoreLayoutId.Reactor);
        var w2 = Expansion.Build(core, Expansion.GridCoreCuts(), null, null, CoreLayoutId.Reactor);
        int solids = CoreLayouts.Expanded(CoreLayoutId.Reactor).Count(p => p[0] < 2);
        Check(w1.Core == CoreLayoutId.Reactor && plain.Core == CoreLayoutId.None && w1.Cover.Length == solids && w2.Cover.Length == solids
              && w1.Cover.Where((c, i) => c.Pos.DistanceTo(w2.Cover[i].Pos) > 0.01f).Count() == 0,
            "the core's cover is its layout's " + solids + " walls and pillars, none of the random cover (" + plain.Cover.Length + " without)");
        var lay = w1.With(LayoutId.Cross);
        Check(lay.Crates.Length == CoreLayouts.Crates(CoreLayoutId.Reactor).Length && lay.Crates.Length > 0 && lay.Solids.Length == w1.Cover.Length + w1.Walls().Count()
              && lay.Name.StartsWith("Reactor"), "in the hub the core's plan takes the place of the boss cycle's: its crates, none of the plan's walls (" + lay.Crates.Length + " crates)");
        var notGrid = Expansion.Build(new List<ExpansionRoom>(), null, null, null, CoreLayoutId.Reactor);
        Check(notGrid.Core == CoreLayoutId.None && notGrid.Cover.Length == 0, "no core, no core layout");

        // Picking: the Campaign's ten maps see all ten, never the same twice running.
        {
            var rng = new Random(5);
            int used = 0;
            var last = CoreLayoutId.None;
            var seen = new List<CoreLayoutId>();
            bool repeat = false;
            for (int i = 0; i < 30; i++)
            {
                var id = CoreLayouts.Pick(rng, ref used, last);
                repeat |= id == last;
                seen.Add(id);
                last = id;
            }
            Check(seen.Take(10).Distinct().Count() == 10 && seen.Skip(10).Take(10).Distinct().Count() == 10 && !repeat, "ten picks are all ten layouts, then all ten again, never one twice running");
        }

        // Saves keep the layout and the ones used; an older save has none (a fresh pick on Continue).
        var made = new RunSave { Id = "c1", Wave = 12, LongRun = true, HasExpansion = true, ExpandSeed = 77, FloatCycle = 2, MapNumber = 3, CoreLayout = CoreLayoutId.Trenches, CoreLayoutsUsed = 0x105 };
        made.Seats.Add(new SeatSave { Name = "A", Vectors = 30, VectorCap = 70 });
        var back = RunSave.Parse(made.ToText(), "c1");
        var old = RunSave.Parse(made.ToText().Replace("expand 77 2 3 0 8 261", "expand 77 2 3 0"), "c0");
        Check(back != null && back.CoreLayout == CoreLayoutId.Trenches && back.CoreLayoutsUsed == 0x105 && old != null && old.CoreLayout == CoreLayoutId.None,
            "a save keeps the core layout and the ones used; an older save has none");

        // A game: the host picks one at the start and the client gets it; a new pick reaches the client too.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        typeof(Game).GetMethod("BroadcastExpansion", flags).Invoke(g, null);
        Run(s, dt, 1f, () => cg.CoreLayout == g.CoreLayout);
        Check(g.CoreLayout != CoreLayoutId.None && cg.CoreLayout == g.CoreLayout, "a run starts with a core layout (" + CoreLayouts.Name(g.CoreLayout) + "), the same on the client");
        var first = g.CoreLayout;
        typeof(Game).GetMethod("PickCoreLayout", flags).Invoke(g, null);
        typeof(Game).GetMethod("BroadcastExpansion", flags).Invoke(g, null);
        Run(s, dt, 1f, () => cg.CoreLayout == g.CoreLayout);
        g.Expanded.Clear(); g.Expanded.AddRange(Expansion.GridCore()); g.Openings.Clear(); g.Openings.AddRange(Expansion.GridCoreCuts());
        typeof(Game).GetField("expWorld", flags).SetValue(g, null);
        Check(g.CoreLayout != first && cg.CoreLayout == g.CoreLayout && g.ExpansionWorld.Core == g.CoreLayout,
            "a new pick (" + CoreLayouts.Name(g.CoreLayout) + ") is never the last one, reaches the client and builds into the core");
    }

    static float Gap(int[] a, int[] b, Func<int[], int[]> boxOf)
    {
        if (a[0] == 1 && b[0] == 1)
            return (float)Math.Sqrt((a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2])) - a[3] - b[3];
        if (b[0] == 1) { var t = a; a = b; b = t; }
        var B = boxOf(b);
        if (a[0] == 1)
        {
            float nx = Math.Max(B[0], Math.Min(a[1], B[2])), ny = Math.Max(B[1], Math.Min(a[2], B[3]));
            return (float)Math.Sqrt((nx - a[1]) * (nx - a[1]) + (ny - a[2]) * (ny - a[2])) - a[3];
        }
        var A = boxOf(a);
        float dx = Math.Max(0, Math.Max(A[0] - B[2], B[0] - A[2])), dy = Math.Max(0, Math.Max(A[1] - B[3], B[1] - A[3]));
        return dx == 0 && dy == 0 ? -1f : (float)Math.Sqrt(dx * dx + dy * dy);
    }

    static int Rad(int[] p) => p[0] == 1 ? p[3] : p[0] == 3 ? 23 : 18;

    /// Flood the core's floor on a 20 px grid for a body 18 px round, from the middle; counts the open spots and those reached.
    static void Flood(List<int[]> pieces, Func<int[], int[]> boxOf, out int open, out int reached)
    {
        const int step = 20, body = 18, cw = CoreLayouts.W / step, ch = CoreLayouts.H / step;
        var free = new bool[cw, ch];
        open = 0;
        for (int i = 0; i < cw; i++)
            for (int j = 0; j < ch; j++)
            {
                float x = i * step + step / 2f, y = j * step + step / 2f;
                bool ok = x > 14 + body && y > 14 + body && x < CoreLayouts.W - 14 - body && y < CoreLayouts.H - 14 - body;
                foreach (var p in pieces)
                {
                    if (!ok) break;
                    if (p[0] == 1) ok = Math.Sqrt((x - p[1]) * (x - p[1]) + (y - p[2]) * (y - p[2])) > p[3] + body;
                    else { var b = boxOf(p); ok = x < b[0] - body || x > b[2] + body || y < b[1] - body || y > b[3] + body; }
                }
                free[i, j] = ok;
                if (ok) open++;
            }
        var seen = new bool[cw, ch];
        var q = new Queue<int>();
        q.Enqueue(cw / 2 * ch + ch / 2); seen[cw / 2, ch / 2] = true;
        reached = 0;
        while (q.Count > 0)
        {
            int k = q.Dequeue(), ci = k / ch, cj = k % ch;
            reached++;
            foreach (var d in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
            {
                int ni = ci + d[0], nj = cj + d[1];
                if (ni < 0 || nj < 0 || ni >= cw || nj >= ch || seen[ni, nj] || !free[ni, nj]) continue;
                seen[ni, nj] = true;
                q.Enqueue(ni * ch + nj);
            }
        }
    }
}
