using System;
using System.Collections.Generic;
namespace Arena
{
    /// Wildcard (Nov 2026, Docs/Wildcard.md) is a fourth role that borrows DPS gear from any class: it is unlocked by winning a run
    /// with each of the other three (Profile.WildcardUnlocked). Its lobby picks are encoded as kit * 3 + choice (see Defs.WildcardChoices).
    public enum Role : byte { Tank = 0, Healer = 1, Dps = 2, Wildcard = 3 }

    public enum EnemyType : byte
    {
        Grunt, Burster, Sniper, Spinner, Boss,
        Charger, Splitter, Shard, Mortar, Shielder, Medic,
        HiveQueen, TwinA, TwinB, Singularity,
        // late game
        Phantom, Hexer, Juggernaut, Nest,
        Colossus, Duelist, Decoy, Lich, Phylactery, StormHerald, Devourer, Eclipse,
        GravityWell,  // the Singularity's adds: they pull players' shots into themselves
        // melee: they never shoot, they run at you (early waves are mostly these)
        Spiker, Brute, Lunger,
        HiveBomber,  // the Hive Queen's explosive drones: fly straight at where you were and blow up
        CageWell,    // the Singularity's small wells that ring a player in a shrinking cage
        Rioter,      // the Warden's riot: prisoners who run at whoever's nearest, the Warden included (Warden.cs)
        HiveEgg,     // the Hive Queen's eggs: break them before they hatch into drones (Queen.cs)
        Guard,       // the Warden's riot guards: they beat the rioters left after 10 s, then come for you (Warden.cs)
        DualReaper,  // the Twin Reapers fused into one (Reapers.cs)
        ReaperPillar,  // the Dual Reaper's corner pillars: each one killed leaves a hole that can trap it (Reapers.cs)
        SingShard,     // the Singularity's Split: shards that orbit and fire; their damage goes to the core (Singularity.cs)
        BallLightning, // the Storm Herald's Gale: slow orbs that drift at you on the wind and arc to each other (Herald.cs)
        Morsel,        // the Devourer's Hungry turn: food it spits out and crawls over to eat; shoot it or step on it (Devourer.cs)
        // the second half of the run (Variety.cs)
        BeamSentry, Scorcher, Reaver, Warder,
        // new shooting patterns (Variety.cs)
        Pauser, Leader, Stitcher,
        // boss approaches: the Warden's faction (WardenFaction.cs)
        RiotShield, GasGrenadier,
        // challenge rooms (Challenges.cs): the mini-boss, and a Mirror Match's shadows of the players
        Gatekeeper, Shadow,
        // Zombie Infection's horde (Zombies.cs, ZombieLook.cs)
        Shambler, Runner, Spitter, Crawler, Bloater, Shrieker, Mender,
        // melee grunts from wave 5 (EnemyAI.cs, MeleeGrunts.cs)
        Buzzsaw, SpikeBlock,
        // the simplest melee grunt: a white chomper that walks straight at you, in packs (EnemyAI.cs)
        Swarmer,
        // enemies that don't shoot (NonShooters.cs, Docs/EnemyRoles.md): a Hunter, a Disruptor, a Zoner and the Zoner's mines
        FuseRunner, Hookman, Sapper, SapperMine,
        // melee swordsmen (Swordsman.cs): a long blade that cuts a cone, and one with a shield up in front
        Swordsman, ShieldSwordsman,
        // the Warden's faction: a ranged guard whose Fork Bolt stuns you (WardenFaction.cs, Game/TaserLook.cs)
        TaserGuard,
        // the Warden's faction: a guard with a service pistol, seven tracer rounds a magazine (SecurityGuard.cs, Game/SecurityLook.cs)
        SecurityGuard,
        // the Warden's faction, prisoners: a Brute and a Lunger in prison gear (PrisonKin.cs, Game/PrisonLook.cs)
        Thug, Shiv,
        // a spawn portal: a burst of the wave comes through it; shoot it closed to cancel the rest (SpawnPortals.cs, Game/SpawnPortalLook.cs)
        Portal,
        // the Hive Queen's faction (Docs/HiveFaction.md, HiveKin.cs): each plays like one of the Warden's, in the same order
        Worker, Soldier, CombShield, HoneyLobber, Stinger, Needler, Bumble, Hornet,
        // the Twin Reapers' faction, the Dusk and Dawn cult (ReaperFaction.cs, Game/ReaperCultLook.cs): Wisp (a Swarmer), Acolyte
        // (a Swordsman with a sickle), Bound Wraiths (a tethered pair), Scythe Wraith (a Lunger that reaps), Lamplighter (lines and
        // the cross), Mourner (the twin spiral), Gravedigger (a Brute), Shade (a Phantom)
        Wisp, Acolyte, BoundWraith, ScytheWraith, Lamplighter, Mourner, Gravedigger, Shade,
        // the Singularity's faction, the Infall (Docs/SingularityFaction.md, Infall.cs, Game/InfallLook.cs): step 1
        Debris, VoidAcolyte, Pulsar,
        // ...step 2, with the Graviton's small well and the Collapsar's implosion (both only come from them)
        Graviton, Orbiter, Collapsar, MiniWell, Implosion,
        // ...step 3
        Lens, WhiteDwarf,
        // the Colossus's faction, The Quarry (QuarryFaction.cs, Game/QuarryLook.cs): Rubble plays like a Rioter, a Quarryman
        // like a Guard, a Stomper like a Thug that stomps out a small ring of push blocks
        Rubble, Quarryman, Stomper,
        // step 2: a Slab Bearer plays like a Riot Shield (its broken slab drops as cover), a Rock Hurler like a Gas Grenadier (a boulder)
        SlabBearer, RockHurler,
        // step 3: a Driller plays like a Shiv (its drill leaves a crack that erupts), a Geomancer and a Cinder Priest like Taser
        // Guards (a crack sent at you; a magma bolt that leaves lava)
        Driller, Geomancer, CinderPriest,
        // faction mini-bosses (FactionMinis.cs): the Warden's, a shield officer (RiotCaptain.cs, Game/RiotCaptainLook.cs)
        RiotCaptain,
        // the Duelist's faction, the Masked Court (CourtFaction.cs, Game/CourtLook.cs): a Fencer plays like a Shiv with a short
        // hop, a Blade Dancer like a Guard that lunges and leaves blades, a Parrier like a Riot Shield that parries shots back
        Fencer, BladeDancer, Parrier,
        // step 2: a Second plays like a Security Guard that throws dagger fans, a Marshal like a Gas Grenadier that draws a
        // dueling circle round you, a Gallant like a Taser Guard that salutes and lunges (a perfect parry leaves it open)
        Second, Marshal, Gallant,
        // step 3: a Harlequin plays like a Thug that dashes an X of blades through you, a Mummer like a Shiv that splits into
        // three; a Mummer Copy is one of its two copies (no XP, its shots are hollow; it looks just like the Mummer)
        Harlequin, Mummer, MummerCopy,
        // the Lich's faction: a zombie in his soul cyan that cracks graves open round you (Zombies.cs, Game/ZombieLook.cs)
        GraveCaller,
        // the Devourer's faction, The Larder (LarderFaction.cs, Game/LarderLook.cs): a Gnawling plays like a Rioter, a Butcher
        // like a Guard that chops a half-circle, a Gulper like a Thug that breathes you in
        Gnawling, Butcher, Gulper,
        // step 2: a Carcass Bearer plays like a Riot Shield (its broken slab drops as a morsel), a Bile Spitter like a Gas Grenadier (an acid pool)
        CarcassBearer, BileSpitter,
        // the Storm Herald's faction, the Squall (Docs/StormFaction.md, Squall.cs, Game/SquallLook.cs): step 1
        DustDevil, SquallGunner, Stormcaller,
    }

    public class RoleDef
    {
        public string Name, Ability, Desc;
        public Rgba Color;
        public float Hp, Speed, HitR, BodyR, FireInterval, AbilityCd;
    }

    /// Soldier is the original gun-based kit; Mage and Summoner are the alternatives for each role.
    /// The classes, in lobby order (the order is sent over the network and indexes the class tables: add new ones at the end).
    /// Each has a KitDef (Classes.cs). Run saves from before the October 2026 rename say "Mage" and "BloodMage" (KitFromSave).
    public enum Kit : byte { Soldier, Elementalist, Summoner, Knight, Ninja, Hemomancer }

    public enum WeaponId : byte
    {
        // Soldier
        Scattergun, FlakCannon, RiotRepeater, LightBolt, Pistol, MendersBow, TwinRifle, Railgun, BurstSmg,
        // Mage
        Flamethrower, FrostLance, StormHammer, RadiantOrb, ChainLight, Nimbus, Fireball, ArcLightning, ArcaneMissiles,
        // Summoner
        GolemStaff, BoneGuard, BearTotem, SpriteStaff, TreantRod, SpiritChalk, WolfWhistle, WispLantern, ImpCaller,
        // Knight (melee)
        Warhammer, Lance, Halberd, HolyMace, Lightbringer, Censer, TwinBlades, Greatsword, Rapier,
        // Ninja (Ninja.cs): thrown blades and quick melee
        Kusarigama, BoStaff, FumaShuriken, MedicineDarts, SpiritFan, Ofuda, Shuriken, Katana, Kunai,
        // Blood Mage (BloodMage.cs): blood spells that drain and heal
        BloodLash, ClotBolt, LeechBeam, TransfusionDarts, SanguineOrb, VeinTether, BloodBolts, Implosion, BoneSpear,
        // Fourth weapons (Choice 3), where a class has one: the Knight DPS's Arc Knuckles (ArcKnuckles.cs), the Soldier DPS's Double Barrel (DoubleBarrel.cs); the Twin Rifle's variants (Twin Rifle Auto, Out of Ammo) are Choice 4 and 5
        ArcKnuckles, TwinRifleAuto, OutOfAmmo, DoubleBarrel,
    }

    public class WeaponDef
    {
        public WeaponId Id;
        public Kit Kit;            // whose weapon it is: FireVolley sends it to that class's Fire (Classes.cs) unless it has its own case
        public Role Role;          // with Kit and Choice, its place in the lobby (Defs.Weapons is gathered from each class's list)
        public int Choice;         // 0-2: first, second or third weapon of its role and class
        public string Name, Desc;
        public Sfx Sound = Sfx.Throw;  // the sound of its shots (summoner staffs, darts and rocks just throw)
        public string AreaDesc;    // set on weapons whose damage lands in an area (Game.AreaWeapon): what Homing Rounds grow instead of bending shots
        public string HomingDesc;  // what Homing Rounds do for it instead (the card text after the weapon's name; checked before AreaDesc, so cones say they condense)
        public string BarrageDesc; // what Barrage does for it, when that isn't the usual fanned copies (the card text after the weapon's name)
        public string ExtraDesc;  // what each extra projectile (Multishot, Artillery Assault) does for this weapon
        public float FireInterval;
        public bool Variant;      // a variant of another weapon (Variants.VariantOf): picked in that weapon's Variants window, not on its own lobby key
        // Summoner weapons keep companions around. Imps blow themselves up, so a new one
        // arrives every CompanionRespawn seconds; the others stay until they are lost.
        public MinionKind Companion = MinionKind.None;
        public int CompanionCount;
        public float CompanionRespawn = 3f;
        public bool Melee;  // swings instead of shots (Knight.cs; the Ninja's Kusarigama, Bo Staff and Katana and the Blood Lash too)

        // The main attack's numbers, in one place: the code that fires it, WeaponRange (the aim line) and the bots
        // all read these. Secondary parts (fragments, sword waves, a Katana's Kage Chain and Zanshin, heals) stay with the weapon's code.
        public float Speed, Life;      // a shot's speed (px/s) and lifetime (s): it reaches Speed × Life
        public float Damage;           // the main shot's or swing's damage (before upgrades)
        public float Reach;            // a swing's reach (px), or a beam's length
        public float Grow;             // per extra projectile: a shot lives (a beam reaches) this much longer, as a fraction;
                                       // a swing reaches this many px further. 0 if extras do something else
        public float BotRange = 330f;  // how far away bots like to fight with it
    }

    /// Where the Practice Range's picker lists an enemy: by group, then in EnemyType order.
    public enum PickerGroup : byte { Hidden, Melee, Ranged, Late, Boss }

    public class EnemyDef
    {
        public float Hp, Radius, Speed;
        public int Score, Sides, Xp;
        public string Name;
        public bool IsBoss;
        public Rgba Color;
        public float Contact;  // melee: damage when it touches you (0 = the default contact damage)
        public bool Melee;
        public bool Late;      // its AI is in LateGame.cs (LateMove, LateSpecial, LateFire)
        public bool Heavy;     // too heavy to slow down, shove around or knock back (bosses always are)
        public bool Mini;      // a challenge room's mini-boss: a boss bar, but none of a boss's rules (Challenges.cs)
        public PickerGroup Picker;  // where the Practice Range's picker lists it (Hidden: only comes with its boss)
        public string Blurb;   // one line on what it does (the Practice Range shows it on hover)
    }

    /// Stats for roles, enemies and bullets. Tweak numbers here to rebalance.
    public static partial class Defs
    {
        public static readonly RoleDef[] Roles =
        {
            new RoleDef
            {
                Name = "Tank", Ability = "Barrier", Color = new Rgba(0.35f, 0.6f, 1f),
                Hp = 240f, Speed = 185f, HitR = 9f, BodyR = 18f, FireInterval = 0.45f, AbilityCd = 12f,
                Desc = "Big HP pool. Enemies prefer to target you.",
            },
            new RoleDef
            {
                Name = "Healer", Ability = "Mend Pulse", Color = new Rgba(0.35f, 1f, 0.55f),
                Hp = 140f, Speed = 215f, HitR = 7f, BodyR = 14f, FireInterval = 0.25f, AbilityCd = 10f,  // 140 (was 120): healers took 7 of 9 downs in a recorded run
                Desc = "Your aura slowly heals an ally standing near you, and you a little.",
            },
            new RoleDef
            {
                Name = "DPS", Ability = "Dash", Color = new Rgba(1f, 0.55f, 0.25f),
                Hp = 95f, Speed = 240f, HitR = 6f, BodyR = 12f, FireInterval = 0.1f, AbilityCd = 3.5f,
                Desc = "Fragile, but deals the most damage.",
            },
            new RoleDef
            {
                // Wildcard (Docs/Wildcard.md): middle health and no role passive; DPS gear from any class, a second starting ability,
                // and the price is WildcardDmgMult / WildcardCdMult (Game.cs).
                Name = "Wildcard", Ability = "Any DPS ability", Color = new Rgba(0.78f, 0.45f, 1f),
                Hp = 130f, Speed = 225f, HitR = 6.5f, BodyR = 14f, FireInterval = 0.1f, AbilityCd = 3.5f,
                Desc = "Mix any class's DPS weapon and two DPS abilities. Weaker shots and slower cooldowns.",
            },
        };

        // Indexed by EnemyType.
        public static readonly EnemyDef[] Enemies =
        {
            new EnemyDef { Name = "Grunt", Hp = 60f, Radius = 14f, Speed = 85f, Score = 10, Xp = 10, Sides = 3, Color = new Rgba(1f, 0.35f, 0.35f),
                Picker = PickerGroup.Ranged, Blurb = "Fires aimed 3-shot spreads." },
            new EnemyDef { Name = "Burster", Hp = 110f, Radius = 17f, Speed = 55f, Score = 20, Xp = 18, Sides = 6, Color = new Rgba(0.8f, 0.4f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Fires rings of bullets; more in each ring on later waves." },
            new EnemyDef { Name = "Sniper", Hp = 50f, Radius = 12f, Speed = 95f, Score = 15, Xp = 14, Sides = 4, Color = new Rgba(1f, 0.9f, 0.3f),
                Picker = PickerGroup.Ranged, Blurb = "Fast, accurate shots from far away." },
            new EnemyDef { Name = "Spinner", Hp = 150f, Radius = 18f, Speed = 35f, Score = 25, Xp = 22, Sides = 5, Color = new Rgba(1f, 0.6f, 0.2f),
                Picker = PickerGroup.Ranged, Blurb = "Wanders and sprays spirals in bursts; it spins up while bursting." },
            new EnemyDef { Name = "The Warden", IsBoss = true, Hp = 2400f, Radius = 46f, Speed = 45f, Score = 300, Xp = 150, Sides = 8, Color = new Rgba(1f, 0.25f, 0.5f),
                Picker = PickerGroup.Boss, Blurb = "Boss (wave 5): a prison riot in three stages — searchlight, gates and gas, then rioters." },
            new EnemyDef { Name = "Charger", Hp = 90f, Radius = 15f, Speed = 70f, Score = 20, Xp = 18, Sides = 3, Color = new Rgba(0.3f, 0.9f, 0.9f),
                Picker = PickerGroup.Ranged, Blurb = "Flashes a red line, then dashes along it and bursts into a ring." },
            new EnemyDef { Name = "Splitter", Hp = 120f, Radius = 19f, Speed = 50f, Score = 20, Xp = 16, Sides = 4, Color = new Rgba(0.6f, 1f, 0.3f),
                Picker = PickerGroup.Ranged, Blurb = "Splits into 3 fast Shards when killed." },
            new EnemyDef { Name = "Shard", Hp = 25f, Radius = 8f, Speed = 130f, Score = 5, Xp = 4, Sides = 3, Color = new Rgba(0.8f, 1f, 0.55f),
                Picker = PickerGroup.Ranged, Blurb = "Tiny and fast. Comes from Splitters, Nests and the Hive Queen." },
            new EnemyDef { Name = "Mortar", Hp = 80f, Radius = 16f, Speed = 40f, Score = 20, Xp = 18, Sides = 7, Color = new Rgba(0.95f, 0.55f, 0.3f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs bombs at where you stand; they burst into a ring on landing." },
            new EnemyDef { Name = "Shielder", Hp = 140f, Radius = 17f, Speed = 60f, Score = 25, Xp = 22, Sides = 6, Color = new Rgba(0.6f, 0.7f, 0.9f),
                Picker = PickerGroup.Ranged, Blurb = "A front shield blocks shots and swings. It turns slowly: flank it." },
            new EnemyDef { Name = "Medic", Hp = 70f, Radius = 13f, Speed = 75f, Score = 25, Xp = 20, Sides = 4, Color = new Rgba(1f, 0.5f, 0.75f),
                Picker = PickerGroup.Ranged, Blurb = "Heals nearby enemies every few seconds. Kill it first." },
            new EnemyDef { Name = "Hive Queen", IsBoss = true, Hp = 3000f, Radius = 44f, Speed = 40f, Score = 400, Xp = 180, Sides = 6, Color = new Rgba(1f, 0.8f, 0.2f),
                Picker = PickerGroup.Boss, Blurb = "Boss (wave 10): a hive in three stages — drones and bombers, sealing cells and honey, then a frenzied swarm." },
            new EnemyDef { Name = "Reaper of Dusk", IsBoss = true, Hp = 2600f, Radius = 34f, Speed = 95f, Score = 250, Xp = 100, Sides = 5, Color = new Rgba(1f, 0.35f, 0.6f),
                Picker = PickerGroup.Boss, Blurb = "Boss pair (wave 15): a tether sweep, then they fuse into the Dual Reaper (pillars, holes), then split for a finale." },
            new EnemyDef { Name = "Reaper of Dawn", IsBoss = true, Hp = 2600f, Radius = 34f, Speed = 95f, Score = 250, Xp = 100, Sides = 5, Color = new Rgba(0.45f, 0.55f, 1f) },
            new EnemyDef { Name = "The Singularity", IsBoss = true, Hp = 3600f, Radius = 40f, Speed = 0f, Score = 500, Xp = 220, Sides = 10, Color = new Rgba(0.6f, 0.35f, 1f),
                Picker = PickerGroup.Boss, Blurb = "Boss (wave 20): corner gravity wells, pull, lasers and shrinking cages." },
            new EnemyDef { Name = "Phantom", Hp = 90f, Radius = 13f, Speed = 90f, Score = 25, Xp = 22, Sides = 4, Color = new Rgba(0.55f, 0.85f, 1f),
                Picker = PickerGroup.Late, Late = true, Blurb = "Flickers, blinks next to you and bursts into a ring." },
            new EnemyDef { Name = "Hexer", Hp = 100f, Radius = 14f, Speed = 60f, Score = 25, Xp = 22, Sides = 5, Color = new Rgba(0.75f, 0.3f, 0.9f),
                Picker = PickerGroup.Late, Late = true, Blurb = "Fires curving pincer shots that close in from both sides." },
            new EnemyDef { Name = "Juggernaut", Hp = 420f, Radius = 26f, Speed = 32f, Score = 60, Xp = 50, Sides = 8, Color = new Rgba(0.55f, 0.55f, 0.62f),
                Picker = PickerGroup.Late, Late = true, Heavy = true, Blurb = "Armored and slow, can't be slowed or knocked back. Rotating stars of orbs." },
            new EnemyDef { Name = "Nest", Hp = 260f, Radius = 22f, Speed = 0f, Score = 40, Xp = 35, Sides = 6, Color = new Rgba(0.7f, 0.9f, 0.4f),
                Picker = PickerGroup.Late, Late = true, Heavy = true, Blurb = "Stationary spawner that keeps hatching Shards until destroyed." },
            new EnemyDef { Name = "The Colossus", IsBoss = true, Hp = 6800f, Radius = 54f, Speed = 35f, Score = 600, Xp = 260, Sides = 6, Color = new Rgba(0.78f, 0.62f, 0.45f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 25): huge stomp rings with one gap, and thrown rocks." },
            new EnemyDef { Name = "The Duelist", IsBoss = true, Hp = 6000f, Radius = 30f, Speed = 80f, Score = 650, Xp = 280, Sides = 4, Color = new Rgba(0.85f, 0.85f, 1f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 30): teleports and leaves decoys that burst unless killed." },
            new EnemyDef { Name = "The Duelist", Hp = 70f, Radius = 30f, Speed = 0f, Score = 0, Xp = 0, Sides = 4, Color = new Rgba(0.85f, 0.85f, 1f), Late = true },  // decoy
            new EnemyDef { Name = "The Lich", IsBoss = true, Hp = 5000f, Radius = 36f, Speed = 45f, Score = 700, Xp = 300, Sides = 7, Color = new Rgba(0.5f, 1f, 0.75f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 35): immune while its phylactery stands; raises skeletons." },
            new EnemyDef { Name = "Phylactery", Hp = 600f, Radius = 18f, Speed = 0f, Score = 50, Xp = 0, Sides = 4, Color = new Rgba(0.4f, 1f, 0.6f), Late = true, Heavy = true },
            new EnemyDef { Name = "Storm Herald", IsBoss = true, Hp = 6000f, Radius = 40f, Speed = 55f, Score = 800, Xp = 320, Sides = 9, Color = new Rgba(0.55f, 0.75f, 1f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 40), three stages: a gathering storm (strikes, lightning rods), a gale that pushes you and bends bullets, then a supercell that marks one player at a time." },
            new EnemyDef { Name = "The Devourer", IsBoss = true, Hp = 6500f, Radius = 50f, Speed = 40f, Score = 900, Xp = 340, Sides = 12, Color = new Rgba(0.85f, 0.28f, 0.38f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 45), three stages: hungry for morsels (your shots heal it), then swallowing players, then a mouth that heals it from the front." },
            new EnemyDef { Name = "The Eclipse", IsBoss = true, Hp = 9000f, Radius = 52f, Speed = 30f, Score = 2000, Xp = 500, Sides = 16, Color = new Rgba(1f, 0.85f, 0.4f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Final boss (wave 50), five stages and Totality: a sun and its moon. Hide in the moon's shadow from the flash, stay in the light, and kill it before the nova." },
            new EnemyDef { Name = "Gravity Well", Hp = 260f, Radius = 15f, Speed = 160f, Score = 40, Xp = 15, Sides = 6, Color = new Rgba(0.7f, 0.45f, 1f), Late = true },
            // Melee
            new EnemyDef { Name = "Spiker", Hp = 22f, Radius = 9f, Speed = 135f, Score = 5, Xp = 5, Sides = 3, Color = new Rgba(1f, 0.6f, 0.3f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "Small and fast melee rusher with long thin spikes. It zig-zags toward you in pairs." },
            new EnemyDef { Name = "Brute", Hp = 200f, Radius = 19f, Speed = 62f, Score = 15, Xp = 14, Sides = 7, Color = new Rgba(0.8f, 0.45f, 0.35f), Melee = true, Contact = 24f,
                Picker = PickerGroup.Melee, Blurb = "Slow, tough melee bruiser that hits hard (24) when it reaches you." },
            new EnemyDef { Name = "Lunger", Hp = 45f, Radius = 12f, Speed = 95f, Score = 12, Xp = 12, Sides = 4, Color = new Rgba(0.95f, 0.35f, 0.65f), Melee = true, Contact = 18f,
                Picker = PickerGroup.Melee, Blurb = "Circles in close, flashes a pink line, then leaps along it." },
            new EnemyDef { Name = "Hive Bomber", Hp = 30f, Radius = 10f, Speed = 330f, Score = 2, Xp = 0, Sides = 3, Color = new Rgba(1f, 0.45f, 0.15f), Melee = true },
            new EnemyDef { Name = "Cage Well", Hp = 40f, Radius = 11f, Speed = 0f, Score = 5, Xp = 2, Sides = 6, Color = new Rgba(0.85f, 0.4f, 1f), Melee = true },
            new EnemyDef { Name = "Rioter", Hp = 40f, Radius = 12f, Speed = 110f, Score = 3, Xp = 7, Sides = 8, Color = new Rgba(1f, 0.55f, 0.2f), Melee = true, Contact = 10f },
            new EnemyDef { Name = "Hive Egg", Hp = 60f, Radius = 15f, Speed = 0f, Score = 4, Xp = 3, Sides = 10, Color = new Rgba(0.96f, 0.92f, 0.78f), Melee = true },
            new EnemyDef { Name = "Guard", Hp = 90f, Radius = 13f, Speed = 125f, Score = 6, Xp = 15, Sides = 8, Color = new Rgba(0.35f, 0.55f, 1f), Melee = true, Contact = 14f },
            new EnemyDef { Name = "The Dual Reaper", IsBoss = true, Hp = 5200f, Radius = 58f, Speed = 150f, Score = 500, Xp = 200, Sides = 10, Color = new Rgba(1f, 0.8f, 0.45f) },
            new EnemyDef { Name = "Reaper Pillar", Hp = 120f, Radius = 24f, Speed = 0f, Score = 5, Xp = 4, Sides = 4, Color = new Rgba(0.55f, 0.5f, 0.75f), Melee = true },
            new EnemyDef { Name = "Singularity Shard", Hp = 1e6f, Radius = 20f, Speed = 0f, Score = 0, Xp = 0, Sides = 5, Color = new Rgba(0.8f, 0.5f, 1f), Melee = true },
            new EnemyDef { Name = "Ball Lightning", Hp = 70f, Radius = 15f, Speed = 0f, Score = 3, Xp = 2, Sides = 8, Color = new Rgba(0.75f, 0.9f, 1f), Melee = true, Contact = 20f },
            new EnemyDef { Name = "Morsel", Hp = 60f, Radius = 14f, Speed = 0f, Score = 0, Xp = 0, Sides = 7, Color = new Rgba(0.88f, 0.5f, 0.35f), Late = true },
            new EnemyDef { Name = "Beam Sentry", Hp = 160f, Radius = 17f, Speed = 70f, Score = 30, Xp = 12, Sides = 6, Color = new Rgba(0.44f, 0.89f, 1f),
                Picker = PickerGroup.Late, Blurb = "Plants itself, aims a line at you, then sweeps a beam across your side." },
            new EnemyDef { Name = "Scorcher", Hp = 130f, Radius = 15f, Speed = 95f, Score = 30, Xp = 12, Sides = 5, Color = new Rgba(1f, 0.71f, 0.28f),
                Picker = PickerGroup.Late, Blurb = "Circles you leaving a trail of embers. Cut across before it closes." },
            new EnemyDef { Name = "Reaver", Hp = 240f, Radius = 19f, Speed = 150f, Score = 40, Xp = 16, Sides = 3, Color = new Rgba(1f, 0.3f, 0.43f), Melee = true, Heavy = true, Contact = 20f,
                Picker = PickerGroup.Late, Blurb = "Armoured melee: a red cone, then a rake. Open for a moment after: +50% damage." },
            new EnemyDef { Name = "Warder", Hp = 180f, Radius = 16f, Speed = 65f, Score = 40, Xp = 16, Sides = 8, Color = new Rgba(0.71f, 0.61f, 1f),
                Picker = PickerGroup.Late, Blurb = "Puts bubbles on up to three allies. Kill it and they all pop." },
            new EnemyDef { Name = "Pauser", Hp = 100f, Radius = 15f, Speed = 60f, Score = 25, Xp = 12, Sides = 6, Color = new Rgba(0.45f, 0.95f, 0.85f),
                Picker = PickerGroup.Late, Blurb = "Fans out shots that stop dead, glow, then all rush at where you are. Keep moving." },
            new EnemyDef { Name = "Leader", Hp = 90f, Radius = 13f, Speed = 80f, Score = 25, Xp = 12, Sides = 3, Color = new Rgba(1f, 0.55f, 0.85f),
                Picker = PickerGroup.Late, Blurb = "Aims where you're going, not where you are. Change direction when it fires." },
            new EnemyDef { Name = "Stitcher", Hp = 120f, Radius = 15f, Speed = 65f, Score = 30, Xp = 14, Sides = 5, Color = new Rgba(1f, 0.82f, 0.45f),
                Picker = PickerGroup.Late, Blurb = "Fires slow orbs; nearby orbs are tied by a harmful thread. Slip through the gaps." },
            new EnemyDef { Name = "Riot Shield", Hp = 60f, Radius = 16f, Speed = 120f, Score = 10, Xp = 10, Sides = 6, Color = new Rgba(0.55f, 0.68f, 0.95f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "A guard behind a tall shield that bashes you back. Go round it, or break the shield: then it falls fast." },
            new EnemyDef { Name = "Gas Grenadier", Hp = 50f, Radius = 14f, Speed = 75f, Score = 8, Xp = 9, Sides = 5, Color = new Rgba(0.62f, 0.92f, 0.42f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs tear-gas canisters at you. Step out of the cloud." },
            new EnemyDef { Name = "The Gatekeeper", Hp = 3000f, Radius = 34f, Speed = 55f, Score = 150, Xp = 80, Sides = 8, Color = new Rgba(1f, 0.62f, 0.3f), Heavy = true, Contact = 25f, Mini = true,
                Blurb = "Challenge room mini-boss: a ring of shots with a gap, three aimed fans, and a charge along a shown line that ends in a burst." },
            new EnemyDef { Name = "Shadow", Hp = 1800f, Radius = 16f, Speed = 190f, Score = 60, Xp = 40, Sides = 0, Color = new Rgba(0.62f, 0.4f, 1f),
                Blurb = "Mirror Match: a dark copy of a player, with their class and their way of fighting. It sidesteps every few seconds." },
            // Zombie Infection's horde (Zombies.cs): green, dripping, and the dead may rise.
            new EnemyDef { Name = "Shambler", Hp = 70f, Radius = 14f, Speed = 70f, Score = 8, Xp = 10, Sides = 3, Color = new Rgba(0.5f, 0.83f, 0.35f), Melee = true, Contact = 18f,
                Picker = PickerGroup.Melee, Blurb = "Zombie: a wide, slow lurcher that hits hard. Don't get cornered." },
            new EnemyDef { Name = "Runner", Hp = 28f, Radius = 10f, Speed = 175f, Score = 5, Xp = 5, Sides = 3, Color = new Rgba(0.77f, 1f, 0.36f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "Zombie: frail but faster than you walk, in packs of three. Turn and shoot." },
            new EnemyDef { Name = "Spitter", Hp = 45f, Radius = 12f, Speed = 60f, Score = 8, Xp = 9, Sides = 5, Color = new Rgba(0.31f, 0.88f, 0.65f),
                Picker = PickerGroup.Ranged, Blurb = "Zombie: hangs back and lobs globs that leave burning, slowing acid. Keep moving." },
            new EnemyDef { Name = "Crawler", Hp = 55f, Radius = 11f, Speed = 95f, Score = 8, Xp = 8, Sides = 4, Color = new Rgba(0.25f, 0.67f, 0.33f), Melee = true, Contact = 6f,
                Picker = PickerGroup.Melee, Blurb = "Zombie: scuttles low and grabs you, rooting you in place. Shoot it to break free." },
            new EnemyDef { Name = "Bloater", Hp = 90f, Radius = 18f, Speed = 50f, Score = 10, Xp = 14, Sides = 12, Color = new Rgba(0.66f, 0.79f, 0.23f), Melee = true, Contact = 20f,
                Picker = PickerGroup.Melee, Blurb = "Zombie: swollen and slow; it bursts into a slowing spore cloud. Pop it at range." },
            new EnemyDef { Name = "Shrieker", Hp = 40f, Radius = 11f, Speed = 80f, Score = 10, Xp = 12, Sides = 4, Color = new Rgba(0.89f, 1f, 0.61f),
                Picker = PickerGroup.Ranged, Blurb = "Zombie: hangs back and screams, sending the horde round it into a frenzy. Silence it." },
            new EnemyDef { Name = "Mender", Hp = 50f, Radius = 12f, Speed = 65f, Score = 10, Xp = 14, Sides = 4, Color = new Rgba(0.18f, 0.84f, 0.49f),
                Picker = PickerGroup.Ranged, Blurb = "Zombie: heals the horde round it and makes the dead rise faster. Kill it first." },
            new EnemyDef { Name = "Buzzsaw", Hp = 30f, Radius = 12f, Speed = 170f, Score = 8, Xp = 8, Sides = 24, Color = new Rgba(0.35f, 0.82f, 1f), Melee = true, Contact = 4f,
                Picker = PickerGroup.Melee, Blurb = "A fast ring of spinning spikes that swoops round on curves. Each touch is small, but they come about eight times a second." },
            new EnemyDef { Name = "Spike Block", Hp = 60f, Radius = 14f, Speed = 120f, Score = 14, Xp = 14, Sides = 4, Color = new Rgba(0.9f, 0.28f, 0.3f), Melee = true, Contact = 12f,
                Picker = PickerGroup.Melee, Blurb = "Rocks from side to side as it closes in, then stops, flashes a red line and lunges for a big hit (35). Slow to recover." },
            new EnemyDef { Name = "Swarmer", Hp = 14f, Radius = 9f, Speed = 115f, Score = 3, Xp = 3, Sides = 24, Color = new Rgba(0.97f, 0.97f, 0.95f), Melee = true, Contact = 6f,
                Picker = PickerGroup.Melee, Blurb = "The simplest melee rusher: a white chomper with no eyes that walks straight at you. Weak alone, and it comes in packs." },
            // Enemies that don't shoot (NonShooters.cs). Melee = true keeps them out of the shooting turns; they're drawn without spikes.
            new EnemyDef { Name = "Fuse Runner", Hp = 50f, Radius = 12f, Speed = 200f, Score = 12, Xp = 10, Sides = 24, Color = new Rgba(1f, 0.55f, 0.15f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Late, Blurb = "A powder keg on the run: it plants its feet near you and blows up after a 0.8 s fuse, hurting enemies too. Step out, or shoot it early." },
            new EnemyDef { Name = "Hookman", Hp = 150f, Radius = 15f, Speed = 70f, Score = 25, Xp = 14, Sides = 4, Color = new Rgba(0.73f, 0.55f, 1f), Melee = true,
                Picker = PickerGroup.Late, Blurb = "Throws a harpoon along a blinking line and yanks you to its squad. Sidestep or parry; a miss leaves it open (+50%)." },
            new EnemyDef { Name = "Sapper", Hp = 110f, Radius = 14f, Speed = 85f, Score = 25, Xp = 14, Sides = 6, Color = new Rgba(0.91f, 0.81f, 0.29f), Melee = true,
                Picker = PickerGroup.Late, Blurb = "Plants tripwire pods; armed pods are joined by lasers. Cross a laser or step on a pod and it blows. Shoot a pod to use it on enemies." },
            new EnemyDef { Name = "Tripwire Pod", Hp = 1f, Radius = 7f, Speed = 0f, Score = 0, Xp = 0, Sides = 6, Color = new Rgba(1f, 0.42f, 0.35f), Melee = true, Heavy = true,
                Picker = PickerGroup.Hidden, Blurb = "A Sapper's pod: arms in 1 s, then a laser joins it to armed pods near it. Shoot it to blow it up." },
            new EnemyDef { Name = "Swordsman", Hp = 85f, Radius = 15f, Speed = 95f, Score = 22, Xp = 14, Sides = 24, Color = new Rgba(0.86f, 0.3f, 0.25f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "Walks up, raises a long sword and cuts the cone drawn on the floor in front of it. Step out of the cone, then hit it while it recovers." },
            new EnemyDef { Name = "Shield Swordsman", Hp = 85f, Radius = 15f, Speed = 75f, Score = 28, Xp = 16, Sides = 24, Color = new Rgba(0.86f, 0.3f, 0.25f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "A Swordsman behind a shield that stops every shot from the front. The shield drops when it cuts and stays down while it recovers: shoot it then, or from the side." },
            new EnemyDef { Name = "Taser Guard", Hp = 70f, Radius = 13f, Speed = 80f, Score = 9, Xp = 11, Sides = 8, Color = new Rgba(0.4f, 0.6f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Keeps its distance, charges its taser and fires a white Fork Bolt that stuns you. Dodge the bolt, or parry it." },
            new EnemyDef { Name = "Security Guard", Hp = 45f, Radius = 15f, Speed = 70f, Score = 9, Xp = 9, Sides = 24, Color = new Rgba(0.42f, 0.61f, 0.7f),
                Picker = PickerGroup.Ranged, Blurb = "Locks on where you're heading, then fires one tracer round at a time: seven shots, then a long reload. Change direction in the red lane, or rush it while it reloads." },
            new EnemyDef { Name = "Thug", Hp = 140f, Radius = 19f, Speed = 62f, Score = 10, Xp = 10, Sides = 7, Color = new Rgba(0.6f, 0.62f, 0.66f), Melee = true, Contact = 20f,
                Picker = PickerGroup.Melee, Blurb = "A prisoner brute dragging a ball and chain. Slow and tough, hits hard when it reaches you." },
            new EnemyDef { Name = "Shiv", Hp = 45f, Radius = 12f, Speed = 95f, Score = 9, Xp = 9, Sides = 4, Color = new Rgba(0.85f, 0.85f, 0.8f), Melee = true, Contact = 18f,
                Picker = PickerGroup.Melee, Blurb = "A prisoner with a taped shank. Circles in close, flashes a line, then lunges along it." },
            new EnemyDef { Name = "Spawn Portal", Hp = 200f, Radius = 30f, Speed = 0f, Score = 30, Xp = 20, Sides = 24, Color = new Rgba(0.78f, 0.35f, 1f), Melee = true, Heavy = true,
                Picker = PickerGroup.Hidden, Blurb = "A burst of the wave comes through it. Shoot it closed before it empties to cancel the rest; it drops coins for each one it held." },
            // the Hive Queen's faction (HiveKin.cs): the numbers are their Warden counterpart's, bar the Stinger's and Needler's health (half as much again)
            new EnemyDef { Name = "Worker", Hp = 40f, Radius = 12f, Speed = 110f, Score = 3, Xp = 7, Sides = 8, Color = new Rgba(1f, 0.773f, 0.2f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "A small, quick worker bee that weaves at you, in loose pairs." },
            new EnemyDef { Name = "Soldier", Hp = 90f, Radius = 13f, Speed = 125f, Score = 6, Xp = 15, Sides = 8, Color = new Rgba(1f, 0.478f, 0.122f), Melee = true, Contact = 14f,
                Picker = PickerGroup.Melee, Blurb = "An armoured bee that rushes straight at you, jaws snapping." },
            new EnemyDef { Name = "Comb Shield", Hp = 60f, Radius = 16f, Speed = 120f, Score = 10, Xp = 10, Sides = 6, Color = new Rgba(0.953f, 0.894f, 0.702f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "A bee behind a plate of wax cells that stops shots and bashes you back. Break the cells, or go round it." },
            new EnemyDef { Name = "Honey Lobber", Hp = 50f, Radius = 14f, Speed = 75f, Score = 8, Xp = 9, Sides = 5, Color = new Rgba(1f, 0.635f, 0.102f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs honey at you. A ring marks the spot; the puddle slows whoever stands in it." },
            new EnemyDef { Name = "Stinger", Hp = 105f, Radius = 13f, Speed = 80f, Score = 9, Xp = 11, Sides = 8, Color = new Rgba(0.784f, 1f, 0.239f),
                Picker = PickerGroup.Ranged, Blurb = "Keeps its distance, charges a venom lance and fires one barbed dart that stuns you. Strafe across the line, or parry it." },
            new EnemyDef { Name = "Needler", Hp = 70f, Radius = 15f, Speed = 70f, Score = 9, Xp = 9, Sides = 24, Color = new Rgba(1f, 0.925f, 0.604f),
                Picker = PickerGroup.Ranged, Blurb = "Hovers and strafes, locks on, then fires one white needle at a time: seven shots, then a long reload." },
            new EnemyDef { Name = "Bumble", Hp = 140f, Radius = 19f, Speed = 62f, Score = 10, Xp = 10, Sides = 7, Color = new Rgba(0.91f, 0.573f, 0.047f), Melee = true, Contact = 20f,
                Picker = PickerGroup.Melee, Blurb = "A huge, furry bumblebee. Slow and tough, hits hard when it reaches you." },
            new EnemyDef { Name = "Hornet", Hp = 45f, Radius = 12f, Speed = 95f, Score = 9, Xp = 9, Sides = 4, Color = new Rgba(1f, 0.29f, 0.157f), Melee = true, Contact = 18f,
                Picker = PickerGroup.Melee, Blurb = "Circles in close, flashes a red lane, then darts along it." },
            // The Twin Reapers' faction (ReaperFaction.cs). Each is sworn to Dusk (pink) or Dawn (blue): drawn in its side's colour.
            new EnemyDef { Name = "Wisp", Hp = 14f, Radius = 9f, Speed = 120f, Score = 3, Xp = 3, Sides = 3, Color = new Rgba(1f, 0.35f, 0.6f), Melee = true, Contact = 6f,
                Picker = PickerGroup.Melee, Blurb = "A soul flame that drifts straight at you in a loose pack and burns on touch. Thin them before they reach you." },
            new EnemyDef { Name = "Acolyte", Hp = 90f, Radius = 13f, Speed = 120f, Score = 14, Xp = 12, Sides = 6, Color = new Rgba(0.45f, 0.55f, 1f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "A hooded chaser. Stops just short, raises its sickle over a pale cone, then one wide swing. Step back out of the cone." },
            new EnemyDef { Name = "Bound Wraith", Hp = 60f, Radius = 12f, Speed = 95f, Score = 10, Xp = 9, Sides = 5, Color = new Rgba(1f, 0.35f, 0.6f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "Comes as a pair circling near you, joined by a tether that hurts. Kill one and the other brings it back in 3 s, unless you kill both." },
            new EnemyDef { Name = "Scythe Wraith", Hp = 75f, Radius = 14f, Speed = 85f, Score = 12, Xp = 11, Sides = 5, Color = new Rgba(0.45f, 0.55f, 1f), Melee = true, Contact = 18f,
                Picker = PickerGroup.Melee, Blurb = "Marks a long line through you, then dashes along it and reaps both sides of its path. Leave the line sideways." },
            new EnemyDef { Name = "Lamplighter", Hp = 55f, Radius = 13f, Speed = 70f, Score = 10, Xp = 10, Sides = 4, Color = new Rgba(1f, 0.35f, 0.6f),
                Picker = PickerGroup.Ranged, Blurb = "Plants its feet, its lantern flares and gold lines show the volley: three lines at you, then a four-way cross." },
            new EnemyDef { Name = "Mourner", Hp = 80f, Radius = 15f, Speed = 60f, Score = 12, Xp = 12, Sides = 7, Color = new Rgba(0.45f, 0.55f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Stands still and weeps a twin spiral, one arm each way, then rests. Push in while it rests." },
            new EnemyDef { Name = "Gravedigger", Hp = 140f, Radius = 19f, Speed = 62f, Score = 10, Xp = 10, Sides = 8, Color = new Rgba(1f, 0.35f, 0.6f), Melee = true, Contact = 20f,
                Picker = PickerGroup.Melee, Blurb = "A cultist brute with a shovel. Slow and tough, hits hard when it reaches you." },
            new EnemyDef { Name = "Shade", Hp = 90f, Radius = 13f, Speed = 90f, Score = 25, Xp = 22, Sides = 3, Color = new Rgba(0.45f, 0.55f, 1f),
                Picker = PickerGroup.Late, Late = true, Blurb = "A shrouded Phantom: fades to smoke, shows a ring where it will land, appears and fires." },
            new EnemyDef { Name = "Debris", Hp = 22f, Radius = 10f, Speed = 125f, Score = 3, Xp = 6, Sides = 7, Color = new Rgba(0.85f, 0.35f, 1f), Melee = true, Contact = 9f,
                Picker = PickerGroup.Melee, Blurb = "Tumbling rubble that falls at you on a curving inward spiral, so it's hard to line up." },
            new EnemyDef { Name = "Void Acolyte", Hp = 45f, Radius = 13f, Speed = 85f, Score = 7, Xp = 10, Sides = 6, Color = new Rgba(1f, 0.47f, 0.86f),
                Picker = PickerGroup.Ranged, Blurb = "A void cultist. Fires three slow orbs: the outer two bend in and meet where you stand. Step forward or back, not sideways." },
            new EnemyDef { Name = "Pulsar", Hp = 70f, Radius = 13f, Speed = 75f, Score = 9, Xp = 12, Sides = 12, Color = new Rgba(0.5f, 0.84f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "A spinning neutron star. Its two beams flicker for a second, then switch on and sweep a quarter turn. Walk with the sweep." },
            new EnemyDef { Name = "Graviton", Hp = 50f, Radius = 14f, Speed = 80f, Score = 8, Xp = 10, Sides = 6, Color = new Rgba(0.75f, 0.5f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs a small gravity well by you every 6 s. For 4 s it drags you in and swallows shots near it. Get clear, or shoot the well." },
            new EnemyDef { Name = "Orbiter", Hp = 60f, Radius = 12f, Speed = 95f, Score = 8, Xp = 11, Sides = 10, Color = new Rgba(0.85f, 0.35f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Circles you with three orbs turning round it. One flares, then it slings that orb straight at you, fast." },
            new EnemyDef { Name = "Collapsar", Hp = 220f, Radius = 22f, Speed = 58f, Score = 12, Xp = 16, Sides = 14, Color = new Rgba(1f, 0.47f, 0.86f), Melee = true, Contact = 20f,
                Picker = PickerGroup.Melee, Blurb = "A slow, dense star that shrinks as it's shot. Killed, it collapses: a short pull, then a ring of 12 shots. Kill it at range." },
            new EnemyDef { Name = "Small Well", Hp = 30f, Radius = 11f, Speed = 0f, Score = 1, Xp = 3, Sides = 6, Color = new Rgba(0.85f, 0.35f, 1f), Melee = true,
                Picker = PickerGroup.Hidden },
            new EnemyDef { Name = "Implosion", Hp = 1e6f, Radius = 10f, Speed = 0f, Score = 0, Xp = 0, Sides = 6, Color = new Rgba(1f, 0.47f, 0.86f), Melee = true,
                Picker = PickerGroup.Hidden },
            new EnemyDef { Name = "Lens", Hp = 90f, Radius = 14f, Speed = 70f, Score = 9, Xp = 11, Sides = 8, Color = new Rgba(0.5f, 0.84f, 1f), Melee = true, Contact = 12f,
                Picker = PickerGroup.Melee, Blurb = "Carries a gravity lens in front of it. Shots into its face curve round it and away; it turns slowly, so hit it from the side." },
            new EnemyDef { Name = "White Dwarf", Hp = 40f, Radius = 9f, Speed = 230f, Score = 8, Xp = 10, Sides = 4, Color = new Rgba(1f, 0.96f, 0.9f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "A small hot star that darts in, flashes white, then shoves you back and turns your shots near it round at you. Stop shooting when it flashes." },
            // the Colossus's faction (QuarryFaction.cs): Rubble and the Quarryman have their Warden counterpart's numbers
            new EnemyDef { Name = "Rubble", Hp = 40f, Radius = 12f, Speed = 110f, Score = 3, Xp = 7, Sides = 7, Color = new Rgba(0.78f, 0.62f, 0.45f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "A fist-sized chunk of the Colossus that rolls at you, weaving, in pairs." },
            new EnemyDef { Name = "Quarryman", Hp = 90f, Radius = 13f, Speed = 125f, Score = 6, Xp = 15, Sides = 8, Color = new Rgba(0.62f, 0.53f, 0.4f), Melee = true, Contact = 14f,
                Picker = PickerGroup.Melee, Blurb = "A quarry worker with a hard hat, a headlamp and a pickaxe. Walks straight at you." },
            new EnemyDef { Name = "Stomper", Hp = 180f, Radius = 21f, Speed = 58f, Score = 12, Xp = 20, Sides = 7, Color = new Rgba(0.69f, 0.6f, 0.5f), Melee = true, Contact = 20f,
                Picker = PickerGroup.Melee, Blurb = "A slab-shouldered stone brute. Every few seconds it lifts a foot and stomps out a small ring of push blocks with one green gap: step into the gap." },
            new EnemyDef { Name = "Slab Bearer", Hp = 60f, Radius = 16f, Speed = 120f, Score = 10, Xp = 10, Sides = 6, Color = new Rgba(0.62f, 0.53f, 0.4f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "Carries a quarried slab that stops shots and bashes you back. Break the slab and it drops it as cover for a few seconds; then the bearer falls fast." },
            new EnemyDef { Name = "Rock Hurler", Hp = 50f, Radius = 14f, Speed = 75f, Score = 8, Xp = 9, Sides = 5, Color = new Rgba(0.7f, 0.6f, 0.49f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs a boulder where you're heading. Walk out of the ring: it lands hard, bursts into shards and leaves a rubble pile that stops shots." },
            new EnemyDef { Name = "Driller", Hp = 45f, Radius = 12f, Speed = 95f, Score = 9, Xp = 9, Sides = 4, Color = new Rgba(0.62f, 0.53f, 0.4f), Melee = true, Contact = 18f,
                Picker = PickerGroup.Melee, Blurb = "Circles in close with a jackhammer, shows a lane, then drills along it. The crack it leaves erupts a moment later: get off the line." },
            new EnemyDef { Name = "Geomancer", Hp = 70f, Radius = 13f, Speed = 80f, Score = 9, Xp = 11, Sides = 8, Color = new Rgba(0.48f, 0.4f, 0.31f),
                Picker = PickerGroup.Ranged, Blurb = "Keeps its distance, plants its crystal staff and sends a crack racing at you. It glows, then erupts: step off the line." },
            new EnemyDef { Name = "Cinder Priest", Hp = 70f, Radius = 13f, Speed = 80f, Score = 9, Xp = 11, Sides = 8, Color = new Rgba(0.24f, 0.2f, 0.19f),
                Picker = PickerGroup.Ranged, Blurb = "Charges a molten core, then fires one magma bolt straight at you. Wherever it stops, it leaves a pool of lava. Strafe across the line, or parry it." },
            new EnemyDef { Name = "Riot Captain", Hp = 3000f, Radius = 30f, Speed = 70f, Score = 150, Xp = 80, Sides = 8, Color = new Rgba(0.5f, 0.66f, 1f), Heavy = true, Contact = 20f, Mini = true,
                Blurb = "The Warden's mini-boss: a tower shield blocks the front. Bait its shield charge into a wall to daze it; it also fires buckshot, lobs tear gas and whistles for Taser Guards." },
            // the Duelist's faction (CourtFaction.cs): masked fencers
            new EnemyDef { Name = "Fencer", Hp = 32f, Radius = 11f, Speed = 115f, Score = 4, Xp = 7, Sides = 4, Color = new Rgba(0.81f, 0.84f, 1f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "A masked foil-fencer that runs in crowds. Close up it shows a short red line, then hops down it: step off the line." },
            new EnemyDef { Name = "Blade Dancer", Hp = 70f, Radius = 13f, Speed = 105f, Score = 8, Xp = 14, Sides = 5, Color = new Rgba(1f, 0.3f, 0.42f), Melee = true, Contact = 14f,
                Picker = PickerGroup.Melee, Blurb = "Shows a long warning line, then lunges down it and leaves blades hanging on the path. Each fires one shot out of each side: never stand beside the line." },
            new EnemyDef { Name = "Parrier", Hp = 60f, Radius = 15f, Speed = 110f, Score = 10, Xp = 10, Sides = 6, Color = new Rgba(1f, 0.8f, 0.35f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "On guard behind a main-gauche: shots into its front are parried straight back at you, and it bashes you away. It turns slowly: get behind it." },
            new EnemyDef { Name = "Second", Hp = 45f, Radius = 13f, Speed = 70f, Score = 9, Xp = 9, Sides = 5, Color = new Rgba(1f, 0.8f, 0.35f),
                Picker = PickerGroup.Ranged, Blurb = "Hangs back, glints gold, then throws an aimed fan of five daggers. Slip between them." },
            new EnemyDef { Name = "Marshal", Hp = 55f, Radius = 14f, Speed = 75f, Score = 8, Xp = 9, Sides = 6, Color = new Rgba(0.85f, 0.75f, 0.54f),
                Picker = PickerGroup.Ranged, Blurb = "The duel's referee: raises its baton and draws a gold circle round you. For 4 s you can't leave it, and leaning on the edge hurts: fight in the middle." },
            new EnemyDef { Name = "Gallant", Hp = 80f, Radius = 14f, Speed = 90f, Score = 10, Xp = 12, Sides = 6, Color = new Rgba(0.81f, 0.84f, 1f), Contact = 20f,
                Picker = PickerGroup.Ranged, Blurb = "Salutes with its sword raised, then lunges at you. Parry it perfectly as it arrives and it reels, open to 50% more damage; dodge it and it rakes the spot." },
            new EnemyDef { Name = "Harlequin", Hp = 240f, Radius = 20f, Speed = 60f, Score = 14, Xp = 22, Sides = 8, Color = new Rgba(1f, 0.3f, 0.42f), Melee = true, Contact = 20f,
                Picker = PickerGroup.Melee, Blurb = "A heavy two-sworded jester. It marks a line through you and dashes it, vaults to the other arm and dashes across: an X of blades that fire out sideways. Stand in a gap of the X." },
            new EnemyDef { Name = "Mummer", Hp = 40f, Radius = 11f, Speed = 150f, Score = 9, Xp = 10, Sides = 4, Color = new Rgba(0.71f, 0.61f, 1f), Melee = true, Contact = 14f,
                Picker = PickerGroup.Melee, Blurb = "A masked player that blinks and becomes three. They circle you and all fire, but only the real one's shots are solid; the copies' are hollow rings. Find the real one." },
            new EnemyDef { Name = "Mummer", Hp = 1f, Radius = 11f, Speed = 0f, Score = 0, Xp = 0, Sides = 4, Color = new Rgba(0.71f, 0.61f, 1f), Melee = true, Contact = 0f,
                Picker = PickerGroup.Hidden, Blurb = "One of a Mummer's two copies: its shots are hollow rings that pass through you. It vanishes when its time is up." },
            new EnemyDef { Name = "Grave Caller", Hp = 60f, Radius = 14f, Speed = 60f, Score = 12, Xp = 16, Sides = 4, Color = new Rgba(0.37f, 0.85f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Zombie: hangs back and cracks three graves open round you; a Shambler claws out of each. Step out of the ring, then kill it: its risen crumble." },
            // the Devourer's faction (LarderFaction.cs): the Gnawling and the Butcher have their Warden counterpart's numbers
            new EnemyDef { Name = "Gnawling", Hp = 40f, Radius = 11f, Speed = 110f, Score = 3, Xp = 7, Sides = 8, Color = new Rgba(0.91f, 0.53f, 0.6f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "A mouth on legs, the Devourer in miniature. Weaves at you in pairs." },
            new EnemyDef { Name = "Butcher", Hp = 90f, Radius = 13f, Speed = 125f, Score = 6, Xp = 15, Sides = 8, Color = new Rgba(0.43f, 0.29f, 0.24f), Melee = true, Contact = 14f,
                Picker = PickerGroup.Melee, Blurb = "A hooded butcher with a bone mask. Close in, it raises its cleaver and chops a half-circle in front: step out of it." },
            new EnemyDef { Name = "Gulper", Hp = 180f, Radius = 22f, Speed = 58f, Score = 12, Xp = 25, Sides = 9, Color = new Rgba(0.86f, 0.5f, 0.56f), Melee = true, Contact = 20f,
                Picker = PickerGroup.Melee, Blurb = "A swollen gut. Its cheeks swell, then it breathes in and pulls you toward its mouth: step out of the cone sideways." },
            new EnemyDef { Name = "Carcass Bearer", Hp = 60f, Radius = 16f, Speed = 120f, Score = 10, Xp = 10, Sides = 6, Color = new Rgba(0.37f, 0.29f, 0.25f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "Carries a side of meat that stops shots and bashes you back. Break it and it drops as a morsel: shoot it or walk over it before a Gnawling eats it." },
            new EnemyDef { Name = "Bile Spitter", Hp = 50f, Radius = 14f, Speed = 75f, Score = 8, Xp = 9, Sides = 5, Color = new Rgba(0.84f, 0.55f, 0.55f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs a glob of bile where you're heading. Walk out of the ring: it leaves a pool of acid for a few seconds." },
            new EnemyDef { Name = "Dust Devil", Hp = 22f, Radius = 10f, Speed = 135f, Score = 3, Xp = 6, Sides = 6, Color = new Rgba(0.62f, 0.91f, 0.85f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "A small whirlwind that weaves in at you. Its touch shoves you hard, into whatever is behind you." },
            new EnemyDef { Name = "Squall Gunner", Hp = 45f, Radius = 13f, Speed = 85f, Score = 7, Xp = 10, Sides = 5, Color = new Rgba(0.56f, 0.82f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Carries its own gust: it aims three shots upwind of you, and they curve back onto you. Dodge against the curve." },
            new EnemyDef { Name = "Stormcaller", Hp = 50f, Radius = 13f, Speed = 80f, Score = 8, Xp = 11, Sides = 7, Color = new Rgba(1f, 0.82f, 0.4f),
                Picker = PickerGroup.Ranged, Blurb = "Raises its staff and calls lightning on you: a ring fills under you, then the strike. Step out of the ring." },
        };

        public const int WeaponsPerRole = 3;  // every role and class has three weapons (Choice 0-2); a few have a fourth (Choice 3, the lobby's 4th key): WeaponCount
        public const int MaxWeapons = 6;  // (the fifth and sixth are the Twin Rifle's variant weapons, Choice 4 and 5: the Auto and Out of Ammo; the Soldier DPS's fourth is the Double Barrel)

        public const int KitCount = 6;

        /// Every weapon, in lobby order (Role, then Kit, then Choice), gathered from each class's list: Weapons.cs (Soldier),
        /// Minions.cs (Mage, Summoner), Knight.cs, Ninja.cs, BloodMage.cs. Every place must be filled exactly once.
        /// The three weapons of every role and class come first (indexed by role, kit and choice); the fourth weapons follow them.
        public static readonly WeaponDef[] Weapons = WeaponRegistry(Game.SoldierWeapons, Game.ElementalistWeapons, Game.SummonerWeapons,
            Game.KnightWeapons, Game.NinjaWeapons, Game.HemomancerWeapons);

        static WeaponDef[] fifth;   // a role and class's fifth weapon (Choice 4, a variant), or null
        static WeaponDef[] sixth;   // a role and class's sixth weapon (Choice 5, a second variant), or null
        static WeaponDef[] fourth;  // a role and class's fourth weapon (Choice 3), or null: [role * KitCount + kit]

        /// How many weapons a role and class has in the lobby: three, or four where it has a fourth (the Knight DPS's Arc
        /// Knuckles). A Wildcard picks from WildcardChoices (every class's first three DPS weapons).
        public static int WeaponCount(Role role, Kit kit) =>
            role == Role.Wildcard ? WildcardWeaponChoices : WeaponsPerRole + (Fourth(role, kit) != null && !Fourth(role, kit).Variant ? 1 : 0);

        /// The highest weapon pick a role and class accepts, plus one: WeaponCount, and one more where its fourth weapon is a variant
        /// (Variants.cs: picked in the Variants window, so the lobby shows no key for it).
        public static int WeaponLimit(Role role, Kit kit) =>
            role == Role.Wildcard ? WildcardWeaponChoices : WeaponsPerRole + (Fourth(role, kit) != null ? 1 : 0) + (Fifth(role, kit) != null ? 1 : 0) + (Sixth(role, kit) != null ? 1 : 0);

        /// How many weapons a Wildcard can pick from: every class's three DPS ones (kit * 3 + choice, as its abilities are
        /// numbered), then the DPS fourth weapons after them, in class order (WildcardChoices + n: the Arc Knuckles are 18).
        public static int WildcardWeaponChoices => WildcardChoices + WildFourthKits().Count;

        static List<Kit> wildFourth;

        /// The classes with a fourth DPS weapon, in class order (the Wildcard's picks after WildcardChoices).
        static List<Kit> WildFourthKits()
        {
            if (wildFourth == null)
            {
                wildFourth = new List<Kit>();
                for (int k = 0; k < KitCount; k++)
                    if (Fourth(Role.Dps, (Kit)k) != null && !Fourth(Role.Dps, (Kit)k).Variant)
                        wildFourth.Add((Kit)k);
            }
            return wildFourth;
        }

        /// The class a Wildcard weapon pick comes from.
        public static Kit WildWeaponKit(int pick)
        {
            if (pick >= 0 && pick < WildcardChoices)
                return (Kit)(pick / WeaponsPerRole);
            var f = WildFourthKits();
            int i = pick - WildcardChoices;
            return i >= 0 && i < f.Count ? f[i] : Kit.Soldier;
        }

        /// A Wildcard weapon pick from a class's DPS weapon `choice` (0-2, or 3 for its fourth).
        public static int WildWeaponPick(Kit kit, int choice)
        {
            if (choice < WeaponsPerRole)
                return (int)kit * WeaponsPerRole + Math.Max(0, choice);
            int i = WildFourthKits().IndexOf(kit);
            return i >= 0 ? WildcardChoices + i : (int)kit * WeaponsPerRole;
        }

        static WeaponDef Fifth(Role role, Kit kit)
        {
            if (Weapons == null || (int)role >= 3 || (int)kit >= KitCount)
                return null;
            return fifth[(int)role * KitCount + (int)kit];
        }

        /// The weapon pick (index for Weapon) of `id` for a role and class, or 0 if it isn't one of theirs.
        public static int PickOf(Role role, Kit kit, WeaponId id)
        {
            for (int i = 0; i < WeaponLimit(role, kit); i++)
                if (Weapon(role, kit, i).Id == id)
                    return i;
            return 0;
        }

        static WeaponDef Sixth(Role role, Kit kit)
        {
            if (Weapons == null || (int)role >= 3 || (int)kit >= KitCount)
                return null;
            return sixth[(int)role * KitCount + (int)kit];
        }

        static WeaponDef Fourth(Role role, Kit kit)
        {
            if (Weapons == null || (int)role >= 3 || (int)kit >= KitCount)
                return null;
            return fourth[(int)role * KitCount + (int)kit];
        }

        /// A weapon's damage a second with no upgrades (companions and burning included), measured in the sim
        /// (Tools/SimTest/run.sh 1 1 weapondps write, which writes WeaponDps.Generated.cs); 0 if not measured.
        public static float WeaponDps(WeaponId id) => MeasuredDps.TryGetValue(id, out float v) ? v : 0f;

        /// How many weapons (or Space abilities) a Wildcard can pick from: every class's three DPS ones, numbered kit * 3 + choice.
        public const int WildcardChoices = KitCount * WeaponsPerRole;

        /// A Wildcard's lobby "wild" byte: its second ability (0-17, same numbering as the first) and the key it sits on (1 Q, 2 E, 3 Shift).
        public static int WildSecond(int wild) => wild % WildcardChoices;
        public static int WildKey(int wild) => 1 + Math.Min(2, wild / WildcardChoices);
        public static byte MakeWild(int second, int key) => (byte)(Math.Max(0, Math.Min(WildcardChoices - 1, second)) + WildcardChoices * (Math.Max(1, Math.Min(3, key)) - 1));

        public static WeaponDef Weapon(Role role, Kit kit, int index)
        {
            if (role == Role.Wildcard)  // index = kit * 3 + choice, DPS gear of any class; then the DPS fourth weapons (WildWeaponPick)
            {
                if (index >= WildcardChoices && index < WildcardWeaponChoices)
                    return Fourth(Role.Dps, WildWeaponKit(index));
                index = index >= 0 && index < WildcardChoices ? index : 0;
                return Weapons[((int)Role.Dps * KitCount + index / WeaponsPerRole) * WeaponsPerRole + index % WeaponsPerRole];
            }
            if (index == WeaponsPerRole && Fourth(role, kit) != null)
                return Fourth(role, kit);
            if (index == WeaponsPerRole + 1 && Fifth(role, kit) != null)
                return Fifth(role, kit);
            if (index == WeaponsPerRole + 2 && Sixth(role, kit) != null)
                return Sixth(role, kit);
            if (index < 0 || index >= WeaponsPerRole)
                index = 0;
            return Weapons[((int)role * KitCount + (int)kit) * WeaponsPerRole + index];
        }

        /// Puts every class's weapons in lobby order. The game refuses to start if a place is empty or doubled, or a weapon id is missing.
        static WeaponDef[] WeaponRegistry(params WeaponDef[][] lists)
        {
            var all = new WeaponDef[3 * KitCount * WeaponsPerRole];
            var four = new WeaponDef[3 * KitCount];
            var five = new WeaponDef[3 * KitCount];
            var six = new WeaponDef[3 * KitCount];
            var seen = new bool[Enum.GetValues(typeof(WeaponId)).Length];
            foreach (var list in lists)
                foreach (var w in list)
                {
                    if (w.Choice == WeaponsPerRole + 2 && (int)w.Kit < KitCount && w.Role != Role.Wildcard)
                    {
                        int f6 = (int)w.Role * KitCount + (int)w.Kit;
                        if (six[f6] != null || seen[(int)w.Id])
                            throw new InvalidOperationException(w.Id + ": two sixth weapons for one place, or two WeaponDefs");
                        six[f6] = w;
                        seen[(int)w.Id] = true;
                        continue;
                    }
                    if (w.Choice == WeaponsPerRole + 1 && (int)w.Kit < KitCount && w.Role != Role.Wildcard)
                    {
                        int f5 = (int)w.Role * KitCount + (int)w.Kit;
                        if (five[f5] != null || seen[(int)w.Id])
                            throw new InvalidOperationException(w.Id + ": two fifth weapons for one place, or two WeaponDefs");
                        five[f5] = w;
                        seen[(int)w.Id] = true;
                        continue;
                    }
                    if (w.Choice == WeaponsPerRole && (int)w.Kit < KitCount && w.Role != Role.Wildcard)
                    {
                        int f = (int)w.Role * KitCount + (int)w.Kit;
                        if (four[f] != null || seen[(int)w.Id])
                            throw new InvalidOperationException(w.Id + ": two fourth weapons for one place, or two WeaponDefs");
                        four[f] = w;
                        seen[(int)w.Id] = true;
                        continue;
                    }
                    if (w.Choice < 0 || w.Choice >= WeaponsPerRole || (int)w.Kit >= KitCount)
                        throw new InvalidOperationException(w.Id + " has no lobby place (Kit " + w.Kit + ", Choice " + w.Choice + ")");
                    int i = ((int)w.Role * KitCount + (int)w.Kit) * WeaponsPerRole + w.Choice;
                    if (all[i] != null)
                        throw new InvalidOperationException(w.Id + " and " + all[i].Id + " are in the same lobby place");
                    if (seen[(int)w.Id])
                        throw new InvalidOperationException("Two WeaponDefs for " + w.Id);
                    all[i] = w;
                    seen[(int)w.Id] = true;
                }
            for (int i = 0; i < all.Length; i++)
                if (all[i] == null)
                    throw new InvalidOperationException("No " + (Role)(i / (KitCount * WeaponsPerRole)) + " weapon (Choice " + i % WeaponsPerRole
                        + ") for Kit." + (Kit)(i / WeaponsPerRole % KitCount) + ": add it to that class's WeaponDef list");
            for (int i = 0; i < seen.Length; i++)
                if (!seen[i])
                    throw new InvalidOperationException("No WeaponDef for " + (WeaponId)i);
            fourth = four;
            fifth = five;
            sixth = six;
            var withFourth = new List<WeaponDef>(all);
            foreach (var w in four)
                if (w != null)
                    withFourth.Add(w);
            foreach (var w in five)
                if (w != null)
                    withFourth.Add(w);
            foreach (var w in six)
                if (w != null)
                    withFourth.Add(w);
            return withFourth.ToArray();
        }

        static WeaponDef[] byId;

        /// The definition of a weapon, by id.
        public static WeaponDef WeaponOf(WeaponId id)
        {
            if (byId == null)
            {
                var all = new WeaponDef[Weapons.Length];
                foreach (var w in Weapons)
                    all[(int)w.Id] = w;
                byId = all;
            }
            return byId[(int)id];
        }

        /// A class's name, health and speed shares come from its KitDef (Classes.cs): mages and summoners trade some health
        /// for their spells and minions; knights wear armour to get close; ninjas are quick and lightly protected; blood
        /// mages keep their full health to spend.
        public static string KitName(Kit k) => Classes.Get(k).Name;
        public static float KitHpMult(Kit k) => Classes.Get(k).HpMult;
        public static float KitSpeedMult(Kit k) => Classes.Get(k).SpeedMult;

        /// XP needed to go from this level to the next.
        public static float XpToNext(int level) => 90f + 55f * (level - 1);

        public static RoleDef Get(Role r) => Roles[(int)r];
        public static EnemyDef Get(EnemyType t) => Enemies[(int)t];

        // Bullet kinds. Kinds >= SmallShot belong to enemies.
        public const byte TankShot = 0, HealerShot = 1, DpsShot = 2, TurretShot = 3, NovaShot = 4, FlakShell = 5, RailShot = 6, WispShot = 7, GrenadeShot = 8, SwarmShot = 9, SmallShot = 10, BigShot = 11, FastShot = 12, MineShot = 13, PierceShot = 14, StunShot = 15, HoneyShot = 16, HollowShot = 17, BileShot = 18, ToothShot = 19,
            // more player kinds (enemy kinds are 10-19)
            FireShot = 20, FrostShot = 21, ArcaneShot = 22, HolyShot = 23, BoltShot = 24, SeedShot = 25, BoneShot = 26, RockShot = 27, MinionShot = 28,
            KnightSlash = 29,  // never flies: stands in for a melee hit so weapon mods and legendaries apply to it
            KnightWave = 30,   // a crescent of steel or light thrown by a swing
            ArrowShot = 31,    // a Mender's Bow arrow
            // Ninja
            StarShot = 32, NeedleShot = 33, KunaiShot = 34, OfudaShot = 35, GustShot = 36, FumaStar = 37,
            FrostLance = 38,  // retired: the Frost Lance no longer flies (it is an icicle swing, Minions.cs)
            // Blood Mage
            BloodShot = 39, ClotShot = 40, LeechShot = 41, TransfuseShot = 42, SanguineShot = 43, VeinShot = 44, ImplodeShot = 45, BoneSpearShot = 46,
            StunBolt = 47,   // the Stun Gun's bolt (Stun.cs)
            FlameShot = 48,  // a Flamethrower flame
            HammerShot = 49, // the Storm Hammer, spinning as it flies
            BoulderShot = 50, // the Golem Staff's boulder, lobbed: it flies over enemies and crushes where it lands
            FireballShot = 51, // the Fireball: big, rolling on through enemies and setting them burning
            CrescentShot = 52, // the Eclipse's crescent blade (an enemy kind: 10-19 were all taken)
            PauseShot = 53,    // the Pauser's shots: fly out, stop dead and hang, then the host fires a rush shot from each (Variety.cs)
            StitchShot = 54,   // the Stitcher's orbs: each is tied to the nearest other one by a harmful thread (Variety.cs)
            FireflyShot = 55,  // the Sprite Staff's firefly: seeds what it hits, then hops to the nearest enemy that isn't seeded (KitWeapons.cs)
            MedkitShot = 56,   // the Deployable Medkit, lobbed: it flies over everything and bursts into healing orbs where it lands (Medkit.cs)
            TreantSeed = 57,   // a tiny seed the Treant Rod's Treant flicks at enemies, drawn as a thorn dart (KitWeapons.cs, Game/SeedLook.cs)
            SteelShard = 58,   // a bullet Caught Steel caught, hurled back as a sliver of steel (CaughtSteel.cs, Game/CaughtSteelLook.cs)
            ParryNeedle = 59,  // a perfectly parried shot, sent straight back as a fast white needle (Parry.cs, Game/ParryLook.cs)
            Caltrop = 60,      // a Katana caltrop: thrown, lying (it hurts and slows what steps on it), flung or seeking (Katana.cs, Game/KatanaLook.cs)
            TaserBolt = 61,    // the Taser Guard's Fork Bolt: a white lightning glyph that stuns you for a second (WardenFaction.cs, Game/TaserLook.cs)
            TracerRound = 62,  // the Security Guard's pistol round: a hot-white tracer with a long tail (SecurityGuard.cs, Game/SecurityLook.cs)
            StingBolt = 63,    // the Stinger's venom dart: a lime barbed dart that stuns like the Fork Bolt (WardenFaction.cs, Game/HiveLook.cs)
            MagmaBolt = 64,    // the Cinder Priest's magma bolt: it leaves a lava pool wherever it stops (QuarryFaction.cs, Game/QuarryLook.cs)
            ThrownDagger = 65; // a Second's thrown dagger: steel with a gold hilt, spinning a little (CourtFaction.cs, Game/CourtLook.cs)

        public static bool IsEnemyShot(byte kind) => kind >= 10 && kind < 20 || kind == CrescentShot || kind == PauseShot || kind == StitchShot || kind == TaserBolt || kind == TracerRound || kind == StingBolt || kind == MagmaBolt || kind == ThrownDagger;

        /// Bosses' aimed attacks: barriers, walls and other blockers can't stop these.
        public static bool Unblockable(byte kind) => kind == PierceShot;

        public static float BulletRadius(byte kind)
        {
            switch (kind)
            {
                case TankShot: return 5f;
                case HealerShot: return 4.5f;
                case DpsShot: return 3.5f;
                case TurretShot: return 4f;
                case NovaShot: return 6f;
                case FlakShell: return 8f;
                case RailShot: return 3.5f;
                case WispShot: return 5f;  // no longer fired by any weapon (Twin Wisps became the Pistol)
                case GrenadeShot: return 7f;
                case SwarmShot: return 3f;
                case FireShot: return 6f;
                case FrostShot: return 4.5f;
                case FrostLance: return 10f;  // a wide spearhead: much easier to land than the old 4.5 px shard
                case ArcaneShot: return 5f;
                case HolyShot: return 9f;
                case BoltShot: return 4f;
                case SeedShot: return 4f;
                case FireflyShot: return 5f;
                case TreantSeed: return 2.5f;
                case SteelShard: return 4f;
                case ParryNeedle: return 4f;
                case Caltrop: return 5f;
                case BoneShot: return 3.5f;
                case RockShot: return 6f;
                case MinionShot: return 3.5f;
                case KnightWave: return 11f;
                case ArrowShot: return 4.5f;
                case StarShot: return 5.5f;
                case NeedleShot: return 3f;
                case KunaiShot: return 4f;
                case OfudaShot: return 6f;
                case GustShot: return 9f;
                case FumaStar: return 20f;  // giant
                case BloodShot: return 4f;
                case ClotShot: return 14f;  // the Clot Cannon's massive clot (lobbed: it never collides)
                case LeechShot: return 3.5f;
                case TransfuseShot: return 3f;
                case SanguineShot: return 6.5f;
                case ImplodeShot: return 3f;
                case BoneSpearShot: return 6f;
                case BigShot: return 10f;
                case MineShot: return 9f;
                case PierceShot: return 6.5f;
                case FastShot: return 4.5f;
                case StunShot: return 7f;
                case StunBolt: return 5f;
                case FlameShot: return 7f;
                case HammerShot: return 22f;
                case BoulderShot: return 16f;
                case MedkitShot: return 11f;
                case FireballShot: return 15f;
                case HoneyShot: return 9f;
                case HollowShot: return 7f;
                case CrescentShot: return 14f;
                case PauseShot: return 6.5f;
                case StitchShot: return 8f;
                case TaserBolt: return 7f;
                case StingBolt: return 7f;
                case MagmaBolt: return 8f;
                case ThrownDagger: return 5f;
                case TracerRound: return 5f;
                case BileShot: return 6f;
                case ToothShot: return 10f;
                default: return 6f;
            }
        }

        /// Every enemy bullet hits this much harder than its listed damage below.
        public const float EnemyBulletMult = 1.15f;

        public static float BulletDamage(byte kind) => BaseBulletDamage(kind) * EnemyBulletMult;

        static float BaseBulletDamage(byte kind)
        {
            switch (kind)
            {
                case BigShot: return 20f;
                case MineShot: return 18f;
                case PierceShot: return 16f;
                case FastShot: return 15f;
                case StunShot: return 8f;  // the Warden's searchlight turn: it stuns you for 1 s
                case TaserBolt: return 8f;  // a Taser Guard's Fork Bolt: the same stun
                case StingBolt: return 8f;  // a Stinger's dart: the same
                case MagmaBolt: return 9f;  // a Cinder Priest's bolt (and its lava)
                case ThrownDagger: return 10f;  // a Second's dagger
                case TracerRound: return 6.1f;  // a Security Guard's round: 7 once the enemy bullet multiplier is on
                case HoneyShot: return 14f;  // the Hive Queen's nectar spray
                case ToothShot: return 20f;  // the Devourer's Jaws
                case HollowShot: return 0f;  // the Duelist's decoys fire these: they pass through you
                case CrescentShot: return 18f;  // the Eclipse's crescent blades
                default: return 12f;
            }
        }

        public static Rgba BulletColor(byte kind)
        {
            switch (kind)
            {
                case TankShot: return new Rgba(0.5f, 0.75f, 1f);
                case HealerShot: return new Rgba(0.5f, 1f, 0.65f);
                case DpsShot: return new Rgba(1f, 0.7f, 0.35f);
                case TurretShot: return new Rgba(0.6f, 0.95f, 1f);
                case NovaShot: return new Rgba(0.95f, 0.8f, 1f);
                case FlakShell: return new Rgba(0.7f, 0.85f, 1f);
                case RailShot: return new Rgba(1f, 1f, 0.75f);
                case WispShot: return new Rgba(0.4f, 1f, 0.85f);
                case GrenadeShot: return new Rgba(0.75f, 0.85f, 0.4f);
                case SwarmShot: return new Rgba(1f, 0.85f, 0.35f);
                case FireShot: return new Rgba(1f, 0.45f, 0.15f);
                case FrostShot:
                case FrostLance: return new Rgba(0.6f, 0.9f, 1f);
                case ArcaneShot: return new Rgba(0.75f, 0.45f, 1f);
                case HolyShot: return new Rgba(1f, 0.95f, 0.6f);
                case BoltShot: return new Rgba(0.75f, 0.85f, 1f);
                case SeedShot: return new Rgba(0.55f, 0.9f, 0.35f);
                case FireflyShot: return new Rgba(0.89f, 0.97f, 0.56f);
                case TreantSeed: return new Rgba(0.74f, 0.94f, 0.54f);
                case SteelShard: return new Rgba(1f, 0.59f, 0.24f);
                case ParryNeedle: return new Rgba(1f, 1f, 1f);
                case Caltrop: return new Rgba(0.81f, 0.84f, 0.9f);
                case BoneShot: return new Rgba(0.92f, 0.9f, 0.8f);
                case RockShot: return new Rgba(0.7f, 0.6f, 0.5f);
                case MinionShot: return new Rgba(0.85f, 1f, 0.95f);
                case KnightSlash:
                case KnightWave: return new Rgba(0.85f, 0.92f, 1f);
                case ArrowShot: return new Rgba(0.5f, 1f, 0.65f);
                case StarShot:
                case FumaStar:
                case KunaiShot: return new Rgba(0.8f, 0.85f, 0.95f);
                case NeedleShot: return new Rgba(0.55f, 1f, 0.75f);
                case OfudaShot: return new Rgba(1f, 0.92f, 0.75f);
                case GustShot: return new Rgba(0.7f, 1f, 0.85f);
                case BloodShot:
                case ImplodeShot: return new Rgba(0.9f, 0.12f, 0.2f);
                case ClotShot: return new Rgba(0.6f, 0.05f, 0.12f);
                case LeechShot: return new Rgba(1f, 0.25f, 0.3f);
                case VeinShot: return new Rgba(1f, 0.35f, 0.5f);
                case TransfuseShot: return new Rgba(1f, 0.5f, 0.55f);
                case SanguineShot: return new Rgba(0.85f, 0.1f, 0.25f);
                case BoneSpearShot: return new Rgba(0.95f, 0.9f, 0.82f);
                case BigShot: return new Rgba(0.85f, 0.35f, 1f);
                case MineShot: return new Rgba(1f, 0.55f, 0.2f);
                case PierceShot: return new Rgba(1f, 0.12f, 0.12f);
                case FastShot: return new Rgba(1f, 0.95f, 0.4f);
                case StunShot:
                case StunBolt: return new Rgba(0.43f, 0.88f, 1f);
                case FlameShot: return new Rgba(1f, 0.55f, 0.15f);
                case HammerShot: return new Rgba(0.75f, 0.85f, 1f);
                case BoulderShot: return new Rgba(0.62f, 0.54f, 0.45f);
                case MedkitShot: return new Rgba(0.95f, 0.97f, 1f);
                case FireballShot: return new Rgba(1f, 0.5f, 0.15f);
                case HoneyShot: return new Rgba(1f, 0.67f, 0.16f);
                case HollowShot: return new Rgba(0.9f, 0.91f, 1f);  // a Duelist decoy's shot: a harmless ring
                case CrescentShot: return new Rgba(0.89f, 0.83f, 1f);  // the Eclipse's crescent blade
                case PauseShot: return new Rgba(0.45f, 0.95f, 0.85f);  // the Pauser's teal
                case StitchShot: return new Rgba(1f, 0.82f, 0.45f);    // the Stitcher's gold
                case TaserBolt: return new Rgba(0.75f, 0.91f, 1f);     // the Fork Bolt's ice white
                case StingBolt: return new Rgba(0.784f, 1f, 0.239f);   // the Stinger's venom lime
                case MagmaBolt: return new Rgba(1f, 0.48f, 0.23f);     // the Cinder Priest's magma
                case ThrownDagger: return new Rgba(0.81f, 0.84f, 1f);  // a Second's dagger (steel)
                case TracerRound: return new Rgba(0.94f, 0.97f, 1f);   // the Security Guard's hot-white tracer
                case BileShot: return new Rgba(0.75f, 0.9f, 0.31f);  // the Devourer's vomit
                case ToothShot: return new Rgba(0.96f, 0.9f, 0.82f);  // the Devourer's Jaws
                default: return new Rgba(1f, 0.3f, 0.45f);
            }
        }
    }
}
