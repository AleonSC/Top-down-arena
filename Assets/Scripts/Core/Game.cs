using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    public enum MatchState : byte { Idle, Fighting, Intermission, GameOver }

    public enum FxKind : byte { Boom, Spark, Ring, Bolt }

    /// Sound cues raised by the game; the Unity layer plays them.
    public enum Sfx : byte
    {
        None, EnemyShot, Hit, Hurt, Explode, BigExplode, Ability, Shockwave, Blink,
        LevelUp, Pick, WaveStart, WaveClear, GameOver, Downed, Revive,
        // One gunshot per weapon, plus the turret and Nova.
        ShotShotgun, ShotCannon, ShotRifle, ShotSuppressed, ShotWisp, ShotBow, ShotAssault, ShotSniper, ShotSmg,
        ShotTurret, ShotNova,
        Pickup, PickupBig, Roar,
        SpellFire, SpellFrost, SpellZap, SpellArcane, SpellHoly, Throw, Summon, Freeze,
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
        public bool Firing;
        public float Hp, MaxHp;
        public bool Downed;
        public float Revive, FireCd, BarrierT, InvulnT, OverdriveT, Flash, LevelFlash;
        public float TauntT;  // Roar: enemies target this player
        public float AegisT, SurgeT, FrenzyT;  // Arcane Aegis, Arcane Surge, Alpha Call
        public float ShieldHp, ShieldT;        // Arcane Ward
        public float CompanionCd, TotemCd;     // host: summoner companion and totem timers
        public readonly float[] HpHistory = new float[16];   // host: Time Rewind (every 0.25 s)
        public readonly bool[] DownHistory = new bool[16];
        public int HistoryIdx;
        public float LinkT;   // Soul Link: protecting LinkTo
        public int LinkTo;

        public int Level = 1, PendingPicks, Rerolls = Game.StartingRerolls;
        public float Xp;
        public int[] Offer;  // three upgrade ids while choosing, otherwise null
        public bool OfferLegendary;  // the current offer is a boss reward
        public int PendingLegendary; // boss rewards still to pick (host keeps the queue below)
        public readonly List<EnemyType> LegendaryQueue = new List<EnemyType>();
        public int Volleys;          // host: shots fired, for Warden's Halo
        public float LifestealPool;  // host: Royal Jelly heals at most 10 HP per second
        public readonly List<int> Taken = new List<int>();
        public Stats Stats = new Stats();
        public readonly AbilitySlot[] Slots = { new AbilitySlot(), new AbilitySlot(), new AbilitySlot(), new AbilitySlot() };

        public float Speed => Def.Speed * Stats.SpeedMult;
        public float BarrierRadius => Game.BarrierRadius * Stats.BarrierRadiusMult;
        public float AuraRange => Game.AuraRange * Stats.AuraRangeMult;
    }

    public class Enemy
    {
        public int Id;
        public EnemyType Type;
        public EnemyDef Def;
        public V2 Pos, Disp, Waypoint;
        public float Hp, MaxHp, Radius, Speed;
        public int Score;
        public float T, SpawnT, FireT, Ang, Strafe = 1f, RetargetT, Flash;
        public float Facing;    // direction it faces (shields, charges)
        public byte Phase;      // per-type state, e.g. charger wind-up / dash, boss shield
        public float PhaseT;
        public bool Elite;
        public float MarkT, MarkMult = 1f;  // Mark for Death (host); Marked is the replicated flag
        public float SlowT, SlowMult = 1f;  // Graviton Rounds (host)
        public int LastHitBy;
        public bool Marked;
        public int TargetId, ParentId;
        public bool Spawning;  // replicated SpawnT > 0 on clients
    }

    public class Bullet
    {
        public int Id;
        public byte Kind;
        public V2 Pos, Vel;
        public float AngVel, Life, Radius, Dmg;
        public int Owner, Pierce, LastHit;
        public float Splash;      // host only: explosion radius on impact
        public float HealAlly;    // host only: heals an ally it passes through
        public int Burst;         // host only: mortar bombs burst into this many bullets when they land
        public int Chain;         // host only: lightning jumps to this many more enemies
        public float SlowAmt;     // host only: frost slows enemies hit
    }

    /// A health pickup dropped by a dead enemy.
    public class Orb
    {
        public int Id;
        public bool Big;
        public V2 Pos, Disp;
        public float Life;

        public float Radius => Big ? 11f : 6f;
        public float Heal => Big ? Game.BigOrbHeal : Game.OrbHeal;
    }

    /// An area on the ground: Haven, Flame Wall, Blizzard, Soul Lantern, healing totem or a meteor's warning.
    public class Zone
    {
        public int Id, Owner;
        public ZoneKind Kind;
        public V2 Pos;
        public float Radius, T, Duration;
        public float DmgMult = 1f;  // host: owner's damage multiplier when cast
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
        public bool Companion;
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
        public const float Width = 1280f, Height = 720f;

        const int SnapshotEvery = 3;  // ticks
        public const int StartingRerolls = 2;
        const float OrbChance = 0.18f;      // small health orb drop chance per kill
        const float BigOrbChance = 0.03f;   // rare large health orb
        public const float OrbHeal = 15f;
        public const float BigOrbHeal = 60f;
        public const float OrbLifetime = 12f;
        const float OrbMagnetRange = 110f;
        const int BossRerolls = 2;
        const float PassiveRegen = 0.5f;  // HP per second for everyone
        const float StartDelay = 3f;
        const float IntermissionTime = 4f;
        const float ContactDamage = 15f;
        const float HitIframes = 0.35f;
        const float ReviveRange = 70f;
        const float ReviveTime = 3f;
        public const float AuraRange = 170f;
        const float AuraHeal = 9f;
        const float BarrierTime = 4f;
        public const float BarrierRadius = 105f;
        const float PulseRange = 260f;
        const float PulseHeal = 60f;
        public const float DashTime = 0.18f;
        const float DashSpeed = 1200f;
        const float BulldozeSpeed = 750f, BulldozeTime = 0.35f, BulldozeDamage = 60f;
        const float RoarTime = 5f, RoarReduction = 0.4f;
        const float HavenRadius = 130f, HavenTime = 6f, HavenHeal = 15f;
        const float LinkTime = 6f, LinkHeal = 8f;
        const float GrenadeRange = 380f, GrenadeFlight = 0.55f, GrenadeRadius = 110f, GrenadeDamage = 80f;
        const float MarkTime = 6f;
        const float SpellRange = 420f;
        const float BlinkRange = 300f;
        const float ShockwaveRange = 230f;
        const float ShockwaveDamage = 60f;
        const float OverdriveTime = 5f;
        const float TurretTime = 10f;

        readonly Session session;
        readonly Random rng = new Random();

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
            DoLog("game started with " + Players.Count + " player(s)");
        }

        public void Stop()
        {
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
            float oldMax = p.MaxHp;
            p.MaxHp = p.BaseHp + p.Stats.BonusHp;
            if (p.MaxHp > oldMax && !p.Downed)
                p.Hp += p.MaxHp - oldMax;

            var ids = new AbilityId[Upgrades.SlotCount];
            ids[0] = p.RoleAbility;
            for (int i = 1; i < ids.Length; i++)
                ids[i] = AbilityId.None;
            int next = 1;
            foreach (int t in p.Taken)
                if (Upgrades.All[t].Grants != AbilityId.None && next < ids.Length)
                    ids[next++] = Upgrades.All[t].Grants;

            for (int i = 0; i < ids.Length; i++)
            {
                var slot = p.Slots[i];
                int max = ids[i] == AbilityId.None ? 0 : 1 + (i == 0 ? p.Stats.RoleCharges : p.Stats.ExtraCharges);
                if (slot.Id != ids[i])
                {
                    slot.Id = ids[i];
                    slot.Charges = max;
                    slot.Timer = 0f;
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
            float cd = Upgrades.Ability(id).Cooldown * p.Stats.CdMult;
            if (id == AbilityId.Dash)
                cd *= p.Stats.DashCdMult;
            return cd;
        }

        void TickSlots(Player p, float dt)
        {
            for (int i = 0; i < p.Slots.Length; i++)
            {
                var s = p.Slots[i];
                if (s.Id == AbilityId.None || s.Charges >= s.MaxCharges)
                    continue;
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
            foreach (var p in Players.Values)
            {
                p.Xp += amount;
                while (p.Xp >= Defs.XpToNext(p.Level))
                {
                    p.Xp -= Defs.XpToNext(p.Level);
                    p.Level++;
                    p.PendingPicks++;
                }
            }
        }

        /// Called by the front end when the local player clicks an upgrade card.
        public void PickUpgrade(int index)
        {
            var me = Me;
            if (me == null || me.Offer == null || index < 0 || index >= me.Offer.Length || RerollPending)
                return;
            Sounds.Add(Sfx.Pick);
            if (IsHost)
            {
                HostPick(MyId, index);
            }
            else
            {
                var w = Proto.Begin(Msg.Pick);
                w.Write((byte)index);
                session.SendToHost(Proto.End(w));
                me.Offer = null;  // hide the cards until the host confirms
                awaitingPick = true;
            }
        }

        /// Called by the front end when the local player rerolls their cards.
        public void RerollOffer()
        {
            var me = Me;
            if (me == null || me.Offer == null || me.OfferLegendary || me.Rerolls <= 0 || RerollPending)
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
            if (!Players.TryGetValue(id, out p) || p.Offer == null || p.OfferLegendary || p.Rerolls <= 0 || State != MatchState.Intermission)
                return;
            p.Rerolls--;
            p.Offer = Upgrades.RollOffer(rng, p.Role, p.RoleAbility, p.Taken, p.Level);
            DoLog(p.Role + " rerolled (" + p.Rerolls + " left)");
            SendSnapshot();
        }

        public void OnPick(int peer, BinaryReader r)
        {
            HostPick(peer, r.ReadByte());
        }

        void HostPick(int id, int index)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Offer == null || index < 0 || index >= p.Offer.Length || State != MatchState.Intermission)
                return;
            int upgrade = p.Offer[index];
            if (p.OfferLegendary)
                p.LegendaryQueue.RemoveAt(0);
            else
                p.PendingPicks = Math.Max(0, p.PendingPicks - 1);
            GrantUpgrade(id, upgrade);
            NextOffer(p);
            DoLog(p.Role + " picked " + Upgrades.All[upgrade].Name);
            SendSnapshot();  // show the next offer right away
        }

        /// Boss rewards come first, then regular level-up picks.
        void NextOffer(Player p)
        {
            p.PendingLegendary = p.LegendaryQueue.Count;
            p.OfferLegendary = p.LegendaryQueue.Count > 0;
            if (p.OfferLegendary)
                p.Offer = Upgrades.LegendaryOffer(rng, p.LegendaryQueue[0], p.Role, p.RoleAbility, p.Taken);
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
            var w = Proto.Begin(Msg.Upgrades);
            w.Write(p.Id);
            w.Write((byte)p.Taken.Count);
            foreach (int t in p.Taken)
                w.Write((byte)t);
            session.Broadcast(Proto.End(w));
        }

        public bool AnyonePicking()
        {
            foreach (var p in Players.Values)
                if (p.PendingPicks > 0 || p.PendingLegendary > 0)
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
            if (IsHost)
            {
                HostTick(dt);
            }
            else
            {
                MoveBullets(dt);
                tick++;
                if (tick % 120 == 0)
                    DoLog(string.Format("wave {0} state {1} enemies {2} bullets {3} hp {4}", Wave, State, Enemies.Count, Bullets.Count, HpSummary()));
            }
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
            float k = 1f - (float)Math.Exp(-18.0 * dt);
            foreach (var p in Players.Values)
                p.Disp = p.Id == MyId ? p.Pos : V2.Lerp(p.Disp, p.Pos, k);
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
            me.Aim = (aimAt - me.Pos).Angle;
            bool firing = input.Fire && !me.Downed;
            bool[] held = { input.Ability, input.AbilityQ, input.AbilityE, input.AbilityShift };

            if (Bot)
            {
                botT += dt;
                move = V2.FromAngle(botT * 0.9f + MyId);
                Enemy nearest = null;
                foreach (var e in Enemies.Values)
                    if (nearest == null || e.Pos.DistanceSq(me.Pos) < nearest.Pos.DistanceSq(me.Pos))
                        nearest = e;
                if (nearest != null)
                {
                    aimAt = nearest.Pos;
                    me.Aim = (aimAt - me.Pos).Angle;
                }
                firing = !me.Downed;
                for (int i = 0; i < held.Length; i++)
                    held[i] = MathX.Fmod(botT + i * 1.3f, 4f) < 0.1f;
                if (me.Offer != null && MathX.Fmod(botT, 0.5f) < dt)
                {
                    // Reroll once per pick while rerolls last, then pick at random.
                    if (BotRerolls && me.Rerolls > 0 && botRerolledAt != me.Taken.Count)
                    {
                        botRerolledAt = me.Taken.Count;
                        RerollOffer();
                    }
                    else
                        PickUpgrade(rng.Next(me.Offer.Length));
                }
            }

            if (me.Downed)
                DashT = 0f;
            else if (DashT > 0f)
            {
                DashT -= dt;
                me.Pos += DashDir * dashSpeed * dt;
            }
            else
                me.Pos += move * me.Speed * dt;
            if (PullStrength > 0f && !me.Downed && DashT <= 0f)
                me.Pos += (PullCenter - me.Pos).Normalized() * PullStrength * dt;

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

            me.Pos = ClampToArena(me.Pos, me.Def.BodyR);
            if (IsHost)
            {
                me.Firing = firing;
            }
            else
            {
                var w = Proto.Begin(Msg.Input);
                w.Write(me.Pos);
                w.Write(me.Aim);
                w.Write(firing);
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

        static V2 ClampToArena(V2 pos, float r) => pos.Clamp(new V2(r, r), new V2(Width - r, Height - r));

        public void OnInput(int peer, BinaryReader r)
        {
            V2 pos = r.ReadV2();
            float aim = r.ReadSingle();
            bool firing = r.ReadBoolean();
            Player p;
            if (!Players.TryGetValue(peer, out p))
                return;
            if (!p.Downed)
                p.Pos = ClampToArena(pos, p.Def.BodyR);
            p.Aim = aim;
            p.Firing = firing;
        }

        public void OnAbility(int peer, BinaryReader r)
        {
            int slot = r.ReadByte();
            V2 target = r.ReadV2();
            if (slot < Upgrades.SlotCount)
                HostAbility(peer, slot, target, false);
        }

        // --- Host simulation ---------------------------------------------------

        void HostTick(float dt)
        {
            TickPlayers(dt);
            TickWaves(dt);
            TickEnemies(dt);
            TickMinions(dt);
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
                SendSnapshot();
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
                    p.Hp = Math.Min(p.MaxHp, p.Hp + heal);
                    if (Players.TryGetValue(p.LinkTo, out q) && !q.Downed && q != p)
                        q.Hp = Math.Min(q.MaxHp, q.Hp + heal);
                }
                p.FireCd -= dt;
                if (p.Downed)
                    continue;
                if (p.Firing && p.FireCd <= 0f && State != MatchState.GameOver)
                {
                    p.FireCd = p.Weapon.FireInterval / p.Stats.FireRateMult / (p.OverdriveT > 0f ? 2f : 1f);
                    FirePlayer(p);
                }
                p.Hp = Math.Min(p.MaxHp, p.Hp + (PassiveRegen + p.Stats.Regen) * dt);
                if (p.Role == Role.Healer)
                {
                    p.Hp = Math.Min(p.MaxHp, p.Hp + 3f * dt);
                    foreach (var q in Players.Values)
                        if (q != p && !q.Downed && q.Pos.DistanceTo(p.Pos) < p.AuraRange)
                            q.Hp = Math.Min(q.MaxHp, q.Hp + AuraHeal * p.Stats.AuraHealMult * dt);
                }
            }

            TickZones(dt);
            RecordHistory(dt);

            // Standing next to a downed ally slowly brings them back.
            foreach (var p in Players.Values)
            {
                if (!p.Downed)
                    continue;
                bool helped = false;
                foreach (var q in Players.Values)
                    if (q != p && !q.Downed && q.Pos.DistanceTo(p.Pos) < ReviveRange)
                        helped = true;
                if (helped)
                {
                    p.Revive += dt / ReviveTime;
                    if (p.Revive >= 1f)
                        RevivePlayer(p, 0.35f);
                }
                else
                    p.Revive = Math.Max(0f, p.Revive - dt * 0.25f);
            }
        }

        void FirePlayer(Player p)
        {
            float dmg = p.Stats.DmgMult;
            p.Volleys++;
            if (p.Stats.HaloEvery > 0 && p.Volleys % p.Stats.HaloEvery == 0)
                for (int i = 0; i < 8; i++)
                {
                    V2 d = V2.FromAngle(p.Aim + MathX.Tau * i / 8f);
                    SpawnBullet(Defs.NovaShot, p.Pos + d * p.Def.BodyR, d * 450f, 0f, 0.8f, 9f * dmg, p.Id);
                }
            // Volley 0 is straight ahead; Multishot adds volleys fanned out to alternating sides.
            for (int v = 0; v <= p.Stats.ExtraShots; v++)
            {
                float aim = p.Aim + (v == 0 ? 0f : (v % 2 == 1 ? 1f : -1f) * 0.14f * ((v + 1) / 2));
                V2 dir = V2.FromAngle(aim);
                V2 muzzle = p.Pos + dir * p.Def.BodyR;
                switch (p.Weapon.Id)
                {
                    case WeaponId.Scattergun:
                        for (int i = 0; i < 5; i++)
                            SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + (i - 2) * 0.13f) * 520f, 0f, 0.5f, 7f * dmg, p.Id);
                        break;
                    case WeaponId.FlakCannon:
                        SpawnBullet(Defs.FlakShell, muzzle, dir * 380f, 0f, 1.1f, 30f * dmg, p.Id).Splash = 70f;
                        break;
                    case WeaponId.RiotRepeater:
                        SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + Rand(-0.06f, 0.06f)) * 600f, 0f, 0.8f, 11f * dmg, p.Id);
                        break;
                    case WeaponId.LightBolt:
                        SpawnBullet(Defs.HealerShot, muzzle, dir * 650f, 0f, 1.3f, 11f * dmg, p.Id);
                        break;
                    case WeaponId.TwinWisps:
                        // Start angled outward and curve back in, crossing about 200 px ahead.
                        SpawnBullet(Defs.WispShot, muzzle, V2.FromAngle(aim - 0.45f) * 420f, 1.9f, 0.9f, 10f * dmg, p.Id);
                        SpawnBullet(Defs.WispShot, muzzle, V2.FromAngle(aim + 0.45f) * 420f, -1.9f, 0.9f, 10f * dmg, p.Id);
                        break;
                    case WeaponId.MendersBow:
                        SpawnBullet(Defs.HealerShot, muzzle, dir * 600f, 0f, 1.3f, 9f * dmg, p.Id).HealAlly = 7f;
                        break;
                    case WeaponId.TwinRifle:
                        V2 side = dir.Orthogonal() * 5f;
                        SpawnBullet(Defs.DpsShot, muzzle + side, dir * 820f, 0f, 1f, 6.5f * dmg, p.Id);
                        SpawnBullet(Defs.DpsShot, muzzle - side, dir * 820f, 0f, 1f, 6.5f * dmg, p.Id);
                        break;
                    case WeaponId.Railgun:
                        SpawnBullet(Defs.RailShot, muzzle, dir * 1600f, 0f, 0.7f, 45f * dmg, p.Id).Pierce += 3;
                        break;
                    case WeaponId.BurstSmg:
                        SpawnBullet(Defs.DpsShot, muzzle, V2.FromAngle(aim + Rand(-0.16f, 0.16f)) * 760f, 0f, 0.8f, 5f * dmg, p.Id);
                        break;
                    default:
                        FireKitWeapon(p, aim, dir, muzzle, dmg);  // mage and summoner weapons (Minions.cs)
                        break;
                }
            }
        }

        void HostAbility(int id, int slot, V2 target, bool consumed)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Downed)
                return;
            if (!consumed && !Consume(p, slot, true))
                return;
            var s = p.Stats;
            switch (p.Slots[slot].Id)
            {
                case AbilityId.Barrier:
                    p.BarrierT = BarrierTime + s.BarrierBonusTime;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.5f, 0.8f, 1f), p.BarrierRadius, 0.4f, Sfx.Ability);
                    break;
                case AbilityId.Roar:
                    p.TauntT = RoarTime + s.RoarBonusTime;
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = p.TauntT;
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.45f, 0.3f), 320f, 0.5f, Sfx.Roar);
                    break;
                case AbilityId.Bulldoze:
                {
                    // Everything near the charge's path takes damage and is shoved aside.
                    p.InvulnT = Math.Max(p.InvulnT, BulldozeTime + 0.1f);
                    V2 from = p.Pos, path = target - from;
                    float len = Math.Max(1f, path.Length);
                    V2 dir = path / len;
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f)
                            continue;
                        float along = Math.Max(0f, Math.Min(len, (e.Pos.X - from.X) * dir.X + (e.Pos.Y - from.Y) * dir.Y));
                        V2 closest = from + dir * along;
                        if (e.Pos.DistanceTo(closest) > e.Radius + 30f)
                            continue;
                        HurtEnemy(e, BulldozeDamage * s.DmgMult * s.BulldozeMult, p.Id);
                        if (!e.Def.IsBoss)
                        {
                            V2 away = (e.Pos - closest).Normalized();
                            e.Pos = ClampToArena(e.Pos + (away.IsZero ? dir.Orthogonal() : away) * 70f, e.Radius);
                        }
                    }
                    BroadcastFx(FxKind.Ring, target, new Rgba(0.5f, 0.75f, 1f), 60f, 0.35f, Sfx.Blink);
                    break;
                }
                case AbilityId.Haven:
                {
                    var z = AddZone(ZoneKind.Haven, p, p.Pos, HavenRadius * s.HavenRadiusMult, HavenTime + s.HavenBonusTime);
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, z.Radius, 0.5f, Sfx.Ability);
                    break;
                }
                case AbilityId.SoulLink:
                {
                    p.LinkT = LinkTime;
                    p.LinkTo = p.Id;
                    foreach (var q in Players.Values)
                        if (q.Id != p.Id)
                            p.LinkTo = q.Id;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.6f, 1f, 0.9f), 60f, 0.4f, Sfx.Ability);
                    break;
                }
                case AbilityId.Grenade:
                {
                    V2 vel = (target - p.Pos) / GrenadeFlight;
                    var g = SpawnBullet(Defs.GrenadeShot, p.Pos, vel, 0f, GrenadeFlight, GrenadeDamage * s.DmgMult, p.Id);
                    g.Splash = GrenadeRadius * s.GrenadeRadiusMult;
                    g.Burst = 8 + s.GrenadeExtraShrapnel;
                    break;
                }
                case AbilityId.Mark:
                {
                    Enemy best = null;
                    foreach (var e in Enemies.Values)
                        if (e.SpawnT <= 0f && (best == null || e.Pos.DistanceSq(target) < best.Pos.DistanceSq(target)))
                            best = e;
                    if (best != null)
                    {
                        best.MarkT = MarkTime;
                        best.MarkMult = 1.5f + s.MarkBonus;
                        BroadcastFx(FxKind.Ring, best.Pos, new Rgba(1f, 0.25f, 0.25f), best.Radius + 30f, 0.5f, Sfx.Ability);
                    }
                    break;
                }
                case AbilityId.Dash:
                    p.InvulnT = Math.Max(p.InvulnT, DashTime + 0.12f + s.DashIframeBonus);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.7f, 0.35f), 24f, 0.25f, Sfx.Blink);
                    break;
                case AbilityId.MendPulse:
                {
                    float range = PulseRange * s.PulseRangeMult;
                    foreach (var q in Players.Values)
                    {
                        if (q.Pos.DistanceTo(p.Pos) >= range)
                            continue;
                        if (q.Downed)
                            RevivePlayer(q, 0.5f);
                        else
                            q.Hp = Math.Min(q.MaxHp, q.Hp + PulseHeal * s.PulseHealMult);
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.4f, 1f, 0.55f), range, 0.6f, Sfx.Ability);
                    break;
                }
                case AbilityId.Shockwave:
                {
                    var erase = new List<int>();
                    foreach (var b in Bullets.Values)
                        if (Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(p.Pos) < ShockwaveRange)
                            erase.Add(b.Id);
                    foreach (int bid in erase)
                    {
                        Bullets.Remove(bid);
                        pendingRemoved.Add(bid);
                    }
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(p.Pos) > ShockwaveRange + e.Radius)
                            continue;
                        HurtEnemy(e, ShockwaveDamage * s.DmgMult, p.Id);
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.9f, 0.5f), ShockwaveRange, 0.45f, Sfx.Shockwave);
                    break;
                }
                case AbilityId.Overdrive:
                    p.OverdriveT = OverdriveTime;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.4f, 0.3f), 60f, 0.4f, Sfx.Ability);
                    break;
                case AbilityId.Blink:
                    p.InvulnT = Math.Max(p.InvulnT, 0.3f);
                    BroadcastFx(FxKind.Ring, target, new Rgba(0.7f, 0.6f, 1f), 40f, 0.35f, Sfx.Blink);
                    break;
                case AbilityId.Nova:
                    for (int i = 0; i < 24; i++)
                    {
                        V2 dir = V2.FromAngle(p.Aim + MathX.Tau * i / 24f);
                        SpawnBullet(Defs.NovaShot, p.Pos + dir * p.Def.BodyR, dir * 480f, 0f, 1.2f, 22f * s.DmgMult, p.Id);
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.8f, 1f), 50f, 0.3f, Sfx.Ability);
                    break;
                case AbilityId.Turret:
                    SpawnMinion(MinionKind.Turret, p, p.Pos, TurretTime);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.6f, 0.95f, 1f), 40f, 0.35f, Sfx.Ability);
                    break;
                default:
                    KitAbility(p, p.Slots[slot].Id, target);  // mage and summoner abilities (Minions.cs)
                    break;
            }
        }

        void Damage(Player p, float amount)
        {
            if (p.TauntT > 0f)
                amount *= Math.Max(0.2f, 1f - RoarReduction - p.Stats.RoarExtraReduction);
            foreach (var q in Players.Values)
                if (q.LinkT > 0f && q.LinkTo == p.Id && q != p)
                    amount *= 0.5f;  // a healer's Soul Link halves the damage
            amount *= p.Stats.DamageTaken;
            if (p.ShieldHp > 0f)  // Arcane Ward soaks damage first
            {
                float soak = Math.Min(p.ShieldHp, amount);
                p.ShieldHp -= soak;
                amount -= soak;
            }
            p.Hp -= amount;
            p.InvulnT = HitIframes;
            p.Flash = 0.1f;
            if (p.Hp <= 0f && InLantern(p))
                p.Hp = 1f;  // a Soul Lantern keeps you standing
            if (p.Hp <= 0f)
            {
                p.Hp = 0f;
                p.Downed = true;
                p.Revive = 0f;
                p.BarrierT = 0f;
                p.OverdriveT = 0f;
                p.Firing = false;
            }
        }

        void RevivePlayer(Player p, float fraction)
        {
            p.Downed = false;
            p.Revive = 0f;
            p.Hp = Math.Max(p.Hp, p.MaxHp * fraction);
            p.InvulnT = 1.5f;
        }

        void CheckGameOver()
        {
            if (State == MatchState.GameOver || State == MatchState.Idle || Players.Count == 0)
                return;
            foreach (var p in Players.Values)
                if (!p.Downed)
                    return;
            State = MatchState.GameOver;
            spawnQueue.Clear();
            foreach (var p in Players.Values)
            {
                p.Offer = null;
                p.LegendaryQueue.Clear();
                p.PendingLegendary = 0;
            }
            DoLog("game over at wave " + Wave + ", score " + Score);
        }

        void TickWaves(float dt)
        {
            switch (State)
            {
                case MatchState.Intermission:
                    // The countdown waits while anyone is still choosing upgrades.
                    if (!AnyonePicking())
                        StateTimer -= dt;
                    if (StateTimer <= 0f)
                    {
                        Wave++;
                        State = MatchState.Fighting;
                        spawnTimer = 0.5f;
                        BuildWave(Wave);
                        DoLog("wave " + Wave + ": " + string.Join(",", spawnQueue));
                    }
                    break;
                case MatchState.Fighting:
                    if (spawnQueue.Count > 0)
                    {
                        spawnTimer -= dt;
                        if (spawnTimer <= 0f)
                        {
                            SpawnEnemy(spawnQueue[0]);
                            spawnQueue.RemoveAt(0);
                            spawnTimer = Math.Max(0.3f, 1.1f - Wave * 0.05f);
                        }
                    }
                    else if (Enemies.Count == 0)
                        WaveCleared();
                    break;
            }
            Remaining = spawnQueue.Count + Enemies.Count;
        }

        void WaveCleared()
        {
            ResetBossState();
            Zones.Clear();
            State = MatchState.Intermission;
            StateTimer = IntermissionTime;
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    RevivePlayer(p, 0.4f);
                else
                    p.Hp = Math.Min(p.MaxHp, p.Hp + p.MaxHp * 0.3f);
                if (p.Offer == null)
                    NextOffer(p);
            }
            RemoveTemporaryMinions();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            DoLog("wave " + Wave + " cleared");
        }

        Bullet SpawnBullet(byte kind, V2 pos, V2 vel, float angVel, float life, float dmg, int owner)
        {
            var b = MakeBullet(nextBulletId++, kind, owner, pos, vel, angVel, life);
            b.Dmg = dmg;
            Player p;
            if (owner != 0 && Players.TryGetValue(owner, out p))
            {
                b.Pierce = p.Stats.Pierce;
                if (p.SurgeT > 0f)  // Arcane Surge
                {
                    b.Dmg *= 1.6f;
                    b.Pierce += 2;
                }
            }
            pendingSpawns.Add(new Bullet { Id = b.Id, Kind = kind, Owner = owner, Pos = pos, Vel = vel, AngVel = angVel, Life = life });
            return b;
        }

        Bullet MakeBullet(int id, byte kind, int owner, V2 pos, V2 vel, float angVel, float life)
        {
            var b = new Bullet { Id = id, Kind = kind, Owner = owner, Pos = pos, Vel = vel, AngVel = angVel, Life = life, Radius = Defs.BulletRadius(kind) };
            Bullets[id] = b;
            if (owner == 0)
                Sounds.Add(Sfx.EnemyShot);
            else
                Sounds.Add(new SoundCue { Sfx = ShotSound(kind, owner), Ally = owner != MyId });
            return b;
        }

        Sfx ShotSound(byte kind, int owner)
        {
            if (kind == Defs.TurretShot || kind == Defs.MinionShot)
                return Sfx.ShotTurret;
            if (kind == Defs.NovaShot)
                return Sfx.ShotNova;
            Player p;
            if (!Players.TryGetValue(owner, out p))
                return Sfx.ShotRifle;
            switch (p.Weapon.Id)
            {
                case WeaponId.Scattergun: return Sfx.ShotShotgun;
                case WeaponId.FlakCannon: return Sfx.ShotCannon;
                case WeaponId.RiotRepeater: return Sfx.ShotRifle;
                case WeaponId.LightBolt: return Sfx.ShotSuppressed;
                case WeaponId.TwinWisps: return Sfx.ShotWisp;
                case WeaponId.MendersBow: return Sfx.ShotBow;
                case WeaponId.TwinRifle: return Sfx.ShotAssault;
                case WeaponId.Railgun: return Sfx.ShotSniper;
                case WeaponId.BurstSmg: return Sfx.ShotSmg;
                case WeaponId.FlameBurst:
                case WeaponId.Fireball: return Sfx.SpellFire;
                case WeaponId.FrostLance: return Sfx.SpellFrost;
                case WeaponId.StormHammer:
                case WeaponId.ChainLight:
                case WeaponId.ArcLightning: return Sfx.SpellZap;
                case WeaponId.PrismBeam:
                case WeaponId.ArcaneMissiles:
                case WeaponId.WispLantern: return Sfx.SpellArcane;
                case WeaponId.RadiantOrb: return Sfx.SpellHoly;
                default: return Sfx.Throw;  // summoner staffs, darts and rocks
            }
        }

        readonly List<int> scratchIds = new List<int>();

        readonly List<Bullet> landed = new List<Bullet>();

        void ExplodeGrenade(Bullet b)
        {
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Pos.DistanceTo(b.Pos) < b.Splash + e.Radius)
                    HurtEnemy(e, b.Dmg, b.Owner);
            float offset = Rand(0f, MathX.Tau);
            for (int i = 0; i < b.Burst; i++)
            {
                V2 dir = V2.FromAngle(offset + MathX.Tau * i / b.Burst);
                SpawnBullet(Defs.DpsShot, b.Pos, dir * 520f, 0f, 0.45f, b.Dmg * 0.12f, b.Owner);
            }
            BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.GrenadeShot), b.Splash, 0.35f, Sfx.Explode);
        }

        void MoveBullets(float dt)
        {
            scratchIds.Clear();
            landed.Clear();
            foreach (var b in Bullets.Values)
            {
                if (b.AngVel != 0f)
                    b.Vel = b.Vel.Rotated(b.AngVel * dt);
                b.Pos += b.Vel * dt;
                b.Life -= dt;
                if (b.Life <= 0f || b.Pos.X < -40f || b.Pos.Y < -40f || b.Pos.X > Width + 40f || b.Pos.Y > Height + 40f)
                {
                    scratchIds.Add(b.Id);
                    if (b.Burst > 0 && b.Life <= 0f)
                        landed.Add(b);
                }
            }
            foreach (int id in scratchIds)
                Bullets.Remove(id);
            // Host only (Burst is never set on clients): landed mortar bombs and grenades explode.
            foreach (var b in landed)
            {
                if (b.Owner != 0)
                {
                    ExplodeGrenade(b);
                    continue;
                }
                float offset = Rand(0f, MathX.Tau);
                for (int i = 0; i < b.Burst; i++)
                {
                    V2 dir = V2.FromAngle(offset + MathX.Tau * i / b.Burst);
                    SpawnBullet(Defs.SmallShot, b.Pos, dir * 150f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0);
                }
                BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.MineShot), 30f, 0.3f, Sfx.Hit);
            }
        }

        readonly List<Bullet> splashes = new List<Bullet>();

        bool HealAllyHit(Bullet b)
        {
            foreach (var p in Players.Values)
            {
                float r = p.Def.BodyR + b.Radius;
                if (p.Id == b.Owner || p.Downed || p.Hp >= p.MaxHp || b.Pos.DistanceSq(p.Pos) >= r * r)
                    continue;
                p.Hp = Math.Min(p.MaxHp, p.Hp + b.HealAlly * (b.Dmg / 9f));
                return true;
            }
            return false;
        }

        void Collide()
        {
            scratchIds.Clear();
            splashes.Clear();
            splits.Clear();
            reflects.Clear();
            foreach (var b in Bullets.Values)
            {
                if (b.Kind == Defs.GrenadeShot)
                    continue;  // grenades fly over enemies and explode where they land
                if (!Defs.IsEnemyShot(b.Kind))
                {
                    if (b.HealAlly > 0f && HealAllyHit(b))
                    {
                        scratchIds.Add(b.Id);
                        continue;
                    }
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Hp <= 0f || e.Id == b.LastHit)
                            continue;
                        float r = e.Radius + b.Radius;
                        if (b.Pos.DistanceSq(e.Pos) < r * r)
                        {
                            if (BlocksShot(e, b.Pos))
                            {
                                scratchIds.Add(b.Id);
                                break;
                            }
                            HurtEnemy(e, b.Dmg, b.Owner);
                            OnBulletHit(b, e);
                            b.LastHit = e.Id;
                            if (b.Splash > 0f)
                                splashes.Add(b);
                            if (b.Pierce > 0)
                                b.Pierce--;
                            else
                                scratchIds.Add(b.Id);
                            break;
                        }
                    }
                }
                else
                {
                    if (BlockedBySummons(b))
                    {
                        scratchIds.Add(b.Id);
                        continue;
                    }
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed)
                            continue;
                        if (p.AegisT > 0f && b.Pos.DistanceTo(p.Pos) < p.Def.BodyR + 24f)
                        {
                            reflects.Add(new Bullet { Pos = b.Pos, Vel = b.Vel, Owner = p.Id });  // Arcane Aegis
                            scratchIds.Add(b.Id);
                            break;
                        }
                        if (p.BarrierT > 0f && b.Pos.DistanceTo(p.Pos) < p.BarrierRadius)
                        {
                            scratchIds.Add(b.Id);
                            break;
                        }
                        float r = p.Def.HitR + b.Radius;
                        if (b.Pos.DistanceSq(p.Pos) < r * r)
                        {
                            // Invulnerable players (dash, recent hit) let bullets pass through.
                            if (p.InvulnT <= 0f)
                            {
                                Damage(p, b.Dmg);
                                scratchIds.Add(b.Id);
                            }
                            break;
                        }
                    }
                }
            }
            foreach (int id in scratchIds)
            {
                Bullet b;
                if (!Bullets.TryGetValue(id, out b))
                    continue;
                RemovedFx(b);
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }

            foreach (var rb in reflects)
            {
                Player owner;
                if (Players.TryGetValue(rb.Owner, out owner))
                    SpawnBullet(Defs.ArcaneShot, rb.Pos, rb.Vel.Normalized() * -520f, 0f, 1f, 12f * owner.Stats.DmgMult, rb.Owner);
            }

            foreach (var sp in splits)
            {
                float offset = Rand(0f, MathX.Tau);
                for (int i = 0; i < sp.Pierce; i++)
                {
                    V2 d = V2.FromAngle(offset + MathX.Tau * i / sp.Pierce);
                    SpawnBullet(Defs.SwarmShot, sp.Pos + d * 6f, d * 520f, 0f, 0.35f, sp.Dmg, sp.Owner).LastHit = sp.LastHit;
                }
            }

            // Flak shells (and Collapse Rounds) hurt everything near the impact, at 60% damage.
            foreach (var b in splashes)
            {
                foreach (var e in Enemies.Values)
                {
                    if (e.Id == b.LastHit || e.SpawnT > 0f || e.Pos.DistanceTo(b.Pos) > b.Splash + e.Radius || BlocksShot(e, b.Pos))
                        continue;
                    HurtEnemy(e, b.Dmg * 0.6f, b.Owner);
                }
                BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(b.Kind), b.Splash, 0.3f, b.Kind == Defs.FlakShell ? Sfx.Explode : Sfx.None);
            }

            var dead = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (e.Hp <= 0f)
                    dead.Add(e);
            foreach (var e in dead)
            {
                bool bossReward = OnEnemyKilled(e);
                KillEffects(e);
                Score += e.Score * (e.Elite ? 3 : 1);
                GrantXp(e.Def.Xp * (e.Elite ? 3 : 1));
                DropOrbs(e, bossReward);
                DeathFx(e, e.Pos);
                Enemies.Remove(e.Id);
            }
        }

        /// All damage to enemies goes through here so marks and other modifiers apply.
        void HurtEnemy(Enemy e, float dmg, int attacker)
        {
            if (e.MarkT > 0f)
                dmg *= e.MarkMult;
            Player p;
            if (Players.TryGetValue(attacker, out p))
            {
                var s = p.Stats;
                if (s.ExecuteBonus > 0f && e.Hp < e.MaxHp * 0.35f)
                    dmg *= 1f + s.ExecuteBonus;
                if (s.CritChance > 0f && rng.NextDouble() < s.CritChance)
                    dmg *= 2f;
                if (s.Lifesteal > 0f && !p.Downed)
                {
                    float heal = Math.Min(s.Lifesteal, p.LifestealPool);
                    p.LifestealPool -= heal;
                    p.Hp = Math.Min(p.MaxHp, p.Hp + heal);
                }
                if (s.SlowOnHit > 0f)
                {
                    e.SlowT = Math.Max(e.SlowT, 2f);
                    e.SlowMult = Math.Min(e.SlowMult, 1f - s.SlowOnHit);
                }
                e.LastHitBy = attacker;
            }
            e.Hp -= dmg;
            e.Flash = 0.06f;
        }

        // Swarm Rounds splits and Aegis reflections, queued while the bullet loop runs.
        readonly List<Bullet> splits = new List<Bullet>();
        readonly List<Bullet> reflects = new List<Bullet>();

        /// Legendary on-hit effects that spawn things (run after the collision loop).
        void OnBulletHit(Bullet b, Enemy e)
        {
            if (b.Chain > 0)
                ChainLightning(e, b.Dmg * 0.6f, b.Chain, b.Owner);
            if (b.SlowAmt > 0f && !e.Def.IsBoss)
            {
                e.SlowT = Math.Max(e.SlowT, 1.5f);
                e.SlowMult = Math.Min(e.SlowMult, 1f - b.SlowAmt);
            }
            Player p;
            if (!Players.TryGetValue(b.Owner, out p))
                return;
            if (p.Stats.SplitShots > 0 && b.Kind != Defs.SwarmShot)
                splits.Add(new Bullet { Pos = b.Pos, Owner = b.Owner, Dmg = b.Dmg * 0.4f, Pierce = p.Stats.SplitShots, LastHit = e.Id });
            if (p.Stats.ImpactSplash > 0f && b.Kind != Defs.SwarmShot)
                splashes.Add(new Bullet { Pos = b.Pos, Owner = b.Owner, Kind = Defs.NovaShot, Splash = p.Stats.ImpactSplash, Dmg = b.Dmg * 0.67f, LastHit = e.Id });
        }

        /// Legendary on-kill effects for whoever landed the last hit.
        void KillEffects(Enemy e)
        {
            Player p;
            if (!Players.TryGetValue(e.LastHitBy, out p))
                return;
            var s = p.Stats;
            if (s.KillHeal > 0f && !p.Downed)
                p.Hp = Math.Min(p.MaxHp, p.Hp + s.KillHeal);
            for (int i = 0; i < s.KillRing; i++)
            {
                V2 d = V2.FromAngle(MathX.Tau * i / s.KillRing);
                SpawnBullet(Defs.NovaShot, e.Pos + d * e.Radius, d * 420f, 0f, 0.7f, 10f * s.DmgMult, p.Id);
            }
            if (s.BroodChance > 0f && rng.NextDouble() < s.BroodChance)
                SpawnMinion(MinionKind.Turret, p, e.Pos, 4f);
        }

        void DropOrbs(Enemy e, bool bossReward)
        {
            if (e.Elite)
            {
                SpawnOrb(e.Pos, rng.NextDouble() < 0.25);  // elites always drop something
                return;
            }
            if (bossReward)
            {
                // Bosses always drop a large orb and a few small ones around it.
                SpawnOrb(e.Pos, true);
                for (int i = 0; i < 4; i++)
                    SpawnOrb(e.Pos + V2.FromAngle(MathX.Tau * i / 4f) * 40f, false);
                return;
            }
            double roll = rng.NextDouble();
            if (roll < BigOrbChance)
                SpawnOrb(e.Pos, true);
            else if (roll < BigOrbChance + OrbChance)
                SpawnOrb(e.Pos, false);
        }

        void SpawnOrb(V2 pos, bool big)
        {
            var o = new Orb { Id = nextOrbId++, Big = big, Pos = ClampToArena(pos, 12f), Life = OrbLifetime };
            o.Disp = o.Pos;
            Orbs[o.Id] = o;
        }

        /// Orbs drift toward the nearest hurt player in range and heal whoever touches them.
        /// Players at full health leave them for their ally.
        void TickOrbs(float dt)
        {
            var gone = new List<int>();
            foreach (var o in Orbs.Values)
            {
                o.Life -= dt;
                if (o.Life <= 0f)
                {
                    gone.Add(o.Id);
                    continue;
                }
                Player nearest = null;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.Hp < p.MaxHp && p.Pos.DistanceTo(o.Pos) < OrbMagnetRange &&
                        (nearest == null || p.Pos.DistanceSq(o.Pos) < nearest.Pos.DistanceSq(o.Pos)))
                        nearest = p;
                if (nearest == null)
                    continue;
                V2 to = nearest.Pos - o.Pos;
                o.Pos += to.LimitLength(300f * dt);
                if (to.Length < nearest.Def.BodyR + o.Radius)
                {
                    nearest.Hp = Math.Min(nearest.MaxHp, nearest.Hp + o.Heal);
                    BroadcastFx(FxKind.Ring, o.Pos, new Rgba(0.4f, 1f, 0.5f), o.Big ? 50f : 25f, 0.35f, o.Big ? Sfx.PickupBig : Sfx.Pickup);
                    gone.Add(o.Id);
                }
            }
            foreach (int id in gone)
                Orbs.Remove(id);
        }

        void RemovedFx(Bullet b)
        {
            AddFx(FxKind.Spark, b.Pos, Defs.BulletColor(b.Kind), 10f, 0.15f);
            if (!Defs.IsEnemyShot(b.Kind))
                Sounds.Add(Sfx.Hit);
        }

        void DeathFx(Enemy e, V2 pos)
        {
            AddFx(FxKind.Boom, pos, e.Def.Color, e.Radius * 2.2f, 0.4f);
            Sounds.Add(e.Def.IsBoss ? Sfx.BigExplode : Sfx.Explode);
        }

        void ClearEnemyBullets()
        {
            scratchIds.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind))
                    scratchIds.Add(b.Id);
            foreach (int id in scratchIds)
                Bullets.Remove(id);
        }

        void BroadcastFx(FxKind kind, V2 pos, Rgba color, float radius, float dur, Sfx sound)
        {
            BroadcastFx(kind, pos, pos, color, radius, dur, sound);
        }

        void BroadcastFx(FxKind kind, V2 pos, V2 pos2, Rgba color, float radius, float dur, Sfx sound)
        {
            AddFx(kind, pos, color, radius, dur);
            Effects[Effects.Count - 1].Pos2 = pos2;
            Sounds.Add(sound);
            var w = Proto.Begin(Msg.Fx);
            w.Write((byte)kind);
            w.Write(pos);
            w.Write(pos2);
            w.Write(color);
            w.Write(radius);
            w.Write(dur);
            w.Write((byte)sound);
            session.Broadcast(Proto.End(w));
        }

        void SendSnapshot()
        {
            var w = Proto.Begin(Msg.Snapshot);
            w.Write((byte)State);
            w.Write(Wave);
            w.Write(StateTimer);
            w.Write(Remaining);
            w.Write(Score);
            w.Write(Paused);
            w.Write(PausedBy);
            w.Write(PullCenter);
            w.Write(PullStrength);
            w.Write(TwinReviveT);
            w.Write((byte)Players.Count);
            foreach (var p in Players.Values)
            {
                w.Write(p.Id);
                w.Write(p.Pos);
                w.Write(p.Aim);
                w.Write(p.Hp);
                w.Write(p.Downed);
                w.Write(p.Revive);
                w.Write(p.BarrierT);
                w.Write(p.InvulnT);
                w.Write(p.OverdriveT);
                w.Write(p.TauntT);
                w.Write(p.LinkT);
                w.Write(p.LinkTo);
                w.Write(p.AegisT);
                w.Write(p.SurgeT);
                w.Write(p.FrenzyT);
                w.Write(p.ShieldHp);
                w.Write((short)p.Level);
                w.Write(p.Xp);
                w.Write((byte)p.PendingPicks);
                w.Write((short)p.Rerolls);
                w.Write((byte)p.PendingLegendary);
                w.Write(p.OfferLegendary);
                for (int i = 0; i < 3; i++)
                    w.Write((byte)(p.Offer != null ? p.Offer[i] : 255));
                foreach (var s in p.Slots)
                {
                    w.Write((byte)s.Charges);
                    w.Write(s.Timer);
                }
            }
            w.Write((short)Enemies.Count);
            foreach (var e in Enemies.Values)
            {
                w.Write(e.Id);
                w.Write((byte)e.Type);
                w.Write(e.Pos);
                w.Write(e.Hp);
                w.Write(e.MaxHp);
                w.Write(e.SpawnT > 0f);
                w.Write(e.Facing);
                w.Write(e.Phase);
                w.Write(e.Elite);
                w.Write(e.MarkT > 0f);
            }
            w.Write((byte)Minions.Count);
            foreach (var m in Minions.Values)
            {
                w.Write(m.Id);
                w.Write((byte)m.Kind);
                w.Write(m.Owner);
                w.Write(m.Pos);
                w.Write(m.Aim);
                w.Write(m.T);
                w.Write(m.Duration);
            }
            w.Write((byte)Zones.Count);
            foreach (var z in Zones.Values)
            {
                w.Write(z.Id);
                w.Write((byte)z.Kind);
                w.Write(z.Owner);
                w.Write(z.Pos);
                w.Write(z.Radius);
                w.Write(z.T);
                w.Write(z.Duration);
            }
            w.Write((short)Orbs.Count);
            foreach (var o in Orbs.Values)
            {
                w.Write(o.Id);
                w.Write(o.Big);
                w.Write(o.Pos);
                w.Write(o.Life);
            }
            session.Broadcast(Proto.End(w));
        }

        // --- Client-side replication ------------------------------------------

        public void OnHostMessage(Msg type, BinaryReader r)
        {
            if (type == Msg.Start)
            {
                ReadStart(r);
                return;
            }
            if (!Running)
                return;
            switch (type)
            {
                case Msg.Spawn:
                {
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        int id = r.ReadInt32();
                        byte kind = r.ReadByte();
                        int owner = r.ReadInt32();
                        V2 pos = r.ReadV2(), vel = r.ReadV2();
                        float angVel = r.ReadSingle(), life = r.ReadSingle();
                        MakeBullet(id, kind, owner, pos, vel, angVel, life);
                    }
                    break;
                }
                case Msg.Remove:
                {
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        int id = r.ReadInt32();
                        Bullet b;
                        if (Bullets.TryGetValue(id, out b))
                        {
                            RemovedFx(b);
                            Bullets.Remove(id);
                        }
                    }
                    break;
                }
                case Msg.ClearEnemyBullets:
                    ClearEnemyBullets();
                    break;
                case Msg.Fx:
                {
                    var kind = (FxKind)r.ReadByte();
                    V2 pos = r.ReadV2(), pos2 = r.ReadV2();
                    Rgba color = r.ReadRgba();
                    float radius = r.ReadSingle(), dur = r.ReadSingle();
                    AddFx(kind, pos, color, radius, dur);
                    Effects[Effects.Count - 1].Pos2 = pos2;
                    Sounds.Add((Sfx)r.ReadByte());
                    break;
                }
                case Msg.Upgrades:
                {
                    int id = r.ReadInt32();
                    int n = r.ReadByte();
                    Player p;
                    if (!Players.TryGetValue(id, out p))
                        break;
                    p.Taken.Clear();
                    for (int i = 0; i < n; i++)
                        p.Taken.Add(r.ReadByte());
                    Recalc(p);
                    if (id == MyId)
                        awaitingPick = false;
                    break;
                }
                case Msg.Snapshot:
                    ReadSnapshot(r);
                    break;
                case Msg.BulletSync:
                    ReadBulletSync(r);
                    break;
            }
        }

        void ReadSnapshot(BinaryReader r)
        {
            State = (MatchState)r.ReadByte();
            Wave = r.ReadInt32();
            StateTimer = r.ReadSingle();
            Remaining = r.ReadInt32();
            Score = r.ReadInt32();
            Paused = r.ReadBoolean();
            PausedBy = r.ReadInt32();
            PullCenter = r.ReadV2();
            PullStrength = r.ReadSingle();
            TwinReviveT = r.ReadSingle();
            int np = r.ReadByte();
            for (int i = 0; i < np; i++)
            {
                int id = r.ReadInt32();
                V2 pos = r.ReadV2();
                float aim = r.ReadSingle(), hp = r.ReadSingle();
                bool downed = r.ReadBoolean();
                float revive = r.ReadSingle(), barrier = r.ReadSingle(), invuln = r.ReadSingle(), overdrive = r.ReadSingle();
                float taunt = r.ReadSingle(), link = r.ReadSingle();
                int linkTo = r.ReadInt32();
                float aegis = r.ReadSingle(), surge = r.ReadSingle(), frenzy = r.ReadSingle(), shield = r.ReadSingle();
                int level = r.ReadInt16();
                float xp = r.ReadSingle();
                int pending = r.ReadByte();
                int rerolls = r.ReadInt16();
                int pendingLegendary = r.ReadByte();
                bool offerLegendary = r.ReadBoolean();
                var offer = new int[3];
                for (int k = 0; k < 3; k++)
                    offer[k] = r.ReadByte();
                var charges = new int[Upgrades.SlotCount];
                var timers = new float[Upgrades.SlotCount];
                for (int k = 0; k < Upgrades.SlotCount; k++)
                {
                    charges[k] = r.ReadByte();
                    timers[k] = r.ReadSingle();
                }

                Player p;
                if (!Players.TryGetValue(id, out p))
                    continue;
                if (id != MyId)
                {
                    p.Pos = pos;
                    p.Aim = aim;
                    // Your own cooldowns are tracked locally; only copy the ally's.
                    for (int k = 0; k < Upgrades.SlotCount; k++)
                    {
                        p.Slots[k].Charges = charges[k];
                        p.Slots[k].Timer = timers[k];
                    }
                }
                if (hp < p.Hp)
                    p.Flash = 0.1f;
                p.Hp = hp;
                p.Downed = downed;
                p.Revive = revive;
                p.BarrierT = barrier;
                p.InvulnT = invuln;
                p.OverdriveT = overdrive;
                p.TauntT = taunt;
                p.LinkT = link;
                p.LinkTo = linkTo;
                p.AegisT = aegis;
                p.SurgeT = surge;
                p.FrenzyT = frenzy;
                p.ShieldHp = shield;
                p.Level = level;
                p.Xp = xp;
                p.PendingPicks = pending;
                p.Rerolls = rerolls;
                p.PendingLegendary = pendingLegendary;
                p.OfferLegendary = offerLegendary;
                if (id == MyId && RerollPending && (rerolls < rerollsAtRequest || offer[0] == 255))
                    rerollsAtRequest = -1;
                p.Offer = offer[0] == 255 || (id == MyId && awaitingPick) ? null : offer;
            }

            var seen = new HashSet<int>();
            int ne = r.ReadInt16();
            for (int i = 0; i < ne; i++)
            {
                int id = r.ReadInt32();
                var type = (EnemyType)r.ReadByte();
                V2 pos = r.ReadV2();
                float hp = r.ReadSingle(), maxHp = r.ReadSingle();
                bool spawning = r.ReadBoolean();
                float facing = r.ReadSingle();
                byte phase = r.ReadByte();
                bool elite = r.ReadBoolean();
                bool marked = r.ReadBoolean();
                Enemy e;
                if (!Enemies.TryGetValue(id, out e))
                {
                    var d = Defs.Get(type);
                    e = new Enemy { Id = id, Type = type, Def = d, Radius = d.Radius, Pos = pos, Disp = pos, Hp = hp };
                    Enemies[id] = e;
                }
                if (hp < e.Hp)
                    e.Flash = 0.06f;
                e.Pos = pos;
                e.Hp = hp;
                e.MaxHp = maxHp;
                e.Spawning = spawning;
                e.Facing = facing;
                e.Phase = phase;
                e.Elite = elite;
                e.Marked = marked;
                e.Radius = Defs.Get(type).Radius * (elite ? EliteSize : 1f);
                seen.Add(id);
            }
            var gone = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (!seen.Contains(e.Id))
                    gone.Add(e);
            foreach (var e in gone)
            {
                DeathFx(e, e.Disp);
                Enemies.Remove(e.Id);
            }

            var seenMinions = new HashSet<int>();
            int nm = r.ReadByte();
            for (int i = 0; i < nm; i++)
            {
                int id = r.ReadInt32();
                var kind = (MinionKind)r.ReadByte();
                int owner = r.ReadInt32();
                V2 pos = r.ReadV2();
                float aim = r.ReadSingle(), t = r.ReadSingle(), duration = r.ReadSingle();
                Minion m;
                if (!Minions.TryGetValue(id, out m))
                {
                    m = new Minion { Id = id, Kind = kind, Owner = owner, Pos = pos, Disp = pos };
                    Minions[id] = m;
                }
                m.Pos = pos;
                m.Aim = aim;
                m.T = t;
                m.Duration = duration;
                seenMinions.Add(id);
            }
            var goneMinions = new List<int>();
            foreach (int id in Minions.Keys)
                if (!seenMinions.Contains(id))
                    goneMinions.Add(id);
            foreach (int id in goneMinions)
                Minions.Remove(id);

            Zones.Clear();
            int nz = r.ReadByte();
            for (int i = 0; i < nz; i++)
            {
                var z = new Zone { Id = r.ReadInt32(), Kind = (ZoneKind)r.ReadByte(), Owner = r.ReadInt32(), Pos = r.ReadV2(), Radius = r.ReadSingle(), T = r.ReadSingle(), Duration = r.ReadSingle() };
                Zones[z.Id] = z;
            }

            var seenOrbs = new HashSet<int>();
            int no = r.ReadInt16();
            for (int i = 0; i < no; i++)
            {
                int id = r.ReadInt32();
                bool big = r.ReadBoolean();
                V2 pos = r.ReadV2();
                float life = r.ReadSingle();
                Orb o;
                if (!Orbs.TryGetValue(id, out o))
                {
                    o = new Orb { Id = id, Big = big, Pos = pos, Disp = pos };
                    Orbs[id] = o;
                }
                o.Pos = pos;
                o.Life = life;
                seenOrbs.Add(id);
            }
            var expired = new List<int>();
            foreach (int id in Orbs.Keys)
                if (!seenOrbs.Contains(id))
                    expired.Add(id);
            foreach (int id in expired)
                Orbs.Remove(id);
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
