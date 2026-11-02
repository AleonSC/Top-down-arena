using System;
namespace Arena
{
    public enum Role : byte { Tank = 0, Healer = 1, Dps = 2 }

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
        Swarmer, Brute, Lunger,
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
        Flamethrower, FrostLance, StormHammer, RadiantOrb, ChainLight, PrismBeam, Fireball, ArcLightning, ArcaneMissiles,
        // Summoner
        GolemStaff, BoneGuard, BearTotem, SpriteStaff, TreantRod, TotemStaff, WolfWhistle, WispLantern, ImpCaller,
        // Knight (melee)
        Warhammer, Lance, Halberd, HolyMace, Lightbringer, Censer, TwinBlades, Greatsword, Rapier,
        // Ninja (Ninja.cs): thrown blades and quick melee
        Kusarigama, BoStaff, FumaShuriken, MedicineDarts, SpiritFan, Ofuda, Shuriken, Katana, Kunai,
        // Blood Mage (BloodMage.cs): blood spells that drain and heal
        BloodLash, ClotBolt, LeechBeam, TransfusionDarts, SanguineOrb, VeinTether, BloodBolts, Implosion, BoneSpear,
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
        public string ExtraDesc;  // what each extra projectile (Multishot, Bullet Storm) does for this weapon
        public float FireInterval;
        // Summoner weapons keep companions around. Imps blow themselves up, so a new one
        // arrives every CompanionRespawn seconds; the others only die to Sacrifice.
        public MinionKind Companion = MinionKind.None;
        public int CompanionCount;
        public float CompanionRespawn = 3f;
        public bool Melee;  // swings instead of shots (Knight.cs; the Ninja's Kusarigama, Bo Staff and Katana and the Blood Lash too)

        // The main attack's numbers, in one place: the code that fires it, WeaponRange (the aim line) and the bots
        // all read these. Secondary parts (fragments, sword waves, a Katana's quick-draw, heals) stay with the weapon's code.
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
            new EnemyDef { Name = "Swarmer", Hp = 22f, Radius = 9f, Speed = 135f, Score = 5, Xp = 5, Sides = 3, Color = new Rgba(1f, 0.6f, 0.3f), Melee = true, Contact = 10f,
                Picker = PickerGroup.Melee, Blurb = "Small and fast melee rusher that weaves toward you in pairs." },
            new EnemyDef { Name = "Brute", Hp = 110f, Radius = 19f, Speed = 62f, Score = 15, Xp = 14, Sides = 7, Color = new Rgba(0.8f, 0.45f, 0.35f), Melee = true, Contact = 24f,
                Picker = PickerGroup.Melee, Blurb = "Slow, tough melee bruiser that hits hard (24) when it reaches you." },
            new EnemyDef { Name = "Lunger", Hp = 45f, Radius = 12f, Speed = 95f, Score = 12, Xp = 12, Sides = 4, Color = new Rgba(0.95f, 0.35f, 0.65f), Melee = true, Contact = 18f,
                Picker = PickerGroup.Melee, Blurb = "Circles in close, flashes a pink line, then leaps along it." },
            new EnemyDef { Name = "Hive Bomber", Hp = 30f, Radius = 10f, Speed = 330f, Score = 2, Xp = 0, Sides = 3, Color = new Rgba(1f, 0.45f, 0.15f), Melee = true },
            new EnemyDef { Name = "Cage Well", Hp = 40f, Radius = 11f, Speed = 0f, Score = 5, Xp = 2, Sides = 6, Color = new Rgba(0.85f, 0.4f, 1f), Melee = true },
            new EnemyDef { Name = "Rioter", Hp = 40f, Radius = 12f, Speed = 110f, Score = 3, Xp = 2, Sides = 8, Color = new Rgba(1f, 0.55f, 0.2f), Melee = true, Contact = 10f },
            new EnemyDef { Name = "Hive Egg", Hp = 60f, Radius = 15f, Speed = 0f, Score = 4, Xp = 3, Sides = 10, Color = new Rgba(0.96f, 0.92f, 0.78f), Melee = true },
            new EnemyDef { Name = "Guard", Hp = 90f, Radius = 13f, Speed = 125f, Score = 6, Xp = 4, Sides = 8, Color = new Rgba(0.35f, 0.55f, 1f), Melee = true, Contact = 14f },
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
            new EnemyDef { Name = "Riot Shield", Hp = 60f, Radius = 16f, Speed = 120f, Score = 10, Xp = 7, Sides = 6, Color = new Rgba(0.55f, 0.68f, 0.95f), Melee = true, Contact = 8f,
                Picker = PickerGroup.Melee, Blurb = "A guard behind a tall shield that bashes you back. Go round it, or break the shield: then it falls fast." },
            new EnemyDef { Name = "Gas Grenadier", Hp = 50f, Radius = 14f, Speed = 75f, Score = 8, Xp = 6, Sides = 5, Color = new Rgba(0.62f, 0.92f, 0.42f),
                Picker = PickerGroup.Ranged, Blurb = "Lobs tear-gas canisters at you. Step out of the cloud." },
        };

        public const int WeaponsPerRole = 3;

        public const int KitCount = 6;

        /// Every weapon, in lobby order (Role, then Kit, then Choice), gathered from each class's list: Weapons.cs (Soldier),
        /// Minions.cs (Mage, Summoner), Knight.cs, Ninja.cs, BloodMage.cs. Every place must be filled exactly once.
        public static readonly WeaponDef[] Weapons = WeaponRegistry(Game.SoldierWeapons, Game.ElementalistWeapons, Game.SummonerWeapons,
            Game.KnightWeapons, Game.NinjaWeapons, Game.HemomancerWeapons);

        /// A weapon's damage a second with no upgrades (companions and burning included), measured in the sim
        /// (Tools/SimTest/run.sh 1 1 weapondps write, which writes WeaponDps.Generated.cs); 0 if not measured.
        public static float WeaponDps(WeaponId id) => MeasuredDps.TryGetValue(id, out float v) ? v : 0f;

        public static WeaponDef Weapon(Role role, Kit kit, int index) => Weapons[((int)role * KitCount + (int)kit) * WeaponsPerRole + index];

        /// Puts every class's weapons in lobby order. The game refuses to start if a place is empty or doubled, or a weapon id is missing.
        static WeaponDef[] WeaponRegistry(params WeaponDef[][] lists)
        {
            var all = new WeaponDef[3 * KitCount * WeaponsPerRole];
            var seen = new bool[Enum.GetValues(typeof(WeaponId)).Length];
            foreach (var list in lists)
                foreach (var w in list)
                {
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
            return all;
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
        public static float XpToNext(int level) => 50f + 30f * (level - 1);

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
            StitchShot = 54;   // the Stitcher's orbs: each is tied to the nearest other one by a harmful thread (Variety.cs)

        public static bool IsEnemyShot(byte kind) => kind >= 10 && kind < 20 || kind == CrescentShot || kind == PauseShot || kind == StitchShot;

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
                case ClotShot: return 7f;
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
                case HammerShot: return 11f;
                case BoulderShot: return 16f;
                case FireballShot: return 15f;
                case HoneyShot: return 9f;
                case HollowShot: return 7f;
                case CrescentShot: return 14f;
                case PauseShot: return 6.5f;
                case StitchShot: return 8f;
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
                case FireballShot: return new Rgba(1f, 0.5f, 0.15f);
                case HoneyShot: return new Rgba(1f, 0.67f, 0.16f);
                case HollowShot: return new Rgba(0.9f, 0.91f, 1f);  // a Duelist decoy's shot: a harmless ring
                case CrescentShot: return new Rgba(0.89f, 0.83f, 1f);  // the Eclipse's crescent blade
                case PauseShot: return new Rgba(0.45f, 0.95f, 0.85f);  // the Pauser's teal
                case StitchShot: return new Rgba(1f, 0.82f, 0.45f);    // the Stitcher's gold
                case BileShot: return new Rgba(0.75f, 0.9f, 0.31f);  // the Devourer's vomit
                case ToothShot: return new Rgba(0.96f, 0.9f, 0.82f);  // the Devourer's Jaws
                default: return new Rgba(1f, 0.3f, 0.45f);
            }
        }
    }
}
