using System.Collections.Generic;

namespace Arena
{
    // Data for the summoner's minions and the ground zones that several abilities create.

    public enum MinionKind : byte
    {
        // In name order, one per line: Tools/Ids/ids.py (CI checks it; --fix sorts). Numbers follow from the order.
        ArcBlade,  // the Arcanist's Orbit Shards (circle you, cut small shots, flung at the cursor and back) and Dancing Blades (hover behind you, launched to slash at the cursor) (Arcanist.cs)
        ArcShard,
        AssaultBot,  // the Engineer's robots (Iron Fist, Stasis Gun, Vector Blade): summoned by a click, they fight and draw aggro (EngineerBots.cs)
        Bat,
        Bear,
        Bee,  // the Honeybee Staff's bee: hatches at the cursor, stings the nearest enemy once and dies (HoneybeeStaff.cs)
        BloodImp,  // Blood Ritual (the DPS Hemomancer's fourth ultimate): pours out of the portal, circles an enemy clawing and taunting it, flies home at the close (BloodRitual.cs)
        Broodling,  // Brood Queen's broodling: runs at an enemy she isn't biting, latches on and bites (BroodQueen.cs)
        BroodQueen,  // Brood Queen (the DPS Summoner's fourth ultimate): drops on a thread, hunts and bites, taunts, spits webs, sends broodlings (BroodQueen.cs)
        BumbleBot,  // the Bumble Remote's robot bee: follows your cursor, shoots stingers, dives into a Swarm Burst; enemies ignore it (BumbleRemote.cs)
        Burrower,  // Earthquake's Burrower (the Tank Summoner's ability): breaks out at the cursor, tunnels through the pack hurting what it passes under (Burrower.cs)
        Butterfly,  // World Tree's healing butterfly: flutters to an ally (LinkTo) and heals them on arrival (WorldTree.cs)
        Clone,  // the Ninja's Shadow Clone: enemies target it; it bursts when it ends
        CloneMedic,  // Shadow Clone Medics (the Healer Ninja's fourth ultimate): trails a player, heals them and stops the shots aimed at them (ShadowCloneMedics.cs)
        CloneWall,  // Shadow Clone: one of nine afterimages in a wall that soaks bullets and gets shoved by players; it does nothing else (CloneWall.cs)
        ClotCrystal,  // Clot Crystals (the Healer Hemomancer's 4th choice): circles its owner and jerks at enemy bullets in reach to eat them (ClotCrystals.cs)
        Demon,  // the Demon Calling's demon: hovers at its owner's shoulder hurling fireballs (Callings.cs)
        Fairy,  // the Fairy Calling's fairy: tends the most hurt ally, healing them; untargetable (Callings.cs)
        Golem,
        Guard,
        Guardian,
        HiveBomber,  // Bomber Escort (the Hive Queen's legendary): flies at the toughest enemy near you and bursts on it (QueenLoot.cs)
        HiveDrone,  // Hive Drones (the Hive Queen's legendary): circles you blocking bullets, flies out to sting, heals you coming home (QueenLoot.cs)
        Hydra,  // Hydra (the DPS Summoner's ability): stays where it was called, its heads each spit venom at a different enemy (Hydra.cs)
        Imp,
        LightOrb,  // Radiant Orb's extra projectiles: circles the orb while it's out (you otherwise), hurting enemies and healing allies it touches
        MedicBot,
        Nimbus,  // the Nimbus weapon's storm cloud: drifts after your cursor, rains healing on allies under it (Nimbus.cs)
        Octopus,  // Octopus Familiar (the Healer Summoner's 4th choice): floats round the player it guards and slaps enemies near them, up to 8 at once (OctopusFamiliar.cs)
        Phoenix,
        Scythe,  // Twin Scythes (the Twin Reapers' legendary): circles you, cutting enemies and small bullets (ReaperLoot.cs)
        SoulLantern,  // Soul Lantern: hovers by the player it is linked to, heals them and flames enemies near it (SoulLantern.cs)
        Spider,  // the Spider Calling's spider (and Spider Queen's spiderlings): leaps on enemies near its owner, its bites web them (Callings.cs)
        Sprite,
        TankBot,
        Thrall,  // Force Pact (the Tank Summoner's ability): an enemy turned to your side; it fights near you, and comes back after it falls (ForcePact.cs)
        ThrownScythe,  // Scythe Throw (the Twin Reapers' legendary ability): flies to your cursor and back, cutting (ReaperLoot.cs)
        Titan,
        Treant,
        Turret,
        VectorBlock,  // one tile of a Vector Wall or Bastion: stops enemy shots until its soak is gone or it times out (Engineer.cs)
        Wall,
        WardenGuard,  // Warden's Guard (legendary): walks with you and fights; it has health, and comes back after it falls (Stun.cs)
        Wisp,
        Wolf,
        Wyvern,  // the Wyvern Staff's wind Wyvern: hovers over you, its gale slows and shoves, it breathes fire at your cursor and slams (WyvernStaff.cs)
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

    public enum ZoneKind : byte
    {
        // In name order, one per line: Tools/Ids/ids.py (CI checks it; --fix sorts). Numbers follow from the order.
        AegisStandard,
        ArcCrush,
        ArmorDrop,
        Ballista,
        BlastOfuda,
        Blizzard,
        Bloodbath,
        BloodPool,
        BloodRitual,  // the DPS Hemomancer's fourth ultimate (BloodRitual.cs): see its ZoneDef
        BloodSapling,  // a Blood Garden sapling (BloodGarden.cs): see its ZoneDef
        BloodTotem,
        BloodWall,
        BoneSpear,
        BroodWeb,  // a Brood Queen web (BroodQueen.cs): slows enemies on it
        BulletRain,
        Caltrop,
        Claymore,
        Consecrate,
        Cover,
        DecoyDummy,
        DeployTurret,
        DragPylon,
        Drawbridge,
        FieldHospital,
        FlakMine,
        FlameWall,
        Flood,
        GongSmoke,
        GrandSigil,
        Grove,
        GustGate,
        HallowedBlade,
        HazardFoam,
        HealingRain,
        HolySword,
        HoneyPool,
        HoneyPuddle,
        HoneyRaid,
        Incense,
        LandShark,
        LumenWard,
        Mammoth,
        MedPylon,
        MendingWeb,
        Meteor,
        Mist,
        OathWard,
        OssuaryWall,
        Phalanx,
        PilgrimsStandard,
        RocketDrop,
        RocketFire,
        SafeRoom,
        Sanctum,
        SanctumSigil,
        ShockGrenade,
        SleepyCat,
        Smoke,
        SmokeShell,
        Stampede,
        Stasis,
        StaticResidue,
        Steam,
        SupplyDrop,
        TearGas,
        TortoiseOracle,
        ToxicGas,
        TrojanHorse,
        Tusker,
        VeinTrail,
        Ward,
        WarStandard,
    }

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
        public static readonly MinionDef[] Minions = IdTable.Of(new Dictionary<MinionKind, MinionDef>
        {
            [MinionKind.Turret] = new MinionDef { Name = "Turret", Radius = 13f, Sides = 6, Color = new Rgba(0.6f, 0.95f, 1f), Move = MinionMove.Static,
                Attack = MinionAttack.Shoot, Interval = 0.25f, Damage = 9f, Range = 520f },
            [MinionKind.Golem] = new MinionDef { Name = "Golem", Radius = 18f, Sides = 6, Color = new Rgba(0.44f, 0.89f, 1f), Move = MinionMove.Chase, Speed = 110f,
                Attack = MinionAttack.Slam, Interval = 1.2f, Damage = 130f, Range = 190f, BlockRadius = 140f, BlockCapacity = 1250f },  // a small Titan (Game.GolemScale): Titan.cs walks, slams and soaks for it; the numbers are for Scale 1
            [MinionKind.Guard] = new MinionDef { Name = "Skeleton Guard", Radius = 10f, Sides = 4, Color = new Rgba(0.92f, 0.9f, 0.8f), Move = MinionMove.Orbit, Speed = 1.6f, OrbitRadius = 55f,
                Attack = MinionAttack.Bite, Interval = 0.5f, Damage = 8f, BlockRadius = 16f, BlockCapacity = 90f },
            [MinionKind.Bear] = new MinionDef { Name = "Bear", Radius = 15f, Sides = 7, Color = new Rgba(0.6f, 0.4f, 0.25f), Move = MinionMove.Chase, Speed = 200f,
                Attack = MinionAttack.Bite, Interval = 0.6f, Damage = 22f },
            [MinionKind.Sprite] = new MinionDef { Name = "Sprite", Radius = 6f, Sides = 4, Color = new Rgba(0.5f, 1f, 0.6f), Move = MinionMove.FollowHurt, Speed = 220f,
                Attack = MinionAttack.Heal, HealRate = 5f, Range = 60f },
            [MinionKind.Treant] = new MinionDef { Name = "Treant", Radius = 20f, Sides = 6, Color = new Rgba(0.35f, 0.7f, 0.3f), Move = MinionMove.FollowOwner, Speed = 160f,
                Attack = MinionAttack.Slam, Interval = 1.2f, Damage = 15f, Range = 50f, BlockRadius = 28f, BlockCapacity = 260f },
            [MinionKind.Wolf] = new MinionDef { Name = "Wolf", Radius = 10f, Sides = 3, Color = new Rgba(0.7f, 0.7f, 0.8f), Move = MinionMove.Chase, Speed = 260f,
                Attack = MinionAttack.Bite, Interval = 0.5f, Damage = 12f },
            [MinionKind.Wisp] = new MinionDef { Name = "Wisp", Radius = 6f, Sides = 5, Color = new Rgba(0.85f, 1f, 0.95f), Move = MinionMove.Orbit, Speed = 2.5f, OrbitRadius = 40f,
                Attack = MinionAttack.Shoot, Interval = 0.5f, Damage = 7f, Range = 380f },
            [MinionKind.Imp] = new MinionDef { Name = "Imp", Radius = 8f, Sides = 3, Color = new Rgba(1f, 0.35f, 0.2f), Move = MinionMove.Chase, Speed = 240f,
                Attack = MinionAttack.Explode, Damage = 70f, Range = 70f },
            [MinionKind.Bat] = new MinionDef { Name = "Bat", Radius = 6f, Sides = 3, Color = new Rgba(0.55f, 0.4f, 0.7f), Move = MinionMove.Chase, Speed = 320f,
                Attack = MinionAttack.Bite, Interval = 0.4f, Damage = 6f },
            [MinionKind.Phoenix] = new MinionDef { Name = "Phoenix", Radius = 12f, Sides = 5, Color = new Rgba(1f, 0.6f, 0.2f), Move = MinionMove.Orbit, Speed = 1.8f, OrbitRadius = 70f,
                Attack = MinionAttack.Shoot, Interval = 0.35f, Damage = 12f, Range = 450f },
            [MinionKind.Guardian] = new MinionDef { Name = "Guardian Spirit", Radius = 22f, Sides = 8, Color = new Rgba(0.55f, 0.75f, 1f), Move = MinionMove.FollowOwner, Speed = 400f,
                BlockRadius = 75f, BlockCapacity = 400f },
            [MinionKind.Wall] = new MinionDef { Name = "Basalt Golem", Radius = 38f, Sides = 8, Color = new Rgba(0.26f, 0.25f, 0.3f), Move = MinionMove.Static,
                BlockRadius = 46f, BlockCapacity = 200f },  // Golem Wall: stays until its health is gone (BlockedBySummons); look in Game/GolemWallLook.cs
            [MinionKind.Titan] = new MinionDef { Name = "Titan", Radius = 140f, Sides = 6, Color = new Rgba(0.44f, 0.89f, 1f), Move = MinionMove.Chase, Speed = 75f,
                Attack = MinionAttack.Slam, Interval = 1.4f, Damage = 90f, Range = 190f, BlockRadius = 140f, BlockCapacity = 1000f },  // the Colossus: Titan.cs does its walking and slamming
            [MinionKind.LightOrb] = new MinionDef { Name = "Light Orb", Radius = 8f, Sides = 6, Color = new Rgba(1f, 0.95f, 0.6f), Move = MinionMove.Orbit, Speed = 2.2f, OrbitRadius = 85f,
                Attack = MinionAttack.Bite, Interval = 0.35f, Damage = 12f, HealRate = 6f, Range = 22f },
            [MinionKind.CloneMedic] = new MinionDef { Name = "Clone Medic", Radius = 13f, Sides = 8, Color = Game.ScmInk, Move = MinionMove.Static },  // moved, healed and its shots by ShadowCloneMedics.cs; health in Minion.Hp
            [MinionKind.Clone] = new MinionDef { Name = "Shadow Clone", Radius = 14f, Sides = 8, Color = new Rgba(0.45f, 0.42f, 0.6f), Move = MinionMove.Static, BlockRadius = 16f, BlockCapacity = 100f },  // Thousand Clones: soaks enemy shots, bursts when it breaks
            [MinionKind.Spider] = new MinionDef { Name = "Spider", Radius = 11f, Sides = 8, Color = new Rgba(0.55f, 0.35f, 0.75f), Move = MinionMove.Escort, Speed = 250f,
                Attack = MinionAttack.Bite, Interval = 0.8f, Damage = 14f },  // moved by Callings.cs
            [MinionKind.Fairy] = new MinionDef { Name = "Fairy", Radius = 6f, Sides = 5, Color = new Rgba(1f, 0.72f, 0.95f), Move = MinionMove.FollowHurt, Speed = 300f,
                Attack = MinionAttack.Heal, HealRate = Game.FairyHeal, Range = Game.FairyReach },
            [MinionKind.Demon] = new MinionDef { Name = "Demon", Radius = 12f, Sides = 5, Color = new Rgba(0.95f, 0.28f, 0.15f), Move = MinionMove.FollowOwner, Speed = 280f,
                Attack = MinionAttack.Shoot, Interval = 1.2f, Damage = 20f, Range = Game.DemonRange },
            [MinionKind.WardenGuard] = new MinionDef { Name = "Warden's Guard", Radius = 13f, Sides = 8, Color = new Rgba(0.35f, 0.55f, 1f), Move = MinionMove.Escort, Speed = 240f,  // looks like the riot's guards
                Attack = MinionAttack.Bite, Interval = 0.7f, Damage = 14f },
            [MinionKind.HiveDrone] = new MinionDef { Name = "Hive Drone", Radius = 7f, Sides = 3, Color = Rgba.Gold, Move = MinionMove.Orbit, Speed = 2.6f, OrbitRadius = 45f,
                BlockRadius = 16f, BlockCapacity = 60f },  // moved and stings by QueenLoot.cs
            [MinionKind.HiveBomber] = new MinionDef { Name = "Hive Bomber", Radius = 9f, Sides = 3, Color = new Rgba(1f, 0.5f, 0.18f), Move = MinionMove.Static,
                Damage = 55f, Range = 70f },  // flown by QueenLoot.cs; bursts for Damage within Range
            [MinionKind.Scythe] = new MinionDef { Name = "Twin Scythe", Radius = 14f, Sides = 3, Color = new Rgba(1f, 0.35f, 0.6f), Move = MinionMove.Orbit, Speed = 2.4f, OrbitRadius = 60f,
                Damage = 16f },  // moved and cuts by ReaperLoot.cs (Dusk's colour; the outer one is drawn in Dawn's)
            [MinionKind.ThrownScythe] = new MinionDef { Name = "Thrown Scythe", Radius = 22f, Sides = 3, Color = new Rgba(0.85f, 0.75f, 1f), Move = MinionMove.Static,
                Damage = 45f },  // thrown by ReaperLoot.cs
            [MinionKind.Nimbus] = new MinionDef { Name = "Nimbus", Radius = 20f, Sides = 6, Color = new Rgba(0.62f, 0.74f, 1f), Move = MinionMove.FollowOwner, Speed = 600f },  // steered to your cursor and raining by Nimbus.cs
            [MinionKind.SoulLantern] = new MinionDef { Name = "Soul Lantern", Radius = 14f, Sides = 6, Color = new Rgba(0.47f, 1f, 0.88f), Move = MinionMove.Static },  // moved, healing and flaming by SoulLantern.cs
            [MinionKind.Hydra] = new MinionDef { Name = "Hydra", Radius = 26f, Sides = 8, Color = new Rgba(0.53f, 0.88f, 0.56f), Move = MinionMove.Static,
                Interval = 0.6f, Damage = 10f, Range = 520f },  // shoots by Hydra.cs
            [MinionKind.Butterfly] = new MinionDef { Name = "Butterfly", Radius = 6f, Sides = 4, Color = new Rgba(1f, 0.84f, 0.43f), Move = MinionMove.Static },  // flown by WorldTree.cs
            [MinionKind.CloneWall] = new MinionDef { Name = "Shadow Clone", Radius = 16f, Sides = 8, Color = new Rgba(0.22f, 0.8f, 0.9f), Move = MinionMove.Static, BlockRadius = 16f, BlockCapacity = 50f },  // shoved by CloneWall.cs
            [MinionKind.Thrall] = new MinionDef { Name = "Thrall", Radius = 14f, Sides = 6, Color = new Rgba(0.75f, 0.55f, 1f), Move = MinionMove.Escort, Speed = 180f },  // ForcePact.cs: its shape, speed and attack come from the enemy type it was
            [MinionKind.Burrower] = new MinionDef { Name = "Burrower", Radius = 18f, Sides = 8, Color = new Rgba(0.64f, 0.54f, 0.38f), Move = MinionMove.Static,
                Speed = 190f, Damage = 30f, Range = 38f },  // tunnels and bites by Burrower.cs (Range: how close it has to pass to an enemy)
            [MinionKind.BroodQueen] = new MinionDef { Name = "Brood Queen", Radius = 24f, Sides = 8, Color = new Rgba(0.72f, 0.49f, 1f), Move = MinionMove.Static, Speed = 120f,
                BlockRadius = 26f, BlockCapacity = 900f },  // moved, biting and taunting by BroodQueen.cs (TickBroodQueen); BlockCapacity her health
            [MinionKind.Broodling] = new MinionDef { Name = "Broodling", Radius = 8f, Sides = 8, Color = new Rgba(0.72f, 0.49f, 1f), Move = MinionMove.Static, Speed = 240f },  // run and latched by BroodQueen.cs (TickBroodling)
            [MinionKind.Bee] = new MinionDef { Name = "Bee", Radius = 5f, Sides = 3, Color = new Rgba(1f, 0.77f, 0.24f), Move = MinionMove.Static },  // flown by HoneybeeStaff.cs (MoveBee)
            [MinionKind.VectorBlock] = new MinionDef { Name = "Vector Block", Radius = 24f, Sides = 4, Color = new Rgba(0.36f, 0.88f, 1f), Move = MinionMove.Static,
                BlockRadius = 24f, BlockCapacity = 400f },
            // the Engineer's robots (EngineerBots.cs TickBot): Range is how far they shoot, Interval their fire rate
            [MinionKind.TankBot] = new MinionDef { Name = "Tank Bot", Radius = 24f, Sides = 6, Color = new Rgba(1f, 0.71f, 0.28f), Move = MinionMove.Static, Speed = 115f,
                Interval = 0.75f, Damage = 6f, Range = 210f },
            [MinionKind.MedicBot] = new MinionDef { Name = "Medic Bot", Radius = 19f, Sides = 6, Color = new Rgba(0.44f, 0.94f, 0.66f), Move = MinionMove.Static, Speed = 150f,
                Interval = 0.4f, Damage = 9f, Range = 330f, HealRate = 9f },
            [MinionKind.AssaultBot] = new MinionDef { Name = "Assault Bot", Radius = 19f, Sides = 6, Color = new Rgba(0.36f, 0.88f, 1f), Move = MinionMove.Static, Speed = 175f,
                Interval = 0.22f, Damage = 11f, Range = 340f },
            [MinionKind.Wyvern] = new MinionDef { Name = "Wyvern", Radius = 22f, Sides = 6, Color = new Rgba(0.44f, 0.88f, 0.81f), Move = MinionMove.FollowOwner, Speed = 240f },  // flown by WyvernStaff.cs (MoveWyvern)
            [MinionKind.ArcShard] = new MinionDef { Name = "Orbit Shard", Radius = 7f, Sides = 4, Color = new Rgba(0.72f, 0.55f, 1f), Move = MinionMove.Static },   // flown by Arcanist.cs (MoveArcane)
            [MinionKind.ArcBlade] = new MinionDef { Name = "Dancing Blade", Radius = 8f, Sides = 3, Color = new Rgba(1f, 0.83f, 0.42f), Move = MinionMove.Static },  // flown by Arcanist.cs (MoveArcane)
            [MinionKind.BumbleBot] = new MinionDef { Name = "Bumble Bot", Radius = 22f, Sides = 6, Color = new Rgba(1f, 0.8f, 0.3f), Move = MinionMove.Static, Range = Game.BumbleRange },  // flown and fired by BumbleRemote.cs (MoveBumble)
            [MinionKind.Octopus] = new MinionDef { Name = "Octopus", Radius = 16f, Sides = 8, Color = Game.OctoColor, Move = MinionMove.Static },  // floated and its arms by OctopusFamiliar.cs; health in Minion.Hp (HasHealth, Stun.cs)
            [MinionKind.ClotCrystal] = new MinionDef { Name = "Clot Crystal", Radius = 9f, Sides = 6, Color = new Rgba(0.88f, 0.16f, 0.29f), Move = MinionMove.Static,
                BlockRadius = Game.CryCatch, BlockCapacity = Game.CryHp },  // moved by ClotCrystals.cs (TickClotCrystal); it eats any enemy shot it touches (ClotCatches); BlockCapacity its health
            [MinionKind.BloodImp] = new MinionDef { Name = "Blood Imp", Radius = 7f, Sides = 3, Color = new Rgba(0.86f, 0.1f, 0.2f), Move = MinionMove.Static, Speed = 260f,
                Attack = MinionAttack.Bite, Interval = 0.4f, Damage = 6f, BlockRadius = 10f, BlockCapacity = 30f },  // flown, clawing and taunting by BloodRitual.cs (TickBloodImp); BlockCapacity its health
        });

        // Indexed by ZoneKind.
        public static readonly ZoneDef[] Zones = IdTable.Of(new Dictionary<ZoneKind, ZoneDef>
        {
            [ZoneKind.FlameWall] = new ZoneDef { Name = "Flame Wall", Color = new Rgba(1f, 0.45f, 0.15f), Dps = 30f, BlockCapacity = 300f },
            [ZoneKind.Blizzard] = new ZoneDef { Name = "Blizzard", Color = new Rgba(0.6f, 0.85f, 1f), Dps = 20f, Slow = 0.6f },
            [ZoneKind.Meteor] = new ZoneDef { Name = "Meteor", Color = new Rgba(1f, 0.35f, 0.15f), BlastDamage = 150f },
            [ZoneKind.Sanctum] = new ZoneDef { Name = "Sanctum", Color = new Rgba(1f, 0.92f, 0.55f), PreventDown = true, BlockCapacity = 1500f },  // heals in pulses (Sanctum.cs), not by HealRate
            [ZoneKind.Grove] = new ZoneDef { Name = "World Tree", Color = new Rgba(0.4f, 0.9f, 0.4f), HealRate = 18f, Dps = 20f, Slow = 0.4f },
            [ZoneKind.Consecrate] = new ZoneDef { Name = "Consecrated Ground", Color = new Rgba(1f, 0.85f, 0.45f), HealRate = 12f, Dps = 20f },
            [ZoneKind.HolySword] = new ZoneDef { Name = "Hallowed Blade", Color = new Rgba(1f, 0.95f, 0.65f), BlastDamage = 200f },
            [ZoneKind.Smoke] = new ZoneDef { Name = "Smoke Bomb", Color = new Rgba(0.62f, 0.62f, 0.7f) },  // slows enemy bullets inside (Ninja.cs)
            [ZoneKind.Mist] = new ZoneDef { Name = "Healing Footprint", Color = new Rgba(0.55f, 1f, 0.8f) },  // a Healing Dash footprint: heals whoever stands in it (Ninja.cs)
            [ZoneKind.Ward] = new ZoneDef { Name = "Spirit Ward", Color = new Rgba(1f, 0.85f, 0.6f) },  // takes the damage allies near it would take, then pays some back as healing (Ninja.cs)
            [ZoneKind.BloodPool] = new ZoneDef { Name = "Blood Pool", Color = new Rgba(0.6f, 0.05f, 0.12f), Slow = 0.4f },  // a Gore Mortar's
            [ZoneKind.BoneSpear] = new ZoneDef { Name = "Bone Spear", Color = new Rgba(0.95f, 0.9f, 0.82f) },
            [ZoneKind.Incense] = new ZoneDef { Name = "Incense", Color = new Rgba(0.85f, 0.8f, 0.6f), HealRate = 4f },  // the Censer's puffs and clouds (Censer.cs); enemy shots fired inside it stray
            [ZoneKind.BlastOfuda] = new ZoneDef { Name = "Blast Ofuda", Color = new Rgba(1f, 0.35f, 0.3f) },  // a Blast Ofuda stuck to an enemy or lying lit on the ground (Ofuda.cs)
            [ZoneKind.HoneyPool] = new ZoneDef { Name = "Honey Pool", Color = new Rgba(1f, 0.75f, 0.2f), HealRate = 6f, Slow = 0.4f },  // Royal Dive's landing (QueenLoot.cs)  // an Ofuda charm stuck to the ground: bursts when an enemy steps on it (Ninja.cs)  // a thrown Bone Spear stuck in the ground: walk over it for its health back (Hemomancer.cs)
            [ZoneKind.HealingRain] = new ZoneDef { Name = "Healing Rain", Color = new Rgba(0.4f, 0.85f, 0.5f), HealRate = 8f, Slow = 0.3f },  // the Healer Elementalist's rain (Docs/HealingRain.md)
            [ZoneKind.BulletRain] = new ZoneDef { Name = "Bullet Rain", Color = new Rgba(0.47f, 0.69f, 0.94f), Dps = 45f },  // the Elementalist DPS ultimate (BulletRainLook.cs)
            [ZoneKind.LumenWard] = new ZoneDef { Name = "Lumen Ward", Color = new Rgba(1f, 0.85f, 0.45f), HealRate = 9f },  // a cone of light that follows the caster's aim (LumenWard.cs): heals and burns enemy bullets inside
            [ZoneKind.HallowedBlade] = new ZoneDef { Name = "Hallowed Blade", Color = new Rgba(1f, 0.95f, 0.65f), HealRate = 15f, Dps = 20f },  // the planted sword (HallowedBlade.cs): ring heals allies and burns enemies, and the whole arena gets a weaker pulse
            [ZoneKind.OathWard] = new ZoneDef { Name = "Ward of Light", Color = new Rgba(1f, 0.85f, 0.4f) },  // Guardian's Oath (GuardiansOath.cs): allies inside take less damage (Zone.DamageCut) and downed ones revive faster
            [ZoneKind.VeinTrail] = new ZoneDef { Name = "Vein Trail", Color = new Rgba(0.85f, 0.12f, 0.2f), Slow = 0.3f },  // the blood a Vein Rush leaves behind (VeinRush.cs): slows enemies on it
            [ZoneKind.Drawbridge] = new ZoneDef { Name = "Drawbridge", Color = new Rgba(0.48f, 0.33f, 0.2f) },  // the Tank Knight's key-2 ultimate (Drawbridge.cs): Pos is the hinge, Aim the way it fell
            [ZoneKind.BroodWeb] = new ZoneDef { Name = "Brood Web", Color = new Rgba(0.93f, 0.9f, 1f) },  // a Brood Queen web (BroodQueen.cs): Pos and Radius the web, slows by TickBroodWeb
            [ZoneKind.Bloodbath] = new ZoneDef { Name = "Bloodbath", Color = new Rgba(0.55f, 0.03f, 0.08f) },  // the Tank Hemomancer's second ultimate (Bloodbath.cs): slows, bleeds and guards by its own tick
            [ZoneKind.BloodWall] = new ZoneDef { Name = "Blood Wall", Color = new Rgba(0.7f, 0.07f, 0.14f) },  // one node of the tank Hemomancer's Blood Wall (BloodWall.cs): Aim is the node before it
            [ZoneKind.Phalanx] = new ZoneDef { Name = "Bone Phalanx", Color = new Rgba(0.93f, 0.9f, 0.8f) },  // the Tank Summoner's second ultimate (BonePhalanx.cs): Pos is the middle of the shield face, Aim the heading
            [ZoneKind.FlakMine] = new ZoneDef { Name = "Proximity Shell", Color = new Rgba(1f, 0.72f, 0.3f) },  // the Flak Cannon's Alt (TankAlts.cs): Radius is the burst, Aim the way it flew in
            [ZoneKind.TearGas] = new ZoneDef { Name = "Tear Gas", Color = new Rgba(0.92f, 0.9f, 0.7f) },  // the Riot Repeater's Alt (TankAlts.cs): suppresses and slows by its own tick
            [ZoneKind.OssuaryWall] = new ZoneDef { Name = "Ossuary Wall", Color = new Rgba(0.93f, 0.9f, 0.8f) },  // the Bone Guard's Alt (BoneGuard.cs): Pos the middle, Aim the way it was thrown, Radius half its length
            [ZoneKind.SmokeShell] = new ZoneDef { Name = "Smoke Shell", Color = new Rgba(0.37f, 0.83f, 0.74f) },  // the Grenade Launcher's smoke (GrenadeLauncher.cs): Aim the way it flew in, GuardFrac its flight distance; slows and jams by its own tick
            [ZoneKind.HoneyPuddle] = new ZoneDef { Name = "Honey Puddle", Color = new Rgba(1f, 0.71f, 0.18f), Slow = 0.4f },
            [ZoneKind.Flood] = new ZoneDef { Name = "Flood", Color = new Rgba(0.31f, 0.82f, 0.91f) },  // the Tank Elementalist's Flood (Flood.cs): slows and soaks by its own tick  // the Honeybee Staff's spray (HoneybeeStaff.cs): slows enemies; an enemy dying in it bursts into bees
            [ZoneKind.Cover] = new ZoneDef { Name = "Cover", Color = new Rgba(0.44f, 0.69f, 0.94f) },
            [ZoneKind.DragPylon] = new ZoneDef { Name = "Drag Pylon", Color = new Rgba(0.64f, 0.55f, 1f) },
            [ZoneKind.MedPylon] = new ZoneDef { Name = "Med Pylon", Color = new Rgba(0.44f, 0.94f, 0.66f), HealRate = 10f },  // the Healer Engineer's 1st ability: heals allies inside; stays until the next one
            [ZoneKind.SafeRoom] = new ZoneDef { Name = "Safe Room", Color = new Rgba(0.44f, 0.94f, 0.66f), HealRate = 20f },  // the Healer Engineer's ultimate: a room (or dome) that heals and cuts damage (Zone.DamageCut)  // the Engineer's 1st ability (Engineer.cs): slows enemies inside by its own tick; stays until the next one  // the Tank Soldier's Deployable Cover (DeployableCover.cs): Pos its middle, Aim the way it faces, GuardFrac its health left; blocks by CoverStops, not as a circle
            [ZoneKind.Stasis] = new ZoneDef { Name = "Stasis", Color = new Rgba(0.55f, 0.85f, 1f) },  // the Stasis Gun's field (EngineerBots.cs): at your cursor while you hold Fire; holds enemies in place and hurts them by its own tick
            [ZoneKind.Stampede] = new ZoneDef { Name = "Stampede", Color = new Rgba(0.5f, 0.88f, 0.67f) },  // the Tank Summoner's 4th choice (Stampede.cs): Pos the middle of the line (it moves), Aim the heading, Radius half its length
            [ZoneKind.DecoyDummy] = new ZoneDef { Name = "Decoy Dummy", Color = new Rgba(0.95f, 0.85f, 0.71f) },  // the Healer Soldier's 4th choice (DecoyDummy.cs): Pos where it stands, Aim and Marched its throw, GuardFrac its health left
            [ZoneKind.DeployTurret] = new ZoneDef { Name = "Deployable Turret", Color = new Rgba(1f, 0.78f, 0.29f) },  // the DPS Soldier's 4th choice (DeployableTurret.cs): Pos where it stands (you sit in it), Aim the way the guns point, Radius the seat
            [ZoneKind.ToxicGas] = new ZoneDef { Name = "Gas Grenade", Color = new Rgba(0.72f, 0.42f, 1f) },  // a Gas Grenade gadget's cloud (Gadgets.cs): lobbed in like Tear Gas, Aim the way it flew; hurts and slows by its own tick
            [ZoneKind.MendingWeb] = new ZoneDef { Name = "Mending Web", Color = new Rgba(0.87f, 0.91f, 1f) },  // the Healer Summoner's 1st choice (MendingWeb.cs): a band between two walls that heals allies, slows and poisons enemies (its own tick)
            [ZoneKind.GustGate] = new ZoneDef { Name = "Gust Gate", Color = new Rgba(0.78f, 0.96f, 1f) },  // the Healer Elementalist's 2nd choice (GustGate.cs): boosts players crossing its edge and pushes enemies out by its own tick
            [ZoneKind.GrandSigil] = new ZoneDef { Name = "Grand Sigil", Color = new Rgba(0.72f, 0.55f, 1f) },     // the Tank Arcanist's ultimate (Arcanist.cs): binds and taunts enemies inside, freezes enemy shots at its rim, sends them back as it ends
            [ZoneKind.SanctumSigil] = new ZoneDef { Name = "Sanctum Sigil", Color = new Rgba(1f, 0.85f, 0.45f), HealRate = 25f },  // the Healer Arcanist's ultimate (Arcanist.cs): follows its caster; Aim = the six runes left (a bit mask)
            [ZoneKind.ArcCrush] = new ZoneDef { Name = "Telekinetic Crush", Color = new Rgba(0.72f, 0.55f, 1f) },  // the DPS Arcanist's 1st ability (Arcanist.cs): lifts and pulls enemies in, then slams them together as it ends
            [ZoneKind.ShockGrenade] = new ZoneDef { Name = "Shock Grenade", Color = new Rgba(0.45f, 0.85f, 1f) },
            [ZoneKind.FieldHospital] = new ZoneDef { Name = "Field Hospital", Color = new Rgba(0.4f, 1f, 0.6f) },  // the Healer Soldier's second ultimate (FieldHospital.cs): Pos the cell's middle (or the tent's), Radius half the cell, Aim the way to its door (NaN: a tent), Side who has used its shrine (a bit per player id)  // the Tank Soldier's Scattergun Alt (TankAlts.cs): lobbed in, shocks and stuns on landing; Aim the way it flew in, Radius its reach
            [ZoneKind.Steam] = new ZoneDef { Name = "Steam", Color = new Rgba(0.9f, 0.93f, 0.95f) },  // fire + water (Steam.cs): scalds and jams by its own tick
            [ZoneKind.StaticResidue] = new ZoneDef { Name = "Static Residue", Color = new Rgba(0.5f, 0.91f, 1f) },  // a Living Bolt dash's live trail (LivingBolt.cs): a lane, Pos its middle, Aim its heading, Radius half its length; hurts by its own tick
            [ZoneKind.SupplyDrop] = new ZoneDef { Name = "Supply Drop", Color = new Rgba(0.4f, 1f, 0.55f) },  // the Healer Soldier's third ultimate (SupplyDrop.cs): Pos the crate, Radius the open zone; its timer gives the phase (flare, landing, open, folding)
            [ZoneKind.LandShark] = new ZoneDef { Name = "Land Shark", Color = new Rgba(0.36f, 0.62f, 0.92f) },  // the DPS Summoner's 4th choice (LandShark.cs): the puddle, Pos its middle, Radius its size, Aim the leap's heading
            [ZoneKind.Claymore] = new ZoneDef { Name = "Claymore", Color = new Rgba(1f, 0.48f, 0.3f) },  // the DPS Soldier's 3rd choice (Claymore.cs): Pos the mine, Aim its facing, Radius the trigger's reach, GuardFrac its state, Marched a mastered spare blast
            [ZoneKind.Mammoth] = new ZoneDef { Name = "Mammoth Fall", Color = new Rgba(0.65f, 0.49f, 1f) },  // the DPS Summoner's third ultimate (MammothFall.cs): Pos the mammoth, Origin where it lands, Radius the shockwave, Aim its heading, Side its prey
            [ZoneKind.ArmorDrop] = new ZoneDef { Name = "Armor Drop", Color = new Rgba(0.62f, 0.85f, 1f) },  // the Tank Soldier's fourth ultimate (ArmorDrop.cs): Pos the crate, Radius the landing's blast; its timer gives the phase (flare, landing, open, fold)
            [ZoneKind.TrojanHorse] = new ZoneDef { Name = "Trojan Horse", Color = new Rgba(0.69f, 0.48f, 0.27f) },  // the Tank Knight's second ultimate (TrojanHorse.cs): Pos the horse, Aim its heading, Radius its half width, BlockHp/SoakMax its health, Marched how far it rolled, Quiet parked
            [ZoneKind.Ballista] = new ZoneDef { Name = "Ballista", Color = Rgba.Gold },  // the DPS Knight's third ultimate (Ballista.cs): Pos the frame, Aim the bow, Radius the mount reach (0 = breaking), Rider, BoltsLeft, GunCd, GuardFrac its health
            [ZoneKind.SleepyCat] = new ZoneDef { Name = "Sleepy Cat", Color = new Rgba(1f, 0.8f, 0.35f) },  // the Healer Summoner's fourth ultimate (SleepyCat.cs): Pos the cat, Aim its heading, Radius the meow's reach, Marched its speed, Quiet napping, BoltsLeft meows left, GunCd since the last meow
            [ZoneKind.TortoiseOracle] = new ZoneDef { Name = "Tortoise Oracle", Color = new Rgba(0.37f, 0.88f, 0.73f) },  // the Healer Summoner's third ultimate (TortoiseOracle.cs): Pos the tortoise, Radius the circle; slows enemies and their shots, speeds up allies
            [ZoneKind.HoneyRaid] = new ZoneDef { Name = "Honey Raid", Color = new Rgba(1f, 0.7f, 0.15f) },  // the Healer Summoner's second ultimate (HoneyRaid.cs): Pos the pool, Origin where the bee came in, Radius the pool; slows, hurts and heals by its own tick
            [ZoneKind.Caltrop] = new ZoneDef { Name = "Caltrop", Color = new Rgba(0.62f, 0.85f, 1f) },  // the DPS Knight's Caltrop Run (CaltropRun.cs): one caltrop, Pos where it lands, Aim the way it bounced, GuardFrac the catches left
            [ZoneKind.Tusker] = new ZoneDef { Name = "Tidal Tusker", Color = new Rgba(0.39f, 0.89f, 0.82f) },  // the Tank Summoner's fourth ultimate (TidalTusker.cs): Pos the elephant, Aim its facing, Radius the stream's reach, GuardFrac its health, Origin/Origin2 where its streams end, Side/Rider what they hit
            [ZoneKind.RocketDrop] = new ZoneDef { Name = "Rocket Drop", Color = new Rgba(1f, 0.54f, 0.11f) },  // the DPS Soldier's fourth ultimate (RocketDrop.cs): Pos the pod, Radius the slam; its timer gives the phase (call-in, landing, open, fold); Side (host) 1 = mastered
            [ZoneKind.RocketFire] = new ZoneDef { Name = "Rocket Fire", Color = new Rgba(1f, 0.45f, 0.1f) },  // a Rocket Drop rocket's burning ground (RocketDrop.cs): Pos, Radius; TickT (host) the time to the next burn
            [ZoneKind.AegisStandard] = new ZoneDef { Name = "Aegis Standard", Color = new Rgba(0.62f, 0.78f, 1f) },  // the Tank Knight's banner ultimate (KnightBanners.cs): Pos the pole's foot, Radius the ring, Aim 1 folding, Cap mastered
            [ZoneKind.PilgrimsStandard] = new ZoneDef { Name = "Pilgrim's Standard", Color = new Rgba(1f, 0.89f, 0.6f) },  // the Healer Knight's banner ultimate (KnightBanners.cs); TickT (host) the time to the next heal
            [ZoneKind.WarStandard] = new ZoneDef { Name = "War Standard", Color = new Rgba(1f, 0.42f, 0.35f) },  // the DPS Knight's banner ultimate (KnightBanners.cs)
            [ZoneKind.GongSmoke] = new ZoneDef { Name = "Gong Smoke", Color = new Rgba(0.72f, 0.61f, 1f), Slow = Game.FsgSmokeSlow },  // a Five Shadow Gong shadow's smoke grenade (FiveShadowGong.cs): Pos, Radius; slows enemies in it, moving and shooting (Enemy.FsgSmokeT)
            [ZoneKind.HazardFoam] = new ZoneDef { Name = "Hazard Foam", Color = Game.FoamCream },  // a lump of the Healer Soldier's Hazard Foam (HazardFoam.cs): Pos, Radius; solid to everyone and every shot; Soak/SoakMax its health (GuardFrac replicated), Aim its bubbles' turn
            [ZoneKind.BloodTotem] = new ZoneDef { Name = "Blood Totem", Color = new Rgba(0.88f, 0.29f, 0.37f) },  // the Tank Hemomancer's totem (BloodTotem.cs): its veins slow by their own tick; Radius is the reach
            [ZoneKind.BloodSapling] = new ZoneDef { Name = "Blood Sapling", Color = Game.BgRed },  // a Healer Hemomancer's Blood Garden sapling (BloodGarden.cs): Pos, Radius its aura; Aim (replicated) the age its seed lands at; Soaked (host) health siphoned, GuardFrac its share of the bulb
            [ZoneKind.BloodRitual] = new ZoneDef { Name = "Blood Ritual", Color = new Rgba(0.86f, 0.1f, 0.2f) },  // the DPS Hemomancer's fourth ultimate (BloodRitual.cs): Pos the sigil, Radius its size, its age the phase; Side (host) imps poured out, TickT the next one, Landed opened, Cap mastered
        });

        public static MinionDef Get(MinionKind k) => Minions[(int)k];
        public static ZoneDef Get(ZoneKind k) => Zones[(int)k];
    }
}
