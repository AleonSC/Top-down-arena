using System;
using System.Collections.Generic;

namespace Arena
{
    /// Enemy waves, spawning, movement, attack patterns and boss mechanics (host only).
    public partial class Game
    {
        // A different boss every 5 waves up to the final one at 50; endless mode then cycles through them.
        internal static readonly EnemyType[] BossCycle =  // the Campaign's order (BossSchedule.cs)
        {
            EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus,
            EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse,
        };

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

        public const float TetherWidth = 8f;

        void ResetBossState()
        {
            PullStrength = 0f;
            TwinReviveT = 0f;
            twinPending = false;
            Reapers = null;
        }

        // --- Waves -------------------------------------------------------------

        /// Enemy health for the party size: solo 85% (the difficulty's Solo enemy health), two players 100%, three
        /// players 120% (bosses 150%, since there are no extra bosses to share the load).
        float PartyHpScale(bool boss) => PartyHpScale(boss, Players.Count);

        float PartyHpScale(bool boss, int n)
        {
            if (n <= 1) return D(DiffKnob.SoloHp);
            if (n == 2) return 1f;
            return boss ? 1.5f : 1.2f;
        }

        /// How much of a regular wave is melee: all of wave 1, none from wave 13 on.
        public static float MeleeShare(int wave) => Math.Max(0f, Math.Min(1f, (13f - wave) / 12f));

        /// The enemy types a regular wave can draw from: new ones join as the waves go up (also the between-waves box's "next wave" list).
        public static List<EnemyType> WavePool(int n)
        {
            var pool = new List<EnemyType> { EnemyType.Grunt };
            if (n >= 2) pool.Add(EnemyType.Burster);
            if (n >= 3) pool.Add(EnemyType.Sniper);
            if (n >= 4) pool.Add(EnemyType.Spinner);
            if (n >= 6) pool.Add(EnemyType.Charger);
            if (n >= 7) pool.Add(EnemyType.Splitter);
            if (n >= 8) pool.Add(EnemyType.Mortar);
            if (n >= 9) pool.Add(EnemyType.Shielder);
            if (n >= 11) pool.Add(EnemyType.Medic);
            if (n >= 14) pool.Add(EnemyType.Pauser);   // new shooting patterns (Variety.cs)
            if (n >= 15) pool.Add(EnemyType.FuseRunner);  // enemies that don't shoot (NonShooters.cs; waves from 13 are built by WaveRecipe.cs)
            if (n >= 17) pool.Add(EnemyType.Hookman);
            if (n >= 18) pool.Add(EnemyType.Sapper);
            if (n >= 16) pool.Add(EnemyType.Phantom);
            if (n >= 18) pool.Add(EnemyType.BeamSentry);  // the second half's new enemies (Variety.cs)
            if (n >= 19) pool.Add(EnemyType.Leader);
            if (n >= 21) pool.Add(EnemyType.Hexer);
            if (n >= 22) pool.Add(EnemyType.Stitcher);
            if (n >= 24) pool.Add(EnemyType.Scorcher);
            if (n >= 26) pool.Add(EnemyType.Juggernaut);
            if (n >= 28) pool.Add(EnemyType.Reaver);
            if (n >= 31) pool.Add(EnemyType.Nest);
            if (n >= 36) pool.Add(EnemyType.Warder);
            pool.RemoveAll(IsRetired);  // only the basic faction's are left (BasicFaction.cs)
            return pool;
        }

        /// A wave is built by stages, each filling the plan and the queues (WavePlan.cs). The order matters: the rolls are drawn in it.
        void BuildWave(int n)
        {
            spawnQueue.Clear();
            squadQueue.Clear();
            stragglers = 0;
            bossRewarded = false;
            plan = new WavePlan(n);
            if (StageBoss())
                return;  // a boss wave: the boss and its escort, nothing else
            StageSize();
            if (StageZombies())
                return;  // Zombie Infection: a horde instead (Zombies.cs)
            StageApproach();
            if (!StagePath() && !StageRecipe())
                StageLegacy();
            MixApproach();  // the boss faction's enemies go in among the rest
        }

        WavePlan plan;  // the wave being built (BuildWave)

        /// Stage 1: a boss wave's own queue (true: the wave is done).
        bool StageBoss()
        {
            int n = plan.N;
            if (!(Bosses.Slotted ? BossSlot : IsBossWave(n)))
                return false;
            spawnQueue.AddRange(BossWaves.Queue(BossAt(n), n));  // (a hunt's boss on its last wave; Hunts.cs)
            return true;
        }

        /// Stage 2: how many picks the wave has and what it picks from.
        void StageSize()
        {
            int n = plan.N;
            plan.Pool = WavePool(n);  // the types that can join the mix on this wave
            int count = WaveNumbers.Picks(n);  // (the first waves get a little extra so the opening isn't too quiet)
            if (LayoutOn && Layout != LayoutId.Open)  // (the Rooms world alone doesn't count: Rooms.cs)
                count = (int)Math.Round(count * LayoutEnemyMult);  // a floor plan's walls and gates make the crowd easier to read: more of them (Balance.cs)
            count = (int)Math.Round(count * ExpansionScale);  // a bigger Expanding map: more of them (ExpansionGame.cs)
            if (Players.Count >= 3)
                count = (int)(count * WaveNumbers.ThirdPlayerShare);  // a third player: a quarter more enemies
            count = Math.Max(1, (int)Math.Round(count * D(DiffKnob.EnemyCount)));  // the difficulty's enemy count
            if (Rules.CountMult != 1f)
                count = Math.Max(1, (int)Math.Round(count * Rules.CountMult));  // the mode's own count (a Boss Hunt's: WaveNumbers.cs)
            plan.Count = count;
        }

        /// Stage 3: Zombie Infection builds the whole wave itself (true: done).
        bool StageZombies() => BuildZombieWave(plan.N, plan.Count);

        /// Stage 4: the boss's faction takes its share first (Approaches.cs).
        void StageApproach()
        {
            int n = plan.N;
            // The first waves are melee rushers; shooters take over a little more each wave until
            // the melee are gone by wave 13.
            var melee = plan.Melee = new List<EnemyType> { EnemyType.Swarmer };  // the first waves: packs of plain Swarmers
            if (n >= 2) melee.Add(EnemyType.Spiker);
            if (n >= 3) melee.Add(EnemyType.Brute);
            if (n >= 5) melee.Add(EnemyType.Lunger);
            if (n >= 5) { melee.Add(EnemyType.Buzzsaw); melee.Add(EnemyType.SpikeBlock); }  // the melee grunts (MeleeGrunts.cs)
            if (n >= SwordsmanFrom) melee.Add(EnemyType.Swordsman);  // the swordsmen (Swordsman.cs)
            if (n >= ShieldSwordsmanFrom) melee.Add(EnemyType.ShieldSwordsman);
            melee.RemoveAll(IsRetired);  // (BasicFaction.cs)
            // A recipe wave shows at most KindCap(n) distinct types, the boss factions' enemies included (WaveRecipe.cs): the
            // factions may bring in all but the headliners' places, then the recipe fills up to the cap.
            plan.KindCap = n >= RecipeFrom ? KindCap(n) : 99;
            plan.ApproachKindCap = n >= RecipeFrom ? plan.KindCap - HeadlinerCount(n) : 99;
            int count = plan.Count;
            BuildApproach(n, ref count);
            plan.Count = count;
        }

        /// Stage 5a: a path's own mix (Paths.cs; true: done).
        bool StagePath()
        {
            int count = plan.Count;
            bool done = BuildPathWave(plan.N, ref count, plan.Pool, plan.Melee);
            plan.Count = count;
            return done;
        }

        /// Stage 5b: the late recipe from wave 13 (WaveRecipe.cs): headliners, half the wave as squads, then singles to each job's share.
        bool StageRecipe()
        {
            int n = plan.N;
            if (n < RecipeFrom)
                return false;
            PickHeadliners(n);
            BuildRecipeSingles(n, plan.Count, BuildSquads(n, plan.Count));
            return true;
        }

        /// Stage 5c: waves 1-12: about 70% squads (Variety.cs), the rest singles, melee rushers first and shooters taking over.
        void StageLegacy()
        {
            int n = plan.N, count = plan.Count;
            count -= BuildSquads(n, count);
            float meleeShare = MeleeShare(n);
            for (int i = 0; i < count; i++)
            {
                if (rng.NextDouble() < meleeShare)
                {
                    var type = plan.Melee[rng.Next(plan.Melee.Count)];
                    spawnQueue.Add(type);
                    if (type == EnemyType.Spiker)
                        spawnQueue.Add(type);  // spikers come in pairs
                    else if (type == EnemyType.Swarmer)
                    {
                        spawnQueue.Add(type);  // swarmers come in packs of four
                        spawnQueue.Add(type);
                        spawnQueue.Add(type);
                    }
                }
                else
                    spawnQueue.Add(plan.Pool[rng.Next(plan.Pool.Count)]);
            }
        }

        void SpawnEnemy(EnemyType type)
        {
            var d = Defs.Get(type);
            V2 pos;
            int home = -1;
            V2? breach = d.IsBoss ? null : BreachSpot(d.Radius);  // the Expanding arena: through one of the wave's breaches (Breaches.cs)
            if (breach.HasValue)
                pos = breach.Value;
            else
                pos = SpawnSpot(d.Radius, !d.IsBoss, out home);
            // Bosses appear where BossWaves says (the spot drawn above is thrown away, so the rolls that follow stay as they were).
            var bossAt = BossWaves.SpawnPoint(type, Width, Height);
            if (bossAt.HasValue)
                pos = bossAt.Value;
            else if (type == EnemyType.Nest)
                pos = RandomInArena(Walled ? 80f : 160f, Walled ? 80f : 120f);

            // From wave 12, regular enemies can show up as tougher elites.
            bool elite = RollElite(d);
            spawnHome = home;
            SpawnEnemyAt(type, pos, d.IsBoss ? 1.5f : 0.9f, 0, elite);  // bosses take longer to appear, so you can step out of the middle
            spawnHome = -1;
        }

        /// Where a regular enemy (or a spawn portal: SpawnPortals.cs) comes in: anywhere on the Rooms map away from you (or by a
        /// room's nest), through a layout's enemy gate, or a random spot along an edge of the open arena.
        V2 SpawnSpot(float radius, bool mapRules, out int home)
        {
            home = -1;
            float m = radius + 30f;
            V2 lo = ArenaMin + new V2(m, m), hi = ArenaMax - new V2(m, m);  // along the edges (Narrow Pass: its walls)
            if (RoomsOn && mapRules)
                return Rooms.RoomByRoom ? RoomSpawnSpot(radius, out home) : RoomsRandomSpot(radius);  // anywhere on the map, away from you (or by a room's nest: Rooms.cs)
            if (LayoutOn && mapRules)
                return LayoutSpawn(m);  // through an enemy gate, or an edge the layout allows (LayoutRules.cs)
            switch (rng.Next(4))
            {
                case 0: return new V2(Rand(lo.X, hi.X), lo.Y);
                case 1: return new V2(Rand(lo.X, hi.X), hi.Y);
                case 2: return new V2(lo.X, Rand(lo.Y, hi.Y));
                default: return new V2(hi.X, Rand(lo.Y, hi.Y));
            }
        }

        /// An enemy's health when it spawns on a wave, for this party and the run's modifiers (before paths and elites).
        /// The Field guide's Bestiary shows it too.
        public float SpawnHp(EnemyType type, int wave)
        {
            var d = Defs.Get(type);
            if (type == EnemyType.Gatekeeper || type == EnemyType.Shadow || FactionMinis.Is(type))  // a challenge or mini-boss room's: a share of a boss's (Challenges.cs)
                return BossEquivalentHp(wave) * (type == EnemyType.Shadow ? Challenges.ShadowShare : Challenges.MiniBossShare);
            float hp = d.Hp * (1f + 0.15f * (wave - 1)) * PartyHpScale(d.IsBoss);
            if (d.IsBoss)
                hp *= BossHpMult(type) * BossHpBonus;  // Balance.cs
            // Players' damage multiplies as upgrades stack, so late bosses need more than linear scaling.
            if (d.IsBoss)
                hp *= LateBossBoost(wave);
            if (Has(RunMod.ToughCrowd))
                hp *= Modifiers.ToughCrowdHp;
            hp *= D(DiffKnob.EnemyHp) * (d.IsBoss ? D(DiffKnob.BossHp) : 1f);  // the run's difficulty (Difficulty.cs)
            return hp;
        }

        /// Late bosses' extra health: +10% a wave after wave 20 (x1.5 at 25, x4 at 50).
        public static float LateBossBoost(int wave) => wave > LateBossWave ? 1f + LateBossScaling * (wave - LateBossWave) : 1f;

        /// How much faster enemies fire on a wave: 5% a wave, up to twice as fast (wave 21 on).
        public static float WaveFireRate(int wave) => Math.Min(2f, 1f + 0.05f * (wave - 1));

        /// A spawn point that falls outside every room of the map (a squad's line or pincer laid out along the world's box, which has holes
        /// in it when the map isn't a rectangle) moves to the nearest room, inside its walls.
        V2 SnapIntoRooms(V2 pos, float r)
        {
            if (!RoomsOn)
                return pos;
            var areas = CurrentWorld.Areas;
            foreach (var a in areas)
                if (!a.Sealed && a.Contains(pos, -r))
                    return pos;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i].Sealed)
                    continue;
                float m = r + 20f;
                V2 c = pos.Clamp(areas[i].Min + new V2(m, m), areas[i].Max - new V2(m, m));
                float dd = c.DistanceTo(pos);
                if (dd < bestD) { bestD = dd; best = i; }
            }
            if (best < 0)
                return pos;
            float mm = r + 20f;
            return PushOutOfSolids(pos.Clamp(areas[best].Min + new V2(mm, mm), areas[best].Max - new V2(mm, mm)), r);
        }

        /// Host: an enemy comes into the arena: its stats (NewEnemy), then its place and first timers (PlaceEnemy).
        Enemy SpawnEnemyAt(EnemyType type, V2 pos, float spawnT, int parent, bool elite = false)
        {
            var d = Defs.Get(type);
            pos = SnapIntoRooms(pos, d.Radius);
            var e = NewEnemy(type, d, pos, parent, elite);
            Recorder?.EnemySpawned(e);
            PlaceEnemy(e, pos, spawnT, parent);
            Enemies[e.Id] = e;
            if (type == EnemyType.BoundWraith)
                PairBound(e, spawnT, parent, elite);  // they come in pairs (ReaperFaction.cs)
            PairPylon(e, spawnT, parent, elite);  // so do Pylons (Squall.cs)
            return e;
        }

        /// An enemy's stats at spawn: health for the wave, party and run, speed, path and elite changes, a boss's strength, traits, shield.
        Enemy NewEnemy(EnemyType type, EnemyDef d, V2 pos, int parent, bool elite)
        {
            var e = new Enemy { Id = nextEnemyId++, Type = type, Def = d, Radius = d.Radius, Speed = d.Speed, Score = d.Score, ParentId = parent, Elite = elite };
            e.MaxHp = SpawnHp(type, Wave);
            if (Has(RunMod.SwiftFoes))
                e.Speed *= Modifiers.SwiftFoesSpeed;
            e.Speed *= D(DiffKnob.EnemySpeed);
            ApplyPathToEnemy(e, pos, ref elite);  // Paths.cs
            e.Elite = elite;
            if (elite)
            {
                e.MaxHp *= 2.5f;
                e.Radius *= EliteSize;
            }
            if (d.IsBoss)
                ScaleBoss(e);
            e.Hp = e.MaxHp;
            if (elite)
                RollTraits(e);  // Variety.cs
            else if (parent == 0)
                RollCoreCarrier(e);  // CoreDrops.cs
            var brain = BrainOf(type);
            if (brain != null && brain.Guard != null)
                brain.Guard.Setup(this, e);  // a shield bearer's shield (ShieldBrains.cs)
            return e;
        }

        /// A boss's health: a party far above a typical build meets a tougher boss (BuildPower.cs). Late bosses already get more
        /// health for the same reason (LateBossScaling), so the bigger of the two counts, not both together.
        void ScaleBoss(Enemy e)
        {
            if (Quick)
            {
                // Quick Play: about a two-minute fight at any wave (QuickPlay.cs), plus the strong-build boost.
                e.MaxHp = QuickBossHp(e.Type, Wave);
                e.PowerScale = BossPowerScale();
                e.MaxHp *= e.PowerScale;
            }
            else
            {
                float late = LateBossBoost(Wave);
                e.PowerScale = Math.Max(late, BossPowerScale());
                e.MaxHp *= e.PowerScale / late;
            }
        }

        /// An enemy's place and first timers: its home room, where it stands, when it appears, its first rolls.
        void PlaceEnemy(Enemy e, V2 pos, float spawnT, int parent)
        {
            Enemy parentE;
            if (parent != 0 && Enemies.TryGetValue(parent, out parentE))
            {
                e.Home = parentE.Home;  // (a Splitter's halves, a Nest's shards: the Rooms arena's room of their parent)
                e.Spilled = parentE.Spilled;
            }
            else
                e.Home = spawnHome;
            e.Pos = ClampToArena(pos, e.Radius);
            e.Disp = e.Pos;
            e.Waypoint = e.Pos;
            e.SpawnT = spawnT;
            e.FireT = Rand(0.3f, 1.2f);
            e.PhaseT = Rand(1f, 3f);
            if (IsSecurity(e.Type))
                e.Phase = SecurityMag;  // a full magazine (SecurityGuard.cs)
            e.Strafe = rng.NextDouble() < 0.5 ? 1f : -1f;
            e.Ang = Rand(0f, MathX.Tau);
        }

        readonly List<int> pickAlive = new List<int>(), pickTanks = new List<int>();  // (PickTarget runs per enemy, every tick when everyone is down)

        int PickTarget()
        {
            var alive = pickAlive;
            var tanks = pickTanks;
            alive.Clear();
            tanks.Clear();
            foreach (var p in Players.Values)
                if (!p.Downed && (p.TauntT > 0f || JuggOn(p) || p.RodT > 0f))
                    return p.Id;  // Roar (or the Juggernaut Suit, or a Lightning Rod's charge): everyone goes for the tank
            Player hunted;
            if (Has(RunMod.Hunted) && Players.TryGetValue(HuntedId, out hunted) && !hunted.Downed && hunted.VanishT <= 0f)
                return hunted.Id;  // Hunted: everyone goes for the quarry (Modifiers.cs)
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
        // fire, and while one is up the others only get a share (the difficulty's Adds' fire in boss fights), so the boss's patterns stay readable.

        int enemyShots;  // enemy bullets in flight, counted each tick (and as they're fired)

        // 40 + 3 a wave, up to 85 (wave 15 on), 20% more for each extra player. (It was 50 + 4 a wave up to 120, 30% more
        // a player: a duo's screen sat at 130-160 bullets from wave 11 in recorded runs.)
        const float BudgetBase = 40f, BudgetPerWave = 3f, BudgetMax = 85f, BudgetPerPlayer = 0.2f;

        public int ShotBudget => (int)(Math.Min(BudgetMax, BudgetBase + BudgetPerWave * Wave) * (1f + BudgetPerPlayer * (Players.Count - 1)) * D(DiffKnob.BulletBudget));

        int RegularShotBudget()
        {
            foreach (var e in Enemies.Values)
                if (e.Def.IsBoss)
                    return (int)(ShotBudget * D(DiffKnob.BossBullets));  // Normal: 60%
            return ShotBudget;
        }

        void CountEnemyShots()
        {
            enemyShots = 0;
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind))
                    enemyShots++;
        }

        // Reused every tick by TickEnemies: the enemies to move (a copy, since some spawn others) and to keep apart.
        readonly List<Enemy> tickEnemies = new List<Enemy>();
        readonly List<Enemy> separateEnemies = new List<Enemy>();

        void TickEnemies(float dt)
        {
            float rate = WaveFireRate(Wave) * D(DiffKnob.FireRate);
            PullStrength = 0f;
            TickDevArena(dt);  // the Dev Arena's hero dummies, and the enemy you play healed up (DevArena.cs)
            CountEnemyShots();
            PrepareVariety();  // squads and Commanders (Variety.cs)
            PrepareAccretion();  // the Accretion rings' bands (Infall.cs)
            AssignShooterTurns(dt);  // only so many regular shooters at once (Readability.cs)
            tickEnemies.Clear();
            tickEnemies.AddRange(Enemies.Values);
            foreach (var e in tickEnemies)  // copy: some enemies spawn others
            {
                actingEnemy = e;  // its shots are stamped with it (Lightning Rod; LightningRod.cs)
                e.T += dt;
                TickKnock(e, dt);  // a push sliding (Knock.cs)
                e.MarkT = Math.Max(0f, e.MarkT - dt);
                TickSlow(e, dt);
                if ((e.WadeT = Math.Max(0f, e.WadeT - dt)) <= 0f)
                    e.WadeMult = 1f;
                TickWet(e, dt);  // Tsunami.cs: soaked enemies are slowed
                TickBurn(e, dt);
                TickBleed(e, dt);  // KitWeapons.cs
                TickBoneCage(e, dt);  // KitWeapons.cs
                TickHemorrhage(e, dt);  // BloodMage.cs
                TickPoison(e, dt);  // Ninja.cs
                TickVenom(e, dt);   // Poison Dart (PoisonDart.cs)
                TickDaze(e, dt);    // Daze.cs
                foreach (var tick in Classes.EnemyHooks)  // each class's KitDef.TickEnemy
                    tick(this, e, dt);
                e.Suppress = Math.Max(0f, e.Suppress - SuppressFade * dt);
                KeepOutOfDuel(e);  // GatekeeperRoom.cs
                if (e.SpawnT > 0f)
                {
                    e.SpawnT -= dt;
                    continue;
                }
                if (e.Dummy)
                {
                    TickDummy(e, dt);  // a balancing dummy (DpsTools.cs)
                    continue;
                }
                if (DevFreeze)
                    continue;  // F1 dev tool: frozen in place, no moving or shooting (DevTools.cs)
                if (ShowcaseMode)
                    continue;  // the loadout preview's target dummies stand still and never shoot (Showcase.cs)
                if (e.FrozenT > 0f)  // Glacial Tomb: frozen solid, no moving or shooting
                {
                    e.FrozenT -= dt;
                    e.SlowT = Math.Max(e.SlowT, e.FrozenT);
                    continue;
                }
                if (e.GraspT > 0f)  // held by Leech Tendrils (BloodMage.cs)
                {
                    e.GraspT -= dt;
                    continue;
                }
                if (e.CageT > 0f)  // caged by Bone Guard darts (Minions.cs): no moving or shooting
                    continue;
                if (e.Type == EnemyType.Portal)
                    continue;  // a spawn portal stands still and never hurts (TickPortals: SpawnPortals.cs)
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
                tpos = RoomTarget(e, tpos);
                tpos = GnawlingTarget(e, tpos);  // a Gnawling smells a morsel (LarderFaction.cs)  // a challenge room: the crystal, or the zone (Challenges.cs)
                bool held = RoomsLeash(e, ref tpos);  // the Rooms arena: guarding its nest, or the vault (Rooms.cs)
                bool mine = IsPossessed(e);  // the Dev Arena's enemy being played: it goes for the cursor (DevArena.cs)
                if (mine)
                {
                    tpos = possessAim;
                    held = false;
                }
                else if (HeroDummyAim(e, ref tpos))
                    held = false;  // the Dev Arena's hero dummies draw its aim (DevArena.cs)
                if (e.Type != EnemyType.Singularity && e.Type != EnemyType.Devourer && !(e.Type == EnemyType.Gatekeeper && e.Phase != 0))  // (the Gatekeeper's charge keeps its line)  // their Facing is the lasers' sweep, or the Devourer's mouth (Devourer.cs)
                    Face(e, tpos, dt);
                if (mine)
                    PossessMoveEnemy(e, tpos, dt);  // your keys, or its own moves while it attacks (DevArena.cs)
                else
                    MoveEnemy(e, tpos, dt);
                if (RoomsOn && !e.Def.IsBoss && e.Home != Rooms.Vault)
                {
                    V2 was = e.Pos;
                    e.Pos = RoomsPortal(e.Pos, ref e.PortalT, dt);  // wraparound portals (Rooms.cs)
                    if (e.Pos.DistanceTo(was) > 600f)
                        steerMem.Remove(e.Id);  // a fresh route from the far side
                }
                if (State == MatchState.Fighting && e.DazeT > 0f)
                {
                    // Dazed (Daze.cs): no attacking, and a wind-up starts over once it wears off.
                    float rateDazed = rate * TraitFireMult(e) * (IsTwin(e.Type) && e.Phase == 1 ? 1.7f : 1f) * (Has(RunMod.Frenzy) ? Modifiers.FrenzyFire : 1f) * (1f - e.Suppress);
                    e.FireT = Math.Max(e.FireT, WindUpOf(e.Type) * rateDazed + 0.05f);
                    e.Winding = false;
                    e.GasLocked = false;
                }
                else if (mine && State == MatchState.Fighting && !PossessAttacking)
                    e.Winding = false;  // the enemy being played waits for you to attack (DevArena.cs)
                else if (State == MatchState.Fighting)
                {
                    if (mine)
                        PossessFireNow(e);
                    Special(e, tpos, dt);
                    if (e.Def.IsBoss)
                        BossCues(e);  // BossSounds.cs
                    float mult = rate * TraitFireMult(e) * (IsTwin(e.Type) && e.Phase == 1 ? 1.7f : 1f) * (Has(RunMod.Frenzy) ? Modifiers.FrenzyFire : 1f);
                    bool guardFar = IsSecurity(e.Type) && e.Phase > 0 && e.Pos.DistanceTo(tpos) > SecurityEngage;  // too far to shoot: no countdown (SecurityGuard.cs)
                    if (guardFar)
                        e.FireT = Math.Max(e.FireT, SecurityEvery);  // it opens fire a full gap after it gets close, not at once
                    else
                        e.FireT -= dt * mult * (1f - e.Suppress);  // the Riot Repeater's suppression (Weapons.cs)
                    // A regular shooter glows just before it fires (Readability.cs).
                    float rateNow = mult * (1f - e.Suppress);
                    e.Winding = (IsSecurity(e.Type) ? e.Phase > 0 : TakesTurns(e) && (HasTurn(e) || mine)) && rateNow > 0f && e.FireT / rateNow <= WindUpOf(e.Type) && e.FireT > -0.5f;
                    var fb = BrainOf(e.Type);
                    if (fb != null && fb.Fire != null)
                        fb.Fire.Wind(this, e, tpos, rateNow);  // a lobber locks its lob when the wind-up starts (LobBrains.cs)
                    if (held && e.FireT <= 0f)
                        e.FireT = Rand(0.2f, 0.4f);  // nobody in its room: nothing to shoot at
                    if (e.FireT <= 0f)
                    {
                        if (!HasTurn(e) && !mine)
                            e.FireT = Rand(0.2f, 0.4f);  // not its turn: hold fire and reposition (Readability.cs)
                        else if (!mine && !e.Def.IsBoss && !Challenges.Own(e.Type) && !Zombies.Is(e.Type) && enemyShots >= RegularShotBudget())
                            e.FireT = Rand(0.2f, 0.4f);  // the screen is full: hold this volley and try again shortly
                        else
                            EnemyFire(e, tpos);
                    }
                }
                if (e.Hp <= 0f || e.Type == EnemyType.HiveBomber || e.Type == EnemyType.HiveEgg || e.Type == EnemyType.ReaperPillar || QueenAirborne(e)
                    || e.Type == EnemyType.Morsel || e.Type == EnemyType.SapperMine || DevourUnder(e) || GutwormUnder(e) || e.Dummy || e.DazeT > 0f
                    || e.Type == EnemyType.MiniWell || e.Type == EnemyType.Implosion || e.Type == EnemyType.MummerCopy)
                    continue;  // blew up this tick, explodes instead of bumping, an egg, the Hive Queen up in a dive, a morsel, the Devourer underground, dazed (Daze.cs)
                // Bosses and heavies hurt to stand in (the difficulty's per-wave damage growth scales all of these).
                var cb = BrainOf(e.Type);
                float brainContact = cb != null && cb.Move != null ? cb.Move.Contact(this, e) : -1f;  // a lunger's leap (Brain.cs)
                float contact = e.Def.IsBoss ? 25f
                    : e.Type == EnemyType.Juggernaut ? 25f
                    : brainContact >= 0f ? brainContact  // a Charger's dash, a lunger's leap, a Spike Block's lunge (their brains)
                    : e.Def.Contact > 0f ? e.Def.Contact * BruteDamageMult(e) : ContactDamage;
                GuardContact(e, contact);  // Warden's Guards in the way get hurt too (Stun.cs)
                if (!mine || PossessAttacking)
                    HeroDummyContact(e, contact);  // the Dev Arena's hero dummies (DevArena.cs)
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f || p.SwallowT > 0f || p.MeleeGraceT > 0f)
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
                        RodTouched(p, e);  // a charged Lightning Rod calls a bolt on it (LightningRod.cs)
                        DevilTouched(p, e);  // a Dust Devil shoves you (Squall.cs)
                        if (e.Type == EnemyType.Buzzsaw && !p.Downed)
                            p.InvulnT = Math.Min(p.InvulnT, SawGrace);  // its touches come fast, not once per hit grace
                    }
                }
            }

            actingEnemy = null;
            TickHazards(dt);
            TickReapers(dt);  // the Twin Reapers' stages, turns and tether (Reapers.cs)
            TickSingularityHazards(dt);
            TickHerald(dt);  // the Storm Herald's ball lightning and spokes (Herald.cs)
            TickStormFronts(dt);  // the Storm front set piece's wall of strikes (Squall.cs)
            TickPortals(dt);  // spawn portals opening, letting their bursts out and closing (SpawnPortals.cs)
            TickVariety(dt);  // embers, Volatile bursts, Mirrored shots, bubbles growing back (Variety.cs)

            // A fallen Twin Reaper comes back unless its twin dies before the timer runs out.
            if (twinPending && ReapersJoined)
            {
                twinPending = false;  // they fused: nothing left to revive (Reapers.cs)
                TwinReviveT = 0f;
            }
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
            var list = separateEnemies;
            list.Clear();
            list.AddRange(Enemies.Values);
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
            if (e.Type == EnemyType.HiveBomber)
                return;  // committed to the flight line (a lunger's leap, a Charger's dash, a Spike Block's lunge: their brains)
            if (LarderFaceLocked(e))
                return;  // a Butcher's chop, a Gulper's cone (LarderFaction.cs)
            var brain = BrainOf(e.Type);
            if (brain != null && brain.Move != null && brain.Move.Turn(this, e, want, tpos, dt))
                return;  // its own turning, e.g. a shield bearer's (Brain.cs)
            if (e.Type == EnemyType.RiotCaptain)
            {
                CaptainFace(e, want, dt);  // RiotCaptain.cs
                return;
            }
            e.Facing = want;
        }

        /// A hit the shield stopped (or time) wears it down; it cracks in three steps, then shatters.
        void WearShield(Enemy e, float amount)
        {
            var brain = BrainOf(e.Type);
            if (brain != null && brain.Guard != null)
            {
                brain.Guard.Wear(this, e, amount);  // a shield bearer's (ShieldBrains.cs)
                return;
            }
            if (e.Type == EnemyType.RiotCaptain)
            {
                CaptainShieldStruck(e);  // it never breaks (RiotCaptain.cs)
                return;
            }
            if (e.Type == EnemyType.Boss && e.Warden != null)
            {
                WearWardenShield(e, amount);  // the Warden's riot shield (Warden.cs)
                return;
            }
        }

        /// Shielders block shots from the front; the Hive Queen is immune while her swarm protects her.
        bool BlocksShot(Enemy e, V2 from)
        {
            if (ReaperImmune(e))
                return true;  // the Twin Reapers changing stage (Reapers.cs)
            if (SingImmune(e))
                return true;  // the Singularity changing stage, or split into its shards (Singularity.cs)
            if (HeraldImmune(e))
                return true;  // the Storm Herald changing stage (Herald.cs)
            if (EclipseImmune(e))
                return true;  // the Eclipse changing stage (Eclipse.cs)
            if (LarderBlocks(e, from))
                return true;  // a Gutworm underground; a Maw Priest notes which side the hit came from (LarderFaction.cs)
            if (DevourBlocks(e, from))
                return true;  // the Devourer changing stage or underground; otherwise it notes which side the hit came from (Devourer.cs)
            if (DuelImmune(e) || e.Type == EnemyType.Duelist && DuelGuardBlocks(e, from))
                return true;  // the Duelist changing stage, or its guard from the front (Duelist.cs)
            if (ColossusImmune(e))
                return true;  // the Colossus changing stage (Colossus.cs)
            if (e.Type == EnemyType.HiveQueen && (e.Phase == 1 || QueenImmune(e)))
                return true;  // shielded by her drones, changing stage, or up in a dive (Queen.cs)
            if (e.Type == EnemyType.Boss && (WardenImmune(e) || WardenShieldBlocks(e, from)))
                return true;  // changing stage, or behind its riot shield (Warden.cs)
            if (SoulBound(e))
                return true;  // the more-hurt Twin Reaper, until the other catches up
            if (LichImmune(e))
                return true;  // its phylacteries stand, it's blinking between graves, or it's changing stage (Lich.cs)
            var brain = BrainOf(e.Type);
            if (brain != null && brain.Guard != null)
                return brain.Guard.Blocks(this, e, from);  // Brain.cs
            if (e.Type == EnemyType.RiotCaptain)
                return CaptainShieldBlocks(e, from);  // RiotCaptain.cs
            return false;
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
            if (dist > 0.01f && !e.Def.IsBoss && LayoutOn)
                dir = LayoutDir(e, tpos, dir);  // round a wall that's in the way (LayoutRules.cs)
            if (IsHost && dist > 0.01f && !e.Def.IsBoss && Props.Count > 0)
                dir = PropDir(e, dir);  // round crates and barrels if there's a way; shove them if not (Props.cs)
            V2 v = V2.Zero;
            float speed = e.Speed * BruteSpeedMult(e);  // Brutes enrage as they are shot (BruteEnrage.cs)
            var brain = BrainOf(e.Type);
            if (brain != null && brain.Move != null)
                v = brain.Move.Move(this, e, tpos, dir, dist, dt, ref speed);  // Brain.cs
            else
            {
                switch (e.Type)
                {
                    case EnemyType.HiveBomber:  // a straight line to the spot it was launched at; Swarm's flankers swoop round (Queen.cs)
                        if (e.Phase >= 1)
                        {
                            v = FlankerMove(e, dt);
                            if (e.Phase == 2)
                                speed *= FlankDive;
                        }
                        else
                            v = e.Waypoint - e.Pos;
                        break;
                    case EnemyType.Boss:
                        v = WardenMove(e, tpos, ref speed);  // Warden.cs
                        break;
                    case EnemyType.HiveQueen:
                        v = QueenMove(e, tpos, ref speed);  // Queen.cs
                        break;
                    case EnemyType.TwinA:
                    case EnemyType.TwinB:
                        v = ReaperMove(e, tpos, ref speed);  // Reapers.cs
                        break;
                    case EnemyType.DualReaper:
                        v = DualMove(e, tpos, ref speed);  // Reapers.cs
                        break;
                }
                if (e.Type == EnemyType.Gutworm)
                    v = dir;  // straight at you, on the surface or under it (LarderFaction.cs)
                if (IsLateGame(e.Type))
                    v = LateMove(e, tpos, dir, dist, dt, ref speed);
                if (Challenges.Own(e.Type))
                    v = RoomMove(e, tpos, dir, dist, ref speed);  // Challenges.cs
                if (e.Type == EnemyType.RiotCaptain)
                    v = CaptainMove(e, dir, dist, ref speed);  // RiotCaptain.cs
            }
            if (RoomsOn && !e.Def.IsBoss && !e.Def.Melee && e.Home < 0 && dist > 0.01f && SegBlocked(ActiveLayout, e.Pos, tpos, 4f))
                v = dir;  // the Rooms map: a shooter with a wall between it and you comes round (steered) instead of holding its distance behind the wall
            v = SquadMove(e, tpos, dir, v);  // squads keep together (Variety.cs)
            if (e.Type == EnemyType.Worker)
                speed *= WorkerPackSpeed(e);  // a crowd of Workers runs faster (HiveBees.cs)
            if (HasTrait(e, EliteTrait.Hasted))
                speed *= HasteMult;
            // A sweeping Beam Sentry steers its beam by Strafe, so it keeps the side it telegraphed (the roll still happens, keeping the RNG sequence).
            // A Security Guard keeps its side on its own timer (SecurityGuard.cs); Debris keeps its spiral's side, a Pulsar its sweep's (Infall.cs).
            if (rng.NextDouble() < dt * 0.3 && !(e.Type == EnemyType.BeamSentry && e.Phase == 3) && !IsSecurity(e.Type) && !PulsarLocked(e) && !HelioLocked(e) && e.Type != EnemyType.Debris && e.Type != EnemyType.SquallGunner)
                e.Strafe = -e.Strafe;
            if (StomperPlanted(e))
                speed = 0f;  // a Stomper stands still while its foot is up (QuarryFaction.cs)
            if (DancerBusy(e) || GallantBusy(e) || HarlequinBusy(e) || MummerMasked(e) || e.Type == EnemyType.MummerCopy)
                speed = 0f;  // (a Mummer and its copies are carried round you while masked)  // a Blade Dancer's lunge moves it itself (CourtFaction.cs)
            if (LarderPlanted(e))
                speed = 0f;  // a Butcher chopping, a Gulper breathing in (LarderFaction.cs)
            if (e.SlowT > 0f && !Heavy(e))
                speed *= e.SlowMult;
            else if (e.WadeT > 0f)
                speed *= e.WadeMult;  // Heavy enemies and bosses ignore the usual slow, but not a Bloodbath (Bloodbath.cs)
            e.Pos += v.LimitLength(1f) * speed * dt;
            e.Pos = ClampToArena(e.Pos, e.Radius);
            if (IsHost && Props.Count > 0 && !e.Def.IsBoss)
                ShoveProps(e.Pos, e.Radius * 0.8f, PushEnemy, dt, 0f, V2.Zero);  // they shove crates and barrels, very slowly (Props.cs)
            if ((Props.Count > 0 || LayoutOn) && !e.Def.IsBoss)
                e.Pos = PushOutOfProps(e.Pos, e.Radius * 0.8f);  // they walk round crates and barrels (Props.cs)
        }

        /// Behaviour that isn't just "shoot when the timer runs out".
        void Special(Enemy e, V2 tpos, float dt)
        {
            if (Challenges.Own(e.Type))
            {
                RoomSpecial(e, tpos, dt);  // Challenges.cs
                return;
            }
            if (e.Type == EnemyType.RiotCaptain)
            {
                CaptainSpecial(e, tpos, dt);  // RiotCaptain.cs
                return;
            }
            if (IsLateGame(e.Type))
            {
                LateSpecial(e, tpos, dt);
                return;
            }
            if (e.Type == EnemyType.Mummer && MummerSpecial(e, tpos, dt))
                return;  // masked: it circles you with its copies and fires; no lunge (CourtFaction.cs)
            if (e.Type == EnemyType.Gutworm)
                GutwormSpecial(e, tpos, dt);  // dives, chases, marks, erupts (LarderFaction.cs)
            var brain = BrainOf(e.Type);
            if (brain != null && brain.Special != null)
                brain.Special.Tick(this, e, tpos, dt);  // Brain.cs
            else switch (e.Type)
            {
                case EnemyType.Boss:
                    WardenSpecial(e, tpos, dt);  // Warden.cs: its stages, turns, searchlight, gates and riot
                    break;
                case EnemyType.TwinA:
                case EnemyType.TwinB:
                {
                    // Soul Bond: whichever Reaper is clearly more hurt is immune until you even them out (Twilight only).
                    var other = OtherTwin(e);
                    if (other == null || e.Phase == 1 || (Reapers != null && Reapers.Stage != ReaperStage.Twilight))
                        break;
                    float gap = other.Hp / other.MaxHp - e.Hp / e.MaxHp;
                    if (e.Phase == 0 && gap > SoulBondGap)
                        e.Phase = 2;
                    else if (e.Phase == 2 && gap < SoulBondRelease)
                        e.Phase = 0;
                    break;
                }
                case EnemyType.HiveQueen:
                    QueenSpecial(e, tpos, dt);  // Queen.cs: her stages and turns
                    break;
                case EnemyType.HiveBomber:
                {
                    // A flanker at its flank point turns in toward its target (Queen.cs).
                    if (e.Phase == 1 && (e.Pos.DistanceTo(e.Waypoint) < FlankArrive || e.T > FlankMaxOut))
                        FlankerTurnIn(e);
                    // Blows up when it reaches the spot it was launched at, or on touching anyone on the way.
                    bool hit = e.Phase != 1 && e.Pos.DistanceTo(e.Waypoint) < 12f;
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
            if (Challenges.Own(e.Type) && RoomFire(e, tpos, aim))
                return;  // Challenges.cs
            if (e.Type == EnemyType.RiotCaptain)
            {
                e.FireT = 99f;  // its attacks are its Special (RiotCaptain.cs)
                return;
            }
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
            var brain = BrainOf(e.Type);
            if (brain != null && brain.Fire != null)
            {
                brain.Fire.Fire(this, e, tpos, aim);  // Brain.cs
                return;
            }
            switch (e.Type)
            {
                case EnemyType.Boss:
                    WardenFire(e, tpos);  // Warden.cs
                    break;
                case EnemyType.HiveQueen:
                    QueenFire(e, tpos);  // Queen.cs
                    break;
                case EnemyType.TwinA:
                case EnemyType.TwinB:
                    ReaperFire(e, tpos);  // Reapers.cs
                    break;
                case EnemyType.DualReaper:
                    e.FireT = 0.2f;  // its lasers and scythes are timed by the fight (Reapers.cs)
                    break;
                case EnemyType.Singularity:
                    SingularityFire(e, aim);  // Singularity.cs
                    break;
            }
        }

        void EnemyShot(Enemy e, float ang, float speed, byte kind, float angVel = 0f)
        {
            V2 dir = V2.FromAngle(ang);
            var b = SpawnBullet(kind, e.Pos + dir * e.Radius, dir * speed, angVel, 8f, Defs.BulletDamage(kind), 0, e.Def.IsBoss);
            b.Source = e.Type;
            b.Shooter = e.Id;  // (the Juggernaut Suit's flak finds its way back by this)
            b.Look = b.Sent.Look = LookFor(e, ang, angVel);  // a dart, a crescent or round (Readability.cs)
            NoteVolley(e, b);  // a ring or a spray stays round all the way round
        }

        /// Death side effects. Returns true when this kill earns the boss rewards
        /// (rerolls and the big orb drop).
        bool OnEnemyKilled(Enemy e)
        {
            if (InRoom)
                RoomKilled(e);  // the kill rush's count, a shadow gone (Challenges.cs)
            VarietyKilled(e);  // Volatile, Splitting, a Warder's bubbles (Variety.cs)
            ReaperCultKilled(e);  // a Bound Wraith's partner channels it back (ReaperFaction.cs)
            if (e.Type == EnemyType.Boss)
            {
                ClearRioters();  // the riot's over (Warden.cs)
                ClearGuards();
                ClearWardenShields(e);  // WardenFaction.cs
            }
            if (e.Type == EnemyType.HiveQueen)
                ClearQueenAdds(e);
            if (e.Type == EnemyType.Collapsar)
                CollapsarDied(e);  // a short pull, then a ring (Infall.cs)
            if (e.Type == EnemyType.ReaperPillar)
                PillarFell(e);  // leaves a hole (Reapers.cs)
            if (e.Type == EnemyType.DualReaper)
                ClearPillars();  // her eggs, bombers and drones go with her (Queen.cs)
            if (e.Type == EnemyType.Singularity)
                foreach (var o in Enemies.Values)
                    if (o.Type == EnemyType.GravityWell || o.Type == EnemyType.CageWell || o.Type == EnemyType.SingShard)
                        o.Hp = 0f;  // its wells and shards collapse with it
            if (e.Type == EnemyType.StormHerald)
                foreach (var o in Enemies.Values)
                    if (o.Type == EnemyType.BallLightning)
                        o.Hp = 0f;  // the storm breaks
            if (e.Type == EnemyType.Devourer)
            {
                ReleaseSwallowed(e);  // whoever's inside comes out (Devourer.cs)
                foreach (var o in Enemies.Values)
                    if (o.Type == EnemyType.Morsel)
                        o.Hp = 0f;
            }
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
                if (other != null && !ReapersJoined)
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
            var bossKey = IsReaper(e.Type) ? EnemyType.TwinA : e.Type;
            foreach (var p in Players.Values)
            {
                p.Rerolls += BossRerolls + p.Stats.BossRerolls;  // (Reroll Cache adds to it)
                p.LegendaryQueue.Add(bossKey);  // picked at the end of the wave
                p.PendingLegendary = p.LegendaryQueue.Count;
                p.PendingMods++;                // then a weapon mod
            }
            DoLog(e.Def.Name + " defeated: everyone gets " + BossRerolls + "+ rerolls");
            return true;
        }
    }
}
