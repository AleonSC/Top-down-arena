using System.Collections.Generic;

namespace Arena
{
    // Node fog is off (owner, Nov 18) and scout towers and gold veins are retired (Nov 20): this file only clears a fogged node
    // (`MapNode.Hidden`) that an old save still carries.
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
    }
}
