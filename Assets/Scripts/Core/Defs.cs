using System;
using System.Collections.Generic;
using System.Linq;
namespace Arena
{
    /// Wildcard (Nov 2026, Docs/Wildcard.md) is a fourth role that borrows DPS gear from any class: it is unlocked by winning a run
    /// with each of the other three (Profile.WildcardUnlocked). Its lobby picks are encoded as kit * 3 + choice (see Defs.WildcardChoices).
    public enum Role : byte { Tank = 0, Healer = 1, Dps = 2, Wildcard = 3 }

    public enum EnemyType : byte
    {
        Grunt, Sniper, Spinner, Warden,
        Shard, Mortar, Shielder,
        HiveQueen, ReaperOfDusk, ReaperOfDawn, Singularity,
        // late game
        Colossus, Duelist, Decoy, Lich, Phylactery, StormHerald, Devourer, Eclipse,
        GravityWell,  // the Singularity's adds: they pull players' shots into themselves
        // melee: they never shoot, they run at you (early waves are mostly these)
        Brute, Lunger,
        HiveBomber,  // the Hive Queen's explosive drones: fly straight at where you were and blow up
        CageWell,  // the Singularity's small wells that ring a player in a shrinking cage
        Rioter,  // the Warden's riot: prisoners who run at whoever's nearest, the Warden included (Warden.cs)
        HiveEgg,  // the Hive Queen's eggs: break them before they hatch into drones (Queen.cs)
        Guard,  // the Warden's riot guards: they beat the rioters left after 10 s, then come for you (Warden.cs)
        DualReaper,  // the Twin Reapers fused into one (Reapers.cs)
        ReaperPillar,  // the Dual Reaper's corner pillars: each one killed leaves a hole that can trap it (Reapers.cs)
        SingularityShard,  // the Singularity's Split: shards that orbit and fire; their damage goes to the core (Singularity.cs)
        BallLightning,  // the Storm Herald's Gale: slow orbs that drift at you on the wind and arc to each other (Herald.cs)
        Morsel,  // the Devourer's Hungry turn: food it spits out and crawls over to eat; shoot it or step on it (Devourer.cs)
        // boss approaches: the Warden's faction (WardenFaction.cs)
        RiotShield, GasGrenadier,
        // challenge rooms (Challenges.cs): the mini-boss, and a Mirror Match's shadows of the players
        Gatekeeper, Shadow,
        // Zombie Infection's horde (Zombies.cs, ZombieLook.cs)
        Shambler, Runner, Spitter, Crawler, Bloater, Shrieker, Mender,
        // the simplest melee grunt: a white chomper that walks straight at you, in packs (EnemyAI.cs)
        Swarmer,
        // the Warden's faction: a ranged guard whose Fork Bolt stuns you (WardenFaction.cs, Game/TaserLook.cs)
        TaserGuard,
        // the Warden's faction: a guard with a service pistol, seven tracer rounds a magazine (SecurityGuard.cs, Game/SecurityLook.cs)
        SecurityGuard,
        // the Warden's faction, prisoners: a Brute and a Lunger in prison gear (PrisonKin.cs, Game/PrisonLook.cs)
        Thug, Shiv,
        // a spawn portal: a burst of the wave comes through it; shoot it closed to cancel the rest (SpawnPortals.cs, Game/SpawnPortalLook.cs)
        SpawnPortal,
        // the Hive Queen's faction (Docs/HiveFaction.md, HiveKin.cs): each plays like one of the Warden's, in the same order
        Worker, Soldier, CombShield, HoneyLobber, Stinger, Needler, Bumble, Hornet,
        // the Twin Reapers' faction, the Dusk and Dawn cult (ReaperFaction.cs, Game/ReaperCultLook.cs): Wisp (a Swarmer), Acolyte
        // (a sickle that cuts a cone), Bound Wraiths (a tethered pair), Scythe Wraith (a Lunger that reaps), Lamplighter (lines and
        // the cross), Mourner (the twin spiral), Gravedigger (a Brute), Shade (blinks next to you)
        Wisp, Acolyte, BoundWraith, ScytheWraith, Lamplighter, Mourner, Gravedigger, Shade,
        // the Singularity's faction, the Infall (Docs/SingularityFaction.md, Infall.cs, Game/InfallLook.cs): step 1
        Debris, VoidAcolyte, Pulsar,
        // ...step 2, with the Graviton's small well and the Collapsar's implosion (both only come from them)
        Graviton, Orbiter, Collapsar, SmallWell, Implosion,
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
        // the Eclipse's faction, the Penumbral Court (Docs/EclipseFaction.md, Penumbral.cs, Game/PenumbralLook.cs): step 1
        Ember, CoronaPriest, Heliograph,
        // ...step 2: a Dusk Lobber plays like a Gas Grenadier that lobs a pool of dark (HiveKin.cs)
        Moonward, Gnomon, DuskLobber,
        // ...step 3
        Sickle, Nova,
        // step 3: a Gutworm dives and erupts under you, a Hook Thrower throws a meat hook that yanks you in, a Maw Priest like a
        // Taser Guard with a maw in front that feeds on your shots
        Gutworm, HookThrower, MawPriest,
        // the Storm Herald's faction, the Squall (Docs/StormFaction.md, Squall.cs, Game/SquallLook.cs): step 1
        DustDevil, SquallGunner, Stormcaller,
        // step 2: a Grounder grounds shots on its copper shield and claps them back, Pylons come in pairs and arc between
        // them, a Lineman marks a player and the charge jumps to allies near them
        Grounder, Pylon, Lineman,
        // step 3: a Thunderhead walks at you dropping rows of strikes ahead of it (the faction's Ball Lightning is the
        // Herald's own BallLightning, made to work in waves)
        Thunderhead,
        // the Warden's faction: the Turnkey runs from you planting traps and Cell Doors that let Rioters out; a Floor Mine and
        // a Shackle Trap are its traps (its third, the Mop Slick, is a patch: PatchKind.Slick) (Turnkey.cs, Game/TurnkeyLook.cs)
        Turnkey, CellDoor, FloorMine, ShackleTrap,
    }

    public class RoleDef
    {
        public string Name, Ability, Desc;
        public Rgba Color;
        public float Hp, Speed, HitR, BodyR, FireInterval, AbilityCd;
    }

    /// Soldier is the original gun-based kit; Elementalist and Summoner are the alternatives for each role.
    /// The classes, in lobby order (the order is sent over the network and indexes the class tables: add new ones at the end).
    /// Each has a KitDef (Classes.cs). Run saves from before the October 2026 rename say "Mage" and "BloodMage" (KitFromSave).
    public enum Kit : byte { Soldier, Elementalist, Summoner, Knight, Ninja, Hemomancer, Engineer, Arcanist }

    public enum WeaponId : byte
    {
        // Soldier
        Scattergun, FlakCannon, RiotRepeater, RepulsionCannon, Revolver, MendersBow, TwinRifle, Railgun, BurstSmg,
        // Elementalist
        Flamethrower, FrostLance, StormHammer, RadiantOrb, ChainLight, Nimbus, Fireball, ArcLightning, IceShards,
        // Summoner
        GolemStaff, BoneGuard, BearTotem, SpriteStaff, TreantRod, SpiritChalk, WolfWhistle, WispLantern, ImpCaller,
        // Knight (melee)
        Warhammer, Lance, Halberd, Quarterstaff, Lightbringer, Censer, TwinBlades, Greatsword, Rapier,
        // Ninja (Ninja.cs): thrown blades and quick melee
        Kusarigama, BoStaff, FumaShuriken, AcupunctureNeedles, SpiritFan, Ofuda, Shuriken, Katana, Kunai,
        // Hemomancer (Hemomancer.cs): blood spells that drain and heal
        BloodLash, GoreMortar, LeechTendrils, TransfusionDarts, SanguineOrb, VeinTether, NeedleSwarm, Implosion, BloodBrawler,
        // Fourth weapons (Choice 3), where a class has one: the Knight DPS's Arc Knuckles (ArcKnuckles.cs), the Soldier DPS's Double Barrel (DoubleBarrel.cs), the Elementalist DPS's Stonespear (Stonespear.cs); the Twin Rifle's variants (Twin Rifle Auto, Out of Ammo) are Choice 4 and 5
        ArcKnuckles, TwinRifleAuto, OutOfAmmo, DoubleBarrel, Stonespear,
        // the Tank Soldier's fourth (GrenadeLauncher.cs)
        GrenadeLauncher,
        // the Summoner DPS's fourth (HoneybeeStaff.cs)
        HoneybeeStaff,
        // the Ninja DPS's fourth (Nunchaku.cs): Twin Storm, a nunchaku in each hand
        Nunchaku,
        // the Tank Elementalist's fourth (WindClash.cs)
        WindClash,
        // the Hemomancer DPS's fourth (EmbolusSpears.cs): a blood spear in each hand
        EmbolusSpears,
        // the Tank Knight's fourth (BulwarkBarrier.cs): a kite shield
        BulwarkBarrier,
        // the Tank Hemomancer's fourth (BloodBlast.cs): a six-step chain of blood cuts
        BloodBlast,
        // the Engineer's second weapons (EngineerBots.cs): Fire summons a robot Engineer of the role, then is a basic attack
        IronFist, StasisGun, VectorBlade,
        // the Tank Summoner's fourth (WyvernStaff.cs): a wind Wyvern that blows enemies back, breathes fire and slams
        WyvernStaff,
        // the Tank Ninja's fourth (Nodachi.cs): a katana as tall as you, two stances
        Nodachi,
        // the Healer Soldier's fourth (TwinPistols.cs): a pistol in each hand, Laser Sights on Alt
        TwinPistols,
        // the Fireball's variants (ElementBalls.cs): the Waterball soaks (wet), the Earthball stops on the first enemy and stuns it
        Waterball, Earthball,
        // the Healer Knight's fourth (Vowguard.cs): a sword and shield, two stances, chains you build
        Vowguard,
        // the Arcanist (Arcanist.cs): Tank Orbit Shards, Healer Glyph Stylus, DPS Dancing Blades (one weapon per role so far)
        OrbitShards, GlyphStylus, DancingBlades,
        // the Healer Elementalist's fourth (StaticShock.cs): a held cone of static, the Arc Cannon on Alt
        StaticShock,
        // Training only (Training.cs): the recruit's Training Pistol; no lobby place, never in Defs.Weapons
        TrainingPistol,
        // the Flamethrower's variants (ElementGuns.cs): the Water Gun splashes wet, the Gale Gun slices and slows
        WaterGun, GaleGun,
        // the Healer Ninja's fourth (TwinTessen.cs): an iron war fan in each hand, the Kamikaze Gust on Alt
        TwinTessen,
        // the DPS Engineer's second (BumbleRemote.cs): a remote steering the Bumble Bot, a robot bee that fights at your cursor
        BumbleRemote,
        // the Healer Summoner's fourth (HiveCenser.cs): insects poured from the cursor that swarm enemies and shell allies, Hive Call on Alt
        HiveCenser,
    }

    public class WeaponDef
    {
        public WeaponId Id;
        public Kit Kit;            // whose weapon it is: FireVolley sends it to that class's Fire (Classes.cs) unless it has its own case
        public Role Role;          // with Kit and Choice, its place in the lobby (Defs.Weapons is gathered from each class's list)
        public int Choice;         // 0-2: first, second or third weapon of its role and class
        public string KeyLabel;    // the lobby key's text when the name is one word too long for it (a line break where it splits); null: the name
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
        public bool FillsGaps;    // a class still being built (the Engineer): this weapon stands in for the class's empty lobby places, which the lobby shows locked
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
        public BotAltFn BotAlt;        // when the smart bot presses its Alt (BotWeaponAlt.cs); null: never
        public bool BotAltHold;        // that Alt is held down rather than tapped (the parry stays down while it is held: it would let go of it)
    }

    /// Where the dev tools' enemy list puts an enemy: by group, then in EnemyType order.
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
        public PickerGroup Picker;  // where the dev tools' enemy list puts it (Hidden: only comes with its boss)
        public string Blurb;   // one line on what it does
    }

    /// Stats for roles, enemies and bullets. Tweak numbers here to rebalance.
    public static partial class Defs
    {
        static EnemyType[] pickable;

        /// Every enemy the dev tools can spawn, in list order: each EnemyDef's Picker group, then EnemyType order. (Boss summons
        /// like Gravity Wells and Hive Bombers are Hidden: they come with their boss.)
        public static EnemyType[] Pickable => pickable ?? (pickable = ((EnemyType[])Enum.GetValues(typeof(EnemyType)))
            .Where(t => Get(t).Picker != PickerGroup.Hidden).OrderBy(t => Get(t).Picker).ThenBy(t => (int)t).ToArray());

        /// An enemy's name in lists (the Reapers come as a pair).
        public static string ListName(EnemyType t) => t == EnemyType.ReaperOfDusk ? "Twin Reapers" : Get(t).Name;

        public static readonly RoleDef[] Roles =
        {
            new RoleDef
            {
                Name = "Tank", Ability = "Barrier", Color = new Rgba(0.35f, 0.6f, 1f),
                Hp = 170f, Speed = 185f, HitR = 9f, BodyR = 18f, FireInterval = 0.45f, AbilityCd = 12f,
                Desc = "The front line. A slower step, and enemies prefer to target you, so stand between them and your team. Ironhide: a 25-point shield takes half of every hit (the rest reaches your health) and refills 3 s after the last one. Your weapons and abilities block, shove and soak: barriers, shields and cover that buy your allies room to work.",
            },
            new RoleDef
            {
                Name = "Healer", Ability = "Mend Pulse", Color = new Rgba(0.35f, 1f, 0.55f),
                Hp = 140f, Speed = 215f, HitR = 7f, BodyR = 14f, FireInterval = 0.25f, AbilityCd = 10f,  // 140 (was 120): healers took 7 of 9 downs in a recorded run
                Desc = "Keeps the team standing. Your aura heals allies within 170 px, 17 HP a second next to you down to 5 at its edge, and you for 1; healing past full health becomes a short shield. Your abilities boost, protect and bring the fallen back, and they work on you when you fight alone.",
            },
            new RoleDef
            {
                Name = "DPS", Ability = "Dash", Color = new Rgba(1f, 0.55f, 0.25f),
                Hp = 110f, Speed = 240f, HitR = 6f, BodyR = 12f, FireInterval = 0.1f, AbilityCd = 3.5f,
                Desc = "The damage. The lowest health but the fastest feet and the hardest-hitting weapons. Momentum: each kill adds a stack (up to 5) for +3% move speed and fire rate, and the stacks fade 4 s after your last kill, so keep the kills coming.",
            },
            new RoleDef
            {
                // Wildcard (Docs/Wildcard.md): middle health and no role passive; DPS gear from any class, a Wildcard ability,
                // and the price is WildcardDmgMult / WildcardCdMult (Game.cs).
                Name = "Wildcard", Ability = "Any DPS ability", Color = new Rgba(0.78f, 0.45f, 1f),
                Hp = 125f, Speed = 225f, HitR = 6.5f, BodyR = 14f, FireInterval = 0.1f, AbilityCd = 3.5f,
                Desc = "Build your own. Take any class's DPS weapon, any two DPS abilities on the keys you choose, and any class's DPS ultimate. The price: weaker shots, slower cooldowns, middle health and no role passive.",
            },
        };

        // Indexed by EnemyType.
        public static readonly EnemyDef[] Enemies =
        {
            new EnemyDef { Name = "Grunt", Hp = 60f, Radius = 14f, Speed = 85f, Score = 10, Xp = 10, Sides = 3, Color = new Rgba(1f, 0.35f, 0.35f),
                Picker = PickerGroup.Ranged, Blurb = "Fires aimed 3-shot spreads." },
            new EnemyDef { Name = "Sniper", Hp = 50f, Radius = 12f, Speed = 95f, Score = 15, Xp = 14, Sides = 4, Color = new Rgba(1f, 0.9f, 0.3f),
                Picker = PickerGroup.Ranged, Blurb = "Fast, accurate shots from far away." },
            new EnemyDef { Name = "Spinner", Hp = 150f, Radius = 18f, Speed = 35f, Score = 25, Xp = 22, Sides = 5, Color = new Rgba(1f, 0.6f, 0.2f),
                Picker = PickerGroup.Ranged, Blurb = "Wanders and sprays spirals in bursts; it spins up while bursting." },
            new EnemyDef { Name = "The Warden", IsBoss = true, Hp = 2400f, Radius = 46f, Speed = 45f, Score = 300, Xp = 150, Sides = 8, Color = new Rgba(1f, 0.25f, 0.5f),
                Picker = PickerGroup.Boss, Blurb = "Boss (wave 5): a prison riot in three phases — searchlight, gates and gas, then rioters." },
            new EnemyDef { Name = "Shard", Hp = 25f, Radius = 8f, Speed = 130f, Score = 5, Xp = 4, Sides = 3, Color = new Rgba(0.8f, 1f, 0.55f),
                Picker = PickerGroup.Ranged, Blurb = "Tiny and fast. Comes from the Hive Queen and Splitting elites." },
            new EnemyDef { Name = "Mortar", Hp = 80f, Radius = 16f, Speed = 40f, Score = 20, Xp = 18, Sides = 7, Color = new Rgba(0.95f, 0.55f, 0.3f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs bombs at where you stand; they burst into a ring on landing." },
            new EnemyDef { Name = "Shielder", Hp = 140f, Radius = 17f, Speed = 60f, Score = 25, Xp = 22, Sides = 6, Color = new Rgba(0.6f, 0.7f, 0.9f),
                Picker = PickerGroup.Ranged, Blurb = "A front shield blocks shots and swings. It turns slowly: flank it." },
            new EnemyDef { Name = "Hive Queen", IsBoss = true, Hp = 3000f, Radius = 44f, Speed = 40f, Score = 400, Xp = 180, Sides = 6, Color = new Rgba(1f, 0.8f, 0.2f),
                Picker = PickerGroup.Boss, Blurb = "Boss (wave 10): a hive in three phases — drones and bombers, sealing cells and honey, then a frenzied swarm." },
            new EnemyDef { Name = "Reaper of Dusk", IsBoss = true, Hp = 2600f, Radius = 34f, Speed = 95f, Score = 250, Xp = 100, Sides = 5, Color = new Rgba(1f, 0.35f, 0.6f),
                Picker = PickerGroup.Boss, Blurb = "Boss pair (wave 15): a tether sweep, then they fuse into the Dual Reaper (pillars, holes), then split for a finale." },
            new EnemyDef { Name = "Reaper of Dawn", IsBoss = true, Hp = 2600f, Radius = 34f, Speed = 95f, Score = 250, Xp = 100, Sides = 5, Color = new Rgba(0.45f, 0.55f, 1f), Blurb = "The Twin Reapers' second half: fights alongside the Reaper of Dusk, then fuses with it into the Dual Reaper." },
            new EnemyDef { Name = "The Singularity", IsBoss = true, Hp = 3600f, Radius = 40f, Speed = 0f, Score = 500, Xp = 220, Sides = 10, Color = new Rgba(0.6f, 0.35f, 1f),
                Picker = PickerGroup.Boss, Blurb = "Boss (wave 20): corner gravity wells, pull, lasers and shrinking cages." },
            new EnemyDef { Name = "The Colossus", IsBoss = true, Hp = 6800f, Radius = 54f, Speed = 35f, Score = 600, Xp = 260, Sides = 6, Color = new Rgba(0.78f, 0.62f, 0.45f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 25): huge stomp rings with one gap, and thrown rocks." },
            new EnemyDef { Name = "The Duelist", IsBoss = true, Hp = 6000f, Radius = 30f, Speed = 80f, Score = 650, Xp = 280, Sides = 4, Color = new Rgba(0.85f, 0.85f, 1f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 30): teleports and leaves decoys that burst unless killed." },
            new EnemyDef { Name = "The Duelist", Hp = 70f, Radius = 30f, Speed = 0f, Score = 0, Xp = 0, Sides = 4, Color = new Rgba(0.85f, 0.85f, 1f), Late = true },  // decoy
            new EnemyDef { Name = "The Lich", IsBoss = true, Hp = 5000f, Radius = 36f, Speed = 45f, Score = 700, Xp = 300, Sides = 7, Color = new Rgba(0.5f, 1f, 0.75f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 35): immune while its phylactery stands; raises skeletons." },
            new EnemyDef { Name = "Phylactery", Hp = 600f, Radius = 18f, Speed = 0f, Score = 50, Xp = 0, Sides = 4, Color = new Rgba(0.4f, 1f, 0.6f), Late = true, Heavy = true, Blurb = "The Lich's soul jar: the Lich can't be hurt while it stands, so break it first." },
            new EnemyDef { Name = "Storm Herald", IsBoss = true, Hp = 6000f, Radius = 40f, Speed = 55f, Score = 800, Xp = 320, Sides = 9, Color = new Rgba(0.55f, 0.75f, 1f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 40), three phases: a gathering storm (strikes, lightning rods), a gale that pushes you and bends bullets, then a supercell that marks one player at a time." },
            new EnemyDef { Name = "The Devourer", IsBoss = true, Hp = 6500f, Radius = 50f, Speed = 40f, Score = 900, Xp = 340, Sides = 12, Color = new Rgba(0.85f, 0.28f, 0.38f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Boss (wave 45), three phases: hungry for morsels (your shots heal it), then swallowing players, then a mouth that heals it from the front." },
            new EnemyDef { Name = "The Eclipse", IsBoss = true, Hp = 9000f, Radius = 52f, Speed = 30f, Score = 2000, Xp = 500, Sides = 16, Color = new Rgba(1f, 0.85f, 0.4f),
                Picker = PickerGroup.Boss, Late = true, Blurb = "Final boss (wave 50), five phases and Totality: a sun and its moon. Hide in the moon's shadow from the flash, stay in the light, and kill it before the nova." },
            new EnemyDef { Name = "Gravity Well", Hp = 260f, Radius = 15f, Speed = 160f, Score = 40, Xp = 15, Sides = 6, Color = new Rgba(0.7f, 0.45f, 1f), Late = true, Blurb = "The Singularity's corner wells: they pull your shots into themselves." },
            // Melee
            new EnemyDef { Name = "Brute", Hp = 200f, Radius = 19f, Speed = 62f, Score = 15, Xp = 14, Sides = 7, Color = new Rgba(0.8f, 0.45f, 0.35f), Melee = true, Contact = 24f,
                Picker = PickerGroup.Melee, Blurb = "Slow, tough melee bruiser that hits hard (24) when it reaches you." },
            new EnemyDef { Name = "Lunger", Hp = 45f, Radius = 12f, Speed = 95f, Score = 12, Xp = 12, Sides = 4, Color = new Rgba(0.95f, 0.35f, 0.65f), Melee = true, Contact = 18f,
                Picker = PickerGroup.Melee, Blurb = "Circles in close, flashes a pink line, then leaps along it." },
            new EnemyDef { Name = "Hive Bomber", Hp = 30f, Radius = 10f, Speed = 330f, Score = 2, Xp = 0, Sides = 3, Color = new Rgba(1f, 0.45f, 0.15f), Melee = true, Blurb = "The Hive Queen's explosive drones: they fly straight at where you were and blow up." },
            new EnemyDef { Name = "Cage Well", Hp = 40f, Radius = 11f, Speed = 0f, Score = 5, Xp = 2, Sides = 6, Color = new Rgba(0.85f, 0.4f, 1f), Melee = true, Blurb = "The Singularity's small wells: they ring a player in a shrinking cage." },
            new EnemyDef { Name = "Rioter", Hp = 40f, Radius = 12f, Speed = 110f, Score = 3, Xp = 7, Sides = 8, Color = new Rgba(1f, 0.55f, 0.2f), Melee = true, Contact = 15f,
                Blurb = "A prisoner in an orange jumpsuit throwing cuffed punches. Some drag a ball and chain and run slower." },
            new EnemyDef { Name = "Hive Egg", Hp = 60f, Radius = 15f, Speed = 0f, Score = 4, Xp = 3, Sides = 10, Color = new Rgba(0.96f, 0.92f, 0.78f), Melee = true, Blurb = "The Hive Queen's eggs: break them before they hatch into drones." },
            new EnemyDef { Name = "Guard", Hp = 90f, Radius = 13f, Speed = 125f, Score = 6, Xp = 15, Sides = 8, Color = new Rgba(0.35f, 0.55f, 1f), Melee = true, Contact = 14f ,
                Blurb = "A corrections officer in khaki with a peaked cap and a nightstick. Up close it swings forehand then backhand: step out of the amber arc." },
            new EnemyDef { Name = "The Dual Reaper", IsBoss = true, Hp = 5200f, Radius = 58f, Speed = 150f, Score = 500, Xp = 200, Sides = 10, Color = new Rgba(1f, 0.8f, 0.45f), Blurb = "The Twin Reapers fused into one: corner pillars, holes that can trap it, armored to a third of the damage." },
            new EnemyDef { Name = "Reaper Pillar", Hp = 120f, Radius = 24f, Speed = 0f, Score = 5, Xp = 4, Sides = 4, Color = new Rgba(0.55f, 0.5f, 0.75f), Melee = true, Blurb = "The Dual Reaper's corner pillars: each one killed leaves a hole that can trap it." },
            new EnemyDef { Name = "Singularity Shard", Hp = 1e6f, Radius = 20f, Speed = 0f, Score = 0, Xp = 0, Sides = 5, Color = new Rgba(0.8f, 0.5f, 1f), Melee = true, Blurb = "The Singularity's Split: shards that orbit and fire; damage to them goes to the core." },
            new EnemyDef { Name = "Ball Lightning", Hp = 70f, Radius = 15f, Speed = 0f, Score = 3, Xp = 2, Sides = 8, Color = new Rgba(0.75f, 0.9f, 1f), Melee = true, Contact = 20f, Blurb = "The Storm Herald's orbs: they drift at you on the wind, arc to each other and pop into 8 shots." },
            new EnemyDef { Name = "Morsel", Hp = 60f, Radius = 14f, Speed = 0f, Score = 0, Xp = 0, Sides = 7, Color = new Rgba(0.88f, 0.5f, 0.35f), Late = true, Blurb = "Food the Devourer spits out and crawls over to eat: shoot it or step on it first." },
            new EnemyDef { Name = "Riot Shield", Hp = 60f, Radius = 16f, Speed = 120f, Score = 10, Xp = 10, Sides = 6, Color = new Rgba(0.55f, 0.68f, 0.95f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "An officer in steel-blue riot gear behind a tall shield that bashes you back. Go round it, or break the shield: then it falls fast." },
            new EnemyDef { Name = "Gas Grenadier", Hp = 50f, Radius = 14f, Speed = 75f, Score = 8, Xp = 9, Sides = 5, Color = new Rgba(0.62f, 0.92f, 0.42f),
                Picker = PickerGroup.Ranged, Blurb = "An officer in olive drab and a gas mask, lobbing tear-gas canisters at you. Step out of the cloud." },
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
            new EnemyDef { Name = "Swarmer", Hp = 14f, Radius = 9f, Speed = 115f, Score = 3, Xp = 3, Sides = 24, Color = new Rgba(0.97f, 0.97f, 0.95f), Melee = true, Contact = 6f,
                Picker = PickerGroup.Melee, Blurb = "The simplest melee rusher: a white chomper with no eyes that walks straight at you. Weak alone, and it comes in packs." },
            // Enemies that don't shoot (NonShooters.cs). Melee = true keeps them out of the shooting turns; they're drawn without spikes.
            new EnemyDef { Name = "Taser Guard", Hp = 70f, Radius = 13f, Speed = 80f, Score = 9, Xp = 11, Sides = 8, Color = new Rgba(0.4f, 0.6f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "An officer in slate with a hi-vis band. Keeps its distance, charges its taser and fires a white Fork Bolt that stuns you. Dodge the bolt, or parry it." },
            new EnemyDef { Name = "Security Guard", Hp = 45f, Radius = 15f, Speed = 70f, Score = 9, Xp = 9, Sides = 24, Color = new Rgba(0.42f, 0.61f, 0.7f),
                Picker = PickerGroup.Ranged, Blurb = "An officer in navy with a service pistol. Strafes, locks on and fires one tracer round at a time: seven shots, then a long reload. Sidestep the lane, or rush it while it reloads." },
            new EnemyDef { Name = "Thug", Hp = 175f, Radius = 19f, Speed = 62f, Score = 10, Xp = 10, Sides = 7, Color = new Rgba(0.6f, 0.62f, 0.66f), Melee = true, Contact = 25f,
                Picker = PickerGroup.Melee, Blurb = "A hunched prisoner brute with giant taped fists. Slow and tough, hits hard when it reaches you." },
            new EnemyDef { Name = "Shiv", Hp = 45f, Radius = 12f, Speed = 115f, Score = 9, Xp = 9, Sides = 4, Color = new Rgba(0.85f, 0.85f, 0.8f), Melee = true, Contact = 18f,
                Picker = PickerGroup.Melee, Blurb = "A prisoner with a taped shank: hooded, or shirtless with one in each hand. Circles in close, flashes a line, then lunges along it." },
            new EnemyDef { Name = "Spawn Portal", Hp = 200f, Radius = 30f, Speed = 0f, Score = 30, Xp = 20, Sides = 24, Color = new Rgba(0.78f, 0.35f, 1f), Melee = true, Heavy = true,
                Picker = PickerGroup.Hidden, Blurb = "A burst of the wave comes through it. Shoot it closed before it empties to cancel the rest; it drops coins for each one it held." },
            // the Hive Queen's faction (HiveKin.cs): the numbers are their Warden counterpart's, bar the Stinger's and Needler's health (half as much again)
            new EnemyDef { Name = "Worker", Hp = 40f, Radius = 12f, Speed = 110f, Score = 3, Xp = 7, Sides = 8, Color = new Rgba(1f, 0.773f, 0.2f), Melee = true, Contact = 15f,
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
            new EnemyDef { Name = "Bumble", Hp = 175f, Radius = 19f, Speed = 62f, Score = 10, Xp = 10, Sides = 7, Color = new Rgba(0.91f, 0.573f, 0.047f), Melee = true, Contact = 25f,
                Picker = PickerGroup.Melee, Blurb = "A huge, furry bumblebee. Slow and tough, hits hard when it reaches you." },
            new EnemyDef { Name = "Hornet", Hp = 45f, Radius = 12f, Speed = 115f, Score = 9, Xp = 9, Sides = 4, Color = new Rgba(1f, 0.29f, 0.157f), Melee = true, Contact = 18f,
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
                Picker = PickerGroup.Late, Late = true, Blurb = "A shrouded wraith: fades to smoke, shows a ring where it will land, appears and fires." },
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
                Picker = PickerGroup.Hidden, Blurb = "Lobbed by a Graviton: for a few seconds it drags you toward it and bends your shots into it." },
            new EnemyDef { Name = "Implosion", Hp = 1e6f, Radius = 10f, Speed = 0f, Score = 0, Xp = 0, Sides = 6, Color = new Rgba(1f, 0.47f, 0.86f), Melee = true,
                Picker = PickerGroup.Hidden, Blurb = "What a Collapsar leaves when killed: it pulls players in, then bursts into a ring of shots." },
            new EnemyDef { Name = "Lens", Hp = 90f, Radius = 14f, Speed = 70f, Score = 9, Xp = 11, Sides = 8, Color = new Rgba(0.5f, 0.84f, 1f), Melee = true, Contact = 12f,
                Picker = PickerGroup.Melee, Blurb = "Carries a gravity lens in front of it. Shots into its face curve round it and away; it turns slowly, so hit it from the side." },
            new EnemyDef { Name = "White Dwarf", Hp = 40f, Radius = 9f, Speed = 230f, Score = 8, Xp = 10, Sides = 4, Color = new Rgba(1f, 0.96f, 0.9f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "A small hot star that darts in, flashes white, then shoves you back and turns your shots near it round at you. Stop shooting when it flashes." },
            // the Colossus's faction (QuarryFaction.cs): Rubble and the Quarryman have their Warden counterpart's numbers
            new EnemyDef { Name = "Rubble", Hp = 40f, Radius = 12f, Speed = 110f, Score = 3, Xp = 7, Sides = 7, Color = new Rgba(0.78f, 0.62f, 0.45f), Melee = true, Contact = 15f,
                Picker = PickerGroup.Melee, Blurb = "A fist-sized chunk of the Colossus that rolls at you, weaving, in pairs." },
            new EnemyDef { Name = "Quarryman", Hp = 90f, Radius = 13f, Speed = 125f, Score = 6, Xp = 15, Sides = 8, Color = new Rgba(0.62f, 0.53f, 0.4f), Melee = true, Contact = 14f,
                Picker = PickerGroup.Melee, Blurb = "A quarry worker with a hard hat, a headlamp and a pickaxe. Walks straight at you." },
            new EnemyDef { Name = "Stomper", Hp = 180f, Radius = 21f, Speed = 58f, Score = 12, Xp = 20, Sides = 7, Color = new Rgba(0.69f, 0.6f, 0.5f), Melee = true, Contact = 25f,
                Picker = PickerGroup.Melee, Blurb = "A slab-shouldered stone brute. Every few seconds it lifts a foot and stomps out a small ring of push blocks with one green gap: step into the gap." },
            new EnemyDef { Name = "Slab Bearer", Hp = 60f, Radius = 16f, Speed = 120f, Score = 10, Xp = 10, Sides = 6, Color = new Rgba(0.62f, 0.53f, 0.4f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "Carries a quarried slab that stops shots and bashes you back. Break the slab and it drops it as cover for a few seconds; then the bearer falls fast." },
            new EnemyDef { Name = "Rock Hurler", Hp = 50f, Radius = 14f, Speed = 75f, Score = 8, Xp = 9, Sides = 5, Color = new Rgba(0.7f, 0.6f, 0.49f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs a boulder where you're heading. Walk out of the ring: it lands hard, bursts into shards and leaves a rubble pile that stops shots." },
            new EnemyDef { Name = "Driller", Hp = 45f, Radius = 12f, Speed = 115f, Score = 9, Xp = 9, Sides = 4, Color = new Rgba(0.62f, 0.53f, 0.4f), Melee = true, Contact = 18f,
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
            new EnemyDef { Name = "Harlequin", Hp = 240f, Radius = 20f, Speed = 60f, Score = 14, Xp = 22, Sides = 8, Color = new Rgba(1f, 0.3f, 0.42f), Melee = true, Contact = 25f,
                Picker = PickerGroup.Melee, Blurb = "A heavy two-sworded jester. It marks a line through you and dashes it, vaults to the other arm and dashes across: an X of blades that fire out sideways. Stand in a gap of the X." },
            new EnemyDef { Name = "Mummer", Hp = 40f, Radius = 11f, Speed = 150f, Score = 9, Xp = 10, Sides = 4, Color = new Rgba(0.71f, 0.61f, 1f), Melee = true, Contact = 14f,
                Picker = PickerGroup.Melee, Blurb = "A masked player that blinks and becomes three. They circle you and all fire, but only the real one's shots are solid; the copies' are hollow rings. Find the real one." },
            new EnemyDef { Name = "Mummer", Hp = 1f, Radius = 11f, Speed = 0f, Score = 0, Xp = 0, Sides = 4, Color = new Rgba(0.71f, 0.61f, 1f), Melee = true, Contact = 0f,
                Picker = PickerGroup.Hidden, Blurb = "One of a Mummer's two copies: its shots are hollow rings that pass through you. It vanishes when its time is up." },
            new EnemyDef { Name = "Grave Caller", Hp = 60f, Radius = 14f, Speed = 60f, Score = 12, Xp = 16, Sides = 4, Color = new Rgba(0.37f, 0.85f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Zombie: hangs back and cracks three graves open round you; a Shambler claws out of each. Step out of the ring, then kill it: its risen crumble." },
            // the Devourer's faction (LarderFaction.cs): the Gnawling and the Butcher have their Warden counterpart's numbers
            new EnemyDef { Name = "Gnawling", Hp = 40f, Radius = 11f, Speed = 110f, Score = 3, Xp = 7, Sides = 8, Color = new Rgba(0.91f, 0.53f, 0.6f), Melee = true, Contact = 15f,
                Picker = PickerGroup.Melee, Blurb = "A mouth on legs, the Devourer in miniature. Weaves at you in pairs." },
            new EnemyDef { Name = "Butcher", Hp = 90f, Radius = 13f, Speed = 125f, Score = 6, Xp = 15, Sides = 8, Color = new Rgba(0.43f, 0.29f, 0.24f), Melee = true, Contact = 14f,
                Picker = PickerGroup.Melee, Blurb = "A hooded butcher with a bone mask. Close in, it raises its cleaver and chops a half-circle in front: step out of it." },
            new EnemyDef { Name = "Gulper", Hp = 180f, Radius = 22f, Speed = 58f, Score = 12, Xp = 25, Sides = 9, Color = new Rgba(0.86f, 0.5f, 0.56f), Melee = true, Contact = 25f,
                Picker = PickerGroup.Melee, Blurb = "A swollen gut. Its cheeks swell, then it breathes in and pulls you toward its mouth: step out of the cone sideways." },
            new EnemyDef { Name = "Carcass Bearer", Hp = 60f, Radius = 16f, Speed = 120f, Score = 10, Xp = 10, Sides = 6, Color = new Rgba(0.37f, 0.29f, 0.25f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "Carries a side of meat that stops shots and bashes you back. Break it and it drops as a morsel: shoot it or walk over it before a Gnawling eats it." },
            new EnemyDef { Name = "Bile Spitter", Hp = 50f, Radius = 14f, Speed = 75f, Score = 8, Xp = 9, Sides = 5, Color = new Rgba(0.84f, 0.55f, 0.55f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs a glob of bile where you're heading. Walk out of the ring: it leaves a pool of acid for a few seconds." },
            // the Eclipse's faction, the Penumbral Court (Penumbral.cs)
            new EnemyDef { Name = "Ember", Hp = 16f, Radius = 9f, Speed = 135f, Score = 3, Xp = 6, Sides = 5, Color = new Rgba(1f, 0.54f, 0.24f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "A scrap of solar flare that weaves in at you, dropping sparks that hang in the air behind it." },
            new EnemyDef { Name = "Corona Priest", Hp = 45f, Radius = 13f, Speed = 85f, Score = 7, Xp = 10, Sides = 6, Color = new Rgba(1f, 0.82f, 0.4f),
                Picker = PickerGroup.Ranged, Blurb = "A sun-cultist. Its halo flares, then bursts into a ring of slow shots: one at you, and a gap beside it." },
            new EnemyDef { Name = "Heliograph", Hp = 70f, Radius = 13f, Speed = 75f, Score = 9, Xp = 12, Sides = 8, Color = new Rgba(1f, 0.69f, 0.36f),
                Picker = PickerGroup.Ranged, Blurb = "Tilts a sun mirror at you: a line flickers for a second, then becomes a lane of fire. Step off the line." },
            new EnemyDef { Name = "Moonward", Hp = 100f, Radius = 14f, Speed = 70f, Score = 9, Xp = 11, Sides = 8, Color = new Rgba(0.79f, 0.82f, 0.89f), Melee = true, Contact = 12f,
                Picker = PickerGroup.Melee, Blurb = "A pale knight with a small moon circling it. The moon swallows any shot it touches: fire when it swings round the back." },
            new EnemyDef { Name = "Gnomon", Hp = 240f, Radius = 22f, Speed = 55f, Score = 14, Xp = 20, Sides = 4, Color = new Rgba(0.62f, 0.55f, 0.45f), Melee = true, Contact = 20f, Heavy = true,
                Picker = PickerGroup.Melee, Blurb = "A walking sundial with a sun lantern at its back. The lantern swells, then flashes: the only safe place is the slab's shadow, just to the side of you." },
            new EnemyDef { Name = "Dusk Lobber", Hp = 50f, Radius = 13f, Speed = 80f, Score = 8, Xp = 9, Sides = 6, Color = new Rgba(0.61f, 0.48f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs a ball of night that bursts into a pool of the Umbra's dark: you can barely see inside, and it drains you. Stay in the light." },
            new EnemyDef { Name = "Sickle", Hp = 60f, Radius = 13f, Speed = 90f, Score = 9, Xp = 11, Sides = 5, Color = new Rgba(0.84f, 0.8f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Throws a crescent blade that loops out through you and comes back round to it. Dodge it twice." },
            new EnemyDef { Name = "Nova", Hp = 40f, Radius = 9f, Speed = 230f, Score = 8, Xp = 10, Sides = 4, Color = new Rgba(1f, 0.6f, 0.26f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "A small unstable star. Close in it lights a 3 s fuse, then goes nova. While lit it takes 50% more damage: burst it or back off." },
            new EnemyDef { Name = "Gutworm", Hp = 70f, Radius = 13f, Speed = 110f, Score = 9, Xp = 12, Sides = 7, Color = new Rgba(0.85f, 0.54f, 0.54f), Melee = true, Contact = 12f,
                Picker = PickerGroup.Melee, Blurb = "Dives, and a ripple chases you underground. It stops, marks the ground, then erupts with a bite and a ring of shots: get out of the mark." },
            new EnemyDef { Name = "Hook Thrower", Hp = 55f, Radius = 14f, Speed = 80f, Score = 9, Xp = 10, Sides = 6, Color = new Rgba(0.35f, 0.24f, 0.23f), Melee = true,
                Picker = PickerGroup.Melee, Blurb = "Shows a line, then throws a meat hook down it. A hit hurts and reels you in: sidestep the line, or shoot the chain." },
            new EnemyDef { Name = "Maw Priest", Hp = 90f, Radius = 14f, Speed = 80f, Score = 10, Xp = 12, Sides = 8, Color = new Rgba(0.42f, 0.16f, 0.2f),
                Picker = PickerGroup.Ranged, Blurb = "Wears the Devourer's jaw: shots into its maw heal it, shots in its back do more. It turns slowly: circle it." },
            new EnemyDef { Name = "Dust Devil", Hp = 22f, Radius = 10f, Speed = 135f, Score = 3, Xp = 6, Sides = 6, Color = new Rgba(0.62f, 0.91f, 0.85f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "A small whirlwind that weaves in at you. Its touch shoves you hard, into whatever is behind you." },
            new EnemyDef { Name = "Squall Gunner", Hp = 45f, Radius = 13f, Speed = 85f, Score = 7, Xp = 10, Sides = 5, Color = new Rgba(0.56f, 0.82f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Carries its own gust: it aims three shots upwind of you, and they curve back onto you. Dodge against the curve." },
            new EnemyDef { Name = "Stormcaller", Hp = 50f, Radius = 13f, Speed = 80f, Score = 8, Xp = 11, Sides = 7, Color = new Rgba(1f, 0.82f, 0.4f),
                Picker = PickerGroup.Ranged, Blurb = "Raises its staff and calls lightning on you: a ring fills under you, then the strike. Step out of the ring." },
            new EnemyDef { Name = "Grounder", Hp = 90f, Radius = 15f, Speed = 70f, Score = 9, Xp = 12, Sides = 6, Color = new Rgba(0.82f, 0.54f, 0.29f),
                Picker = PickerGroup.Ranged, Blurb = "A copper shield grounds your shots. Twelve charge it, and it claps a ring back with two gaps. Flank it." },
            new EnemyDef { Name = "Pylon", Hp = 55f, Radius = 12f, Speed = 110f, Score = 5, Xp = 7, Sides = 4, Color = new Rgba(0.56f, 0.82f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Comes in pairs that plant either side of you. The arc between them flickers, then fires. Step off the line, or kill one." },
            new EnemyDef { Name = "Lineman", Hp = 60f, Radius = 13f, Speed = 85f, Score = 8, Xp = 11, Sides = 5, Color = new Rgba(0.56f, 0.82f, 1f),
                Picker = PickerGroup.Ranged, Blurb = "Hooks a player with a charge. When it runs out it jumps to every ally nearby. The marked player walks away from the team." },
            new EnemyDef { Name = "Thunderhead", Hp = 220f, Radius = 24f, Speed = 55f, Score = 12, Xp = 20, Sides = 9, Color = new Rgba(0.6f, 0.68f, 0.82f), Melee = true, Contact = 20f, Heavy = true,
                Picker = PickerGroup.Melee, Blurb = "A slow, heavy storm cloud. As it rolls at you it drops a row of strikes ahead of it. Sidestep the row." },
            new EnemyDef { Name = "Turnkey", Hp = 70f, Radius = 13f, Speed = 160f, Score = 12, Xp = 16, Sides = 8, Color = new Rgba(0.95f, 0.78f, 0.3f), Melee = true, Contact = 6f,
                Picker = PickerGroup.Ranged, Blurb = "A prison janitor with a ring of keys. Runs from you, stops to plant mines, shackle traps and wet floors, and builds Cell Doors that let Rioters out. Corner it while it works." },
            new EnemyDef { Name = "Cell Door", Hp = 90f, Radius = 16f, Speed = 0f, Score = 4, Xp = 5, Sides = 4, Color = new Rgba(0.6f, 0.66f, 0.74f), Melee = true, Heavy = true,
                Picker = PickerGroup.Hidden, Blurb = "A Turnkey's cell door, built into the arena's outer wall: its red beacon spins faster as a Rioter is due, then the gate slides open to let it out. It jams shut when its Turnkey dies." },
            new EnemyDef { Name = "Floor Mine", Hp = 1f, Radius = 8f, Speed = 0f, Score = 0, Xp = 0, Sides = 8, Color = new Rgba(1f, 0.45f, 0.3f), Melee = true, Heavy = true,
                Picker = PickerGroup.Hidden, Blurb = "A Turnkey's mine: arms, then beeps and blows when you step close. Shoot it to blow it on enemies." },
            new EnemyDef { Name = "Shackle Trap", Hp = 1f, Radius = 9f, Speed = 0f, Score = 0, Xp = 0, Sides = 8, Color = new Rgba(0.75f, 0.78f, 0.85f), Melee = true, Heavy = true,
                Picker = PickerGroup.Hidden, Blurb = "A Turnkey's shackle: arms, then snaps shut on the first player over it and holds them a moment. Shoot it to break it." },
        };

        public const int WeaponsPerRole = 3;  // every role and class has three weapons (Choice 0-2); a few have a fourth (Choice 3, the lobby's 4th key): WeaponCount
        public const int MaxWeapons = 6;  // (the fifth and sixth are the Twin Rifle's variant weapons, Choice 4 and 5: the Auto and Out of Ammo; the Soldier DPS's fourth is the Double Barrel)

        public const int KitCount = 8;

        /// Every weapon, in lobby order (Role, then Kit, then Choice), gathered from each class's list: Weapons.cs (Soldier),
        /// Minions.cs (Elementalist, Summoner), Knight.cs, Ninja.cs, Hemomancer.cs. Every place must be filled exactly once.
        /// The three weapons of every role and class come first (indexed by role, kit and choice); the fourth weapons follow them.
        public static readonly WeaponDef[] Weapons = WeaponRegistry(Classes.WeaponLists());

        static bool[] gap;          // lobby places a FillsGaps weapon stands in for (locked in the lobby): [((role * KitCount) + kit) * 3 + choice]

        /// True when this lobby place has no weapon of its own yet (a class still being built): the lobby shows it locked.
        public static bool WeaponGap(Role role, Kit kit, int index) =>
            gap != null && (int)role < 3 && (int)kit < KitCount && index >= 0 && index < WeaponsPerRole && gap[((int)role * KitCount + (int)kit) * WeaponsPerRole + index];

        /// A class with no weapons of its own for this role (the Engineer outside DPS): the lobby won't pick it for that role.
        public static bool KitLocked(Role role, Kit kit) => role != Role.Wildcard && WeaponGap(role, kit, 0);

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

        /// How many weapons (or 1st abilities) a Wildcard can pick from: every class's three DPS ones, numbered kit * 3 + choice.
        public const int WildcardChoices = KitCount * WeaponsPerRole;

        /// A Wildcard's lobby "wild" byte: its Wildcard ability (0-17, same numbering as the first), which starts on the 2nd ability key.
        /// (Older favourites also stored a key as 18 * (key - 1); WildSecond drops it.)
        public static int WildSecond(int wild) => wild % WildcardChoices;
        public static byte MakeWild(int second) => (byte)Math.Max(0, Math.Min(WildcardChoices - 1, second));

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
            gap = new bool[all.Length];
            for (int i = 0; i < all.Length; i++)
                if (all[i] == null)
                {
                    int kit = i / WeaponsPerRole % KitCount, role = i / (KitCount * WeaponsPerRole);
                    for (int pass = 0; pass < 2 && all[i] == null; pass++)  // its own role's stand-in first, else any of the class's
                        foreach (var list in lists)
                            foreach (var w in list)
                                if (all[i] == null && w.FillsGaps && (int)w.Kit == kit && (pass == 1 || (int)w.Role == role))
                                    all[i] = w;
                    gap[i] = all[i] != null;
                }
            for (int i = 0; i < all.Length; i++)
                if (all[i] == null)
                    throw new InvalidOperationException("No " + (Role)(i / (KitCount * WeaponsPerRole)) + " weapon (Choice " + i % WeaponsPerRole
                        + ") for Kit." + (Kit)(i / WeaponsPerRole % KitCount) + ": add it to that class's WeaponDef list");
            seen[(int)WeaponId.TrainingPistol] = true;  // (Training's own weapon: Training.Pistol, never in a lobby place)
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
            if (id == WeaponId.TrainingPistol)
                return Training.Pistol;
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
            // Hemomancer
            BloodShot = 39, ClotShot = 40, LeechShot = 41, TransfuseShot = 42, SanguineShot = 43, VeinShot = 44, ImplodeShot = 45, BloodBrawlerShot = 46,
            StunBolt = 47,   // the Stun Gun's bolt (Stun.cs)
            FlameShot = 48,  // a Flamethrower flame
            HammerShot = 49, // the Storm Hammer, spinning as it flies
            BoulderShot = 50, // the Golem Staff's boulder, lobbed: it flies over enemies and crushes where it lands
            FireballShot = 51, // the Fireball: big, rolling on through enemies and setting them burning
            CrescentShot = 52, // the Eclipse's crescent blade (an enemy kind: 10-19 were all taken)
            // 53, 54: retired (were the Pauser's and the Stitcher's shots)
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
            ThrownDagger = 65, // a Second's thrown dagger: steel with a gold hilt, spinning a little (CourtFaction.cs, Game/CourtLook.cs)
            EmberSpark = 66,   // an Ember's spark: drifts back slowly and hangs behind it (Penumbral.cs, Game/PenumbralLook.cs)
            GreatStar = 67,    // the Shuriken's Great Star, flying a drawn route and back (GreatStar.cs, Game/GreatStarLook.cs)
            StoneSpear = 68,   // a Stonespear's earth spear, thrown or sent in a volley (Stonespear.cs, Game/StonespearLook.cs)
            HoneyGlob = 69,    // a Honeybee Staff honey glob: flies over enemies and lands as a honey puddle (HoneybeeStaff.cs, Game/HoneybeeLook.cs)
            BottleShot = 70,   // a Rioter's thrown bottle: spins as it flies, then shatters (RiotBrains.cs, Game/WardenLook.cs)
            WaterballShot = 71, // the Waterball: rolls on through enemies and soaks them (ElementBalls.cs, Game/ElementBallLook.cs)
            GlyphShot = 73,    // a Glyph Stylus rune bolt: goes through enemies and heals the allies it passes (Arcanist.cs, Game/ArcanistLook.cs)
            SpectralBlade = 74, // a spectral blade: Runic Array's volleys and the shots a Grand Sigil sends back (Arcanist.cs, Game/ArcanistLook.cs)
            FocusArrow = 75,   // a Mender's Bow Focus arrow: gold, wrapped in wind, shoves what it passes (BowFocus.cs, Game/BowFocusLook.cs)
            ScatterArrow = 76, // one of the Mender's Bow's Scatter arrows: small and teal (BowFocus.cs, Game/BowFocusLook.cs)
            WaterJet = 77,     // a Water Gun jet: stops on the first enemy and splashes it and those near it wet (ElementGuns.cs, Game/ElementGunLook.cs)
            GaleShot = 78,     // a Gale Gun gust: passes through, slicing (bleed) and slowing (ElementGuns.cs, Game/ElementGunLook.cs)
            TankShell = 79,    // A Literal Tank's shell: bursts on what it hits, or at the cursor (LiteralTank.cs, Game/LiteralTankLook.cs)
            Senbon = 81,       // a Senbon Seekers needle curving in on an enemy (SenbonSeekers.cs, Game/SenbonSeekersLook.cs)
            RodentShot = 80,   // a Rodent Minigun rodent: a mouse, rat or hamster by its id (Game.RodentOf; RodentMinigun.cs, Game/RodentMinigunLook.cs)
            BlastOfudaShot = 82, // the Ofuda's Blast Ofuda: a red-sealed charm that sticks to what it hits and blows up (Ofuda.cs, Game/WorldShots.cs)
            AstralBolt = 83,   // Astral Projection's bolt, shot from the spirit (AstralProjection.cs)
            BloodNeedle = 84,  // the Acupuncture Needles' Bloodletting needle: long, red-beaded; sticks and bleeds (AcupunctureNeedles.cs, Game/AcupunctureLook.cs)
            HorseArrow = 85,   // an arrow shot out of a Trojan Horse's side slit (TrojanHorse.cs, Game/TrojanHorseLook.cs)
            BallistaBolt = 86, // a Ballista's giant bolt: through everything, knocking what it passes (Ballista.cs, Game/BallistaLook.cs)
            EarthballShot = 72; // the Earthball: stops on the first enemy, stuns it and breaks into rubble (ElementBalls.cs, Game/ElementBallLook.cs)

        public static bool IsEnemyShot(byte kind) => kind >= 10 && kind < 20 || kind == CrescentShot || kind == TaserBolt || kind == TracerRound || kind == StingBolt || kind == MagmaBolt || kind == ThrownDagger || kind == EmberSpark || kind == BottleShot;

        /// Bosses' aimed attacks: barriers, walls and other blockers can't stop these.
        public static bool Unblockable(byte kind) => kind == PierceShot;

        /// What each bullet kind looks like and does, in one place: its radius, its colour and, for enemy shots, its listed
        /// damage (before EnemyBulletMult). A kind with no row here gets the defaults. Add a row for a new kind.
        public struct BulletInfo { public float Radius, Damage; public Rgba Color; }

        static readonly BulletInfo[] BulletTable = BuildBulletTable();

        static void Row(BulletInfo[] table, byte kind, float radius = 6f, Rgba? color = null, float damage = 12f)
        {
            table[kind] = new BulletInfo { Radius = radius, Damage = damage, Color = color ?? new Rgba(1f, 0.3f, 0.45f) };
        }

        static BulletInfo[] BuildBulletTable()
        {
            var table = new BulletInfo[256];
            for (int i = 0; i < table.Length; i++)
                table[i] = new BulletInfo { Radius = 6f, Damage = 12f, Color = new Rgba(1f, 0.3f, 0.45f) };
            Row(table, TankShot, radius: 5f, color: new Rgba(0.5f, 0.75f, 1f));
            Row(table, HealerShot, radius: 4.5f, color: new Rgba(0.5f, 1f, 0.65f));
            Row(table, DpsShot, radius: 3.5f, color: new Rgba(1f, 0.7f, 0.35f));
            Row(table, TurretShot, radius: 4f, color: new Rgba(0.6f, 0.95f, 1f));
            Row(table, NovaShot, radius: 6f, color: new Rgba(0.95f, 0.8f, 1f));
            Row(table, FlakShell, radius: 8f, color: new Rgba(0.7f, 0.85f, 1f));
            Row(table, TankShell, radius: 9f, color: new Rgba(1f, 0.62f, 0.25f));
            Row(table, HorseArrow, radius: 4.5f, color: new Rgba(1f, 0.8f, 0.35f));
            Row(table, BallistaBolt, radius: 12f, color: new Rgba(1f, 0.8f, 0.35f));  // the Ballista's giant bolt (Ballista.cs)
            Row(table, AstralBolt, radius: 5f, color: new Rgba(0.73f, 0.96f, 1f));  // the spirit's pale cyan (AstralProjection.cs)
            Row(table, Senbon, radius: 4f, color: new Rgba(0.86f, 0.89f, 0.93f));  // steel (the venom tip and ink tail are drawn by SenbonSeekersLook.cs)
            Row(table, RodentShot, radius: 5f, color: new Rgba(0.72f, 0.7f, 0.77f));  // a mouse's grey (rats and hamsters are drawn in their own colours)
            Row(table, RailShot, radius: 3.5f, color: new Rgba(1f, 1f, 0.75f));
            Row(table, WispShot, radius: 5f, color: new Rgba(0.4f, 1f, 0.85f));  // no longer fired by any weapon (Twin Wisps became the Pistol)
            Row(table, GrenadeShot, radius: 7f, color: new Rgba(0.75f, 0.85f, 0.4f));
            Row(table, SwarmShot, radius: 3f, color: new Rgba(1f, 0.85f, 0.35f));
            Row(table, FireShot, radius: 6f, color: new Rgba(1f, 0.45f, 0.15f));
            Row(table, FrostShot, radius: 4.5f, color: new Rgba(0.6f, 0.9f, 1f));
            Row(table, FrostLance, radius: 10f, color: new Rgba(0.6f, 0.9f, 1f));  // a wide spearhead: much easier to land than the old 4.5 px shard
            Row(table, ArcaneShot, radius: 5f, color: new Rgba(0.75f, 0.45f, 1f));
            Row(table, HolyShot, radius: 9f, color: new Rgba(1f, 0.95f, 0.6f));
            Row(table, BoltShot, radius: 4f, color: new Rgba(0.75f, 0.85f, 1f));
            Row(table, SeedShot, radius: 4f, color: new Rgba(0.55f, 0.9f, 0.35f));
            Row(table, FireflyShot, radius: 5f, color: new Rgba(0.89f, 0.97f, 0.56f));
            Row(table, TreantSeed, radius: 2.5f, color: new Rgba(0.74f, 0.94f, 0.54f));
            Row(table, SteelShard, radius: 4f, color: new Rgba(1f, 0.59f, 0.24f));
            Row(table, ParryNeedle, radius: 4f, color: new Rgba(1f, 1f, 1f));
            Row(table, Caltrop, radius: 5f, color: new Rgba(0.81f, 0.84f, 0.9f));
            Row(table, BoneShot, radius: 3.5f, color: new Rgba(0.92f, 0.9f, 0.8f));
            Row(table, RockShot, radius: 6f, color: new Rgba(0.7f, 0.6f, 0.5f));
            Row(table, MinionShot, radius: 3.5f, color: new Rgba(0.85f, 1f, 0.95f));
            Row(table, KnightWave, radius: 11f, color: new Rgba(0.85f, 0.92f, 1f));
            Row(table, ArrowShot, radius: 4.5f, color: new Rgba(0.5f, 1f, 0.65f));
            Row(table, FocusArrow, radius: 6f, color: new Rgba(1f, 0.82f, 0.3f));
            Row(table, ScatterArrow, radius: 3.5f, color: new Rgba(0.5f, 0.9f, 0.85f));
            Row(table, StarShot, radius: 5.5f, color: new Rgba(0.8f, 0.85f, 0.95f));
            Row(table, NeedleShot, radius: 3f, color: new Rgba(0.55f, 1f, 0.75f));
            Row(table, BloodNeedle, radius: 3.5f, color: new Rgba(1f, 0.3f, 0.38f));
            Row(table, KunaiShot, radius: 4f, color: new Rgba(0.8f, 0.85f, 0.95f));
            Row(table, OfudaShot, radius: 6f, color: new Rgba(1f, 0.92f, 0.75f));  // the Ofuda's Healing Talisman
            Row(table, BlastOfudaShot, radius: 6f, color: new Rgba(1f, 0.9f, 0.78f));
            Row(table, GustShot, radius: 9f, color: new Rgba(0.7f, 1f, 0.85f));
            Row(table, FumaStar, radius: 20f, color: new Rgba(0.8f, 0.85f, 0.95f));  // giant
            Row(table, GreatStar, radius: 24f, color: new Rgba(0.8f, 0.85f, 0.95f));  // huger
            Row(table, StoneSpear, radius: 8f, color: new Rgba(1f, 0.69f, 0.29f));  // basalt with amber seams (the colour is the seams' glow)
            Row(table, HoneyGlob, radius: 4f, color: new Rgba(1f, 0.71f, 0.18f));  // lobbed: it never collides
            Row(table, BloodShot, radius: 4f, color: new Rgba(0.9f, 0.12f, 0.2f));
            Row(table, ClotShot, radius: 14f, color: new Rgba(0.6f, 0.05f, 0.12f));  // the Gore Mortar's massive clot (lobbed: it never collides)
            Row(table, LeechShot, radius: 3.5f, color: new Rgba(1f, 0.25f, 0.3f));
            Row(table, TransfuseShot, radius: 3f, color: new Rgba(1f, 0.5f, 0.55f));
            Row(table, SanguineShot, radius: 6.5f, color: new Rgba(0.85f, 0.1f, 0.25f));
            Row(table, ImplodeShot, radius: 3f, color: new Rgba(0.9f, 0.12f, 0.2f));
            Row(table, BloodBrawlerShot, radius: 6f, color: new Rgba(0.95f, 0.9f, 0.82f));
            Row(table, BigShot, radius: 10f, color: new Rgba(0.85f, 0.35f, 1f), damage: 20f);
            Row(table, MineShot, radius: 9f, color: new Rgba(1f, 0.55f, 0.2f), damage: 18f);
            Row(table, PierceShot, radius: 6.5f, color: new Rgba(1f, 0.12f, 0.12f), damage: 30f);  // a boss's red aimed shot: unblockable, and it hurts
            Row(table, FastShot, radius: 4.5f, color: new Rgba(1f, 0.95f, 0.4f), damage: 15f);
            Row(table, StunShot, radius: 7f, color: new Rgba(0.43f, 0.88f, 1f), damage: 8f);  // the Warden's searchlight turn: it stuns you for 1 s
            Row(table, StunBolt, radius: 5f, color: new Rgba(0.43f, 0.88f, 1f));
            Row(table, FlameShot, radius: 7f, color: new Rgba(1f, 0.55f, 0.15f));
            Row(table, HammerShot, radius: 22f, color: new Rgba(0.75f, 0.85f, 1f));
            Row(table, BoulderShot, radius: 16f, color: new Rgba(0.62f, 0.54f, 0.45f));
            Row(table, MedkitShot, radius: 11f, color: new Rgba(0.95f, 0.97f, 1f));
            Row(table, FireballShot, radius: 15f, color: new Rgba(1f, 0.5f, 0.15f));
            Row(table, WaterballShot, radius: 15f, color: new Rgba(0.3f, 0.65f, 1f));
            Row(table, WaterJet, radius: 5f, color: new Rgba(0.31f, 0.82f, 0.91f));
            Row(table, GaleShot, radius: 8f, color: new Rgba(0.85f, 0.97f, 1f));
            Row(table, EarthballShot, radius: 16f, color: new Rgba(0.82f, 0.6f, 0.36f));
            Row(table, GlyphShot, radius: 6f, color: new Rgba(1f, 0.85f, 0.45f));     // the Arcanist's rune bolt (Arcanist.cs)
            Row(table, SpectralBlade, radius: 5f, color: new Rgba(0.78f, 0.62f, 1f));  // Runic Array's and the Grand Sigil's spectral blades  // (R over 0.8: its rubble isn't read as a crate's, PropsLook.IsCrateDebris)
            Row(table, HoneyShot, radius: 9f, color: new Rgba(1f, 0.67f, 0.16f), damage: 14f);  // the Hive Queen's nectar spray
            Row(table, HollowShot, radius: 7f, color: new Rgba(0.9f, 0.91f, 1f), damage: 0f);  // a Duelist decoy's shot: a harmless ring; the Duelist's decoys fire these: they pass through you
            Row(table, CrescentShot, radius: 14f, color: new Rgba(0.89f, 0.83f, 1f), damage: 18f);  // the Eclipse's crescent blade; the Eclipse's crescent blades
            Row(table, TaserBolt, radius: 7f, color: new Rgba(0.75f, 0.91f, 1f), damage: 8f);  // the Fork Bolt's ice white; a Taser Guard's Fork Bolt: the same stun
            Row(table, StingBolt, radius: 7f, color: new Rgba(0.784f, 1f, 0.239f), damage: 8f);  // the Stinger's venom lime; a Stinger's dart: the same
            Row(table, MagmaBolt, radius: 8f, color: new Rgba(1f, 0.48f, 0.23f), damage: 9f);  // the Cinder Priest's magma; a Cinder Priest's bolt (and its lava)
            Row(table, ThrownDagger, radius: 5f, color: new Rgba(0.81f, 0.84f, 1f), damage: 10f);  // a Second's dagger (steel); a Second's dagger
            Row(table, EmberSpark, radius: 5f, color: new Rgba(1f, 0.6f, 0.26f), damage: 5.2f);  // an Ember's spark; an Ember's spark: 6 once the enemy bullet multiplier is on
            Row(table, BottleShot, radius: 6f, color: new Rgba(0.45f, 0.8f, 0.4f), damage: 10f);  // a Rioter's bottle (glass green)
            Row(table, TracerRound, radius: 5f, color: new Rgba(0.94f, 0.97f, 1f), damage: 6.1f);  // the Security Guard's hot-white tracer; a Security Guard's round: 7 once the enemy bullet multiplier is on
            Row(table, BileShot, radius: 6f, color: new Rgba(0.75f, 0.9f, 0.31f));  // the Devourer's vomit
            Row(table, ToothShot, radius: 10f, color: new Rgba(0.96f, 0.9f, 0.82f), damage: 20f);  // the Devourer's Jaws
            Row(table, KnightSlash, color: new Rgba(0.85f, 0.92f, 1f));
            Row(table, VeinShot, color: new Rgba(1f, 0.35f, 0.5f));
            return table;
        }

        public static float BulletRadius(byte kind) => BulletTable[kind].Radius;

        public static Rgba BulletColor(byte kind) => BulletTable[kind].Color;

        static float BaseBulletDamage(byte kind) => BulletTable[kind].Damage;

        /// Every enemy bullet hits this much harder than its listed damage below.
        public const float EnemyBulletMult = 1.15f;

        public static float BulletDamage(byte kind) => BaseBulletDamage(kind) * EnemyBulletMult;
    }
}
