using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Divergent paths (Docs/Paths.md): after each boss the host picks one of two paths for the four regular waves
    /// before the next boss. A path changes the enemy mix (and, in later steps, the arena), pays a bonus to XP and
    /// score while you're on it, and a prize when it ends (before the boss, which is always standard).
    public enum PathId : byte { None, SwarmFields, SniperRidge, EliteGauntlet, BurningGrounds, Sanctuary, FrozenLake, BloodMoon, Fogbound, NarrowPass, Bazaar }

    public enum PatchKind : byte { Fire, Heal, Gas, Honey, Lava, Bile }  // Gas: the Warden's canisters (Warden.cs); Honey: the Hive Queen's puddles (Queen.cs); Lava: the Colossus's pools (Colossus.cs); Bile: the Devourer's acid (Devourer.cs)

    /// A patch of a hazard floor (Burning Grounds' fire, Sanctuary's healing pools). The host spawns it and tells
    /// everyone once; each game then ages, grows and expires it the same way.
    public class PathPatch
    {
        public int Id;
        public PatchKind Kind;
        public V2 Pos;
        public float Age, Warn, Life, R0, R1;

        /// Fire: warning until Warn, then burning. Heal: fading in until Warn.
        public bool Active => Age >= Warn;
        public bool Done => Age >= Life;

        /// Fire spreads from R0 to R1 while it burns; pools stay the same size.
        public float Radius => Kind == PatchKind.Fire && Active ? R0 + (R1 - R0) * Math.Min(1f, (Age - Warn) / Math.Max(0.01f, Life - Warn)) : R0;
    }

    public class PathDef
    {
        public PathId Id;
        public string Name, Kind, Blurb;
        public string[] Rules;          // short lines for the path card
        public int Danger;              // 1-3: sets the bonus and the prize
        public Rgba Floor, Accent;      // the arena's tint and the path's colour
        public EnemyType[] Showcase;    // shown on the path card
        public string Prize;            // its own prize instead of the danger rating's (the Bazaar's shop)
    }

    public static class Paths
    {
        public static readonly PathDef[] All =
        {
            new PathDef { Id = PathId.SwarmFields, Name = "Swarm Fields", Kind = "Enemy mix", Danger = 2,
                Blurb = "Open grassland overrun by rushers.",
                Rules = new[] { "Mostly melee rushers: Swarmers, Brutes, Lungers", "Twice as many of them, at 60% health" },
                Floor = new Rgba(0.16f, 0.32f, 0.14f), Accent = new Rgba(0.55f, 0.95f, 0.45f),
                Showcase = new[] { EnemyType.Swarmer, EnemyType.Brute, EnemyType.Lunger } },
            new PathDef { Id = PathId.SniperRidge, Name = "Sniper Ridge", Kind = "Enemy mix", Danger = 2,
                Blurb = "High rocky ground with long sightlines.",
                Rules = new[] { "Mostly Snipers, Mortars and Grunts", "Hardly any melee" },
                Floor = new Rgba(0.34f, 0.25f, 0.16f), Accent = new Rgba(1f, 0.78f, 0.4f),
                Showcase = new[] { EnemyType.Sniper, EnemyType.Mortar, EnemyType.Grunt } },
            new PathDef { Id = PathId.EliteGauntlet, Name = "Elite Gauntlet", Kind = "Enemy mix", Danger = 3,
                Blurb = "A proving ground for the strongest.",
                Rules = new[] { "Half as many enemies", "Every one of them is elite (2.5x health, faster fire)" },
                Floor = new Rgba(0.3f, 0.24f, 0.08f), Accent = new Rgba(1f, 0.85f, 0.3f),
                Showcase = new[] { EnemyType.Charger, EnemyType.Spinner, EnemyType.Shielder } },
            new PathDef { Id = PathId.BurningGrounds, Name = "Burning Grounds", Kind = "Hazard floor", Danger = 3,
                Blurb = "Scorched earth that keeps catching fire.",
                Rules = new[] { "Fire patches flash a warning, then ignite and spread", "They burn you and the enemies alike" },
                Floor = new Rgba(0.2f, 0.09f, 0.05f), Accent = new Rgba(1f, 0.5f, 0.2f),
                Showcase = new[] { EnemyType.Grunt, EnemyType.Burster, EnemyType.Spinner } },
            new PathDef { Id = PathId.Sanctuary, Name = "Sanctuary", Kind = "Hazard floor", Danger = 1,
                Blurb = "Quiet ground with springs of healing light.",
                Rules = new[] { "Healing pools appear around the arena", "20% fewer enemies" },
                Floor = new Rgba(0.08f, 0.16f, 0.26f), Accent = new Rgba(0.5f, 0.9f, 1f),
                Showcase = new[] { EnemyType.Grunt, EnemyType.Sniper, EnemyType.Medic } },
            new PathDef { Id = PathId.FrozenLake, Name = "Frozen Lake", Kind = "Twist", Danger = 2,
                Blurb = "A sheet of cracked ice. Mind your footing.",
                Rules = new[] { "You slide: speed builds up and carries on", "Enemies are chilled: 10% slower" },
                Floor = new Rgba(0.16f, 0.27f, 0.36f), Accent = new Rgba(0.65f, 0.92f, 1f),
                Showcase = new[] { EnemyType.Charger, EnemyType.Lunger, EnemyType.Spinner } },
            new PathDef { Id = PathId.BloodMoon, Name = "Blood Moon", Kind = "Twist", Danger = 3,
                Blurb = "Under a red moon, only the hunt keeps you alive.",
                Rules = new[] { "No health orbs and no regeneration", "Every kill heals its killer a little" },
                Floor = new Rgba(0.24f, 0.05f, 0.07f), Accent = new Rgba(1f, 0.35f, 0.4f),
                Showcase = new[] { EnemyType.Brute, EnemyType.Burster, EnemyType.Sniper } },
            new PathDef { Id = PathId.Fogbound, Name = "Fogbound", Kind = "Arena rule", Danger = 2,
                Blurb = "A thick mist rolls in. Stay close together.",
                Rules = new[] { "You only see a short way around each player", "Enemies and bullets beyond fade into the mist" },
                Floor = new Rgba(0.17f, 0.18f, 0.21f), Accent = new Rgba(0.8f, 0.86f, 0.95f),
                Showcase = new[] { EnemyType.Lunger, EnemyType.Sniper, EnemyType.Swarmer } },
            new PathDef { Id = PathId.NarrowPass, Name = "Narrow Pass", Kind = "Arena rule", Danger = 2,
                Blurb = "A canyon pass hemmed in by glowing walls.",
                Rules = new[] { "The arena closes in to a narrow pass", "Walls on every side: no wrapping round the edges" },
                Floor = new Rgba(0.27f, 0.19f, 0.14f), Accent = new Rgba(1f, 0.7f, 0.45f),
                Showcase = new[] { EnemyType.Brute, EnemyType.Charger, EnemyType.Grunt } },
            new PathDef { Id = PathId.Bazaar, Name = "Bazaar", Kind = "Market", Danger = 1,
                Blurb = "A trading town on the road.",
                Rules = new[] { "The usual enemies, but every kill drops gold", "Everyone gets the same gold, and can give it away", "Gold you don't spend becomes XP" },
                Floor = new Rgba(0.3f, 0.2f, 0.1f), Accent = new Rgba(1f, 0.8f, 0.35f), Prize = "A shop",
                Showcase = new[] { EnemyType.Grunt, EnemyType.Burster, EnemyType.Sniper } },
        };

        // Fogbound and Narrow Pass.
        public const float FogSight = 350f, FogEdge = 130f, FogIn = 2f;  // clear radius, soft edge, seconds to roll in
        public const float NarrowInsetX = 240f, NarrowInsetY = 90f, NarrowClose = 1.5f;  // 1120 x 720 between the walls

        // Frozen Lake and Blood Moon.
        public const float IceGrip = 3.2f, IceTopSpeed = 1.1f, IceEnemySpeed = 0.9f;
        public static float BloodMoonHeal(int wave) => 2f + 0.1f * wave;

        // Burning Grounds and Sanctuary.
        public const float FireWarn = 1.4f, FireLife = 9f, FireR0 = 55f, FireR1 = 115f, FireEvery = 2.2f;
        public const int FireMax = 7;
        public const float PoolWarn = 0.6f, PoolLife = 10f, PoolRadius = 80f, PoolEvery = 5f, PoolHeal = 10f;
        public const int PoolMax = 3;
        public const float PatchTick = 0.5f;
        public static float FirePlayerDps(int wave) => 10f + 0.4f * wave;
        public static float FireEnemyDps(int wave) => 20f + 1.5f * wave;

        public const int Length = 4;  // regular waves per path (the ones before the next boss)

        public static PathDef Get(PathId id)
        {
            foreach (var d in All)
                if (d.Id == id)
                    return d;
            return null;
        }

        /// While on a path: XP and score multiplier.
        public static float Bonus(int danger) => danger >= 3 ? 1.5f : danger == 2 ? 1.25f : 1f;

        public static string BonusText(int danger) => danger >= 3 ? "+50% XP and score" : danger == 2 ? "+25% XP and score" : "No bonus";

        public static string PrizeText(PathDef d) => d.Prize ?? PrizeText(d.Danger);

        public static string PrizeText(int danger) => danger >= 3 ? "An extra legendary pick" : danger == 2 ? "+1 upgrade pick" : "Full heal and 2 rerolls";
    }

    public partial class Game
    {
        public PathId CurrentPath;           // the chosen path (None between paths)
        public int PathFrom, PathTo;         // the waves it covers
        public PathId[] PathOffer;           // the two paths to choose from, while the host is choosing (else null)
        public readonly List<PathId> Route = new List<PathId>();  // the paths taken this run
        PathId lastPath;

        /// The current wave is being played (or about to be) on a path.
        public bool OnPath => CurrentPath != PathId.None && Wave >= PathFrom && Wave <= PathTo;

        /// Waves of the chosen path still to come, counting the current one.
        public int PathWavesLeft
        {
            get
            {
                if (CurrentPath == PathId.None)
                    return 0;
                int next = State == MatchState.Fighting ? Wave : Wave + 1;
                return Math.Max(0, PathTo - Math.Max(next, PathFrom) + 1);
            }
        }

        float PathBonus => OnPath ? Paths.Bonus(Paths.Get(CurrentPath).Danger) : 1f;

        bool OnPathOf(PathId id) => OnPath && CurrentPath == id && Wave % 5 != 0;

        void ResetPaths()
        {
            CurrentPath = PathId.None;
            PathOffer = null;
            Route.Clear();
            lastPath = PathId.None;
            Patches.Clear();
            NarrowF = FogF = NightF = 0f;
            TinyInset = V2.Zero;
            ShopOpen = false;
            ShopT = 0f;
        }

        /// Host, after a boss wave is cleared: offer two paths for the next stretch (not in the Practice Range or
        /// after the final wave).
        void OfferPaths()
        {
            if (!IsHost || Practice != null || (Wave >= MaxWave && !Endless))
                return;
            var pool = new List<PathId>();
            foreach (var d in Paths.All)
                if (d.Id != lastPath)
                    pool.Add(d.Id);
            if (pool.Count < 2)
                foreach (var d in Paths.All)
                    if (!pool.Contains(d.Id))
                        pool.Add(d.Id);
            int a = rng.Next(pool.Count), b = (a + 1 + rng.Next(pool.Count - 1)) % pool.Count;
            PathOffer = new[] { pool[a], pool[b] };
            var w = Proto.Begin(Msg.PathOffer);
            w.Write((byte)PathOffer[0]);
            w.Write((byte)PathOffer[1]);
            session.Broadcast(Proto.End(w));
            Recorder?.PathOffered(this, PathOffer);
            DoLog("paths offered: " + PathOffer[0] + " / " + PathOffer[1]);
        }

        /// The host picks one of the offered paths (index 0 or 1), or 2 for no path (the plain arena, no bonus or
        /// prize). Only the host's choice counts.
        public void ChoosePath(int index)
        {
            if (!IsHost || PathOffer == null || index < 0 || index > 2)
                return;
            CurrentPath = index == 2 ? PathId.None : PathOffer[index];
            PathFrom = Wave + 1;
            PathTo = Wave + Paths.Length;
            PathOffer = null;
            Route.Add(CurrentPath);
            lastPath = CurrentPath;
            BroadcastPath();
            Recorder?.PathChosen(CurrentPath);
            Sounds.Add(Sfx.Pick);
            DoLog("path: " + PathName(CurrentPath) + " for waves " + PathFrom + "-" + PathTo);
        }

        void BroadcastPath()
        {
            var w = Proto.Begin(Msg.PathChosen);
            w.Write((byte)CurrentPath);
            w.Write(PathFrom);
            w.Write(PathTo);
            session.Broadcast(Proto.End(w));
        }

        void ReadPathOffer(BinaryReader r) => PathOffer = new[] { (PathId)r.ReadByte(), (PathId)r.ReadByte() };

        void ReadPathChosen(BinaryReader r)
        {
            CurrentPath = (PathId)r.ReadByte();
            PathFrom = r.ReadInt32();
            PathTo = r.ReadInt32();
            if (PathOffer != null)  // this answers an offer (a path, or no path); otherwise a path just ended
                Route.Add(CurrentPath);
            PathOffer = null;
            if (CurrentPath == PathId.None)
                Patches.Clear();
        }

        /// A path's name for the route and logs ("Plain arena" for no path).
        public static string PathName(PathId id) => id == PathId.None ? "Plain arena" : Paths.Get(id).Name;

        /// Host, when the path's last wave is cleared: the prize, then back to the plain arena for the boss.
        void FinishPath()
        {
            var d = Paths.Get(CurrentPath);
            if (d == null)
                return;
            var lastBoss = BossForWave(Math.Max(5, PathFrom - 1));
            var key = IsReaper(lastBoss) ? EnemyType.TwinA : lastBoss;
            if (d.Id == PathId.Bazaar)
                OpenShop();  // the shop is its prize (Shop.cs)
            else foreach (var p in Players.Values)
            {
                if (d.Danger >= 3)
                    p.LegendaryQueue.Add(key);  // picked with the other cards
                else if (d.Danger == 2)
                    p.PendingPicks++;
                else
                {
                    if (p.Downed)
                        RevivePlayer(p, 1f);
                    p.Hp = p.MaxHp;
                    p.Rerolls += 2;
                }
            }
            DoLog(d.Name + " complete: " + Paths.PrizeText(d));
            Recorder?.PathFinished(Paths.PrizeText(d));
            CurrentPath = PathId.None;
            BroadcastPath();
        }

        /// A path's enemy mix replaces a regular wave's (BuildWave).
        bool BuildPathWave(int n, ref int count, List<EnemyType> pool, List<EnemyType> melee)
        {
            if (!OnPath || n % 5 == 0)
                return false;
            switch (CurrentPath)
            {
                case PathId.Sanctuary:
                    count = Math.Max(2, (int)(count * 0.8f));  // the usual mix, a fifth smaller
                    return false;
                case PathId.SwarmFields:
                {
                    var rushers = new List<EnemyType> { EnemyType.Swarmer, EnemyType.Brute, EnemyType.Lunger };
                    for (int i = 0; i < count; i++)
                    {
                        if (rng.NextDouble() < 0.8)
                        {
                            var type = rushers[rng.Next(rushers.Count)];
                            spawnQueue.Add(type);
                            spawnQueue.Add(type);  // twice as many
                        }
                        else
                            spawnQueue.Add(pool[rng.Next(pool.Count)]);
                    }
                    return true;
                }
                case PathId.SniperRidge:
                {
                    var ridge = new List<EnemyType> { EnemyType.Sniper, EnemyType.Sniper, EnemyType.Mortar, EnemyType.Grunt };
                    for (int i = 0; i < count; i++)
                    {
                        if (rng.NextDouble() < 0.1)
                            spawnQueue.Add(melee[rng.Next(melee.Count)]);
                        else if (rng.NextDouble() < 0.8)
                            spawnQueue.Add(ridge[rng.Next(ridge.Count)]);
                        else
                            spawnQueue.Add(pool[rng.Next(pool.Count)]);
                    }
                    return true;
                }
                case PathId.EliteGauntlet:
                {
                    float meleeShare = MeleeShare(n);
                    for (int i = 0; i < Math.Max(2, count / 2); i++)
                        spawnQueue.Add(rng.NextDouble() < meleeShare ? melee[rng.Next(melee.Count)] : pool[rng.Next(pool.Count)]);
                    return true;
                }
            }
            return false;
        }

        /// Path changes to a freshly spawned regular enemy (SpawnEnemyAt).
        void ApplyPathToEnemy(Enemy e, ref bool elite)
        {
            if (e.Def.IsBoss || !OnPath || Wave % 5 == 0)
                return;
            if (CurrentPath == PathId.SwarmFields && e.Def.Melee)
                e.MaxHp *= 0.6f;
            if (CurrentPath == PathId.FrozenLake)
                e.Speed *= Paths.IceEnemySpeed;  // chilled by the ice
            if (CurrentPath == PathId.EliteGauntlet && e.ParentId == 0)
                elite = true;
        }

        // --- Arena rules (Fogbound, Narrow Pass) ----------------------------------------------

        /// How far Narrow Pass's walls have closed in, and how thick Fogbound's mist is (0-1). Every game eases them
        /// the same way from the path, so nothing needs sending.
        public float NarrowF, FogF;

        /// The rule is on from the countdown before the path's first wave (the walls close in, the mist rolls in)
        /// until its last wave is cleared.
        bool ArenaRule(PathId id) => CurrentPath == id && (OnPath || Wave + 1 == PathFrom) && Practice == null;

        void TickArenaRules(float dt)
        {
            NarrowF = MathX.MoveTowards(NarrowF, ArenaRule(PathId.NarrowPass) ? 1f : 0f, dt / Paths.NarrowClose);
            FogF = MathX.MoveTowards(FogF, ArenaRule(PathId.Fogbound) ? 1f : 0f, dt / Paths.FogIn);
            V2 tiny = TinyTarget();
            TinyMoving = TinyInset.DistanceTo(tiny) > 0.01f;
            TinyInset = new V2(MathX.MoveTowards(TinyInset.X, tiny.X, dt * TinySpeed), MathX.MoveTowards(TinyInset.Y, tiny.Y, dt * TinySpeed * Height / Width));
            NightF = MathX.MoveTowards(NightF, Has(RunMod.Nightfall) && State != MatchState.Idle ? 1f : 0f, dt / Paths.FogIn);
        }

        // --- Tiny Arena and Nightfall (run modifiers) -------------------------------------------

        /// Tiny Arena: how far its walls stand in from each edge, and whether they're still moving.
        /// Nightfall: how dark it is (0-1). Every game works both out from the wave, so nothing needs sending.
        public V2 TinyInset;
        public bool TinyMoving;
        public float NightF;
        public const float TinyShrink = 0.75f, TinyMinArea = 0.4f, TinySpeed = 220f;  // room kept per boss, the least it gets to, px/s across

        /// After each boss the walls take a quarter of the room (a quarter of its area, down to 40%); they open
        /// for the next boss's countdown and fight, since the bosses' mechanics use the whole arena.
        V2 TinyTarget()
        {
            if (!Has(RunMod.TinyArena) || Practice != null || State == MatchState.Idle)
                return V2.Zero;
            int upcoming = State == MatchState.Intermission ? Wave + 1 : Wave;
            if (upcoming % 5 == 0)
                return V2.Zero;
            int bosses = (upcoming - 1) / 5;
            float s = (float)Math.Sqrt(Math.Max(TinyMinArea, Math.Pow(TinyShrink, bosses)));
            return new V2(Width * (1f - s) / 2f, Height * (1f - s) / 2f);
        }

        /// Narrow Pass or Tiny Arena is walling the arena in: you can't wrap round the edges, and bullets stop at the walls.
        public bool Walled => NarrowF > 0f || TinyInset.X > 0.5f;

        /// The playable part of the arena: all of it, or what's inside Narrow Pass's or Tiny Arena's walls.
        public V2 ArenaMin
        {
            get
            {
                float f = NarrowF * NarrowF * (3f - 2f * NarrowF);
                return new V2(Math.Max(Paths.NarrowInsetX * f, TinyInset.X), Math.Max(Paths.NarrowInsetY * f, TinyInset.Y));
            }
        }

        public V2 ArenaMax => new V2(Width, Height) - ArenaMin;

        /// A random spot in the playable arena, at least margin (mx across, my down) from its edges.
        V2 RandomInArena(float mx, float my)
        {
            V2 lo = ArenaMin, hi = ArenaMax;
            return new V2(Rand(lo.X + mx, hi.X - mx), Rand(lo.Y + my, hi.Y - my));
        }

        V2 RandomInArena(float margin) => RandomInArena(margin, margin);

        /// A player's position after moving: wrapped round the edges, or held inside Narrow Pass's walls.
        V2 KeepInArena(V2 pos, float r) => KeepInDuelRing(Walled ? ClampToArena(pos, r) : WrapArena(pos), r);  // and inside the Duelist's circle (Duelist.cs)

        // --- Hazard floors (Burning Grounds, Sanctuary) -------------------------------------------

        public readonly Dictionary<int, PathPatch> Patches = new Dictionary<int, PathPatch>();
        int nextPatchId = 1;
        float patchSpawnT, patchTickT;

        /// Every game: patches age and expire.
        void AgePatches(float dt)
        {
            if (Patches.Count == 0)
                return;
            scratchIds.Clear();
            foreach (var pt in Patches.Values)
            {
                pt.Age += dt;
                if (pt.Done)
                    scratchIds.Add(pt.Id);
            }
            foreach (int id in scratchIds)
                Patches.Remove(id);
        }

        /// Host: spawn the path's patches during a wave, and apply their burns and heals.
        void TickPaths(float dt)
        {
            bool fire = OnPathOf(PathId.BurningGrounds), heal = OnPathOf(PathId.Sanctuary);
            if (State != MatchState.Fighting || (!fire && !heal))
                return;
            patchSpawnT -= dt;
            if (patchSpawnT <= 0f)
            {
                patchSpawnT = fire ? Paths.FireEvery : Paths.PoolEvery;
                int count = 0;
                foreach (var pt in Patches.Values)
                    count++;
                if (count < (fire ? Paths.FireMax : Paths.PoolMax))
                    SpawnPatch(fire ? PatchKind.Fire : PatchKind.Heal);
            }
            patchTickT -= dt;
            if (patchTickT > 0f)
                return;
            patchTickT = Paths.PatchTick;
            foreach (var pt in Patches.Values)
            {
                if (!pt.Active)
                    continue;
                float r = pt.Radius;
                foreach (var p in Players.Values)
                {
                    if (p.Pos.DistanceTo(pt.Pos) > r + p.Def.HitR)
                        continue;
                    if (pt.Kind == PatchKind.Heal)
                    {
                        if (!p.Downed)
                            Heal(p, Paths.PoolHeal * Paths.PatchTick, null, "Sanctuary pools");
                    }
                    else if (!p.Downed && p.InvulnT <= 0f)
                        Damage(p, Paths.FirePlayerDps(Wave) * Paths.PatchTick, "Burning Grounds");
                }
                if (pt.Kind == PatchKind.Fire)
                    foreach (var e in Enemies.Values)
                        if (!e.Def.IsBoss && e.SpawnT <= 0f && e.Pos.DistanceTo(pt.Pos) < r + e.Radius)
                        {
                            Recorder?.HazardDamage(Math.Min(Paths.FireEnemyDps(Wave) * Paths.PatchTick, Math.Max(0f, e.Hp)));
                            HurtEnemy(e, Paths.FireEnemyDps(Wave) * Paths.PatchTick, 0);
                        }
            }
        }

        void SpawnPatch(PatchKind kind)
        {
            // Fire often starts near someone, so it keeps you moving; pools appear anywhere open.
            V2 pos = RandomInArena(90f);
            if (kind == PatchKind.Fire && rng.NextDouble() < 0.5 && Players.Count > 0)
            {
                var list = new List<Player>(Players.Values);
                var p = list[rng.Next(list.Count)];
                pos = ClampToArena(p.Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(60f, 240f), 60f);
            }
            if (kind == PatchKind.Fire)
                SpawnPatchAt(kind, pos, Paths.FireWarn, Paths.FireLife, Paths.FireR0, Paths.FireR1);
            else
                SpawnPatchAt(kind, pos, Paths.PoolWarn, Paths.PoolLife, Paths.PoolRadius, Paths.PoolRadius);
        }

        /// A patch on the ground, told to everyone once.
        void SpawnPatchAt(PatchKind kind, V2 pos, float warn, float life, float r0, float r1)
        {
            var pt = new PathPatch { Id = nextPatchId++, Kind = kind, Pos = pos, Warn = warn, Life = life, R0 = r0, R1 = r1 };
            Patches[pt.Id] = pt;
            var w = Proto.Begin(Msg.PathPatch);
            w.Write(pt.Id);
            w.Write((byte)pt.Kind);
            w.Write(pt.Pos);
            w.Write(pt.Warn);
            w.Write(pt.Life);
            w.Write(pt.R0);
            w.Write(pt.R1);
            session.Broadcast(Proto.End(w));
        }

        void ReadPathPatch(BinaryReader r)
        {
            var pt = new PathPatch { Id = r.ReadInt32(), Kind = (PatchKind)r.ReadByte(), Pos = r.ReadV2(), Warn = r.ReadSingle(), Life = r.ReadSingle(), R0 = r.ReadSingle(), R1 = r.ReadSingle() };
            Patches[pt.Id] = pt;
        }
    }
}
