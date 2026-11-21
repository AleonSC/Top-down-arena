using System;
using System.Collections.Generic;

namespace Arena
{
    /// The core's floor plans (Docs/Mockups/core-layouts.html): ten hand-made layouts for the
    /// Grid run's 4 x 3 cell core. Each keeps the 14 door spots and the boss gate's floor in the middle clear.
    /// Walls are the only hard obstacles (a pillar is a square wall block, 64 or 96 px); every other obstacle is crates, always a
    /// pair, a 2 x 2 block, or a heavy pair, on a 22 px grid.
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
                Pieces = new[] { P(0, 420, 560, 24, 200), P(0, 300, 440, 140, 24), P(0, 1150, 150, 50, 24), P(0, 528, 298, 64, 64), P(0, 668, 298, 64, 64), P(0, 1068, 298, 64, 64), P(0, 118, 568, 64, 64), P(0, 568, 488, 64, 64), P(0, 788, 488, 64, 64), P(0, 1008, 488, 64, 64), P(0, 568, 668, 64, 64), P(2, 814, 748), P(2, 858, 748), P(3, 770, 660), P(3, 814, 660), P(2, 330, 748), P(2, 374, 748), P(2, 66, 132), P(2, 110, 132), P(3, 1078, 220), P(3, 1122, 220) } },
            new CoreLayout { Id = CoreLayoutId.Citadel, Name = "Citadel", Turned = false, Blurb = "Two broken rings of wall round the gate. The outer ring opens at the middle doors; the inner one only at the corners, so the fight spirals in.",
                Pieces = new[] { P(0, 440, 340, 600, 24), P(0, 440, 340, 24, 300), P(0, 740, 570, 200, 24), P(0, 740, 570, 24, 160), P(0, 300, 560, 140, 24), P(0, 404, 304, 96, 96), P(0, 720, 550, 64, 64), P(0, 568, 118, 64, 64), P(0, 1078, 208, 64, 64), P(3, 66, 132), P(3, 110, 132), P(2, 110, 572), P(2, 154, 572), P(2, 594, 242), P(2, 638, 242), P(2, 594, 286), P(2, 638, 286), P(3, 1078, 132), P(3, 1122, 132), P(2, 594, 462), P(2, 638, 462), P(2, 594, 506), P(2, 638, 506), P(2, 858, 484), P(2, 902, 484) } },
            new CoreLayout { Id = CoreLayoutId.CircuitBoard, Name = "Circuit Board", Turned = false, Blurb = "Thin traces wired between pads, a chip block on each side of the top middle. Lots of long sight lines broken at right angles.",
                Pieces = new[] { P(0, 300, 300, 400, 16), P(0, 684, 300, 16, 220), P(0, 684, 504, 260, 16), P(0, 130, 560, 16, 200), P(0, 130, 560, 290, 16), P(0, 500, 700, 16, 200), P(0, 500, 700, 240, 16), P(0, 1040, 180, 120, 90), P(0, 528, 128, 64, 64), P(0, 588, 128, 64, 64), P(2, 330, 352), P(2, 374, 352), P(2, 902, 572), P(2, 946, 572), P(2, 418, 528), P(2, 462, 528), P(2, 198, 704), P(2, 242, 704), P(2, 726, 748), P(2, 770, 748), P(2, 858, 374), P(2, 902, 374), P(2, 858, 418), P(2, 902, 418), P(3, 286, 484), P(3, 330, 484), P(2, 814, 836), P(2, 858, 836) } },
            new CoreLayout { Id = CoreLayoutId.Bastions, Name = "Bastions", Turned = false, Blurb = "A walled fort in each quarter, open toward the gate, with crates inside to hold. Flank walls along the edges stop easy wraps round them.",
                Pieces = new[] { P(0, 470, 380, 300, 24), P(0, 470, 380, 24, 240), P(0, 470, 596, 160, 24), P(0, 746, 380, 24, 120), P(0, 520, 150, 180, 24), P(0, 1080, 260, 24, 200), P(0, 130, 520, 24, 180), P(0, 438, 348, 64, 64), P(2, 726, 616), P(2, 770, 616), P(3, 550, 484), P(3, 594, 484), P(2, 286, 682), P(2, 330, 682), P(2, 286, 726), P(2, 330, 726), P(2, 814, 704), P(2, 858, 704), P(3, 946, 308), P(3, 990, 308), P(2, 1078, 572), P(2, 1122, 572), P(2, 858, 484), P(2, 902, 484), P(2, 330, 308), P(2, 374, 308) } },
            new CoreLayout { Id = CoreLayoutId.Terraces, Name = "Terraces", Turned = false, Blurb = "Stepped walls run like stairs down toward the gate, one big flight and one small per quarter. Every step is a corner to peek from.",
                Pieces = new[] { P(0, 300, 300, 160, 24), P(0, 436, 300, 24, 120), P(0, 436, 396, 160, 24), P(0, 572, 396, 24, 120), P(0, 572, 492, 160, 24), P(0, 708, 492, 24, 120), P(0, 708, 588, 140, 24), P(0, 560, 140, 200, 24), P(0, 736, 140, 24, 130), P(0, 736, 250, 200, 24), P(0, 912, 250, 24, 120), P(0, 912, 346, 160, 24), P(0, 140, 500, 120, 24), P(0, 140, 500, 24, 160), P(2, 814, 704), P(2, 858, 704), P(2, 990, 528), P(2, 1034, 528), P(2, 330, 440), P(2, 374, 440), P(2, 594, 572), P(2, 638, 572), P(3, 1078, 132), P(3, 1122, 132), P(2, 418, 814), P(2, 462, 814), P(2, 418, 858), P(2, 462, 858) } },
            new CoreLayout { Id = CoreLayoutId.ShatteredPlaza, Name = "Shattered Plaza", Turned = true, Blurb = "Broken shards of wall in stepped diagonals and fallen pillar rubble. Turned 180° instead of mirrored, so each side reads differently.",
                Pieces = new[] { P(0, 400, 500, 80, 40), P(0, 460, 540, 80, 40), P(0, 520, 580, 80, 40), P(0, 580, 620, 80, 40), P(0, 700, 250, 120, 40), P(0, 780, 290, 40, 140), P(0, 300, 1100, 40, 200), P(0, 340, 1260, 160, 40), P(0, 760, 1300, 200, 30), P(0, 930, 1150, 30, 180), P(0, 552, 852, 96, 96), P(0, 968, 568, 64, 64), P(0, 1018, 1418, 64, 64), P(2, 682, 924), P(2, 726, 924), P(2, 330, 1496), P(2, 374, 1496), P(3, 506, 308), P(3, 550, 308), P(2, 814, 836), P(2, 858, 836), P(2, 682, 1496), P(2, 726, 1496), P(2, 110, 1188), P(2, 154, 1188), P(3, 1078, 1540), P(3, 1122, 1540), P(2, 1122, 308), P(2, 1166, 308), P(2, 374, 814), P(2, 418, 814), P(2, 374, 858), P(2, 418, 858) } },
            new CoreLayout { Id = CoreLayoutId.HedgeMaze, Name = "Hedge Maze", Turned = false, Blurb = "A maze ring round an open middle. Crates sit in the dead ends; the gate floor stays clear for bosses.",
                Pieces = new[] { P(0, 300, 280, 300, 20), P(0, 580, 280, 20, 200), P(0, 300, 280, 20, 140), P(0, 420, 400, 20, 200), P(0, 420, 580, 280, 20), P(0, 680, 460, 20, 140), P(0, 700, 300, 220, 20), P(0, 900, 300, 20, 160), P(0, 1040, 160, 20, 240), P(0, 1040, 400, 100, 20), P(0, 160, 520, 20, 220), P(0, 160, 520, 140, 20), P(0, 300, 700, 200, 20), P(0, 300, 700, 20, 120), P(0, 780, 600, 20, 180), P(0, 600, 760, 180, 20), P(2, 946, 572), P(2, 990, 572), P(2, 374, 352), P(2, 418, 352), P(2, 462, 660), P(2, 506, 660), P(3, 814, 352), P(3, 858, 352), P(2, 1122, 264), P(2, 1166, 264) } },
            new CoreLayout { Id = CoreLayoutId.Reactor, Name = "Reactor", Turned = false, Blurb = "A ring of pillars round the gate like a reactor core, spokes out to the walls and a big coolant tower in each quarter.",
                Pieces = new[] { P(0, 760, 520, 100, 24), P(0, 760, 520, 24, 100), P(0, 520, 888, 240, 24), P(0, 1188, 240, 24, 180), P(0, 260, 600, 24, 140), P(0, 600, 160, 140, 24), P(0, 867, 759, 64, 64), P(0, 942, 642, 64, 64), P(0, 1059, 567, 64, 64), P(0, 432, 272, 96, 96), P(2, 682, 418), P(2, 726, 418), P(2, 682, 462), P(2, 726, 462), P(3, 1078, 220), P(3, 1122, 220), P(2, 330, 836), P(2, 374, 836), P(3, 594, 616), P(3, 638, 616) } },
            new CoreLayout { Id = CoreLayoutId.Trenches, Name = "Trenches", Turned = true, Blurb = "Long trench walls cross the room in staggered rows with gaps to push through. Crate lines sit behind each trench.",
                Pieces = new[] { P(0, 260, 380, 500, 24), P(0, 860, 380, 340, 24), P(0, 120, 640, 340, 24), P(0, 560, 640, 400, 24), P(0, 300, 1160, 380, 24), P(0, 780, 1160, 180, 24), P(0, 260, 1420, 500, 24), P(0, 860, 1420, 340, 24), P(0, 478, 868, 64, 64), P(0, 668, 868, 64, 64), P(2, 418, 462), P(2, 462, 462), P(2, 418, 506), P(2, 462, 506), P(3, 638, 704), P(3, 682, 704), P(2, 374, 1078), P(2, 418, 1078), P(2, 374, 1122), P(2, 418, 1122), P(2, 858, 1364), P(2, 902, 1364), P(2, 1078, 1496), P(2, 1122, 1496), P(2, 770, 528), P(2, 814, 528), P(2, 594, 1276), P(2, 638, 1276) } },
            new CoreLayout { Id = CoreLayoutId.Pinball, Name = "Pinball", Turned = false, Blurb = "Bumper pillars of every size and short deflector walls. Hard to line up shots, easy to dodge round: good for swarm waves.",
                Pieces = new[] { P(0, 300, 760, 220, 24), P(0, 500, 640, 24, 140), P(0, 480, 120, 240, 24), P(0, 1060, 110, 24, 160), P(0, 120, 500, 24, 240), P(0, 512, 372, 96, 96), P(0, 728, 268, 64, 64), P(0, 348, 528, 64, 64), P(0, 852, 472, 96, 96), P(0, 1048, 368, 64, 64), P(3, 682, 748), P(3, 726, 748), P(2, 330, 308), P(2, 374, 308), P(2, 682, 550), P(2, 726, 550), P(2, 682, 594), P(2, 726, 594), P(2, 814, 748), P(2, 858, 748) } },
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
