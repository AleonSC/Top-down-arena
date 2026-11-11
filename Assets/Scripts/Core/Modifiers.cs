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
        HeadStart = 64,
        Practiced = 128,
        Bloodlust = 256,
        GoldRush = 512,
        TinyArena = 1024,
        Nightfall = 2048,
        Hunted = 4096,
        DeathThroes = 8192,
        SolidWalls = 16384,
        SecondWind = 32768,
        ZombieInfection = 65536,
        Cluttered = 131072,
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
            new ModDef { Mod = RunMod.HeadStart, Name = "Head Start", Desc = "Everyone starts at level 3 with a free legendary pick.", Lean = ModLean.Easier },
            new ModDef { Mod = RunMod.Practiced, Name = "Practiced", Desc = "Abilities gain mastery twice as fast.", Lean = ModLean.Easier },
            new ModDef { Mod = RunMod.Bloodlust, Name = "Bloodlust", Desc = "Every kill heals its killer, but no health orbs drop and nobody regenerates.", Lean = ModLean.Risky },
            new ModDef { Mod = RunMod.GoldRush, Name = "Gold Rush", Desc = "Every kill drops gold, and a shop opens before each boss. Unspent gold is kept.", Lean = ModLean.Easier },
            new ModDef { Mod = RunMod.TinyArena, Name = "Tiny Arena", Desc = "After each boss, walls take a quarter of the room (down to 40%).", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.Nightfall, Name = "Nightfall", Desc = "It's dark: you see a short way round each player, plus where you aim.", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.Hunted, Name = "Hunted", Desc = "Each wave one player is hunted by every enemy, and deals double damage.", Lean = ModLean.Risky },
            new ModDef { Mod = RunMod.DeathThroes, Name = "Death Throes", Desc = "Every regular enemy bursts into a ring of 6 shots a moment after it dies.", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.SolidWalls, Name = "Solid Walls", Desc = "No wrapping round the edges: the arena's sides are walls.", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.ZombieInfection, Name = "Zombie Infection", Desc = "Every wave is a growing horde of melee zombies, and some of the dead get back up.", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.Cluttered, Name = "Cluttered", Desc = "Every wave piles a lot more crates into the arena. Wind your way through the maze; boss fights clear it.", Lean = ModLean.Harder },
            new ModDef { Mod = RunMod.SecondWind, Name = "Second Wind", Desc = "Once between bosses, each player gets back up by themselves at half health.", Lean = ModLean.Easier },
        };

        public const float GlassCannonDealt = 1.5f, GlassCannonTaken = 1.5f, ToughCrowdHp = 1.5f, FrenzyFire = 1.3f, SwiftFoesSpeed = 1.3f;
        public const int LuckyRerolls = 3;
        public const int HeadStartLevel = 3;
        public const float PracticedMastery = 2f;
        public const float HuntedDamage = 2f;                                   // the hunted player's damage
        public const int ThroesShots = 6;
        public const float ThroesFuse = 0.5f, ThroesSpeed = 120f;               // Death Throes: a small ring, a moment after the kill
        public const float SecondWindTime = 2f;                                 // down this long, then up at half health

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

        /// Hunted: the player every enemy goes for this wave (0: nobody). Replicated.
        public int HuntedId;

        public bool IsHunted(Player p) => p != null && HuntedId == p.Id && Has(RunMod.Hunted);

        /// Host, as a wave starts: Hunted picks its quarry; Second Wind comes back at the start of each boss cycle.
        void ModsWaveStart()
        {
            HuntedId = 0;
            if (Has(RunMod.Hunted))
            {
                var up = new List<Player>();
                foreach (var p in Players.Values)
                    if (!p.Downed)
                        up.Add(p);
                if (up.Count > 0)
                {
                    var h = up[rng.Next(up.Count)];
                    HuntedId = h.Id;
                    foreach (var e in Enemies.Values)
                        e.RetargetT = 0f;
                    BroadcastFx(FxKind.Ring, h.Pos, new Rgba(1f, 0.3f, 0.3f), 120f, 0.6f, Sfx.BossCharge);
                    DoLog(h.Role + " is hunted on wave " + Wave);
                }
            }
            if (Has(RunMod.SecondWind) && (Wave - 1) % 5 == 0 && FirstLeg)
                foreach (var p in Players.Values)
                    p.SecondWind = true;
        }
    }
}
