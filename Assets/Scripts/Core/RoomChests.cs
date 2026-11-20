using System.Collections.Generic;

namespace Arena
{
    /// Room chests (owner Nov 20, Docs/Nodes.md): a basic piece a player places (modifiers or not) or a mystery room may hold a treasure chest,
    /// in the middle of one of its cells. Vendor rooms (shop, Black Market, Arms, Medic, Builder Shop) never do. The chest's kind is rolled by
    /// rarity (RoomChestWeights) and kept in its node's Rule (ChestKind + 1), so TickNodes spawns that kind.
    public static partial class Expansion
    {
        public static int RoomChestChance = 30;  // percent (a field: tests set it): a placed piece or opened mystery room holds a chest

        /// How often each ChestKind turns up (by ChestKind order: Upgrade, Gold, Vector, Relic, Piece, Buildable, Gadget): the better the chest, the rarer.
        public static readonly int[] RoomChestWeights = { 8, 30, 25, 3, 16, 10, 8 };

        /// The chest a new room rolls, or null for none.
        public static ChestKind? RollRoomChest(DetRandom rng)
        {
            if (rng.Int(100) >= RoomChestChance)
                return null;
            return RollChestKind(rng);
        }

        /// A chest kind by rarity (RoomChestWeights): what a Loot piece guarantees, and what a room's chest roll holds.
        public static ChestKind RollChestKind(DetRandom rng)
        {
            int total = 0;
            foreach (int w in RoomChestWeights)
                total += w;
            int roll = rng.Int(total);
            for (int k = 0; k < RoomChestWeights.Length; k++)
            {
                roll -= RoomChestWeights[k];
                if (roll < 0)
                    return (ChestKind)k;
            }
            return ChestKind.Gold;
        }

        /// The middles of every lattice cell these boxes cover.
        public static List<V2> CellMiddles(IList<ExpansionRoom> boxes)
        {
            var list = new List<V2>();
            foreach (var b in boxes)
                for (int y = b.Y; y + GridCell <= b.Y1; y += GridCell)
                    for (int x = b.X; x + GridCell <= b.X1; x += GridCell)
                        list.Add(new V2(x + GridCell / 2, y + GridCell / 2));
            if (list.Count == 0 && boxes.Count > 0)
                list.Add(new V2((boxes[0].X + boxes[0].X1) / 2, (boxes[0].Y + boxes[0].Y1) / 2));  // (not on the lattice: the first box's middle)
            return list;
        }
    }

    public partial class Game
    {
        /// Host: a new basic piece or mystery room may hold a chest (Expansion.RollRoomChest), placed as a chest node in one of its cells.
        void RollRoomChest(IList<ExpansionRoom> made, string what)
        {
            if (!IsHost || made == null || made.Count == 0)
                return;
            var rng = new DetRandom(expandSeed * 911 + Expanded.Count * 37 + Nodes.Count * 13 + made[0].X * 7 + made[0].Y * 3 + Wave);
            var kind = Expansion.RollRoomChest(rng);
            if (!kind.HasValue)
                return;
            PlaceRoomChest(made, what, kind.Value, rng);
        }

        /// Host: a chest of this kind stands in the middle of one of these rooms' cells (a Loot piece's guarantee, or a rolled room chest).
        internal void PlaceRoomChest(IList<ExpansionRoom> made, string what, ChestKind kind, DetRandom rng)
        {
            if (!IsHost || made == null || made.Count == 0)
                return;
            var cells = Expansion.CellMiddles(made);
            V2 at = cells[rng.Int(cells.Count)];
            Nodes.Add(new MapNode { X = (short)at.X, Y = (short)at.Y, Kind = NodeKind.Chest, Rule = (byte)((int)kind + 1) });
            DoLog("the " + what + " holds a " + kind + " chest at " + (int)at.X + "," + (int)at.Y);
            expWorld = null;  // (its chest stands where the rebuilt world says: TickNodes, after the caller builds the room)
            TickNodes();
            BroadcastExpansion();
        }
    }
}
