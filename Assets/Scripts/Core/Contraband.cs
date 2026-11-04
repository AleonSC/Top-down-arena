namespace Arena
{
    /// Contraband (Perks.cs, Docs/Contraband.md): the Warden perk that brings treasure. On the first wave after each boss (waves 6, 11,
    /// 16...) a chest always turns up when anyone in the party has it, on top of the random ones (Props.cs, WaveProps), and the
    /// holder's own item from any chest is rare or better (Props.cs, RollChestLoot). Teammates still get their own normal item from the
    /// same chest. Hunts have no boss cycles, so no extra chest there.
    public partial class Game
    {
        /// The first wave after a boss: 6, 11, 16...
        public static bool IsCycleStart(int wave) => wave >= 6 && (wave - 1) % 5 == 0;

        /// Host: does this wave get Contraband's guaranteed chest?
        bool ContrabandChest()
        {
            if (Hunt != null || !IsCycleStart(Wave))
                return false;
            foreach (var p in Players.Values)
                if (HasPerk(p, PerkId.Contraband))
                    return true;
            return false;
        }
    }
}
