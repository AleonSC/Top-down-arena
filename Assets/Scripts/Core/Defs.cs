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
    }

    public class RoleDef
    {
        public string Name, Ability, Desc;
        public Rgba Color;
        public float Hp, Speed, HitR, BodyR, FireInterval, AbilityCd;
    }

    /// Soldier is the original gun-based kit; Mage and Summoner are the alternatives for each role.
    public enum Kit : byte { Soldier, Mage, Summoner, Knight }

    public enum WeaponId : byte
    {
        // Soldier
        Scattergun, FlakCannon, RiotRepeater, LightBolt, Pistol, MendersBow, TwinRifle, Railgun, BurstSmg,
        // Mage
        FlameBurst, FrostLance, StormHammer, RadiantOrb, ChainLight, PrismBeam, Fireball, ArcLightning, ArcaneMissiles,
        // Summoner
        GolemStaff, BoneGuard, BearTotem, SpriteStaff, TreantRod, TotemStaff, WolfWhistle, WispLantern, ImpCaller,
        // Knight (melee)
        Warhammer, SwordShield, Halberd, HolyMace, Lightbringer, Censer, TwinBlades, Greatsword, Rapier,
    }

    public class WeaponDef
    {
        public WeaponId Id;
        public string Name, Desc;
        public string ExtraDesc;  // what each extra projectile (Multishot, Iron Volley, Bullet Storm) does for this weapon
        public float FireInterval;
        // Summoner weapons keep companions around. Imps blow themselves up, so a new one
        // arrives every CompanionRespawn seconds; the others only die to Sacrifice.
        public MinionKind Companion = MinionKind.None;
        public int CompanionCount;
        public float CompanionRespawn = 3f;
        public bool Melee;  // a Knight weapon: swings instead of shots (Knight.cs)
    }

    public class EnemyDef
    {
        public float Hp, Radius, Speed;
        public int Score, Sides, Xp;
        public string Name;
        public bool IsBoss;
        public Rgba Color;
        public float Contact;  // melee: damage when it touches you (0 = the default contact damage)
        public bool Melee;
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
                Desc = "Your aura slowly heals an ally standing near you.",
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
            new EnemyDef { Name = "Grunt", Hp = 60f, Radius = 14f, Speed = 85f, Score = 10, Xp = 10, Sides = 3, Color = new Rgba(1f, 0.35f, 0.35f) },
            new EnemyDef { Name = "Burster", Hp = 110f, Radius = 17f, Speed = 55f, Score = 20, Xp = 18, Sides = 6, Color = new Rgba(0.8f, 0.4f, 1f) },
            new EnemyDef { Name = "Sniper", Hp = 50f, Radius = 12f, Speed = 95f, Score = 15, Xp = 14, Sides = 4, Color = new Rgba(1f, 0.9f, 0.3f) },
            new EnemyDef { Name = "Spinner", Hp = 150f, Radius = 18f, Speed = 35f, Score = 25, Xp = 22, Sides = 5, Color = new Rgba(1f, 0.6f, 0.2f) },
            new EnemyDef { Name = "The Warden", IsBoss = true, Hp = 2400f, Radius = 46f, Speed = 45f, Score = 300, Xp = 150, Sides = 8, Color = new Rgba(1f, 0.25f, 0.5f) },
            new EnemyDef { Name = "Charger", Hp = 90f, Radius = 15f, Speed = 70f, Score = 20, Xp = 18, Sides = 3, Color = new Rgba(0.3f, 0.9f, 0.9f) },
            new EnemyDef { Name = "Splitter", Hp = 120f, Radius = 19f, Speed = 50f, Score = 20, Xp = 16, Sides = 4, Color = new Rgba(0.6f, 1f, 0.3f) },
            new EnemyDef { Name = "Shard", Hp = 25f, Radius = 8f, Speed = 130f, Score = 5, Xp = 4, Sides = 3, Color = new Rgba(0.8f, 1f, 0.55f) },
            new EnemyDef { Name = "Mortar", Hp = 80f, Radius = 16f, Speed = 40f, Score = 20, Xp = 18, Sides = 7, Color = new Rgba(0.95f, 0.55f, 0.3f) },
            new EnemyDef { Name = "Shielder", Hp = 140f, Radius = 17f, Speed = 60f, Score = 25, Xp = 22, Sides = 6, Color = new Rgba(0.6f, 0.7f, 0.9f) },
            new EnemyDef { Name = "Medic", Hp = 70f, Radius = 13f, Speed = 75f, Score = 25, Xp = 20, Sides = 4, Color = new Rgba(1f, 0.5f, 0.75f) },
            new EnemyDef { Name = "Hive Queen", IsBoss = true, Hp = 3000f, Radius = 44f, Speed = 40f, Score = 400, Xp = 180, Sides = 6, Color = new Rgba(1f, 0.8f, 0.2f) },
            new EnemyDef { Name = "Reaper of Dusk", IsBoss = true, Hp = 1800f, Radius = 34f, Speed = 95f, Score = 250, Xp = 100, Sides = 5, Color = new Rgba(1f, 0.35f, 0.6f) },
            new EnemyDef { Name = "Reaper of Dawn", IsBoss = true, Hp = 1800f, Radius = 34f, Speed = 95f, Score = 250, Xp = 100, Sides = 5, Color = new Rgba(0.45f, 0.55f, 1f) },
            new EnemyDef { Name = "The Singularity", IsBoss = true, Hp = 3600f, Radius = 40f, Speed = 0f, Score = 500, Xp = 220, Sides = 10, Color = new Rgba(0.6f, 0.35f, 1f) },
            new EnemyDef { Name = "Phantom", Hp = 90f, Radius = 13f, Speed = 90f, Score = 25, Xp = 22, Sides = 4, Color = new Rgba(0.55f, 0.85f, 1f) },
            new EnemyDef { Name = "Hexer", Hp = 100f, Radius = 14f, Speed = 60f, Score = 25, Xp = 22, Sides = 5, Color = new Rgba(0.75f, 0.3f, 0.9f) },
            new EnemyDef { Name = "Juggernaut", Hp = 420f, Radius = 26f, Speed = 32f, Score = 60, Xp = 50, Sides = 8, Color = new Rgba(0.55f, 0.55f, 0.62f) },
            new EnemyDef { Name = "Nest", Hp = 260f, Radius = 22f, Speed = 0f, Score = 40, Xp = 35, Sides = 6, Color = new Rgba(0.7f, 0.9f, 0.4f) },
            new EnemyDef { Name = "The Colossus", IsBoss = true, Hp = 6800f, Radius = 54f, Speed = 35f, Score = 600, Xp = 260, Sides = 6, Color = new Rgba(0.78f, 0.62f, 0.45f) },
            new EnemyDef { Name = "The Duelist", IsBoss = true, Hp = 6000f, Radius = 30f, Speed = 80f, Score = 650, Xp = 280, Sides = 4, Color = new Rgba(0.85f, 0.85f, 1f) },
            new EnemyDef { Name = "The Duelist", Hp = 70f, Radius = 30f, Speed = 0f, Score = 0, Xp = 0, Sides = 4, Color = new Rgba(0.85f, 0.85f, 1f) },  // decoy
            new EnemyDef { Name = "The Lich", IsBoss = true, Hp = 5000f, Radius = 36f, Speed = 45f, Score = 700, Xp = 300, Sides = 7, Color = new Rgba(0.5f, 1f, 0.75f) },
            new EnemyDef { Name = "Phylactery", Hp = 600f, Radius = 18f, Speed = 0f, Score = 50, Xp = 0, Sides = 4, Color = new Rgba(0.4f, 1f, 0.6f) },
            new EnemyDef { Name = "Storm Herald", IsBoss = true, Hp = 6000f, Radius = 40f, Speed = 55f, Score = 800, Xp = 320, Sides = 9, Color = new Rgba(0.55f, 0.75f, 1f) },
            new EnemyDef { Name = "The Devourer", IsBoss = true, Hp = 6500f, Radius = 50f, Speed = 40f, Score = 900, Xp = 340, Sides = 12, Color = new Rgba(0.85f, 0.28f, 0.38f) },
            new EnemyDef { Name = "The Eclipse", IsBoss = true, Hp = 9000f, Radius = 52f, Speed = 30f, Score = 2000, Xp = 500, Sides = 16, Color = new Rgba(1f, 0.85f, 0.4f) },
            new EnemyDef { Name = "Gravity Well", Hp = 260f, Radius = 15f, Speed = 160f, Score = 40, Xp = 15, Sides = 6, Color = new Rgba(0.7f, 0.45f, 1f) },
            // Melee
            new EnemyDef { Name = "Swarmer", Hp = 22f, Radius = 9f, Speed = 135f, Score = 5, Xp = 5, Sides = 3, Color = new Rgba(1f, 0.6f, 0.3f), Melee = true, Contact = 10f },
            new EnemyDef { Name = "Brute", Hp = 110f, Radius = 19f, Speed = 62f, Score = 15, Xp = 14, Sides = 7, Color = new Rgba(0.8f, 0.45f, 0.35f), Melee = true, Contact = 24f },
            new EnemyDef { Name = "Lunger", Hp = 45f, Radius = 12f, Speed = 95f, Score = 12, Xp = 12, Sides = 4, Color = new Rgba(0.95f, 0.35f, 0.65f), Melee = true, Contact = 18f },
            new EnemyDef { Name = "Hive Bomber", Hp = 30f, Radius = 10f, Speed = 330f, Score = 2, Xp = 0, Sides = 3, Color = new Rgba(1f, 0.45f, 0.15f), Melee = true },
            new EnemyDef { Name = "Cage Well", Hp = 40f, Radius = 11f, Speed = 0f, Score = 5, Xp = 2, Sides = 6, Color = new Rgba(0.85f, 0.4f, 1f), Melee = true },
        };

        public const int WeaponsPerRole = 3;

        public const int KitCount = 4;

        // Three weapons per role and kit, ordered Role, then Kit (Soldier, Mage, Summoner, Knight).
        public static readonly WeaponDef[] Weapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Scattergun, ExtraDesc = "+2 pellets and a 15% tighter cone", Name = "Scattergun", FireInterval = 0.45f, Desc = "Short-range spread of 5 pellets." },
            new WeaponDef { Id = WeaponId.FlakCannon, ExtraDesc = "Shells burst into 2 more bomblets on impact", Name = "Flak Cannon", FireInterval = 0.8f, Desc = "Slow, heavy shells that explode on impact." },
            new WeaponDef { Id = WeaponId.RiotRepeater, ExtraDesc = "+1 round per burst (each trigger pull fires a burst, then a short pause)", Name = "Riot Repeater", FireInterval = 0.18f, Desc = "Steady single shots with a little spread." },
            new WeaponDef { Id = WeaponId.FlameBurst, ExtraDesc = "+2 flames and 25% more reach", Name = "Flame Burst", FireInterval = 0.4f, Desc = "A short-range cone of fire." },
            new WeaponDef { Id = WeaponId.FrostLance, ExtraDesc = "Shards shatter into 2 more icicles on hit", Name = "Frost Lance", FireInterval = 0.35f, Desc = "Piercing ice shards that slow enemies." },
            new WeaponDef { Id = WeaponId.StormHammer, ExtraDesc = "Lightning chains to 1 more enemy", Name = "Storm Hammer", FireInterval = 0.7f, Desc = "A heavy orb whose lightning chains to 3 nearby enemies." },
            new WeaponDef { Id = WeaponId.GolemStaff, ExtraDesc = "+1 Golem (up to 3)", Name = "Golem Staff", FireInterval = 0.5f, Desc = "Hurls rocks. A stone Golem fights beside you and blocks bullets.",
                Companion = MinionKind.Golem, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.BoneGuard, ExtraDesc = "+1 Skeleton Guard", Name = "Bone Guard", FireInterval = 0.3f, Desc = "Bone darts. Two skeleton guards circle you and block bullets.",
                Companion = MinionKind.Guard, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.BearTotem, ExtraDesc = "+2 claws, wider swipes, and your Bear's bites hit enemies around its target", Name = "Bear Totem", FireInterval = 0.35f, Desc = "Wide claw swipes. A Bear companion mauls nearby enemies.",
                Companion = MinionKind.Bear, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.Warhammer, Melee = true, ExtraDesc = "+1 aftershock: a second smash 0.2 s later with 60% damage and more reach", Name = "Warhammer", FireInterval = 0.75f,
                Desc = "Slow overhead smashes in a wide arc that knock enemies back." },
            new WeaponDef { Id = WeaponId.SwordShield, Melee = true, ExtraDesc = "Shield bashes come 1 swing sooner (every swing at 2+), and knock back further", Name = "Sword & Shield", FireInterval = 0.32f,
                Desc = "Quick sword cuts. Every third swing is a shield bash that knocks enemies back and blocks every bullet in front of you." },
            new WeaponDef { Id = WeaponId.Halberd, Melee = true, ExtraDesc = "+30 px reach and a quick follow-up thrust (60% damage)", Name = "Halberd", FireInterval = 0.55f,
                Desc = "Long-reaching thrusts that hit everything along the blade." },
            // Healer
            new WeaponDef { Id = WeaponId.LightBolt, ExtraDesc = "+1 homing bolt with every shot", Name = "Light Bolt", FireInterval = 0.25f, Desc = "Accurate long-range bolts." },
            new WeaponDef { Id = WeaponId.Pistol, ExtraDesc = "+2 rounds in the cylinder, loaded as heavy rounds (double damage)", Name = "Revolver", FireInterval = 0.45f, Desc = "Tap for aimed shots; hold to fan the hammer and empty the cylinder in a 10° arc. Six rounds, the last one heavy." },
            new WeaponDef { Id = WeaponId.MendersBow, ExtraDesc = "+1 arrow nocked: a full draw also looses an extra arrow (half damage) in a tight fan", Name = "Mender's Bow", FireInterval = 0.9f, Desc = "Hold to draw, release to fire. A full draw (0.9 s) hits for 40, pierces 2 and heals an ally it passes for 20; let go right at full draw for a perfect shot (+25%)." },
            new WeaponDef { Id = WeaponId.RadiantOrb, ExtraDesc = "+1 light orb circling you that hurts enemies and heals allies it touches", Name = "Radiant Orb", FireInterval = 0.5f, Desc = "Slow piercing orbs that heal allies they pass through." },
            new WeaponDef { Id = WeaponId.ChainLight, ExtraDesc = "Bolts chain to 1 more enemy", Name = "Chain Light", FireInterval = 0.3f, Desc = "Bolts of light that chain to 2 more enemies." },
            new WeaponDef { Id = WeaponId.PrismBeam, ExtraDesc = "+30% beam length, +25% damage and +50% healing", Name = "Prism Beam", FireInterval = 0.06f, Desc = "A rapid short-range beam. Grazing your ally heals them." },
            new WeaponDef { Id = WeaponId.SpriteStaff, ExtraDesc = "+1 Sprite", Name = "Sprite Staff", FireInterval = 0.35f, Desc = "Seed shots. Two sprites fly to whoever is hurt and heal them.",
                Companion = MinionKind.Sprite, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.TreantRod, ExtraDesc = "Your Treant grows 30% bigger: blocks more, hits harder and reaches farther", Name = "Treant Rod", FireInterval = 0.3f, Desc = "Bolts heal allies they pass. A Treant guards you and blocks bullets.",
                Companion = MinionKind.Treant, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.TotemStaff, ExtraDesc = "Plants totems 60% more often", Name = "Totem Staff", FireInterval = 0.35f, Desc = "Seed shots. Plants a healing totem where you stand every 10 s." },
            new WeaponDef { Id = WeaponId.HolyMace, Melee = true, ExtraDesc = "+1 HP healed per enemy hit, and the healing reaches 60 px further", Name = "Holy Mace", FireInterval = 0.45f,
                Desc = "Mace swings. Every enemy you hit heals you and allies near you for 2 (up to 8 a swing)." },
            new WeaponDef { Id = WeaponId.Lightbringer, Melee = true, ExtraDesc = "+1 crescent of light, fanned out", Name = "Lightbringer", FireInterval = 0.5f,
                Desc = "Sword swings that also send a short crescent of light, which heals allies it passes." },
            new WeaponDef { Id = WeaponId.Censer, Melee = true, ExtraDesc = "+20 px reach and +1 HP healed per spin", Name = "Censer", FireInterval = 0.4f,
                Desc = "A flail whirled all the way around you. Each spin heals allies near you for 3." },
            // DPS
            new WeaponDef { Id = WeaponId.TwinRifle, ExtraDesc = "+1 barrel", Name = "Twin Rifle", FireInterval = 0.1f, Desc = "Fast parallel twin stream." },
            new WeaponDef { Id = WeaponId.Railgun, ExtraDesc = "Throws 2 shrapnel shards sideways at every enemy it pierces", Name = "Railgun", FireInterval = 0.7f, Desc = "Slow, devastating shots that pierce 3 enemies." },
            new WeaponDef { Id = WeaponId.BurstSmg, ExtraDesc = "+1 bullet per shot, with a wider spray", Name = "Burst SMG", FireInterval = 0.055f, Desc = "Sprays very fast, inaccurate shots." },
            new WeaponDef { Id = WeaponId.Fireball, ExtraDesc = "Splits into 2 small fireballs on impact", Name = "Fireball", FireInterval = 0.45f, Desc = "Fireballs that explode on impact." },
            new WeaponDef { Id = WeaponId.ArcLightning, ExtraDesc = "Bolts chain to 1 more enemy", Name = "Arc Lightning", FireInterval = 0.25f, Desc = "Bolts that chain to 4 nearby enemies." },
            new WeaponDef { Id = WeaponId.ArcaneMissiles, ExtraDesc = "+1 missile, and missiles home in on enemies", Name = "Arcane Missiles", FireInterval = 0.3f, Desc = "Three curving missiles per cast." },
            new WeaponDef { Id = WeaponId.WolfWhistle, ExtraDesc = "+1 Wolf", Name = "Wolf Whistle", FireInterval = 0.3f, Desc = "Darts. Two wolves hunt down enemies.",
                Companion = MinionKind.Wolf, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.WispLantern, ExtraDesc = "+1 Wisp", Name = "Wisp Lantern", FireInterval = 0.45f, Desc = "Weak bolts. Three wisps orbit you and shoot.",
                Companion = MinionKind.Wisp, CompanionCount = 3 },
            new WeaponDef { Id = WeaponId.ImpCaller, ExtraDesc = "+1 Imp, and they arrive 50% faster", Name = "Imp Caller", FireInterval = 0.35f, Desc = "Darts. Calls an imp every 3 s that charges an enemy and explodes.",
                Companion = MinionKind.Imp, CompanionCount = 3, CompanionRespawn = 3f },
            new WeaponDef { Id = WeaponId.TwinBlades, Melee = true, ExtraDesc = "+1 cut: each swing strikes once more (60% damage)", Name = "Twin Blades", FireInterval = 0.16f,
                Desc = "Very fast alternating slashes." },
            new WeaponDef { Id = WeaponId.Greatsword, Melee = true, ExtraDesc = "+1 sword beam: each sweep launches a piercing crescent (40% damage)", Name = "Greatsword", FireInterval = 0.7f,
                Desc = "Huge sweeping cuts with long reach." },
            new WeaponDef { Id = WeaponId.Rapier, Melee = true, ExtraDesc = "Skewers 1 more enemy behind the first", Name = "Rapier", FireInterval = 0.28f,
                Desc = "Precise thrusts at the nearest enemy in front. Each hit in a row on the same enemy deals 10% more (up to +60%)." },
        };

        public static WeaponDef Weapon(Role role, Kit kit, int index) => Weapons[((int)role * KitCount + (int)kit) * WeaponsPerRole + index];

        public static string KitName(Kit k) => k.ToString();

        /// Mages and summoners trade some health for their spells and minions; knights wear armour to get close.
        public static float KitHpMult(Kit k) => k == Kit.Mage ? 0.85f : k == Kit.Summoner ? 0.9f : k == Kit.Knight ? 1.15f : 1f;

        /// XP needed to go from this level to the next.
        public static float XpToNext(int level) => 50f + 30f * (level - 1);

        public static RoleDef Get(Role r) => Roles[(int)r];
        public static EnemyDef Get(EnemyType t) => Enemies[(int)t];

        // Bullet kinds. Kinds >= SmallShot belong to enemies.
        public const byte TankShot = 0, HealerShot = 1, DpsShot = 2, TurretShot = 3, NovaShot = 4, FlakShell = 5, RailShot = 6, WispShot = 7, GrenadeShot = 8, SwarmShot = 9, SmallShot = 10, BigShot = 11, FastShot = 12, MineShot = 13, PierceShot = 14,
            // more player kinds (enemy kinds are 10-19)
            FireShot = 20, FrostShot = 21, ArcaneShot = 22, HolyShot = 23, BoltShot = 24, SeedShot = 25, BoneShot = 26, RockShot = 27, MinionShot = 28,
            KnightSlash = 29,  // never flies: stands in for a melee hit so weapon mods and legendaries apply to it
            KnightWave = 30,   // a crescent of steel or light thrown by a swing
            ArrowShot = 31;    // a Mender's Bow arrow

        public static bool IsEnemyShot(byte kind) => kind >= 10 && kind < 20;

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
                case ArcaneShot: return 5f;
                case HolyShot: return 9f;
                case BoltShot: return 4f;
                case SeedShot: return 4f;
                case BoneShot: return 3.5f;
                case RockShot: return 6f;
                case MinionShot: return 3.5f;
                case KnightWave: return 11f;
                case ArrowShot: return 4.5f;
                case BigShot: return 10f;
                case MineShot: return 9f;
                case PierceShot: return 6.5f;
                case FastShot: return 4.5f;
                default: return 6f;
            }
        }

        public static float BulletDamage(byte kind)
        {
            switch (kind)
            {
                case BigShot: return 20f;
                case MineShot: return 18f;
                case PierceShot: return 16f;
                case FastShot: return 15f;
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
                case FrostShot: return new Rgba(0.6f, 0.9f, 1f);
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
                case BigShot: return new Rgba(0.85f, 0.35f, 1f);
                case MineShot: return new Rgba(1f, 0.55f, 0.2f);
                case PierceShot: return new Rgba(1f, 0.12f, 0.12f);
                case FastShot: return new Rgba(1f, 0.95f, 0.4f);
                default: return new Rgba(1f, 0.3f, 0.45f);
            }
        }
    }
}
