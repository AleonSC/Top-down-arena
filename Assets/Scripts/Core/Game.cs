using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    public enum MatchState : byte { Idle, Fighting, Intermission, GameOver }

    public enum FxKind : byte { Boom, Spark, Ring }

    public class Player
    {
        public int Id;
        public Role Role;
        public RoleDef Def;
        public string Name;
        public V2 Pos, Disp;
        public float Aim;
        public bool Firing;
        public float Hp, MaxHp;
        public bool Downed;
        public float Revive, FireCd, AbilityCd, BarrierT, InvulnT, Flash;
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
        public int Owner;
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
        public V2 Move;     // desired direction, length <= 1
        public V2 AimAt;    // point in arena coordinates
        public bool Fire;
        public bool Ability;  // held state; Game detects the press
    }

    /// The match. The host simulates enemies, bullets, damage and waves; every
    /// peer moves its own player locally and renders the shared state.
    ///
    /// Network traffic:
    ///   client -> host  Input (every tick: position/aim/firing), Ability
    ///   host -> all     Spawn / Remove (bullets fly deterministically, so only
    ///                   spawns and hits are sent), Snapshot (20 Hz: players,
    ///                   enemies and wave state), Fx, ClearEnemyBullets
    public class Game
    {
        public const float Width = 1280f, Height = 720f;

        const int SnapshotEvery = 3;  // ticks
        const float StartDelay = 3f;
        const float IntermissionTime = 5f;
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

        readonly Session session;
        readonly Random rng = new Random();

        public bool Running;
        public MatchState State = MatchState.Idle;
        public int Wave, Remaining, Score;
        public float StateTimer;

        public readonly SortedDictionary<int, Player> Players = new SortedDictionary<int, Player>();
        public readonly Dictionary<int, Enemy> Enemies = new Dictionary<int, Enemy>();
        public readonly Dictionary<int, Bullet> Bullets = new Dictionary<int, Bullet>();
        public readonly List<Effect> Effects = new List<Effect>();

        public float MyAbilityCd, DashT, AnimT;
        public V2 DashDir = new V2(1f, 0f);
        bool abilityWasDown;

        public bool Bot;  // drive the local player automatically (testing)
        public Action<string> Log;
        float botT;

        // Host-only simulation state.
        int nextBulletId = 1, nextEnemyId = 1, tick;
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
                var def = Defs.Get(role);
                var p = new Player { Id = id, Role = role, Def = def, Name = name, Hp = def.Hp, MaxHp = def.Hp, Aim = -MathX.Pi / 2f };
                p.Pos = new V2(Width / 2f + (i - (n - 1) / 2f) * 90f, Height / 2f + 140f);
                p.Disp = p.Pos;
                Players[id] = p;
            }
            Wave = Math.Max(0, startWave - 1);
            State = MatchState.Intermission;
            StateTimer = StartDelay;
            Running = true;
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
            Effects.Clear();
            spawnQueue.Clear();
            pendingSpawns.Clear();
            pendingRemoved.Clear();
            nextBulletId = 1;
            nextEnemyId = 1;
            MyAbilityCd = 0f;
            DashT = 0f;
            tick = 0;
        }

        public void OnPeerLeft(int id)
        {
            Players.Remove(id);
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
                p.Flash = Math.Max(0f, p.Flash - dt);
        }

        /// Per-frame visual update: smoothing and effects.
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
        }

        void LocalControl(float dt, PlayerInput input)
        {
            Player me;
            if (!Players.TryGetValue(MyId, out me))
                return;
            MyAbilityCd = Math.Max(0f, MyAbilityCd - dt);
            V2 move = input.Move.LimitLength(1f);
            me.Aim = (input.AimAt - me.Pos).Angle;
            bool firing = input.Fire && !me.Downed;
            bool abilityPressed = input.Ability && !abilityWasDown;
            abilityWasDown = input.Ability;

            if (Bot)
            {
                botT += dt;
                move = V2.FromAngle(botT * 0.9f + MyId);
                Enemy nearest = null;
                foreach (var e in Enemies.Values)
                    if (nearest == null || e.Pos.DistanceSq(me.Pos) < nearest.Pos.DistanceSq(me.Pos))
                        nearest = e;
                if (nearest != null)
                    me.Aim = (nearest.Pos - me.Pos).Angle;
                firing = !me.Downed;
                abilityPressed = MathX.Fmod(botT, 4f) < dt;
            }

            if (me.Downed)
                DashT = 0f;
            else if (DashT > 0f)
            {
                DashT -= dt;
                me.Pos += DashDir * DashSpeed * dt;
            }
            else
                me.Pos += move * me.Def.Speed * dt;

            if (abilityPressed && !me.Downed && MyAbilityCd <= 0f && State != MatchState.GameOver)
            {
                MyAbilityCd = me.Def.AbilityCd;
                if (me.Role == Role.Dps)
                {
                    DashDir = move.IsZero ? V2.FromAngle(me.Aim) : move.Normalized();
                    DashT = DashTime;
                }
                if (IsHost)
                    HostAbility(MyId);
                else
                    session.SendToHost(Proto.End(Proto.Begin(Msg.Ability)));
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

        // --- Host simulation ---------------------------------------------------

        void HostTick(float dt)
        {
            TickPlayers(dt);
            TickWaves(dt);
            TickEnemies(dt);
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
                session.Broadcast(Snapshot());
            if (tick % 120 == 0)
                DoLog(string.Format("wave {0} state {1} enemies {2} bullets {3} score {4} hp {5}", Wave, State, Enemies.Count, Bullets.Count, Score, HpSummary()));
        }

        void TickPlayers(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.AbilityCd = Math.Max(0f, p.AbilityCd - dt);
                p.BarrierT = Math.Max(0f, p.BarrierT - dt);
                p.InvulnT = Math.Max(0f, p.InvulnT - dt);
                p.FireCd -= dt;
                if (p.Downed)
                    continue;
                if (p.Firing && p.FireCd <= 0f && State != MatchState.GameOver)
                {
                    p.FireCd = p.Def.FireInterval;
                    FirePlayer(p);
                }
                if (p.Role == Role.Healer)
                {
                    p.Hp = Math.Min(p.MaxHp, p.Hp + 3f * dt);
                    foreach (var q in Players.Values)
                        if (q != p && !q.Downed && q.Pos.DistanceTo(p.Pos) < AuraRange)
                            q.Hp = Math.Min(q.MaxHp, q.Hp + AuraHeal * dt);
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
            V2 dir = V2.FromAngle(p.Aim);
            V2 muzzle = p.Pos + dir * p.Def.BodyR;
            switch (p.Role)
            {
                case Role.Tank:
                    for (int i = 0; i < 5; i++)
                        SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(p.Aim + (i - 2) * 0.13f) * 520f, 0f, 0.5f, 7f, p.Id);
                    break;
                case Role.Healer:
                    SpawnBullet(Defs.HealerShot, muzzle, dir * 650f, 0f, 1.3f, 11f, p.Id);
                    break;
                case Role.Dps:
                    V2 side = dir.Orthogonal() * 5f;
                    SpawnBullet(Defs.DpsShot, muzzle + side, dir * 820f, 0f, 1f, 6.5f, p.Id);
                    SpawnBullet(Defs.DpsShot, muzzle - side, dir * 820f, 0f, 1f, 6.5f, p.Id);
                    break;
            }
        }

        public void HostAbility(int id)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Downed || p.AbilityCd > 0.5f)
                return;
            p.AbilityCd = p.Def.AbilityCd;
            switch (p.Role)
            {
                case Role.Tank:
                    p.BarrierT = BarrierTime;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.5f, 0.8f, 1f), BarrierRadius, 0.4f);
                    break;
                case Role.Dps:
                    p.InvulnT = Math.Max(p.InvulnT, DashTime + 0.12f);
                    break;
                case Role.Healer:
                    foreach (var q in Players.Values)
                    {
                        if (q.Pos.DistanceTo(p.Pos) >= PulseRange)
                            continue;
                        if (q.Downed)
                            RevivePlayer(q, 0.5f);
                        else
                            q.Hp = Math.Min(q.MaxHp, q.Hp + PulseHeal);
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.4f, 1f, 0.55f), PulseRange, 0.6f);
                    break;
            }
        }

        void Damage(Player p, float amount)
        {
            p.Hp -= amount;
            p.InvulnT = HitIframes;
            p.Flash = 0.1f;
            if (p.Hp <= 0f)
            {
                p.Hp = 0f;
                p.Downed = true;
                p.Revive = 0f;
                p.BarrierT = 0f;
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
            DoLog("game over at wave " + Wave + ", score " + Score);
        }

        void TickWaves(float dt)
        {
            switch (State)
            {
                case MatchState.Intermission:
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
            }
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

        void SpawnBullet(byte kind, V2 pos, V2 vel, float angVel, float life, float dmg, int owner)
        {
            var b = MakeBullet(nextBulletId++, kind, pos, vel, angVel, life);
            b.Dmg = dmg;
            b.Owner = owner;
            pendingSpawns.Add(new Bullet { Id = b.Id, Kind = kind, Pos = pos, Vel = vel, AngVel = angVel, Life = life });
        }

        Bullet MakeBullet(int id, byte kind, V2 pos, V2 vel, float angVel, float life)
        {
            var b = new Bullet { Id = id, Kind = kind, Pos = pos, Vel = vel, AngVel = angVel, Life = life, Radius = Defs.BulletRadius(kind) };
            Bullets[id] = b;
            return b;
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

        void Collide()
        {
            scratchIds.Clear();
            foreach (var b in Bullets.Values)
            {
                if (b.Kind < Defs.SmallShot)
                {
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Hp <= 0f)
                            continue;
                        float r = e.Radius + b.Radius;
                        if (b.Pos.DistanceSq(e.Pos) < r * r)
                        {
                            e.Hp -= b.Dmg;
                            e.Flash = 0.06f;
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
                        if (p.BarrierT > 0f && b.Pos.DistanceTo(p.Pos) < BarrierRadius)
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
                AddFx(FxKind.Spark, b.Pos, Defs.BulletColor(b.Kind), 10f, 0.15f);
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }

            var dead = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (e.Hp <= 0f)
                    dead.Add(e);
            foreach (var e in dead)
            {
                Score += e.Score;
                AddFx(FxKind.Boom, e.Pos, e.Def.Color, e.Radius * 2.2f, 0.4f);
                Enemies.Remove(e.Id);
            }
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

        void BroadcastFx(FxKind kind, V2 pos, Rgba color, float radius, float dur)
        {
            AddFx(kind, pos, color, radius, dur);
            var w = Proto.Begin(Msg.Fx);
            w.Write((byte)kind);
            w.Write(pos);
            w.Write(color);
            w.Write(radius);
            w.Write(dur);
            session.Broadcast(Proto.End(w));
        }

        byte[] Snapshot()
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
                w.Write(p.AbilityCd);
                w.Write(p.BarrierT);
                w.Write(p.InvulnT);
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
            return Proto.End(w);
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
                        V2 pos = r.ReadV2(), vel = r.ReadV2();
                        float angVel = r.ReadSingle(), life = r.ReadSingle();
                        MakeBullet(id, kind, pos, vel, angVel, life);
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
                            AddFx(FxKind.Spark, b.Pos, Defs.BulletColor(b.Kind), 10f, 0.15f);
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
                    V2 pos = r.ReadV2();
                    Rgba color = r.ReadRgba();
                    float radius = r.ReadSingle(), dur = r.ReadSingle();
                    AddFx(kind, pos, color, radius, dur);
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
                float revive = r.ReadSingle(), abilityCd = r.ReadSingle(), barrier = r.ReadSingle(), invuln = r.ReadSingle();
                Player p;
                if (!Players.TryGetValue(id, out p))
                    continue;
                if (id != MyId)
                {
                    p.Pos = pos;
                    p.Aim = aim;
                }
                if (hp < p.Hp)
                    p.Flash = 0.1f;
                p.Hp = hp;
                p.Downed = downed;
                p.Revive = revive;
                p.AbilityCd = abilityCd;
                p.BarrierT = barrier;
                p.InvulnT = invuln;
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
                AddFx(FxKind.Boom, e.Disp, e.Def.Color, e.Radius * 2.2f, 0.4f);
                Enemies.Remove(e.Id);
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
                parts.Add(p.Role + ":" + (int)p.Hp + (p.Downed ? "(down)" : ""));
            return string.Join(" ", parts);
        }

        void DoLog(string msg)
        {
            if (Log != null)
                Log("[" + (IsHost ? "host" : "client") + "] " + msg);
        }
    }
}
