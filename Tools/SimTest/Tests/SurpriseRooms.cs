using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Surprise rooms (Expansion.Surprise, Docs/SurpriseRooms.md step 1): five well-shaped rooms, set against the map where they close
// loops, the same on every machine, free.
static partial class Program
{
    static void ExpandingSurpriseTest()
    {
        string why;
        var none = new List<ExpansionRoom>();
        var shapes = (SurpriseShape[])Enum.GetValues(typeof(SurpriseShape));
        int[] tiles = { 96, 81, 54, 63, 47, 15 };  // floor tiles: ring, cross, L, T, chamfer, stash
        foreach (var s in shapes)
        {
            var b = Expansion.SurpriseBase(s);
            int n = 0;
            foreach (bool f in b) n += f ? 1 : 0;
            Check(n == tiles[(int)s] && n * 10000 <= Expansion.MaxArea, Expansion.SurpriseName(s) + " has " + n + " floor tiles (" + tiles[(int)s] + ")");
            // Four turns and a mirror keep the floor, and a quarter turn swaps the footprint.
            var t = Expansion.SurpriseMask(s, 1, true);
            int m = 0;
            foreach (bool f in t) m += f ? 1 : 0;
            Check(m == n && t.GetLength(0) == b.GetLength(1) && t.GetLength(1) == b.GetLength(0), Expansion.SurpriseName(s) + " keeps its floor when turned and mirrored");
            var opts = Expansion.SurpriseOptions(none, s);
            Check(opts.Count > 0, Expansion.SurpriseName(s) + " fits on the plain hub (" + opts.Count + " spots)");
            if (opts.Count == 0) continue;
            for (int i = 0; i < opts.Count; i += Math.Max(1, opts.Count / 12))
            {
                var o = opts[i];
                var w = Expansion.Build(o.Rooms);
                bool ok = o.Rooms.All(r => r.Price == 0 && r.Kind == RoomKind.Combat) && Expansion.ConnectedAreas(o.Rooms).All(c => c)
                          && Expansion.BoughtCount(o.Rooms) == 1 && w.Areas.Length == o.Rooms.Count + 1;
                for (int a = 0; a < o.Rooms.Count && ok; a++)
                {
                    bool hub = o.Rooms[a].X < (int)Game.Width && o.Rooms[a].X1 > 0 && o.Rooms[a].Y < (int)Game.Height && o.Rooms[a].Y1 > 0;
                    ok &= !hub;
                    for (int c = a + 1; c < o.Rooms.Count; c++)
                        ok &= o.Rooms[a].X1 <= o.Rooms[c].X || o.Rooms[c].X1 <= o.Rooms[a].X || o.Rooms[a].Y1 <= o.Rooms[c].Y || o.Rooms[c].Y1 <= o.Rooms[a].Y;
                }
                int floor = o.Rooms.Sum(r => r.W * r.H / 10000);
                Check(ok && floor == tiles[(int)s], Expansion.SurpriseName(s) + " spot " + i + ": free, one room, connected, no overlap, " + floor + " tiles");
                if (s == SurpriseShape.Stash)  // the cache sits in the middle: no cover on it (and the room still gets some)
                {
                    var rm = o.Rooms[0]; V2 mid = new V2((rm.X + rm.X1) / 2f, (rm.Y + rm.Y1) / 2f);
                    bool clearMid = w.Cover.All(c => c.Round ? c.Pos.DistanceTo(mid) > c.R + 40f : Math.Abs(c.Pos.X - mid.X) > c.HW + 40f || Math.Abs(c.Pos.Y - mid.Y) > c.HH + 40f);
                    int inside = w.Cover.Count(c => c.Pos.X > rm.X && c.Pos.X < rm.X1 && c.Pos.Y > rm.Y && c.Pos.Y < rm.Y1);
                    Check(clearMid && inside > 0, "Stash spot " + i + ": cover in the room (" + inside + "), none on the cache");
                }
            }
        }
        // The ring is a loop: its lane joins all the way round (every piece has two neighbours or more).
        var ring = Expansion.SurpriseOptions(none, SurpriseShape.Ring)[0].Rooms;
        var rw = Expansion.Build(ring);
        var deg = new int[ring.Count + 1];
        foreach (var d in rw.Doors) { deg[d.A]++; deg[d.B]++; }
        int ringPieces = 0, closed = 0;
        for (int a = 1; a <= ring.Count; a++) { ringPieces++; closed += deg[a] >= 2 ? 1 : 0; }
        Check(ring.Count >= 4 && closed == ringPieces, "the ring is four boxes or more with a way round (" + ring.Count + " boxes)");

        // Closing a loop: two rooms above the hub with a gap; the best spot touches both.
        string fw;
        var pair = new List<ExpansionRoom> { Expansion.Custom(none, 0, -600, 600, 600, out fw).Value };
        pair.Add(Expansion.Custom(pair, 1000, -600, 600, 600, out fw).Value);
        bool anyLoop = false;
        foreach (var s in shapes)
        {
            var o = Expansion.SurpriseOptions(pair, s);
            Check(o.Count > 0 && o.Zip(o.Skip(1), (p, q) => p.Score >= q.Score).All(x => x), Expansion.SurpriseName(s) + " options come best first");
            anyLoop |= o.Count > 0 && o[0].Loops >= 1;
        }
        Check(anyLoop, "with a gap between two rooms, some shape's best spot closes a loop");

        // Deterministic, free, and the pick is one of the best three.
        var a1 = Expansion.Surprise(7, 10, pair, SurpriseShape.Cross, out why);
        var a2 = Expansion.Surprise(7, 10, pair, SurpriseShape.Cross, out why);
        Check(a1.HasValue && a2.HasValue && a1.Value.Rooms[0].X == a2.Value.Rooms[0].X && a1.Value.Rooms[0].Y == a2.Value.Rooms[0].Y && a1.Value.Shape == a2.Value.Shape, "the same seed, wave and map give the same room");
        var top = Expansion.SurpriseOptions(pair, a1.Value.Shape).Take(Expansion.SurpriseTop).Select(o => o.Rooms[0].X + "," + o.Rooms[0].Y).ToList();
        Check(top.Contains(a1.Value.Rooms[0].X + "," + a1.Value.Rooms[0].Y), "it is one of the " + Expansion.SurpriseTop + " best spots");
        var picks = new HashSet<string>();
        for (int sd = 1; sd < 40; sd++)
        {
            var p = Expansion.Surprise(sd, 10, pair, SurpriseShape.Cross, out why);
            if (p.HasValue) picks.Add(p.Value.Rooms[0].X + "," + p.Value.Rooms[0].Y);
        }
        Check(picks.Count >= 2 && picks.Count <= Expansion.SurpriseTop, "other seeds pick other spots (" + picks.Count + ")");

        // A full map gets nothing.
        var full = new List<ExpansionRoom>();
        for (int i = 0; i < Expansion.MaxRooms; i++) full.Add(new ExpansionRoom { Parent = 0, X = 10000 + i * 400, Y = 0, W = 300, H = 300 });
        Check(Expansion.Surprise(1, 10, full, SurpriseShape.Ring, out why) == null && why != null, "a full map gets no surprise room: " + why);
    }

    // In a run (step 2): the host gives a surprise room, both screens get it free with its flag, the look's clock starts, and the world's areas carry the flag.
    static void ExpandingSurpriseGameTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); }
        g.BossSlot = true; g.State = MatchState.Intermission;  // (a boss just cleared)
        Run(s, dt, 1f, () => cg.State == MatchState.Intermission && cg.BossNow);
        int vec = g.Players[1].Vectors;
        var give = typeof(Game).GetMethod("GiveSurpriseRoom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        give.Invoke(g, null);
        Run(s, dt, 1f, () => cg.Expanded.Count == g.Expanded.Count && g.Expanded.Count > 0);
        var stashKind = (byte)(1 + (int)SurpriseShape.Stash);
        var sur = g.Expanded.Where(r => r.Surprise > 0 && r.Surprise != stashKind).ToList();
        Check(sur.Count > 0 && sur.All(r => r.Price == 0) && Game.SurpriseCount(g.Expanded) == 1, "the host gives one free surprise room (" + sur.Count + " boxes)");
        Check(cg.Expanded.Count == g.Expanded.Count && cg.Expanded.Where(r => r.Surprise > 0).Count() == sur.Count, "it reaches the client with its flag");
        Check(g.Players[1].Vectors == vec && g.SurpriseKind == sur[0].Surprise && cg.SurpriseKind == g.SurpriseKind && cg.SurpriseAge < 2f, "nobody pays, and both screens start the build-in and the chip (vec " + g.Players[1].Vectors + "/" + vec + ", kind " + g.SurpriseKind + "/" + cg.SurpriseKind + ", age " + cg.SurpriseAge + ")");
        Check(g.SurpriseFresh && g.ExpansionWorld.Areas.Count(a => a.Surprise > 0) == sur.Count, "the world's areas carry the flag, and it is fresh this wave");
        give.Invoke(g, null);
        Check(Game.SurpriseCount(g.Expanded) == 2 && g.Expanded.Where(r => r.Leg == 0 && r.Surprise > 0).Select(r => r.Surprise).Distinct().Count() == 2, "the next one is another shape");
        // Not on a full map.
        while (Expansion.BoughtCount(g.Expanded) <= Expansion.MaxRooms - 2) g.Expanded.Add(new ExpansionRoom { Parent = 0, X = 20000 + g.Expanded.Count * 400, Y = 0, W = 300, H = 300 });
        int before = g.Expanded.Count;
        give.Invoke(g, null);
        Check(g.Expanded.Count == before, "a map within two rooms of full gets none");
    }
}

// The breach event (Docs/SurpriseRooms.md step 3): a gold breach on some regular waves, and the wall behind it opens onto a surprise room.
static partial class Program
{
    static void ExpandingBreachEventTest(float dt)
    {
        // Which waves: breach waves only, from wave 7, never the wave after a boss, about half of those.
        int eligible = 0, hits = 0; bool rules = true;
        for (int seed = 1; seed <= 300; seed++)
            for (int wave = 1; wave <= 100; wave++)
            {
                bool ev = BreachEvent.IsEventWave(seed, wave);
                bool could = Breaches.IsBreachWave(wave) && wave >= BreachEvent.FromWave && wave % Expansion.Milestone != 1;
                eligible += could ? 1 : 0; hits += ev ? 1 : 0;
                rules &= !ev || could;
            }
        double rate = hits / (double)eligible;
        Check(rules, "an event wave is a breach wave, wave 7 or later, not the wave after a boss");
        Check(rate > 0.4 && rate < 0.6, "about half of those have one (" + rate.ToString("0.000") + ")");

        // A room behind a breach: every spot has floor on the point just behind the wall, and a plain map still gets a plan.
        string why;
        var list = new List<ExpansionRoom>();
        list.Add(Expansion.Custom(list, 1600, 100, 800, 600, out why).Value);
        var w = Expansion.Build(list);
        var bs = Breaches.For(w, 15);
        bool any = false, behind = true;
        foreach (var b in bs)
        {
            V2 pt = b.Pos - b.In * BreachEvent.Behind;
            var opts = Expansion.SurpriseOptions(list, SurpriseShape.LHall, pt);
            any |= opts.Count > 0;
            foreach (var o in opts) behind &= o.Rooms.Any(q => pt.X >= q.X && pt.X < q.X1 && pt.Y >= q.Y && pt.Y < q.Y1);
        }
        Check(any && behind, "a room planned behind a breach has floor right behind its wall (" + bs.Length + " breaches)");

        // In a run: the host plans it, the friend sees the gold breach, the wall opens at the delay.
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Expanded.Add(list[0]); }
        int seedPick = Enumerable.Range(1, 100000).First(sd => BreachEvent.IsEventWave(sd, 15));
        typeof(Game).GetField("expandSeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, seedPick);
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.BreachAge > 0.2f, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        g.Wave = 15; cg.Wave = 15;
        g.BreachAge = 2f; cg.BreachAge = 2f;
        Run(s, dt, 3f, () => cg.EventMark.HasValue);
        Check(g.EventMark.HasValue && cg.EventMark.HasValue && g.EventMark.Value.DistanceTo(cg.EventMark.Value) < 2f, "the host plans an event and the friend sees its gold breach (on " + g.BreachesOn + " exp " + g.ExpandingOn + " rooms " + g.RoomsOn + " byroom " + Rooms.RoomByRoom + " boss " + g.BossNow + ", " + g.State + ", wave " + g.Wave + ", age " + g.BreachAge + ", breaches " + g.ActiveBreaches.Length + ", event wave " + BreachEvent.IsEventWave(seedPick, 15) + ", rooms " + g.Expanded.Count + ", host mark " + g.EventMark.HasValue + ", client " + cg.EventMark.HasValue + ")");
        Check(g.Expanded.Any(r => r.Surprise == 1 + (int)SurpriseShape.Stash) && cg.Expanded.Any(r => r.Surprise == 1 + (int)SurpriseShape.Stash), "the Stashes are already open 2 s in, well before the gold room's 20 s");
        var before = g.ActiveBreaches.Select(b => b.Pos).ToList();
        Check(g.ActiveBreaches.Any(b => b.Pos.DistanceTo(g.EventMark.Value) < 3f) && cg.ActiveBreaches.Any(b => b.Pos.DistanceTo(cg.EventMark.Value) < 3f), "it is one of the wave's breaches on both screens");
        int rooms0 = g.Expanded.Count;
        var gp = g.Players.Values.First(); gp.Vectors = 10; gp.VectorCap = 70;
        g.BreachAge = BreachEvent.Delay + 0.1f;
        Run(s, dt, 3f, () => cg.Expanded.Count > rooms0 && !cg.EventMark.HasValue);
        var stashKind = (byte)(1 + (int)SurpriseShape.Stash);
        var sur = g.Expanded.Where(r => r.Surprise > 0 && r.Surprise != stashKind).ToList();
        Check(sur.Count > 0 && sur.All(r => r.Price == 0) && Game.SurpriseCount(g.Expanded) == 1 && !g.EventMark.HasValue, "at the delay the wall opens onto a free surprise room (" + sur.Count + " boxes) and the gold clears");
        Check(cg.Expanded.Count == g.Expanded.Count && !cg.EventMark.HasValue && cg.SurpriseKind > 0 && cg.SurpriseAge < 3f, "the friend gets it too, with the build-in and the chip");
        var after = g.ActiveBreaches.Select(b => b.Pos).ToList();
        Check(after.All(p => before.Any(q => q.DistanceTo(p) < 1f)) &&  cg.ActiveBreaches.Select(b => b.Pos).SequenceEqual(after), "the wave's other breaches stay where they were, the same on both screens");
        // Breach rooms (Docs/BreachRooms.md): at the same 20 s every breach left opens onto a free 5 x 3 Stash worth vectors; not a gift, so the rotation stays.
        var stashes = g.Expanded.Where(r => r.Surprise == stashKind).ToList();
        int left = before.Count;
        Check(stashes.Count > 0 && stashes.Count == left - 1 && stashes.All(r => r.Price == 0 && (r.X1 - r.X) * (r.Y1 - r.Y) == 150000), "the breaches open onto free Stashes right away (" + stashes.Count + " of " + left + ")");
        var sw = g.CurrentWorld; float worstDoor = 0f;
        foreach (var b in g.ActiveBreaches)
        {
            int ai = sw.Areas.ToList().FindIndex(a => a.Surprise == stashKind && a.Contains(b.Pos - b.In * BreachEvent.Behind));
            if (ai < 0) continue;
            var dr = sw.Doors.Where(d => d.Joins(ai) && d.Vertical == b.Vertical).ToList();
            worstDoor = Math.Max(worstDoor, dr.Count == 0 ? 999f : dr.Min(d => d.Pos.DistanceTo(b.Pos)));
        }
        Check(worstDoor <= 60f, "each Stash's doorway lines up with its breach (worst " + worstDoor.ToString("0") + " px off)");
        Check(cg.Expanded.Count == g.Expanded.Count && Game.SurpriseCount(g.Expanded) == 1, "the friend gets them too, and they are not counted as gifts");
        Check(gp.Vectors == 10 && stashes.All(r => !r.Claimed), "opening a Stash pays nobody: its vector cache waits to be walked into");
        int count = g.Expanded.Count;
        var st0 = stashes[0]; int si = g.Expanded.FindIndex(r => r.Surprise == stashKind);
        gp.Pos = gp.Disp = new V2((st0.X + st0.X1) / 2f, (st0.Y + st0.Y1) / 2f);
        Run(s, dt, 1.5f, () => cg.Expanded[si].Claimed);
        Check(g.Expanded[si].Claimed && cg.Expanded[si].Claimed && gp.Vectors == 10 + Game.StashVectors, "the player who walks in takes the cache (+" + Game.StashVectors + ") and the friend sees it taken (" + gp.Vectors + ")");
        Check(g.Expanded.Count == count && g.Expanded.Where(r => r.Surprise == stashKind && !r.Claimed).Count() == stashes.Count - 1, "only that Stash's cache is taken");
        // Step 4: a Stash does not close its breach; each breach lets its share in, then stops (the rest come the usual way).
        var nb = g.ActiveBreaches.Length;
        Check(nb == before.Count - 1, "the breaches with a Stash behind them stay open (" + nb + " of " + before.Count + ")");
        var bfl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(Game).GetField("breachTotal", bfl).SetValue(g, 6);
        typeof(Game).GetField("breachUsed", bfl).SetValue(g, new int[nb]);
        var spot = typeof(Game).GetMethod("BreachSpot", bfl, null, new[] { typeof(float) }, null);
        int quota = Breaches.Quota(6, nb), got = 0;
        for (int k = 0; k < nb * quota; k++) got += spot.Invoke(g, new object[] { 20f }) != null ? 1 : 0;
        Check(nb > 0 && got == nb * quota && spot.Invoke(g, new object[] { 20f }) == null, "each breach lets its " + quota + " in, then none (" + got + " came through " + nb + " breaches)");
        int vecNow = gp.Vectors;
        gp.Pos = gp.Disp = new V2(st0.X - 400, st0.Y - 400);
        Run(s, dt, 0.5f, () => false);
        gp.Pos = gp.Disp = new V2((st0.X + st0.X1) / 2f, (st0.Y + st0.Y1) / 2f);
        g.BreachAge = 40f;
        Run(s, dt, 1f, () => false);
        Check(g.Expanded.Count == count && gp.Vectors == vecNow, "one event and one set of Stashes a wave; a cache pays once");
        typeof(Game).GetMethod("RevealBreachRooms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(g, null);
        Check(g.Expanded.Count == count, "the wave's end does not open them again");
    }
}
