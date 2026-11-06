using System;
using System.Collections.Generic;

namespace Arena
{
    public struct StatLine
    {
        public string Label, Value;
        public bool Header;  // a section title rather than a stat
    }

    /// The numbers shown beside the upgrade cards. Built from a list of picks so the
    /// menu can also preview what a card would change.
    public static class StatSheet
    {
        /// The starting value of a milestone stat line, for a card's "old → new" when you have none of it yet.
        public static string Default(string label)
        {
            switch (label)
            {
                case "XP gain": return "+0% XP";
                case "Extra pick chance": return "0%";
                case "Cards per offer": return "3 cards";
                case "Rerolls per boss": return Game.BossRerolls.ToString();
                case "Rarer cards": return "+0%";
                case "Milestone chance": return "0%";
                default: return null;
            }
        }

        /// newKey: for a preview that adds a new ability, the key it would go on (1-3; 0 = first free).
        /// baseRegen: the run's passive regeneration (its difficulty's; Game.PassiveRegen).
        public static List<StatLine> Describe(Player p, List<int> taken, int newKey = 0, float baseRegen = 0.25f)
        {
            var s = Upgrades.Compute(taken);
            var lines = new List<StatLine>();
            lines.Add(new StatLine { Label = "Max HP", Value = ((int)(p.BaseHp + s.BonusHp)).ToString() });
            lines.Add(new StatLine { Label = "Damage", Value = Pct(s.DmgMult * (1f - Game.HomingDamageCost * s.Homing)) });  // Homing Rounds cost damage
            lines.Add(new StatLine { Label = "Fire rate", Value = Pct(s.FireRateMult) });
            lines.Add(new StatLine { Label = "Move speed", Value = Pct(s.SpeedMult) });
            lines.Add(new StatLine { Label = "Ability cooldowns", Value = Pct(s.CdMult) });
            lines.Add(new StatLine { Label = "Damage taken", Value = Pct(s.DamageTaken) });
            lines.Add(new StatLine { Label = "Regeneration", Value = (baseRegen + s.Regen).ToString("0.0#") + " HP/s" });
            lines.Add(new StatLine { Label = "Extra projectiles", Value = "+" + s.ExtraShots });
            lines.Add(new StatLine { Label = "Barrage (fanned copies)", Value = "+" + s.Fan });
            lines.Add(new StatLine { Label = "Pierce", Value = "+" + s.Pierce });
            if (s.ShotSizeMult != 1f)
                lines.Add(new StatLine { Label = "Shot size", Value = Pct(s.ShotSizeMult) });  // Magnum Rounds
            // Milestone upgrades: shown once you have one (Default() gives the starting value a card's preview compares with).
            if (s.XpMult != 1f)
                lines.Add(new StatLine { Label = "XP gain", Value = "+" + Math.Round((s.XpMult - 1f) * 100f) + "% XP" });
            if (s.ExtraPickChance > 0f)
                lines.Add(new StatLine { Label = "Extra pick chance", Value = Math.Round(s.ExtraPickChance * 100f) + "%" });
            if (s.OfferExtra > 0)
                lines.Add(new StatLine { Label = "Cards per offer", Value = (3 + s.OfferExtra) + " cards" });
            if (s.BossRerolls > 0)
                lines.Add(new StatLine { Label = "Rerolls per boss", Value = (Game.BossRerolls + s.BossRerolls).ToString() });
            if (s.RarityLuck > 0f)
                lines.Add(new StatLine { Label = "Rarer cards", Value = "+" + Math.Round(s.RarityLuck * 100f) + "%" });
            if (s.MilestoneChance > 0f)
                lines.Add(new StatLine { Label = "Milestone chance", Value = Math.Round(s.MilestoneChance * 100f) + "%" });
            lines.Add(new StatLine { Label = "Parry durability", Value = ((int)(Game.ParryCapacity * s.ParryCapacityMult)).ToString() });
            lines.Add(new StatLine { Label = Upgrades.Ability(p.RoleAbility).Name + " charges", Value = (1 + s.RoleCharges).ToString() });

            var bonus = new List<string>();
            var legendary = new Dictionary<string, int>();
            var mods = new Dictionary<string, int>();
            if (p.StartBonus != AbilityId.None)
                bonus.Add(Upgrades.Ability(p.StartBonus).Name);  // a Wildcard's second ability is on a bonus key from the start
            foreach (int id in taken)
            {
                var u = Upgrades.All[id];
                if (u.Grants != AbilityId.None)
                    bonus.Add(Upgrades.Ability(u.Grants).Name);
                if (u.Rarity == Rarity.Legendary)
                    legendary[u.Name] = legendary.ContainsKey(u.Name) ? legendary[u.Name] + 1 : 1;
                if (u.Rarity == Rarity.Mod)
                    mods[u.Name] = mods.ContainsKey(u.Name) ? mods[u.Name] + 1 : 1;
            }
            if (bonus.Count > 0)
            {
                lines.Add(new StatLine { Label = "Bonus abilities", Header = true });
                // Laid out the way the player arranged their keys (and where a previewed new one would go).
                string[] keys = { "Q", "E", "Shift" };
                var layout = (AbilityId[])p.BonusKeys.Clone();
                AbilityId added = AbilityId.None;
                foreach (int id in taken)
                    if (Upgrades.All[id].Grants != AbilityId.None && Array.IndexOf(layout, Upgrades.All[id].Grants) < 0)
                        added = Upgrades.All[id].Grants;
                if (added != AbilityId.None)
                {
                    if (newKey >= 1)
                        Game.PlaceOnKey(layout, added, newKey);
                    else if (Array.IndexOf(layout, AbilityId.None) >= 0)
                        layout[Array.IndexOf(layout, AbilityId.None)] = added;
                }
                for (int i = 0; i < keys.Length; i++)
                    if (layout[i] != AbilityId.None)
                        lines.Add(new StatLine { Label = keys[i], Value = Upgrades.Ability(layout[i]).Name });
                lines.Add(new StatLine { Label = "Bonus charges", Value = (1 + s.ExtraCharges).ToString() });
            }
            if (mods.Count > 0)
            {
                lines.Add(new StatLine { Label = "Weapon mods", Header = true });
                foreach (var kv in mods)
                    lines.Add(new StatLine { Label = kv.Key, Value = kv.Value > 1 ? "x" + kv.Value : "" });
            }
            if (legendary.Count > 0)
            {
                lines.Add(new StatLine { Label = "Legendary", Header = true });
                foreach (var kv in legendary)
                    lines.Add(new StatLine { Label = kv.Key, Value = kv.Value > 1 ? "x" + kv.Value : "" });
            }
            return lines;
        }

        static string Pct(float mult) => ((int)System.Math.Round(mult * 100f)) + "%";
    }
}
