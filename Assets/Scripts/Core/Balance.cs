namespace Arena
{
    /// The game's general balance numbers in one place: change a value and rebuild. Host and client must run the
    /// same build (a changed number isn't sent over the network).
    ///
    /// Every class, boss and system keeps its own numbers in a block at the top of its file, beside the code that
    /// uses them:
    ///   Stats       Defs.cs (roles, weapons, enemies) · Kits.cs (minions, zones) · each ability's AbilityDef entry
    ///               (cooldown, health cost) in its class's file
    ///   Classes     Knight.cs · Ninja.cs · BloodMage.cs · Minions.cs (minions, Bear Totem, bleeding, chain lightning;
    ///               Mage and Summoner ability numbers are written in KitAbility) · Ultimates.cs (written in the code)
    ///   Weapons     Weapons.cs (revolver, bow, fanning, Twin Rifle) · Combat.cs (Homing Rounds) · Parry.cs ·
    ///               Stun.cs (Stun Gun, guard companion)
    ///   Enemies     EnemyAI.cs (Shielder, Spinner, Twin Reapers, the Warden's cross, gravity wells) · Warden.cs ·
    ///               Queen.cs · Singularity.cs · LateGame.cs (written in the code)
    ///   Runs        Upgrades.cs (cards) · Mastery.cs (XP per level) · Shop.cs (the Bazaar) · Paths.cs · Modifiers.cs
    public partial class Game
    {
        // --- Waves ---------------------------------------------------------------------------------------------------

        const float StartDelay = 3f;
        const float IntermissionTime = 4f;
        public const int MaxWave = 50;
        const int LateBossWave = 20;
        const float LateBossScaling = 0.1f;  // bosses after wave 20 get +10% health per wave on top (x2 at 30, x4 at 50)
        const float BossHpMult = 2.5f;       // every boss: fights last about 60-120 s (Boss Lab, Good bots, solo to trio)
        public const float EliteSize = 1.25f;

        // --- Level-up picks ------------------------------------------------------------------------------------------

        public const int StartingRerolls = 2;
        const int BossRerolls = 2;

        // --- Health: regeneration, health orbs, reviving -------------------------------------------------------------

        public const float PassiveRegen = 0.5f;  // HP per second for everyone
        const float OrbChance = 0.18f;      // small health orb drop chance per kill
        const float BigOrbChance = 0.03f;   // rare large health orb
        public const float OrbHeal = 15f;
        public const float TinyOrbHeal = 6f;   // the Repulsion Cannon knocks these loose (Repulsion.cs)
        public const float BigOrbHeal = 60f;
        public const float OrbLifetime = 12f;
        const float OrbMagnetRange = 110f;
        const float ReviveRange = 70f;
        const float ReviveTime = 3f;

        // --- Taking hits ---------------------------------------------------------------------------------------------

        const float ContactDamage = 15f;
        const float HitIframes = 0.35f;
        const float BossContactEvery = 1f, BossShoveTime = 0.15f, BossShoveSpeed = 700f;  // about 105 px out

        // --- The Healer's aura (every Healer heals allies near them) -------------------------------------------------

        public const float AuraRange = 170f;
        const float AuraHeal = 9f;

        // --- Soldier abilities, and the ones only very rare cards give -----------------------------------------------

        const float BarrierTime = 4f;
        const float BarrierCapacity = 360f;
        public const float BarrierRadius = 105f;
        const float PulseRange = 260f;
        const float PulseHeal = 60f;
        public const float DashTime = 0.18f;
        const float DashSpeed = 1200f;
        const float BulldozeSpeed = 750f, BulldozeTime = 0.35f, BulldozeDamage = 60f;
        const float RoarTime = 5f, RoarReduction = 0.4f;
        const float HavenRadius = 130f, HavenTime = 6f;
        const float LinkTime = 6f, LinkHeal = 8f;
        const float GrenadeRange = 380f, GrenadeFlight = 0.55f, GrenadeRadius = 110f, GrenadeDamage = 80f;
        const float MarkTime = 6f;
        const float BlinkRange = 300f;
        const float ShockwaveRange = 230f;
        const float ShockwaveDamage = 60f;
        const float OverdriveTime = 5f;
        const float TurretTime = 10f;

        // --- Mage abilities (the rest of their numbers are written in their cast code, Minions.cs) -------------------

        const float AegisCapacity = 300f;
        const float SpellRange = 420f;
    }
}
