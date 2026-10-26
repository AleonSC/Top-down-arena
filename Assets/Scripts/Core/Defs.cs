namespace Arena
{
    public enum Role : byte { Tank = 0, Healer = 1, Dps = 2 }

    public enum EnemyType : byte
    {
        Grunt, Burster, Sniper, Spinner, Boss,
        Charger, Splitter, Shard, Mortar, Shielder, Medic,
        HiveQueen, TwinA, TwinB, Singularity,
    }

    public class RoleDef
    {
        public string Name, Ability, Desc;
        public Rgba Color;
        public float Hp, Speed, HitR, BodyR, FireInterval, AbilityCd;
    }

    /// Soldier is the original gun-based kit; Mage and Summoner are the alternatives for each role.
    public enum Kit : byte { Soldier, Mage, Summoner }

    public enum WeaponId : byte
    {
        // Soldier
        Scattergun, FlakCannon, RiotRepeater, LightBolt, TwinWisps, MendersBow, TwinRifle, Railgun, BurstSmg,
        // Mage
        FlameBurst, FrostLance, StormHammer, RadiantOrb, ChainLight, PrismBeam, Fireball, ArcLightning, ArcaneMissiles,
        // Summoner
        GolemStaff, BoneGuard, BearTotem, SpriteStaff, TreantRod, TotemStaff, WolfWhistle, WispLantern, ImpCaller,
    }

    public class WeaponDef
    {
        public WeaponId Id;
        public string Name, Desc;
        public float FireInterval;
        // Summoner weapons keep companions around. Imps blow themselves up, so a new one
        // arrives every CompanionRespawn seconds; the others only die to Sacrifice.
        public MinionKind Companion = MinionKind.None;
        public int CompanionCount;
        public float CompanionRespawn = 3f;
    }

    public class EnemyDef
    {
        public float Hp, Radius, Speed;
        public int Score, Sides, Xp;
        public string Name;
        public bool IsBoss;
        public Rgba Color;
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
            new EnemyDef { Name = "Reaper of Dusk", IsBoss = true, Hp = 1500f, Radius = 34f, Speed = 70f, Score = 250, Xp = 100, Sides = 5, Color = new Rgba(1f, 0.35f, 0.6f) },
            new EnemyDef { Name = "Reaper of Dawn", IsBoss = true, Hp = 1500f, Radius = 34f, Speed = 70f, Score = 250, Xp = 100, Sides = 5, Color = new Rgba(0.45f, 0.55f, 1f) },
            new EnemyDef { Name = "The Singularity", IsBoss = true, Hp = 3600f, Radius = 40f, Speed = 0f, Score = 500, Xp = 220, Sides = 10, Color = new Rgba(0.6f, 0.35f, 1f) },
        };

        public const int WeaponsPerRole = 3;

        // Three starting weapons per role, in the same order as Role.
        // Three weapons per role and kit, ordered Role, then Kit (Soldier, Mage, Summoner).
        public static readonly WeaponDef[] Weapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Scattergun, Name = "Scattergun", FireInterval = 0.45f, Desc = "Short-range spread of 5 pellets." },
            new WeaponDef { Id = WeaponId.FlakCannon, Name = "Flak Cannon", FireInterval = 0.8f, Desc = "Slow, heavy shells that explode on impact." },
            new WeaponDef { Id = WeaponId.RiotRepeater, Name = "Riot Repeater", FireInterval = 0.18f, Desc = "Steady single shots with a little spread." },
            new WeaponDef { Id = WeaponId.FlameBurst, Name = "Flame Burst", FireInterval = 0.4f, Desc = "A short-range cone of fire." },
            new WeaponDef { Id = WeaponId.FrostLance, Name = "Frost Lance", FireInterval = 0.35f, Desc = "Piercing ice shards that slow enemies." },
            new WeaponDef { Id = WeaponId.StormHammer, Name = "Storm Hammer", FireInterval = 0.7f, Desc = "A heavy orb whose lightning chains to 3 nearby enemies." },
            new WeaponDef { Id = WeaponId.GolemStaff, Name = "Golem Staff", FireInterval = 0.5f, Desc = "Hurls rocks. A stone Golem fights beside you and blocks bullets.",
                Companion = MinionKind.Golem, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.BoneGuard, Name = "Bone Guard", FireInterval = 0.3f, Desc = "Bone darts. Two skeleton guards circle you and block bullets.",
                Companion = MinionKind.Guard, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.BearTotem, Name = "Bear Totem", FireInterval = 0.35f, Desc = "Wide claw swipes. A Bear companion mauls nearby enemies.",
                Companion = MinionKind.Bear, CompanionCount = 1 },
            // Healer
            new WeaponDef { Id = WeaponId.LightBolt, Name = "Light Bolt", FireInterval = 0.25f, Desc = "Accurate long-range bolts." },
            new WeaponDef { Id = WeaponId.TwinWisps, Name = "Twin Wisps", FireInterval = 0.35f, Desc = "Two curving shots that cross in front of you." },
            new WeaponDef { Id = WeaponId.MendersBow, Name = "Mender's Bow", FireInterval = 0.3f, Desc = "Bolts heal your ally when they pass through them." },
            new WeaponDef { Id = WeaponId.RadiantOrb, Name = "Radiant Orb", FireInterval = 0.5f, Desc = "Slow piercing orbs that heal allies they pass through." },
            new WeaponDef { Id = WeaponId.ChainLight, Name = "Chain Light", FireInterval = 0.3f, Desc = "Bolts of light that chain to 2 more enemies." },
            new WeaponDef { Id = WeaponId.PrismBeam, Name = "Prism Beam", FireInterval = 0.06f, Desc = "A rapid short-range beam. Grazing your ally heals them." },
            new WeaponDef { Id = WeaponId.SpriteStaff, Name = "Sprite Staff", FireInterval = 0.35f, Desc = "Seed shots. Two sprites fly to whoever is hurt and heal them.",
                Companion = MinionKind.Sprite, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.TreantRod, Name = "Treant Rod", FireInterval = 0.3f, Desc = "Bolts heal allies they pass. A Treant guards you and blocks bullets.",
                Companion = MinionKind.Treant, CompanionCount = 1 },
            new WeaponDef { Id = WeaponId.TotemStaff, Name = "Totem Staff", FireInterval = 0.35f, Desc = "Seed shots. Plants a healing totem where you stand every 10 s." },
            // DPS
            new WeaponDef { Id = WeaponId.TwinRifle, Name = "Twin Rifle", FireInterval = 0.1f, Desc = "Fast parallel twin stream." },
            new WeaponDef { Id = WeaponId.Railgun, Name = "Railgun", FireInterval = 0.7f, Desc = "Slow, devastating shots that pierce 3 enemies." },
            new WeaponDef { Id = WeaponId.BurstSmg, Name = "Burst SMG", FireInterval = 0.055f, Desc = "Sprays very fast, inaccurate shots." },
            new WeaponDef { Id = WeaponId.Fireball, Name = "Fireball", FireInterval = 0.45f, Desc = "Fireballs that explode on impact." },
            new WeaponDef { Id = WeaponId.ArcLightning, Name = "Arc Lightning", FireInterval = 0.25f, Desc = "Bolts that chain to 4 nearby enemies." },
            new WeaponDef { Id = WeaponId.ArcaneMissiles, Name = "Arcane Missiles", FireInterval = 0.3f, Desc = "Three curving missiles per cast." },
            new WeaponDef { Id = WeaponId.WolfWhistle, Name = "Wolf Whistle", FireInterval = 0.3f, Desc = "Darts. Two wolves hunt down enemies.",
                Companion = MinionKind.Wolf, CompanionCount = 2 },
            new WeaponDef { Id = WeaponId.WispLantern, Name = "Wisp Lantern", FireInterval = 0.45f, Desc = "Weak bolts. Three wisps orbit you and shoot.",
                Companion = MinionKind.Wisp, CompanionCount = 3 },
            new WeaponDef { Id = WeaponId.ImpCaller, Name = "Imp Caller", FireInterval = 0.35f, Desc = "Darts. Calls an imp every 3 s that charges an enemy and explodes.",
                Companion = MinionKind.Imp, CompanionCount = 3, CompanionRespawn = 3f },
        };

        public static WeaponDef Weapon(Role role, Kit kit, int index) => Weapons[((int)role * 3 + (int)kit) * WeaponsPerRole + index];

        public static string KitName(Kit k) => k.ToString();

        /// Mages and summoners trade some health for their spells and minions.
        public static float KitHpMult(Kit k) => k == Kit.Mage ? 0.85f : k == Kit.Summoner ? 0.9f : 1f;

        /// XP needed to go from this level to the next.
        public static float XpToNext(int level) => 50f + 30f * (level - 1);

        public static RoleDef Get(Role r) => Roles[(int)r];
        public static EnemyDef Get(EnemyType t) => Enemies[(int)t];

        // Bullet kinds. Kinds >= SmallShot belong to enemies.
        public const byte TankShot = 0, HealerShot = 1, DpsShot = 2, TurretShot = 3, NovaShot = 4, FlakShell = 5, RailShot = 6, WispShot = 7, GrenadeShot = 8, SwarmShot = 9, SmallShot = 10, BigShot = 11, FastShot = 12, MineShot = 13,
            // more player kinds (enemy kinds are 10-19)
            FireShot = 20, FrostShot = 21, ArcaneShot = 22, HolyShot = 23, BoltShot = 24, SeedShot = 25, BoneShot = 26, RockShot = 27, MinionShot = 28;

        public static bool IsEnemyShot(byte kind) => kind >= 10 && kind < 20;

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
                case WispShot: return 5f;
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
                case BigShot: return 10f;
                case MineShot: return 9f;
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
                case BigShot: return new Rgba(0.85f, 0.35f, 1f);
                case MineShot: return new Rgba(1f, 0.55f, 0.2f);
                case FastShot: return new Rgba(1f, 0.95f, 0.4f);
                default: return new Rgba(1f, 0.3f, 0.45f);
            }
        }
    }
}
