using System;
using System.Collections.Generic;

namespace Arena
{
    /// Enemy waves, spawning, movement, attack patterns and boss mechanics (host only).
    public partial class Game
    {
        // A different boss every 5 waves up to the final one at 50; endless mode then cycles through them.
        static readonly EnemyType[] BossCycle =
        {
            EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus,
            EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse,
        };

        public const int MaxWave = 50;

        public const float EliteSize = 1.25f;
        const float ShieldHalfAngle = 1.13f;  // Shielder blocks shots within ~65 degrees of where it faces
        const float TwinReviveTime = 6f;
        const float TwinReviveHp = 0.6f;       // a revived Reaper comes back with this much health
        const float SoulBondGap = 0.1f;        // the more-hurt Reaper turns immune once it's this far behind...
        const float SoulBondRelease = 0.03f;   // ...until the other one is brought back within this
        const float SingularityPull = 150f;
        const float SpinnerCycle = 3.3f, SpinnerBurst = 1.3f;  // Spinner: spray for 1.3 s out of every 3.3 s
        const int WardenCross = 3;  // the Warden's fourth attack: a static "+" that cuts the arena into quadrants
        const float CrossPlant = 3f, CrossDash = 1f;  // each plant: dash 1 s to a new spot, then fire for 2 s

        static int WardenAttack(Enemy e) => (int)(e.T / 6f) % 4;

        /// During the cross attack: is the Warden dashing to its next spot (rather than planted and firing)?
        static bool CrossDashing(Enemy e) => MathX.Fmod(e.T, CrossPlant) < CrossDash;

        /// The boss that appears on a boss wave (every 5th).
        public static EnemyType BossForWave(int wave) => BossCycle[(Math.Max(5, wave) / 5 - 1) % BossCycle.Length];
        const int LateBossWave = 20;
        const float LateBossScaling = 0.1f;  // bosses after wave 20 get +10% health per wave on top (x2 at 30, x4 at 50)
        const int WellCount = 4;
        const float WellOrbit = 135f, WellRespawn = 15f;
        public const float WellPullRange = 170f;  // players' shots this close bend into a gravity well

        // Replicated to clients in the snapshot.
        public V2 PullCenter;      // The Singularity's gravity well
        public float PullStrength; // px/s toward PullCenter; 0 when inactive
        public float TwinReviveT;  // countdown before a fallen Twin Reaper revives; 0 when inactive

        // Host-only twin revive bookkeeping.
        EnemyType deadTwinType;
        V2 deadTwinPos;
        float deadTwinMaxHp;
        bool twinPending, bossRewarded;

        public static bool IsTwin(EnemyType t) => t == EnemyType.TwinA || t == EnemyType.TwinB;

        /// Soul Bond: this Twin Reaper is immune (it's too far below its twin's health).
        public static bool SoulBound(Enemy e) => IsTwin(e.Type) && e.Phase == 2;

        public Enemy OtherTwin(Enemy e)
        {
            foreach (var o in Enemies.Values)
                if (IsTwin(o.Type) && o.Id != e.Id && o.Hp > 0f && !IsSpawning(o))
                    return o;
            return null;
        }

        /// The Reapers' tether: a beam between the two that sweeps the arena as they circle.
        /// Touching it hurts; dashing through it is fine.
        void TickReaperTether()
        {
            Enemy a = null, b = null;
            foreach (var e in Enemies.Values)
                if (IsTwin(e.Type) && !IsSpawning(e))
                {
                    if (a == null) a = e; else b = e;
                }
            if (a == null || b == null)
                return;
            V2 d = b.Pos - a.Pos;
            float len = Math.Max(1f, d.Length);
            V2 dir = d / len;
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.InvulnT > 0f)
                    continue;
                float along = (p.Pos.X - a.Pos.X) * dir.X + (p.Pos.Y - a.Pos.Y) * dir.Y;
                if (along < a.Radius || along > len - b.Radius)
                    continue;
                if (p.Pos.DistanceTo(a.Pos + dir * along) < TetherWidth / 2f + p.Def.HitR)
                    Damage(p, 18f + Wave * 0.4f, "Twin Reapers (tether)");
            }
        }

        public const float TetherWidth = 8f;

        void ResetBossState()
        {
            PullStrength = 0f;
            TwinReviveT = 0f;
            twinPending = false;
        }

        // --- Waves -------------------------------------------------------------

        /// Enemy health for the party size: solo 70%, two players 100%, three players 120%
        /// (bosses 150%, since there are no extra bosses to share the load).
        float PartyHpScale(bool boss)
        {
            int n = Players.Count;
            if (n <= 1) return 0.7f;
            if (n == 2) return 1f;
            return boss ? 1.5f : 1.2f;
        }

        /// How much of a regular wave is melee: all of wave 1, none from wave 13 on.
        public static float MeleeShare(int wave) => Math.Max(0f, Math.Min(1f, (13f - wave) / 12f));

        void BuildWave(int n)
        {
            spawnQueue.Clear();
            bossRewarded = false;
            if (n % 5 == 0)
            {
                switch (BossCycle[(n / 5 - 1) % BossCycle.Length])
                {
                    case EnemyType.Boss:
                        spawnQueue.Add(EnemyType.Boss);
                        for (int i = 0; i < n / 2; i++)
                            spawnQueue.Add(EnemyType.Grunt);
                        break;
                    case EnemyType.HiveQueen:
                        spawnQueue.Add(EnemyType.HiveQueen);  // she brings her own swarm
                        break;
                    case EnemyType.TwinA:
                        spawnQueue.Add(EnemyType.TwinA);
                        spawnQueue.Add(EnemyType.TwinB);
                        break;
                    case EnemyType.Singularity:
                        spawnQueue.Add(EnemyType.Singularity);
                        for (int i = 0; i < n / 4; i++)
                            spawnQueue.Add(i % 2 == 0 ? EnemyType.Grunt : EnemyType.Sniper);
                        break;
                    case EnemyType.Colossus:
                        spawnQueue.Add(EnemyType.Colossus);
                        for (int i = 0; i < n / 5; i++)
                            spawnQueue.Add(EnemyType.Grunt);
                        break;
                    case EnemyType.StormHerald:
                        spawnQueue.Add(EnemyType.StormHerald);
                        for (int i = 0; i < n / 8; i++)
                            spawnQueue.Add(EnemyType.Sniper);
                        break;
                    default:  // Duelist, Lich, Devourer and the Eclipse fight alone (or bring their own help)
                        spawnQueue.Add(BossCycle[(n / 5 - 1) % BossCycle.Length]);
                        break;
                }
                return;
            }
            // New enemy types join the mix as the waves go up.
            var pool = new List<EnemyType> { EnemyType.Grunt };
            if (n >= 2) pool.Add(EnemyType.Burster);
            if (n >= 3) pool.Add(EnemyType.Sniper);
            if (n >= 4) pool.Add(EnemyType.Spinner);
            if (n >= 6) pool.Add(EnemyType.Charger);
            if (n >= 7) pool.Add(EnemyType.Splitter);
            if (n >= 8) pool.Add(EnemyType.Mortar);
            if (n >= 9) pool.Add(EnemyType.Shielder);
            if (n >= 11) pool.Add(EnemyType.Medic);
            if (n >= 16) pool.Add(EnemyType.Phantom);
            if (n >= 21) pool.Add(EnemyType.Hexer);
            if (n >= 26) pool.Add(EnemyType.Juggernaut);
            if (n >= 31) pool.Add(EnemyType.Nest);
            int count = Math.Min(4 + n * 2, n < 20 ? 32 : Math.Min(40, 32 + (n - 20) / 2));
            if (Players.Count >= 3)
                count = (int)(count * 1.25f);  // a third player: a quarter more enemies
            // The first waves are melee rushers; shooters take over a little more each wave until
            // the melee are gone by wave 13.
            var melee = new List<EnemyType> { EnemyType.Swarmer };
            if (n >= 3) melee.Add(EnemyType.Brute);
            if (n >= 5) melee.Add(EnemyType.Lunger);
            float meleeShare = MeleeShare(n);
            for (int i = 0; i < count; i++)
            {
                if (rng.NextDouble() < meleeShare)
                {
                    var type = melee[rng.Next(melee.Count)];
                    spawnQueue.Add(type);
                    if (type == EnemyType.Swarmer)
                        spawnQueue.Add(type);  // swarmers come in pairs
                }
                else
                    spawnQueue.Add(pool[rng.Next(pool.Count)]);
            }
        }

        void SpawnEnemy(EnemyType type)
        {
            var d = Defs.Get(type);
            float m = d.Radius + 30f;
            V2 pos;
            switch (rng.Next(4))
            {
                case 0: pos = new V2(Rand(m, Width - m), m); break;
                case 1: pos = new V2(Rand(m, Width - m), Height - m); break;
                case 2: pos = new V2(m, Rand(m, Height - m)); break;
                default: pos = new V2(Width - m, Rand(m, Height - m)); break;
            }
            if (type == EnemyType.Boss || type == EnemyType.HiveQueen)
                pos = new V2(Width / 2f, 140f);
            else if (type == EnemyType.TwinA)
                pos = new V2(Width / 2f - Width * 0.26f, 180f);
            else if (type == EnemyType.TwinB)
                pos = new V2(Width / 2f + Width * 0.26f, 180f);
            else if (type == EnemyType.Singularity || type == EnemyType.Duelist || type == EnemyType.Eclipse)
                pos = new V2(Width / 2f, Height / 2f);
            else if (d.IsBoss)
                pos = new V2(Width / 2f, 150f);
            else if (type == EnemyType.Nest)
                pos = new V2(Rand(160f, Width - 160f), Rand(120f, Height - 120f));

            // From wave 12, regular enemies can show up as tougher elites.
            bool elite = !d.IsBoss && Wave >= 12 && rng.NextDouble() < Math.Min(0.35, 0.04 * (Wave - 11));
            SpawnEnemyAt(type, pos, 0.9f, 0, elite);
        }

        Enemy SpawnEnemyAt(EnemyType type, V2 pos, float spawnT, int parent, bool elite = false)
        {
            var d = Defs.Get(type);
            var e = new Enemy { Id = nextEnemyId++, Type = type, Def = d, Radius = d.Radius, Speed = d.Speed, Score = d.Score, ParentId = parent, Elite = elite };
            e.MaxHp = d.Hp * (1f + 0.15f * (Wave - 1)) * PartyHpScale(d.IsBoss);
            // Players' damage multiplies as upgrades stack, so late bosses need more than linear scaling.
            if (d.IsBoss && Wave > LateBossWave)
                e.MaxHp *= 1f + LateBossScaling * (Wave - LateBossWave);
            if (elite)
            {
                e.MaxHp *= 2.5f;
                e.Radius *= EliteSize;
            }
            e.Hp = e.MaxHp;
            Recorder?.EnemySpawned(e);
            e.Pos = ClampToArena(pos, e.Radius);
            e.Disp = e.Pos;
            e.Waypoint = e.Pos;
            e.SpawnT = spawnT;
            e.FireT = Rand(0.3f, 1.2f);
            e.PhaseT = Rand(1f, 3f);
            e.Strafe = rng.NextDouble() < 0.5 ? 1f : -1f;
            e.Ang = Rand(0f, MathX.Tau);
            Enemies[e.Id] = e;
            return e;
        }

        int PickTarget()
        {
            var alive = new List<int>();
            var tanks = new List<int>();
            foreach (var p in Players.Values)
                if (!p.Downed && p.TauntT > 0f)
                    return p.Id;  // Roar: everyone goes for the roaring tank
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

        // --- Per-tick behaviour ------------------------------------------------

        void TickEnemies(float dt)
        {
            float rate = Math.Min(2f, 1f + 0.05f * (Wave - 1));
            PullStrength = 0f;
            foreach (var e in new List<Enemy>(Enemies.Values))  // copy: some enemies spawn others
            {
                e.T += dt;
                e.MarkT = Math.Max(0f, e.MarkT - dt);
                e.SlowT = Math.Max(0f, e.SlowT - dt);
                if (e.SlowT <= 0f)
                    e.SlowMult = 1f;
                TickBurn(e, dt);
                if (e.SpawnT > 0f)
                {
                    e.SpawnT -= dt;
                    continue;
                }
                if (e.FrozenT > 0f)  // Glacial Tomb: frozen solid, no moving or shooting
                {
                    e.FrozenT -= dt;
                    e.SlowT = Math.Max(e.SlowT, e.FrozenT);
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
                Face(e, tpos, dt);
                MoveEnemy(e, tpos, dt);
                if (State == MatchState.Fighting)
                {
                    Special(e, tpos, dt);
                    float mult = rate * (e.Elite ? 1.35f : 1f) * (IsTwin(e.Type) && e.Phase == 1 ? 1.7f : 1f);
                    e.FireT -= dt * mult;
                    if (e.FireT <= 0f)
                        EnemyFire(e, tpos);
                }
                // Bosses and heavies hurt to stand in, more so later on.
                float contact = e.Def.IsBoss ? 25f + Wave * 0.6f
                    : e.Type == EnemyType.Charger && e.Phase == 2 || e.Type == EnemyType.Juggernaut ? 25f
                    : e.Type == EnemyType.Lunger && e.Phase == 2 ? 26f
                    : e.Def.Contact > 0f ? e.Def.Contact : ContactDamage;
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f)
                        continue;
                    if (p.ParryActive && p.Pos.DistanceTo(e.Pos) < e.Radius + p.Def.BodyR + ParryReach && ParryContact(p, e, contact))
                        continue;
                    if (p.Pos.DistanceTo(e.Pos) < e.Radius + p.Def.HitR)
                        Damage(p, contact, e.Def.Name + " (contact)");
                }
            }

            TickHazards(dt);
            TickReaperTether();

            // A fallen Twin Reaper comes back unless its twin dies before the timer runs out.
            if (twinPending)
            {
                TwinReviveT = Math.Max(0f, TwinReviveT - dt);
                if (TwinReviveT <= 0f)
                {
                    twinPending = false;
                    var twin = SpawnEnemyAt(deadTwinType, deadTwinPos, 0.6f, 0);
                    twin.MaxHp = deadTwinMaxHp;
                    twin.Hp = deadTwinMaxHp * TwinReviveHp;
                    foreach (var other in Enemies.Values)
                        if (IsTwin(other.Type))
                            other.Phase = 0;
                    BroadcastFx(FxKind.Ring, deadTwinPos, twin.Def.Color, 120f, 0.6f, Sfx.Ability);
                    DoLog(twin.Def.Name + " revived");
                }
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
                        // Bosses don't get shoved around by small fry.
                        float wa = Heavy(a) ? 0f : (Heavy(b) ? 1f : 0.5f);
                        float wb = Heavy(b) ? 0f : (Heavy(a) ? 1f : 0.5f);
                        V2 push = d / l * (minD - l);
                        a.Pos -= push * wa;
                        b.Pos += push * wb;
                    }
                }
            }
        }

        void Face(Enemy e, V2 tpos, float dt)
        {
            float want = (tpos - e.Pos).Angle;
            if (e.Type == EnemyType.Charger && e.Phase != 0)
                return;  // locked in while winding up and dashing
            if (e.Type == EnemyType.Lunger && e.Phase >= 2)
                return;  // committed to the leap
            if (e.Type == EnemyType.Shielder)
            {
                // Turns slowly, so you can get around the shield.
                float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
                e.Facing += Math.Max(-1.6f * dt, Math.Min(1.6f * dt, diff));
                return;
            }
            e.Facing = want;
        }

        const float QueenSwarmHp = 3f;  // the Hive Queen's shards have 3x normal shard health

        /// Shielders block shots from the front; the Hive Queen is immune while her swarm protects her.
        bool BlocksShot(Enemy e, V2 from)
        {
            if (e.Type == EnemyType.HiveQueen && e.Phase == 1)
                return true;
            if (SoulBound(e))
                return true;  // the more-hurt Twin Reaper, until the other catches up
            if (e.Type == EnemyType.Lich && e.Phase == 1)
                return true;  // protected while its phylactery stands
            if (e.Type != EnemyType.Shielder)
                return false;
            float diff = Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
            return diff < ShieldHalfAngle;
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
            float speed = e.Speed;
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
                case EnemyType.Shard:
                    v = KeepDistance(dir, dist, 180f) + dir.Orthogonal() * e.Strafe;
                    break;
                case EnemyType.Mortar:
                    v = KeepDistance(dir, dist, 450f) + dir.Orthogonal() * e.Strafe * 0.4f;
                    break;
                case EnemyType.Shielder:
                    v = KeepDistance(dir, dist, 200f);
                    break;
                case EnemyType.Medic:
                    v = KeepDistance(dir, dist, 360f) + dir.Orthogonal() * e.Strafe * 0.5f;
                    break;
                case EnemyType.Splitter:
                    v = KeepDistance(dir, dist, 220f);
                    break;
                case EnemyType.Charger:
                    if (e.Phase == 0)
                        v = KeepDistance(dir, dist, 300f) + dir.Orthogonal() * e.Strafe * 0.5f;
                    else if (e.Phase == 2)
                    {
                        v = V2.FromAngle(e.Facing);
                        speed = 560f;
                    }
                    break;
                case EnemyType.Swarmer:  // straight at you, weaving a little
                    v = dir + dir.Orthogonal() * (float)Math.Sin(e.T * 5f + e.Id) * 0.45f;
                    break;
                case EnemyType.Brute:
                    v = dir;
                    break;
                case EnemyType.Lunger:
                    if (e.Phase == 0)
                        v = dist > 170f ? dir : dir.Orthogonal() * e.Strafe * 0.6f;  // close in, then circle while it winds up
                    else if (e.Phase == 2)
                    {
                        v = V2.FromAngle(e.Facing);
                        speed = 640f;
                    }
                    else if (e.Phase == 3)
                        speed *= 0.3f;  // recovering after the leap
                    break;
                case EnemyType.Spinner:
                    if (e.Pos.DistanceTo(e.Waypoint) < 20f)
                        e.Waypoint = new V2(Rand(150f, Width - 150f), Rand(150f, Height - 150f));
                    v = (e.Waypoint - e.Pos).Normalized();
                    break;
                case EnemyType.Boss:
                case EnemyType.HiveQueen:
                {
                    if (e.Type == EnemyType.Boss && WardenAttack(e) == WardenCross)
                    {
                        // Dash to the spot picked for this plant, then stand still while firing.
                        v = CrossDashing(e) && e.Pos.DistanceTo(e.Waypoint) > 8f ? (e.Waypoint - e.Pos).Normalized() : V2.Zero;
                        speed = 340f;
                        break;
                    }
                    var goal = new V2(Width / 2f + (float)Math.Sin(e.T * 0.5f) * Width * 0.3f, 150f + (float)Math.Sin(e.T * 0.8f) * 40f);
                    v = (goal - e.Pos) / 60f;
                    break;
                }
                case EnemyType.TwinA:
                case EnemyType.TwinB:
                {
                    // The twins circle the arena on opposite sides.
                    float a = e.T * 0.47f + (e.Type == EnemyType.TwinA ? 0f : MathX.Pi);
                    var goal = new V2(Width / 2f + (float)Math.Cos(a) * Width * 0.26f, Height / 2f + (float)Math.Sin(a) * Height * 0.28f);
                    v = (goal - e.Pos) / 60f;
                    break;
                }
            }
            if (IsLateGame(e.Type))
                v = LateMove(e, tpos, dir, dist, dt, ref speed);
            if (rng.NextDouble() < dt * 0.3)
                e.Strafe = -e.Strafe;
            if (e.SlowT > 0f && !Heavy(e))
                speed *= e.SlowMult;
            e.Pos += v.LimitLength(1f) * speed * dt;
            e.Pos = ClampToArena(e.Pos, e.Radius);
        }

        /// Behaviour that isn't just "shoot when the timer runs out".
        void Special(Enemy e, V2 tpos, float dt)
        {
            if (IsLateGame(e.Type))
            {
                LateSpecial(e, tpos, dt);
                return;
            }
            switch (e.Type)
            {
                case EnemyType.Boss:
                {
                    // The Warden rotates through four attacks, 6 s each; Phase tells clients which.
                    int attack = WardenAttack(e);
                    e.Phase = (byte)attack;
                    if (attack == WardenCross)
                    {
                        // A new spot somewhere in the middle of the arena for each plant (T2 counts them),
                        // so the quadrants come out a different size every time.
                        int plant = (int)(e.T / CrossPlant);
                        if (plant != (int)e.T2)
                        {
                            e.T2 = plant;
                            e.Waypoint = new V2(Rand(Width * 0.28f, Width * 0.72f), Rand(Height * 0.3f, Height * 0.7f));
                        }
                        e.Facing = 0f;  // arms along the arena's axes; clients draw the guides from this
                    }
                    break;
                }
                case EnemyType.TwinA:
                case EnemyType.TwinB:
                {
                    // Soul Bond: whichever Reaper is clearly more hurt is immune until you even them out.
                    var other = OtherTwin(e);
                    if (other == null || e.Phase == 1)
                        break;
                    float gap = other.Hp / other.MaxHp - e.Hp / e.MaxHp;
                    if (e.Phase == 0 && gap > SoulBondGap)
                        e.Phase = 2;
                    else if (e.Phase == 2 && gap < SoulBondRelease)
                        e.Phase = 0;
                    break;
                }
                case EnemyType.Lunger:
                {
                    // Phase 0: approach; 1: wind up (telegraph line); 2: leap; 3: recover.
                    float dist = tpos.DistanceTo(e.Pos);
                    e.PhaseT -= dt;
                    if (e.Phase == 0 && dist < 220f && e.PhaseT <= 0f)
                    {
                        e.Phase = 1;
                        e.PhaseT = 0.6f;
                    }
                    else if (e.Phase == 1)
                    {
                        e.Facing = (tpos - e.Pos).Angle;  // tracks you while winding up
                        if (e.PhaseT <= 0f)
                        {
                            e.Phase = 2;
                            e.PhaseT = 0.32f;
                        }
                    }
                    else if (e.Phase == 2 && e.PhaseT <= 0f)
                    {
                        e.Phase = 3;
                        e.PhaseT = 0.8f;
                    }
                    else if (e.Phase == 3 && e.PhaseT <= 0f)
                    {
                        e.Phase = 0;
                        e.PhaseT = Rand(0.8f, 1.6f);
                    }
                    break;
                }
                case EnemyType.Charger:
                    // Phase 1: wind up (telegraph line); phase 2: dash, then burst.
                    if (e.Phase == 0)
                        break;
                    e.PhaseT -= dt;
                    if (e.PhaseT > 0f)
                        break;
                    if (e.Phase == 1)
                    {
                        e.Phase = 2;
                        e.PhaseT = 0.45f;
                    }
                    else
                    {
                        e.Phase = 0;
                        e.FireT = 3.2f;
                        for (int i = 0; i < 10; i++)
                            EnemyShot(e, e.Facing + MathX.Tau * i / 10f, 140f, Defs.SmallShot);
                    }
                    break;
                case EnemyType.Medic:
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f)
                    {
                        e.PhaseT = 3f;
                        foreach (var o in Enemies.Values)
                            if (o != e && !o.Def.IsBoss && o.Pos.DistanceTo(e.Pos) < 180f)
                                o.Hp = Math.Min(o.MaxHp, o.Hp + o.MaxHp * 0.15f);
                        BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 180f, 0.5f, Sfx.None);
                    }
                    break;
                case EnemyType.HiveQueen:
                {
                    int swarm = 0;
                    foreach (var o in Enemies.Values)
                        if (o.ParentId == e.Id)
                            swarm++;
                    e.Phase = (byte)(swarm >= 3 ? 1 : 0);  // shielded while her swarm is big
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f && swarm < 8)
                    {
                        e.PhaseT = 6f;
                        for (int i = 0; i < 3; i++)
                        {
                            // Her swarm is tougher than ordinary shards, so it takes real effort to break her shield.
                            var drone = SpawnEnemyAt(EnemyType.Shard, e.Pos + V2.FromAngle(e.T + MathX.Tau * i / 3f) * 70f, 0.5f, e.Id);
                            drone.MaxHp *= QueenSwarmHp;
                            drone.Hp = drone.MaxHp;
                        }
                        BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 90f, 0.5f, Sfx.Ability);
                    }
                    break;
                }
                case EnemyType.Singularity:
                {
                    // 12 s cycle: 4 s gravity pull, 2 s collapse rings, 6 s normal.
                    float c = MathX.Fmod(e.T, 12f);
                    e.Phase = (byte)(c < 4f ? 1 : c < 6f ? 2 : 0);
                    if (e.Phase == 1)
                    {
                        PullCenter = e.Pos;
                        PullStrength = SingularityPull;
                    }
                    // Gravity wells orbit it and swallow your shots. Kill all of them and they
                    // come back 15 s later.
                    int wells = CountChildren(e, EnemyType.GravityWell);
                    e.T2 -= dt;
                    if (wells > 0)
                        e.T2 = Math.Max(e.T2, WellRespawn);
                    else if (e.T2 <= 0f)
                    {
                        for (int i = 0; i < WellCount; i++)
                        {
                            var w = SpawnEnemyAt(EnemyType.GravityWell, e.Pos + V2.FromAngle(MathX.Tau * i / WellCount) * WellOrbit, 0.8f, e.Id);
                            w.Ang = MathX.Tau * i / WellCount;
                        }
                        BroadcastFx(FxKind.Ring, e.Pos, Defs.Get(EnemyType.GravityWell).Color, WellOrbit + 40f, 0.6f, Sfx.Summon);
                    }
                    break;
                }
            }
        }

        void EnemyFire(Enemy e, V2 tpos)
        {
            float aim = (tpos - e.Pos).Angle;
            if (IsLateGame(e.Type))
            {
                LateFire(e, tpos, aim);
                return;
            }
            if (e.Def.Melee)
            {
                e.FireT = 99f;  // melee enemies don't shoot
                return;
            }
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
                {
                    // Bursts: a three-armed spiral for a moment, then a breather. Phase 1 = bursting.
                    float c = MathX.Fmod(e.T + e.Id * 0.7f, SpinnerCycle);
                    if (c >= SpinnerBurst)
                    {
                        e.Phase = 0;
                        e.FireT = SpinnerCycle - c;  // wait for the next burst
                        break;
                    }
                    e.Phase = 1;
                    e.FireT = 0.11f;
                    e.Ang += 0.27f;
                    for (int k = 0; k < 3; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 125f, Defs.SmallShot, 0.25f);
                    break;
                }
                case EnemyType.Charger:
                    if (e.Phase == 0)
                    {
                        e.Phase = 1;      // start winding up toward the target
                        e.PhaseT = 0.7f;
                        e.Facing = aim;
                    }
                    e.FireT = 99f;        // the dash cycle resets this
                    break;
                case EnemyType.Splitter:
                    e.FireT = 2f;
                    EnemyShot(e, aim - 0.08f, 150f, Defs.SmallShot);
                    EnemyShot(e, aim + 0.08f, 150f, Defs.SmallShot);
                    break;
                case EnemyType.Shard:
                    e.FireT = 1.6f;
                    EnemyShot(e, aim, 260f, Defs.FastShot);
                    break;
                case EnemyType.Mortar:
                {
                    // Lob a bomb at where the target is now; it bursts into a ring when it lands.
                    e.FireT = 3f;
                    const float flight = 1.3f;
                    V2 vel = ((tpos - e.Pos) / flight).LimitLength(330f);
                    var bomb = SpawnBullet(Defs.MineShot, e.Pos + V2.FromAngle(aim) * e.Radius, vel, 0f, flight, Defs.BulletDamage(Defs.MineShot), 0);
                    bomb.Burst = e.Elite ? 14 : 10;
                    bomb.Source = e.Type;
                    break;
                }
                case EnemyType.Shielder:
                    e.FireT = 2.2f;
                    for (int i = 0; i < 5; i++)
                        EnemyShot(e, e.Facing + (i - 2) * 0.15f, 190f, Defs.SmallShot);
                    break;
                case EnemyType.Medic:
                    e.FireT = 2.5f;
                    EnemyShot(e, aim, 160f, Defs.SmallShot);
                    break;
                case EnemyType.Boss:
                    switch (WardenAttack(e))
                    {
                        case WardenCross:  // planted: straight streams up, down, left and right (a wall of bullets)
                            e.FireT = 0.075f;
                            if (CrossDashing(e))
                                break;
                            for (int k = 0; k < 4; k++)
                                EnemyShot(e, MathX.Pi / 2f * k, 240f, Defs.SmallShot);
                            break;
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
                                EnemyShot(e, aim + (i - 4) * 0.12f, 210f, Defs.PierceShot);
                            break;
                    }
                    break;
                case EnemyType.HiveQueen:
                    if ((int)(e.T / 5f) % 2 == 0)
                    {
                        e.FireT = 0.7f;
                        for (int i = 0; i < 5; i++)
                            EnemyShot(e, aim + (i - 2) * 0.16f, 200f, Defs.PierceShot);
                    }
                    else
                    {
                        e.FireT = 1.6f;
                        e.Ang += 0.15f;
                        for (int i = 0; i < 20; i++)
                            EnemyShot(e, e.Ang + MathX.Tau * i / 20f, 100f, Defs.BigShot);
                    }
                    break;
                case EnemyType.TwinA:
                case EnemyType.TwinB:
                    if ((int)(e.T / 5f) % 2 == 0)
                    {
                        // Three "lines": bullets at rising speeds that stretch into streaks.
                        e.FireT = 1.4f;
                        for (int l = -1; l <= 1; l++)
                            for (int k = 0; k < 6; k++)
                                EnemyShot(e, aim + l * 0.5f, 140f + k * 35f, l == 0 ? Defs.PierceShot : Defs.SmallShot);
                    }
                    else
                    {
                        // A rotating cross.
                        e.FireT = 0.9f;
                        e.Ang += 0.35f;
                        for (int d = 0; d < 4; d++)
                            for (int k = 0; k < 3; k++)
                                EnemyShot(e, e.Ang + MathX.Tau * d / 4f, 120f + k * 50f, Defs.SmallShot);
                    }
                    break;
                case EnemyType.Singularity:
                    switch (e.Phase)
                    {
                        case 1:  // gravity pull: slow three-armed spiral
                            e.FireT = 0.09f;
                            e.Ang += 0.21f;
                            for (int k = 0; k < 3; k++)
                                EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 110f, Defs.SmallShot);
                            break;
                        case 2:  // collapse: dense rings
                            e.FireT = 0.7f;
                            e.Ang += 0.09f;
                            for (int i = 0; i < 36; i++)
                                EnemyShot(e, e.Ang + MathX.Tau * i / 36f, 170f, Defs.SmallShot);
                            break;
                        default:  // aimed fan plus a rotating cross
                            e.FireT = 0.5f;
                            e.Ang += 0.3f;
                            for (int i = 0; i < 7; i++)
                                EnemyShot(e, aim + (i - 3) * 0.13f, 200f, Defs.PierceShot);
                            for (int d = 0; d < 4; d++)
                                EnemyShot(e, e.Ang + MathX.Tau * d / 4f, 150f, Defs.BigShot);
                            break;
                    }
                    break;
            }
        }

        void EnemyShot(Enemy e, float ang, float speed, byte kind, float angVel = 0f)
        {
            V2 dir = V2.FromAngle(ang);
            SpawnBullet(kind, e.Pos + dir * e.Radius, dir * speed, angVel, 8f, Defs.BulletDamage(kind), 0, e.Def.IsBoss).Source = e.Type;
        }

        /// Death side effects. Returns true when this kill earns the boss rewards
        /// (rerolls and the big orb drop).
        bool OnEnemyKilled(Enemy e)
        {
            if (e.Type == EnemyType.Singularity)
                foreach (var o in Enemies.Values)
                    if (o.Type == EnemyType.GravityWell)
                        o.Hp = 0f;  // its wells collapse with it
            if (e.Type == EnemyType.Splitter)
            {
                int n = e.Elite ? 5 : 3;
                for (int i = 0; i < n; i++)
                    SpawnEnemyAt(EnemyType.Shard, e.Pos + V2.FromAngle(MathX.Tau * i / n) * 20f, 0.15f, 0);
            }
            if (!e.Def.IsBoss)
                return false;

            if (IsTwin(e.Type))
            {
                Enemy other = null;
                foreach (var o in Enemies.Values)
                    if (IsTwin(o.Type) && o.Id != e.Id && o.Hp > 0f)
                        other = o;
                if (other != null)
                {
                    // The survivor enrages; kill it before the timer runs out or this one comes back.
                    other.Phase = 1;
                    twinPending = true;
                    TwinReviveT = TwinReviveTime;
                    deadTwinType = e.Type;
                    deadTwinPos = e.Pos;
                    deadTwinMaxHp = e.MaxHp;
                    DoLog(e.Def.Name + " fell; " + other.Def.Name + " enrages");
                    return false;
                }
                twinPending = false;
                TwinReviveT = 0f;
            }
            if (bossRewarded)
                return false;
            bossRewarded = true;
            var bossKey = IsTwin(e.Type) ? EnemyType.TwinA : e.Type;
            foreach (var p in Players.Values)
            {
                p.Rerolls += BossRerolls;
                p.LegendaryQueue.Add(bossKey);  // picked at the end of the wave
                p.PendingLegendary = p.LegendaryQueue.Count;
                p.PendingMods++;                // then a weapon mod
            }
            DoLog(e.Def.Name + " defeated: everyone gets " + BossRerolls + " rerolls");
            return true;
        }
    }
}
