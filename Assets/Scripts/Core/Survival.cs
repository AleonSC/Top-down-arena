using System;
using System.Collections.Generic;

namespace Arena
{
    /// One enemy the survival spawner wants on the field now.
    public struct SurvivalSpawn
    {
        public EnemyType Type;
        public int Wave;        // the wave number its health and damage scale like (Survival.WaveAt)
        public bool Elite;
        public string Squad;    // the squad it comes with (null: a single)
    }

    /// Survival mode (Docs/Survival.md): no waves, one clock. The spawner keeps a budget of on-screen threat near
    /// Pressure(t) (so killing fast brings more), phases factions in as time passes, and sends a squad on a timer.
    /// Pure logic, no Unity and no Game: the caller says how much threat and how many enemies are alive and gets back
    /// what to spawn; where they appear (off every player's view) is the game's job. Numbers are first guesses.
    public class Survival
    {
        public const float Grace = 3f;            // no spawns in the first seconds
        public const float Tick = 0.5f;           // how often the budget is topped up
        public const int MaxLive = 60;            // enemies alive at once, whatever the budget says
        public const int MaxPerTick = 6;          // singles added in one top-up (squads come on top)
        public const float PhaseLength = 240f;    // a new faction joins every four minutes
        public const float SquadEvery = 45f;      // a squad on this timer
        public const float WaveSeconds = 30f;     // 30 s of the clock count as one wave of scaling
        public const float OldFactionFade = 0.6f; // an earlier faction's weight, once per phase after it joined
        public const float RangedFrom = 120f;     // no shooters before this (two minutes of melee), then they ramp in...
        public const float RangedRamp = 180f;     // ...over this long
        public const float EaseIn = 117f;         // the budget starts at about a third and reaches its full line by here

        /// The factions that join, in order: the basic eight first, then the boss factions (Approaches).
        static readonly EnemyType[] JoinOrder = { EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity };

        public readonly int Seed;
        public readonly int Players;
        public float Time { get; private set; }

        readonly Random rng;
        float topUp, squadTimer = SquadEvery;

        public Survival(int seed, int players)
        {
            Seed = seed;
            Players = Math.Max(1, Math.Min(3, players));
            rng = new Random(seed);
        }

        /// The wave number the enemies scale like at time t (1 at the start, one more every WaveSeconds).
        public static int WaveAt(float t) => 1 + (int)(Math.Max(0f, t) / WaveSeconds);

        /// How many faction phases are open at time t: 0 is the basic faction alone.
        public static int PhaseAt(float t) => Math.Min(JoinOrder.Length, (int)(Math.Max(0f, t) / PhaseLength));

        /// The on-screen threat the spawner aims for (in Grunts: see Cost), for one player. Linear to 10 minutes, steeper after.
        public static float Pressure(float t, int players = 1)
        {
            t = Math.Max(0f, t);
            float p = 4f + 0.08f * Math.Min(t, 600f) + 0.16f * Math.Max(0f, t - 600f);
            p *= Math.Min(1f, 0.35f + 0.65f * t / EaseIn);  // a slow start
            return p * PartyMult(players);
        }

        public static float PartyMult(int players) => players >= 3 ? 2f : players == 2 ? 1.5f : 1f;

        /// How much of its weight an enemy has at time t: melee ones all of it; shooters none for the first two minutes, then ramping up.
        public static float RangedShare(EnemyType type, float t)
        {
            if (Defs.Get(type).Melee)
                return 1f;
            return Math.Max(0f, Math.Min(1f, (t - RangedFrom) / RangedRamp));
        }

        /// The share of spawns that come as Elites (the tougher variants): 0 at the start, 40% at most.
        public static float EliteShare(float t) => Math.Min(0.4f, Math.Max(0f, t) / 2700f);

        /// How much of the budget an enemy takes: its health against a Grunt's, between 0.6 and six Grunts (a swarm of weak ones still hurts: they cost more than their health).
        public static float Cost(EnemyType t)
        {
            float c = Defs.Get(t).Hp / Defs.Get(EnemyType.Grunt).Hp;
            return Math.Max(0.6f, Math.Min(6f, c));
        }

        /// The factions open at time t: the basic one, then the first PhaseAt(t) boss factions.
        public static List<FactionDef> FactionsAt(float t)
        {
            var l = new List<FactionDef> { BasicFaction.Def };
            for (int i = 0; i < PhaseAt(t); i++)
            {
                var f = Approaches.For(JoinOrder[i]);
                if (f != null) l.Add(f);
            }
            return l;
        }

        /// Advance the clock and return what to spawn. liveThreat is the sum of Cost over the enemies alive.
        public List<SurvivalSpawn> Step(float dt, float liveThreat, int liveCount)
        {
            var outList = new List<SurvivalSpawn>();
            Time += dt;
            if (Time < Grace) return outList;
            topUp += dt;
            squadTimer -= dt;
            int wave = WaveAt(Time);
            var pool = Pool(Time);
            if (pool.Count == 0) return outList;

            if (topUp >= Tick)
            {
                topUp = 0f;
                float budget = Pressure(Time, Players) - liveThreat;
                int live = liveCount;
                for (int i = 0; i < MaxPerTick && budget > 0f && live < MaxLive; i++)
                {
                    var type = PickSingle(pool);
                    outList.Add(new SurvivalSpawn { Type = type, Wave = wave, Elite = rng.NextDouble() < EliteShare(Time) });
                    budget -= Cost(type);
                    live++;
                }
            }

            if (squadTimer <= 0f)
            {
                squadTimer = SquadEvery;
                var sq = PickSquad(Time);
                // A squad also needs room: it is skipped when the field is full, and tried again on the next timer.
                if (sq != null && liveCount + outList.Count + sq.Members.Length <= MaxLive)
                    foreach (var m in sq.Members)
                        outList.Add(new SurvivalSpawn { Type = m, Wave = wave, Elite = rng.NextDouble() < EliteShare(Time), Squad = sq.Name });
            }
            return outList;
        }

        // The singles to pick from: every open faction's members that have come in (From against the time reach), earlier
        // factions thinned by OldFactionFade for each phase since they joined.
        static List<KeyValuePair<EnemyType, float>> Pool(float t)
        {
            var pool = new List<KeyValuePair<EnemyType, float>>();
            int reach = 1 + (int)(Math.Max(0f, t) / 60f);
            var fs = FactionsAt(t);
            for (int fi = 0; fi < fs.Count; fi++)
            {
                float fade = (float)Math.Pow(OldFactionFade, fs.Count - 1 - fi);
                foreach (var m in fs[fi].Members)
                    if (m.From <= reach && !Game.IsRetired(m.Type) && RangedShare(m.Type, t) > 0f)
                        pool.Add(new KeyValuePair<EnemyType, float>(m.Type, m.Weight * fade * RangedShare(m.Type, t)));
            }
            return pool;
        }

        EnemyType PickSingle(List<KeyValuePair<EnemyType, float>> pool)
        {
            float total = 0f;
            foreach (var p in pool) total += p.Value;
            float r = (float)rng.NextDouble() * total;
            foreach (var p in pool)
            {
                r -= p.Value;
                if (r <= 0f) return p.Key;
            }
            return pool[pool.Count - 1].Key;
        }

        SquadDef PickSquad(float t)
        {
            int reach = 1 + (int)(Math.Max(0f, t) / 60f);
            var fs = FactionsAt(t);
            var opts = new List<SquadDef>();
            for (int fi = 0; fi < fs.Count; fi++)
            {
                int copies = fi == fs.Count - 1 ? 2 : 1;  // the newest faction's squads come a bit more often
                foreach (var s in fs[fi].Squads)
                    if (s.From <= reach && Game.SquadLive(s) && Array.TrueForAll(s.Members, m => RangedShare(m, t) >= 1f))  // (no squad with a shooter until they have all come in)
                        for (int c = 0; c < copies; c++) opts.Add(s);
            }
            return opts.Count == 0 ? null : opts[rng.Next(opts.Count)];
        }
    }
}
