namespace Arena
{
    // Data for the summoner's minions and the ground zones that several abilities create.

    public enum MinionKind : byte
    {
        Turret, Golem, Guard, Bear, Sprite, Treant, Wolf, Wisp, Imp, Bat, Phoenix, Guardian, Wall, Titan,
        LightOrb,  // Radiant Orb's extra projectiles: circles the orb while it's out (you otherwise), hurting enemies and healing allies it touches
        Clone,     // the Ninja's Shadow Clone: enemies target it; it bursts when it ends
        WardenGuard,  // Warden's Guard (legendary): walks with you and fights; it has health, and comes back after it falls (Stun.cs)
        HiveDrone,    // Hive Drones (the Hive Queen's legendary): circles you blocking bullets, flies out to sting, heals you coming home (QueenLoot.cs)
        HiveBomber,   // Bomber Escort (the Hive Queen's legendary): flies at the toughest enemy near you and bursts on it (QueenLoot.cs)
        Scythe,       // Twin Scythes (the Twin Reapers' legendary): circles you, cutting enemies and small bullets (ReaperLoot.cs)
        ThrownScythe, // Scythe Throw (the Twin Reapers' legendary ability): flies to your cursor and back, cutting (ReaperLoot.cs)
        Nimbus,       // the Nimbus weapon's storm cloud: drifts after your cursor, rains healing on allies under it (Nimbus.cs)
        SoulLantern,  // Soul Lantern: hovers by the player it is linked to, heals them and flames enemies near it (SoulLantern.cs)
        Hydra,        // Hydra (the DPS Summoner's ability): stays where it was called, its heads each spit venom at a different enemy (Hydra.cs)
        Butterfly,    // World Tree's healing butterfly: flutters to an ally (LinkTo) and heals them on arrival (WorldTree.cs)
        CloneWall,    // Shadow Clone: one of nine afterimages in a wall that soaks bullets and gets shoved by players; it does nothing else (CloneWall.cs)
        Thrall,       // Force Pact (the Tank Summoner's ability): an enemy turned to your side; it fights near you, and comes back after it falls (ForcePact.cs)
        Burrower,     // Earthquake's Burrower (the Tank Summoner's ability): breaks out at the cursor, tunnels through the pack hurting what it passes under (Burrower.cs)
        Bee,          // the Honeybee Staff's bee: hatches at the cursor, stings the nearest enemy once and dies (HoneybeeStaff.cs)
        VectorBlock,  // one tile of a Vector Wall or Bastion: stops enemy shots until its soak is gone or it times out (Engineer.cs)
        TankBot, MedicBot, AssaultBot,  // the Engineer's robots (Iron Fist, Stasis Gun, Vector Blade): summoned by a click, they fight and draw aggro (EngineerBots.cs)
        Wyvern,       // the Wyvern Staff's wind Wyvern: hovers over you, its gale slows and shoves, it breathes fire at your cursor and slams (WyvernStaff.cs)
        ArcShard, ArcBlade,  // the Arcanist's Orbit Shards (circle you, cut small shots, flung at the cursor and back) and Dancing Blades (hover behind you, launched to slash at the cursor) (Arcanist.cs)
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

    public enum ZoneKind : byte { FlameWall, Blizzard, Meteor, Sanctum, Grove, Consecrate, HolySword, Smoke, Mist, Ward, BloodPool, BoneSpear, Incense, OfudaWard, HoneyPool, HealingRain, BulletRain, LumenWard, HallowedBlade, OathWard, VeinTrail, Drawbridge, Bloodbath, BloodWall, Phalanx, FlakMine, TearGas, OssuaryWall, SmokeShell, HoneyPuddle, Flood, Cover, DragPylon, MedPylon, SafeRoom, Stasis, Stampede, DecoyDummy, DeployTurret, ToxicGas, GustGate, GrandSigil, SanctumSigil, ArcCrush, ShockGrenade, FieldHospital, Steam, StaticResidue, SupplyDrop, LandShark, Claymore }

    public class ZoneDef
    {
        public string Name;
        public Rgba Color;
        public float HealRate;  // HP/s for allies inside
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
            new MinionDef { Name = "Golem", Radius = 18f, Sides = 6, Color = new Rgba(0.44f, 0.89f, 1f), Move = MinionMove.Chase, Speed = 110f,
                Attack = MinionAttack.Slam, Interval = 1.2f, Damage = 130f, Range = 190f, BlockRadius = 140f, BlockCapacity = 1250f },  // a small Titan (Game.GolemScale): Titan.cs walks, slams and soaks for it; the numbers are for Scale 1
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
                Attack = MinionAttack.Explode, Damage = 70f, Range = 70f },
            new MinionDef { Name = "Bat", Radius = 6f, Sides = 3, Color = new Rgba(0.55f, 0.4f, 0.7f), Move = MinionMove.Chase, Speed = 320f,
                Attack = MinionAttack.Bite, Interval = 0.4f, Damage = 6f },
            new MinionDef { Name = "Phoenix", Radius = 12f, Sides = 5, Color = new Rgba(1f, 0.6f, 0.2f), Move = MinionMove.Orbit, Speed = 1.8f, OrbitRadius = 70f,
                Attack = MinionAttack.Shoot, Interval = 0.35f, Damage = 12f, Range = 450f },
            new MinionDef { Name = "Guardian Spirit", Radius = 22f, Sides = 8, Color = new Rgba(0.55f, 0.75f, 1f), Move = MinionMove.FollowOwner, Speed = 400f,
                BlockRadius = 75f, BlockCapacity = 400f },
            new MinionDef { Name = "Basalt Golem", Radius = 38f, Sides = 8, Color = new Rgba(0.26f, 0.25f, 0.3f), Move = MinionMove.Static,
                BlockRadius = 46f, BlockCapacity = 200f },  // Golem Wall: stays until its health is gone (BlockedBySummons); look in Game/GolemWallLook.cs
            new MinionDef { Name = "Titan", Radius = 140f, Sides = 6, Color = new Rgba(0.44f, 0.89f, 1f), Move = MinionMove.Chase, Speed = 75f,
                Attack = MinionAttack.Slam, Interval = 1.4f, Damage = 90f, Range = 190f, BlockRadius = 140f, BlockCapacity = 1000f },  // the Colossus: Titan.cs does its walking and slamming
            new MinionDef { Name = "Light Orb", Radius = 8f, Sides = 6, Color = new Rgba(1f, 0.95f, 0.6f), Move = MinionMove.Orbit, Speed = 2.2f, OrbitRadius = 85f,
                Attack = MinionAttack.Bite, Interval = 0.35f, Damage = 12f, HealRate = 6f, Range = 22f },
            new MinionDef { Name = "Shadow Clone", Radius = 14f, Sides = 8, Color = new Rgba(0.45f, 0.42f, 0.6f), Move = MinionMove.Static, BlockRadius = 16f, BlockCapacity = 100f },  // Thousand Clones: soaks enemy shots, bursts when it breaks
            new MinionDef { Name = "Warden's Guard", Radius = 13f, Sides = 8, Color = new Rgba(0.35f, 0.55f, 1f), Move = MinionMove.Escort, Speed = 240f,  // looks like the riot's guards
                Attack = MinionAttack.Bite, Interval = 0.7f, Damage = 14f },
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
            new MinionDef { Name = "Shadow Clone", Radius = 16f, Sides = 8, Color = new Rgba(0.22f, 0.8f, 0.9f), Move = MinionMove.Static, BlockRadius = 16f, BlockCapacity = 50f },  // shoved by CloneWall.cs
            new MinionDef { Name = "Thrall", Radius = 14f, Sides = 6, Color = new Rgba(0.75f, 0.55f, 1f), Move = MinionMove.Escort, Speed = 180f },  // ForcePact.cs: its shape, speed and attack come from the enemy type it was
            new MinionDef { Name = "Burrower", Radius = 18f, Sides = 8, Color = new Rgba(0.64f, 0.54f, 0.38f), Move = MinionMove.Static,
                Speed = 190f, Damage = 30f, Range = 38f },  // tunnels and bites by Burrower.cs (Range: how close it has to pass to an enemy)
            new MinionDef { Name = "Bee", Radius = 5f, Sides = 3, Color = new Rgba(1f, 0.77f, 0.24f), Move = MinionMove.Static },  // flown by HoneybeeStaff.cs (MoveBee)
            new MinionDef { Name = "Vector Block", Radius = 24f, Sides = 4, Color = new Rgba(0.36f, 0.88f, 1f), Move = MinionMove.Static,
                BlockRadius = 24f, BlockCapacity = 400f },
            // the Engineer's robots (EngineerBots.cs TickBot): Range is how far they shoot, Interval their fire rate
            new MinionDef { Name = "Tank Bot", Radius = 24f, Sides = 6, Color = new Rgba(1f, 0.71f, 0.28f), Move = MinionMove.Static, Speed = 115f,
                Interval = 0.75f, Damage = 6f, Range = 210f },
            new MinionDef { Name = "Medic Bot", Radius = 19f, Sides = 6, Color = new Rgba(0.44f, 0.94f, 0.66f), Move = MinionMove.Static, Speed = 150f,
                Interval = 0.4f, Damage = 9f, Range = 330f, HealRate = 9f },
            new MinionDef { Name = "Assault Bot", Radius = 19f, Sides = 6, Color = new Rgba(0.36f, 0.88f, 1f), Move = MinionMove.Static, Speed = 175f,
                Interval = 0.22f, Damage = 11f, Range = 340f },
            new MinionDef { Name = "Wyvern", Radius = 22f, Sides = 6, Color = new Rgba(0.44f, 0.88f, 0.81f), Move = MinionMove.FollowOwner, Speed = 240f },  // flown by WyvernStaff.cs (MoveWyvern)
            new MinionDef { Name = "Orbit Shard", Radius = 7f, Sides = 4, Color = new Rgba(0.72f, 0.55f, 1f), Move = MinionMove.Static },   // flown by Arcanist.cs (MoveArcane)
            new MinionDef { Name = "Dancing Blade", Radius = 8f, Sides = 3, Color = new Rgba(1f, 0.83f, 0.42f), Move = MinionMove.Static },  // flown by Arcanist.cs (MoveArcane)
        };

        // Indexed by ZoneKind.
        public static readonly ZoneDef[] Zones =
        {
            new ZoneDef { Name = "Flame Wall", Color = new Rgba(1f, 0.45f, 0.15f), Dps = 30f, BlockCapacity = 300f },
            new ZoneDef { Name = "Blizzard", Color = new Rgba(0.6f, 0.85f, 1f), Dps = 20f, Slow = 0.6f },
            new ZoneDef { Name = "Meteor", Color = new Rgba(1f, 0.35f, 0.15f), BlastDamage = 150f },
            new ZoneDef { Name = "Sanctum", Color = new Rgba(1f, 0.92f, 0.55f), PreventDown = true, BlockCapacity = 1500f },  // heals in pulses (Sanctum.cs), not by HealRate
            new ZoneDef { Name = "World Tree", Color = new Rgba(0.4f, 0.9f, 0.4f), HealRate = 18f, Dps = 20f, Slow = 0.4f },
            new ZoneDef { Name = "Consecrated Ground", Color = new Rgba(1f, 0.85f, 0.45f), HealRate = 12f, Dps = 20f },
            new ZoneDef { Name = "Hallowed Blade", Color = new Rgba(1f, 0.95f, 0.65f), BlastDamage = 200f },
            new ZoneDef { Name = "Smoke Bomb", Color = new Rgba(0.62f, 0.62f, 0.7f) },  // slows enemy bullets inside (Ninja.cs)
            new ZoneDef { Name = "Healing Footprint", Color = new Rgba(0.55f, 1f, 0.8f) },  // a Healing Dash footprint: heals whoever stands in it (Ninja.cs)
            new ZoneDef { Name = "Spirit Ward", Color = new Rgba(1f, 0.85f, 0.6f) },  // takes the damage allies near it would take, then pays some back as healing (Ninja.cs)
            new ZoneDef { Name = "Blood Pool", Color = new Rgba(0.6f, 0.05f, 0.12f), Slow = 0.4f, HealRate = 4f },  // a Gore Mortar's
            new ZoneDef { Name = "Bone Spear", Color = new Rgba(0.95f, 0.9f, 0.82f) },
            new ZoneDef { Name = "Incense", Color = new Rgba(0.85f, 0.8f, 0.6f), HealRate = 4f },  // the Censer's puffs and clouds (Censer.cs); enemy shots fired inside it stray
            new ZoneDef { Name = "Ofuda Ward", Color = new Rgba(1f, 0.85f, 0.6f) },
            new ZoneDef { Name = "Honey Pool", Color = new Rgba(1f, 0.75f, 0.2f), HealRate = 6f, Slow = 0.4f },  // Royal Dive's landing (QueenLoot.cs)  // an Ofuda charm stuck to the ground: bursts when an enemy steps on it (Ninja.cs)  // a thrown Bone Spear stuck in the ground: walk over it for its health back (Hemomancer.cs)
            new ZoneDef { Name = "Healing Rain", Color = new Rgba(0.4f, 0.85f, 0.5f), HealRate = 8f, Slow = 0.3f },  // the Healer Elementalist's rain (Docs/HealingRain.md)
            new ZoneDef { Name = "Bullet Rain", Color = new Rgba(0.47f, 0.69f, 0.94f), Dps = 45f },  // the Elementalist DPS ultimate (BulletRainLook.cs)
            new ZoneDef { Name = "Lumen Ward", Color = new Rgba(1f, 0.85f, 0.45f), HealRate = 9f },  // a cone of light that follows the caster's aim (LumenWard.cs): heals and burns enemy bullets inside
            new ZoneDef { Name = "Hallowed Blade", Color = new Rgba(1f, 0.95f, 0.65f), HealRate = 15f, Dps = 20f },  // the planted sword (HallowedBlade.cs): ring heals allies and burns enemies, and the whole arena gets a weaker pulse
            new ZoneDef { Name = "Ward of Light", Color = new Rgba(1f, 0.85f, 0.4f) },  // Guardian's Oath (GuardiansOath.cs): allies inside take less damage (Zone.DamageCut) and downed ones revive faster
            new ZoneDef { Name = "Vein Trail", Color = new Rgba(0.85f, 0.12f, 0.2f), Slow = 0.3f },  // the blood a Vein Rush leaves behind (VeinRush.cs): slows enemies on it
            new ZoneDef { Name = "Drawbridge", Color = new Rgba(0.48f, 0.33f, 0.2f) },  // the DPS Knight's ultimate (Drawbridge.cs): Pos is the hinge, Aim the way it fell
            new ZoneDef { Name = "Bloodbath", Color = new Rgba(0.55f, 0.03f, 0.08f) },  // the Tank Hemomancer's second ultimate (Bloodbath.cs): slows, bleeds and guards by its own tick
            new ZoneDef { Name = "Blood Wall", Color = new Rgba(0.7f, 0.07f, 0.14f) },  // one node of the tank Hemomancer's Blood Wall (BloodWall.cs): Aim is the node before it
            new ZoneDef { Name = "Bone Phalanx", Color = new Rgba(0.93f, 0.9f, 0.8f) },  // the Tank Summoner's second ultimate (BonePhalanx.cs): Pos is the middle of the shield face, Aim the heading
            new ZoneDef { Name = "Proximity Shell", Color = new Rgba(1f, 0.72f, 0.3f) },  // the Flak Cannon's Alt (TankAlts.cs): Radius is the burst, Aim the way it flew in
            new ZoneDef { Name = "Tear Gas", Color = new Rgba(0.92f, 0.9f, 0.7f) },  // the Riot Repeater's Alt (TankAlts.cs): suppresses and slows by its own tick
            new ZoneDef { Name = "Ossuary Wall", Color = new Rgba(0.93f, 0.9f, 0.8f) },  // the Bone Guard's Alt (BoneGuard.cs): Pos the middle, Aim the way it was thrown, Radius half its length
            new ZoneDef { Name = "Smoke Shell", Color = new Rgba(0.37f, 0.83f, 0.74f) },  // the Grenade Launcher's smoke (GrenadeLauncher.cs): Aim the way it flew in, GuardFrac its flight distance; slows and jams by its own tick
            new ZoneDef { Name = "Honey Puddle", Color = new Rgba(1f, 0.71f, 0.18f), Slow = 0.4f },
            new ZoneDef { Name = "Flood", Color = new Rgba(0.31f, 0.82f, 0.91f) },  // the Tank Elementalist's Flood (Flood.cs): slows and soaks by its own tick  // the Honeybee Staff's spray (HoneybeeStaff.cs): slows enemies; an enemy dying in it bursts into bees
            new ZoneDef { Name = "Cover", Color = new Rgba(0.44f, 0.69f, 0.94f) },
            new ZoneDef { Name = "Drag Pylon", Color = new Rgba(0.64f, 0.55f, 1f) },
            new ZoneDef { Name = "Med Pylon", Color = new Rgba(0.44f, 0.94f, 0.66f), HealRate = 10f },  // the Healer Engineer's 1st ability: heals allies inside; stays until the next one
            new ZoneDef { Name = "Safe Room", Color = new Rgba(0.44f, 0.94f, 0.66f), HealRate = 20f },  // the Healer Engineer's ultimate: a room (or dome) that heals and cuts damage (Zone.DamageCut)  // the Engineer's 1st ability (Engineer.cs): slows enemies inside by its own tick; stays until the next one  // the Tank Soldier's Deployable Cover (DeployableCover.cs): Pos its middle, Aim the way it faces, GuardFrac its health left; blocks by CoverStops, not as a circle
            new ZoneDef { Name = "Stasis", Color = new Rgba(0.55f, 0.85f, 1f) },  // the Stasis Gun's field (EngineerBots.cs): at your cursor while you hold Fire; holds enemies in place and hurts them by its own tick
            new ZoneDef { Name = "Stampede", Color = new Rgba(0.5f, 0.88f, 0.67f) },  // the Tank Summoner's 4th choice (Stampede.cs): Pos the middle of the line (it moves), Aim the heading, Radius half its length
            new ZoneDef { Name = "Decoy Dummy", Color = new Rgba(0.95f, 0.85f, 0.71f) },  // the Healer Soldier's 4th choice (DecoyDummy.cs): Pos where it stands, Aim and Marched its throw, GuardFrac its health left
            new ZoneDef { Name = "Deployable Turret", Color = new Rgba(1f, 0.78f, 0.29f) },  // the DPS Soldier's 4th choice (DeployableTurret.cs): Pos where it stands (you sit in it), Aim the way the guns point, Radius the seat
            new ZoneDef { Name = "Gas Grenade", Color = new Rgba(0.72f, 0.42f, 1f) },  // a Gas Grenade gadget's cloud (Gadgets.cs): lobbed in like Tear Gas, Aim the way it flew; hurts and slows by its own tick
            new ZoneDef { Name = "Gust Gate", Color = new Rgba(0.78f, 0.96f, 1f) },  // the Healer Elementalist's 2nd choice (GustGate.cs): boosts players crossing its edge and pushes enemies out by its own tick
            new ZoneDef { Name = "Grand Sigil", Color = new Rgba(0.72f, 0.55f, 1f) },     // the Tank Arcanist's ultimate (Arcanist.cs): binds and taunts enemies inside, freezes enemy shots at its rim, sends them back as it ends
            new ZoneDef { Name = "Sanctum Sigil", Color = new Rgba(1f, 0.85f, 0.45f), HealRate = 25f },  // the Healer Arcanist's ultimate (Arcanist.cs): follows its caster; Aim = the six runes left (a bit mask)
            new ZoneDef { Name = "Telekinetic Crush", Color = new Rgba(0.72f, 0.55f, 1f) },  // the DPS Arcanist's 1st ability (Arcanist.cs): lifts and pulls enemies in, then slams them together as it ends
            new ZoneDef { Name = "Shock Grenade", Color = new Rgba(0.45f, 0.85f, 1f) },
            new ZoneDef { Name = "Field Hospital", Color = new Rgba(0.4f, 1f, 0.6f) },  // the Healer Soldier's second ultimate (FieldHospital.cs): Pos the cell's middle (or the tent's), Radius half the cell, Aim the way to its door (NaN: a tent), Side who has used its shrine (a bit per player id)  // the Tank Soldier's Scattergun Alt (TankAlts.cs): lobbed in, shocks and stuns on landing; Aim the way it flew in, Radius its reach
            new ZoneDef { Name = "Steam", Color = new Rgba(0.9f, 0.93f, 0.95f) },  // fire + water (Steam.cs): scalds and jams by its own tick
            new ZoneDef { Name = "Static Residue", Color = new Rgba(0.5f, 0.91f, 1f) },  // a Living Bolt dash's live trail (LivingBolt.cs): a lane, Pos its middle, Aim its heading, Radius half its length; hurts by its own tick
            new ZoneDef { Name = "Supply Drop", Color = new Rgba(0.4f, 1f, 0.55f) },  // the Healer Soldier's third ultimate (SupplyDrop.cs): Pos the crate, Radius the open zone; its timer gives the phase (flare, landing, open, folding)
            new ZoneDef { Name = "Land Shark", Color = new Rgba(0.36f, 0.62f, 0.92f) },  // the DPS Summoner's 4th choice (LandShark.cs): the puddle, Pos its middle, Radius its size, Aim the leap's heading
            new ZoneDef { Name = "Claymore", Color = new Rgba(1f, 0.48f, 0.3f) },  // the DPS Soldier's 3rd choice (Claymore.cs): Pos the mine, Aim its facing, Radius the trigger's reach, GuardFrac its state, Marched a mastered spare blast
        };

        public static MinionDef Get(MinionKind k) => Minions[(int)k];
        public static ZoneDef Get(ZoneKind k) => Zones[(int)k];
    }
}
