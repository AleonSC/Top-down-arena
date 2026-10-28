using System;
using System.Collections.Generic;

namespace Arena
{
    /// Run modifiers the host picks in the lobby (the Modifiers tab). They apply to the whole run, for everyone.
    [Flags]
    public enum RunMod
    {
        None = 0,
        GlassCannon = 1,
        ToughCrowd = 2,
        Frenzy = 4,
        SwiftFoes = 8,
        Famine = 16,
        Lucky = 32,
    }

    public enum ModLean { Harder, Easier, Risky }

    public class ModDef
    {
        public RunMod Mod;
        public string Name, Desc;
        public ModLean Lean;
    }

    public static class Modifiers
    {
        public static readonly ModDef[] All =
        {
            new ModDef { Mod = RunMod.GlassCannon, Name = "Glass Cannon", Desc = "Everyone deals 50% more damage, and takes 50% more.", Lean = ModLean.Risky },
            new ModDef { Mod = RunMod.ToughCrowd, Name = "Tough Crowd", Desc = "Enemies and bosses have 50% more health.", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.Frenzy, Name = "Frenzy", Desc = "Enemies and bosses shoot 30% faster.", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.SwiftFoes, Name = "Swift Foes", Desc = "Enemies move 30% faster.", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.Famine, Name = "Famine", Desc = "Enemies never drop health orbs.", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.Lucky, Name = "Lucky", Desc = "Everyone starts with 3 extra upgrade rerolls.", Lean = ModLean.Easier },
        };

        public const float GlassCannonDealt = 1.5f, GlassCannonTaken = 1.5f, ToughCrowdHp = 1.5f, FrenzyFire = 1.3f, SwiftFoesSpeed = 1.3f;
        public const int LuckyRerolls = 3;

        public static int Count(RunMod mods)
        {
            int n = 0;
            foreach (var d in All)
                if ((mods & d.Mod) != 0)
                    n++;
            return n;
        }

        public static List<string> Names(RunMod mods)
        {
            var names = new List<string>();
            foreach (var d in All)
                if ((mods & d.Mod) != 0)
                    names.Add(d.Name);
            return names;
        }
    }

    public partial class Game
    {
        /// The run's modifiers (host: copied from the lobby when the match starts; clients: told by the host).
        public RunMod Mods;

        public bool Has(RunMod m) => (Mods & m) != 0;
    }
}
