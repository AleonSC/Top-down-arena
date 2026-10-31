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
    }

    public class RoleDef
    {
        public string Name, Ability, Desc;
        public Rgba Color;
        public float Hp, Speed, HitR, BodyR, FireInterval, AbilityCd;
    }

    /// Soldier is the original gun-based kit; Mage and Summoner are the alternatives for each role.
    public enum Kit : byte { Soldier, Mage, Summoner, Knight, Ninja, BloodMage }

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
        public string Name, Desc;
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
    public static class Defs
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
                Hp = 120f, Speed = 215f, HitR = 7f, BodyR = 14f, FireInterval = 0.25f, AbilityCd = 10f,
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
        };

        public const int WeaponsPerRole = 3;

        public const int KitCount = 6;

        // Three weapons per role and kit, ordered Role, then Kit (Soldier, Mage, Summoner, Knight, Ninja, Blood Mage).
        public static readonly WeaponDef[] Weapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Scattergun, Speed = 520f, Life = 0.5f, Damage = 7f, BotRange = 210f, ExtraDesc = "+2 pellets and a 15% tighter cone", Name = "Scattergun", FireInterval = 0.45f,
                Desc = "A short-range spread of 5 pellets. Point-blank (within 120 px), each pellet does double and shoves the enemy back." },
            new WeaponDef { Id = WeaponId.FlakCannon, Speed = 380f, Life = 1.1f, Damage = 30f, ExtraDesc = "Shells burst into 2 more bomblets", Name = "Flak Cannon", FireInterval = 0.8f,
                Desc = "Shells fly over enemies to your cursor and burst in the air there (30 within 80 px), destroying enemy bullets caught in the burst." },
            new WeaponDef { Id = WeaponId.RiotRepeater, Speed = 600f, Life = 0.8f, Damage = 11f, ExtraDesc = "+1 round per burst (each trigger pull fires a burst, then a short pause)", Name = "Riot Repeater", FireInterval = 0.18f,
                Desc = "Steady single shots with a little spread. Each round that hits suppresses the enemy: it shoots 8% slower, up to 50% (25% for bosses), fading quickly." },
            new WeaponDef { Id = WeaponId.Flamethrower, Speed = 480f, Life = 0.4f, Damage = 3f, Grow = 0.25f, BotRange = 160f, ExtraDesc = "+1 flame a burst and 25% more reach", Name = "Flamethrower", FireInterval = 0.06f,
                Desc = "A short-range stream of fire: each flame hits the first enemy for 3 and carries on through the rest for 1, setting them burning (5 a second for 3 s)." },
            new WeaponDef { Id = WeaponId.FrostLance, Damage = 18f, Reach = 230f, Grow = 30f, BotRange = 190f, ExtraDesc = "+30 px longer icicle and 25% more damage, and its splinters fly 15 px further", Name = "Frost Lance", FireInterval = 0.4f,
                Desc = "A great icicle juts out from you (230 px), then splinters off its sides into enemies up to 60 px beside it (50% damage). Both slow." },
            new WeaponDef { Id = WeaponId.StormHammer, Speed = 460f, Life = 1.1f, Damage = 40f, BotRange = 260f, ExtraDesc = "+15% hammer damage, and the electrified enemy arcs to 1 more enemy", Name = "Storm Hammer", FireInterval = 0.7f,
                Desc = "Throws a spinning hammer that smashes one enemy and electrifies it: for a second it arcs lightning to 3 enemies around it, three times, for a little damage." },
            new WeaponDef { Id = WeaponId.GolemStaff, Damage = 40f, BotRange = 380f, ExtraDesc = "+1 Golem (up to 3)", Name = "Golem Staff", FireInterval = 0.5f, Desc = "Hold to heave a boulder, let go to throw it in an arc: the longer you hold, the farther it flies. It crushes everything where it lands. A stone Golem fights beside you and blocks bullets.",
                Companion = MinionKind.Golem, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.BoneGuard, Speed = 720f, Life = 0.8f, Damage = 7f, ExtraDesc = "+1 Skeleton Guard", Name = "Bone Guard", FireInterval = 0.3f,
                Desc = "Bone darts that stick in enemies: the third in one enemy cages it for 1.5 s (not bosses). Two skeleton guards circle you, block bullets, and shoot at caged enemies.",
                Companion = MinionKind.Guard, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.BearTotem, Damage = 11f, Reach = 85f, Grow = 15f, Melee = true, ExtraDesc = "+15 px reach, 25% more bleed, and your Bear's bites hit enemies around its target", Name = "Bear Totem", FireInterval = 0.45f,
                Desc = "Two quick claw swipes in front of you that make enemies bleed. A Bear companion mauls nearby enemies.",
                Companion = MinionKind.Bear, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.Warhammer, Damage = 80f, Reach = 130f, Grow = 40f, Melee = true, ExtraDesc = "+1 aftershock: another slam 0.2 s later, 40 px further out, at 60% damage", Name = "Warhammer", FireInterval = 0.75f,
                Desc = "Hold to charge, let go to slam the ground in front of you (a 100 px wide strip). The longer the charge, the harder it hits (up to 80) and the longer it stuns (up to 0.9 s)." },
            new WeaponDef { Id = WeaponId.Lance, Damage = 36f, Reach = 180f, Grow = 30f, Melee = true, ExtraDesc = "+30 px reach, 24 px wider and +60 px knockback", Name = "Lance", FireInterval = 0.6f,
                Desc = "A heavy jab in front of you that shoves enemies far back and stuns them for 1 s." },
            new WeaponDef { Id = WeaponId.Halberd, Damage = 30f, Reach = 200f, Grow = 30f, Melee = true, ExtraDesc = "+30 px reach and a return sweep (60% damage)", Name = "Halberd", FireInterval = 0.6f,
                Desc = "Sweeps a long, thin blade across in front of you (200 px, 80°), hitting each enemy once a sweep." },
            new WeaponDef { Id = WeaponId.Kusarigama, Damage = 26f, Reach = 280f, Grow = 40f, Melee = true, ExtraDesc = "+40 px throw and +30% slash damage", Name = "Kusarigama", FireInterval = 0.55f,
                Desc = "Throw the chain-sickle straight out (280 px): it hooks the first enemy it hits (26). Fire again to reel yourself in to it and land a heavy slash (52, a 0.5 s stun)." },
            new WeaponDef { Id = WeaponId.BoStaff, Damage = 16f, Reach = 90f, Grow = 20f, Melee = true, ExtraDesc = "+20 px reach and a 30 px longer vault", Name = "Bo Staff", FireInterval = 0.4f,
                Desc = "A staff spun all the way around you (90 px) that cuts small bullets out of the air. Click while moving to vault 150 px, striking everything you pass (24); once every 1.2 s." },
            new WeaponDef { Id = WeaponId.FumaShuriken, Life = 1.1f, Damage = 30f, BotRange = 300f, ExtraDesc = "+1 shuriken, looping the other way", Name = "Fuma Shuriken", FireInterval = 0.7f,
                Desc = "A giant spinning star thrown in an oval loop 380 px out in front of you and back, cutting through everything both ways." },
            new WeaponDef { Id = WeaponId.BloodLash, Damage = 24f, Reach = 130f, Grow = 20f, Melee = true, ExtraDesc = "+20 px reach and +1 max Clot", Name = "Blood Lash", FireInterval = 0.5f,
                Desc = "A whip of blood (130 px, 110°). Its tip cracks for double damage and taunts; enemies hit closer in are flung out to the tip. "
                    + "Each hit clots your blood (+1 Clot, +2 from the tip; 4% less damage taken each, up to 5). At full Clot your next lash bursts all round you." },
            new WeaponDef { Id = WeaponId.ClotBolt, Speed = 520f, Life = 1f, Damage = 26f, ExtraDesc = "+1 bolt, fanned out", Name = "Clot Bolt", FireInterval = 0.75f,
                Desc = "A heavy bolt (26) that leaves a pool of blood where it hits, slowing enemies in it by 40% for 3 s. After 1 s the pool congeals: enemies still in it are stuck for 1 s. You take 15% less damage standing in your own pool." },
            new WeaponDef { Id = WeaponId.LeechBeam, Damage = 1.2f, Reach = 160f, Grow = 20f, BotRange = 130f, ExtraDesc = "+1 tendril and +20 px reach", Name = "Leech Tendrils", FireInterval = 0.1f,
                Desc = "Hold to lash out blood tendrils in a 70° arc (160 px) that grasp up to 3 enemies while you hold on: they're stunned, take 12 damage a second per tendril, and 25% of it heals you. Spare tendrils join the ones already holding on." },
            // Healer
            new WeaponDef { Id = WeaponId.LightBolt, Damage = 6f, Reach = 240f, ExtraDesc = "+20° wider blast, +2 damage and a harder push", Name = "Repulsion Cannon", FireInterval = 0.7f,
                Desc = "Blasts enemies away in a cone. Light damage; enemies it hits shed tiny health orbs." },
            new WeaponDef { Id = WeaponId.Pistol, Speed = 700f, Life = 1.1f, Damage = 24f, ExtraDesc = "+2 rounds in the cylinder, loaded as heavy rounds (double damage)", Name = "Revolver", FireInterval = 0.45f, Desc = "Tap for aimed shots; hold to fan the hammer and empty the cylinder in a 10° arc. Six rounds, the last one heavy." },
            new WeaponDef { Id = WeaponId.MendersBow, BotRange = 390f, ExtraDesc = "+1 arrow nocked: a full draw also looses an extra arrow (half damage) in a tight fan", Name = "Mender's Bow", FireInterval = 0.9f, Desc = "Hold to draw, release to fire. A full draw (0.9 s) hits for 40, pierces 2 and heals an ally it passes for 20; let go right at full draw for a perfect shot (+25%)." },
            new WeaponDef { Id = WeaponId.RadiantOrb, Damage = 8f, Reach = 520f, BotRange = 300f, ExtraDesc = "+1 light orb that circles your Radiant Orb (you, when it's not out), hurting enemies and healing allies it touches", Name = "Radiant Orb", FireInterval = 0.2f,
                Desc = "Hold to cast an orb of light out to your cursor; it stays there while you hold. Drag it through enemies to burn them (8, 5 times a second) and over allies to heal them." },
            new WeaponDef { Id = WeaponId.ChainLight, Damage = 40f, BotRange = 240f, ExtraDesc = "+25% damage and a 15% longer cone", Name = "Chain Light", FireInterval = 0.5f,
                Desc = "A great cone of light in front of you (320 px). Its 40 damage is shared between every enemy in it: one alone takes it all. Light arcs to each." },
            new WeaponDef { Id = WeaponId.PrismBeam, Speed = 1200f, Life = 0.35f, Damage = 4f, Grow = 0.3f, ExtraDesc = "+30% beam length, +25% damage and +50% healing", Name = "Prism Beam", FireInterval = 0.06f,
                Desc = "A rapid short-range beam. Shoot through an ally: it heals them and splits into three beams beyond them." },
            new WeaponDef { Id = WeaponId.SpriteStaff, Speed = 600f, Life = 1f, Damage = 9f, ExtraDesc = "+1 Sprite", Name = "Sprite Staff", FireInterval = 0.35f,
                Desc = "Seed shots that stick in enemies. Two sprites fly to whoever is hurt and heal them, and every seeded enemy you kill frees another for 6 s (up to 4 more).",
                Companion = MinionKind.Sprite, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.TreantRod, Speed = 600f, Life = 1f, Damage = 8f, ExtraDesc = "Your Treant grows 30% bigger: blocks more, hits harder and reaches farther", Name = "Treant Rod", FireInterval = 0.3f,
                Desc = "Bolts heal allies they pass. Your Treant roots where it stands and blocks bullets; a vine runs from you to it, healing allies who cross it and slowing enemies (it walks after you if you go too far).",
                Companion = MinionKind.Treant, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.TotemStaff, Speed = 600f, Life = 1f, Damage = 9f, ExtraDesc = "Plants totems 60% more often", Name = "Totem Staff", FireInterval = 0.35f,
                Desc = "Seed shots. Every 10 s your next shot also throws a healing totem to your cursor (up to 400 px). Seed hits on enemies near your totems charge them: at 6, a totem pulses 25 healing to allies near it." },
            new WeaponDef { Id = WeaponId.HolyMace, Damage = 18f, Reach = 105f, Melee = true, ExtraDesc = "+1 strike with the other end of the staff (60% damage)", Name = "Quarterstaff", FireInterval = 0.5f,
                Desc = "Wide staff sweeps that knock enemies back and bat enemy bullets in the arc back at them (all but red aimed shots). Every enemy hit heals you and allies near you for 2 (up to 8 a swing)." },
            new WeaponDef { Id = WeaponId.Lightbringer, Damage = 17f, Reach = 80f, Melee = true, ExtraDesc = "+1 crescent of light, fanned out", Name = "Lightbringer", FireInterval = 0.5f,
                Desc = "Sword swings that also send a short crescent of light, which heals allies it passes. Every enemy you hit fills a light gauge; at 12, your next swing's crescents heal three times as much." },
            new WeaponDef { Id = WeaponId.Censer, Damage = 14f, Reach = 80f, Grow = 20f, Melee = true, ExtraDesc = "+20 px reach and +1 HP healed per spin", Name = "Censer", FireInterval = 0.4f,
                Desc = "A flail whirled all the way around you. Each spin heals allies near you for 3. As you move it leaves a trail of incense (3 s) that heals allies in it, and enemy shots fired from inside it stray." },
            new WeaponDef { Id = WeaponId.MedicineDarts, Speed = 900f, Life = 0.8f, Damage = 7f, ExtraDesc = "+1 dart, fanned out", Name = "Medicine Darts", FireInterval = 0.18f,
                Desc = "Fast needles that dose what they hit. Enemies: a stack of poison (2 a second for 3 s). Hurt allies: a stack of healing (2 a second for 3 s). Up to 5 stacks each." },
            new WeaponDef { Id = WeaponId.SpiritFan, Speed = 420f, Life = 0.3f, Damage = 6f, Grow = 0.25f, ExtraDesc = "+25% reach and +1 HP healed", Name = "Spirit Fan", FireInterval = 0.4f,
                Desc = "A short gust that heals allies in it for 3 and pushes small enemy bullets back. Shots alternate: one blows enemies in front away, the next draws them in." },
            new WeaponDef { Id = WeaponId.Ofuda, Speed = 360f, Life = 1.6f, Damage = 25f, ExtraDesc = "+1 charm, fanned out", Name = "Ofuda", FireInterval = 0.5f,
                Desc = "Paper charms fly to your cursor and lie there as wards (up to 5, for 10 s). An enemy that steps on one sets it off: 25 to enemies round it and 12 healing to allies near it." },
            new WeaponDef { Id = WeaponId.TransfusionDarts, Speed = 850f, Life = 0.85f, Damage = 7f, ExtraDesc = "+1 dart, fanned out", Name = "Transfusion Darts", FireInterval = 0.2f,
                Desc = "Darts that transfuse your blood: one that reaches a hurt ally gives them 20 health for 4 of yours. Each enemy hit steals a sliver back (0.5)." },
            new WeaponDef { Id = WeaponId.SanguineOrb, Speed = 420f, Life = 1.4f, Damage = 16f, ExtraDesc = "+1 orb, fanned out", Name = "Sanguine Orb", FireInterval = 0.45f, BotRange = 380f,
                Desc = "Orbs of blood that home in at the last moment, circle an enemy and cover it (16, plus a bleed stack), charging your heal reserve. "
                    + "With 25 reserve, an orb that touches a hurt ally coats them in healing blood instead (30 health over 4 s)." },
            new WeaponDef { Id = WeaponId.VeinTether, Damage = 4f, Reach = 350f, Grow = 0.25f, ExtraDesc = "+25% beam length and +25% damage", Name = "Vein Tether", FireInterval = 0.08f,
                Desc = "A draining beam that hurts whatever you hold it on: keep it on your target. 40% of its damage heals, split between you and the nearest ally." },
            // DPS
            new WeaponDef { Id = WeaponId.TwinRifle, Speed = 820f, Life = 1f, Damage = 5f, ExtraDesc = "+1 barrel", Name = "Twin Rifle", FireInterval = 0.1f,
                Desc = "Twin streams (5 per barrel) angled in to cross at your cursor's distance: hits where they cross are crossfire, for double." },
            new WeaponDef { Id = WeaponId.Railgun, Speed = 1600f, Life = 0.7f, Damage = 45f, BotRange = 460f, ExtraDesc = "Throws 2 shrapnel shards sideways at every enemy it pierces", Name = "Railgun", FireInterval = 0.7f,
                Desc = "Slow, devastating shots that pierce 3 enemies. The round's path stays charged for 1 s behind it, hurting anything in it (25 a second)." },
            new WeaponDef { Id = WeaponId.BurstSmg, Speed = 760f, Life = 0.8f, Damage = 5f, BotRange = 280f, ExtraDesc = "+1 bullet per shot, with a wider spray", Name = "Burst SMG", FireInterval = 0.055f,
                Desc = "Sprays very fast shots. Keep firing to spin it up (2 s): the spray tightens and damage climbs to +60%. It winds down fast when you stop." },
            new WeaponDef { Id = WeaponId.Fireball, Speed = 430f, Life = 1.4f, Damage = 22f, BotRange = 390f, ExtraDesc = "+1 fireball, fanned out", Name = "Fireball", FireInterval = 0.5f,
                Desc = "A big, slow fireball that rolls on through every enemy in its path, hitting each (22) and setting it burning (8 a second for 3 s)." },
            new WeaponDef { Id = WeaponId.ArcLightning, Damage = 40f, BotRange = 320f, ExtraDesc = "+25% damage", Name = "Arc Lightning", FireInterval = 0.25f,
                Desc = "Hold to charge a strip of ground in front of you, longer the longer you hold (160 px to half the arena). Let go: lightning shocks everything in it (40, and a 0.4 s stun)." },
            new WeaponDef { Id = WeaponId.ArcaneMissiles, Speed = 520f, Life = 1f, Damage = 7f, ExtraDesc = "+1 missile a cast, +2 in orbit, and faster launches", Name = "Arcane Missiles", FireInterval = 0.3f,
                Desc = "Each cast conjures 3 missiles into orbit round you (up to 6). They launch at enemies within 300 px, homing, one after another. With the orbit full, a cast flies at your aim." },
            new WeaponDef { Id = WeaponId.WolfWhistle, Speed = 720f, Life = 0.8f, Damage = 6.5f, ExtraDesc = "+1 Wolf", Name = "Wolf Whistle", FireInterval = 0.3f,
                Desc = "Darts mark the enemy they hit as prey, and your two wolves hunt it. When every wolf bites it within a second: a takedown (20 per wolf and a 0.6 s stun).",
                Companion = MinionKind.Wolf, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.WispLantern, Damage = 22f, BotRange = 340f, ExtraDesc = "+1 Wisp, and each shot sends one more", Name = "Wisp Lantern", FireInterval = 0.25f,
                Desc = "Three wisps orbit you, and they are your shots: each time you fire, one flies at the enemy nearest your cursor (22), strikes it and comes back.",
                Companion = MinionKind.Wisp, CompanionCount = 3 },
            new WeaponDef { Id = WeaponId.ImpCaller, BotRange = 380f, ExtraDesc = "+1 Imp, and they arrive 50% faster", Name = "Imp Caller", FireInterval = 0.35f,
                Desc = "An imp arrives every 3 s (up to 3) and waits by you. Tap fire to send them all charging at your cursor to explode (45 each); hold to merge them into one big imp whose blast is bigger than theirs put together.",
                Companion = MinionKind.Imp, CompanionCount = 3, CompanionRespawn = 3f },
            new WeaponDef { Id = WeaponId.TwinBlades, Damage = 15f, Reach = 75f, Melee = true, ExtraDesc = "+1 cut: each swing strikes once more (60% damage)", Name = "Twin Blades", FireInterval = 0.16f,
                Desc = "Very fast alternating slashes. Land 10 in a row and your next one is a dash-strike: you lunge 140 px through enemies for triple damage." },
            new WeaponDef { Id = WeaponId.Greatsword, Damage = 60f, Reach = 125f, Melee = true, ExtraDesc = "+1 sword beam: each sweep launches a piercing crescent (40% damage)", Name = "Greatsword", FireInterval = 0.7f,
                Desc = "Huge sweeping cuts with long reach. Each sweep's edge hangs in the air for 0.5 s, a crescent of steel that stops enemy bullets." },
            new WeaponDef { Id = WeaponId.Rapier, Damage = 18f, Reach = 130f, Melee = true, ExtraDesc = "Skewers 1 more enemy behind the first", Name = "Rapier", FireInterval = 0.28f,
                Desc = "Precise thrusts at the nearest enemy in front. Each hit in a row on the same enemy deals 10% more (up to +60%). Each thrust skewers up to 3 enemy bullets along its line (not red aimed shots), and each adds to the streak." },
            new WeaponDef { Id = WeaponId.Shuriken, Speed = 700f, Life = 0.75f, Damage = 9f, ExtraDesc = "+1 star in the fan", Name = "Shuriken", FireInterval = 0.35f,
                Desc = "Fans of 3 spinning stars that stick in what they hit (for 5 s). Double-tap fire to recall them: each rips out for another hit and flies back to you, cutting through what it passes." },
            new WeaponDef { Id = WeaponId.Katana, Damage = 20f, Reach = 95f, Melee = true, ExtraDesc = "+1 cut: each slash strikes once more (60% damage)", Name = "Katana", FireInterval = 0.3f,
                Desc = "Fast 110° slashes. Hold off for 0.6 s and the blade is sheathed: your next cut is a quick-draw, 220 px long, through every enemy in its line for 2.5× damage." },
            new WeaponDef { Id = WeaponId.Kunai, Speed = 850f, Life = 0.75f, Damage = 8f, ExtraDesc = "+1 knife, fanned out (half damage)", Name = "Kunai", FireInterval = 0.12f,
                Desc = "Rapid throwing knives, +50% damage to enemies that aren't targeting you. Every 4th is a shadow kunai that leaves a mark where it lands: double-tap fire to blink there, and your next hit lands double." },
            new WeaponDef { Id = WeaponId.BloodBolts, Speed = 800f, Life = 0.8f, Damage = 9f, ExtraDesc = "+1 bolt, fanned out", Name = "Blood Bolts", FireInterval = 0.13f,
                Desc = "Fast bolts of blood (9) that stick in enemies: the fifth within 2 s makes it hemorrhage, bursting for 36 to it and every enemy within 80 px." },
            new WeaponDef { Id = WeaponId.Implosion, Damage = 34f, Reach = 480f, ExtraDesc = "+1 implosion: another enemy near your target collapses too (60% damage)", Name = "Implosion", FireInterval = 0.45f,
                Desc = "Click an enemy: its blood collapses inward (34), hurting enemies pressed against it (15) and pulling them in." },
            new WeaponDef { Id = WeaponId.BoneSpear, Speed = 1000f, Life = 0.9f, Damage = 60f, ExtraDesc = "+1 spear, fanned out (costs no more health)", Name = "Bone Spear", FireInterval = 0.8f,
                Desc = "A heavy spear (60) that pierces 3 enemies. Each throw costs 2 health; the spear stays stuck where it stops (8 s), and walking over it gives the 2 back." },
        };

        public static WeaponDef Weapon(Role role, Kit kit, int index) => Weapons[((int)role * KitCount + (int)kit) * WeaponsPerRole + index];

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

        public static string KitName(Kit k) => k == Kit.BloodMage ? "Hemomancer" : k == Kit.Mage ? "Elementalist" : k.ToString();

        /// Mages and summoners trade some health for their spells and minions; knights wear armour to get close;
        /// ninjas are quick and lightly protected; blood mages keep their full health to spend.
        public static float KitHpMult(Kit k) => k == Kit.Mage ? 0.85f : k == Kit.Summoner ? 0.9f : k == Kit.Knight ? 1.15f : k == Kit.Ninja ? 0.85f : 1f;

        /// Ninjas move 12% faster.
        public static float KitSpeedMult(Kit k) => k == Kit.Ninja ? 1.12f : 1f;

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
            CrescentShot = 52; // the Eclipse's crescent blade (an enemy kind: 10-19 were all taken)

        public static bool IsEnemyShot(byte kind) => kind >= 10 && kind < 20 || kind == CrescentShot;

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
                case BileShot: return new Rgba(0.75f, 0.9f, 0.31f);  // the Devourer's vomit
                case ToothShot: return new Rgba(0.96f, 0.9f, 0.82f);  // the Devourer's Jaws
                default: return new Rgba(1f, 0.3f, 0.45f);
            }
        }
    }
}
