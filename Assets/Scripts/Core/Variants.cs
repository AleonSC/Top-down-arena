using System;

namespace Arena
{
    /// Weapon variants (Docs/Variants.md): each weapon has Tiers variant tiers, bought with Cores (kept on Profile).
    public static class Variants
    {
        public const int Tiers = 2;

        /// Variants that are whole other weapons, picked in the base weapon's Variants window (WeaponDef.Variant): the Twin Rifle's is the Twin Rifle Auto.
        /// Unlocked for testing: no Cores needed yet (TODO: tie to a tier when the unlock rules are decided).
        public const bool WeaponVariantsUnlocked = true;

        /// The weapon that is tier `tier` (0 or 1) of `id`'s Variants window, or null: the Twin Rifle's are the Auto and Out of Ammo.
        public static WeaponId? VariantOf(WeaponId id, int tier = 0) => id != WeaponId.TwinRifle ? (WeaponId?)null : tier == 0 ? WeaponId.TwinRifleAuto : tier == 1 ? WeaponId.OutOfAmmo : (WeaponId?)null;

        /// Is `variant` any of `id`'s variant weapons?
        public static bool IsVariantOf(WeaponId id, WeaponId variant) => VariantOf(id, 0) == variant || VariantOf(id, 1) == variant;

        public static string Blurb(WeaponId variant) => variant == WeaponId.TwinRifleAuto
            ? "Both rifles aim and fire on their own at the nearest enemy on their side. Alt locks both onto one enemy until it dies."
            : variant == WeaponId.OutOfAmmo ? "The rifles swat the nearest bullet each. A knife chain on close enemies, ending in a rifle clap. Alt: a spin." : "";
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
