using System;
using System.Collections.Generic;

namespace Arena
{
    /// What a boss wave holds and where its bosses stand (Docs/WaveRefactor.md step 4): one table instead of a switch in
    /// `BuildWave` and a copy in the Practice Range. A boss wave is the boss (and its partner), then an escort that grows with
    /// the wave number: `n / EscortEvery` of Escort, or alternating Escort and EscortAlt. Bosses that fight alone (or bring
    /// their own swarm) have no escort. Which boss comes when is `BossSchedule`.
    public static class BossWaves
    {
        struct Row
        {
            public EnemyType Boss;
            public EnemyType? Partner;     // comes with it (the Twin Reapers)
            public EnemyType Escort;
            public EnemyType EscortAlt;    // every second escort (the same as Escort when there is one kind)
            public int EscortEvery;        // one escort per this many waves; 0: none
        }

        static Row Alone(EnemyType boss) => new Row { Boss = boss };
        static Row With(EnemyType boss, EnemyType escort, int every, EnemyType? alt = null) =>
            new Row { Boss = boss, Escort = escort, EscortAlt = alt ?? escort, EscortEvery = every };

        static readonly Row[] Rows =
        {
            With(EnemyType.Boss, EnemyType.Grunt, 2),                              // the Warden
            Alone(EnemyType.HiveQueen),                                            // she brings her own swarm
            new Row { Boss = EnemyType.TwinA, Partner = EnemyType.TwinB },         // the Reapers come as a pair
            With(EnemyType.Singularity, EnemyType.Grunt, 6, EnemyType.Sniper),
            With(EnemyType.Colossus, EnemyType.Grunt, 5),
            With(EnemyType.StormHerald, EnemyType.Sniper, 8),
        };

        static Row Get(EnemyType boss)
        {
            foreach (var r in Rows)
                if (r.Boss == boss)
                    return r;
            return Alone(boss);  // Duelist, Lich, Devourer and the Eclipse fight alone (or bring their own help)
        }

        /// The boss and its partner: what a Practice Range wave adds for one boss.
        public static List<EnemyType> Own(EnemyType boss)
        {
            var r = Get(boss);
            var l = new List<EnemyType> { boss };
            if (r.Partner.HasValue)
                l.Add(r.Partner.Value);
            return l;
        }

        /// A boss wave's queue on wave n: the boss, its partner, then the escort.
        public static List<EnemyType> Queue(EnemyType boss, int n)
        {
            var r = Get(boss);
            var l = Own(boss);
            int escorts = r.EscortEvery > 0 ? n / r.EscortEvery : 0;
            for (int i = 0; i < escorts; i++)
                l.Add(i % 2 == 0 ? r.Escort : r.EscortAlt);
            return l;
        }

        /// Where a boss comes in, in an arena w x h: the middle (the Twin Reapers on either side of it, where their circle
        /// starts). Null for anything that isn't a boss.
        public static V2? SpawnPoint(EnemyType type, float w, float h)
        {
            if (type == EnemyType.TwinA)
                return new V2(w / 2f - w * 0.26f, h / 2f);
            if (type == EnemyType.TwinB)
                return new V2(w / 2f + w * 0.26f, h / 2f);
            if (Defs.Get(type).IsBoss)
                return new V2(w / 2f, h / 2f);
            return null;
        }
    }
}
