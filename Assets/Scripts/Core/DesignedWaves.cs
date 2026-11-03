using System;

namespace Arena
{
    /// The designed wave sets built into the game (the F1 wave editor's .waves files in Data/Waves, turned into code by
    /// Tools/WaveGen/build.sh; DesignedWaves.Generated.cs holds `Names` and `Build`). A final build needs no loose files.
    public static partial class DesignedWaves
    {
        /// A fresh copy of a built-in set (yours to change), or null if there's none by that name.
        public static WaveSet Get(string name) => Array.IndexOf(Names, name) >= 0 ? Build(name) : null;
    }
}
