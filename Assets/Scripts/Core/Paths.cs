using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Divergent paths (Docs/Paths.md): after each boss the host picks one of two paths for the four regular waves
    /// before the next boss. A path changes the enemy mix (and, in later steps, the arena), pays a bonus to XP and
    /// score while you're on it, and a prize when it ends (before the boss, which is always standard).
    public enum PathId : byte { None, SwarmFields, SniperRidge, EliteGauntlet }

    public class PathDef
    {
        public PathId Id;
        public string Name, Kind, Blurb;
        public string[] Rules;          // short lines for the path card
        public int Danger;              // 1-3: sets the bonus and the prize
        public Rgba Floor, Accent;      // the arena's tint and the path's colour
        public EnemyType[] Showcase;    // shown on the path card
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
        };

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
            lastPathFromSeen = -1;
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
            DoLog("paths offered: " + PathOffer[0] + " / " + PathOffer[1]);
        }

        /// The host picks one of the offered paths (index 0 or 1). Only the host's choice counts.
        public void ChoosePath(int index)
        {
            if (!IsHost || PathOffer == null || index < 0 || index > 1)
                return;
            CurrentPath = PathOffer[index];
            PathFrom = Wave + 1;
            PathTo = Wave + Paths.Length;
            PathOffer = null;
            Route.Add(CurrentPath);
            lastPath = CurrentPath;
            BroadcastPath();
            Sounds.Add(Sfx.Pick);
            DoLog("path: " + Paths.Get(CurrentPath).Name + " for waves " + PathFrom + "-" + PathTo);
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
            PathOffer = null;
            if (CurrentPath != PathId.None && (Route.Count == 0 || Route[Route.Count - 1] != CurrentPath || PathFrom != lastPathFromSeen))
                Route.Add(CurrentPath);
            lastPathFromSeen = PathFrom;
        }

        int lastPathFromSeen = -1;

        /// Host, when the path's last wave is cleared: the prize, then back to the plain arena for the boss.
        void FinishPath()
        {
            var d = Paths.Get(CurrentPath);
            if (d == null)
                return;
            var lastBoss = BossForWave(Math.Max(5, PathFrom - 1));
            var key = IsTwin(lastBoss) ? EnemyType.TwinA : lastBoss;
            foreach (var p in Players.Values)
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
            DoLog(d.Name + " complete: " + Paths.PrizeText(d.Danger));
            CurrentPath = PathId.None;
            BroadcastPath();
        }

        /// A path's enemy mix replaces a regular wave's (BuildWave).
        bool BuildPathWave(int n, int count, List<EnemyType> pool, List<EnemyType> melee)
        {
            if (!OnPath || n % 5 == 0)
                return false;
            switch (CurrentPath)
            {
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
            if (CurrentPath == PathId.EliteGauntlet && e.ParentId == 0)
                elite = true;
        }
    }
}
