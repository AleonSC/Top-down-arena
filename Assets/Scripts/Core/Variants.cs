using System;

namespace Arena
{
    /// Weapon variants (Docs/Variants.md): each weapon has Tiers variant tiers, bought with Cores (kept on Profile).
    public static class Variants
    {
        public const int Tiers = 2;
        static readonly int[] Costs = { 10, 20 };

        public const int WinCores = 5, BossCores = 1, HuntWinCores = 5, CarrierMin = 1, CarrierMax = 3;

        /// Cores a finished Campaign run pays: 1 a boss defeated and 5 for the win (a full 100-wave win: 15). Endless pays none.
        public static int RunCores(bool won, int bosses, bool endless) => endless ? 0 : Math.Max(0, bosses) * BossCores + (won ? WinCores : 0);

        /// Cores a finished Boss Hunt pays: 5 for a win.
        public static int HuntCores(bool won) => won ? HuntWinCores : 0;

        /// Cores to buy the tier after `owned` tiers; -1 when all are bought.
        public static int Cost(int owned) => owned >= 0 && owned < Tiers ? Costs[owned] : -1;
    }
}
