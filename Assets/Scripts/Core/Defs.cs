namespace Arena
{
    public enum Role : byte { Tank = 0, Healer = 1, Dps = 2 }

    public enum EnemyType : byte { Grunt = 0, Burster = 1, Sniper = 2, Spinner = 3, Boss = 4 }

    public class RoleDef
    {
        public string Name, Ability, Desc;
        public Rgba Color;
        public float Hp, Speed, HitR, BodyR, FireInterval, AbilityCd;
    }

    public enum WeaponId : byte { Scattergun, FlakCannon, RiotRepeater, LightBolt, TwinWisps, MendersBow, TwinRifle, Railgun, BurstSmg }

    public class WeaponDef
    {
        public WeaponId Id;
        public string Name, Desc;
        public float FireInterval;
    }

    public class EnemyDef
    {
        public float Hp, Radius, Speed;
        public int Score, Sides, Xp;
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
                Desc = "Big HP pool and a short-range shotgun. Enemies prefer to target you.\n" +
                       "Ability: a 4 s barrier bubble that deletes enemy bullets. Keep your ally inside it.",
            },
            new RoleDef
            {
                Name = "Healer", Ability = "Mend Pulse", Color = new Rgba(0.35f, 1f, 0.55f),
                Hp = 120f, Speed = 215f, HitR = 7f, BodyR = 14f, FireInterval = 0.25f, AbilityCd = 10f,
                Desc = "Your aura slowly heals an ally standing near you.\n" +
                       "Ability: heal everyone nearby for 60 HP and instantly revive downed allies in range.",
            },
            new RoleDef
            {
                Name = "DPS", Ability = "Dash", Color = new Rgba(1f, 0.55f, 0.25f),
                Hp = 95f, Speed = 240f, HitR = 6f, BodyR = 12f, FireInterval = 0.1f, AbilityCd = 3.5f,
                Desc = "Fragile, but shreds enemies with a twin-stream rifle.\n" +
                       "Ability: a quick dash that makes you briefly invulnerable.",
            },
        };

        public static readonly EnemyDef[] Enemies =
        {
            new EnemyDef { Hp = 60f, Radius = 14f, Speed = 85f, Score = 10, Xp = 10, Sides = 3, Color = new Rgba(1f, 0.35f, 0.35f) },
            new EnemyDef { Hp = 110f, Radius = 17f, Speed = 55f, Score = 20, Xp = 18, Sides = 6, Color = new Rgba(0.8f, 0.4f, 1f) },
            new EnemyDef { Hp = 50f, Radius = 12f, Speed = 95f, Score = 15, Xp = 14, Sides = 4, Color = new Rgba(1f, 0.9f, 0.3f) },
            new EnemyDef { Hp = 150f, Radius = 18f, Speed = 35f, Score = 25, Xp = 22, Sides = 5, Color = new Rgba(1f, 0.6f, 0.2f) },
            new EnemyDef { Hp = 2400f, Radius = 46f, Speed = 45f, Score = 300, Xp = 150, Sides = 8, Color = new Rgba(1f, 0.25f, 0.5f) },
        };

        public const int WeaponsPerRole = 3;

        // Three starting weapons per role, in the same order as Role.
        public static readonly WeaponDef[] Weapons =
        {
            new WeaponDef { Id = WeaponId.Scattergun, Name = "Scattergun", FireInterval = 0.45f, Desc = "Short-range spread of 5 pellets." },
            new WeaponDef { Id = WeaponId.FlakCannon, Name = "Flak Cannon", FireInterval = 0.8f, Desc = "Slow, heavy shells that explode on impact." },
            new WeaponDef { Id = WeaponId.RiotRepeater, Name = "Riot Repeater", FireInterval = 0.18f, Desc = "Steady single shots with a little spread." },
            new WeaponDef { Id = WeaponId.LightBolt, Name = "Light Bolt", FireInterval = 0.25f, Desc = "Accurate long-range bolts." },
            new WeaponDef { Id = WeaponId.TwinWisps, Name = "Twin Wisps", FireInterval = 0.35f, Desc = "Two curving shots that cross in front of you." },
            new WeaponDef { Id = WeaponId.MendersBow, Name = "Mender's Bow", FireInterval = 0.3f, Desc = "Bolts heal your ally when they pass through them." },
            new WeaponDef { Id = WeaponId.TwinRifle, Name = "Twin Rifle", FireInterval = 0.1f, Desc = "Fast parallel twin stream." },
            new WeaponDef { Id = WeaponId.Railgun, Name = "Railgun", FireInterval = 0.7f, Desc = "Slow, devastating shots that pierce 3 enemies." },
            new WeaponDef { Id = WeaponId.BurstSmg, Name = "Burst SMG", FireInterval = 0.055f, Desc = "Sprays very fast, inaccurate shots." },
        };

        public static WeaponDef Weapon(Role role, int index) => Weapons[(int)role * WeaponsPerRole + index];

        /// XP needed to go from this level to the next.
        public static float XpToNext(int level) => 50f + 30f * (level - 1);

        public static RoleDef Get(Role r) => Roles[(int)r];
        public static EnemyDef Get(EnemyType t) => Enemies[(int)t];

        // Bullet kinds. Kinds >= SmallShot belong to enemies.
        public const byte TankShot = 0, HealerShot = 1, DpsShot = 2, TurretShot = 3, NovaShot = 4, FlakShell = 5, RailShot = 6, WispShot = 7, SmallShot = 10, BigShot = 11, FastShot = 12;

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
                case BigShot: return 10f;
                case FastShot: return 4.5f;
                default: return 6f;
            }
        }

        public static float BulletDamage(byte kind)
        {
            switch (kind)
            {
                case BigShot: return 20f;
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
                case BigShot: return new Rgba(0.85f, 0.35f, 1f);
                case FastShot: return new Rgba(1f, 0.95f, 0.4f);
                default: return new Rgba(1f, 0.3f, 0.45f);
            }
        }
    }
}
