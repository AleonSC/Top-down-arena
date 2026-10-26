using System;
using System.Collections.Generic;

namespace Arena
{
    public enum Rarity : byte { Common, Uncommon, Rare, VeryRare }

    public enum AbilityId : byte { Barrier, MendPulse, Dash, Shockwave, Overdrive, Blink, Nova, Turret, None = 255 }

    public class AbilityDef
    {
        public string Name, Desc;
        public float Cooldown;
    }

    /// Stats derived from the upgrades a player has picked (recomputed on every pick).
    public class Stats
    {
        public float DmgMult = 1f, FireRateMult = 1f, SpeedMult = 1f, CdMult = 1f, DamageTaken = 1f;
        public float BonusHp, Regen;
        public int ExtraShots, Pierce;
        public int RoleCharges, ExtraCharges;
        public float BarrierRadiusMult = 1f, BarrierBonusTime;
        public float PulseHealMult = 1f, PulseRangeMult = 1f, AuraRangeMult = 1f, AuraHealMult = 1f;
        public float DashIframeBonus, DashCdMult = 1f;
    }

    public class UpgradeDef
    {
        public string Name, Desc;
        public Rarity Rarity;
        public int MaxStacks = 99;
        public Role? Role;                          // only offered to this role
        public AbilityId Grants = AbilityId.None;   // very rare: a new Q/E/Shift ability
        public bool NeedsExtraAbility;              // only offered once you have a bonus ability
        public Action<Stats> Apply;
    }

    public static class Upgrades
    {
        public const int SlotCount = 4;  // 0 = role ability (Space), 1 = Q, 2 = E, 3 = Shift

        public static readonly AbilityDef[] Abilities =
        {
            new AbilityDef { Name = "Barrier", Cooldown = 12f, Desc = "A bubble that deletes enemy bullets." },
            new AbilityDef { Name = "Mend Pulse", Cooldown = 10f, Desc = "Heal everyone nearby and revive downed allies." },
            new AbilityDef { Name = "Dash", Cooldown = 3.5f, Desc = "Quick dash with brief invulnerability." },
            new AbilityDef { Name = "Shockwave", Cooldown = 14f, Desc = "Erase enemy bullets around you and damage nearby enemies." },
            new AbilityDef { Name = "Overdrive", Cooldown = 18f, Desc = "Double fire rate for 5 seconds." },
            new AbilityDef { Name = "Blink", Cooldown = 7f, Desc = "Teleport toward your cursor." },
            new AbilityDef { Name = "Nova", Cooldown = 9f, Desc = "Fire a ring of 24 heavy shots." },
            new AbilityDef { Name = "Turret", Cooldown = 20f, Desc = "Deploy a turret that shoots for 10 seconds." },
        };

        public static AbilityDef Ability(AbilityId id) => Abilities[(int)id];

        public static AbilityId RoleAbility(Role r)
        {
            switch (r)
            {
                case Role.Tank: return AbilityId.Barrier;
                case Role.Healer: return AbilityId.MendPulse;
                default: return AbilityId.Dash;
            }
        }

        public static readonly UpgradeDef[] All =
        {
            // Common: small stat boosts
            new UpgradeDef { Rarity = Rarity.Common, Name = "Sharpened Rounds", Desc = "+8% damage", Apply = s => s.DmgMult += 0.08f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Quick Trigger", Desc = "+8% fire rate", Apply = s => s.FireRateMult += 0.08f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Light Boots", Desc = "+6% move speed", Apply = s => s.SpeedMult += 0.06f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Vitality", Desc = "+15 max HP", Apply = s => s.BonusHp += 15f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Mending", Desc = "Regenerate 1 HP per second", Apply = s => s.Regen += 1f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Focus", Desc = "-6% ability cooldowns", Apply = s => s.CdMult *= 0.94f },

            // Uncommon: larger stat boosts
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Hollow Points", Desc = "+20% damage", Apply = s => s.DmgMult += 0.2f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Hair Trigger", Desc = "+18% fire rate", Apply = s => s.FireRateMult += 0.18f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Sprinter", Desc = "+12% move speed", Apply = s => s.SpeedMult += 0.12f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Fortitude", Desc = "+40 max HP", Apply = s => s.BonusHp += 40f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Regeneration", Desc = "Regenerate 3 HP per second", Apply = s => s.Regen += 3f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Clarity", Desc = "-15% ability cooldowns", Apply = s => s.CdMult *= 0.85f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Thick Skin", Desc = "Take 10% less damage", MaxStacks = 3, Apply = s => s.DamageTaken *= 0.9f },

            // Rare: ability upgrades and weapon mods
            new UpgradeDef { Rarity = Rarity.Rare, Role = Role.Tank, MaxStacks = 1, Name = "Twin Barrier", Desc = "Barrier gets a second charge", Apply = s => s.RoleCharges += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, Role = Role.Tank, MaxStacks = 2, Name = "Bastion", Desc = "Barrier is 40% bigger and lasts 1.5 s longer",
                Apply = s => { s.BarrierRadiusMult += 0.4f; s.BarrierBonusTime += 1.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Role = Role.Healer, MaxStacks = 1, Name = "Second Wind", Desc = "Mend Pulse gets a second charge", Apply = s => s.RoleCharges += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, Role = Role.Healer, MaxStacks = 2, Name = "Greater Mending", Desc = "Mend Pulse heals 60% more and reaches 25% farther",
                Apply = s => { s.PulseHealMult += 0.6f; s.PulseRangeMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Role = Role.Healer, MaxStacks = 2, Name = "Sanctuary", Desc = "Aura reaches 40% farther and heals twice as fast",
                Apply = s => { s.AuraRangeMult += 0.4f; s.AuraHealMult += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Role = Role.Dps, MaxStacks = 2, Name = "Double Dash", Desc = "Dash gets an extra charge", Apply = s => s.RoleCharges += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, Role = Role.Dps, MaxStacks = 2, Name = "Phase Step", Desc = "Dash invulnerability lasts 0.25 s longer; -25% Dash cooldown",
                Apply = s => { s.DashIframeBonus += 0.25f; s.DashCdMult *= 0.75f; } },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 3, Name = "Multishot", Desc = "Fire an extra volley at an angle", Apply = s => s.ExtraShots += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 2, Name = "Piercing Rounds", Desc = "Shots pass through one more enemy", Apply = s => s.Pierce += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 1, NeedsExtraAbility = true, Name = "Overcharge", Desc = "Your Q, E and Shift abilities get a second charge",
                Apply = s => s.ExtraCharges += 1 },

            // Very rare: a new Q / E / Shift ability
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Shockwave, Name = "Shockwave", Desc = "New ability: erase enemy bullets around you and damage nearby enemies" },
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Overdrive, Name = "Overdrive", Desc = "New ability: double fire rate for 5 seconds" },
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Blink, Name = "Blink", Desc = "New ability: teleport toward your cursor" },
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Nova, Name = "Nova", Desc = "New ability: fire a ring of 24 heavy shots" },
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Turret, Name = "Turret", Desc = "New ability: deploy a turret that shoots for 10 seconds" },
        };

        public static Rgba RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return new Rgba(0.35f, 0.9f, 0.4f);
                case Rarity.Rare: return new Rgba(0.35f, 0.6f, 1f);
                case Rarity.VeryRare: return new Rgba(0.8f, 0.4f, 1f);
                default: return new Rgba(0.85f, 0.85f, 0.9f);
            }
        }

        public static string RarityName(Rarity r) => r == Rarity.VeryRare ? "VERY RARE" : r.ToString().ToUpperInvariant();

        public static int Count(List<int> taken, int id)
        {
            int n = 0;
            foreach (int t in taken)
                if (t == id)
                    n++;
            return n;
        }

        /// Can this upgrade be offered to a player with these picks and role?
        public static bool Available(int id, Role role, List<int> taken)
        {
            var u = All[id];
            if (u.Role.HasValue && u.Role.Value != role)
                return false;
            if (Count(taken, id) >= (u.Grants != AbilityId.None ? 1 : u.MaxStacks))
                return false;  // each bonus ability can only be owned once
            int grants = 0;
            foreach (int t in taken)
                if (All[t].Grants != AbilityId.None)
                    grants++;
            if (u.Grants != AbilityId.None && grants >= SlotCount - 1)
                return false;
            if (u.NeedsExtraAbility && grants == 0)
                return false;
            return true;
        }

        /// Roll three different upgrades. Higher levels make rare cards a bit likelier.
        public static int[] RollOffer(Random rng, Role role, List<int> taken, int level)
        {
            float veryRare = Math.Min(10f, 3f + level * 0.5f);
            float rare = Math.Min(22f, 11f + level * 0.8f);
            float[] weights = { 55f, 28f, rare, veryRare };
            var offer = new int[3];
            var used = new List<int>();
            for (int i = 0; i < offer.Length; i++)
            {
                float roll = (float)rng.NextDouble() * (weights[0] + weights[1] + weights[2] + weights[3]);
                int rarity = 0;
                while (rarity < 3 && roll >= weights[rarity])
                {
                    roll -= weights[rarity];
                    rarity++;
                }
                int pick = -1;
                // Try the rolled rarity first, then fall back to lower (and finally any) rarities.
                for (int r = rarity; r >= -3 && pick < 0; r--)
                {
                    int want = r >= 0 ? r : -r;
                    var candidates = new List<int>();
                    for (int id = 0; id < All.Length; id++)
                        if ((int)All[id].Rarity == want && !used.Contains(id) && Available(id, role, taken))
                            candidates.Add(id);
                    if (candidates.Count > 0)
                        pick = candidates[rng.Next(candidates.Count)];
                }
                offer[i] = pick;
                used.Add(pick);
            }
            return offer;
        }

        public static Stats Compute(List<int> taken)
        {
            var s = new Stats();
            foreach (int id in taken)
                if (All[id].Apply != null)
                    All[id].Apply(s);
            return s;
        }
    }
}
