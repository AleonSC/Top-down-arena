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
        // A Shielder's shield holds this share of its health; blocked hits wear it down, and it crumbles by itself
        // at ShieldDecay of its strength a second, cracking as it goes (Phase 1-3), until it shatters (Phase 4).
        const float ShieldShare = 0.6f, ShieldDecay = 0.03f;
        public const byte ShieldBroken = 4;
        const float TwinReviveTime = 6f;
        const float TwinReviveHp = 0.6f;       // a revived Reaper comes back with this much health
        const float SoulBondGap = 0.1f;        // the more-hurt Reaper turns immune once it's this far behind...
        const float SoulBondRelease = 0.03f;   // ...until the other one is brought back within this
        const float SingularityPull = 150f;
        const float SpinnerCycle = 3.3f, SpinnerBurst = 1.3f;  // Spinner: spray for 1.3 s out of every 3.3 s
        const float CrossPlant = 3f, CrossDash = 1f;  // the Warden's cross: each plant, dash 1 s to a new spot, then fire for 2 s

        /// The boss that appears on a boss wave (every 5th).
        public static EnemyType BossForWave(int wave) => BossCycle[(Math.Max(5, wave) / 5 - 1) % BossCycle.Length];
        const int LateBossWave = 20;
        const float LateBossScaling = 0.1f;  // bosses after wave 20 get +10% health per wave on top (x2 at 30, x4 at 50)
        const float WellRespawn = 15f;

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
                        for (int i = 0; i < n / 6; i++)
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
            if (BuildPathWave(n, ref count, pool, melee))  // a path's own mix (Paths.cs)
                return;
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
            V2 lo = ArenaMin + new V2(m, m), hi = ArenaMax - new V2(m, m);  // along the edges (Narrow Pass: its walls)
            V2 pos;
            switch (rng.Next(4))
            {
                case 0: pos = new V2(Rand(lo.X, hi.X), lo.Y); break;
                case 1: pos = new V2(Rand(lo.X, hi.X), hi.Y); break;
                case 2: pos = new V2(lo.X, Rand(lo.Y, hi.Y)); break;
                default: pos = new V2(hi.X, Rand(lo.Y, hi.Y)); break;
            }
            // Bosses appear in the middle of the arena (the Twin Reapers on either side of it, where their circle starts).
            if (type == EnemyType.TwinA)
                pos = new V2(Width / 2f - Width * 0.26f, Height / 2f);
            else if (type == EnemyType.TwinB)
                pos = new V2(Width / 2f + Width * 0.26f, Height / 2f);
            else if (d.IsBoss)
                pos = new V2(Width / 2f, Height / 2f);
            else if (type == EnemyType.Nest)
                pos = RandomInArena(Walled ? 80f : 160f, Walled ? 80f : 120f);

            // From wave 12, regular enemies can show up as tougher elites.
            bool elite = !d.IsBoss && Wave >= 12 && rng.NextDouble() < Math.Min(0.35, 0.04 * (Wave - 11));
            SpawnEnemyAt(type, pos, d.IsBoss ? 1.5f : 0.9f, 0, elite);  // bosses take longer to appear, so you can step out of the middle
        }

        Enemy SpawnEnemyAt(EnemyType type, V2 pos, float spawnT, int parent, bool elite = false)
        {
            var d = Defs.Get(type);
            var e = new Enemy { Id = nextEnemyId++, Type = type, Def = d, Radius = d.Radius, Speed = d.Speed, Score = d.Score, ParentId = parent, Elite = elite };
            e.MaxHp = d.Hp * (1f + 0.15f * (Wave - 1)) * PartyHpScale(d.IsBoss);
            // Players' damage multiplies as upgrades stack, so late bosses need more than linear scaling.
            if (d.IsBoss && Wave > LateBossWave)
                e.MaxHp *= 1f + LateBossScaling * (Wave - LateBossWave);
            if (Has(RunMod.ToughCrowd))
                e.MaxHp *= Modifiers.ToughCrowdHp;
            if (Has(RunMod.SwiftFoes))
                e.Speed *= Modifiers.SwiftFoesSpeed;
            ApplyPathToEnemy(e, ref elite);  // Paths.cs
            e.Elite = elite;
            if (elite)
            {
                e.MaxHp *= 2.5f;
                e.Radius *= EliteSize;
            }
            e.Hp = e.MaxHp;
            if (type == EnemyType.Shielder)
                e.ShieldHp = e.ShieldMax = e.MaxHp * ShieldShare;
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
                if (!p.Downed && (p.TauntT > 0f || BloodTaunting(p)))
                    return p.Id;  // Roar: everyone goes for the roaring tank
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.VanishT > 0f)  // a vanished Ninja can't be picked out (Ninja.cs)
                    continue;
                alive.Add(p.Id);
                if (p.Role == Role.Tank)
                    tanks.Add(p.Id);
            }
            if (alive.Count == 0)
                foreach (var p in Players.Values)
                    if (!p.Downed)
                        alive.Add(p.Id);  // everyone has vanished: they search where they last saw them
            if (alive.Count == 0)
                return 0;
            if (tanks.Count > 0 && rng.NextDouble() < 0.7)
                return tanks[rng.Next(tanks.Count)];
            return alive[rng.Next(alive.Count)];
        }

        // --- Per-tick behaviour ------------------------------------------------

        // --- Bullet budget ---------------------------------------------------------------
        // A cap on enemy bullets on screen, so later waves get harder through tougher enemies rather than
        // a wall of bullets. Regular enemies hold their fire while the screen is over budget; bosses always
        // fire, and while one is up the others only get a small share, so the boss's patterns stay readable.

        int enemyShots;  // enemy bullets in flight, counted each tick (and as they're fired)

        public int ShotBudget => (int)(Math.Min(120f, 50f + 4f * Wave) * (1f + 0.3f * (Players.Count - 1)));

        int RegularShotBudget()
        {
            foreach (var e in Enemies.Values)
                if (e.Def.IsBoss)
                    return (int)(ShotBudget * 0.4f);
            return ShotBudget;
        }

        void CountEnemyShots()
        {
            enemyShots = 0;
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind))
                    enemyShots++;
        }

        void TickEnemies(float dt)
        {
            float rate = Math.Min(2f, 1f + 0.05f * (Wave - 1));
            PullStrength = 0f;
            CountEnemyShots();
            foreach (var e in new List<Enemy>(Enemies.Values))  // copy: some enemies spawn others
            {
                e.T += dt;
                e.MarkT = Math.Max(0f, e.MarkT - dt);
                e.SlowT = Math.Max(0f, e.SlowT - dt);
                if (e.SlowT <= 0f)
                    e.SlowMult = 1f;
                TickBurn(e, dt);
                TickBleed(e, dt);  // Minions.cs
                if (e.SpawnT > 0f)
                {
                    e.SpawnT -= dt;
                    continue;
                }
                if (ShowcaseMode)
                    continue;  // the loadout preview's target dummies stand still and never shoot (Showcase.cs)
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
                tpos = NinjaTarget(e, target, tpos);  // Shadow Clones draw them off; Vanish loses them (Ninja.cs)
                if (e.Type != EnemyType.Singularity)  // its Facing is the lasers' sweep, not where it looks
                    Face(e, tpos, dt);
                MoveEnemy(e, tpos, dt);
                if (State == MatchState.Fighting)
                {
                    Special(e, tpos, dt);
                    if (e.Def.IsBoss)
                        BossCues(e);  // BossSounds.cs
                    float mult = rate * (e.Elite ? 1.35f : 1f) * (IsTwin(e.Type) && e.Phase == 1 ? 1.7f : 1f) * (Has(RunMod.Frenzy) ? Modifiers.FrenzyFire : 1f);
                    e.FireT -= dt * mult;
                    if (e.FireT <= 0f)
                    {
                        if (!e.Def.IsBoss && enemyShots >= RegularShotBudget())
                            e.FireT = Rand(0.2f, 0.4f);  // the screen is full: hold this volley and try again shortly
                        else
                            EnemyFire(e, tpos);
                    }
                }
                if (e.Hp <= 0f || e.Type == EnemyType.HiveBomber || e.Type == EnemyType.HiveEgg || QueenAirborne(e))
                    continue;  // blew up this tick, explodes instead of bumping, an egg, or the Hive Queen up in a dive
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
                    {
                        if (e.Def.IsBoss)
                        {
                            // A boss's body hurts once and shoves you out (the shove happens on your own game).
                            if (p.BossContactT > 0f)
                                continue;
                            p.BossContactT = BossContactEvery;
                        }
                        Damage(p, contact, e.Def.Name + " (contact)");
                    }
                }
            }

            TickHazards(dt);
            TickReaperTether();
            TickSingularityHazards(dt);

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
            if (e.Type == EnemyType.Lunger && e.Phase >= 2 || e.Type == EnemyType.HiveBomber)
                return;  // committed to the leap / flight line
            if (e.Type == EnemyType.Shielder)
            {
                // Turns slowly, so you can get around the shield.
                float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
                e.Facing += Math.Max(-1.6f * dt, Math.Min(1.6f * dt, diff));
                return;
            }
            e.Facing = want;
        }

        /// A hit the shield stopped (or time) wears it down; it cracks in three steps, then shatters.
        void WearShield(Enemy e, float amount)
        {
            if (e.Type != EnemyType.Shielder || e.Phase >= ShieldBroken || e.ShieldMax <= 0f)
                return;
            e.ShieldHp -= amount;
            float frac = e.ShieldHp / e.ShieldMax;
            byte phase = (byte)(frac <= 0f ? ShieldBroken : frac <= 0.25f ? 3 : frac <= 0.5f ? 2 : frac <= 0.75f ? 1 : 0);
            if (phase == e.Phase)
                return;
            e.Phase = phase;
            if (phase == ShieldBroken)
                BroadcastFx(FxKind.Shatter, e.Pos, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 7f), new Rgba(0.7f, 0.85f, 1f), ShieldHalfAngle, 0.6f, Sfx.Clang);
            else
                BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 7f), new Rgba(0.8f, 0.9f, 1f), 16f, 0.25f, Sfx.Hit);
        }

        /// Shielders block shots from the front; the Hive Queen is immune while her swarm protects her.
        bool BlocksShot(Enemy e, V2 from)
        {
            if (e.Type == EnemyType.HiveQueen && (e.Phase == 1 || QueenImmune(e)))
                return true;  // shielded by her drones, changing stage, or up in a dive (Queen.cs)
            if (e.Type == EnemyType.Boss && (WardenImmune(e) || WardenShieldBlocks(e, from)))
                return true;  // changing stage, or behind its riot shield (Warden.cs)
            if (SoulBound(e))
                return true;  // the more-hurt Twin Reaper, until the other catches up
            if (e.Type == EnemyType.Lich && e.Phase == 1)
                return true;  // protected while its phylactery stands
            if (e.Type != EnemyType.Shielder || e.Phase >= ShieldBroken)
                return false;
            float diff = Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
            return diff < ShieldHalfAngle;
        }

        int AlivePlayers()
        {
            int n = 0;
            foreach (var p in Players.Values)
                if (!p.Downed)
                    n++;
            return n;
        }

        /// An explosive drone that flies straight to `at` (where its target was when it launched).
        void LaunchBomber(Enemy queen, V2 at)
        {
            V2 dir = (at - queen.Pos).Normalized();
            var b = SpawnEnemyAt(EnemyType.HiveBomber, queen.Pos + dir * (queen.Radius + 8f), 0.15f, queen.Id);
            b.Waypoint = at;
            b.Facing = dir.Angle;
            float flight = 0.15f + Math.Max(0f, at.DistanceTo(b.Pos) - 12f) / b.Speed;
            BroadcastFx(FxKind.Target, at, b.Def.Color, BomberBlast, flight, Sfx.None);
        }

        void BomberExplode(Enemy e)
        {
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.InvulnT > 0f || p.Pos.DistanceTo(e.Pos) > BomberBlast + p.Def.HitR)
                    continue;
                if (p.ParryActive && ParryContact(p, e, BomberDamage))
                    continue;  // a raised guard facing the blast takes it
                Damage(p, BomberDamage, e.Def.Name);
            }
            BroadcastFx(FxKind.Boom, e.Pos, e.Def.Color, BomberBlast, 0.35f, Sfx.Explode);
            e.LastHitBy = 0;  // no kill credit for blowing itself up
            e.Hp = 0f;
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
                    v = DroneGoingHome(e) ? DroneHomeMove(e, ref speed)  // the Hive Queen's drones, flying back into her (Queen.cs)
                        : KeepDistance(dir, dist, 180f) + dir.Orthogonal() * e.Strafe;
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
                case EnemyType.HiveBomber:  // a straight line to the spot it was launched at
                    v = e.Waypoint - e.Pos;
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
                        e.Waypoint = RandomInArena(Walled ? 80f : 150f);
                    v = (e.Waypoint - e.Pos).Normalized();
                    break;
                case EnemyType.Boss:
                    v = WardenMove(e, tpos, ref speed);  // Warden.cs
                    break;
                case EnemyType.Rioter:
                    v = RioterMove(e, tpos);  // Warden.cs
                    break;
                case EnemyType.Guard:
                    v = GuardMove(e, tpos);  // Warden.cs
                    break;
                case EnemyType.HiveQueen:
                    v = QueenMove(e, tpos, ref speed);  // Queen.cs
                    break;
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
                case EnemyType.Shielder:
                    WearShield(e, e.ShieldMax * ShieldDecay * dt);
                    break;
                case EnemyType.Boss:
                    WardenSpecial(e, tpos, dt);  // Warden.cs: its stages, turns, searchlight, gates and riot
                    break;
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
                    QueenSpecial(e, tpos, dt);  // Queen.cs: her stages and turns
                    break;
                case EnemyType.HiveBomber:
                {
                    // Blows up when it reaches the spot it was launched at, or on touching anyone on the way.
                    bool hit = e.Pos.DistanceTo(e.Waypoint) < 12f;
                    foreach (var p in Players.Values)
                        hit |= !p.Downed && p.Pos.DistanceTo(e.Pos) < e.Radius + p.Def.HitR;
                    if (hit)
                        BomberExplode(e);
                    break;
                }
                case EnemyType.Singularity:
                    SingularitySpecial(e, dt);  // Singularity.cs
                    break;
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
                    int n = 12 + Math.Min(Wave, 6);  // the ring grows each wave up to 18 bullets (wave 6)
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
                    WardenFire(e, tpos);  // Warden.cs
                    break;
                case EnemyType.HiveQueen:
                    QueenFire(e, tpos);  // Queen.cs
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
                    SingularityFire(e, aim);  // Singularity.cs
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
            if (e.Type == EnemyType.Boss)
            {
                ClearRioters();  // the riot's over (Warden.cs)
                ClearGuards();
            }
            if (e.Type == EnemyType.HiveQueen)
                ClearQueenAdds(e);  // her eggs, bombers and drones go with her (Queen.cs)
            if (e.Type == EnemyType.Singularity)
                foreach (var o in Enemies.Values)
                    if (o.Type == EnemyType.GravityWell || o.Type == EnemyType.CageWell)
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
