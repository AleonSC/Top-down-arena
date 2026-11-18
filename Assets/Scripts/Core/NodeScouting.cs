using System.Collections.Generic;

namespace Arena
{
    /// Node fog, scout towers and gold veins on the Expanding arena (Docs/Nodes.md, "Fog, scout towers and gold veins").
    /// A node is placed fogged (`MapNode.Hidden`): everyone sees where it is but not what it is ("?") until a player walks within
    /// `RevealReach` of it or a claimed scout tower has it in range. A claimed gold vein pays every player gold each cleared wave.
    public static class NodeScouting
    {
        public const float RevealReach = 800f;   // a player this close (8 tiles) reveals a fogged node for everyone
        public const float TowerReach = 3000f;   // a claimed scout tower reveals every node this close (30 tiles), ones placed later too
        public const int VeinGold = 10, VeinGoldPerWave = 1;  // a claimed vein's pay per player per cleared wave (first guesses, tune with vecbal)

        /// What a vein pays each player as wave `wave` is cleared.
        public static int VeinPay(int wave) => VeinGold + VeinGoldPerWave * System.Math.Max(0, wave);

        /// Whether a node's kind shows: not fogged, or already claimed (its room is built round it).
        public static bool Known(MapNode n, bool claimed) => !n.Hidden || claimed;

        /// Whether a claimed scout tower among `nodes` has `at` in range.
        public static bool TowerSees(IList<ExpansionRoom> built, IList<MapNode> nodes, V2 at)
        {
            foreach (var t in nodes)
                if (t.Kind == NodeKind.Tower && at.CloserThan(t.Pos, TowerReach) && Expansion.NodeClaimed(built, t))
                    return true;
            return false;
        }
    }

    public partial class Game
    {
        float fogT;

        /// Host, every frame (throttled): fog is off (owner, Nov 18), so a fogged node from an old save is revealed for everyone at once.
        void TickNodeFog(float dt)
        {
            if (!IsHost || !ExpandingOn || Nodes.Count == 0)
                return;
            fogT += dt;
            if (fogT < 0.25f)
                return;
            fogT = 0f;
            bool changed = false;
            for (int i = 0; i < Nodes.Count; i++)
            {
                var nd = Nodes[i];
                if (!nd.Hidden)
                    continue;
                // (fog is off since Nov 18, owner: nodes are always visible; an old save's fogged node clears at once)
                nd.Hidden = false;
                Nodes[i] = nd;
                changed = true;
                DoLog("a fogged node is revealed: " + nd.Kind);
            }
            if (changed)
                BroadcastExpansion();
        }

        /// Host, as a wave is cleared: every claimed gold vein pays every player (stacking per vein).
        void PayVeins()
        {
            if (!IsHost || !ExpandingOn || !ModeRules.Extras)
                return;
            foreach (var nd in Nodes)
            {
                if (nd.Kind != NodeKind.Vein || !Expansion.NodeClaimed(Expanded, nd))
                    continue;
                int pay = NodeScouting.VeinPay(Wave);
                foreach (var p in Players.Values)
                    p.Gold += pay;
                BroadcastFx(FxKind.Ring, nd.Pos, new Rgba(1f, 0.82f, 0.3f), 110f, 0.5f, Sfx.Pickup);
                DoLog("a gold vein pays " + pay + " gold each");
            }
        }
    }
}
