namespace Arena
{
    // Data for the summoner's minions and the ground zones that several abilities create.

    public enum MinionKind : byte
    {
        Turret, Golem, Guard, Bear, Sprite, Treant, Wolf, Wisp, Imp, Bat, Phoenix, Guardian, Wall, Titan,
        LightOrb,  // Radiant Orb's extra projectiles: circles the orb while it's out (you otherwise), hurting enemies and healing allies it touches
        Clone,     // the Ninja's Shadow Clone: enemies target it; it bursts when it ends
        WardenGuard,  // Warden's Guard (legendary): walks with you and fights; it has health, and comes back after it falls (Stun.cs)
        SoulGhost,    // Soul Link cast with no ally: a ghost of you, linked, that fights beside you; it has health (SoulGhost.cs)
        HiveDrone,    // Hive Drones (the Hive Queen's legendary): circles you blocking bullets, flies out to sting, heals you coming home (QueenLoot.cs)
        HiveBomber,   // Bomber Escort (the Hive Queen's legendary): flies at the toughest enemy near you and bursts on it (QueenLoot.cs)
        Scythe,       // Twin Scythes (the Twin Reapers' legendary): circles you, cutting enemies and small bullets (ReaperLoot.cs)
        ThrownScythe, // Scythe Throw (the Twin Reapers' legendary ability): flies to your cursor and back, cutting (ReaperLoot.cs)
        Nimbus,       // the Nimbus weapon's storm cloud: drifts after your cursor, rains healing on allies under it (Nimbus.cs)
        SoulLantern,  // Soul Lantern: hovers by the player it is linked to, heals them and flames enemies near it (SoulLantern.cs)
        Hydra,        // Hydra (the DPS Summoner's ability): stays where it was called, its heads each spit venom at a different enemy (Hydra.cs)
        Butterfly,    // World Tree's healing butterfly: flutters to an ally (LinkTo) and heals them on arrival (WorldTree.cs)
        None = 255,
    }

    public enum MinionMove : byte { Static, Orbit, Chase, FollowHurt, FollowOwner, Escort }  // Escort: fights near its owner (MoveGuard)

    public enum MinionAttack : byte { None, Shoot, Bite, Slam, Explode, Heal }

    public class MinionDef
    {
        public string Name;
        public float Radius;
        public Rgba Color;
        public int Sides;
        public MinionMove Move;
        public float Speed;          // px/s, or rad/s when orbiting
        public float OrbitRadius;
        public MinionAttack Attack;
        public float Interval, Damage, Range;  // Range: shoot range, slam/explode radius, heal reach
        public float HealRate;       // HP per second while healing
        public float BlockRadius;    // stops enemy bullets coming in this close (0 = none)
        public float BlockCapacity;  // boss damage it can soak before it breaks (or, for companions, staggers)
    }

    public enum ZoneKind : byte { Haven, FlameWall, Blizzard, Lantern, Meteor, Sanctum, Grove, Consecrate, HolySword, Smoke, Mist, Ward, Siphon, BloodPool, BoneSpear, Incense, OfudaWard, HoneyPool, HealingRain, BulletRain, LumenWard }

    public class ZoneDef
    {
        public string Name;
        public Rgba Color;
        public float HealRate, ReviveBoost;  // HP/s for allies inside; extra revive speed for downed allies
        public float Dps, Slow;              // damage per second to enemies inside; slow amount (0..1)
        public float BlockCapacity;          // enemy bullets coming in are destroyed until this much boss damage is soaked
        public bool PreventDown;             // allies inside can't drop below 1 HP
        public float BlastDamage;            // explodes when it expires (meteor)
    }

    public static class Kits
    {
        // Indexed by MinionKind.
        public static readonly MinionDef[] Minions =
        {
            new MinionDef { Name = "Turret", Radius = 13f, Sides = 6, Color = new Rgba(0.6f, 0.95f, 1f), Move = MinionMove.Static,
                Attack = MinionAttack.Shoot, Interval = 0.25f, Damage = 9f, Range = 520f },
            new MinionDef { Name = "Golem", Radius = 18f, Sides = 5, Color = new Rgba(0.65f, 0.6f, 0.55f), Move = MinionMove.Chase, Speed = 110f,
                Attack = MinionAttack.Slam, Interval = 1f, Damage = 25f, Range = 60f, BlockRadius = 24f, BlockCapacity = 220f },
            new MinionDef { Name = "Skeleton Guard", Radius = 10f, Sides = 4, Color = new Rgba(0.92f, 0.9f, 0.8f), Move = MinionMove.Orbit, Speed = 1.6f, OrbitRadius = 55f,
                Attack = MinionAttack.Bite, Interval = 0.5f, Damage = 8f, BlockRadius = 16f, BlockCapacity = 90f },
            new MinionDef { Name = "Bear", Radius = 15f, Sides = 7, Color = new Rgba(0.6f, 0.4f, 0.25f), Move = MinionMove.Chase, Speed = 200f,
                Attack = MinionAttack.Bite, Interval = 0.6f, Damage = 22f },
            new MinionDef { Name = "Sprite", Radius = 6f, Sides = 4, Color = new Rgba(0.5f, 1f, 0.6f), Move = MinionMove.FollowHurt, Speed = 220f,
                Attack = MinionAttack.Heal, HealRate = 5f, Range = 60f },
            new MinionDef { Name = "Treant", Radius = 20f, Sides = 6, Color = new Rgba(0.35f, 0.7f, 0.3f), Move = MinionMove.FollowOwner, Speed = 160f,
                Attack = MinionAttack.Slam, Interval = 1.2f, Damage = 15f, Range = 50f, BlockRadius = 28f, BlockCapacity = 260f },
            new MinionDef { Name = "Wolf", Radius = 10f, Sides = 3, Color = new Rgba(0.7f, 0.7f, 0.8f), Move = MinionMove.Chase, Speed = 260f,
                Attack = MinionAttack.Bite, Interval = 0.5f, Damage = 12f },
            new MinionDef { Name = "Wisp", Radius = 6f, Sides = 5, Color = new Rgba(0.85f, 1f, 0.95f), Move = MinionMove.Orbit, Speed = 2.5f, OrbitRadius = 40f,
                Attack = MinionAttack.Shoot, Interval = 0.5f, Damage = 7f, Range = 380f },
            new MinionDef { Name = "Imp", Radius = 8f, Sides = 3, Color = new Rgba(1f, 0.35f, 0.2f), Move = MinionMove.Chase, Speed = 240f,
                Attack = MinionAttack.Explode, Damage = 45f, Range = 70f },
            new MinionDef { Name = "Bat", Radius = 6f, Sides = 3, Color = new Rgba(0.55f, 0.4f, 0.7f), Move = MinionMove.Chase, Speed = 320f,
                Attack = MinionAttack.Bite, Interval = 0.4f, Damage = 6f },
            new MinionDef { Name = "Phoenix", Radius = 12f, Sides = 5, Color = new Rgba(1f, 0.6f, 0.2f), Move = MinionMove.Orbit, Speed = 1.8f, OrbitRadius = 70f,
                Attack = MinionAttack.Shoot, Interval = 0.35f, Damage = 12f, Range = 450f },
            new MinionDef { Name = "Guardian Spirit", Radius = 22f, Sides = 8, Color = new Rgba(0.55f, 0.75f, 1f), Move = MinionMove.FollowOwner, Speed = 400f,
                BlockRadius = 75f, BlockCapacity = 400f },
            new MinionDef { Name = "Stone Wall", Radius = 22f, Sides = 4, Color = new Rgba(0.6f, 0.55f, 0.5f), Move = MinionMove.Static,
                BlockRadius = 30f, BlockCapacity = 200f },
            new MinionDef { Name = "Titan", Radius = 32f, Sides = 6, Color = new Rgba(0.8f, 0.7f, 0.45f), Move = MinionMove.Chase, Speed = 110f,
                Attack = MinionAttack.Slam, Interval = 1.1f, Damage = 70f, Range = 100f, BlockRadius = 46f, BlockCapacity = 1000f },
            new MinionDef { Name = "Light Orb", Radius = 8f, Sides = 6, Color = new Rgba(1f, 0.95f, 0.6f), Move = MinionMove.Orbit, Speed = 2.2f, OrbitRadius = 85f,
                Attack = MinionAttack.Bite, Interval = 0.35f, Damage = 12f, HealRate = 6f, Range = 22f },
            new MinionDef { Name = "Shadow Clone", Radius = 14f, Sides = 8, Color = new Rgba(0.45f, 0.42f, 0.6f), Move = MinionMove.Static },
            new MinionDef { Name = "Warden's Guard", Radius = 13f, Sides = 8, Color = new Rgba(0.35f, 0.55f, 1f), Move = MinionMove.Escort, Speed = 240f,  // looks like the riot's guards
                Attack = MinionAttack.Bite, Interval = 0.7f, Damage = 14f },
            new MinionDef { Name = "Soul Ghost", Radius = 14f, Sides = 12, Color = new Rgba(0.6f, 1f, 0.9f), Move = MinionMove.FollowOwner, Speed = 320f,
                Attack = MinionAttack.Shoot, Interval = 0.3f, Damage = 11f, Range = 460f },
            new MinionDef { Name = "Hive Drone", Radius = 7f, Sides = 3, Color = new Rgba(1f, 0.82f, 0.3f), Move = MinionMove.Orbit, Speed = 2.6f, OrbitRadius = 45f,
                BlockRadius = 16f, BlockCapacity = 60f },  // moved and stings by QueenLoot.cs
            new MinionDef { Name = "Hive Bomber", Radius = 9f, Sides = 3, Color = new Rgba(1f, 0.5f, 0.18f), Move = MinionMove.Static,
                Damage = 55f, Range = 70f },  // flown by QueenLoot.cs; bursts for Damage within Range
            new MinionDef { Name = "Twin Scythe", Radius = 14f, Sides = 3, Color = new Rgba(1f, 0.35f, 0.6f), Move = MinionMove.Orbit, Speed = 2.4f, OrbitRadius = 60f,
                Damage = 16f },  // moved and cuts by ReaperLoot.cs (Dusk's colour; the outer one is drawn in Dawn's)
            new MinionDef { Name = "Thrown Scythe", Radius = 22f, Sides = 3, Color = new Rgba(0.85f, 0.75f, 1f), Move = MinionMove.Static,
                Damage = 45f },  // thrown by ReaperLoot.cs
            new MinionDef { Name = "Nimbus", Radius = 20f, Sides = 6, Color = new Rgba(0.62f, 0.74f, 1f), Move = MinionMove.FollowOwner, Speed = 600f },  // steered to your cursor and raining by Nimbus.cs
            new MinionDef { Name = "Soul Lantern", Radius = 14f, Sides = 6, Color = new Rgba(0.47f, 1f, 0.88f), Move = MinionMove.Static },  // moved, healing and flaming by SoulLantern.cs
            new MinionDef { Name = "Hydra", Radius = 26f, Sides = 8, Color = new Rgba(0.53f, 0.88f, 0.56f), Move = MinionMove.Static,
                Interval = 0.6f, Damage = 10f, Range = 520f },  // shoots by Hydra.cs
            new MinionDef { Name = "Butterfly", Radius = 6f, Sides = 4, Color = new Rgba(1f, 0.84f, 0.43f), Move = MinionMove.Static },  // flown by WorldTree.cs
        };

        // Indexed by ZoneKind.
        public static readonly ZoneDef[] Zones =
        {
            new ZoneDef { Name = "Haven",  // unused since the Adrenaline Shot; kept so the ZoneKind numbers stay put
                Color = new Rgba(0.4f, 1f, 0.6f), HealRate = 15f, ReviveBoost = 2f },
            new ZoneDef { Name = "Flame Wall", Color = new Rgba(1f, 0.45f, 0.15f), Dps = 30f, BlockCapacity = 300f },
            new ZoneDef { Name = "Blizzard", Color = new Rgba(0.6f, 0.85f, 1f), Dps = 20f, Slow = 0.6f },
            new ZoneDef { Name = "Soul Lantern", Color = new Rgba(1f, 0.9f, 0.5f), HealRate = 5f, PreventDown = true },
            new ZoneDef { Name = "Meteor", Color = new Rgba(1f, 0.35f, 0.15f), BlastDamage = 150f },
            new ZoneDef { Name = "Sanctum", Color = new Rgba(1f, 0.92f, 0.55f), PreventDown = true, BlockCapacity = 1500f },  // heals in pulses (Sanctum.cs), not by HealRate
            new ZoneDef { Name = "World Tree", Color = new Rgba(0.4f, 0.9f, 0.4f), HealRate = 18f, Dps = 20f, Slow = 0.4f },
            new ZoneDef { Name = "Consecrated Ground", Color = new Rgba(1f, 0.85f, 0.45f), HealRate = 12f, Dps = 20f },
            new ZoneDef { Name = "Hallowed Blade", Color = new Rgba(1f, 0.95f, 0.65f), BlastDamage = 200f },
            new ZoneDef { Name = "Smoke Bomb", Color = new Rgba(0.62f, 0.62f, 0.7f) },  // slows enemy bullets inside (Ninja.cs)
            new ZoneDef { Name = "Healing Footprint", Color = new Rgba(0.55f, 1f, 0.8f) },  // a Healing Dash footprint: heals whoever stands in it (Ninja.cs)
            new ZoneDef { Name = "Spirit Ward", Color = new Rgba(1f, 0.85f, 0.6f) },  // takes the damage allies near it would take, then pays some back as healing (Ninja.cs)
            new ZoneDef { Name = "Siphon Field", Color = new Rgba(0.85f, 0.15f, 0.3f), HealRate = 10f, Dps = 10f },
            new ZoneDef { Name = "Blood Pool", Color = new Rgba(0.6f, 0.05f, 0.12f), Slow = 0.4f },  // a Clot Bolt's
            new ZoneDef { Name = "Bone Spear", Color = new Rgba(0.95f, 0.9f, 0.82f) },
            new ZoneDef { Name = "Incense", Color = new Rgba(0.85f, 0.8f, 0.6f), HealRate = 4f },  // the Censer's trail (Knight.cs); enemy shots fired inside it stray
            new ZoneDef { Name = "Ofuda Ward", Color = new Rgba(1f, 0.85f, 0.6f) },
            new ZoneDef { Name = "Honey Pool", Color = new Rgba(1f, 0.75f, 0.2f), HealRate = 6f, Slow = 0.4f },  // Royal Dive's landing (QueenLoot.cs)  // an Ofuda charm stuck to the ground: bursts when an enemy steps on it (Ninja.cs)  // a thrown Bone Spear stuck in the ground: walk over it for its health back (BloodMage.cs)
            new ZoneDef { Name = "Healing Rain", Color = new Rgba(0.4f, 0.85f, 0.5f), HealRate = 8f, Slow = 0.3f },  // the Healer Elementalist's rain (Docs/HealingRain.md)
            new ZoneDef { Name = "Bullet Rain", Color = new Rgba(0.47f, 0.69f, 0.94f), Dps = 45f },  // the Elementalist DPS ultimate (BulletRainLook.cs)
            new ZoneDef { Name = "Lumen Ward", Color = new Rgba(1f, 0.85f, 0.45f), HealRate = 9f },  // a cone of light along the caster's aim (LumenWard.cs): heals inside, enemy bullets outside slow to a stop
        };

        public static MinionDef Get(MinionKind k) => Minions[(int)k];
        public static ZoneDef Get(ZoneKind k) => Zones[(int)k];
    }
}
