using System;
using System.Collections.Generic;

namespace Arena
{
    /// The core's floor plans (Docs/Mockups/core-layouts.html): ten hand-made layouts for the
    /// Grid run's 4 x 3 cell core. Each keeps the 14 door spots and the boss gate's floor in the middle clear.
    /// Walls are the only hard obstacles (a pillar is a square wall block, one or two squares); every other obstacle is crates, always a
    /// pair, a 2 x 2 block, or a heavy pair. Everything sits on the 50 px floor grid (RoomTraps.Tile; the core's corner is on it): walls
    /// one square thick with their edges on grid lines, crates one to a square on its middle.
    public enum CoreLayoutId : byte { Colonnade, Citadel, CircuitBoard, Bastions, Terraces, ShatteredPlaza, HedgeMaze, Reactor, Trenches, Pinball, None }

    public class CoreLayout
    {
        public CoreLayoutId Id;
        public string Name, Blurb;
        public bool Turned;     // false: Pieces are the top-left quarter, mirrored to all four; true: the left half, turned 180 degrees for the right
        public int[][] Pieces;  // in the core's own frame (0..2400 x 0..1800): {0, x, y, w, h} wall, {1, x, y, r} pillar, {2, x, y} crate, {3, x, y} heavy crate
    }

    public static class CoreLayouts
    {
        public const int W = 2400, H = 1800;  // the core: 4 x 3 cells
        public const int GateClear = 260;      // nothing within this of the core's middle (the boss gate and the boss fight's floor)
        public const int LaneHalf = 110, LaneDeep = 240;  // each door spot's lane: this wide either side, this deep into the room

        static int[] P(params int[] v) => v;

        public static readonly CoreLayout[] All =
        {
            new CoreLayout { Id = CoreLayoutId.Colonnade, Name = "Colonnade", Turned = false, Blurb = "A nave of pillar rows leads from every door to the gate. Short screens and crate stacks make side aisles to flank down.",
                Pieces = new[] { P(0, 400, 550, 50, 200), P(0, 300, 450, 150, 50), P(0, 1150, 150, 50, 50), P(0, 550, 300, 50, 50), P(0, 700, 300, 50, 50), P(0, 1100, 300, 50, 50), P(0, 150, 600, 50, 50), P(0, 600, 500, 50, 50), P(0, 800, 500, 50, 50), P(0, 1000, 500, 50, 50), P(0, 600, 700, 50, 50), P(2, 825, 725), P(2, 875, 725), P(3, 775, 675), P(3, 825, 675), P(2, 325, 725), P(2, 375, 725), P(2, 75, 125), P(2, 125, 125), P(3, 1075, 225), P(3, 1125, 225) } },
            new CoreLayout { Id = CoreLayoutId.Citadel, Name = "Citadel", Turned = false, Blurb = "Two broken rings of wall round the gate. The outer ring opens at the middle doors; the inner one only at the corners, so the fight spirals in.",
                Pieces = new[] { P(0, 450, 350, 600, 50), P(0, 450, 350, 50, 300), P(0, 750, 550, 200, 50), P(0, 750, 550, 50, 200), P(0, 300, 550, 150, 50), P(0, 400, 300, 100, 100), P(0, 750, 550, 50, 50), P(0, 600, 150, 50, 50), P(0, 1100, 200, 50, 50), P(3, 75, 125), P(3, 125, 125), P(2, 125, 575), P(2, 175, 575), P(2, 575, 225), P(2, 625, 225), P(2, 575, 275), P(2, 625, 275), P(3, 1075, 125), P(3, 1125, 125), P(2, 575, 475), P(2, 625, 475), P(2, 575, 525), P(2, 625, 525), P(2, 875, 475), P(2, 925, 475) } },
            new CoreLayout { Id = CoreLayoutId.CircuitBoard, Name = "Circuit Board", Turned = false, Blurb = "Thin traces wired between pads, a chip block on each side of the top middle. Lots of long sight lines broken at right angles.",
                Pieces = new[] { P(0, 300, 300, 400, 50), P(0, 650, 300, 50, 200), P(0, 700, 500, 250, 50), P(0, 100, 550, 50, 200), P(0, 150, 550, 250, 50), P(0, 500, 700, 50, 200), P(0, 500, 700, 250, 50), P(0, 1050, 200, 100, 100), P(0, 550, 150, 50, 50), P(0, 600, 150, 50, 50), P(2, 325, 375), P(2, 375, 375), P(2, 925, 575), P(2, 875, 575), P(2, 425, 525), P(2, 475, 525), P(2, 175, 725), P(2, 225, 725), P(2, 675, 675), P(2, 775, 725), P(2, 875, 375), P(2, 925, 375), P(2, 875, 425), P(2, 925, 425), P(3, 275, 475), P(3, 325, 475), P(2, 825, 825), P(2, 875, 825) } },
            new CoreLayout { Id = CoreLayoutId.Bastions, Name = "Bastions", Turned = false, Blurb = "A walled fort in each quarter, open toward the gate, with crates inside to hold. Flank walls along the edges stop easy wraps round them.",
                Pieces = new[] { P(0, 450, 350, 300, 50), P(0, 450, 400, 50, 200), P(0, 450, 600, 200, 50), P(0, 750, 400, 50, 100), P(0, 500, 150, 200, 50), P(0, 1050, 250, 50, 200), P(0, 100, 500, 50, 200), P(0, 450, 350, 50, 50), P(2, 725, 625), P(2, 775, 625), P(3, 575, 475), P(3, 525, 425), P(2, 275, 675), P(2, 325, 675), P(2, 275, 725), P(2, 325, 725), P(2, 825, 725), P(2, 875, 725), P(3, 925, 325), P(3, 975, 325), P(2, 1075, 575), P(2, 1125, 575), P(2, 875, 475), P(2, 925, 475), P(2, 325, 325), P(2, 375, 325) } },
            new CoreLayout { Id = CoreLayoutId.Terraces, Name = "Terraces", Turned = false, Blurb = "Stepped walls run like stairs down toward the gate, one big flight and one small per quarter. Every step is a corner to peek from.",
                Pieces = new[] { P(0, 300, 300, 150, 50), P(0, 400, 300, 50, 100), P(0, 450, 400, 150, 50), P(0, 550, 400, 50, 100), P(0, 550, 500, 200, 50), P(0, 700, 500, 50, 100), P(0, 700, 600, 150, 50), P(0, 550, 150, 200, 50), P(0, 700, 150, 50, 100), P(0, 750, 250, 200, 50), P(0, 900, 250, 50, 100), P(0, 900, 350, 150, 50), P(0, 150, 500, 100, 50), P(0, 150, 500, 50, 150), P(2, 825, 725), P(2, 875, 725), P(2, 975, 525), P(2, 1025, 525), P(2, 325, 425), P(2, 375, 425), P(2, 575, 575), P(2, 625, 575), P(3, 1075, 125), P(3, 1125, 125), P(2, 425, 825), P(2, 475, 825), P(2, 425, 875), P(2, 475, 875) } },
            new CoreLayout { Id = CoreLayoutId.ShatteredPlaza, Name = "Shattered Plaza", Turned = true, Blurb = "Broken shards of wall in stepped diagonals and fallen pillar rubble. Turned 180° instead of mirrored, so each side reads differently.",
                Pieces = new[] { P(0, 400, 500, 100, 50), P(0, 450, 550, 100, 50), P(0, 500, 600, 100, 50), P(0, 600, 600, 50, 50), P(0, 700, 250, 100, 50), P(0, 800, 300, 50, 150), P(0, 300, 1100, 50, 200), P(0, 350, 1250, 150, 50), P(0, 750, 1300, 200, 50), P(0, 900, 1150, 50, 200), P(0, 550, 850, 100, 100), P(0, 1000, 600, 50, 50), P(0, 1050, 1450, 50, 50), P(2, 675, 925), P(2, 725, 925), P(2, 325, 1475), P(2, 375, 1475), P(3, 525, 325), P(3, 575, 325), P(2, 825, 825), P(2, 875, 825), P(2, 675, 1475), P(2, 725, 1475), P(2, 125, 1175), P(2, 175, 1175), P(3, 1075, 1525), P(3, 1125, 1525), P(2, 1125, 325), P(2, 1175, 325), P(2, 375, 825), P(2, 425, 825), P(2, 375, 875), P(2, 425, 875) } },
            new CoreLayout { Id = CoreLayoutId.HedgeMaze, Name = "Hedge Maze", Turned = false, Blurb = "A maze ring round an open middle. Crates sit in the dead ends; the gate floor stays clear for bosses.",
                Pieces = new[] { P(0, 300, 250, 300, 50), P(0, 550, 300, 50, 200), P(0, 300, 300, 50, 100), P(0, 400, 400, 50, 200), P(0, 400, 550, 300, 50), P(0, 650, 450, 50, 150), P(0, 700, 300, 200, 50), P(0, 900, 300, 50, 150), P(0, 1050, 150, 50, 250), P(0, 1050, 400, 100, 50), P(0, 150, 500, 50, 250), P(0, 150, 500, 150, 50), P(0, 300, 700, 200, 50), P(0, 300, 700, 50, 100), P(0, 750, 600, 50, 200), P(0, 600, 750, 200, 50), P(2, 925, 575), P(2, 975, 575), P(2, 375, 375), P(2, 425, 375), P(2, 475, 675), P(2, 525, 675), P(3, 825, 375), P(3, 875, 375), P(2, 1125, 275), P(2, 1175, 275) } },
            new CoreLayout { Id = CoreLayoutId.Reactor, Name = "Reactor", Turned = false, Blurb = "A ring of pillars round the gate like a reactor core, spokes out to the walls and a big coolant tower in each quarter.",
                Pieces = new[] { P(0, 750, 500, 100, 50), P(0, 750, 500, 50, 100), P(0, 500, 900, 250, 50), P(0, 1200, 250, 50, 150), P(0, 250, 600, 50, 150), P(0, 600, 150, 150, 50), P(0, 850, 750, 50, 50), P(0, 950, 650, 50, 50), P(0, 1050, 550, 50, 50), P(0, 450, 250, 100, 100), P(2, 675, 425), P(2, 725, 425), P(2, 675, 475), P(2, 725, 475), P(3, 1075, 225), P(3, 1125, 225), P(2, 325, 825), P(2, 375, 825), P(3, 575, 625), P(3, 625, 625) } },
            new CoreLayout { Id = CoreLayoutId.Trenches, Name = "Trenches", Turned = true, Blurb = "Long trench walls cross the room in staggered rows with gaps to push through. Crate lines sit behind each trench.",
                Pieces = new[] { P(0, 250, 350, 500, 50), P(0, 850, 350, 350, 50), P(0, 100, 650, 350, 50), P(0, 550, 650, 400, 50), P(0, 300, 1150, 400, 50), P(0, 800, 1150, 150, 50), P(0, 250, 1400, 500, 50), P(0, 850, 1400, 350, 50), P(0, 500, 900, 50, 50), P(0, 700, 900, 50, 50), P(2, 425, 475), P(2, 475, 475), P(2, 425, 525), P(2, 475, 525), P(3, 625, 725), P(3, 675, 725), P(2, 375, 1075), P(2, 425, 1075), P(2, 375, 1125), P(2, 425, 1125), P(2, 875, 1375), P(2, 925, 1375), P(2, 1075, 1475), P(2, 1125, 1475), P(2, 775, 525), P(2, 825, 525), P(2, 575, 1275), P(2, 625, 1275) } },
            new CoreLayout { Id = CoreLayoutId.Pinball, Name = "Pinball", Turned = false, Blurb = "Bumper pillars of every size and short deflector walls. Hard to line up shots, easy to dodge round: good for swarm waves.",
                Pieces = new[] { P(0, 300, 750, 200, 50), P(0, 500, 650, 50, 150), P(0, 500, 100, 200, 50), P(0, 1050, 100, 50, 150), P(0, 100, 500, 50, 250), P(0, 500, 350, 100, 100), P(0, 750, 300, 50, 50), P(0, 350, 550, 50, 50), P(0, 850, 450, 100, 100), P(0, 1050, 400, 50, 50), P(3, 675, 725), P(3, 725, 725), P(2, 325, 325), P(2, 375, 325), P(2, 675, 575), P(2, 725, 575), P(2, 625, 525), P(2, 675, 525), P(2, 825, 725), P(2, 875, 725) } },
        };

        public static CoreLayout Get(CoreLayoutId id) => id < CoreLayoutId.None ? All[(int)id] : null;

        public static string Name(CoreLayoutId id) => id < CoreLayoutId.None ? All[(int)id].Name : "";

        /// Every piece of the layout over the whole core, in the core's frame (the quarter or half mirrored or turned, no doubles).
        public static List<int[]> Expanded(CoreLayoutId id)
        {
            var l = Get(id);
            var list = new List<int[]>();
            if (l == null)
                return list;
            var seen = new HashSet<string>();
            Action<int[]> add = p =>
            {
                if (seen.Add(string.Join(",", p)))
                    list.Add(p);
            };
            foreach (var p in l.Pieces)
            {
                add(p);
                if (l.Turned)
                    add(Flip(Flip(p, true), false));
                else
                {
                    add(Flip(p, true));
                    add(Flip(p, false));
                    add(Flip(Flip(p, true), false));
                }
            }
            return list;
        }

        static int[] Flip(int[] p, bool acrossX)
        {
            var q = (int[])p.Clone();
            if (acrossX)
                q[1] = p[0] == 0 ? W - p[1] - p[3] : W - p[1];
            else
                q[2] = p[0] == 0 ? H - p[2] - p[4] : H - p[2];
            return q;
        }

        /// The layout's walls and pillars in the world (the core's top-left corner is the lattice origin, GridRooms.cs).
        public static void AddSolids(List<Solid> to, CoreLayoutId id)
        {
            const int ox = Expansion.GridX0, oy = Expansion.GridY0;
            foreach (var p in Expanded(id))
                if (p[0] == 0)
                    to.Add(World.Box(ox + p[1], oy + p[2], ox + p[1] + p[3], oy + p[2] + p[4]));
                else if (p[0] == 1)
                    to.Add(World.Pillar(ox + p[1], oy + p[2], p[3]));
        }

        /// The layout's crates in the world (they go down as props with each boss cycle's first wave, LayoutRules.cs).
        public static LayoutCrate[] Crates(CoreLayoutId id)
        {
            var list = new List<LayoutCrate>();
            foreach (var p in Expanded(id))
                if (p[0] >= 2)
                    list.Add(new LayoutCrate { Pos = new V2(Expansion.GridX0 + p[1], Expansion.GridY0 + p[2]), Heavy = p[0] == 3 });
            return list.ToArray();
        }

        /// Host: the next layout, one not used yet this run (Used: a bit per layout; all ten used, they all come back).
        public static CoreLayoutId Pick(Random rng, ref int used, CoreLayoutId last)
        {
            if ((used & ((1 << All.Length) - 1)) == (1 << All.Length) - 1)
                used = 0;
            var pool = new List<CoreLayoutId>();
            for (int i = 0; i < All.Length; i++)
                if ((used & (1 << i)) == 0 && (CoreLayoutId)i != last)
                    pool.Add((CoreLayoutId)i);
            if (pool.Count == 0)
                pool.Add((CoreLayoutId)((((int)last) + 1) % All.Length));
            var id = pool[rng.Next(pool.Count)];
            used |= 1 << (int)id;
            return id;
        }
    }

    public partial class Game
    {
        /// The core's floor plan (CoreLayouts.cs): one per run, a new one at each Campaign map reset (a new faction's map).
        public CoreLayoutId CoreLayout = CoreLayoutId.None;
        public int CoreLayoutsUsed;  // host: a bit per layout picked this run, so the Campaign's ten maps see all ten

        /// Host: the core gets its next floor plan; its crates go down with the next regular wave.
        void PickCoreLayout()
        {
            CoreLayout = CoreLayouts.Pick(sideRng, ref CoreLayoutsUsed, CoreLayout);
            expWorld = null;
            layoutFresh = true;
            DoLog("core layout: " + CoreLayouts.Name(CoreLayout));
        }

        /// Client: the host's plan from Msg.ExpandState.
        void ReadCoreLayout(byte b)
        {
            var id = b < (byte)CoreLayoutId.None ? (CoreLayoutId)b : CoreLayoutId.None;
            if (id == CoreLayout)
                return;
            CoreLayout = id;
            expWorld = null;
            LayoutAge = 0f;
        }
    }
}
