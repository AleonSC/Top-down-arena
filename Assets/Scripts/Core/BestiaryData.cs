using System.Collections.Generic;

namespace Arena
{
    /// One of an enemy's attacks, as the Field guide's Bestiary lists it.
    public class BestiaryAttack
    {
        public string Name, Pattern;
        public string Shot;          // the kind of bullet ("Small shot", "Aimed shot"...), or null
        public Rgba ShotColor;
        public string Count, Speed, Damage, Notes;  // shown as written (null: not shown)
        public float Every = -1f;    // seconds between volleys at wave 1 (shorter on later waves unless Fixed), or -1
        public string EveryText;     // ...or a timing that isn't a plain number
        public bool Fixed;           // doesn't speed up with the wave
    }

    /// An enemy or boss in the in-game Bestiary (the pause menu's Field guide). Its numbers (health, speed, size, XP)
    /// come from its EnemyDef; this holds the words.
    public class BestiaryEntry
    {
        public EnemyType Type;
        public string Name, Group, Appears, Lead;
        public int Wave;             // the first wave it can show up in
        public string[] Stages;      // a staged boss's stages, in order
        public BestiaryAttack[] Attacks = new BestiaryAttack[0];
        public string[] Notes = new string[0];
    }

    /// The in-game Bestiary. Most entries are generated from the published Bestiary page
    /// (BestiaryData.Generated.cs, by Docs/Bestiary/build_data.js); the boss adds the page doesn't list are below.
    public static partial class BestiaryData
    {
        public static readonly string[] Groups = { "Melee", "Shooters", "Late game", "Bosses", "Summons" };

        static readonly BestiaryEntry[] Extra =
        {
            new BestiaryEntry
            {
                Type = EnemyType.Rioter, Name = "Rioter", Group = "Summons", Wave = 5,
                Appears = "The Warden's Riot stage: cell doors burst open, 9 at a time",
                Lead = "A prisoner on the loose. Runs at whoever is nearest, the Warden included.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "10" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Guard, Name = "Guard", Group = "Summons", Wave = 5,
                Appears = "The Warden's Riot stage, when rioters are still loose after 10 s",
                Lead = "Blue riot guards with batons. They beat down the rioters left over, then come for the players.",
                Attacks = new[] { new BestiaryAttack { Name = "Baton", Pattern = "Touch", Damage = "14 contact (25 a swing to rioters)" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.HiveEgg, Name = "Hive Egg", Group = "Summons", Wave = 10,
                Appears = "Laid by the Hive Queen in her Honeycomb stage",
                Lead = "Doesn't move or attack. Break it before it hatches into drones.",
            },
            new BestiaryEntry
            {
                Type = EnemyType.DualReaper, Name = "The Dual Reaper", Group = "Summons", Wave = 15,
                Appears = "The Twin Reapers fused into one, in their second stage",
                Lead = "Holds both twins' health and is armored (a third of the damage). Twin rotating lasers while its 4 corner pillars stand (kill them or it pulls you in and blows), then it chases you swinging both scythes. Lure it into a pillar's hole and break its shield: it's open for 4 s (half again the damage). At 30% it splits again.",
            },
            new BestiaryEntry
            {
                Type = EnemyType.ReaperPillar, Name = "Reaper Pillar", Group = "Summons", Wave = 15,
                Appears = "The Dual Reaper's corner pillars",
                Lead = "Each one you kill leaves a hole in the floor that can trap the Dual Reaper.",
            },
            new BestiaryEntry
            {
                Type = EnemyType.SingShard, Name = "Singularity Shard", Group = "Summons", Wave = 20,
                Appears = "The Singularity's Split",
                Lead = "Shards that orbit and fire. Damage you deal them goes to the core.",
            },
            new BestiaryEntry
            {
                Type = EnemyType.BallLightning, Name = "Ball Lightning", Group = "Summons", Wave = 40,
                Appears = "The Storm Herald's Gale",
                Lead = "Slow orbs (3 at a time) that drift at you at 80 px/s plus the wind. Two within 200 px arc lightning to each other, and each pops into 8 shots after 6 s.",
                Attacks = new[] { new BestiaryAttack { Name = "Contact", Pattern = "Touch", Damage = "20" } },
            },
            new BestiaryEntry
            {
                Type = EnemyType.Morsel, Name = "Morsel", Group = "Summons", Wave = 45,
                Appears = "Spat out by the Devourer when it turns Hungry",
                Lead = "Spat 220–440 px away, 4 at a time; the Devourer crawls to the nearest at 90 px/s, and each one it eats heals it 1% of its health. Shoot them or step on them first.",
            },
        };

        static BestiaryEntry[] all;

        /// Every entry, by group (Groups' order), then by first wave.
        public static BestiaryEntry[] All
        {
            get
            {
                if (all != null)
                    return all;
                var list = new List<BestiaryEntry>(FromPage);
                list.AddRange(Extra);
                var sorted = new List<BestiaryEntry>();
                foreach (var g in Groups)
                {
                    var inGroup = list.FindAll(e => e.Group == g);
                    // Stable by first wave, keeping the page's order for ties.
                    for (int i = 1; i < inGroup.Count; i++)
                        for (int j = i; j > 0 && inGroup[j - 1].Wave > inGroup[j].Wave; j--)
                        {
                            var t = inGroup[j];
                            inGroup[j] = inGroup[j - 1];
                            inGroup[j - 1] = t;
                        }
                    sorted.AddRange(inGroup);
                }
                foreach (var e in list)
                    if (!sorted.Contains(e))
                        sorted.Add(e);  // a group the list above doesn't name: still shown, last
                return all = sorted.ToArray();
            }
        }

        /// The entry for an enemy type (the second Twin Reaper shares the first's), or null.
        public static BestiaryEntry For(EnemyType t)
        {
            if (t == EnemyType.TwinB)
                t = EnemyType.TwinA;
            foreach (var e in All)
                if (e.Type == t)
                    return e;
            return null;
        }
    }
}
