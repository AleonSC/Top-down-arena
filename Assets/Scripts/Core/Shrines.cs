using System;

namespace Arena
{
    /// Health shrines (owner Nov 20, Docs/Nodes.md): the only map node placed. A room built round one turns its cell into the shrine;
    /// each player can use it once by walking up to it, which clears their wounds, fills their health and adds health past max (Player.OverHp).
    public static class Shrines
    {
        public const int Ring = 3;            // shrines round the hub in the first boss cycle (one per side in turn); each later cycle adds one
        public const float Reach = 46f;       // how close a player walks to a shrine to use it
        public const float OverFrac = 0.5f;   // the health past max it gives, as a share of max health

        /// A shrine's bit in Player.ShrinesUsed: its place among the map's shrines (shrines past the 64th share the last bit).
        public static ulong Bit(System.Collections.Generic.IList<MapNode> nodes, int index)
        {
            int n = 0;
            for (int i = 0; i < index && i < nodes.Count; i++)
                if (nodes[i].Kind == NodeKind.Shrine)
                    n++;
            return 1UL << Math.Min(63, n);
        }
    }

    public partial class Game
    {
        /// Has this player used the shrine at node `i`?
        public bool ShrineUsed(Player p, int i) => p != null && (p.ShrinesUsed & Shrines.Bit(Nodes, i)) != 0;

        /// Host, every frame (from TickExpansion, in waves and between them): a living player who walks up to a claimed shrine they haven't used is healed to full
        /// plus OverFrac of their max health past it (kept until damage takes it or they go down).
        void TickShrines()
        {
            if (!IsHost || !ExpandingOn || Nodes.Count == 0 || State == MatchState.GameOver || State == MatchState.Victory)
                return;
            for (int i = 0; i < Nodes.Count; i++)
            {
                var nd = Nodes[i];
                if (nd.Kind != NodeKind.Shrine || !Expansion.NodeClaimed(Expanded, nd))
                    continue;
                V2 at = NodeSpot(i);
                foreach (var p in Players.Values)
                {
                    if (p.Downed || ShrineUsed(p, i) || !p.Pos.CloserThan(at, Shrines.Reach + p.Def.BodyR))
                        continue;
                    UseShrine(p, i);
                }
            }
        }

        void UseShrine(Player p, int i)
        {
            p.ShrinesUsed |= Shrines.Bit(Nodes, i);
            p.Wound = 0f;  // a shrine clears wounds (owner Nov 20)
            p.Hp = Math.Max(p.Hp, p.HealCap);
            p.OverHp = Math.Max(p.OverHp, p.MaxHp * Shrines.OverFrac);
            BroadcastFx(FxKind.Ring, NodeSpot(i), new Rgba(0.4f, 1f, 0.55f), 110f, 0.6f, Sfx.Revive);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.85f, 0.35f), 60f, 0.5f, Sfx.None);
            DoLog("player " + p.Id + " used a health shrine: +" + (int)p.OverHp + " health past max");
        }

        // --- Network: the player's shrine fields (SyncPlayer) ---

        void SyncShrineBuffs(NetSync s, Player p)
        {
            s.Float(ref p.OverHp);       // a health shrine's health past max
            s.ULong(ref p.ShrinesUsed);
        }
    }
}
