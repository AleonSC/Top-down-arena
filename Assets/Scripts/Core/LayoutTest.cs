using System;
using System.Collections.Generic;

namespace Arena
{
    /// Layout tests for the Cell Editor (TEST LAYOUT): build a piece or a hub floor plan in the run you are in and stand you in it. Dev only (host, solo).
    public partial class Game
    {
        /// Host, in a Grid run: build this piece free at the first spot it fits, its cells' plans fixed to `plans` (one per cell of the orientation, in `Expansion.PieceShapeOrients` order),
        /// and put the host's player in its middle. Returns "" or why not.
        public string DevTestCells(PieceShape shape, int orient, CellLayouts.CellPlan[] plans)
        {
            if (!IsHost || State == MatchState.Idle || !ExpandingOn || Me == null || !Expanded.Exists(Expansion.IsGridCore))
                return "Start a Grid run first (Quick Play or the Campaign), then open the editor from F1.";
            var cells = Expansion.PieceShapeOrients(shape)[orient];
            Placement? at = null;
            foreach (var pl in Expansion.PlacementsFor(Expanded, shape))
                if (pl.Orient == orient)
                {
                    at = pl;
                    break;
                }
            if (!at.HasValue)
                return "No spot for that shape and turn on this map (it may be full).";
            var spot = at.Value;
            string why;
            var made = Expansion.PieceShapeRooms(Expanded, shape, orient, spot.X, spot.Y, out why);
            if (made == null)
                return "The piece didn't fit (" + why + ").";
            for (int i = 0; i < cells.Length; i++)
            {
                int cx, cy;
                CellLayouts.CellOf(spot.X + 1 + cells[i][0] * CellLayouts.Cell, spot.Y + 1 + cells[i][1] * CellLayouts.Cell, out cx, out cy);
                CellLayouts.ForcePlan(cx, cy, plans[i]);
            }
            DevUsed("tested a cell layout");
            for (int i = 0; i < made.Count; i++)
            {
                var q = made[i];
                q.Owner = MyId;
                made[i] = q;
            }
            ApplyBuilt(MyId, made[0], made.GetRange(1, made.Count - 1));
            roundBuilt++;
            PlantDormantGroups(made, false, null, 0, false, DormantGroups.PlainChance);
            PlantBonusLoot(made);
            BroadcastExpansion();
            Me.Pos = spot.Mid;
            DoLog("dev: layout test piece at " + spot.X + "," + spot.Y);
            return "";
        }

        /// Host: put a hub floor plan down now (its crates too) and stand the host's player in the middle of the hub.
        public string DevTestHub(LayoutId id)
        {
            if (!IsHost || State == MatchState.Idle || Me == null)
                return "Start a run first, then open the editor from F1.";
            DevUsed("tested a hub layout");
            DevSetLayout(id);
            PlaceLayoutCrates();
            expWorld = null;
            Me.Pos = new V2(Width / 2f, Height / 2f);
            DoLog("dev: hub layout test " + id);
            return "";
        }
    }
}
