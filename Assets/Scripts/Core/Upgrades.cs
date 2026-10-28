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
        None = 255,
    }

    public class AbilityDef
    {
        public string Name, Desc;
        public float Cooldown;
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
        public int HaloEvery, KillRing, SplitShots;
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

        public static readonly AbilityDef[] Abilities =
        {
            new AbilityDef { Name = "Barrier", Cooldown = 12f, Desc = "A bubble that stops incoming enemy bullets. Bosses can break it (it soaks 360 of their damage)." },
            new AbilityDef { Name = "Mend Pulse", Cooldown = 10f, Desc = "Heal everyone nearby and revive downed allies." },
            new AbilityDef { Name = "Dash", Cooldown = 3.5f, Desc = "Quick dash with brief invulnerability." },
            new AbilityDef { Name = "Shockwave", Cooldown = 14f, Desc = "Erase enemy bullets around you and damage nearby enemies." },
            new AbilityDef { Name = "Overdrive", Cooldown = 18f, Desc = "Double fire rate for 5 seconds." },
            new AbilityDef { Name = "Blink", Cooldown = 7f, Desc = "Teleport toward your cursor." },
            new AbilityDef { Name = "Nova", Cooldown = 9f, Desc = "Fire a ring of 24 heavy shots." },
            new AbilityDef { Name = "Turret", Cooldown = 20f, Desc = "Deploy a turret that shoots for 10 seconds." },
            new AbilityDef { Name = "Roar", Cooldown = 14f, Desc = "Every enemy targets you for 5 s, and you take 40% less damage meanwhile." },
            new AbilityDef { Name = "Bulldoze", Cooldown = 9f, Desc = "Charge forward, damaging and knocking back everything in your path." },
            new AbilityDef { Name = "Haven", Cooldown = 16f, Desc = "Place a healing zone for 6 s. Allies inside regenerate fast and revive 3x quicker." },
            new AbilityDef { Name = "Soul Link", Cooldown = 18f, Desc = "Tether to your ally for 6 s: they take half damage and you both regenerate." },
            new AbilityDef { Name = "Cluster Grenade", Cooldown = 6f, Desc = "Throw a grenade at your cursor that explodes into shrapnel." },
            new AbilityDef { Name = "Mark for Death", Cooldown = 12f, Desc = "Mark the enemy nearest your cursor: it takes 50% more damage for 6 s." },
            // Mage
            new AbilityDef { Name = "Arcane Aegis", Cooldown = 11f, Desc = "For 3 s, enemy bullets that touch you are reflected back as your own." },
            new AbilityDef { Name = "Frost Nova", Cooldown = 12f, Desc = "Freeze every enemy near you for 3 s and deal 40 damage." },
            new AbilityDef { Name = "Flame Wall", Cooldown = 14f, Desc = "A wall of fire at your cursor for 5 s: burns enemies and blocks bullets." },
            new AbilityDef { Name = "Time Rewind", Cooldown = 22f, Desc = "Set everyone's health back to where it was 4 s ago, even undoing a downing." },
            new AbilityDef { Name = "Arcane Ward", Cooldown = 14f, Desc = "Shield you and your ally for 60 damage for 8 s." },
            new AbilityDef { Name = "Sanctify", Cooldown = 12f, Desc = "Erase enemy bullets around you and heal everyone nearby for 25." },
            new AbilityDef { Name = "Meteor", Cooldown = 10f, Desc = "Call a meteor at your cursor: after a short warning it hits for 150 damage." },
            new AbilityDef { Name = "Blizzard", Cooldown = 14f, Desc = "A storm at your cursor for 5 s that slows and damages enemies." },
            new AbilityDef { Name = "Arcane Surge", Cooldown = 16f, Desc = "For 5 s your shots deal 60% more damage and pierce 2 more enemies." },
            // Summoner
            new AbilityDef { Name = "Golem Wall", Cooldown = 13f, Desc = "Raise three stone walls in front of you for 6 s that block bullets." },
            new AbilityDef { Name = "Earthquake", Cooldown = 10f, Desc = "You and every minion you have slam the ground, damaging and slowing enemies." },
            new AbilityDef { Name = "Guardian Spirit", Cooldown = 18f, Desc = "A guardian follows you for 8 s and absorbs every bullet near it." },
            new AbilityDef { Name = "Spirit Swarm", Cooldown = 14f, Desc = "Release 5 healing sprites for 6 s." },
            new AbilityDef { Name = "Phoenix", Cooldown = 22f, Desc = "A phoenix fights for 10 s. If your ally goes down, it revives them instantly." },
            new AbilityDef { Name = "Soul Lantern", Cooldown = 20f, Desc = "For 5 s, allies near the lantern can't go down." },
            new AbilityDef { Name = "Bat Swarm", Cooldown = 12f, Desc = "Release 8 bats that hunt enemies for 6 s." },
            new AbilityDef { Name = "Sacrifice", Cooldown = 10f, Desc = "All your minions explode; your companions return 3 s later." },
            new AbilityDef { Name = "Alpha Call", Cooldown = 14f, Desc = "Your minions frenzy for 6 s: faster, and they attack twice as often." },
            // Ultimates. Their cooldowns aren't shortened by cooldown upgrades.
            new AbilityDef { Name = "Fortress", Cooldown = 70f, Desc = "For 8 s every enemy targets you and you take 75% less damage. Erases nearby bullets and shoves enemies away." },
            new AbilityDef { Name = "Glacial Tomb", Cooldown = 75f, Desc = "Erase every enemy bullet and freeze every enemy solid for 4 s (bosses take 250 damage instead)." },
            new AbilityDef { Name = "Titan", Cooldown = 80f, Desc = "Summon a stone titan for 15 s that smashes enemies and blocks bullets (it soaks 1000 boss damage)." },
            new AbilityDef { Name = "Lifeline", Cooldown = 90f, Desc = "Instantly revive and fully heal the whole party, with 3 s of invulnerability." },
            new AbilityDef { Name = "Sanctum", Cooldown = 80f, Desc = "A holy dome for 7 s: allies inside heal fast and can't go down, and bullets can't get in." },
            new AbilityDef { Name = "World Tree", Cooldown = 80f, Desc = "Grow a great tree for 10 s that heals allies, entangles enemies around it and sends out healing sprites." },
            new AbilityDef { Name = "Bullet Storm", Cooldown = 60f, Desc = "For 6 s you fire three times as fast, with +2 extra projectiles." },
            new AbilityDef { Name = "Meteor Storm", Cooldown = 70f, Desc = "Twelve meteors rain down on enemies over 3 s, 150 damage each." },
            new AbilityDef { Name = "Legion", Cooldown = 70f, Desc = "Summon 6 wolves and 6 bats for 10 s, and all your minions frenzy." },
            // Knight
            new AbilityDef { Name = "Shield Wall", Cooldown = 12f, Desc = "Raise your shield for 4 s: it stops every enemy bullet coming at your front (120°), even red aimed shots. Bosses can break it (it soaks 400 of their damage)." },
            new AbilityDef { Name = "Leap Slam", Cooldown = 9f, Desc = "Leap to your cursor (up to 320 px), untouchable in the air. Landing deals 50 damage around you and stuns enemies for 1 s." },
            new AbilityDef { Name = "Unbreakable", Cooldown = 16f, Desc = "For 5 s you take 50% less damage and your hits heal you for 15% of the damage they deal." },
            new AbilityDef { Name = "Consecrate", Cooldown = 14f, Desc = "Bless the ground at your feet for 6 s: allies inside heal 12 HP/s and enemies burn for 20 damage a second." },
            new AbilityDef { Name = "Guardian's Oath", Cooldown = 15f, Desc = "Shield the ally nearest your cursor for 100 damage (and yourself for 50) for 6 s." },
            new AbilityDef { Name = "Lay on Hands", Cooldown = 18f, Desc = "Heal the most hurt ally within 300 px (or yourself) for 70, or instantly revive them at half health if they're down." },
            new AbilityDef { Name = "Whirlwind", Cooldown = 10f, Desc = "Spin for 2 s, hitting everything within 110 px for 12 every 0.2 s and slicing small bullets out of the air." },
            new AbilityDef { Name = "Lunge", Cooldown = 6f, Desc = "Dash 260 px toward your cursor, hitting everything you pass for 35. Two charges." },
            new AbilityDef { Name = "Battle Fury", Cooldown = 16f, Desc = "For 6 s you swing 40% faster and deal 25% more damage." },
            new AbilityDef { Name = "Last Stand", Cooldown = 70f, Desc = "For 8 s you can't drop below 1 HP, every enemy targets you, and you deal 50% more damage." },
            new AbilityDef { Name = "Hallowed Blade", Cooldown = 80f, Desc = "A giant holy sword falls at your cursor: 200 damage around it, then the ground is blessed for 8 s (allies heal 15 HP/s, enemies burn)." },
            new AbilityDef { Name = "Blade Tempest", Cooldown = 65f, Desc = "For 6 s every swing also hurls three piercing sword beams." },
            // Ninja
            new AbilityDef { Name = "Shadow Clone", Cooldown = 14f, Desc = "Leave a decoy for 5 s that every enemy targets. It bursts for 60 damage when it ends." },
            new AbilityDef { Name = "Smoke Bomb", Cooldown = 14f, Desc = "A 140 px cloud at your feet for 5 s: enemy bullets inside slow to half speed, and allies inside take 30% less damage." },
            new AbilityDef { Name = "Iron Body", Cooldown = 12f, Desc = "For 2 s you take 80% less damage, then a shockwave hits everything within 140 px for 50." },
            new AbilityDef { Name = "Healing Mist", Cooldown = 15f, Desc = "A mist follows you for 6 s: allies inside heal 10 HP/s." },
            new AbilityDef { Name = "Substitution", Cooldown = 12f, Desc = "Swap places with the ally nearest your cursor and shield them for 60 for 6 s. Alone: slip to your cursor and shield yourself." },
            new AbilityDef { Name = "Spirit Ward", Cooldown = 18f, Desc = "Plant a talisman at your cursor for 8 s: allies near it regenerate 6 HP/s and revive 3x faster." },
            new AbilityDef { Name = "Shadow Step", Cooldown = 7f, Desc = "Teleport behind the enemy nearest your cursor (up to 450 px away) and strike it for 80." },
            new AbilityDef { Name = "Vanish", Cooldown = 16f, Desc = "For 3 s enemies lose track of you. Your next hit (within 4 s) deals double damage." },
            new AbilityDef { Name = "Blade Fan", Cooldown = 9f, Desc = "Throw 12 kunai in a ring around you, 18 damage each." },
            new AbilityDef { Name = "Thousand Clones", Cooldown = 70f, Desc = "Five clones surround you for 8 s: every enemy targets them, and each bursts for 60 when it ends." },
            new AbilityDef { Name = "Blossom Storm", Cooldown = 80f, Desc = "For 5 s petals heal the whole party 20 HP/s and erase enemy bullets near every ally." },
            new AbilityDef { Name = "Death Lotus", Cooldown = 65f, Desc = "Strike up to 8 enemies near your cursor in a flurry of cuts, 100 damage each." },
            // Blood Mage: every Space ability costs health
            new AbilityDef { Name = "Blood Shell", Cooldown = 9f, HealthCost = 0.15f, Desc = "Pay 15% of your max health for a shield worth three times as much, for 5 s." },
            new AbilityDef { Name = "Crimson Pact", Cooldown = 10f, HealthCost = 0.1f,
                Desc = "Pay 10% of your max health: every enemy targets you for 4 s, then the damage you took meanwhile bursts back out at 150% within 220 px." },
            new AbilityDef { Name = "Exsanguinate", Cooldown = 8f, HealthCost = 0.1f, Desc = "Pay 10% of your max health to pull enemies within 220 px toward you and drain 20 health from each into you." },
            new AbilityDef { Name = "Blood Gift", Cooldown = 8f, HealthCost = 0.25f, CostsCurrent = true,
                Desc = "Pay 25% of your current health: the most hurt ally heals twice as much. Alone, it becomes a shield for you." },
            new AbilityDef { Name = "Siphon Field", Cooldown = 11f, HealthCost = 0.1f,
                Desc = "Pay 10% of your max health for a 140 px field at your feet for 6 s: enemies inside lose 10 HP/s and allies inside heal 10 HP/s." },
            new AbilityDef { Name = "Martyr", Cooldown = 16f, HealthCost = 0.4f,
                Desc = "Pay 40% of your max health to revive the downed ally nearest your cursor, anywhere, at 50% health. If no one is down, the most hurt ally heals twice what you paid." },
            new AbilityDef { Name = "Blood Boil", Cooldown = 8f, HealthCost = 0.2f, Desc = "Pay 20% of your max health: enemies within 260 px boil and burst after 1 s for three times what you paid." },
            new AbilityDef { Name = "Frenzy Pact", Cooldown = 11f, HealthCost = 0.05f,
                Desc = "Pay 5% of your max health: for 6 s you fire 50% faster and heal 10% of the damage you deal, but lose 3 HP/s." },
            new AbilityDef { Name = "Hemo Nova", Cooldown = 6f, HealthCost = 0.1f, Desc = "Pay 10% of your max health to fire a ring of 16 blood bolts (20 each). Each that hits heals you 3." },
            new AbilityDef { Name = "Blood Rite", Cooldown = 75f, Desc = "For 8 s every enemy targets you and you take 60% less damage. The damage you take heals your allies." },
            new AbilityDef { Name = "Sanguine Rain", Cooldown = 80f, Desc = "For 6 s, every hit any ally lands heals them for 20% of its damage." },
            new AbilityDef { Name = "Crimson Apotheosis", Cooldown = 65f,
                Desc = "For 6 s you can't drop below 1 health and Blood Price can double your damage. When it ends, heal for half the damage you dealt." },
        };

        /// Each role and class has its own ultimate; it can't be changed.
        static readonly AbilityId[] Ultimates =
        {
            AbilityId.Fortress, AbilityId.GlacialTomb, AbilityId.Titan, AbilityId.LastStand, AbilityId.ThousandClones, AbilityId.BloodRite,  // Tank: Soldier, Mage, Summoner, Knight, Ninja, Blood Mage
            AbilityId.Lifeline, AbilityId.Sanctum, AbilityId.WorldTree, AbilityId.HallowedBlade, AbilityId.BlossomStorm, AbilityId.SanguineRain,  // Healer
            AbilityId.BulletStorm, AbilityId.MeteorStorm, AbilityId.Legion, AbilityId.BladeTempest, AbilityId.DeathLotus, AbilityId.CrimsonApotheosis,  // DPS
        };

        public static AbilityId Ultimate(Role r, Kit kit) => Ultimates[(int)r * Defs.KitCount + (int)kit];

        /// The three Space abilities for each role and kit, picked in the lobby.
        static readonly AbilityId[] RoleAbilities =
        {
            AbilityId.Barrier, AbilityId.Roar, AbilityId.Bulldoze,                // Tank: Soldier
            AbilityId.ArcaneAegis, AbilityId.FrostNova, AbilityId.FlameWall,      // Tank: Mage
            AbilityId.GolemWall, AbilityId.Earthquake, AbilityId.GuardianSpirit,  // Tank: Summoner
            AbilityId.ShieldWall, AbilityId.LeapSlam, AbilityId.Unbreakable,      // Tank: Knight
            AbilityId.ShadowClone, AbilityId.SmokeBomb, AbilityId.IronBody,       // Tank: Ninja
            AbilityId.BloodShell, AbilityId.CrimsonPact, AbilityId.Exsanguinate,  // Tank: Blood Mage
            AbilityId.MendPulse, AbilityId.Haven, AbilityId.SoulLink,             // Healer: Soldier
            AbilityId.TimeRewind, AbilityId.ArcaneWard, AbilityId.Sanctify,       // Healer: Mage
            AbilityId.SpiritSwarm, AbilityId.Phoenix, AbilityId.SoulLantern,      // Healer: Summoner
            AbilityId.Consecrate, AbilityId.GuardiansOath, AbilityId.LayOnHands,  // Healer: Knight
            AbilityId.HealingMist, AbilityId.Substitution, AbilityId.SpiritWard,  // Healer: Ninja
            AbilityId.BloodGift, AbilityId.SiphonField, AbilityId.Martyr,         // Healer: Blood Mage
            AbilityId.Dash, AbilityId.Grenade, AbilityId.Mark,                    // DPS: Soldier
            AbilityId.Meteor, AbilityId.Blizzard, AbilityId.ArcaneSurge,          // DPS: Mage
            AbilityId.BatSwarm, AbilityId.Sacrifice, AbilityId.AlphaCall,         // DPS: Summoner
            AbilityId.Whirlwind, AbilityId.Lunge, AbilityId.BattleFury,           // DPS: Knight
            AbilityId.ShadowStep, AbilityId.Vanish, AbilityId.BladeFan,           // DPS: Ninja
            AbilityId.BloodBoil, AbilityId.FrenzyPact, AbilityId.HemoNova,        // DPS: Blood Mage
        };

        public static AbilityId RoleAbility(Role r, Kit kit, int choice) =>
            RoleAbilities[((int)r * Defs.KitCount + (int)kit) * RoleAbilityChoices + (choice >= 0 && choice < RoleAbilityChoices ? choice : 0)];

        public const int RoleAbilityChoices = 3;

        public static AbilityDef Ability(AbilityId id) => Abilities[(int)id];


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
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Boss, MaxStacks = 3, Name = "Warden's Halo", Desc = "Every 4th volley also fires a ring of 8 shots around you",
                Apply = s => s.HaloEvery = s.HaloEvery == 0 ? 4 : Math.Max(2, s.HaloEvery - 1) },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Boss, MaxStacks = 3, Name = "Iron Volley", Desc = "+1 projectile per volley and +25% damage",
                Apply = s => { s.ExtraShots += 1; s.DmgMult += 0.25f; } },
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

        /// The card text for this weapon: Multishot and Iron Volley say what an extra projectile does for it.
        public static string DescFor(UpgradeDef u, WeaponDef weapon)
        {
            if (weapon == null || weapon.ExtraDesc == null)
                return u.Desc;
            if (u.Name == "Multishot")
                return weapon.Name + ": " + weapon.ExtraDesc;
            if (u.Name == "Iron Volley")
                return weapon.Name + ": " + weapon.ExtraDesc + ". Also +25% damage";
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
                    Rarity = Rarity.VeryRare, Grants = id, ClassAbility = true, Weight = 2, Name = a.Name, Desc = a.Desc,
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
                            for (int k = 0; k < All[id].Weight; k++)  // class ability cards count double
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
