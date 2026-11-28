using System;
using System.Collections.Generic;

namespace Arena
{
    /// Cell variants (Docs/CellVariants.md): a cell layout can have other versions besides its plain one, each a whole layout of its
    /// own. A Combat lattice cell whose layout has variants rolls one (`RollVariant`, seeded from its place, so every machine
    /// builds the same room and nothing goes on the wire). The first kind is the pit variant: some cover swapped for pits, voids
    /// in the floor (Pits.cs). Mockup: Docs/Mockups/cell-pits.html.
    public static partial class CellLayouts
    {
        public const float VariantChance = 0.4f;  // a Combat cell whose layout has variants takes one this often
        public static string Forced;  // dev (previews, tests): a variant's name (or "any") every cell that can take it takes; null: rolled

        static int[] C(int x, int y, int r) => new[] { x, y, r };  // a round pit

        /// The variants, by the plain layout's name (so a reorder of `All` keeps them). Each keeps the middle (the chest) and the four
        /// door lanes clear, and its slots off its pits.
        static readonly Dictionary<string, CellLayout[]> variants = new Dictionary<string, CellLayout[]>
        {
            ["Open Plaza"] = new[] { new CellLayout { Name = "Sinkholes",
                Walls = new[] { W(50, 50, 50, 50), W(500, 500, 50, 50) },
                Pits = new[] { C(175, 425, 75), C(425, 175, 75) },
                Sleepers = new[] { P(130, 130), P(470, 470) },
                Traps = new[] { T(300, 150, 0), T(300, 450, 0), T(150, 300, 0), T(450, 300, 0) },
                Loot = new[] { P(510, 480) } } },
            ["Four Pillars"] = new[] { new CellLayout { Name = "Four Wells",
                Walls = new int[0][],
                Pits = new[] { C(150, 150, 50), C(450, 150, 50), C(150, 450, 50), C(450, 450, 50) },
                Sleepers = new[] { P(100, 300), P(500, 300) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(300, 200, 0), T(300, 400, 0) },
                Loot = new[] { P(540, 60) } } },
            ["Vault"] = new[] { new CellLayout { Name = "Island Vault",
                Walls = new int[0][],
                Pits = new[] { W(250, 150, 100, 50), W(250, 400, 100, 50), W(150, 250, 50, 100), W(400, 250, 50, 100) },
                Sleepers = new[] { P(120, 120), P(480, 120) },
                Traps = new[] { T(100, 300, 1), T(500, 300, 1), T(300, 80, 0), T(300, 520, 0) },
                Loot = new[] { P(480, 490) } } },
            ["Side Alcoves"] = new[] { new CellLayout { Name = "The Chasm",
                Walls = new int[0][],
                Pits = new[] { W(150, 150, 300, 50), W(150, 400, 300, 50) },
                Sleepers = new[] { P(110, 300), P(490, 300) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(200, 300, 0), T(400, 300, 0) },
                Loot = new[] { P(500, 500) } } },
            ["Corner Nooks"] = new[] { new CellLayout { Name = "Corner Drops",
                Walls = new int[0][],
                Pits = new[] { W(50, 50, 100, 100), W(450, 50, 100, 100), W(50, 450, 100, 100), W(450, 450, 100, 100) },
                Sleepers = new[] { P(200, 200), P(400, 400) },
                Traps = new[] { T(300, 150, 0), T(300, 450, 0), T(150, 300, 0), T(450, 300, 0) },
                Loot = new[] { P(400, 200), P(200, 400) } } },
            ["Crossfire"] = new[] { new CellLayout { Name = "Crossfire Gap",
                Walls = new[] { W(150, 150, 250, 50), W(200, 400, 250, 50) },
                Pits = new[] { W(50, 100, 150, 50), W(400, 400, 150, 150) },
                Sleepers = new[] { P(200, 290), P(120, 480) },
                Traps = new[] { T(540, 100, 1), T(540, 330, 1), T(225, 275, 0), T(225, 325, 0) },
                Loot = new[] { P(500, 120) } } },
            ["Pillar Field"] = new[] { new CellLayout { Name = "Stepping Stones",
                Walls = new[] { W(250, 100, 50, 50), W(100, 250, 50, 50), W(400, 250, 50, 50), W(250, 400, 50, 50) },
                Pits = new[] { C(150, 150, 50), C(450, 150, 50), C(150, 450, 50), C(450, 450, 50) },
                Sleepers = new[] { P(200, 225), P(355, 355), P(200, 375) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(60, 300, 1), T(210, 330, 0), T(380, 380, 0) },
                Loot = new[] { P(400, 200) } } },
            ["Hall & Alcoves"] = new[] { new CellLayout { Name = "Pit Halls",
                Walls = new[] { W(0, 150, 250, 50), W(350, 150, 250, 50), W(0, 400, 250, 50), W(350, 400, 250, 50) },
                Pits = new[] { W(500, 50, 50, 100), W(50, 450, 50, 100) },
                Sleepers = new[] { P(130, 90), P(400, 90) },
                Traps = new[] { T(60, 300, 1), T(540, 300, 1), T(200, 300, 0), T(400, 300, 0) },
                Loot = new[] { P(190, 510), P(470, 510) } } },
        };

        /// Dev: the layout whose variant `Forced` names (a lone cell takes it), or -1.
        static int ForcedBase()
        {
            if (Forced == null || Forced == "any")
                return -1;
            for (int i = 0; i < All.Length; i++)
                foreach (var v in VariantsOf(i))
                    if (v.Name == Forced)
                        return i;
            return -1;
        }

        /// The variants of layout `layout` (empty: plain only).
        public static CellLayout[] VariantsOf(int layout)
            => layout >= 0 && layout < All.Length && variants.TryGetValue(All[layout].Name, out var v) ? v : Array.Empty<CellLayout>();

        /// The layout a plan builds: the plain one, or its variant.
        public static CellLayout LayoutOf(CellPlan plan)
        {
            if (plan.Variant <= 0)
                return All[plan.Layout];
            var v = VariantsOf(plan.Layout);
            return plan.Variant <= v.Length ? v[plan.Variant - 1] : All[plan.Layout];
        }

        /// Its name as players see it: the variant's, or the plain layout's.
        public static string NameOf(CellPlan plan) => LayoutOf(plan).Name;

        /// Do this piece's cells roll variants? Only a plain Combat piece on the lattice (not the core, a surprise room or a service room).
        static bool CanVary(IList<ExpansionRoom> rooms, IEnumerable<int> group)
        {
            foreach (int i in group)
            {
                var r = rooms[i];
                if (r.Kind != RoomKind.Combat || r.Floating || r.Surprise != 0 || Expansion.IsGridCore(r))
                    return false;
            }
            return true;
        }

        /// A cell's variant: 0 (plain) or 1..n, seeded from its place (its own stream: the layout picks are as before).
        public static int RollVariant(int layout, int cx, int cy)
        {
            var v = VariantsOf(layout);
            int n = v.Length;
            if (n == 0)
                return 0;
            if (Forced != null)
            {
                for (int i = 0; i < n; i++)
                    if (Forced == "any" || v[i].Name == Forced)
                        return i + 1;
                return 0;
            }
            var rng = new DetRandom(cx * 19349663 ^ cy * 83492791 ^ 0x68e31da4);
            if (rng.Float() >= VariantChance)
                return 0;
            return 1 + rng.Int(n);
        }

        /// Is (x, y) (cell-local) inside this pit grown by pad? A pit is x, y, w, h (a box) or x, y, r (round).
        public static bool InPit(int[] p, float x, float y, float pad)
        {
            if (p.Length == 3)
            {
                float dx = x - p[0], dy = y - p[1], r = p[2] + pad;
                return dx * dx + dy * dy < r * r;
            }
            return x > p[0] - pad && x < p[0] + p[2] + pad && y > p[1] - pad && y < p[1] + p[3] + pad;
        }

        /// A room's pits from its cells' layouts, as solids (boxes and discs) in world space.
        public static void AddPits(List<Solid> to, ExpansionRoom r, Dictionary<long, CellPlan> plans)
        {
            foreach (var o in CellOrigins(r))
            {
                CellOf(o.X + 1, o.Y + 1, out int cx, out int cy);
                if (!plans.TryGetValue(Key(cx, cy), out var plan))
                    continue;
                foreach (var p in TurnedOf(plan).Pits)
                    to.Add(p.Length == 3 ? World.Pillar(o.X + p[0], o.Y + p[1], p[2]) : World.Box(o.X + p[0], o.Y + p[1], o.X + p[0] + p[2], o.Y + p[1] + p[3]));
            }
        }
    }
}
