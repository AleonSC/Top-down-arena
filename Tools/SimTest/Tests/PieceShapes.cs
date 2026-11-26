using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Grid pieces (Docs/GridPieces.md step 1): polyominoes of 1 to 4 cells placed on the lattice at door spots, each an open room.
static partial class Program
{
    static void PieceShapesTest()
    {
        var expect = new Dictionary<PieceShape, int> { { PieceShape.Mono, 1 }, { PieceShape.Domino, 2 }, { PieceShape.TriI, 2 }, { PieceShape.TriL, 4 }, { PieceShape.TetI, 2 }, { PieceShape.TetO, 1 }, { PieceShape.TetT, 4 }, { PieceShape.TetS, 4 }, { PieceShape.TetL, 8 } };
        foreach (var kv in expect)
            Check(Expansion.PieceShapeOrients(kv.Key).Count == kv.Value, Expansion.PieceShapeName(kv.Key) + " has " + kv.Value + " distinct turns and mirrors (" + Expansion.PieceShapeOrients(kv.Key).Count + ")");
        Check(expect.Keys.Sum(k => Expansion.PieceShapeOrients(k).Count) == 28 && expect.Keys.All(k => Expansion.PieceShapeOrients(k).All(o => o.Length == Expansion.PieceShapeCells(k))), "28 orientations in all, each with the piece's cells");

        var core = Expansion.GridCore();
        string why = null;
        foreach (var kv in expect)
        {
            var opts = Expansion.PlacementsFor(core, kv.Key);
            bool aligned = opts.All(o => o.Rooms.All(r => (r.X - Expansion.GridX0) % 600 == 0 && (r.Y - Expansion.GridY0) % 600 == 0 && r.W % 600 == 0 && r.H % 600 == 0));
            bool free = opts.All(o => o.Rooms.All(r => r.Price == 0));
            bool openRoom = opts.All(o => { var m = new List<ExpansionRoom>(core); m.AddRange(o.Rooms); return Expansion.GroupOf(m, core.Count).Count == o.Rooms.Count && Expansion.BoughtCount(m) == 0 + 1; });
            Check(opts.Count > 0 && aligned && free && openRoom, Expansion.PieceShapeName(kv.Key) + ": " + opts.Count + " places round the core, every box on the lattice, free, one open room");
        }

        Check(Expansion.PieceShapeRooms(core, PieceShape.Domino, 0, 2100, 0, out why) == null && why == "Off the grid", "a piece off the lattice is refused (" + why + ")");
        Check(Expansion.PieceShapeRooms(core, PieceShape.Mono, 0, 5000 + 600 - 200 - 0, -400, out why) == null, "far from the map is refused (" + why + ")");
        Check(Expansion.PieceShapeRooms(core, PieceShape.Mono, 0, 200, -400, out why) == null && why.StartsWith("Overlaps"), "on top of the core is refused (" + why + ")");
        Check(Expansion.PieceShapeRooms(core, PieceShape.Mono, 99, 2000, -400, out why) == null && why == "No such turn", "no such turn is refused");

        // Gaps: two singles on the core's right wall leave one cell between them; a piece fits in it if it fits, and nothing overlaps what is built.
        var map = new List<ExpansionRoom>(core);
        foreach (int y in new[] { -400, 800 })
        {
            var r = Expansion.PieceShapeRooms(map, PieceShape.Mono, 0, 2000, y, out why);
            Check(r != null, "a single at (2000, " + y + ") (" + why + ")");
            if (r != null) map.AddRange(r);
        }
        Func<PieceShape, bool> coversGap = p => Expansion.PlacementsFor(map, p).Any(o => o.Rooms.Any(r => 2300 > r.X && 2300 < r.X1 && 500 > r.Y && 500 < r.Y1));
        Check(coversGap(PieceShape.Mono) && coversGap(PieceShape.Domino) && coversGap(PieceShape.TriI) && coversGap(PieceShape.TetI) && coversGap(PieceShape.TetL), "the gap takes a single, a domino, a line of 3, a line of 4 and an L");
        bool clean = true;
        foreach (PieceShape p in Enum.GetValues(typeof(PieceShape)))
            foreach (var o in Expansion.PlacementsFor(map, p))
                foreach (var r in o.Rooms)
                    foreach (var b in map)
                        clean &= !(r.X < b.X1 && b.X < r.X1 && r.Y < b.Y1 && b.Y < r.Y1);
        Check(clean, "no place for any piece overlaps a built room");
        Check(!Expansion.PlacementsFor(map, PieceShape.TetI).Any(o => o.Rooms.Any(r => r.X <= 2300 && 2300 < r.X1 && r.Y < 500 && 500 < r.Y1 && r.W == 600 && r.H > 600)), "and a line of 4 standing up can't squeeze into the one-cell gap");
    }

    static void PieceCardsTest()
    {
        bool ok = true;
        foreach (PieceShape p in Enum.GetValues(typeof(PieceShape)))
            foreach (var f in new[] { PathId.None, PathId.SwarmFields, PathId.Bazaar })
            {
                var c = Expansion.MakePieceCard(p, f);
                ok &= Expansion.IsShapePieceCard(c) && Expansion.ShapeOfPieceCard(c) == p && Expansion.PieceCardFloor(c) == f && Expansion.IsGridPieceCard(c) && (int)c < BossCards.KitBase;
            }
        Check(ok, "every piece, plain or on a floor, is a hand code that reads back (32 up, below the kit codes)");
        Check(!Expansion.IsShapePieceCard(PieceCard.Box) && !Expansion.IsGridPieceCard(PieceCard.Box) && !Expansion.IsGridPieceCard(PieceCard.MiniBoss) && Expansion.IsGridPieceCard(PieceCard.Shop) && Expansion.IsGridPieceCard(PieceCard.BuilderShop), "the old shape cards and the Mini-boss card aren't played in a Grid run; service cards are");
        Check(Expansion.PieceShapeOf(PieceCard.Market) == PieceShape.Mono && Expansion.PieceShapeOf(Expansion.MakePieceCard(PieceShape.TetL)) == PieceShape.TetL, "a service card is a single cell");
        Check(Expansion.CardName(Expansion.MakePieceCard(PieceShape.TriL)) == "Corner" && Expansion.CardName(Expansion.MakePieceCard(PieceShape.TetT, PathId.FrozenLake)).EndsWith(" T") && Expansion.CardPrice(Expansion.MakePieceCard(PieceShape.TetO)) == 24 && Expansion.CardPrice(Expansion.MakePieceCard(PieceShape.Mono, PathId.Sanctuary)) == 14,
            "names and prices: a cell is 6 vectors, a modifier 8 more");
        Check(PieceMods.Check() && PieceMods.All.Length >= 2 && PieceMods.Floor.Get(PieceMods.Floor.Set(0, (int)PathId.FrozenLake)) == (int)PathId.FrozenLake
            && Expansion.MakePieceCardMods(PieceShape.TetS, PieceMods.Floor.Set(0, (int)PathId.BloodMoon)) == Expansion.MakePieceCard(PieceShape.TetS, PathId.BloodMoon),
            "the modifier list: each owns its own bits, floors keep their old codes");
        int both = PieceMods.Combat.Set(PieceMods.Floor.Set(0, (int)PathId.BurningGrounds), 1);
        var two = Expansion.MakePieceCardMods(PieceShape.TetL, both);
        Check(Expansion.IsShapePieceCard(two) && Expansion.ShapeOfPieceCard(two) == PieceShape.TetL && Expansion.PieceCardMods(two) == both && Expansion.PieceCardFloor(two) == PathId.BurningGrounds && Expansion.PieceCardCombat(two)
            && Expansion.IsGridPieceCard(two) && (int)two < BossCards.KitBase && (int)two < 32768 && Expansion.CardName(two) == "Burning Grounds Combat L" && Expansion.CardPrice(two) == 24 + 16,
            "a card can carry a floor and combat together: it reads back, is playable, costs 8 a modifier (" + Expansion.CardName(two) + ")");
        var stale = (PieceCard)(Expansion.PieceCodeBase + (1 << (PieceMods.Bits - 1) << 4) + (int)PieceShape.Domino);
        Check(Expansion.PieceCardMods(stale) == 0 && Expansion.IsGridPieceCard(stale) && Expansion.ShapeOfPieceCard(stale) == PieceShape.Domino, "a modifier that isn't in the list (removed, or not added yet) is ignored: the card stays playable as plain");
        var modded = PieceMods.ApplyRoom(new ExpansionRoom { Kind = Arena.RoomKind.Combat }, both);
        var plainRoom = PieceMods.ApplyRoom(new ExpansionRoom { Kind = Arena.RoomKind.Combat }, 0);
        Check(modded.Kind == Arena.RoomKind.Floor && modded.Floor == PathId.BurningGrounds && plainRoom.Kind == Arena.RoomKind.Combat && Expansion.PieceCardMods(PieceCard.Shop) == 0, "a floor modifier makes the room a floor room; no modifier leaves it a combat room");
        int none = 0, flo = 0, com = 0, bo = 0;
        for (int i = 0; i < 1000; i++)
        {
            int m = PieceMods.Roll(new DetRandom(i * 17 + 3), 30);
            bool f = PieceMods.Floor.Get(m) != 0, c = PieceMods.Combat.Get(m) != 0;
            if (!f && !c) none++; else if (f && c) bo++; else if (f) flo++; else com++;
        }
        Check(none > 300 && flo > 100 && com > 100 && bo > 30 && PieceMods.Roll(new DetRandom(1), 1, true) != 0, "rolls: each modifier on its own chance, so a card gets none, one or both (" + none + " plain, " + flo + " floor, " + com + " combat, " + bo + " both)");
        var allPieces = new[] { MysteryRoom.Small, MysteryRoom.Medium, MysteryRoom.Hallway }.SelectMany(Mystery.PiecesOf).Distinct().Count();
        Check(allPieces == Enum.GetValues(typeof(PieceShape)).Length, "every piece shape can come from a mystery door (" + allPieces + " shapes)");
        var hand = Expansion.GridStartHand(5);
        Check(hand.Length == 1 && Expansion.IsShapePieceCard(hand[0]) && hand.SequenceEqual(Expansion.GridStartHand(5)), "the starting hand: one piece card (modifiers rolled), the same each time");
        Check(Enumerable.Range(0, 40).Select(s => Expansion.GridStartHand(s)[0]).Distinct().Count() > 3, "the starting piece varies with the seed");
        var so = Expansion.GridStartOffer(5);
        Check(so.Length == 2 && so.All(Expansion.IsShapePieceCard) && Expansion.ShapeOfPieceCard(so[0]) != Expansion.ShapeOfPieceCard(so[1]) && so.SequenceEqual(Expansion.GridStartOffer(5))
            && Enumerable.Range(0, 60).All(s2 => { var o = Expansion.GridStartOffer(s2); return Expansion.ShapeOfPieceCard(o[0]) != Expansion.ShapeOfPieceCard(o[1]); }), "the starting pick: two pieces of different shapes, the same each time");
        Check(Expansion.ShapeOfPieceCard(Expansion.GridRoundCard(new DetRandom(4), 1)) == PieceShape.Domino && Expansion.ShapeOfPieceCard(Expansion.GridRoundCard(new DetRandom(4), 2)) == PieceShape.Mono, "the round card: a domino after odd waves, a single after even");
        int service = 0, themed = 0, fighting = 0, tet = 0, pieces = 0, bad = 0;
        for (int i = 0; i < 600; i++)
        {
            var c = Expansion.GridDropCard(new DetRandom(i * 37 + 1), 30);
            if (!Expansion.IsGridPieceCard(c)) bad++;
            if (!Expansion.IsShapePieceCard(c)) { service++; continue; }
            pieces++;
            if (Expansion.PieceCardFloor(c) != PathId.None) themed++;
            if (Expansion.PieceCardCombat(c)) fighting++;
            if (Expansion.PieceShapeCells(Expansion.ShapeOfPieceCard(c)) == 4) tet++;
        }
        Check(bad == 0 && service >= 80 && service <= 160 && themed >= 80 && themed <= 220 && fighting >= 80 && fighting <= 220 && tet >= 120, "drops: about 1 in 5 a service card, about 3 in 10 pieces on a floor and 3 in 10 Combat, a third tetrominoes (" + service + " service, " + themed + " floor, " + fighting + " combat, " + tet + " of " + pieces + " pieces)");
        var shelf = Expansion.GridBuilderOffers(7, 3);
        Check(shelf.Length == Expansion.BuilderOfferCount && shelf.All(Expansion.IsGridPieceCard) && Expansion.BuilderPrice(shelf[0]) < Expansion.CardPrice(shelf[0]), "the Builder Shop's five are grid cards at a quarter off");
        Check(Expansion.PieceCardMods(shelf[4]) != 0 && Enumerable.Range(0, 30).All(w => Expansion.PieceCardMods(Expansion.GridBuilderOffers(w, 4)[4]) != 0), "the Builder Shop's last card always carries a modifier");
        var spoils = BossCards.Offer(new DetRandom(3), Expansion.GridCore(), new List<RoomToken>(), 12);
        Check(spoils.Length == BossCards.OfferSize && spoils.Distinct().Count() == spoils.Length && spoils.All(c => BossCards.IsKit(c) || Expansion.IsGridPieceCard((PieceCard)c)) && !spoils.Contains((int)PieceCard.MiniBoss), "a Grid boss pick: pieces, service cards and floor kits, no Mini-boss card");
        Check(new Difficulty().ToText() == "Normal" && Difficulty.Parse("Normal").SameAs(new Difficulty()), "a preset saves as its name alone (the retired building knobs aren't written)");
        var save = new RunSave();
        var seat = new SeatSave { Vectors = 30, VectorCap = 70 };
        seat.Hand.AddRange(new[] { (int)Expansion.MakePieceCard(PieceShape.TetL, PathId.Fogbound), (int)Expansion.MakePieceCard(PieceShape.Mono), (int)PieceCard.Shop });
        save.Seats.Add(seat);
        var back = RunSave.Parse(save.ToText());
        Check(back.Seats.Count == 1 && back.Seats[0].Hand.SequenceEqual(seat.Hand), "a hand of pieces is saved and read back");
    }

    // Step 4: a mini-boss room waits sealed on the lattice, two cells, one cell out from the map; a piece into the cell between opens it.
    static void GridMiniTest()
    {
        var core = Expansion.GridCore();
        var mini = Expansion.GridMiniFor(9, 0, core, new List<MapNode>());
        Check(mini.HasValue, "a mini-boss room finds a spot off the core");
        if (!mini.HasValue) return;
        var m = mini.Value;
        bool lattice = (m.X - Expansion.GridX0) % 600 == 0 && (m.Y - Expansion.GridY0) % 600 == 0 && (m.W == 1200 && m.H == 600 || m.W == 600 && m.H == 1200);
        Check(m.Floating && m.Kind == RoomKind.Gatekeeper && lattice && Expansion.GridMiniFor(9, 0, core, new List<MapNode>()).Value.Equals(m), "it is two cells on the lattice, a floating mini-boss room, the same each time (" + m.X + "," + m.Y + " " + m.W + "x" + m.H + ")");
        var map = new List<ExpansionRoom>(core) { m };
        Check(Expansion.GridMinisWaiting(map) == 1 && Expansion.BoughtCount(map) == 0 && !core.Any(c => c.X < m.X1 + 1 && m.X - 1 < c.X1 && c.Y < m.Y1 + 1 && m.Y - 1 < c.Y1), "it waits sealed, apart from the map, and isn't a bought room");
        Check(!Expansion.GridSpots(map).Any(sp => sp.X >= m.X - 600 && sp.X < m.X1 && sp.Y >= m.Y - 600 && sp.Y < m.Y1 && (sp.SX == m.X || sp.SX == m.X1 || sp.SY == m.Y || sp.SY == m.Y1)), "a sealed room has no door spots of its own");
        var bridge = Expansion.PlacementsFor(map, PieceShape.Mono).FirstOrDefault(o => o.Rooms.Any(r => (r.X1 == m.X || r.X == m.X1) && r.Y < m.Y1 && m.Y < r.Y1 || (r.Y1 == m.Y || r.Y == m.Y1) && r.X < m.X1 && m.X < r.X1));
        Check(bridge.Rooms != null, "a single fits in the cell between the map and the mini-boss room");
        if (bridge.Rooms == null) return;
        map.AddRange(bridge.Rooms);
        var w = Expansion.Build(map);
        int area = core.Count + 1;
        Check(Expansion.GridMinisWaiting(map) == 0 && !w.Areas[area].Sealed && w.Areas[area].Kind == RoomKind.Gatekeeper, "built, it joins the map: the mini-boss room is open");
        Check(Expansion.GridSpots(map).Any(sp => sp.SX == m.X || sp.SX == m.X1 || sp.SY == m.Y || sp.SY == m.Y1), "and its walls get door spots");
    }

    // Owner Nov 18: a Grid run's boss surprise rooms and breach rooms (Stashes, the gold breach's room, the Engineer's Breach) sit on the lattice.
    static void GridSurpriseTest()
    {
        var core = Expansion.GridCore();
        Func<ExpansionRoom, bool> onLattice = r => (r.X - Expansion.GridX0) % 600 == 0 && (r.Y - Expansion.GridY0) % 600 == 0 && r.W % 600 == 0 && r.H % 600 == 0;
        var stash = Expansion.GridSurpriseOptions(core, SurpriseShape.Stash, new V2(2100f, 500f));
        Check(stash.Count > 0 && stash.All(o => o.Rooms.Count == 1 && o.Rooms[0].W == 600 && o.Rooms[0].H == 600 && onLattice(o.Rooms[0]) && o.Rooms[0].X < 2100 && 2100 < o.Rooms[0].X1 && o.Rooms[0].Surprise == 1 + (int)SurpriseShape.Stash && o.Rooms[0].Price == 0),
            "a Stash behind a breach on the core's right wall is the cell behind it, free, marked a Stash (" + stash.Count + ")");
        foreach (var shape in new[] { SurpriseShape.Ring, SurpriseShape.Cross, SurpriseShape.LHall, SurpriseShape.THall, SurpriseShape.Chamfer })
        {
            string why = null;
            var pick = Expansion.SurpriseFor(5, 11, core, shape, out why);
            int area = pick.HasValue ? pick.Value.Rooms.Sum(r => r.W * r.H) : 0;
            Check(pick.HasValue && pick.Value.Rooms.All(onLattice) && area == 4 * 600 * 600 && pick.Value.Rooms.All(r => r.Surprise == 1 + (int)shape) && Expansion.SurpriseFor(5, 11, core, shape, out why).Value.X == pick.Value.X,
                "a boss's " + Expansion.SurpriseName(shape) + " gift is a 4-cell piece on the lattice, the same each time");
        }
    }

    static void PieceShapesGameTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg })
        {
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
            gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true;
        }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);
        Check(g.Expanded.Count == 4 && cg.Expanded.Count == 4, "the core is up first");

        // The first build round deals the hand: the starting pick (one of two pieces), then a wave card pick (the round's basic piece or a dropped card), and the friend's game has them.
        foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = p.WavePicks = 0; p.LegendaryQueue.Clear(); p.Offer = null; }
        typeof(Game).GetMethod("OpenBuildRound", flags).Invoke(g, null);
        var friend = g.Players[2];
        var fought = g.State;
        g.State = MatchState.Intermission;  // (picks only count in a break; called on the host directly, so the round isn't ticked through)
        Check(Game.HandCount(friend) == 0 && friend.OfferCards && friend.OfferWave && friend.OfferStart && friend.StartPick && friend.PendingCards == 2 && friend.WavePicks == 1 && friend.Offer.Length == 2
            && friend.Offer.All(c => Expansion.IsShapePieceCard((PieceCard)c)) && friend.Offer[0] != friend.Offer[1], "the first round offers the starting pick first: two pieces (" + string.Join(",", friend.Offer.Select(BossCards.Name)) + ")");
        friend.Rerolls = 3;
        g.HostReroll(2);
        Check(friend.Rerolls == 3 && friend.OfferStart, "the starting pick can't be rerolled");
        int wantStart = friend.Offer[1];
        typeof(Game).GetMethod("HostPick", flags).Invoke(g, new object[] { 2, 1, 0 });
        Check(Game.HandCount(friend) == 1 && friend.Hand[0] == wantStart && !friend.StartPick && !friend.OfferStart && friend.PendingCards == 1 && friend.WavePicks == 1, "the picked piece starts the hand (" + BossCards.Name(wantStart) + ")");
        Check(friend.OfferCards && friend.OfferWave && friend.PendingCards == 1 && friend.WavePicks == 1 && friend.Offer.Length == 2
            && Expansion.IsShapePieceCard((PieceCard)friend.Offer[0]) && Expansion.ShapeOfPieceCard((PieceCard)friend.Offer[0]) == (g.Wave % 2 == 1 ? PieceShape.Domino : PieceShape.Mono) && friend.Offer[1] != friend.Offer[0], "and a wave pick of two: the round's basic piece and a dropped card (" + string.Join(",", friend.Offer.Select(BossCards.Name)) + ")");
        g.HostReroll(2);
        Check(friend.Rerolls == 3 && friend.OfferWave, "a wave pick can't be rerolled");
        int wantWave = friend.Offer[1];
        typeof(Game).GetMethod("HostPick", flags).Invoke(g, new object[] { 2, 1, 0 });
        Check(friend.PendingCards == 0 && friend.WavePicks == 0 && !friend.OfferWave && Game.HandCount(friend) == 2 && friend.Hand[1] == (int)wantWave, "the picked card goes to the hand (" + BossCards.Name(wantWave) + ")");
        g.State = fought;
        Run(s, dt, 1f, () => cg.ExpandOpen && Game.HandCount(cg.Players[2]) == 2, noPicks);
        Check(cg.Players[2].Hand.SequenceEqual(friend.Hand), "the friend's game sees the same hand");

        // Playing a card: built at once as its piece, free, the card leaves the hand.
        int vec = friend.Vectors;
        var card = (PieceCard)friend.Hand[0];
        var opt = Expansion.PlacementsFor(g.Expanded, Expansion.ShapeOfPieceCard(card)).First();
        cg.PlayPieceCard(0, opt.Orient, opt.X, opt.Y);
        Run(s, dt, 1f, () => cg.Expanded.Count == 4 + opt.Rooms.Count, noPicks);
        Check(g.Expanded.Count == 4 + opt.Rooms.Count && cg.Expanded.Count == g.Expanded.Count && g.Expanded.Skip(4).All(r => r.Owner == 2 && r.Kind == Arena.RoomKind.Combat), "the friend's piece is built at once on both screens, all its boxes theirs");
        Run(s, dt, 0.5f, () => Game.HandCount(cg.Players[2]) == 1, noPicks);
        Check(Game.HandCount(friend) == 1 && Game.HandCount(cg.Players[2]) == 1 && friend.Vectors == vec && Expansion.BoughtCount(g.Expanded) == 1, "the card leaves the hand, it cost nothing, and the open room counts as one room");

        // Refusals keep the card: a spot off the lattice, an empty slot, a card that isn't a grid card.
        int n = g.Expanded.Count;
        var held = friend.Hand.ToArray();
        cg.PlayPieceCard(0, 0, opt.X + 100, opt.Y);
        cg.PlayPieceCard(5, 0, opt.X, opt.Y);
        friend.Hand[1] = (int)PieceCard.Box;
        cg.PlayPieceCard(1, 0, opt.X, opt.Y);
        Run(s, dt, 0.6f, null, noPicks);
        Check(g.Expanded.Count == n && friend.Hand[0] == held[0] && friend.Hand[1] == (int)PieceCard.Box, "an off-lattice spot, an empty slot and an old Box card are refused, the cards stay");

        // A floor piece is a floor room; a service card is one cell with its job.
        friend.Hand[0] = (int)Expansion.MakePieceCard(PieceShape.Domino, PathId.BurningGrounds);
        var fo = Expansion.PlacementsFor(g.Expanded, PieceShape.Domino).First();
        cg.PlayPieceCard(0, fo.Orient, fo.X, fo.Y);
        Run(s, dt, 1f, () => g.Expanded.Count > n, noPicks);
        var made = g.Expanded.Skip(n).ToList();
        Check(made.Count > 0 && made.All(r => r.Kind == Arena.RoomKind.Floor && r.Floor == PathId.BurningGrounds), "a Burning Grounds domino builds a floor room");
        friend.Hand[0] = (int)PieceCard.Shop;
        int n2 = g.Expanded.Count;
        var so = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).First();
        cg.PlayPieceCard(0, 0, so.X, so.Y);
        Run(s, dt, 1f, () => g.Expanded.Count > n2, noPicks);
        var shop = g.Expanded.Last();
        Check(g.Expanded.Count == n2 + 1 && shop.Kind == Arena.RoomKind.Shop && shop.W == 600 && shop.H == 600, "a Shop card builds one cell as a shop");

        // A stall card (service card or Builder Shop) can't go on a chest, vector chest or relic node; a plain piece can.
        var spot = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).First();
        var relic = new MapNode { X = (short)spot.Mid.X, Y = (short)spot.Mid.Y, Kind = NodeKind.Relic };
        g.Nodes.Add(relic);
        bool listed = Expansion.PlacementsForCard(g.Expanded, g.Nodes, PieceCard.Market).Any(o => o.X == spot.X && o.Y == spot.Y);
        bool plainListed = Expansion.PlacementsForCard(g.Expanded, g.Nodes, Expansion.MakePieceCard(PieceShape.Mono)).Any(o => o.X == spot.X && o.Y == spot.Y);
        friend.Hand[0] = (int)PieceCard.BuilderShop;
        int n5 = g.Expanded.Count;
        cg.PlayPieceCard(0, spot.Orient, spot.X, spot.Y);
        Run(s, dt, 0.6f, null, noPicks);
        Check(!listed && plainListed && g.Expanded.Count == n5 && friend.Hand[0] == (int)PieceCard.BuilderShop, "a stall card can't be placed on a relic node (not offered, the host refuses, the card stays); a plain piece can");
        g.Nodes.Remove(relic);
        friend.Hand[0] = 0;

        // Sources: a chest gives everyone a grid card; the Builder Shop has five (the Black Market sells no cards).
        var chest = typeof(Game).GetMethod("ChestCardFor", flags);  // (a piece chest offers each player one: Props.cs LootKind.Card)
        Check(g.Players.Values.All(p => Expansion.IsGridPieceCard((PieceCard)chest.Invoke(g, new object[] { p, 3 }))), "a chest offers everyone a grid card");
        Check(g.BuilderCards.Length == 5 && g.BuilderCards.All(Expansion.IsGridPieceCard), "the Builder Shop sells five grid cards");

        // A bot plays a piece from its hand.
        foreach (var p in g.Players.Values) Array.Clear(p.Hand, 0, p.Hand.Length);
        g.Players[1].Hand[0] = (int)Expansion.MakePieceCard(PieceShape.TriL);
        int n3 = g.Expanded.Count;
        bool played = (bool)typeof(Game).GetMethod("BotPlayPieceCard", flags).Invoke(g, null);
        Check(played && g.Expanded.Count > n3 && Game.HandCount(g.Players[1]) == 0, "a bot plays its piece at a door spot");
        // Mystery doors: the room behind each is a piece on the lattice; a small room (a shop or vendor) is one cell.
        var roll = typeof(Game).GetMethod("RollMysteryDoors", flags);
        var plans = (System.Collections.IList)typeof(Game).GetField("mysteryPlans", flags).GetValue(g);
        bool lattice = true, cellSmall = true; int seen = 0;
        for (int w = 0; w < 6; w++)
        {
            g.Wave = 7 + w;
            roll.Invoke(g, null);
            foreach (SurpriseOption pl in plans)
            {
                seen++;
                lattice &= pl.Rooms.All(r => (r.X - Expansion.GridX0) % 600 == 0 && (r.Y - Expansion.GridY0) % 600 == 0 && r.W % 600 == 0 && r.H % 600 == 0) && pl.Price > 0;
                if (pl.Rooms.Count == 1 && pl.Rooms[0].W == pl.Rooms[0].H) cellSmall &= pl.Rooms[0].W == 600;
            }
        }
        Check(seen > 0 && lattice && cellSmall, "mystery door rooms are pieces on the lattice, priced, a small one exactly a cell (" + seen + " plans)");
        var place = typeof(Game).GetMethod("PlaceGridMini", flags);
        int before4 = g.Expanded.Count;
        bool first = (bool)place.Invoke(g, new object[] { 1 }), second = (bool)place.Invoke(g, new object[] { 1 });
        var waiting = g.Expanded.Skip(before4).ToList();
        Check(first && !second && waiting.Count == 1 && waiting[0].Floating && waiting[0].Kind == Arena.RoomKind.Gatekeeper && waiting[0].Mini == (byte)g.MiniBossNow(), "a boss cycle places one mini-boss room on the lattice, holding the coming boss's faction's mini-boss; one waits at a time");
    }

    /// Test helper: `who` plays a piece (put in their last hand slot on the host) at its first place round the map (or the first `where` accepts),
    /// and the run waits until both screens have it. Returns the place, or null if none fits.
    static Placement? PlayPieceCard(Session[] s, float dt, Game host, Game who, PieceShape piece, PathId floor = PathId.None, Func<Placement, bool> where = null)
    {
        var hand = host.Players[who.MyId].Hand;
        hand[hand.Length - 1] = (int)Expansion.MakePieceCard(piece, floor);
        var opts = Expansion.PlacementsFor(host.Expanded, piece).Where(o => where == null || where(o)).ToList();
        if (opts.Count == 0)
            return null;
        int n = host.Expanded.Count;
        who.PlayPieceCard(hand.Length - 1, opts[0].Orient, opts[0].X, opts[0].Y);
        Run(s, dt, 1f, () => host.Expanded.Count > n && s.All(x => x.Game.Expanded.Count == host.Expanded.Count));
        return opts[0];
    }


    /// Test helper: the Grid core and these pieces, each at the first place round the map on the core's east side (else anywhere).
    static List<ExpansionRoom> GridMap(params PieceShape[] pieces)
    {
        var map = Expansion.GridCore();
        foreach (var p in pieces)
        {
            var opts = Expansion.PlacementsFor(map, p);
            var o = opts.FirstOrDefault(q => q.Side == 3);
            if (o.Rooms == null && opts.Count > 0) o = opts[0];
            if (o.Rooms != null) map.AddRange(o.Rooms);
        }
        return map;
    }

    /// Test helper: these games' maps are this one, with the core's inner walls open (as TickGridCore builds it).
    static void UseMap(IEnumerable<Game> games, List<ExpansionRoom> map)
    {
        foreach (var g in games)
        {
            g.Expanded.Clear(); g.Expanded.AddRange(map);
            g.Openings.Clear(); g.Openings.AddRange(Expansion.GridCoreCuts());
            typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, null);
        }
    }


    /// Test helper: a plain box room (no checks: the test's own map), as the drawn rooms of old tests were. A theme makes it a rule room.
    static ExpansionRoom? Box(int x, int y, int w, int h, PathId floor = PathId.None)
        => new ExpansionRoom { X = x, Y = y, W = w, H = h, Kind = floor == PathId.None ? RoomKind.Combat : RoomKind.Floor, Floor = floor };

}
