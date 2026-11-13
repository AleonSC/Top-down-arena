using System;
using System.Collections.Generic;

namespace Arena
{
    /// What a node of a hunt map is (Docs/HuntMap.md). Only Skirmish and Elite are built so far: the others are listed
    /// so the numbers don't change later; the generator makes Skirmish, Elite, Rest, Shop and the Boss so far (Ambush, Contraband and Gamble come later).
    public enum HuntNodeType { Skirmish, Elite, Ambush, Contraband, Rest, Shop, Gamble, Boss, None }

    /// One node of the map: a square on one of three lanes, in one row (step).
    public class HuntNode
    {
        public int Step;           // the row, 0 (the first fight) to HuntMap.Steps - 1 (the boss)
        public int Index;          // its place in the row, left to right
        public int Lane;           // 0 left, 1 middle, 2 right
        public HuntNodeType Type;
        public int[] Next = new int[0];   // indexes into the next row: where you can go from here (empty for the boss)

        public bool Fight => Type != HuntNodeType.Rest && Type != HuntNodeType.Shop;
    }

    /// The map of a Boss Hunt (Docs/HuntMap.md): ten rows, the last is the boss, two or three nodes a row on three lanes,
    /// made from a seed (the host rolls it; everyone gets the same map) plus how far you have got. No Unity in here.
    ///
    /// Links between two rows are laid out so the drawn map stays readable: every node links straight down its lane
    /// when the next row has a node there, plus one shift per row gap (all the same way in that gap, right in even gaps and
    /// left in odd ones), so two links never cross; a node left without a link in or out gets one to the nearest lane
    /// that doesn't cross another; a node with a single way on gets a second one where that crosses nothing.
    public class HuntMap
    {
        public const int Steps = Hunts.Length;     // rows, the boss's included
        public const int Lanes = 3;
        public const int MaxClues = 3;             // elites cleared that count towards the boss (a clue each)

        public readonly int Seed;
        public readonly HuntNode[][] Rows = new HuntNode[Steps][];

        // How far the party has got: the index (in its row) of the node cleared in each row so far.
        readonly List<int> path = new List<int>();
        int current = -1;                          // the node being fought (an index in the row after the cleared ones), -1: none picked

        HuntMap(int seed) { Seed = seed; }

        // ---------- making the map ----------

        public static HuntMap Generate(int seed)
        {
            var m = new HuntMap(seed);
            var rng = new Rng(seed);
            for (int step = 0; step < Steps; step++)
            {
                int[] lanes = RowLanes(step, rng);
                m.Rows[step] = new HuntNode[lanes.Length];
                for (int i = 0; i < lanes.Length; i++)
                    m.Rows[step][i] = new HuntNode { Step = step, Index = i, Lane = lanes[i] };
            }
            for (int step = 0; step < Steps - 1; step++)
                Link(m.Rows[step], m.Rows[step + 1], step);
            AssignTypes(m, rng);
            return m;
        }

        /// The lanes the nodes of a row sit on: the first and last rows are one node in the middle, the row before the
        /// boss is the two outer lanes, any other row three nodes (60%) or two.
        static int[] RowLanes(int step, Rng rng)
        {
            if (step == 0 || step == Steps - 1)
                return new[] { 1 };
            if (step == Steps - 2)
                return new[] { 0, 2 };
            if (rng.Next(100) < 60)
                return new[] { 0, 1, 2 };
            switch (rng.Next(3))
            {
                case 0: return new[] { 0, 1 };
                case 1: return new[] { 1, 2 };
                default: return new[] { 0, 2 };
            }
        }

        static void Link(HuntNode[] from, HuntNode[] to, int gap)
        {
            int dir = gap % 2 == 0 ? 1 : -1;
            var links = new List<int>[from.Length];
            var incoming = new bool[to.Length];
            for (int i = 0; i < from.Length; i++)
            {
                links[i] = new List<int>();
                for (int j = 0; j < to.Length; j++)
                {
                    int dl = to[j].Lane - from[i].Lane;
                    if (dl == 0 || dl == dir)
                    {
                        links[i].Add(j);
                        incoming[j] = true;
                    }
                }
            }
            for (int j = 0; j < to.Length; j++)   // a node nothing leads to: the nearest lane that doesn't make two links cross
                if (!incoming[j])
                    AddNearest(from, to, links, -1, j);
            for (int i = 0; i < from.Length; i++) // a node that leads nowhere
                if (links[i].Count == 0)
                    AddNearest(from, to, links, i, -1);
            for (int i = 0; i < from.Length; i++)  // a node with one way on gets a second where that crosses nothing (a choice is the point)
                if (links[i].Count < 2)
                    for (int j = 0; j < to.Length && links[i].Count < 2; j++)
                        if (!links[i].Contains(j) && Math.Abs(to[j].Lane - from[i].Lane) <= 1 && !Crosses(from, to, links, i, j))
                            links[i].Add(j);
            for (int i = 0; i < from.Length; i++)
            {
                links[i].Sort();
                from[i].Next = links[i].ToArray();
            }
        }

        /// Adds a link to a target (or from a source) with none, picking the nearest lane whose link crosses no other.
        static void AddNearest(HuntNode[] from, HuntNode[] to, List<int>[] links, int i0, int j0)
        {
            int bi = -1, bj = -1, best = 99;
            for (int i = 0; i < from.Length; i++)
                for (int j = 0; j < to.Length; j++)
                {
                    if ((i0 >= 0 && i != i0) || (j0 >= 0 && j != j0) || links[i].Contains(j))
                        continue;
                    int d = Math.Abs(from[i].Lane - to[j].Lane);
                    if (d < best && !Crosses(from, to, links, i, j))
                    {
                        best = d; bi = i; bj = j;
                    }
                }
            if (bi < 0)   // (never happens for the lane layouts above: a straight staircase always exists)
                for (int i = 0; i < from.Length && bi < 0; i++)
                    for (int j = 0; j < to.Length && bi < 0; j++)
                        if (!((i0 >= 0 && i != i0) || (j0 >= 0 && j != j0)) && !links[i].Contains(j))
                        {
                            bi = i; bj = j;
                        }
            if (bi >= 0)
                links[bi].Add(bj);
        }

        static bool Crosses(HuntNode[] from, HuntNode[] to, List<int>[] links, int i, int j)
        {
            for (int a = 0; a < from.Length; a++)
                foreach (int b in links[a])
                    if ((from[a].Lane - from[i].Lane) * (to[b].Lane - to[j].Lane) < 0)
                        return true;
            return false;
        }

        /// What each node is. The first row is a Skirmish, the boss row the boss, the row before the boss a Rest or a Shop (a
        /// breather before the boss), the middle row at least one Elite, no row is all Elites, and every other row has a
        /// Skirmish. A Rest or Shop can come in the rows from the third to the seventh (not the middle row, which has an Elite) (never first, never straight before the
        /// breather row, so a route never has two no-fight steps in a row).
        static void AssignTypes(HuntMap m, Rng rng)
        {
            bool prevBreather = false;   // (a Rest or Shop never sits in the row right after another, so no route has two in a row)
            for (int step = 0; step < Steps; step++)
            {
                var row = m.Rows[step];
                if (step == Steps - 1) { row[0].Type = HuntNodeType.Boss; continue; }
                if (step == Steps - 2)
                {
                    foreach (var n in row) n.Type = rng.Next(2) == 0 ? HuntNodeType.Rest : HuntNodeType.Shop;
                    continue;
                }
                bool breather = step >= 2 && step <= Steps - 4 && step != Steps / 2 - 1 && !prevBreather;
                for (int i = 0; i < row.Length; i++)
                {
                    int roll = step == 0 ? 99 : rng.Next(100);
                    row[i].Type = roll < 22 ? HuntNodeType.Elite
                        : breather && roll < 30 ? HuntNodeType.Rest
                        : breather && roll < 38 ? HuntNodeType.Shop
                        : HuntNodeType.Skirmish;
                }
                if (step == Steps / 2 - 1)
                {
                    bool any = false;
                    foreach (var n in row) any |= n.Type == HuntNodeType.Elite;
                    if (!any) row[rng.Next(row.Length)].Type = HuntNodeType.Elite;
                }
                bool skirmish = false;
                foreach (var n in row) skirmish |= n.Type == HuntNodeType.Skirmish;
                if (!skirmish) row[rng.Next(row.Length)].Type = HuntNodeType.Skirmish;
                prevBreather = false;
                foreach (var n in row) prevBreather |= !n.Fight;
            }
        }

        // ---------- progress ----------

        /// Rows cleared so far (0 at the start, Steps when the boss is down).
        public int Step => path.Count;

        public bool Complete => path.Count == Steps;

        /// Elite nodes cleared, up to MaxClues: each reveals something about the boss (Docs/HuntMap.md).
        public int Clues { get; private set; }

        /// The node being fought, or null before a pick.
        public HuntNode Current => current < 0 || Complete ? null : Rows[Step][current];

        /// Where you can go next: indexes in row `Step` (the first row has one node; after that, the cleared node's links).
        public int[] Choices()
        {
            if (Complete) return new int[0];
            if (Step == 0) return new[] { 0 };
            return Rows[Step - 1][path[Step - 1]].Next;
        }

        /// Picks the node to fight next (an index in row `Step`; it must be one of Choices()).
        public bool Pick(int index)
        {
            if (current >= 0 || Array.IndexOf(Choices(), index) < 0)
                return false;
            current = index;
            return true;
        }

        /// The picked node is cleared: move on (an Elite adds a clue).
        public bool Clear()
        {
            if (current < 0 || Complete)
                return false;
            if (Rows[Step][current].Type == HuntNodeType.Elite && Clues < MaxClues)
                Clues++;
            path.Add(current);
            current = -1;
            return true;
        }

        /// The cleared nodes' indexes, one per row: with the seed this is everything a save or the network needs.
        public int[] Path => path.ToArray();

        /// A map with its progress back: the seed plus the cleared path (null if the path doesn't fit the map).
        public static HuntMap Restore(int seed, int[] cleared)
        {
            var m = Generate(seed);
            foreach (int i in cleared)
                if (!m.Pick(i) || !m.Clear())
                    return null;
            return m;
        }

        // ---------- what a node type is ----------

        public static string Name(HuntNodeType t)
        {
            switch (t)
            {
                case HuntNodeType.Skirmish: return "Skirmish";
                case HuntNodeType.Elite: return "Elite squad";
                case HuntNodeType.Ambush: return "Ambush";
                case HuntNodeType.Contraband: return "Contraband";
                case HuntNodeType.Rest: return "Rest";
                case HuntNodeType.Shop: return "Shop";
                case HuntNodeType.Gamble: return "Gamble";
                case HuntNodeType.Boss: return "Boss";
                default: return "";
            }
        }

        /// How dangerous the fight is, 0 (no fight) to 3 (the boss): the pips on the map.
        public static int Danger(HuntNodeType t)
        {
            switch (t)
            {
                case HuntNodeType.Skirmish: return 1;
                case HuntNodeType.Elite: case HuntNodeType.Ambush: case HuntNodeType.Contraband: return 2;
                case HuntNodeType.Boss: return 3;
                default: return 0;
            }
        }

        /// One line on what a node is, for the map screen.
        public static string Blurb(HuntNodeType t)
        {
            switch (t)
            {
                case HuntNodeType.Skirmish: return "The boss's own troops at this step's strength.";
                case HuntNodeType.Elite: return "A tougher squad with the faction's set piece.";
                case HuntNodeType.Ambush: return "A short wave from every side, fast.";
                case HuntNodeType.Contraband: return "A dark arena with spawn portals.";
                case HuntNodeType.Rest: return "No fight. Everyone heals to full and gets a reroll.";
                case HuntNodeType.Shop: return "The Bazaar. Spend the gold you have earned this hunt.";
                case HuntNodeType.Gamble: return "Unknown: a fight, a gift or a curse.";
                case HuntNodeType.Boss: return "The boss, at its full strength.";
                default: return "";
            }
        }

        /// What clearing a node pays, short enough for a tile.
        public static string Reward(HuntNodeType t)
        {
            switch (t)
            {
                case HuntNodeType.Skirmish: return "Gold + XP";
                case HuntNodeType.Elite: return "Legendary + clue";
                case HuntNodeType.Ambush: return "XP + chest";
                case HuntNodeType.Contraband: return "Rare+ card";
                case HuntNodeType.Rest: return "Full heal + reroll";
                case HuntNodeType.Shop: return "Upgrades";
                case HuntNodeType.Gamble: return "Varies";
                case HuntNodeType.Boss: return "Hunt complete";
                default: return "";
            }
        }

        /// A small deterministic generator (the same map on every machine, whatever the runtime's Random does).
        sealed class Rng
        {
            uint s;
            public Rng(int seed) { s = (uint)seed * 2654435761u + 0x9E3779B9u; if (s == 0) s = 1; Next(2); Next(2); }
            public int Next(int n)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                return (int)(s % (uint)n);
            }
        }
    }
}
