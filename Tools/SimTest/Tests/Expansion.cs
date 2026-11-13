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

        int coverSeen = 0, hallCover = 0, badCover = 0, loopDoors = 0, badWallGap = 0, badFit = 0, badTree = 0, badDoor = 0, badWall = 0, badSteer = 0, badCap = 0, worlds = 0, offersSeen = 0;
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
                    if (overlap || (run >= Expansion.MinDoorRun && !doored) || (run < Expansion.MinDoorRun && doored)) badFit++;
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
            // obstacles: inside their room clear of its walls (a narrow strip's to one side, leaving a lane), off every door, the same every time
            var again = Expansion.Build(built);
            coverSeen += w.Cover.Length;
            if (again.Cover.Length != w.Cover.Length || w.Cover.Where((c, i) => c.Pos.DistanceTo(again.Cover[i].Pos) > 0.01f).Any()) badCover++;
            foreach (var c in w.Cover)
            {
                var host = w.Areas.Skip(1).Where(ar2 => ar2.Contains(c.Pos, -(Strip(ar2) ? Expansion.StripCoverWall + Expansion.StripCoverR : Expansion.CoverWall) + 5f)).ToList();
                if (host.Count != 1) badCover++;
                else if (Strip(host[0])) { hallCover++; float r = c.Round ? c.R : c.HW; if (Math.Min(host[0].Size.X, host[0].Size.Y) - Expansion.StripCoverWall - 2f * r < 150f) badCover++; }  // a 150 px lane past it
                if (w.Doors.Any(d => d.Pos.DistanceTo(c.Pos) < 200f)) badCover++;
            }
            // every room's centre is in the open and in its own area
            for (int i = 0; i < w.Areas.Length; i++)
                if (plan.Solids.Any(sd => Layouts.Inside(sd, w.Areas[i].Mid, 0f)) || w.AreaAt(w.Areas[i].Mid) != i) badWall++;
        }
        Check(offersSeen > 600, "offers keep coming until the cap (" + offersSeen + " seen over " + worlds + " maps)");
        Check(badCover == 0 && coverSeen > 60 && hallCover > 10, "built rooms hold obstacles and bars of wall: inside their room clear of its walls, narrow strips' to one side with a lane past them, off every door, the same on every build (" + coverSeen + " over " + worlds + " maps, " + hallCover + " in strips, " + badCover + " bad)");
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

        // Drawn rooms (Expansion.Custom): a narrow room off the hub is fine; thin, huge, off the grid, overlapping and floating boxes are refused.
        string why;
        var hall = Expansion.Custom(none, 500, -600, 300, 600, out why);
        Check(hall.HasValue && hall.Value.Parent == 0 && hall.Value.Shape == "Room" && hall.Value.Kind == RoomKind.Combat && hall.Value.Price > 0, "a 3-wide room off the hub is accepted (" + (hall.HasValue ? hall.Value.Price + " gold" : why) + ")");
        Check(Expansion.Custom(none, 500, -600, 100, 600, out why) == null && why.Contains("thin"), "too thin is refused: " + why);
        Check(Expansion.Custom(none, 500, -1300, 1300, 1300, out why) == null && why.Contains("big"), "too big is refused: " + why);
        Check(Expansion.Custom(none, 550, -600, 300, 600, out why) == null && why.Contains("grid"), "off the grid is refused: " + why);
        Check(Expansion.Custom(none, 100, 100, 300, 300, out why) == null && why.Contains("Overlaps"), "overlapping the hub is refused: " + why);
        Check(Expansion.Custom(none, 3000, 3000, 300, 300, out why) == null && why.Contains("share"), "floating off by itself is refused: " + why);
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
        Check(link.HasValue && link.Value.Parent == 0 && Expansion.LoopsOf(withFloater, link.Value) == 1, "a strip from the hub to the floating room is accepted and opens it (" + (link.HasValue ? "ok" : why) + ")");
        var opened = new List<ExpansionRoom> { floater, link.Value };
        var openedWorld = Expansion.Build(opened);
        Check(Expansion.SealedCount(withFloater) == 1 && Expansion.SealedCount(opened) == 0 && openedWorld.Areas[1].Floating && !openedWorld.Areas[1].Sealed && openedWorld.Doors.Any(d => d.Joins(1)) && Expansion.BoughtCount(opened) == 1,
            "built rooms that touch it give it a door: it is no longer sealed, and only the strip counts as bought");
        // Vector cost (Docs/VectorBuilding.md): the new wall length, so a shared wall is free and a loop is cheap.
        Check(Expansion.Cost(none, 500, -400, 600, 400) == 14, "a 6 x 4 closet along the hub's wall costs its perimeter 20 less the 6 it shares: 14 (" + Expansion.Cost(none, 500, -400, 600, 400) + ")");
        Check(Expansion.Cost(none, 500, -1200, 300, 1200) == 27, "a 3 x 12 room standing off the hub pays its new wall like any room (30 less the 3 it shares): 27 (" + Expansion.Cost(none, 500, -1200, 300, 1200) + ")");
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
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); cg.Diff.Set(DiffKnob.BuildVote, 1f);
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

        // The vote: the host backs the client's room; with the drawer backing their own that's a majority, so it is built at once and paid for by its drawer.
        int cost = g.ExpandProposals[0].Price, clientBefore = g.Players[2].Vectors, hostBefore = g.Players[1].Vectors;
        g.CastExpandVote(2);
        Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen && cg.Expanded.Count == 1 && cg.Players[2].Vectors == g.Players[2].Vectors);
        Check(g.Expanded.Count == 1 && g.Expanded[0].X == b1[0] && g.Expanded[0].Owner == 2 && g.Players[2].Vectors == clientBefore - cost && g.Players[1].Vectors == hostBefore && cg.Players[2].Vectors == g.Players[2].Vectors && cg.Expanded.Count == 1 && cg.Expanded[0].Equals(g.Expanded[0]),
            "a deciding vote settles the round at once: the room is built, its drawer pays " + cost + " vectors, nobody else does, both screens agree");

        // The room comes up on the next regular wave, with its walls and a bigger crowd; boss waves stay in the middle.
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.4f);
        Check(g.RoomsOn && cg.RoomsOn && g.CurrentWorld.Areas.Length == 2 && g.ArenaMax.DistanceTo(g.CurrentWorld.Max) < 0.01f && cg.CurrentWorld.Areas.Length == 2 && g.Walled,
            "the built room is up on a regular wave: two areas, walled, the same on the client");
        Check(g.LayoutOn && g.ActiveLayout.Name.Contains("Expanding") && g.ActiveLayout.Solids.Length >= g.CurrentWorld.WallsFor(false).Length, "its walls are part of the floor plan (" + g.ActiveLayout.Name + ")");
        Check(g.ExpansionScale > 1f && g.ExpansionScale <= Game.MaxExpandScale, "a bigger map means a bigger crowd (x" + g.ExpansionScale.ToString("0.00") + ")");
        bool inRooms = true, inNew = false;
        // (A spot is the nearest a player of four valid ones, so it follows the players: sample once with everyone in the hub, once with a player by the new room's door.)
        var savedAt = g.Players.ToDictionary(kv => kv.Key, kv => kv.Value.Pos);
        for (int round = 0; round < 2; round++)
        {
            if (round == 1)
                { V2 door = g.CurrentWorld.Doors[0].Pos, hubMid = g.CurrentWorld.Areas[0].Mid; V2 by = door + (hubMid - door).Normalized() * 60f; foreach (var pl in g.Players.Values) pl.Pos = pl.Disp = by; }  // (in the hub by the new room's door)
            for (int i = 0; i < 300; i++)
            {
                V2 at = (V2)randomSpot.Invoke(g, new object[] { 20f });
                var a = g.CurrentWorld.Areas[g.CurrentWorld.AreaAt(at)];
                inRooms &= a.Contains(at);
                inNew |= !a.Hub;
            }
        }
        foreach (var kv in savedAt) g.Players[kv.Key].Pos = g.Players[kv.Key].Disp = kv.Value;
        Check(inRooms && inNew, "enemies come in inside a room, never in the gaps between them, and the new room gets some (" + inRooms + "/" + inNew + ")");
        int w0 = g.Wave;

        // Before the boss, anyone out in a built room is beamed into the hub (BossBeam.cs), not snapped there.
        {
            Quiet(); g.Enemies.Clear(); NoPicks();
            var room = g.CurrentWorld.Areas[1];
            var hubA = g.CurrentWorld.Areas[0];
            g.Players[2].Pos = g.Players[2].Disp = cg.Players[2].Pos = cg.Players[2].Disp = room.Mid;
            int leg0 = g.Leg; g.Wave = 5; g.BossSlot = false; g.Leg = 1;
            g.State = MatchState.Intermission; g.StateTimer = 30f;
            Run(s, dt, 0.3f, null, () => { NoPicks(); cg.Players[2].Pos = cg.Players[2].Disp = room.Mid; });
            g.StateTimer = 0f;
            Run(s, dt, 0.4f, null, NoPicks);
            Check(g.State == MatchState.Intermission && g.Players[2].WarpT > 0f && g.Effects.Any(e => e.Kind == FxKind.BossBeam) && cg.Effects.Any(e => e.Kind == FxKind.BossBeam) && g.Players[2].Pos.DistanceTo(room.Mid) < 8f,
                "Expanding: the boss wave waits while the player out in a room is beamed (state " + g.State + ", warp " + g.Players[2].WarpT + ", rooms " + g.RoomsOn + ", next " + g.BossNext + ")");
            Run(s, dt, 2f, () => g.State == MatchState.Fighting, NoPicks);
            Check(g.State == MatchState.Fighting && g.BossNow && hubA.Contains(g.Players[2].Pos, -40f) && hubA.Contains(cg.Players[2].Pos, -40f), "Expanding: then the boss wave starts with them in the hub");
            Quiet(); g.Enemies.Clear(); g.Leg = leg0;
        }
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

        // A new boss cycle places sealed Gatekeeper, shop and vendor rooms (shop and vendor tiny 4 x 4 tile squares) and a chest node (Docs/Nodes.md),
        // the same on the client, adding no crowd and no spawns.
        var placeFloating = typeof(Game).GetMethod("PlaceFloatingRooms", flags);
        int boughtNow = Expansion.BoughtCount(g.Expanded), allNow = g.Expanded.Count, nodesNow = g.Nodes.Count;
        int waveHere = g.Wave;
        g.Wave = 11;
        float scale0 = g.ExpansionScale;
        placeFloating.Invoke(g, null);
        Run(s, dt, 3f, () => cg.Expanded.Count == g.Expanded.Count && cg.Nodes.Count == g.Nodes.Count);
        var added = g.Expanded.Skip(allNow).ToList();
        Check(added.Count == 3 && added.All(r => r.Floating) && added.Any(r => r.Kind == RoomKind.Gatekeeper) && cg.Expanded.Skip(allNow).All(r => r.Floating) && Expansion.BoughtCount(g.Expanded) == boughtNow,
            "a new boss cycle places floating Gatekeeper, shop and vendor rooms, the same on the client, and they aren't counted as bought");
        Check(added.Count(r => r.Kind == RoomKind.Shop && r.W == Expansion.ShopRoomSide && r.H == Expansion.ShopRoomSide) == 1 && added.Count(r => r.Kind == RoomKind.Vendor && r.W == Expansion.ShopRoomSide && r.H == Expansion.ShopRoomSide) == 1,
            "the shop and vendor rooms are tiny 4 x 4 tile squares");
        var newNodes = g.Nodes.Skip(nodesNow).ToList();  // (a node is skipped when the grown map has no room left for it, the big rule node most often)
        int newCount = newNodes.Count;
        Check(newCount >= 1 && newNodes.All(n => n.Kind == NodeKind.Chest || n.Kind == NodeKind.Relic || n.Kind == NodeKind.Rule) && newNodes.Select(n => n.Kind == NodeKind.Chest ? 0 : n.Kind == NodeKind.Relic ? 1 : 2).SequenceEqual(newNodes.Select(n => n.Kind == NodeKind.Chest ? 0 : n.Kind == NodeKind.Relic ? 1 : 2).OrderBy(k => k))
              && newNodes.All(n => n.Kind != NodeKind.Rule || n.Rule < 6) && cg.Nodes.SequenceEqual(g.Nodes),
            "and a chest node, on an even cycle a relic node, and a rule node, as many as fit (no shop or vendor nodes any more), the same on the client");
        placeFloating.Invoke(g, null);
        Check(g.Expanded.Count == allNow + 3 && g.Nodes.Count == nodesNow + newCount, "the same cycle places them only once");
        pickRooms.Invoke(g, null);
        var floatWorld = g.ExpansionWorld;
        Check(floatWorld.Areas.Skip(allNow + 1).All(a => a.Floating && a.Sealed) && Math.Abs(g.ExpansionScale - scale0) < 0.001f && !g.HasShopRoom, "the rooms stay sealed, add no crowd and a sealed shop room isn't a shop yet");
        {
            int si = g.Expanded.FindIndex(r => r.Floating && r.Kind == RoomKind.Shop);
            var con = si >= 0 ? Expansion.ConnectorFor(g.Expanded, si) : null;
            Check(con.HasValue, "a box can be built up to the shop room");
            // A bot votes for the box toward the sealed shop when someone proposed it (a duo used to split its votes at random and build nothing).
            var want = (ExpansionRoom?)typeof(Game).GetMethod("BotFloaterPick", flags).Invoke(g, null);
            Check(want.HasValue && con.HasValue && want.Value.X == con.Value.X && want.Value.Y == con.Value.Y, "a bot's first pick is the box up to the sealed shop room");
            if (want.HasValue)
            {
                var other = want.Value; other.Owner = 1; other.X -= 1000;
                var mine = want.Value; mine.Owner = 2;
                g.ExpandProposals.Add(other); g.ExpandProposals.Add(mine);
                var botVote = typeof(Game).GetMethod("BotVote", flags);
                Check(Enumerable.Range(0, 20).All(_ => (int)botVote.Invoke(g, null) == 2), "a bot votes for the proposal that builds toward the shop");
                g.ExpandProposals.Clear();
            }
            if (con.HasValue)
            {
                var open = new List<ExpansionRoom>(g.Expanded) { con.Value };
                var ow = Expansion.Build(open);
                var sa = ow.Areas[si + 1];
                Check(!sa.Sealed && sa.Kind == RoomKind.Shop && ow.Areas.Any(x => x.Hub) && floatWorld.Areas[si + 1].Sealed,
                    "touching it with a built box opens its door: you walk into a shop room");
            }
        }
        int inSealed = 0;
        for (int i = 0; i < 400; i++)
        {
            V2 at = (V2)randomSpot.Invoke(g, new object[] { 20f });
            if (floatWorld.Areas[floatWorld.AreaAt(at)].Floating)
                inSealed++;
        }
        Check(inSealed == 0, "no enemy comes in to a floating room");
        g.Wave = waveHere;

        // A chest node inside a built room opens when a player walks up to it: gold and vectors for everyone, once.
        {
            ExpansionRoom? extra = null;
            for (int cy = -600; cy <= 1000 && !extra.HasValue; cy += 100)
                for (int cx = -600; cx <= 1700 && !extra.HasValue; cx += 100)  // (anywhere touching the map: the new shop and vendor rooms may sit beside the hub)
                {
                    var cand = Expansion.Custom(g.Expanded, cx, cy, 500, 500, out string whyNot);
                    if (cand.HasValue && !g.Nodes.Any(nd => Expansion.NodeEncloses(cand.Value, nd)))  // (not round a node already out there)
                        extra = cand;
                }
            int ri = -1;
            if (extra.HasValue) { g.Expanded.Add(extra.Value); ri = g.Expanded.Count - 1; }
            Check(ri >= 0, "a 5 x 5 room fits on the map for the chest test");
            if (ri >= 0)
            {
                var rm = g.Expanded[ri];
                var chest = new MapNode { X = (short)(rm.X + rm.W / 2 / 100 * 100 + 50), Y = (short)(rm.Y + rm.H / 2 / 100 * 100 + 50), Kind = NodeKind.Chest };
                g.Nodes.Add(chest);
                int ci = g.Nodes.Count - 1;
                var tickNodes = typeof(Game).GetMethod("TickNodes", flags);
                var pl = g.Players.Values.First();
                var tickProps = typeof(Game).GetMethod("TickProps", flags);
                pl.Pos = chest.Pos + new V2(400f, 0f);
                tickNodes.Invoke(g, null);
                tickNodes.Invoke(g, null);
                var chests = g.Props.Values.Where(q => q.Kind == PropKind.Chest && q.Pos.DistanceTo(chest.Pos) < 1f).ToList();
                Check(chests.Count == 1 && !chests[0].Open && !g.Nodes[ci].Spent, "a claimed chest node holds one real treasure chest, shut");
                Check(!g.PropsHoldWave(), "the wave doesn't wait on a node's chest");
                pl.Pos = chest.Pos;
                tickProps.Invoke(g, new object[] { 0.05f });
                tickNodes.Invoke(g, null);
                tickNodes.Invoke(g, null);
                Check(chests[0].Open && g.Nodes[ci].Spent && g.Loots.Values.Any(l => l.Owner == pl.Id), "walking onto it opens it: the node is spent and the player gets their own item");
                Check(g.Props.Values.Count(q => q.Kind == PropKind.Chest && !q.Open && q.Pos.DistanceTo(chest.Pos) < 1f) == 0, "a spent node holds no second chest");
                g.Nodes.RemoveAt(ci);

                // A relic node (Relics.cs): walking up to it fills every player's relic slot, once.
                var relic = new MapNode { X = chest.X, Y = chest.Y, Kind = NodeKind.Relic };
                g.Nodes.Add(relic);
                int rni = g.Nodes.Count - 1;
                pl.Pos = relic.Pos + new V2(400f, 0f);
                tickNodes.Invoke(g, null);
                Check(!g.Nodes[rni].Spent && g.Players.Values.All(q => q.Relic == RelicId.None), "a relic node waits for someone to walk up");
                pl.Pos = relic.Pos;
                tickNodes.Invoke(g, null);
                Check(g.Nodes[rni].Spent && g.Players.Values.All(q => q.Relic == RelicId.WallBounce),
                    "walking onto it gives everyone Wall Bounce and spends the node");
                g.Nodes.RemoveAt(rni);
                foreach (var q in g.Players.Values) q.Relic = RelicId.None;  // (the rest of the suite runs without it)
                g.Expanded.RemoveAt(ri);
            }
        }

        // Wall Bounce: a player's shot that reaches a wall bounces off it once, keeping 70% of its damage.
        {
            var wallBounce = typeof(Game).GetMethod("WallBounce", flags);
            var counts = typeof(Game).GetMethod("Counts", flags);
            var pl = g.Players.Values.First();
            Solid wall = default(Solid);
            bool found = false;
            foreach (var so in g.ActiveLayout.Solids)
                if (!so.Round && so.HH > so.HW + 20f && (bool)counts.Invoke(g, new object[] { so })) { wall = so; found = true; break; }
            Check(found, "the map has an upright wall for the bounce test");
            if (found)
            {
                Func<Bullet> shot = () => new Bullet { Id = 999999, Kind = Defs.DpsShot, Owner = pl.Id, Radius = 4f, Dmg = 10f, Life = 1f,
                    Pos = new V2(wall.Pos.X + wall.HW + 3f, wall.Pos.Y), Vel = new V2(-600f, 100f) };
                var keep = pl.Relic;
                pl.Relic = RelicId.None;
                Check(!(bool)wallBounce.Invoke(g, new object[] { shot() }), "without the relic a wall stops the shot");
                pl.Relic = RelicId.WallBounce;
                var b = shot();
                bool hop = (bool)wallBounce.Invoke(g, new object[] { b });
                Check(hop && b.Vel.X > 0f && Math.Abs(b.Vel.Y - 100f) < 0.01f && Math.Abs(b.Dmg - 7f) < 0.01f && b.Pos.X > wall.Pos.X + wall.HW + b.Radius,
                    "with Wall Bounce it mirrors off the face, out of the wall, at 70% damage");
                b.Pos = new V2(wall.Pos.X + wall.HW + 3f, wall.Pos.Y);
                Check(!(bool)wallBounce.Invoke(g, new object[] { b }), "it bounces only once");
                var en = new Bullet { Id = 999998, Kind = Defs.SmallShot, Owner = 0, Radius = 4f, Dmg = 10f, Life = 1f, Pos = b.Pos, Vel = new V2(-600f, 0f) };
                Check(!(bool)wallBounce.Invoke(g, new object[] { en }), "enemy shots never bounce");
                pl.Relic = keep;
                ((List<Bullet>)typeof(Game).GetField("bounced", flags).GetValue(g)).Clear();
            }
            var seat = new RunSave { Wave = 6 };
            seat.Seats.Add(new SeatSave { Relic = RelicId.WallBounce });
            var back = RunSave.Parse(seat.ToText());
            Check(back != null && back.Seats[0].Relic == RelicId.WallBounce, "the relic survives a saved run");
        }

        // Nodes (Docs/Nodes.md): 10+ tiles out, deterministic, 8+ tiles apart, claimed by a room built round them, which takes on their job.
        {
            var nodeMap = new List<ExpansionRoom>();
            bool placedOk = true, farOk = true, hallOk = true, claimOk = true, sameOk = true;
            int placedN = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var list = new List<MapNode>();
                foreach (var nk in new[] { NodeKind.Shop, NodeKind.Vendor, NodeKind.Chest })
                {
                    var nd = Expansion.NodeFor(seed, seed % 5, nodeMap, list, nk);
                    if (!nd.HasValue) { placedOk = false; continue; }
                    placedN++;
                    var n = nd.Value;
                    var again = Expansion.NodeFor(seed, seed % 5, nodeMap, list, nk);
                    sameOk &= again.HasValue && again.Value.Equals(n);
                    farOk &= n.X <= -1000 || n.X >= Game.Width + 1000 || n.Y <= -1000 || n.Y >= Game.Height + 1000;
                    foreach (var o in list) farOk &= new V2(o.X - n.X, o.Y - n.Y).Length >= Expansion.NodeApart;
                    var step = Expansion.NodeStep(nodeMap, n);
                    hallOk &= step.HasValue && step.Value.Kind == RoomKind.Combat && !Expansion.NodeClaimed(nodeMap, n) && Expansion.NodeReachable(nodeMap, n);
                    var built = new List<ExpansionRoom>(nodeMap);
                    if (step.HasValue) built.Add(step.Value);
                    if (step.HasValue && !Expansion.NodeClaimed(built, n))  // (a strip first, then the room round it)
                    {
                        var step2 = Expansion.NodeStep(built, n);
                        hallOk &= Math.Min(step.Value.W, step.Value.H) == 300;
                        if (step2.HasValue) built.Add(step2.Value);
                    }
                    int at = built.FindIndex(br => Expansion.NodeEncloses(br, n)) + 1;
                    var kind = nk == NodeKind.Shop ? RoomKind.Shop : nk == NodeKind.Vendor ? RoomKind.Vendor : RoomKind.Combat;
                    var with = Expansion.Build(built, null, null, new List<MapNode> { n });
                    claimOk &= at > nodeMap.Count && with.Areas[at].Kind == kind && Expansion.Build(built).Areas[at].Kind == RoomKind.Combat && (at == nodeMap.Count + 1 || with.Areas[nodeMap.Count + 1].Kind == RoomKind.Combat);
                    list.Add(n);
                }
            }
            Check(placedOk && placedN == 120 && sameOk, "a node is found for every seed, the same each time (" + placedN + " placed)");
            Check(farOk, "nodes sit 10 or more tiles off the start room and 8 or more tiles apart");
            Check(hallOk && claimOk, "a room (or a strip, then a room) can be built to each: a strip alone claims nothing, and the room takes on the node's job (only with the node in the map)");
            var saved = new RunSave { Wave = 6, HasExpansion = true };
            saved.Nodes.Add(new MapNode { X = -1250, Y = 450, Kind = NodeKind.Vendor });
            saved.Nodes.Add(new MapNode { X = 850, Y = 2150, Kind = NodeKind.Chest, Spent = true });
            saved.Seats.Add(new SeatSave());
            var back = RunSave.Parse(saved.ToText());
            Check(back != null && back.Nodes.Count == 2 && back.Nodes[0].Equals(saved.Nodes[0]) && back.Nodes[1].Equals(saved.Nodes[1]), "nodes survive a saved run");
        }

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
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); cg.Diff.Set(DiffKnob.BuildVote, 1f);
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
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); gg.Diff.Set(DiffKnob.BuildVote, 1f);
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

        // Gold and the shop: kills drop coins that lie on the ground for whoever walks over them (Coins.cs); the wave after a boss ends with the shop open; walking in and shopping closes it.
        Action<V2, V2> PutBoth = (p1, p2) => { g.Players[1].Pos = g.Players[1].Disp = cg.Players[1].Pos = p1; g.Players[2].Pos = g.Players[2].Disp = cg.Players[2].Pos = p2; };  // (each game moves its own player)
        PutBoth(hubAt, hubAt);
        Quiet();
        Run(s, dt, 0.3f);
        foreach (var p in g.Players.Values) p.Gold = 0;
        g.Coins.Clear();
        for (int i = 0; i < 4; i++) spawnEnemyAt.Invoke(g, new object[] { EnemyType.Grunt, shopAt + new V2(30f * i, 0f), 0f, 0, false });
        Quiet();
        Run(s, dt, 0.5f);
        int coinGold = g.Coins.Values.Sum(c => c.Value);
        Check(g.Coins.Count > 0 && cg.Coins.Count == g.Coins.Count && g.Players.Values.All(p => p.Gold == 0), "kills drop coins that stay on the ground, on both screens, until someone walks over them (" + g.Coins.Count + "/" + cg.Coins.Count + " coins, " + g.Players[1].Gold + "/" + g.Players[2].Gold + " held, " + coinGold + " gold)");
        PutBoth(hubAt, shopAt + new V2(45f, 0f));
        Run(s, dt, 2f, () => g.Coins.Count == 0);
        for (int k = 0; k < 20 && g.Coins.Count > 0; k++)  // (a grunt can wander off before it dies: walk to each coin left)
        {
            PutBoth(hubAt, g.Coins.Values.First().Pos);
            Run(s, dt, 0.5f, () => g.Coins.Count == 0);
        }
        Check(g.Coins.Count == 0 && g.Players[2].Gold == coinGold && g.Players[1].Gold == 0, "the player who walks over the coins gets their gold (" + g.Players[2].Pos.DistanceTo(shopAt) + " away, " + g.Players[1].Gold + "/" + g.Players[2].Gold + "/" + coinGold + ")");
        // Vector pickups (Coins.cs, Docs/VectorBuilding.md): the rare drop adds vectors to whoever walks over it, waits while their stock is full, and shows on both screens.
        var vp = g.Players[2];
        int vpWas = vp.Vectors, p1Was = g.Players[1].Vectors;
        vp.Vectors = 10;
        g.Coins.Clear();
        g.Coins[9001] = new Coin { Id = 9001, Vec = true, Value = Game.VectorDropSmall, Pos = vp.Pos, Disp = vp.Pos };
        Run(s, dt, 0.2f);
        Check(cg.Coins.Values.Any(c => c.Vec && c.Value == Game.VectorDropSmall) || g.Coins.Count == 0, "a vector pickup reaches the other screen with its flag");
        Run(s, dt, 2f, () => g.Coins.Count == 0);
        Check(g.Coins.Count == 0 && vp.Vectors == 10 + Game.VectorDropSmall && g.Players[1].Vectors == p1Was, "walking over a vector pickup adds its vectors to that player only (" + vp.Vectors + ")");
        vp.Vectors = vp.VectorCap;
        g.Coins[9002] = new Coin { Id = 9002, Vec = true, Value = Game.VectorDropBig, Pos = vp.Pos, Disp = vp.Pos };
        Run(s, dt, 1f);
        Check(g.Coins.ContainsKey(9002) && vp.Vectors == vp.VectorCap, "a vector pickup waits while the stock is full");
        vp.Vectors = vp.VectorCap - 5;
        Run(s, dt, 1f, () => g.Coins.Count == 0);
        Check(g.Coins.Count == 0 && vp.Vectors == vp.VectorCap, "and is taken once there is room, never past the cap");
        g.Coins.Clear();
        vp.Vectors = vpWas;
        PutBoth(hubAt, hubAt);
        Quiet();
        Run(s, dt, 30f, () => g.State == MatchState.Intermission, NoPicks);
        Check(g.State == MatchState.Intermission && g.ShopOpen && cg.ShopOpen, "the wave after a boss ends with the shop open, on both screens");
        Check(g.Players.Values.All(p => p.Shelf.Any(u => u >= 0)), "everyone has a shelf");
        Check(g.Players.Values.All(p => p.Vectors >= Game.VectorStart + Game.VectorWaveGain) && cg.Players[1].Vectors == g.Players[1].Vectors, "a cleared wave gives everyone " + Game.VectorWaveGain + " vectors, and the client sees them (" + g.Players[1].Vectors + ")");
        // The room shops: upgrades and restocks only in the shop room, services (heals, revives, sales) in either room, nothing in the hub.
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
        pm.Hp = pm.MaxHp * 0.5f;
        int gold2 = pm.Gold;
        g.Shop(ShopAction.Heal);
        Check(pm.Gold == gold2 - Game.HealPrice && pm.Hp >= pm.MaxHp, "the shop room's services work too: it heals");
        g.Shop(ShopAction.BuyBuild, 0);
        Check(pm.Gold == gold2 - Game.HealPrice && pm.Stash[0] == 0, "buildables are only sold at the vendor");
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
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); gg.Diff.Set(DiffKnob.BuildVote, 1f);
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

        // No other foe may be in a locked duel room.
        var intruder = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", flags).Invoke(g, new object[] { EnemyType.Grunt, g.CurrentWorld.Areas[2].Mid + new V2(200f, 0f), 1f, 0, false });
        Run(s, dt, 0.5f, null, NoPicks);
        Check(!g.Enemies.ContainsKey(intruder.Id) || !g.CurrentWorld.Areas[2].Contains(intruder.Pos), "other foes are kept out of a locked duel room");

        // A duel left over from an earlier, bigger map (a restart after a game over) must not break the enemy tick.
        var duels = (Dictionary<int, int>)typeof(Game).GetField("duelBoss", flags).GetValue(g);
        duels[99] = -1;
        var mover = (Enemy)spawnAt.Invoke(g, new object[] { EnemyType.Grunt, new V2(800f, 300f), 0f, 0, false });
        V2 from = mover.Pos;
        Exception stale = null;
        try { Run(s, dt, 0.5f, null, NoPicks); } catch (Exception ex) { stale = ex; }
        Check(stale == null && (mover.Pos - from).Length > 1f, "a stale duel area past the map's end is skipped, and enemies still move (" + (stale == null ? "ok" : stale.GetType().Name) + ")");
        duels.Remove(99);
        mover.Hp = 0f;

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

        // A rule node is a big area: only a room that encloses all of it claims it, and the bot steps reach it.
        {
            var ruleNode = Expansion.NodeFor(5, 0, new List<ExpansionRoom>(), new List<MapNode>(), NodeKind.Rule);
            Check(ruleNode.HasValue && ruleNode.Value.Kind == NodeKind.Rule && ruleNode.Value.Rule < 6, "a rule node is placed");
            if (ruleNode.HasValue)
            {
                var rn = ruleNode.Value;
                Check(!Expansion.NodeEncloses(new ExpansionRoom { X = rn.X - 250, Y = rn.Y - 250, W = 500, H = 500 }, rn) && !Expansion.NodeEncloses(new ExpansionRoom { X = rn.X - 400, Y = rn.Y - 400, W = 800, H = 800 }, rn)
                    && Expansion.NodeEncloses(new ExpansionRoom { X = rn.X - 450, Y = rn.Y - 450, W = 900, H = 900 }, rn), "a 5 x 5 or 8 x 8 room round a rule node does not claim it, a 9 x 9 room does (the whole area must be inside the walls)");
                var plan = new List<ExpansionRoom>();
                for (int i = 0; i < 4 && !Expansion.NodeClaimed(plan, rn); i++) { var st = Expansion.NodeStep(plan, rn); if (st.HasValue) plan.Add(st.Value); }
                Check(Expansion.NodeClaimed(plan, rn) && Expansion.NodeReachable(new List<ExpansionRoom>(), rn), "NodeStep builds up to a room that claims the rule node (" + plan.Count + " boxes)");
                var rr = plan.LastOrDefault();
                Check(plan.Count > 0 && rr.W >= 900 && rr.H >= 900, "and that room is at least 9 x 9 tiles");
            }
        }

        // A claimed rule room takes the rule's ground, and its prize comes out when a wave is cleared.
        {
            var plan = new List<ExpansionRoom>();
            var rn = Expansion.NodeFor(5, 0, plan, new List<MapNode>(), NodeKind.Rule);
            Check(rn.HasValue, "a rule node for the room test");
            if (rn.HasValue)
            {
                var saveRooms = g.Expanded.ToList(); var saveNodes = g.Nodes.ToList();
                for (int i = 0; i < 4 && !Expansion.NodeClaimed(plan, rn.Value); i++) { var st = Expansion.NodeStep(plan, rn.Value); if (st.HasValue) plan.Add(st.Value); }
                foreach (NodeRule rule in System.Enum.GetValues(typeof(NodeRule)))
                {
                    var nd = rn.Value; nd.Rule = (byte)rule;
                    var wld = Expansion.Build(plan, null, null, new List<MapNode> { nd });
                    var rule1 = wld.Areas.Skip(1).Where(a => a.Kind == RoomKind.Floor && a.Floor == Expansion.RuleFloor(nd.Rule)).ToList();
                    Check(rule1.Count >= 1, "a room round a " + rule + " node has " + Expansion.RuleFloor(nd.Rule) + " ground");
                }
                var wUn = Expansion.Build(plan.Take(plan.Count - 1).ToList(), null, null, new List<MapNode> { rn.Value });
                Check(wUn.Areas.All(a => a.Kind != RoomKind.Floor), "the hallway on the way to it is no rule room");
                var clearRules = typeof(Game).GetMethod("ClearRuleRooms", flags);
                var tickNodes2 = typeof(Game).GetMethod("TickNodes", flags);
                foreach (var gg in new[] { g, cg }) { gg.Expanded.Clear(); gg.Expanded.AddRange(plan); gg.Nodes.Clear(); typeof(Game).GetField("expWorld", flags).SetValue(gg, null); }
                var frozen = rn.Value; frozen.Rule = (byte)NodeRule.Frozen;
                var dark = rn.Value; dark.Rule = (byte)NodeRule.Dark;
                g.Nodes.Add(frozen);
                int gold0 = g.Players.Values.Sum(p => p.Gold);
                clearRules.Invoke(g, null);
                Check(g.Nodes[0].Cleared && g.Nodes[0].Spent && g.Players.Values.Sum(p => p.Gold) > gold0, "clearing a Frozen rule room pays gold at once and spends the node");
                int gold1 = g.Players.Values.Sum(p => p.Gold);
                clearRules.Invoke(g, null);
                Check(g.Players.Values.Sum(p => p.Gold) == gold1, "and only once");
                g.Nodes.Clear(); g.Nodes.Add(dark);
                tickNodes2.Invoke(g, null);
                Check(!g.Props.Values.Any(q => q.Kind == PropKind.Chest && q.Pos.DistanceTo(dark.Pos) < 1f), "a Dark rule room holds no chest before it is cleared");
                clearRules.Invoke(g, null);
                tickNodes2.Invoke(g, null);
                Check(g.Nodes[0].Cleared && !g.Nodes[0].Spent && g.Props.Values.Count(q => q.Kind == PropKind.Chest && q.Pos.DistanceTo(dark.Pos) < 1f) == 1, "and one chest at the node once it is");
                foreach (var q in g.Props.Values.Where(q => q.Kind == PropKind.Chest && q.Pos.DistanceTo(dark.Pos) < 1f).ToList()) g.Props.Remove(q.Id);
                foreach (var gg in new[] { g, cg }) { gg.Expanded.Clear(); gg.Expanded.AddRange(saveRooms); gg.Nodes.Clear(); gg.Nodes.AddRange(saveNodes); typeof(Game).GetField("expWorld", flags).SetValue(gg, null); }
            }
        }

        // A node's far room (a long hallway, then a room round it): an enemy there still walks all the way back to the players in the hub.
        {
            var far = new List<ExpansionRoom>();
            var node = Expansion.NodeFor(5, 0, far, new List<MapNode>(), NodeKind.Chest);
            Check(node.HasValue, "a node for the path test");
            for (int i = 0; i < 4 && node.HasValue && !Expansion.NodeClaimed(far, node.Value); i++) { var st = Expansion.NodeStep(far, node.Value); if (st.HasValue) far.Add(st.Value); }
            foreach (var gg in new[] { g, cg }) { gg.Expanded.Clear(); gg.Expanded.AddRange(far); gg.Nodes.Clear(); if (node.HasValue) gg.Nodes.Add(node.Value); typeof(Game).GetField("expWorld", flags).SetValue(gg, null); }
            pickRooms.Invoke(g, null);
            Run(s, dt, 0.3f, null, NoPicks);
            int last = g.CurrentWorld.Areas.Length - 1;
            Check(last == far.Count && last >= 1 && Expansion.NodeClaimed(far, node.Value) && g.CurrentWorld.Areas[last].Kind == RoomKind.Combat, "the rooms out to the node are up (" + g.CurrentWorld.Areas.Length + " areas)");
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            var farFoe = (Enemy)spawnAt.Invoke(g, new object[] { EnemyType.RiotShield, g.CurrentWorld.Areas[last].Mid, 0f, 0, false });
            foreach (var p in g.Players.Values) p.Pos = p.Disp = new V2(800f, 450f);
            float d0 = farFoe.Pos.DistanceTo(new V2(800f, 450f));
            Run(s, dt, 90f, () => g.CurrentWorld.AreaAt(farFoe.Pos) == 0 || farFoe.Hp <= 0f, NoPicks);
            Check(farFoe.Hp <= 0f || g.CurrentWorld.AreaAt(farFoe.Pos) == 0, "a Riot Shield that spawned in the node's far room reaches the hub (started " + d0.ToString("0") + " px away, now " + farFoe.Pos.DistanceTo(new V2(800f, 450f)).ToString("0") + ")");
            // And the other way: players waiting in the far room are still hunted by an enemy from the hub.
            farFoe.Hp = 0f;
            var hubFoe = (Enemy)spawnAt.Invoke(g, new object[] { EnemyType.Grunt, new V2(800f, 450f), 0f, 0, false });
            foreach (var p in g.Players.Values) p.Pos = p.Disp = g.CurrentWorld.Areas[last].Mid;
            // A Grunt keeps 230 px off its target, so it may hold at the far room's door and shoot in: in range counts as there.
            Func<bool> there = () => hubFoe.Hp <= 0f || g.CurrentWorld.AreaAt(hubFoe.Pos) == last
                || g.CurrentWorld.AreaAt(hubFoe.Pos) != 0 && g.Players.Values.Any(p => g.CurrentWorld.AreaAt(p.Pos) == last && p.Pos.DistanceTo(hubFoe.Pos) < 320f);
            Run(s, dt, 90f, there, NoPicks);
            Check(there(), "a Grunt in the hub walks to players waiting in the node's far room, or into range at its door (now in area " + g.CurrentWorld.AreaAt(hubFoe.Pos) + ", " + hubFoe.Pos.DistanceTo(g.CurrentWorld.Areas[last].Mid).ToString("0") + " px away)");
        }
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
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); gg.Diff.Set(DiffKnob.BuildVote, 1f); gg.ExpandOpen = true; }
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
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); gg.Diff.Set(DiffKnob.BuildVote, 1f); gg.Expanded.Add(east); gg.ExpandOpen = true; }
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

    // Breaches (Docs/RoomBuilding2.md step 1): enemies come in through the outer wall, the same breaches on every machine.
    static void ExpandingBreachesTest(float dt)
    {
        string why;
        var list = new List<ExpansionRoom>();
        var east = Expansion.Custom(list, 1600, 100, 800, 600, out why).Value;  // a dead end off the hub's east wall
        list.Add(east);
        var w = Expansion.Build(list);
        var cand = Breaches.Candidates(w);
        bool outer = cand.Count > 0;
        foreach (var c in cand)
        {
            V2 inside = c.Key.Pos + c.Key.In * 20f, outside = c.Key.Pos - c.Key.In * 20f;
            bool inAny = false, outAny = false;
            foreach (var a in w.Areas) { inAny |= a.Contains(inside); outAny |= a.Contains(outside); }
            outer &= inAny && !outAny;
        }
        Check(outer, "every breach spot is on the outer wall, facing into the map (" + cand.Count + " spots)");
        Check(cand.All(c => !(c.Key.Vertical && Math.Abs(c.Key.Pos.X - 1600f) < 1f && c.Key.Pos.Y > 100f && c.Key.Pos.Y < 700f)), "never on the wall the hub and the room share");
        float hubW = cand.Where(c => c.Key.Area == 0).Max(c => c.Value), endW = cand.Where(c => c.Key.Area == 1).Max(c => c.Value);
        Check(endW > hubW, "a dead end's outer wall breaks more often than the hub's (" + endW.ToString("0.00") + " vs " + hubW.ToString("0.00") + ")");
        var b1 = Breaches.For(w, 6); var b2 = Breaches.For(Expansion.Build(list), 6);
        Check(b1.Length == Breaches.CountFor(1) && b1.Select(b => b.Pos).SequenceEqual(b2.Select(b => b.Pos)), "a wave's breaches are the same on every build of the map (" + b1.Length + ")");
        bool apart = true;
        for (int i = 0; i < b1.Length; i++) for (int j = i + 1; j < b1.Length; j++) apart &= b1[i].Pos.DistanceTo(b1[j].Pos) >= Breaches.Apart;
        Check(apart, "and kept apart");
        var withShop = new List<ExpansionRoom>(list);
        var fl = Expansion.FloatingFor(3, 0, withShop, RoomKind.Shop);
        if (fl.HasValue)
        {
            withShop.Add(fl.Value);
            var ws = Expansion.Build(withShop);
            Check(Breaches.Candidates(ws).All(c => !ws.Areas[c.Key.Area].Floating), "a floating room's walls never break");
        }

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 7 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); gg.Diff.Set(DiffKnob.BuildVote, 1f); gg.Expanded.Clear(); gg.Expanded.Add(east); }
        var first = new Dictionary<int, V2>();
        float earliest = float.MaxValue;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        int startUnits = -1;
        bool warned = false, clientWarned = false;
        Run(s, dt, 30f, () => g.State == MatchState.Fighting && g.BreachAge > 9f, () =>
        {
            foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; }
            if (g.State != MatchState.Fighting) return;
            if (startUnits < 0) startUnits = (int)typeof(Game).GetMethod("QueuedUnits", flags).Invoke(g, null) + g.Enemies.Count;
            warned |= g.BreachWarnOn; clientWarned |= cg.BreachWarnOn;
            foreach (var e in g.Enemies.Values)
                if (!e.Def.IsBoss && !first.ContainsKey(e.Id)) { first[e.Id] = e.Pos; earliest = Math.Min(earliest, g.BreachAge); }
        });
        Check(Breaches.IsBreachWave(g.Wave) && !Breaches.IsBreachWave(1) && !Breaches.IsBreachWave(5) && Breaches.IsBreachWave(7) && Breaches.IsBreachWave(11), "breaches are a rare event: wave 3, then every 4th (this is wave " + g.Wave + ")");
        var hb = g.ActiveBreaches; var cb = cg.ActiveBreaches;
        Check(g.BreachesOn && hb.Length > 0 && hb.Select(b => b.Pos).SequenceEqual(cb.Select(b => b.Pos)), "in a wave the host and a friend see the same breaches (" + hb.Length + " / " + cb.Length + ")");
        int byBreach = first.Values.Count(p => hb.Any(b => b.Pos.DistanceTo(p) < 260f));
        Check(first.Count > 0 && byBreach >= first.Count * 0.8f, "enemies come in through them (" + byBreach + " of " + first.Count + " by a breach)");
        Check(first.Values.All(p => g.CurrentWorld.Areas.Any(a => a.Contains(p))), "every one of them comes in inside a room");
        Check(earliest >= Breaches.Telegraph - 0.2f, "not before the barriers are up (first at " + earliest.ToString("0.00") + " s)");
        float warn = Breaches.WarnTime(hb.Length);
        Check(warned && clientWarned && earliest >= warn - 0.2f, "the breach warning plays on both screens and nothing comes in until it's over (" + warn.ToString("0.0") + " s, first at " + earliest.ToString("0.00") + " s)");
        Check(!g.BreachWarnOn && !cg.BreachWarnOn, "and it ends");
        typeof(Game).GetMethod("BuildWave", flags).Invoke(g, new object[] { g.Wave });
        int plain = (int)typeof(Game).GetMethod("QueuedUnits", flags).Invoke(g, null);
        Check(startUnits >= plain * (Breaches.Flood - 0.7f), "a breach wave brings " + Breaches.Flood + "x the enemies (" + startUnits + " vs " + plain + " for the plain wave)");
    }

    // Shutter gates (Docs/RoomBuilding2.md step 3): a gate on a door, voted, synced; G shuts it for everyone for 8 s, then it recharges.
    static void ExpandingGatesTest(float dt)
    {
        string why;
        var list = new List<ExpansionRoom>();
        var east = Expansion.Custom(list, 1600, 100, 800, 600, out why).Value; list.Add(east);
        var w = Expansion.Build(list);
        var door = w.Doors[0];
        Check(Gates.CanFit(w, new List<GateFit>(), door.Pos, out why), "a plain door takes a gate" + (why != null ? " (" + why + ")" : ""));
        Check(!Gates.CanFit(w, new List<GateFit>(), door.Pos + new V2(0f, 200f), out why) && why != null, "a spot that isn't a door doesn't (" + why + ")");
        Check(!Gates.CanFit(w, new List<GateFit> { Gates.Of(door) }, door.Pos, out why) && why != null, "nor a door that has one (" + why + ")");
        var cutW = Expansion.Build(list, new List<WallCut> { new WallCut { Vertical = true, At = 1600, From = 100, Till = 700 } });
        Check(Gates.DoorAt(cutW, door.Pos) < 0, "an opened wall isn't a door");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Diff.Set(DiffKnob.BuildVote, 1f); gg.Expanded.Clear(); gg.Expanded.AddRange(list); gg.RoomsUp = true; gg.ExpandOpen = true; }
        g.Players[2].Vectors = Gates.Price + 2;
        cg.ProposeExpandGate(door.Pos);
        Run(s, dt, 0.5f, () => cg.ExpandProposals.Count == 1);
        Check(g.ExpandProposals.Count == 1 && g.ExpandProposals[0].Kind == RoomKind.Gate && cg.ExpandProposals.Count == 1, "a friend's gate goes to the vote on both screens");
        g.CastExpandVote(2);
        typeof(Game).GetMethod("ResolveBuildRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(g, null);
        Run(s, dt, 0.5f, () => cg.GateFits.Count == 1);
        Check(g.GateFits.Count == 1 && cg.GateFits.Count == 1 && g.Players[2].Vectors == 2, "the winning gate is fitted on both screens and its proposer pays");
        foreach (var gg in new[] { g, cg }) gg.RoomsUp = true;
        Check(g.GateLive(0) && g.GateShut(0) == 0f && g.PushOutOfSolids(door.Pos, 14f).DistanceTo(door.Pos) < 0.5f, "open, the doorway is free");
        foreach (var gg in new[] { g, cg }) foreach (var p in gg.Players.Values) if (p.Id == 2) p.Pos = p.Disp = door.Pos + new V2(-80f, 0f);
        Run(s, dt, 0.3f);  // (the host hears where the friend now stands)
        cg.UseGate();
        Run(s, dt, 0.5f, () => cg.GateShut(0) > 0f);
        Check(g.GateShut(0) > 7f && cg.GateShut(0) > 7f, "a friend by it shuts it: shut on both screens (" + g.GateShut(0).ToString("0.0") + " s)");
        V2 pushed = g.PushOutOfSolids(door.Pos + new V2(-3f, 0f), 14f);
        Check(pushed.DistanceTo(door.Pos) > 20f && pushed.X < door.Pos.X, "shut, it's a wall: someone in the doorway is put back out the side they came from");
        cg.UseGate();
        Run(s, dt, 0.3f);
        Check(g.GateShut(0) < Gates.CloseTime - 0.2f, "shutting it again does nothing while it's shut");
        Run(s, dt, Gates.CloseTime + 0.5f);
        Check(g.GateShut(0) == 0f && g.GateCool(0) > 0f && g.PushOutOfSolids(door.Pos, 14f).DistanceTo(door.Pos) < 0.5f, "after 8 s it opens and recharges");
        g.UseGate();
        Run(s, dt, 0.3f);
        Check(g.GateShut(0) == 0f, "it can't be shut again until it has recharged");

        // Steering: with a loop round the gated door, an enemy heads for the other way round while it's shut.
        var loop = new List<ExpansionRoom>();
        var eastL = Expansion.Custom(loop, 1600, 100, 800, 800, out why).Value; loop.Add(eastL);
        var southL = Expansion.Custom(loop, 1100, 900, 1200, 400, out why);
        Check(southL.HasValue, "a loop room fits" + (why != null ? " (" + why + ")" : ""));
        if (!southL.HasValue) return;
        loop.Add(southL.Value);
        foreach (var gg in new[] { g, cg }) { gg.Expanded.Clear(); gg.Expanded.AddRange(loop); typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(gg, null); gg.RoomsUp = true; }
        var lw = g.ExpansionWorld;
        int hubEast = Array.FindIndex(lw.Doors, d => d.Vertical && Math.Abs(d.Pos.X - 1600f) < 1f);
        var gd = lw.Doors[hubEast];
        g.GateFits.Clear(); g.GateFits.Add(Gates.Of(gd));
        var steer = typeof(Game).GetMethod("SteerDir", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        V2 from = gd.Pos + new V2(-260f, 0f), to = gd.Pos + new V2(300f, 0f);
        Func<int, V2> ask = key => (V2)steer.Invoke(g, new object[] { key, from, 0f, 14f, to, (to - from).Normalized() });
        V2 open = ask(9001);
        typeof(Game).GetMethod("SyncGateClocks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(g, null);
        ((List<float>)typeof(Game).GetField("gateShut", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(g))[0] = 5f;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        V2 shutDir = ask(9002);
        long ms = sw.ElapsedMilliseconds;
        Check(open.X > 0.7f, "open, an enemy walks straight through the door (" + open.X.ToString("0.00") + ", " + open.Y.ToString("0.00") + ")");
        Check(shutDir.Y > 0.5f, "shut, it heads the other way round, for the south room's door (" + shutDir.X.ToString("0.00") + ", " + shutDir.Y.ToString("0.00") + ", route worked out in " + ms + " ms)");
        Check(g.ActiveLayout.Solids.Length < ((Arena.LayoutDef)typeof(Game).GetProperty("SteerLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(g)).Solids.Length, "the drawn floor plan doesn't change (only steering sees the gate)");
    }

    // Portal links (Docs/RoomBuilding2.md step 2): a portal pair between two rooms, priced by the walking it saves, voted, synced.
    static void ExpandingLinksTest(float dt)
    {
        string why;
        var list = new List<ExpansionRoom>();
        var east = Expansion.Custom(list, 1600, 100, 800, 600, out why).Value; list.Add(east);
        var south = Expansion.Custom(list, 0, 900, 800, 600, out why).Value; list.Add(south);
        var w = Expansion.Build(list);
        Func<RoomArea, V2> clearSpot = a =>  // a grid spot in the room clear of its obstacles and doors
        {
            for (float y = a.Min.Y + 100f; y < a.Max.Y; y += 100f)
                for (float x = a.Min.X + 100f; x < a.Max.X; x += 100f)
                {
                    var p = new V2(x, y);
                    if (a.Contains(p, -Expansion.LinkEdge) && !w.Cover.Any(c => Layouts.Inside(c, p, 60f)) && w.Doors.All(d => d.Pos.DistanceTo(p) >= 300f))
                        return p;
                }
            return a.Mid;
        };
        V2 ea = clearSpot(w.Areas[1]), sb = clearSpot(w.Areas[2]);
        var link = new PortalLink { AX = (short)ea.X, AY = (short)ea.Y, BX = (short)sb.X, BY = (short)sb.Y };
        float walk = Expansion.WalkDistance(w, link.A, link.B);
        int price = Expansion.LinkPrice(w, 0, link, out why);
        Check(walk > 1800f && price == Math.Min(Expansion.LinkMax, Expansion.LinkBase + (int)Math.Round(walk / 200f)), "a link costs 8 + 1 per 2 tiles of walking it saves (" + (int)walk + " px, " + price + " vectors" + (why != null ? ", " + why : "") + ")");
        Check(Expansion.LinkPrice(w, 0, new PortalLink { AX = link.AX, AY = link.AY, BX = link.AX, BY = (short)(link.AY + 200) }, out why) == 0 && why != null, "both ends in one room is refused (" + why + ")");
        Check(Expansion.LinkPrice(w, 0, new PortalLink { AX = link.AX, AY = link.AY, BX = 3000, BY = 400 }, out why) == 0 && why != null, "an end outside the map is refused (" + why + ")");
        Check(Expansion.LinkPrice(w, 0, new PortalLink { AX = 1700, AY = 400, BX = link.BX, BY = link.BY }, out why) == 0 && why != null, "an end by a door is refused (" + why + ")");
        Check(Expansion.LinkPrice(w, Expansion.MaxLinks, link, out why) == 0 && why != null, "at most " + Expansion.MaxLinks + " pairs");
        var wl = Expansion.Build(list, null, new List<PortalLink> { link });
        var exit = wl.PortalExitFor(link.A);
        Check(wl.Portals.Length == 1 && exit.HasValue && wl.AreaAt(exit.Value) == wl.AreaAt(link.B) && exit.Value.DistanceTo(link.B) < 120f, "built, it is a portal pair: step in one end, come out the other");
        Check(Expansion.WalkDistance(wl, link.A, link.B) < 1f, "and walking between its ends now takes no time");
        Check(Expansion.LinkPrice(wl, 1, new PortalLink { AX = (short)(link.AX + 100), AY = link.AY, BX = 1000, BY = 300 }, out why) == 0 && why != null, "a second pair right by the first is refused (" + why + ")");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); gg.Diff.Set(DiffKnob.BuildVote, 1f); gg.Expanded.Clear(); gg.Expanded.AddRange(list); gg.ExpandOpen = true; }
        // The Black Market sells the teleporter: bought with gold into the stash, then placed (no vectors, no vote).
        g.Players[2].Vectors = 3;
        cg.ProposeExpandLink(link);
        Run(s, dt, 0.5f, () => false);
        Check(g.Links.Count == 0 && g.ExpandProposals.Count == 0, "a portal pair is refused without a teleporter in the stash");
        g.Players[1].Gold = 200;
        g.ShopOpen = true;
        g.Shop(ShopAction.BuyBuild, (int)BuildKind.Teleporter);
        Check(g.Players[1].Stash[0] == 1 && g.Players[1].Gold == 200 - Market.Prices[0], "the Black Market sells a teleporter for gold into the stash");
        g.Shop(ShopAction.BuyBuild, (int)BuildKind.SpikeTrap);
        g.ShopOpen = false;
        g.Players[2].Stash[0] = 1;
        g.Players[2].Stash[(int)BuildKind.SpikeTrap] = 1;
        cg.ProposeExpandLink(link);
        Run(s, dt, 0.5f, () => cg.Links.Count == 1);
        Check(g.Links.Count == 1 && cg.Links.Count == 1 && g.Players[2].Vectors == 3 && g.Players[2].Stash[0] == 0 && g.ExpandProposals.Count == 0, "a teleporter from the stash builds the pair at once on both screens, for no vectors");
        V2 trap = V2.Zero;
        string pw = null;
        bool spotOk = false;
        foreach (var ar in g.ExpansionWorld.Areas)
            for (float ty = ar.Min.Y; ty <= ar.Max.Y && !spotOk; ty += 100f)
                for (float tx = ar.Min.X; tx <= ar.Max.X && !spotOk; tx += 100f)
                    spotOk = Expansion.PlaceSpot(g.ExpansionWorld, g.Placed, trap = new V2(tx, ty), out pw);
        cg.PlaceBuild(BuildKind.SpikeTrap, (int)trap.X, (int)trap.Y);
        Run(s, dt, 0.5f, () => cg.Placed.Count == 1);
        Check(spotOk && g.Placed.Count == 1 && cg.Placed.Count == 1 && g.Players[2].Stash[(int)BuildKind.SpikeTrap] == 0, "a trap from the stash is placed on both screens (" + pw + ")");
        Check(!Expansion.PlaceSpot(g.ExpansionWorld, g.Placed, trap, out pw), "and a second one right on it is refused (" + pw + ")");
        Check(g.ExpansionWorld.Portals.Length == 1 && cg.ExpansionWorld.Portals.Length == 1, "and both maps have the portals");
    }

    static bool Strip(RoomArea a) => Math.Min(a.Size.X, a.Size.Y) <= Expansion.StripMax;

    // Room sizes (no hallways: any box 2 to 24 tiles a side, up to 144 tiles of floor, priced by its new wall), a room that only
    // brushes another's wall (no door there, no breach on it), and side walls: built from the map alone (the same every build), out
    // from a room's own outer walls, off its doors, clear of its middle, and every design turns up.
    static void ExpandingRoomSizesTest(float dt)
    {
        string why;
        var none = new List<ExpansionRoom>();
        Check(Expansion.Custom(none, 0, -200, 2400, 200, out why).HasValue, "a 24 x 2 strip along the hub is fine (" + why + ")");
        Check(Expansion.Custom(none, 500, -600, 2400, 600, out why).HasValue, "a 24 x 6 room is fine (" + why + ")");
        Check(Expansion.Custom(none, 500, -2500, 300, 2500, out why) == null && why.Contains("big"), "25 tiles is too long: " + why);
        Check(Expansion.Custom(none, 500, -1300, 1200, 1300, out why) == null && why.Contains("big"), "more than 144 tiles of floor is too big: " + why);
        Check(Expansion.Custom(none, 500, -1200, 1200, 1200, out why).HasValue, "a 12 x 12 room is fine (" + why + ")");
        // Brushing: a box that shares 3 tiles with the hub and 1 with the east room: accepted, a door to the hub only, no breach on the brushed stretch.
        var east = Expansion.Custom(none, 1600, 0, 600, 300, out why).Value;
        var withEast = new List<ExpansionRoom> { east };
        var brush = Expansion.Custom(withEast, 1300, -300, 400, 300, out why);
        Check(brush.HasValue && brush.Value.Parent == 0, "a room that only brushes another room's wall is fine (" + why + ")");
        if (!brush.HasValue) return;
        var bw = Expansion.Build(new List<ExpansionRoom> { east, brush.Value });
        Check(!bw.Doors.Any(d => d.Joins(1) && d.Joins(2)) && bw.Doors.Count(d => d.Joins(0) && d.Joins(2)) == 1, "it gets no door where it brushes, and its own door to the hub");
        int badBreach = Breaches.Candidates(bw).Count(c => bw.Areas.Any(ar => ar.Contains(c.Key.Pos + c.Key.In * 20f)) && bw.Areas.Any(ar => ar.Contains(c.Key.Pos - c.Key.In * 20f)));
        Check(badBreach == 0, "no breach spot has floor on both sides (" + badBreach + ")");

        // Side walls (Expansion.AddFins): every design, forced and rolled, on rooms of many sizes off the hub's north wall.
        int finRooms = 0, badFin = 0, notSame = 0, small = 0, smallBare = 0;
        var styles = new HashSet<int>();
        for (int style = 0; style <= 4; style++)
            for (int w = 200; w <= 1600; w += 100)
                for (int h = 200; h * w <= Expansion.MaxArea && h <= 1600; h += 100)
                {
                    Expansion.FinStyle = style;
                    var r = Expansion.Custom(none, 0, -h, w, h, out why);
                    if (!r.HasValue) continue;
                    var map = new List<ExpansionRoom> { r.Value };
                    var wd = Expansion.Build(map);
                    var plain = wd.Spans.Where(sp => !Inner(wd, sp)).Count();
                    var fins = wd.Spans.Where(sp => Inner(wd, sp)).ToList();
                    if (fins.Count > 0) { finRooms++; if (style > 0) styles.Add(style); }
                    if (Math.Min(w, h) < 500 && Math.Max(w, h) >= 400) { small++; if (fins.Count == 0) { smallBare++; Console.WriteLine("  bare: style " + style + " " + w + " x " + h); } }
                    var again = Expansion.Build(map);
                    if (again.Spans.Length != wd.Spans.Length) notSame++;
                    var room = wd.Areas[1];
                    foreach (var sp in fins)
                    {
                        V2 a = sp.Vertical ? new V2(sp.At, sp.From) : new V2(sp.From, sp.At), b = sp.Vertical ? new V2(sp.At, sp.Till) : new V2(sp.Till, sp.At);
                        if (!room.Contains(a, 1f) || !room.Contains(b, 1f)) badFin++;                                     // inside its room
                        foreach (var d in wd.Doors) if (Math.Min(d.Pos.DistanceTo(a), d.Pos.DistanceTo(b)) < 150f) badFin++;  // off the doors
                    }
                    foreach (var sd in wd.WallsFor(false)) if (Layouts.Inside(sd, room.Mid, 30f)) badFin++;               // the middle is open
                }
        Expansion.FinStyle = 0;
        Check(finRooms > 100 && badFin == 0 && notSame == 0, "rooms grow side walls inside, off their doors, clear of their middle, the same every build (" + finRooms + " rooms, " + badFin + " bad)");
        Check(smallBare == 0, "small rooms and narrow strips get side walls too (" + (small - smallBare) + " of " + small + ")");
        Check(styles.Count == 4, "every design turns up: fins, baffles, pincers, T-fins (" + styles.Count + ")");
        var rolled = new HashSet<int>();
        for (int w = 500; w <= 1200; w += 100) { var r = Expansion.Custom(none, 0, -800, w, 800, out why); if (r.HasValue) rolled.Add(Expansion.Build(new List<ExpansionRoom> { r.Value }).Spans.Count(sp => Inner(Expansion.Build(new List<ExpansionRoom> { r.Value }), sp))); }
        Check(rolled.Count >= 2, "rolled per room, rooms differ (" + rolled.Count + " different wall counts)");
    }

    /// A wall span with the same room on both sides: a side wall.
    static bool Inner(World w, WallSpan sp)
    {
        float mid = (sp.From + sp.Till) / 2f;
        V2 across = sp.Vertical ? new V2(1f, 0f) : new V2(0f, 1f), on = sp.Vertical ? new V2(sp.At, mid) : new V2(mid, sp.At);
        int a = -1, b = -1;
        for (int i = 0; i < w.Areas.Length; i++) { if (w.Areas[i].Contains(on + across * 20f)) a = i; if (w.Areas[i].Contains(on - across * 20f)) b = i; }
        return a >= 0 && a == b;
    }

    // Drawn outlines (Expansion.Outline): corners on the grid become a room of boxes joined open, priced by new wall, sent to the vote and built.
    static void ExpandingOutlineTest(float dt)
    {
        string why;
        var none = new List<ExpansionRoom>();
        System.Func<int[], List<int[]>> P = xy => { var pl = new List<int[]>(); for (int i = 0; i < xy.Length; i += 2) pl.Add(new[] { xy[i], xy[i + 1] }); return pl; };
        // A rule node inside a drawn L (cut into two boxes, neither enclosing the whole 8 x 8 area): the room as a whole claims it and takes its ground.
        {
            var ruleL = Expansion.Outline(none, P(new[] { -900, -1100, 600, -1100, 600, -600, 200, -600, 200, 0, -900, 0 }), PathId.None, out why);
            var frozen = new MapNode { X = -350, Y = -550, Kind = NodeKind.Rule, Rule = (byte)NodeRule.Frozen };
            Check(ruleL != null && ruleL.Count >= 2 && !ruleL.Any(r => Expansion.NodeEncloses(r, frozen)), "the L is two boxes, neither enclosing the rule node alone (" + why + ")");
            if (ruleL != null)
            {
                var ew = Expansion.Build(ruleL, null, null, new List<MapNode> { frozen });
                Check(Expansion.NodeClaimed(ruleL, frozen) && Enumerable.Range(1, ruleL.Count).All(i => ew.Areas[i].Kind == RoomKind.Floor && ew.Areas[i].Floor == PathId.FrozenLake),
                    "a drawn L round a rule node claims it: every box of the room is Frozen ground");
            }
        }
        // A plain box drawn as four corners: one box, priced like the same box drawn the old way.
        var box = Expansion.Outline(none, P(new[] { 0, -600, 800, -600, 800, 0, 0, 0 }), PathId.None, out why);
        Check(box != null && box.Count == 1 && box[0].Parent == 0 && box[0].Price == Expansion.Cost(none, 0, -600, 800, 600), "four corners on the hub's wall make one box at the box's price (" + why + ")");
        // An L above the hub: 6 wide at the bottom, an arm going up on the left. A straight-on corner in the middle of a wall is fine.
        var ell = P(new[] { 0, 0, 300, 0, 600, 0, 600, -400, 300, -400, 300, -1000, 0, -1000 });
        var l = Expansion.Outline(none, ell, PathId.None, out why);
        Check(l != null && l.Count == 2 && l[0].Leg == 0 && Expansion.IsPiece(l[1]) && l[1].Parent == 1 && l[1].Price == 0, "an L is two boxes: the one on the hub, then a piece joined to it (" + why + ")");
        if (l == null) return;
        Check(l[0].Price == 2 * (600 + 1000) / 100 - 6 && Expansion.FloorTiles(l[0], l.GetRange(1, 1)) == 6 * 4 + 3 * 6, "it costs its new wall: the outline less the 6 tiles on the hub (" + l[0].Price + ")");
        var lw = Expansion.Build(l);
        int seam = System.Array.FindIndex(lw.Doors, d => d.Joins(1) && d.Joins(2));
        Check(seam >= 0 && lw.Doors[seam].W == 300f && lw.Seam(seam), "its seam is open the full width and counts as a seam");
        Check(lw.Areas[1].Group != 0 && lw.Areas[1].Group == lw.Areas[2].Group && !lw.Areas[1].Hall && !lw.Areas[2].Hall && lw.Areas[0].Group == 0, "both boxes are one room (a group), not hallways");
        float rimLen = lw.Rim(1, 0f).Concat(lw.Rim(2, 0f)).Sum(e => e[0].DistanceTo(e[1]));
        Check(System.Math.Abs(rimLen - 3200f) < 1f, "its rim runs round the L, not across the seam (" + rimLen + " px)");
        var inset = lw.Rim(1, 18f).Concat(lw.Rim(2, 18f)).ToList();
        Check(inset.All(e => lw.Areas[1].Contains(e[0]) || lw.Areas[2].Contains(e[0])) && System.Math.Abs(inset.Sum(e => e[0].DistanceTo(e[1])) - (3200f - 8f * 18f)) < 1f, "pulled in 18 px it stays inside and closes up at the corners");
        Check(Expansion.BoughtCount(l) == 1 && Expansion.ConnectedAreas(l).All(c => c), "it counts as one room, and both boxes are on the map");
        // A U on the hub's north wall, three wide everywhere: three boxes.
        var u = Expansion.Outline(none, P(new[] { 0, 0, 300, 0, 300, -600, 900, -600, 900, 0, 1200, 0, 1200, -900, 0, -900 }), PathId.None, out why);
        Check(u != null && u.Count == 3, "a U is three boxes (" + why + ")");
        // A 2-tile-tall box wedged between two rooms, sharing 2 tiles with each (the owner's notch fill): it builds, with a door to each.
        var sides = new List<ExpansionRoom>();
        var lr = Expansion.Custom(sides, 0, -800, 600, 800, out why); if (lr.HasValue) sides.Add(lr.Value);
        var rr = Expansion.Custom(sides, 800, -800, 800, 800, out why); if (rr.HasValue) sides.Add(rr.Value);
        var notch = Expansion.Outline(sides, P(new[] { 600, -800, 800, -800, 800, -600, 600, -600 }), PathId.None, out why);
        Check(sides.Count == 2 && notch != null && notch.Count == 1, "a 2-tall box between two rooms, 2 tiles on each, builds (" + why + ")");
        if (notch != null)
        {
            var nw = Expansion.Build(sides.Concat(notch).ToList());
            Check(nw.Doors.Count(d => d.Joins(3)) == 2, "it gets a door to both rooms (" + nw.Doors.Count(d => d.Joins(3)) + ")");
        }
        Check(Expansion.Outline(none, P(new[] { 1600, 0, 1800, 0, 1800, -200, 1600, -200 }), PathId.None, out why) == null, "a box touching the hub only at a corner is refused: " + why);
        // Refusals.
        Check(Expansion.Outline(none, P(new[] { 0, 0, 1000, 0, 1000, -600, 900, -600, 900, -100, 0, -100 }), PathId.None, out why) == null && why.Contains("thin"), "1 tile wide is too thin: " + why);
        Check(Expansion.Outline(none, P(new[] { 0, 0, 600, -600, 0, -600, 0, -300 }), PathId.None, out why) == null, "a slanted wall is refused: " + why);
        Check(Expansion.Outline(none, P(new[] { 0, 0, 900, 0, 900, -900, 300, -900, 300, 300, 600, 300, 600, -600, 0, -600 }), PathId.None, out why) == null && why.Contains("cross"), "walls that cross are refused: " + why);
        Check(Expansion.Outline(none, P(new[] { 200, 100, 800, 100, 800, -600, 200, -600 }), PathId.None, out why) == null && why.Contains("Overlaps"), "an outline over the hub is refused: " + why);
        Check(Expansion.Outline(none, P(new[] { 0, -1000, 600, -1000, 600, -400, 0, -400 }), PathId.None, out why) == null && why.Contains("share"), "one off the map is refused: " + why);
        Check(Expansion.Outline(none, P(new[] { 0, 0, 1300, 0, 1300, -1300, 0, -1300 }), PathId.None, out why) == null && why.Contains("big"), "more than " + Expansion.MaxArea / 10000 + " tiles of floor is refused: " + why);
        var themed = Expansion.Outline(none, ell, PathId.SwarmFields, out why);
        Check(themed != null && themed.All(r => r.Kind == RoomKind.Floor && r.Floor == PathId.SwarmFields) && themed[0].Price == l[0].Price + Expansion.ThemeCost(PathId.SwarmFields), "a theme goes on every box and costs its extra once");

        // Fill: a run of walls from one room's wall to another that the map's walls close off. Two rooms above the hub, 4 tiles apart.
        string fw;
        var pair = new List<ExpansionRoom> { Expansion.Custom(none, 0, -600, 600, 600, out fw).Value };
        pair.Add(Expansion.Custom(pair, 1000, -600, 600, 600, out fw).Value);
        var lid = Expansion.Fill(pair, P(new[] { 600, -600, 1000, -600 }), PathId.None, out why);
        Check(lid != null && lid.Count == 1 && lid[0].X == 600 && lid[0].Y == -600 && lid[0].W == 400 && lid[0].H == 600 && lid[0].Price == 4, "a lid across the gap between two rooms fills the gap, paying only the lid (" + why + (lid != null ? ", " + lid[0].Price : "") + ")");
        var east = new List<ExpansionRoom>(pair) { Expansion.Custom(pair, 1600, 0, 600, 300, out fw).Value };
        var corner = Expansion.Fill(east, P(new[] { 1600, -600, 2200, -600, 2200, 0 }), PathId.None, out why);
        Check(corner != null && Expansion.FloorTiles(corner[0], corner.GetRange(1, corner.Count - 1)) == 36 && corner[0].Price == 12, "an L from one room's corner to another's fills the corner between them (" + why + (corner != null ? ", " + corner[0].Price : "") + ")");
        Check(Expansion.Fill(pair, P(new[] { 600, -600, 600, -1200 }), PathId.None, out why) == null && why.Contains("wall"), "a wall ending in the open is refused: " + why);
        Check(Expansion.Fill(pair, P(new[] { 0, -600, 0, -1500, 1600, -1500, 1600, -600 }), PathId.None, out why) == null && why.Contains("big"), "fencing in more than a room may hold is refused: " + why);
        Check(Expansion.Fill(pair, P(new[] { 100, 0, 100, -600 }), PathId.None, out why) == null && why.Contains("through"), "a wall through a room is refused: " + why);

        // In a run: a friend's outline goes to the vote as one card and is built whole on both screens.
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Diff.Set(DiffKnob.BuildVote, 1f); gg.Expanded.Clear(); gg.ExpandOpen = true; }
        g.Players[2].Vectors = l[0].Price - 1;
        cg.ProposeExpandOutline(ell);
        Run(s, dt, 0.5f, () => g.ExpandProposals.Count > 0);
        Check(g.ExpandProposals.Count == 0, "an outline its drawer can't pay for is refused");
        g.Players[2].Vectors = l[0].Price + 3;
        cg.ProposeExpandOutline(ell);
        Run(s, dt, 0.5f, () => cg.ExpandProposals.Count == 2);
        Check(g.ProposalHeads.Count == 1 && cg.LegsOf(2).Count == 1 && Expansion.IsPiece(cg.LegsOf(2)[0]), "it goes to the vote as one proposal with its piece, on both screens");
        g.CastExpandVote(2);
        typeof(Game).GetMethod("ResolveBuildRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(g, null);
        Run(s, dt, 0.5f, () => cg.Expanded.Count == 2);
        Check(g.Expanded.Count == 2 && cg.Expanded.Count == 2 && g.Players[2].Vectors == 3, "winning builds both boxes on both screens, and the drawer pays once (" + g.Players[2].Vectors + " left)");
        Check(cg.ExpansionWorld.Doors.Count(d => d.Width == 300f) == 1, "and the client's map has the open seam");
        Check(Expansion.GroupOf(g.Expanded, 1).Count == 2 && Expansion.HeadOf(g.Expanded, 1) == 0, "a box of it finds its whole room");
        // And a friend's fill: the lid between two rooms, sent as an open run of walls.
        foreach (var gg in new[] { g, cg }) { gg.Expanded.Clear(); gg.Expanded.AddRange(pair); gg.ExpandOpen = true; }
        g.ExpandProposals.Clear(); g.ExpandVotes.Clear();
        g.Players[2].Vectors = 10;
        cg.ProposeExpandOutline(P(new[] { 600, -600, 1000, -600 }), PathId.None, true);
        Run(s, dt, 0.5f, () => g.ExpandProposals.Count > 0);
        Check(g.ExpandProposals.Count == 1 && g.ProposalOf(2).Value.W == 400 && g.ProposalOf(2).Value.Price == 4, "a friend's fill reaches the host as the space it fences in");
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
        Check(costs[costs.Count / 2] >= 4 && costs[costs.Count - 1] <= 44, "reaching a floating room mostly takes a narrow strip (median 4+, at most 44 vectors, median " + costs[costs.Count / 2] + ")");
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
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); cg.Diff.Set(DiffKnob.BuildVote, 1f);
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
        Check(inkwell >= 0 && architect >= 0 && Upgrades.All[inkwell].NeedsExpanding && Upgrades.All[architect].NeedsExpanding, "the two vector cards exist and only come in the Expanding arena");
        Check(!Upgrades.Available(inkwell, new OfferCtx(g.Players[1].Role, g.Players[1].RoleAbility, new List<int>())) && Upgrades.Available(inkwell, new OfferCtx(g.Players[1].Role, g.Players[1].RoleAbility, new List<int>(), null, true)), "a vector card is only available in the Expanding arena");
        var rolls = new Random(5);
        int offeredPlain = 0, offeredExpanding = 0;
        for (int i = 0; i < 400; i++)
        {
            foreach (int u in Upgrades.RollOffer(rolls, new OfferCtx(g.Players[1].Role, g.Players[1].RoleAbility, new List<int>()), 1 + i % 20))
                offeredPlain += Upgrades.All[u].NeedsExpanding ? 1 : 0;
            foreach (int u in Upgrades.RollOffer(rolls, new OfferCtx(g.Players[1].Role, g.Players[1].RoleAbility, new List<int>(), null, true), 1 + i % 20))
                offeredExpanding += Upgrades.All[u].NeedsExpanding ? 1 : 0;
        }
        Check(offeredPlain == 0 && offeredExpanding > 0, "level-up offers hold a vector card only in the Expanding arena (" + offeredExpanding + " of 1200 cards)");
        p1.Vectors = 5;
        g.GrantUpgrade(1, inkwell);
        Check(p1.VectorCap == Game.VectorCapBase + 20 && p1.Vectors == 5, "Deep Inkwell raises the cap by 20 and gives nothing now");
        g.GrantUpgrade(1, architect);
        Check(p1.VectorCap == Game.VectorCapBase + 60 && p1.Vectors == 25, "Architect raises it by 40 more and gives 20 vectors now (" + p1.VectorCap + ", " + p1.Vectors + ")");
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
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.BuildVote, 1f); cg.Diff.Set(DiffKnob.BuildVote, 1f);
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

    /// Free building (the default; "Team vote building" off): a drawing is built the moment it's valid, any number a round, and with a wave on
    /// only wall openings (doors) are accepted. Team vote building keeps the old rules.
    static void ExpandingFreeBuildTest(float dt)
    {
        string why;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        Check(g.State == MatchState.Intermission && cg.State == MatchState.Intermission && !g.DoorsOpen && !cg.DoorsOpen && !g.CanCut, "no doors in the opening countdown");
        foreach (var gg in new[] { g, cg }) gg.Diff.Set(DiffKnob.ArenaExpanding, 0f);
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        var list = new List<ExpansionRoom>();
        var east = Expansion.Custom(list, 1600, 100, 800, 600, out why).Value;
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Add(east); }
        Check(g.FreeBuild && cg.FreeBuild && !g.BuildVoting, "free building is the default for the Expanding arena");
        Check(g.State == MatchState.Fighting && !g.ExpandOpen && !g.DoorsOpen && !g.CanCut, "with a wave on, nothing can be built");

        // Mid-wave: a room and a door are both refused.
        cg.ProposeExpandRoom(-800, 100, 800, 600);
        g.ProposeExpandRoom(-800, 100, 800, 600);
        cg.ProposeExpandCut(new WallCut { Vertical = true, At = 1600, From = 100, Till = 700 });
        Run(s, dt, 0.5f);
        Check(g.Expanded.Count == 1 && cg.Expanded.Count == 1 && g.Openings.Count == 0 && cg.Openings.Count == 0, "no room or door mid-wave");
        g.Players[2].Vectors = 10;
        var all = new WallCut { Vertical = true, At = 1600, From = 100, Till = 700 };

        // Between waves: every drawing is built at once, as many as people can pay for.
        foreach (var gg in new[] { g, cg }) gg.ExpandOpen = true;
        Check(!g.DoorsOpen, "(the building round has its own rules, not the doors-only ones)");
        Run(s, dt, 0.3f);
        int v2 = g.Players[2].Vectors = 100, v1 = g.Players[1].Vectors = 100;
        var west = Expansion.Custom(g.Expanded, -800, 100, 800, 600, out why).Value;
        cg.ProposeExpandRoom(-800, 100, 800, 600);
        Run(s, dt, 0.5f, () => cg.Expanded.Count == 2);
        Check(g.Expanded.Count == 2 && cg.Expanded.Count == 2 && g.Expanded[1].Owner == 2 && g.ProposalHeads.Count == 0 && g.ExpandOpen, "a friend's room is built the moment it's drawn; the round stays open");
        Run(s, dt, 1f, () => cg.Players[2].Vectors == g.Players[2].Vectors);
        Check(g.Players[2].Vectors == v2 - west.Price && cg.Players[2].Vectors == g.Players[2].Vectors, "and only they pay (" + west.Price + ")");
        var offers = Expansion.Offers(5, 6, g.Expanded);
        Check(offers.Count > 0, "there is room for another");
        var o = offers[0];
        g.ProposeExpandRoom(o.X, o.Y, o.W, o.H);
        Run(s, dt, 0.5f, () => cg.Expanded.Count == 3);
        Check(g.Expanded.Count == 3 && cg.Expanded.Count == 3 && g.Players[1].Vectors == v1 - o.Price, "a second room in the same round is built too (no one-at-a-time limit)");
        cg.CastExpandVote(0);
        Run(s, dt, 0.3f);
        Check(g.ExpandVotes.Count == 0, "votes mean nothing in free building");
        var west2 = new WallCut { Vertical = true, At = 0, From = 100, Till = 700 };
        g.ProposeExpandCut(west2);
        Run(s, dt, 0.5f, () => cg.Openings.Count == 1);
        Check(g.Openings.Count == 1 && cg.Openings.Count == 1, "a door between waves is built at once");
        typeof(Game).GetMethod("ResolveBuildRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(g, null);
        Run(s, dt, 0.5f, () => !cg.ExpandOpen);
        Check(!g.ExpandOpen && !cg.ExpandOpen && g.Expanded.Count == 3, "when the wave starts the round just closes, nothing changes");

        // Team vote building: no instant builds, no doors mid-wave.
        foreach (var gg in new[] { g, cg }) gg.Diff.Set(DiffKnob.BuildVote, 1f);
        Check(g.BuildVoting && !g.FreeBuild && !g.DoorsOpen && !g.CanCut, "with Team vote building on, doors mid-wave are off");
        cg.ProposeExpandCut(new WallCut { Vertical = true, At = 0, From = 300, Till = 600 });
        Run(s, dt, 0.4f);
        Check(g.Openings.Count == 1 && g.ExpandProposals.Count == 0, "and a mid-wave door is refused");
        foreach (var gg in new[] { g, cg }) gg.ExpandOpen = true;
        var o2 = Expansion.Offers(6, 6, g.Expanded);
        if (o2.Count > 0)
        {
            int before = g.Expanded.Count;
            cg.ProposeExpandRoom(o2[0].X, o2[0].Y, o2[0].W, o2[0].H);
            Run(s, dt, 0.5f, () => g.ExpandProposals.Count == 1);
            Check(g.ExpandProposals.Count == 1 && g.Expanded.Count == before, "a drawing waits for the vote");
        }
    }

    /// Team purse: a build can spend the whole team's vectors, the builder paying first then the richest teammate; off, only your own.
    static void ExpandingPurseTest(float dt)
    {
        string why;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.ExpandOpen = true; }
        var west = Expansion.Custom(g.Expanded, -800, 100, 800, 600, out why).Value;
        g.Players[2].Vectors = 5; g.Players[1].Vectors = 30;
        Check(west.Price > 5 && west.Price <= 35, "(the test room costs more than the friend holds: " + west.Price + ")");
        cg.ProposeExpandRoom(-800, 100, 800, 600);
        Run(s, dt, 0.4f);
        Check(g.Expanded.Count == 0, "without Team purse the friend can't afford it");
        foreach (var gg in new[] { g, cg }) gg.Diff.Set(DiffKnob.BuildPurse, 1f);
        Check(g.SpendableVectors(g.Players[2]) == 35, "Team purse counts everyone's vectors (" + g.SpendableVectors(g.Players[2]) + ")");
        cg.ProposeExpandRoom(-800, 100, 800, 600);
        Run(s, dt, 0.5f, () => cg.Expanded.Count == 1);
        Check(g.Expanded.Count == 1 && cg.Expanded.Count == 1, "with it the room is built");
        Check(g.Players[2].Vectors == 0 && g.Players[1].Vectors == 35 - west.Price, "the builder pays first, then the richest teammate (" + g.Players[2].Vectors + ", " + g.Players[1].Vectors + ")");
    }
}
