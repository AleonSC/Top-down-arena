using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    public enum MatchState : byte { Idle, Fighting, Intermission, GameOver, Victory }

    public enum FxKind : byte { Boom, Spark, Ring, Bolt, Target, Slash, Jab, Shatter, Claw, Implode, Repulse }  // Target: a marker where something is about to land; Slash: a melee swing (Pos2 = tip, Radius = half the arc); Jab: the Lance's box (Pos2 = far end, Radius = half its width); Shatter: a parry shield breaking (Pos = its owner, Pos2 = the shield's middle, Radius = half its arc); Claw: a bear-claw swipe (Pos2 = reach, Radius = half the arc, signed by the way it rakes); Implode: blood collapsing into an enemy (Implosion)

    /// Sound cues raised by the game; the Unity layer plays them.
    public enum Sfx : byte
    {
        None, EnemyShot, Hit, Hurt, Explode, BigExplode, Ability, Shockwave, Blink,
        LevelUp, Pick, WaveStart, WaveClear, GameOver, Downed, Revive,
        // One gunshot per weapon, plus the turret and Nova.
        ShotShotgun, ShotCannon, ShotRifle, ShotSuppressed, ShotPistol, ShotBow, ShotAssault, ShotSniper, ShotSmg,
        ShotTurret, ShotNova,
        Pickup, PickupBig, Roar, Victory,
        SpellFire, SpellFrost, SpellZap, SpellArcane, SpellHoly, Throw, Summon, Freeze,
        SwingLight, SwingHeavy, Clang,  // Knight swings, and a shield taking a hit
        BossShift, BossCharge, BossPulse, BossChime,  // soft cues for bosses' abilities (BossSounds.cs)
    }

    public struct SoundCue
    {
        public Sfx Sfx;
        public bool Ally;  // your friend's shot: played quieter

        public static implicit operator SoundCue(Sfx s) => new SoundCue { Sfx = s };
    }

    public class AbilitySlot
    {
        public AbilityId Id = AbilityId.None;
        public int Charges, MaxCharges = 1;
        public float Timer;  // time until the next charge comes back
    }

    public class Player
    {
        public int Id;
        public Role Role;
        public RoleDef Def;
        public Kit Kit;
        public WeaponDef Weapon;
        public AbilityId RoleAbility;
        public float BaseHp;
        public string Name;
        public V2 Pos, Disp;
        public float Aim;
        public V2 AimAt;         // the point they're aiming at (Implosion picks the enemy under it; sent with inputs)
        public bool Firing;
        public bool FiringSeen;  // holding fire, as everyone's screen knows it (for drawing beams; synced in snapshots)
        public float Hp, MaxHp;
        public bool Downed;
        public float Revive, FireCd, BarrierT, InvulnT, OverdriveT, Flash, LevelFlash;
        public float TauntT;  // Roar: enemies target this player
        public float AegisT, SurgeT, FrenzyT;  // Arcane Aegis, Arcane Surge, Alpha Call
        public float WallT, UnbreakT, FuryT, WhirlT;  // Knight: Shield Wall, Unbreakable, Battle Fury, Whirlwind (replicated)
        public float VanishT, IronT;                  // Ninja: Vanish and Iron Body (replicated)
        public float AmbushT;                         // host: Vanish's double-damage hit is ready for this long
        public V2 VanishFrom;                         // host: where enemies last saw you before you vanished
        public float IronPower = 1f;                  // host: Iron Body's mastery, for its shockwave
        public bool IronMastered;
        public float PactT, RushT;                    // Blood Mage: Crimson Pact and Frenzy Pact (replicated)
        public int Clot, ClotMax = 5;                 // Blood Lash: Clot stacks and the most it can hold (replicated)
        public float ClotT;                           // host: until the next Clot stack fades
        public float PactStore, PactPower = 1f, PactRadius, ApoDealt;  // host: damage stored by the Pact; dealt during Crimson Apotheosis
        public bool PactMastered, RushMastered, ApoUp;
        public readonly List<int> Grasp = new List<int>();  // Leech Tendrils: the enemies held right now (replicated)
        public float WallHp, WhirlTick, WhirlPower = 1f;  // host
        public bool WallReflect, WhirlAll;               // host: mastered Shield Wall / Whirlwind
        public int SwingCount, RapierTarget, RapierStreak;  // host: Knight weapon state
        public float BossContactT;  // host: a boss's body can only hurt you once a second
        public float StunT;         // stunned by the Warden's stun shot: can't move (replicated; your own game stops you)
        public float UltT;                     // Fortress / Bullet Storm time left
        public int EchoLeft;                   // Riot Repeater: rounds still to fire in this burst
        public float EchoT, EchoGap;
        public int AmmoUsed;                   // Revolver: rounds fired since the last reload (replicated)
        public float ReloadT, ReloadIdle;      // Revolver: reload time left (replicated); how long since you last fired
        public bool WasFiring, TapQueued, Fanning;  // Revolver (host): tap vs hold; a click that came too early; fanning the hammer
        public float HeldT;                    // Revolver (host): how long the trigger has been held
        public float DrawT;                    // Mender's Bow: how long the current draw has been held (replicated; your own is local)
        public float BowRelease;               // Mender's Bow (host): a release waiting to fire, as its draw time; the shooter measures it
        public float BowShotDraw;              // Mender's Bow (host): the draw of the arrow being fired
        public bool Parrying;                  // holding the parry shield up (replicated)
        public float ParryHp = Game.ParryCapacity;  // damage the shield can still take (replicated)
        public float CounterT;                 // host: Counterstance fire-rate boost time left
        public float ParryBroken;              // seconds until a broken shield can be raised again (replicated)
        public float ParryHeld, ParryDown = 10f;  // host: how long it's been up / down
        public bool ParryPerfect;              // host: raised fresh, so the first moment reflects
        public float ParryBumpT;               // host: a melee hit on the shield counts once per bump
        public bool ParryActive => Parrying && ParryBroken <= 0f && !Downed;
        public float BarrierHp, BarrierMax, AegisHp;  // damage the Barrier / Aegis can still soak (host)
        public bool BarrierReflect;                   // host: a mastered Barrier sends bullets back
        public float AegisReflectMult = 1f;           // host: a mastered Aegis reflects twice as hard
        public Dictionary<AbilityId, float> MasteryXp = new Dictionary<AbilityId, float>();  // ability mastery (Mastery.cs)
        public float BarrierFrac;                     // replicated BarrierHp / BarrierMax, for drawing
        public float ShieldHp, ShieldT;        // Arcane Ward
        public float CompanionCd, TotemCd;     // host: summoner companion and totem timers
        public readonly float[] HpHistory = new float[16];   // host: Time Rewind (every 0.25 s)
        public readonly bool[] DownHistory = new bool[16];
        public int HistoryIdx;
        public float LinkT;   // Soul Link: protecting LinkTo
        public int LinkTo;

        public int Level = 1, PendingPicks, Rerolls = Game.StartingRerolls;
        public int Gold;                       // the Bazaar's gold (Shop.cs, replicated)
        public readonly int[] Shelf = { -1, -1, -1, -1, -1, -1 };  // this player's shop: upgrade ids (Game.ShelfSize, replicated)
        public byte ShelfSold;                 // bit per shelf slot: bought (replicated)
        public int Restocks;                   // shelf restocks this visit (replicated; each costs more)
        public bool ShopDone;                  // clicked Done shopping (replicated)
        public bool ReviveToken;               // bought at the Bazaar: get back up once by yourself (replicated)
        public float TokenT;                   // host: how long you've been down with a token
        public float Xp;
        public int[] Offer;  // three upgrade ids while choosing, otherwise null
        public bool OfferLegendary;  // the current offer is a boss reward
        public int PendingLegendary; // boss rewards still to pick (host keeps the queue below)
        public readonly List<EnemyType> LegendaryQueue = new List<EnemyType>();
        public int PendingMods;      // weapon mods still to pick (one per boss, after its legendary)
        public bool OfferMod;        // the current offer is the weapon mod list
        public int StunShots;        // Stun Gun: bolts left while it's out (replicated; Stun.cs)
        public float GuardCd;        // host: until the next Warden's Guard arrives
        public V2 LightPos, LightDisp;  // Searchlight: its pool (replicated), and where this screen draws it (Stun.cs)
        public RunStats Run = new RunStats();
        public int ReviveBy;         // host: who is reviving this downed player (credited when it completes)
        public float LifestealPool;  // host: Royal Jelly heals at most 10 HP per second
        public readonly List<int> Taken = new List<int>();
        public Stats Stats = new Stats();
        // Which bonus ability sits on Q, E and Shift (the player picks; host-authoritative, sent with the upgrades).
        public AbilityId[] BonusKeys = { AbilityId.None, AbilityId.None, AbilityId.None };
        public Dictionary<int, float> StatOverrides = new Dictionary<int, float>();  // dev menu: hand-edited stats (index into Game.StatFields)
        public bool DevGod;  // dev menu: takes no damage
        public readonly AbilitySlot[] Slots = { new AbilitySlot(), new AbilitySlot(), new AbilitySlot(), new AbilitySlot(), new AbilitySlot() };  // Upgrades.SlotCount

        public float Speed => Def.Speed * Stats.SpeedMult * Defs.KitSpeedMult(Kit);
        public float BarrierRadius => Game.BarrierRadius * Stats.BarrierRadiusMult;
        public float AuraRange => Game.AuraRange * Stats.AuraRangeMult;
    }

    public class Enemy
    {
        public int Id;
        public float LitT;      // host: seconds held in a Searchlight (below 0: just locked, resting) (Stun.cs)
        public EnemyType Type;
        public EnemyDef Def;
        public V2 Pos, Disp, Waypoint;
        public float Hp, MaxHp, Radius, Speed;
        public int Score;
        public float T, SpawnT, FireT, Ang, Strafe = 1f, RetargetT, Flash, T2;
        public float Facing;    // direction it faces (shields, charges)
        public byte Phase;      // per-type state, e.g. charger wind-up / dash, boss shield
        public float PhaseT;
        public bool Elite;
        public float MarkT, MarkMult = 1f;  // Mark for Death (host); Marked is the replicated flag
        public float SlowT, SlowMult = 1f;  // Graviton Rounds (host)
        public float BurnT, BurnDps;        // Incendiary Rounds (host)
        public int BurnBy;
        public float BleedT, BleedDps;      // Bear Totem's claws (host)
        public int BleedBy;
        public bool Burning, Chilled, Frozen, Bleeding;  // replicated, for drawing (Chilled = slowed but not frozen)
        public float FrozenT;               // Glacial Tomb: can't move or shoot (host)
        public float GraspT;                // Leech Tendrils: held, can't move or shoot (host)
        public int LastHitBy;
        public bool BloodDropped;  // host: a Sanguine Orb's kill has rolled for its health orb
        public bool Marked;
        public int TargetId, ParentId;
        public int CueSig = int.MinValue;  // host: the boss attack last announced with a sound
        public bool Spawning;  // replicated SpawnT > 0 on clients
        public WardenState Warden;  // the Warden's fight (Warden.cs; replicated)
        public QueenState Queen;    // the Hive Queen's fight (Queen.cs; replicated)
        public float ShedT;           // host: until the Repulsion Cannon can knock another health orb out of it
        public int SweepMark;         // host: the last Halberd sweep that hit it (once a sweep)
        public float ShieldHp, ShieldMax;  // host: a Shielder's shield, worn down by blocked hits and time (its Phase shows the cracks)
    }

    public class Bullet
    {
        public int Id;
        public byte Kind;
        public V2 Pos, Vel;
        public float AngVel, Life, Radius, Dmg;
        public float Life0;       // its lifetime when fired (for drawing it aging: the Flamethrower's flames)
        public int Owner, Pierce, LastHit;
        public bool FromWeapon;   // host only: fired by the player's gun (for accuracy)
        public float Splash;      // host only: explosion radius on impact
        public float HealAlly;    // host only: heals an ally it passes through
        public float HealBurst;   // host only: heals allies near where it bursts (the Ninja's Ofuda)
        public float HealOwner;   // host only: heals whoever fired it this much when it hits (Hemo Nova)
        public float Pool, PoolTime;  // host only: leaves a Blood Pool this big, for this long, where it hits (Clot Bolt)
        public int Burst;         // host only: mortar bombs burst into this many bullets when they land
        public int Chain;         // host only: lightning jumps to this many more enemies
        public float SlowAmt;     // host only: frost slows enemies hit
        public float Ignite;      // host only: sets enemies hit burning for this much a second (Flamethrower)
        public bool Pierced;      // host only: a Flamethrower flame that has hit its first enemy (it does less after)
        public bool FromBoss;     // fired by a boss (only these wear blockers down; drawn with a dark core)
        public float Homing;      // Homing Rounds: turn rate toward the nearest enemy (host corrects clients)
        public int Bounces;       // host only: Ricochet bounces left
        public int Frags;         // host only: on hit, throws this many fragments (Frost Lance, Railgun, Flak, Fireball)
        public byte FragKind;
        public float FragDmg, FragSpeed, FragLife, FragSplash;
        public bool FragSideways; // fragments fly out to the sides (shatter, shrapnel) rather than all around (bomblets)
        public bool Bomblet;      // host only: a Flak Cannon bomblet; it pops where it lands (Burst marks it as landing)
        public bool Pulled;       // host only: bent by a gravity well (so the host sends its course)
        public EnemyType? Source; // host only: the enemy that fired it (for the session record)
    }

    /// A health pickup dropped by a dead enemy.
    public class Orb
    {
        public int Id;
        public bool Big, Tiny;  // Tiny: knocked loose by the Repulsion Cannon
        public V2 Pos, Disp;
        public float Life;

        public float Radius => Big ? 11f : Tiny ? 4f : 6f;
        public float Heal => Big ? Game.BigOrbHeal : Tiny ? Game.TinyOrbHeal : Game.OrbHeal;
    }

    /// An area on the ground: Haven, Flame Wall, Blizzard, Soul Lantern, healing totem or a meteor's warning.
    public class Zone
    {
        public int Id, Owner;
        public ZoneKind Kind;
        public V2 Pos;
        public float Radius, T, Duration;
        public float DmgMult = 1f;  // host: owner's damage multiplier when cast
        public float HealMult = 1f; // host: the casting ability's mastery
        public float ExtraSlow;     // host: mastered Flame Wall also slows
        public bool Follow;         // host: mastered Soul Lantern moves with its owner
        public float DamageCut;     // host: mastered Haven: allies inside take less damage
        public bool Embers;         // host: a mastered Meteor leaves burning ground
        public bool Consecrates;    // host: Hallowed Blade blesses the ground where it lands
        public float BlockHp;       // host: damage it can still soak (blocking zones)
        public float GuardFrac;     // replicated BlockHp fraction, for drawing
        public ZoneDef Def => Kits.Get(Kind);
    }

    /// A friendly unit: turrets, summoner companions and summoned creatures.
    public class Minion
    {
        public int Id, Owner;
        public MinionKind Kind;
        public V2 Pos, Disp;
        public float Aim;
        public float T = -1f, Duration;  // seconds left; -1 = a companion that stays
        public float Cd, Ang, HealRate;
        public float BlockHp, StaggerT;  // host: damage it can still soak; a staggered companion doesn't block
        public float GuardFrac = 1f;     // replicated: 0 while staggered or broken
        public bool Companion;
        public float Scale = 1f;         // replicated: Treant Rod's treant grows with extra projectiles
        public float Power = 1f;         // host: mastery of the ability that summoned it
        public float Hp, MaxHp, HurtT;   // host: a Warden's Guard's or Soul Ghost's health, and its contact-damage cooldown
        public float InvulnT;            // host: a Soul Ghost from a mastered Soul Link can't be hurt at first
        public bool Mastered;            // host: summoned by a mastered ability (Phoenix, Guardian Spirit)
        public MinionDef Def => Kits.Get(Kind);
    }

    public class Effect
    {
        public FxKind Kind;
        public V2 Pos, Pos2;  // Pos2: the far end of a lightning bolt
        public Rgba Color;
        public float Radius, T, Dur;
    }

    public struct PlayerInput
    {
        public V2 Move;       // desired direction, length <= 1
        public V2 AimAt;      // point in arena coordinates
        public bool Fire;
        public bool Ability;  // held states; Game detects the presses
        public bool AbilityQ;
        public bool AbilityE;
        public bool AbilityShift;
        public bool Ultimate;
        public bool Parry;     // held: raise the parry shield toward the cursor
    }

    /// The match. The host simulates enemies, bullets, damage, XP and waves; every
    /// peer moves its own player locally and renders the shared state.
    ///
    /// Network traffic:
    ///   client -> host  Input (every tick: position/aim/firing), Ability, Pick
    ///   host -> all     Spawn / Remove (bullets fly deterministically, so only
    ///                   spawns and hits are sent), Snapshot (20 Hz: players,
    ///                   enemies, turrets and wave state), Upgrades (a player's
    ///                   picks, after each pick), Fx, ClearEnemyBullets
    public partial class Game
    {
        public const float Width = 1600f, Height = 900f;

        const int SnapshotEvery = 3;  // ticks
        float shoveT;
        V2 shoveDir;

        readonly Session session;
        readonly Random rng = NewRng();

        // Tests (Tools/SimTest) can make every game's random rolls repeatable: SeedForTests gives each game
        // made after it a seed drawn from one sequence. The real game never calls it, so it stays random.
        static Random testSeeds;

        public static void SeedForTests(int seed) => testSeeds = new Random(seed);

        static Random NewRng() => testSeeds != null ? new Random(testSeeds.Next()) : new Random();

        public bool Running;
        public MatchState State = MatchState.Idle;
        public int Wave, Remaining, Score;
        public float StateTimer;

        public readonly SortedDictionary<int, Player> Players = new SortedDictionary<int, Player>();
        public readonly Dictionary<int, Enemy> Enemies = new Dictionary<int, Enemy>();
        public readonly Dictionary<int, Bullet> Bullets = new Dictionary<int, Bullet>();
        public readonly Dictionary<int, Minion> Minions = new Dictionary<int, Minion>();
        public readonly Dictionary<int, Orb> Orbs = new Dictionary<int, Orb>();
        public readonly Dictionary<int, Zone> Zones = new Dictionary<int, Zone>();
        public readonly List<Effect> Effects = new List<Effect>();
        public readonly List<SoundCue> Sounds = new List<SoundCue>();  // drained by the front end each frame

        public float DashT, AnimT;
        float dashSpeed = DashSpeed;
        public V2 DashDir = new V2(1f, 0f);
        readonly bool[] abilityWasDown = new bool[Upgrades.SlotCount];
        bool awaitingPick;  // client sent a pick; ignore offers until the host confirms it
        int rerollsAtRequest = -1;  // client sent a reroll; block picks until new cards arrive

        public bool RerollPending => rerollsAtRequest >= 0;

        public bool Bot;  // drive the local player automatically (testing)
        V2 iceVel;        // your sliding velocity on the Frozen Lake path (Paths.cs)
        public bool HoldParry;  // keep the parry up (testing)

        /// Round trip to the host in milliseconds (0 when you're the host), measured once a second.
        public float PingMs;
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float pingT;

        /// For the aim debug overlay: the direction of your most recent shot, as the host fired it.
        public int LastShotId;
        public float LastShotAngle, LastShotAim;
        public V2 LastShotFrom;
        long lastShotTicks;

        /// Keep the shot from the newest volley that's closest to where you're aiming (spreads fire several).
        void NoteShot(Bullet b)
        {
            var me = Me;
            if (me == null)
                return;
            float off = Math.Abs(MathX.Fmod(b.Vel.Angle - me.Aim + MathX.Pi, MathX.Tau) - MathX.Pi);
            float prevOff = Math.Abs(MathX.Fmod(LastShotAngle - me.Aim + MathX.Pi, MathX.Tau) - MathX.Pi);
            bool newVolley = clock.ElapsedTicks - lastShotTicks > System.Diagnostics.Stopwatch.Frequency / 25;
            if (newVolley || off < prevOff)
            {
                LastShotId = b.Id;
                LastShotAngle = b.Vel.Angle;
                LastShotAim = me.Aim;  // what you were aiming at when it showed up
                LastShotFrom = b.Pos;
            }
            lastShotTicks = clock.ElapsedTicks;
        }

        /// Dev: when set (on the host), each match is recorded to a new file in this folder.
        public string RecordFolder;
        public SessionRecorder Recorder;
        public Action<Player, float, string> DamageEvent;  // host: every hit a player takes (Boss Lab)
        public bool BotRerolls = true;
        int botRerolledAt = -1;
        public Action<string> Log;
        float botT;

        // Previous values, to raise sounds when things change.
        MatchState lastState;
        readonly Dictionary<int, bool> lastDowned = new Dictionary<int, bool>();
        readonly Dictionary<int, int> lastLevel = new Dictionary<int, int>();
        float lastMyHp;

        // Host-only simulation state.
        int nextBulletId = 1, nextEnemyId = 1, nextMinionId = 1, nextOrbId = 1, nextZoneId = 1, tick;
        readonly List<EnemyType> spawnQueue = new List<EnemyType>();
        float spawnTimer;
        // Bullets spawned this tick, recorded as they were at spawn time (the host
        // moves them once before the batch is sent).
        readonly List<Bullet> pendingSpawns = new List<Bullet>();
        readonly List<int> pendingRemoved = new List<int>();

        public Game(Session session)
        {
            this.session = session;
        }

        public int MyId => session.MyId;
        bool IsHost => session.IsHost;

        public Player Me
        {
            get
            {
                Player p;
                Players.TryGetValue(MyId, out p);
                return p;
            }
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // --- Lifecycle ---------------------------------------------------------

        public void HostStart()
        {
            var w = Proto.Begin(Msg.Start);
            w.Write(session.StartWave);
            w.Write((byte)session.Lobby.Count);
            foreach (var e in session.Lobby.Values)
            {
                w.Write(e.Id);
                w.Write(e.Name);
                w.Write((byte)e.Role);
                w.Write((byte)e.Kit);
                w.Write(e.Weapon);
                w.Write(e.Ability);
            }
            var msg = Proto.End(w);
            session.Broadcast(msg);
            var r = new BinaryReader(new MemoryStream(msg));
            r.ReadByte();
            ReadStart(r);
        }

        void ReadStart(BinaryReader r)
        {
            Stop();
            PracticeIndex = -1;
            ResetPaths();
            int startWave = r.ReadInt32();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                string name = r.ReadString();
                var role = (Role)r.ReadByte();
                var kit = (Kit)r.ReadByte();
                int weapon = r.ReadByte();
                int ability = r.ReadByte();
                var def = Defs.Get(role);
                var p = new Player { Id = id, Role = role, Def = def, Name = name, Hp = def.Hp, MaxHp = def.Hp, Aim = -MathX.Pi / 2f };
                p.Kit = kit;
                p.Weapon = Defs.Weapon(role, kit, weapon < Defs.WeaponsPerRole ? weapon : 0);
                p.RoleAbility = Upgrades.RoleAbility(role, kit, ability);
                p.BaseHp = def.Hp * Defs.KitHpMult(kit);
                p.MaxHp = p.Hp = p.BaseHp;
                p.TotemCd = 2f;
                for (int k = 0; k < p.HpHistory.Length; k++)
                    p.HpHistory[k] = p.Hp;
                p.Pos = new V2(Width / 2f + (i - (n - 1) / 2f) * 90f, Height / 2f + 140f);
                p.Disp = p.Pos;
                Recalc(p);
                if (Has(RunMod.Lucky))
                    p.Rerolls += Modifiers.LuckyRerolls;
                Players[id] = p;
                lastDowned[id] = false;
                lastLevel[id] = 1;
            }
            Wave = Math.Max(0, startWave - 1);
            State = MatchState.Intermission;
            lastState = State;
            StateTimer = StartDelay;
            Running = true;
            lastMyHp = Me != null ? Me.Hp : 0f;
            session.Screen = AppScreen.Game;
            if (Recorder != null && !Recorder.Ended)
                Recorder.End(this, "restarted");  // a new match started over one still going: finish its file first
            Recorder = null;
            if (IsHost && RecordFolder != null)
            {
                Recorder = new SessionRecorder(RecordFolder);
                Recorder.Begin(this, startWave);
            }
            DoLog("game started with " + Players.Count + " player(s)");
        }

        /// Ends the match; `how` is what its recording says happened ("left", or "quit" when the game is closed).
        public void Stop(string how = "left")
        {
            if (Recorder != null && Running)
                Recorder.End(this, how);
            Running = false;
            State = MatchState.Idle;
            Wave = 0;
            Score = 0;
            Remaining = 0;
            Players.Clear();
            Enemies.Clear();
            Bullets.Clear();
            Minions.Clear();
            Orbs.Clear();
            Zones.Clear();
            Effects.Clear();
            Sounds.Clear();
            spawnQueue.Clear();
            pendingSpawns.Clear();
            pendingRemoved.Clear();
            lastDowned.Clear();
            lastLevel.Clear();
            nextBulletId = 1;
            nextEnemyId = 1;
            nextMinionId = 1;
            nextOrbId = 1;
            nextZoneId = 1;
            ResetBossState();
            DashT = 0f;
            Paused = false;
            PausedBy = 0;
            Endless = false;
            RunTime = 0f;
            RunStatsReady = false;
            Hazards.Clear();
            Cages.Clear();
            awaitingPick = false;
            rerollsAtRequest = -1;
            tick = 0;
        }

        public void OnPeerLeft(int id)
        {
            Players.Remove(id);
        }

        // --- Upgrades ----------------------------------------------------------

        /// Recompute a player's stats and ability slots from their picks. Runs on
        /// every peer, so all derived values agree without being sent.
        void Recalc(Player p)
        {
            p.Stats = Upgrades.Compute(p.Taken);
            ApplyStatOverrides(p);
            float oldMax = p.MaxHp;
            p.MaxHp = p.BaseHp + p.Stats.BonusHp;
            if (p.MaxHp > oldMax && !p.Downed)
                p.Hp += p.MaxHp - oldMax;

            var ids = new AbilityId[Upgrades.SlotCount];
            ids[0] = p.RoleAbility;
            for (int i = 1; i < ids.Length; i++)
                ids[i] = AbilityId.None;
            ids[Upgrades.UltSlot] = Upgrades.Ultimate(p.Role, p.Kit);
            // Bonus abilities go on the keys their owner chose; any others take the first free key.
            var granted = new List<AbilityId>();
            foreach (int t in p.Taken)
                if (Upgrades.All[t].Grants != AbilityId.None && granted.Count < Upgrades.BonusSlots && !granted.Contains(Upgrades.All[t].Grants))
                    granted.Add(Upgrades.All[t].Grants);
            for (int k = 0; k < Upgrades.BonusSlots; k++)
                if (granted.Contains(p.BonusKeys[k]) && Array.IndexOf(ids, p.BonusKeys[k]) < 0)
                    ids[1 + k] = p.BonusKeys[k];
            foreach (var g in granted)
                if (Array.IndexOf(ids, g) < 0)
                    for (int k = 1; k <= Upgrades.BonusSlots; k++)
                        if (ids[k] == AbilityId.None)
                        {
                            ids[k] = g;
                            break;
                        }
            for (int k = 0; k < Upgrades.BonusSlots; k++)
                p.BonusKeys[k] = ids[1 + k];

            // Charges and cooldowns follow an ability when it moves to another key.
            var moved = new Dictionary<AbilityId, AbilitySlot>();
            for (int i = 1; i <= Upgrades.BonusSlots; i++)
                if (p.Slots[i].Id != AbilityId.None)
                    moved[p.Slots[i].Id] = new AbilitySlot { Id = p.Slots[i].Id, Charges = p.Slots[i].Charges, MaxCharges = p.Slots[i].MaxCharges, Timer = p.Slots[i].Timer };

            for (int i = 0; i < ids.Length; i++)
            {
                var slot = p.Slots[i];
                var def = ids[i] == AbilityId.None ? null : Upgrades.Ability(ids[i]);
                int max = def == null ? 0 : i == Upgrades.UltSlot ? 1 : 1 + (i == 0 ? p.Stats.RoleCharges : p.Stats.ExtraCharges)
                    + def.ExtraCharges + (def.CapstoneCharge && Mastered(p, def.Id) ? 1 : 0);
                AbilitySlot was;
                if (slot.Id != ids[i] && ids[i] != AbilityId.None && moved.TryGetValue(ids[i], out was))
                {
                    slot.Id = ids[i];
                    slot.Charges = was.Charges;
                    slot.MaxCharges = was.MaxCharges;
                    slot.Timer = was.Timer;
                }
                if (slot.Id != ids[i])
                {
                    slot.Id = ids[i];
                    slot.Charges = max;
                    slot.Timer = 0f;
                    if (i == Upgrades.UltSlot)
                    {
                        // The ultimate starts half charged.
                        slot.Charges = 0;
                        slot.Timer = Upgrades.Ability(ids[i]).Cooldown * 0.5f;
                    }
                }
                else if (max > slot.MaxCharges)
                    slot.Charges += max - slot.MaxCharges;
                slot.MaxCharges = max;
                slot.Charges = Math.Min(slot.Charges, max);
            }
        }

        public float Cooldown(Player p, int slot)
        {
            var id = p.Slots[slot].Id;
            if (id == AbilityId.None)
                return 1f;
            if (slot == Upgrades.UltSlot)
                return Upgrades.Ability(id).Cooldown * MasteryCooldown(p, id);  // ultimates ignore cooldown upgrades
            float cd = Upgrades.Ability(id).Cooldown * p.Stats.CdMult * MasteryCooldown(p, id);
            if (id == AbilityId.Dash)
                cd *= p.Stats.DashCdMult;
            return cd;
        }

        void TickSlots(Player p, float dt)
        {
            for (int i = 0; i < p.Slots.Length; i++)
            {
                var s = p.Slots[i];
                if (s.Id == AbilityId.None || s.Charges >= s.MaxCharges || StunHeld(p, s))
                    continue;  // (the Stun Gun's cooldown waits until its shots are spent)
                s.Timer -= dt;
                if (s.Timer <= 0f)
                {
                    s.Charges++;
                    s.Timer = s.Charges < s.MaxCharges ? Cooldown(p, i) : 0f;
                }
            }
        }

        /// Spend a charge. lenient: accept presses that arrive slightly early (the
        /// owner tracks its own cooldowns, so the host's copy can lag a little).
        bool Consume(Player p, int slot, bool lenient)
        {
            var s = p.Slots[slot];
            if (s.Id == AbilityId.None)
                return false;
            if (s.Charges <= 0)
            {
                if (!lenient || s.Timer > 0.5f)
                    return false;
                s.Charges = 1;
                s.Timer = 0f;
            }
            s.Charges--;
            if (s.Timer <= 0f)
                s.Timer = Cooldown(p, slot);
            return true;
        }

        void GrantXp(float amount)
        {
            if (ShowcaseMode)
                return;  // no levelling up in the loadout preview
            foreach (var p in Players.Values)
                GrantXpTo(p, amount);
        }

        void GrantXpTo(Player p, float amount)
        {
            p.Xp += amount;
            while (p.Xp >= Defs.XpToNext(p.Level))
            {
                p.Xp -= Defs.XpToNext(p.Level);
                p.Level++;
                p.PendingPicks++;
            }
        }

        /// Called by the front end when the local player clicks an upgrade card.
        public void PickUpgrade(int index, int key = 0)
        {
            var me = Me;
            if (me == null || me.Offer == null || index < 0 || index >= me.Offer.Length || RerollPending)
                return;
            Sounds.Add(Sfx.Pick);
            if (IsHost)
            {
                HostPick(MyId, index, key);
            }
            else
            {
                var w = Proto.Begin(Msg.Pick);
                w.Write((byte)index);
                w.Write((byte)key);
                session.SendToHost(Proto.End(w));
                me.Offer = null;  // hide the cards until the host confirms
                awaitingPick = true;
            }
        }

        /// Called by the front end when the local player rerolls their cards.
        public void RerollOffer()
        {
            var me = Me;
            if (me == null || me.Offer == null || me.OfferLegendary || me.OfferMod || me.Rerolls <= 0 || RerollPending)
                return;
            Sounds.Add(Sfx.Pick);
            if (IsHost)
            {
                HostReroll(MyId);
            }
            else
            {
                rerollsAtRequest = me.Rerolls;
                session.SendToHost(Proto.End(Proto.Begin(Msg.Reroll)));
            }
        }

        public void HostReroll(int id)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Offer == null || p.OfferLegendary || p.OfferMod || p.Rerolls <= 0 || State != MatchState.Intermission)
                return;
            p.Rerolls--;
            Recorder?.Rerolled(p);
            p.Offer = Upgrades.RollOffer(rng, p.Role, p.RoleAbility, p.Taken, p.Level);
            DoLog(p.Role + " rerolled (" + p.Rerolls + " left)");
            SendSnapshot();
        }

        public void OnPick(int peer, BinaryReader r)
        {
            int index = r.ReadByte();
            HostPick(peer, index, r.ReadByte());
        }

        /// key: for a card that grants a new ability, the key to put it on (1 = Q, 2 = E, 3 = Shift; 0 = first free).
        void HostPick(int id, int index, int key = 0)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Offer == null || index < 0 || index >= p.Offer.Length || State != MatchState.Intermission)
                return;
            int upgrade = p.Offer[index];
            Recorder?.Picked(this, p, p.Offer, upgrade, p.OfferLegendary ? "legendary" : p.OfferMod ? "mod" : "level");
            if (p.OfferLegendary)
                p.LegendaryQueue.RemoveAt(0);
            else if (p.OfferMod)
                p.PendingMods = Math.Max(0, p.PendingMods - 1);
            else
                p.PendingPicks = Math.Max(0, p.PendingPicks - 1);
            PlaceOnKey(p.BonusKeys, Upgrades.All[upgrade].Grants, key);
            GrantUpgrade(id, upgrade);
            NextOffer(p);
            DoLog(p.Role + " picked " + Upgrades.All[upgrade].Name);
            SendSnapshot();  // show the next offer right away
        }

        /// Boss rewards come first (legendaries, then weapon mods), then regular level-up picks.
        void NextOffer(Player p)
        {
            p.PendingLegendary = p.LegendaryQueue.Count;
            p.OfferLegendary = p.LegendaryQueue.Count > 0;
            p.OfferMod = false;
            int[] mods = null;
            if (!p.OfferLegendary && p.PendingMods > 0)
            {
                mods = Upgrades.ModOffer(p.Role, p.RoleAbility, p.Taken);
                if (mods == null)
                    p.PendingMods = 0;  // every mod maxed out
            }
            if (p.OfferLegendary)
                p.Offer = Upgrades.LegendaryOffer(rng, p.LegendaryQueue[0], p.Role, p.RoleAbility, p.Taken);
            else if (mods != null)
            {
                p.Offer = mods;
                p.OfferMod = true;
            }
            else if (p.PendingPicks > 0)
                p.Offer = Upgrades.RollOffer(rng, p.Role, p.RoleAbility, p.Taken, p.Level);
            else
                p.Offer = null;
        }

        /// Host only: give a player an upgrade and tell everyone.
        public void GrantUpgrade(int id, int upgrade)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
            p.Taken.Add(upgrade);
            Recalc(p);
            BroadcastUpgrades(p);
        }

        void BroadcastUpgrades(Player p)
        {
            var w = Proto.Begin(Msg.Upgrades);
            w.Write(p.Id);
            w.Write((byte)p.Taken.Count);
            foreach (int t in p.Taken)
                w.Write((byte)t);
            foreach (var k in p.BonusKeys)
                w.Write((byte)k);
            session.Broadcast(Proto.End(w));
        }

        /// Put a bonus ability on a key (1 = Q, 2 = E, 3 = Shift). Whatever was there swaps to its old key.
        public static void PlaceOnKey(AbilityId[] k, AbilityId id, int key)
        {
            if (key < 1 || key > Upgrades.BonusSlots || id == AbilityId.None)
                return;
            int from = Array.IndexOf(k, id);
            var displaced = k[key - 1];
            k[key - 1] = id;
            if (from >= 0 && from != key - 1)
                k[from] = displaced;  // a swap
            else if (from < 0 && displaced != AbilityId.None)
            {
                int free = Array.IndexOf(k, AbilityId.None);  // a new ability: the old one moves to a free key
                if (free >= 0)
                    k[free] = displaced;
            }
        }

        /// The local player moves a bonus ability to another key (the Tab menu).
        public void SetAbilityKey(AbilityId id, int key)
        {
            var me = Me;
            if (me == null)
                return;
            if (IsHost)
            {
                HostSetKey(MyId, id, key);
                return;
            }
            var w = Proto.Begin(Msg.SetKey);
            w.Write((byte)id);
            w.Write((byte)key);
            session.SendToHost(Proto.End(w));
        }

        public void OnSetKey(int peer, BinaryReader r)
        {
            var id = (AbilityId)r.ReadByte();
            HostSetKey(peer, id, r.ReadByte());
        }

        void HostSetKey(int playerId, AbilityId id, int key)
        {
            Player p;
            if (!Players.TryGetValue(playerId, out p) || Array.IndexOf(p.BonusKeys, id) < 0)
                return;  // you can only move abilities you have
            PlaceOnKey(p.BonusKeys, id, key);
            Recalc(p);
            BroadcastUpgrades(p);
        }

        public bool AnyonePicking()
        {
            foreach (var p in Players.Values)
                if (p.PendingPicks > 0 || p.PendingLegendary > 0 || p.PendingMods > 0)
                    return true;
            return false;
        }

        // --- Frame loop --------------------------------------------------------

        /// Fixed-rate simulation step (60 Hz).
        public void FixedStep(float dt, PlayerInput input)
        {
            if (!Running)
                return;
            if (Paused)
            {
                // Frozen: the host keeps sending snapshots so clients stay up to date.
                if (IsHost && ++tick % SnapshotEvery == 0)
                    SendSnapshot();
                return;
            }
            LocalControl(dt, input);
            if (!IsHost && (pingT -= dt) <= 0f)
            {
                pingT = 1f;
                var pw = Proto.Begin(Msg.Ping);
                pw.Write(clock.ElapsedTicks);
                session.SendToHost(Proto.End(pw));
            }
            if (IsHost)
            {
                if (State == MatchState.Fighting || State == MatchState.Intermission)
                    RunTime += dt;
                HostTick(dt);
            }
            else
            {
                MoveBullets(dt);
                tick++;
                if (tick % 120 == 0)
                    DoLog(string.Format("wave {0} state {1} enemies {2} bullets {3} hp {4}", Wave, State, Enemies.Count, Bullets.Count, HpSummary()));
            }
            AgePatches(dt);  // Paths.cs (every game, so the floors look the same everywhere)
            TickArenaRules(dt);
            foreach (var e in Enemies.Values)
                e.Flash = Math.Max(0f, e.Flash - dt);
            foreach (var p in Players.Values)
            {
                p.Flash = Math.Max(0f, p.Flash - dt);
                p.LevelFlash = Math.Max(0f, p.LevelFlash - dt);
            }
        }

        /// Per-frame visual update: smoothing, effects and sound cues.
        public void Update(float dt)
        {
            if (!Running)
                return;
            AnimT += dt;
            TickShopNotices(dt);
            UpdateWardenView(dt);  // Warden.cs
            UpdateQueenView(dt);   // Queen.cs
            UpdateReaperView(dt);  // Reapers.cs
            StepLightDisplay(dt);  // Searchlights (Stun.cs)
            foreach (var p in Players.Values)
                p.StunT = Math.Max(0f, p.StunT - dt);
            float k = 1f - (float)Math.Exp(-18.0 * dt);
            foreach (var p in Players.Values)
                // Snap instead of sliding across the screen when the player wraps around an edge.
                p.Disp = p.Id == MyId || Math.Abs(p.Disp.X - p.Pos.X) > Width / 2f || Math.Abs(p.Disp.Y - p.Pos.Y) > Height / 2f
                    ? p.Pos : V2.Lerp(p.Disp, p.Pos, k);
            foreach (var e in Enemies.Values)
                e.Disp = V2.Lerp(e.Disp, e.Pos, k);
            foreach (var o in Orbs.Values)
                o.Disp = IsHost ? o.Pos : V2.Lerp(o.Disp, o.Pos, k);
            foreach (var m in Minions.Values)
                m.Disp = IsHost ? m.Pos : V2.Lerp(m.Disp, m.Pos, k);
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                Effects[i].T += dt;
                if (Effects[i].T >= Effects[i].Dur)
                    Effects.RemoveAt(i);
            }

            if (State != lastState)
            {
                if (State == MatchState.Fighting)
                    Sounds.Add(Sfx.WaveStart);
                else if (State == MatchState.Intermission && lastState == MatchState.Fighting)
                    Sounds.Add(Sfx.WaveClear);
                else if (State == MatchState.GameOver)
                    Sounds.Add(Sfx.GameOver);
                else if (State == MatchState.Victory)
                    Sounds.Add(Sfx.Victory);
                lastState = State;
            }
            foreach (var p in Players.Values)
            {
                bool wasDown;
                if (lastDowned.TryGetValue(p.Id, out wasDown) && wasDown != p.Downed)
                    Sounds.Add(p.Downed ? Sfx.Downed : Sfx.Revive);
                lastDowned[p.Id] = p.Downed;
                int lvl;
                if (lastLevel.TryGetValue(p.Id, out lvl) && p.Level > lvl)
                {
                    p.LevelFlash = 1.5f;
                    if (p.Id == MyId)
                        Sounds.Add(Sfx.LevelUp);
                }
                lastLevel[p.Id] = p.Level;
            }
            var me = Me;
            if (me != null)
            {
                if (me.Hp < lastMyHp - 0.5f && !me.Downed)
                    Sounds.Add(Sfx.Hurt);
                lastMyHp = me.Hp;
            }
        }

        void LocalControl(float dt, PlayerInput input)
        {
            var me = Me;
            if (me == null)
                return;
            if (!IsHost)
                TickSlots(me, dt);  // the host ticks everyone's slots in TickPlayers
            V2 move = input.Move.LimitLength(1f);
            V2 aimAt = input.AimAt;
            V2 before = me.Pos;
            me.Aim = (aimAt - me.Pos).Angle;
            me.Parrying = (input.Parry || HoldParry) && !me.Downed;
            bool firing = input.Fire && !me.Downed && !me.ParryActive;  // can't shoot behind the shield
            bool[] held = { input.Ability, input.AbilityQ, input.AbilityE, input.AbilityShift, input.Ultimate };

            if (Bot && BotLevel != BotSkill.Off)
            {
                botT += dt;
                SmartBot(me, dt, ref move, ref aimAt, ref firing, held);  // BotBrain.cs
            }
            else if (Bot)
            {
                botT += dt;
                move = V2.FromAngle(botT * 0.9f + MyId);
                Enemy nearest = null;
                foreach (var e in Enemies.Values)
                    if (!SoulBound(e) && (nearest == null || e.Pos.DistanceSq(me.Pos) < nearest.Pos.DistanceSq(me.Pos)))
                        nearest = e;
                if (nearest != null)
                {
                    aimAt = nearest.Pos;
                    me.Aim = (aimAt - me.Pos).Angle;
                }
                firing = !me.Downed && (!ChargeWeapon(me.Weapon.Id) || MathX.Fmod(botT, 1.2f) < 1f);  // bow and Warhammer: charge, then let go
                for (int i = 0; i < held.Length; i++)
                    held[i] = MathX.Fmod(botT + i * 1.3f, 4f) < 0.1f;
            }
            if (Bot)
            {
                // Both bots: level-up picks (reroll once per pick while rerolls last, then pick at random) and the Bazaar.
                if (me.Offer != null && MathX.Fmod(botT, 0.5f) < dt)
                {
                    if (BotRerolls && me.Rerolls > 0 && botRerolledAt != me.Taken.Count)
                    {
                        botRerolledAt = me.Taken.Count;
                        RerollOffer();
                    }
                    else
                        PickUpgrade(rng.Next(me.Offer.Length));
                }
                else if (me.Offer == null && ShopOpen && !me.ShopDone && MathX.Fmod(botT, 0.5f) < dt)
                    BotShop(me);  // Shop.cs
            }

            float bowRelease = LocalBow(me, firing, dt);
            if (me.Downed)
                DashT = 0f;
            else if (DashT > 0f)
            {
                DashT -= dt;
                me.Pos += DashDir * dashSpeed * dt;
                iceVel = DashDir * me.Speed * Paths.IceTopSpeed;  // on ice you slide out of a dash
            }
            else
            {
                V2 want = move * me.Speed * (me.ParryActive && !me.Stats.ParryNoSlow ? ParrySlow : 1f) * (bowDraw >= 0f ? BowDrawSlow : 1f) * HoneySlowAt(me.Pos);
                if (OnPathOf(PathId.FrozenLake))
                {
                    // Frozen Lake (Paths.cs): speed builds toward where you're heading and carries on when you let go.
                    iceVel += (want * Paths.IceTopSpeed - iceVel) * Math.Min(1f, Paths.IceGrip * dt);
                    me.Pos += iceVel * dt;
                }
                else
                {
                    iceVel = want;
                    me.Pos += want * dt;
                }
            }
            if (me.Downed)
                iceVel = V2.Zero;
            if (PullStrength > 0f && !me.Downed && DashT <= 0f)
                me.Pos += (PullCenter - me.Pos).Normalized() * PullStrength * dt;
            if (!me.Downed && DashT <= 0f)
                me.Pos += WellDriftAt(me.Pos) * dt;  // the Singularity's corner wells (Singularity.cs)
            if (me.StunT > 0f && !me.Downed)
            {
                me.Pos = before;  // stunned by the Warden: stuck where you are
                DashT = 0f;
            }
            WardenGatePush(me, before);  // the Warden's gates shove you back (Warden.cs)
            // Touching a boss shoves you back out (it only hurts once a second; see TickEnemies).
            if (!me.Downed && shoveT <= 0f)
                foreach (var e in Enemies.Values)
                    if (e.Def.IsBoss && !IsSpawning(e) && !QueenAirborne(e) && me.Pos.DistanceTo(e.Pos) < e.Radius + me.Def.HitR + 2f)
                    {
                        V2 away = me.Pos - e.Pos;
                        shoveDir = away.IsZero ? V2.FromAngle(me.Aim + MathX.Pi) : away.Normalized();
                        shoveT = BossShoveTime;
                        break;
                    }
            if (shoveT > 0f)
            {
                shoveT -= dt;
                me.Pos += shoveDir * BossShoveSpeed * dt;
            }

            for (int slot = 0; slot < held.Length; slot++)
            {
                bool pressed = held[slot] && !abilityWasDown[slot];
                abilityWasDown[slot] = held[slot];
                if (!pressed || me.Downed || State == MatchState.GameOver)
                    continue;
                if (me.Slots[slot].Id == AbilityId.Mark && !AnyTarget())
                    continue;  // nothing to mark: don't waste the cooldown
                if (!Consume(me, slot, false))
                    continue;
                V2 target = aimAt;
                switch (me.Slots[slot].Id)
                {
                    case AbilityId.Dash:
                        DashDir = move.IsZero ? V2.FromAngle(me.Aim) : move.Normalized();
                        DashT = DashTime;
                        dashSpeed = DashSpeed;
                        break;
                    case AbilityId.LeapSlam:
                    {
                        // Leap to the cursor (up to 320 px); the host lands the slam there.
                        target = ClampToArena(me.Pos + (aimAt - me.Pos).LimitLength(LeapRange), me.Def.BodyR);
                        float dist = target.DistanceTo(me.Pos);
                        DashDir = dist > 1f ? (target - me.Pos) / dist : V2.Zero;
                        DashT = LeapTime;
                        dashSpeed = dist / LeapTime;
                        break;
                    }
                    case AbilityId.Lunge:
                    {
                        V2 dir = (aimAt - me.Pos).IsZero ? V2.FromAngle(me.Aim) : (aimAt - me.Pos).Normalized();
                        float range = LungeRange * me.Stats.LungeRangeMult;
                        target = ClampToArena(me.Pos + dir * range, me.Def.BodyR);
                        DashDir = dir;
                        DashT = target.DistanceTo(me.Pos) / LungeSpeed;
                        dashSpeed = LungeSpeed;
                        break;
                    }
                    case AbilityId.Bulldoze:
                        DashDir = V2.FromAngle(me.Aim);
                        DashT = BulldozeTime;
                        dashSpeed = BulldozeSpeed;
                        target = ClampToArena(me.Pos + DashDir * BulldozeSpeed * BulldozeTime, me.Def.BodyR);  // where the charge ends
                        break;
                    case AbilityId.Grenade:
                        target = ClampToArena(me.Pos + (aimAt - me.Pos).LimitLength(GrenadeRange), 10f);
                        break;
                    case AbilityId.FlameWall:
                    case AbilityId.Meteor:
                    case AbilityId.Blizzard:
                        target = ClampToArena(me.Pos + (aimAt - me.Pos).LimitLength(SpellRange), 10f);
                        break;
                    case AbilityId.ShadowStep:
                        target = ShadowStepLanding(me, aimAt);  // Ninja.cs: behind the enemy nearest your cursor
                        AddFx(FxKind.Ring, me.Pos, new Rgba(0.45f, 0.4f, 0.65f), 26f, 0.3f);
                        me.Pos = target;
                        DashT = 0f;
                        break;
                    case AbilityId.Blink:
                        target = me.Pos + (aimAt - me.Pos).LimitLength(BlinkRange);
                        target = ClampToArena(target, me.Def.BodyR);
                        AddFx(FxKind.Ring, me.Pos, new Rgba(0.7f, 0.6f, 1f), 30f, 0.3f);
                        me.Pos = target;
                        DashT = 0f;
                        break;
                }
                if (IsHost)
                {
                    HostAbility(MyId, slot, target, true);
                }
                else
                {
                    var w = Proto.Begin(Msg.Ability);
                    w.Write((byte)slot);
                    w.Write(target);
                    session.SendToHost(Proto.End(w));
                }
            }

            me.Pos = KeepInArena(me.Pos, me.Def.BodyR);
            me.FiringSeen = firing;
            me.AimAt = aimAt;
            if (IsHost)
            {
                me.Firing = firing;
                if (bowRelease > 0f)
                    me.BowRelease = bowRelease;
            }
            else
            {
                var w = Proto.Begin(Msg.Input);
                w.Write(me.Pos);
                w.Write(me.Aim);
                w.Write(me.AimAt);
                w.Write(firing);
                w.Write(me.Parrying);
                w.Write(me.DrawT);
                w.Write(bowRelease);
                session.SendToHost(Proto.End(w));
            }
        }

        bool AnyTarget()
        {
            foreach (var e in Enemies.Values)
                if (!IsSpawning(e))
                    return true;
            return false;
        }

        V2 ClampToArena(V2 pos, float r) => pos.Clamp(ArenaMin + new V2(r, r), ArenaMax - new V2(r, r));

        /// Players wrap around: walking off one edge brings you in on the opposite side.
        public static V2 WrapArena(V2 pos) => new V2(MathX.Fmod(pos.X, Width), MathX.Fmod(pos.Y, Height));

        // --- Host simulation ---------------------------------------------------

        void HostTick(float dt)
        {
            tickDt = dt;
            Recorder?.Tick(this, dt);
            TickPlayers(dt);
            TickKnights(dt);  // Knight.cs
            TickNinjas(dt);   // Ninja.cs
            TickBloodMages(dt);  // BloodMage.cs
            TickWaves(dt);
            TickPaths(dt);    // Paths.cs: hazard floors
            TickEnemies(dt);
            TickMinions(dt);
            TickSearchlights(dt);  // Stun.cs
            TickOrbs(dt);
            MoveBullets(dt);
            Collide();
            CheckGameOver();

            if (pendingSpawns.Count > 0)
            {
                var w = Proto.Begin(Msg.Spawn);
                w.Write(pendingSpawns.Count);
                foreach (var b in pendingSpawns)
                {
                    w.Write(b.Id);
                    w.Write(b.Kind);
                    w.Write(b.Owner);
                    w.Write(b.Pos);
                    w.Write(b.Vel);
                    w.Write(b.AngVel);
                    w.Write(b.Life);
                    w.Write((byte)((b.FromBoss ? 1 : 0) | (b.Homing > 0f ? 2 : 0)));
                    if (b.Homing > 0f)
                        w.Write(b.Homing);
                }
                session.Broadcast(Proto.End(w));
                pendingSpawns.Clear();
            }
            if (pendingRemoved.Count > 0)
            {
                var w = Proto.Begin(Msg.Remove);
                w.Write(pendingRemoved.Count);
                foreach (int id in pendingRemoved)
                    w.Write(id);
                session.Broadcast(Proto.End(w));
                pendingRemoved.Clear();
            }
            tick++;
            if (tick % SnapshotEvery == 0)
            {
                SendSnapshot();
                SendSteer();
            }
            if (tick % 120 == 0)
                DoLog(string.Format("wave {0} state {1} enemies {2} bullets {3} score {4} hp {5}", Wave, State, Enemies.Count, Bullets.Count, Score, HpSummary()));
        }

        void TickPlayers(float dt)
        {
            foreach (var p in Players.Values)
            {
                TickSlots(p, dt);
                p.BarrierT = Math.Max(0f, p.BarrierT - dt);
                p.InvulnT = Math.Max(0f, p.InvulnT - dt);
                p.OverdriveT = Math.Max(0f, p.OverdriveT - dt);
                p.TauntT = Math.Max(0f, p.TauntT - dt);
                p.AegisT = Math.Max(0f, p.AegisT - dt);
                p.SurgeT = Math.Max(0f, p.SurgeT - dt);
                p.FrenzyT = Math.Max(0f, p.FrenzyT - dt);
                p.UltT = p.Downed ? 0f : Math.Max(0f, p.UltT - dt);
                TickParry(p, dt);
                p.ShieldT = Math.Max(0f, p.ShieldT - dt);
                if (p.ShieldT <= 0f)
                    p.ShieldHp = 0f;
                p.LifestealPool = Math.Min(10f, p.LifestealPool + 10f * dt);
                p.LinkT = Math.Max(0f, p.LinkT - dt);
                if (p.Downed)
                    p.TauntT = p.LinkT = 0f;
                if (p.LinkT > 0f)
                {
                    Player q;
                    float heal = LinkHeal * p.Stats.LinkHealMult * dt;
                    Heal(p, heal, p);
                    if (Players.TryGetValue(p.LinkTo, out q) && q != p)
                        Heal(q, heal, p);
                    else
                        HealSoulGhost(p, heal);  // alone: the link heals your ghost (SoulGhost.cs)
                }
                p.FireCd -= dt;
                if (p.Downed)
                    continue;
                if (p.StunShots > 0)
                    TickStunGun(p);  // the Stun Gun is out instead of your weapon (Stun.cs)
                else if (p.Weapon.Id == WeaponId.Pistol)
                {
                    TickRevolver(p, dt);
                    RevolverTrigger(p, dt);
                }
                else if (ChargeWeapon(p.Weapon.Id))
                    TickBow(p);  // the bow and the Warhammer fire when you let go
                else if (p.Firing && !p.ParryActive && p.FireCd <= 0f && State != MatchState.GameOver && ImplodeReady(p))
                {
                    p.FireCd = p.Weapon.FireInterval / FireRate(p);
                    FirePlayer(p);
                }
                // Riot Repeater: the rest of the burst.
                if (p.EchoLeft > 0)
                {
                    if (p.ParryActive)
                        p.EchoLeft = 0;
                    for (p.EchoT -= dt; p.EchoLeft > 0 && p.EchoT <= 0f; p.EchoT += p.EchoGap)
                    {
                        p.EchoLeft--;
                        FireFanned(p, p.Aim, 0);
                    }
                }
                if (!OnPathOf(PathId.BloodMoon))  // Blood Moon (Paths.cs): no regeneration
                {
                    Recorder?.HealReceived(p, Math.Min((PassiveRegen + p.Stats.Regen) * dt, p.MaxHp - p.Hp), "regeneration");
                    p.Hp = Math.Min(p.MaxHp, p.Hp + (PassiveRegen + p.Stats.Regen) * dt);
                }
                if (p.Role == Role.Healer)
                {
                    Recorder?.HealReceived(p, Math.Min(3f * dt, p.MaxHp - p.Hp), "regeneration");
                    p.Hp = Math.Min(p.MaxHp, p.Hp + 3f * dt);
                    foreach (var q in Players.Values)
                        if (q != p && q.Pos.DistanceTo(p.Pos) < p.AuraRange)
                            Heal(q, AuraHeal * p.Stats.AuraHealMult * dt, p);
                }
            }

            TickZones(dt);
            RecordHistory(dt);

            // Standing next to a downed ally slowly brings them back.
            foreach (var p in Players.Values)
            {
                TickReviveToken(p, dt);  // Shop.cs
                if (!p.Downed)
                    continue;
                Player helper = null;
                foreach (var q in Players.Values)
                    if (q != p && !q.Downed && q.Pos.DistanceTo(p.Pos) < ReviveRange)
                        helper = q;
                if (helper != null)
                {
                    p.Revive += dt / ReviveTime;
                    p.ReviveBy = helper.Id;
                }
                else if (p.ReviveBy == 0)
                    p.Revive = Math.Max(0f, p.Revive - dt * 0.25f);
                // A Haven or other zone can finish the revive on its own (it sets ReviveBy to its owner).
                if (p.Revive >= 1f)
                    RevivePlayer(p, 0.35f, PlayerOrNull(p.ReviveBy));
                p.ReviveBy = 0;
            }
        }

        // --- Helpers -----------------------------------------------------------

        public bool IsSpawning(Enemy e) => IsHost ? e.SpawnT > 0f : e.Spawning;

        void AddFx(FxKind kind, V2 pos, Rgba color, float radius, float dur)
        {
            Effects.Add(new Effect { Kind = kind, Pos = pos, Color = color, Radius = radius, Dur = dur });
        }

        public string HpSummary()
        {
            var parts = new List<string>();
            foreach (var p in Players.Values)
                parts.Add(p.Role + ":" + (int)p.Hp + (p.Downed ? "(down)" : "") + "/L" + p.Level);
            return string.Join(" ", parts);
        }

        void DoLog(string msg)
        {
            if (Log != null)
                Log("[" + (IsHost ? "host" : "client") + "] " + msg);
        }
    }
}
