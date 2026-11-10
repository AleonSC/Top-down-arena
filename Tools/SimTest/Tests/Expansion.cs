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

        int coverSeen = 0, badCover = 0, loopDoors = 0, badWallGap = 0, badFit = 0, badTree = 0, badDoor = 0, badWall = 0, badSteer = 0, badCap = 0, worlds = 0, offersSeen = 0;
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
            // obstacles: inside their room clear of its walls, off every door, none in a hallway, the same every time
            var again = Expansion.Build(built);
            coverSeen += w.Cover.Length;
            if (again.Cover.Length != w.Cover.Length || w.Cover.Where((c, i) => c.Pos.DistanceTo(again.Cover[i].Pos) > 0.01f).Any()) badCover++;
            foreach (var c in w.Cover)
            {
                var host = w.Areas.Skip(1).Where(ar2 => ar2.Contains(c.Pos, -Expansion.CoverWall + 5f)).ToList();
                if (host.Count != 1 || Math.Min(host[0].Size.X, host[0].Size.Y) <= Expansion.MinSide) badCover++;
                if (w.Doors.Any(d => d.Pos.DistanceTo(c.Pos) < 200f)) badCover++;
            }
            // every room's centre is in the open and in its own area
            for (int i = 0; i < w.Areas.Length; i++)
                if (plan.Solids.Any(sd => Layouts.Inside(sd, w.Areas[i].Mid, 0f)) || w.AreaAt(w.Areas[i].Mid) != i) badWall++;
        }
        Check(offersSeen > 600, "offers keep coming until the cap (" + offersSeen + " seen over " + worlds + " maps)");
        Check(badCover == 0 && coverSeen > 60, "built rooms hold obstacles and bars of wall: inside their room clear of its walls, off every door, none in a hallway, the same on every build (" + coverSeen + " over " + worlds + " maps, " + badCover + " bad)");
        Check(badWallGap == 0, "every wall is closed all the way along, even where a room is wider than the one it joins (" + badWallGap + " gaps)");
        Check(loopDoors > worlds * 3, "rooms touching along a door's width get a door, so loops form (" + loopDoors + " doors over " + worlds + " maps)");
        Check(badFit == 0, "rooms never overlap, never touch in a sliver, and every shared stretch of wall has its door (" + badFit + " bad)");
        Check(badTree == 0, "every map is connected (" + badTree + " bad)");
        Check(badDoor == 0 && badSteer == 0, "every door sits on both rooms' shared wall, is open, and has a steering spot each side (" + badDoor + "/" + badSteer + " bad)");
        Check(badWall == 0, "every room's middle is open ground in its own area (" + badWall + " bad)");
        Check(badCap == 0, "the map stops at " + Expansion.MaxRooms + " rooms and its size cap (" + badCap + " over)");
        Check(shapes.Count == Expansion.Shapes.Length && kinds.Count == 3 && floors.Count >= 4, "the offers use every shape (" + shapes.Count + "), every kind and several floors (" + floors.Count + ")");
        Check(maxShops >= 1 && maxShops <= 1 + Expansion.MaxRooms / 6, "shops turn up, a few at most (" + maxShops + ")");
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

        // Floating rooms (Docs/FloatingRooms.md): placed off the map, deterministic, clear of everything, sealed until a built room touches them.
        var shopF = Expansion.FloatingFor(7, 0, none, RoomKind.Shop);
        Check(shopF.HasValue && shopF.Value.Floating && shopF.Value.Kind == RoomKind.Shop && Expansion.FloatingFor(7, 0, none, RoomKind.Shop).Value.Equals(shopF.Value)
            && !Expansion.FloatingFor(7, 1, none, RoomKind.Shop).Value.Equals(shopF.Value), "a floating room is the same for the same seed and cycle, and another one for the next cycle");
        int badFloat = 0, floatsMade = 0;
        for (int seed = 1; seed <= 80; seed++)
        {
            var map = new List<ExpansionRoom>();
            for (int k = 0; k < 4; k++)
            {
                var fk = Expansion.FloatingFor(seed, k, map, k % 2 == 0 ? RoomKind.Shop : RoomKind.Gatekeeper);
                if (!fk.HasValue) { badFloat++; continue; }
                var f = fk.Value; floatsMade++;
                foreach (var o in map.Concat(new[] { new ExpansionRoom { X = 0, Y = 0, W = (int)Game.Width, H = (int)Game.Height } }))
                {
                    bool clear = f.X - 200 >= o.X1 || f.X1 + 200 <= o.X || f.Y - 200 >= o.Y1 || f.Y1 + 200 <= o.Y;
                    if (!clear) badFloat++;
                }
                map.Add(f);
                var all = Expansion.Build(map);
                if (all.Max.X - all.Min.X > Expansion.MaxWidth || all.Max.Y - all.Min.Y > Expansion.MaxHeight) badFloat++;
            }
            if (Expansion.SealedCount(map) != map.Count) badFloat++;
            if (Expansion.Build(map).Doors.Length != 0 || Expansion.BoughtCount(map) != 0) badFloat++;
        }
        Check(badFloat == 0 && floatsMade > 250, "floating rooms are 2 tiles clear of every room, inside the size cap, and sealed: no doors, none counted as bought (" + floatsMade + " made, " + badFloat + " bad)");
        var floater = new ExpansionRoom { X = 500, Y = -1200, W = 600, H = 400, Kind = RoomKind.Shop, Floating = true };
        var withFloater = new List<ExpansionRoom> { floater };
        int fp, fl2; string fwhy;
        Check(!Expansion.Placement(withFloater, 500, -1600, 600, 400, out fp, out fl2, out fwhy) && fwhy.Contains("connected"), "a box touching only a floating room is refused: " + fwhy);
        var link = Expansion.Custom(withFloater, 700, -800, 300, 800, out why);
        Check(link.HasValue && link.Value.Parent == 0 && Expansion.LoopsOf(withFloater, link.Value) == 1, "a hallway from the hub to the floating room is accepted and opens it (" + (link.HasValue ? "ok" : why) + ")");
        var opened = new List<ExpansionRoom> { floater, link.Value };
        var openedWorld = Expansion.Build(opened);
        Check(Expansion.SealedCount(withFloater) == 1 && Expansion.SealedCount(opened) == 0 && openedWorld.Areas[1].Floating && !openedWorld.Areas[1].Sealed && openedWorld.Doors.Any(d => d.Joins(1)) && Expansion.BoughtCount(opened) == 1,
            "built rooms that touch it give it a door: it is no longer sealed, and only the hallway counts as bought");
        // Vector cost (Docs/VectorBuilding.md): the new wall length, so a shared wall is free and a loop is cheap.
        Check(Expansion.Cost(none, 500, -400, 600, 400) == 14, "a 6 x 4 closet along the hub's wall costs its perimeter 20 less the 6 it shares: 14 (" + Expansion.Cost(none, 500, -400, 600, 400) + ")");
        Check(Expansion.Cost(none, 500, -1200, 300, 1200) == 27, "a 3 x 12 hallway standing off the hub costs 30 less the 3 it shares: 27 (" + Expansion.Cost(none, 500, -1200, 300, 1200) + ")");
        Check(Expansion.Cost(withEast, under.Value) == 9, "a box that touches two rooms costs less again: 18 less 3 and 6 = 9 (" + Expansion.Cost(withEast, under.Value) + ")");
        Check(Expansion.Cost(none, 500, -400, 600, 400) < Expansion.Cost(none, 500, -400, 1200, 400), "a bigger room costs more");
        var owned = east; owned.Owner = 2;
        var ownedWorld = Expansion.Build(new List<ExpansionRoom> { owned });
        Check(ownedWorld.Areas[1].Owner == 2 && ownedWorld.Areas[0].Owner == 0, "a built room remembers who drew it (the hub nobody)");
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
        Run(s, dt, 3f, () => g.ExpandProposals.Count == 1 && cg.ExpandProposals.Count == 1);
        Check(g.ExpandProposals.Count == 1 && g.ExpandProposals[0].Owner == 2 && g.ExpandProposals[0].Price == Expansion.Cost(g.Expanded, b0[0], b0[1], b0[2], b0[3]) && cg.ExpandProposals[0].Equals(g.ExpandProposals[0]) && !g.ExpandVotes.ContainsKey(2) && g.VotesFor(2) == 1 && cg.VotesFor(2) == 1 && g.ExpandOpen,
            "a good drawing becomes its drawer's proposal, costs its wall length, backs itself without casting a vote, the same on both screens, and the round stays open");
        cg.ProposeExpandRoom(b1[0], b1[1], b1[2], b1[3]);  // a newer drawing replaces it
        Run(s, dt, 3f, () => g.ExpandProposals.Count == 1 && g.ExpandProposals[0].X == b1[0] && cg.ExpandProposals.Count == 1 && cg.ExpandProposals[0].X == b1[0]);
        Check(g.ExpandProposals.Count == 1 && g.ExpandProposals[0].X == b1[0] && g.ExpandProposals[0].Y == b1[1], "a newer drawing replaces the drawer's last one");

        // The vote: the host backs the client's room; everyone has voted, so it is built and paid for by its drawer.
        int cost = g.ExpandProposals[0].Price, clientBefore = g.Players[2].Vectors, hostBefore = g.Players[1].Vectors;
        g.CastExpandVote(2);
        Run(s, dt, 0.5f);
        Check(g.ExpandOpen, "one cast vote of two doesn't settle the round");
        cg.CastExpandVote(2);
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
        g.Wave = 10; g.BossSlot = true;
        pickRooms.Invoke(g, null);
        Check(g.BossNow && !g.RoomsUp, "a boss wave is fought in the hub alone");
        g.Wave = w0; g.BossSlot = (w0) % 5 == 0 && (w0) > 0;
        pickRooms.Invoke(g, null);

        // The vote decides, not who drew last: the client draws first, the host draws second, both vote for the client's room.
        foreach (var p in g.Players.Values) p.Vectors = 35;
        openRound.Invoke(g, null);
        Run(s, dt, 3f, () => g.ExpandOpen && cg.ExpandOpen);
        var lb = FindBoxes(g.Expanded, 2);
        cg.ProposeExpandRoom(lb[0][0], lb[0][1], lb[0][2], lb[0][3]);
        Run(s, dt, 3f, () => g.ExpandProposals.Count == 1 && cg.ExpandProposals.Count == 1);
        g.ProposeExpandRoom(lb[1][0], lb[1][1], lb[1][2], lb[1][3]);  // the host draws last
        Run(s, dt, 3f, () => g.ExpandProposals.Count == 2 && cg.ExpandProposals.Count == 2);
        Check(g.ExpandOpen && cg.ExpandOpen && g.ExpandProposals.Count == 2, "the second drawing doesn't end the round: both proposals stand");
        int lastBuilt = g.Expanded.Count;
        g.CastExpandVote(2); cg.CastExpandVote(2);
        Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen && cg.Expanded.Count == g.Expanded.Count);
        Check(g.Expanded.Count == lastBuilt + 1 && g.Expanded[g.Expanded.Count - 1].Owner == 2 && g.Expanded[g.Expanded.Count - 1].X == lb[0][0], "both vote for the first drawing: it wins, not the last one drawn");

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
            Run(s, dt, 3f, () => g.ExpandProposals.Count == 2 && cg.ExpandProposals.Count == 2);
            g.CastExpandVote(1); cg.CastExpandVote(2);  // each backs their own: a 50/50 tie
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

        // Floating rooms: a new boss cycle places its shop, vendor and Gatekeeper, sealed, the same on the client, adding no crowd and no spawns.
        var placeFloating = typeof(Game).GetMethod("PlaceFloatingRooms", flags);
        int boughtNow = Expansion.BoughtCount(g.Expanded), allNow = g.Expanded.Count;
        float scale0 = g.ExpansionScale;
        int waveHere = g.Wave;
        g.Wave = 11;
        placeFloating.Invoke(g, null);
        Run(s, dt, 3f, () => cg.Expanded.Count == g.Expanded.Count);
        var added = g.Expanded.Skip(allNow).ToList();
        Check(added.Count == 3 && added.All(r => r.Floating) && added.Any(r => r.Kind == RoomKind.Shop) && added.Any(r => r.Kind == RoomKind.Vendor) && added.Any(r => r.Kind == RoomKind.Gatekeeper) && cg.Expanded.Skip(allNow).All(r => r.Floating) && Expansion.BoughtCount(g.Expanded) == boughtNow,
            "a new boss cycle places a floating shop, vendor and Gatekeeper, the same on the client, and they aren't counted as bought");
        placeFloating.Invoke(g, null);
        Check(g.Expanded.Count == allNow + 3, "the same cycle places them only once");
        pickRooms.Invoke(g, null);
        var floatWorld = g.ExpansionWorld;
        Check(floatWorld.Areas.Skip(allNow + 1).All(a => a.Floating && a.Sealed) && Math.Abs(g.ExpansionScale - scale0) < 0.001f && !g.HasShopRoom, "they stay sealed, add no crowd and a sealed shop isn't a shop room yet");
        int inSealed = 0;
        for (int i = 0; i < 400; i++)
        {
            V2 at = (V2)randomSpot.Invoke(g, new object[] { 20f });
            if (floatWorld.Areas[floatWorld.AreaAt(at)].Floating)
                inSealed++;
        }
        Check(inSealed == 0, "no enemy comes in to a floating room");
        g.Wave = waveHere;

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
        add(500, -1200, 600, 600, RoomKind.Vendor, PathId.None);             // 6: north of the north room
        foreach (var gg in new[] { g, cg })
        {
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
            gg.Expanded.AddRange(built);
        }
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.3f);
        Check(g.RoomsOn && g.CurrentWorld.Areas.Length == 7 && g.HasShopRoom && g.HasVendorRoom, "the map with a shop room and four floors is up");
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
        // The room shops: upgrades and restocks only in the shop room, heals, revives and removals only at the vendor.
        Check(g.ServiceRooms && !g.ShopBlocksWave, "with a shop and a vendor room the shop never holds the countdown");
        V2 vendorAt = g.CurrentWorld.Areas[6].Mid;
        var pm = g.Players[1];
        pm.Gold = 1000; pm.Hp = pm.MaxHp * 0.5f; pm.Pos = pm.Disp = hubAt;
        int slot = Enumerable.Range(0, Game.ShelfSize).First(i => pm.Shelf[i] >= 0);
        g.Shop(ShopAction.Buy, slot);
        g.Shop(ShopAction.Heal);
        Check(pm.Gold == 1000 && pm.Hp < pm.MaxHp * 0.6f, "in the hub you can neither buy nor heal");
        pm.Pos = pm.Disp = vendorAt;
        g.Shop(ShopAction.Buy, slot);
        Check(pm.Gold == 1000, "the vendor sells no upgrades");
        g.Shop(ShopAction.Heal);
        Check(pm.Gold == 1000 - Game.HealPrice && pm.Hp >= pm.MaxHp, "the vendor heals");
        pm.Pos = pm.Disp = shopAt;
        int gold1 = pm.Gold;
        g.Shop(ShopAction.Buy, slot);
        Check(pm.Gold < gold1, "the shop room sells upgrades");
        g.Shop(ShopAction.Done, 1);
        client.Game.Shop(ShopAction.Done, 1);
        Run(s, dt, 0.5f);
        Check(!g.ShopOpen && !cg.ShopOpen, "everyone clicking Done closes the shop");
        Check(pm.Gold == gold1 - (gold1 - pm.Gold) && pm.Gold > 0, "closing the shop keeps your gold (" + pm.Gold + ")");

        // The cleared wave opened a building round; a lone proposal is built as the next wave starts (the votes cast count).
        Check(g.ExpandOpen && cg.ExpandOpen && g.ExpandProposals.Count == 0 && !g.ShopOpen, "the cleared wave opened a building round, on both screens");
        int rooms0 = Expansion.BoughtCount(g.Expanded);
        var fb = FindBoxes(g.Expanded, 8).First(bx => Expansion.Cost(g.Expanded, bx[0], bx[1], bx[2], bx[3]) <= g.Players[1].Vectors);
        g.ProposeExpandRoom(fb[0], fb[1], fb[2], fb[3]);
        Run(s, dt, 0.4f);
        Check(g.ExpandOpen && Expansion.BoughtCount(g.Expanded) == rooms0, "the round doesn't wait for the wave to start: the countdown ticks on");
        typeof(Game).GetField("StateTimer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).SetValue(g, 0.05f);
        Run(s, dt, 30f, () => g.State == MatchState.Fighting, NoPicks);
        Run(s, dt, 0.6f);
        Check(!g.ExpandOpen && !cg.ExpandOpen && Expansion.BoughtCount(g.Expanded) == rooms0 + 1 && cg.Expanded.Count == g.Expanded.Count, "the round closes as the wave starts: the proposal's room is built, on both screens");
    }
}

static partial class Program
{
    // The Gatekeeper room (GatekeeperRoom.cs, Docs/FloatingRooms.md step 3): walking in wakes the Gatekeeper and locks the room, the wave ends without
    // an unbeaten duel (it goes, you're let out), beating it gives the prize pick and the room becomes an ordinary one.
    static void ExpandingGatekeeperTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var pickRooms = typeof(Game).GetMethod("PickRooms", flags);
        var endDuels = typeof(Game).GetMethod("EndDuels", flags);
        var roomsHold = typeof(Game).GetMethod("RoomsHold", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, NoPicks);
        string why;
        var list = new List<ExpansionRoom>();
        var hall = Expansion.Custom(list, 1600, 100, 800, 600, out why);
        Check(hall.HasValue, "the connecting room fits: " + why);
        list.Add(hall.Value);
        var r = Expansion.Custom(list, 2400, 100, 600, 600, out why);
        Check(r.HasValue, "the test room fits: " + why);
        var room = r.Value; room.Kind = RoomKind.Gatekeeper; room.Floating = true;
        foreach (var gg in new[] { g, cg })
        {
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
            gg.Expanded.Add(hall.Value);
            gg.Expanded.Add(room);
        }
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(g.RoomsOn && g.CurrentWorld.Areas.Length == 3 && g.CurrentWorld.Areas[2].Kind == RoomKind.Gatekeeper && !g.CurrentWorld.Areas[2].Sealed, "a Gatekeeper room touching the hub is open (rooms " + g.RoomsOn + ", areas " + g.CurrentWorld.Areas.Length + ", kind " + g.CurrentWorld.Areas[g.CurrentWorld.Areas.Length - 1].Kind + ")");
        // A spawn point in a hole of the world's box (outside every room) is moved into the nearest room.
        var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var stray = (Enemy)spawnAt.Invoke(g, new object[] { EnemyType.Grunt, new V2(2500f, 30f), 0f, 0, false });
        Check(g.CurrentWorld.Areas.Any(a => !a.Sealed && a.Contains(stray.Pos)) && stray.Pos.Y > 100f, "an enemy placed outside every room is moved into the nearest one (" + stray.Pos.X + "," + stray.Pos.Y + ")");
        stray.Hp = 0f;
        Func<Game, int> keepers = gm => gm.Enemies.Values.Count(e => e.Type == EnemyType.Gatekeeper && e.Hp > 0f);
        Func<Game, int> area1 = gm => gm.CurrentWorld.AreaAt(gm.Players[1].Pos);
        var me = g.Players[1];
        me.Pos = me.Disp = new V2(800f, 450f);
        Run(s, dt, 0.5f, null, NoPicks);
        Check(keepers(g) == 0 && !g.AreaLocked(2), "nobody inside: the Gatekeeper sleeps and the room is open");
        me.Pos = me.Disp = g.CurrentWorld.Areas[2].Mid + new V2(0f, 100f);
        Run(s, dt, 0.8f, null, NoPicks);
        Check(keepers(g) == 1 && keepers(cg) == 1, "walking in wakes the Gatekeeper, for both screens");
        Check(g.AreaLocked(2) && cg.AreaLocked(2), "the room locks while the duel is on, for both screens");
        var inside = g.CurrentWorld.Areas[2].Mid;
        V2 held = (V2)roomsHold.Invoke(g, new object[] { inside, new V2(1800f, 450f), 20f });
        Check(g.CurrentWorld.Areas[2].Contains(held), "a locked room holds you in (a step out of it is clamped)");

        // An unbeaten duel ends with the wave: the Gatekeeper goes and you are let out.
        endDuels.Invoke(g, null);
        Run(s, dt, 0.4f, null, NoPicks);
        Check(keepers(g) == 0 && !g.CurrentWorld.Areas[2].Contains(me.Pos) && !g.AreaLocked(2), "the wave ending takes an unbeaten Gatekeeper away and lets you out");

        // Beating it: prize pick, ordinary room.
        me.Pos = me.Disp = g.CurrentWorld.Areas[2].Mid + new V2(0f, 100f);
        Run(s, dt, 0.8f, null, NoPicks);
        Check(keepers(g) == 1, "walking in again wakes it again");
        int picks0 = g.Players.Values.Sum(p => p.PrizePicks);
        foreach (var e in g.Enemies.Values.Where(e => e.Type == EnemyType.Gatekeeper)) e.Hp = 0f;
        Run(s, dt, 0.8f, null, NoPicks);
        Check(g.Players.Values.Sum(p => p.PrizePicks) == picks0 + g.Players.Count, "beating it gives everyone a prize pick");
        Check(g.CurrentWorld.Areas[2].Kind == RoomKind.Combat && cg.CurrentWorld.Areas[2].Kind == RoomKind.Combat && !g.AreaLocked(2), "the room is an ordinary combat room now, on both screens");
    }

    // Themed rooms (Expansion.cs, Docs/FloatingRooms.md step 4): a path as a room's theme costs extra vectors and unlocks with bosses.
    static void ExpandingThemesTest(float dt)
    {
        Check(Expansion.ThemeCost(PathId.None) == 0 && Expansion.ThemeCost(PathId.Sanctuary) == 3 && Expansion.ThemeCost(PathId.FrozenLake) == 6 && Expansion.ThemeCost(PathId.BloodMoon) == 10, "a theme costs 3, 6 or 10 vectors by its path's danger");
        Check(Expansion.ThemeUnlocked(PathId.Sanctuary, 1) && !Expansion.ThemeUnlocked(PathId.FrozenLake, 5) && Expansion.ThemeUnlocked(PathId.FrozenLake, 6) && !Expansion.ThemeUnlocked(PathId.BloodMoon, 10) && Expansion.ThemeUnlocked(PathId.BloodMoon, 11), "danger 1 themes are free, danger 2 unlock after the first boss, danger 3 after the second");
        var built = new List<ExpansionRoom>();
        string why;
        var boxes = FindBoxes(built, 1);
        var b = boxes[0];
        var plain = Expansion.Custom(built, b[0], b[1], b[2], b[3], out why).Value;
        var ice = Expansion.Custom(built, b[0], b[1], b[2], b[3], PathId.FrozenLake, out why).Value;
        Check(ice.Kind == RoomKind.Floor && ice.Floor == PathId.FrozenLake && ice.Price == plain.Price + 6, "a themed room is a Floor room of that path and costs the theme on top");
        Check(!Expansion.Custom(built, b[0], b[1], b[2], b[3], PathId.Fogbound, out why).HasValue, "only the floor paths are themes");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.ExpandOpen = true; }
        foreach (var p in g.Players.Values) p.Vectors = 60;
        cg.ProposeExpandRoom(b[0], b[1], b[2], b[3], PathId.BloodMoon);
        Run(s, dt, 0.4f);
        Check(g.ExpandProposals.Count == 0, "a theme still locked is refused (Blood Moon on wave 6)");
        cg.ProposeExpandRoom(b[0], b[1], b[2], b[3], PathId.FrozenLake);
        Run(s, dt, 0.5f, () => cg.ExpandProposals.Count == 1);
        var prop = g.ExpandProposals.Count == 1 ? g.ExpandProposals[0] : default(ExpansionRoom);
        Check(g.ExpandProposals.Count == 1 && prop.Kind == RoomKind.Floor && prop.Floor == PathId.FrozenLake && prop.Price == plain.Price + 6 && prop.Owner == 2, "an unlocked theme goes to the vote as a Floor room, priced with the theme");
        Check(cg.ExpandProposals.Count == 1 && cg.ExpandProposals[0].Floor == PathId.FrozenLake, "and both screens see its theme");
    }

    // Opened walls (Expansion.cs CutRefund/Build, ExpansionGame.cs): a stretch of interior wall opens for vectors back, by vote.
    static void ExpandingWallsTest(float dt)
    {
        string why;
        var list = new List<ExpansionRoom>();
        var east = Expansion.Custom(list, 1600, 100, 800, 600, out why).Value;
        list.Add(east);
        var none = new List<WallCut>();
        // The shared wall (x = 1600, y 100..700) has one 160 px door in the middle (2 of its 6 tiles).
        var all = new WallCut { Vertical = true, At = 1600, From = 100, Till = 700 };
        Check(Expansion.CutRefund(list, none, all, out why) == 4, "opening the whole shared wall gives back its closed tiles: 6 minus the 2 the door covers");
        Check(Expansion.CutRefund(list, none, new WallCut { Vertical = true, At = 1600, From = 100, Till = 300 }, out why) == 2, "two closed tiles give back 2");
        Check(Expansion.CutRefund(list, none, new WallCut { Vertical = true, At = 0, From = 100, Till = 400 }, out why) == 0 && why != null, "the outer wall can't be opened");
        Check(Expansion.CutRefund(list, none, new WallCut { Vertical = true, At = 1600, From = 100, Till = 200 }, out why) == 0, "one tile is too short");
        Check(Expansion.CutRefund(list, none, new WallCut { Vertical = true, At = 1600, From = 150, Till = 450 }, out why) == 0, "an opening must sit on the grid");
        var w0 = Expansion.Build(list, none);
        var cuts = new List<WallCut> { all };
        var w1 = Expansion.Build(list, cuts);
        Check(w1.Doors.Length == 1 && w1.Doors[0].Width == 600f && Expansion.CutRefund(list, cuts, all, out why) == 0, "the opening replaces the door and a second try finds nothing left to open");
        Func<World, int> wallsAt = w => w.WallsFor(false).Count(sl => Math.Abs(sl.Pos.X - 1600f) < 1f && sl.Pos.Y + sl.HH > 160f && sl.Pos.Y - sl.HH < 640f);
        Check(wallsAt(w0) > 0 && wallsAt(w1) == 0, "the wall is gone along the whole opening");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Add(east); gg.ExpandOpen = true; }
        g.Players[2].Vectors = 10;
        cg.ProposeExpandCut(all);
        Run(s, dt, 0.5f, () => cg.ExpandProposals.Count == 1);
        Check(g.ExpandProposals.Count == 1 && g.ExpandProposals[0].Kind == RoomKind.Opening && g.ExpandProposals[0].Price == -4 && cg.ExpandProposals.Count == 1, "a friend's wall opening goes to the vote, priced at minus its refund, on both screens");
        cg.ProposeExpandCut(new WallCut { Vertical = true, At = 0, From = 100, Till = 400 });
        Run(s, dt, 0.3f);
        Check(g.ExpandProposals.Count == 1 && g.ExpandProposals[0].Owner == 2 && g.ExpandProposals[0].H == 600, "an opening that can't be made is refused (their proposal stays)");
        g.CastExpandVote(2);
        typeof(Game).GetMethod("ResolveBuildRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(g, null);
        Run(s, dt, 0.5f, () => cg.Openings.Count == 1);
        Check(g.Openings.Count == 1 && cg.Openings.Count == 1 && g.Players[2].Vectors == 14, "the winning opening is made on both screens and its proposer gets the vectors (" + g.Players[2].Vectors + ")");
        Check(g.ExpansionWorld.Doors.Length == 1 && g.ExpansionWorld.Doors[0].Width == 600f && cg.ExpansionWorld.Doors[0].Width == 600f, "and both see the wide gap");
    }

    // Balance (Docs/FloatingRooms.md step 5): what it costs to connect a floating room, against the vectors a player has.
    static void ExpandingFloatCostTest()
    {
        var costs = new List<int>();
        int noConnector = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            var map = new List<ExpansionRoom>();
            var rng = new Random(seed);
            for (int k = 0; k < rng.Next(0, 5); k++)
            {
                var boxes = FindBoxes(map, 12);
                if (boxes.Count == 0) break;
                var b = boxes[rng.Next(boxes.Count)];
                string why;
                var r = Expansion.Custom(map, b[0], b[1], b[2], b[3], out why);
                if (r.HasValue) map.Add(r.Value);
            }
            foreach (var kind in new[] { RoomKind.Shop, RoomKind.Vendor, RoomKind.Gatekeeper })
            {
                var f = Expansion.FloatingFor(seed, seed % 4, map, kind);
                if (!f.HasValue) continue;
                map.Add(f.Value);
                var c = Expansion.ConnectorFor(map, map.Count - 1);
                if (!c.HasValue) noConnector++;
                else costs.Add(c.Value.Price);
                map.RemoveAt(map.Count - 1);
            }
        }
        costs.Sort();
        Console.WriteLine("  floating rooms: connector cost min " + costs[0] + ", median " + costs[costs.Count / 2] + ", max " + costs[costs.Count - 1] + " vectors over " + costs.Count + " rooms (" + noConnector + " with none)");
        Check(noConnector == 0, "every floating room can be reached by some box");
        Check(costs[costs.Count / 2] >= 10 && costs[costs.Count - 1] <= 22, "reaching a floating room costs about a room's worth (10 to 22 vectors, median " + costs[costs.Count / 2] + ")");
    }

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
        g.Wave = 6; g.BossSlot = false;
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
        g.Wave = 10; g.BossSlot = true;
        grantWave.Invoke(g, null);
        Check(g.Players.Values.All(p => p.Vectors == p.VectorCap + Game.VectorBossBonus), "a cleared boss wave refills everyone to the cap and adds " + Game.VectorBossBonus + " on top");
        grantWave.Invoke(g, null);
        Check(p1.Vectors == p1.VectorCap + Game.VectorBossBonus, "and doesn't stack with itself");
        g.Wave = 6; g.BossSlot = false;
        grantWave.Invoke(g, null);
        Check(p1.Vectors == p1.VectorCap + Game.VectorBossBonus, "ordinary gains leave a stock above the cap alone");
        Run(s, dt, 0.5f);
        Check(cg.Players[1].Vectors == p1.Vectors && cg.Players[2].Vectors == g.Players[2].Vectors && cg.Players[1].VectorCap == p1.VectorCap, "the client sees everyone's vectors");
        // The cap cards (Core/Upgrades.cs): offered only in the Expanding arena, they raise the cap; Architect also gives vectors now.
        int inkwell = Array.FindIndex(Upgrades.All, u => u.Name == "Deep Inkwell"), architect = Array.FindIndex(Upgrades.All, u => u.Name == "Architect");
        Check(inkwell >= 0 && architect > inkwell && architect == Upgrades.All.Length - 1 && Upgrades.All[inkwell].NeedsExpanding && Upgrades.All[architect].NeedsExpanding, "the two vector cards are the last upgrades, so every older one keeps its number");
        Check(!Upgrades.Available(inkwell, g.Players[1].Role, g.Players[1].RoleAbility, new List<int>()) && Upgrades.Available(inkwell, g.Players[1].Role, g.Players[1].RoleAbility, new List<int>(), null, true), "a vector card is only available in the Expanding arena");
        var rolls = new Random(5);
        int offeredPlain = 0, offeredExpanding = 0;
        for (int i = 0; i < 400; i++)
        {
            foreach (int u in Upgrades.RollOffer(rolls, g.Players[1].Role, g.Players[1].RoleAbility, new List<int>(), 1 + i % 20))
                offeredPlain += Upgrades.All[u].NeedsExpanding ? 1 : 0;
            foreach (int u in Upgrades.RollOffer(rolls, g.Players[1].Role, g.Players[1].RoleAbility, new List<int>(), 1 + i % 20, null, true))
                offeredExpanding += Upgrades.All[u].NeedsExpanding ? 1 : 0;
        }
        Check(offeredPlain == 0 && offeredExpanding > 0, "level-up offers hold a vector card only in the Expanding arena (" + offeredExpanding + " of 1200 cards)");
        p1.Vectors = 5;
        g.GrantUpgrade(1, inkwell);
        Check(p1.VectorCap == Game.VectorCapBase + 10 && p1.Vectors == 5, "Deep Inkwell raises the cap by 10 and gives nothing now");
        g.GrantUpgrade(1, architect);
        Check(p1.VectorCap == Game.VectorCapBase + 30 && p1.Vectors == 15, "Architect raises it by 20 more and gives 10 vectors now (" + p1.VectorCap + ", " + p1.Vectors + ")");
        Run(s, dt, 0.6f);
        Check(cg.Players[1].VectorCap == p1.VectorCap && cg.Players[1].Vectors == p1.Vectors, "the client sees the new cap");
        g.Wave = 10; g.BossSlot = true;
        grantWave.Invoke(g, null);
        Check(p1.Vectors == p1.VectorCap + Game.VectorBossBonus, "a boss refill fills the raised cap (" + p1.Vectors + " of " + p1.VectorCap + " + " + Game.VectorBossBonus + ")");
        g.Wave = 6; g.BossSlot = false;
        g.Diff.Set(DiffKnob.ArenaExpanding, 0f);
        p1.Vectors = 5;
        grantWave.Invoke(g, null);
        Check(p1.Vectors == 5, "outside the Expanding arena vectors don't change");
    }

    static void ExpandingKitsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var grantWave = typeof(Game).GetMethod("GrantVectorsForWave", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.Expanded.Add(new ExpansionRoom { Parent = 0, X = 1600, Y = 100, W = 800, H = 600, Kind = RoomKind.Combat });
        g.Expanded.Add(new ExpansionRoom { Parent = 0, X = -800, Y = 100, W = 800, H = 600, Kind = RoomKind.Combat });
        string why;
        var shop = new RoomToken { Kind = RoomKind.Shop };
        var fl = Expansion.FloorToken(5, 10, g.Expanded, g.RoomTokens);
        Check(fl.Kind == RoomKind.Floor && Array.IndexOf(Expansion.Floors, fl.Floor) >= 0, "a Floor kit names a floor a room can carry");
        Check(Expansion.FloorToken(5, 10, g.Expanded, g.RoomTokens).Floor == fl.Floor, "and the same seed and wave give the same floor");
        Check(Expansion.CanRetype(g.Expanded, 0, shop, out why) && !Expansion.CanRetype(g.Expanded, 5, shop, out why), "only a bought room can take a kit");
        var done = Expansion.Retype(g.Expanded[0], fl);
        g.Expanded[0] = done;
        Check(!Expansion.CanRetype(g.Expanded, 0, shop, out why), "one modifier per room: a changed room refuses another kit");
        g.Expanded[0] = new ExpansionRoom { Parent = 0, X = 1600, Y = 100, W = 800, H = 600, Kind = RoomKind.Combat };
        // A boss clear gives a Floor kit; ordinary clears give none.
        g.Wave = 6; g.BossSlot = false;
        grantWave.Invoke(g, null);
        Check(g.RoomTokens.Count == 0, "an ordinary cleared wave gives no kit");
        g.Wave = 10; g.BossSlot = true;
        grantWave.Invoke(g, null);
        Check(g.RoomTokens.Count == 1 && g.RoomTokens[0].Kind == RoomKind.Floor, "a cleared boss wave gives a Floor kit");
        Run(s, dt, 0.5f);
        Check(cg.RoomTokens.Count == 1 && cg.RoomTokens[0].Floor == g.RoomTokens[0].Floor, "the client sees the kit");
        // Spending: only in a building round, host and client alike.
        g.ApplyRoomToken(0, 0);
        Check(g.Expanded[0].Kind == RoomKind.Combat && g.RoomTokens.Count == 1, "a kit can't be spent outside a building round");
        g.ExpandOpen = true; cg.ExpandOpen = true;
        var floor = g.RoomTokens[0].Floor;
        cg.ApplyRoomToken(0, 1);
        Run(s, dt, 0.6f);
        Check(g.Expanded[1].Kind == RoomKind.Floor && g.Expanded[1].Floor == floor && g.RoomTokens.Count == 0, "a client spends a kit on room 2: it becomes that floor and the kit is gone");
        Check(cg.Expanded[1].Kind == RoomKind.Floor && cg.Expanded[1].Floor == floor && cg.RoomTokens.Count == 0, "the client's map matches");
        Check(g.ExpansionWorld.Areas[1].Kind == RoomKind.Combat && g.ExpansionWorld.Areas[2].Kind == RoomKind.Floor && g.ExpansionWorld.Areas[2].Floor == floor, "the built map reads the new kind (Floor rules then hold in that room, FloorAt)");
        g.RoomTokens.Add(shop);
        g.ApplyRoomToken(0, 1);
        Check(g.Expanded[1].Kind == RoomKind.Floor && g.RoomTokens.Count == 1, "a changed room refuses a second kit");
        g.ApplyRoomToken(0, 0);
        Check(g.Expanded[0].Kind == RoomKind.Shop && g.RoomTokens.Count == 0, "a Shop kit makes a shop room");
        g.ExpandOpen = false; cg.ExpandOpen = false;
    }
}
