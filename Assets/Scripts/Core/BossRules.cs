using System;

namespace Arena
{
    /// A boss fight's numbers on a mechanics tier (Docs/DifficultyTiers.md step 3). Normal is the fight as balanced: every
    /// field at its no-change value. The bosses read these through `Game.BossRest`, `BossAdds` and `EscortMult`.
    public struct BossRules
    {
        public float RestPace;    // x: the breathers (each boss's Rest turn) between its attacks; the attacks keep their length
        public int ExtraAdds;     // + the helpers each summon brings (Warden's rioters per door and guards, Queen's drones and eggs,
                                  // Lich's dead per grave and spirits, Devourer's morsels, Eclipse's court)
        public float EscortMult;  // x: a boss wave's escort (BossWaves.cs: the Warden's Security Guards, ...)
    }

    public partial class Game
    {
        /// Each tier's boss numbers (Easy, Normal, Hard, Nightmare). Owner's picks (Nov 26): summons -1 / +1 / +2, escorts
        /// 75% / 125% / 150%; the rest pace stays 1x on every tier for now (to tune later).
        public static readonly BossRules[] TierBossRules =
        {
            new BossRules { RestPace = 1f, ExtraAdds = -1, EscortMult = 0.75f },  // Easy
            new BossRules { RestPace = 1f, ExtraAdds = 0, EscortMult = 1f },      // Normal
            new BossRules { RestPace = 1f, ExtraAdds = 1, EscortMult = 1.25f },   // Hard
            new BossRules { RestPace = 1f, ExtraAdds = 2, EscortMult = 1.5f },    // Nightmare
        };

        /// This run's boss numbers (its mechanics tier).
        public BossRules BossRulesNow => TierBossRules[(int)MechTier];

        /// A boss turn's length on this tier: a Rest (every boss's turn 0) stretches or shrinks by the rest pace.
        public float BossTurnLen(byte turn, float len) => turn == 0 ? len * BossRulesNow.RestPace : len;

        /// How many helpers a summon brings on this tier (`normal` on Normal; never fewer than `min`).
        public int BossAdds(int normal, int min = 1) => Math.Max(min, normal + BossRulesNow.ExtraAdds);

        /// A boss wave's escort size on this tier.
        public float EscortMult => BossRulesNow.EscortMult;
    }
}
