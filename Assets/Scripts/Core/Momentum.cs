using System;

namespace Arena
{
    /// The DPS role's passive, Momentum: every MomentumKillsPer kills you land add a stack (up to MomentumMax); each
    /// stack gives +MomentumPer move speed and fire rate (Player.Speed, FireRate). Stacks and the kills toward the next
    /// one age out after MomentumHold seconds without a kill, all at once; going down drops them. Tank has the taunt and Healer the aura; a Wildcard has none.
    public partial class Game
    {
        public const int MomentumMax = 5;
        public const int MomentumKillsPer = 3;  // kills for each stack: 15 to fill it
        public const float MomentumPer = 0.03f, MomentumHold = 4f;  // +3% per stack (15% at full), refreshed by each kill

        /// Host only: called for every dead enemy. Only a DPS counts, and only kills that give XP (not dummies or summons).
        void MomentumKill(Player killer, Enemy e)
        {
            if (killer == null || killer.Role != Role.Dps || killer.Downed || e.Def.Xp <= 0)
                return;
            killer.MomentumT = MomentumHold;
            if (killer.MomentumStacks >= MomentumMax)
                return;
            if (++killer.MomentumKills >= MomentumKillsPer)
            {
                killer.MomentumKills = 0;
                killer.MomentumStacks++;
            }
        }

        void TickMomentum(Player p, float dt)
        {
            if (p.MomentumStacks <= 0 && p.MomentumKills <= 0)
                return;
            p.MomentumT = Math.Max(0f, p.MomentumT - dt);
            if (p.MomentumT <= 0f || p.Downed)
                p.MomentumStacks = p.MomentumKills = 0;
        }

        /// The multiplier on speed and fire rate: 1 with no stacks.
        public static float MomentumMult(Player p) => 1f + MomentumPer * p.MomentumStacks;

        /// The aura's level: the stacks plus the way to the next one (0 to MomentumMax), which MomentumLook.cs eases toward.
        public static float MomentumLevel(Player p) => p.MomentumStacks >= MomentumMax ? MomentumMax : p.MomentumStacks + p.MomentumKills / (float)MomentumKillsPer;

        // --- Network: the player's Momentum fields (SyncPlayer) ---

        void SyncMomentum(NetSync s, Player p)
        {
            s.Int(ref p.MomentumStacks);  // Momentum.cs
            s.Float(ref p.MomentumT);
            s.Byte(ref p.MomentumKills);
        }
    }
}
