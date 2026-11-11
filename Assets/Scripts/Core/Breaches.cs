using System;
using System.Collections.Generic;

namespace Arena
{
    /// A breach: a stretch of the Expanding arena's outer wall that enemies come in through this wave (Docs/RoomBuilding2.md
    /// step 1). Pos is on the wall line; In points into the map; Area is the room it opens into.
    public struct Breach
    {
        public V2 Pos, In;
        public bool Vertical;  // the wall runs up and down
        public int Area;
        public V2 Along => Vertical ? new V2(0f, 1f) : new V2(1f, 0f);
    }

    /// Which outer walls break open each wave. Worked out the same on every machine from the map and the wave number, so
    /// nothing goes over the wire. Every 100 px of outer wall is a spot, so long walls break more often, and a dead end's (one door) more still.
    public static class Breaches
    {
        public const float Telegraph = 1.5f;  // the barrier flickers up this long before the first enemy steps through
        public const float Len = 160f;        // a breach's length along the wall
        public const float EndPad = 70f;      // kept off a wall's ends (corners and door frames)
        public const float Apart = 400f;      // breaches of one wave at least this far apart
        public const float DeadEnd = 1.5f;    // extra weight for a dead-end room's wall

        /// A breach wave is a rare event (they reveal rooms, so often would clutter the map): wave 3, then every 4th
        /// after it. Every other regular wave enemies come in at a random spot at least Rooms.SpawnClear from every player.
        public const int FirstWave = 3, Every = 4;
        public static bool IsBreachWave(int wave) => wave >= FirstWave && (wave - FirstWave) % Every == 0;

        /// How many open each wave: 2, one more per 3 rooms built, at most 6.
        public static int CountFor(int rooms) => Math.Max(2, Math.Min(6, 2 + rooms / 3));

        /// The connected map's room at p (a sealed floating room, the shop, vendor or Gatekeeper room: none, -1).
        static int OpenAreaAt(World w, V2 p)
        {
            for (int i = 0; i < w.Areas.Length; i++)
            {
                var a = w.Areas[i];
                if (!a.Sealed && !a.Vault && a.Contains(p))
                    return i;
            }
            return -1;
        }

        static bool Spawnable(RoomArea a) => a.Kind == RoomKind.Combat || a.Kind == RoomKind.Floor;

        /// Every spot on the outer wall a breach could open at (one per 100 px tile, off the ends), with its weight.
        public static List<KeyValuePair<Breach, float>> Candidates(World w)
        {
            var doorsOf = new int[w.Areas.Length];
            foreach (var d in w.Doors)
            {
                doorsOf[d.A]++;
                doorsOf[d.B]++;
            }
            var list = new List<KeyValuePair<Breach, float>>();
            foreach (var s in w.Spans)
            {
                float mid = (s.From + s.Till) / 2f;
                V2 across = s.Vertical ? new V2(1f, 0f) : new V2(0f, 1f);
                V2 onLine = s.Vertical ? new V2(s.At, mid) : new V2(mid, s.At);
                int plus = OpenAreaAt(w, onLine + across * 20f), minus = OpenAreaAt(w, onLine - across * 20f);
                if ((plus >= 0) == (minus >= 0))
                    continue;  // a wall between two rooms, or round a sealed room: not the outer wall
                int area = plus >= 0 ? plus : minus;
                if (!Spawnable(w.Areas[area]))
                    continue;
                V2 inward = plus >= 0 ? across : across * -1f;
                float weight = 1f + (doorsOf[area] <= 1 && !w.Areas[area].Hub ? DeadEnd : 0f);  // (a long wall has more spots, so it breaks more often already)
                for (float u = s.From + EndPad + Len / 2f; u <= s.Till - EndPad - Len / 2f + 0.5f; u += Expansion.Step)
                {
                    V2 spot = s.Vertical ? new V2(s.At, u) : new V2(u, s.At);
                    if (OpenAreaAt(w, spot + across * 20f) >= 0 && OpenAreaAt(w, spot - across * 20f) >= 0)
                        continue;  // (a room brushes this stretch of the wall: there's floor behind it)
                    var b = new Breach { Pos = s.Vertical ? new V2(s.At, u) : new V2(u, s.At), In = inward, Vertical = s.Vertical, Area = area };
                    list.Add(new KeyValuePair<Breach, float>(b, weight));
                }
            }
            return list;
        }

        /// This wave's breaches on this map (empty for a map with no outer wall to break).
        public static Breach[] For(World w, int wave)
        {
            var cand = Candidates(w);
            int seed = wave * 7919 + w.Areas.Length * 131;
            foreach (var a in w.Areas)
                seed = seed * 31 + (int)(a.Min.X * 3f + a.Min.Y * 7f + a.Max.X * 11f + a.Max.Y * 13f);
            var rng = new DetRandom(seed);
            int built = 0;
            foreach (var a in w.Areas)
                if (!a.Hub && !a.Floating)
                    built++;
            int want = CountFor(built);
            var picked = new List<Breach>();
            while (picked.Count < want && cand.Count > 0)
            {
                float total = 0f;
                foreach (var c in cand)
                    total += c.Value;
                float pick = rng.Float() * total;
                int at = cand.Count - 1;
                for (int i = 0; i < cand.Count; i++)
                {
                    pick -= cand[i].Value;
                    if (pick <= 0f) { at = i; break; }
                }
                var b = cand[at].Key;
                picked.Add(b);
                cand.RemoveAll(c => c.Key.Pos.DistanceTo(b.Pos) < Apart);
            }
            return picked.ToArray();
        }
    }

    public partial class Game
    {
        /// Every game, for the look: seconds since this wave's breaches opened (the wave started).
        public float BreachAge;

        Breach[] breachCache;
        World breachWorld;
        int breachWave = -1;

        /// Are enemies coming in through breaches now? The grown Expanding arena on a breach wave (Breaches.IsBreachWave: a
        /// rare event, not the room-by-room version, which uses its nests, and never a boss wave).
        public bool BreachesOn => ExpandingOn && RoomsOn && !Rooms.RoomByRoom && !BossNow && State == MatchState.Fighting && Breaches.IsBreachWave(Wave);

        /// This wave's breaches (empty while they're off).
        public Breach[] ActiveBreaches
        {
            get
            {
                if (!BreachesOn)
                    return Array.Empty<Breach>();
                var w = CurrentWorld;
                if (breachCache == null || breachWorld != w || breachWave != Wave)
                {
                    breachCache = Breaches.For(w, Wave);
                    breachWorld = w;
                    breachWave = Wave;
                }
                return breachCache;
            }
        }

        /// Host: where an enemy of this radius comes in: just inside a breach, the one farthest from every player when none is
        /// SpawnClear away, else a random one of those that are. Null: no breaches (use the usual spot).
        V2? BreachSpot(float radius) => BreachSpot(radius, out _);

        V2? BreachSpot(float radius, out V2 inward)
        {
            var bs = ActiveBreaches;
            inward = V2.Zero;
            if (bs.Length == 0)
                return null;
            var clear = new List<int>();
            int far = 0;
            float farD = -1f;
            for (int i = 0; i < bs.Length; i++)
            {
                float near = float.MaxValue;
                foreach (var p in Players.Values)
                    if (!p.Downed)
                        near = Math.Min(near, p.Pos.DistanceTo(bs[i].Pos));
                if (near >= Rooms.SpawnClear)
                    clear.Add(i);
                if (near > farD) { farD = near; far = i; }
            }
            var b = bs[clear.Count > 0 ? clear[rng.Next(clear.Count)] : far];
            inward = b.In;
            for (int tries = 0; tries < 8; tries++)
            {
                V2 at = b.Pos + b.In * (radius + 45f + Rand(0f, 40f)) + b.Along * Rand(-Breaches.Len * 0.35f, Breaches.Len * 0.35f);
                if (!NearSolid(at, radius + 4f))
                    return at;
            }
            return b.Pos + b.In * (radius + 60f);
        }
    }
}
