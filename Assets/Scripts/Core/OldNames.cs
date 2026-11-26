using System.Collections.Generic;

namespace Arena
{
    /// Code names renamed to match the game's names (Docs/Glossary.md): profiles, run saves and wave files written
    /// before still load. Each maps an old enum name to the current one; anything else passes through.
    public static class OldNames
    {
        static readonly Dictionary<string, string> Enemies = new Dictionary<string, string>
        {
            { "Boss", "Warden" }, { "TwinA", "ReaperOfDusk" }, { "TwinB", "ReaperOfDawn" },
            { "SingShard", "SingularityShard" }, { "MiniWell", "SmallWell" }, { "Portal", "SpawnPortal" },
        };

        static readonly Dictionary<string, string> Abilities = new Dictionary<string, string>
        {
            { "Grenade", "Airstrike" }, { "Nova", "Starburst" },  // (the Cluster Grenade became Airstrike)
            { "BattleFury", "CaltropRun" }, { "CaughtSteel", "CaltropRun" }, { "BladeTempest", "Drawbridge" }, { "Lunge", "Gauntlet" }, { "Whirlwind", "BladedDive" },
            { "IronBody", "CeremonialArmor" }, { "ArcaneAegis", "Aeroaegis" }, { "Sanctify", "LumenWard" }, { "GuardianSpirit", "ForcePact" },
            { "BulletStorm", "ArtilleryAssault" }, { "BloodBoil", "ArterialSpray" }, { "BloodRite", "VeinTithe" }, { "CrimsonPact", "BloodWall" },
            { "KineticWall", "GaleWall" }, { "FrenzyPact", "BloodDebt" }, { "HemoNova", "VeinRush" }, { "SiphonField", "BloodFreeze" }, { "CrimsonApotheosis", "CrimsonHarvest" },
        };

        static readonly Dictionary<string, string> Upgrades = new Dictionary<string, string>
        {
            { "Cluster Grenade", "Airstrike" }, { "Kinetic Wall", "Gale Wall" }, { "Second Charge", "Extra Use: 1st Ability" },
            { "Extra Use: Space", "Extra Use: 1st Ability" }, { "Extra Use: Q", "Extra Use: 2nd Ability" },  // (before the abilities were called 1st-4th)
            { "Extra Use: E", "Extra Use: 3rd Ability" }, { "Extra Use: Shift", "Extra Use: 4th Ability" },
            { "Nova", "Starburst" }, { "Lifeline", "Red Thread" },  // (the Vein Tether card; the Healer ultimate kept Lifeline)
            { "Stormcaller", "Forked Thunder" }, { "Forged Edge", "Barbed Iron" },  // (Caught Steel's card; Caltrop Run took its place)
        };

        /// An upgrade card's name as it is called now (run saves keep taken cards by name).
        public static string Upgrade(string name)
        {
            string now;
            return Upgrades.TryGetValue(name, out now) ? now : name;
        }

        /// An EnemyType name as it is called now; a suffix after ':' (a long hunt's key) is kept.
        public static string Enemy(string name)
        {
            int colon = name.IndexOf(':');
            string head = colon < 0 ? name : name.Substring(0, colon), now;
            return Enemies.TryGetValue(head, out now) ? now + (colon < 0 ? "" : name.Substring(colon)) : name;
        }

        /// An AbilityId name as it is called now.
        public static string Ability(string name)
        {
            string now;
            return Abilities.TryGetValue(name, out now) ? now : name;
        }
    }
}
