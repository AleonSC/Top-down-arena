using System;
using System.Collections.Generic;

namespace Arena
{
    /// Bonus loot (owner Nov 20): a built room's cells hold a small stash at their layout's loot slot (`CellLayout.Loot`): some gold coins,
    /// now and then a small vector pickup or a health orb. Everyone gets a full copy, like any drop. A plain Combat or floor room only.
    public static class BonusLoot
    {
        public static float Chance = 0.6f;          // (static: tests set it) each slot: the chance it holds a stash
        public const float VectorChance = 0.25f, OrbChance = 0.25f;
        public const int VectorValue = 4;

        /// The gold in a stash on this wave: four to six coins' worth of 5.
        public static int Gold(int wave) => 15 + 2 * Math.Max(0, wave);

        public static bool Eligible(ExpansionRoom r) => (r.Kind == RoomKind.Combat || r.Kind == RoomKind.Floor) && !r.Floating && CellLayouts.OnLattice(r);
    }

    public partial class Game
    {
        /// Host, as rooms are built: each loot slot of a plain room's cells may hold a stash. Returns how many.
        internal int PlantBonusLoot(IList<ExpansionRoom> boxes)
        {
            if (!IsHost || !ExpandingOn || !Expanded.Exists(Expansion.IsGridCore))
                return 0;
            var eligible = new List<ExpansionRoom>();
            foreach (var b in boxes)
                if (BonusLoot.Eligible(b))
                    eligible.Add(b);
            var roll = new DetRandom(expandSeed * 53 + boxes[0].X * 11 + boxes[0].Y * 5 + Wave);
            int stashes = 0;
            foreach (var at in CellLayouts.LootSlots(eligible, Expanded))
            {
                if (roll.Float() >= BonusLoot.Chance)
                    continue;
                bool vec = roll.Float() < BonusLoot.VectorChance, orb = roll.Float() < BonusLoot.OrbChance;
                foreach (int owner in LootOwners())  // (a full copy each)
                {
                    int gold = BonusLoot.Gold(Wave);
                    while (gold > 0)
                    {
                        int v = Math.Min(5, gold);
                        gold -= v;
                        AddCoin(owner, v, false, at, 6f, 16f);
                    }
                    if (vec)
                        AddCoin(owner, BonusLoot.VectorValue, true, at, 6f, 16f);
                    if (orb)
                        SpawnOrb(at, false, owner: owner);
                }
                stashes++;
            }
            if (stashes > 0)
                DoLog(stashes + " bonus loot stash(es) in a new room");
            return stashes;
        }
    }
}
