using System;
using System.Collections.Generic;

namespace Arena
{
    /// The core's floor plans (Docs/Mockups/core-layouts.html): ten hand-made layouts of walls, pillars and crates for the
    /// Grid run's 4 x 3 cell core. Each keeps the 14 door spots and the boss gate's floor in the middle clear.
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
                Pieces = new[] { P(1, 560, 330, 30), P(1, 700, 330, 30), P(1, 1100, 330, 30), P(1, 150, 600, 30), P(1, 600, 520, 34), P(1, 820, 520, 34), P(1, 1040, 520, 34), P(1, 600, 700, 30), P(1, 930, 760, 26), P(0, 420, 560, 24, 200), P(0, 300, 440, 140, 24), P(0, 1150, 150, 50, 24), P(2, 760, 620), P(2, 800, 660), P(3, 760, 660), P(2, 300, 760), P(2, 140, 140), P(3, 1080, 200) } },
            new CoreLayout { Id = CoreLayoutId.Citadel, Name = "Citadel", Turned = false, Blurb = "Two broken rings of wall round the gate. The outer ring opens at the middle doors; the inner one only at the corners, so the fight spirals in.",
                Pieces = new[] { P(0, 440, 340, 600, 24), P(0, 440, 340, 24, 300), P(0, 740, 570, 200, 24), P(0, 740, 570, 24, 160), P(1, 452, 352, 44), P(1, 752, 582, 30), P(0, 300, 560, 140, 24), P(1, 600, 150, 30), P(1, 1110, 240, 30), P(2, 110, 110), P(3, 150, 140), P(2, 150, 560), P(2, 600, 180), P(2, 640, 200), P(3, 1100, 150), P(2, 580, 460), P(2, 620, 470), P(2, 880, 470) } },
            new CoreLayout { Id = CoreLayoutId.CircuitBoard, Name = "Circuit Board", Turned = false, Blurb = "Thin traces wired between pads, a chip block on each side of the top middle. Lots of long sight lines broken at right angles.",
                Pieces = new[] { P(0, 300, 300, 400, 16), P(0, 684, 300, 16, 220), P(0, 684, 504, 260, 16), P(1, 300, 308, 22), P(1, 944, 512, 22), P(0, 130, 560, 16, 200), P(0, 130, 560, 290, 16), P(1, 420, 568, 22), P(1, 138, 760, 22), P(0, 500, 700, 16, 200), P(0, 500, 700, 240, 16), P(1, 740, 708, 22), P(0, 1040, 180, 120, 90), P(1, 560, 160, 34), P(1, 620, 160, 34), P(2, 860, 380), P(2, 900, 410), P(3, 300, 450), P(2, 820, 840) } },
            new CoreLayout { Id = CoreLayoutId.Bastions, Name = "Bastions", Turned = false, Blurb = "A walled fort in each quarter, open toward the gate, with crates inside to hold. Flank walls along the edges stop easy wraps round them.",
                Pieces = new[] { P(0, 470, 380, 300, 24), P(0, 470, 380, 24, 240), P(0, 470, 596, 160, 24), P(0, 746, 380, 24, 120), P(1, 470, 380, 30), P(1, 770, 620, 22), P(2, 540, 450), P(3, 580, 480), P(2, 540, 520), P(0, 520, 150, 180, 24), P(0, 1080, 260, 24, 200), P(0, 130, 520, 24, 180), P(2, 300, 700), P(2, 340, 720), P(2, 900, 700), P(3, 950, 320), P(2, 1120, 620), P(1, 900, 480, 26), P(1, 300, 300, 26) } },
            new CoreLayout { Id = CoreLayoutId.Terraces, Name = "Terraces", Turned = false, Blurb = "Stepped walls run like stairs down toward the gate, one big flight and one small per quarter. Every step is a corner to peek from.",
                Pieces = new[] { P(0, 300, 300, 160, 24), P(0, 436, 300, 24, 120), P(0, 436, 396, 160, 24), P(0, 572, 396, 24, 120), P(0, 572, 492, 160, 24), P(0, 708, 492, 24, 120), P(0, 708, 588, 140, 24), P(0, 560, 140, 200, 24), P(0, 736, 140, 24, 130), P(0, 736, 250, 200, 24), P(0, 912, 250, 24, 120), P(0, 912, 346, 160, 24), P(0, 140, 500, 120, 24), P(0, 140, 500, 24, 160), P(1, 820, 720, 22), P(1, 1000, 520, 26), P(2, 380, 420), P(2, 660, 560), P(3, 1100, 150), P(2, 420, 820), P(2, 460, 840) } },
            new CoreLayout { Id = CoreLayoutId.ShatteredPlaza, Name = "Shattered Plaza", Turned = true, Blurb = "Broken shards of wall in stepped diagonals and fallen pillar rubble. Turned 180° instead of mirrored, so each side reads differently.",
                Pieces = new[] { P(0, 400, 500, 80, 40), P(0, 460, 540, 80, 40), P(0, 520, 580, 80, 40), P(0, 580, 620, 80, 40), P(0, 700, 250, 120, 40), P(0, 780, 290, 40, 140), P(0, 300, 1100, 40, 200), P(0, 340, 1260, 160, 40), P(0, 760, 1300, 200, 30), P(0, 930, 1150, 30, 180), P(1, 600, 900, 40), P(1, 640, 940, 18), P(1, 1000, 600, 30), P(1, 300, 1500, 26), P(1, 1050, 1450, 34), P(2, 500, 300), P(3, 540, 330), P(2, 860, 820), P(2, 700, 1500), P(2, 150, 1200), P(3, 1100, 1550), P(2, 1150, 300), P(2, 380, 800), P(2, 420, 830) } },
            new CoreLayout { Id = CoreLayoutId.HedgeMaze, Name = "Hedge Maze", Turned = false, Blurb = "A maze ring round an open middle. Crates sit in the dead ends; the gate floor stays clear for bosses.",
                Pieces = new[] { P(0, 300, 280, 300, 20), P(0, 580, 280, 20, 200), P(0, 300, 280, 20, 140), P(0, 420, 400, 20, 200), P(0, 420, 580, 280, 20), P(0, 680, 460, 20, 140), P(0, 700, 300, 220, 20), P(0, 900, 300, 20, 160), P(0, 1040, 160, 20, 240), P(0, 1040, 400, 100, 20), P(0, 160, 520, 20, 220), P(0, 160, 520, 140, 20), P(0, 300, 700, 200, 20), P(0, 300, 700, 20, 120), P(0, 780, 600, 20, 180), P(0, 600, 760, 180, 20), P(1, 960, 560, 20), P(2, 360, 340), P(2, 480, 640), P(3, 860, 360), P(2, 1100, 250) } },
            new CoreLayout { Id = CoreLayoutId.Reactor, Name = "Reactor", Turned = false, Blurb = "A ring of pillars round the gate like a reactor core, spokes out to the walls and a big coolant tower in each quarter.",
                Pieces = new[] { P(1, 899, 791, 28), P(1, 974, 674, 28), P(1, 1091, 599, 28), P(0, 760, 520, 100, 24), P(0, 760, 520, 24, 100), P(0, 520, 888, 240, 24), P(0, 1188, 240, 24, 180), P(1, 480, 320, 56), P(0, 260, 600, 24, 140), P(0, 600, 160, 140, 24), P(2, 680, 420), P(2, 720, 440), P(3, 1080, 200), P(2, 340, 840), P(2, 600, 640), P(3, 640, 600) } },
            new CoreLayout { Id = CoreLayoutId.Trenches, Name = "Trenches", Turned = true, Blurb = "Long trench walls cross the room in staggered rows with gaps to push through. Crate lines sit behind each trench.",
                Pieces = new[] { P(0, 260, 380, 500, 24), P(0, 860, 380, 340, 24), P(0, 120, 640, 340, 24), P(0, 560, 640, 400, 24), P(0, 300, 1160, 380, 24), P(0, 780, 1160, 180, 24), P(0, 260, 1420, 500, 24), P(0, 860, 1420, 340, 24), P(2, 400, 420), P(2, 440, 420), P(2, 620, 680), P(3, 700, 680), P(2, 380, 1120), P(2, 420, 1120), P(2, 900, 1380), P(2, 1100, 1460), P(1, 810, 520, 26), P(1, 510, 900, 30), P(1, 700, 900, 30), P(1, 600, 1290, 26) } },
            new CoreLayout { Id = CoreLayoutId.Pinball, Name = "Pinball", Turned = false, Blurb = "Bumper pillars of every size and short deflector walls. Hard to line up shots, easy to dodge round: good for swarm waves.",
                Pieces = new[] { P(1, 560, 420, 50), P(1, 760, 300, 34), P(1, 380, 560, 34), P(1, 900, 520, 40), P(1, 1080, 400, 34), P(1, 680, 780, 24), P(1, 260, 300, 18), P(0, 300, 760, 220, 24), P(0, 500, 640, 24, 140), P(0, 480, 120, 240, 24), P(0, 1060, 110, 24, 160), P(0, 120, 500, 24, 240), P(2, 700, 560), P(2, 740, 600), P(3, 720, 700), P(2, 900, 760) } },
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
