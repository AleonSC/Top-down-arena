namespace Arena
{
    /// Every weapon Alt id: the kind byte of Msg.WeaponDash (LocalWeaponDash / HostWeaponDash, Ninja.cs). This is the one
    /// registry of taken numbers. The values go over the network, so never renumber one; a new Alt takes the next free
    /// number (43 is free: it was the Strider; the next new one is 67). The "classes" sim test (WeaponAltIdsTest,
    /// Tools/SimTest/Tests/Classes.cs) fails on a repeated value.
    public static class WeaponAlt
    {
        public const byte WhirlOn = 1;            // Kusarigama: whirl on
        public const byte Strike = 2;             // Twin Blades / Greatsword: Twin Fang, Moonwheel
        public const byte Blink = 3;              // Kunai: blink to the mark
        public const byte Recall = 4;             // Shuriken: the stars come back
        public const byte KatanaStance = 5;       // Katana: stance
        public const byte Shove = 6;              // Bo Staff: shove
        public const byte Jab = 7;                // Quarterstaff: jab
        public const byte WhirlOff = 10;          // Kusarigama: whirl off
        public const byte HammerSlam = 11;        // Storm Hammer: slam
        public const byte Lance = 12;             // Lightbringer: lance
        public const byte Cast = 13;              // Censer: cast
        public const byte Hook = 14;              // Lance: hook
        public const byte Nade = 15;              // Twin Rifle: grenade
        public const byte Flourish = 16;          // Rapier: Flourish
        public const byte RapierOn = 17;          // Rapier: Riposte on
        public const byte RapierOff = 18;         // Rapier: Riposte off
        public const byte PullOn = 19;            // Repulsion / Blood Lash: pull on
        public const byte PullOff = 20;           // Repulsion / Blood Lash: pull off
        public const byte Double = 21;            // tank Alt (TankAlts.cs)
        public const byte Mine = 22;              // tank Alt (TankAlts.cs)
        public const byte Gas = 23;               // tank Alt (TankAlts.cs)
        public const byte FumaStance = 24;        // Fuma Shuriken: stance switch
        public const byte KnuckleStance = 25;     // Arc Knuckles: stance
        public const byte Lock = 26;              // Twin Rifle Auto: lock
        public const byte Spin = 27;              // Out of Ammo: spin
        public const byte Both = 28;              // Double Barrel: both barrels
        public const byte BoneWall = 29;          // Bone Guard: bone wall (TankAlts.cs)
        public const byte Volley = 30;            // Stonespear: volley
        public const byte Pack = 31;              // Wolf Pack
        public const byte Fork = 32;              // Arc Lock: fork
        public const byte Storm = 33;             // Nimbus: Eye of the Storm
        public const byte Drum = 34;              // Grenade Launcher: smoke
        public const byte HoneyOn = 35;           // Honeybee Staff: on
        public const byte HoneyOff = 36;          // Honeybee Staff: off
        public const byte NunGuard = 37;          // Nunchaku: guard
        public const byte Tempest = 38;           // Wind Clash: Tempest
        public const byte Embolus = 39;           // Embolus Spears
        public const byte BraceOn = 40;           // Bulwark Barrier: brace on
        public const byte BraceOff = 41;          // Bulwark Barrier: brace off
        public const byte RevStance = 42;         // Revolver: Pistol Whip stance (PistolWhip.cs)
        public const byte Zero = 44;              // Frost Lance: Absolute Zero
        public const byte Sympathy = 45;          // Blood Brawler: Sympathy
        public const byte Tide = 46;              // Blood Blast: Tide
        public const byte Ignite = 47;            // Chalk: ignite
        public const byte NodStance = 48;         // Nodachi: stance
        public const byte WyvernSlam = 49;        // Wyvern Staff: slam
        public const byte Laser = 50;             // Twin Pistols: laser
        public const byte VgStance = 51;          // Vowguard: stance
        public const byte ShardOn = 52;           // Arcanist: Shard Guard on
        public const byte ShardOff = 53;          // Arcanist: Shard Guard off
        public const byte BladeRecall = 54;       // Arcanist: the blades come back
        public const byte HeatDump = 55;          // Rail Beam: heat dump
        public const byte ArcCannon = 56;         // Static Shock: Arc Cannon
        public const byte Scatter = 57;           // Bow: scatter (BowFocus.cs)
        public const byte PikeOn = 58;            // Halberd: pike on
        public const byte PikeOff = 59;           // Halberd: pike off
        public const byte Bolt = 60;              // Living Bolt
        public const byte Ravager = 61;           // Blood Ravager
        public const byte Needles = 62;           // Needle Swarm
        public const byte Ofuda = 63;             // Ofuda
        public const byte TessenOn = 64;          // Twin Tessen: on
        public const byte TessenOff = 65;         // Twin Tessen: off
        public const byte ArmBash = 66;           // Iron Armadillo: bash
        public const byte BumbleBurst = 67;       // Bumble Remote: burst
        public const byte HiveCall = 68;          // Hive Censer: call
        public const byte Bloodlet = 69;          // Acupuncture Needles: bloodletting
    }
}
