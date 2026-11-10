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
                if (built.Count > 0 && pick.Price <= Expansion.PriceFor(pick.Kind, pick.W, pick.H, 0)) prevPriceBad++;
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
        Check(prevPriceBad == 0, "each room costs more than the same room would have at the start");

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
    }

    // The Expanding arena in a run (ExpansionGame.cs): the Arena setting, the purse, the vote on the offer, the bought
    // room coming up on both screens, the plain box until then and on boss waves, spawns inside rooms, a bigger crowd.
    static void ExpandingGameTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var pickRooms = typeof(Game).GetMethod("PickRooms", flags);
        var offerExpansion = typeof(Game).GetMethod("OfferExpansion", flags);
        var addPurse = typeof(Game).GetMethod("AddPurse", flags);
        var spawnEnemyAt = typeof(Game).GetMethod("SpawnEnemyAt", flags);
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

        // The setting: Expanding wins over Rooms, and until a room is bought the arena is the plain Classic box.
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.Diff.Set(DiffKnob.ArenaRooms, 1f);
        Check(g.ArenaKind == ArenaLayout.Expanding && g.ExpandingOn, "the Expanding setting wins over Rooms");
        g.Diff.Set(DiffKnob.ArenaRooms, 0f);
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.3f);
        Check(!g.RoomsOn && !cg.RoomsOn && g.CurrentWorld == World.Single && g.ArenaMin.X == 0f && g.ExpansionScale == 1f, "before the first room the Expanding arena is the plain box");

        // The purse: a kill pays the team.
        g.Purse = 0;
        var foe = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Grunt, new V2(800f, 450f), 0.9f, 0, false });
        addPurse.Invoke(g, new object[] { foe });
        Quiet();
        Check(g.Purse >= 1, "a kill pays the team purse (" + g.Purse + ")");

        // The offer: three rooms, the same on the client, the purse synced; nothing offered when nothing is affordable.
        g.Purse = 0;
        offerExpansion.Invoke(g, null);
        Check(g.ExpandOffer == null, "with an empty purse nothing is offered");
        g.Purse = 500;
        offerExpansion.Invoke(g, null);
        Run(s, dt, 0.4f);
        Check(g.ExpandOffer != null && g.ExpandOffer.Count == 3 && cg.ExpandOffer != null && cg.ExpandOffer.SequenceEqual(g.ExpandOffer) && cg.Purse == g.Purse && g.Purse >= 500, "the host offers three rooms, the client sees the same ones and the purse");

        // The vote: one vote isn't enough; both counted, the winner is bought and paid for from the purse on both screens.
        var first = g.ExpandOffer[0];
        g.CastExpandVote(0);
        Run(s, dt, 0.3f);
        Check(g.ExpandOffer != null && cg.ExpandVotes.ContainsKey(1), "one vote waits for the others (and the client sees who voted)");
        int purseBefore = g.Purse;
        cg.CastExpandVote(0);
        Run(s, dt, 0.4f);
        Check(g.ExpandOffer == null && cg.ExpandOffer == null && g.Expanded.Count == 1 && cg.Expanded.Count == 1 && cg.Expanded[0].Equals(first) && g.Purse >= purseBefore - first.Price && g.Purse <= purseBefore - first.Price + 60 && cg.Purse == g.Purse,
            "both vote for a room: it is bought, paid from the purse, and both screens have it");

        // The room comes up on the next regular wave, with its walls and a bigger crowd; boss waves stay in the middle.
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.4f);
        Check(g.RoomsOn && cg.RoomsOn && g.CurrentWorld.Areas.Length == 2 && g.ArenaMax.DistanceTo(g.CurrentWorld.Max) < 0.01f && cg.CurrentWorld.Areas.Length == 2 && g.Walled,
            "the bought room is up on a regular wave: two areas, walled, the same on the client");
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

        // A tie goes to the room, not to buying nothing; buying nothing keeps the purse.
        g.Purse = 500;
        int before = g.Expanded.Count;
        offerExpansion.Invoke(g, null);
        Run(s, dt, 0.3f);
        int skip = g.ExpandOffer.Count;
        g.CastExpandVote(skip);
        cg.CastExpandVote(0);
        Run(s, dt, 0.4f);
        Check(g.Expanded.Count == before + 1, "a tie between a room and buying nothing buys the room");
        g.Purse = 500;
        offerExpansion.Invoke(g, null);
        Run(s, dt, 0.3f);
        int n2 = g.ExpandOffer.Count;
        g.CastExpandVote(n2); cg.CastExpandVote(n2);
        Run(s, dt, 0.4f);
        Check(g.Expanded.Count == before + 1 && g.Purse >= 500 && cg.Purse == g.Purse && g.ExpandOffer == null, "everyone buying nothing keeps the purse and the map");

        // A drawn room: a player draws one and it joins the vote as a card; a bad or too dear box is refused; a newer one replaces it.
        g.Purse = 2000;
        offerExpansion.Invoke(g, null);
        Run(s, dt, 0.3f);
        int n0 = g.ExpandOffer.Count;
        g.CastExpandVote(n0);  // the host votes "buy nothing" first: its vote must move along when the card is added
        Run(s, dt, 0.2f);
        int dx = 0, dy = 0, dw = 300, dh = 600;
        bool found = false;
        for (int tx = -300; tx <= 1600 && !found; tx += 100)
        {
            string w1;
            if (Expansion.Custom(g.Expanded, tx, -dh, dw, dh, out w1).HasValue) { dx = tx; dy = -dh; found = true; }
        }
        Check(found, "there is somewhere to draw a hallway off the hub");
        cg.ProposeExpandRoom(100, 100, 300, 300);  // overlaps the hub
        Run(s, dt, 0.3f);
        Check(g.ExpandOffer.Count == n0 && g.ExpandCustom == -1, "a bad drawing is refused");
        cg.ProposeExpandRoom(dx, dy, dw, dh);
        Run(s, dt, 0.4f);
        Check(g.ExpandOffer.Count == n0 + 1 && g.ExpandCustom == n0 && cg.ExpandCustom == n0 && cg.ExpandOffer.Count == n0 + 1 && cg.ExpandOffer[n0].W == dw,
            "a drawn room joins the offer as its last card, on both screens");
        Check(g.ExpandVotes.ContainsKey(1) && g.ExpandVotes[1] == n0 + 1, "the \"buy nothing\" vote moved up a place");
        g.CastExpandVote(n0);  // the host switches to the drawn room (the client hasn't voted, so it stays open)
        Run(s, dt, 0.3f);
        cg.ProposeExpandRoom(dx, dy, dw + 100, dh);  // a newer drawing replaces it and its votes go
        Run(s, dt, 0.4f);
        Check(g.ExpandOffer.Count == n0 + 1 && g.ExpandOffer[n0].W == dw + 100 && !g.ExpandVotes.ContainsValue(n0), "a newer drawing replaces the old one and its votes");
        int purseKeep = g.Purse;
        g.Purse = 10;
        cg.ProposeExpandRoom(dx, dy, dw + 200, dh);
        Run(s, dt, 0.3f);
        Check(g.ExpandOffer[n0].W == dw + 100, "a drawing the purse can't pay for is refused");
        g.Purse = purseKeep;
        int beforeDraw = g.Expanded.Count;
        g.CastExpandVote(n0); cg.CastExpandVote(n0);
        Run(s, dt, 0.4f);
        Check(g.Expanded.Count == beforeDraw + 1 && g.Expanded[beforeDraw].X == dx && g.Expanded[beforeDraw].W == dw + 100 && cg.Expanded.Count == g.Expanded.Count, "both vote for it: the drawn room is bought, on both screens");
        var drawn = Expansion.Build(g.Expanded);
        Check(drawn.Doors.Length >= drawn.Areas.Length - 1, "and the map still connects");

        // The map stops growing at the cap.
        var full = new List<ExpansionRoom>();
        for (int i = 0; i < Expansion.MaxRooms; i++) full.Add(Expansion.Offers(3, 5 + 5 * i, full)[0]);
        Check(Expansion.Offers(3, 100, full).Count == 0, "a full map offers nothing more");
    }
}

static partial class Program
{
    // Bots play from wave 1 on the Expanding arena: the purse at the first milestone buys something, and bot players vote on their own.
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
        Run(s, dt, 400f, () => g.ExpandOffer != null || g.Wave >= 7);
        Console.WriteLine("  purse at the first milestone (wave " + g.Wave + "): " + g.Purse + (g.ExpandOffer != null ? ", offers " + string.Join(", ", g.ExpandOffer.Select(o => o.Shape + " " + o.Kind + " " + o.Price).ToArray()) : ", no offer"));
        Check(g.ExpandOffer != null, "the first boss clears with enough in the purse for a room");
        Run(s, dt, 20f, () => g.ExpandOffer == null);
        Check(g.ExpandOffer == null && cg.ExpandOffer == null, "bot players vote on their own and the offer closes");
        Console.WriteLine("  rooms bought: " + g.Expanded.Count + ", purse " + g.Purse);
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
        g.Purse = 3000;  // (this wave's clear, a moment on, finds the purse for a room)
        for (int i = 0; i < 4; i++) spawnEnemyAt.Invoke(g, new object[] { EnemyType.Grunt, hubAt + new V2(30f * i, 0f), 0f, 0, false });
        Quiet();
        Run(s, dt, 0.5f);
        Check(g.Players.Values.All(p => p.Gold > 0), "kills drop gold for everyone while the map has a shop room (" + g.Players[1].Gold + ")");
        Quiet();
        Run(s, dt, 30f, () => g.State == MatchState.Intermission, NoPicks);
        Check(g.State == MatchState.Intermission && g.ShopOpen && cg.ShopOpen, "the wave after a boss ends with the shop open, on both screens");
        Check(g.Players.Values.All(p => p.Shelf.Any(u => u >= 0)), "everyone has a shelf");
        host.Game.Shop(ShopAction.Done, 1);
        client.Game.Shop(ShopAction.Done, 1);
        Run(s, dt, 0.5f);
        Check(!g.ShopOpen && !cg.ShopOpen, "everyone clicking Done closes the shop");

        // The optional offer (a chip): a regular wave cleared with the purse for a room offers one that doesn't hold the countdown.
        var offerOptional = typeof(Game).GetMethod("OfferOptionalExpansion", flags);
        Check(g.ExpandOffer != null && g.ExpandOptional && cg.ExpandOffer != null && cg.ExpandOptional && !g.ExpandBlocksWave && cg.ExpandOffer.Count == g.ExpandOffer.Count,
            "a regular wave cleared with the purse for a room offers an optional one, on both screens, and the wave doesn't wait for it");
        int rooms0 = g.Expanded.Count;
        g.CastExpandVote(0);
        Run(s, dt, 0.3f);
        Check(g.ExpandOffer != null && g.Expanded.Count == rooms0, "one vote of two doesn't buy it by itself");
        typeof(Game).GetField("StateTimer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).SetValue(g, 0.05f);
        Run(s, dt, 30f, () => g.State == MatchState.Fighting, NoPicks);
        Run(s, dt, 0.4f);
        Check(g.ExpandOffer == null && cg.ExpandOffer == null && !cg.ExpandOptional && g.Expanded.Count == rooms0 && g.Purse >= 3000, "the offer closes as the wave starts: one vote of two isn't a majority, so nothing is bought and the purse is kept");
        Quiet();
        offerOptional.Invoke(g, null);
        Run(s, dt, 0.3f);
        Check(g.ExpandOffer != null && g.ExpandOptional, "another optional offer");
        g.CastExpandVote(0); cg.CastExpandVote(0);
        Run(s, dt, 0.4f);
        Check(g.Expanded.Count == rooms0 + 1 && g.ExpandOffer == null && cg.Expanded.Count == g.Expanded.Count, "both vote for a room: it is bought at once, countdown or not");
    }
}
