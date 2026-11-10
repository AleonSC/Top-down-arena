using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Expanding arena's rooms and offers (Expansion.cs, Docs/ExpandingArena.md step 2): the same offers from the same
// seed, rooms that never overlap or touch except through their own door, a connected map with open doors and steering
// spots either side of them, a size cap, and a mix of shapes and kinds.
static partial class Program
{
    static void ExpansionTest()
    {
        var none = new List<ExpansionRoom>();
        var a = Expansion.Offers(11, 5, none);
        var b = Expansion.Offers(11, 5, none);
        Check(a.Count == 3 && a.SequenceEqual(b), "the same seed, round and map give the same three offers");
        Check(Enumerable.Range(1, 40).Any(s => !Expansion.Offers(s, 5, none).SequenceEqual(a)) && !Expansion.Offers(11, 10, none).SequenceEqual(a), "other seeds and rounds give other offers");
        Check(a.All(o => o.Parent == 0 && o.Price > 0 && o.Kind != RoomKind.Shop), "the first offers all join the hub, cost something and none is a shop");

        var hubOnly = Expansion.Build(none);
        Check(hubOnly.Areas.Length == 1 && hubOnly.Doors.Length == 0 && hubOnly.WallsFor(false).Length == 4 && hubOnly.Max.X == Game.Width && hubOnly.Max.Y == Game.Height,
            "with nothing bought the world is the hub alone, walled in");

        int loopDoors = 0, badWallGap = 0, badFit = 0, badTree = 0, badDoor = 0, badWall = 0, badSteer = 0, badCap = 0, worlds = 0, offersSeen = 0;
        var shapes = new HashSet<string>(); var kinds = new HashSet<RoomKind>(); var floors = new HashSet<PathId>();
        int maxShops = 0; int prevPriceBad = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var built = new List<ExpansionRoom>();
            var rng = new Random(seed);
            for (int round = 5; ; round += 5)
            {
                var offers = Expansion.Offers(seed, round, built);
                if (built.Count >= Expansion.MaxRooms) { badCap += offers.Count; break; }
                if (offers.Count == 0) break;
                offersSeen += offers.Count;
                foreach (var o in offers) { shapes.Add(o.Shape); kinds.Add(o.Kind); if (o.Kind == RoomKind.Floor) floors.Add(o.Floor); }
                var pick = offers[rng.Next(offers.Count)];
                if (pick.Price != Expansion.Cost(built, pick)) prevPriceBad++;
                built.Add(pick);
            }
            worlds++;
            var w = Expansion.Build(built);
            maxShops = Math.Max(maxShops, built.Count(r => r.Kind == RoomKind.Shop));
            // no overlaps, and only the parent shares a wall
            for (int i = 0; i < w.Areas.Length; i++)
                for (int j = i + 1; j < w.Areas.Length; j++)
                {
                    var p = w.Areas[i]; var q = w.Areas[j];
                    bool overlap = p.Min.X < q.Max.X && q.Min.X < p.Max.X && p.Min.Y < q.Max.Y && q.Min.Y < p.Max.Y;
                    float run = (p.Max.X == q.Min.X || q.Max.X == p.Min.X) ? Math.Max(0f, Math.Min(p.Max.Y, q.Max.Y) - Math.Max(p.Min.Y, q.Min.Y))
                              : (p.Max.Y == q.Min.Y || q.Max.Y == p.Min.Y) ? Math.Max(0f, Math.Min(p.Max.X, q.Max.X) - Math.Max(p.Min.X, q.Min.X)) : 0f;
                    bool doored = w.Doors.Any(d => d.Joins(i) && d.Joins(j));
                    if (overlap || (run > 0f && run < Expansion.MinDoorRun) || (run >= Expansion.MinDoorRun && !doored) || (run < Expansion.MinDoorRun && doored)) badFit++;
                    if (doored) loopDoors++;
                }
            if (w.Max.X - w.Min.X > Expansion.MaxWidth || w.Max.Y - w.Min.Y > Expansion.MaxHeight) badCap++;
            // connected: every room reachable from the hub through doors (a tree: one door per room)
            var seen = new HashSet<int> { 0 }; var queue = new Queue<int>(); queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (var d in w.Doors.Where(d => d.Joins(cur)))
                    if (seen.Add(d.Other(cur))) queue.Enqueue(d.Other(cur));
            }
            if (seen.Count != w.Areas.Length || w.Doors.Length < w.Areas.Length - 1) badTree++;
            // each door sits on both rooms' shared wall, DoorW inside it, and is open (no solid) in the walls and the floor plan
            var plan = w.With(LayoutId.Open);
            foreach (var d in w.Doors)
            {
                var pa = w.Areas[d.A]; var pb = w.Areas[d.B];
                bool onBoth = pa.Contains(d.Pos, 1f) && pb.Contains(d.Pos, 1f);
                float run = d.Vertical ? Math.Min(pa.Max.Y, pb.Max.Y) - Math.Max(pa.Min.Y, pb.Min.Y) : Math.Min(pa.Max.X, pb.Max.X) - Math.Max(pa.Min.X, pb.Min.X);
                if (!onBoth || run < Expansion.MinDoorRun || plan.Solids.Any(sd => Layouts.Inside(sd, d.Pos, 30f))) badDoor++;
                if (!plan.Waypoints.Any(wp => wp.DistanceTo(d.Pos + d.Across * Rooms.DoorPad) < 1f) || !plan.Waypoints.Any(wp => wp.DistanceTo(d.Pos - d.Across * Rooms.DoorPad) < 1f)) badSteer++;
            }
            // the walls are closed: every point along every room's edge is wall, except at its doors
            foreach (var ar in w.Areas)
                for (int side = 0; side < 4; side++)
                    for (float t = 10f; t < (side < 2 ? ar.Size.Y : ar.Size.X); t += 25f)
                    {
                        V2 pt = side == 0 ? new V2(ar.Min.X, ar.Min.Y + t) : side == 1 ? new V2(ar.Max.X, ar.Min.Y + t) : side == 2 ? new V2(ar.Min.X + t, ar.Min.Y) : new V2(ar.Min.X + t, ar.Max.Y);
                        bool atDoor = w.Doors.Any(d => pt.DistanceTo(d.Pos) < Rooms.DoorW / 2f + 14f);
                        if (!atDoor && !plan.Solids.Any(sd => Layouts.Inside(sd, pt, 0f))) badWallGap++;
                    }
            // every room's centre is in the open and in its own area
            for (int i = 0; i < w.Areas.Length; i++)
                if (plan.Solids.Any(sd => Layouts.Inside(sd, w.Areas[i].Mid, 0f)) || w.AreaAt(w.Areas[i].Mid) != i) badWall++;
        }
        Check(offersSeen > 600, "offers keep coming until the cap (" + offersSeen + " seen over " + worlds + " maps)");
        Check(badWallGap == 0, "every wall is closed all the way along, even where a room is wider than the one it joins (" + badWallGap + " gaps)");
        Check(loopDoors > worlds * 3, "rooms touching along a door's width get a door, so loops form (" + loopDoors + " doors over " + worlds + " maps)");
        Check(badFit == 0, "rooms never overlap, never touch in a sliver, and every shared stretch of wall has its door (" + badFit + " bad)");
        Check(badTree == 0, "every map is connected (" + badTree + " bad)");
        Check(badDoor == 0 && badSteer == 0, "every door sits on both rooms' shared wall, is open, and has a steering spot each side (" + badDoor + "/" + badSteer + " bad)");
        Check(badWall == 0, "every room's middle is open ground in its own area (" + badWall + " bad)");
        Check(badCap == 0, "the map stops at " + Expansion.MaxRooms + " rooms and its size cap (" + badCap + " over)");
        Check(shapes.Count == Expansion.Shapes.Length && kinds.Count == 3 && floors.Count >= 4, "the offers use every shape (" + shapes.Count + "), every kind and several floors (" + floors.Count + ")");
        Check(maxShops >= 1 && maxShops <= 3, "shops turn up, a few at most (" + maxShops + ")");
        Check(prevPriceBad == 0, "every offer costs its vector cost (the new wall length)");

        // Drawn rooms (Expansion.Custom): a hallway off the hub is fine; thin, huge, off the grid, overlapping, floating and sliver boxes are refused.
        string why;
        var hall = Expansion.Custom(none, 500, -600, 300, 600, out why);
        Check(hall.HasValue && hall.Value.Parent == 0 && hall.Value.Shape == "Hallway" && hall.Value.Kind == RoomKind.Combat && hall.Value.Price > 0, "a 3-wide hallway off the hub is accepted (" + (hall.HasValue ? hall.Value.Price + " gold" : why) + ")");
        Check(Expansion.Custom(none, 500, -600, 200, 600, out why) == null && why.Contains("thin"), "too thin is refused: " + why);
        Check(Expansion.Custom(none, 500, -1300, 1300, 1300, out why) == null && why.Contains("big"), "too big is refused: " + why);
        Check(Expansion.Custom(none, 550, -600, 300, 600, out why) == null && why.Contains("grid"), "off the grid is refused: " + why);
        Check(Expansion.Custom(none, 100, 100, 300, 300, out why) == null && why.Contains("Overlaps"), "overlapping the hub is refused: " + why);
        Check(Expansion.Custom(none, 3000, 3000, 300, 300, out why) == null && why.Contains("share"), "floating off by itself is refused: " + why);
        Check(Expansion.Custom(none, 1400, -500, 300, 500, out why) == null && why.Contains("briefly"), "a sliver of shared wall is refused: " + why);
        var east = Expansion.Custom(none, 1600, 0, 600, 300, out why).Value;
        var withEast = new List<ExpansionRoom> { east };
        var under = Expansion.Custom(withEast, 1600, 300, 600, 300, out why);
        Check(under.HasValue && Expansion.LoopsOf(withEast, under.Value) == 1 && under.Value.Parent == 1, "a box touching two rooms closes a loop (parent = the longer wall)");
        var loopWorld = Expansion.Build(new List<ExpansionRoom> { east, under.Value });
        Check(loopWorld.Doors.Length == 3 && loopWorld.Doors.Count(d => d.Joins(0)) == 2, "and gets a door to each (" + loopWorld.Doors.Length + " doors)");
        Check(Expansion.Custom(Enumerable.Range(0, Expansion.MaxRooms).Select(i => east).ToList(), 500, -600, 300, 600, out why) == null, "a full map takes no more");

        // Vector cost (Docs/VectorBuilding.md): the new wall length, so a shared wall is free and a loop is cheap.
        Check(Expansion.Cost(none, 500, -400, 600, 400) == 14, "a 6 x 4 closet along the hub's wall costs its perimeter 20 less the 6 it shares: 14 (" + Expansion.Cost(none, 500, -400, 600, 400) + ")");
        Check(Expansion.Cost(none, 500, -1200, 300, 1200) == 27, "a 3 x 12 hallway standing off the hub costs 30 less the 3 it shares: 27 (" + Expansion.Cost(none, 500, -1200, 300, 1200) + ")");
        Check(Expansion.Cost(withEast, under.Value) == 9, "a box that touches two rooms costs less again: 18 less 3 and 6 = 9 (" + Expansion.Cost(withEast, under.Value) + ")");
        Check(Expansion.Cost(none, 500, -400, 600, 400) < Expansion.Cost(none, 500, -400, 1200, 400), "a bigger room costs more");
    }

    // The Expanding arena in a run (ExpansionGame.cs): the Arena setting, the purse, the vote on the offer, the bought
    // room coming up on both screens, the plain box until then and on boss waves, spawns inside rooms, a bigger crowd.
    // Boxes that fit on the map (Expansion.Custom accepts them), found by trying 3x6, 6x3 and 6x6 tiles round every room.
    static List<int[]> FindBoxes(IList<ExpansionRoom> built, int count)
    {
        var found = new List<int[]>();
        var w = Expansion.Build(built);
        var sizes = new[] { new[] { 300, 600 }, new[] { 600, 300 }, new[] { 600, 600 } };
        foreach (var a in w.Areas)
            foreach (var sz in sizes)
            {
                int bw = sz[0], bh = sz[1], ax0 = (int)a.Min.X, ay0 = (int)a.Min.Y, ax1 = (int)a.Max.X, ay1 = (int)a.Max.Y;
                var cands = new List<int[]>();
                for (int x = ax0 - bw + 300; x <= ax1 - 300; x += 100) { cands.Add(new[] { x, ay0 - bh, bw, bh }); cands.Add(new[] { x, ay1, bw, bh }); }
                for (int y = ay0 - bh + 300; y <= ay1 - 300; y += 100) { cands.Add(new[] { ax1, y, bw, bh }); cands.Add(new[] { ax0 - bw, y, bw, bh }); }
                foreach (var c in cands)
                {
                    string why;
                    if (Expansion.Custom(built, c[0], c[1], c[2], c[3], out why).HasValue && !found.Any(f => f[0] == c[0] && f[1] == c[1] && f[2] == c[2] && f[3] == c[3]))
                    {
                        found.Add(c);
                        if (found.Count >= count)
                            return found;
                    }
                }
            }
        return found;
    }

    // The Expanding arena in a run (ExpansionGame.cs, Docs/VectorBuilding.md): the Arena setting, a building round between waves,
    // proposals drawn by players, the vote (a tie is a random pick), the winner's drawer paying vectors, the room coming up on both
    // screens, the plain box until then and on boss waves, spawns inside rooms, a bigger crowd.
    static void ExpandingGameTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var pickRooms = typeof(Game).GetMethod("PickRooms", flags);
        var openRound = typeof(Game).GetMethod("OpenBuildRound", flags);
        var resolveRound = typeof(Game).GetMethod("ResolveBuildRound", flags);
        var randomSpot = typeof(Game).GetMethod("RoomsRandomSpot", flags);

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Action Quiet = () =>
        {
            ((IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
            ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
        };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, NoPicks);
        Quiet();

        // The setting: Expanding wins over Rooms, and until a room is built the arena is the plain Classic box.
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.Diff.Set(DiffKnob.ArenaRooms, 1f);
        Check(g.ArenaKind == ArenaLayout.Expanding && g.ExpandingOn, "the Expanding setting wins over Rooms");
        g.Diff.Set(DiffKnob.ArenaRooms, 0f);
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.3f);
        Check(!g.RoomsOn && !cg.RoomsOn && g.CurrentWorld == World.Single && g.ArenaMin.X == 0f && g.ExpansionScale == 1f, "before the first room the Expanding arena is the plain box");

        // A building round opens: everyone sees it, nobody has proposed.
        openRound.Invoke(g, null);
        Run(s, dt, 3f, () => g.ExpandOpen && cg.ExpandOpen);
        Check(g.ExpandOpen && cg.ExpandOpen && g.ExpandProposals.Count == 0 && cg.ExpandProposals.Count == 0, "a cleared wave opens a building round on both screens, with no proposals yet");

        // Proposals: a bad drawing and one the drawer can't pay for are refused; a good one is backed by its drawer.
        var boxes = FindBoxes(g.Expanded, 3);
        Check(boxes.Count == 3, "there is somewhere to draw rooms off the hub");
        var b0 = boxes[0]; var b1 = boxes[1];
        cg.ProposeExpandRoom(100, 100, 300, 300);  // overlaps the hub
        Run(s, dt, 0.5f);
        Check(g.ExpandProposals.Count == 0, "a bad drawing is refused");
        g.Players[2].Vectors = 3;
        cg.ProposeExpandRoom(b0[0], b0[1], b0[2], b0[3]);
        Run(s, dt, 0.5f);
        Check(g.ExpandProposals.Count == 0, "a drawing its drawer can't pay for is refused");
        g.Players[2].Vectors = Game.VectorStart;
        cg.ProposeExpandRoom(b0[0], b0[1], b0[2], b0[3]);
        Run(s, dt, 3f, () => g.ExpandProposals.Count == 1 && cg.ExpandProposals.Count == 1 && cg.ExpandVotes.ContainsKey(2));
        Check(g.ExpandProposals.Count == 1 && g.ExpandProposals[0].Owner == 2 && g.ExpandProposals[0].Price == Expansion.Cost(g.Expanded, b0[0], b0[1], b0[2], b0[3]) && cg.ExpandProposals[0].Equals(g.ExpandProposals[0]) && g.ExpandVotes[2] == 2 && cg.ExpandVotes[2] == 2 && g.ExpandOpen,
            "a good drawing becomes its drawer's proposal, costs its wall length, backed by its drawer, the same on both screens, and the round waits for the host's vote");
        cg.ProposeExpandRoom(b1[0], b1[1], b1[2], b1[3]);  // a newer drawing replaces it
        Run(s, dt, 3f, () => g.ExpandProposals.Count == 1 && g.ExpandProposals[0].X == b1[0] && cg.ExpandProposals.Count == 1 && cg.ExpandProposals[0].X == b1[0]);
        Check(g.ExpandProposals.Count == 1 && g.ExpandProposals[0].X == b1[0] && g.ExpandProposals[0].Y == b1[1], "a newer drawing replaces the drawer's last one");

        // The vote: the host backs the client's room; everyone has voted, so it is built and paid for by its drawer.
        int cost = g.ExpandProposals[0].Price, clientBefore = g.Players[2].Vectors, hostBefore = g.Players[1].Vectors;
        g.CastExpandVote(2);
        Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen && cg.Expanded.Count == 1 && cg.Players[2].Vectors == g.Players[2].Vectors);
        Check(g.Expanded.Count == 1 && g.Expanded[0].X == b1[0] && g.Expanded[0].Owner == 2 && g.Players[2].Vectors == clientBefore - cost && g.Players[1].Vectors == hostBefore && cg.Players[2].Vectors == g.Players[2].Vectors && cg.Expanded.Count == 1 && cg.Expanded[0].Equals(g.Expanded[0]),
            "everyone voting settles the round: the room is built, its drawer pays " + cost + " vectors, nobody else does, both screens agree");

        // The room comes up on the next regular wave, with its walls and a bigger crowd; boss waves stay in the middle.
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.4f);
        Check(g.RoomsOn && cg.RoomsOn && g.CurrentWorld.Areas.Length == 2 && g.ArenaMax.DistanceTo(g.CurrentWorld.Max) < 0.01f && cg.CurrentWorld.Areas.Length == 2 && g.Walled,
            "the built room is up on a regular wave: two areas, walled, the same on the client");
        Check(g.LayoutOn && g.ActiveLayout.Name.Contains("Expanding") && g.ActiveLayout.Solids.Length >= g.CurrentWorld.WallsFor(false).Length, "its walls are part of the floor plan (" + g.ActiveLayout.Name + ")");
        Check(g.ExpansionScale > 1f && g.ExpansionScale <= Game.MaxExpandScale, "a bigger map means a bigger crowd (x" + g.ExpansionScale.ToString("0.00") + ")");
        bool inRooms = true, inNew = false;
        for (int i = 0; i < 300; i++)
        {
            V2 at = (V2)randomSpot.Invoke(g, new object[] { 20f });
            var a = g.CurrentWorld.Areas[g.CurrentWorld.AreaAt(at)];
            inRooms &= a.Contains(at);
            inNew |= !a.Hub;
        }
        Check(inRooms && inNew, "enemies come in inside a room, never in the gaps between them, and the new room gets some");
        int w0 = g.Wave;
        g.Wave = 10;
        pickRooms.Invoke(g, null);
        Check(g.IsBossWave(g.Wave) && !g.RoomsUp, "a boss wave is fought in the hub alone");
        g.Wave = w0;
        pickRooms.Invoke(g, null);

        // A 50/50 tie is a random pick: over eight rounds both players' rooms win, one room is built each round, and the loser pays nothing.
        var winners = new HashSet<int>();
        int built0 = g.Expanded.Count, rounds = 0;
        bool loserPaid = false;
        for (int round = 0; round < 8; round++)
        {
            foreach (var p in g.Players.Values) p.Vectors = 40;
            openRound.Invoke(g, null);
            Run(s, dt, 3f, () => g.ExpandOpen && cg.ExpandOpen);
            var bx = FindBoxes(g.Expanded, 2);
            if (bx.Count < 2) break;
            g.ProposeExpandRoom(bx[0][0], bx[0][1], bx[0][2], bx[0][3]);
            cg.ProposeExpandRoom(bx[1][0], bx[1][1], bx[1][2], bx[1][3]);
            Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen && cg.Expanded.Count == g.Expanded.Count);
            rounds++;
            var last = g.Expanded[g.Expanded.Count - 1];
            winners.Add(last.Owner);
            int other = last.Owner == 1 ? 2 : 1;
            loserPaid |= g.Players[other].Vectors != 40;
        }
        Check(rounds >= 6 && g.Expanded.Count == built0 + rounds, "one room is built each round (" + rounds + " rounds)");
        Check(winners.Contains(1) && winners.Contains(2), "a 50/50 tie is a random pick: both players' rooms won (" + string.Join(",", winners.Select(w => w.ToString()).ToArray()) + ")");
        Check(!loserPaid, "the player whose room lost pays nothing");

        // "No room" can win; no proposals builds nothing; a round closing with one player's vote builds that room.
        foreach (var p in g.Players.Values) p.Vectors = 40;
        int builtNow = g.Expanded.Count;
        openRound.Invoke(g, null);
        Run(s, dt, 3f, () => g.ExpandOpen && cg.ExpandOpen);
        var nb = FindBoxes(g.Expanded, 1)[0];
        g.ProposeExpandRoom(nb[0], nb[1], nb[2], nb[3]);
        Run(s, dt, 3f, () => g.ExpandProposals.Count == 1);
        g.CastExpandVote(0);
        cg.CastExpandVote(0);
        Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen);
        Check(g.Expanded.Count == builtNow && g.Players[1].Vectors == 40 && !g.ExpandOpen, "everyone voting for no room builds nothing and keeps the vectors");
        openRound.Invoke(g, null);
        Run(s, dt, 3f, () => g.ExpandOpen && cg.ExpandOpen);
        resolveRound.Invoke(g, null);  // (what the next wave's start does)
        Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen);
        Check(g.Expanded.Count == builtNow && !cg.ExpandOpen, "a round with no proposals closes with nothing built");
        openRound.Invoke(g, null);
        Run(s, dt, 3f, () => g.ExpandOpen && cg.ExpandOpen);
        g.ProposeExpandRoom(nb[0], nb[1], nb[2], nb[3]);
        Run(s, dt, 0.5f);
        Check(g.ExpandOpen, "one player's vote of two leaves the round open");
        resolveRound.Invoke(g, null);  // the wave is about to start: the votes cast count
        Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen && cg.Expanded.Count == g.Expanded.Count);
        Check(g.Expanded.Count == builtNow + 1 && g.Expanded[g.Expanded.Count - 1].Owner == 1 && cg.Expanded.Count == g.Expanded.Count, "when the wave starts the votes cast count: the only vote builds that room");

        // The map stops growing at the cap.
        var full = new List<ExpansionRoom>();
        for (int i = 0; i < Expansion.MaxRooms; i++) full.Add(Expansion.Offers(3, 5 + 5 * i, full)[0]);
        Check(Expansion.Offers(3, 100, full).Count == 0, "a full map offers nothing more");
    }
}

static partial class Program
{
    // Bots play from wave 1 on the Expanding arena: they draw rooms they can afford, vote, and the map grows; no round is left open.
    static void ExpandingEconomyTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 1 }, port, true, "Host", Role.Tank);
        var client = JoinAs(port, true, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        Run(s, dt, 700f, () => g.Wave >= 8 && g.State == MatchState.Fighting);
        Console.WriteLine("  by wave " + g.Wave + ": " + g.Expanded.Count + " rooms built (owners " + string.Join(",", g.Expanded.Select(r => r.Owner.ToString()).ToArray()) + "), vectors " + string.Join("/", g.Players.Values.Select(p => p.Vectors.ToString()).ToArray()));
        Check(g.Expanded.Count >= 1 && cg.Expanded.Count == g.Expanded.Count, "bot players draw rooms they can afford and vote: the map grows (" + g.Expanded.Count + " rooms)");
        Check(!g.ExpandOpen && !cg.ExpandOpen, "no building round is left open once the wave starts");
    }
}

static partial class Program
{
    // Room features (ExpansionGame.cs, Paths.cs, Shop.cs): Floor rooms run a path's rule inside the room only, pay its bonus,
    // shop rooms are safe, drop gold and open the shop after the wave that follows a boss.
    static void ExpandingFeaturesTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var pickRooms = typeof(Game).GetMethod("PickRooms", flags);
        var spawnEnemyAt = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var randomSpot = typeof(Game).GetMethod("RoomsRandomSpot", flags);
        var tickPaths = typeof(Game).GetMethod("TickPaths", flags);
        var floorBonus = typeof(Game).GetMethod("FloorBonusAt", flags);

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Action Quiet = () =>
        {
            ((IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
            ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
        };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, NoPicks);
        Quiet();

        // A map with a shop room and one room of each floor we test, around the hub.
        var built = new List<ExpansionRoom>();
        Func<int, int, int, int, RoomKind, PathId, ExpansionRoom> add = (x, y, w, h, kind, floor) =>
        {
            string why;
            var r = Expansion.Custom(built, x, y, w, h, out why);
            if (!r.HasValue) throw new Exception("test map: " + why);
            var room = r.Value; room.Kind = kind; room.Floor = floor;
            built.Add(room);
            return room;
        };
        add(1600, 300, 600, 300, RoomKind.Shop, PathId.None);                // 1: east of the hub
        add(500, -600, 600, 600, RoomKind.Floor, PathId.BurningGrounds);     // 2: north
        add(500, 900, 600, 600, RoomKind.Floor, PathId.FrozenLake);          // 3: south
        add(-600, 300, 600, 300, RoomKind.Floor, PathId.BloodMoon);          // 4: west
        add(1600, 600, 600, 300, RoomKind.Floor, PathId.EliteGauntlet);      // 5: south-east (touches the shop and the hub: a loop)
        foreach (var gg in new[] { g, cg })
        {
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
            gg.Expanded.AddRange(built);
        }
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.3f);
        Check(g.RoomsOn && g.CurrentWorld.Areas.Length == 6 && g.HasShopRoom, "the map with a shop room and four floors is up");
        V2 shopAt = g.CurrentWorld.Areas[1].Mid, fireAt = g.CurrentWorld.Areas[2].Mid, iceAt = g.CurrentWorld.Areas[3].Mid, bloodAt = g.CurrentWorld.Areas[4].Mid, eliteAt = g.CurrentWorld.Areas[5].Mid, hubAt = new V2(800f, 450f);
        Check(g.FloorAt(fireAt) == PathId.BurningGrounds && g.FloorAt(iceAt) == PathId.FrozenLake && g.FloorAt(bloodAt) == PathId.BloodMoon && g.FloorAt(eliteAt) == PathId.EliteGauntlet && g.FloorAt(hubAt) == PathId.None && g.FloorAt(shopAt) == PathId.None,
            "each Floor room knows its path's ground; the hub and the shop room have none");
        Check((float)floorBonus.Invoke(g, new object[] { fireAt }) == Paths.Bonus(Paths.Get(PathId.BurningGrounds).Danger) && (float)floorBonus.Invoke(g, new object[] { hubAt }) == 1f && Paths.Bonus(Paths.Get(PathId.BurningGrounds).Danger) > 1f,
            "kills in a Floor room pay its path's bonus, kills in the hub don't");
        var me = g.Players[1];
        me.Pos = me.Disp = shopAt;
        Check(g.InShopRoom(me), "standing in the shop room counts as being in it");
        me.Pos = me.Disp = hubAt;
        Check(!g.InShopRoom(me), "...and the hub doesn't");

        // Spawns: none in the shop room, more in the mix floors, always inside a room.
        int inShop = 0, inElite = 0, inFloors = 0, total = 600;
        for (int i = 0; i < total; i++)
        {
            V2 at = (V2)randomSpot.Invoke(g, new object[] { 20f });
            var a = g.CurrentWorld.Areas[g.CurrentWorld.AreaAt(at)];
            if (a.Kind == RoomKind.Shop) inShop++;
            if (a.Kind == RoomKind.Floor && a.Floor == PathId.EliteGauntlet) inElite++;
            if (a.Kind == RoomKind.Floor) inFloors++;
        }
        Check(inShop == 0 && inElite > 0 && inFloors > 0, "no enemy comes in to the shop room, and the Floor rooms get their share (" + inFloors + "/" + total + ")");

        // Floor rules run in their room only: ice chills enemies there, the Elite floor makes them elite, the hub does neither.
        var inIce = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Grunt, iceAt, 0f, 0, false });
        var inHub = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Grunt, hubAt, 0f, 0, false });
        var inElite2 = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Grunt, eliteAt, 0f, 0, false });
        Check(Math.Abs(inIce.Speed - inHub.Speed * Paths.IceEnemySpeed) < 0.01f && !inHub.Elite && inElite2.Elite, "a foe in the ice room is chilled, one in the Elite room is elite, one in the hub is neither");
        Quiet();

        // Patches: the Burning room burns (inside it only), and no other room does.
        g.State = MatchState.Fighting;
        for (int i = 0; i < 60; i++) tickPaths.Invoke(g, new object[] { 1f });
        var fires = g.Patches.Values.Where(pt => pt.Kind == PatchKind.Fire).ToList();
        Check(fires.Count > 0 && fires.All(pt => g.FloorAt(pt.Pos) == PathId.BurningGrounds) && fires.Count <= (int)Math.Ceiling(Paths.FireMax * 0.25f), "fire patches burn inside the Burning room only (" + fires.Count + ")");
        Check(g.Patches.Values.All(pt => pt.Kind == PatchKind.Fire), "no healing pools without a Sanctuary room");

        // Gold and the shop: kills drop gold with a shop room; the wave after a boss ends with the shop open; walking in and shopping closes it.
        foreach (var p in g.Players.Values) p.Gold = 0;
        for (int i = 0; i < 4; i++) spawnEnemyAt.Invoke(g, new object[] { EnemyType.Grunt, hubAt + new V2(30f * i, 0f), 0f, 0, false });
        Quiet();
        Run(s, dt, 0.5f);
        Check(g.Players.Values.All(p => p.Gold > 0), "kills drop gold for everyone while the map has a shop room (" + g.Players[1].Gold + ")");
        Quiet();
        Run(s, dt, 30f, () => g.State == MatchState.Intermission, NoPicks);
        Check(g.State == MatchState.Intermission && g.ShopOpen && cg.ShopOpen, "the wave after a boss ends with the shop open, on both screens");
        Check(g.Players.Values.All(p => p.Shelf.Any(u => u >= 0)), "everyone has a shelf");
        Check(g.Players.Values.All(p => p.Vectors >= Game.VectorStart + Game.VectorWaveGain) && cg.Players[1].Vectors == g.Players[1].Vectors, "a cleared wave gives everyone " + Game.VectorWaveGain + " vectors, and the client sees them (" + g.Players[1].Vectors + ")");
        host.Game.Shop(ShopAction.Done, 1);
        client.Game.Shop(ShopAction.Done, 1);
        Run(s, dt, 0.5f);
        Check(!g.ShopOpen && !cg.ShopOpen, "everyone clicking Done closes the shop");

        // The cleared wave opened a building round; a lone proposal is built as the next wave starts (the votes cast count).
        Check(g.ExpandOpen && cg.ExpandOpen && g.ExpandProposals.Count == 0 && !g.ShopOpen, "the cleared wave opened a building round, on both screens");
        int rooms0 = g.Expanded.Count;
        var fb = FindBoxes(g.Expanded, 8).First(bx => Expansion.Cost(g.Expanded, bx[0], bx[1], bx[2], bx[3]) <= g.Players[1].Vectors);
        g.ProposeExpandRoom(fb[0], fb[1], fb[2], fb[3]);
        Run(s, dt, 0.4f);
        Check(g.ExpandOpen && g.Expanded.Count == rooms0, "the round doesn't wait for the wave to start: the countdown ticks on");
        typeof(Game).GetField("StateTimer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).SetValue(g, 0.05f);
        Run(s, dt, 30f, () => g.State == MatchState.Fighting, NoPicks);
        Run(s, dt, 0.6f);
        Check(!g.ExpandOpen && !cg.ExpandOpen && g.Expanded.Count == rooms0 + 1 && cg.Expanded.Count == g.Expanded.Count, "the round closes as the wave starts: the proposal's room is built, on both screens");
    }
}

static partial class Program
{
    // Vectors (ExpansionGame.cs, Docs/VectorBuilding.md step 1): the start, the cap, a cleared wave, every 25 kills, a boss refill, synced.
    static void ExpandingVectorsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var grantWave = typeof(Game).GetMethod("GrantVectorsForWave", flags);
        var grantKill = typeof(Game).GetMethod("GrantVectorKill", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        var p1 = g.Players[1];
        Check(p1.Vectors == Game.VectorStart && p1.VectorCap == Game.VectorCapBase && Game.VectorStart < Game.VectorCapBase, "everyone starts with " + Game.VectorStart + " vectors and a cap of " + Game.VectorCapBase);
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.Wave = 6;
        grantWave.Invoke(g, null);
        Check(p1.Vectors == Game.VectorStart + Game.VectorWaveGain, "a cleared wave gives " + Game.VectorWaveGain + " (" + p1.Vectors + ")");
        p1.Vectors = p1.VectorCap - 2;
        grantWave.Invoke(g, null);
        Check(p1.Vectors == p1.VectorCap, "gains stop at the cap");
        p1.Run.Kills = Game.VectorKillEvery - 1; p1.Vectors = 10;
        grantKill.Invoke(g, new object[] { p1 });
        Check(p1.Vectors == 10, "24 kills give nothing yet");
        p1.Run.Kills = Game.VectorKillEvery;
        grantKill.Invoke(g, new object[] { p1 });
        Check(p1.Vectors == 11, "the 25th kill gives a vector");
        p1.Vectors = 3;
        g.Wave = 10;
        grantWave.Invoke(g, null);
        Check(g.Players.Values.All(p => p.Vectors == p.VectorCap + Game.VectorBossBonus), "a cleared boss wave refills everyone to the cap and adds " + Game.VectorBossBonus + " on top");
        grantWave.Invoke(g, null);
        Check(p1.Vectors == p1.VectorCap + Game.VectorBossBonus, "and doesn't stack with itself");
        g.Wave = 6;
        grantWave.Invoke(g, null);
        Check(p1.Vectors == p1.VectorCap + Game.VectorBossBonus, "ordinary gains leave a stock above the cap alone");
        Run(s, dt, 0.5f);
        Check(cg.Players[1].Vectors == p1.Vectors && cg.Players[2].Vectors == g.Players[2].Vectors && cg.Players[1].VectorCap == p1.VectorCap, "the client sees everyone's vectors");
        g.Diff.Set(DiffKnob.ArenaExpanding, 0f);
        p1.Vectors = 5;
        grantWave.Invoke(g, null);
        Check(p1.Vectors == 5, "outside the Expanding arena vectors don't change");
    }
}
