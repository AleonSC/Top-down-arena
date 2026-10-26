using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    public enum MatchState : byte { Idle, Fighting, Intermission, GameOver }

    public enum FxKind : byte { Boom, Spark, Ring }

    /// Sound cues raised by the game; the Unity layer plays them.
    public enum Sfx : byte
    {
        None, EnemyShot, Hit, Hurt, Explode, BigExplode, Ability, Shockwave, Blink,
        LevelUp, Pick, WaveStart, WaveClear, GameOver, Downed, Revive,
        // One gunshot per weapon, plus the turret and Nova.
        ShotShotgun, ShotCannon, ShotRifle, ShotSuppressed, ShotWisp, ShotBow, ShotAssault, ShotSniper, ShotSmg,
        ShotTurret, ShotNova,
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
        public WeaponDef Weapon;
        public string Name;
        public V2 Pos, Disp;
        public float Aim;
        public bool Firing;
        public float Hp, MaxHp;
        public bool Downed;
        public float Revive, FireCd, BarrierT, InvulnT, OverdriveT, Flash, LevelFlash;

        public int Level = 1, PendingPicks, Rerolls = Game.StartingRerolls;
        public float Xp;
        public int[] Offer;  // three upgrade ids while choosing, otherwise null
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
        public int TargetId;
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
    }

    public class Turret
    {
        public int Id, Owner;
        public V2 Pos;
        public float T, FireCd, Aim;
    }

    public class Effect
    {
        public FxKind Kind;
        public V2 Pos;
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
    public class Game
    {
        public const float Width = 1280f, Height = 720f;

        const int SnapshotEvery = 3;  // ticks
        public const int StartingRerolls = 2;
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
        const float BlinkRange = 300f;
        const float ShockwaveRange = 230f;
        const float ShockwaveDamage = 60f;
        const float OverdriveTime = 5f;
        const float TurretTime = 10f;
        const float TurretRange = 520f;

        readonly Session session;
        readonly Random rng = new Random();

        public bool Running;
        public MatchState State = MatchState.Idle;
        public int Wave, Remaining, Score;
        public float StateTimer;

        public readonly SortedDictionary<int, Player> Players = new SortedDictionary<int, Player>();
        public readonly Dictionary<int, Enemy> Enemies = new Dictionary<int, Enemy>();
        public readonly Dictionary<int, Bullet> Bullets = new Dictionary<int, Bullet>();
        public readonly Dictionary<int, Turret> Turrets = new Dictionary<int, Turret>();
        public readonly List<Effect> Effects = new List<Effect>();
        public readonly List<SoundCue> Sounds = new List<SoundCue>();  // drained by the front end each frame

        public float DashT, AnimT;
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
        int nextBulletId = 1, nextEnemyId = 1, nextTurretId = 1, tick;
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
                w.Write(e.Weapon);
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
                int weapon = r.ReadByte();
                var def = Defs.Get(role);
                var p = new Player { Id = id, Role = role, Def = def, Name = name, Hp = def.Hp, MaxHp = def.Hp, Aim = -MathX.Pi / 2f };
                p.Weapon = Defs.Weapon(role, weapon < Defs.WeaponsPerRole ? weapon : 0);
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
            Turrets.Clear();
            Effects.Clear();
            Sounds.Clear();
            spawnQueue.Clear();
            pendingSpawns.Clear();
            pendingRemoved.Clear();
            lastDowned.Clear();
            lastLevel.Clear();
            nextBulletId = 1;
            nextEnemyId = 1;
            nextTurretId = 1;
            DashT = 0f;
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
            p.MaxHp = p.Def.Hp + p.Stats.BonusHp;
            if (p.MaxHp > oldMax && !p.Downed)
                p.Hp += p.MaxHp - oldMax;

            var ids = new AbilityId[Upgrades.SlotCount];
            ids[0] = Upgrades.RoleAbility(p.Role);
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
            if (me == null || me.Offer == null || me.Rerolls <= 0 || RerollPending)
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
            if (!Players.TryGetValue(id, out p) || p.Offer == null || p.Rerolls <= 0 || State != MatchState.Intermission)
                return;
            p.Rerolls--;
            p.Offer = Upgrades.RollOffer(rng, p.Role, p.Taken, p.Level);
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
            p.PendingPicks = Math.Max(0, p.PendingPicks - 1);
            p.Offer = p.PendingPicks > 0 ? Upgrades.RollOffer(rng, p.Role, p.Taken, p.Level) : null;
            GrantUpgrade(id, upgrade);
            DoLog(p.Role + " picked " + Upgrades.All[upgrade].Name);
            SendSnapshot();  // show the next offer right away
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
                if (p.PendingPicks > 0)
                    return true;
            return false;
        }

        // --- Frame loop --------------------------------------------------------

        /// Fixed-rate simulation step (60 Hz).
        public void FixedStep(float dt, PlayerInput input)
        {
            if (!Running)
                return;
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
                me.Pos += DashDir * DashSpeed * dt;
            }
            else
                me.Pos += move * me.Speed * dt;

            for (int slot = 0; slot < held.Length; slot++)
            {
                bool pressed = held[slot] && !abilityWasDown[slot];
                abilityWasDown[slot] = held[slot];
                if (!pressed || me.Downed || State == MatchState.GameOver || !Consume(me, slot, false))
                    continue;
                V2 target = aimAt;
                switch (me.Slots[slot].Id)
                {
                    case AbilityId.Dash:
                        DashDir = move.IsZero ? V2.FromAngle(me.Aim) : move.Normalized();
                        DashT = DashTime;
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
            TickTurrets(dt);
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
                        if (b.Kind >= Defs.SmallShot && b.Pos.DistanceTo(p.Pos) < ShockwaveRange)
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
                        e.Hp -= ShockwaveDamage * s.DmgMult;
                        e.Flash = 0.1f;
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
                {
                    var t = new Turret { Id = nextTurretId++, Owner = p.Id, Pos = p.Pos, T = TurretTime, Aim = p.Aim };
                    Turrets[t.Id] = t;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.6f, 0.95f, 1f), 40f, 0.35f, Sfx.Ability);
                    break;
                }
            }
        }

        void TickTurrets(float dt)
        {
            var expired = new List<int>();
            foreach (var t in Turrets.Values)
            {
                t.T -= dt;
                t.FireCd -= dt;
                Player owner;
                Players.TryGetValue(t.Owner, out owner);
                if (t.T <= 0f || owner == null)
                {
                    expired.Add(t.Id);
                    continue;
                }
                Enemy target = null;
                foreach (var e in Enemies.Values)
                    if (e.SpawnT <= 0f && e.Pos.DistanceTo(t.Pos) < TurretRange && (target == null || e.Pos.DistanceSq(t.Pos) < target.Pos.DistanceSq(t.Pos)))
                        target = e;
                if (target == null)
                    continue;
                t.Aim = (target.Pos - t.Pos).Angle;
                if (t.FireCd <= 0f && State == MatchState.Fighting)
                {
                    t.FireCd = 0.25f;
                    V2 dir = V2.FromAngle(t.Aim);
                    SpawnBullet(Defs.TurretShot, t.Pos + dir * 14f, dir * 700f, 0f, 0.9f, 9f * owner.Stats.DmgMult, t.Owner);
                }
            }
            foreach (int id in expired)
            {
                AddFx(FxKind.Boom, Turrets[id].Pos, new Rgba(0.6f, 0.95f, 1f), 20f, 0.3f);
                Turrets.Remove(id);
            }
        }

        void Damage(Player p, float amount)
        {
            p.Hp -= amount * p.Stats.DamageTaken;
            p.InvulnT = HitIframes;
            p.Flash = 0.1f;
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
                p.Offer = null;
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

        void BuildWave(int n)
        {
            spawnQueue.Clear();
            if (n % 5 == 0)
            {
                spawnQueue.Add(EnemyType.Boss);
                for (int i = 0; i < n / 2; i++)
                    spawnQueue.Add(EnemyType.Grunt);
                return;
            }
            var pool = new List<EnemyType> { EnemyType.Grunt };
            if (n >= 2) pool.Add(EnemyType.Burster);
            if (n >= 3) pool.Add(EnemyType.Sniper);
            if (n >= 4) pool.Add(EnemyType.Spinner);
            int count = Math.Min(4 + n * 2, 32);
            for (int i = 0; i < count; i++)
                spawnQueue.Add(pool[rng.Next(pool.Count)]);
        }

        void WaveCleared()
        {
            State = MatchState.Intermission;
            StateTimer = IntermissionTime;
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    RevivePlayer(p, 0.4f);
                else
                    p.Hp = Math.Min(p.MaxHp, p.Hp + p.MaxHp * 0.3f);
                if (p.PendingPicks > 0 && p.Offer == null)
                    p.Offer = Upgrades.RollOffer(rng, p.Role, p.Taken, p.Level);
            }
            Turrets.Clear();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            DoLog("wave " + Wave + " cleared");
        }

        void SpawnEnemy(EnemyType type)
        {
            var d = Defs.Get(type);
            var e = new Enemy { Id = nextEnemyId++, Type = type, Def = d, Radius = d.Radius, Speed = d.Speed, Score = d.Score };
            e.MaxHp = d.Hp * (1f + 0.15f * (Wave - 1)) * (Players.Count > 1 ? 1f : 0.7f);
            e.Hp = e.MaxHp;
            float m = e.Radius + 30f;
            switch (rng.Next(4))
            {
                case 0: e.Pos = new V2(Rand(m, Width - m), m); break;
                case 1: e.Pos = new V2(Rand(m, Width - m), Height - m); break;
                case 2: e.Pos = new V2(m, Rand(m, Height - m)); break;
                default: e.Pos = new V2(Width - m, Rand(m, Height - m)); break;
            }
            if (type == EnemyType.Boss)
                e.Pos = new V2(Width / 2f, 140f);
            e.Disp = e.Pos;
            e.Waypoint = e.Pos;
            e.SpawnT = 0.9f;
            e.FireT = Rand(0.3f, 1.2f);
            e.Strafe = rng.NextDouble() < 0.5 ? 1f : -1f;
            e.Ang = Rand(0f, MathX.Tau);
            Enemies[e.Id] = e;
        }

        int PickTarget()
        {
            var alive = new List<int>();
            var tanks = new List<int>();
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    continue;
                alive.Add(p.Id);
                if (p.Role == Role.Tank)
                    tanks.Add(p.Id);
            }
            if (alive.Count == 0)
                return 0;
            if (tanks.Count > 0 && rng.NextDouble() < 0.7)
                return tanks[rng.Next(tanks.Count)];
            return alive[rng.Next(alive.Count)];
        }

        void TickEnemies(float dt)
        {
            float rate = Math.Min(2f, 1f + 0.05f * (Wave - 1));
            foreach (var e in Enemies.Values)
            {
                e.T += dt;
                if (e.SpawnT > 0f)
                {
                    e.SpawnT -= dt;
                    continue;
                }
                e.RetargetT -= dt;
                Player target;
                Players.TryGetValue(e.TargetId, out target);
                if (e.RetargetT <= 0f || target == null || target.Downed)
                {
                    e.TargetId = PickTarget();
                    e.RetargetT = 3f;
                    Players.TryGetValue(e.TargetId, out target);
                }
                V2 tpos = target != null ? target.Pos : new V2(Width / 2f, Height / 2f);
                MoveEnemy(e, tpos, dt);
                if (State == MatchState.Fighting)
                {
                    e.FireT -= dt * rate;
                    if (e.FireT <= 0f)
                        EnemyFire(e, tpos);
                }
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(e.Pos) < e.Radius + p.Def.HitR)
                        Damage(p, ContactDamage);
            }

            // Keep enemies from stacking on top of each other.
            var list = new List<Enemy>(Enemies.Values);
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    Enemy a = list[i], b = list[j];
                    V2 d = b.Pos - a.Pos;
                    float l = d.Length, minD = a.Radius + b.Radius;
                    if (l < minD && l > 0.001f)
                    {
                        V2 push = d / l * (minD - l) * 0.5f;
                        a.Pos -= push;
                        b.Pos += push;
                    }
                }
            }
        }

        static V2 KeepDistance(V2 dir, float dist, float want)
        {
            if (dist > want + 40f) return dir;
            if (dist < want - 40f) return -dir;
            return V2.Zero;
        }

        void MoveEnemy(Enemy e, V2 tpos, float dt)
        {
            V2 to = tpos - e.Pos;
            float dist = to.Length;
            V2 dir = dist > 0.01f ? to / dist : V2.Zero;
            V2 v = V2.Zero;
            switch (e.Type)
            {
                case EnemyType.Grunt:
                    v = KeepDistance(dir, dist, 230f) + dir.Orthogonal() * e.Strafe * 0.6f;
                    break;
                case EnemyType.Sniper:
                    v = KeepDistance(dir, dist, 420f) + dir.Orthogonal() * e.Strafe * 0.8f;
                    break;
                case EnemyType.Burster:
                    v = KeepDistance(dir, dist, 160f);
                    break;
                case EnemyType.Spinner:
                    if (e.Pos.DistanceTo(e.Waypoint) < 20f)
                        e.Waypoint = new V2(Rand(150f, Width - 150f), Rand(150f, Height - 150f));
                    v = (e.Waypoint - e.Pos).Normalized();
                    break;
                case EnemyType.Boss:
                    var goal = new V2(Width / 2f + (float)Math.Sin(e.T * 0.5f) * 380f, 150f + (float)Math.Sin(e.T * 0.8f) * 40f);
                    v = (goal - e.Pos) / 60f;
                    break;
            }
            if (rng.NextDouble() < dt * 0.3)
                e.Strafe = -e.Strafe;
            e.Pos += v.LimitLength(1f) * e.Speed * dt;
            e.Pos = ClampToArena(e.Pos, e.Radius);
        }

        void EnemyFire(Enemy e, V2 tpos)
        {
            float aim = (tpos - e.Pos).Angle;
            switch (e.Type)
            {
                case EnemyType.Grunt:
                    e.FireT = 1.8f;
                    for (int i = 0; i < 3; i++)
                        EnemyShot(e, aim + (i - 1) * 0.22f, 175f, Defs.SmallShot);
                    break;
                case EnemyType.Burster:
                {
                    e.FireT = 2.4f;
                    int n = 12 + Math.Min(Wave, 10);
                    e.Ang += MathX.Pi / n;
                    for (int i = 0; i < n; i++)
                        EnemyShot(e, e.Ang + MathX.Tau * i / n, 135f, Defs.SmallShot);
                    break;
                }
                case EnemyType.Sniper:
                    e.FireT = 2.6f;
                    EnemyShot(e, aim, 400f, Defs.FastShot);
                    break;
                case EnemyType.Spinner:
                    e.FireT = 0.11f;
                    e.Ang += 0.27f;
                    for (int k = 0; k < 3; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 125f, Defs.SmallShot, 0.25f);
                    break;
                case EnemyType.Boss:
                    switch ((int)(e.T / 6f) % 3)
                    {
                        case 0:  // four-armed spiral
                            e.FireT = 0.07f;
                            e.Ang += 0.19f;
                            for (int k = 0; k < 4; k++)
                                EnemyShot(e, e.Ang + MathX.Tau * k / 4f, 150f, Defs.SmallShot);
                            break;
                        case 1:  // slow rings of big orbs
                            e.FireT = 1f;
                            e.Ang += 0.12f;
                            for (int i = 0; i < 26; i++)
                                EnemyShot(e, e.Ang + MathX.Tau * i / 26f, 110f, Defs.BigShot);
                            break;
                        default:  // aimed fan
                            e.FireT = 0.45f;
                            for (int i = 0; i < 9; i++)
                                EnemyShot(e, aim + (i - 4) * 0.12f, 210f, Defs.SmallShot);
                            break;
                    }
                    break;
            }
        }

        void EnemyShot(Enemy e, float ang, float speed, byte kind, float angVel = 0f)
        {
            V2 dir = V2.FromAngle(ang);
            SpawnBullet(kind, e.Pos + dir * e.Radius, dir * speed, angVel, 8f, Defs.BulletDamage(kind), 0);
        }

        Bullet SpawnBullet(byte kind, V2 pos, V2 vel, float angVel, float life, float dmg, int owner)
        {
            var b = MakeBullet(nextBulletId++, kind, owner, pos, vel, angVel, life);
            b.Dmg = dmg;
            Player p;
            if (owner != 0 && Players.TryGetValue(owner, out p))
                b.Pierce = p.Stats.Pierce;
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
            if (kind == Defs.TurretShot)
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
                default: return Sfx.ShotSmg;
            }
        }

        readonly List<int> scratchIds = new List<int>();

        void MoveBullets(float dt)
        {
            scratchIds.Clear();
            foreach (var b in Bullets.Values)
            {
                if (b.AngVel != 0f)
                    b.Vel = b.Vel.Rotated(b.AngVel * dt);
                b.Pos += b.Vel * dt;
                b.Life -= dt;
                if (b.Life <= 0f || b.Pos.X < -40f || b.Pos.Y < -40f || b.Pos.X > Width + 40f || b.Pos.Y > Height + 40f)
                    scratchIds.Add(b.Id);
            }
            foreach (int id in scratchIds)
                Bullets.Remove(id);
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
            foreach (var b in Bullets.Values)
            {
                if (b.Kind < Defs.SmallShot)
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
                            e.Hp -= b.Dmg;
                            e.Flash = 0.06f;
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
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed)
                            continue;
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

            // Flak shells hurt everything near the impact, at 60% damage.
            foreach (var b in splashes)
            {
                foreach (var e in Enemies.Values)
                {
                    if (e.Id == b.LastHit || e.SpawnT > 0f || e.Pos.DistanceTo(b.Pos) > b.Splash + e.Radius)
                        continue;
                    e.Hp -= b.Dmg * 0.6f;
                    e.Flash = 0.06f;
                }
                BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(b.Kind), b.Splash, 0.3f, Sfx.Explode);
            }

            var dead = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (e.Hp <= 0f)
                    dead.Add(e);
            foreach (var e in dead)
            {
                if (e.Type == EnemyType.Boss)
                {
                    foreach (var p in Players.Values)
                        p.Rerolls += BossRerolls;
                    DoLog("boss killed: everyone gets " + BossRerolls + " rerolls");
                }
                Score += e.Score;
                GrantXp(e.Def.Xp);
                DeathFx(e, e.Pos);
                Enemies.Remove(e.Id);
            }
        }

        void RemovedFx(Bullet b)
        {
            AddFx(FxKind.Spark, b.Pos, Defs.BulletColor(b.Kind), 10f, 0.15f);
            if (b.Kind < Defs.SmallShot)
                Sounds.Add(Sfx.Hit);
        }

        void DeathFx(Enemy e, V2 pos)
        {
            AddFx(FxKind.Boom, pos, e.Def.Color, e.Radius * 2.2f, 0.4f);
            Sounds.Add(e.Type == EnemyType.Boss ? Sfx.BigExplode : Sfx.Explode);
        }

        void ClearEnemyBullets()
        {
            scratchIds.Clear();
            foreach (var b in Bullets.Values)
                if (b.Kind >= Defs.SmallShot)
                    scratchIds.Add(b.Id);
            foreach (int id in scratchIds)
                Bullets.Remove(id);
        }

        void BroadcastFx(FxKind kind, V2 pos, Rgba color, float radius, float dur, Sfx sound)
        {
            AddFx(kind, pos, color, radius, dur);
            Sounds.Add(sound);
            var w = Proto.Begin(Msg.Fx);
            w.Write((byte)kind);
            w.Write(pos);
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
                w.Write((short)p.Level);
                w.Write(p.Xp);
                w.Write((byte)p.PendingPicks);
                w.Write((short)p.Rerolls);
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
            }
            w.Write((byte)Turrets.Count);
            foreach (var t in Turrets.Values)
            {
                w.Write(t.Id);
                w.Write(t.Owner);
                w.Write(t.Pos);
                w.Write(t.T);
                w.Write(t.Aim);
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
                    Turrets.Clear();
                    break;
                case Msg.Fx:
                {
                    var kind = (FxKind)r.ReadByte();
                    V2 pos = r.ReadV2();
                    Rgba color = r.ReadRgba();
                    float radius = r.ReadSingle(), dur = r.ReadSingle();
                    AddFx(kind, pos, color, radius, dur);
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
            }
        }

        void ReadSnapshot(BinaryReader r)
        {
            State = (MatchState)r.ReadByte();
            Wave = r.ReadInt32();
            StateTimer = r.ReadSingle();
            Remaining = r.ReadInt32();
            Score = r.ReadInt32();
            int np = r.ReadByte();
            for (int i = 0; i < np; i++)
            {
                int id = r.ReadInt32();
                V2 pos = r.ReadV2();
                float aim = r.ReadSingle(), hp = r.ReadSingle();
                bool downed = r.ReadBoolean();
                float revive = r.ReadSingle(), barrier = r.ReadSingle(), invuln = r.ReadSingle(), overdrive = r.ReadSingle();
                int level = r.ReadInt16();
                float xp = r.ReadSingle();
                int pending = r.ReadByte();
                int rerolls = r.ReadInt16();
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
                p.Level = level;
                p.Xp = xp;
                p.PendingPicks = pending;
                p.Rerolls = rerolls;
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

            Turrets.Clear();
            int nt = r.ReadByte();
            for (int i = 0; i < nt; i++)
            {
                var t = new Turret { Id = r.ReadInt32(), Owner = r.ReadInt32(), Pos = r.ReadV2(), T = r.ReadSingle(), Aim = r.ReadSingle() };
                Turrets[t.Id] = t;
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
