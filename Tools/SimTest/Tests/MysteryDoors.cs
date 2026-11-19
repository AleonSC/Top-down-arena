using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arena;

// Mystery doors (Docs/MysteryDoors.md step 1): sealed doors on the outer wall, a hidden room rolled behind each; pay to open, save one each.
static partial class Program
{
    static void MysteryDoorsTest(float dt)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Action<Game> roll = gg => typeof(Game).GetMethod("RollMysteryDoors", flags).Invoke(gg, null);
        Action<Game> tick = gg => typeof(Game).GetMethod("TickMysteryDoors", flags).Invoke(gg, null);

        // The roll: 1 or 2 new doors, both counts seen.
        var counts = Enumerable.Range(1, 200).Select(sd => Mystery.NewDoors(sd, 7)).ToList();
        Check(counts.All(c => c == 1 || c == 2) && counts.Contains(1) && counts.Contains(2), "a cleared wave rolls 1 or 2 new doors (seeded)");
        Check(Mystery.PriceForCells(1) == 9 && Mystery.PriceForCells(4) == 24, "a door costs by the cells behind it: 9 for one, 24 for four");

        string why = null;
        var list = GridMap(PieceShape.Mono);

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.RoomsUp = true; }
        UseMap(new[] { g, cg }, list);
        typeof(Game).GetField("expandSeed", flags).SetValue(g, 4242);
        var hp = g.Players[g.MyId]; var fp = g.Players[cg.MyId]; var cme = cg.Players[cg.MyId];
        Action<Player, Player, V2> place = (onHost, onClient, at) => { onHost.Pos = onHost.Disp = at; if (onClient != null) onClient.Pos = onClient.Disp = at; };

        // Rolled at a cleared wave: on the map's outer wall, no room behind yet, and the friend sees them.
        roll(g);
        PollBoth(host, client);
        var w0 = g.CurrentWorld;
        int n0 = g.MysteryDoors.Count;
        Check(n0 >= 1 && n0 <= 2, "the first roll puts 1-2 sealed doors on the map (" + n0 + ")");
        bool onWall = g.MysteryDoors.All(d => w0.Areas.Any(ar => ar.Contains(d.Pos, 2f)) && !w0.Doors.Any(x => x.Pos.CloserThan(d.Pos, 2f)));
        Check(onWall, "each sits on an existing room's wall, where no door is yet");
        Check(cg.MysteryDoors.Count == n0 && cg.MysteryDoors.Zip(g.MysteryDoors, (u, v) => u.X == v.X && u.Y == v.Y && u.Price == v.Price).All(x => x), "the friend gets the same doors and prices");
        Check(g.MysteryDoors.All(d => d.Price >= 1) && g.MysteryDoors.Select(d => d.Pos).Distinct().Count() == n0, "every door has a price and its own spot");

        // Too poor: nothing happens.
        var d0 = g.MysteryDoors[0];
        place(fp, cme, d0.Pos + new V2(0f, 0f));
        fp.Vectors = cme.Vectors = d0.Price - 1;
        int rooms0 = g.Expanded.Count;
        cg.UseMysteryDoor(false);
        PollBoth(host, client);
        Check(g.Expanded.Count == rooms0 && g.MysteryDoors.Count == n0 && fp.Vectors == d0.Price - 1, "without the vectors the door stays shut and nothing is charged");

        // Paying opens it: the room is built, the payer charged, the door gone, the friend told; the new room's door is at that spot.
        fp.Vectors = cme.Vectors = d0.Price + 5;
        cg.UseMysteryDoor(false);
        PollBoth(host, client);
        var w1 = g.CurrentWorld;
        Check(g.Expanded.Count > rooms0 && fp.Vectors == 5 && g.MysteryDoors.Count == n0 - 1, "paying " + d0.Price + " opens it: a room is built and the payer charged (" + fp.Vectors + " left)");
        Check(w1.Doors.Any(x => x.Pos.CloserThan(d0.Pos, 2f)), "the room's door is where the sealed door was");
        Check(g.Expanded.Skip(rooms0).All(r => r.Surprise == 0 && r.Owner == cg.MyId) && g.Expanded[rooms0].Price == d0.Price, "it is a bought room: the payer's, its price on it, not a gift");
        Check(cg.Expanded.Count == g.Expanded.Count && cg.MysteryDoors.Count == g.MysteryDoors.Count, "the friend gets the room and loses the door");

        // Saving: one door per player; a saved door outlives the next roll, the others expire.
        g.Wave = 7;
        roll(g);
        PollBoth(host, client);
        Check(g.MysteryDoors.Count >= 2 && g.MysteryDoors.Count <= Mystery.Cap, "the next roll tops the map up (" + g.MysteryDoors.Count + ", cap " + Mystery.Cap + ")");
        var a = g.MysteryDoors[0]; var b = g.MysteryDoors[1];
        place(hp, null, a.Pos);
        g.UseMysteryDoor(true);
        Check(g.MysterySavedBy(g.MyId) == 0, "the host saves a door");
        place(hp, null, b.Pos);
        g.UseMysteryDoor(true);
        Check(g.MysterySavedBy(g.MyId) == 1 && g.MysteryDoors.Count(x => x.Saver == g.MyId) == 1, "saving another moves the save: one per player");
        g.UseMysteryDoor(true);
        Check(g.MysterySavedBy(g.MyId) < 0, "saving your own again lets it go");
        g.UseMysteryDoor(true);
        place(fp, cme, a.Pos);
        cg.UseMysteryDoor(true);
        PollBoth(host, client);
        Check(g.MysteryDoors[0].Saver == cg.MyId && cg.MysteryDoors[0].Saver == cg.MyId, "the friend saves another, and sees it saved");
        place(hp, null, a.Pos);
        g.UseMysteryDoor(true);
        Check(g.MysteryDoors[0].Saver == cg.MyId, "a door someone else saved can't be taken over");
        var kept = new[] { a, b }.Select(x => x.Pos).ToList();
        g.Wave = 8;
        roll(g);
        PollBoth(host, client);
        Check(kept.All(k => g.MysteryDoors.Any(x => x.Pos.CloserThan(k, 1f) && x.Saver != 0)) && g.MysteryDoors.Count <= Mystery.Cap, "the saved doors outlive the roll, the rest expire (" + g.MysteryDoors.Count + " now)");
        Check(g.MysteryDoors.Count(x => x.Saver == 0) == Mystery.Cap - 2, "new doors only fill up to the cap");

        // The build map: a click buys a door from anywhere, but only while the build round is open.
        {
            var bd = g.MysteryDoors[g.MysteryDoors.Count - 1];
            place(fp, cme, new V2(Game.Width / 2f, Game.Height / 2f));
            fp.Vectors = cme.Vectors = bd.Price + 3; fp.VectorCap = cme.VectorCap = 500;
            int nd = g.MysteryDoors.Count, nr = g.Expanded.Count;
            g.ExpandOpen = false;
            cg.BuyMysteryDoor(cg.MysteryDoors.FindIndex(x => x.X == bd.X && x.Y == bd.Y));
            PollBoth(host, client);
            Check(g.MysteryDoors.Count == nd && g.Expanded.Count == nr && fp.Vectors == bd.Price + 3, "no build round open: a buy from the map does nothing");
            g.ExpandOpen = true;
            cg.BuyMysteryDoor(cg.MysteryDoors.FindIndex(x => x.X == bd.X && x.Y == bd.Y));
            PollBoth(host, client);
            Check(g.MysteryDoors.Count == nd - 1 && g.Expanded.Count > nr && fp.Vectors == 3 && cg.MysteryDoors.Count == nd - 1, "in a build round the friend buys a far door from the map: room built, " + bd.Price + " charged");
            g.ExpandOpen = false;
        }

        // A drawn room taking a door's space: the door is re-planned behind the same spot, or goes; never left over a room.
        for (int t = 0; t < 6; t++)
        {
            var free = g.MysteryDoors.FindIndex(x => x.Saver == 0);
            if (free < 0) break;
            var plans = (System.Collections.IList)typeof(Game).GetField("mysteryPlans", flags).GetValue(g);
            var plan = (SurpriseOption)plans[free];
            g.Expanded.AddRange(plan.Rooms.Select(r => { r.Surprise = 0; return r; }));
            typeof(Game).GetField("expWorld", flags).SetValue(g, null);
            tick(g);
        }
        var w2 = g.CurrentWorld;
        Check(g.MysteryDoors.All(x => !w2.Doors.Any(o => o.Pos.CloserThan(x.Pos, 2f))), "a door whose room was built over is re-planned or gone, never left in a doorway");

        // Mid-wave: a door opens during a fight too.
        g.Wave = 9;
        roll(g);
        g.State = MatchState.Fighting;
        if (g.MysteryDoors.Count > 0)
        {
            var m = g.MysteryDoors[0];
            place(hp, null, m.Pos);
            hp.Vectors = 500; hp.VectorCap = 500;
            int before = g.Expanded.Count;
            g.UseMysteryDoor(false);
            Check(g.Expanded.Count > before && hp.Vectors == 500 - m.Price, "a door opens mid-wave too, for its price");
        }
        else
            Check(false, "a door to open mid-wave");
    }
}

// Mystery doors' rooms (owner Nov 17): small or medium squares and rectangles (mostly enemies, sometimes empty; a small one can be a shop or
// a Black Market) and long hallways with a chest at the far end.
static partial class Program
{
    static void MysteryContentsTest(float dt)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;

        // The roll: the three rooms near their weights; what each holds.
        var rooms = new int[3]; var small = new int[7]; var medium = new int[7]; bool hallsChest = true, quiet = true;
        for (int sd = 1; sd <= 6000; sd++)
        {
            var r = Mystery.RoomFor(sd, 9, 0);
            rooms[(int)r]++;
            var k = Mystery.KindFor(sd, 9, 0, r, true);
            if (r == MysteryRoom.Small) small[(int)k]++;
            if (r == MysteryRoom.Medium) medium[(int)k]++;
            if (r == MysteryRoom.Hallway) hallsChest &= k == MysteryKind.Chest;
            var kq = Mystery.KindFor(sd, 9, 0, r, false);
            quiet &= r == MysteryRoom.Small ? kq == k : kq != MysteryKind.Enemies;
        }
        Check(rooms.All(c => c > 1500), "small, medium and hallway rooms all turn up (" + string.Join(" ", rooms) + ")");
        Check(small[(int)MysteryKind.Enemies] > 15 * small[(int)MysteryKind.Shop] && small[(int)MysteryKind.Empty] == 0 && small[(int)MysteryKind.Shop] > 0 && small[(int)MysteryKind.BlackMarket] > 0 && small[(int)MysteryKind.Arms] > 0 && small[(int)MysteryKind.Medic] > 0 && small[(int)MysteryKind.Chest] == 0,
            "a small room is mostly enemies, never empty, rarely a shop, a Black Market, an Arms Dealer or a Medic (" + string.Join(" ", small) + ")");
        Check(medium[(int)MysteryKind.Enemies] > 2 * medium[(int)MysteryKind.Empty] && medium[(int)MysteryKind.Empty] > 0 && medium[(int)MysteryKind.Shop] + medium[(int)MysteryKind.BlackMarket] + medium[(int)MysteryKind.Medic] + medium[(int)MysteryKind.Chest] == 0,
            "a medium room is mostly enemies, rarely empty, never a shop (" + string.Join(" ", medium) + ")");
        Check(hallsChest, "a hallway always holds a chest");
        Check(quiet, "round a boss no medium room holds enemies, but a small room keeps its pack");
        Check(Mystery.AmbushCount(5) == 5 && Mystery.AmbushCount(100) == Mystery.AmbushMax && Mystery.AmbushCount(5, MysteryRoom.Small) == Mystery.SmallPack && Mystery.SmallPack == 25, "enemies grow with the waves; a small room is a pack of 25");
        Check(Mystery.PiecesOf(MysteryRoom.Small).All(pc => pc == PieceShape.Mono) && Mystery.PiecesOf(MysteryRoom.Hallway).All(pc => pc == PieceShape.TriI || pc == PieceShape.TetI), "a small room is one cell, a hallway a straight line of cells");

        string why = null;
        var list = GridMap(PieceShape.Mono);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 8 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.RoomsUp = true; }
        UseMap(new[] { g, cg }, list);
        typeof(Game).GetField("expandSeed", flags).SetValue(g, 777);
        var hp = g.Players[g.MyId];
        hp.Vectors = hp.VectorCap = 999;

        // A real roll: hallways open at one end, nothing touching their sides.
        for (int w = 3; w < 40; w++)
        {
            g.Wave = w;
            typeof(Game).GetMethod("RollMysteryDoors", flags).Invoke(g, null);
        }
        typeof(Game).GetMethod("ResetMysteryDoors", flags).Invoke(g, null);

        // Puts a door of this room and kind on the map (the first spot that fits) and opens it. Returns the rooms it built and the door.
        MysteryDoor lastDoor = default(MysteryDoor);
        Func<PieceShape, MysteryKind, List<ExpansionRoom>> open = (piece, kind) =>
        {
            typeof(Game).GetMethod("ResetMysteryDoors", flags).Invoke(g, null);
            var opts = Expansion.GridMysteryOptions(g.Expanded, piece);
            foreach (var o in opts)
            {
                var args = new object[] { o, null, null };
                if (!(bool)typeof(Game).GetMethod("MysteryDoorOf", flags).Invoke(g, args)) continue;
                var d = (MysteryDoor)args[1];
                g.MysteryDoors.Add(d);
                ((List<SurpriseOption>)typeof(Game).GetField("mysteryPlans", flags).GetValue(g)).Add(o);
                ((List<V2>)typeof(Game).GetField("mysteryBehind", flags).GetValue(g)).Add((V2)args[2]);
                ((List<MysteryKind>)typeof(Game).GetField("mysteryKinds", flags).GetValue(g)).Add(kind);
                hp.Pos = hp.Disp = d.Pos;
                lastDoor = d;
                int before = g.Expanded.Count;
                g.UseMysteryDoor(false);
                PollBoth(host, client);
                return g.Expanded.Skip(before).ToList();
            }
            return new List<ExpansionRoom>();
        };
        Func<ExpansionRoom, V2> mid = r => new V2((r.X + r.X1) / 2f, (r.Y + r.Y1) / 2f);

        // A hallway: a line of four cells, its chest at the far end.
        int nodes0 = g.Nodes.Count;
        var hall = open(PieceShape.TetI, MysteryKind.Chest);
        var hr = hall.Count == 1 ? hall[0] : default(ExpansionRoom);
        Check(hall.Count == 1 && Math.Max(hr.W, hr.H) == 2400 && Math.Min(hr.W, hr.H) == 600, "a hallway is a line of four cells (" + hr.W + " x " + hr.H + ")");
        var chestNode = g.Nodes.Count == nodes0 + 1 ? g.Nodes.Last() : default(MapNode);
        float doorToChest = chestNode.Pos.DistanceTo(lastDoor.Pos);
        Check(g.Nodes.Count == nodes0 + 1 && chestNode.Kind == NodeKind.Chest && Expansion.NodeClaimed(g.Expanded, chestNode) && doorToChest > 600f, "its chest is away from the door (" + doorToChest.ToString("0") + " px from the door)");
        V2 chestSpot = g.NodeSpot(g.Nodes.Count - 1);  // (a claimed chest stands in the middle of the room that claims it)
        Check(g.Props.Values.Any(pr => pr.Kind == PropKind.Chest && pr.Pos.DistanceTo(chestSpot) < 2f), "the chest is there at once");

        var shop = open(PieceShape.Mono, MysteryKind.Shop);
        Check(shop.Count == 1 && shop[0].W == 600 && shop[0].H == 600 && shop[0].Kind == RoomKind.Shop && g.HasShopRoom, "a small room (one cell) can be a shop");
        var vendor = open(PieceShape.Mono, MysteryKind.BlackMarket);
        Check(vendor.Count == 1 && vendor[0].Kind == RoomKind.BlackMarket && g.HasBlackMarketRoom, "or a Black Market");
        var armsRoom = open(PieceShape.Mono, MysteryKind.Arms);
        Check(armsRoom.Count == 1 && armsRoom[0].Kind == RoomKind.Arms && g.HasArmsRoom, "or an Arms Dealer (Docs/ArmsDealer.md)");
        var medicRoom = open(PieceShape.Mono, MysteryKind.Medic);
        Check(medicRoom.Count == 1 && medicRoom[0].Kind == RoomKind.Medic && g.HasMedicRoom, "or a Medic");
        PollBoth(host, client);
        Check(cg.HasShopRoom && cg.HasBlackMarketRoom && cg.HasArmsRoom && cg.HasMedicRoom && cg.Nodes.Count == g.Nodes.Count, "the friend gets them too");

        // Enemies: mid-wave they appear in the room at once; between waves they wait for the wave to start. A small room holds fewer.
        g.State = MatchState.Fighting;
        g.Enemies.Clear();
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        var queueWas = new List<EnemyType>(queue);
        queue.Clear(); queue.Add(EnemyType.Rioter); queue.Add(EnemyType.Guard);  // (this wave's own types: a Warden wave)
        int e0 = g.Enemies.Count;
        var med = open(PieceShape.TetO, MysteryKind.Enemies);
        int want = Mystery.AmbushCount(g.Wave, MysteryRoom.Medium);
        int inRoom = g.Enemies.Values.Count(e => med.Any(r => e.Pos.X >= r.X - 20 && e.Pos.X <= r.X1 + 20 && e.Pos.Y >= r.Y - 20 && e.Pos.Y <= r.Y1 + 20));
        Check(med.Count == 1 && med[0].W == 1200 && med[0].H == 1200 && g.Enemies.Count - e0 == want && inRoom >= want, "mid-wave a medium room's " + want + " enemies appear in it (" + (g.Enemies.Count - e0) + " new, " + inRoom + " in the room)");
        var kinds = g.Enemies.Values.Select(e => e.Type).Distinct().ToList();
        Check(kinds.All(t => t == EnemyType.Rioter || t == EnemyType.Guard), "they're the wave's own types, not the old basic pool's Grunts (" + string.Join(", ", kinds) + ")");
        queue.Clear(); queue.AddRange(queueWas);
        g.State = MatchState.Intermission;
        e0 = g.Enemies.Count;
        var sm = open(PieceShape.Mono, MysteryKind.Enemies);
        var pend = (List<V2>)typeof(Game).GetField("ambushPending", flags).GetValue(g);
        Check(sm.Count == 1 && g.Enemies.Count == e0 && pend.Count == 1, "between waves they wait");
        typeof(Game).GetMethod("SpringPendingAmbushes", flags).Invoke(g, null);
        var pack = g.Enemies.Values.Where(e => !(e.Pos.X < sm[0].X - 20 || e.Pos.X > sm[0].X1 + 20 || e.Pos.Y < sm[0].Y - 20 || e.Pos.Y > sm[0].Y1 + 20)).ToList();
        Check(g.Enemies.Count - e0 == Mystery.SmallPack && pend.Count == 0 && pack.Count == Mystery.SmallPack && pack.All(e => e.Type == g.MysteryPackType()),
            "and come out as the wave starts: a small room's pack of " + Mystery.SmallPack + " " + g.MysteryPackType() + ", grouped in the room (" + pack.Count + ")");
        Check(Game.FactionFor(EnemyType.Warden).Members[0].Type == EnemyType.Rioter, "a Warden stretch's pack is Rioters");

        e0 = g.Enemies.Count;
        var empty = open(PieceShape.Domino, MysteryKind.Empty);
        Check(empty.Count == 1 && empty[0].W * empty[0].H == 2 * 600 * 600 && empty[0].Kind == RoomKind.Combat && g.Enemies.Count == e0 && ((List<V2>)typeof(Game).GetField("ambushPending", flags).GetValue(g)).Count == 0, "an empty room (7 x 6 here) is just floor");
    }
}

// Mystery doors step 4: bots open the doors they can pay for between waves, keeping a reserve back for building.
static partial class Program
{
    static void MysteryBotsTest(float dt)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;

        // One door, a bot host beside it: too poor (inside the reserve) it waits; with the vectors it opens it.
        string why = null;
        var list = GridMap(PieceShape.Mono);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, true, "Host", Role.Tank);
        var client = JoinAs(port, true, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.RoomsUp = true; }
        UseMap(new[] { g, cg }, list);
        typeof(Game).GetField("expandSeed", flags).SetValue(g, 99);
        Action hold = () => { g.State = MatchState.Intermission; g.StateTimer = 30f; foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        hold();
        typeof(Game).GetMethod("RollMysteryDoors", flags).Invoke(g, null);
        Run(s, dt, 0.5f, null, hold);
        Check(g.MysteryDoors.Count > 0, "a door to try (" + g.MysteryDoors.Count + ")");
        var d = g.MysteryDoors.OrderBy(x => x.Price).First();
        var hp = g.Players[g.MyId]; var fp = g.Players[cg.MyId]; var cme = cg.Players[cg.MyId];
        foreach (var p in new[] { hp, fp, cme }) { p.VectorCap = 999; p.Vectors = d.Price + Mystery.BotReserve - 1; }
        hp.Pos = hp.Disp = d.Pos + (d.Vertical ? new V2(-70f, 0f) : new V2(0f, -70f));
        if (g.CurrentWorld.AreaAt(hp.Pos) < 0) hp.Pos = hp.Disp = d.Pos - (hp.Pos - d.Pos);
        int rooms0 = g.Expanded.Count, doors0 = g.MysteryDoors.Count;
        Run(s, dt, 3f, null, hold);
        Check(g.Expanded.Count == rooms0 && g.MysteryDoors.Count == doors0, "a bot that would dip into its " + Mystery.BotReserve + "-vector reserve leaves the doors shut");
        foreach (var p in new[] { hp, fp, cme }) p.Vectors = 200;
        Run(s, dt, 12f, () => g.MysteryDoorsOpened > 0, hold);
        Check(g.MysteryDoorsOpened > 0 && g.Expanded.Count > rooms0, "with the vectors a bot walks to a door and opens it (" + g.MysteryDoorsOpened + " opened, " + g.MysteryVectorsSpent + " vectors)");
        Run(s, dt, 1f, null, hold);
        Check(cg.Expanded.Count == g.Expanded.Count && cg.MysteryDoors.Count == g.MysteryDoors.Count, "the friend's game agrees");

        // A run: bots from wave 1, the Expanding arena; the doors get opened along the way. The numbers for the balance pass.
        port = nextPort++;
        var h2 = HostAs(new Session { AutoStart = 2, StartWave = 1 }, port, true, "Host", Role.Tank);
        var c2 = JoinAs(port, true, "Friend", Role.Dps);
        var s2 = new[] { h2, c2 };
        WaitForGame(h2, c2, dt);
        var g2 = h2.Game; var cg2 = c2.Game;
        g2.DevSetGod(1, true); g2.DevSetGod(2, true);
        g2.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg2.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        Run(s2, dt, 900f, () => g2.Wave >= 10 && g2.State == MatchState.Fighting);
        Console.WriteLine("  by wave " + g2.Wave + ": " + g2.MysteryDoorsOpened + " doors opened for " + g2.MysteryVectorsSpent + " vectors, " + g2.Expanded.Count + " rooms in all, " + g2.MysteryDoors.Count + " doors waiting, vectors " + string.Join("/", g2.Players.Values.Select(p => p.Vectors.ToString()).ToArray()));
        Check(g2.Wave >= 10 && g2.MysteryDoorsOpened >= 1, "bots open mystery doors in a run (" + g2.MysteryDoorsOpened + " by wave " + g2.Wave + ")");
        Check(cg2.Expanded.Count == g2.Expanded.Count, "both games have the same map");
    }
}
