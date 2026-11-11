namespace Arena
{
    /// Weapon variants (Docs/Variants.md): each weapon has Tiers variant tiers, bought with Cores (kept on Profile).
    public static class Variants
    {
        public const int Tiers = 2;
        static readonly int[] Costs = { 10, 20 };

        /// Cores to buy the tier after `owned` tiers; -1 when all are bought.
        public static int Cost(int owned) => owned >= 0 && owned < Tiers ? Costs[owned] : -1;
    }
}
