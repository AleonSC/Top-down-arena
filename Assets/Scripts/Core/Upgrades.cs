using System;
using System.Collections.Generic;

namespace Arena
{
    public enum Rarity : byte { Common, Uncommon, Rare, VeryRare, Legendary, Mod, Milestone }  // Mod: weapon mods, picked after each boss; Milestone: their own pool, offered every 10 levels

    public enum AbilityId : byte
    {
        Barrier, MendPulse, PoisonDart, Shockwave, Overdrive, Blink, Nova, Turret,
        Roar, Bulldoze, Adrenaline, Medkit, Airstrike, Mark,  // Poison Dart (PoisonDart.cs) took Dash's place (and number); Medkit (Medkit.cs) took Soul Link's place (and number); Adrenaline (Adrenaline.cs) took Haven's; Airstrike (Airstrike.cs) the Cluster Grenade's
        // Mage
        ArcaneAegis, FrostNova, FlameWall, HealingRain, EnergyWard, Sanctify, Meteor, Blizzard, Tsunami,  // Tsunami (Tsunami.cs) took Arcane Surge's place (and number)
        // Summoner
        GolemWall, Earthquake, GuardianSpirit, SpiritSwarm, Phoenix, SoulLantern, BatSwarm, Hydra, AlphaCall,
        // Ultimates (F): one per role and class
        Fortress, GlacialTomb, Titan, Lifeline, Sanctum, WorldTree, BulletStorm, BulletRain, Legion,
        // Knight (Space), then the Knight ultimates
        ShieldWall, LeapSlam, CavalryCall, Consecrate, GuardiansOath, LayOnHands, Whirlwind, Lunge, BattleFury,
        LastStand, HallowedBlade, BladeTempest,
        // Ninja (Space), then the Ninja ultimates
        ShadowClone, SmokeBomb, IronBody, HealingDash, Substitution, SpiritWard, ShadowStep, Vanish, BladeFan,
        ThousandClones, BlossomStorm, DeathLotus,
        // Blood Mage (Space), then the Blood Mage ultimates
        BloodShell, CrimsonPact, Exsanguinate, BloodGift, SiphonField, Martyr, BloodBoil, FrenzyPact, HemoNova,
        BloodRite, SanguineRain, CrimsonApotheosis,
        StunGun,  // the Warden's legendary: a Q / E / Shift ability (Stun.cs)
        RoyalDive,  // the Hive Queen's legendary: a Q / E / Shift ability (QueenLoot.cs)
        ScytheThrow,  // the Twin Reapers' legendary: a Q / E / Shift ability (ReaperLoot.cs)
        JuggernautSuit,  // the Tank Soldier's second ultimate (Juggernaut.cs), picked on the lobby's Ultimate row
        Bloodbath,    // the Tank Hemomancer's second ultimate (Bloodbath.cs), picked with LobbyEntry.Ult
        LightningRod,    // the Tank Elementalist's second ultimate (LightningRod.cs), picked on the lobby's Ultimate row
        BonePhalanx,  // the Tank Summoner's second ultimate (BonePhalanx.cs), picked on the lobby's Ultimate row
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
        public int UltKey;             // an ultimate's key on the lobby's Ultimate row: 0 the default one, 1-3 the ones picked instead (LobbyEntry.Ult)
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
        public int VectorCap;  // the Expanding arena: vectors you can hold beyond the base (Docs/VectorBuilding.md)
        public int Fan;  // Barrage: extra copies of every shot, fanned out to the sides
        public int RoleCharges, ExtraCharges;
        public float BarrierRadiusMult = 1f, BarrierBonusTime, BarrierCapacityMult = 1f;
        public float PulseHealMult = 1f, PulseRangeMult = 1f, AuraRangeMult = 1f, AuraHealMult = 1f;
        public float VenomMult = 1f, VenomReachBonus;  // Virulence (Poison Dart, PoisonDart.cs)
        public float RoarBonusTime, RoarExtraReduction, BulldozeMult = 1f;
        public float AdrenalineBonusTime, LinkHealMult = 1f;
        public float GrenadeRadiusMult = 1f, MarkBonus;
        // Knight abilities
        public float WallCapMult = 1f, WallArcMult = 1f;
        public float SlamRadiusMult = 1f, SlamStunBonus;
        public float UnbreakBonus, CavalryDmgBonus;  // Cavalry Call: extra damage blocked while the guard is up, extra damage per lance
        public float HolyRangeMult = 1f, HolySwiftBonus, OathCut, LayMult = 1f;
        public float MercyBelow, MercyReach, GauntletRange = 1f, GauntletDamage = 1f, SteelDamage, SteelReach;  // the DPS Knight's cards: Coup de Grace, Iron Grip, Forged Edge
        // Ninja abilities
        public float CloneBonusTime, CloneHpMult = 1f, SmokeRadiusMult = 1f, SmokeBonusTime, IronBonusTime, IronWaveMult = 1f;
        public float MistHealMult = 1f, MistRadiusMult = 1f, SubShieldMult = 1f, SubBonusTime, WardRadiusMult = 1f, WardBonusTime;
        public float StepDmgMult = 1f, StepRangeMult = 1f, VanishBonusTime, AmbushMult = 2f;
        public int FanExtraKunai;
        // Blood Mage abilities
        public float ShellBonus, WallLifeBonus, WallRampBonus, DrainMult = 1f, DrainRadiusMult = 1f;
        public float GiftMult = 1f, FreezeBonusTime, MartyrBonus, MartyrInvuln;
        public float SprayMult = 1f, SprayReach = 1f;          // Arterial Spray: High Pressure
        public float DebtRepay, DebtBonusTime;                 // Blood Debt: Usury
        public float RushReach = 1f, RushRefund;               // Vein Rush: Crimson Tide
        public bool ChalkWard;             // Warding Ink: Spirit Chalk blocks enemy bullets (at the cost of wearing the ink away)
        // Elementalist abilities (Docs/UpgradePools.md step 5)
        public float AegisBonusTime, AegisReflectBonus;                    // Aeroaegis: Eye of the Storm
        public float NovaFreezeBonus, NovaRangeMult = 1f;                  // Frost Nova: Permafrost
        public float FlameWallBonusTime, FlameWallBurnMult = 1f;           // Flame Wall: Inferno
        public float RainRadiusMult = 1f, RainHealMult = 1f;               // Healing Rain: Monsoon
        public float EnergySoakMult = 1f, EnergyRepayBonus;                // Energy Ward: Overcharged Ward
        public float LumenBonusTime, LumenHealMult = 1f;                   // Lumen Ward: Dawnlight
        public float MeteorDmgMult = 1f, MeteorRadiusMult = 1f;            // Meteor: Cataclysm
        public float BlizzardRadiusMult = 1f, BlizzardBonusTime;           // Blizzard: Whiteout
        public float TsunamiWidthMult = 1f, WetBonusTime;                  // Tsunami: Riptide
        // Summoner abilities
        public float GolemHpMult = 1f;                                     // Golem Wall: Basalt Hide
        public float QuakeBonusTime, QuakeDmgMult = 1f;                    // Earthquake: Deep Tremor
        public float PactHpMult = 1f, PactReturnCut;                       // Force Pact: Kindred Bond
        public float SpriteHealMult = 1f, SpriteBonusTime;                 // Spirit Swarm: Sprite Choir
        public float PhoenixBonusTime, PhoenixReviveBonus;                 // Phoenix: Rebirth Flame
        public float BatDmgMult = 1f, BatBonusTime;                        // Bat Swarm: Blood Bats
        public float HydraSlow, HydraBonusTime;                            // Hydra: Venom Glands
        public float ScentBonusTime; public int ScentStartBonus;           // Alpha Call: Pack Leader
        public int MedkitExtraOrbs; public float MedkitHealBonus;          // Medkit: Field Surgeon (Soldier)
        // Class cards (Kit; Docs/UpgradePools.md step 6)
        public float OrbHealMult = 1f, BonusCdMult = 1f;                   // Soldier: Quartermaster (orbs you pick up; Q/E/Shift cooldowns)
        public float SurgeRate;                                            // Elementalist: Arcane Surge (fire rate for SurgeTime after a Space cast)
        public float MinionDmgMult = 1f, MinionToughMult = 1f;             // Summoner: Pack Bond
        public float CutReachMult = 1f;                                    // Knight: Plate Armour (how far out swings cut bullets)
        public float GhostCut, GhostTime;                                  // Ninja: Ghost Step (less damage for GhostTime after a dodge)
        public float HealthCostMult = 1f;                                  // Blood Mage: Bloodthirst (health paid for abilities)
        // Knight weapon cards (Weapons; Docs/UpgradePools.md step 7)
        public float HammerAreaMult = 1f, HammerStunBonus;                 // Warhammer: Quake Hammer
        public float BreachKnockMult = 1f, ImpactMult = 1f;                // Lance: Breacher
        public float HalberdReachMult = 1f, HalberdSweepBonus;             // Halberd: Long Haft (sweep bonus in radians, each side)
        public float StaffHealBonus; public int StaffExtraStacks;          // Quarterstaff: Pilgrim's Staff
        public float LightGainMult = 1f, SunlanceHealMult = 1f;            // Lightbringer: Daybreak
        public float IncenseSizeMult = 1f, IncenseBonusTime;               // Censer: Thick Incense
        public float FangReachMult = 1f, FangDmgMult = 1f;                 // Twin Blades: Whirling Sabres
        public float GreatRendMult = 1f, TemperGainMult = 1f;              // Greatsword: Tempered Blade
        public int SkewerBonus; public float TempoHoldBonus;               // Rapier: Duelist's Edge
        public float KnuckleDashMult = 1f, KnuckleStunBonus;               // Arc Knuckles: Live Wire
        // Ninja weapon cards (Docs/UpgradePools.md step 8)
        public float KusariReachMult = 1f, KusariStunBonus;                // Kusarigama: Heavy Weight
        public float BoKnockMult = 1f, BoSpinDmgMult = 1f;                 // Bo Staff: Iron Bo
        public float FumaLoopMult = 1f, FumaOrbitMult = 1f;                // Fuma Shuriken: Long Loop
        public float DoseMult = 1f;                                        // Medicine Darts: Potent Dose (poison and healing)
        public float FanReachMult = 1f, FanHealMult = 1f;                  // Spirit Fan: Gale Fan
        public float CharmBurstMult = 1f, CharmHealMult = 1f;              // Ofuda: Blessed Paper
        public float StarStickCut, StarReturnMult = 1f;                    // Shuriken: Barbed Stars
        public float BurstPushMult = 1f, CaltropBonusTime;                 // Katana: Honed Edge
        public float KunaiBlinkMult = 1f, KunaiMarkBonus;                  // Kunai: Shadow Mark
        // Blood Mage weapon cards (Docs/UpgradePools.md step 9)
        public float LashReachMult = 1f, ClotCutBonus;                     // Blood Lash: Crimson Whip
        public float PoolSizeMult = 1f, PoolBonusTime;                     // Clot Cannon: Deep Pool
        public float LeechDrainMult = 1f, LeechStealBonus;                 // Leech Tendrils: Hungry Tendrils
        public float TransfuseMult = 1f;                                   // Transfusion Darts: Generous Blood
        public float ReserveGainMult = 1f, CoatHealMult = 1f;              // Sanguine Orb: Rich Blood
        public float VeinStealBonus;                                       // Vein Tether: Lifeline
        public int HemoHitCut; public float HemoRadiusMult = 1f;           // Blood Bolts: Hemophilia
        public float ImplodeReachMult = 1f, ImplodeDmgMult = 1f;           // Implosion: Event Horizon
        public int SpearPierceBonus; public float SpearPickupBonus;        // Bone Spear: Marrow
        // Summoner weapon cards (Docs/UpgradePools.md step 10)
        public float StaffGolemTough = 1f, BoulderSplashMult = 1f;         // Golem Staff: Granite Golem
        public float CageBonusTime, SkeletonRateMult = 1f;                 // Bone Guard: Ossuary
        public float ClawReachMult = 1f, ClawBleedMult = 1f;               // Bear Totem: Deep Claws
        public int FireflyHopBonus; public float StaffSpriteHealMult = 1f; // Sprite Staff: Wildfire Seeds
        public float RootReachMult = 1f, RootHealMult = 1f;                // Treant Rod: Deep Roots
        public float TakedownMult = 1f, TakedownStunBonus;                 // Wolf Whistle: Alpha Pack
        public int WispExtra;                                              // Wisp Lantern: Will-o'-Wisp
        public int ImpExtra; public float ImpRateMult = 1f;                // Imp Caller: Imp Horde

        // Legendary weapon upgrades from bosses.
        public int Guards, Searchlight, SplitShots;  // Warden's Guard companions and Searchlight stacks (Stun.cs)
        public int HiveDrones, Bombers;
        public int TwinScythes;                 // the Twin Reapers' Twin Scythes stacks (ReaperLoot.cs)
        public float ReapBelow, ReapBossBonus;  // Final Reap: reaps regular enemies below this health; more damage to bosses below 30% (ReaperLoot.cs)  // the Hive Queen's Hive Drones (how many) and Bomber Escort (bombers a launch) (QueenLoot.cs)
        public float Lifesteal, BroodChance, ExecuteBonus, CritChance, KillHeal, ImpactSplash, SlowOnHit, SlowedBonus;

        // Weapon mods, one picked after each boss (and its legendary).
        public float Homing;              // turn rate (radians per second) toward the nearest enemy
        public int Ricochet;              // bounces to another enemy on hit
        public float BurnFrac;            // a hit adds this much of its damage to the enemy's burn, burned off over 3 s
        public float CryoSlow, FreezeChance;
        public float ShockChance;
        public float ExplosiveRadius;
        public float ShotSpeedMult = 1f;
        public float ShotSizeMult = 1f;   // Magnum Rounds: your shots are bigger (and area weapons' areas wider)

        // Parry upgrades.
        public float ParryCapacityMult = 1f, ParryArcMult = 1f, PerfectBonus, ReflectMult = 1f, CounterFireRate, MirrorChance;
        public bool ParryNoSlow;

        // Milestone upgrades (offered every 10 levels; Upgrades.Milestones).
        public float XpMult = 1f;          // Scholar: XP earned
        public float ExtraPickChance;      // Second Helping: a level-up may give an extra pick
        public int OfferExtra;             // Wide Choice: more cards in every level-up offer
        public float RarityLuck;           // Fortune: rarer cards turn up more often
        public int BossRerolls;            // Reroll Cache: more rerolls after every boss
        public float MilestoneChance;      // Milestone Compass: a level-up may also offer a milestone pick
    }

    /// Who a card offer is for (Docs/UpgradePools.md step 2): every offer and Available take one, so no filter can be
    /// left out. In a match build it with Game.OfferFor(p); tests and build simulations use the constructor.
    public class OfferCtx
    {
        public readonly Role Role;
        public readonly int Kits;          // the classes whose cards you're offered, one bit per Kit (HasKit)
        public readonly AbilityId RoleAbility;
        public readonly List<int> Taken;
        public readonly WeaponId? Weapon;  // null: no weapon (weapon cards are never offered)
        public readonly bool Expanding;    // the Expanding arena (the vector cards)

        /// `kit` defaults to the class of the Space ability. A Wildcard has no class of its own: it counts as the class
        /// of its Space ability, of its weapon and of its second ability (`second`, on Q/E/Shift), so any of those
        /// classes' cards can turn up (Docs/UpgradePools.md step 3).
        public OfferCtx(Role role, AbilityId roleAbility, List<int> taken, WeaponId? weapon = null, bool expanding = false, Kit? kit = null,
            AbilityId second = AbilityId.None)
        {
            Role = role; RoleAbility = roleAbility; Taken = taken; Weapon = weapon; Expanding = expanding;
            if (role != Role.Wildcard)
                Kits = Bit(kit ?? KitOf(roleAbility));
            else
            {
                Kits = Bit(KitOf(roleAbility));
                if (weapon.HasValue && Defs.WeaponOf(weapon.Value) != null)
                    Kits |= Bit(Defs.WeaponOf(weapon.Value).Kit);
                if (second != AbilityId.None)
                    Kits |= Bit(KitOf(second));
            }
        }

        static int Bit(Kit k) => 1 << (int)k;
        static Kit KitOf(AbilityId a) => a != AbilityId.None ? Upgrades.Ability(a).Kit : Kit.Soldier;

        /// Are this class's cards offered here?
        public bool HasKit(Kit k) => (Kits & Bit(k)) != 0;

        /// A player's offers: their role, class (a Wildcard: its gear's classes), Space ability, picks and weapon.
        public static OfferCtx Of(Player p, bool expanding) =>
            new OfferCtx(p.Role, p.RoleAbility, p.Taken, p.Weapon != null ? p.Weapon.Id : (WeaponId?)null, expanding, p.Kit, p.StartBonus);
    }

    public class UpgradeDef
    {
        public string Name, Desc;
        public int RerollsNow;  // picking it also gives this many rerolls on the spot (Reroll Cache)
        public int VectorsNow;  // ...and this many vectors (Architect; the Expanding arena)
        public bool NeedsExpanding;  // only offered in the Expanding arena (the vector cards)
        public Rarity Rarity;
        public int MaxStacks = 99;
        public Role? Role;                          // only offered to this role
        public AbilityId Grants = AbilityId.None;   // very rare: a new Q/E/Shift ability
        public bool NeedsExtraAbility;              // only offered once you have a bonus ability
        public AbilityId RequiresAbility = AbilityId.None;  // only offered if you have this ability (on any key)
        public WeaponId[] Weapons;                  // only offered if your weapon is one of these (Docs/UpgradePools.md step 3)
        public Kit? Kit;                            // only offered to this class (a Wildcard: any class its gear comes from)
        public bool ClassAbility;                   // very rare: one of your class's other Space abilities, for Q/E/Shift
        public int Weight = 1;                      // how many times it counts among the cards of its rarity
        public EnemyType? Boss;                     // legendary: the boss that awards it (TwinA = the Twin Reapers)
        public bool Unoffered;                      // kept (old saves, dev tools) but never offered: the Queen's and the Twin Reapers' old cards (Swarm Rounds, Royal Jelly, Brood Mother; Executioner, Twin Fang, Soul Harvest)
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
        public static readonly AbilityDef[] Abilities = Registry(Game.SoldierAbilities, Game.ElementalistAbilities, Game.SummonerAbilities,
            Game.UltimateAbilities, Game.KnightAbilities, Game.NinjaAbilities, Game.HemomancerAbilities, Game.WardenAbilities, Game.QueenAbilities, Game.ReaperAbilities, Game.SecondUltimates);

        /// Each role and class has its default ultimate (key 0 of the lobby's Ultimate row), and may have others on keys 1-3.
        static readonly AbilityId[] Ultimates = LobbyTable(true);

        /// The keys on the lobby's Ultimate row; key 0 is the default ultimate.
        public const int UltKeys = 4;

        /// A role and class's default ultimate. A Wildcard gets the DPS ones (pass the class it picked its ultimate from, UltKitOf).
        public static AbilityId Ultimate(Role r, Kit kit) => Ultimates[(int)(r == Role.Wildcard ? Role.Dps : r) * Defs.KitCount + (int)kit];

        /// The ultimate on key `choice` of the Ultimate row (LobbyEntry.Ult): the default one for 0 or a key with nothing on it yet.
        /// A Wildcard's choice is kit * UltKeys + key (any class's DPS ultimates; WildUlt), so only its key part is used here.
        public static AbilityId Ultimate(Role r, Kit kit, int choice)
        {
            if (r == Role.Wildcard)
                choice %= UltKeys;
            var alt = choice > 0 ? AltUltimate(r, kit, choice) : AbilityId.None;
            return alt != AbilityId.None ? alt : Ultimate(r, kit);
        }

        /// A Wildcard's Ultimate pick: the class it comes from times UltKeys, plus the key on that class's DPS Ultimate row.
        public static int WildUlt(Kit kit, int key) => (int)kit * UltKeys + key;

        /// Whose ultimates an Ultimate pick is from: a Wildcard's class is in the pick (WildUlt), anyone else's is their own.
        public static Kit UltKitOf(Role r, Kit kit, int choice) =>
            r == Role.Wildcard ? (Kit)Math.Max(0, Math.Min(Defs.KitCount - 1, choice / UltKeys)) : kit;

        /// The ultimate on key 1-3 of this role and class's Ultimate row, or None while that key is locked (a Wildcard: the DPS row).
        public static AbilityId AltUltimate(Role r, Kit kit, int key)
        {
            if (r == Role.Wildcard)
                r = Role.Dps;
            if (key <= 0 || key >= UltKeys)
                return AbilityId.None;
            foreach (var a in Abilities)
                if (a.Choice == AbilityDef.Ult && a.UltKey == key && a.Role == r && a.Kit == kit)
                    return a.Id;
            return AbilityId.None;
        }

        /// Keeps an Ultimate row pick to a key that has an ultimate for this role and class (else 0). A Wildcard's pick keeps
        /// its class (0-5) and a key that class's DPS row has (WildUlt).
        public static int ClampUlt(Role r, Kit kit, int choice)
        {
            if (r == Role.Wildcard)
            {
                if (choice < 0 || choice >= Defs.KitCount * UltKeys)
                    return 0;
                var wk = (Kit)(choice / UltKeys);
                return WildUlt(wk, ClampUlt(Role.Dps, wk, choice % UltKeys));
            }
            return choice > 0 && choice < UltKeys && AltUltimate(r, kit, choice) != AbilityId.None ? choice : 0;
        }

        /// The three Space abilities for each role and kit, picked in the lobby.
        static readonly AbilityId[] RoleAbilities = LobbyTable(false);

        public static AbilityId RoleAbility(Role r, Kit kit, int choice)
        {
            if (r == Role.Wildcard)  // choice = kit * 3 + choice: any class's DPS Space ability
            {
                choice = choice >= 0 && choice < Defs.WildcardChoices ? choice : 0;
                return RoleAbilities[((int)Role.Dps * Defs.KitCount + choice / RoleAbilityChoices) * RoleAbilityChoices + choice % RoleAbilityChoices];
            }
            return RoleAbilities[((int)r * Defs.KitCount + (int)kit) * RoleAbilityChoices + (choice >= 0 && choice < RoleAbilityChoices ? choice : 0)];
        }

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
                if (a.Choice == AbilityDef.CardOnly || ultimates != (a.Choice == AbilityDef.Ult) || a.UltKey != 0)
                    continue;  // (the other keys of the Ultimate row are found by AltUltimate)
                int i = ((int)a.Role * Defs.KitCount + (int)a.Kit) * per + (ultimates ? 0 : a.Choice);
                if (table[i] != AbilityId.None)
                    throw new InvalidOperationException(a.Id + " and " + table[i] + " are in the same lobby place");
                table[i] = a.Id;
            }
            int empty = Array.IndexOf(table, AbilityId.None);
            if (empty >= 0)
                throw new InvalidOperationException("No " + (Role)(empty / per / Defs.KitCount) + " " + (ultimates ? "ultimate" : "Space ability (Choice " + empty % per + ")")
                    + " for Kit." + (Kit)(empty / per % Defs.KitCount) + ": add it to that class's AbilityDef list");
            return table;
        }


        /// Every upgrade card; a card's id is its place here. The general cards, then the class ability cards, the
        /// milestone pool and the vector cards, then each class's own cards (Game.SoldierCards ... Game.HemomancerCards,
        /// beside that class's abilities). Ids are positions, so adding a card anywhere just bumps Session.ProtocolVersion
        /// (saves name their cards; Docs/UpgradePools.md step 4).
        public static readonly UpgradeDef[] All = Gather(new[]
        {
            // Common: small stat boosts
            new UpgradeDef { Rarity = Rarity.Common, Name = "Sharpened Rounds", Desc = "+10% damage", Apply = s => s.DmgMult += 0.1f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Quick Trigger", Desc = "+10% fire rate", Apply = s => s.FireRateMult += 0.1f },
            new UpgradeDef { Rarity = Rarity.Common, MaxStacks = 4, Name = "Magnum Rounds", Desc = "Your shots are 15% bigger (area weapons: 15% wider areas)", Apply = s => s.ShotSizeMult += 0.15f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Light Boots", Desc = "+6% move speed", Apply = s => s.SpeedMult += 0.06f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Vitality", Desc = "+15 max HP", Apply = s => s.BonusHp += 15f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Mending", Desc = "Regenerate 1 HP per second", Apply = s => s.Regen += 1f },
            new UpgradeDef { Rarity = Rarity.Common, Name = "Focus", Desc = "-6% ability cooldowns", Apply = s => s.CdMult *= 0.94f },
            new UpgradeDef { Rarity = Rarity.Common, MaxStacks = 4, Name = "Reinforced Guard", Desc = "Your parry shield takes 25% more before it breaks", Apply = s => s.ParryCapacityMult += 0.25f },

            // Uncommon: larger stat boosts
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Hollow Points", Desc = "+20% damage", Apply = s => s.DmgMult += 0.2f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Hair Trigger", Desc = "+20% fire rate", Apply = s => s.FireRateMult += 0.2f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Sprinter", Desc = "+12% move speed", Apply = s => s.SpeedMult += 0.12f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Fortitude", Desc = "+40 max HP", Apply = s => s.BonusHp += 40f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Regeneration", Desc = "Regenerate 3 HP per second", Apply = s => s.Regen += 3f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Clarity", Desc = "-15% ability cooldowns", Apply = s => s.CdMult *= 0.85f },
            new UpgradeDef { Rarity = Rarity.Uncommon, Name = "Thick Skin", Desc = "Take 10% less damage", MaxStacks = 3, Apply = s => s.DamageTaken *= 0.9f },
            new UpgradeDef { Rarity = Rarity.Uncommon, MaxStacks = 2, Name = "Wide Guard", Desc = "Your parry shield covers a 30% wider arc", Apply = s => s.ParryArcMult += 0.3f },

            // Rare: ability upgrades and weapon mods
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 1, Name = "Second Charge", Desc = "Your Space ability gets a second charge", Apply = s => s.RoleCharges += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, Role = Role.Healer, MaxStacks = 2, Name = "Sanctuary", Desc = "Aura reaches 40% farther and heals allies twice as fast; it heals you +1 HP/s",
                Apply = s => { s.AuraRangeMult += 0.4f; s.AuraHealMult += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 3, Name = "Multishot", Desc = "+1 projectile: upgrades your weapon in its own way", Apply = s => s.ExtraShots += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 3, Name = "Barrage", Desc = "Every shot also fires a copy fanned out to the side, at 60% damage. Stack it into a wall of bullets",
                Apply = s => s.Fan += 1 },
            new UpgradeDef { Rarity = Rarity.Rare, MaxStacks = 2, Name = "Piercing Rounds", Desc = "Shots pass through two more enemies. Weapons whose shots already pass through everything, or that have none, reach 15% farther instead", Apply = s => s.Pierce += 2 },
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
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Boss, MaxStacks = 3, Name = "Searchlight",
                Desc = "A searchlight follows your cursor: enemies in it take 25% more damage from everyone, and one held in it for 2 s is stunned for 1 s. More stacks: a wider light, then +40%",
                Apply = s => s.Searchlight += 1 },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.HiveQueen, MaxStacks = 3, Name = "Hive Drones",
                Desc = "Two of the Queen's drones switch sides: they circle you and block bullets. After each of your kills one flies out to sting the nearest enemy (3 x 20), then comes home and heals you 4; while you hit a boss, one goes every 3 s. Each stack adds a drone, and another flies out each time",
                Apply = s => s.HiveDrones += s.HiveDrones == 0 ? 2 : 1 },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.HiveQueen, Grants = AbilityId.RoyalDive, Name = "Royal Dive",
                Desc = "New ability: the Queen's dive. Leap (untouchable) and crash down at your cursor, hurting and knocking back everything there, and leave a honey pool that slows enemies and heals allies" },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.HiveQueen, MaxStacks = 3, Name = "Bomber Escort",
                Desc = "Every 5 s one of the Queen's bombers flies from you to the toughest enemy within 400 px and bursts on it (55 damage within 70 px). +1 bomber per stack",
                Apply = s => s.Bombers += 1 },
            new UpgradeDef { Rarity = Rarity.Legendary, Unoffered = true, MaxStacks = 3, Name = "Swarm Rounds", Desc = "Your shots split into 3 smaller shots when they hit. With nothing else near, the swarm bites the enemy hit instead",
                Apply = s => s.SplitShots += 3 },
            new UpgradeDef { Rarity = Rarity.Legendary, Unoffered = true, MaxStacks = 3, Name = "Royal Jelly", Desc = "Every hit heals you 1 HP (up to 10 HP per second)",
                Apply = s => s.Lifesteal += 1f },
            new UpgradeDef { Rarity = Rarity.Legendary, Unoffered = true, MaxStacks = 3, Name = "Brood Mother", Desc = "40% of your kills spawn a mini-turret for 6 seconds, and hitting a boss spawns one every 8 seconds. More stacks, more turrets from kills",
                Apply = s => s.BroodChance += 0.4f },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.TwinA, MaxStacks = 3, Name = "Twin Scythes",
                Desc = "Two of the Reapers' scythes circle you in opposite directions, cutting enemies they pass (16) and slicing small enemy bullets out of the air. Every 1.6 s each reaps out at the nearest enemy within 240 px and flies back. 2nd stack: faster (every 1.1 s); 3rd: a third scythe",
                Apply = s => s.TwinScythes += 1 },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.TwinA, MaxStacks = 3, Name = "Final Reap",
                Desc = "When your hit leaves an enemy below 25% health, a phantom scythe reaps it and heals you 3 (not bosses). Your hits on a boss below 40% health deal double damage. More stacks: reap below 30%, then 35%; bosses x2.5, then x3",
                Apply = s => { s.ReapBelow += s.ReapBelow == 0f ? 0.25f : 0.05f; s.ReapBossBonus += s.ReapBossBonus == 0f ? 1f : 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.TwinA, Grants = AbilityId.ScytheThrow, Name = "Scythe Throw",
                Desc = "New ability: throw a giant scythe at your cursor. It cuts everything on the way out and on the way back; catch it to take 40% off its cooldown" },
            new UpgradeDef { Rarity = Rarity.Legendary, Unoffered = true, MaxStacks = 3, Name = "Executioner", Desc = "Double damage to enemies below 50% health",
                Apply = s => s.ExecuteBonus += 1f },
            new UpgradeDef { Rarity = Rarity.Legendary, Unoffered = true, MaxStacks = 3, Name = "Twin Fang", Desc = "40% chance for each hit to deal double damage",
                Apply = s => s.CritChance += 0.4f },
            new UpgradeDef { Rarity = Rarity.Legendary, Unoffered = true, MaxStacks = 3, Name = "Soul Harvest", Desc = "Every kill heals you 5 HP",
                Apply = s => s.KillHeal += 5f },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Singularity, MaxStacks = 3, Name = "Collapse Rounds", Desc = "Shots implode on impact, hurting enemies nearby (and the enemy hit, at half)",
                Apply = s => s.ImpactSplash += 45f },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Singularity, MaxStacks = 3, Name = "Graviton Rounds", Desc = "Enemies you hit are slowed by 40% for 2 seconds, and slowed enemies take 15% more damage from you",
                Apply = s => { s.SlowOnHit = Math.Min(0.7f, s.SlowOnHit + 0.4f); s.SlowedBonus += 0.15f; } },
            new UpgradeDef { Rarity = Rarity.Legendary, Boss = EnemyType.Singularity, MaxStacks = 3, Name = "Dark Matter", Desc = "Shots pierce 1 more enemy and deal 35% more damage",
                Apply = s => { s.Pierce += 1; s.DmgMult += 0.35f; } },

            // Weapon mods: one is picked after every boss, from the full list.
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Homing Rounds", Desc = "Shots bend toward an enemy close ahead of them (fixes near misses), but deal 5% less damage per stack. More stacks bend harder",
                Apply = s => s.Homing += 2f },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Ricochet", Desc = "Shots bounce to another nearby enemy after a hit (40% damage). +1 bounce per stack. Weapons whose hits can't bounce fling a bounce bolt instead",
                Apply = s => s.Ricochet += 1 },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Incendiary Rounds", Desc = "Hits set enemies on fire: each burns for another 40% of the hit's damage over 3 s, and burns from every hit add up",
                Apply = s => s.BurnFrac += 0.4f },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Cryo Rounds", Desc = "Hits chill enemies (20% slower) with a 6% chance to freeze them for 1 s",
                Apply = s => { s.CryoSlow = Math.Min(0.6f, s.CryoSlow + 0.2f); s.FreezeChance += 0.06f; } },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Shock Rounds", Desc = "25% chance per hit for lightning to arc to 2 more enemies (60% damage)",
                Apply = s => s.ShockChance += 0.25f },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Explosive Rounds", Desc = "Shots explode on impact, dealing 30% damage to enemies nearby",
                Apply = s => s.ExplosiveRadius += s.ExplosiveRadius == 0f ? 40f : 15f },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Mirror Guard", Desc = "Each bullet your parry blocks has a 25% chance to fly back at enemies",
                Apply = s => s.MirrorChance = Math.Min(0.75f, s.MirrorChance + 0.25f) },
            new UpgradeDef { Rarity = Rarity.Mod, MaxStacks = 3, Name = "Velocity Rounds", Desc = "Shots fly 30% faster and farther and deal 25% more damage",
                Apply = s => { s.ShotSpeedMult += 0.3f; s.DmgMult += 0.25f; } },
        }, ClassAbilityCards(), Milestones(), VectorCards(), Game.SoldierCards, Game.ElementalistCards, Game.SummonerCards,
            Game.KnightCards, Game.NinjaCards, Game.HemomancerCards);

        /// After a boss: every weapon mod you can still take, to choose from.
        public static int[] ModOffer(OfferCtx c)
        {
            var ids = new List<int>();
            for (int id = 0; id < All.Length; id++)
                if (All[id].Rarity == Rarity.Mod && Available(id, c))
                    ids.Add(id);
            return ids.Count > 0 ? ids.ToArray() : null;
        }

        /// The three legendaries for a boss (topped up from other bosses' if you've maxed some out).
        public static int[] LegendaryOffer(Random rng, EnemyType boss, OfferCtx c)
        {
            var own = new List<int>();
            var others = new List<int>();
            for (int id = 0; id < All.Length; id++)
            {
                if (All[id].Rarity != Rarity.Legendary || !Available(id, c))
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
            if (u.Name == "Homing Rounds" && weapon.HomingDesc != null)
                return weapon.Name + ": " + weapon.HomingDesc;
            if (u.Name == "Homing Rounds" && weapon.AreaDesc != null)
                return weapon.Name + ": " + weapon.AreaDesc + " per stack, instead of bending shots. 5% less damage per stack";
            if (u.Name == "Barrage" && weapon.BarrageDesc != null)
                return weapon.Name + ": " + weapon.BarrageDesc;
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
            { "Ricochet", "Your hits fling a bounce bolt at another nearby enemy (40% of the damage, up to 4 a second), and sword waves bounce. +1 bounce per stack" },
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
                case Rarity.Milestone: return new Rgba(1f, 0.8f, 0.35f);
                default: return new Rgba(0.85f, 0.85f, 0.9f);
            }
        }

        public static string RarityName(Rarity r) => r == Rarity.VeryRare ? "VERY RARE" : r == Rarity.Mod ? "WEAPON MOD" : r.ToString().ToUpperInvariant();

        /// Who a card is limited to, for the dev tools: "Knight", "Spirit Chalk", "Knight · 2 weapons", or "".
        public static string FilterNote(UpgradeDef u)
        {
            string kit = u.Kit.HasValue ? u.Kit.Value.ToString() : "";
            string weapons = u.Weapons == null ? "" : u.Weapons.Length == 1 && Defs.WeaponOf(u.Weapons[0]) != null
                ? Defs.WeaponOf(u.Weapons[0]).Name : u.Weapons.Length + " weapons";
            return kit.Length > 0 && weapons.Length > 0 ? kit + " · " + weapons : kit + weapons;
        }

        public static int Count(List<int> taken, int id)
        {
            int n = 0;
            foreach (int t in taken)
                if (t == id)
                    n++;
            return n;
        }

        /// The very rare class ability cards: for every role and class, a card for each of its Space abilities. A
        /// player is offered the two they didn't pick (see Available).
        static UpgradeDef[] ClassAbilityCards()
        {
            var all = new List<UpgradeDef>();
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

        /// The Expanding arena's vector cards (Docs/VectorBuilding.md).
        static UpgradeDef[] VectorCards() => new[]
        {
            new UpgradeDef { Rarity = Rarity.Uncommon, MaxStacks = 4, NeedsExpanding = true, Name = "Deep Inkwell", Desc = "Hold 20 more vectors (the Expanding arena)", Apply = s => s.VectorCap += 20 },
            new UpgradeDef { Rarity = Rarity.Milestone, MaxStacks = 3, NeedsExpanding = true, Name = "Architect", Desc = "Hold 40 more vectors, and gain 20 now (the Expanding arena)", VectorsNow = 20, Apply = s => s.VectorCap += 40 },
        };

        /// The milestone pool and the Milestone Compass.
        static UpgradeDef[] Milestones() => new[]
        {
            new UpgradeDef { Rarity = Rarity.Milestone, MaxStacks = 5, Name = "Scholar", Desc = "Gain 15% more XP per stack", Apply = s => s.XpMult += 0.15f },
            new UpgradeDef { Rarity = Rarity.Milestone, MaxStacks = 5, Name = "Second Helping", Desc = "10% chance a level-up gives an extra pick", Apply = s => s.ExtraPickChance += 0.1f },
            new UpgradeDef { Rarity = Rarity.Milestone, MaxStacks = 2, Name = "Wide Choice", Desc = "+1 card in every level-up offer (max 2)", Apply = s => s.OfferExtra += 1 },
            new UpgradeDef { Rarity = Rarity.Milestone, MaxStacks = 4, Name = "Reroll Cache", Desc = "Gain 3 rerolls now, and 1 more after every boss", RerollsNow = 3, Apply = s => s.BossRerolls += 1 },
            new UpgradeDef { Rarity = Rarity.Milestone, MaxStacks = 5, Name = "Fortune", Desc = "Rarer cards turn up 8% more often", Apply = s => s.RarityLuck += 0.08f },
            new UpgradeDef { Rarity = Rarity.Milestone, MaxStacks = 5, Name = "Iron Constitution", Desc = "+25 max HP", Apply = s => s.BonusHp += 25f },
            new UpgradeDef { Rarity = Rarity.Milestone, MaxStacks = 5, Name = "Quickening", Desc = "-4% ability cooldowns", Apply = s => s.CdMult *= 0.96f },
            // A normal very rare card: milestone offers can turn up between the milestone levels.
            new UpgradeDef { Rarity = Rarity.VeryRare, MaxStacks = 3, Name = "Milestone Compass", Desc = "Each level-up has a 5% chance to also offer a milestone pick", Apply = s => s.MilestoneChance += 0.05f },
        };

        /// Joins the card lists into Upgrades.All. Saves find cards by name, so every name must be unique.
        static UpgradeDef[] Gather(params UpgradeDef[][] lists)
        {
            var all = new List<UpgradeDef>();
            var names = new HashSet<string>();
            foreach (var list in lists)
                foreach (var u in list)
                {
                    if (!names.Add(u.Name))
                        throw new InvalidOperationException("Two upgrade cards are named " + u.Name + ": saves find cards by name");
                    all.Add(u);
                }
            return all.ToArray();
        }

        /// A milestone pick: up to three cards from the milestone pool (not the ones you've maxed out), or null if none are left.
        public static int[] MilestoneOffer(Random rng, OfferCtx c)
        {
            var ids = new List<int>();
            for (int id = 0; id < All.Length; id++)
                if (All[id].Rarity == Rarity.Milestone && Available(id, c))
                    ids.Add(id);
            if (ids.Count == 0)
                return null;
            var offer = new List<int>();
            while (offer.Count < 3 && ids.Count > 0)
            {
                int k = rng.Next(ids.Count);
                offer.Add(ids[k]);
                ids.RemoveAt(k);
            }
            return offer.ToArray();
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
        public static bool Available(int id, OfferCtx c)
        {
            Role role = c.Role;
            AbilityId roleAbility = c.RoleAbility;
            List<int> taken = c.Taken;
            var u = All[id];
            if (u.Unoffered)
                return false;
            if (u.NeedsExpanding && !c.Expanding)
                return false;  // the vector cards are for the Expanding arena
            if (u.Weapons != null && (!c.Weapon.HasValue || Array.IndexOf(u.Weapons, c.Weapon.Value) < 0))
                return false;
            if (u.Kit.HasValue && !c.HasKit(u.Kit.Value))
                return false;
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
            int own = role == Role.Wildcard ? 1 : 0;  // a Wildcard's second ability already sits on one of the bonus keys
            if (u.Grants != AbilityId.None && grants + own >= BonusSlots)
                return false;
            if (u.NeedsExtraAbility && grants + own == 0)
                return false;
            return true;
        }

        /// One upgrade of this rarity this player could take (falling back to lower rarities), or -1. (Chests, Props.cs.)
        public static int RandomOfRarity(Random rng, Rarity want, OfferCtx c)
        {
            var candidates = new List<int>();
            for (int r = (int)want; r >= 0; r--)
            {
                candidates.Clear();
                for (int id = 0; id < All.Length; id++)
                    if ((int)All[id].Rarity == r && Available(id, c))
                        for (int k = 0; k < All[id].Weight; k++)
                            candidates.Add(id);
                if (candidates.Count > 0)
                    return candidates[rng.Next(candidates.Count)];
            }
            return -1;
        }

        /// Roll three different upgrades. Higher levels make rare cards a bit likelier.
        /// A challenge room's prize: three cards, each rare or very rare (Challenges.cs).
        public static int[] PrizeOffer(Random rng, OfferCtx c) =>
            RollWeighted(rng, c, new[] { 0f, 0f, 55f, 45f }, 3);

        public static int[] RollOffer(Random rng, OfferCtx c, int level)
        {
            var st = Compute(c.Taken);  // Fortune (rarer cards) and Wide Choice (more cards) are milestone upgrades
            float veryRare = Math.Min(10f, 3f + level * 0.5f) * (1f + st.RarityLuck);
            float rare = Math.Min(22f, 11f + level * 0.8f) * (1f + st.RarityLuck);
            return RollWeighted(rng, c, new[] { 55f, 28f * (1f + st.RarityLuck), rare, veryRare }, 3 + Math.Min(2, st.OfferExtra));
        }

        static int[] RollWeighted(Random rng, OfferCtx c, float[] weights, int count)
        {
            var offer = new int[count];
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
                        if ((int)All[id].Rarity == want && !used.Contains(id) && Available(id, c))
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
            var copies = new Dictionary<int, int>();
            foreach (int id in taken)
            {
                if (All[id].Apply == null)
                    continue;
                int n;
                copies.TryGetValue(id, out n);
                copies[id] = ++n;
                float f = CopyStrength(All[id], n);
                if (f >= 0.999f)
                {
                    All[id].Apply(s);
                    continue;
                }
                // Diminishing returns: this copy moves every number it touches only f of the way.
                var before = new float[statFloats.Length];
                for (int i = 0; i < statFloats.Length; i++)
                    before[i] = (float)statFloats[i].GetValue(s);
                All[id].Apply(s);
                for (int i = 0; i < statFloats.Length; i++)
                {
                    float after = (float)statFloats[i].GetValue(s);
                    if (after != before[i])
                        statFloats[i].SetValue(s, before[i] + (after - before[i]) * f);
                }
            }
            return s;
        }

        static readonly System.Reflection.FieldInfo[] statFloats = Array.FindAll(typeof(Stats).GetFields(), fi => fi.FieldType == typeof(float));

        /// Diminishing returns (Docs/PowerStacking.md, owner Nov 2026): the cards you can take any number of times (the
        /// plain stat cards: no MaxStacks) give each extra copy a bit less: copy n is worth 1 / (1 + DiminishStep x (n - 1))
        /// of the first (100%, 80%, 67%, 57%, 50%, ...). Fourteen Hair Triggers come to +128% fire rate, not +280%.
        /// Cards with a stack limit are already held in check and keep full strength.
        public const float DiminishStep = 0.25f;

        public static bool Diminishes(UpgradeDef u) => u.MaxStacks >= 99 && u.Apply != null;

        public static float CopyStrength(UpgradeDef u, int copy) => Diminishes(u) && copy > 1 ? 1f / (1f + DiminishStep * (copy - 1)) : 1f;

        /// "Copy 3: 67% strength" for an offer of a card you already have (empty for a first copy or a capped card).
        public static string CopyNote(UpgradeDef u, List<int> taken)
        {
            int idx = Array.IndexOf(All, u), have = 0;
            foreach (int t in taken)
                if (t == idx)
                    have++;
            if (have == 0 || !Diminishes(u))
                return "";
            return "Copy " + (have + 1) + ": " + Math.Round(CopyStrength(u, have + 1) * 100f) + "% strength";
        }
    }
}
