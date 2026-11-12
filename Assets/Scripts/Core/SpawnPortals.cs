using System;
using System.Collections.Generic;

namespace Arena
{
    /// Spawn portals (Docs/Spawns.md): a timed wave's single enemies come in bursts, each through a portal that opens where
    /// the burst will come in (an edge, a layout's enemy gate, a spot on the Rooms map), then lets its cargo out one by one.
    /// Shoot a portal closed before it empties and the rest of its burst is cancelled; it drops coins for each one it still
    /// held (the Expanding arena). Now and then a burst opens behind the party (a flank): it takes longer to open and every
    /// player sees a red arrow pointing at it. Surges (TimedWaves.cs) let more portals open at once.
    /// Not on breach waves (the breaches are the way in), boss, practice, designed, zombie or room-by-room waves.
    /// Replicated through the enemy snapshot: Phase (below) and Facing (the way it lets enemies out); the cargo is host only.
    public static class SpawnPortals
    {
        public static bool Enabled = true;
        public const float Appear = 0.3f;         // the portal's own spawn-in
        public const float OpenTime = 1f;          // from appearing to the first enemy out
        public const float FlankOpenTime = 1.6f;   // a flank portal gives you longer to turn round
        public const float EmitEvery = 0.35f;      // one enemy out every this many seconds
        public const float CloseTime = 0.45f;      // empty: it shrinks shut (no reward)
        public const float Emerge = 0.3f;          // an enemy's spawn-in as it steps out
        public const int FlankFrom = 3;            // flanks from this wave
        public const int FlankOdds = 4;            // one burst in this many is a flank (never the wave's first)
        public const int OpenMax = 2, SurgeOpenMax = 3;  // portals open at once (during a surge)
        public const float ClearOfPlayers = 160f;  // a portal never opens this close to a player
        public const int SealGold = 2;             // gold per enemy still inside when it is shot closed (Expanding arena coins)
        public const int SealScore = 5;            // score per enemy still inside
        public const float Quiet = 4f;             // with nothing open this long, a part burst comes anyway (no dead air in a lull)

        // Enemy.Phase of a portal (sent in the snapshot).
        public const byte Opening = 0, Emitting = 1, FlankOpening = 2, FlankEmitting = 3, Closing = 4;

        public static bool IsFlank(byte phase) => phase == FlankOpening || phase == FlankEmitting;
        public static bool IsOpening(byte phase) => phase == Opening || phase == FlankOpening;

        /// Enemies a burst carries: 3 on wave 1, one more every 4 waves, up to 8 (±1).
        public static int BurstSize(int wave) => Math.Min(8, 3 + Math.Max(0, wave - 1) / 4);
    }

    public partial class Game
    {
        readonly Dictionary<int, List<EnemyType>> portalCargo = new Dictionary<int, List<EnemyType>>();
        readonly List<Enemy> scratchPortals = new List<Enemy>();
        int portalsThisWave, nextBurst;
        float lastBurstAt;

        bool PortalsOn => SpawnPortals.Enabled && timedWave && !BreachesOn;

        /// Enemies still inside the open portals (they count toward the wave's Remaining and the alive cap).
        public int PortalCargoCount()
        {
            int n = 0;
            foreach (var c in portalCargo.Values)
                n += c.Count;
            return n;
        }

        int PortalsOpen()
        {
            int n = 0;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Portal && e.Phase != SpawnPortals.Closing)
                    n++;
            return n;
        }

        /// BeginTimedWave: a fresh wave's bursts.
        void ResetPortals()
        {
            portalCargo.Clear();
            portalsThisWave = 0;
            lastBurstAt = -SpawnPortals.Quiet;  // (the first burst opens as the wave starts)
            if (PortalsOn)
                nextBurst = RollBurst();  // (only then: other waves keep the rng's sequence as it was)
        }

        int RollBurst() => Math.Max(2, SpawnPortals.BurstSize(Wave) + rng.Next(3) - 1);

        /// TickTimedSpawn: debt is how many enemies the arrival curve says should be out by now and aren't; room is how many
        /// more fit under the alive cap.
        void TickPortalSpawn(int debt, int room, float x)
        {
            if (debt <= 0 || room <= 0)
                return;
            if (SpawnNextSquad())
                return;  // a squad keeps its own formation (Variety.cs)
            if (spawnQueue.Count == 0)
                return;
            var first = spawnQueue[0];
            if (first == EnemyType.Nest || Defs.Get(first).IsBoss)
            {
                SpawnEnemy(first);  // (a Nest picks its own spot in the arena)
                spawnQueue.RemoveAt(0);
                singlesSinceSquad++;
                return;
            }
            int open = PortalsOpen();
            if (open >= (TimedWaves.InSurge(x) ? SpawnPortals.SurgeOpenMax : SpawnPortals.OpenMax))
                return;
            int size = Math.Min(Math.Min(nextBurst, room), spawnQueue.Count);
            bool quiet = open == 0 && timedClock - lastBurstAt >= SpawnPortals.Quiet;
            if (debt < size && x < 1f && !quiet)
                return;  // wait until a whole burst is due
            if (quiet && portalsThisWave > 0)
                size = Math.Min(size, Math.Max(debt, 2));
            var cargo = new List<EnemyType>();
            while (cargo.Count < size && spawnQueue.Count > 0 && spawnQueue[0] != EnemyType.Nest && !Defs.Get(spawnQueue[0]).IsBoss)
            {
                cargo.Add(spawnQueue[0]);
                spawnQueue.RemoveAt(0);
            }
            singlesSinceSquad += cargo.Count;
            bool flank = Wave >= SpawnPortals.FlankFrom && portalsThisWave > 0 && rng.Next(SpawnPortals.FlankOdds) == 0;
            OpenPortal(cargo, flank);
            lastBurstAt = timedClock;
            nextBurst = RollBurst();
        }

        /// Host: a portal with this cargo. Also the preview's and tests' way in.
        public Enemy OpenPortal(List<EnemyType> cargo, bool flank, V2? at = null)
        {
            float r = Defs.Get(EnemyType.Portal).Radius;
            V2 pos = at ?? (flank ? FlankSpot(r) : PortalSpot(r));
            var e = SpawnEnemyAt(EnemyType.Portal, pos, SpawnPortals.Appear, -1);  // (parent -1: no core carrier, no parent)
            e.Phase = flank ? SpawnPortals.FlankOpening : SpawnPortals.Opening;
            e.PhaseT = flank ? SpawnPortals.FlankOpenTime : SpawnPortals.OpenTime;
            V2 to = PartyMiddle(new V2(Width / 2f, Height / 2f)) - e.Pos;
            e.Facing = to.Length > 1f ? to.Angle : 0f;
            portalCargo[e.Id] = cargo;
            portalsThisWave++;
            BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, e.Radius * 2.2f, 0.5f, Sfx.Summon);
            return e;
        }

        /// The middle of the living players (or fallback when nobody is up).
        V2 PartyMiddle(V2 fallback)
        {
            V2 sum = V2.Zero;
            int n = 0;
            foreach (var p in Players.Values)
                if (!p.Downed)
                {
                    sum += p.Pos;
                    n++;
                }
            return n > 0 ? sum / n : fallback;
        }

        bool ClearOfPlayers(V2 pos)
        {
            foreach (var p in Players.Values)
                if (!p.Downed && p.Pos.DistanceTo(pos) < SpawnPortals.ClearOfPlayers)
                    return false;
            return true;
        }

        V2 PortalSpot(float r)
        {
            int home;
            V2 pos = SpawnSpot(r, true, out home);
            for (int k = 0; k < 3 && !ClearOfPlayers(pos); k++)
                pos = SpawnSpot(r, true, out home);
            return pos;
        }

        /// A spot behind the party: of a few candidate spawn spots, the one most opposite the way the players are aiming.
        V2 FlankSpot(float r)
        {
            V2 mid = PartyMiddle(new V2(Width / 2f, Height / 2f)), aim = V2.Zero;
            foreach (var p in Players.Values)
                if (!p.Downed)
                    aim += V2.FromAngle(p.Aim);
            if (aim.Length < 0.3f)
                return PortalSpot(r);  // (the party is aiming every which way: no "behind")
            V2 back = -aim.Normalized();
            V2 best = V2.Zero;
            float bestScore = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                int home;
                V2 s = SpawnSpot(r, true, out home);
                float score = (s - mid).Normalized().Dot(back) - (ClearOfPlayers(s) ? 0f : 2f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        /// Host, each tick (TickEnemies): portals open, let their cargo out and close. A frozen or caged portal holds.
        void TickPortals(float dt)
        {
            var list = scratchPortals;
            list.Clear();
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Portal)
                    list.Add(e);
            foreach (var e in list)
            {
                if (e.Hp <= 0f || e.SpawnT > 0f || e.FrozenT > 0f || e.CageT > 0f || e.GraspT > 0f)
                    continue;
                List<EnemyType> cargo;
                if (!portalCargo.TryGetValue(e.Id, out cargo))
                    portalCargo[e.Id] = cargo = new List<EnemyType>();
                e.PhaseT -= dt;
                if (e.PhaseT > 0f)
                    continue;
                bool flank = SpawnPortals.IsFlank(e.Phase);
                switch (e.Phase)
                {
                    case SpawnPortals.Opening:
                    case SpawnPortals.FlankOpening:
                        e.Phase = flank ? SpawnPortals.FlankEmitting : SpawnPortals.Emitting;
                        e.PhaseT = 0f;
                        break;
                    case SpawnPortals.Emitting:
                    case SpawnPortals.FlankEmitting:
                        if (cargo.Count > 0)
                        {
                            EmitFromPortal(e, cargo[0]);
                            cargo.RemoveAt(0);
                            e.PhaseT = SpawnPortals.EmitEvery;
                        }
                        else
                        {
                            e.Phase = SpawnPortals.Closing;
                            e.PhaseT = SpawnPortals.CloseTime;
                        }
                        break;
                    case SpawnPortals.Closing:
                        portalCargo.Remove(e.Id);
                        Enemies.Remove(e.Id);  // shut: no kill, no reward
                        break;
                }
            }
            if (portalCargo.Count > list.Count)  // a portal taken out some other way: its cargo goes with it
            {
                var gone = new List<int>();
                foreach (int id in portalCargo.Keys)
                    if (!Enemies.ContainsKey(id))
                        gone.Add(id);
                foreach (int id in gone)
                    portalCargo.Remove(id);
            }
        }

        void EmitFromPortal(Enemy portal, EnemyType type)
        {
            var d = Defs.Get(type);
            V2 fwd = V2.FromAngle(portal.Facing);
            V2 pos = portal.Pos + fwd * (portal.Radius * 0.6f) + fwd.Orthogonal() * Rand(-portal.Radius * 0.5f, portal.Radius * 0.5f);
            spawnHome = portal.Home;
            var e = SpawnEnemyAt(type, pos, SpawnPortals.Emerge, 0, RollElite(d));
            spawnHome = -1;
            e.Waypoint = e.Pos + fwd * 40f;
        }

        /// Combat.cs, a portal shot closed: the rest of its burst is cancelled, and it pays out for each one.
        void PortalSealed(Enemy e)
        {
            if (e.Type != EnemyType.Portal)
                return;
            List<EnemyType> cargo;
            int n = portalCargo.TryGetValue(e.Id, out cargo) ? cargo.Count : 0;
            portalCargo.Remove(e.Id);
            if (n <= 0)
                return;
            Score += n * SpawnPortals.SealScore;
            if (ExpandingOn)
                DropGold(e.Pos, n * SpawnPortals.SealGold);
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.85f, 0.35f), e.Radius * 3f, 0.5f, Sfx.PickupBig);
        }

        /// Coins scattered round pos, gold in all (a coin per 5, then ones; at most 10 coins).
        void DropGold(V2 pos, int gold)
        {
            int n = 0;
            while (gold > 0 && n++ < 10)
            {
                int v = gold >= 5 ? 5 : 1;
                gold -= v;
                var c = new Coin { Id = nextCoinId++, Value = v };
                c.Pos = ClampToArena(pos + V2.FromAngle((float)rng.NextDouble() * MathX.Tau) * (10f + 24f * (float)rng.NextDouble()), 12f);
                c.Disp = c.Pos;
                Coins[c.Id] = c;
            }
        }
    }
}
