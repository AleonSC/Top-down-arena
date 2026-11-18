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
        var hubOnly = Expansion.Build(none);
        Check(hubOnly.Areas.Length == 1 && hubOnly.Doors.Length == 0 && hubOnly.Walls().Length == 4 && hubOnly.Max.X == Game.Width && hubOnly.Max.Y == Game.Height,
            "with nothing built the world is the hub alone, walled in");

        int coverSeen = 0, hallCover = 0, badCover = 0, loopDoors = 0, badWallGap = 0, badFit = 0, badTree = 0, badDoor = 0, badWall = 0, badSteer = 0, badCap = 0, worlds = 0, piecesSeen = 0;
        var pieceKinds = (GridPiece[])Enum.GetValues(typeof(GridPiece));
        for (int seed = 1; seed <= 60; seed++)
        {
            var built = Expansion.GridCore();  // (random Grid maps: pieces placed at random door spots until the cap)
            var rng = new Random(seed);
            for (int guard = 0; guard < 200; guard++)
            {
                var opts = Expansion.GridPieceOptions(built, pieceKinds[rng.Next(pieceKinds.Length)]);
                if (opts.Count == 0)
                {
                    if (Expansion.BoughtCount(built) >= Expansion.MaxRooms) break;
                    continue;
                }
                built.AddRange(opts[rng.Next(opts.Count)].Rooms);
                piecesSeen++;
            }
            if (Expansion.BoughtCount(built) > Expansion.MaxRooms) badCap++;
            worlds++;
            var w = Expansion.Build(built);
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
                        bool atDoor = w.Doors.Any(d => pt.DistanceTo(d.Pos) < Math.Max(Rooms.DoorW, d.Width) / 2f + 14f);  // (a piece's inner seams are open full width)
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
        Check(piecesSeen > 600, "pieces keep going down until the cap (" + piecesSeen + " over " + worlds + " maps)");
        Check(badCover == 0 && coverSeen > 60, "built rooms hold obstacles and bars of wall: inside their room clear of its walls, off every door, the same on every build (" + coverSeen + " over " + worlds + " maps, " + badCover + " bad)");
        Check(badWallGap == 0, "every wall is closed all the way along, even where a room is wider than the one it joins (" + badWallGap + " gaps)");
        Check(loopDoors > worlds * 3, "rooms touching along a door's width get a door, so loops form (" + loopDoors + " doors over " + worlds + " maps)");
        Check(badFit == 0, "rooms never overlap, never touch in a sliver, and every shared stretch of wall has its door (" + badFit + " bad)");
        Check(badTree == 0, "every map is connected (" + badTree + " bad)");
        Check(badDoor == 0 && badSteer == 0, "every door sits on both rooms' shared wall, is open, and has a steering spot each side (" + badDoor + "/" + badSteer + " bad)");
        Check(badWall == 0, "every room's middle is open ground in its own area (" + badWall + " bad)");
        Check(badCap == 0, "the map stops at " + Expansion.MaxRooms + " rooms and its size cap (" + badCap + " over)");

        var east = Expansion.GridPieceOptions(Expansion.GridCore(), GridPiece.Mono).First(o => o.Side == 3).Rooms[0];
        var owned = east; owned.Owner = 2;
        var ownedWorld = Expansion.Build(new List<ExpansionRoom>(Expansion.GridCore()) { owned });
        Check(ownedWorld.Areas[5].Owner == 2 && ownedWorld.Areas[0].Owner == 0, "a built room remembers who placed it (the hub nobody)");
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
                    string why = null;
                    if (Box(c[0], c[1], c[2], c[3]).HasValue && !found.Any(f => f[0] == c[0] && f[1] == c[1] && f[2] == c[2] && f[3] == c[3]))
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
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Action Quiet = () =>
        {
            ((IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
            ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
        };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, NoPicks);
        Quiet();

        // The setting: the arena is its walled world: the hub and the Grid core round it (GridRooms.cs).
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        Check(g.ArenaKind == ArenaLayout.Expanding && g.ExpandingOn, "the Expanding setting makes the Expanding arena");
        Run(s, dt, 1f, () => cg.Expanded.Count == 4);
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.3f);
        Check(g.RoomsOn && cg.RoomsOn && g.Walled && cg.Walled && !g.EdgesGated && g.CurrentWorld == g.ExpansionWorld && g.Expanded.Count == 4 && cg.Expanded.Count == 4 && g.Expanded.All(Expansion.IsGridCore),
            "the Expanding arena is walled (no wrap round the edges) and starts with its core, on both screens");

        // A building round opens: everyone sees it.
        openRound.Invoke(g, null);
        Run(s, dt, 3f, () => g.ExpandOpen && cg.ExpandOpen);
        Check(g.ExpandOpen && cg.ExpandOpen, "a cleared wave opens a building round on both screens");

        // Building: the friend plays a single cell; it goes up at once, theirs, free, and the round stays open.
        int clientBefore = g.Players[2].Vectors;
        var first = PlayPiece(s, dt, g, cg, GridPiece.Mono);
        Check(first.HasValue && g.Expanded.Count == 5 && g.Expanded[4].Owner == 2 && g.Players[2].Vectors == clientBefore && cg.Expanded.Count == 5 && cg.Expanded[4].Equals(g.Expanded[4]) && g.ExpandOpen,
            "a piece is built at once: the friend's, free, both screens agree, and the round stays open");
        resolveRound.Invoke(g, null);  // (what the next wave's start does)
        Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen);

        // The room comes up on the next regular wave, with its walls and a bigger crowd; boss waves stay in the middle.
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.4f);
        Check(g.RoomsOn && cg.RoomsOn && g.CurrentWorld.Areas.Length == 6 && g.ArenaMax.DistanceTo(g.CurrentWorld.Max) < 0.01f && cg.CurrentWorld.Areas.Length == 6 && g.Walled,
            "the built room is up on a regular wave: the hub, the core and the piece, walled, the same on the client");
        Check(g.LayoutOn && g.ActiveLayout.Name.Contains("Expanding") && g.ActiveLayout.Solids.Length >= g.CurrentWorld.Walls().Length, "its walls are part of the floor plan (" + g.ActiveLayout.Name + ")");
        Check(g.ExpansionScale > 1f && g.ExpansionScale <= Game.MaxExpandScaleEarly, "a bigger map means a bigger crowd (x" + g.ExpansionScale.ToString("0.00") + ")");

        // A teleport (Shadow Step, Blink) never lands in a gap between the rooms or in a wall: it backs up onto the floor.
        {
            var landing = typeof(Game).GetMethod("TeleportLanding", flags);
            var hubArea = g.CurrentWorld.Areas[g.CurrentWorld.Hub];
            V2 gap = V2.Zero; bool gapFound = false;
            for (float gx = g.ArenaMin.X + 30f; gx < g.ArenaMax.X - 30f && !gapFound; gx += 20f)
                for (float gy = g.ArenaMin.Y + 30f; gy < g.ArenaMax.Y - 30f && !gapFound; gy += 20f)
                {
                    var c = new V2(gx, gy);
                    if (!g.CurrentWorld.Areas.Any(a => a.Contains(c, 40f))) { gap = c; gapFound = true; }
                }
            V2 from = hubArea.Mid;
            V2 land = gapFound ? (V2)landing.Invoke(g, new object[] { from, gap, 14f }) : from;
            V2 wallAim = new V2(g.CurrentWorld.Max.X + 300f, hubArea.Mid.Y);  // (past the map's outer wall)
            V2 wallLand = (V2)landing.Invoke(g, new object[] { from, wallAim, 14f });
            Func<V2, bool> onFloor = q => g.CurrentWorld.Areas[g.CurrentWorld.AreaAt(q)].Contains(q) && g.PushOutOfProps(q, 14f).DistanceTo(q) < 0.5f;
            Check(gapFound && onFloor(land) && onFloor(wallLand) && wallLand.X < g.CurrentWorld.Max.X,
                "a teleport aimed into a gap between rooms or into a wall lands on the floor (gap " + gapFound + ")");
        }
        bool inRooms = true, inNew = false;
        // (A spot is the nearest a player of four valid ones, so it follows the players: sample once with everyone in the hub, once with a player by the new room's door.)
        var savedAt = g.Players.ToDictionary(kv => kv.Key, kv => kv.Value.Pos);
        for (int round = 0; round < 2; round++)
        {
            if (round == 1)
                { var nr = g.CurrentWorld.Areas[g.CurrentWorld.Areas.Length - 1]; foreach (var pl in g.Players.Values) pl.Pos = pl.Disp = nr.Mid; }  // (in the new room)
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
            var room = g.CurrentWorld.Areas[g.CurrentWorld.Areas.Length - 1];  // (the piece, out past the core)
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

        // Free building: both players build in the same round; a round with nothing placed closes with nothing built.
        int builtNow = g.Expanded.Count;
        openRound.Invoke(g, null);
        Run(s, dt, 3f, () => g.ExpandOpen && cg.ExpandOpen);
        PlayPiece(s, dt, g, cg, GridPiece.Mono);
        PlayPiece(s, dt, g, g, GridPiece.Domino);
        Check(g.Expanded.Count > builtNow + 1 && cg.Expanded.Count == g.Expanded.Count && g.ExpandOpen && g.Expanded.Skip(builtNow).Any(r => r.Owner == 2) && g.Expanded.Skip(builtNow).Any(r => r.Owner == 1),
            "both players build in one round, a single and a domino");
        resolveRound.Invoke(g, null);
        Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen);
        builtNow = g.Expanded.Count;
        openRound.Invoke(g, null);
        Run(s, dt, 3f, () => g.ExpandOpen && cg.ExpandOpen);
        resolveRound.Invoke(g, null);
        Run(s, dt, 3f, () => !g.ExpandOpen && !cg.ExpandOpen);
        Check(g.Expanded.Count == builtNow && !cg.ExpandOpen, "a round with nothing placed closes with nothing built");

        // A new boss cycle places nodes only (owner, Nov 18: no floating shop, vendor or Gatekeeper rooms; those come from room cards and mystery doors),
        // never fogged, the same on the client, adding no crowd and no spawns.
        var placeFloating = typeof(Game).GetMethod("PlaceFloatingRooms", flags);
        int boughtNow = Expansion.BoughtCount(g.Expanded), allNow = g.Expanded.Count, nodesNow = g.Nodes.Count;
        int waveHere = g.Wave;
        g.Wave = 11;
        float scale0 = g.ExpansionScale;
        placeFloating.Invoke(g, null);
        Run(s, dt, 3f, () => cg.Expanded.Count == g.Expanded.Count && cg.Nodes.Count == g.Nodes.Count);
        Check(g.Expanded.Count == allNow && cg.Expanded.Count == allNow && Expansion.BoughtCount(g.Expanded) == boughtNow, "a new boss cycle places no floating rooms (no shop, vendor or Gatekeeper)");
        var newNodes = g.Nodes.Skip(nodesNow).ToList();  // (a node is skipped when the grown map has no room left for it, the big rule node most often)
        int newCount = newNodes.Count;
        Func<MapNode, int> nodeOrder = n => n.Kind == NodeKind.Chest || n.Kind == NodeKind.VectorChest ? 0 : n.Kind == NodeKind.Relic ? 1 : n.Kind == NodeKind.Rule ? 2 : n.Kind == NodeKind.Vein ? 3 : 9;
        Check(!g.Nodes.Any(n => n.Kind == NodeKind.Rule) && g.Nodes.Count(n => Expansion.IsChest(n.Kind) || n.Kind == NodeKind.Relic || n.Kind == NodeKind.Vein) >= 1 && newNodes.All(n => nodeOrder(n) < 9) && newNodes.Select(nodeOrder).SequenceEqual(newNodes.Select(nodeOrder).OrderBy(k => k)) && !newNodes.Any(n => n.Kind == NodeKind.Tower)
              && newNodes.All(n => n.Kind != NodeKind.Rule || n.Rule < 6) && newNodes.All(n => !n.Hidden) && cg.Nodes.SequenceEqual(g.Nodes),
            "a new cycle places no rule node (owner Nov 18; the first cycle rings the hub with chests and relics), only chests, relics and veins as fit, none fogged, the same on the client");
        placeFloating.Invoke(g, null);
        Check(g.Expanded.Count == allNow && g.Nodes.Count == nodesNow + newCount, "the same cycle places them only once");
        // A sealed room waiting on the lattice (the cycle's mini-boss room, GridCards.cs): no crowd, no spawns, until a piece reaches it.
        {
            var mini = Expansion.GridMiniFor((int)typeof(Game).GetField("expandSeed", flags).GetValue(g), 1, g.Expanded, g.Nodes);
            Check(mini.HasValue, "a sealed mini-boss room fits on the lattice");
            if (mini.HasValue) g.Expanded.Add(mini.Value);
            typeof(Game).GetField("expWorld", flags).SetValue(g, null);
        }
        pickRooms.Invoke(g, null);
        var floatWorld = g.ExpansionWorld;
        Check(floatWorld.Areas.Skip(allNow + 1).All(a => a.Floating && a.Sealed) && Math.Abs(g.ExpansionScale - scale0) < 0.001f, "the room stays sealed and adds no crowd");
        {
            int si = g.Expanded.FindIndex(r => r.Floating);
            var reach = si >= 0 ? Expansion.GridPieceOptions(g.Expanded, GridPiece.Mono).Where(o => { var m = new List<ExpansionRoom>(g.Expanded); m.AddRange(o.Rooms); return !Expansion.Build(m).Areas[si + 1].Sealed; }).ToList() : new List<GridPieceOption>();
            Check(reach.Count > 0, "a single cell can be built into the cell between, reaching it");
            if (reach.Count > 0)
            {
                var open = new List<ExpansionRoom>(g.Expanded);
                open.AddRange(reach[0].Rooms);
                var sa = Expansion.Build(open).Areas[si + 1];
                Check(!sa.Sealed && sa.Kind == RoomKind.Gatekeeper && floatWorld.Areas[si + 1].Sealed, "and that opens its door");
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
            for (int cy = -1200; cy <= 1700 && !extra.HasValue; cy += 100)
                for (int cx = -1200; cx <= 2700 && !extra.HasValue; cx += 100)  // (anywhere touching the map: round the core, or beside an old save's sealed rooms)
                {
                    var cand = Box(cx, cy, 500, 500);
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
                Check(chests[0].Open && g.Nodes[ci].Spent && (chests[0].Chest != ChestKind.Upgrade || g.Loots.Values.Any(l => l.Owner == pl.Id)), "walking onto it opens it: the node is spent and pays its kind (" + chests[0].Chest + ")");
                Check(g.Props.Values.Count(q => q.Kind == PropKind.Chest && !q.Open && q.Pos.DistanceTo(chest.Pos) < 1f) == 0, "a spent node holds no second chest");
                g.Nodes.RemoveAt(ci);

                // A relic node (Relics.cs): walking up to it gives that player its relic; an old relic is left in the node.
                var relic = new MapNode { X = chest.X, Y = chest.Y, Kind = NodeKind.Relic };
                g.Nodes.Add(relic);
                int rni = g.Nodes.Count - 1;
                pl.Pos = relic.Pos + new V2(400f, 0f);
                tickNodes.Invoke(g, null);
                Check(!g.Nodes[rni].Spent && g.Players.Values.All(q => q.Relic == RelicId.None), "a relic node waits for someone to walk up");
                var wantRelic = Relics.ForNode(relic);
                pl.Pos = relic.Pos;
                tickNodes.Invoke(g, null);
                Check(g.Nodes[rni].Spent && wantRelic != RelicId.None && pl.Relic == wantRelic && g.Players.Values.All(q => q == pl || q.Relic == RelicId.None),
                    "walking onto it gives only that player its relic (" + Relics.Name(wantRelic) + ") and spends the node");
                // swapping: a player holding another relic leaves it in the node
                var swapRelic = wantRelic == RelicId.Backup ? RelicId.HallwayRunner : RelicId.Backup;
                relic = new MapNode { X = chest.X, Y = chest.Y, Kind = NodeKind.Relic };
                g.Nodes[rni] = relic;
                pl.Relic = swapRelic;
                tickNodes.Invoke(g, null);
                Check(!g.Nodes[rni].Spent && pl.Relic == wantRelic && Relics.Held(g.Nodes[rni]) == swapRelic, "taking a relic leaves your old one in the node");
                tickNodes.Invoke(g, null);
                Check(pl.Relic == wantRelic, "you can't swap straight back while standing on it");
                pl.Pos = relic.Pos + new V2(400f, 0f);
                tickNodes.Invoke(g, null);
                pl.Pos = relic.Pos;
                tickNodes.Invoke(g, null);
                Check(pl.Relic == swapRelic && Relics.Held(g.Nodes[rni]) == wantRelic, "walking away and back swaps again");
                var save2 = new RunSave { Wave = 6, HasExpansion = true, Nodes = { g.Nodes[rni] } }; save2.Seats.Add(new SeatSave());
                var back2 = RunSave.Parse(save2.ToText());
                Check(back2 != null && back2.Nodes.Count == 1 && back2.Nodes[0].Drop == g.Nodes[rni].Drop, "a relic left in a node survives a saved run");
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

        // Rolled relics, Backup and Hallway Runner (Relics.cs).
        {
            var rolled = new HashSet<RelicId>();
            for (int x = 0; x < 40; x++) rolled.Add(Relics.ForNode(new MapNode { X = (short)(x * 100 + 50), Y = (short)(x * 300 - 650), Kind = NodeKind.Relic }));
            Check(rolled.Count == Relics.Count && !rolled.Contains(RelicId.None), "relic nodes roll every relic across spots (" + rolled.Count + ")");
            var rn = new MapNode { X = 1250, Y = -650, Kind = NodeKind.Relic };
            var keepRel = g.Players.Values.ToDictionary(q => q.Id, q => q.Relic);
            Check(Relics.Held(rn) == Relics.ForNode(rn) && Relics.Held(new MapNode { X = rn.X, Y = rn.Y, Kind = NodeKind.Relic, Drop = (byte)((int)RelicId.Backup + 1) }) == RelicId.Backup, "a node holds the relic left in it, else the rolled one");
            var pl = g.Players.Values.First();
            pl.Relic = RelicId.Backup;
            float hp0 = pl.Hp;
            pl.Hp = 0f; pl.Downed = false;
            typeof(Game).GetMethod("DownIfDead", flags).Invoke(g, new object[] { pl, "test" });
            Check(!pl.Downed && pl.Hp >= pl.MaxHp * Relics.BackupHp - 1f && pl.Relic == RelicId.None, "Backup gets you straight back up at half health once, then it's gone");
            pl.Hp = 0f;
            typeof(Game).GetMethod("DownIfDead", flags).Invoke(g, new object[] { pl, "test" });
            Check(pl.Downed, "the next time you go down");
            typeof(Game).GetMethod("RevivePlayer", flags).Invoke(g, new object[] { pl, 1f, null });
            pl.Hp = hp0;
            var hallWorld = Expansion.Build(new List<ExpansionRoom> { new ExpansionRoom { Parent = 0, X = (int)Game.Width, Y = 400, W = 600, H = 300 } }, null, null, null);
            V2 inHall = new V2(Game.Width + 300f, 550f), inHub = new V2(Game.Width / 2f, Game.Height / 2f);
            Check(Relics.InHall(hallWorld, inHall) && !Relics.InHall(hallWorld, inHub), "a 3-tile strip is a hallway, the start room isn't");
            pl.Relic = RelicId.HallwayRunner;
            var speedAt = typeof(Game).GetMethod("RelicSpeedAt", flags);
            var keepPos = pl.Pos;
            pl.Pos = inHub;
            Check(Math.Abs((float)speedAt.Invoke(g, new object[] { pl }) - 1f) < 0.001f, "Hallway Runner adds nothing in a room");
            foreach (var q in g.Players.Values) q.Relic = keepRel[q.Id];
            pl.Pos = keepPos;
        }

        // Nodes survive a saved run (where they are placed: GridNodeFor, the gridrooms suite).
        {
            var saved = new RunSave { Wave = 6, HasExpansion = true };
            saved.Nodes.Add(new MapNode { X = -1250, Y = 450, Kind = NodeKind.Vendor });
            saved.Nodes.Add(new MapNode { X = 850, Y = 2150, Kind = NodeKind.Chest, Spent = true });
            saved.Nodes.Add(new MapNode { X = 2850, Y = -650, Kind = NodeKind.Tower, Hidden = true });
            saved.Seats.Add(new SeatSave());
            var back = RunSave.Parse(saved.ToText());
            Check(back != null && back.Nodes.Count == 3 && back.Nodes[0].Equals(saved.Nodes[0]) && back.Nodes[1].Equals(saved.Nodes[1]) && back.Nodes[2].Equals(saved.Nodes[2]), "nodes survive a saved run (a fogged scout tower too)");
        }

        // The map stops growing at the cap: no piece has a place once the rooms reach it.
        var full = Expansion.GridCore();
        while (Expansion.BoughtCount(full) < Expansion.MaxRooms)
        {
            var o = Expansion.GridPieceOptions(full, GridPiece.Mono);
            if (o.Count == 0) break;
            full.AddRange(o[0].Rooms);
        }
        Check(Expansion.BoughtCount(full) == Expansion.MaxRooms && Expansion.GridPieceOptions(full, GridPiece.Mono).Count == 0, "a full map takes no more pieces");
    }
}

static partial class Program
{
    // Bots play from wave 1 on the Expanding arena: they play pieces and the map grows; no round is left open.
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
        Run(s, dt, 1200f, () => g.Wave >= 6 && g.State == MatchState.Fighting);  // (a Grid run starts with its core: a bigger crowd, slower waves)
        Console.WriteLine("  by wave " + g.Wave + ": " + g.Expanded.Count + " rooms built (owners " + string.Join(",", g.Expanded.Select(r => r.Owner.ToString()).ToArray()) + "), vectors " + string.Join("/", g.Players.Values.Select(p => p.Vectors.ToString()).ToArray()));
        Check(g.Expanded.Count > 4 && cg.Expanded.Count == g.Expanded.Count, "bot players play pieces: the map grows past its core (" + g.Expanded.Count + " boxes)");
        Check(!g.ExpandOpen && !cg.ExpandOpen, "no building round is left open once the wave starts (" + g.State + ", wave " + g.Wave + ")");
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
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
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
            string why = null;
            var r = Box(x, y, w, h);
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
        int coinGold = g.Coins.Values.Where(c => !c.Vec).Sum(c => c.Value);  // (a rare vector pickup among them adds vectors, not gold)
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
        for (int k = 0; k < 5 && g.Enemies.Count > 0; k++) { Run(s, dt, 0.2f); Quiet(); }  // (what Quiet killed may leave something behind: a Splitting elite's Shards)
        Run(s, dt, 30f, () => g.State == MatchState.Intermission, NoPicks);
        Run(s, dt, 1f, () => cg.Players[1].Vectors == g.Players[1].Vectors, NoPicks);  // (the wave's vectors reach the client with the next snapshot)
        Check(g.State == MatchState.Intermission && g.ShopOpen && cg.ShopOpen, "the wave after a boss ends with the shop open, on both screens");
        Check(g.Players.Values.All(p => p.Shelf.Any(u => u >= 0)), "everyone has a shelf");
        Check(g.Players.Values.All(p => p.Vectors >= Game.VectorStart + Game.VectorWaveGain) && cg.Players[1].Vectors == g.Players[1].Vectors, "a cleared wave gives everyone " + Game.VectorWaveGain + " vectors, and the client sees them (" + g.Players[1].Vectors + ")");
        // The room shops: upgrades and restocks only in the shop room, sales in either room, heals and revives in neither (the Medic sells them), nothing in the hub.
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
        Check(pm.Gold == 1000 && pm.Hp < pm.MaxHp * 0.6f, "the vendor doesn't heal (only the Medic does)");
        pm.Pos = pm.Disp = shopAt;
        int gold1 = pm.Gold;
        g.Shop(ShopAction.Buy, slot);
        Check(pm.Gold < gold1, "the shop room sells upgrades");
        pm.Hp = pm.MaxHp * 0.5f;
        int gold2 = pm.Gold;
        g.Shop(ShopAction.Heal);
        Check(pm.Gold == gold2 && pm.Hp < pm.MaxHp * 0.6f, "nor does the shop room");
        g.Shop(ShopAction.BuyBuild, 0);
        Check(pm.Gold == gold2 && pm.Stash[0] == 0, "buildables are only sold at the vendor");
        g.Shop(ShopAction.Done, 1);
        client.Game.Shop(ShopAction.Done, 1);
        Run(s, dt, 0.5f);
        Check(!g.ShopOpen && !cg.ShopOpen, "everyone clicking Done closes the shop");
        Check(pm.Gold == gold1 - (gold1 - pm.Gold) && pm.Gold > 0, "closing the shop keeps your gold (" + pm.Gold + ")");

        // The cleared wave opened a building round; it doesn't hold the wave up, and it closes as the next wave starts.
        Check(g.ExpandOpen && cg.ExpandOpen && !g.ShopOpen, "the cleared wave opened a building round, on both screens");
        int rooms0 = Expansion.BoughtCount(g.Expanded);
        Run(s, dt, 0.4f);
        Check(g.ExpandOpen, "the round stays open while the countdown ticks on");
        typeof(Game).GetField("StateTimer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).SetValue(g, 0.05f);
        Run(s, dt, 30f, () => g.State == MatchState.Fighting, NoPicks);
        Run(s, dt, 0.6f);
        Check(!g.ExpandOpen && !cg.ExpandOpen && Expansion.BoughtCount(g.Expanded) == rooms0 && cg.Expanded.Count == g.Expanded.Count, "the round closes as the wave starts, the map kept on both screens");
    }
}

static partial class Program
{
    // The Gatekeeper room (GatekeeperRoom.cs, Docs/FloatingRooms.md step 3): walking in (in a wave or between waves) wakes the Gatekeeper and locks the room, the wave ends without
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
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, NoPicks);
        string why = null;
        var list = new List<ExpansionRoom>();
        var hall = Box(1600, 100, 800, 600);
        Check(hall.HasValue, "the connecting room fits: " + why);
        list.Add(hall.Value);
        var r = Box(2400, 100, 600, 600);
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
        Func<Game, int> keepers = gm => gm.Enemies.Values.Count(e => FactionMinis.Duels(e.Type) && e.Hp > 0f);
        Func<Game, int> area1 = gm => gm.CurrentWorld.AreaAt(gm.Players[1].Pos);
        var me = g.Players[1];
        me.Pos = me.Disp = new V2(800f, 450f);
        Run(s, dt, 0.5f, null, NoPicks);
        Check(keepers(g) == 0 && !g.AreaLocked(2), "nobody inside: the Gatekeeper sleeps and the room is open");
        me.Pos = me.Disp = g.CurrentWorld.Areas[2].Mid + new V2(0f, 100f);
        Run(s, dt, 0.8f, null, NoPicks);
        Check(keepers(g) == 1 && keepers(cg) == 1, "walking in wakes the mini-boss, for both screens");
        Check(g.Enemies.Values.Any(e => e.Type == EnemyType.RiotCaptain) && !g.Enemies.Values.Any(e => e.Type == EnemyType.Gatekeeper), "the room's mini-boss is its faction's (the Warden's Riot Captain), never the Data Gates' Gatekeeper");
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

        // Between waves: walking in wakes it too (a duel taken on early, or one missed in the wave), and the countdown waits for it.
        g.State = MatchState.Intermission;
        g.StateTimer = 30f;
        Run(s, dt, 0.3f, null, NoPicks);
        Check(g.State == MatchState.Intermission && keepers(g) == 0, "between waves, nobody inside: it sleeps (still between waves: " + g.State + ")");
        me.Pos = me.Disp = g.CurrentWorld.Areas[2].Mid + new V2(0f, 100f);
        Run(s, dt, 0.8f, null, NoPicks);
        Run(s, dt, 0.8f, null, NoPicks);
        Check(keepers(g) == 1 && keepers(cg) == 1 && g.AreaLocked(2), "between waves, walking in wakes the mini-boss and locks the room, for both screens");

        Check(g.Battle && g.DuelBetweenWaves && cg.DuelBetweenWaves, "a duel between waves counts as a fight, on both screens");
        {
            var mb = g.Enemies.Values.First(e => FactionMinis.Duels(e.Type));
            int shots0 = g.Bullets.Values.Count(b => b.Shooter != 0);
            float fire0 = mb.FireT; bool acted = false;
            for (int k = 0; k < 40 && !acted; k++) { Run(s, dt, 0.1f, null, NoPicks); acted = mb.FireT != fire0 || mb.Winding || g.Bullets.Values.Count(b => b.Shooter != 0) > shots0; }
            Check(acted, "between waves, the woken mini-boss attacks (its attack clock runs)");
        }
        g.StateTimer = 0.05f;
        Run(s, dt, 30f, () => g.State == MatchState.Fighting, NoPicks);
        Run(s, dt, 0.5f, null, NoPicks);
        Check(g.State == MatchState.Fighting && keepers(g) == 1 && g.AreaLocked(2), "a duel between waves doesn't hold up the next wave (ready still works): it carries on into it (" + g.State + ", " + keepers(g) + ")");

        {   // Health orbs drop in the duelRoom every so often during the fight, for both screens.
            var duelRoom = g.CurrentWorld.Areas[2];
            var seen0 = new HashSet<int>(g.Orbs.Keys);
            bool hostSaw = false, clientSaw = false;
            Run(s, dt, Game.DuelOrbEvery + 1f, null, () => {
                NoPicks();
                hostSaw |= g.Orbs.Values.Any(o => !seen0.Contains(o.Id) && duelRoom.Contains(o.Pos));
                clientSaw |= cg.Orbs.Values.Any(o => !seen0.Contains(o.Id) && duelRoom.Contains(o.Pos));
            });
            Check(hostSaw && clientSaw, "a duel drops health orbs in its room every " + Game.DuelOrbEvery + " s, on both screens (" + hostSaw + ", " + clientSaw + ")");
        }

        // Beating it: prize pick, ordinary room.
        Run(s, dt, 0.8f, null, NoPicks);
        Check(keepers(g) == 1, "walking in again wakes it again");
        int picks0 = g.Players.Values.Sum(p => p.PrizePicks);
        foreach (var e in g.Enemies.Values.Where(e => FactionMinis.Duels(e.Type))) e.Hp = 0f;
        Run(s, dt, 0.8f, null, NoPicks);
        Check(g.Players.Values.Sum(p => p.PrizePicks) == picks0 + g.Players.Count, "beating it gives everyone a prize pick");
        Check(g.CurrentWorld.Areas[2].Kind == RoomKind.Combat && cg.CurrentWorld.Areas[2].Kind == RoomKind.Combat && !g.AreaLocked(2), "the room is an ordinary combat room now, on both screens");

        // Won between waves: the prize is offered at once (no wave clear will), so it can be picked and Ready works after.
        {
            g.State = MatchState.Intermission; g.StateTimer = 30f;
            foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = p.PendingMilestones = p.PrizePicks = 0; p.LegendaryQueue.Clear(); p.Offer = null; p.Ready = p.AutoReady = false; }
            typeof(Game).GetMethod("GrantVaultPrize", flags).Invoke(g, new object[] { g.CurrentWorld.Areas[2].Mid });
            Check(g.Players.Values.All(p => p.Offer != null && (p.OfferPrize || p.OfferLegendary)), "a mini-boss beaten between waves offers its prize pick right away");
            var hostPick = typeof(Game).GetMethod("HostPick", flags);
            foreach (var p in g.Players.Values.ToList())
                for (int k = 0; k < 4 && p.Offer != null; k++) hostPick.Invoke(g, new object[] { p.Id, 0, 0 });
            g.SetMyReady(true);
            Check(g.Me.PrizePicks == 0 && g.Me.Ready, "after picking it, Ready works between waves");
        }

        // A rule node is a big area: only a room that encloses all of it claims it.
        {
            var rn = RuleNodeMap(out var rplan);
            Check(!Expansion.NodeEncloses(new ExpansionRoom { X = rn.X - 250, Y = rn.Y - 250, W = 500, H = 500 }, rn) && !Expansion.NodeEncloses(new ExpansionRoom { X = rn.X - 400, Y = rn.Y - 400, W = 800, H = 800 }, rn)
                && Expansion.NodeEncloses(new ExpansionRoom { X = rn.X - 450, Y = rn.Y - 450, W = 900, H = 900 }, rn), "a 5 x 5 or 8 x 8 room round a rule node does not claim it, a 9 x 9 room does (the whole area must be inside the walls)");
            Check(Expansion.NodeClaimed(rplan, rn), "a 2 x 2 cell piece round it claims it");
        }

        // A claimed rule room takes the rule's ground, and its prize comes out when a wave is cleared.
        {
            MapNode? rn = RuleNodeMap(out var plan);
            {
                var saveRooms = g.Expanded.ToList(); var saveNodes = g.Nodes.ToList();
                foreach (NodeRule rule in System.Enum.GetValues(typeof(NodeRule)))
                {
                    var nd = rn.Value; nd.Rule = (byte)rule;
                    var wld = Expansion.Build(plan, null, null, new List<MapNode> { nd });
                    var rule1 = wld.Areas.Skip(1).Where(a => a.Kind == RoomKind.Floor && a.Floor == Expansion.RuleFloor(nd.Rule)).ToList();
                    Check(rule1.Count >= 1, "a room round a " + rule + " node has " + Expansion.RuleFloor(nd.Rule) + " ground");
                }
                var wUn = Expansion.Build(plan.Take(plan.Count - 1).ToList(), null, null, new List<MapNode> { rn.Value });
                Check(wUn.Areas.All(a => a.Kind != RoomKind.Floor), "the map without it has no rule room");
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

        // No fog (owner, Nov 18): nodes are placed in plain view, and an old save's fogged node clears at once; gold veins (NodeScouting.cs).
        {
            var saveRooms = g.Expanded.ToList(); var saveNodes = g.Nodes.ToList();
            var tickFog = typeof(Game).GetMethod("TickNodeFog", flags);
            var payVeins = typeof(Game).GetMethod("PayVeins", flags);
            var tw = Expansion.GridNodeFor(5, 1, Expansion.GridCore(), new List<MapNode>(), NodeKind.Chest);
            Check(tw.HasValue && !tw.Value.Hidden, "a node is placed unfogged");
            if (tw.HasValue)
            {
                var pl = g.Players.Values.First();
                var homes = g.Players.Values.Select(p => p.Pos).ToList();
                V2 hub = new V2(Game.Width / 2f, Game.Height / 2f), dir = (tw.Value.Pos - hub) * (1f / (tw.Value.Pos - hub).Length);
                V2 farP = tw.Value.Pos + dir * 3600f;
                var old = new MapNode { X = (short)farP.X, Y = (short)farP.Y, Kind = NodeKind.Vein, Hidden = true };
                foreach (var gg in new[] { g, cg }) { gg.Expanded.Clear(); gg.Nodes.Clear(); typeof(Game).GetField("expWorld", flags).SetValue(gg, null); }
                g.Nodes.Add(old);
                tickFog.Invoke(g, new object[] { 1f });
                Check(!g.Nodes[0].Hidden, "an old save's fogged node far from everyone is revealed at once");
                Run(s, dt, 3f, () => cg.Nodes.Count == g.Nodes.Count && cg.Nodes.All(n => !n.Hidden));
                Check(cg.Nodes.Count == 1 && cg.Nodes.All(n => !n.Hidden), "the client sees it revealed too");
                pl.Pos = homes[0];
                // A gold vein pays every player each cleared wave while its room stands.
                var vn = Expansion.GridNodeFor(5, 0, Expansion.GridCore(), new List<MapNode>(), NodeKind.Vein);
                Check(vn.HasValue, "a gold vein node is placed");
                if (vn.HasValue)
                {
                    var vein = vn.Value; vein.Hidden = false;
                    var vp = GridChainTo(Expansion.GridCore(), vein, 3) ?? Expansion.GridCore();
                    var golds = g.Players.Values.ToDictionary(p => p.Id, p => p.Gold);
                    g.Expanded.Clear(); g.Expanded.AddRange(vp.Take(vp.Count - 1)); typeof(Game).GetField("expWorld", flags).SetValue(g, null);
                    g.Nodes.Clear(); g.Nodes.Add(vein);
                    payVeins.Invoke(g, null);
                    Check(g.Players.Values.All(p => p.Gold == golds[p.Id]), "an unclaimed gold vein pays nothing");
                    g.Expanded.Add(vp[vp.Count - 1]); typeof(Game).GetField("expWorld", flags).SetValue(g, null);
                    payVeins.Invoke(g, null);
                    Check(Expansion.NodeClaimed(vp, vein) && g.Players.Values.All(p => p.Gold == golds[p.Id] + NodeScouting.VeinPay(g.Wave)), "a claimed gold vein pays every player " + NodeScouting.VeinPay(g.Wave) + " gold as a wave is cleared");
                    foreach (var p in g.Players.Values) p.Gold = golds[p.Id];
                }
            }
            foreach (var gg in new[] { g, cg }) { gg.Expanded.Clear(); gg.Expanded.AddRange(saveRooms); gg.Nodes.Clear(); gg.Nodes.AddRange(saveNodes); typeof(Game).GetField("expWorld", flags).SetValue(gg, null); }
        }

        // A node's far room (a long hallway, then a room round it): an enemy there still walks all the way back to the players in the hub.
        {
            var node = Expansion.GridNodeFor(5, 0, Expansion.GridCore(), new List<MapNode>(), NodeKind.Chest);
            Check(node.HasValue, "a node for the path test");
            var far = (node.HasValue ? GridChainTo(Expansion.GridCore(), node.Value, 3) : null) ?? Expansion.GridCore();
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

    // Breaches (Docs/RoomBuilding2.md step 1): enemies come in through the outer wall, the same breaches on every machine.
    static void ExpandingBreachesTest(float dt)
    {
        string why = null;
        var list = new List<ExpansionRoom>();
        var east = Box(1600, 100, 800, 600).Value;  // a dead end off the hub's east wall
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
        var withMini = new List<ExpansionRoom>(Expansion.GridCore());
        var mini = Expansion.GridMiniFor(3, 0, withMini, new List<MapNode>());
        if (mini.HasValue)
        {
            withMini.Add(mini.Value);
            var ws = Expansion.Build(withMini);
            Check(Breaches.Candidates(ws).All(c => !ws.Areas[c.Key.Area].Floating), "a sealed room's walls never break");
        }

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 7 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        UseMap(new[] { g, cg }, GridMap(GridPiece.TriI));  // (a dead end east of the core)
        var first = new Dictionary<int, V2>();
        float earliest = float.MaxValue;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        int startUnits = -1;
        bool warned = false, clientWarned = false;
        Run(s, dt, 30f, () => g.State == MatchState.Fighting && g.BreachAge > 9f, () =>
        {
            foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; }
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
        {
            // A Campaign plays each wave twice: only its first leg breaches (the second would reopen them over their Stashes).
            bool lr = g.LongRun; int leg = g.Leg;
            g.LongRun = true; g.Leg = 0; bool firstLeg = g.BreachesOn;
            g.Leg = 1; bool secondLeg = g.BreachesOn, legs = g.LongLegs;
            g.LongRun = lr; g.Leg = leg;
            Check(firstLeg && (!legs || !secondLeg), "a Campaign wave breaches on its first leg only (legs " + legs + ", second leg " + secondLeg + ")");
        }
        // The flood on the same rolled wave (a fresh roll of the wave would be another random size to compare with).
        typeof(Game).GetMethod("BuildWave", flags).Invoke(g, new object[] { g.Wave });
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        var squads = (List<SquadDef>)typeof(Game).GetField("squadQueue", flags).GetValue(g);
        int plain = (int)typeof(Game).GetMethod("QueuedUnits", flags).Invoke(g, null);
        int plainBosses = queue.Count(t => Defs.Get(t).IsBoss);
        var plainSingles = queue.Where(t => !Defs.Get(t).IsBoss).GroupBy(t => t).ToDictionary(x => x.Key, x => x.Count());
        var plainSquads = squads.ToList();
        typeof(Game).GetMethod("FloodBreachWave", flags).Invoke(g, null);
        int flooded = (int)typeof(Game).GetMethod("QueuedUnits", flags).Invoke(g, null);
        bool tripled = queue.Count(t => Defs.Get(t).IsBoss) == plainBosses
            && queue.Where(t => !Defs.Get(t).IsBoss).GroupBy(t => t).All(x => plainSingles.ContainsKey(x.Key) && x.Count() == plainSingles[x.Key] * Breaches.Flood)
            && plainSquads.All(q => squads.Count(o => o == q) == Breaches.Flood * plainSquads.Count(o => o == q));
        Check(plain > 0 && tripled && flooded == (plain - plainBosses) * Breaches.Flood + plainBosses && startUnits > 0,
            "a breach wave brings " + Breaches.Flood + "x the enemies, no extra bosses (" + flooded + " vs " + plain + " for the plain wave; this one started with " + startUnits + ")");
    }

    // Shutter gates (Docs/RoomBuilding2.md step 3): a gate on a door, voted, synced; G shuts it for everyone for 8 s, then it recharges.
    static void ExpandingGatesTest(float dt)
    {
        string why = null;
        var list = new List<ExpansionRoom>();
        var east = Box(1600, 100, 800, 600).Value; list.Add(east);
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
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Expanded.AddRange(list); gg.RoomsUp = true; gg.ExpandOpen = true; }
        g.Players[2].Vectors = Gates.Price + 2;
        cg.ProposeExpandGate(door.Pos);
        Run(s, dt, 0.5f, () => cg.GateFits.Count == 1);
        Check(g.GateFits.Count == 1 && cg.GateFits.Count == 1 && g.Players[2].Vectors == 2, "a friend's gate is fitted at once on both screens and they pay");
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
        var eastL = Box(1600, 100, 800, 800).Value; loop.Add(eastL);
        var southL = Box(1100, 900, 1200, 400);
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
        string why = null;
        var list = new List<ExpansionRoom>();
        var east = Box(1600, 100, 800, 600).Value; list.Add(east);
        var south = Box(0, 900, 800, 600).Value; list.Add(south);
        var w = Expansion.Build(list);
        Func<RoomArea, List<V2>> wallSpots = a =>  // the grid spots on a room's outer wall (the wall faces into it)
        {
            var l = new List<V2>();
            for (float x = a.Min.X; x <= a.Max.X; x += 100f) { l.Add(new V2(x, a.Min.Y)); l.Add(new V2(x, a.Max.Y)); }
            for (float y = a.Min.Y; y <= a.Max.Y; y += 100f) { l.Add(new V2(a.Min.X, y)); l.Add(new V2(a.Max.X, y)); }
            return l.Where(p => { var side = Expansion.OuterWall(w, p); return side.HasValue && a.Contains(p + side.Value * 30f); }).ToList();
        };
        PortalLink link = default(PortalLink);
        int price = 0;
        foreach (var ea in wallSpots(w.Areas[1]))
            foreach (var sb in wallSpots(w.Areas[2]))
            {
                var l = new PortalLink { AX = (short)ea.X, AY = (short)ea.Y, BX = (short)sb.X, BY = (short)sb.Y };
                int pr = Expansion.LinkPrice(w, 0, l, out why);
                if (price == 0 && pr > 0) { link = l; price = pr; }
            }
        float walk = Expansion.LinkWalk(w, link);
        Check(price > 0 && walk > 1800f && price == Math.Min(Expansion.LinkMax, Expansion.LinkBase + (int)Math.Round(walk / 200f)), "a link goes on two rooms' outer walls and costs 8 + 1 per 2 tiles of walking it saves (" + (int)walk + " px, " + price + " vectors)");
        var same = wallSpots(w.Areas[1]).OrderByDescending(p => p.DistanceTo(link.A)).First();
        Check(Expansion.LinkPrice(w, 0, new PortalLink { AX = link.AX, AY = link.AY, BX = (short)same.X, BY = (short)same.Y }, out why) == 0 && why != null, "both ends in one room is refused (" + why + ")");
        Check(Expansion.LinkPrice(w, 0, new PortalLink { AX = link.AX, AY = link.AY, BX = 3000, BY = 400 }, out why) == 0 && why != null, "an end outside the map is refused (" + why + ")");
        V2 mid = new V2((float)Math.Round(w.Areas[2].Mid.X / 100f) * 100f, (float)Math.Round(w.Areas[2].Mid.Y / 100f) * 100f);
        Check(Expansion.LinkPrice(w, 0, new PortalLink { AX = link.AX, AY = link.AY, BX = (short)mid.X, BY = (short)mid.Y }, out why) == 0 && why != null && why.Contains("outer wall"), "an end in the middle of a room (off the walls) is refused (" + why + ")");
        Check(Expansion.LinkPrice(w, 0, new PortalLink { AX = 1600, AY = 200, BX = link.BX, BY = link.BY }, out why) == 0 && why != null, "an end on a wall two rooms share is refused (" + why + ")");
        Check(Expansion.LinkPrice(w, Expansion.MaxLinks, link, out why) == 0 && why != null, "at most " + Expansion.MaxLinks + " pairs");
        var wl = Expansion.Build(list, null, new List<PortalLink> { link });
        var pt = wl.Portals[0];
        V2 along = pt.InA.Orthogonal();
        var exit = wl.PortalExitFor(link.A + pt.InA * (16f + Rooms.WallT / 2f) + along * 40f, 16f);
        Check(wl.Portals.Length == 1 && pt.Wall && exit.HasValue && wl.AreaAt(exit.Value) == wl.AreaAt(pt.MouthB) && exit.Value.DistanceTo(link.B) < 120f, "built, the walls are a portal pair: walk into one, come out of the other (" + (exit.HasValue ? exit.Value.ToString() : "none") + ")");
        var exit2 = wl.PortalExitFor(link.A + pt.InA * (16f + Rooms.WallT / 2f) - along * 40f, 16f);
        Check(exit.HasValue && exit2.HasValue && Math.Abs(exit.Value.DistanceTo(exit2.Value) - 80f) < 1f, "and you keep your place along the wall");
        Check(!wl.PortalExitFor(link.A + pt.InA * 120f, 16f).HasValue && !wl.PortalExitFor(link.A + pt.InA * 30f + along * (Expansion.LinkHalf + 40f), 16f).HasValue, "not from across the room, or off the end of the portal's stretch");
        Check(Expansion.WalkDistance(wl, pt.MouthA, pt.MouthB) < 1f, "and walking between its ends now takes no time");
        Check(Expansion.LinkPrice(wl, 1, link, out why) == 0 && why != null && why.Contains("portal"), "a second pair on the first one's wall is refused (" + why + ")");
        var plan = wl.With(LayoutId.Open);
        Check(plan.Mouths != null && plan.Mouths.Length == 2, "enemies steer to each wall portal's mouth, then into the wall");
        Check(Breaches.Candidates(wl).All(c => c.Key.Pos.FartherThan(link.A, Expansion.LinkHalf + Breaches.Len / 2f) && c.Key.Pos.FartherThan(link.B, Expansion.LinkHalf + Breaches.Len / 2f)), "breaches never open on a portal's wall");
        var best = Expansion.BestLink(w, 0);
        Check(best.HasValue && Expansion.OuterWall(w, best.Value.A).HasValue && Expansion.OuterWall(w, best.Value.B).HasValue, "the bots' pick is on outer walls too");
        V2 fa = new V2(1900, 400), fb = new V2(400, 1200);  // an older save's pair: inside the rooms, a floor portal still
        var wf = Expansion.Build(list, null, new List<PortalLink> { new PortalLink { AX = (short)fa.X, AY = (short)fa.Y, BX = (short)fb.X, BY = (short)fb.Y } });
        var fexit = wf.PortalExitFor(fa);
        Check(!wf.Portals[0].Wall && fexit.HasValue && fexit.Value.DistanceTo(fb) < 120f, "an older save's pair in the middle of rooms still works as floor portals");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Expanded.AddRange(list); gg.ExpandOpen = true; }
        // The Black Market sells the teleporter: bought with gold into the stash, then placed (no vectors, no vote).
        g.Players[2].Vectors = 3;
        cg.ProposeExpandLink(link);
        Run(s, dt, 0.5f, () => false);
        Check(g.Links.Count == 0, "a portal pair is refused without a teleporter in the stash");
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
        Check(g.Links.Count == 1 && cg.Links.Count == 1 && g.Players[2].Vectors == 3 && g.Players[2].Stash[0] == 0, "a teleporter from the stash builds the pair at once on both screens, for no vectors");
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
        // An enemy in the east room after a player in the south room takes the wall portal: it walks to the mouth, into the wall, and out.
        var gw = g.ExpansionWorld;
        var wp = gw.Portals[0];
        int eastArea = gw.AreaAt(wp.MouthA), southArea = gw.AreaAt(wp.MouthB);
        foreach (var e in g.Enemies.Values) { e.Pos = e.Disp = new V2(-5000f, -5000f); }
        g.DevSpawnEnemy(1, EnemyType.Grunt, 1, false);
        var walker = g.Enemies.Values.OrderByDescending(e => e.Id).First();
        V2 startAt = wp.MouthA + wp.InA * 150f;
        walker.Pos = walker.Disp = startAt;
        foreach (var pl in g.Players.Values) { pl.Pos = pl.Disp = wp.MouthB + wp.InB * 200f; pl.Hp = pl.MaxHp = 99999f; }
        bool through = false;
        Run(s, dt, 8f, () => through = gw.AreaAt(walker.Pos) == southArea && walker.Pos.CloserThan(wp.B, 400f), () => { walker.Pos = walker.Pos; foreach (var pl in g.Players.Values) pl.Pos = pl.Disp = wp.MouthB + wp.InB * 200f; });
        Check(through, "an enemy chasing you through a wall portal walks into the wall and comes out of the other (" + gw.AreaAt(startAt) + " -> " + gw.AreaAt(walker.Pos) + ", at " + (int)walker.Pos.X + "," + (int)walker.Pos.Y + ")");
    }

    static bool Strip(RoomArea a) => Math.Min(a.Size.X, a.Size.Y) <= Expansion.StripMax;

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
        g.Coins.Clear();
        V2 killAt = p1.Pos + new V2(300f, 0f);
        grantKill.Invoke(g, new object[] { p1, killAt });
        Check(p1.Vectors == 10 && g.Coins.Count == 0, "kills before the " + Game.VectorKillEvery + "th give nothing yet");
        p1.Run.Kills = Game.VectorKillEvery;
        grantKill.Invoke(g, new object[] { p1, killAt });
        Check(p1.Vectors == 10 && g.Coins.Values.Count(c => c.Vec && c.Value == 1 && c.Pos.DistanceTo(killAt) < 40f) == 1, "the " + Game.VectorKillEvery + "th kill drops a 1-vector pickup where the enemy fell (not given straight away)");
        g.Coins.Clear();
        p1.Vectors = 11;
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
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.Expanded.Add(new ExpansionRoom { Parent = 0, X = 1600, Y = 100, W = 800, H = 600, Kind = RoomKind.Combat });
        g.Expanded.Add(new ExpansionRoom { Parent = 0, X = -800, Y = 100, W = 800, H = 600, Kind = RoomKind.Combat });
        string why = null;
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
        Check(g.RoomTokens.Count == 0, "a cleared boss wave gives no free kit either: kits come from the boss card pick (BossCards.cs)");
        typeof(Game).GetMethod("GrantRoomToken", flags).Invoke(g, new object[] { fl });
        Check(g.RoomTokens.Count == 1 && g.RoomTokens[0].Kind == RoomKind.Floor, "a granted Floor kit joins the team's kits");
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

    /// Test helper: the Grid core with a 2 x 2 cell piece east of it, and a rule node at that piece's middle (claimed by it).
    static MapNode RuleNodeMap(out List<ExpansionRoom> plan)
    {
        plan = GridMap(GridPiece.TetO);
        var r = plan[plan.Count - 1];
        int x0 = plan.Skip(4).Min(q => q.X), y0 = plan.Skip(4).Min(q => q.Y), x1 = plan.Skip(4).Max(q => q.X1), y1 = plan.Skip(4).Max(q => q.Y1);
        return new MapNode { X = (short)((x0 + x1) / 2), Y = (short)((y0 + y1) / 2), Kind = NodeKind.Rule };
    }

}
