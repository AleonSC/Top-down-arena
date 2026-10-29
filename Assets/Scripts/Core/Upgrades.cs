using System;
using System.Collections.Generic;

namespace Arena
{
    public enum Rarity : byte { Common, Uncommon, Rare, VeryRare, Legendary, Mod }  // Mod: weapon mods, picked after each boss

    public enum AbilityId : byte
    {
        Barrier, MendPulse, Dash, Shockwave, Overdrive, Blink, Nova, Turret,
        Roar, Bulldoze, Haven, SoulLink, Grenade, Mark,
        // Mage
        ArcaneAegis, FrostNova, FlameWall, TimeRewind, ArcaneWard, Sanctify, Meteor, Blizzard, ArcaneSurge,
        // Summoner
        GolemWall, Earthquake, GuardianSpirit, SpiritSwarm, Phoenix, SoulLantern, BatSwarm, Sacrifice, AlphaCall,
        // Ultimates (F): one per role and class
        Fortress, GlacialTomb, Titan, Lifeline, Sanctum, WorldTree, BulletStorm, MeteorStorm, Legion,
        // Knight (Space), then the Knight ultimates
        ShieldWall, LeapSlam, Unbreakable, Consecrate, GuardiansOath, LayOnHands, Whirlwind, Lunge, BattleFury,
        LastStand, HallowedBlade, BladeTempest,
        // Ninja (Space), then the Ninja ultimates
        ShadowClone, SmokeBomb, IronBody, HealingMist, Substitution, SpiritWard, ShadowStep, Vanish, BladeFan,
        ThousandClones, BlossomStorm, DeathLotus,
        // Blood Mage (Space), then the Blood Mage ultimates
        BloodShell, CrimsonPact, Exsanguinate, BloodGift, SiphonField, Martyr, BloodBoil, FrenzyPact, HemoNova,
        BloodRite, SanguineRain, CrimsonApotheosis,
        StunGun,  // the Warden's legendary: a Q / E / Shift ability (Stun.cs)
        None = 255,
    }

    /// Everything about one ability except its cast code (which is in its class's file, next to its entry).
    public class AbilityDef
    {
        public const int CardOnly = -1, Ult = 3;

        public AbilityId Id;
        public Kit Kit;
        public Role Role;
        public int Choice = CardOnly;  // its place in the lobby: Space choice 0-2 for Role and Kit, Ult, or CardOnly (very rare cards only)
        public string Name, Desc;
        public string Capstone;        // what mastering it (level 5) adds
        public float Cooldown;
        public bool Instant;           // earns mastery XP by what it hits (0.5x for nothing, up to 1.5x); lasting ones earn the full amount
        public int ExtraCharges;       // charges beyond the first
        public bool CapstoneCharge;    // mastering it adds a charge
        public Func<BotSense, bool> Bot;  // when the smart bot uses it (BotBrain.cs); null: when a target is within 520 px
        public float HealthCost;   // the Blood Mage's abilities cost this much of your max health (it can't take you below 1)
        public bool CostsCurrent;  // ...or of your current health (Blood Gift)
    }

    /// Stats derived from the upgrades a player has picked (recomputed on every pick).
    public class Stats
    {
        public float DmgMult = 1f, FireRateMult = 1f, SpeedMult = 1f, CdMult = 1f, DamageTaken = 1f;
        public float BonusHp, Regen;
        public int ExtraShots, Pierce;
        public int Fan;  // Barrage: extra copies of every shot, fanned out to the sides
        public int RoleCharges, ExtraCharges;
        public float BarrierRadiusMult = 1f, BarrierBonusTime, BarrierCapacityMult = 1f;
        public float PulseHealMult = 1f, PulseRangeMult = 1f, AuraRangeMult = 1f, AuraHealMult = 1f;
        public float DashIframeBonus, DashCdMult = 1f;
        public float RoarBonusTime, RoarExtraReduction, BulldozeMult = 1f;
        public float HavenRadiusMult = 1f, HavenBonusTime, LinkHealMult = 1f;
        public float GrenadeRadiusMult = 1f, MarkBonus;
        // Knight abilities
        public float WallCapMult = 1f, WallArcMult = 1f, WallBonusTime;
        public float SlamRadiusMult = 1f, SlamStunBonus;
        public float UnbreakBonus, UnbreakHealBonus;
        public float ConsecrateRadiusMult = 1f, ConsecrateBonusTime, OathMult = 1f, LayMult = 1f;
        public float WhirlBonusTime, WhirlRadiusMult = 1f, LungeRangeMult = 1f, LungeDmgMult = 1f, FuryLifesteal;
        public int GrenadeExtraShrapnel;
        // Ninja abilities
        public float CloneBonusTime, CloneBurstMult = 1f, SmokeRadiusMult = 1f, SmokeBonusTime, IronBonusTime, IronWaveMult = 1f;
        public float MistHealMult = 1f, MistRadiusMult = 1f, SubShieldMult = 1f, SubBonusTime, WardRadiusMult = 1f, WardBonusTime;
        public float StepDmgMult = 1f, StepRangeMult = 1f, VanishBonusTime, AmbushMult = 2f;
        public int FanExtraKunai;
        // Blood Mage abilities
        public float ShellBonus, ShellBonusTime, PactMult = 1f, PactRadiusMult = 1f, DrainMult = 1f, DrainRadiusMult = 1f;
        public float GiftMult = 1f, SiphonRadiusMult = 1f, SiphonBonusTime, MartyrBonus, MartyrInvuln;
        public float BoilMult = 1f, BoilRadiusMult = 1f, RushBonusTime, RushRate;
        public int NovaExtraBolts;

        // Legendary weapon upgrades from bosses.
        public int Guards, KillRing, SplitShots;  // Guards: Warden's Guard companions (Stun.cs)
        public float Lifesteal, BroodChance, ExecuteBonus, CritChance, KillHeal, ImpactSplash, SlowOnHit;

        // Weapon mods, one picked after each boss (and its legendary).
        public float Homing;              // turn rate (radians per second) toward the nearest enemy
        public int Ricochet;              // bounces to another enemy on hit
        public float BurnFrac;            // a hit burns for this much of its damage per second, for 3 s
        public float CryoSlow, FreezeChance;
        public float ShockChance;
        public float ExplosiveRadius;
        public float ShotSpeedMult = 1f;

        // Parry upgrades.
        public float ParryCapacityMult = 1f, ParryArcMult = 1f, PerfectBonus, ReflectMult = 1f, CounterFireRate, MirrorChance;
        public bool ParryNoSlow;
    }

    public class UpgradeDef
    {
        public string Name, Desc;
        public Rarity Rarity;
        public int MaxStacks = 99;
        public Role? Role;                          // only offered to this role
        public AbilityId Grants = AbilityId.None;   // very rare: a new Q/E/Shift ability
        public bool NeedsExtraAbility;              // only offered once you have a bonus ability
        public AbilityId RequiresAbility = AbilityId.None;  // only offered if you have this ability (on any key)
        public bool ClassAbility;                   // very rare: one of your class's other Space abilities, for Q/E/Shift
        public int Weight = 1;                      // how many times it counts among the cards of its rarity
        public EnemyType? Boss;                     // legendary: the boss that awards it (TwinA = the Twin Reapers)
        public Action<Stats> Apply;
    }

    public static class Upgrades
    {
        public const int SlotCount = 5;  // 0 = role ability (Space), 1 = Q, 2 = E, 3 = Shift, 4 = ultimate (F)
        public const int BonusSlots = 3; // Q, E and Shift, filled by very rare upgrades
        public const int UltSlot = 4;

        /// Every ability, indexed by AbilityId. Each class keeps its entries beside its cast code: Abilities.cs (Soldier and
        /// the card-only abilities), Minions.cs (Mage, Summoner), Ultimates.cs (their ultimates), Knight.cs, Ninja.cs,
        /// BloodMage.cs, Stun.cs (the Warden's Stun Gun). An entry's Kit, Role and Choice place it in the lobby; the tables below are built from them.
        public static readonly AbilityDef[] Abilities = Registry(Game.SoldierAbilities, Game.MageAbilities, Game.SummonerAbilities,
            Game.UltimateAbilities, Game.KnightAbilities, Game.NinjaAbilities, Game.BloodMageAbilities, Game.WardenAbilities);

        /// Each role and class has its own ultimate; it can't be changed.
        static readonly AbilityId[] Ultimates = LobbyTable(true);

        public static AbilityId Ultimate(Role r, Kit kit) => Ultimates[(int)r * Defs.KitCount + (int)kit];

        /// The three Space abilities for each role and kit, picked in the lobby.
        static readonly AbilityId[] RoleAbilities = LobbyTable(false);

        public static AbilityId RoleAbility(Role r, Kit kit, int choice) =>
            RoleAbilities[((int)r * Defs.KitCount + (int)kit) * RoleAbilityChoices + (choice >= 0 && choice < RoleAbilityChoices ? choice : 0)];

        public const int RoleAbilityChoices = 3;

        public static AbilityDef Ability(AbilityId id) => Abilities[(int)id];

        static AbilityDef[] Registry(params AbilityDef[][] lists)
        {
            var all = new AbilityDef[Enum.GetValues(typeof(AbilityId)).Length - 1];  // every id but None
            foreach (var list in lists)
                foreach (var a in list)
                {
                    if (all[(int)a.Id] != null)
                        throw new InvalidOperationException("Two AbilityDefs for " + a.Id);
                    all[(int)a.Id] = a;
                }
            for (int i = 0; i < all.Length; i++)
                if (all[i] == null)
                    throw new InvalidOperationException("No AbilityDef for " + (AbilityId)i);
            return all;
        }

        /// The lobby's ultimates (one per role and class) or Space choices (RoleAbilityChoices per role and class),
        /// from where each entry says it goes. Every place must be filled exactly once.
        static AbilityId[] LobbyTable(bool ultimates)
        {
            int per = ultimates ? 1 : RoleAbilityChoices;
            var table = new AbilityId[3 * Defs.KitCount * per];
            for (int i = 0; i < table.Length; i++)
                table[i] = AbilityId.None;
            foreach (var a in Abilities)
            {
                if (a.Choice == AbilityDef.CardOnly || ultimates != (a.Choice == AbilityDef.Ult))
                    continue;
                int i = ((int)a.Role * Defs.KitCount + (int)a.Kit) * per + (ultimates ? 0 : a.Choice);
                if (table[i] != AbilityId.None)
                    throw new InvalidOperationException(a.Id + " and " + table[i] + " are in the same lobby place");
                table[i] = a.Id;
            }
            int empty = Array.IndexOf(table, AbilityId.None);
            if (empty >= 0)
                throw new InvalidOperationException("No " + (ultimates ? "ultimate" : "Space ability") + " for lobby place " + empty);
            return table;
        }


        public static readonly UpgradeDef[] All = WithClassAbilities(new[]
        {
            // Common: small stat boosts
            new UpgradeDef { Rarity = Rarity.Common, Name = "Sharpened Rounds", Desc = "+8% damage", Apply = s => s.DmgMult += 0.08f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Quick Trigger", Desc = "+8% fire rate", Apply = s => s.FireRateMult += 0.08f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Light Boots", Desc = "+6% move speed", Apply = s => s.SpeedMult += 0.06f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Vitality", Desc = "+15 max HP", Apply = s => s.BonusHp += 15f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Mending", Desc = "Regenerate 1 HP per second", Apply = s => s.Regen += 1f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Focus", Desc = "-6% ability cooldowns", Apply = s => s.CdMult *= 0.94f },
            new UpgradeDef { Rarity = Rarity.Common, MaxStacks = 4, Name = "Reinforced Guard", Desc = "Your parry shield takes 25% more before it breaks", Apply = s => s.ParryCapacityMult += 0.25f },

            // Uncommon: larger stat boosts
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Hollow Points", Desc = "+20% damage", Apply = s => s.DmgMult += 0.2f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Hair Trigger", Desc = "+18% fire rate", Apply = s => s.FireRateMult += 0.18f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Sprinter", Desc = "+12% move speed", Apply = s => s.SpeedMult += 0.12f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Fortitude", Desc = "+40 max HP", Apply = s => s.BonusHp += 40f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Regeneration", Desc = "Regenerate 3 HP per second", Apply = s => s.Regen += 3f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Clarity", Desc = "-15% ability cooldowns", Apply = s => s.CdMult *= 0.85f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Thick Skin", Desc = "Take 10% less damage", MaxStacks = 3, Apply = s => s.DamageTaken *= 0.9f },
            new UpgradeDef { Rarity = Rarity.Uncommon, MaxStacks = 2, Name = "Wide Guard", Desc = "Your parry shield covers a 30% wider arc", Apply = s => s.ParryArcMult += 0.3f },

            // Rare: ability upgrades and weapon mods
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 1, Name = "Second Charge", Desc = "Your Space ability gets a second charge", Apply = s => s.RoleCharges += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Barrier, MaxStacks = 2, Name = "Bastion", Desc = "Barrier is 40% bigger, lasts 1.5 s longer and soaks 50% more",
                Apply = s => { s.BarrierRadiusMult += 0.4f; s.BarrierBonusTime += 1.5f; s.BarrierCapacityMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Roar, MaxStacks = 2, Name = "Bellow", Desc = "Roar lasts 2 s longer and blocks 15% more damage",
                Apply = s => { s.RoarBonusTime += 2f; s.RoarExtraReduction += 0.15f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Bulldoze, MaxStacks = 2, Name = "Juggernaut", Desc = "Bulldoze deals double damage", Apply = s => s.BulldozeMult += 1f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.MendPulse, MaxStacks = 2, Name = "Greater Mending", Desc = "Mend Pulse heals 60% more and reaches 25% farther",
                Apply = s => { s.PulseHealMult += 0.6f; s.PulseRangeMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Haven, MaxStacks = 2, Name = "Hallowed Ground", Desc = "Haven is 50% bigger and lasts 3 s longer",
                Apply = s => { s.HavenRadiusMult += 0.5f; s.HavenBonusTime += 3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SoulLink, MaxStacks = 2, Name = "Kindred Spirits", Desc = "Soul Link heals twice as fast", Apply = s => s.LinkHealMult += 1f },
            new UpgradeDef { Rarity = Rarity.Rare, Role = Role.Healer, MaxStacks = 2, Name = "Sanctuary", Desc = "Aura reaches 40% farther and heals twice as fast",
                Apply = s => { s.AuraRangeMult += 0.4f; s.AuraHealMult += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Dash, MaxStacks = 2, Name = "Phase Step", Desc = "Dash invulnerability lasts 0.25 s longer; -25% Dash cooldown",
                Apply = s => { s.DashIframeBonus += 0.25f; s.DashCdMult *= 0.75f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Grenade, MaxStacks = 2, Name = "Bigger Boom", Desc = "Grenade blast is 50% bigger with 6 more shrapnel",
                Apply = s => { s.GrenadeRadiusMult += 0.5f; s.GrenadeExtraShrapnel += 6; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Mark, MaxStacks = 2, Name = "Exploit Weakness", Desc = "Marked enemies take an extra 50% damage", Apply = s => s.MarkBonus += 0.5f },
            // Knight abilities
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ShieldWall, MaxStacks = 2, Name = "Tower Shield", Desc = "Shield Wall covers 160°, lasts 1.5 s longer and soaks 50% more",
                Apply = s => { s.WallArcMult += 0.33f; s.WallBonusTime += 1.5f; s.WallCapMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.LeapSlam, MaxStacks = 2, Name = "Earthshaker", Desc = "Leap Slam hits 40% wider and stuns for 1 s longer",
                Apply = s => { s.SlamRadiusMult += 0.4f; s.SlamStunBonus += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Unbreakable, MaxStacks = 2, Name = "Iron Will", Desc = "Unbreakable blocks 15% more damage and heals you for 10% more of what you deal",
                Apply = s => { s.UnbreakBonus += 0.15f; s.UnbreakHealBonus += 0.1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Consecrate, MaxStacks = 2, Name = "Sacred Ground", Desc = "Consecrate is 40% bigger and lasts 3 s longer",
                Apply = s => { s.ConsecrateRadiusMult += 0.4f; s.ConsecrateBonusTime += 3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.GuardiansOath, MaxStacks = 2, Name = "Bulwark", Desc = "Guardian's Oath shields are 60% stronger", Apply = s => s.OathMult += 0.6f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.LayOnHands, MaxStacks = 2, Name = "Miracle", Desc = "Lay on Hands heals 60% more and reaches 30% further", Apply = s => s.LayMult += 0.6f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Whirlwind, MaxStacks = 2, Name = "Cyclone", Desc = "Whirlwind lasts 1 s longer and reaches 30% further",
                Apply = s => { s.WhirlBonusTime += 1f; s.WhirlRadiusMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Lunge, MaxStacks = 2, Name = "Flurry", Desc = "Lunge goes 40% further and hits 50% harder",
                Apply = s => { s.LungeRangeMult += 0.4f; s.LungeDmgMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BattleFury, MaxStacks = 2, Name = "Bloodlust", Desc = "During Battle Fury your hits heal you for 10% of the damage they deal",
                Apply = s => s.FuryLifesteal += 0.1f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ShadowClone, MaxStacks = 2, Name = "Mirror Image", Desc = "Shadow Clone lasts 2 s longer and bursts 50% harder",
                Apply = s => { s.CloneBonusTime += 2f; s.CloneBurstMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SmokeBomb, MaxStacks = 2, Name = "Thick Smoke", Desc = "Smoke Bomb is 40% bigger and lasts 2 s longer",
                Apply = s => { s.SmokeRadiusMult += 0.4f; s.SmokeBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.IronBody, MaxStacks = 2, Name = "Tempered Steel", Desc = "Iron Body lasts 1 s longer and its shockwave hits 50% harder",
                Apply = s => { s.IronBonusTime += 1f; s.IronWaveMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.HealingMist, MaxStacks = 2, Name = "Morning Dew", Desc = "Healing Mist heals 50% more and is 25% bigger",
                Apply = s => { s.MistHealMult += 0.5f; s.MistRadiusMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Substitution, MaxStacks = 2, Name = "Body Double", Desc = "Substitution's shield is 60% stronger and lasts 2 s longer",
                Apply = s => { s.SubShieldMult += 0.6f; s.SubBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SpiritWard, MaxStacks = 2, Name = "Sacred Seal", Desc = "Spirit Ward is 40% bigger and lasts 3 s longer",
                Apply = s => { s.WardRadiusMult += 0.4f; s.WardBonusTime += 3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ShadowStep, MaxStacks = 2, Name = "Assassinate", Desc = "Shadow Step strikes 60% harder and reaches enemies 30% farther",
                Apply = s => { s.StepDmgMult += 0.6f; s.StepRangeMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Vanish, MaxStacks = 2, Name = "Unseen Blade", Desc = "Vanish lasts 1.5 s longer and your ambush hit deals one more times its damage",
                Apply = s => { s.VanishBonusTime += 1.5f; s.AmbushMult += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BladeFan, MaxStacks = 2, Name = "Storm of Steel", Desc = "Blade Fan throws 6 more kunai",
                Apply = s => s.FanExtraKunai += 6 },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BloodShell, MaxStacks = 2, Name = "Hardened Blood", Desc = "Blood Shell is worth one more times what you paid and lasts 2 s longer",
                Apply = s => { s.ShellBonus += 1f; s.ShellBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.CrimsonPact, MaxStacks = 2, Name = "Blood Oath", Desc = "Crimson Pact's burst hits 50% harder and reaches 30% farther",
                Apply = s => { s.PactMult += 0.5f; s.PactRadiusMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Exsanguinate, MaxStacks = 2, Name = "Deep Veins", Desc = "Exsanguinate reaches 30% farther and drains 50% more",
                Apply = s => { s.DrainRadiusMult += 0.3f; s.DrainMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BloodGift, MaxStacks = 2, Name = "Lifeblood", Desc = "Blood Gift gives 50% more",
                Apply = s => s.GiftMult += 0.5f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SiphonField, MaxStacks = 2, Name = "Hungry Earth", Desc = "Siphon Field is 40% bigger and lasts 3 s longer",
                Apply = s => { s.SiphonRadiusMult += 0.4f; s.SiphonBonusTime += 3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Martyr, MaxStacks = 2, Name = "Saint's Blood", Desc = "Martyr revives with 25% more health and leaves them invulnerable 1 s longer",
                Apply = s => { s.MartyrBonus += 0.25f; s.MartyrInvuln += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BloodBoil, MaxStacks = 2, Name = "Boiling Point", Desc = "Blood Boil bursts 50% harder and reaches 25% farther",
                Apply = s => { s.BoilMult += 0.5f; s.BoilRadiusMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.FrenzyPact, MaxStacks = 2, Name = "Bloodrush", Desc = "Frenzy Pact lasts 2 s longer and fires another 25% faster",
                Apply = s => { s.RushBonusTime += 2f; s.RushRate += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.HemoNova, MaxStacks = 2, Name = "Crimson Tide", Desc = "Hemo Nova fires 8 more bolts",
                Apply = s => s.NovaExtraBolts += 8 },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 3, Name = "Multishot", Desc = "+1 projectile: upgrades your weapon in its own way", Apply = s => s.ExtraShots += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 3, Name = "Barrage", Desc = "Every shot also fires a copy fanned out to the side. Stack it into a wall of bullets",
                Apply = s => s.Fan += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 2, Name = "Piercing Rounds", Desc = "Shots pass through one more enemy", Apply = s => s.Pierce += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 2, Name = "Riposte", Desc = "Perfect-parry window is 0.12 s longer and reflected shots deal double damage",
                Apply = s => { s.PerfectBonus += 0.12f; s.ReflectMult += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 2, Name = "Counterstance", Desc = "A perfect parry gives +50% fire rate for 2.5 s",
                Apply = s => s.CounterFireRate += 0.5f },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 1, Name = "Light Guard", Desc = "You don't slow down while parrying", Apply = s => s.ParryNoSlow = true },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 1, NeedsExtraAbility = true, Name = "Overcharge", Desc = "Your Q, E and Shift abilities get a second charge",
                Apply = s => s.ExtraCharges += 1 },

            // Very rare: a new Q / E / Shift ability
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Shockwave, Name = "Shockwave", Desc = "New ability: erase enemy bullets around you and damage nearby enemies" },
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Overdrive, Name = "Overdrive", Desc = "New ability: double fire rate for 5 seconds" },
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Blink, Name = "Blink", Desc = "New ability: teleport toward your cursor" },
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Nova, Name = "Nova", Desc = "New ability: fire a ring of 24 heavy shots" },
            new UpgradeDef { Rarity = Rarity.VeryRare, Grants = AbilityId.Turret, Name = "Turret", Desc = "New ability: deploy a turret that shoots for 10 seconds" },

            // Legendary: unique weapon upgrades, only from killing bosses (three per boss).
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Boss, MaxStacks = 3, Name = "Warden's Guard",
                Desc = "One of the Warden's riot guards switches sides: it follows you and fights for you with its baton (+1 guard per stack). If it falls, another arrives 15 s later",
                Apply = s => s.Guards += 1 },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Boss, Grants = AbilityId.StunGun, Name = "Stun Gun",
                Desc = "New ability: draw a stun gun with 6 shots that stun enemies. The cooldown starts once they're all fired" },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Boss, MaxStacks = 3, Name = "Ringburst", Desc = "Enemies you kill burst into a ring of 6 shots",
                Apply = s => s.KillRing += 6 },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.HiveQueen, MaxStacks = 3, Name = "Swarm Rounds", Desc = "Your shots split into 3 smaller shots when they hit",
                Apply = s => s.SplitShots += 3 },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.HiveQueen, MaxStacks = 3, Name = "Royal Jelly", Desc = "Every hit heals you 1 HP (up to 10 HP per second)",
                Apply = s => s.Lifesteal += 1f },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.HiveQueen, MaxStacks = 3, Name = "Brood Mother", Desc = "20% of your kills spawn a mini-turret for 4 seconds",
                Apply = s => s.BroodChance += 0.2f },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.TwinA, MaxStacks = 3, Name = "Executioner", Desc = "Double damage to enemies below 35% health",
                Apply = s => s.ExecuteBonus += 1f },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.TwinA, MaxStacks = 3, Name = "Twin Fang", Desc = "25% chance for each hit to deal double damage",
                Apply = s => s.CritChance += 0.25f },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.TwinA, MaxStacks = 3, Name = "Soul Harvest", Desc = "Every kill heals you 5 HP",
                Apply = s => s.KillHeal += 5f },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Singularity, MaxStacks = 3, Name = "Collapse Rounds", Desc = "Shots implode on impact, hurting enemies nearby",
                Apply = s => s.ImpactSplash += 45f },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Singularity, MaxStacks = 3, Name = "Graviton Rounds", Desc = "Enemies you hit are slowed by 40% for 2 seconds",
                Apply = s => s.SlowOnHit = Math.Min(0.7f, s.SlowOnHit + 0.4f) },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Singularity, MaxStacks = 3, Name = "Dark Matter", Desc = "Shots pierce 2 more enemies and deal 20% more damage",
                Apply = s => { s.Pierce += 2; s.DmgMult += 0.2f; } },

            // Weapon mods: one is picked after every boss, from the full list.
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Homing Rounds", Desc = "Shots bend toward an enemy close ahead of them (fixes near misses), but deal 10% less damage per stack. More stacks bend harder",
                Apply = s => s.Homing += 2f },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Ricochet", Desc = "Shots bounce to another nearby enemy after a hit (75% damage). +1 bounce per stack",
                Apply = s => s.Ricochet += 1 },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Incendiary Rounds", Desc = "Hits set enemies on fire, burning for 25% of the hit's damage each second for 3 s",
                Apply = s => s.BurnFrac += 0.25f },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Cryo Rounds", Desc = "Hits chill enemies (20% slower) with a 6% chance to freeze them for 1 s",
                Apply = s => { s.CryoSlow = Math.Min(0.6f, s.CryoSlow + 0.2f); s.FreezeChance += 0.06f; } },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Shock Rounds", Desc = "15% chance per hit for lightning to arc to 2 more enemies (60% damage)",
                Apply = s => s.ShockChance += 0.15f },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Explosive Rounds", Desc = "Shots explode on impact, dealing 30% damage to enemies nearby",
                Apply = s => s.ExplosiveRadius += s.ExplosiveRadius == 0f ? 40f : 15f },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Mirror Guard", Desc = "Each bullet your parry blocks has a 25% chance to fly back at enemies",
                Apply = s => s.MirrorChance = Math.Min(0.75f, s.MirrorChance + 0.25f) },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Velocity Rounds", Desc = "Shots fly 30% faster and farther and deal 15% more damage",
                Apply = s => { s.ShotSpeedMult += 0.3f; s.DmgMult += 0.15f; } },
        });

        /// After a boss: every weapon mod you can still take, to choose from.
        public static int[] ModOffer(Role role, AbilityId roleAbility, List<int> taken)
        {
            var ids = new List<int>();
            for (int id = 0; id < All.Length; id++)
                if (All[id].Rarity == Rarity.Mod && Available(id, role, roleAbility, taken))
                    ids.Add(id);
            return ids.Count > 0 ? ids.ToArray() : null;
        }

        /// The three legendaries for a boss (topped up from other bosses' if you've maxed some out).
        public static int[] LegendaryOffer(Random rng, EnemyType boss, Role role, AbilityId roleAbility, List<int> taken)
        {
            var own = new List<int>();
            var others = new List<int>();
            for (int id = 0; id < All.Length; id++)
            {
                if (All[id].Rarity != Rarity.Legendary || !Available(id, role, roleAbility, taken))
                    continue;
                if (All[id].Boss == boss)
                    own.Add(id);
                else
                    others.Add(id);
            }
            while (own.Count < 3 && others.Count > 0)
            {
                int k = rng.Next(others.Count);
                own.Add(others[k]);
                others.RemoveAt(k);
            }
            while (own.Count < 3)
                own.Add(own.Count > 0 ? own[0] : 0);  // everything maxed: unreachable in practice
            return new[] { own[0], own[1], own[2] };
        }

        /// The card text for this weapon: Multishot says what an extra projectile does for it.
        public static string DescFor(UpgradeDef u, WeaponDef weapon)
        {
            if (weapon == null || weapon.ExtraDesc == null)
                return u.Desc;
            if (u.Name == "Multishot")
                return weapon.Name + ": " + weapon.ExtraDesc;
            string knight;
            if (weapon.Melee && KnightDesc.TryGetValue(u.Name, out knight))
                return knight;
            return u.Desc;
        }

        /// What shot-based upgrades do for a Knight's melee weapon instead.
        static readonly Dictionary<string, string> KnightDesc = new Dictionary<string, string>
        {
            { "Piercing Rounds", "Your swings reach 10% further, and sword waves pass through one more enemy" },
            { "Barrage", "Every swing also throws a sword wave, fanned out to the side. Stack it into a wall of steel" },
            { "Homing Rounds", "Your sword waves bend toward enemies close ahead of them (10% less wave damage per stack). Swings aren't affected" },
            { "Ricochet", "Your sword waves bounce to another nearby enemy after a hit (75% damage). +1 bounce per stack" },
            { "Explosive Rounds", "Your hits explode, dealing 30% damage to enemies nearby" },
            { "Velocity Rounds", "Sword waves fly 30% faster and farther, and everything you do deals 15% more damage" },
        };

        public static Rgba RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return new Rgba(0.35f, 0.9f, 0.4f);
                case Rarity.Rare: return new Rgba(0.35f, 0.6f, 1f);
                case Rarity.VeryRare: return new Rgba(0.8f, 0.4f, 1f);
                case Rarity.Legendary: return new Rgba(1f, 0.6f, 0.15f);
                case Rarity.Mod: return new Rgba(0.25f, 0.95f, 0.85f);
                default: return new Rgba(0.85f, 0.85f, 0.9f);
            }
        }

        public static string RarityName(Rarity r) => r == Rarity.VeryRare ? "VERY RARE" : r == Rarity.Mod ? "WEAPON MOD" : r.ToString().ToUpperInvariant();

        public static int Count(List<int> taken, int id)
        {
            int n = 0;
            foreach (int t in taken)
                if (t == id)
                    n++;
            return n;
        }

        /// The very rare class ability cards, added after the rest: for every role and class, a card for each of
        /// its Space abilities. A player is offered the two they didn't pick (see Available).
        static UpgradeDef[] WithClassAbilities(UpgradeDef[] basic)
        {
            var all = new List<UpgradeDef>(basic);
            foreach (var id in RoleAbilities)
            {
                var a = Ability(id);
                all.Add(new UpgradeDef
                {
                    Rarity = Rarity.VeryRare, Grants = id, ClassAbility = true, Weight = 3, Name = a.Name, Desc = a.Desc,
                });
            }
            return all.ToArray();
        }

        /// Which role and class a Space ability belongs to (each belongs to exactly one), or -1.
        static int KitOf(AbilityId id)
        {
            int i = Array.IndexOf(RoleAbilities, id);
            return i < 0 ? -1 : i / RoleAbilityChoices;
        }

        /// Do you have this ability: as your Space pick, or from a card that put it on Q, E or Shift?
        public static bool HasAbility(AbilityId id, AbilityId roleAbility, List<int> taken)
        {
            if (id == roleAbility)
                return true;
            foreach (int t in taken)
                if (All[t].Grants == id)
                    return true;
            return false;
        }

        /// Can this upgrade be offered to a player with these picks and role?
        public static bool Available(int id, Role role, AbilityId roleAbility, List<int> taken)
        {
            var u = All[id];
            if (u.Role.HasValue && u.Role.Value != role)
                return false;
            if (u.RequiresAbility != AbilityId.None && !HasAbility(u.RequiresAbility, roleAbility, taken))
                return false;
            if (u.ClassAbility && (u.Grants == roleAbility || KitOf(u.Grants) != KitOf(roleAbility)))
                return false;  // only your own class's other two Space abilities
            if (Count(taken, id) >= (u.Grants != AbilityId.None ? 1 : u.MaxStacks))
                return false;  // each bonus ability can only be owned once
            int grants = 0;
            foreach (int t in taken)
                if (All[t].Grants != AbilityId.None)
                    grants++;
            if (u.Grants != AbilityId.None && grants >= BonusSlots)
                return false;
            if (u.NeedsExtraAbility && grants == 0)
                return false;
            return true;
        }

        /// Roll three different upgrades. Higher levels make rare cards a bit likelier.
        public static int[] RollOffer(Random rng, Role role, AbilityId roleAbility, List<int> taken, int level)
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
                        if ((int)All[id].Rarity == want && !used.Contains(id) && Available(id, role, roleAbility, taken))
                            for (int k = 0; k < All[id].Weight; k++)  // class ability cards count triple
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
