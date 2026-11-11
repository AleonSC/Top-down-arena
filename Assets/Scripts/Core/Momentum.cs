using System;

namespace Arena
{
    /// The DPS role's passive, Momentum: every kill you land adds a stack (up to MomentumMax); each stack gives
    /// +MomentumPer move speed and fire rate (Player.Speed, FireRate). A stack ages out after MomentumHold seconds
    /// without a kill, all at once; going down drops them. Tank has the taunt and Healer the aura; a Wildcard has none.
    public partial class Game
    {
        public const int MomentumMax = 5;
        public const float MomentumPer = 0.03f, MomentumHold = 4f;  // +3% per stack (15% at full), refreshed by each kill

        /// Host only: called for every dead enemy. Only a DPS counts, and only kills that give XP (not dummies or summons).
        void MomentumKill(Player killer, Enemy e)
        {
            if (killer == null || killer.Role != Role.Dps || killer.Downed || e.Def.Xp <= 0)
                return;
            killer.MomentumStacks = Math.Min(MomentumMax, killer.MomentumStacks + 1);
            killer.MomentumT = MomentumHold;
        }

        void TickMomentum(Player p, float dt)
        {
            if (p.MomentumStacks <= 0)
                return;
            p.MomentumT = Math.Max(0f, p.MomentumT - dt);
            if (p.MomentumT <= 0f || p.Downed)
                p.MomentumStacks = 0;
        }

        /// The multiplier on speed and fire rate: 1 with no stacks.
        public static float MomentumMult(Player p) => 1f + MomentumPer * p.MomentumStacks;
    }
}
